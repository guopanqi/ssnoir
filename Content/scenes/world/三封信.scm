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

    ;; ── 状态 ────────────────────────────────────────
    ;; 0=未开场 1=小节一·交割 2=小节二·老街
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
    (define scene-flags '())

    ;; ── flag 登记 ───────────────────────────────────
    ;; 未登记的 flag 直接报错,避免拼错字悄悄变成一个新状态。
    (define (flag-id flag)
      (cond
        ((or (equal? flag '交割已结算) (equal? flag "交割已结算")) "交割已结算")
        ((or (equal? flag '伤后探望) (equal? flag "伤后探望")) "伤后探望")
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
    (define (singer-present?)
      (and (>= story-stage 1) (< condition-level 2)))

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
      (set-global! '交割结果 delivery-result))

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
        (else
         (begin
           (rest-release! "三封信/开场敲门")
           (rest-release! "三封信/交割日")
           (rest-release! "三封信/伤后探望")))))

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
      (if (equal? delivery-result "坏") (worsen-condition! 1) #f)
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

    ;; ── 状态卡 ──────────────────────────────────────
    (define (client-subtitle)
      (cond
        ((= story-stage 1)
         (string-append "酒馆驻唱 · 首演还有 " (number->string (days-to-premiere)) " 天"))
        ((= story-stage 2)
         (string-append "她的过去被人攥在手里 · 首演还有 " (number->string (days-to-premiere)) " 天"))
        (else "")))

    (define (situation-text)
      (cond
        ((= story-stage 1)
         "她在老街的酒馆唱歌，刚被一个剧院经理看中。首演是她等了多年的那一步——如果走得到的话。写信的人挑的就是这个时候。")
        ((= story-stage 2)
         "取信的人往码头居民区去了。那一片是她长大的地方，也是她再没回去过的地方。")
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

    (define (render-data)
      (if (>= story-stage 1)
          (list (node "夜莺"
                  :subtitle (client-subtitle)
                  :children (append
                              (list (observe-action "她的处境" (situation-text)))
                              (if (and (= story-stage 2) (not (has-flag? '伤后探望)))
                                  (list (node-her-visit))
                                  '()))
                  :clocks (if (beat1-open?) (prep-clock) '())))
          '()))

    ;; 世界根节点上的待办:开场敲门与交割日入口。
    (define (world-nodes)
      (append
        (if (= story-stage 0) (list (node-answer-door)) '())
        (if delivery-pending? (list (node-delivery-entry)) '())))

    ;; 各地点向故事要自己这一拍的节点。地点不认识故事状态,只认自己的名字。
    (define (nodes-at location)
      (cond
        ((equal? location "码头") (dock-prep-nodes))
        (else '())))

    ;; ── 日终 ────────────────────────────────────────
    (define-turn-rule "第一章定日事件"
      (lambda ()
        (and (= story-stage 1) (not delivery-pending?)
             (>= (+ world-day 1) delivery-day)))
      (lambda () (begin-delivery!)))

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
          ((equal? msg 'dock-prep) dock-prep)
          ((equal? msg 'delivery-result) delivery-result)
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
             (set! scene-flags (normalize-flags (assoc-get data "scene-flags" '())))
             (sync-globals!)
             (sync-blockers!)))
          ((equal? msg 'debug-stage!) (advance-stage! (cadr args)))
          ((equal? msg 'debug-set-prep!) (set! dock-prep (cadr args)) (sync-globals!))
          (#t #f))))))

;; 新游戏自动执行的开场动作。客户端读这个全局去找节点，不写死章节内容。
(set-global! '开场动作 "有人敲门")
