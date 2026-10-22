;; 第二幕旧街与剧院生活：只复用骰子、金钱、冷静和 Dialogue
(define 第二幕生活
  (let ()
    (define visited-street? #f)
    (define invited? #f)
    (define met-frank? #f)
    (define visited-theater? #f)
    (define met-manager? #f)
    (define stage-worked? #f)
    ;; 主角的职业愿望通过工作里的人际碰撞表现，不单独设置“阅读旧名片”按钮。
    (define (dossier-at)
      (if (not (active?)) '()
        (append
          (if (or (not visited-street?) (not visited-theater?))
            (list (dossier "城里的新去处" :kind '城市 :primary #f
              :now (cond
                ((and (not visited-street?) (not visited-theater?))
                  "码头后面有旧街，大剧院也在招临工。都可以直接去看看。")
                ((not visited-street?)
                  "剧院的工作已经见过了。码头后面的旧街还有食堂和牌桌。")
                (else "旧街已经走过。大剧院前厅贴着临工招募告示。"))
              :where (if (not visited-theater?) "剧院" "码头居民区")))
            '())
          (list (dossier "想做个真正的侦探" :kind '打算 :primary #f
            :now (if met-manager?
              "经理认可了尼尔的工作，可他真正想接的是调查委托。城里或许还有别的机会。"
              "靠零工维持生活之余，尼尔仍在寻找一份真正的调查委托。")
            :where "")))))

    (define (active?) (and (= (第一幕 'act) 2) (not (第二章 'started?))))
    (define (on-dock-work!)
      (if (and (>= world-day 5) (<= world-day 9) (>= (第一幕 'eddie-stage) 2)
               (not invited?))
        (begin
          (set! invited? #t)
          (play-dialogue!
            (line "艾迪" "我收工以后常去旧街食堂，便宜又管饱。")
            (line "尼尔" "离这远吗？")
            (line "艾迪" "工棚后面就是。哪天有空我带你去认认门。")))
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
    (define (cards! outcome)
      (remove-item! "金钱" 5)
      (cond
        ((eq? outcome '赢) (add-item! "金钱" 12)
          (play-bubble! (line "牌客" "今晚手气不错，别把好运也赌进去。")))
        ((eq? outcome '平) (add-item! "金钱" 5)
          (play-bubble! (line "牌客" "算是打了个平手。")))
        (else (spend-composure! 1)
          (play-bubble! (line "牌客" "五块没了。还玩吗？")))))
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
      (if stage-worked? (error "剧院布景工作已经做完") #t)
      (set! stage-worked? #t)
      (set! visited-theater? #t)
      (add-item! "金钱" money)
      (if failure? (spend-composure! 1) #f)
      (if (not met-manager?)
        (begin
          (set! met-manager? #t)
          (play-dialogue!
            (line "经理" "你是今天新来的临工？")
            (line "尼尔" "我以前接过几件调查的小委托。眼下没活儿，只好来搬景片。")
            (line "经理" "调查？那得很会看人。先把这里的活儿做利索吧。")))
        #f))
    (define (nodes-at location)
      (if (not (active?)) '()
        (cond
          ((equal? location "码头居民区")
            (append
              (if (not visited-street?)
                (list (instant-action "看看旧街" street-intro!)) '())
              (if (and (>= world-day 8) (not met-frank?))
                (list (instant-action "食堂里的弗兰克" frank-intro!)) '())
              (list
                (node "旧街热饭" :name "吃顿热饭"
                  :subtitle "花3美元和一枚骰子，恢复2点冷静"
                  :requires (list (req-die) (req-item "金钱" 3))
                  :resolve (instant (outcome meal!)))
                (node "旧街牌桌" :name "打几手牌"
                  :subtitle "押5美元；输牌会损失冷静"
                  :risk '冒险 :risk-target (list 'composure "player")
                  :progress-target (list 'item "金钱")
                  :requires (list (req-die) (req-item "金钱" 5))
                  :resolve (roll 'sharpness
                    (outcome (lambda () (cards! '输)))
                    (outcome (lambda () (cards! '平)))
                    (outcome (lambda () (cards! '赢))))))))
          ((equal? location "剧院")
            (append
              (if (not visited-theater?)
                (list (instant-action "看看剧院招工告示" theater-intro!)) '())
              (if (not stage-worked?)
                (list (工作 "剧院布景临工" '稳妥 'sharpness
                  (outcome (lambda () (theater-job! 14 #f)))
                  (outcome (lambda () (theater-job! 8 #f)))
                  (outcome (lambda () (theater-job! 4 #t)))
                  "仅此一班；帮忙布置后台并认识经理"
                  :risk-target (list 'composure "player")
                  :progress-target (list 'item "金钱"))) '())))
          (else '()))))
    ;; 不因“新的一天进入地点”强制播放人物剧情。
    (define (arrivals-at location) '())
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
                  (list "stage-worked" stage-worked?)))
          ((eq? msg 'load!)
            (let ((data (cadr args)))
              (set! visited-street? (assoc-get data "street" #f))
              (set! invited? (assoc-get data "invited" #f))
              (set! met-frank? (assoc-get data "frank" #f))
              (set! visited-theater? (assoc-get data "theater" #f))
              (set! met-manager? (assoc-get data "manager" #f))
              (set! stage-worked? (assoc-get data "stage-worked" #f))))
          (else (error "第二幕生活：未知消息")))))))