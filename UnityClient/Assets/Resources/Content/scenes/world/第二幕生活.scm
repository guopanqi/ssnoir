;; 第二幕旧街与剧院生活：只复用骰子、金钱、冷静和 Dialogue

(define 旧街食堂老板 (speaker "旧街食堂老板" :name (tr "老板" "Owner")))

(define 第二幕生活
  (let ()
    (define visited-street? #f)
    (define met-manager? #f)
    (define manager-met-day 0)
    (define (meet-manager!)
      (if met-manager? #f
        (begin
          (set! met-manager? #t)
          (set! manager-met-day world-day)

          (play-dialogue!
    (line "世界" "后台走廊尽头的办公室门半掩着。夜莺带你走进去时，一个穿着考究三件套、头发梳得一丝不苟的中年男人正放下手里的账本。")
    (line "夜莺" "经理先生，这位就是我提过的尼尔，之前在老街帮过我。")
    (line "经理" "（微笑着起身，朝你伸出手）幸会，尼尔先生。老街酒馆的酒保也跟我提过你的名字，说你是个办事稳妥的人。")))))
    (define (active?) (and (= (第一幕 'act) 2) (not (第二章 'started?))))
    (define (street-intro!)
      (set! visited-street? #t)
      (play-dialogue!
        (line "世界" "绕过码头工棚，就是旧街的砖房。食堂门口摆着牌桌，有人在巷口等零工。")
        (line "世界" "收工的人往这里走。有人端着汤找座位，有人还在打听明天的活。")))
    (define (frank-intro!)
      (frank 'meet!)
      (居民区相识 'on-met!)
      (play-dialogue!
        (line "世界" "食堂老板把一张欠账单推到老工人的碗边。弗兰克伸手接了过去。")
        (line 旧街食堂老板 "上礼拜的还没结。弗兰克，你不能天天替人垫。")
        (line "弗兰克" "这顿先算我的。让他吃完。")
        (line "世界" "他把钱压在单子下面，转身给尼尔让出半张长凳。")
        (line "弗兰克" "坐吧。桶里还有热豆汤。")
        (line "尼尔" "我叫尼尔。")
        (line "弗兰克" "弗兰克。干了一天，吃饭还站着？")
        (line "弗兰克" "我们下工都在这里。有空过来坐坐。")))
    (define (nodes-at location) '())
    ;; 初次入场介绍只发生一次，不占用一张“看看”动作。
    (define (arrivals-at location)
      (if (equal? location "码头居民区")
        (append
          (if (not visited-street?) (list (arrival "初到旧街" street-intro!)) '())
          (if (and (人物线开放? "弗兰克") (>= world-day 10) visited-street? (not (frank 'met?))) (list (arrival "食堂账单" frank-intro!)) '())) '()))
    (lambda args
      (let ((msg (car args)))
        (cond
          ((eq? msg 'remember-manager!) (set! met-manager? #t) (set! manager-met-day world-day))
          ((eq? msg 'meet-manager!) (meet-manager!))
          ((eq? msg 'nodes-at) (nodes-at (cadr args)))
          ((eq? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((eq? msg 'manager-met?) met-manager?)
          ((eq? msg 'save)
            (list (list "street" visited-street?)

                  (list "manager" met-manager?) (list "manager-met-day" manager-met-day)
                  (list "version" 3)))
          ((eq? msg 'load!)
            (let ((data (cadr args)))
              (set! visited-street? (assoc-get data "street" #f))
              (set! met-manager? (assoc-get data "manager" #f))
              (set! manager-met-day (assoc-get data "manager-met-day" 0))))
          (else (error "第二幕生活：未知消息")))))))
