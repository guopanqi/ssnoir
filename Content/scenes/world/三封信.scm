;; scenes/world/三封信.scm - 第一章「三封信」主线模块
;;
;; 设计见 docs/第一章·三封信.md。这个闭包拥有第一章的全部故事状态,
;; 世界协调器只负责日历、地点与存档转发,不解释故事。
;;
;; 章节的两个日子是钉死的:
;;   交割日   = 登门当天 + 3(信上写的期限),小节一的到期
;;   首演之夜 = 第 22 天,全章可见,整章的总长上限
;; 中间的分界浮动:小节二由账单结清推进,越晚办完,小节三的准备天数越少。

(define three-letters
  (let ()
    ;; ── 常量 ────────────────────────────────────────
    (define prepayment 30)          ; 她攒了一阵子的钱,刚够几天房租
    (define letter-deadline 3)      ; 信上写的期限:三天后
    (define premiere-day 22)        ; 首演之夜,全章不变
    (define dock-prep-max 6)        ; 码头准备满格(三项各好结果 +2)

    ;; ── 小节二·老街 ─────────────────────────────────
    (define negative-target 3)      ; 「底片在哪」满格
    (define familiar-max 3)         ; 老街熟脸满格;戒心随它递减
    (define negatives-price 260)    ; 买回底片与照片的总价
    (define manager-share 100)      ; 经理愿意出的那一部分
    (define spread-max 4)           ; 「照片在老街传开」的格数
    (define spread-interval 3)      ; 每几天推一格
    (define trust-threshold 3)      ; 够这个数,小节三才能改动她的登台安排
    (define manager-fee-good 220)   ; 结算报酬:办得干净
    (define manager-fee-fair 150)   ; 勉强
    (define manager-fee-poor 80)    ; 难看(含传开超时)

    ;; ── 平静期与小节三 ───────────────────────────────
    (define quiet-days 3)           ; 小节二结算后,第三封信隔几天到
    (define prep-count 5)           ; 五项准备,骰子不够做全部

    ;; ── 状态 ────────────────────────────────────────
    ;; 0=未开场 1=小节一·交割 2=小节二·老街 3=平静期 4=小节三 5=首演之后
    ;; 后续小节在各自批次接入,不预留空壳。
    (define story-stage 0)
    (define delivery-day 0)         ; 交割日的世界日,开场当天算出
    (define delivery-pending? #f)   ; 交割日已到、尚未处理
    (define delivery-result "未定") ; 未定 / 好 / 中 / 坏
    (define dock-prep 0)            ; 码头准备 0..dock-prep-max
    (define scouted? #f)            ; 踩点已完成(无论好中)
    (define mailbox-checked? #f)    ; 邮箱周围已看过
    (define locals-asked? #f)       ; 地形已打听
    (define condition-level 0)      ; 夜莺处境 0=稳定 1=不安 2=受伤
    (define trust 0)                ; 她对你的信任
    (define negative-progress 0)    ; 「底片在哪」0..negative-target
    (define familiar 0)             ; 老街熟脸 0..familiar-max
    (define spread 0)               ; 「照片在老街传开」0..spread-max
    (define spread-day 0)           ; 上一次推进传开钟的世界日
    (define escorted-day 0)         ; 夜莺最近一次陪你走老街的世界日
    (define material-settled? #f)   ; 底片与照片是否已经了结
    (define settle-route "无")      ; 交易 / 关系 / 强制 / 传开
    (define settle-quality "无")    ; 好 / 中 / 坏
    (define lyon-fate "无")         ; 逃走 / 被释放 / 被扣押
    (define settled-day 0)          ; 小节二结算当天的世界日
    (define found-lyon? #f)         ; 准备①：找到莱恩,确认有无同伙
    (define police-guard? #f)       ; 准备②：警方到场
    (define backstage-checked? #f)  ; 准备③：后台出口已封
    (define staging-changed? #f)    ; 准备④：登台安排已改
    (define decoy-set? #f)          ; 准备⑤：诱饵已放出
    (define premiere-done? #f)      ; 首演之夜已结算
    (define scene-flags '())

    ;; ── flag 登记 ───────────────────────────────────
    ;; 未登记的 flag 直接报错,避免拼错字悄悄变成一个新状态。
    (define (flag-id flag)
      (cond
        ((or (equal? flag '交割已结算) (equal? flag "交割已结算")) "交割已结算")
        ((or (equal? flag '伤后探望) (equal? flag "伤后探望")) "伤后探望")
        ((or (equal? flag '第二封信) (equal? flag "第二封信")) "第二封信")
        ((or (equal? flag '她的过去) (equal? flag "她的过去")) "她的过去")
        ((or (equal? flag '留下的信) (equal? flag "留下的信")) "留下的信")
        ((or (equal? flag '第三封信) (equal? flag "第三封信")) "第三封信")
        ((or (equal? flag '她不取消) (equal? flag "她不取消")) "她不取消")
        ((or (equal? flag '薇拉) (equal? flag "薇拉")) "薇拉")
        ((or (equal? flag '结案) (equal? flag "结案")) "结案")
        (else (error "三封信 flag 未登记"))))

    (define (has-flag? flag) (member? (flag-id flag) scene-flags))
    (define (set-flag! flag)
      (let ((id (flag-id flag)))
        (if (not (member? id scene-flags))
            (set! scene-flags (cons id scene-flags))
            #f)))

    (define (normalize-flags flags)
      (if (null? flags)
          '()
          (cons (flag-id (car flags)) (normalize-flags (cdr flags)))))

    ;; ── 派生 ────────────────────────────────────────
    (define (days-to-delivery) (max 0 (- delivery-day world-day)))
    (define (days-to-premiere) (max 0 (- premiere-day world-day)))
    (define (beat1-open?) (and (= story-stage 1) (not delivery-pending?)))

    ;; 她在酒馆驻唱的日子。受伤时不上台——这份便宜随她一起消失。
    ;; 平静期起她去剧院排练,酒馆里她出现的频率降低,这个变化本身就是叙事。
    (define (singer-present?)
      (and (>= story-stage 1) (<= story-stage 2) (< condition-level 2)))

    ;; 老街从小节一结算后开放:酒馆、码头居民区都在这一片。
    (define (old-street-open?) (>= story-stage 2))
    ;; 小节二真正开始要等第二封信寄到剧院、经理找上门。
    (define (beat2-open?)
      (and (= story-stage 2) (has-flag? '第二封信) (not material-settled?)))
    (define (negatives-located?) (>= negative-progress negative-target))
    (define (trust-met?) (>= trust trust-threshold))

    ;; 剧院在小节二结算后开放,一直留到章末。
    (define (theater-open?) (>= story-stage 3))
    ;; 平静期:主线没有新压力。第三封信在结算后第 quiet-days 天到。
    (define (quiet-period?) (and (= story-stage 3) (not (has-flag? '第三封信))))
    (define (third-letter-due?)
      (and (= story-stage 3) (not (has-flag? '第三封信))
           (>= (- world-day settled-day) quiet-days)))
    (define (beat3-open?) (and (= story-stage 4) (not premiere-done?)))
    (define (prep-done)
      (+ (if found-lyon? 1 0) (if police-guard? 1 0) (if backstage-checked? 1 0)
         (if staging-changed? 1 0) (if decoy-set? 1 0)))

    ;; 老街的戒心:你穿得不像这里的人,你替一个走了就没回来的姑娘办事。
    ;; 混脸熟能把它磨掉,是可见修正,不是隐藏难度。
    (define (street-modifiers)
      (let ((wary (- 2 familiar)))
        (if (> wary 0)
            (list (modifier (- wary) "老街的戒心"))
            '())))

    (define (add-familiar! n)
      (set! familiar (min familiar-max (+ familiar n)))
      (sync-globals!))

    (define (gain-trust! n)
      (set! trust (+ trust n))
      (sync-globals!))

    (define (condition-label)
      (cond
        ((= condition-level 0) "稳定")
        ((= condition-level 1) "不安")
        ((= condition-level 2) "受伤")
        (else (error "三封信：夜莺处境等级非法"))))

    (define (sync-globals!)
      (set-global! '第一章阶段 story-stage)
      (set-global! '夜莺处境 (condition-label))
      (set-global! '码头准备 dock-prep)
      (set-global! '交割结果 delivery-result)
      (set-global! '老街熟脸 familiar)
      (set-global! '照片传开 spread)
      (set-global! '底片去向 settle-route)
      (set-global! '小节二结果 settle-quality)
      (set-global! '莱恩下落 lyon-fate)
      (set-global! '夜莺信任达标 (trust-met?))
      ;; 首演交锋读这五项决定起始场面（改场面，不改骰子）。
      (set-global! '准备-找到莱恩 found-lyon?)
      (set-global! '准备-警方到场 police-guard?)
      (set-global! '准备-后台已封 backstage-checked?)
      (set-global! '准备-登台已改 staging-changed?)
      (set-global! '准备-诱饵 decoy-set?))

    (define (advance-stage! new-stage)
      (set! story-stage new-stage)
      (sync-globals!))

    (define (worsen-condition! n)
      (set! condition-level (min 2 (+ condition-level n)))
      (sync-globals!))

    ;; ── 阻塞同步 ────────────────────────────────────
    ;; 必看的拍子当晚不看完不能睡。读档后由 world-load! 统一重新注册。
    (define (sync-blockers!)
      (cond
        ((= story-stage 0)
         (rest-block! "三封信/开场敲门" "有人在敲门，先去看看是谁。"))
        (delivery-pending?
         (rest-block! "三封信/交割日" "钱已经放进邮箱，你得在那儿盯着。"))
        ((and (= story-stage 2) (not (has-flag? '伤后探望)))
         (rest-block! "三封信/伤后探望" "她在门外等着，要问今天的事。"))
        ((and (= story-stage 2) (has-flag? '伤后探望) (not (has-flag? '第二封信)))
         (rest-block! "三封信/第二封信" "剧院的经理在楼下等你，手里捏着一封信。"))
        ((third-letter-due?)
         (rest-block! "三封信/第三封信" "剧院来人找你，说她的化妆间里有东西。"))
        ((and (beat3-open?) (not (has-flag? '她不取消)))
         (rest-block! "三封信/她不取消" "她在剧院等你，说要当面讲。"))
        (premiere-pending?
         (rest-block! "三封信/首演" "今晚是首演。你答应过她要在场。"))
        ((and (= story-stage 5) (not (has-flag? '结案)))
         (rest-block! "三封信/结案" "剧院外面有人在等你说话。"))
        (else
         (begin
           (rest-release! "三封信/开场敲门")
           (rest-release! "三封信/交割日")
           (rest-release! "三封信/伤后探望")
           (rest-release! "三封信/第二封信")
           (rest-release! "三封信/第三封信")
           (rest-release! "三封信/她不取消")
           (rest-release! "三封信/首演")
           (rest-release! "三封信/结案")))))

    ;; ── 开场：她找上门 ──────────────────────────────
    ;; 她不是经理介绍来的——老街的人脉听说旅馆住了个新来的侦探。
    ;; 一个穷歌女请得起的,刚好是一个穷侦探。
    (define (node-answer-door)
      (instant-action "有人敲门"
        (lambda ()
          (play-remote-dialogue!
            (line "夜莺" "你是那个新搬来的侦探？我打听过了，这条街上只有你收得起我这样的价钱。")
            (line "主角" "什么事？")
            (line "夜莺" "有人给我写信。他要钱，不然就把我从前的事抖出去。")
            (line "夜莺" "钱放码头的邮箱，三天后。我想知道是谁写的，然后我想让他别再来了——不是这一次，是往后都别再来。")
            (line "主角" "为什么不报警？")
            (line "夜莺" "我不能。"))
          (add-item! "金钱" prepayment)
          (set! delivery-day (+ world-day letter-deadline))
          (advance-stage! 1)
          (rest-release! "三封信/开场敲门")
          (spotlight! "一封信的复印件"
            (string-append
              "她把一小叠钱按在桌上，纸角起了毛——攒了有一阵子了。信是复印的，字迹工整得刻意。"
              "她说也许是莱恩，她在老街的旧朋友，也许不是。"
              "临走时她在楼梯口回过头：演出的夜晚我会给你留一张票的——如果你有空的话。")))))

    ;; ── 小节一·交割：码头准备 ───────────────────────
    ;; 三项各做一次。好 +2 / 中 +1 / 坏 0 且可以再来——一天只有四颗骰子,
    ;; 房租和身体在抢同一批,这一节考的就是"你的一天不够用"。
    (define (add-prep! n)
      (set! dock-prep (min dock-prep-max (+ dock-prep n)))
      (sync-globals!))

    (define (prep-clock)
      (list (list 'clock "码头准备" dock-prep dock-prep-max 'segments
                  "踩点、看邮箱、打听地形各做一次。攒下的准备会变成交割那天的起跑位置。")))

    (define (node-scout)
      (node "在码头踩点"
        :subtitle "巷子通向哪里，哪堵墙翻得过去，哪里能藏住一个人"
        :tags (list "低风险")
        :clocks (prep-clock)
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome "被人盯上了" "两个装卸工靠在墙边看了你一路。你穿的衣服在这儿太扎眼，只好绕开。"
            (lambda () (spend-composure! 1)))
          (outcome "大概摸清了" "转了一圈，路记了个大概，几个岔口没敢深进去。"
            (lambda () (add-prep! 1) (set! scouted? #t)))
          (outcome "把路都记住了" "你沿着货栈墙根走了两趟。哪条巷子通到后街，哪堵墙翻得过去，心里有了数。"
            (lambda () (add-prep! 2) (set! scouted? #t))))))

    (define (node-check-mailbox)
      (node "查看邮箱周围"
        :subtitle "邮箱在哪、四周有多敞、从哪个位置盯着不显眼"
        :tags (list "低风险")
        :clocks (prep-clock)
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome "白站了一场" "雨把那条街冲得没人。你等了半天，什么也没看出来。"
            (lambda () (spend-composure! 1)))
          (outcome "看清了大概" "邮箱的位置记下了，只是四周太敞，蹲在哪儿都显眼。"
            (lambda () (add-prep! 1) (set! mailbox-checked? #t)))
          (outcome "找到了位置" "邮箱在面摊斜对过。你在摊子上要了碗面，坐下来正好能盯住它，谁也看不出你在看。"
            (lambda () (add-prep! 2) (set! mailbox-checked? #t))))))

    (define (node-ask-locals)
      (node "和码头的人打听地形"
        :subtitle "这片的路数得问住在这儿的人；你这身衣服不受欢迎"
        :tags (list "低风险")
        :clocks (prep-clock)
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "碰了一鼻子灰" "你们，他们说，你们这些人。话头到这儿就断了，剩下的全是后背。"
            (lambda () (spend-composure! 1)))
          (outcome "问出个大概" "有人含糊地指了指方向，没多说。够用，但不多。"
            (lambda () (add-prep! 1) (set! locals-asked? #t)))
          (outcome "有人愿意说" "一个老搬运工替你把这片的路数说了个透——哪条道是死的，哪个门白天不锁。"
            (lambda () (add-prep! 2) (set! locals-asked? #t))))))

    (define (dock-prep-nodes)
      (if (beat1-open?)
          (append
            (if scouted? '() (list (node-scout)))
            (if mailbox-checked? '() (list (node-check-mailbox)))
            (if locals-asked? '() (list (node-ask-locals))))
          '()))

    ;; ── 交割日 ──────────────────────────────────────
    ;; 到期当天不自动播放:它是必看事件,用阻塞休息逼玩家亲自去。
    (define (begin-delivery!)
      (if delivery-pending?
          (error "三封信：交割日已经在等待处理")
          #t)
      (set! delivery-pending? #t)
      (sync-blockers!)
      (notify! "今天是信上写的日子。钱得放进邮箱，你得在那儿盯着。"))

    ;; 入场剧情由调用方播放:交锋脚本把「你已经在追了」当既定前提。
    (define (node-delivery-entry)
      (encounter-action "去码头盯着邮箱"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "钱在这儿。我放进去就走，剩下的看你的。")
            (line "主角" "放完别回头，一直走到电车站。"))
          (spotlight! "一个钟头"
            (string-append
              "她把纸包投进邮箱，沿着街走了。你在斜对过的面摊上要了碗面，慢慢吃。"
              "一个钟头里零零散散有人来投信。一个邮差过来收信，翻身上车——"
              "邮差。这一片的邮差每天下午才来一趟。"))
          (start-encounter "交割" on-delivery-result))))

    ;; 三档结果:追到人 / 跟丢但拿到东西 / 人和钱都丢了。
    ;; 无论哪一档故事都往前走——交锋失败留疤,不阻断主线。
    (define (on-delivery-result result)
      (if (not delivery-pending?)
          (error "三封信：没有待处理的交割日")
          #t)
      (set! delivery-pending? #f)
      (set! delivery-result
            (cond
              ((equal? result '好) "好")
              ((equal? result '中) "中")
              ((equal? result '坏) "坏")
              (else (error "三封信：交割交锋返回了未登记的结果"))))
      ;; 交割的三档兑现成小节二的起步条件,不是单纯的文案差别。
      (cond
        ((equal? delivery-result "好") (set! negative-progress 1))
        ((equal? delivery-result "中") (set! familiar 1))
        (else (worsen-condition! 1)))
      (set-flag! '交割已结算)
      (advance-stage! 2)
      (complete-section!)
      (sync-globals!)
      (sync-blockers!)
      (spotlight! "取信的人"
        (cond
          ((equal? delivery-result "好")
           "你把他按在了货栈的墙上。钱追回了一部分，他的脸你也看清了——不是写信的那个人，是替人跑腿的。他嘴里吐出来的方向，指着码头居民区。")
          ((equal? delivery-result "中")
           "他挣脱了，自行车倒在巷口。你手里攥着从他身上扯下来的东西——一角布，和一个写在纸片上的地址。方向是码头居民区。")
          (else
           "人跑了，钱也没了。你只知道他往哪个方向去——码头居民区，老街那一片。"))))

    ;; ── 结算后的人物戏(必看) ────────────────────────
    (define (node-her-visit)
      (instant-action "她来看你"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "伤着了？")
            (line "主角" "不要紧。钱没能全拿回来。")
            (line "夜莺" "那些钱本来就是破财消灾。")
            (line "夜莺" "我担心的不是这个。他们还会来吗？会不会要得更多？")
            (line "主角" "我要去码头居民区一趟。")
            (line "夜莺" "我在那里长大。我每天要排练，有半天的时间可以和你一起。")
            (line "夜莺" "在那之前你先把伤养好。"))
          (set-flag! '伤后探望)
          (rest-release! "三封信/伤后探望")
          (sync-globals!)
          (spotlight! "老街"
            "她说那话的时候没看你，眼睛落在窗外。老街那一片在城市的另一头，从明天起，你可以往那边去了。"))))

    ;; ── 小节二触发：第二封信(必看) ──────────────────
    ;; 信寄到了剧院,经理因此第一次介入。他出钱,也开始用他自己的方式处理——
    ;; 他的利益是首演成功、品牌不受损,和她的利益从此不完全重合。
    (define (node-second-letter)
      (instant-action "见剧院的经理"
        (lambda ()
          (play-dialogue!
            (line "经理" "这封信寄到了剧院的收发室。收发室的姑娘拆开了，念了两行才反应过来。")
            (line "经理" "他这次要的数目翻了一倍。还附了一张照片——裁过的，只留下半个人。")
            (line "经理" "他说钱要她自己送到老街去。")
            (line "主角" "他想让她穿着好衣服回那个地方低头。")
            (line "经理" "我不关心他想什么。我关心的是三个星期以后那张海报上的名字还值不值钱。")
            (line "经理" "查清楚他手里还剩什么，然后让这件事到此为止。钱我出一部分。"))
          (set-flag! '第二封信)
          (set! spread-day world-day)
          (rest-release! "三封信/第二封信")
          (sync-globals!)
          (sync-blockers!)
          (spotlight! "第二封信"
            (string-append
              "他手里确实有底片，不只是几张洗出来的照片。在他把东西散出去之前，"
              "你得查清楚那些底片在谁手上、藏在哪儿，然后把它拿回来——买、换、还是抢，是你的事。")))))

    ;; ── 小节二·账单一：底片在哪 ─────────────────────
    (define (add-negative! n)
      (set! negative-progress (min negative-target (+ negative-progress n)))
      (sync-globals!))

    (define (negative-clock)
      (list (list 'clock "底片在哪" negative-progress negative-target 'segments
                  "问清楚东西在谁手上、藏在什么地方。填满以后才谈得上拿回来。")))

    (define (spread-clock)
      (list (list 'clock "照片在老街传开" spread spread-max 'countdown
                  (string-append "每 " (number->string spread-interval)
                                 " 天多一格：看过那些照片的人越来越多。"
                                 "填满就来不及了——东西散了出去，这件事只能以最难看的方式收场。"))))

    ;; 酒馆老板：老街的守门人。他认识她,也不愿意谈她。
    (define (node-ask-owner)
      (node "跟酒馆老板打听"
        :subtitle "他认识她,也不愿意谈她;这道门得慢慢磨"
        :tags (list "低风险")
        :clocks (negative-clock)
        :requires (list (req-die))
        :resolve (roll 'social street-modifiers
          (outcome "他低头擦杯子" "他把杯子举到灯下看了看，又擦了一遍。等你说完，他去了后厨。"
            (lambda () (spend-composure! 1)))
          (outcome "他说了半句" "'那些东西不在他自己手上。'他说完就不再往下说了。"
            (lambda () (add-negative! 1)))
          (outcome "他说了个名字" "'他把值钱的东西都搁在别人那儿。'他报了个名字，'你别说是我讲的。'"
            (lambda () (add-negative! 2) (add-familiar! 1))))))

    ;; 居民区：挨着台阶一家一家问。这里的路很难走,房子挤在一起。
    (define (node-search-district)
      (node "在居民区搜集线索"
        :subtitle "台阶、晾衣绳、挤在一起的房子;这里的人不喜欢你这身衣服"
        :tags (list "低风险")
        :clocks (negative-clock)
        :requires (list (req-die))
        :resolve (roll 'sharpness street-modifiers
          (outcome "门一扇扇关上" "你敲过的门在你走开之后才重新打开。没人愿意在门口跟你站着说话。"
            (lambda () (spend-composure! 1)))
          (outcome "有人指了个方向" "一个晾衣服的女人朝坡下扬了扬下巴，没说话。"
            (lambda () (add-negative! 1) (add-familiar! 1)))
          (outcome "找到了那间屋" "半地下的一间屋，窗户糊着报纸。有人在那儿冲洗过东西——药水味还没散。"
            (lambda () (add-negative! 2) (add-familiar! 1))))))

    ;; 阿瑟：查莱恩的案底。程序管得着的那部分。
    (define (node-check-record)
      (node "请阿瑟查莱恩的案底"
        :subtitle "偷窃、诈骗、小额敲诈;登记在册的那部分"
        :tags (list "低风险")
        :clocks (negative-clock)
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (outcome "卷宗调不出来" "阿瑟翻了两遍登记簿。'这个名字下面什么也没有——至少今天没有。'"
            (lambda () #f))
          (outcome "翻到了旧案" "偷窃、诈骗、几笔小额敲诈。'这种人，'阿瑟说，'从来不自己去取钱。'"
            (lambda () (add-negative! 1)))
          (outcome "翻到了住处" "旧案卷上留着一个地址，还有一个替他保管过东西的人的名字。"
            (lambda () (add-negative! 2))))))

    ;; 她陪你走一趟。每天一次——她还要排练,只有半天。
    (define (escort-available?)
      (and (beat2-open?) (< condition-level 2) (not (= escorted-day world-day))))

    (define (node-with-nightingale)
      (node "让夜莺带你走一趟"
        :subtitle "她每天只有半天;有她在,老街的门开得快一些"
        :tags (list "低风险" "每天一次")
        :clocks (negative-clock)
        :requires (list (req-die))
        :resolve (roll 'social (lambda () (list (modifier 1 "夜莺同行")))
          (outcome "她被人认了出来" "有人在背后喊了一句难听的。她没回头，脚步也没慢，可那半天就这么过去了。"
            (lambda ()
              (set! escorted-day world-day)
              (spend-composure! 1)
              (sync-globals!)))
          (outcome "她带你穿过这里" "她走在前面，路熟得像从没离开过。有人叫她的旧名字，她应了。"
            (lambda ()
              (set! escorted-day world-day)
              (add-negative! 1)
              (add-familiar! 1)
              (sync-globals!)))
          (outcome "有人替她开了门" "一个老太太拉住她的手看了很久，然后把你们让进了屋。"
            (lambda ()
              (set! escorted-day world-day)
              (add-negative! 2)
              (add-familiar! 1)
              (gain-trust! 1))))))

    ;; ── 小节二·人物戏与见闻 ─────────────────────────
    ;; 她谈起自己怎么离开。第一次读:袒露内心。
    (define (node-her-past)
      (instant-action "听她说起从前"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "那边第三个门洞，我在那儿住到十六岁。冬天水管冻上，得下坡去挑。")
            (line "夜莺" "我在这条街的酒吧唱了六年。六年，先生。没人来听，来的人也不是来听的。")
            (line "主角" "后来呢？")
            (line "夜莺" "后来有一天我算了一笔账。我发现我再唱六年，还是站在同一块地板上。")
            (line "夜莺" "所以我走了。走的时候没跟谁道别——这就是他们记恨的那件事。")
            (line "主角" "值得吗？")
            (line "夜莺" "你看见我现在站在哪儿了。"))
          (set-flag! '她的过去)
          (gain-trust! 1)
          (sync-globals!))))

    ;; 莱恩的下作,和"老街不是莱恩"。两张见闻卡分散在老街两处,
    ;; 不做成一次性过场——这两件事要玩家在生活里反复撞见。
    (define (node-lyon-talk)
      (observe-action "酒馆里的闲话"
        (if (>= familiar 2)
            "他把那几张照片给人看过——就在这张桌子上，摊开了，添上些不存在的故事。'我们都认识她嘛。'有人笑，也有人把杯子推开走了。今天有个搬运工说：那些东西该烧掉。"
            "角落里几个人正说着什么，看见你就停了。散开的时候，其中一个把桌上的东西按进了口袋。")))

    (define (node-district-mood)
      (observe-action "台阶上的人们"
        (if (>= familiar 2)
            "有人肯跟你说话了。他们不喜欢那个写信的人——'做得恶心'，一个老太太原话。但真提到把本地人交给警察，所有人都摇头。这是两码事，他们说。"
            "晾衣绳底下有人在看你。你走过去，说话声就停了；你走开，声音又起来。你穿的衣服在这里像一句挑衅。")))

    ;; ── 小节二·账单二：三条路线 ─────────────────────
    (define (player-share) (max 0 (- negatives-price manager-share)))

    (define (settle-material! route quality fate)
      (set! material-settled? #t)
      (set! settle-route route)
      (set! settle-quality quality)
      (set! lyon-fate fate)
      (set! settled-day world-day)
      (advance-stage! 3)
      (complete-section!)
      (set-flag! '留下的信)
      (let ((fee (cond
                   ((equal? quality "好") manager-fee-good)
                   ((equal? quality "中") manager-fee-fair)
                   (else manager-fee-poor))))
        (add-item! "金钱" fee)
        (sync-globals!)
        (sync-blockers!)
        (spotlight! "勒索到此为止"
          (string-append
            (cond
              ((equal? quality "好")
               "底片和照片都在你手里。你在旅馆的洗脸盆里把它们烧了，纸卷起来，边缘先黑。")
              ((equal? quality "中")
               "东西拿回来了，只是不能确定拿全了。剩下的只能赌他没留底。")
              (else
               "东西是拿回来了——用了不太好看的办法。老街那边，有些门以后不会再对你开。"))
            "经理结了报酬 " (number->string fee) " 金。"
            "她把取回的照片烧掉了，却留下了一封莱恩写过的信，压在梳妆台的镜子底下。"
            "首演还有 " (number->string (days-to-premiere)) " 天。"))))

    ;; 交易：钱最直接,代价纯经济。问题是给了钱怎么确保他不留底。
    (define (node-buy-back)
      (node "把底片买回来"
        :subtitle (string-append "总价 " (number->string negatives-price)
                                 " 金，经理出 " (number->string manager-share)
                                 " 金，你出 " (number->string (player-share))
                                 " 金；东西当场交割，留没留底只能赌")
        :requires (list (req-item "金钱" (player-share)))
        :resolve (instant
          (outcome "钱货两清" "中间人把牛皮纸袋推过桌面，没有打开看。'剩下的事我不管。'"
            (lambda ()
              (remove-item! "金钱" (player-share))
              (settle-material! "交易" "中" "逃走"))))))

    ;; 关系：把时间和脸面换成东西。要老街真的把你当回事。
    (define (node-through-street)
      (node "让老街的人替你去谈"
        :subtitle "不花钱;需要老街熟脸满格。他们不肯把本地人交给警察，但愿意替她把东西要回来"
        :tags (list "低风险")
        :requires (list (req-die))
        :resolve (roll 'social street-modifiers
          (outcome "他起了疑心" "话传到他耳朵里的时候变了味。当天晚上，又有人看见了那些照片。"
            (lambda ()
              (set! spread (min spread-max (+ spread 1)))
              (spend-composure! 1)
              (sync-globals!)))
          (outcome "东西要回来了" "两个搬运工去了一趟，回来时手里多了个铁盒。'差不多都在里头。'"
            (lambda () (settle-material! "关系" "中" "逃走")))
          (outcome "连人带东西" "他们把铁盒放在桌上，又说：'那个人今天早上坐船走了。这里没人留他。'"
            (lambda () (settle-material! "关系" "好" "逃走"))))))

    ;; 强制：最快,但可能毁掉东西,也会得罪老街。
    (define (node-force)
      (node "直接上门把东西拿走"
        :subtitle "高风险;成了最快，砸了他可能毁掉底片。事败会得罪劳工"
        :tags (list "高风险")
        :requires (list (req-die))
        :resolve (roll 'violence street-modifiers
          (outcome "他先把东西毁了" "门撞开的时候，他正把一卷底片按进洗手池。药水在池底冒泡。"
            (lambda ()
              (damage-party! 1)
              (change-faction-relation! "劳工" -2)
              (settle-material! "强制" "坏" "逃走")))
          (outcome "东西拿到了" "你把铁盒从床板下面拖出来。他靠在墙角，一句狠话也没说出口。"
            (lambda ()
              (change-faction-relation! "劳工" -1)
              (settle-material! "强制" "中" "逃走")))
          (outcome "连人一起交出去" "阿瑟带的人堵住了后门。铁盒和他一起被带走了——问过话，第二天就放了。"
            (lambda () (settle-material! "强制" "好" "被释放"))))))

    (define (beat2-settle-nodes)
      (if (and (beat2-open?) (negatives-located?))
          (append
            (list (node-buy-back))
            (if (>= familiar familiar-max) (list (node-through-street)) '())
            (list (node-force)))
          '()))

    ;; 传开钟满格:替玩家以最难看的方式收场。到期不是隐藏的,备注里写清楚了。
    (define (force-settle-by-spread!)
      (worsen-condition! 1)
      (settle-material! "传开" "坏" "逃走"))

    (define-turn-rule "照片在老街传开"
      (lambda ()
        (and (beat2-open?) (>= (- world-day spread-day) spread-interval)))
      (lambda ()
        (set! spread (min spread-max (+ spread 1)))
        (set! spread-day world-day)
        (sync-globals!)
        (if (>= spread spread-max)
            (force-settle-by-spread!)
            (notify! (string-append "又有人看过那些照片了。照片在老街传开 "
                                    (number->string spread) "/"
                                    (number->string spread-max) "。")))))

    ;; ── 平静期：剧院的排练 ──────────────────────────
    ;; 主线没有新压力。她正在从酒馆歌女变成剧院演员——酒馆里她出现的
    ;; 频率降低，这个变化本身就是叙事（singer-present? 在 stage 3 起为假）。
    (define (node-watch-rehearsal)
      (observe-action "看她排练"
        "乐队还在对拍子，她已经站到位置上了。中间断过两次，第二次是她自己喊停的。她跟指挥说话的样子，和在酒馆里完全不同——那儿她是在唱给一屋子不听的人，这儿她在跟人干活。"))

    (define (node-manager-desk)
      (observe-action "经理的办公室"
        (string-append
          "他在核一张座位表，笔尖点着前排的几个位置。"
          "'赞助的人要来，'他说，'那几位的名字我背得出来。'"
          "墙上钉着首演的海报，她的名字排在第三行。")))

    ;; ── 小节三触发：第三封信(必看) ──────────────────
    ;; 不要钱,不提过去。只写一件事。信里提到一个只有内部人员才知道的
    ;; 排练细节——玩家和经理都认为是莱恩,勒索失败后升级到报复,合理推断。
    (define (node-third-letter)
      (instant-action "去剧院看那封信"
        (lambda ()
          (play-dialogue!
            (line "经理" "在她化妆间的镜子底下。没有信封，没有邮戳。有人把它放进去的。")
            (line "主角" "写了什么？")
            (line "经理" "不要钱。一个字都没提钱。")
            (line "经理" "只说她要是当晚登台，她会死在台上。")
            (line "主角" "……这里写着她的登台时间。连换装的顺序都写了。")
            (line "经理" "只有后台的人知道那个顺序。")
            (line "主角" "他勒索没成，就换了个法子。")
            (line "经理" "演出照常。票已经卖出去了，报纸也约好了。"))
          (set-flag! '第三封信)
          (advance-stage! 4)
          (rest-release! "三封信/第三封信")
          (sync-blockers!)
          (spotlight! "第三封信"
            (string-append
              "他勒索失败，于是把要钱改成了要命——你和经理都这么想，这是合理的推断。"
              "经理拒绝取消首演。剩下的日子只有一件事：让她活着唱完。"
              "距首演还有 " (number->string (days-to-premiere)) " 天，"
              "五件事能做，骰子不够做完。")))))

    ;; ── 小节三·人物戏(必看) ─────────────────────────
    (define (node-she-refuses)
      (instant-action "她要当面跟你讲"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "经理说你想让我别上台。")
            (line "主角" "有人写信说要你的命。")
            (line "夜莺" "我等了这么多年。")
            (line "夜莺" "我在那条街上唱了六年，先生。六年里没有一个人写信说要我的命——因为没有一个人在乎我死不死。")
            (line "夜莺" "现在有人在乎了。这说明我走到了什么地方。")
            (line "主角" "这说明有人想让你下不来台。")
            (line "夜莺" "那天晚上你留在后台，行吗？")
            (line "夜莺" "别站在台下看。站在我能看见你的地方。"))
          (set-flag! '她不取消)
          (gain-trust! 1)
          (rest-release! "三封信/她不取消")
          (sync-globals!))))

    ;; 赞助公司的人。零机制,两句话——第二章的种子,第一章不解释。
    (define (node-vera)
      (instant-action "和赞助方的人握手"
        (lambda ()
          (play-dialogue!
            (line "薇拉" "你就是那位侦探。经理跟我提过。")
            (line "薇拉" "夜莺唱得很好。我很喜欢。")
            (line "主角" "您听过她唱？")
            (line "薇拉" "我的助理告诉过我她唱得很好。")
            (line "薇拉" "这样的孩子应该被更多人听见。有时候需要一点运气——运气也是可以安排的。"))
          (set-flag! '薇拉)
          (sync-globals!))))

    ;; ── 小节三·五项准备 ─────────────────────────────
    ;; 每项一次性;骰子不够做全部,取决于此前积累了什么关系和资源。
    (define (prep-clock-3)
      (list (list 'clock "首演之前" (prep-done) prep-count 'segments
                  "五件事，做成几件决定那天晚上你从什么局面开始。做不完是常态。")))

    (define (node-find-lyon)
      (node "找到莱恩"
        :subtitle "他跑了/被放了;找到他，至少能确认他有没有同伙"
        :tags (list "低风险")
        :clocks (prep-clock-3)
        :requires (list (req-die))
        :resolve (roll 'sharpness street-modifiers
          (outcome "线索断了" "他住过的屋子空着，房东说前天就搬了。往哪儿去没人知道。"
            (lambda () (spend-composure! 1)))
          (outcome "问到了下落" "他躲在码头另一头的一间棚屋里，喝得站不起来。他矢口否认写过第三封信——嘴硬得反常。"
            (lambda ()
              (set! found-lyon? #t)
              (sync-globals!)))
          (outcome "问清了他身边的人" "他一个人。没有同伙，没有钱，连酒都是赊的。'我要她的钱，'他说，'我要她的命干什么？'"
            (lambda ()
              (set! found-lyon? #t)
              (sync-globals!))))))

    (define (node-ask-police)
      (node "请警方派人保护"
        :subtitle "阿瑟能安排人手;官僚关系越好，来的人越多"
        :tags (list "低风险")
        :clocks (prep-clock-3)
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "报告压在桌上" "'威胁信我们收到过很多，'值班的人说，'首演那天全城都要人手。'"
            (lambda () #f))
          (outcome "答应派两个人" "阿瑟把信抄了一份归档。'两个人，后台门口，从开演站到散场。'"
            (lambda ()
              (set! police-guard? #t)
              (sync-globals!)))
          (outcome "上头点了头" "'上司已经吩咐过这件事，'阿瑟扶了扶眼镜，'剧院那边的面子，比我们大。'"
            (lambda ()
              (set! police-guard? #t)
              (change-faction-relation! "官僚" 1)
              (sync-globals!))))))

    (define (node-check-backstage)
      (node "把剧院后台走一遍"
        :subtitle "通行证、临时工、舞台结构、登台路线;封住的出口在那天晚上都算数"
        :tags (list "低风险")
        :clocks (prep-clock-3)
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (outcome "图纸对不上" "经理给的图纸是三年前的。有两道门在图上根本不存在。"
            (lambda () (spend-composure! 1)))
          (outcome "记住了出口" "四个出口，两个通向后巷。你把临时工的名单也抄了一份。"
            (lambda ()
              (set! backstage-checked? #t)
              (sync-globals!)))
          (outcome "把路线捋清了" "从化妆间到台口只有一条路，中间经过配电间。你在那儿站了很久。"
            (lambda ()
              (set! backstage-checked? #t)
              (sync-globals!))))))

    (define (node-change-staging)
      (node "改动她的登台安排"
        :subtitle (if (trust-met?)
                      "她信得过你，才肯为了你改自己的演出"
                      "她不会为一个还没赢得信任的人改演出")
        :disabled (not (trust-met?))
        :tags (list "低风险")
        :clocks (prep-clock-3)
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "她不肯动" "'换了位置，灯就打不到我脸上。'这一条她寸步不让。"
            (lambda () #f))
          (outcome "换了登台顺序" "她同意把自己的段落挪到后面，让升台那一段避开人最多的时候。"
            (lambda ()
              (set! staging-changed? #t)
              (sync-globals!)))
          (outcome "连路线一起改了" "她照你说的改了登台顺序和上台的路线，还把备用扶梯的位置记熟了。"
            (lambda ()
              (set! staging-changed? #t)
              (gain-trust! 1)
              (sync-globals!))))))

    (define (node-set-decoy)
      (node "放出假的登台时间"
        :subtitle "逼写信的人提前动手;成了那晚的局面小一些，你得早到"
        :tags (list "低风险")
        :clocks (prep-clock-3)
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "没人接这个饵" "消息放出去了，什么也没发生。你只是让后台多了几句闲话。"
            (lambda () (spend-composure! 1)))
          (outcome "话传出去了" "假的排练时间通过三个人的嘴传了出去。至少有人会照着它安排。"
            (lambda ()
              (set! decoy-set? #t)
              (sync-globals!)))
          (outcome "有人上钩了" "假时间放出去的第二天，配电间的锁被人动过。他照着你的假消息来了。"
            (lambda ()
              (set! decoy-set? #t)
              (sync-globals!))))))

    (define (beat3-prep-nodes)
      (if (beat3-open?)
          (append
            (if found-lyon? '() (list (node-find-lyon)))
            (if police-guard? '() (list (node-ask-police)))
            (if backstage-checked? '() (list (node-check-backstage)))
            (if staging-changed? '() (list (node-change-staging)))
            (if decoy-set? '() (list (node-set-decoy))))
          '()))

    ;; ── 首演之夜 ────────────────────────────────────
    (define premiere-pending? #f)

    (define (begin-premiere!)
      (if premiere-pending?
          (error "三封信：首演之夜已经在等待处理")
          #t)
      (set! premiere-pending? #t)
      (sync-blockers!)
      (notify! "今天是首演。天黑以前你得到剧院去。"))

    (define (node-premiere-entry)
      (encounter-action "去剧院"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "别站在台下。站在我能看见你的地方。")
            (line "主角" "我就在侧台。")
            (line "经理" "两分钟。各就各位。"))
          (spotlight! "开演"
            (string-append
              "灯暗下去，乐队起了第一个音。她走进那束光里，前两段唱得干干净净。"
              "第三段的舞台开始升起——就在这时候，全场的灯一起灭了。"))
          (start-encounter "首演之夜" on-premiere-result))))

    ;; 交锋只回传 'done：四个向量走 global，由这里解释并写成结案。
    (define (on-premiere-result result)
      (if (not premiere-pending?)
          (error "三封信：没有待处理的首演")
          #t)
      (if (equal? result 'done) #t (error "三封信：首演交锋返回了未登记的结果"))
      (set! premiere-pending? #f)
      (set! premiere-done? #t)
      (if (get-global '首演-她受伤) (worsen-condition! 2) #f)
      (advance-stage! 5)
      (complete-section!)
      (sync-globals!)
      (sync-blockers!))

    ;; ── 公开结案(必看) ──────────────────────────────
    ;; 第一章在情绪上是一次胜利。按这个基调写，不留反讽的语气。
    ;; §9.1 的那些细节只写进台词和描述,不设 flag、不标注、不提示。
    (define (node-closing)
      (instant-action "散场之后"
        (lambda ()
          (play-dialogue!
            (line "阿瑟" "莱恩已经在我们手里了。")
            (line "主角" "这么快。")
            (line "阿瑟" "上头催得紧。会有记者来问，你知道他们会写什么。")
            (line "阿瑟" "港口失控，警方依法处置，夜莺没有受伤，演出是成功的。")
            (line "主角" "追到后台的那个人，比他冷静得多。他知道哪道门通哪儿。")
            (line "阿瑟" "他雇的人。这种人手上从来不干净。")
            (line "阿瑟" "别把事情想复杂了。案子结了，姑娘没事，你拿到了钱。"))
          (set-flag! '结案)
          (rest-release! "三封信/结案")
          (sync-globals!)
          (spotlight! "第二天的头版"
            (string-append
              "「港口无业人员勒索威胁，夜莺不惧危险，华丽谢幕」——占了整个头版。"
              "经理的公关团队准备得异常充分。三封信被装进同一份案卷。"
              "警方以搜索同伙为由封锁了老街。"
              (if (get-global '首演-她受伤)
                  "她手上还缠着绷带，照片里看不出来。"
                  "照片里她站在谢幕的灯下，恢复得比谁都快。")
              "你拿到了报酬，委托到此结束。"))
          (play-dialogue!
            (line "夜莺" "你来了。")
            (line "主角" "你唱完了。")
            (line "夜莺" "我说过我会唱完的。")
            (line "夜莺" "那张票我一直留着。你没用上——你站在后台。")
            (line "主角" "下次吧。")
            (line "夜莺" "下次。")))))

    ;; ── 状态卡 ──────────────────────────────────────
    (define (days-tail)
      (string-append " · 首演还有 " (number->string (days-to-premiere)) " 天"))

    (define (client-subtitle)
      (cond
        ((= story-stage 1) (string-append "酒馆驻唱" (days-tail)))
        ((= story-stage 2) (string-append "她的过去被人攥在手里" (days-tail)))
        ((= story-stage 3) (string-append "勒索结束了，她在剧院排练" (days-tail)))
        ((= story-stage 4) (string-append "有人要她死在台上" (days-tail)))
        ((= story-stage 5) "首演之后")
        (else "")))

    (define (situation-text)
      (cond
        ((= story-stage 1)
         "她在老街的酒馆唱歌，刚被一个剧院经理看中。首演是她等了多年的那一步——如果走得到的话。写信的人挑的就是这个时候。")
        ((= story-stage 2)
         "取信的人往码头居民区去了。那一片是她长大的地方，也是她再没回去过的地方。")
        ((= story-stage 3)
         (string-append
           "勒索到此为止。她这些天几乎住在剧院里，排练排到嗓子哑。"
           (cond
             ((equal? settle-quality "好") "东西是干干净净拿回来的，她知道。")
             ((equal? settle-quality "中") "东西拿回来了，只是谁也不敢说拿全了。")
             (else "办法不太好看。老街那边，有些人不再跟你说话。"))))
        ((= story-stage 4)
         (string-append
           "第三封信不要钱，只要她的命，而且知道只有后台的人才知道的事。"
           (if found-lyon?
               "你找到了莱恩——他喝得站不起来，矢口否认写过这封信。"
               "莱恩不知去向。")))
        ((= story-stage 5)
         "报纸把这件事写完了。案子结了，她站上了她等了多年的那个位置。")
        (else "")))

    ;; 会阻塞世界日程的到期挂在世界根节点上；进度条挂在委托卡与各自的动作上。
    (define (world-clocks)
      (cond
        (delivery-pending?
         (list (list 'clock "交割日" 0 letter-deadline 'countdown
                     "就是今天。先把这件事办了才能结束这一天。")))
        ((= story-stage 1)
         (list (list 'clock "交割日" (days-to-delivery) letter-deadline 'countdown
                     "信上写的日子。到期当天必须去码头盯住邮箱，那天之前的准备决定你从哪儿起跑。")))
        (else '())))

    (define (card-clocks)
      (cond
        ((beat1-open?) (prep-clock))
        ((beat2-open?) (append (negative-clock) (spread-clock)))
        ((beat3-open?) (prep-clock-3))
        (else '())))

    (define (render-data)
      (if (>= story-stage 1)
          (list (node "夜莺"
                  :subtitle (client-subtitle)
                  :children (append
                              (list (observe-action "她的处境" (situation-text)))
                              (if (and (= story-stage 2) (not (has-flag? '伤后探望)))
                                  (list (node-her-visit))
                                  '())
                              (beat2-settle-nodes))
                  :clocks (card-clocks)))
          '()))

    ;; 世界根节点上的待办:开场敲门、交割日入口、经理登门。
    (define (world-nodes)
      (append
        (if (= story-stage 0) (list (node-answer-door)) '())
        (if delivery-pending? (list (node-delivery-entry)) '())
        (if (and (= story-stage 2) (has-flag? '伤后探望) (not (has-flag? '第二封信)))
            (list (node-second-letter))
            '())
        (if (third-letter-due?) (list (node-third-letter)) '())
        (if (and (beat3-open?) (not (has-flag? '她不取消)))
            (list (node-she-refuses))
            '())
        (if premiere-pending? (list (node-premiere-entry)) '())
        (if (and (= story-stage 5) (not (has-flag? '结案)))
            (list (node-closing))
            '())))

    ;; 各地点向故事要自己这一拍的节点。地点不认识故事状态,只认自己的名字。
    (define (nodes-at location)
      (cond
        ((equal? location "码头") (dock-prep-nodes))
        ((equal? location "酒馆")
         (if (beat2-open?)
             (append
               (if (negatives-located?) '() (list (node-ask-owner)))
               (list (node-lyon-talk)))
             '()))
        ((equal? location "居民区")
         (append
           (if (beat2-open?)
               (append
                 (if (negatives-located?) '() (list (node-search-district)))
                 (if (escort-available?) (list (node-with-nightingale)) '())
                 (if (and (>= familiar 1) (not (has-flag? '她的过去)))
                     (list (node-her-past))
                     '())
                 (list (node-district-mood)))
               '())
           (if (and (beat3-open?) (not found-lyon?))
               (list (node-find-lyon))
               '())))
        ((equal? location "警局")
         (append
           (if (and (beat2-open?) (not (negatives-located?)))
               (list (node-check-record))
               '())
           (if (and (beat3-open?) (not police-guard?))
               (list (node-ask-police))
               '())))
        ((equal? location "剧院")
         (cond
           ((quiet-period?)
            (list (node-watch-rehearsal) (node-manager-desk)))
           ((beat3-open?)
            (append
              (if backstage-checked? '() (list (node-check-backstage)))
              (if staging-changed? '() (list (node-change-staging)))
              (if decoy-set? '() (list (node-set-decoy)))
              (if (has-flag? '薇拉) '() (list (node-vera)))
              (list (node-watch-rehearsal))))
           (else (list (node-manager-desk)))))
        (else '())))

    ;; ── 日终 ────────────────────────────────────────
    ;; 两个钉死的日子：交割日（信上写的期限）与首演之夜。都在日终判定，
    ;; 到期当天不自动播放——它们是必看事件，用阻塞休息逼玩家亲自去。
    (define-turn-rule "第一章定日事件"
      (lambda ()
        (or (and (= story-stage 1) (not delivery-pending?)
                 (>= (+ world-day 1) delivery-day))
            (and (= story-stage 4) (not premiere-pending?) (not premiere-done?)
                 (>= (+ world-day 1) premiere-day))))
      (lambda ()
        (if (= story-stage 1)
            (begin-delivery!)
            (begin-premiere!))))

    ;; ── 消息接口 ────────────────────────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data) (render-data))
          ((equal? msg 'world-nodes) (world-nodes))
          ((equal? msg 'world-clocks) (world-clocks))
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'story-stage) story-stage)
          ((equal? msg 'singer-present?) (singer-present?))
          ((equal? msg 'old-street-open?) (old-street-open?))
          ((equal? msg 'theater-open?) (theater-open?))
          ((equal? msg 'dock-prep) dock-prep)
          ((equal? msg 'delivery-result) delivery-result)
          ((equal? msg 'trust-met?) (trust-met?))
          ((equal? msg 'sync-blockers!) (sync-blockers!))
          ((equal? msg 'save)
           (list
             (list "story-stage" story-stage)
             (list "delivery-day" delivery-day)
             (list "delivery-pending?" delivery-pending?)
             (list "delivery-result" delivery-result)
             (list "dock-prep" dock-prep)
             (list "scouted?" scouted?)
             (list "mailbox-checked?" mailbox-checked?)
             (list "locals-asked?" locals-asked?)
             (list "condition-level" condition-level)
             (list "trust" trust)
             (list "negative-progress" negative-progress)
             (list "familiar" familiar)
             (list "spread" spread)
             (list "spread-day" spread-day)
             (list "escorted-day" escorted-day)
             (list "material-settled?" material-settled?)
             (list "settle-route" settle-route)
             (list "settle-quality" settle-quality)
             (list "lyon-fate" lyon-fate)
             (list "settled-day" settled-day)
             (list "found-lyon?" found-lyon?)
             (list "police-guard?" police-guard?)
             (list "backstage-checked?" backstage-checked?)
             (list "staging-changed?" staging-changed?)
             (list "decoy-set?" decoy-set?)
             (list "premiere-done?" premiere-done?)
             (list "premiere-pending?" premiere-pending?)
             (list "scene-flags" scene-flags)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! story-stage (assoc-get data "story-stage" 0))
             (set! delivery-day (assoc-get data "delivery-day" 0))
             (set! delivery-pending? (assoc-get data "delivery-pending?" #f))
             (set! delivery-result (assoc-get data "delivery-result" "未定"))
             (set! dock-prep (assoc-get data "dock-prep" 0))
             (set! scouted? (assoc-get data "scouted?" #f))
             (set! mailbox-checked? (assoc-get data "mailbox-checked?" #f))
             (set! locals-asked? (assoc-get data "locals-asked?" #f))
             (set! condition-level (assoc-get data "condition-level" 0))
             (if (or (< condition-level 0) (> condition-level 2))
                 (error "三封信存档错误：夜莺处境等级非法")
                 #t)
             (set! trust (assoc-get data "trust" 0))
             (set! negative-progress (assoc-get data "negative-progress" 0))
             (set! familiar (assoc-get data "familiar" 0))
             (set! spread (assoc-get data "spread" 0))
             (set! spread-day (assoc-get data "spread-day" 0))
             (set! escorted-day (assoc-get data "escorted-day" 0))
             (set! material-settled? (assoc-get data "material-settled?" #f))
             (set! settle-route (assoc-get data "settle-route" "无"))
             (set! settle-quality (assoc-get data "settle-quality" "无"))
             (set! lyon-fate (assoc-get data "lyon-fate" "无"))
             (set! settled-day (assoc-get data "settled-day" 0))
             (set! found-lyon? (assoc-get data "found-lyon?" #f))
             (set! police-guard? (assoc-get data "police-guard?" #f))
             (set! backstage-checked? (assoc-get data "backstage-checked?" #f))
             (set! staging-changed? (assoc-get data "staging-changed?" #f))
             (set! decoy-set? (assoc-get data "decoy-set?" #f))
             (set! premiere-done? (assoc-get data "premiere-done?" #f))
             (set! premiere-pending? (assoc-get data "premiere-pending?" #f))
             (set! scene-flags (normalize-flags (assoc-get data "scene-flags" '())))
             (sync-globals!)
             (sync-blockers!)))
          ;; 调试台专用：把故事整段拨到某一拍，连同该拍需要的前置 flag
          ;; 一起补齐，否则只改 stage 会卡在必看事件的阻塞上。
          ((equal? msg 'debug-jump!)
           (let ((target (cadr args)))
             (if (>= target 1)
                 (begin
                   (set! delivery-day (+ world-day letter-deadline))
                   (set-flag! '交割已结算))
                 #f)
             (if (>= target 2)
                 (begin
                   (set! delivery-result "中")
                   (set-flag! '伤后探望)
                   (set-flag! '第二封信)
                   (set! spread-day world-day))
                 #f)
             (if (>= target 3)
                 (begin
                   (set! material-settled? #t)
                   (set! settle-route "交易")
                   (set! settle-quality "中")
                   (set! lyon-fate "逃走")
                   (set! settled-day world-day)
                   (set-flag! '留下的信))
                 #f)
             (if (>= target 4)
                 (begin
                   (set-flag! '第三封信)
                   (set! trust trust-threshold))
                 #f)
             (advance-stage! target)
             (sync-blockers!)))
          ((equal? msg 'debug-set-prep!) (set! dock-prep (cadr args)) (sync-globals!))
          ((equal? msg 'debug-locate-negatives!)
           (set! negative-progress negative-target)
           (set! familiar familiar-max)
           (sync-globals!))
          ((equal? msg 'debug-all-prep!)
           (set! found-lyon? #t)
           (set! police-guard? #t)
           (set! backstage-checked? #t)
           (set! staging-changed? #t)
           (set! decoy-set? #t)
           (sync-globals!))
          (#t #f))))))

;; 新游戏自动执行的开场动作。客户端读这个全局去找节点，不写死章节内容。
(set-global! '开场动作 "有人敲门")
