(define 第二幕
  (let ()
    (define wage-state "未做")
    (define lucy-state "未做")
    (define (available?) (and (= (第一幕 'act) 2) (>= world-day 7) (<= world-day 9)))
    (define (wage-end result)
      (if (member? result '(拿回工钱 保住工作 弗兰克调停 撤出)) #t
        (error "工钱交锋：非法结算"))
      (set! wage-state (symbol->string result)))
    (define (lucy-help! kind)
      (set! lucy-state kind)
      (play-dialogue!
        (line "露西" "真的？你帮我顶班，我就能去报名了。")
        (line "尼尔" "快去吧。")))
    (define (nodes-at loc)
      (append
        (if (and (equal? loc "码头") (available?) (equal? wage-state "未做"))
          (list (node "少了十块钱" :anchor "码头-货堆"
            :subtitle "艾迪替工友争工钱，你可以参与"
            :resolve (instant (outcome
              (lambda () (start-encounter "少了十块钱" wage-end))))))
          '())
        (if (and (equal? loc "街角咖啡馆") (available?)
                 (第一幕 'lucy-met?) (equal? lucy-state "未做"))
          (list
            (node "替露西顶班" :subtitle "用一枚骰帮她争取报名时间"
              :requires (list (req-die))
              :resolve (instant (outcome (lambda () (lucy-help! "顶班")))))
            (node "付代班费" :subtitle "花10美元，请人替她工作"
              :requires (list (req-item "金钱" 10))
              :resolve (instant (outcome (lambda ()
                (remove-item! "金钱" 10)
                (lucy-help! "代班"))))))
          '())))
    (define-turn-start-rule "第二幕事件期限"
      (lambda () #t)
      (lambda ()
        (if (>= world-day 10)
          (begin
            (if (equal? wage-state "未做") (set! wage-state "错过") #f)
            (if (equal? lucy-state "未做") (set! lucy-state "错过") #f))
          #f)))
    (lambda args
      (cond
        ((eq? (car args) 'nodes-at) (nodes-at (cadr args)))
        ((eq? (car args) 'save)
          (list (list "wage" wage-state) (list "lucy" lucy-state)))
        ((eq? (car args) 'load!)
          (let ((data (cadr args)))
            (set! wage-state (assoc-get data "wage" "未做"))
            (set! lucy-state (assoc-get data "lucy" "未做"))))
        (else (error "第二幕：未知消息"))))))
