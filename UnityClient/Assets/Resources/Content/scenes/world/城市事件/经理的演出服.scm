;; 城市事件：经理的演出服。全部地点节点、进展、期限与结算归本文件。
(define 剧院运货人 (speaker "剧院运货人" :name "运货人" :appearance '成年男性))
(define 经理的演出服
  (let ()
    (define contract-state "未提起")
    (define 出现日 0)
    (define record-clock (make-clock "核对收货记录" 2 'gauge "后台签收了衣箱，但库房里没有。这笔签收需要查清。" :beneficial-direction 'increase))
    (define transport-clock (make-clock "查清运输去向" 2 'gauge "核对送货人与实际交接地点，查明衣箱停在哪里。" :beneficial-direction 'increase))
    (define (clue-clocks) (list (record-clock 'render-data) (transport-clock 'render-data)))
    (define (offer!)
      (if (and (active?) (<= world-day 13) (equal? contract-state "未提起"))
        (begin (set! contract-state "待介绍") (set! 出现日 world-day)) #f))
    (define (completed?) (equal? contract-state "完成"))
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
            (line "经理" "把单子和实际去向查清，别只给我搬回一箱衣服。办好了，我会把你介绍给需要调查的人。"))) #f))
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
    (define (clue-progress! clock amount)
      (if (finding?) #t (error "演出服：当前不可调查"))
      (let ((was-full? (clock 'full?)))
        (clock 'advance! amount)
        (if (and (not was-full?) (clock 'full?))
          (play-dialogue!
            (line "世界" (if (eq? clock record-clock)
              "后台的签收人承认：他照着清单签了字，并没有点过衣箱。单上另记着岸边的临时存货棚。"
              "运货人核对了送货底单：箱子送到岸边存货棚后，就没有人再来提。"))) #f))
      (if (and (record-clock 'full?) (transport-clock 'full?))
        (begin
          (add-item! "演出服" 1)
          (play-dialogue!
            (line "世界" "两张单据上的棚号对上了。尼尔照着号码取回衣箱，打开核对，剧院的标记和尺码都对。"))) #f))
    (define (costume-action id name skill anchor clock)
      (node id :name name :anchor anchor :subtitle "查明交接事实；坏结果损失1心情"
        :risk '冒险 :risk-target (list 'composure "player")
        :progress-target (list 'clock (if (eq? clock record-clock) "核对收货记录" "查清运输去向"))
        :requires (list (req-die))
        :resolve (roll skill
          (outcome (lambda () (spend-composure! 1)))
          (outcome (lambda () (clue-progress! clock 1)))
          (outcome (lambda () (clue-progress! clock 2))))))
    (define (deliver-contract!)
      (if (and (contract-open?) (equal? contract-state "已接下") (>= (item-count "演出服") 1))
        #t (error "演出服委托：需要实际交付演出服"))
      ;; 演出服由交付节点的资源槽扣除，不在效果中重复扣除。
      (set! contract-state "完成")
      (set-global! '身份 "侦探")
      (set-global! '侦探入行日 world-day)
      (add-item! "金钱" 45)
      (play-dialogue!
        (line "世界" "经理带着运货人来到尼尔的住处。他打开衣箱，照着清单点过戏服，让运货人把箱子抬走。")
        (line "经理" "原来卡在交接上。这是四十五块。有人问起私人调查，我会把你的名字告诉他。")))
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
                ((equal? contract-state "完成") "演出服已交付，收到45美元。经理愿意介绍客户，你开始以无名侦探的身份承接调查。")
                ((equal? contract-state "另找他人") "期限到了，经理已另派人找回衣服。这次差事结束了。")
                ((>= (item-count "演出服") 1)
                  (string-append "衣箱已经找到，" (第二幕-日期 13) "收工前带回家交给经理。"))
                (else (string-append (第二幕-日期 13)
                  "收工前带回一批演出服。可以搜找后台，或向运货人查问。")))
              :clocks (append (if (or (offer-available?) (contract-open?)) (期限) '()) (if (finding?) (clue-clocks) '())) :where "家")))
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
                (list (node "城市事件-经理" :name "经理：丢失的演出服" :notice-version contract-state :display 'inline :anchor "家"
                  :clocks (期限) :children children)))))
          ((equal? location "剧院")
            (if (and (finding?) (not (record-clock 'full?)))
              (list (node "演出服-后台搜寻组" :name "核对签收" :display 'inline :anchor "剧院-后台"
                :clocks (cons (record-clock 'render-data) (期限))
                :children (list (costume-action "旧库房找衣箱" "核对收货单" 'sharpness "剧院-后台" record-clock)))) '()))
          ((equal? location "码头")
            (if (and (finding?) (not (transport-clock 'full?)))
              (list (node "演出服-码头搜寻组" :name "追查衣箱" :display 'inline :anchor "码头-账房"
                :clocks (cons (transport-clock 'render-data) (期限))
                :children (append
                  (list (costume-action "问运货人衣箱" "询问运货人" 'social "码头-账房" transport-clock)
                    (node "雇人代找衣箱" :name "请人核查" :anchor "码头-账房"
                      :subtitle "付15美元，请运货人查清运输去向"
                      :progress-target (list 'clock "查清运输去向")
                      :requires (list (req-item "金钱" 15))
                      :resolve (instant (outcome (lambda () (clue-progress! transport-clock 2))))))
                  (if (>= (第一幕 'dock-work-count) 3)
                    (list (node "向熟识搬运工核实" :name "找搬运熟人" :anchor "码头-货堆"
                      :subtitle "过去一起干过活的人能帮你查清去向"
                      :progress-target (list 'clock "查清运输去向")
                      :requires (list (req-die))
                      :resolve (instant (outcome (lambda () (clue-progress! transport-clock 2)))))) '())))) '()))
          (else '()))))
    (lambda (msg . args)
      (cond
        ((eq? msg 'offer!) (offer!))
        ((eq? msg 'completed?) (completed?))
        ((eq? msg 'career-next)
          (if (or (> world-day 13) (member? contract-state '("婉拒" "另找他人")))
            "没能接住演出服这次机会。继续寻找愿意付钱的客户，经理的正式调查仍可能到来。"
            "经理给了你一次调查机会。查清演出服去向，交出第一份成果。"))
        ((eq? msg 'eligible?) (and (active?) (<= world-day 13) (equal? contract-state "未提起")))
        ((eq? msg 'activate!)
          (if (equal? contract-state "未提起") (begin (set! contract-state "待介绍") (set! 出现日 world-day)) (error "演出服：重复出现")))
        ((eq? msg 'on-day-start!)
          (if (and (> world-day 13) (member? contract-state '("待介绍" "待答复" "已接下")))
            (set! contract-state "另找他人") #f))
        ((eq? msg 'nodes-at) (nodes-at (car args)))
        ((eq? msg 'dossier) (dossier-at))
        ((eq? msg 'save) (list (list "stage" contract-state) (list "appeared" 出现日) (list "records" (record-clock 'save)) (list "transport" (transport-clock 'save))))
        ((eq? msg 'load!)
          (let ((data (car args)))
            (set! contract-state (assoc-get data "stage" "未提起"))
            (set! 出现日 (assoc-get data "appeared" world-day))
            (if (member? contract-state '("未提起" "待介绍" "待答复" "已接下" "完成" "婉拒" "另找他人")) #t (error "演出服：非法阶段"))
            ;; 旧版单根四格进度按先记录、后运输迁移，不重复发物品或报酬。
            (record-clock 'load! (assoc-get data "records" (min 2 (assoc-get data "progress" 0))))
            (transport-clock 'load! (assoc-get data "transport" (max 0 (- (assoc-get data "progress" 0) 2))))))
        (else (error "演出服：未知消息"))))))
