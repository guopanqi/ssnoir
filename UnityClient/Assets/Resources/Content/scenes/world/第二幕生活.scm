;; 第二幕旧街与剧院生活：只复用骰子、金钱、冷静和 Dialogue
(define 剧院运货人 (speaker "剧院运货人" :name (tr "运货人" "Delivery worker") :appearance '成年男性))

(define 旧街食堂老板 (speaker "旧街食堂老板" :name (tr "老板" "Owner")))

(define 第二幕生活
  (let ()
    (define visited-street? #f)
    (define met-frank? #f)
    (define met-manager? #f)
    (define dream-known? #f)
    (define contract-state "未提起")
    (define warehouse-known? #f)
    (define expiry-known? #f)
    (define (contract-open?)
      (and (active?) (<= world-day 11)
        (member? contract-state '("待答复" "已接下"))))
    (define (meet-manager!)
      (if met-manager? #f
        (begin
          (set! met-manager? #t)
          (play-dialogue!
            (line "夜莺" "这是尼尔。我在酒馆唱歌的时候就认识他。")
            (line "经理" "难怪你一散场就往这边看。你好，尼尔。")
            (line "尼尔" "刚才那一段，她在酒馆也唱过。这里听起来很不一样。")
            (line "经理" "往后还有得忙。你们聊，我去找舞台监督。")))))
    (define (talk-dream!)
      (set! dream-known? #t)
      (play-dialogue!
        (line "经理" "你也在码头做工？")
        (line "尼尔" "哪里有活就去哪儿。不过我想做的是调查，替人把事情查清楚。")
        (line "经理" "找你的人多吗？")
        (line "尼尔" "还不多。总得有人肯把事情交给我，才能慢慢做下去。"))
      (if (<= world-day 11)
        (begin
          (set! contract-state "待答复")
          (play-dialogue!
            (line "经理" "我这儿还真有件事。演出服的收货单签了字，衣服却没进后台。")
            (line "经理" (string-append "赞助人的试唱在" (第二幕-日期 11) "。那天收工前，我得拿到这批衣服。"))
            (line "经理" "把演出服带回来，四十五美元。单上写着后门，也许落在后面的旧库房。")
            (line "经理" "码头的运货人也许知道。他熟悉这些库房，花钱请他找也行。到时候没拿到衣服，我就另找人。")))
        (play-dialogue! (line "经理" "这次演出的杂事已经有人接了。以后有合适的事，我们再谈。"))))
    (define (accept-contract!)
      (set! contract-state "已接下")
      (play-dialogue!
        (line "经理" "衣服装在一只写着剧院名字的箱子里。旧库房在后台，运货人常在码头账房旁等活。")
        (line "尼尔" "我会把衣服带回来。")))
    (define (finding?)
      (and (contract-open?) (equal? contract-state "已接下") (= (item-count "演出服") 0)))
    (define (receive-costume!)
      (if (finding?) #t (error "演出服：已经找到或当前没有委托"))
      (add-item! "演出服" 1))
    (define (search-costume! grade)
      (if (finding?) #t (error "演出服：当前不可搜找"))
      (cond
        ((eq? grade '坏)
          (spend-composure! 1)
          (play-bubble! (line "尼尔" "标签全泡花了。再找找别处。" :at "剧院-后台")))
        (else
          (if (eq? grade '中) (spend-composure! 1) #f)
          (receive-costume!)
          (play-bubble! (line "尼尔" "剧院的名字，尺码也对。就是这箱。" :at "剧院-后台")))))
    (define (ask-carrier! grade)
      (if (finding?) #t (error "演出服：当前不可打听"))
      (cond
        ((eq? grade '坏)
          (spend-composure! 1)
          (play-dialogue! (line 剧院运货人 "我这几天送了那么多车。你说哪一箱？我真记不清。")))
        (else
          (set! warehouse-known? #t)
          (play-dialogue!
            (line 剧院运货人 "那天下雨，侧门锁着。我把它送到后台旧库房，最里面靠窗那一排。"))
          (if (eq? grade '好)
            (begin
              (receive-costume!)
              (focus-node! "剧院")
              (play-dialogue!
                (line 剧院运货人 "我正好要过去取空箱。上车吧，一起把它搬出来。")
                (line "世界" "尼尔跟车回到旧库房，找到了标着剧院名字的演出服箱。"))) #f))))
    (define (collect-costume!)
      (if (and (finding?) warehouse-known?) #t (error "演出服：尚不知道确切库位"))
      (receive-costume!)
      (play-bubble! (line "尼尔" "靠窗这一排……找到了。" :at "剧院-后台")))
    (define (hire-carrier!)
      (receive-costume!)
      (focus-node! "剧院")
      (play-dialogue!
        (line 剧院运货人 "二十五块，替你把这批货重新找齐送回去。我认得那些库房。")
        (line "世界" "运货人带着尼尔找到衣箱，重新核过标签，将演出服交到他手里。")))
    (define (deliver-contract!)
      (if (and (contract-open?) (equal? contract-state "已接下") (>= (item-count "演出服") 1))
        #t (error "演出服委托：需要实际交付演出服"))
      ;; 演出服由交付节点的资源槽扣除，不在效果中重复扣除。
      (set! contract-state "完成")
      (add-item! "金钱" 45)
      (play-dialogue!
        (line "世界" "经理打开衣箱，按清单一件件点过，把箱子推给后台的人。")
        (line "经理" "都在。四十五美元，你收着。以后有需要查清、办妥的事情，我还会找你。")))
    ;; 主角的职业愿望通过工作里的人际碰撞表现，不单独设置“阅读旧名片”按钮。
    (define (contract-story-pending?)
      (or (contract-open?)
          (and (equal? contract-state "另找他人") (not expiry-known?))))
    (define (dossier-at)
      (if (not (active?)) '()
        (append
          (if (or (not visited-street?) (not (第一幕 'rehearsal-seen?)))
            (list (dossier "城里的新去处" :kind '城市 :primary #f
              :now (cond
                ((and (not visited-street?) (not (第一幕 'rehearsal-seen?)))
                  "夜莺已去大剧院。码头后面是工友住的旧街。")
                ((not visited-street?)
                  "你看过夜莺的彩排。码头后面的旧街还有食堂和牌桌。")
                (else "旧街已经走过。你还答应了去大剧院听夜莺彩排。"))
              :where (if (not (第一幕 'rehearsal-seen?)) "剧院" "码头居民区")))
            '())
          (list (dossier "想做个真正的侦探" :kind '打算 :primary #f
            :now (cond
              ((equal? contract-state "完成") "第一笔调查报酬已经到手，经理愿意继续把查账的工作交给你。")
              (dream-known? "你已经告诉经理自己想做私人调查，今后要靠具体的工作建立信誉。")
              (met-manager? "你已认识剧院经理，可以在剧院与他聊聊自己的打算。")
              (else "靠零工维持生活之余，尼尔仍在寻找一份真正的调查委托。"))
            :where (if met-manager? "剧院" "")))
          (if (equal? contract-state "未提起") '()
            (list (dossier "经理：找回演出服" :kind '委托 :primary (contract-story-pending?)
              :status (if (contract-story-pending?) '进行中 '了结)
              :now (cond
                ((equal? contract-state "完成")
                  "演出服已交给经理，收到45美元报酬。经理认可了尼尔办事的能力，说以后有需要还会找他。")
                ((equal? contract-state "另找他人") (if expiry-known?
                  "经理已另找人处理演出服的事，这次没有得到调查报酬。"
                  "委托期限已过，还没与经理谈过后来的处理。"))
                ((equal? contract-state "待答复") (string-append
                  "经理请你找回演出服，报酬45美元。" (第二幕-日期 11)
                  "收工前交付，可以到剧院答复。"))
                (else (string-append "目标：交回一批演出服。" (第二幕-日期 11)
                  "收工前交付，报酬45美元。"
                  (cond ((>= (item-count "演出服") 1) "衣服已经拿到，可以回剧院交付。")
                    (warehouse-known? "运货人说箱子在剧院后台旧库房靠窗的一排。")
                    (else "经理说衣箱可能在后台旧库房；码头账房旁的运货人也许知道，或可花25美元请他找回。")))))
              :steps (if (equal? contract-state "已接下")
                (list (step "带回演出服并交给经理" #f)) '())
              :where "剧院"))))))

    (define (active?) (and (= (第一幕 'act) 2) (not (第二章 'started?))))
    (define (street-intro!)
      (set! visited-street? #t)
      (play-dialogue!
        (line "世界" "工棚后面是旧街。食堂、牌桌和零工挤在一起。")
        (line "世界" "码头工人常在这里收工、吃饭，谈论别人的生活。")))
    (define (frank-intro!)
      (set! met-frank? #t)
      (play-dialogue!
        (line "艾迪" "弗兰克，这是尼尔。工棚那事他也在。")
        (line "弗兰克" "听说了。你替那孩子说话没错，揪住工头的领子可拿不回饭碗。")
        (line "尼尔" "工头不认账的时候，还能怎么办？")
        (line "弗兰克" "把同一班的人找齐，留下签过名的工时。别让他把一群人的工钱，说成一个人的脾气。")
        (line "艾迪" "你说得容易。")
        (line "弗兰克" "吃完饭，我陪你去找那几个工友。")))
    (define (meal!)
      (restore-actor-composure! 'player 2)
      (play-bubble! (line 旧街食堂老板 "热汤在这儿，面包自己拿。" :at "码头居民区")))
    (define (cards! outcome)
      (cond
        ((eq? outcome '赢) (add-item! "金钱" 12)
          (play-bubble! (line "牌客" "今晚手气不错，别把好运也赌进去。" :at "码头居民区")))
        ((eq? outcome '平) (add-item! "金钱" 5)
          (play-bubble! (line "牌客" "算是打了个平手。" :at "码头居民区")))
        (else (spend-composure! 1)
          (play-bubble! (line "牌客" "五块没了。还玩吗？" :at "码头居民区")))))
    (define (nodes-at location)
      (if (not (active?)) '()
        (cond
          ((equal? location "码头居民区")
            (append
              (if (and (not met-frank?) (第二幕 'eddie-visited?))
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
              (if (and (equal? contract-state "另找他人") (not expiry-known?))
                (list (instant-action "问委托后续" (lambda ()
                  (set! expiry-known? #t)
                  (play-dialogue!
                    (line "经理" "衣服已经派别人去找了。试演不能等。")
                    (line "尼尔" "是我没赶上。")
                    (line "经理" "以后接活儿，先算好手头的时间。"))))) '())
              (if (and met-manager? (not dream-known?))
                (list (node "谈谈自己的打算" :name "和经理谈打算"
                  :subtitle "向经理说起想做私人调查的打算"
                  :button-text "交谈"
                  :resolve (instant (outcome talk-dream!)))) '())
              (if (and (contract-open?) (equal? contract-state "待答复"))
                (list (node "接下演出服调查"
                  :subtitle "带回演出服，报酬45美元；逾期另找人"
                  :clocks (list (第二幕-期限钟 "委托截止" 11 7))
                  :resolve (instant (outcome accept-contract!)))) '())
              (if (and (contract-open?) (equal? contract-state "已接下"))
                (list
                  (note-node "演出服交付目标" "交回演出服"
                    (string-append (第二幕-日期 11) "收工前交付一批演出服，报酬45美元。")
                    (list 'clock "演出服" (min 1 (item-count "演出服")) 1 'readout "交付需要实际物品。"))
                  (node "交付演出服" :subtitle "交出一批演出服，领取45美元"
                    :requires (list (req-item "演出服" 1))
                    :clocks (list (第二幕-期限钟 "委托截止" 11 7))
                    :resolve (instant (outcome deliver-contract!)))) '())
              (if (finding?)
                (list (node "旧库房找衣箱" :name (if warehouse-known? "取回演出服" "搜找演出服")
                  :anchor "剧院-后台"
                  :subtitle (if warehouse-known? "库位已知，直接取回演出服" "搜旧库房；中、坏结果损失1冷静")
                  :requires (if warehouse-known? '() (list (req-die)))
                  :risk (if warehouse-known? #f '危险)
                  :risk-target (if warehouse-known? #f (list 'composure "player"))
                  :progress-target (if warehouse-known? #f (list 'item "演出服"))
                  :resolve (if warehouse-known? (instant (outcome collect-costume!))
                    (roll 'sharpness
                      (outcome (lambda () (search-costume! '坏)))
                      (outcome (lambda () (search-costume! '中)))
                      (outcome (lambda () (search-costume! '好))))))) '())
              ))
          ((equal? location "码头")
            (if (finding?)
              (append
                (if warehouse-known? '()
                  (list (node "问运货人衣箱" :name "打听演出服" :anchor "码头-账房"
                    :subtitle "询问衣箱落在哪里；失手损失1冷静"
                    :risk '冒险 :risk-target (list 'composure "player")
                    :requires (list (req-die))
                    :resolve (roll 'social
                      (outcome (lambda () (ask-carrier! '坏)))
                      (outcome (lambda () (ask-carrier! '中)))
                      (outcome (lambda () (ask-carrier! '好)))))))
                (list (node "请人找回衣箱" :name "请人找回衣服" :anchor "码头-账房"
                  :subtitle "花25美元请运货人找回，不耗骰子"
                  :requires (list (req-item "金钱" 25))
                  :resolve (instant (outcome hire-carrier!))))) '()))
          (else '()))))
    ;; 初次入场介绍只发生一次，不占用一张“看看”动作。
    (define (arrivals-at location)
      (if (and (equal? location "码头居民区") (not visited-street?))
        (list (arrival "初到旧街" street-intro!)) '()))
    (define-turn-start-rule "经理另外找人"
      (lambda () (and (> world-day 11) (member? contract-state '("待答复" "已接下"))))
      (lambda () (set! contract-state "另找他人")))
    (lambda args
      (let ((msg (car args)))
        (cond
          ((eq? msg 'meet-manager!) (meet-manager!))
          ((eq? msg 'dossier) (dossier-at))
          ((eq? msg 'nodes-at) (nodes-at (cadr args)))
          ((eq? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((eq? msg 'met-frank?) met-frank?)
          ((eq? msg 'save)
            (list (list "street" visited-street?)
                  (list "frank" met-frank?)
                  (list "manager" met-manager?)
                  (list "dream-known" dream-known?) (list "contract" contract-state)
                  (list "warehouse-known" warehouse-known?)
                  (list "expiry-known" expiry-known?)))
          ((eq? msg 'load!)
            (let ((data (cadr args)))
              (set! visited-street? (assoc-get data "street" #f))
              (set! met-frank? (assoc-get data "frank" #f))
              (set! met-manager? (assoc-get data "manager" #f))
              (set! dream-known? (assoc-get data "dream-known" #f))
              (set! contract-state (assoc-get data "contract" "未提起"))
              (set! warehouse-known? (assoc-get data "warehouse-known" #f))
              (set! expiry-known? (assoc-get data "expiry-known" #f))
              (if (member? contract-state '("未提起" "待答复" "已接下" "完成" "另找他人")) #t
                (error "经理委托：非法存档阶段"))))
          (else (error "第二幕生活：未知消息")))))))
