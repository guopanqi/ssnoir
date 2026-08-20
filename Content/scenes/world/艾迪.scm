;; scenes/world/艾迪.scm - 第一章·艾迪与地下拳场
;;
;; 一条临时的人物线，附带一套只跑四个晚上的赌博玩法。四夜之后连人带地点一起收走。
;;
;; 骰子花在「理解比赛」上，不花在下注上：
;;   看盘口 → 花骰子查情报 → 花钱下注 → 比赛自己发生
;; 下注不掷骰，因为比赛不是玩家的判定。引擎也不允许别的写法：FateStrip 要求
;; 放入骰必须是 1..6，命运条天生只能表达「你准备得怎么样」，表达不了「外面那场
;; 比赛会怎样」。所以比赛结果由作者写死，玩家买的只是自己的判断质量。
;;
;; 调查的坏结果不是「没查到」，是「查到了一句听起来很确定的假话」。
;; 这是准备值在这套玩法里唯一有质感的地方。
;;
;; 四个晚上各教一件事：
;;   一 冷门赢——不查就只能跟着赔率买，跟着赔率买会输
;;   二 热门赢——别人的大注不是情报；你第一次看见写票的那双眼睛
;;   三 艾迪认真打——他赢了，拿八块
;;   四 艾迪打假赛——他给你内幕，你决定敢赚多少
;;
;; 赢钱没问题，赢得太聪明才有问题：庄家的注意是一根摆在拳场里的钟，
;; 它决定第四夜之后那只手伤成什么样。玩家从他手上赚过多少，账就记多少。

