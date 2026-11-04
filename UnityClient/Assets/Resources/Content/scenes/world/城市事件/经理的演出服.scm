;; 城市事件：经理的演出服。全部地点节点、进展、期限与结算归本文件。
(define 剧院运货人 (speaker "剧院运货人" :name "运货人" :appearance '成年男性))
(define 经理的演出服
  (let ()
    (define contract-state "未提起")
    (define 出现日 0)
    (define costume-clock (make-clock "找回演出服" 4 'gauge "后台的衣箱和运货记录还没对上。把这批衣服找齐，带回家交付。" :beneficial-direction 'increase))
    (define (contract-open?)
      (and (active?) (<= world-day 13)
        (member? contract-state '("待答复" "已接下"))))
    (define (offer-available?)
      (and (active?) (<= world-day 13) (equal? contract-state "待介绍")))
    (define (期限) (list (clock-priority (list 'clock "演出服交付期限" (max 0 (- 14 world-day)) (max 1 (- 14 出现日)) 'countdown "试演当天收工前交付；到期经理另找他人。") 'secondary)))
    (define (offer-lines!)
      (if (offer-available?)
        (begin
          (set! contract-state "待答复")
          (play-dialogue!
            (line "经理" "收货单签了字，演出服却没送进后台。试演那天收工前，把衣箱带回住处，我派人来取。给你四十五块。")
            (line "经理" "后台旧库房可以找找，码头的运货人也许知道衣箱去了哪儿。")
            (line "经理" "办好了，下回有事我还找你。"))) #f))
    (define (accept-contract!)
      (if (and (contract-open?) (equal? contract-state "待答复")) #t
        (error "演出服：当前不可接下委托"))
      (set! contract-state "已接下")
      (play-dialogue! (line "尼尔" "我去把它找回来。")
        (line "经理" (string-append (第二幕-日期 13) "试演收工前，把衣箱带回住处，我派人来取。"))))
    (define (decline-contract!)
      (if (and (contract-open?) (equal? contract-state "待答复")) #t
        (error "演出服：当前不可婉拒委托"))
      (set! contract-state "婉拒")
      (play-dialogue!
        (line "尼尔" "这次恐怕腾不出手。你另找个人吧。")
        (line "经理" "行，兄弟。以后有机会再合作。")))
    (define (finding?)
      (and (contract-open?) (equal? contract-state "已接下") (= (item-count "演出服") 0)))
    (define (costume-progress! amount anchor)
      (if (finding?) #t (error "演出服：当前不可搜找"))
      (costume-clock 'advance! amount)
      (if (costume-clock 'full?)
        (begin
          (add-item! "演出服" 1)
          (play-bubble! (line "尼尔" "剧院的名字，尺码也对。就是这箱。" :at anchor))) #f))
    (define (代找费用) (+ 5 (* 5 (- 4 (costume-clock 'current)))))
    (define (请人代找!)
      (if (finding?) #t (error "演出服：尚未接受委托"))
      ;; 金钱由 req-item 支付；成本随此前查到的进展下降。
      (costume-progress! (- 4 (costume-clock 'current)) "剧院-后台")
      (play-dialogue!
        (line 剧院运货人 "顺着单子查到了。衣箱送到后台了，您进去点点数。")))
    (define (costume-action id name skill anchor)
      (node id :name name :anchor anchor :subtitle "核对衣箱去向；坏结果损失1心情"
        :risk '冒险 :risk-target (list 'composure "player")
        :progress-target (list 'clock "找回演出服") :requires (list (req-die))
        :resolve (roll skill
          (outcome (lambda () (spend-composure! 1) (costume-progress! 0 anchor)))
          (outcome (lambda () (costume-progress! 1 anchor)))
          (outcome (lambda () (costume-progress! 2 anchor))))))
    (define (deliver-contract!)
      (if (and (contract-open?) (equal? contract-state "已接下") (>= (item-count "演出服") 1))
        #t (error "演出服委托：需要实际交付演出服"))
      ;; 演出服由交付节点的资源槽扣除，不在效果中重复扣除。
      (set! contract-state "完成")
      (add-item! "金钱" 45)
      (play-dialogue!
        (line "世界" "经理带着运货人来到尼尔的住处。他打开衣箱，照着清单点过戏服，让运货人把箱子抬走。")
        (line "经理" "一件没少。这是四十五块。下次有事，我知道该找谁。")))
    ;; 主角的职业愿望通过工作里的人际碰撞表现，不单独设置“阅读旧名片”按钮。
    (define (dossier-at)
      (if (not (active?)) '()
        (append
          (if (equal? contract-state "未提起") '()
            (list (dossier "经理：找回演出服" :kind '委托
              :primary (or (offer-available?) (contract-open?)) :status (if (or (offer-available?) (contract-open?)) '进行中 '了结)
              :now (cond
                ((equal? contract-state "待介绍") "经理上门，有一件演出服的差事想谈，试演收工前有效。")
                ((equal? contract-state "婉拒") "你婉拒了演出服调查，经理会另找人。")
                ((and (> world-day 13) (member? contract-state '("待答复" "已接下")))
                  "试演已经结束，这项委托过期了。")
                ((equal? contract-state "待答复")
                  (string-append "经理提出45美元的演出服调查，" (第二幕-日期 13) "试演收工前交付。还没答应，可以接下或婉拒。"))
                ((equal? contract-state "完成") "演出服已交付，收到45美元。经理记住了尼尔做事的能力。")
                ((equal? contract-state "另找他人") "期限到了，经理已另派人找回衣服。这次差事结束了。")
                ((>= (item-count "演出服") 1)
                  (string-append "衣箱已经找到，" (第二幕-日期 13) "收工前带回家交给经理。"))
                (else (string-append (第二幕-日期 13)
                  "收工前带回一批演出服。可以搜找后台，或向运货人查问。")))
              :clocks (append (if (or (offer-available?) (contract-open?)) (期限) '()) (if (finding?) (list (costume-clock 'render-data)) '())) :where "家")))
          )))
    (define (active?) (and (= (第一幕 'act) 2) (not (第二章 'started?))))
    (define (nodes-at location)
      (if (not (active?)) '()
        (cond
          ((equal? location "家")
            (let ((children
              (append
                (if (offer-available?)
                  (list (node "经理有话想说" :name "听取委托" :button-text "交谈"
                    :subtitle "经理上门，似乎有件事想谈"
                    :resolve (instant (outcome offer-lines!)))) '())
                (if (and (contract-open?) (equal? contract-state "待答复"))
                  (list (node "接下演出服调查" :name "接受委托" :button-text "接受" :resolve (instant (outcome accept-contract!)))
                        (node "婉拒演出服调查" :name "拒绝委托" :button-text "拒绝" :resolve (instant (outcome decline-contract!)))) '())
                (if (and (contract-open?) (equal? contract-state "已接下"))
                  (list (node "交付演出服" :button-text "交付" :subtitle "交出一批演出服，领取45美元"
                    :requires (list (req-item "演出服" 1)) :resolve (instant (outcome deliver-contract!)))) '()))))
              (if (null? children) '()
                (list (node "城市事件-经理" :name "经理：丢失的演出服" :display 'inline :anchor "家"
                  :clocks (期限) :children children)))))
          ((equal? location "剧院")
            (if (finding?)
              (list (node "演出服-后台搜寻组" :name "找回演出服" :display 'inline :anchor "剧院-后台"
                :clocks (cons (costume-clock 'render-data) (期限))
                :children (list
                  (costume-action "旧库房找衣箱" "翻找旧库房" 'sharpness "剧院-后台")
                  (node "雇人代找衣箱" :anchor "剧院-后台" :name "请人代找" :progress-target (list 'clock "找回演出服")
                    :subtitle (string-append "花" (number->string (代找费用)) "美元补齐剩余线索")
                    :requires (list (req-item "金钱" (代找费用)))
                    :resolve (instant (outcome 请人代找!)))))) '()))
          ((equal? location "码头")
            (if (finding?)
              (list (node "演出服-码头搜寻组" :name "找回演出服" :display 'inline :anchor "码头-账房"
                :clocks (cons (costume-clock 'render-data) (期限))
                :children (list (costume-action "问运货人衣箱" "向运货人查问" 'social "码头-账房")))) '()))
          (else '()))))
    (lambda (msg . args)
      (cond
        ((eq? msg 'eligible?) (and (active?) (<= world-day 13) (equal? contract-state "未提起")))
        ((eq? msg 'activate!)
          (if (equal? contract-state "未提起") (begin (set! contract-state "待介绍") (set! 出现日 world-day)) (error "演出服：重复出现")))
        ((eq? msg 'on-day-start!)
          (if (and (> world-day 13) (member? contract-state '("待介绍" "待答复" "已接下")))
            (set! contract-state "另找他人") #f))
        ((eq? msg 'nodes-at) (nodes-at (car args)))
        ((eq? msg 'dossier) (dossier-at))
        ((eq? msg 'save) (list (list "stage" contract-state) (list "appeared" 出现日) (list "progress" (costume-clock 'save))))
        ((eq? msg 'load!)
          (let ((data (car args)))
            (set! contract-state (assoc-get data "stage" "未提起"))
            (set! 出现日 (assoc-get data "appeared" world-day))
            (if (member? contract-state '("未提起" "待介绍" "待答复" "已接下" "完成" "婉拒" "另找他人")) #t (error "演出服：非法阶段"))
            (costume-clock 'load! (assoc-get data "progress" 0))))
        (else (error "演出服：未知消息"))))))
