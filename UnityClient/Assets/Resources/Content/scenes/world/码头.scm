;; 码头——第一章新版城市生计。旧《三封信》的邮箱与诱捕内容已经退役。
;; 玩家每天投骰前知道搭档；救场改实际结算，但不修改判定的概率。
(define dock
  (let ()
    (define dock-partner "艾迪")
    (define help-day 0)
    (define joe-met? #f)
    (define buddy-talk-day 0)
    (define (today-partner)
      ;; 不能让玩家第一次来码头时恰好错过艾迪，直到初识后才排其他班。
      (if (< (第一幕 'eddie-stage) 3) "艾迪" dock-partner))
    (define (choose-partner!)
      (set! dock-partner
        (if (< (第一幕 'eddie-stage) 3) "艾迪"
          (random-choice (list "艾迪" "艾迪" "乔" "乔" "陌生人")))))
    (define-turn-start-rule "码头每日搭班"
      (lambda () #t)
      (lambda () (choose-partner!)))

    (define (haul-subtitle)
      (cond
        ((equal? (today-partner) "艾迪")
          (if (= help-day world-day)
            "艾迪搭班，今日已替你挡过一次麻烦"
            "艾迪搭班，首次失手少损失1冷静"))
        ((equal? (today-partner) "乔") "乔搭班，失手时可帮你避免损失")
        (else "独自搬运，失手损失2冷静")))

    (define (on-haul! grade)
      (let* ((partner (today-partner))
             (eddie-first? (and (equal? partner "艾迪")
                                (not (第一幕 'eddie-met?))))
             (rescued? (and (eq? grade '坏) (equal? partner "艾迪")
                            (not (= help-day world-day)))))
        ;; 一次坏判定的工资仍为零。冷静损失必须通过引擎效果条真实出现。
        (cond
          ((eq? grade '好) (add-item! "金钱" 20))
          ((eq? grade '中) (add-item! "金钱" 10) (spend-composure! 1))
          ((equal? partner "乔") #f)
          (rescued?
            (set! help-day world-day)
            (spend-composure! 2)
            (play-bubble!
              (line "尼尔" "糟了，箱子压下来了！" :at "码头-货堆")
              (line "艾迪" "松手！这头我来接！" :at "码头-货堆"))
            (play-bubble!
              (line "艾迪" "手扭着没有？歇一会儿，别硬撑。" :at "码头-货堆")
              (line "尼尔" "没事。刚才多亏你了。" :at "码头-货堆"))
            (restore-actor-composure! 'player 1))
          (else (spend-composure! 2)))
        (if (and (equal? partner "乔") (not joe-met?))
          (begin (set! joe-met? #t)
            (play-bubble!
              (line "乔" "我叫乔。绳子别系在湿的地方。" :at "码头-岸口")
              (line "尼尔" "记住了。"))) #f)
        ;; 救场和关心已作为坏结果的两段 Bubble 演出，不再重复。
        ;; 艾迪的初识/后续邀请，属于第一幕人物生活，不由工资模块另造一套阶段。
        (第一幕 'on-work! "码头" grade)
        (if (and (equal? partner "艾迪") (not eddie-first?)
                 (not rescued?) (not (= buddy-talk-day world-day)))
          (begin
            (set! buddy-talk-day world-day)
            (play-bubble!
              (line "艾迪" "干得不错。手酸了就歇一会儿，别硬撑。" :at "码头-货堆")
              (line "尼尔" "你倒像这里的工头。" :at "码头-货堆")
              (line "艾迪" "工头可不管你明天还能不能抬箱子。" :at "码头-货堆"))) #f)))

    (define (haul-node)
      (工作 "搬运" '冒险 'violence
        (outcome (lambda () (on-haul! '好)))
        (outcome (lambda () (on-haul! '中)))
        (outcome (lambda () (on-haul! '坏)))
        (haul-subtitle)
        :anchor "码头-货堆"
        :risk-target (list 'composure "player")
        :progress-target (list 'item "金钱")))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((eq? msg 'render-data)
           (list (place "码头"
             :children (append
               (list (haul-node))
               (map (lambda (n)
                      (if (member? :anchor n) n
                        (append n (list :anchor "码头"))))
                 (地点节点 "码头")))
             :arrivals (append
               (if (not (第一幕 'eddie-met?))
                 (list (arrival "艾迪教你搬运" (lambda () (第一幕 'meet-eddie!))))
                 '())
               (地点入场 "码头")))))
          ((eq? msg 'eddie-met?) (第一幕 'eddie-met?))
          ((eq? msg 'save)
           (list (list "partner" dock-partner)
                 (list "help-day" help-day)
                 (list "joe-met" joe-met?)
                 (list "buddy-talk-day" buddy-talk-day)))
          ((eq? msg 'load!)
           (let ((data (cadr args)))
             (set! dock-partner (assoc-get data "partner" "艾迪"))
             (set! help-day (assoc-get data "help-day" 0))
             (set! joe-met? (assoc-get data "joe-met" #f))
             (set! buddy-talk-day (assoc-get data "buddy-talk-day" 0))
             (if (member? dock-partner '("艾迪" "乔" "陌生人"))
               #t (error "码头存档：无效搭档"))))
          (else (error "码头：未知消息")))))))