(define eddie
  (let ()
    ;; ── 常量 ────────────────────────────────────────
    (define fight-interval 2)     ; 每两天一场
    (define last-night 4)         ; 一共四夜，之后查封
    (define heat-max 6)
    (define heat-hurt 5)          ; 到这一格，第四夜之后那只手就不只是挨一顿
    (define reunion-delay 2)      ; 查封之后几天在码头再见到他

    ;; ── 状态 ────────────────────────────────────────
    ;; 0 还没听说 / 4 巷子里那件事今天可以去 / 5 这条线到此为止
    ;; 1 拳场开着 / 2 已查封 / 3 码头重逢已看过
    ;; 4 和 5 排在后面，是为了不动 1..3 —— 那三个数字后面所有代码都在读。
    (define stage 0)
    (define alley-day 0)          ; 巷子那件事摆在哪天；过了这天就没了
    (define alley-told? #f)       ; 这一轮的消息在酒馆里说过没有
    (define next-fight-day 0)     ; 下一场在哪天
    (define night 0)              ; 排到第几夜（1..4）
    (define shut-day 0)

    ;; 今晚的临时状态，每天清空
    (define bet-side 0)           ; 0 没押 / 1 押甲 / 2 押乙
    (define bet-amount 0)
    (define warmup-seen 0)        ; 0 没查 / 1 坏 / 2 中 / 3 好
    (define ringside-seen 0)
    (define talked? #f)
    (define watched? #f)
    (define awaiting-signal? #f)
    (define signal "")            ; 点头 / 摇头 / 别开眼
    (define result-line "")       ; 看完比赛之后留在场里的一行字

    (define tip? #f)              ; 第四夜的内幕已经到手
    (define hand-bad? #f)

    (define heat-clk
      (make-clock "庄家的注意" heat-max 'segments
        (lambda (current max)
          (cond
            ((>= current max)
             "写票的已经认定有人在漏消息。他们会去找那个知道的人。")
            ((>= current heat-hurt)
             "有人在核对第三场的票是谁买的。再赚得准一点，他们就要找人问了。")
            ((> current 0)
             "赢钱没问题。赢得太聪明才有问题。")
            (else
             "你下的注还没大到值得他们抬一次头。")))))

    ;; ── 四个晚上的排面 ──────────────────────────────
    ;; 赔率是十分之一的整数：24 就是 ×2.4。
    (define (fighter-a n)
      (cond ((= n 1) "「铁牙」莫里斯")
            ((= n 2) "卡尔·韦德")
            ((= n 3) "艾迪")
            (else    "艾迪")))

    (define (fighter-b n)
      (cond ((= n 1) "小马丁")
            ((= n 2) "「船坞」奥班农")
            ((= n 3) "「铁牙」莫里斯")
            (else    "「小刀」凯利")))

    (define (odds-a n)
      (cond ((= n 1) 15) ((= n 2) 14) ((= n 3) 24) (else 14)))

    (define (odds-b n)
      (cond ((= n 1) 25) ((= n 2) 28) ((= n 3) 16) (else 38)))

    ;; 写死的赢家。第四夜是唯一会被玩家改写的一晚。
    (define (winner n)
      (cond ((= n 1) 2)
            ((= n 2) 1)
            ((= n 3) 1)
            (else (if (equal? signal "摇头") 1 2))))

    (define (odds-of side n)
      (if (= side 1) (odds-a n) (odds-b n)))

    (define (odds-text tenths)
      (string-append "×"
        (number->string (quotient tenths 10))
        "."
        (number->string (- tenths (* 10 (quotient tenths 10))))))

    (define (payout amount tenths)
      (quotient (* amount tenths) 10))

    (define (side-name side n)
      (if (= side 1) (fighter-a n) (fighter-b n)))

    ;; ── 遭遇零：《不是说好第四回合吗》 ───────────────
    ;; 它不是一张常驻的卡，是一次遭遇：某天有人把这件事带到你面前，你去或者不去，
    ;; 当天过完就没了。去了是一场真交锋，赢了这条线才开——救不下来就是救不下来。

    (define (node-alley)
      (node "巷子里在打人"
        :anchor "老街酒馆"
        :tags (list "交锋")
        :subtitle "酒馆侧墙那条巷子。三个人围着一个。今晚过了就没了"
        :resolve (instant (lambda () (start-encounter "巷子里在打人" on-alley-result)))))

    (define (on-alley-result result)
      (if (equal? result 'success)
          (begin
            (set! stage 1)
            (set! next-fight-day (+ world-day 1))
            (set! night 0)
            (play-dialogue!
              (line "艾迪" "谢了。")
              (line "尼尔" "欠钱？")
              (line "艾迪" "差不多。")
              (line "艾迪" "昨晚说好第四回合躺下。第二回合那孙子专打我这儿。")
              (line "艾迪" "我就把他打晕了。")
              (line "尼尔" "……")
              (line "艾迪" "老街酒馆后头有场子。既然你救了我，至少该知道你救的是个什么东西。"))
            (spotlight! "地下拳场"
              "酒馆后面那间货栈，每两天一场。他说了名字，也说了从哪扇门进。"))
          (begin
            (set! stage 5)
            (spotlight! "这件事就到这儿"
              "第二天巷子里只剩几摊冲淡的血。酒馆里没有人提起昨晚有谁挨了打。"))))

    ;; 遭遇的触发：老街那边开起来以后的某个早上，这件事摆到了酒馆门口。
    ;; 日终规则在「世界日历推进」之后跑，world-day 已经是第二天，所以它摆出来的
    ;; 就是明天那一天，alley-day 记的正是这个。
    ;;
    ;; 规则本身不说话：它只推进状态、记下日子、让遭遇卡出现在酒馆。消息要在酒馆里
    ;; 听——事发生在老街侧墙，在家里被告知是错的位置。
    (define-turn-rule "巷子里在打人"
      (lambda () (and (= stage 0)
                      (>= (three-letters 'story-stage) 1)))
      (lambda ()
        (set! stage 4)
        (set! alley-day world-day)
        (set! alley-told? #f)))

    ;; 走进酒馆时才听到。标记在回调里立刻置位——玩家可以退出去再进来，
    ;; 等交锋打完再置就会重播一遍。
    (define (arrival-alley)
      (arrival "巷子里在打人"
        (lambda ()
          (set! alley-told? #t)
          (play-remote-dialogue!
            (line "世界" "老街那边有人跑进酒馆找酒保，说侧墙那条巷子里有人在挨打。")
            (line "酒保" "又不是头一回。别往那头去。")
            (line "世界" "没有人打算过去。这一片今晚也不会有巡警。")))))

    ;; 和 nodes-at 同一套写法：地点不认识故事状态，只报自己的名字。
    (define (arrivals-at location)
      (cond
        ((equal? location "酒馆")
         (if (and (= stage 4) (not alley-told?)) (list (arrival-alley)) '()))
        (else '())))

    ;; 没去就是没去。摆出来那天过完，这条线跟着一起收走。
    (define-turn-rule "巷子里那件事过去了"
      (lambda () (and (= stage 4) (> world-day alley-day)))
      (lambda ()
        (set! stage 5)
        (notify! "昨天老街侧墙那条巷子里的事，今天没有人再提。")))

    ;; ── 情报 ────────────────────────────────────────
    ;; 好＝真且有用；中＝真但不够；坏＝一句听起来很确定的假话。
    (define (warmup-text n grade)
      (cond
        ((= n 1)
         (cond ((= grade 3) "莫里斯这周第三场了。右眼还肿着，热身热到一半就靠着绳子喘。")
               ((= grade 2) "莫里斯动作有点沉。也可能他一向这么打。")
               (else        "莫里斯拳风带响。那小子在角落里连手都在抖。")))
        ((= n 2)
         (cond ((= grade 3) "奥班农压根没热身。他坐在凳子上系鞋带，系了五分钟。他今晚是来挨打的。")
               ((= grade 2) "韦德左手腕缠着布。看样子每场都缠。")
               (else        "韦德那只手腕不对劲，热身时一次都没敢发力。")))
        ((= n 3)
         (cond ((= grade 3) "他出拳跟你上两回看见的不一样。没有那种收着的东西。")
               ((= grade 2) "他鼻子上那道口子还没长好。")
               (else        "他热身热了一半就坐下了，眼睛不看擂台。")))
        (else
         (cond ((= grade 3) "艾迪状态好得不像话。凯利比他矮一头，肩膀还没长开。")
               ((= grade 2) "那个新人第一次上这种场子，手在抖。")
               (else        "艾迪的腿在打晃。")))))

    (define (ringside-text n grade)
      (cond
        ((= n 1)
         (cond ((= grade 3) "写票的收莫里斯收得太痛快了。有个老赌客把票撕了，改压小马丁。")
               ((= grade 2) "都在买莫里斯。")
               (else        "有人说小马丁上个月被一拳放倒过，躺了半分钟。")))
        ((= n 2)
         (cond ((= grade 3) "后门进来一个人，一口气吃下奥班农的票。写票的抬起头看了他一眼，然后记住了那张脸。")
               ((= grade 2) "赔率在往奥班农那边动。")
               (else        "都说今晚有安排。买冷门的。")))
        ((= n 3)
         (cond ((= grade 3) "老赌客说艾迪从来不赢。这就是他们从来不赚钱的原因。")
               ((= grade 2) "没人买艾迪。")
               (else        "有人说艾迪今晚收了钱。")))
        (else
         (cond ((= grade 3) "写票的今晚话很少。他从进门起就在看第三场的票怎么走。")
               ((= grade 2) "这场没什么可看的。艾迪。")
               (else        "有人说那个新人是练过的。")))))

    (define (node-warmup)
      (node "看热身"
        :subtitle "敏锐；台下那半个钟头，比谁说的都准——只要你看得懂"
        :disabled (> warmup-seen 0)
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome "你看见的是这个"
            (lambda ()
              (set! warmup-seen 1)
              (result-note! (warmup-text night 1))))
          (outcome "看出来一点"
            (lambda ()
              (set! warmup-seen 2)
              (result-note! (warmup-text night 2))))
          (outcome "看得很清楚"
            (lambda ()
              (set! warmup-seen 3)
              (result-note! (warmup-text night 3)))))))

    (define (node-ringside)
      (node "听场边"
        :subtitle "交际；写票的那张桌子周围，话最多也最不值钱"
        :disabled (> ringside-seen 0)
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "你听来的是这个"
            (lambda ()
              (set! ringside-seen 1)
              (result-note! (ringside-text night 1))))
          (outcome "听来半句"
            (lambda ()
              (set! ringside-seen 2)
              (result-note! (ringside-text night 2))))
          (outcome "有人肯跟你说"
            (lambda ()
              (set! ringside-seen 3)
              (result-note! (ringside-text night 3)))))))

    ;; 第三夜起他认得你了。不耗行动骰，一晚一次——这是救过他换来的东西。
    (define (node-talk)
      (node "找艾迪聊聊"
        :subtitle "不耗行动骰；他在后面缠手"
        :disabled talked?
        :resolve (instant
          (outcome "他跟你说了句实话"
            (lambda ()
              (set! talked? #t)
              (if (= night 3)
                  (play-dialogue!
                    (line "艾迪" "今天别买我输。")
                    (line "尼尔" "为什么？")
                    (line "艾迪" "今天没人付我。"))
                  (play-dialogue!
                    (line "艾迪" "别问了。你知道的比这儿所有人都多。")
                    (line "艾迪" "钱带够没有。"))))))))

    ;; 第四夜的内幕：他自己找上门。不耗骰，也不要求你跟他多熟——
    ;; 你救过他一次，这条线上没有别的关系可攒。
    (define (node-tip)
      (node "艾迪在门口等你"
        :subtitle "不耗行动骰"
        :resolve (instant
          (outcome "他把话说完就进去了"
            (lambda ()
              (set! tip? #t)
              (play-dialogue!
                (line "艾迪" "今晚。第三场。")
                (line "艾迪" "压我输。")
                (line "尼尔" "赔率多少？")
                (line "艾迪" "三块八赔一。")
                (line "艾迪" "第四回合。你手上要是有钱，现在就去写票。")
                (line "尼尔" "你为什么告诉我。")
                (line "艾迪" "你不是缺钱吗。"))
              (result-note! "内幕：他会在第四回合倒下。"))))))

    ;; ── 下注 ────────────────────────────────────────
    ;; 卡名要在整棵渲染树里唯一，所以带上押的是谁——两边各三档，不带名字就会撞。
    (define (node-bet side amount)
      (node (string-append (side-name side night) "·" (number->string amount) " 金")
        :subtitle (string-append "赢了拿回 "
                                 (number->string (payout amount (odds-of side night)))
                                 " 金；输了什么也没有")
        :requires (list (req-item "金钱" amount))
        :resolve (instant
          (outcome "票写好了"
            (lambda ()
              (set! bet-side side)
              (set! bet-amount amount)
              (rest-block! "拳赛" "你押了今晚的票，总得看完再回去睡"
                           "老街酒馆" "看比赛")
              (result-note! (string-append "你押了 " (side-name side night)
                                           " " (number->string amount) " 金。")))))))

    (define (node-bet-side side)
      (node (string-append "押 " (side-name side night) " 赢")
        :subtitle (string-append "赔率 " (odds-text (odds-of side night)) "；只有三个档，写票的不收零头")
        :children (list (node-bet side 5) (node-bet side 10) (node-bet side 20))))

    ;; ── 比赛 ────────────────────────────────────────
    (define (bet-heat)
      (cond ((>= bet-amount 20) 2)
            ((>= bet-amount 10) 1)
            (else 0)))

    (define (settle-bet!)
      (if (= bet-side 0)
          #f
          (if (= bet-side (winner night))
              (let ((take (payout bet-amount (odds-of bet-side night))))
                (add-item! "金钱" take)
                (heat-clk 'advance! (+ (bet-heat) 1))
                (result-note! (string-append "写票的数了 " (number->string take) " 金给你。")))
              (begin
                (heat-clk 'advance! (bet-heat))
                (result-note! "那张票现在是一张废纸。")))))

    (define (finish-night!)
      (set! watched? #t)
      (set! awaiting-signal? #f)
      (rest-release! "拳赛"))

    (define (fight-1!)
      (play-dialogue!
        (line "世界" "第一回合，莫里斯把那小子按在绳上打了半分钟。看台上都在笑。")
        (line "世界" "第二回合他还在笑。第三回合他抬手的时候慢了半拍。")
        (line "世界" "小马丁一记直的从中间穿过去。莫里斯坐在了地上，没再站起来。"))
      (set! result-line "小马丁赢。看台上一半的人把票撕了。")
      (settle-bet!)
      (finish-night!)
      (spotlight! "冷门" "写票的一晚上收了三十几张莫里斯的票。他一张也不用付。"))

    (define (fight-2!)
      (play-dialogue!
        (line "世界" "奥班农上台的时候还在系鞋带。")
        (line "世界" "韦德用那只缠着布的左手把他顶在角上，然后换右手。")
        (line "世界" "两回合。奥班农倒下去的时候，后门那个下大注的人已经不在座位上了。")
        (line "世界" "写票的没看擂台。他在看那个空掉的位子。"))
      (set! result-line "韦德赢。有人在场边输掉了不该输的数目。")
      (settle-bet!)
      (finish-night!)
      (spotlight! "别人的大注不是情报"
        "那个人一口气吃下的票，只证明他也在猜。写票的记住的不是他买了谁，是他买了多少。"))

    (define (fight-3!)
      (play-dialogue!
        (line "世界" "艾迪挨了很多拳。他不躲，他等。")
        (line "世界" "第五回合，莫里斯的右手慢下来。艾迪一直在等的就是这个。")
        (line "世界" "他把莫里斯打到绳子上，然后打到地上。看台站起来了。"))
      (set! result-line "艾迪赢了。他在后面坐着，手还没解开。")
      (settle-bet!)
      (finish-night!)
      (play-dialogue!
        (line "艾迪" "八块。")
        (line "尼尔" "赢了才八块？")
        (line "艾迪" "赢是给观众看的。挣钱才是工作。")
        (line "艾迪" "所以我不常认真打。"))
      (spotlight! "八块" "他把钱折了两折，塞进袜子里，然后开始解手上的布。"))

    ;; 第四夜分两段：先打到第三回合，他抬头看你，然后才结算。
    (define (fight-4-open!)
      (play-dialogue!
        (line "世界" "第一回合什么也没发生。")
        (line "世界" "第二回合凯利往里冲，艾迪的右手比脑子快——那一下是本能的。")
        (line "世界" "凯利退到绳边，扶着绳子站着，眼睛已经不在焦点上了。")
        (line "世界" "第三回合。看台在喊：结束他。")
        (line "世界" "艾迪站在他面前，不出手。他抬头，往你这边看了一眼。"))
      (set! awaiting-signal? #t)
      ;; 「看比赛」这张卡已经不在了，把「今晚不能睡」重新指到场子本身。
      ;; 押没押都要挡：他还站在台上等你那一下，这一晚不能就这么散了。
      (rest-block! "拳赛" "第三回合还没打完，你走不了"
                   "老街酒馆" "地下拳场"))

    (define (fight-4-dive!)
      (play-dialogue!
        (line "世界" "艾迪开始往后退。")
        (line "世界" "他挨了一拳，又一拳。凯利自己都站不稳，还在挥。")
        (line "世界" "看台先是安静，然后开始骂。有人喊：这他妈是假的。")
        (line "世界" "第四回合，艾迪倒下去。裁判数：七。八。九。")
        (line "世界" "他睁着眼睛。他完全能站起来。")
        (line "世界" "十。"))
      (set! result-line "凯利赢。艾迪从帆布上爬起来的时候，整个场子都在骂他。")
      (settle-bet!)
      (if (and tip? (= bet-side 2)) (heat-clk 'advance! 1) #f)
      (finish-night!)
      (spotlight! "十"
        (string-append
          (cond
            ((= bet-side 2) "钱在你口袋里。")
            ((= bet-side 1) "你那张票是废纸。")
            (else "你一分钱也没押。"))
          "他从帆布上爬起来，绕着场子走了半圈，没有一个人停下来。")))

    (define (fight-4-win!)
      (play-dialogue!
        (line "世界" "你把头偏了一下。很轻。")
        (line "世界" "艾迪看了两秒。")
        (line "世界" "然后他一拳把凯利打到帆布上，那孩子没再动。")
        (line "世界" "看台叫起来了。写票的没叫。他把笔放下了。"))
      (set! result-line "艾迪赢。写票的从头到尾没抬手记一笔。")
      (settle-bet!)
      (finish-night!)
      (spotlight! "他没演"
        (string-append
          (cond
            ((= bet-side 2) "你押他倒下的那张票是废纸。")
            ((= bet-side 1) "没人买他，除了你。")
            (else "你一分钱也没押。"))
          "他站在灯下面喘气，看台在为他叫。那笔二十五块他也没拿到。")))

    (define (node-signal name label after)
      (node name
        :subtitle label
        :resolve (instant
          (outcome "你做了个动作"
            (lambda ()
              (set! signal name)
              (after))))))

    (define (signal-nodes)
      (list
        (node-signal "点头" "按你们说好的来" fight-4-dive!)
        (node-signal "摇头" "让他赢；你押的票作废" fight-4-win!)
        (node-signal "别开眼" "什么也不做；他会按原计划" fight-4-dive!)))

    (define (node-watch-fight)
      (node "看比赛"
        :subtitle "不耗行动骰，也不再花钱"
        :resolve (instant
          (outcome "开赛"
            (lambda ()
              (cond ((= night 1) (fight-1!))
                    ((= night 2) (fight-2!))
                    ((= night 3) (fight-3!))
                    (else (fight-4-open!))))))))

    ;; ── 拳场 ────────────────────────────────────────
    (define (fight-night?) (and (= stage 1) (= world-day next-fight-day)))

    (define (card-note)
      (node "标注：今晚的盘口"
        :resolve (note
          (string-append "第 " (number->string night) " 场")
          (string-append
            (fighter-a night) "　" (odds-text (odds-a night))
            "　／　"
            (fighter-b night) "　" (odds-text (odds-b night))
            (if (> bet-side 0)
                (string-append "　　你押了 " (side-name bet-side night)
                               " " (number->string bet-amount) " 金。")
                "")))))

    (define (result-note-node)
      (node "标注：结果"
        :resolve (note "散场" result-line)))

    (define (tip-note-node)
      (node "标注：内幕"
        :resolve (note "内幕" "他会在第四回合倒下。这场子里只有你和写票的对家知道。")))

    (define (quiet-note-node)
      (node "标注：今晚没有比赛"
        :resolve (note "地下拳场"
          (string-append "今晚没有场子。下一场还有 "
                         (number->string (- next-fight-day world-day))
                         " 天。"))))

    ;; 庄家的注意跟着玩家从第一晚走到查封，它决定第四夜之后那只手伤成什么样——
    ;; 所以它必须摆在下注的地方。挂在「地下拳场」的 :clocks 上等于看不见：
    ;; 容器上的钟只在容器还是一张卡的时候可见，点进去就没了，而下注全在里面发生。
    (define (heat-note-node)
      (clock-node "钟：庄家的注意" (heat-clk 'render-data)))

    (define (ring-children)
      (cons
       (heat-note-node)
       (cond
        ;; 比赛日，还没看
        ((and (fight-night?) (not watched?))
         (append
           (list (card-note))
           (if (and (= night 4) tip?) (list (tip-note-node)) '())
           (if (and (= night 4) (not tip?) (not awaiting-signal?)) (list (node-tip)) '())
           (if awaiting-signal?
               (signal-nodes)
               (append
                 (list (node-warmup) (node-ringside))
                 (if (>= night 3) (list (node-talk)) '())
                 (if (= bet-side 0)
                     (list (node-bet-side 1) (node-bet-side 2))
                     '())
                 (list (node-watch-fight))))))
        ;; 比赛日，看完了
        ((fight-night?) (list (result-note-node)))
        ;; 不是比赛日
        (else (list (quiet-note-node))))))

    (define (ring-node)
      (node "地下拳场"
        :anchor "老街酒馆"
        :subtitle (if (fight-night?)
                      "酒馆后面那间货栈。今晚有场子"
                      "酒馆后面那间货栈。今晚门锁着")
        :children (ring-children)))

    ;; ── 查封 ────────────────────────────────────────
    (define (shut-down!)
      (set! stage 2)
      (set! shut-day world-day)
      (set! hand-bad? (and (>= (heat-clk 'current) heat-hurt)
                           (not (equal? signal "摇头"))))
      (rest-release! "拳赛")
      (notify! "地下拳场今早贴了封条。有人举报了。艾迪也不在码头。"))

    ;; ── 码头重逢 ────────────────────────────────────
    (define (node-reunion)
      (node "码头上那个包着手的人"
        :subtitle "不耗行动骰"
        :resolve (instant
          (outcome "他还在搬货"
            (lambda ()
              (set! stage 3)
              (if hand-bad?
                  (play-dialogue!
                    (line "世界" "他右手包着东西，包得很厚。他把箱子架在左肩上走。")
                    (line "尼尔" "手怎么了。")
                    (line "艾迪" "有人问我还告诉了谁。")
                    (line "尼尔" "你说了？")
                    (line "艾迪" "没有。")
                    (line "世界" "他把右手从布里抽出来给你看。中间两根指头不跟着动。")
                    (line "工头" "你这样怎么干？")
                    (line "艾迪" "左手不是还在吗。")
                    (line "世界" "他把箱子架回肩上，走了。"))
                  (play-dialogue!
                    (line "世界" "他脸上又添了新的。右手包着东西，包得不厚。")
                    (line "尼尔" "手怎么了。")
                    (line "艾迪" "违约。规矩就是这样。")
                    (line "工头" "你这样怎么干？")
                    (line "艾迪" "左手不是还在吗。")
                    (line "世界" "他把箱子架回肩上，走了。")))
              (result-note! "他还在码头。仅此而已。"))))))

    (define (reunion-open?)
      (and (= stage 2) (>= world-day (+ shut-day reunion-delay))))

    ;; ── 日终 ────────────────────────────────────────
    ;; 日终规则按注册的倒序跑，「世界日历推进」是最后注册的，所以它先走：
    ;; 这条规则跑的时候 world-day 已经是第二天了，下面一律按新的一天算。
    ;;
    ;;   比赛日过完 → 排下一场（第四夜过完则收摊）
    ;;   新的一天正好是比赛日 → 夜次 +1
    (define-turn-rule "地下拳场排期"
      (lambda () (= stage 1))
      (lambda ()
        (set! bet-side 0)
        (set! bet-amount 0)
        (set! warmup-seen 0)
        (set! ringside-seen 0)
        (set! talked? #f)
        (set! watched? #f)
        (set! awaiting-signal? #f)
        (set! result-line "")
        (if (> world-day next-fight-day)
            (if (>= night last-night)
                (shut-down!)
                (set! next-fight-day (+ next-fight-day fight-interval)))
            #f)
        (if (and (= stage 1) (= world-day next-fight-day))
            (set! night (+ night 1))
            #f)))

    ;; ── 对外 ────────────────────────────────────────
    (define (nodes-at location)
      (cond
        ((equal? location "酒馆")
         (cond
           ((= stage 4) (list (node-alley)))
           ((= stage 1) (list (ring-node)))
           (else '())))
        ((equal? location "码头")
         (if (reunion-open?) (list (node-reunion)) '()))
        (else '())))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((equal? msg 'stage) stage)
          ((equal? msg 'known?) (and (>= stage 1) (<= stage 3)))
          ((equal? msg 'hand-bad?) hand-bad?)
          ((equal? msg 'save)
           (list (list "stage" stage)
                 (list "alley-day" alley-day)
                 (list "alley-told" (if alley-told? 1 0))
                 (list "next-fight-day" next-fight-day)
                 (list "night" night)
                 (list "shut-day" shut-day)
                 (list "bet-side" bet-side)
                 (list "bet-amount" bet-amount)
                 (list "warmup-seen" warmup-seen)
                 (list "ringside-seen" ringside-seen)
                 (list "talked" (if talked? 1 0))
                 (list "watched" (if watched? 1 0))
                 (list "awaiting-signal" (if awaiting-signal? 1 0))
                 (list "signal" signal)
                 (list "result-line" result-line)
                 (list "tip" (if tip? 1 0))
                 (list "hand-bad" (if hand-bad? 1 0))
                 (list "heat" (heat-clk 'save))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! stage (assoc-get data "stage" 0))
             (set! alley-day (assoc-get data "alley-day" 0))
             (set! alley-told? (= (assoc-get data "alley-told" 0) 1))
             (set! next-fight-day (assoc-get data "next-fight-day" 0))
             (set! night (assoc-get data "night" 0))
             (set! shut-day (assoc-get data "shut-day" 0))
             (set! bet-side (assoc-get data "bet-side" 0))
             (set! bet-amount (assoc-get data "bet-amount" 0))
             (set! warmup-seen (assoc-get data "warmup-seen" 0))
             (set! ringside-seen (assoc-get data "ringside-seen" 0))
             (set! talked? (= (assoc-get data "talked" 0) 1))
             (set! watched? (= (assoc-get data "watched" 0) 1))
             (set! awaiting-signal? (= (assoc-get data "awaiting-signal" 0) 1))
             (set! signal (assoc-get data "signal" ""))
             (set! result-line (assoc-get data "result-line" ""))
             (set! tip? (= (assoc-get data "tip" 0) 1))
             (set! hand-bad? (= (assoc-get data "hand-bad" 0) 1))
             (heat-clk 'load! (assoc-get data "heat" 0))
             ;; 押了票还没看比赛就存的档，读回来要把「今晚不能睡」也一起装回去。
             (if (and (= stage 1) (> bet-side 0) (not watched?))
                 (rest-block! "拳赛" "你押了今晚的票，总得看完再回去睡"
                              "老街酒馆" "看比赛")
                 #f)))
          (else #f))))))
