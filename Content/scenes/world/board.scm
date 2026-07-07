;; scenes/world/board.scm - 城市布告栏
;; 只保存模板 ID 与剩余天数；六张委托直接由内容函数渲染，不建立通用任务框架。

(define board
  (let ()
    (define template-ids
      (list "帮人寻物" "替人带话" "押送一批货" "代查一笔账"
            "急收私货的买家" "有人需要药"))
    (define active-missions '()) ; 每项：(模板 ID 剩余天数)
    (define refresh-days 0)
    (define mission-duration 2)
    (define refresh-interval 3)

    (define (mission-id entry) (car entry))
    (define (mission-days entry) (cadr entry))

    (define (valid-template? id)
      (define (walk ids)
        (if (null? ids)
            #f
            (if (equal? id (car ids)) #t (walk (cdr ids)))))
      (walk template-ids))

    (define (mission-active? id entries)
      (if (null? entries)
          #f
          (if (equal? id (mission-id (car entries)))
              #t
              (mission-active? id (cdr entries)))))

    (define (available-template-ids)
      (filter (lambda (id) (not (mission-active? id active-missions))) template-ids))

    (define (add-random-mission!)
      (let ((available (available-template-ids)))
        (if (null? available)
            #f
            (set! active-missions
                  (cons (list (random-choice available) mission-duration) active-missions)))))

    (define (fill-board-to! target)
      (if (and (< (length active-missions) target)
               (not (null? (available-template-ids))))
          (begin (add-random-mission!) (fill-board-to! target))
          #t))

    (define (remove-mission entries id)
      (if (null? entries)
          '()
          (if (equal? (mission-id (car entries)) id)
              (cdr entries)
              (cons (car entries) (remove-mission (cdr entries) id)))))

    (define (complete-mission! id effect)
      (if (not (mission-active? id active-missions))
          (error "布告栏：尝试完成不存在的委托")
          #t)
      (effect)
      (set! active-missions (remove-mission active-missions id)))

    (define (tick-missions entries)
      (if (null? entries)
          '()
          (let ((entry (car entries)))
            (if (<= (mission-days entry) 1)
                (tick-missions (cdr entries))
                (cons (list (mission-id entry) (- (mission-days entry) 1))
                      (tick-missions (cdr entries)))))))

    (define (mission-clocks entry)
      (list (list 'clock (mission-id entry) (mission-days entry) mission-duration 'countdown
                  "归零后买家或委托人就会离开。")))

    (define (roll-mission entry skill good-pay neutral-pay fail-effect)
      (let ((id (mission-id entry)))
        (action-with-clocks id
          (list (req-die))
          (roll skill
            (outcome "没办成" "机会过去了，只留下这一趟的疲惫。"
              (lambda () (complete-mission! id fail-effect)))
            (outcome "勉强交差" "事情办得不算漂亮，委托人还是付了一部分报酬。"
              (lambda ()
                (complete-mission! id (lambda () (add-item! "金钱" neutral-pay)))))
            (outcome "办得漂亮" "委托人很满意，当场付清了报酬。"
              (lambda ()
                (complete-mission! id (lambda () (add-item! "金钱" good-pay))))))
          (mission-clocks entry))))

    (define (private-buyer-node entry)
      (let ((qty (min 2 (item-count "私货"))))
        (node "急收私货的买家"
          :subtitle "这位买家只停留两天，但给的价钱比代理人还高"
          :clocks (mission-clocks entry)
          :disabled (= qty 0)
          :requires (list (req-item "私货" (max 1 qty)))
          :resolve (instant
            (outcome "当场成交" "买家没有多问，把货和钱各自收好。"
              (lambda ()
                (complete-mission! "急收私货的买家"
                  (lambda () (add-item! "金钱" (* qty 30))))))))))

    (define (medicine-request-node entry)
      (node "有人需要药"
        :subtitle "老街有人急着用药，帮这一回会被大家记住"
        :clocks (mission-clocks entry)
        :disabled (< (item-count "药品") 1)
        :requires (list (req-item "药品" 1))
        :resolve (instant
          (outcome "药送到了" "病人家属收下药，老街的人也记住了这份人情。"
            (lambda ()
              (complete-mission! "有人需要药"
                (lambda ()
                  (add-item! "金钱" 20)
                  (change-faction-relation! "劳工" 1))))))))

    (define (mission-node entry)
      (let ((id (mission-id entry)))
        (cond
          ((equal? id "帮人寻物")
           (roll-mission entry 'sharpness 22 12 (lambda () (stress-current-actor! 1))))
          ((equal? id "替人带话")
           (roll-mission entry 'social 20 10 (lambda () (stress-current-actor! 1))))
          ((equal? id "押送一批货")
           (roll-mission entry 'violence 25 14
             (lambda () (stress-current-actor! 1) (damage-party! 1))))
          ((equal? id "代查一笔账")
           (roll-mission entry 'knowledge 22 12 (lambda () (stress-current-actor! 1))))
          ((equal? id "急收私货的买家") (private-buyer-node entry))
          ((equal? id "有人需要药") (medicine-request-node entry))
          (else (error "布告栏：未知委托模板")))))

    (define (node-ask-for-rumors)
      (node "向消息灵通的人打听"
        :subtitle "花掉一条消息，立刻找出一张额外的临时委托"
        :requires (list (req-item "情报" 1))
        :resolve (instant
          (outcome "问到新门路" "角落里的人压低声音，告诉你一件刚刚冒出来的差事。"
            (lambda ()
              (if (not (add-random-mission!))
                  (error "布告栏：没有可追加的委托模板")
                  #t))))))

    (define (validate-missions entries)
      (if (null? entries)
          #t
          (let ((entry (car entries)))
            (if (or (not (valid-template? (mission-id entry)))
                    (<= (mission-days entry) 0)
                    (> (mission-days entry) mission-duration)
                    (mission-active? (mission-id entry) (cdr entries)))
                (error "布告栏存档包含非法或重复委托")
                (validate-missions (cdr entries))))))

    (define-turn-rule "布告栏委托流逝与刷新"
      (lambda () #t)
      (lambda ()
        (set! active-missions (tick-missions active-missions))
        (set! refresh-days (+ refresh-days 1))
        (if (>= refresh-days refresh-interval)
            (begin
              (set! refresh-days 0)
              (fill-board-to! 2))
            #f)))

    (fill-board-to! 2)

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list
             (node "布告栏"
               :children
                 (append
                   (map mission-node active-missions)
                   (if (and (> (item-count "情报") 0)
                            (not (null? (available-template-ids))))
                       (list (node-ask-for-rumors))
                       '())))))
          ((equal? msg 'save)
           (list
             (list "active-missions" active-missions)
             (list "refresh-days" refresh-days)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! active-missions (assoc-get data "active-missions" '()))
             (set! refresh-days (assoc-get data "refresh-days" 0))
             (validate-missions active-missions)))
          ((equal? msg 'debug-refresh)
           (begin (set! active-missions '()) (fill-board-to! 2)))
          (#t #f))))))
