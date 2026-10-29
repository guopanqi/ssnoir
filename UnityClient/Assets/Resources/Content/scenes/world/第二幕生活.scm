;; 第二幕旧街与剧院生活：只复用骰子、金钱、冷静和 Dialogue
(define 剧院运货人 (speaker "剧院运货人" :name (tr "运货人" "Delivery worker") :appearance '成年男性))

(define 旧街食堂老板 (speaker "旧街食堂老板" :name (tr "老板" "Owner")))

(define 第二幕生活
  (let ()
    (define visited-street? #f)
    (define met-frank? #f)
    (define met-manager? #f)
    (define manager-met-day 0)
    (define contract-state "未提起")
    (define costume-clock (make-clock "找回演出服" 4 'gauge "后台的衣箱和运货记录还没对上。把这批衣服找齐，送回剧院。" :beneficial-direction 'increase))
    (define (contract-open?)
      (and (active?) (<= world-day 13)
        (member? contract-state '("待答复" "已接下"))))
    (define (offer-available?)
      (and (active?) met-manager? (> world-day manager-met-day) (<= world-day 13)
           (equal? contract-state "未提起")))
    (define (offer-lines!)
      (if (offer-available?)
        (begin
          (set! contract-state "待答复")
          (play-dialogue!
            (line "经理" "收货单签了字，演出服却没送进后台。试演那天收工前，我要见到那只衣箱。给你四十五块。")
            (line "经理" "后台旧库房可以找找，码头的运货人也许知道衣箱去了哪儿。")
            (line "经理" "办好了，下回有事我还找你。"))) #f))
    (define (meet-manager!)
      (if met-manager? #f
        (begin
          (set! met-manager? #t)
          (set! manager-met-day world-day)
          (play-dialogue!
            (line "夜莺" "经理，这是尼尔。酒馆里他总来听我唱歌。")
            (line "经理" "尼尔！兄弟！夜莺可跟我提过你。")
            (line "世界" "经理握着尼尔的手，又十分亲热地拍了拍他的肩。")
            (line "经理" "她说你靠得住。现在办事靠得住的人，比钢琴还难找！")
            (line "尼尔" "我想接些真正的调查委托。")
            (line "经理" "妙极了！年轻人就该有点盼头。")
            (line "经理" "我得去招呼他们了。往后常来，尼尔。")))))
    (define (accept-contract!)
      (if (and (contract-open?) (equal? contract-state "待答复")) #t
        (error "演出服：当前不可接下委托"))
      (set! contract-state "已接下")
      (play-dialogue! (line "尼尔" "我去把它找回来。")
        (line "经理" (string-append (第二幕-日期 13) "试演收工前，把衣箱带来。"))))
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
      (node id :name name :anchor anchor :subtitle "核对衣箱去向；坏结果损失1冷静"
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
        (line "世界" "经理打开衣箱，照着清单点过戏服，招呼后台的人把箱子抬走。")
        (line "经理" "一件没少。这是四十五块。下次有事，我知道该找谁。")))
    ;; 主角的职业愿望通过工作里的人际碰撞表现，不单独设置“阅读旧名片”按钮。
    (define (dossier-at)
      (if (not (active?)) '()
        (append
          (if (equal? contract-state "未提起") '()
            (list (dossier "经理：找回演出服" :kind '委托
              :primary (contract-open?) :status (if (contract-open?) '进行中 '了结)
              :now (cond
                ((equal? contract-state "婉拒") "你婉拒了演出服调查，经理会另找人。")
                ((equal? contract-state "待答复")
                  (string-append "经理提出45美元的演出服调查，" (第二幕-日期 13) "试演收工前交付。还没答应，可以接下或婉拒。"))
                ((equal? contract-state "完成") "演出服已交付，收到45美元。经理记住了尼尔做事的能力。")
                ((equal? contract-state "另找他人") "期限到了，经理已另派人找回衣服。这次差事结束了。")
                ((>= (item-count "演出服") 1)
                  (string-append "衣箱已经找到，" (第二幕-日期 13) "收工前交给经理。"))
                (else (string-append (第二幕-日期 13)
                  "收工前带回一批演出服。可以搜找后台，或向运货人查问。")))
              :clocks (if (finding?) (list (costume-clock 'render-data)) '()) :where "剧院")))
          (if met-manager?
            (list (dossier "想做个真正的侦探" :kind '打算 :primary #f
              :now (if (equal? contract-state "完成")
                "第一笔调查报酬已经到手。有一位委托人愿意记住你的名字。"
                "你告诉经理自己想做调查，往后要靠具体的工作建立信誉。") :where "剧院")) '()))))

    (define (active?) (and (= (第一幕 'act) 2) (not (第二章 'started?))))
    (define (street-intro!)
      (set! visited-street? #t)
      (play-dialogue!
        (line "世界" "绕过码头工棚，就是旧街的砖房。食堂门口摆着牌桌，有人在巷口等零工。")
        (line "世界" "收工的人往这里走。有人端着汤找座位，有人还在打听明天的活。")))
    (define (frank-intro!)
      (set! met-frank? #t)
      (play-dialogue!
        (line "世界" "食堂老板把一张欠账单推到老工人的碗边。弗兰克伸手接了过去。")
        (line 旧街食堂老板 "上礼拜的还没结。弗兰克，你不能天天替人垫。")
        (line "弗兰克" "这顿先算我的。让他吃完。")
        (line "世界" "他把钱压在单子下面，转身给尼尔让出半张长凳。")
        (line "弗兰克" "坐吧。桶里还有热豆汤。")
        (line "尼尔" "我叫尼尔。")
        (line "弗兰克" "弗兰克。干了一天，吃饭还站着？")))
    (define (meal!)
      (restore-actor-composure! 'player 2)
      (play-bubble! (line 旧街食堂老板 "豆汤在桶里，面包自己拿。吃完把盘子放木盆里。" :at "码头居民区")))
    (define (cards! outcome)
      (cond
        ((eq? outcome '赢) (add-item! "金钱" 12)
          (play-bubble! (line "牌客" "手气够硬。见好就收，留点钱吃饭。" :at "码头居民区")))
        ((eq? outcome '平) (add-item! "金钱" 5)
          (play-bubble! (line "牌客" "不输不赢，白替你洗了半天牌。" :at "码头居民区")))
        (else (spend-composure! 1)
          (play-bubble! (line "牌客" "五块没了。兜里还有吗？还来不来？" :at "码头居民区")))))
    (define (nodes-at location)
      (if (not (active?)) '()
        (cond
          ((equal? location "码头居民区")
            (append
              (list
                (node "旧街热饭" :name "吃顿热饭"
                  :subtitle "花3美元和一枚骰子，恢复2点冷静"
                  :requires (list (req-die) (req-item "金钱" 3))
                  :resolve (instant (outcome meal!)))
                (node "旧街牌桌" :name "打几手牌"
                  :subtitle "押5美元；输牌会损失冷静"
                  :risk '冒险 :risk-target (list 'composure "player")
                  :progress-target (list 'item "金钱")
                  :requires (list (req-die) (req-item "金钱" 5))
                  :resolve (roll 'sharpness
                    (outcome (lambda () (cards! '输)))
                    (outcome (lambda () (cards! '平)))
                    (outcome (lambda () (cards! '赢))))))))
          ((equal? location "剧院")
            (append
              (if (offer-available?)
                (list (node "经理有话想说" :anchor "剧院" :subtitle "经理招呼你过去，似乎有件事想谈"
                  :resolve (instant (outcome offer-lines!)))) '())
              (if (and (contract-open?) (equal? contract-state "待答复"))
                (list (instant-action "接下演出服调查" accept-contract!)
                      (instant-action "婉拒演出服调查" decline-contract!)) '())
              (if (and (contract-open?) (equal? contract-state "已接下"))
                (list (node "交付演出服" :subtitle "交出一批演出服，领取45美元"
                  :requires (list (req-item "演出服" 1)) :resolve (instant (outcome deliver-contract!)))) '())
              (if (finding?) (list
                (clock-node "演出服搜寻进展" (costume-clock 'render-data))
                (costume-action "旧库房找衣箱" "翻找旧库房" 'sharpness "剧院-后台")
                (node "雇人代找衣箱" :name "请人代找"
                  :subtitle (string-append "花" (number->string (代找费用)) "美元补齐剩余线索")
                  :requires (list (req-item "金钱" (代找费用)))
                  :resolve (instant (outcome 请人代找!)))) '())))
          ((equal? location "码头")
            (if (finding?) (list
              (costume-action "问运货人衣箱" "向运货人查问" 'social "码头-账房")) '()))
          (else '()))))
    ;; 初次入场介绍只发生一次，不占用一张“看看”动作。
    (define (arrivals-at location)
      (if (equal? location "码头居民区")
        (append
          (if (not visited-street?) (list (arrival "初到旧街" street-intro!)) '())
          (if (and visited-street? (not met-frank?)) (list (arrival "食堂账单" frank-intro!)) '())) '()))
    (define-turn-start-rule "经理另外找人"
      (lambda () (and (> world-day 13) (member? contract-state '("待答复" "已接下"))))
      (lambda ()
        (set! contract-state "另找他人")
        (notify! "演出服委托已到期，剧院经理另找了人。")))
    (lambda args
      (let ((msg (car args)))
        (cond
          ((eq? msg 'meet-manager!) (meet-manager!))
          ((eq? msg 'dossier) (dossier-at))
          ((eq? msg 'nodes-at) (nodes-at (cadr args)))
          ((eq? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((eq? msg 'met-frank?) met-frank?)
          ((eq? msg 'manager-met?) met-manager?)
          ((eq? msg 'save)
            (list (list "street" visited-street?)
                  (list "frank" met-frank?)
                  (list "manager" met-manager?) (list "manager-met-day" manager-met-day)
                  (list "version" 2) (list "contract" contract-state)
                  (list "progress" (costume-clock 'save))))
          ((eq? msg 'load!)
            (let ((data (cadr args)))
              (set! visited-street? (assoc-get data "street" #f))
              (set! met-frank? (assoc-get data "frank" #f))
              (set! met-manager? (assoc-get data "manager" #f))
              (set! manager-met-day (assoc-get data "manager-met-day" (if met-manager? 8 0)))
              (set! contract-state (assoc-get data "contract" "未提起"))
              ;; 已有衣箱只恢复进展，不再次发物品；已领报酬保持完成。
              (costume-clock 'load! (assoc-get data "progress"
                (if (or (equal? contract-state "完成") (> (item-count "演出服") 0)) 4 0)))
              (if (equal? contract-state "退回") (set! contract-state "另找他人") #f)
              (if (and (>= manager-met-day 0)
                       (or (not met-manager?) (> manager-met-day 0))
                       (member? contract-state '("未提起" "待答复" "已接下" "完成" "另找他人" "婉拒"))) #t
                (error "经理委托：非法存档阶段"))))
          (else (error "第二幕生活：未知消息")))))))
