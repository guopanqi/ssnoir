(define 第二幕
  (let ()
    (define wage-state "未做")
    (define lucy-state "未做")
    (define wage-introduced? #f)
    (define lucy-introduced? #f)
    (define (wage-open?) (and (= (第一幕 'act) 2) (>= world-day 9) (<= world-day 11)))
    (define (lucy-open?) (and (= (第一幕 'act) 2) (>= world-day 9) (<= world-day 12)))
    (define (wage-end result)
      (if (member? result '(拿回工钱 保住工作 弗兰克调停 撤出)) #t
        (error "工钱交锋：非法结算"))
      (set! wage-state (symbol->string result)))
    (define (lucy-help! kind)
      (set! lucy-state kind)
      (play-dialogue!
        (line "露西" (if (equal? kind "代班")
          "你还帮我找了代班的人？这钱我会还你的。"
          "真的？你肯替我顶班，我就能去报名了。"))
        (line "尼尔" "快去吧。")
        (line "露西" "那我今晚就去。谢谢你，尼尔。")))
    (define (nodes-at loc)
      (append
        (if (and (equal? loc "码头") (wage-open?) (equal? wage-state "未做")
                 (第一幕 'eddie-met?) wage-introduced?)
          (list (node "少了十块钱" :anchor "码头-货堆"
            :subtitle "艾迪替工友争工钱，你可以参与"
            :resolve (instant (outcome
              (lambda () (start-encounter "少了十块钱" wage-end))))))
          '())
        (if (and (equal? loc "街角咖啡馆") (lucy-open?)
                 (第一幕 'lucy-met?) (equal? lucy-state "未做") lucy-introduced?)
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
    (define (arrivals-at location)
      (append
        (if (and (equal? location "码头") (wage-open?)
                 (第一幕 'eddie-met?) (equal? wage-state "未做")
                 (not wage-introduced?))
          (list (arrival "工钱纠纷" (lambda ()
            (set! wage-introduced? #t)
            (play-dialogue!
              (line "世界" "一名工友说自己的工钱少了十块，工头却不肯打开账本。")
              (line "艾迪" "他干了整天活，你得把钱给足。")
              (line "工头" "账上就写这么多。")))))
          '())
        (if (and (equal? location "街角咖啡馆") (lucy-open?)
                 (第一幕 'lucy-met?) (equal? lucy-state "未做")
                 (not lucy-introduced?))
          (list (arrival "露西的报名" (lambda ()
            (set! lucy-introduced? #t)
            (play-dialogue!
              (line "露西" "学校有空位了，可我还要上工。")
              (line "露西" "我想请假去报名，老板却还没答应。")))))
          '())))
    (define-turn-start-rule "第二幕事件期限"
      (lambda () #t)
      (lambda ()
        (if (>= world-day 12)
          (if (equal? wage-state "未做") (set! wage-state "错过") #f) #f)
        (if (>= world-day 13)
          (if (equal? lucy-state "未做") (set! lucy-state "错过") #f) #f)))
    (lambda args
      (cond
        ((eq? (car args) 'wage-state) wage-state)
        ((eq? (car args) 'lucy-state) lucy-state)
        ((eq? (car args) 'dossier)
          (if (and (= (第一幕 'act) 2) (< world-day 13))
            (list (dossier "这里有我的朋友" :kind '主线 :primary #t
              :status '进行中
              :now "继续谋生，认识熟人的生活；夜莺的复排也在临近。"
              :where ""))
            '()))
        ((eq? (car args) 'nodes-at) (nodes-at (cadr args)))
        ((eq? (car args) 'arrivals-at) (arrivals-at (cadr args)))
        ((eq? (car args) 'save)
          (list (list "wage" wage-state) (list "lucy" lucy-state)
                (list "wage-introduced" wage-introduced?)
                (list "lucy-introduced" lucy-introduced?)))
        ((eq? (car args) 'load!)
          (let ((data (cadr args)))
            (set! wage-state (assoc-get data "wage" "未做"))
            (set! lucy-state (assoc-get data "lucy" "未做"))
            (set! wage-introduced? (assoc-get data "wage-introduced" #f))
            (set! lucy-introduced? (assoc-get data "lucy-introduced" #f))))
        (else (error "第二幕：未知消息"))))))
