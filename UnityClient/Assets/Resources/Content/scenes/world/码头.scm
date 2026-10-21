;; 码头：普通工作、按天确定的搭档与艾迪的人情。
;; 不再承担旧勒索信的邮箱或埋伏任务。人物演出由第一幕管理。
(define dock
  (let ()
    (define partner "艾迪")
    (define first-haul? #t)
    (define eddie-help-day 0)
    (define joe-met? #f)
    (define echoes (make-echo-pool
      (lambda () (if (= (第一幕 'act) 1) 1 2))
      (list 1 (lambda () (play-bubble! (line "工友" "下一艘货船今晚靠岸。" :at "码头-货堆"))))
      (list 2 (lambda () (play-bubble! (line "工友" "工棚那头又有人来问改造的事情。" :at "码头-货堆"))))))
    (define (today-partner) (if first-haul? "艾迪" partner))
    (define (choose-partner!)
      (set! partner
        (if first-haul? "艾迪"
          (random-choice (list "艾迪" "艾迪" "乔" "乔" "陌生人" "陌生人")))))
    (define-turn-start-rule "码头今日搭班"
      (lambda () #t) (lambda () (choose-partner!)))
    (define (resolve-haul! grade)
      (let ((mate (today-partner)))
        ;; 动作首先保持正常结果层级；伙伴只改变后果，不改变骰子判定。
        (cond
          ((eq? grade '好) (add-item! "金钱" 20))
          ((eq? grade '中) (add-item! "金钱" 10) (spend-composure! 1))
          ((equal? mate "乔") #f)
          ((and (equal? mate "艾迪") (not (= eddie-help-day world-day)))
            (set! eddie-help-day world-day)
            (spend-composure! 1)
            (play-bubble!
              (line "世界" "箱子滑下去，艾迪赶过来接住重的那头。" :at "码头-货堆")
              (line "艾迪" "松手！手坏了可没人替你挣钱。" :at "码头-货堆")))
          (else (spend-composure! 2)))
        (if (and (equal? mate "乔") (not joe-met?))
          (begin (set! joe-met? #t)
            (play-bubble! (line "乔" "绳子打在这边，下次就不用拼蛮力。" :at "码头-岸口"))) #f)
        (if (equal? mate "艾迪")
          (if (and (not first-haul?) (eq? grade '好))
            (play-bubble! (line "艾迪" "今天干得不错。收工早点回去。" :at "码头-货堆")) #f)
          (if (not (eq? grade '好)) (echoes 'try!) #f))
        (if first-haul? (set! first-haul? #f) #f)
        (第一幕 'on-work! "码头" grade)))
    (define (haul-description)
      (cond
        ((equal? (today-partner) "艾迪")
          (if (= eddie-help-day world-day)
            "艾迪搭班；今天已帮你接过一次货"
            "艾迪搭班；失手可少损失1点冷静"))
        ((equal? (today-partner) "乔") "乔搭班；失手时有人接住重货")
        (else "今天和陌生工友搭班；照常结算")))
    (define (nodes)
      (list (node "搬运" :anchor "码头-货堆"
        :subtitle (haul-description) :tags (list "工作" "高风险")
        :requires (list (req-die))
        :resolve (roll 'violence
          (outcome (lambda () (resolve-haul! '坏)))
          (outcome (lambda () (resolve-haul! '中)))
          (outcome (lambda () (resolve-haul! '好)))))))
    (lambda args
      (let ((m (car args)))
        (cond
          ((eq? m 'render-data)
            (list (place "码头" :children (append (nodes) (地点节点 "码头"))
                          :arrivals (地点入场 "码头"))))
          ((eq? m 'eddie-met?) (not first-haul?))
          ((eq? m 'ambush-eddie?) #f)
          ((eq? m 'mailbox-known?) #f)
          ((eq? m 'save)
            (list (list "partner" partner) (list "first-haul" first-haul?)
                  (list "eddie-help-day" eddie-help-day)
                  (list "joe-met" joe-met?)
                  (list "echoes" (echoes 'save))))
          ((eq? m 'load!)
            (let ((data (cadr args)))
              (set! partner (assoc-get data "partner" "艾迪"))
              (set! first-haul? (assoc-get data "first-haul" #t))
              (set! eddie-help-day (assoc-get data "eddie-help-day" 0))
              (set! joe-met? (assoc-get data "joe-met" #f))
              (echoes 'load! (assoc-get data "echoes" '()))
              (if (member? partner '("艾迪" "乔" "陌生人")) #t
                (error "码头搭班存档：未知搭档"))))
          (else (error "码头：未知消息")))))))
