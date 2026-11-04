;; 第二幕旧街与剧院生活：只复用骰子、金钱、冷静和 Dialogue

(define 旧街食堂老板 (speaker "旧街食堂老板" :name (tr "老板" "Owner")))

(define 第二幕生活
  (let ()
    (define visited-street? #f)
    (define met-frank? #f)
    (define met-manager? #f)
    (define manager-met-day 0)
    (define (meet-manager!)
      (if met-manager? #f
        (begin
          (set! met-manager? #t)
          (set! manager-met-day world-day)
          (set-global! '身份 "侦探")
          (set-global! '侦探入行日 world-day)
          (play-dialogue!
            (line "夜莺" "经理，这是尼尔。酒馆里他总来听我唱歌。")
            (line "经理" "尼尔！兄弟！夜莺可跟我提过你。")
            (line "世界" "经理握着尼尔的手，又十分亲热地拍了拍他的肩。")
            (line "经理" "她说你靠得住。现在办事靠得住的人，比钢琴还难找！")
            (line "尼尔" "我想接些真正的调查委托。")
            (line "经理" "妙极了！年轻人就该有点盼头。")
            (line "经理" "我得去招呼他们了。往后常来，尼尔。")))))
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
                  :subtitle "花3美元和一枚骰子，恢复2点心情"
                  :requires (list (req-die) (req-item "金钱" 3))
                  :resolve (instant (outcome meal!)))
                (node "旧街牌桌" :name "打几手牌"
                  :subtitle "押5美元；输牌会损失心情"
                  :risk '冒险 :risk-target (list 'composure "player")
                  :progress-target (list 'item "金钱")
                  :requires (list (req-die) (req-item "金钱" 5))
                  :resolve (roll 'sharpness
                    (outcome (lambda () (cards! '输)))
                    (outcome (lambda () (cards! '平)))
                    (outcome (lambda () (cards! '赢))))))))
          (else '()))))
    ;; 初次入场介绍只发生一次，不占用一张“看看”动作。
    (define (arrivals-at location)
      (if (equal? location "码头居民区")
        (append
          (if (not visited-street?) (list (arrival "初到旧街" street-intro!)) '())
          (if (and (人物线开放? "弗兰克") visited-street? (not met-frank?)) (list (arrival "食堂账单" frank-intro!)) '())) '()))
    (lambda args
      (let ((msg (car args)))
        (cond
          ((eq? msg 'meet-manager!) (meet-manager!))
          ((eq? msg 'dossier) (if met-manager? (list (dossier "身份：侦探" :kind '打算 :primary #f :now "你开始承接城市里的调查委托。" :where "剧院")) '()))
          ((eq? msg 'nodes-at) (nodes-at (cadr args)))
          ((eq? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((eq? msg 'met-frank?) met-frank?)
          ((eq? msg 'manager-met?) met-manager?)
          ((eq? msg 'save)
            (list (list "street" visited-street?)
                  (list "frank" met-frank?)
                  (list "manager" met-manager?) (list "manager-met-day" manager-met-day)
                  (list "version" 3)))
          ((eq? msg 'load!)
            (let ((data (cadr args)))
              (set! visited-street? (assoc-get data "street" #f))
              (set! met-frank? (assoc-get data "frank" #f))
              (set! met-manager? (assoc-get data "manager" #f))
              (set! manager-met-day (assoc-get data "manager-met-day" 0))))
          (else (error "第二幕生活：未知消息")))))))
