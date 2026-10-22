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
    (define lucy-boss-heard? #f) ; D9 起真正面对咖啡馆请假难题
    (define lucy-covered 0)     ; 0..2，每班一颗骰；也可付费请人
    (define wage-checked? #f)   ; 核对机会只能尝试一次
    (define wage-evidence? #f)  ; 真实工时记录可在交锋时出示
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
    (define (eddie-on-dock?)
      (not (and (>= world-day 12) (<= world-day 13)
        (or (and (= world-day 12) (equal? wage-state "拿回工钱"))
            (equal? wage-state "撤出")))))
    (define (wage-check! grade)
      (if wage-checked? (error "工时记录：不得重复调查") #t)
      (set! wage-checked? #t)
      (cond
        ((eq? grade '好)
          (set! wage-evidence? #t)
          (play-dialogue!
            (line "世界" "工时册上有工人签过名的一整班，账房却只记了半班。")
            (line "艾迪" "拿着它去说，工头总不能当没看见。")))
        ((eq? grade '中)
          (set! wage-evidence? #t)
          (spend-composure! 1)
          (play-dialogue!
            (line "世界" "尼尔翻了好一阵，终于对上两本账。手指沾满了油墨。")))
        (else
          (spend-composure! 1)
          (play-dialogue!
            (line "世界" "账房把工时册收了起来。尼尔没来得及抄下签名。")
            (line "艾迪" "那就只能当面问了。")))))
    (define (lucy-help! kind)
      (if (not (equal? lucy-state "未做")) (error "夜校报名：重复安排班次") #t)
      (set! lucy-state kind)
      (play-dialogue!
        (line "露西" (cond
          ((equal? kind "顶班") "你替我守了两班，我终于能赶在报名截止前过去。")
          ((equal? kind "合办") "你已经顶了一班，还帮我请人守另一班。")
          (else "你帮我把两班都安排好了吗？这笔钱我记着。")))
        (line "尼尔" "你准备了这么久，赶快去。")
        (line "露西" "我还得去问学费的事。等我回来再和你说。")))
    (define (lucy-boss!)
      (if lucy-boss-heard? (error "露西已经与老板谈过请假") #t)
      (set! lucy-boss-heard? #t)
      (play-dialogue!
        (line "露西" "夜校只在工作日收报名表。我已经试着换了几回班。")
        (line "老板" "店里只剩两个时段没人。你找到代班，我就让你去。")
        (line "尼尔" "两个班都得补？")
        (line "露西" "我也不愿意让你白帮忙。要不然我再自己想办法。")))
    (define (lucy-cover!)
      (if (>= lucy-covered 2) (error "夜校报名：顶班次数越界") #t)
      (set! lucy-covered (+ lucy-covered 1))
      (if (= lucy-covered 2)
        (lucy-help! "顶班")
        (play-dialogue!
          (line "世界" "尼尔替露西守完一班。咖啡馆的忙碌一直持续到关门。")
          (line "老板" "明天还缺一班。你们想办法安排，我才放她去。")
          (line "露西" "已经麻烦你一回了。下一班我也可以自己再想办法。"))))
    (define (lucy-pay!)
      (remove-item! "金钱" (if (= lucy-covered 1) 15 30))
      (lucy-help! (if (= lucy-covered 1) "合办" "代班")))
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
              ((equal? wage-state "拿回工钱") "工钱拿回了，但工头停了艾迪第12天的班。")
              ((equal? wage-state "保住工作") "这次让了步，工友保住了工作。")
              ((equal? wage-state "弗兰克调停") "弗兰克出面核清了账。")
              (else "工头暂时不让艾迪领班。第12至13天，码头不会有人替你救场。"))
            :steps (append
              (if wage-introduced? (list (step "听到工友少拿了10美元" #t)) '())
              (if (and (equal? wage-state "未做") (<= world-day 11))
                (list
                  (step (if wage-checked?
                    (if wage-evidence? "已取得工时记录，可当面对质" "工时记录没有拿到")
                    "可先核查工时记录，再与工头对质") wage-checked?)
                  (step "去码头参与核账争执" #f)) '()))
            :where "码头"))
          '())
        (if (or lucy-introduced? (二幕余波-露西已知?))
          (list (dossier "露西：夜校报名"
            :kind '人物 :primary #f
            :status (cond ((二幕余波-露西已知?) '了结)
              ((and (equal? lucy-state "未做") (<= world-day 12)) '进行中)
              (else '等着别人))
            :now (cond
              ((二幕余波-露西已知?) (二幕余波-露西近况))
              ((and (equal? lucy-state "未做") (<= world-day 12)
                    (not lucy-boss-heard?))
                (string-append "报名" (第二幕-日期 12)
                  "截止。露西今天还想去与老板商量顶班的事。"))
              ((and (equal? lucy-state "未做") (<= world-day 12))
                (string-append "报名" (第二幕-日期 12) "截止。还需顶"
                  (number->string (- 2 lucy-covered)) "班，或付"
                  (number->string (if (= lucy-covered 1) 15 30)) "美元请人。"))
              ((or (equal? lucy-state "错过") (equal? lucy-state "未做")) "报名期限已过，还没听到露西的消息。")
              (else "班已经安排好，露西说会趁空去报名。还没听到报名结果。"))
            :steps (append
              (if lucy-introduced? (list (step "得知露西想报名夜校" #t)) '())
              (cond
                ((and (equal? lucy-state "未做") (<= world-day 12)
                      (not lucy-boss-heard?))
                  (list (step "听听咖啡馆老板是否准假" #f)))
                ((and (equal? lucy-state "未做") (<= world-day 12))
                  (list (step "去咖啡馆顶两班，或付30美元请人代班" #f)))
                ((equal? lucy-state "顶班") (list (step "替露西守完两班" #t)))
                ((equal? lucy-state "合办") (list (step "顶一班并付费安排另一班" #t)))
                ((equal? lucy-state "代班") (list (step "付钱安排两班" #t)))
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
        (if (and (equal? loc "码头") (wage-open?) wage-introduced?
                 (equal? wage-state "未做") (not wage-checked?))
          (list (node "查工时记录"
             :subtitle "花一骰核对签名；成功后可在交锋中出示"
             :risk '冒险 :risk-target (list 'composure "player")
             :requires (list (req-die))
             :resolve (roll 'sharpness
               (outcome (lambda () (wage-check! '坏)))
               (outcome (lambda () (wage-check! '中)))
               (outcome (lambda () (wage-check! '好)))))) '())
        (if (and (equal? loc "码头") (wage-open?) (equal? wage-state "未做")
                 (第一幕 'eddie-met?) wage-introduced?)
          (list (node "少了十块钱" :anchor "码头-货堆"
            :subtitle "封账前可参与核账，之后艾迪自行处理"
            :clocks (list (第二幕-期限钟 "封账" 11 9))
            :resolve (instant (outcome
              (lambda () (begin
                (set-global! '第二幕工时证据 wage-evidence?)
                (start-encounter "少了十块钱" wage-end)))))))
          '())
        (if (and (equal? loc "街角咖啡馆") (lucy-open?)
                 (第一幕 'lucy-met?) (equal? lucy-state "未做") lucy-introduced?
                 (not lucy-boss-heard?))
          (list (instant-action "听听老板怎么说" lucy-boss!)) '())
        (if (and (equal? loc "街角咖啡馆") (lucy-open?)
                 (第一幕 'lucy-met?) (equal? lucy-state "未做") lucy-introduced?
                 lucy-boss-heard?)
          (list
            (node "替露西顶班" :subtitle "共需两班，每班消耗一枚骰；没有工资"
              :clocks (list (第二幕-期限钟 "报名截止" 12 9))
              :requires (list (req-die))
              :resolve (instant (outcome lucy-cover!)))
            (node "付代班费" :subtitle "支付剩余两班费用；已顶一班只需15美元"
              :clocks (list (第二幕-期限钟 "报名截止" 12 9))
              :requires (list (req-item "金钱" (if (= lucy-covered 1) 15 30)))
              :resolve (instant (outcome lucy-pay!))))
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
            (line "露西" "老板说必须有人守两班。我可以自己设法换班；你若帮忙，就得顶两班，或者花三十美元。")))))
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
        ((eq? (car args) 'wage-evidence?) wage-evidence?)
        ((eq? (car args) 'eddie-on-dock?) (eddie-on-dock?))
        ((eq? (car args) 'on-dock-work!)
          (if (and (wage-intro-open?) (第一幕 'eddie-met?)
                   (not wage-introduced?))
            (tell-wage!) #f))
        ((eq? (car args) 'on-cafe-work!)
          (cond
            ((and (lucy-intro-open?) (第一幕 'lucy-met?)
                   (not lucy-introduced?)) (tell-lucy!))
            ((and (lucy-open?) lucy-introduced?
                   (not lucy-boss-heard?)) (lucy-boss!))
            (else #f)))
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
                (list "lucy-introduced" lucy-introduced?)
                (list "lucy-covered" lucy-covered)
                (list "lucy-boss-heard" lucy-boss-heard?)
                (list "wage-checked" wage-checked?)
                (list "wage-evidence" wage-evidence?)))
        ((eq? (car args) 'load!)
          (let ((data (cadr args)))
            (set! wage-state (assoc-get data "wage" "未做"))
            (set! lucy-state (assoc-get data "lucy" "未做"))
            (set! wage-introduced? (assoc-get data "wage-introduced" #f))
            (set! lucy-introduced? (assoc-get data "lucy-introduced" #f))
            (set! lucy-covered (assoc-get data "lucy-covered" 0))
            (set! lucy-boss-heard? (assoc-get data "lucy-boss-heard" #f))
            (set! wage-checked? (assoc-get data "wage-checked" #f))
            (set! wage-evidence? (assoc-get data "wage-evidence" #f))
            (if (and (>= lucy-covered 0) (<= lucy-covered 2)
                 (or (not wage-evidence?) wage-checked?)) #t
              (error "第二幕：非法准备进度"))))
        (else (error "第二幕：未知消息"))))))
