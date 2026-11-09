;; 周期活动属于日程；缺席结算才可能创建留酒实例，不是预先存在的空占位。
(define 炉边工友 (speaker "炉边工友" :name "工友" :appearance '成年男性))
(define 院里的炉火
  (let ()
    (define next-day 0)
    (define attended-day 0)
    (define visits 0)
    (define gift-arranged? #f)
    (define checked-day 0)
    (define (wine-event id payload)
      (make-triggered-event id "进入地点" ""
        (lambda (ctx) (and (居民区相识 'familiar?)
          (>= world-day (assoc-get payload "from" 0))))
        (lambda (ctx) #f) 0 1 100
        (list (lambda () (>= world-day (assoc-get payload "until" 0)))
          '("码头" "码头居民区")
          (lambda (place finish!)
            (list (node (string-append id "@" place) :name "给你留的"
              :anchor (if (equal? place "码头") "码头-货堆" "码头居民区-东侧门廊")
              :button-text "交谈" :subtitle "一个熟人叫住你，手里拎着小酒瓶。"
              :resolve (instant (outcome (lambda () (finish! (lambda ()
                (spotlight-page! ""
                  (list
                    (list "一个工友叫住你，把一小瓶酒塞进你手里。" "EventSheets/城市生活/留酒")
                    (list "“上回怎么没来？给你留的。凳子也留着，下回有空就来。”" ""))
                  (lambda () (add-item! "邻里留酒" 1))))))))))))))
    (define 后续 (make-event-pool "炉火后续" (list (list "邻里留酒" wine-event))))
    ;; 世界日历在日终先推进；这里结算刚结束的那一场，不由参加动作结算。
    (define (close-gathering!)
      (if (and (> next-day 0) (> world-day next-day))
        (let ((closed (- world-day 1 (modulo (- world-day next-day 1) 4))))
          (set! next-day (+ closed 4))
          (if (and (> visits 0) (not (= attended-day closed)) (not gift-arranged?)
                   (< world-day next-day))
            (if (后续 'schedule! "邻里留酒" (number->string closed)
                (list (list "from" (+ closed 1)) (list "until" next-day)) 50)
              (set! gift-arranged? #t) #f) #f)) #f)
      (后续 'refresh!))
    (define-turn-rule "炉火散场" (lambda () #t) close-gathering!)
    (define (unlock!)
      (if (and (居民区相识 'familiar?) (= next-day 0))
        (set! next-day (+ world-day 3)) #f))
    (define (new-day!)
      (if (> world-day checked-day)
        (begin (set! checked-day world-day)
          (if (and (居民区相识 'familiar?) (= next-day 0))
            (set! next-day (+ world-day 2)) #f)
          (后续 'refresh!)) #f))
    (define-turn-start-rule "院里炉火日程" (lambda () #t) new-day!)
    (define (join!)
      (if (and (居民区相识 'familiar?) (= world-day next-day) (not (= attended-day world-day))) #t
        (error "炉火聚餐：不在本场开放时间或已经参加"))
      (set! attended-day world-day) (set! visits (+ visits 1))
      (spotlight-page! ""
        (list
          (list "铁炉上的炖锅咕嘟作响，有人掰开面包，给你挪出一张板凳。" "EventSheets/城市生活/聚餐")
          (list (cond ((= visits 1) "“尼尔，坐这儿。今天不用干活。”")
                  ((= (modulo visits 2) 0) "有人拿出口琴。刚才还聊个不停的工友，笑着捂住了耳朵。")
                  (else "你的凳子在炉子边。有人伸手替你把碗接满。")) "")
          (list "大家边吃边聊。你把手伸到炉边，听完那首走调的曲子。" ""))
        (lambda () (restore-actor-composure! 'player 2))))
    (lambda (msg . args)
      (cond
        ((eq? msg 'unlock!) (unlock!))
        ((eq? msg 'interactions) (list 后续))
        ((eq? msg 'nodes-at)
          (append
            (if (and (equal? (car args) "码头居民区") (居民区相识 'familiar?))
              (if (= world-day next-day)
                (list (node "院里聚餐" :name "炉边坐坐" :anchor "码头居民区-东侧门廊"
                  :use-limit 'daily-once :disabled (= attended-day world-day)
                  :subtitle (if (= attended-day world-day) "已经和大家吃过了，今晚散场。" "今天聚餐，坐一会儿恢复2点心情。") :button-text "执行"
                  :clocks (list (clock-priority (日期倒计时 "今晚聚餐" (+ next-day 1) 1 "今天结束后散场，不参加没有惩罚。") 'secondary))
                  :resolve (instant (outcome join!))))
                (list (at-anchor "码头居民区-东侧门廊"
                  (clock-node "院里聚餐等待"
                    (日期倒计时 "下次聚餐" next-day 3
                      "散场后等三天再聚，有空就来，不参加没有惩罚。"))))) '())
            (if (and (member? (car args) '("家" "码头" "码头居民区")) (has-item? "邻里留酒" 1))
              (list (node (string-append "喝邻里留酒@" (car args)) :name "喝留好的酒"
                :anchor (cond ((equal? (car args) "家") "家") ((equal? (car args) "码头") "码头-货堆")
                  (else "码头居民区-东侧门廊"))
                :subtitle "小半瓶，恢复1点心情。" :requires (list (req-item "邻里留酒" 1))
                :resolve (instant (outcome (lambda () (restore-actor-composure! 'player 1)))))) '())))
        ((eq? msg 'save) (list (list "next" next-day) (list "attended" attended-day)
          (list "visits" visits) (list "gift-arranged" gift-arranged?) (list "checked" checked-day)
          (list "events" (后续 'save))))
        ((eq? msg 'load!) (let ((d (car args)))
          (set! next-day (assoc-get d "next" 0)) (set! attended-day (assoc-get d "attended" 0))
          (set! visits (assoc-get d "visits" 0)) (set! gift-arranged? (assoc-get d "gift-arranged" #f))
          (set! checked-day (assoc-get d "checked" 0)) (后续 'load! (assoc-get d "events" '()))
          (unlock!)
          (if (and (>= next-day 0) (>= attended-day 0) (>= visits 0) (>= checked-day 0)) #t
            (error "炉火聚餐：非法存档"))))
        (else (error "炉火聚餐：未知消息"))))))
