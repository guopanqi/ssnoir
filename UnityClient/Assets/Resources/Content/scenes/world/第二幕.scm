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

(define 咖啡馆老板 (speaker "咖啡馆老板" :name (tr "老板" "Owner") :appearance '成年男性))

(define 第二幕
  (let ()
    ;; 第二幕保留露西夜校；艾迪工钱争执退出正式流程。
    (define lucy-state "未做")
    (define lucy-cover-clock (make-clock "去学校的时间" 4 'gauge "替她接下几段晚班，老板才肯让她去学校。还没接上的班，也可以请人代。" :beneficial-direction 'increase))
    (define lucy-result-known? #f)
    (define lucy-introduced-day 0)
    (define lucy-deadline-day 0)
    (define lucy-enrolled-day 0)
    (define lucy-school-known? #f)
    (define lucy-class-talk? #f)
    (define lucy-introduced? #f)
    (define (active?) (and (= (第一幕 'act) 2) (not (第二章 'started?))))

    (define (lucy-open?) (and (active?)
      (or (= lucy-deadline-day 0) (<= world-day lucy-deadline-day))
      (equal? lucy-state "未做")))

    ;; 世界只保存Scenario结果；进展和压力属于独立Scenario的回合。

    (define (lucy-at-cafe?)
      ;; 入学手续与第一堂课占两班；此后隔一天上课。不引入早晚时段。
      (if (= lucy-enrolled-day 0) #t
        (let ((elapsed (- world-day lucy-enrolled-day)))
          (not (or (= elapsed 1) (= elapsed 2)
                   (and (> elapsed 3) (= (modulo elapsed 2) 0)))))))
    (define (lucy-note)
      (cond
        ((not (lucy-at-cafe?)) "她去学校了，柜台下留着一本翻开的书。")
        ((and (equal? lucy-state "延后") lucy-result-known?)
          "她还在柜台忙碌，空时读书，准备下一期夜校。")
        (else "她在柜台后忙碌，把客人忘下的书收在一旁。")))

    (define (school-news!)
      (set! lucy-school-known? #t)
      (play-dialogue!
        (line 咖啡馆老板 "找露西？她去学校报到了，今天没排她的班。")
        (line 咖啡馆老板 "往后有些班她去上课，我另找人接。你照常来。")))
    (define (class-talk!)
      (set! lucy-school-known? #t)
      (set! lucy-class-talk? #t)
      (play-dialogue!
        (line "世界" "露西从打字课本里抽出一张练习纸，字迹深浅不一：NIEL INVESTIGATIONS。")
        (line "尼尔" "这是你的功课？")
        (line "露西" "老师让我们练打商业信头。我想来想去，就打了这个。")
        (line "尼尔" "你把 Neil 拼成了 Niel。")
        (line "露西" "所以我才要去上课啊，大侦探。")
        (line "世界" "她把纸推过来，忍不住先笑了。")))
    (define (lucy-fee) (* 10 (- 4 (lucy-cover-clock 'current))))
    (define (lucy-finish!)
      (if (and (lucy-open?) (lucy-cover-clock 'full?)) #t
        (error "夜校代班：目标尚未完成或已经结束"))
      (set! lucy-state "已入学")
      (set! lucy-enrolled-day world-day)
      (play-dialogue!
        (line "露西" "班都接好了！我明天就去学校交报名表。")
        (line "尼尔" "这回别再把它折进围裙兜里了。")
        (line "露西" "不会。这回连铅笔都削好了，放在大衣口袋里。")))
    (define (lucy-cover! progress)
      (if (and (lucy-open?) lucy-introduced?) #t (error "夜校代班：当前不可投入"))
      (lucy-cover-clock 'advance! progress)
      (if (lucy-cover-clock 'full?) (lucy-finish!) #f))
    (define (lucy-pay!)
      (if (and (lucy-open?) lucy-introduced?) #t (error "夜校代班：当前不可付款"))
      (lucy-cover-clock 'advance! (lucy-cover-clock 'remaining))
      (lucy-finish!))
    (define (lucy-missed!)
      (set! lucy-result-known? #t)
      (play-dialogue!
        (line "世界" "招生纸被揭了下来。露西把纸抹平，夹进旧字典。")
        (line "露西" "问了几个人，还是凑不齐班。学校这期已经开课了。")
        (line "尼尔" "还报得上吗？")
        (line "露西" "下期再试。我借了本打字课本，没客人的时候先认认键盘。")))
    ;; 私人的事情只在尼尔听到之后才进入手头的事。
    (define (lucy-story-pending?)
      (cond ((equal? lucy-state "已入学") (not lucy-class-talk?))
            ((equal? lucy-state "延后") (not lucy-result-known?))
            (else #t)))

    (define (known-matters)
      (append

        (if (or lucy-introduced? lucy-school-known? lucy-result-known?)
          (list (dossier "露西：夜校报名" :kind '人物 :primary (lucy-story-pending?)
            :status (if (lucy-story-pending?) '进行中 '了结)
            :now (cond
              ((and (equal? lucy-state "已入学") lucy-class-talk?)
                "露西把课堂上打的事务所招牌送给了你。她记得你想做调查。")
              ((equal? lucy-state "已入学")
                (if lucy-school-known?
                  (if (lucy-at-cafe?) "露西回店里了，忙完会说起学校的事。"
                    "露西去学校了，这班不在咖啡馆。")
                  "代班已安排好。露西准备去办入学手续，之后会少来几班。"))
              ((equal? lucy-state "延后")
                (if lucy-result-known? "代班没能安排齐，露西错过本期，正准备下一期。"
                  "报名截止了，还没见到露西问起后来怎么样。"))
              (else (string-append "报名" (第二幕-日期 lucy-deadline-day) "截止。代班还差"
                (number->string (- 4 (lucy-cover-clock 'current))) "段，可替班或付"
                (number->string (lucy-fee)) "美元请人。")))
            :where "街角咖啡馆")) '())))
    (define (nodes-at loc)
      (append
        (if (and (equal? loc "街角咖啡馆") (lucy-open?) lucy-introduced?)
          (list
            (clock-node "露西代班目标" (lucy-cover-clock 'render-data))
            (node "替露西顶班" :name "帮她顶班" :anchor "露西@咖啡馆"
              :subtitle "社交判定；坏无进展，中1格，好2格；不拿工资"
              :risk '稳妥 :progress-target (list 'clock "去学校的时间")
              :requires (list (req-die))
              :clocks (list (第二幕-期限钟 "报名截止" lucy-deadline-day lucy-introduced-day))
              :resolve (roll 'social
                (outcome (lambda () (lucy-cover! 0)))
                (outcome (lambda () (lucy-cover! 1)))
                (outcome (lambda () (lucy-cover! 2)))))
            (node "付代班费" :name "请人代班" :anchor "露西@咖啡馆"
              :progress-target (list 'clock "去学校的时间")
              :subtitle (string-append "付" (number->string (lucy-fee)) "美元安排剩余代班")
              :requires (list (req-item "金钱" (lucy-fee)))
              :clocks (list (第二幕-期限钟 "报名截止" lucy-deadline-day lucy-introduced-day))
              :resolve (instant (outcome lucy-pay!)))) '())))
    ;; 熟人通过日常工作或主动交谈告诉尼尔，并不是每日进地点自动播放。

    (define (tell-lucy!)
      (set! lucy-introduced? #t)
      (set! lucy-introduced-day world-day)
      ;; 报名窗口共四天，包含露西提出这件事的当天。
      (set! lucy-deadline-day (+ world-day 3))
      (play-dialogue!
        (line "世界" "没有客人的空档，露西擦着柜台，瞥了一眼账本底下的招生纸。老板正在改排班表。")
        (line "露西" "夜校有空位了。我得去办手续，可这几段晚班没人接。")
        (line 咖啡馆老板 "四段。找齐人接手，我就放你去上课。")
        (line "露西" (string-append "报名" (第二幕-日期 lucy-deadline-day) "截止。请人一段十块，我也在找肯换班的人。"))
        (line "露西" "你呢，尼尔？总不能一直跟我在这儿端咖啡吧。")
        (line "尼尔" "我想开间事务所，做私人调查。先把下一笔房费挣出来再说。")
        (line "露西" "那以后就是尼尔侦探了？坐在桌子后面，等人来敲门？")
        (line "尼尔" "有人肯敲就行。你的班，我能先顶一部分。")
        (line "露西" "你还得交房费呢。量力来，剩下的我再找人换。")))
    (define (arrivals-at location)
      (cond

        ((and (active?) (equal? location "街角咖啡馆")
              (not (lucy-at-cafe?)) (not lucy-school-known?))
          (list (arrival "露西去上课" school-news!)))
        ((and (active?) (equal? location "街角咖啡馆") (lucy-at-cafe?)
              (> lucy-enrolled-day 0) (>= world-day (+ lucy-enrolled-day 3))
              (not lucy-class-talk?))
          (list (arrival "露西从学校回来" class-talk!)))
        ((and (active?) (equal? location "街角咖啡馆")
              (equal? lucy-state "延后") (not lucy-result-known?))
          (list (arrival "露西没赶上本期" lucy-missed!)))
        (else '())))
    (define-turn-start-rule "第二幕事件期限"
      (lambda () #t)
      (lambda ()

        (if (and (> lucy-deadline-day 0) (> world-day lucy-deadline-day)
                 (equal? lucy-state "未做"))
          (set! lucy-state "延后") #f)))
    (lambda args
      (cond

        ((eq? (car args) 'lucy-at-cafe?) (lucy-at-cafe?))
        ((eq? (car args) 'lucy-arrival-pending?)
          (or (and (> lucy-enrolled-day 0) (lucy-at-cafe?) (>= world-day (+ lucy-enrolled-day 3)) (not lucy-class-talk?))
              (and (equal? lucy-state "延后") (not lucy-result-known?))))
        ((eq? (car args) 'lucy-note) (lucy-note))
        ((eq? (car args) 'lucy-enrolled-day) lucy-enrolled-day)

        ((eq? (car args) 'eddie-on-dock?) #t)

        ((eq? (car args) 'on-cafe-work!)
          (cond
            ((not (lucy-at-cafe?)) #f)
            ((and (active?) (> lucy-enrolled-day 0)
                  (>= world-day (+ lucy-enrolled-day 3)) (not lucy-class-talk?)) (class-talk!))
            ((and (lucy-open?) (第一幕 'lucy-met?)
                   (not lucy-introduced?)) (tell-lucy!))
            ((and (active?) (equal? lucy-state "延后") (not lucy-result-known?)) (lucy-missed!))
            (else #f)))
        ((eq? (car args) 'lucy-state) lucy-state)
        ((eq? (car args) 'dossier)
          (if (= (第一幕 'act) 2)
            (known-matters)
            '()))
        ((eq? (car args) 'nodes-at) (nodes-at (cadr args)))
        ((eq? (car args) 'arrivals-at) (arrivals-at (cadr args)))
        ((eq? (car args) 'save)
          (list
                (list "lucy-enrolled-day" lucy-enrolled-day)
                (list "lucy-introduced-day" lucy-introduced-day)
                (list "lucy-deadline-day" lucy-deadline-day)
                (list "lucy-school-known" lucy-school-known?) (list "lucy-class-talk" lucy-class-talk?)
                (list "version" 2)
                (list "lucy" lucy-state)
                (list "lucy-introduced" lucy-introduced?)
                (list "lucy-progress" (lucy-cover-clock 'save))
                (list "lucy-result-known" lucy-result-known?)))
        ((eq? (car args) 'load!)
          (let ((data (cadr args)))

            (set! lucy-enrolled-day (assoc-get data "lucy-enrolled-day" 0))
            (set! lucy-school-known? (assoc-get data "lucy-school-known" #f))
            (set! lucy-class-talk? (assoc-get data "lucy-class-talk" #f))

            (set! lucy-state (assoc-get data "lucy" "未做"))

            (set! lucy-introduced? (assoc-get data "lucy-introduced" #f))
            ;; 旧存档沿用原截止日；新存档记录露西提出报名的日期。
            (set! lucy-introduced-day (assoc-get data "lucy-introduced-day"
              (if lucy-introduced? 6 0)))
            (set! lucy-deadline-day (assoc-get data "lucy-deadline-day"
              (if lucy-introduced? 12 0)))
            ;; 旧版已完成的入学不回退；早期替班次数转成同量进展，不重复支付。
            (lucy-cover-clock 'load! (if (equal? lucy-state "已入学") 4
              (assoc-get data "lucy-progress" (min 4 (assoc-get data "lucy-covered" 0)))))
            (if (and (equal? lucy-state "已入学") (= lucy-enrolled-day 0))
              (set! lucy-enrolled-day (max 1 (- world-day 3))) #f)

            (set! lucy-result-known? (assoc-get data "lucy-result-known" #f))
            (if (and (>= lucy-enrolled-day 0)
                 (member? lucy-state '("未做" "已入学" "延后"))
                 (if (equal? lucy-state "已入学")
                   (and (lucy-cover-clock 'full?) (> lucy-enrolled-day 0))
                   (= lucy-enrolled-day 0))) #t
              (error "第二幕：剧情状态无效，请从本轮内容的新档开始"))))
        (else (error "第二幕：未知消息"))))))
