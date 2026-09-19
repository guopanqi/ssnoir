;; scenes/world/第二章/晚宴.scm - 第二章锚点一《晚宴》的城市入口与结算。
;; 场内玩法在 encounters/晚宴.scm；这里只管日期、阻塞、入场和写回城市。

(define 晚宴
  (let ()
    (define 结果 "未开始") ; 未开始 / 进行中 / 已结束

    (define (开始了?) (第二章 'started?))
    (define (今天?) (and (开始了?) (= world-day (第二章 'banquet-day))))
    (define (还没结?) (not (equal? 结果 "已结束")))
    (define (剩几天) (max 0 (- (第二章 'banquet-day) world-day)))

    (define (sync-blockers!)
      (rest-release! "第二章/晚宴")
      (cond
        ((and (今天?) (equal? 结果 "未开始"))
         (rest-block! "第二章/晚宴" "今晚的晚宴，她在酒店等你" "格兰德酒店" "赴晚宴"))
        ((equal? 结果 "进行中")
         (rest-block! "第二章/晚宴" "你还站在那间厅里"))
        (else #f)))

    (define (记结果! results)
      (if (not (list? results)) (error "晚宴：交锋结果必须是列表") #t)
      (set-global! '晚宴-薇拉 (member? '薇拉 results))
      (set-global! '晚宴-沃尔特 (member? '沃尔特 results))
      ;; 沃尔特：吧台上陪他喝到底，他才记得你。过两天他在酒店大堂找你（见 人物/沃尔特.scm）。
      (if (member? '沃尔特 results) (walter 'on-banquet-talk!) #f)
      (set-global! '晚宴-贝恩斯 (member? '贝恩斯 results))
      (set-global! '晚宴-记者 (member? '记者 results))
      ;; 林不占玩家的竞争席位：夜莺在第一幕自然把他带进了后续主线。
      (lin 'on-banquet-talk!)
      (nightingale 'on-drifted-apart!)
      (set! 结果 "已结束")
      (sync-blockers!)
      (第二章 'log! "格兰德酒店晚宴"
        (cond
          ((null? results) "晚宴散场时，你记住了许多名字，没有一个真正属于你。")
          ((= (length results) 1) "晚宴散场时，至少有一个人答应明天接你的电话。")
          (else "晚宴散场时，你手里多了几张名片，也多了几件以后要还的人情。")))
      (complete-task! "格兰德酒店晚宴")
      (spotlight! "晚宴散场"
        "最后一支舞停了。夜莺还在厅那头说话；你带走的是自己真正谈成的那些关系。"))

    (define (now)
      (cond
        ((equal? 结果 "进行中") "晚宴还没有散场")
        ((今天?) "今晚的晚宴。她在格兰德酒店等你")
        ((还没结?) (string-append "还有 " (number->string (剩几天)) " 天，晚宴那晚陪她去"))
        (else "晚宴散场了。她已经开始适应那个地方")))

    (define (where) (if (还没结?) "格兰德酒店" ""))
    (define (clocks)
      (if (and (还没结?) (not (今天?)))
          (list (日期倒计时 "离晚宴" (第二章 'banquet-day) 3
                  "归零那晚去格兰德酒店，只有那一晚。"))
          '()))

    (define (node-go)
      (at-anchor "格兰德酒店"
       (encounter-action "赴晚宴"
        (lambda ()
          (if (equal? 结果 "未开始") #t (error "晚宴：只能进场一次"))
          (set! 结果 "进行中")
          (sync-blockers!)
          (play-dialogue!
            (line "夜莺" "别站那么直，他们又不查你的票。")
            (line "尼尔" "你紧张。")
            (line "夜莺" "我等了六年才有人请我来这种地方。"))
          (start-encounter "晚宴" 记结果!)))))

    (define (nodes-at location)
      (if (and (equal? location "格兰德酒店") (今天?) (equal? 结果 "未开始"))
          (list (node-go))
          '()))

    (define (on-day-end!)
      (if (开始了?)
          (begin
            (sync-blockers!)
            (if (今天?)
                (spotlight! "今晚" "晚宴在格兰德酒店。她说过只等你一次。")
                #f))
          #f))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) '())
          ((equal? msg 'dossier) '())
          ((equal? msg 'done?) (not (还没结?)))
          ((equal? msg 'steps) (list (step "陪她赴晚宴" (not (还没结?)))))
          ((equal? msg 'on-day-end!) (on-day-end!))
          ((equal? msg 'sync-blockers!) (sync-blockers!))
          ((equal? msg 'debug-settle!) (set! 结果 "已结束") (sync-blockers!))
          ((equal? msg 'now) (now))
          ((equal? msg 'where) (where))
          ((equal? msg 'clocks) (clocks))
          ((equal? msg 'result) 结果)
          ((equal? msg 'save) (list (list "result" 结果)))
          ((equal? msg 'load!) (set! 结果 (assoc-get (cadr args) "result" "未开始")))
          (else (error "晚宴：收到未知消息")))))))
