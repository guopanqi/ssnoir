;; 所有相对日期均从真实世界日推导，展示不另存倒计时。
(define (第二幕-日期 day)
  (let ((left (- day world-day)))
    (cond ((= left 0) "今天") ((= left 1) "明天") ((= left 2) "后天")
      ((> left 2) (string-append (number->string left) "天后"))
      (else (error "第二幕：不能预告过去的日期")))))
(define (第二幕-期限钟 label day first-day)
  (clock-priority
    (list 'clock label (+ 1 (- day world-day)) (+ 1 (- day first-day))
      'countdown "今晚结束后少一天，归零后不能再参与。") 'secondary))

(define 第二幕
  (let ()
    (define wage-state "未做")
    (define lucy-state "未做")
    (define wage-introduced? #f)
    (define lucy-introduced? #f)
    (define (wage-open?) (and (= (第一幕 'act) 2) (>= world-day 9) (<= world-day 11)))
    (define (lucy-open?) (and (= (第一幕 'act) 2) (>= world-day 9) (<= world-day 12)))
    (define (wage-intro-open?) (and (= (第一幕 'act) 2)
      (>= world-day 8) (<= world-day 11) (equal? wage-state "未做")))
    (define (lucy-intro-open?) (and (= (第一幕 'act) 2)
      (>= world-day 8) (<= world-day 12) (equal? lucy-state "未做")))
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
    ;; 私人的事情只在尼尔听到之后才进入手头的事。
    (define (known-matters)
      (append
        (if (or wage-introduced? (二幕余波-艾迪已知?))
          (list (dossier "艾迪：少了十块钱"
            :kind '人物 :primary #f
            :status (if (and (equal? wage-state "未做") (<= world-day 11)) '进行中 '了结)
            :now (cond
              ((二幕余波-艾迪已知?) (二幕余波-艾迪近况))
              ((and (equal? wage-state "未做") (<= world-day 11))
                (string-append (第二幕-日期 11) "收工封账。去码头帮艾迪争取复核少付的10美元。"))
              ((or (equal? wage-state "错过") (equal? wage-state "未做")) "核账窗口已过，还没听到后来的结果。")
              ((equal? wage-state "拿回工钱") "少付的10美元已经拿回来了。")
              ((equal? wage-state "保住工作") "这次让了步，工友保住了工作。")
              ((equal? wage-state "弗兰克调停") "弗兰克出面核清了账。")
              (else "你已退出争执，艾迪会继续处理。"))
            :steps (append
              (if wage-introduced? (list (step "听到工友少拿了10美元" #t)) '())
              (if (and (equal? wage-state "未做") (<= world-day 11))
                (list (step "去码头参与核账争执" #f)) '()))
            :where "码头"))
          '())
        (if (or lucy-introduced? (二幕余波-露西已知?))
          (list (dossier "露西：夜校报名"
            :kind '人物 :primary #f
            :status (cond ((二幕余波-露西已知?) '了结)
              ((and (equal? lucy-state "未做") (<= world-day 12)) '进行中)
              (else '等着别人))
            :now (cond
              ((二幕余波-露西已知?) "露西已经交上报名表，说第一堂课在下星期。")
              ((and (equal? lucy-state "未做") (<= world-day 12))
                (string-append "报名" (第二幕-日期 12) "截止。露西还缺人顶班，腾出时间就能去。"))
              ((or (equal? lucy-state "错过") (equal? lucy-state "未做")) "报名期限已过，还没听到露西的消息。")
              (else "班已经安排好，露西说会趁空去报名。还没听到报名结果。"))
            :steps (append
              (if lucy-introduced? (list (step "得知露西想报名夜校" #t)) '())
              (cond
                ((and (equal? lucy-state "未做") (<= world-day 12))
                  (list (step "去咖啡馆顶班，或付10美元请人代班" #f)))
                ((equal? lucy-state "顶班") (list (step "替露西顶班" #t)))
                ((equal? lucy-state "代班") (list (step "付钱请人代班" #t)))
                (else '()))
              (if (二幕余波-露西已知?)
                (list (step "听露西确认报名表已交" #t)) '()))
            :where "街角咖啡馆"))
          '())))
    (define (nodes-at loc)
      (append
        (if (and (equal? loc "码头") (wage-intro-open?)
                 (第一幕 'eddie-met?) (not wage-introduced?))
          (list (instant-action "问艾迪工钱" tell-wage!)) '())
        (if (and (equal? loc "街角咖啡馆") (lucy-intro-open?)
                 (第一幕 'lucy-met?) (not lucy-introduced?))
          (list (instant-action "问露西报名" tell-lucy!)) '())
        (if (and (equal? loc "码头") (wage-open?) (equal? wage-state "未做")
                 (第一幕 'eddie-met?) wage-introduced?)
          (list (node "少了十块钱" :anchor "码头-货堆"
            :subtitle "封账前可参与核账，之后艾迪自行处理"
            :clocks (list (第二幕-期限钟 "封账" 11 9))
            :resolve (instant (outcome
              (lambda () (start-encounter "少了十块钱" wage-end))))))
          '())
        (if (and (equal? loc "街角咖啡馆") (lucy-open?)
                 (第一幕 'lucy-met?) (equal? lucy-state "未做") lucy-introduced?)
          (list
            (node "替露西顶班" :subtitle "用一枚骰顶班，让她今晚去报名"
              :clocks (list (第二幕-期限钟 "报名截止" 12 9))
              :requires (list (req-die))
              :resolve (instant (outcome (lambda () (lucy-help! "顶班")))))
            (node "付代班费" :subtitle "花10美元，让她今晚去报名"
              :clocks (list (第二幕-期限钟 "报名截止" 12 9))
              :requires (list (req-item "金钱" 10))
              :resolve (instant (outcome (lambda ()
                (remove-item! "金钱" 10)
                (lucy-help! "代班"))))))
          '())))
    ;; 熟人通过日常工作或主动交谈告诉尼尔，并不是每日进地点自动播放。
    (define (tell-wage!)
      (if wage-introduced? #f
        (begin
          (set! wage-introduced? #t)
          (play-dialogue!
            (line "艾迪" "一个工友说工钱少了十块。工头连账本都不肯给他看。")
            (line "尼尔" "你要去找他？")
            (line "艾迪" (string-append "账房" (第二幕-日期 11) "收工封账，得赶在那之前说清楚。"))))))
    (define (tell-lucy!)
      (if lucy-introduced? #f
        (begin
          (set! lucy-introduced? #t)
          (play-dialogue!
            (line "露西" "学校有空位了，可我还要在咖啡馆上工。")
            (line "露西" (string-append "报名" (第二幕-日期 12) "截止。我想请假，老板还没答应。"))
            (line "露西" "要是有人替我顶班，我就能赶过去。")))))
    (define (arrivals-at location) '())
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
        ((eq? (car args) 'on-dock-work!)
          (if (and (wage-intro-open?) (第一幕 'eddie-met?)
                   (not wage-introduced?))
            (tell-wage!) #f))
        ((eq? (car args) 'on-cafe-work!)
          (if (and (lucy-intro-open?) (第一幕 'lucy-met?)
                   (not lucy-introduced?))
            (tell-lucy!) #f))
        ((eq? (car args) 'lucy-state) lucy-state)
        ((eq? (car args) 'dossier)
          (if (and (= (第一幕 'act) 2) (>= world-day 7))
            (known-matters)
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
