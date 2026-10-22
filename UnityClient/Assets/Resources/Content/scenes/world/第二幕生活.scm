;; 第二幕旧街与剧院生活：只复用骰子、金钱、冷静和 Dialogue
(define 第二幕生活
  (let ()
    (define visited-street? #f)
    (define invited? #f)
    (define met-frank? #f)
    (define visited-theater? #f)
    (define met-manager? #f)
    (define soup-day 0)
    (define remembered-card? #f)
    (define remembered-manager? #f)
    ;; 尼尔自己的愿望不是必须完成的任务，也没有新增经验值。
    (define (read-old-card!)
      (set! remembered-card? #t)
      (play-dialogue!
        (line "世界" "抽屉底下压着几张旧名片，边角已经卷了。")
        (line "世界" "上面写着：尼尔，私人调查。")
        (line "尼尔" "来城里都一年了。找到的倒都是搬货、跑腿和替人收拾麻烦的活儿。")
        (line "尼尔" "总会有人真需要我查点什么吧。")))
    (define (manager-thought!)
      (set! remembered-manager? #t)
      (play-dialogue!
        (line "世界" "尼尔把从剧院领来的工钱和旧名片放到一处。")
        (line "尼尔" "终于有人觉得我做事可靠。可光会搬景片，算哪门子侦探？")
        (line "尼尔" "下次要是有人找我查事，可得认真接下来。")))
    (define (dossier-at)
      (if (not (active?)) '()
        (append
          (if (or (not visited-street?) (not visited-theater?))
            (list (dossier "城里的新去处" :kind '城市 :primary #f
              :now (cond
                ((and (not visited-street?) (not visited-theater?))
                  "码头后面有旧街，大剧院也在招临工。都可以直接去看看。")
                ((not visited-street?)
                  "剧院的工作已经见过了。码头后面的旧街还有食堂和零活。")
                (else "旧街已经走过。大剧院前厅还在招募临时工。"))
              :where (if (not visited-theater?) "剧院" "码头居民区")))
            '())
          (list (dossier "想做个真正的侦探" :kind '打算 :primary #f
            :now (cond
              ((not remembered-card?) "旅馆抽屉里还有从前印的调查名片。尼尔不想一辈子只做零工。")
              (met-manager?
                "经理愿意再雇尼尔。得到认可是一回事，真正接到调查委托又是另一回事。")
              (else "找一份真正需要调查的委托。眼下先靠零工生活，也留心城里的机会。"))
            :where (if remembered-card? "" "家"))))))

    (define (active?) (and (= (第一幕 'act) 2) (not (第二章 'started?))))
    (define (on-dock-work!)
      (if (and (active?) (>= world-day 8) (>= (第一幕 'eddie-stage) 2)
               (not invited?))
        (begin
          (set! invited? #t)
          (play-dialogue!
            (line "艾迪" "今晚去旧街食堂吃饭吧，便宜又管饱。")
            (line "尼尔" "离这远吗？")
            (line "艾迪" "工棚后面就是。你随时可以过去。")))
        #f))
    (define (street-intro!)
      (set! visited-street? #t)
      (play-dialogue!
        (line "世界" "工棚后面是旧街。食堂、牌桌和零工挤在一起。")
        (line "世界" "码头工人常在这里收工、吃饭，谈论别人的生活。")))
    (define (frank-intro!)
      (set! met-frank? #t)
      (play-dialogue!
        (line "世界" "一名年老工人没钱付饭钱，老板拿着账本叹气。")
        (line "弗兰克" "把账给我。他先吃饭。")
        (line "老板" "你不能每回都替人付。")
        (line "弗兰克" "饿着肚子上不了工。先把人顾好。")))
    (define (meal!)
      (remove-item! "金钱" 3)
      (restore-actor-composure! 'player 2)
      (play-bubble! (line "老板" "热汤在这儿，面包自己拿。")))
    (define (soup!)
      (if (= soup-day world-day) (error "今日救济餐已领取") #t)
      (set! soup-day world-day)
      (restore-actor-composure! 'player 1)
      (play-dialogue!
        (line "老板" "锅里还有汤，坐下吃吧。")
        (line "尼尔" "今天还没挣多少。")
        (line "老板" "有余力时再帮别人就好。")))
    (define (cards! outcome)
      (remove-item! "金钱" 5)
      (cond
        ((eq? outcome '赢) (add-item! "金钱" 12)
          (play-bubble! (line "牌客" "今晚手气不错，别把好运也赌进去。")))
        ((eq? outcome '平) (add-item! "金钱" 5)
          (play-bubble! (line "牌客" "算是打了个平手。")))
        (else (spend-composure! 1)
          (play-bubble! (line "牌客" "五块没了。还玩吗？")))))
    (define (odd-job! money failure?)
      (add-item! "金钱" money)
      (if failure? (spend-composure! 1) #f))
    (define (theater-intro!)
      (set! visited-theater? #t)
      (play-dialogue!
        (line "世界" "大剧院前厅贴着临工招募纸。")
        (line "工作人员" "后台缺人搬景片，还要人递剧本。")
        (line "工作人员" (cond
          ((< world-day 10) (string-append (第二幕-日期 10) "起，赞助人连着两天来看试演，现场会更忙。"))
          ((<= world-day 11) (string-append "赞助人正在看试演，" (第二幕-日期 11) "结束。后台忙得很。"))
          (else "赞助人试演已经结束，接下来还要准备首演。")))))
    (define (theater-job! money failure?)
      (add-item! "金钱" money)
      (if failure? (spend-composure! 1) #f)
      (if (not met-manager?)
        (begin
          (set! met-manager? #t)
          (play-dialogue!
            (line "经理" "你是今天新来的临工？")
            (line "尼尔" "听说这里缺人。")
            (line "经理" "做事利索就行，以后还可以来。")))
        #f))
    (define (nodes-at location)
      (if (not (active?)) '()
        (cond
          ((equal? location "家")
            (append
              (if (not remembered-card?)
                (list (node "抽屉里的旧名片" :name "看看旧名片"
                  :subtitle "尼尔过去想靠什么生活？"
                  :resolve (instant (outcome read-old-card!))))
                '())
              (if (and met-manager? remembered-card? (not remembered-manager?))
                (list (node "剧院的工资和名片" :name "想想今后的工作"
                  :subtitle "经理记住了尼尔，他却还想做真正的调查"
                  :resolve (instant (outcome manager-thought!))))
                '())))

          ((equal? location "码头居民区")
            (list
              (node "旧街热饭" :name "吃顿热饭"
                :subtitle "花3美元和一枚骰子，恢复2点冷静"
                :requires (list (req-die) (req-item "金钱" 3))
                :resolve (instant (outcome meal!)))
              (node "旧街救济" :name "领救济餐"
                :subtitle (if (= soup-day world-day) "今天已经吃过"
                  "每天一次，消耗骰子恢复1点冷静")
                :disabled (= soup-day world-day)
                :requires (list (req-die))
                :resolve (instant (outcome soup!)))
              (node "旧街牌桌" :name "打几手牌" :subtitle "押5美元，可能赢12美元，也可能输光"
                :risk '冒险 :risk-target (list 'composure "player")
                :progress-target (list 'item "金钱")
                :requires (list (req-die) (req-item "金钱" 5))
                :resolve (roll 'sharpness
                  (outcome (lambda () (cards! '输)))
                  (outcome (lambda () (cards! '平)))
                  (outcome (lambda () (cards! '赢)))))
              (工作 "街头跑腿" '稳妥 'social
                (outcome (lambda () (odd-job! 12 #f)))
                (outcome (lambda () (odd-job! 6 #f)))
                (outcome (lambda () (odd-job! 2 #t)))
                "替旧街商铺送货，收入不高，但能认识人"
                :risk-target (list 'composure "player")
                :progress-target (list 'item "金钱"))))
          ((equal? location "剧院")
            (list (工作 "剧院临工" '稳妥 'sharpness
              (outcome (lambda () (theater-job! 14 #f)))
              (outcome (lambda () (theater-job! 8 #f)))
              (outcome (lambda () (theater-job! 4 #t)))
              "搬景片、送道具，熟悉大剧院"
              :risk-target (list 'composure "player")
              :progress-target (list 'item "金钱"))))
          (else '()))))
    (define (arrivals-at location)
      (if (not (active?)) '()
        (append
          (if (and (equal? location "码头居民区") (not visited-street?))
            (list (arrival "旧街初访" street-intro!)) '())
          (if (and (equal? location "码头居民区")
                   (>= world-day 8) (not met-frank?))
            (list (arrival "食堂里的账单" frank-intro!)) '())
          (if (and (equal? location "剧院") (not visited-theater?))
            (list (arrival "剧院招工" theater-intro!)) '()))))
    (lambda args
      (let ((msg (car args)))
        (cond
          ((eq? msg 'dossier) (dossier-at))
          ((eq? msg 'nodes-at) (nodes-at (cadr args)))
          ((eq? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((eq? msg 'on-dock-work!) (on-dock-work!))
          ((eq? msg 'theater-seen?) visited-theater?)
          ((eq? msg 'save)
            (list (list "street" visited-street?)
                  (list "invited" invited?)
                  (list "frank" met-frank?)
                  (list "theater" visited-theater?)
                  (list "manager" met-manager?)
                  (list "soup-day" soup-day)
                  (list "remembered-card" remembered-card?)
                  (list "remembered-manager" remembered-manager?)))
          ((eq? msg 'load!)
            (let ((data (cadr args)))
              (set! visited-street? (assoc-get data "street" #f))
              (set! invited? (assoc-get data "invited" #f))
              (set! met-frank? (assoc-get data "frank" #f))
              (set! visited-theater? (assoc-get data "theater" #f))
              (set! met-manager? (assoc-get data "manager" #f))
              (set! soup-day (assoc-get data "soup-day" 0))
              (set! remembered-card? (assoc-get data "remembered-card" #f))
              (set! remembered-manager? (assoc-get data "remembered-manager" #f))
              (if (and remembered-manager? (not remembered-card?))
                (error "第二幕生活：回想经理前必须看过名片") #t)))
          (else (error "第二幕生活：未知消息")))))))