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
    (define prepayment 30)          ; 她先拿得出的部分,也够玩家续几天房租
    (define delivery-price 100)     ; 勒索信要求放进邮箱的总额
    (define nightingale-cover-max 30) ; 她能当掉的首饰值这么多——兜底到此为止
    (define letter-deadline 3)      ; 信上写的期限:三天后
    (define premiere-day 22)        ; 首演之夜,全章不变

    ;; ── 小节二·城寨 ─────────────────────────────────
    ;; 目标只有一句话：查出那个抽「老金牌」的人是谁。
    ;; 东、西两条通路并行往里长。每条通路由两个 0/4 的空间前沿组成：
    ;; 走完一段，旧动作消失，新动作出现在更深处的 Unity Anchor；人物和地点可在途中留下。
    ;; 烟只是让商人开口的抓手,真正的线索由地点里的人和生活痕迹拼出来。
    ;; 老街熟脸不改判定难度,只在两个地方兑现:酒馆闲话的分档,和小节二结算的报酬档位。
    (define familiar-max 3)
    (define warren-leg-max 4)       ; 每个空间前沿的长度；两段合计仍与旧 0/8 路线相同
    (define merchant-trust-max 8)   ; 埃迪愿意把客人的事告诉你
    (define trust-threshold 3)      ; 够这个数,小节三才能改动她的登台安排
    (define manager-fee-good 220)   ; 结算报酬:查得干净
    (define manager-fee-fair 150)   ; 勉强

    ;; ── 平静期与小节三 ───────────────────────────────
    ;; ── 小节三·教训莱恩 ─────────────────────────────
    (define second-letter-day 7)    ; 第二封信钉死在第 7 天:经理由此登场
    (define patience-max 6)         ; 经理的耐心
    (define patience-interval 1)    ; 每次结束一天自己掉一格
    ;; 带着进展去见他能补回几格。补 2 太少：耐心一共 6 格、每天掉 1 格，
    ;; 补两格等于只买回两天，玩家读不出"让他觉得这件事在往前走"有什么用。
    (define patience-report 4)
    ;; 小节三是一段旅途:三道关口(进得去 → 找得着 → 落单),每一关都是一把锁配几把钥匙。
    ;; 钥匙全部来自前两节认识的人;没有钥匙也过得去,代价是惊动更多人。
    ;; 一路惊动的人数不在城里结算,直接成为巷子那一场「巷口的人」的起始格。
    ;; 小节三：经理雇的人去找莱恩,要三天。这三天是玩家自己的——挣钱、交租、
    ;; 在码头和老街过日子。而那些日子正是在配钥匙:你认识了谁,进场时就有哪几手。
    ;; 三天不可加速:一旦能花骰子催,最优解就是催,这段时间又变回一张跑腿卡。
    (define inquiry-days 3)

    ;; ── 平静期：假的结束 ────────────────────────────
    ;; 巷子之后到第三封信之间的三天。它不是又一段等待——小节三已经用过一次
    ;; 「等三天、不可加速」。这三天的目标是让玩家松手:经理结前半段报酬,
    ;; 城里能做的全是收尾性、消耗性的事。等第三封信落下来,他手上什么也不剩。
    (define quiet-days 3)           ; 教训莱恩之后,第三封信隔几天到
    (define interim-fee 100)        ; 巷子办完那一段的报酬——这三天正是要你把它花掉
    (define premiere-window 10)     ; 首演倒计时进卷宗时的显示窗口

    ;; ── 小节四·第三封信 ─────────────────────────────
    ;; 剧院是一座旧圆形剧场(早年办拳赛):没有侧台、没有幕布、没有后台屏障,
    ;; 她在正中央,三百六十度都是人。城市侧在这张图上布置,首演之夜同一张图活过来。
    ;; 三个环是串起来的:她被挤下台就退到内环,要走回舞台必须穿过人群;
    ;; 从外圈门进来的人能直接走到她跟前。
    ;; 格子是布置,人是活的——人手不占准备格,那天晚上他们各按自己的脾气做事。
    (define ring-max 4)             ; 外圈的门与后廊 / 内环走道与看台
    (define core-max 2)             ; 中央台:圆台四面无遮,能动的只有机械和通风
    (define roster-max 3)           ; 后台名册:每一格提前看见首演那张表上的一条

    ;; ── 状态 ────────────────────────────────────────
    ;; 0=未开场 1=小节一·交割 2=小节二·老街 3=平静期 4=小节三 5=首演之后
    ;; 后续小节在各自批次接入,不预留空壳。
    (define story-stage 0)
    (define delivery-day 0)         ; 交割日的世界日,开场当天算出
    (define delivery-pending? #f)   ; 交割日已到、尚未处理
    (define delivery-result "未定") ; 未定 / 拦下 / 跟丢
    (define delivery-money 0)       ; 交割那夜从街上捡回来的钱
    ;; 半包「老金牌」和巷子里拿到的照片/底片都做成物品,不做成脚本内部的布尔量:
    ;; 它们在虚构里就是揣在兜里、能递给别人的东西,做成物品玩家才在物品栏里一直看得见。
    ;; 判断线：能不能放进兜里、递给别人、被人拿走。是就做物品;
    ;; 「已经查清了」「已经交给萨姆了」这种没有实体的,仍旧留在状态里。
    (define cigs-item "半包「老金牌」")
    (define eddie-address-item "埃迪的地址纸条")
    (define (runner-cigarettes?) (> (item-count cigs-item) 0))
    (define delivery-shortfall -1)  ; -1=尚未装包;0=玩家备足;正数=夜莺补上的差额
    (define joe-mail-tip? #f)       ; 乔那一格已经拿过(只给一次)
    (define singer-mail-tip? #f)    ; 夜莺那一格已经拿过(只给一次)
    (define condition-level 0)      ; 夜莺处境 0=稳定 1=不安 2=受伤
    (define trust 0)                ; 她对你的信任
    (define song-day 0)             ; 最近一次请她唱歌的世界日
    (define familiar 0)             ; 老街熟脸 0..familiar-max
    (define west-friction-cleared? #f) ; 西侧楼梯走完后出现的阻碍已经解决
    (define union-checked? #f)      ; 已按埃迪的东边地址排除弗兰克
    (define bridge-identified? #f)  ; 已按埃迪的西边地址从桥廊住户处问出莱恩
    (define envelope-thin? #f)      ; 交割那夜信封没凑够——他回去数过了
    (define patience-day 0)         ; 上一次经理耐心掉格的世界日
    (define report-pending "")      ; 完成的小节等待向经理汇报："" / "查明" / "巷子"
    (define inquiry-day 0)          ; 经理的人开始找莱恩那天
    (define inquiry-told? #f)       ; 三天到期的那条消息已经播过
    (define lesson-done? #f)        ; 巷子那一场已经了结
    (define lesson-day 0)           ; 了结当天的世界日
    (define lesson-state 0)         ; 「他撒手」钟收手时停在第几格 0..6，就是那一晚的收获
    (define lesson-forced? #f)      ; 是被架出去的,不是自己收的手
    (define lesson-extra? #f)       ; 搜身翻出了他没打算给你的东西
    (define material-settled? #f)   ; 小节二是否已经了结(查明身份)
    (define settle-route "无")      ; 查明 / 无
    (define settle-quality "无")    ; 好 / 中
    (define lyon-fate "无")         ; 逃走 / 被释放 / 被扣押
    (define settled-day 0)          ; 小节二结算当天的世界日
    ;; 首演之夜请到的人手。各有各的脾气,那天晚上他们只做自己那一份。
    (define aide-joe? #f)           ; 乔：守外圈的门,不进场
    (define aide-frank? #f)         ; 弗兰克的人：压后廊,不等你下令
    (define aide-police? #f)        ; 阿瑟的警察：清内环,不管她
    (define aide-usher? #f)         ; 剧院领班：引人群,被推一次就不干
    (define premiere-done? #f)      ; 首演之夜已结算
    (define scene-flags '())

    ;; ── flag 登记 ───────────────────────────────────
    ;; 未登记的 flag 直接报错,避免拼错字悄悄变成一个新状态。
    (define (flag-id flag)
      (cond
        ((or (equal? flag '交割已结算) (equal? flag "交割已结算")) "交割已结算")
        ((or (equal? flag '伤后探望) (equal? flag "伤后探望")) "伤后探望")
        ((or (equal? flag '第二封信来电) (equal? flag "第二封信来电")) "第二封信来电")
        ((or (equal? flag '第二封信) (equal? flag "第二封信")) "第二封信")
        ((or (equal? flag '她的过去) (equal? flag "她的过去")) "她的过去")
        ((or (equal? flag '留下的信) (equal? flag "留下的信")) "留下的信")
        ((or (equal? flag '她说起莱恩) (equal? flag "她说起莱恩")) "她说起莱恩")
        ((or (equal? flag '第三封信来人) (equal? flag "第三封信来人")) "第三封信来人")
        ((or (equal? flag '前段报酬) (equal? flag "前段报酬")) "前段报酬")
        ((or (equal? flag '看彩排) (equal? flag "看彩排")) "看彩排")
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

    ;; 第一章期间她始终在老街酒馆驻唱。交割失利会改变她的处境，
    ;; 但不撤掉这份日常职能；首演结束后才随章节一起结束。
    (define (singer-present?)
      (and (>= story-stage 1) (<= story-stage 4)
           (not premiere-done?)))

    ;; 老街居民区从小节一结算后开放；警察局与货运公司留到首演威胁明确后。
    ;; 酒馆是例外:她在那儿唱歌,开场就得能找到人(见 world.scm 的地点表)。
    (define (old-street-open?) (>= story-stage 2))
    ;; 小节二由交割后的来访开启：夜莺已经把方向和她自己的过去交给你，调查不能再等经理。
    (define (beat2-open?)
      (and (= story-stage 2) (has-flag? '伤后探望) (not material-settled?)))
    (define (trust-met?) (>= trust trust-threshold))

    (define (second-letter-call-due?)
      (and (= story-stage 2) (has-flag? '伤后探望)
           (not (has-flag? '第二封信来电)) (not (has-flag? '第二封信))
           (>= world-day second-letter-day)))

    (define (second-letter-pending?)
      (and (= story-stage 2) (has-flag? '第二封信来电) (not (has-flag? '第二封信))))

    ;; 剧院从经理等候的那天起开放；那封信和经理都在那里，不该从世界根节点跳出来。
    (define (theater-open?) (or (>= story-stage 3) (has-flag? '第二封信) (second-letter-pending?)))
    ;; 小节三之后到第三封信之间的那几天:主线没有新压力。
    (define (quiet-period?) (and (= story-stage 3) lesson-done? (not (has-flag? '第三封信))))
    (define (third-letter-due?)
      (and (= story-stage 3) lesson-done? (not (has-flag? '第三封信))
           (>= (- world-day lesson-day) quiet-days)))
    (define (beat3-open?) (and (= story-stage 4) (not premiere-done?)))
    (define (police-open?) (beat3-open?))
    (define (freight-open?) (beat3-open?))
    (define (aide-count)
      (+ (if aide-joe? 1 0) (if aide-frank? 1 0)
         (if aide-police? 1 0) (if aide-usher? 1 0)))

    (define (validate-joe-aide!)
      (if (and aide-joe? (not (joe 'premiere-aide-eligible?)))
          (error "三封信存档错误：已经请乔守首演外圈，但乔的最终状态不能行动")
          #t))

    (define (validate-frank-aide!)
      (frank 'validate-premiere-request! aide-frank?))

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
      (set-global! '交割结果 delivery-result)
      (set-global! '老街熟脸 familiar)
      (set-global! '小节二路线 settle-route)
      (set-global! '小节二结果 settle-quality)
      (set-global! '莱恩下落 lyon-fate)
      (set-global! '夜莺信任达标 (trust-met?))
      ;; 首演交锋读这几项：三个环的准备格数决定麻烦软到什么程度，
      ;; 请到的人决定那天晚上谁站在哪个环上。
      (set-global! '准备-外圈 (outer-ring-clk 'current))
      (set-global! '准备-内环 (inner-ring-clk 'current))
      (set-global! '准备-中央 (core-ring-clk 'current))
      ;; 乔是否真的到场必须同时读取“请过他”与人物最终命运，不能让旧的人手布尔量越过死亡/未介入状态。
      (set-global! '乔最终状态 (joe 'final-state))
      (set-global! '人手-乔 (and aide-joe? (joe 'premiere-aide-eligible?)))
      (set-global! '人手-弗兰克 aide-frank?)
      (set-global! '人手-警察 aide-police?)
      (set-global! '人手-领班 aide-usher?))

    (define (advance-stage! new-stage)
      (set! story-stage new-stage)
      (sync-globals!))

    (define (report-pending?)
      (not (equal? report-pending "")))

    (define (pending-report-action-name)
      (cond
        ((equal? report-pending "查明") "报告莱恩身份")
        ((equal? report-pending "巷子") "报告巷子结果")
        (else (error "三封信：没有可显示的待汇报结果"))))

    (define (pending-report-subtitle)
      (cond
        ((equal? report-pending "查明")
         "把莱恩的名字、来路和夜莺与他的关系交代给经理；不消耗行动骰")
        ((equal? report-pending "巷子")
         "把莱恩是否收手、取回了什么以及巷子里的动静交代给经理；不消耗行动骰")
        (else (error "三封信：没有可显示的待汇报说明"))))

    (define (report-critical?)
      (and (report-pending?) (<= (patience-clk 'current) 1)))

    (define (mark-report-pending! report-id)
      (if (report-pending?)
          (error "三封信：上一份结果尚未汇报，不能覆盖待汇报状态")
          #t)
      (if (member? report-id (list "查明" "巷子"))
          #t
          (error "三封信：试图登记未定义的汇报结果"))
      (set! report-pending report-id)
      (result-note! (string-append "可汇报：" (pending-report-action-name))))

    (define (worsen-condition! n)
      (set! condition-level (min 2 (+ condition-level n)))
      (sync-globals!))

    ;; ── 阻塞同步 ────────────────────────────────────
    ;; 必看的拍子当晚不看完不能睡。读档后由 world-load! 统一重新注册。
    (define (sync-blockers!)
      ;; 先撤掉本故事上一拍留下的阻塞，再按当前状态注册唯一有效的一项。
      ;; 否则交割完成后旧的「去码头盯邮箱」会残留；下一拍做完后便既不能休息，
      ;; 也没有强制剧情动作，形成软锁。
      (rest-release! "三封信/开场敲门")
      (rest-release! "三封信/交割日")
      (rest-release! "三封信/伤后探望")
      (rest-release! "三封信/第二封信")
      (rest-release! "三封信/她说起莱恩")
      (rest-release! "三封信/经理汇报")
      (rest-release! "三封信/第三封信")
      (rest-release! "三封信/她不取消")
      (rest-release! "三封信/首演")
      (rest-release! "三封信/结案")
      (cond
        ((= story-stage 0)
         (rest-block! "三封信/开场敲门" "有人敲门" "家" "有人敲门"))
        (delivery-pending?
         (rest-block! "三封信/交割日" "去码头交割" "码头" "去码头盯着邮箱"))
        ((and (= story-stage 2) (not (has-flag? '伤后探望)))
         (rest-block! "三封信/伤后探望" "她在门外等你" "家" "她来看你"))
        ((second-letter-pending?)
         (rest-block! "三封信/第二封信" "剧院经理要见你" "剧院" "见剧院的经理"))
        ((and (= story-stage 3) (not (has-flag? '她说起莱恩)))
         (rest-block! "三封信/她说起莱恩" "她要讲莱恩" "家" "听她讲莱恩"))
        ((report-critical?)
         (rest-block! "三封信/经理汇报" "经理的耐心已经见底" "剧院"
                      (pending-report-action-name)))
        ((third-letter-due?)
         (rest-block! "三封信/第三封信" "去剧院看信" "剧院" "去剧院看那封信"))
        ((and (beat3-open?) (not (has-flag? '她不取消)))
         (rest-block! "三封信/她不取消" "她在剧院等你" "剧院" "她要当面跟你讲"))
        (premiere-pending?
         (rest-block! "三封信/首演" "今晚首演" "剧院" "去剧院"))
        ((and (= story-stage 5) (not (has-flag? '结案)))
         (rest-block! "三封信/结案" "剧院外有人等你" "剧院" "散场之后"))
        (else #t)))

    ;; ── 开场：她找上门 ──────────────────────────────
    ;; 她不是经理介绍来的——老街的人脉听说旅馆住了个新来的侦探。
    ;; 一个穷歌女请得起的,刚好是一个穷侦探。
    (define (node-answer-door)
      (instant-action "有人敲门"
        (lambda ()
          ;; 敲门这一下是过场影片演的，不是文字讲的。tag 对上场景里那条 CutsceneSequence；
          ;; 没配镜头时会退回一段占位时长，剧本先行、镜头后补是常态。
          (play-animation! "来访")
          (play-remote-dialogue!
            (line "夜莺" "你是那个新搬来的侦探？我打听过了，这条街上只有你收得起我这样的价钱。")
            (line "尼尔" "什么事？")
            (line "夜莺" "有人给我写信。他要钱，不然就把我从前的事抖出去。")
            (line "夜莺" "钱放码头的邮箱，三天后。我想知道是谁写的，然后我想让他别再来了——不是这一次，是往后都别再来。")
            (line "尼尔" "为什么不报警？")
            (line "夜莺" "我不能。"))
          (add-item! "金钱" prepayment)
          (set! delivery-day (+ world-day letter-deadline))
          (advance-stage! 1)
          (rest-release! "三封信/开场敲门")
          (spotlight! "交割委托"
            "她付了三十金。信上要一百，三天后交到码头邮箱；署名没有，字迹刻意工整。"))))

    ;; ── 小节一·交割：一条不给钱的调查线 ─────────────
    ;; 钱来自整座城市的现有工作。这里只加一条**踩点**：它一分钱不挣,投进去的骰子
    ;; 换的是交割日那晚的形势。玩家每天要答的就是这一句——这颗骰子拿去挣钱,还是拿去踩点。
    ;; 一根 0/6 的钟,两个里程碑,前三格认人、后三格认地,恰好对上交锋的两幕:
    ;;   3 格「认得那张脸」—— 第一幕蹲守时,真邮差变成一个不花骰子就能认出的人
    ;;   6 格「这一片你熟了」—— 另外两个住户也是;第二幕多一条只有你知道的边门
    ;; 六格是按小节一只有三天定的:0/8 在这个窗口里长得没人填得满。
    ;; 不做成两根 0/3:这是全章第一个进度结构,两根钟在教学位置上读起来太重。
    ;; 也不做成 0/2:两颗骰子就满,加上乔和夜莺白送的两格等于不用付钱——那不是投资,是签到。
    ;; 要改长短,改下面这一行的 6 和 mail-routine-fact 的句子。
    (define scout-max 6)
    (define scout-face 3)           ; 认人那一半的里程碑

    (define mail-clock
      (make-clock "踩点" scout-max 'segments
        (lambda (current max)
          (cond
            ((>= current max)
             "这一片你熟了。交割日那晚，住这儿的几个人你一眼就认得出，不必费神；追起来还知道货栈边门在哪儿。")
            ((>= current scout-face)
             "你认得这片真正的邮差了——那晚不用花力气分辨他。再摸熟这一片，追逐里还能多一条路。")
            (else
             "每摸清一件事填一格。3 格认得真邮差，6 格摸熟这一片——两个价钱，都在交割日那晚兑现。")))))

    (define (mail-routine-full?) (mail-clock 'full?))
    (define (scout-knows-face?) (>= (mail-clock 'current) scout-face))

    ;; 城寨的五根钟各有明确 owner：四个空间前沿与埃迪。
    ;; 前沿满格后由更深处的新前沿或永久结果取代；完成过的路线本身不留空节点。
    (define east-entry-clk
      (make-clock "东侧门廊" warren-leg-max 'segments
        "穿过被加盖吞掉的入口。填满后发现埃迪，新前沿会移动到门廊深处。"))

    (define east-gallery-clk
      (make-clock "东侧回廊" warren-leg-max 'segments
        "从埃迪的铺子继续往里认路。填满后发现工会房间。"))

    (define west-stairs-clk
      (make-clock "西侧楼梯" warren-leg-max 'segments
        "沿住户共用的楼梯往上。填满后会抵达有人守着的平台。"))

    (define west-bridge-clk
      (make-clock "上层桥廊" warren-leg-max 'segments
        "越过楼梯口以后继续深入。填满后发现桥廊公寓。"))

    (define merchant-trust-clk
      (make-clock "埃迪的信任" merchant-trust-max 'segments
        (lambda (current max)
          (if (>= current max)
              "埃迪愿意把两笔「老金牌」的去向告诉你。"
              "说清你为什么追这包烟。每到 2、4、6、8 格，他会多说一层。"))))

    ;; 小节四的三个环。每一格是具体的一件事，填格时用 result-note! 说出来。
    ;; 满格的环，首演那张表上属于它的第一条直接不发生；每 2 格让该环的麻烦多撑一回合。
    (define outer-ring-clk
      (make-clock "外圈的门与后廊" ring-max 'segments
        (lambda (current max)
          (if (>= current max)
              "五六扇门都过了一遍。那道后廊的门今晚不会被撬开。"
              "插销、值夜的人、后廊堆的杂物。满格能少掉一整处麻烦。"))))

    (define inner-ring-clk
      (make-clock "内环走道与看台" ring-max 'segments
        (lambda (current max)
          (if (>= current max)
              "疏散路线清过了，栏杆也钉死了。前排今晚翻不起来。"
              "疏散路线、引座的人、翻得过去的栏杆。满格能少掉一整处麻烦。"))))

    (define core-ring-clk
      (make-clock "中央台与升降口" core-max 'segments
        (lambda (current max)
          (if (>= current max)
              "升降台的行程和台底的通风都动过手脚。中央台能做的到此为止。"
              "圆台四面无遮：没有门可封，没有走道可清。能动的只有机械和台底那点风。"))))

    (define roster-clk
      (make-clock "后台名册" roster-max 'segments
        (lambda (current max)
          (if (>= current max)
              "通行证、临时工、这个月换过的人，你都对过一遍了。"
              "写信的人知道换装的顺序，说明他就在这栋楼里。每查清一件事，那天晚上你就少瞎一只眼。"))))

    (define (delivery-fund-clock)
      (list
        (list 'clock "交割款" (min delivery-price (item-count "金钱"))
              delivery-price 'pie
              (string-append
                "交割日要从库存里拿出一百金。钱仍可挪作房租和生活开销。"
                "夜莺最多只能当掉 " (number->string nightingale-cover-max)
                " 金替你补：差在这个数以内，她当首饰，处境差一档；"
                "差得更多，信封就是薄的，写信的人清点之后会先动手。"))))

    ;; 每一格是一件具体的事,不是一段百分比。填格时把这件事说出来,
    ;; 玩家才知道自己「知道了什么」,而不只是看着条往上涨。
    ;; 事实文本是内容,留在这里;格数的加减归时钟自己管。
    ;; 前三格是人,后三格是这片地方——后半段顺带是小节二的预告片:
    ;; 玩家还没走进城寨,已经开始认得它的外墙。
    (define (mail-routine-fact n)
      (cond
        ((= n 1) "知道了：真正的邮差每天下午才来这一片")
        ((= n 2) "知道了：他隔天换班，换的人也走同一条线")
        ((= n 3) "认得那张脸了：车铃坏的那辆，你不会再认错")
        ((= n 4) "知道了：邮箱背后那条巷子一直通到货栈")
        ((= n 5) "知道了：货栈的边门天黑落锁，可锁舌是坏的")
        ((= n 6) "这一片你熟了：后街那几户都是常年住这儿的，往东是门廊，往西是楼梯")
        (else "")))

    (define (advance-mail-routine! delta)
      (let ((old (mail-clock 'current)))
        (mail-clock 'advance! delta)
        (if (> (mail-clock 'current) old)
            (result-note! (mail-routine-fact (mail-clock 'current)))
            #f)))

    ;; 踩点不是工作:它不发工钱,也不给势力关系。这是它和普通工作唯一也是最重要的区别。
    ;; 中和好都只填一格——情报不该靠运气发;两档的差别记在**冷静**上:
    ;; 在雨里站一晚上是要还的。冷静只有 2 点,于是一天之内也踩不了几次点,
    ;; 身体自己就是这条线的节流阀,不必再写一条每日上限。
    ;; 动作只有一个,标题和 subtitle 随进度走三段面貌(和城寨的前沿推进同一套语法)。
    (define (scout-node-name)
      (let ((n (mail-clock 'current)))
        (cond
          ((< n 2) "混进邮务站清点")
          ((< n scout-face) "邮箱附近踩点")
          (else "对街守到天黑"))))

    (define (scout-node-subtitle)
      (let ((n (mail-clock 'current)))
        (cond
          ((>= n scout-max) "这一片你已经熟透了，再蹲也蹲不出新东西")
          ((< n 2) "敏锐；不挣钱。排班表就摊在桌上，谁走哪条线一眼看得见")
          ((< n scout-face) "敏锐；不挣钱。再认清一件事，交割日你就不必费神分辨邮差")
          (else "敏锐；不挣钱。巷子、边门、住在这儿的都有谁"))))

    (define (node-scout)
      (node (scout-node-name)
        :subtitle (scout-node-subtitle)
        :clocks (list (mail-clock 'render-data))
        :requires (list (req-die))
        :disabled (mail-routine-full?)
        :resolve (roll 'sharpness
          (outcome "白站了一晚上"
            (lambda () (spend-composure! 1)))
          (outcome "看明白一件事"
            (lambda () (advance-mail-routine! 1) (spend-composure! 1)))
          (outcome "看明白一件事，也没人注意到你"
            (lambda () (advance-mail-routine! 1))))))

    ;; 乔那一格：不花骰子，也不靠判定，但要跟他一起干过两班活他才会聊这个。
    ;; 头一天认得你不算数——码头上的事，他只跟一起卸过货的人说。
    (define joe-tip-favor 2)

    (define (node-joe-mail-tip)
      (node "问乔邮箱的事"
        :clocks (list (mail-clock 'render-data))
        :resolve (instant (lambda ()
          (set! joe-mail-tip? #t)
          (play-dialogue!
            (line "尼尔" "街口那个邮箱，平时谁来取？")
            (line "乔" "老头子，姓什么我不知道。下午来，车铃坏的那辆。")
            (line "乔" "上礼拜换过一回人，也是那个点。你问这个干什么？")
            (line "尼尔" "有人在那儿等一封不该他拿的信。"))
          (advance-mail-routine! 1)
          (sync-globals!)))))

    ;; 夜莺那一格:她在那一带长大。和乔那一格同构——不花骰子、不掷判定,
    ;; 兑现的是人物关系本身。一格来自你新认识的人,一格来自你的委托人:
    ;; 认识谁,本身就是情报的一部分。
    (define (node-singer-mail-tip)
      (node "问她邮箱那一片"
        :clocks (list (mail-clock 'render-data))
        :resolve (instant (lambda ()
          (set! singer-mail-tip? #t)
          (play-dialogue!
            (line "尼尔" "码头那个邮箱，背后通哪儿？")
            (line "夜莺" "巷子。一直通到货栈。小时候抄近路都走那儿。")
            (line "夜莺" "货栈有个边门，天黑落锁——可那锁舌早就是坏的。")
            (line "尼尔" "你还记得。")
            (line "夜莺" "在那儿长大的人都记得。"))
          (advance-mail-routine! 1)
          (sync-globals!)))))

    (define (beat1-dock-nodes)
      (if (= story-stage 1)
          (append
            (list (node-scout))
            (if (and (>= (joe 'favor) joe-tip-favor)
                     (not joe-mail-tip?)
                     (not (mail-routine-full?)))
                (list (node-joe-mail-tip))
                '()))
          '()))

    (define (beat1-tavern-nodes)
      (if (and (= story-stage 1)
               (not singer-mail-tip?)
               (not (mail-routine-full?)))
          (list (node-singer-mail-tip))
          '()))

    ;; ── 交割日 ──────────────────────────────────────
    ;; 到期当天不自动播放:它是必看事件,用阻塞休息逼玩家亲自去。
    (define (begin-delivery!)
      (if delivery-pending?
          (error "三封信：交割日已经在等待处理")
          #t)
      (set! delivery-pending? #t)
      (sync-blockers!)
      (notify! "今天是信上写的日子。凑好的钱得放进邮箱，你得在那儿盯着。"))

    ;; 她的兜底是有限的：一副首饰当出去就那么多钱，不是一张无限额的保险单。
    ;; 缺口在她能补的范围内 → 她当首饰，处境差一档。
    ;; 缺口超过她能补的 → 信封是薄的，写信的人清点之后开始在老街散照片，
    ;; 小节二一上来就少三天。代价全部落在城里，不进交锋。
    (define (nightingale-covers) (min delivery-shortfall nightingale-cover-max))
    (define (envelope-short) (max 0 (- delivery-shortfall nightingale-cover-max)))

    (define (prepare-delivery-money!)
      (let ((available (item-count "金钱")))
        (set! delivery-shortfall (max 0 (- delivery-price available)))
        (spend-up-to! "金钱" delivery-price)
        (if (> delivery-shortfall 0)
            (worsen-condition! 1)
            #f)
        (if (> (envelope-short) 0)
            (set! envelope-thin? #t)
            #f)
        ;; 交锋只读这一个数,两个里程碑由交锋自己按 4 / 8 判断——
        ;; 免得城市和交锋各存一份平行的"我知道多少"。
        (set-global! '踩点格数 (mail-clock 'current))))

    ;; 入场剧情由调用方播放:交锋脚本把「你已经在追了」当既定前提。
    (define (node-delivery-entry)
      (encounter-action "去码头盯着邮箱"
        (lambda ()
          (prepare-delivery-money!)
          (cond
            ((= delivery-shortfall 0)
             (play-dialogue!
               (line "夜莺" "一百，够数。")
               (line "尼尔" "放进去以后别回头，一直走到电车站。")))
            ((= (envelope-short) 0)
             (play-dialogue!
               (line "夜莺"
                 (string-append "还差 " (number->string delivery-shortfall)
                                "。我把首饰押了，正好够。"))
               (line "尼尔" "这笔差额算我的，等这件事完了还你。")
               (line "夜莺" "先别说这个。放完我就走。")))
            (else
             (play-dialogue!
               (line "夜莺"
                 (string-append "我能当的都当了，也只凑出 "
                                (number->string nightingale-cover-max)
                                "。还差 " (number->string (envelope-short)) "。"))
               (line "尼尔" "那就这么放进去。他不会当街数。")
               (line "夜莺" "他回去会数的。")
               (line "尼尔" "那就让他数。到时候他得先来找我。"))))
          (start-encounter "交割" on-delivery-result))))

    ;; 交锋回传：(list 人 钱 注意到摩托车?)。前两个轴保持原有主线语义；
    ;; 第三项只写入弗兰克人物线的疑点，不改变交割结算。
    ;; 六种组合都让故事往前走——交锋失败留疤,不阻断主线。
    (define (on-delivery-result result)
      (if (not delivery-pending?)
          (error "三封信：没有待处理的交割日")
          #t)
      (if (and (list? result) (= (length result) 3))
          #t
          (error "三封信：交割交锋应回传 (list 人 钱 注意到摩托车?)"))
      (set! delivery-pending? #f)
      (let ((caught (car result))
            (money (cadr result)))
        (if (or (= money 0) (= money 50) (= money 100))
            #t
            (error "三封信：交割交锋返回了非法的追款金额"))
        (frank 'on-delivery-chase! (list-ref result 2))
        (set! delivery-result
              (cond
                ((equal? caught '拦下) "拦下")
                ((equal? caught '跟丢) "跟丢")
                (else (error "三封信：交割交锋返回了未登记的人物结果"))))
        (set! delivery-money money)
        ;; 拦下他才拿得到他身上掉出来的东西：那半包烟是小节二唯一的具名抓手。
        ;; 跟丢则只剩一个方向，她的处境也跟着差一档。
        (if (equal? caught '拦下)
            (add-item! cigs-item 1)
            (worsen-condition! 1))
        (set-flag! '交割已结算)
        (advance-stage! 2)
        (complete-section!)
        (sync-globals!)
        (sync-blockers!)
        (spotlight! "交割之后"
          (string-append
            (if (equal? caught '拦下)
                "跑腿的不是写信人。老金牌烟指向一个穿旧西装的买主。"
                "你跟丢了他，只知道方向在码头居民区。")
            (cond
              ((= money 100) "钱一分不少地拿回来了。")
              ((= money 50) "拿回了一半，五十金。")
              (else "钱一分没剩。"))
            (if (> (envelope-short) 0)
                "他会知道钱不够。"
                "")))))

    ;; ── 结算后的人物戏(必看) ────────────────────────
    (define (node-her-visit)
      (instant-action "她来看你"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "伤着了？")
            (line "尼尔" "不要紧。钱没能全拿回来。")
            (line "夜莺" "那些钱本来就是破财消灾。")
            (line "夜莺" "我担心的不是这个。他们还会来吗？会不会要得更多？")
            (line "尼尔" "我要去码头居民区一趟。")
            (line "夜莺" "我在那里长大。东边门廊通向工会房间，西边楼梯上去是桥廊。外人进去，两边都不会好走。")
            (line "夜莺" "在那之前你先把伤养好。"))
          (set-flag! '伤后探望)
          (rest-release! "三封信/伤后探望")
          (sync-globals!)
          (spotlight! "老街"
            "她望着窗外。明天起，你可以去老街。"))))

    ;; ── 小节二触发：第二封信(必看) ──────────────────
    ;; 信寄到了剧院,经理因此第一次介入。他出钱,也开始用他自己的方式处理——
    ;; 他的利益是首演成功、品牌不受损,和她的利益从此不完全重合。
    (define (begin-second-letter-call!)
      (set-flag! '第二封信来电)
      (play-remote-dialogue!
        (line "世界" "天刚亮，旅馆前台的电话就响了。柜台伙计敲门，说剧院找你，已经打了两遍。")
        (line "剧院接线员" "是那位侦探吗？经理让你今天务必过来。又有一封信，这次寄到了剧院。")
        (line "尼尔" "夜莺看过了吗？")
        (line "剧院接线员" "经理没让人把它拿给她。先生，请你尽快。"))
      (sync-blockers!))

    (define (node-second-letter)
      (instant-action "见剧院的经理"
        (lambda ()
          (play-remote-dialogue!
            (line "经理" "这封信寄到了剧院的收发室。收发室的姑娘拆开了，念了两行才反应过来。")
            (line "经理" "他这次要的数目翻了一倍。还附了一张照片——裁过的，只留下半个人。")
            (line "经理" "他说钱要她自己送到老街去。")
            (line "尼尔" "他想让她穿着好衣服回那个地方低头。")
            (line "经理" "我不关心他想什么。我关心的是三个星期以后那张海报上的名字还值不值钱。")
            (line "尼尔" "那就先弄清楚他是谁。躲在匿名信后面的人，只有在你叫得出他名字的时候才好办。")
            (line "经理" "那你去弄清楚。钱我出一部分。"))
          (set-flag! '第二封信)
          (patience-clk 'set! (patience-clk 'max))
          (set! patience-day world-day)
          (rest-release! "三封信/第二封信")
          (sync-globals!)
          (sync-blockers!)
          (spotlight! "第二封信"
            (string-append
              (if (runner-cigarettes?)
                  "老金牌烟和跑腿人的话都指向老街。"
                  "线索只剩老街。")
              (if envelope-thin?
                  "他已知道钱不够。"
                  "在他再动手前，找出他。")))
          ;; 玩家若已在前几天查到莱恩，经理到场后立刻完成这一节的结算，不能让阶段停在旧状态。
          (if (warren-done?) (settle-beat2!) #f))))

    ;; ── 小节二·城寨探索 ─────────────────────────────
    ;; 四个动作表示玩家当前能抵达的空间前沿。每段走完就被下一段或永久结果取代，
    ;; 因而卡片在 Unity 中也从一个 Anchor 移到另一个 Anchor；它们不是 UI 容器层级。
    (define (crossed? old new mark) (and (< old mark) (>= new mark)))
    (define (east-merchant-found?) (east-entry-clk 'full?))
    (define (east-route-done?) (east-gallery-clk 'full?))
    (define (west-stairs-done?) (west-stairs-clk 'full?))
    (define (west-route-done?) (west-bridge-clk 'full?))
    (define (west-friction-pending?)
      (and (west-stairs-done?) (not west-friction-cleared?)))
    (define (merchant-trusted?) (merchant-trust-clk 'full?))
    (define (warren-done?) bridge-identified?)

    (define (advance-east-entry! n)
      (let ((old (east-entry-clk 'current)))
        (east-entry-clk 'advance! n)
        (let ((new (east-entry-clk 'current)))
          (if (crossed? old new 4)
              (begin
                (result-note! "发现人物：门廊杂货商埃迪")
                (spotlight! "门廊里的铺子"
                  "埃迪守着门廊柜台。东侧回廊在他后面。")
                (play-banter!
                  (line "埃迪" "找路就看门牌。找人的话，别挡在我柜台前面。")))
              #f))))

    (define (advance-east-gallery! n)
      (let ((old (east-gallery-clk 'current)))
        (east-gallery-clk 'advance! n)
        (let ((new (east-gallery-clk 'current)))
          (if (crossed? old new 4)
              (begin
                (result-note! "发现地点：工会房间")
                (spotlight! "东侧走通了"
                  "东侧通向工会房间。码头的消息都在这里过一遍。"))
              #f))))

    (define (advance-west-stairs! n)
      (let ((old (west-stairs-clk 'current)))
        (west-stairs-clk 'advance! n)
        (let ((new (west-stairs-clk 'current)))
          (if (crossed? old new 4)
              (begin
                (result-note! "出现阻碍：楼梯口的人")
                (spotlight! "楼梯口被堵住了"
                  "几个人堵住楼梯。他们不想让你替她继续问下去。"))
              #f))))

    (define (advance-west-bridge! n)
      (let ((old (west-bridge-clk 'current)))
        (west-bridge-clk 'advance! n)
        (let ((new (west-bridge-clk 'current)))
          (if (crossed? old new 4)
              (begin
                (result-note! "发现地点：桥廊公寓")
                (spotlight! "西侧走通了"
                  "西侧通向桥廊公寓。洛蒂记得这里的人。"))
              #f))))

    ;; 经理的耐心：一根有脸的倒计时。它不是均匀流逝的——被具体的事扣掉,
    ;; 也能靠「让他觉得这件事是他推动的」补回来(他要的就是这个)。
    (define patience-clk
      (make-clock "经理的耐心" patience-max 'countdown
        "每次休息后都会减一格；事情闹得剧院难看也会额外扣减。归零他换人,委托到此为止。"))

    (define (node-east-entry)
      (node "穿过东侧门廊"
        :subtitle "从东侧入口往里认路；门廊深处有人做生意"
        :tags (list "低风险")
        :clocks (list (east-entry-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome "路又被封住了"
            (lambda () (spend-composure! 1)))
          (outcome "认出了一段路"
            (lambda () (advance-east-entry! 1)))
          (outcome "看懂了这片门廊"
            (lambda () (add-familiar! 1) (advance-east-entry! 2))))))

    (define (node-east-gallery)
      (node "深入东侧回廊"
        :subtitle "埃迪的铺子后面还有一段常亮着灯的回廊"
        :tags (list "低风险")
        :clocks (list (east-gallery-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome "又绕回杂货铺"
            (lambda () (spend-composure! 1)))
          (outcome "跟住送货的人"
            (lambda () (advance-east-gallery! 1)))
          (outcome "看懂回廊的用途"
            (lambda () (add-familiar! 1) (advance-east-gallery! 2))))))

    (define (node-west-stairs)
      (node "沿西侧楼梯往上走"
        :subtitle "从西侧入口往上；楼梯连着住户的厨房和共用平台"
        :tags (list "低风险")
        :clocks (list (west-stairs-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "被挡了回来"
            (lambda () (spend-composure! 1)))
          (outcome "他们让开了半级台阶"
            (lambda () (advance-west-stairs! 1)))
          (outcome "有人给你指了近路"
            (lambda () (add-familiar! 1) (advance-west-stairs! 2))))))

    (define (node-west-bridge)
      (node "穿过上层桥廊"
        :subtitle "楼梯口已经让开；沿挂在两栋楼之间的桥廊继续往里"
        :tags (list "低风险")
        :clocks (list (west-bridge-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "房门一扇接一扇关上"
            (lambda () (spend-composure! 1)))
          (outcome "有人默许你过去"
            (lambda () (advance-west-bridge! 1)))
          (outcome "洛蒂给你指了门"
            (lambda () (add-familiar! 1) (advance-west-bridge! 2))))))

    (define (resolve-west-friction!)
      (set! west-friction-cleared? #t)
      (result-note! "阻碍解除：西侧楼梯")
      (spotlight! "楼梯口让开了"
        "椅子拖回墙边。楼梯通向上层桥廊。")
      (if (has-flag? '她的过去)
          (sync-globals!)
          (begin
            (set-flag! '她的过去)
            (gain-trust! 1)
            (play-remote-banter!
              (line "世界" "他们收起椅子以前只说了一句：她既然走了，就不该叫外人回来问路。当天晚上，你把这句话带回了酒馆。")
              (line "夜莺" "西楼梯尽头那段桥廊，我在那里住到十六岁。冬天风从地板缝里往上吹，得拿报纸把缝全糊住。")
              (line "夜莺" "后来我在这条街的酒吧唱了六年。没人来听，来的人也不是来听的。")
              (line "尼尔" "所以你走了。")
              (line "夜莺" "有一天我算了一笔账。我再唱六年，还是站在同一块地板上。")
              (line "夜莺" "走的时候我没跟谁道别。这就是他们记恨的事。")
              (line "尼尔" "值得吗？")
              (line "夜莺" "我现在至少知道，门外还有别的地方。")))))

    (define (node-west-friction)
      (node "应付楼梯守门人"
        :subtitle "他们把椅子横在路中间；想继续往上，就得让这场面对你有个结果"
        :tags (list "高风险")
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "事情动了手"
            (lambda () (injure!)))
          (outcome "他们勉强让开"
            (lambda () (spend-composure! 1) (resolve-west-friction!)))
          (outcome "你没有把它变成一场架"
            (lambda () (add-familiar! 1) (resolve-west-friction!))))))


    (define (grant-merchant-addresses!)
      (if (> (item-count eddie-address-item) 0)
          (error "三封信：埃迪的地址纸条不能重复结算")
          #t)
      (add-item! eddie-address-item 2)
      (result-note! "获得线索：埃迪的地址纸条 ×2"))

    (define (advance-merchant-trust! n)
      (let ((old (merchant-trust-clk 'current)))
        (merchant-trust-clk 'advance! n)
        (let ((new (merchant-trust-clk 'current)))
          (cond
            ((crossed? old new 2)
             (play-banter!
               (line "埃迪" "替人拿钱的，这楼里多得是。")
               (line "尼尔" "我要找雇人的那个。")))
            ((crossed? old new 4)
             (play-banter!
               (line "埃迪" "他们关门，是怕收地的人，不是在护谁。")
               (line "尼尔" "我找到人就走。")))
            ((crossed? old new 6)
             (play-banter!
               (line "埃迪" "老金牌不摆柜台。近一个月，只有两笔订单。")
               (line "尼尔" "哪两笔？")))
            ;; 线索做成物品,不做成脚本内部的布尔量:这样「某处产出 → 某个动作需要它」
            ;; 和买烟、买药是同一套概念,玩家在物品栏里看得见自己手上有什么牌。
            ;; 两个地址是同一种线索物品的两份,东边与西边各消耗一份。
            ((crossed? old new 8)
             (begin
               (grant-merchant-addresses!)
               (play-banter!
                 (line "埃迪" "一箱送东边工会房；另一个买家从西边桥廊来。")
                 (line "埃迪" "两个地址都写下来了。照着纸条去找。"))))
            (else #f))
          (sync-globals!))))

    (define (node-buy-from-eddie)
      (node "从埃迪这里买烟"
        :subtitle "15 金；交锋中可在功能区抽烟，恢复 2 点冷静"
        :requires (list (req-item "金钱" 15))
        :resolve (instant
          (outcome "买了烟"
            (lambda ()
              (add-item! "香烟" 1)
              (if (not (merchant-trusted?))
                  (advance-merchant-trust! 1)
                  #f))))))

    ;; 不拿烟盒时，8 格信任通常要花 4 枚行动骰。烟盒不掷骰直接推进一半，
    ;; 但不会替玩家跳过后半段盘问；这让物证稳定地省下约一个工作日。
    (define (node-give-cigarettes-to-eddie)
      (node "递上半包老金牌"
        :subtitle "物证；不耗行动骰。埃迪留下烟盒，信任 +4"
        :requires (list (req-item cigs-item 1))
        :resolve (instant
          (outcome "埃迪认出了这包烟"
            (lambda ()
              (if (runner-cigarettes?)
                  #t
                  (error "三封信：递给埃迪的半包老金牌不在物品栏中"))
              (if (merchant-trusted?)
                  (error "三封信：埃迪已经交代了两笔订单，不能再递烟盒")
                  #t)
              (let ((old (merchant-trust-clk 'current)))
                (merchant-trust-clk 'advance! 4)
                (let ((new (merchant-trust-clk 'current)))
                  (record-clock-progress! "埃迪的信任" (- new old))
                  (if (crossed? old new 8)
                      (begin
                        (grant-merchant-addresses!)
                        (play-banter!
                          (line "尼尔" "取信人的衣袋里，掉出半包老金牌。")
                          (line "埃迪" "烟留下。东边工会房，西边桥廊——两个地址都在纸上。")))
                      (play-banter!
                        (line "尼尔" "取信人的衣袋里，掉出半包老金牌。")
                        (line "埃迪" "烟留下。你说。")))
                  (sync-globals!))))))))

    (define (node-earn-eddie-trust)
      (node "取信埃迪"
        :subtitle "从雇跑腿、旧西装和一封勒索信说起；每次消耗一枚行动骰"
        :tags (list "低风险")
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "他只相信了一点"
            (lambda () (spend-composure! 1) (advance-merchant-trust! 1)))
          (outcome "他肯听你说完"
            (lambda () (advance-merchant-trust! 2)))
          (outcome "他开始问细节"
            (lambda () (add-familiar! 1) (advance-merchant-trust! 2))))))

    (define (eddie-node)
      (node "埃迪"
        :subtitle "门廊杂货商；卖烟、火柴和日用品，也记得哪些货是替谁特别订的"
        :clocks (list (merchant-trust-clk 'render-data))
        :children
          (append
            (list (node-buy-from-eddie))
            (if (merchant-trusted?)
                (list (observe-action "他肯告诉你的事"
                        "埃迪给了两张地址纸条：一张指向东边工会房，一张指向西边桥廊。"))
                (append
                  (if (runner-cigarettes?)
                      (list (node-give-cigarettes-to-eddie))
                      '())
                  (list (node-earn-eddie-trust))))
            '())))


    (define (finish-union-inquiry!)
      (set! union-checked? #t)
      (add-familiar! 1)
      (frank 'meet!)
      (result-note! "排除：弗兰克不是雇跑腿的人")
      (play-dialogue!
        (line "尼尔" "埃迪的纸条指向这间房。那箱老金牌是谁收的？")
        (line "弗兰克" "会开完，我把烟留在桌上。烟盒转了一圈，人人都拿过。")
        (line "弗兰克" "有个人拿走了整包，是从西边桥廊下来的。")
        (line "弗兰克" "你找的不是我。要名字，就去桥廊问。"))
      (sync-globals!))

    (define (node-union-inquiry)
      (node "按东边地址找人"
        :subtitle "工会房里人来人往；放入一张埃迪的地址纸条，找出这笔烟的收货人"
        :requires (list (req-item eddie-address-item 1))
        :resolve (instant
          (outcome "排除了弗兰克"
            (lambda () (finish-union-inquiry!))))))

    (define (union-room-place)
      (container "工会房间"
        (append
          (if union-checked?
              (list (observe-action "那箱烟的去向"
                      "弗兰克在会后把烟留给众人。他不是你要找的人，但有人从桌上拿走了整包。"))
              (if (> (item-count eddie-address-item) 0)
                  (list (node-union-inquiry))
                  (list (node "按东边地址找人"
                          :subtitle "这里人太多；需要放入一张埃迪的地址纸条"
                          :disabled #t)))))))

    (define (finish-bridge-identity!)
      (set! bridge-identified? #t)
      (result-note! "查明身份：莱恩")
      (sync-globals!)
      (if (has-flag? '第二封信)
          (settle-beat2!)
          (spotlight! "名字有了"
            "买老金牌的人叫莱恩。等剧院来信，就能向经理交代。")))

    (define (finish-bridge-inquiry!)
      (play-dialogue!
        (line "尼尔" "埃迪的纸条写的是这段桥廊。旧西装，袖口磨白。")
        (line "洛蒂" "莱恩。他从前在码头做工，后来只剩那件旧西装。")
        (line "洛蒂" "他和那个姑娘住过同一段桥廊。她走了，他没走成。"))
      (finish-bridge-identity!))

    (define (node-bridge-inquiry)
      (node "按西边地址找人"
        :subtitle "桥廊住户太多；放入一张埃迪的地址纸条，让洛蒂认出对应的买家"
        :requires (list (req-item eddie-address-item 1))
        :resolve (instant
          (outcome "找到了莱恩"
            (lambda () (finish-bridge-inquiry!))))))

    (define (bridge-apartments-place)
      (container "桥廊公寓"
        (append
          (if bridge-identified?
              (list (observe-action "她叫出的名字"
                      "穿旧西装、从这里下去买老金牌的人叫莱恩。他早已搬走，但这里的人还记得他。"))
              (if (> (item-count eddie-address-item) 0)
                  (list (node-bridge-inquiry))
                  (list (node "按西边地址找人"
                          :subtitle "这里住户太多；需要放入一张埃迪的地址纸条"
                          :disabled #t))))
          (list (node "洛蒂"
                  :subtitle "桥廊公寓的老住户；她记得哪些人住过这里，也记得他们搬走时的样子"
                  :resolve (observe "她在门口择菜，桥上过一个人就抬一次眼。"))))))


    ;; ── 小节二·人物戏 ──────────────────────────────
    ;; 老街熟脸在这里兑现:熟脸够了,你才听得见他们真正在说什么。
    ;; 写短——这是一眼扫过去的东西,不是要人停下来读的段落。
    (define (node-lyon-talk)
      (observe-action "酒馆里的闲话"
        (if (>= familiar 2)
            "「那些东西该烧掉。」有人把杯子推开，走了。"
            "几个人正说着什么，看见你就散了。")))

    ;; ── 小节二·收场：查明身份 ───────────────────────
    ;; 桥廊调查完成时已经叫出了名字。这里只负责结账与推进章节，
    ;; 不重放一次身份揭晓；这样无论先查到名字还是先收到第二封信，都不会让不在场的人开口。
    (define (settle-beat2!)
      (set! material-settled? #t)
      (set! settle-route "查明")
      (set! settle-quality (if (>= familiar familiar-max) "好" "中"))
      (set! lyon-fate "逃走")
      (set! settled-day world-day)
      (mark-report-pending! "查明")
      (complete-section!)
      (set-flag! '留下的信)
      (sync-globals!)
      (sync-blockers!)
      (spotlight! "查明了"
        (string-append
          "埃迪的账和桥廊的邻居都指向莱恩。"
          (if (equal? settle-quality "好")
              "这里的人愿意开口。"
              "有几扇门以后不会再开。")
          "新行动：去剧院报告莱恩身份。")))

    ;; 三天到了：经理的人带回地址。这条消息点亮出发卡,它自己不花骰子。
    (define-turn-rule "经理的人回话"
      (lambda () (and (lesson-beat?) (not inquiry-told?) (inquiry-done?)))
      (lambda ()
        (set! inquiry-told? #t)
        (sync-globals!)
        (play-remote-dialogue!
          (line "经理" "找到了。货栈后面那一片，租屋，没有窗的那一间。")
          (line "经理" "他每天晚上都回去。我的人不进去——那不是他们的活。")
          (line "尼尔" "那是谁的活。")
          (line "经理" "我付钱给谁，就是谁的活。"))))

    ;; ── 她说起莱恩(必看) ────────────────────────────
    ;; 双重读法：第一遍是她终于肯说实话;第二遍回看,她没有劝你别去——
    ;; 她只是确认了你会去,并且确认了你会觉得这是件小事。她一句假话也没说。
    (define (node-her-and-lyon)
      (instant-action "听她讲莱恩"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "莱恩。我知道你早晚会查到这个名字。")
            (line "夜莺" "那段桥廊上就我们两个孩子。他比我大两岁，冬天替我把漏风的窗框钉过一遍。")
            (line "尼尔" "后来呢。")
            (line "夜莺" "后来我走了。走的时候没跟谁道别——包括他。")
            (line "夜莺" "他给我写过信。头两年还写。我一封也没回。")
            (line "尼尔" "你觉得他恨你。")
            (line "夜莺" "我觉得他恨的是那层楼。我只是唯一一个走出去的。")
            (line "夜莺" "他不是什么厉害角色，先生。他喝多了才敢做这种事。")
            (line "尼尔" "那也是他做的。")
            (line "夜莺" "是。那也是他做的。"))
          (set-flag! '她说起莱恩)
          (rest-release! "三封信/她说起莱恩")
          (gain-trust! 1)
          (sync-globals!)
          (spotlight! "她没有说的那句"
            "她没有求你放过莱恩。"))))

    ;; ── 小节三：找到并教训莱恩 ───────────────────────
    ;; 城市侧只有两件事：等三天,然后挑一个晚上出发。
    ;; 没有任何小节三专属的跑腿卡——钥匙就是你在小节二认识了谁,
    ;; 这三天里在码头做工、在老街露脸,都在悄悄开锁。
    ;;
    ;; 四把普通钥匙与一项弗兰克态势在进场前一次性镜像给交锋。
    ;; 有钥匙那一手不掷骰、不惊动人;没钥匙照样过得去,只是脏一点。
    (define (lesson-beat?) (and (= story-stage 3) (not lesson-done?)))
    (define (days-waited) (- world-day inquiry-day))
    (define (inquiry-done?) (>= (days-waited) inquiry-days))
    (define (alley-open?) (and (lesson-beat?) (inquiry-done?)))

    (define (sync-keys!)
      (set-global! '态势-弗兰克 (frank 'lyon-entry-state))
      (set-global! '钥匙-乔 (joe 'known?))
      (set-global! '钥匙-埃迪 (merchant-trusted?))
      (set-global! '钥匙-洛蒂 (>= familiar 2))
      (set-global! '钥匙-酒馆老板 (>= familiar 2)))

    ;; ── 巷子 ────────────────────────────────────────
    (define (node-alley-entry)
      (encounter-action "今晚去货栈后面"
        (lambda ()
          (sync-keys!)
          (frank 'prepare-lyon-entry!)
          (play-dialogue!
            (line "世界" "天黑透了才动身。货栈那一片没有路灯，你顺着煤渣路一直走到堆场的尽头。")
            (line "世界" "从这里往里，每一步都得有人放你过去——或者你自己想办法。"))
          (start-encounter "巷子" on-lesson-result))))

    ;; 交锋回传 (list 状态 越界? 额外? 熟脸增 劳工增)：
    ;;   状态 0 嘴硬 / 1 动摇 / 2 服软 / 3 崩——他停在轴上的哪一格
    ;;   越界? 巷口的人满格,你是被架出去的
    ;;   额外? 搜身翻出了他没打算给你的东西
    ;;   熟脸增 / 劳工增 第一幕里用谁的路走出来的关系,由这里写回城市
    (define (on-lesson-result result)
      (if (and (list? result) (= (length result) 5))
          #t
          (error "三封信：巷子交锋应回传 (list 状态 越界? 额外? 熟脸增 劳工增)"))
      (set! lesson-done? #t)
      (set! lesson-day world-day)
      (set! lesson-state (car result))
      (set! lesson-forced? (cadr result))
      (set! lesson-extra? (caddr result))
      (add-familiar! (list-ref result 3))
      (change-faction-relation! "劳工" (list-ref result 4))
      ;; 现有巷子交锋没有把莱恩交给警方的路线；他仍被留在老街，因此此处明确写回“遵守”。
      (frank 'on-lyon-result! #f)
      ;; 「他撒手」推到第 4 格，照片和信才到手；2–3 格他只答应不再写。
      ;; 被架出巷子的人什么也带不走。东西直接进物品栏——它就是揣在兜里的东西。
      (cond
        (lesson-forced? #f)
        ((>= lesson-state 6) (add-item! "莱恩的底片与照片" 1))
        ((>= lesson-state 4) (add-item! "莱恩的照片与信" 1))
        (else #f))
      (if lesson-extra? (add-item! "他没打算给人看的那沓纸" 1) #f)
      (mark-report-pending! "巷子")
      ;; 越界的代价落在经理那边：事情闹得剧院难看,他往回收耐心。
      ;; 老街那边的后果只写进文案——熟脸在小节二结算时就已经用完了,
      ;; 这里再动它没有任何下游会读到,那就不是代价,只是一个看不见的数字。
      (if lesson-forced?
          (patience-clk 'advance! -1)
          #f)
      (complete-section!)
      (sync-globals!)
      (sync-blockers!)
      (spotlight! "巷子之后"
        (string-append
          (cond
            (lesson-forced?
             "你被架出巷子。老街不会再给你开门。")
            ((>= lesson-state 6)
             "他把最后的东西从炉子后掏了出来。")
            ((>= lesson-state 4)
             "他把纸包从大衣里掏出，扔在你脚边。")
            ((>= lesson-state 2)
             "他缩在墙根，答应了，但还攥着东西。")
            (#t
             "他什么也没给，只是看着巷口。"))
          (cond
            ((>= lesson-state 6) "底片和照片都在你口袋里。")
            ((>= lesson-state 4) "照片和他写过的信在你口袋里，底片还在他那儿。")
            (#t "东西还在他那儿。"))
          (if lesson-extra? "还有一沓他没打算给任何人看的纸。" "")
          "这种人，吓一次也就够了——你是这么想的。"
          "新行动：去剧院报告巷子结果。")))

    ;; ── 经理的耐心 ──────────────────────────────────
    (define (patience-clock)
      (if (patience-running?) (list (patience-clk 'render-data)) '()))

    (define (patience-note)
      (cond
        ((patience-clk 'empty?) "")
        ((<= (patience-clk 'current) 2) "他已经在跟别人打听侦探了。")
        ((<= (patience-clk 'current) 4) "他开始问你要不要「多一个人手」。")
        (else "他现在还把你当成他找对了的那个人。")))

    ;; 全部报酬在小节三办完后一次结清：查明身份那一段（按结算档位）＋ 巷子那一段。
    ;; 平静期没来得及领的，第三封信到场时一起补——它是设计里让玩家松手的那笔钱，
    ;; 不能因为错过一天就消失。
    (define (inquiry-fee)
      (if (equal? settle-quality "好") manager-fee-good manager-fee-fair))

    (define (settle-manager-pay! note)
      (let ((total (+ (inquiry-fee) interim-fee)))
        (add-item! "金钱" total)
        (set-flag! '前段报酬)
        (result-note! (string-append note "：" (number->string total) " 金"))))

    (define (resolve-pending-report!)
      (if (report-pending?)
          #t
          (error "三封信：没有可向经理汇报的结果"))
      (patience-clk 'advance! patience-report)
      (cond
        ;; 查到名字不结账。钱一旦在这里落袋，小节三就变成"钱已经到手、
        ;; 还得再跑一趟"——玩家会觉得没事做了。押到巷子那一段办完一起结，
        ;; 中间这几天你手上仍然是紧的，去教训莱恩才有非去不可的理由。
        ((equal? report-pending "查明")
         (play-remote-dialogue!
           (line "尼尔" "雇跑腿的人叫莱恩。老街桥廊出来的，夜莺以前认识他。")
           (line "经理" "名字和住处不是一回事。我让人去找他的门牌。")
           (line "尼尔" "那这一段的钱呢。")
           (line "经理" "等他不再写信了，一起算。三天内，他们会给你一个地址。"))
         (result-note! "经理要等这件事了结才结账")
         (set! inquiry-day world-day)
         (advance-stage! 3))
        ((equal? report-pending "巷子")
         (play-remote-dialogue!
           (line "尼尔" "莱恩不会再写信了。照片和信也处理了。")
           (line "经理" "那就到这里。海报已经贴出去，接下来是我的事。")
           (line "经理" "查名字那一段和这一段，一起结给你。你可以歇几天。"))
         (settle-manager-pay! "经理把两段的报酬一起结了"))
        (else (error "三封信：未登记的汇报结果")))
      (set! report-pending "")
      (sync-globals!)
      (sync-blockers!))

    (define (node-report)
      (if (not (report-pending?))
          (error "三封信：没有可向经理汇报的结果")
          (node (pending-report-action-name)
            :subtitle (pending-report-subtitle)
            :resolve (instant
              (outcome "交代清楚"
                (lambda () (resolve-pending-report!)))))))

    ;; 耐心跑到你把巷子那一段交代给他为止——他说"那就到这里，接下来是我的事"，
    ;; 那句话就是这根钟停下的地方。往后事情看起来已经结束，你手上再没有东西
    ;; 可以拿去补它；这之后的时间压力换成首演的倒计时，不是他的脸色。
    ;; （办完了却不去告诉他，耐心照掉——那是你自己的事。）
    (define (patience-running?)
      (and (has-flag? '第二封信)
           (not (has-flag? '结案))
           (or (not lesson-done?) (equal? report-pending "巷子"))))

    (define-turn-rule "经理的耐心"
      (lambda ()
        (and (patience-running?)
             (>= (- world-day patience-day) patience-interval)))
      (lambda ()
        (set! patience-day world-day)
        (patience-clk 'advance! -1)
        (sync-globals!)
        (if (patience-clk 'empty?)
            (begin
              (play-dialogue!
                (line "经理" "我找了别人。")
                (line "尼尔" "我还在查。")
                (line "经理" "你一直在查。这就是问题。")
                (line "经理" "我付钱不是为了让人查，是为了让事情结束。"))
              (fail-game! "你被换掉了"
                "剧院经理另请了一位侦探。委托到此为止——你再没有理由走进那扇门，也再没有人会告诉你后来发生了什么。"))
            (begin
              ;; 从 2 格掉到 1 格以后立刻锁住下一次休息；玩家手里若有结果，
              ;; 必须先去剧院汇报，不能让下一次日终抢在可用的补救行动前触发失败。
              (sync-blockers!)
              (notify! (string-append "经理的耐心还剩 "
                                      (number->string (patience-clk 'current))
                                      " 格。" (patience-note)))))))

    (define (node-manager-desk)
      (observe-action "经理的办公室"
        (string-append
          "他在核一张座位表，笔尖点着前排的几个位置。"
          "'赞助的人要来，'他说，'那几位的名字我背得出来。'"
          "墙上钉着首演的海报，她的名字排在第三行。")))

    ;; 她第一次不在酒馆。人物节点仍留在老街(见 §1.5)，这只是一次性的事件。
    (define (node-rehearsal)
      (instant-action "去看她排练"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "你来了。他们让我从台底下升上来——像变戏法一样。")
            (line "尼尔" "习惯吗。")
            (line "夜莺" "底下黑得很，什么也看不见，只能听着乐队数拍子。")
            (line "夜莺" "数到第四拍我就得笑着上来。")
            (line "夜莺" "在酒馆唱了六年，从来没有人要求我笑着出场。")
            (line "世界" "她说完自己笑了一下，转身回到那个圆台中间去。"))
          (set-flag! '看彩排)
          (gain-trust! 1)
          (sync-globals!))))

    ;; 平静期第三天，剧院派人来找你。没有这一条，那封信只会静静躺在剧院里：
    ;; 节点每帧实时判断所以确实在，但玩家不会收到任何通知，休息阻塞也不会注册。
    ;; 第二封信有对称的「紧急来电」，这一封当初漏了。
    (define (begin-third-letter-call!)
      (set-flag! '第三封信来人)
      (play-remote-dialogue!
        (line "世界" "早饭还没吃完，剧院的跑腿孩子就站在门口，帽子攥在手里。")
        (line "剧院的跑腿" "先生，经理让我来找你。她化妆间里有东西。")
        (line "尼尔" "什么东西。")
        (line "剧院的跑腿" "一封信。经理说，这封得你自己来看。"))
      (sync-blockers!))

    ;; ── 小节四触发：第三封信(必看) ──────────────────
    ;; 不要钱,不提过去。只写一件事。信里提到一个只有内部人员才知道的
    ;; 排练细节——玩家和经理都认为是莱恩,勒索失败后升级到报复,合理推断。
    (define (node-third-letter)
      (instant-action "去剧院看那封信"
        (lambda ()
          ;; 巷子结果尚未单独汇报时，先把这件事当面交代并结账，再谈新信。
          ;; 玩家会完整看见两段 dialogue；不能再静默清空待汇报状态。
          (if (report-pending?)
              (if (equal? report-pending "巷子")
                  (resolve-pending-report!)
                  (error "第三封信到场时仍压着错误的待汇报结果"))
              #f)
          (if (has-flag? '前段报酬)
              #t
              (error "第三封信到场时前段报酬尚未结清，且没有可补交的巷子汇报"))
          (play-remote-dialogue!
            (line "经理" "在她化妆间的镜子底下。没有信封，没有邮戳。有人把它放进去的。")
            (line "尼尔" "写了什么？")
            (line "经理" "不要钱。一个字都没提钱。")
            (line "经理" "只说她要是当晚登台，她会死在台上。")
            (line "尼尔" "……这里写着她的登台时间。连换装的顺序都写了。")
            (line "经理" "只有后台的人知道那个顺序。")
            (line "尼尔" "他勒索没成，就换了个法子。")
            (line "经理" "演出照常。票已经卖出去了，报纸也约好了。"))
          (set-flag! '第三封信)
          (advance-stage! 4)
          (rest-release! "三封信/第三封信")
          (sync-blockers!)
          (spotlight! "第三封信"
            (string-append
              "勒索失败后，威胁指向首演。经理拒绝取消。"
              "距首演还有 " (number->string (days-to-premiere)) " 天。")))))

    ;; ── 小节三·人物戏(必看) ─────────────────────────
    (define (node-she-refuses)
      (instant-action "她要当面跟你讲"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "经理说你想让我别上台。")
            (line "尼尔" "有人写信说要你的命。")
            (line "夜莺" "我等了这么多年。")
            (line "夜莺" "我在那条街上唱了六年，先生。六年里没有一个人写信说要我的命——因为没有一个人在乎我死不死。")
            (line "夜莺" "现在有人在乎了。这说明我走到了什么地方。")
            (line "尼尔" "这说明有人想让你下不来台。")
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
          (play-remote-dialogue!
            (line "薇拉" "你就是那位侦探。经理跟我提过。")
            (line "薇拉" "夜莺唱得很好。我很喜欢。")
            (line "尼尔" "您听过她唱？")
            (line "薇拉" "我的助理告诉过我她唱得很好。")
            (line "薇拉" "这样的孩子应该被更多人听见。有时候需要一点运气——运气也是可以安排的。"))
          (set-flag! '薇拉)
          (sync-globals!))))

    ;; 林线只借用已有的中央台准备钟，不创建另一套剧院流程。
    ;; 陌生相见只做人物回收；已认识林时，保守 / 精密验收分别推进 1 / 2 格。
    (define (apply-lin-technical-help! amount)
      (if (beat3-open?) #t (error "三封信：林的技术帮助只能接入首演准备阶段"))
      (if (member? amount (list 1 2)) #t (error "三封信：林的技术帮助格数非法"))
      (fill-ring! core-ring-clk core-note amount)
      (set-flag! '薇拉)
      (sync-globals!))

    (define (mark-vera-met!)
      (set-flag! '薇拉)
      (sync-globals!))

    ;; ── 小节四·三个环的布置 ─────────────────────────
    ;; 每一格是具体的一件事，填格时直接说出来——不是「准备度 +1」。
    ;; 三个环合计 10 格，按判定期望约八九颗骰；这几天还要交房租、治伤。
    ;; 做得完，但要牺牲别的。
    (define (outer-note n)
      (cond
        ((= n 1) "东边那三扇门的插销全是坏的，你找人换了")
        ((= n 2) "后廊堆的旧布景挪开了——那后面本来能站一个人")
        ((= n 3) "值夜的老头认得你了；七号门今晚只虚掩，从里面推得开")
        (#t "五六扇门你全走过一遍，哪一扇从外面推得动，你心里有数")))

    (define (inner-note n)
      (cond
        ((= n 1) "环廊最窄的那一段，折椅收走了")
        ((= n 2) "两处活栏杆钉死了，翻不过来")
        ((= n 3) "引座的人知道灯一灭该往哪个门引")
        (#t "整圈走道你走了两遍，闭着眼也知道哪儿是死角")))

    (define (core-note n)
      (if (= n 1)
          "升降台的行程让机工改短了半尺——卡住了也爬得出来"
          "台底那个封了一年的通风口重新开了，烟不会正对着她站的地方"))

    (define (roster-note n)
      (cond
        ((= n 1) "临时工名册上有个上个月才换的机工，管的正是升降台")
        ((= n 2) "三号门的钥匙这个月配过一次；配给谁，单子上没写")
        (#t "台底通风的阀门上周有人报修——报修单没有署名")))

    ;; 一圈走完就是首演准备的一项完成；它影响交锋，但不另算作一个故事小节，
    ;; 因而不会再生成向经理汇报的动作。
    (define (fill-ring! clk note-fn n)
      (let ((before (clk 'current)))
        (clk 'advance! n)
        (if (> (clk 'current) before)
            (result-note! (note-fn (clk 'current)))
            #f)
        (sync-globals!)))


    (define (node-outer-ring)
      (node "把外圈的门走一遍"
        :subtitle "五六扇门、插销、值夜的人、后廊堆的杂物;走满这一圈，那道后廊门今晚不会被撬开"
        :tags (list "低风险")
        :clocks (list (outer-ring-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (outcome "图纸和现场对不上"
            (lambda () (spend-composure! 1)))
          (outcome "记住了一扇"
            (lambda () (fill-ring! outer-ring-clk outer-note 1)))
          (outcome "连后廊一起摸清了"
            (lambda () (fill-ring! outer-ring-clk outer-note 2))))))

    (define (node-inner-ring)
      (node "清理内环"
        :subtitle "收折椅、钉活栏杆、跟引座的人交代路线;都是力气活，走满这一圈前排今晚翻不起来"
        :tags (list "低风险")
        :clocks (list (inner-ring-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'violence
          (outcome "扛布景闪了腰"
            (lambda () (injure!)))
          (outcome "清出一段"
            (lambda () (fill-ring! inner-ring-clk inner-note 1)))
          (outcome "半圈都清干净了"
            (lambda () (fill-ring! inner-ring-clk inner-note 2))))))

    ;; 中央台是整座剧场最没法布置的地方：圆台四面无遮，没有门可封、没有走道可清。
    ;; 上限低不是因为谁不肯配合，是这块地方本来就只有机械和通风可动。
    ;; 剩下的只能靠那天晚上你人在场——这也是为什么中央环没有人手。
    (define (node-core-ring)
      (node "摸升降台与台底"
        :subtitle "圆台四面无遮，能动的只有升降台的行程和台底那个封了一年的通风口"
        :tags (list "低风险")
        :clocks (list (core-ring-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome "机工说这台子就这样"
            (lambda () (spend-composure! 1)))
          (outcome "改动了一处"
            (lambda () (fill-ring! core-ring-clk core-note 1)))
          (outcome "两处都动过了"
            (lambda () (fill-ring! core-ring-clk core-note 2))))))

    ;; 名册不产出真相，产出的是首演那张表上的几条：你提前知道台底会进烟、
    ;; 知道三号门是活的，于是知道该往哪个环铺格子。
    (define (node-roster)
      (node "翻后台名册"
        :subtitle "写信的人知道换装的顺序，说明他就在这栋楼里;每查清一件事，那天晚上你就少瞎一只眼"
        :tags (list "低风险")
        :clocks (list (roster-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "名册被人拿走了一页"
            (lambda () (spend-composure! 1)))
          (outcome "对出一条"
            (lambda () (fill-ring! roster-clk roster-note 1)))
          (outcome "对出一条，还问清了经手人"
            (lambda () (fill-ring! roster-clk roster-note 1))))))

    ;; ── 小节四·人手 ─────────────────────────────────
    ;; 人手不占准备格——格子是布置，人是活的。花一颗骰去谈，位置由他自己决定，
    ;; 那天晚上他每回合按自己的方式做一件事，也各有各的不做。
    (define (node-aide-joe)
      (node "请乔守外圈"
        :subtitle "他认得每一辆能横过来的推车;他只管门，人从他手里过去了他也不追"
        :requires (list (req-die))
        :resolve (instant
          (outcome "他答应了"
            (lambda ()
              (if (joe 'premiere-aide-eligible?)
                  #t
                  (error "三封信：乔当前不能承担首演外圈人手"))
              (play-dialogue!
                (line "尼尔" "首演那晚，剧院外圈的门。有人想进去。")
                (line "乔" "门我懂。推车横过去，一辆顶一扇。")
                (line "乔" "不过说好了——我不进场。里头的事我一样也不管。"))
              (set! aide-joe? #t)
              (sync-globals!))))))

    (define (node-aide-frank)
      (node "请弗兰克守后廊"
        :subtitle "他的人认得这一片的每条巷子;他们不等你下令，局面难看了自己就动手"
        :requires (list (req-die))
        :resolve (instant
          (outcome "他派了四个人"
            (lambda ()
              (play-dialogue!
                (line "尼尔" "那天晚上后廊那头，我需要有人站着。")
                (line "弗兰克" "四个人。工余的，不算工会的名义。")
                (line "弗兰克" "先说清楚：我的人不会站着看。真出了事，他们自己就上手了。"))
              (frank 'request-premiere-aid!)
              (set! aide-frank? #t)
              (sync-globals!))))))

    (define (meet-arthur-for-protection!)
      (if (not (arthur 'known?))
          (begin
            (arthur 'meet!)
            (play-dialogue!
              (line "世界" "阿瑟·贝尔坐在登记台后，桌上每一叠文件都用尺子压得笔直。")
              (line "尼尔" "有人威胁首演。我需要那天晚上有人在场。")
              (line "阿瑟" "把信留下。能派多少人，不只看信写得多吓人，也看我替你签字要担多少责任。")))
          #f))

    (define (node-aide-police)
      (node "把威胁信交给阿瑟"
        :subtitle (if (arthur 'known?)
                      "他能派人清内环;按程序办事——人群和门归他们，她不归"
                      "首演在即;请阿瑟按程序在场内安排人手")
        :tags (list "低风险")
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "报告压在桌上"
            (lambda () (meet-arthur-for-protection!)))
          (outcome "答应派两个人"
            (lambda ()
              (meet-arthur-for-protection!)
              (set! aide-police? #t)
              (sync-globals!)))
          (outcome "上头点了头"
            (lambda ()
              (meet-arthur-for-protection!)
              (set! aide-police? #t)
              (change-faction-relation! "官僚" 1)
              (sync-globals!))))))

    (define (node-aide-usher)
      (node "跟领班交代疏散"
        :subtitle "他认得每一排座位;可他也是花钱雇来的，被人推一次就不干了"
        :requires (list (req-die))
        :resolve (instant
          (outcome "他记下了"
            (lambda ()
              (play-dialogue!
                (line "领班" "灯要是灭了，我就把人往七号门引，那边最空。")
                (line "尼尔" "别的门呢。")
                (line "领班" "先生，我一个人只有一双手。"))
              (set! aide-usher? #t)
              (sync-globals!))))))

    ;; ── 首演之夜 ────────────────────────────────────
    (define premiere-pending? #f)

    (define (begin-premiere!)
      (if premiere-pending?
          (error "三封信：首演之夜已经在等待处理")
          #t)
      ;; 坍塌必须按排期走完；两日响应窗口与四日照料都不能在章末压缩补算。
      (dock-collapse 'validate-chapter-end!)
      (lin 'validate-chapter-end!)
      (frank 'validate-chapter-end!)
      (validate-joe-aide!)
      (validate-frank-aide!)
      (set! premiere-pending? #t)
      (sync-globals!)
      (sync-blockers!)
      (notify! "今天是首演。天黑以前你得到剧院去。"))

    (define (node-premiere-entry)
      (encounter-action "去剧院"
        (lambda ()
          (play-remote-dialogue!
            (line "夜莺" "别站在台下。站在我能看见你的地方。")
            (line "尼尔" "我就在侧台。")
            (line "经理" "两分钟。各就各位。"))
          (spotlight! "开演"
            "第三段开始时，全场的灯一起灭了。")
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
          (play-remote-dialogue!
            (line "阿瑟" "莱恩已经在我们手里了。")
            (line "尼尔" "这么快。")
            (line "阿瑟" "上头催得紧。会有记者来问，你知道他们会写什么。")
            (line "阿瑟" "港口失控，警方依法处置，夜莺没有受伤，演出是成功的。")
            (line "尼尔" "追到后台的那个人，比他冷静得多。他知道哪道门通哪儿。")
            (line "阿瑟" "他雇的人。这种人手上从来不干净。")
            (line "阿瑟" "别把事情想复杂了。案子结了，姑娘没事，你拿到了钱。"))
          (set-flag! '结案)
          (rest-release! "三封信/结案")
          (sync-globals!)
          (spotlight! "第二天的头版"
            (string-append
              "头版写着：夜莺不惧威胁，华丽谢幕。"
              "警方封锁了老街。"
              (if (get-global '首演-她受伤)
                  "照片里看不出她手上的绷带。"
                  "照片里的她站在谢幕灯下。")
              "委托结束。"))
          (play-dialogue!
            (line "夜莺" "你来了。")
            (line "尼尔" "你唱完了。")
            (line "夜莺" "我说过我会唱完的。")
            (line "夜莺" "那张票我一直留着。你没用上——你站在后台。")
            (line "尼尔" "下次吧。")
            (line "夜莺" "下次。")))))

    ;; ── 状态卡 ──────────────────────────────────────
    (define (days-tail)
      (string-append " · 首演还有 " (number->string (days-to-premiere)) " 天"))

    (define (client-subtitle)
      (cond
        ((= story-stage 1) (string-append "酒馆驻唱" (days-tail)))
        ((= story-stage 2) (string-append "她的过去被人攥在手里" (days-tail)))
        ((= story-stage 3)
         (string-append (if lesson-done? "莱恩挨了教训" "该去会一会莱恩") (days-tail)))
        ((= story-stage 4) (string-append "有人要她死在台上" (days-tail)))
        ((= story-stage 5) "首演之后")
        (else "")))

    (define (current-objective)
      (cond
        ((= story-stage 1)
         (if delivery-pending?
             "当前目标：去码头盯住邮箱，完成交割"
             "当前目标：交割日前凑足一百金；去码头踩点可以提前认人、认路"))
        ((= story-stage 2)
         (cond
           ((not (has-flag? '伤后探望))
            "当前目标：回家见夜莺，听她讲码头居民区")
           ((second-letter-pending?)
            "当前目标：去剧院见经理，查看第二封信")
           (material-settled?
            (if (has-flag? '第二封信)
                "当前目标：去剧院报告莱恩身份"
                "当前目标：已查出莱恩；等剧院的下一步消息"))
           (else
            "当前目标：去码头居民区追查「老金牌」，找出写信人的身份")))
        ((= story-stage 3)
         (cond
           ((not (has-flag? '她说起莱恩))
            "当前目标：回家听夜莺讲她和莱恩的过去")
           ((not lesson-done?)
            (if (inquiry-done?)
                "当前目标：去码头，今晚到货栈后面找莱恩"
                (string-append
                  "当前目标：等经理的人查到莱恩住处，还要 "
                  (number->string (max 0 (- inquiry-days (days-waited))))
                  " 天；这几天可以赚钱、交租或经营人情")))
           ((equal? report-pending "巷子")
            "当前目标：去剧院报告巷子结果")
           ((third-letter-due?)
            "当前目标：去剧院查看第三封信")
           (else
            "当前目标：前半段委托已了结；在首演前处理自己的生活")))
        ((= story-stage 4)
         (cond
           ((not (has-flag? '她不取消))
            "当前目标：去剧院见夜莺，确认她是否登台")
           (premiere-pending?
            "当前目标：首演就在今晚，去剧院")
           (else
            "当前目标：在剧院布置三层防线，并去城里找能到场的人手")))
        ((= story-stage 5)
         (if (has-flag? '结案)
             "委托已完成"
             "当前目标：去剧院外见阿瑟，听他如何结案"))
        (else (error "三封信：主线任务卡收到未登记的故事阶段"))))

    (define (situation-text)
      (cond
        ((= story-stage 1)
         (string-append
           "她在老街的酒馆唱歌，刚被一个剧院经理看中。首演是她等了多年的那一步——如果走得到的话。"
           "写信的人挑的就是这个时候。三天后，邮箱里得有一百金。"))
        ((= story-stage 2)
         (string-append
           "取信的人往码头居民区去了。那一片是她长大的地方，也是她再没回去过的地方。"
           (if (runner-cigarettes?)
               "写信的人在老街的酒馆雇了他，还落下半包烟——那个牌子在老街买不起。"
               "跑腿的人跟丢了，你手上只有一个方向。")))
        ((= story-stage 3)
         (if lesson-done?
             (string-append
               "莱恩挨了教训。这种人，吓一次也就够了——你是这么想的。"
               "她仍在老街酒馆唱自己的班，只是经理来得越来越勤，首演的海报也已经贴到了门外。")
             (string-append
               "名字有了：莱恩。和她一起在那层楼上长大，她走了，他留下了。"
               "他不敢回城寨，住在码头货栈后面那片租屋里。剩下的事该由你去跟他说清楚。"
               (if (inquiry-done?)
                   "经理的人查到了具体哪一间。今晚随时可以过去——认识谁，决定你怎么进得去那片地方。"
                   (string-append "经理雇的人还在查他住哪一间，还要 "
                                  (number->string (max 0 (- inquiry-days (days-waited))))
                                  " 天。这几天是你自己的。")))))
        ((= story-stage 4)
         (string-append
           "第三封信不要钱，只要她的命，而且知道只有后台的人才知道的事。"
           "经理不肯取消首演，那就只剩一件事：把她要走的那条路清干净。"
           "剧院是个圆的——三层圈子，最外面是门，中间是走道和看台，她在正当中。"
           (if (> (aide-count) 0)
               "那天晚上有人替你站在某个圈子上，但没有人能站在她旁边。"
               "那天晚上三个圈子会一起出事，而你只有一个人。")))
        ((= story-stage 5)
         "报纸把这件事写完了。案子结了，她站上了她等了多年的那个位置。")
        (else "")))

    ;; 世界根节点不再单独展示主线钟；它们统一收在「三封信」任务卡上。
    (define (world-clocks)
      '())

    (define (delivery-deadline-clock)
      (list 'clock "交割日" (days-to-delivery) letter-deadline 'countdown
            (if delivery-pending?
                "就是今天。还能先在城里做事；去码头交割后才能结束这一天。"
                (string-append
                  "信上写的日子。到期当天必须去码头盯住邮箱。"
                  "夜莺最多替你补 " (number->string nightingale-cover-max)
                  " 金，再多她也拿不出来。"))))

    ;; 巷子之后压力换人：经理不再催（他以为事情结束了），催你的是钉死的首演日。
    (define (premiere-clock)
      (list (list 'clock "首演之夜"
                  (min premiere-window (days-to-premiere)) premiere-window 'countdown
                  "钉死的日子。归零那天不管你准备到哪一步，幕都会拉开。")))

    (define (premiere-preparation-clock)
      (list 'clock "首演布置"
            (+ (outer-ring-clk 'current) (inner-ring-clk 'current)
               (core-ring-clk 'current) (roster-clk 'current))
            (+ ring-max ring-max core-max roster-max) 'segments
            "外圈、内环、中央台与后台名册的总进度；具体布置仍在剧院各动作上显示。"))

    (define (premiere-aide-clock)
      (list 'clock "到场人手" (aide-count) 4 'segments
            "乔、弗兰克的人、阿瑟的警察与剧院领班各自能守一处；中央台只能靠你。"))

    (define (card-clocks)
      (cond
        ((= story-stage 1)
         (append (list (delivery-deadline-clock))
                 (delivery-fund-clock)
                 (list (mail-clock 'render-data))))
        ((and (= story-stage 2) (patience-running?)) (patience-clock))
        ((lesson-beat?)
         (append (if (inquiry-done?)
                     '()
                     (list (list 'clock "经理的人在找他"
                                 (max 0 (- inquiry-days (days-waited)))
                                 inquiry-days 'countdown
                                 "他雇的人去查莱恩住哪儿。归零那天消息回来——这几天是你自己的。")))
                 (patience-clock)))
        ((quiet-period?) (premiere-clock))
        ((beat3-open?)
         (append (premiere-clock)
                 (list (premiere-preparation-clock) (premiere-aide-clock))))
        (else '())))

    ;; 案子卡：卷宗,不是人。时钟与案情走到哪儿都看得见。
    ;; 夜莺本人由 nodes-at 按她当天在哪儿发放——见 nightingale-node。
    (define (render-data)
      (if (>= story-stage 1)
          (list (node "三封信"
                  :subtitle (current-objective)
                  :tags (list "主线")
                  :children (append
                              (list (observe-action "案情摘要" (situation-text)))
                              ;; 手上确实攥着的东西，和「案情」分开一条：它是物证，不是叙述。
                              (if (runner-cigarettes?)
                                  (list (observe-action "半包「老金牌」"
                                          (string-append
                                            "从取信人身上掉出来的。烟盒软了，还剩七八根，纸口被反复捏过。"
                                            "这个牌子老街的铺子不进货——买得起的人不住这儿，"
                                            "住这儿还抽它的人，是从别处落下来的。")))
                                  '()))
                  :clocks (card-clocks)))
          '()))

    ;; ── 夜莺本人 ────────────────────────────────────
    ;; 人物卡整个第一章常驻酒馆。
    (define (presence-text)
      (cond
        ((= story-stage 1)
         "她在台边就着灯拧手里的帕子。看见你进来，她把下巴抬了一下算是打招呼——这几天她不敢跟你在人前多说话。")
        ((= story-stage 2)
         (if (has-flag? '第二封信)
             "她今晚唱得比平时轻，像是怕把嗓子用完。中间有一段她盯着门口，忘了词，乐手替她圆了过去。"
             "她唱完一支就下台了。经过你桌边时她说：那些钱本来就是破财消灾，我担心的是他们还会来。"))
        ((>= story-stage 3)
         "首演的海报已经贴到门外。她仍站在酒馆原来的位置上唱，台下的人却第一次知道她不会永远留在这里。")
        (else "")))

    (define (song-text)
      (cond
        ((= story-stage 1)
         "她没有问你想听什么，转身跟钢琴手点了点头。那支慢歌唱到一半，门被风推开一条缝；她看了一眼，声音没有停。")
        ((= story-stage 2)
         "她把声音放得很低，像是只唱给吧台后面那盏灯听。最后一句落下时，酒馆里有几秒钟没人碰杯子。")
        ((= story-stage 3)
         "她挑了一支很久没人点过的慢歌。唱到最高的地方时，酒馆里原本说话的人都安静了下来。")
        ((= story-stage 4)
         "首演的海报已经贴到门外。她没唱剧院安排的曲子，而是挑了一支在这里唱过很多年的旧歌；今晚没人叫她换。")
        (else (error "三封信点歌：夜莺当前不在酒馆驻唱"))))

    (define (node-request-song)
      (node "请夜莺唱一首歌"
        :subtitle (if (= song-day world-day)
                      "今晚已经请她唱过了"
                      "投入一枚行动骰；恢复 2 点冷静，每天一次")
        :disabled (= song-day world-day)
        :requires (list (req-die))
        :resolve (instant
          (outcome "听她唱完一首"
            (lambda ()
              (set! song-day world-day)
              (restore-actor-composure! 'player 2))
            'light))))

    (define (nightingale-anchor-name location)
      (cond
        ((equal? location "酒馆") "夜莺@酒馆")
        ((equal? location "剧院") "夜莺@剧院")
        (else (error "夜莺：人物节点所在地点没有登记空间锚点"))))

    (define (nightingale-node location extra-children)
      (node "夜莺"
        :anchor (nightingale-anchor-name location)
        :subtitle (client-subtitle)
        :children (cons (observe-action "她今晚的样子" (presence-text)) extra-children)))

    ;; 强制事件一律放进实际发生的地点。世界根节点不再承载剧情动作。
    (define (world-nodes)
      '())

    ;; 各地点向故事要自己这一拍的节点。地点不认识故事状态,只认自己的名字。
    (define (nodes-at location)
      (cond
        ((equal? location "家")
         (append
           (if (= story-stage 0) (list (node-answer-door)) '())
           (if (and (= story-stage 2) (not (has-flag? '伤后探望)))
               (list (node-her-visit))
               '())
           (if (and (= story-stage 3) (not (has-flag? '她说起莱恩)))
               (list (node-her-and-lyon))
               '())))
        ((equal? location "码头")
         (append
           (if delivery-pending? (list (node-delivery-entry)) '())
           (beat1-dock-nodes)
           ;; 小节三出发口：货栈后面就在码头这一侧。三天到了才亮。
           (if (alley-open?) (list (node-alley-entry)) '())
           ;; 小节四：弗兰克的人从码头答复；乔伤后不再回码头，他的请求放在居民区。
           (if (and (beat3-open?) (not aide-frank?)
                    (frank 'premiere-aid-open?))
               (list (node-aide-frank))
               '())))
        ((equal? location "酒馆")
         (append
           (if (singer-present?)
               (list (nightingale-node "酒馆"
                       (append (list (node-request-song)) (beat1-tavern-nodes))))
               '())
           (if (beat2-open?) (list (node-lyon-talk)) '())))
        ;; 城寨节点全部平铺在居民区下；探索过程消失后，只留下有后续玩法的人物与地点。
        ((equal? location "居民区")
         (append
           (if (and (beat3-open?) (not aide-joe?) (joe 'premiere-aide-eligible?))
               (list (node-aide-joe))
               '())
           (if (and (not (east-merchant-found?))
                    (or (beat2-open?) (> (east-entry-clk 'current) 0)))
               (list (node-east-entry))
               '())
           (if (east-merchant-found?) (list (eddie-node)) '())
           (if (and (east-merchant-found?) (not (east-route-done?)))
               (list (node-east-gallery))
               '())
           (if (east-route-done?) (list (union-room-place)) '())
           (if (and (not (west-stairs-done?))
                    (or (beat2-open?) (> (west-stairs-clk 'current) 0)))
               (list (node-west-stairs))
               '())
           (if (west-friction-pending?) (list (node-west-friction)) '())
           (if (and west-friction-cleared? (not (west-route-done?)))
               (list (node-west-bridge))
               '())
           (if (west-route-done?)
               (list (bridge-apartments-place))
               '())))
        ((equal? location "警察局")
         (if (and (beat3-open?) (not aide-police?))
             (list (node-aide-police))
             '()))
        ((equal? location "剧院")
         (append
             (if (second-letter-pending?) (list (node-second-letter)) '())
             (if (third-letter-due?) (list (node-third-letter)) '())
             (if (and (beat3-open?) (not (has-flag? '她不取消))) (list (node-she-refuses)) '())
             (if premiere-pending? (list (node-premiere-entry)) '())
             (if (and (= story-stage 5) (not (has-flag? '结案))) (list (node-closing)) '())
             (if (and (quiet-period?) (not (has-flag? '看彩排)))
                 (list (node-rehearsal))
                 '())
             (cond
               ((second-letter-pending?) '())
               ((beat3-open?)
                (append
                  (if (outer-ring-clk 'full?) '() (list (node-outer-ring)))
                  (if (inner-ring-clk 'full?) '() (list (node-inner-ring)))
                  (if (core-ring-clk 'full?) '() (list (node-core-ring)))
                  (if (roster-clk 'full?) '() (list (node-roster)))
                  (if aide-usher? '() (list (node-aide-usher)))
                  (if (has-flag? '薇拉) '() (list (node-vera)))))
               ((report-pending?) (list (node-report) (node-manager-desk)))
               ((patience-running?) (list (node-manager-desk)))
               ((quiet-period?) (list (node-manager-desk)))
               (else (list (node-manager-desk))))))
        (else '())))

    ;; ── 日终 ────────────────────────────────────────
    ;; 世界日历规则先把 world-day 推到醒来后的新日期，本模块再据新日期触发当天事件。
    ;; 第二封信用电话自动表现；交割日与首演只挂起必看事件，由阻塞休息逼玩家亲自前往。
    (define-turn-rule "第二封信紧急来电"
      (lambda () (second-letter-call-due?))
      (lambda () (begin-second-letter-call!)))

    (define-turn-rule "第三封信送到"
      (lambda () (and (third-letter-due?) (not (has-flag? '第三封信来人))))
      (lambda () (begin-third-letter-call!)))

    (define-turn-rule "第一章定日事件"
      (lambda ()
        (or (and (= story-stage 1) (not delivery-pending?)
                 (>= world-day delivery-day))
            (and (= story-stage 4) (not premiere-pending?) (not premiere-done?)
                 (>= world-day premiere-day))))
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
          ((equal? msg 'days-to-premiere) (days-to-premiere))
          ((equal? msg 'singer-present?) (singer-present?))
          ((equal? msg 'old-street-open?) (old-street-open?))
          ((equal? msg 'theater-open?) (theater-open?))
          ((equal? msg 'police-open?) (police-open?))
          ((equal? msg 'freight-open?) (freight-open?))
          ((equal? msg 'delivery-result) delivery-result)
          ;; 小节二重构时从这里取抓手：半包「老金牌」是交割那夜唯一的具名物证。
          ((equal? msg 'runner-cigarettes?) (runner-cigarettes?))
          ((equal? msg 'trust-met?) (trust-met?))
          ((equal? msg 'sync-blockers!) (sync-blockers!))
          ((equal? msg 'sync-globals!) (sync-globals!))
          ((equal? msg 'validate-joe-aide!) (validate-joe-aide!))
          ((equal? msg 'validate-frank-aide!) (validate-frank-aide!))
          ((equal? msg 'apply-lin-technical-help!) (apply-lin-technical-help! (cadr args)))
          ((equal? msg 'mark-vera-met!) (mark-vera-met!))
          ((equal? msg 'save)
           (list
             (list "story-stage" story-stage)
             (list "delivery-day" delivery-day)
             (list "delivery-pending?" delivery-pending?)
             (list "delivery-result" delivery-result)
             (list "delivery-money" delivery-money)
             (list "delivery-shortfall" delivery-shortfall)
             (list "mail-routine" (mail-clock 'save))
             (list "joe-mail-tip?" joe-mail-tip?)
             (list "singer-mail-tip?" singer-mail-tip?)
             (list "condition-level" condition-level)
             (list "trust" trust)
             (list "song-day" song-day)
             (list "familiar" familiar)
             (list "west-friction-cleared?" west-friction-cleared?)
             (list "east-entry" (east-entry-clk 'save))
             (list "east-gallery" (east-gallery-clk 'save))
             (list "west-stairs" (west-stairs-clk 'save))
             (list "west-bridge" (west-bridge-clk 'save))
             (list "merchant-trust" (merchant-trust-clk 'save))
             (list "union-checked?" union-checked?)
             (list "bridge-identified?" bridge-identified?)
             (list "patience" (patience-clk 'save))
             (list "patience-day" patience-day)
             (list "report-pending" report-pending)
             (list "inquiry-day" inquiry-day)
             (list "inquiry-told?" inquiry-told?)
             (list "lesson-done?" lesson-done?)
             (list "lesson-day" lesson-day)
             (list "lesson-state" lesson-state)
             (list "lesson-forced?" lesson-forced?)
             (list "lesson-extra?" lesson-extra?)
             (list "envelope-thin?" envelope-thin?)
             (list "material-settled?" material-settled?)
             (list "settle-route" settle-route)
             (list "settle-quality" settle-quality)
             (list "lyon-fate" lyon-fate)
             (list "settled-day" settled-day)
             (list "outer-ring" (outer-ring-clk 'save))
             (list "inner-ring" (inner-ring-clk 'save))
             (list "core-ring" (core-ring-clk 'save))
             (list "roster" (roster-clk 'save))
             (list "aide-joe?" aide-joe?)
             (list "aide-frank?" aide-frank?)
             (list "aide-police?" aide-police?)
             (list "aide-usher?" aide-usher?)
             (list "premiere-done?" premiere-done?)
             (list "premiere-pending?" premiere-pending?)
             (list "scene-flags" scene-flags)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! story-stage (assoc-get data "story-stage" 0))
             (set! delivery-day (assoc-get data "delivery-day" 0))
             (set! delivery-pending? (assoc-get data "delivery-pending?" #f))
             (set! delivery-result (assoc-get data "delivery-result" "未定"))
             (set! delivery-money (assoc-get data "delivery-money" 0))
             ;; 旧存档把这两样东西记成布尔量。读到 #t 而物品栏里没有,就补发一件;
             ;; 新存档里它们只存在于物品栏,这两行读完就再也不写回去。
             (if (and (assoc-get data "runner-cigarettes?" #f) (not (runner-cigarettes?)))
                 (add-item! cigs-item 1)
                 #f)
             (if (and (assoc-get data "lesson-material?" #f)
                      (= (item-count "莱恩的照片与信") 0)
                      (= (item-count "莱恩的底片与照片") 0))
                 (add-item! "莱恩的照片与信" 1)
                 #f)
             (set! delivery-shortfall (assoc-get data "delivery-shortfall" -1))
             (if (or (< delivery-shortfall -1) (> delivery-shortfall delivery-price))
                 (error "三封信存档错误：交割款差额非法")
                 #t)
             (mail-clock 'load! (assoc-get data "mail-routine" 0))
             (set! joe-mail-tip? (assoc-get data "joe-mail-tip?" #f))
             (set! singer-mail-tip? (assoc-get data "singer-mail-tip?" #f))
             (set! condition-level (assoc-get data "condition-level" 0))
             (if (or (< condition-level 0) (> condition-level 2))
                 (error "三封信存档错误：夜莺处境等级非法")
                 #t)
             (set! trust (assoc-get data "trust" 0))
             (set! song-day (assoc-get data "song-day" 0))
             (set! familiar (assoc-get data "familiar" 0))
             (set! west-friction-cleared? (assoc-get data "west-friction-cleared?" #f))
             (east-entry-clk 'load! (assoc-get data "east-entry" 0))
             (east-gallery-clk 'load! (assoc-get data "east-gallery" 0))
             (west-stairs-clk 'load! (assoc-get data "west-stairs" 0))
             (west-bridge-clk 'load! (assoc-get data "west-bridge" 0))
             (merchant-trust-clk 'load! (assoc-get data "merchant-trust" 0))
             (set! union-checked? (assoc-get data "union-checked?" #f))
             (set! bridge-identified? (assoc-get data "bridge-identified?" #f))
             (patience-clk 'load! (assoc-get data "patience" patience-max))
             (set! patience-day (assoc-get data "patience-day" 0))
             (set! report-pending (assoc-get data "report-pending" ""))
             (if (or (equal? report-pending "")
                     (equal? report-pending "查明")
                     (equal? report-pending "巷子"))
                 #t
                 (error "三封信存档错误：待汇报小节非法"))
             (set! inquiry-day (assoc-get data "inquiry-day" 0))
             (set! inquiry-told? (assoc-get data "inquiry-told?" #f))
             (set! lesson-done? (assoc-get data "lesson-done?" #f))
             (set! lesson-day (assoc-get data "lesson-day" 0))
             (set! lesson-state (assoc-get data "lesson-state" 0))
             (if (or (< lesson-state 0) (> lesson-state 6))
                 (error "三封信存档错误：巷子结算状态非法")
                 #t)
             (set! lesson-forced? (assoc-get data "lesson-forced?" #f))
             (set! lesson-extra? (assoc-get data "lesson-extra?" #f))
             (set! envelope-thin? (assoc-get data "envelope-thin?" #f))
             (set! material-settled? (assoc-get data "material-settled?" #f))
             (set! settle-route (assoc-get data "settle-route" "无"))
             (set! settle-quality (assoc-get data "settle-quality" "无"))
             (set! lyon-fate (assoc-get data "lyon-fate" "无"))
             (set! settled-day (assoc-get data "settled-day" 0))
             (outer-ring-clk 'load! (assoc-get data "outer-ring" 0))
             (inner-ring-clk 'load! (assoc-get data "inner-ring" 0))
             (core-ring-clk 'load! (assoc-get data "core-ring" 0))
             (roster-clk 'load! (assoc-get data "roster" 0))
             (set! aide-joe? (assoc-get data "aide-joe?" #f))
             (set! aide-frank? (assoc-get data "aide-frank?" #f))
             (set! aide-police? (assoc-get data "aide-police?" #f))
             (set! aide-usher? (assoc-get data "aide-usher?" #f))
             (set! premiere-done? (assoc-get data "premiere-done?" #f))
             (set! premiere-pending? (assoc-get data "premiere-pending?" #f))
             (set! scene-flags (normalize-flags (assoc-get data "scene-flags" '())))
             (sync-globals!)
             (sync-blockers!)))
          (#t #f))))))

;; 新游戏自动执行的开场动作。客户端读这个全局去找节点，不写死章节内容。
(set-global! '开场动作 "有人敲门")
