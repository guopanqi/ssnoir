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
    ;; 开票日（比赛前一天）押票的赔率补贴，单位是十分之一。
    ;; 它是这一整套的支点：不给补贴，前一天押票就永远是劣势选择——
    ;; 谁会放着「先查再押」不要，跑去闭着眼睛下注？给了补贴，两天才各自
    ;; 回答一个不同的问题：今天押是赔率好但没情报，明天押是情报全但赔率平。
    (define eve-odds-bonus 2)
    (define last-night 4)         ; 一共四夜，之后查封
    (define heat-max 6)
    (define reunion-delay 2)      ; 查封之后几天在码头再见到他
    ;; 巷子那件事不排日子：整个第一封信到第二封信之间，窗口一直开着，
    ;; 你哪天走进酒馆，就是哪天撞上。撞上以后只有当晚——一个人不可能连着几天挨打。

    ;; ── 状态 ────────────────────────────────────────
    ;; 0 还没听说 / 4 巷子里那件事今天可以去 / 5 这条线到此为止
    ;; 1 拳场开着 / 2 已查封 / 3 码头重逢已看过
    ;; 4 和 5 排在后面，是为了不动 1..3 —— 那三个数字后面所有代码都在读。
    (define stage 0)
    (define alley-day 0)          ; 撞上的是哪天；0＝窗口开着，还没撞上
    (define next-fight-day 0)     ; 下一场在哪天
    (define night 0)              ; 排到第几夜（1..4）
    (define shut-day 0)

    ;; 今晚的临时状态，每天清空
    (define bet-side 0)           ; 0 没押 / 1 押甲 / 2 押乙
    (define bet-amount 0)
    ;; 写票的时候赔率就写死在票上了。前一天押的那张票，赔率跟着那一天走，
    ;; 不会因为第二天赔率变了而改口——所以结算读的是这个，不是当天的表。
    (define bet-odds 0)
    (define warmup-seen 0)        ; 0 没查 / 1 坏 / 2 中 / 3 好
    (define ringside-seen 0)
    (define talked? #f)
    (define watched? #f)
    (define awaiting-signal? #f)
    (define signal "")            ; 点头 / 摇头
    (define result-line "")       ; 看完比赛之后留在场里的一行字

    (define tip? #f)              ; 第四夜的内幕已经到手
    (define hand-bad? #f)
    (define way-out? #f)          ; 第二章：他手上还有没有另一条路
    (define crushed? #f)          ; 第二章高潮：另一只手也压在链条底下了
    (define shared-drink? #f)     ; 事故后，尼尔有没有在酒馆请他喝过一杯

    ;; 这根钟管的是**你还能押多大**，不管艾迪那只手，也不管场子开几天。
    ;; 它以前的去处是决定手废不废，那让"点头还是摇头"的后果取决于你前几晚
    ;; 赢过多少——玩家按下去的时候根本不知道自己在哪一边。后来一度改成提前
    ;; 查封，那更糟：少掉第四夜就是少掉那个选择本身。
    ;; 现在它只做一件事：填满之后写票的不再收你的大票，20 金档灰掉。
    ;; 赢得太聪明的代价是**以后赚得慢**，而且是当场看得见的——他就在你面前收票。
    (define heat-clk
      (make-clock "庄家的注意" heat-max 'gauge
        (lambda (current max)
          (cond
            ((>= current max)
             "写票的记住你了。他不再收你的大票。")
            ((> current 0)
             "赢钱没问题。赢得太聪明才有问题——押满了他们会开始记你的脸。")
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

    ;; 第四夜凯利那档是 2.0，不是 3.8。
    ;; 那一晚你手里有内幕，它不是一次赌，是一次兑现——3.8 乘满档就是 76 金，
    ;; 搬一整周的货也就这个数，而且你一点风险都没冒。确定会赢的票，赔率本来
    ;; 就不该是全场最高的那一个。上限收在 40：靠赔率收，不靠规则挡人。
    (define (odds-b n)
      (cond ((= n 1) 25) ((= n 2) 28) ((= n 3) 16) (else 20)))

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
    ;; 它不是一张常驻的卡，只在触发当天可见。去了是一场真交锋，
    ;; 赢了这条线才开——窗口过去或救不下来，就是救不下来。

    ;; 窗口开着（还没撞上）。这一段里任何一天走进酒馆都会撞上。
    (define (alley-armed?)
      (and (= stage 4) (= alley-day 0)))

    ;; 就是今晚。卡只在撞上的那一天出现。
    (define (alley-live?)
      (and (= stage 4) (> alley-day 0) (= world-day alley-day)))

    ;; 窗口还开着，或撞上的当晚那张卡还在：贝恩斯的门前入场先让开。
    ;; 打完、没去、或窗口合上之后，下一次进酒馆才轮到他。
    (define (alley-pending?)
      (or (alley-armed?) (alley-live?)))

    ;; 死线用钟，不用「剩 N 天」这种 tag——tag 是贴在卡上的一个词，它不长在
    ;; 那条时间线上；同一个概念在别处（酒馆那件麻烦）已经是钟了，这里也得是钟。
    ;; 不用 make-clock：这根钟永远是「就今晚」，没有要存档的状态。
    (define (alley-clock)
      (list 'clock "今晚之内" 1 1 'countdown
            "就在你站着的这会儿。走开或者去睡，明天老街就没人再提这件事。"))

    (define (node-alley)
      (node "巷子里在打人"
        :anchor "老街酒馆"
        :tags (list "交锋")
        :clocks (list (alley-clock))
        :subtitle "酒馆侧墙那条巷子。三个人围着一个，没人打算过去"
        :resolve (instant (lambda () (start-encounter "巷子里在打人" on-alley-result)))))

    (define (on-alley-result result)
      (if (equal? result 'success)
          (begin
            (set! stage 1)
            (journal 'add! "巷子里那三个人围着艾迪。你插了手，他带你去看酒馆后面的拳场。")
            (set! next-fight-day (+ world-day 1))
            (set! night 0)
            (play-dialogue!
              (line "艾迪" "谢了。")
              (line "尼尔" "欠钱？")
              (line "艾迪" "差不多。说好第四回合躺下，没照办。")
              (line "尼尔" "……")
              (line "艾迪" "酒馆后头有场子，每两天一场。有钱的话带上。"))
            (spotlight! "地下拳场"
              "酒馆后面那间货栈，每两天一场。他说了名字，也说了从哪扇门进。"))
          (if (equal? result '倒下)
              ;; 倒下**只换文案，不换状态**：一样是 stage 5，这条线一样到此为止。
              ;; 但你插了手，然后和他躺在同一条巷子里——讲成"没有人提起有谁挨了打"
              ;; 就是把你替他挨的那几下抹掉了。
              (begin
                (set! stage 5)
                (spotlight! "谁也没占着便宜"
                  "他们走的时候，你和艾迪都躺在那条巷子里。第二天他没来找你，你也没去找他。"))
              (begin
                (set! stage 5)
                (spotlight! "这件事就到这儿"
                  "第二天巷子里只剩几摊冲淡的血。酒馆里没有人提起昨晚有谁挨了打。")))))

    ;; 窗口开启：第一封信那一段一开始，这件事就在等着了。
    ;;
    ;; 它<b>不排日子</b>。排日子等于要求玩家在某个特定的晚上正好路过老街——
    ;; 而玩家那几天在忙什么，是他自己的事。窗口从这里一直开到第二封信为止，
    ;; 你哪天走进酒馆，就是哪天撞上。这样"正好撞见"这件事是真的，
    ;; 不是日历替他安排的。
    ;;
    ;; 规则本身不说话：它只把窗口打开。消息要在酒馆里听——
    ;; 事发生在老街侧墙，在家里被告知是错的位置。
    (define-turn-rule "巷子里在打人"
      (lambda () (and (= stage 0)
                      (>= (three-letters 'story-stage) 1)))
      (lambda ()
        (set! stage 4)
        (set! alley-day 0)))

    ;; 走进酒馆的那一刻才撞上。alley-day 在回调里立刻记下——玩家可以退出去再进来，
    ;; 等交锋打完再记就会重播一遍。
    ;;
    ;; 记下的是<b>今天</b>，于是这件事就发生在今晚，也只在今晚：一个人不可能
    ;; 连着几天挨同一顿打。窗口是宽的，撞上之后的反应空间是一天。
    (define (arrival-alley)
      (arrival "巷子里在打人"
        (lambda ()
          (set! alley-day world-day)
          (play-remote-dialogue!
            (line "世界" "老街那边有人跑进酒馆找酒保，说侧墙那条巷子里有人在挨打。")
            (line "酒保" "又不是头一回。别往那头去。")
            (line "世界" "没有人打算过去。这一片今晚也不会有巡警。")))))

    ;; 和 nodes-at 同一套写法：地点不认识故事状态，只报自己的名字。
    (define (arrivals-at location)
      (cond
        ((equal? location "老街酒馆")
         (if (alley-armed?) (list (arrival-alley)) '()))
        (else '())))

    ;; 撞上了没去，就是没去。日终规则在「世界日历推进」之后跑，world-day 已经是
    ;; 第二天，所以撞上的那一晚过完这条就成立——反应空间正好一天。
    (define-turn-rule "巷子里那件事过去了"
      (lambda () (and (= stage 4)
                      (> alley-day 0)
                      (> world-day alley-day)))
      (lambda ()
        (set! stage 5)
        (notify! "老街侧墙那条巷子已经没人再提。")))

    ;; 从头到尾没进过酒馆：第二封信一来，这条线就悄悄合上了。
    ;; 这里<b>不通知</b>——玩家从没听说过这件事，凭空来一句"没人再提"，
    ;; 提的是他压根不知道的东西。
    (define-turn-rule "老街那边的事过去了"
      (lambda () (and (alley-armed?)
                      (>= (three-letters 'story-stage) 2)))
      (lambda () (set! stage 5)))

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
        :subtitle "台下那半个钟头，比谁说的都准——只要你看得懂"
        :disabled (> warmup-seen 0)
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome (lambda ()
              (set! warmup-seen 1)
              (result-supplement! (warmup-text night 1))))
          (outcome (lambda ()
              (set! warmup-seen 2)
              (result-supplement! (warmup-text night 2))))
          (outcome (lambda ()
              (set! warmup-seen 3)
              (result-supplement! (warmup-text night 3)))))))

    (define (node-ringside)
      (node "听场边"
        :subtitle "写票的那张桌子周围，话最多也最不值钱"
        :disabled (> ringside-seen 0)
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome (lambda ()
              (set! ringside-seen 1)
              (result-supplement! (ringside-text night 1))))
          (outcome (lambda ()
              (set! ringside-seen 2)
              (result-supplement! (ringside-text night 2))))
          (outcome (lambda ()
              (set! ringside-seen 3)
              (result-supplement! (ringside-text night 3)))))))

    ;; 第三夜起他认得你了。不耗行动骰，一晚一次——这是救过他换来的东西。
    ;; 第四夜的内幕也走这一张卡：他在门口等你，本身就是这晚「找他聊聊」的内容，
    ;; 不再并排放两个谈话入口。
    (define (node-talk)
      (node (if (= night 4) "艾迪在门口等你" "找艾迪聊聊")
        :subtitle (if (= night 4) "不耗行动骰；他有一件事要先告诉你" "不耗行动骰；他在后面缠手")
        :resolve (instant
          (outcome (lambda ()
              (set! talked? #t)
              (cond
                ((= night 4)
                 (set! tip? #t)
                 (play-dialogue!
                   (line "艾迪" "今晚第三场。")
                   (line "艾迪" "压我输。第四回合。")
                   (line "尼尔" "你喜欢那样打吗。")
                   (line "艾迪" "不常有机会。")
                   (line "尼尔" "你为什么告诉我。")
                   (line "艾迪" "你不是缺钱吗。"))
                 (result-supplement! "内幕：他会在第四回合倒下。"))
                ((= night 3)
                 (play-dialogue!
                   (line "艾迪" "今天别买我输。")
                   (line "尼尔" "为什么？")
                   (line "艾迪" "今天没人付我。")))
                (else
                 (play-dialogue!
                   (line "艾迪" "别问了。你知道的比这儿所有人都多。")
                   (line "艾迪" "钱带够没有。")))))))))

    ;; ── 下注 ────────────────────────────────────────
    ;; 卡名要在整棵渲染树里唯一，所以带上押的是谁——两边各三档，不带名字就会撞。

    ;; 注意满格之后，写票的不再收 20 金那一档。
    ;; 档位照样摆着，只是灰的：让玩家看见"你想押但押不了"，比它凭空消失强——
    ;; 这一档是怎么没的，他自己一晚一晚数着钟看过来的。
    (define big-ticket 20)

    (define (big-ticket-refused? amount)
      (and (= amount big-ticket) (heat-clk 'full?)))

    (define (node-bet side amount)
      (node (string-append (side-name side (card-night)) "·" (number->string amount) " 金")
        :subtitle (if (big-ticket-refused? amount)
                      "写票的不再收你这么大的票"
                      (string-append "赢了拿回 "
                                     (number->string (payout amount (today-odds side)))
                                     " 金；输了什么也没有"))
        :disabled (big-ticket-refused? amount)
        :requires (list (req-item "金钱" amount))
        :resolve (instant
          (outcome (lambda ()
              (set! bet-side side)
              (set! bet-amount amount)
              ;; 赔率写死在票上，从此不再看当天的表。
              (set! bet-odds (today-odds side))
              ;; 押完就不许回去睡，只在比赛当天成立：开票日押的票，那一晚
              ;; 根本没有比赛可看，拦着人睡觉是拦一场不存在的戏。
              ;; 比赛当天早上会替这张票重新上锁（见「地下拳场排期」）。
              (if (fight-night?)
                  (rest-block! "拳赛" "你押了今晚的票，总得看完再回去睡"
                               "老街酒馆" "看比赛")
                  #f)
              (result-supplement! (string-append "你押了 " (side-name side (card-night))
                                           " " (number->string amount) " 金。")))))))

    (define (node-bet-side side)
      (node (string-append "押 " (side-name side (card-night)) " 赢")
        :subtitle (string-append "赔率 " (odds-text (today-odds side))
                                 (if (eve?)
                                     "（提前写票，写票的还没收到大注）；只有三个档"
                                     "；只有三个档，写票的不收零头"))
        :children (list (node-bet side 5) (node-bet side 10) (node-bet side big-ticket))))

    ;; ── 比赛 ────────────────────────────────────────
    (define (bet-heat)
      (cond ((>= bet-amount 20) 2)
            ((>= bet-amount 10) 1)
            (else 0)))

    (define (settle-bet!)
      (if (= bet-side 0)
          #f
          (if (= bet-side (winner night))
              ;; 读票上写死的赔率，不读当天的表：前一天写的票就该按前一天的数赔。
              (let ((take (payout bet-amount bet-odds)))
                ;; 金额与庄家注意的落行由引擎自动写，不复述"数了 N 金"。
                (add-item! "金钱" take)
                (heat-clk 'advance! (+ (bet-heat) 1)))
              (begin
                (heat-clk 'advance! (bet-heat))
                (result-supplement! "那张票现在是一张废纸。")))))

    (define (finish-night!)
      (set! watched? #t)
      (set! awaiting-signal? #f)
      (rest-release! "拳赛"))

    (define (fight-1!)
      (play-dialogue!
        (line "世界" "第一回合，莫里斯把那小子按在绳上打了半分钟。看台上都在笑。")
        (line "世界" "第二回合还在笑。第三回合，莫里斯抬手慢了一拍。")
        (line "世界" "小马丁从中间穿过去，一记直拳。莫里斯坐在了地上，没再站起来。"))
      (set! result-line "小马丁赢。看台上一半的人把票撕了。")
      (settle-bet!)
      (finish-night!)
      (play-banter!
        (line "世界" "人往外走的时候，艾迪从后面出来，在你旁边经过，没停。")
        (line "世界" "他看了一眼你手里有没有票。"))
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
      (play-banter!
        (line "世界" "人往外走。后门那个位子空着。")
        (line "世界" "艾迪从后面绕过来，在那个位子旁边站了一步，然后走了。"))
      (spotlight! "别人的大注不是情报"
        "那个人一口气吃下的票，只证明他也在猜。写票的记住的不是他买了谁，是他买了多少。"))

    (define (fight-3!)
      (play-dialogue!
        (line "世界" "艾迪挨了很多拳。他不躲，他等。")
        (line "世界" "第五回合，莫里斯出右手，慢了一点。就一点。")
        (line "世界" "艾迪往里走了一步，把他打到绳子上，然后打到地上。看台站起来了。"))
      (set! result-line "艾迪赢了。他在后面坐着，手还没解开。")
      (settle-bet!)
      (finish-night!)
      (play-dialogue!
        (line "艾迪" "八块。")
        (line "尼尔" "赢了才八块？")
        (line "艾迪" "赢是给观众看的。挣钱才是工作。"))
      (play-banter!
        (line "世界" "看台上还有几个人没散，还在叫唤。他没往那边看。"))
      (spotlight! "八块" "他把钱折了两折，塞进袜子里，然后开始解手上的布。"))

    ;; 第四夜分两段：先打到第三回合，他抬头看你，然后才结算。
    (define (fight-4-open!)
      (play-dialogue!
        (line "世界" "第一回合什么也没发生。")
        (line "世界" "第二回合凯利往里冲，艾迪的右手比脑子快。")
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
          (outcome (lambda ()
              (set! signal name)
              (after))))))

    ;; 两个选项，不是三个。原来还有一张「别开眼」，它调用的函数和「点头」
    ;; 一模一样——同一个决定换了个说法，摆在那儿只会让人以为自己有第三条路。
    (define (signal-nodes)
      (list
        (node-signal "点头" "按你们说好的来；他第四回合倒下" fight-4-dive!)
        (node-signal "摇头" "让他赢；你押他倒下的票作废" fight-4-win!)))

    (define (node-watch-fight)
      (node "看比赛"
        :subtitle "不耗行动骰，也不再花钱"
        :resolve (instant
          (outcome (lambda ()
              (cond ((= night 1) (fight-1!))
                    ((= night 2) (fight-2!))
                    ((= night 3) (fight-3!))
                    (else (fight-4-open!))))))))

    ;; ── 拳场 ────────────────────────────────────────
    (define (fight-night?) (and (= stage 1) (= world-day next-fight-day)))

    ;; 开票日＝比赛的前一天。每两天一场，所以拳场那边其实天天有事：
    ;; 前一天开票，第二天开打。原来中间那一天只会告诉你「今晚门锁着」，
    ;; 一个占着位置的空节点。
    (define (eve?) (and (= stage 1) (= world-day (- next-fight-day 1))))

    ;; night 要到比赛当天早上才 +1，所以开票日那天它还是上一场的编号。
    ;; 盘口、赔率、押谁，问的都是「下一场」，一律走这个。
    (define (card-night) (if (fight-night?) night (+ night 1)))

    ;; 今天写票按什么赔率：开票日加早鸟补贴，比赛当天就是表上的数。
    (define (today-odds side)
      (+ (odds-of side (card-night)) (if (eve?) eve-odds-bonus 0)))

    (define (card-note)
      (node (if (eve?) "标注：明晚的盘口" "标注：今晚的盘口")
        :resolve (note
          (string-append "第 " (number->string (card-night)) " 场")
          (string-append
            (fighter-a (card-night)) "　" (odds-text (today-odds 1))
            "　／　"
            (fighter-b (card-night)) "　" (odds-text (today-odds 2))
            (if (> bet-side 0)
                (string-append "　　你押了 " (side-name bet-side (card-night))
                               " " (number->string bet-amount) " 金，赔率 "
                               (odds-text bet-odds) "。")
                "")))))

    ;; 开票日站在这儿看得见什么：牌子已经挂出来了，人还没到。
    (define (eve-note-node)
      (node "标注：明晚的场子"
        :resolve (note "开票"
          (if (> bet-side 0)
              "票已经写好了。明晚这个时候，你会知道自己押得对不对。"
              "牌子挂出来了，拳手明晚才到。现在写票，赔率还没被大注压下去；等到明晚，你能先看人再掏钱。"))))

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
           (if awaiting-signal?
               (signal-nodes)
               (append
                 (list (node-warmup) (node-ringside))
                 (if (and (>= night 3) (not talked?)) (list (node-talk)) '())
                 (if (= bet-side 0)
                     (list (node-bet-side 1) (node-bet-side 2))
                     '())
                 (list (node-watch-fight))))))
        ;; 比赛日，看完了
        ((fight-night?) (list (result-note-node)))
        ;; 开票日：盘口已经挂出来，可以提前写票，但看不到人
        ;; ——看热身、听场边都要拳手在场，那是明晚的事。
        ((eve?)
         (append
           (list (card-note) (eve-note-node))
           (if (= bet-side 0)
               (list (node-bet-side 1) (node-bet-side 2))
               '())))
        ;; 排期之外（查封前后的过渡日）
        (else (list (quiet-note-node))))))

    (define (ring-node)
      (node "地下拳场"
        :anchor "老街酒馆"
        :subtitle (cond
                    ((fight-night?) "酒馆后面那间货栈。今晚有场子")
                    ((eve?) "酒馆后面那间货栈。明晚的牌子已经挂出来了")
                    (else "酒馆后面那间货栈。今晚门锁着"))
        :children (ring-children)))

    ;; ── 查封 ────────────────────────────────────────
    ;; 只有一条路走到这儿：四夜打完。
    ;; 这里曾经还有第二条——「庄家的注意」满格就提前查封。那是个窟窿：
    ;; 少掉第四夜，就等于少掉点头还是摇头那个选择，这条线最后的戏被一根
    ;; 数值钟掐掉了。注意涨满该收紧的是你的钱，不是艾迪的故事。
    (define (shut-down!)
      (set! stage 2)
      (set! shut-day world-day)
      ;; 手废不废，只看你那一下，和任何钟都无关。
      ;; 它以前还要求「庄家的注意 ≥ 5」：于是同一个点头，有人换来两根废手指，
      ;; 有人什么事也没有——而玩家按下去的那一刻根本不知道自己在哪一边。
      ;; 那是数值上的分岔，不是选择上的分岔，玩起来只是无聊。现在就一句话：
      ;; 你点头，他按计划倒下，对家赔了钱，他们回头去找漏消息的人；
      ;; 你摇头，他违约挨一顿，手还是他自己的。
      (set! hand-bad? (equal? signal "点头"))
      (rest-release! "拳赛")
      (journal 'add! (if hand-bad?
                         "拳场贴了封条。那一场是卖的，赔钱的人回头去找了漏消息的人。"
                         "拳场贴了封条。有人举报了。"))
      (notify! "地下拳场今早贴了封条。有人举报了。艾迪也不在码头。"))

    ;; ── 码头重逢 ────────────────────────────────────
    (define (node-reunion)
      (node "码头上那个包着手的人"
        :anchor "码头-货堆"
        :subtitle "不耗行动骰"
        :resolve (instant
          (outcome (lambda ()
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
              ;; 码头重逢是这条线的收束。stage 5（没去巷子 / 窗口过了）不发——
              ;; 那条路上玩家什么也没经历。发点放在对白之后，否则通知被整段对白盖掉。
              (complete-task! "巷子里的人"))))))

    (define (reunion-open?)
      (and (= stage 2) (>= world-day (+ shut-day reunion-delay))))

    ;; 说完话他不会从码头上消失——只是没有可做的事了。往后他是一条标注：
    ;; 站在这儿就看得见的人，不是一张要点的卡。
    ;; 标注的节点名不渲染，所以这里把它当 ID 用，固定不变；玩家看见的标题是「艾迪」。
    ;; 重逢前那张卡叫「码头上那个包着手的人」——那时候还没认出他来，标题本身是那一眼。
    (define (node-eddie-at-dock)
      (at-anchor "码头-货堆"
       (note-node "艾迪·码头" "艾迪"
        (if hand-bad?
            "在货堆那头搬箱子。右手包着，包得很厚，箱子架在左肩上。他不往这边看。"
            "在货堆那头搬箱子。右手包着，包得不厚。他不往这边看。"))))

    ;; 人物结局在酒馆里留下的投影。它不是一条救济任务：玩家只能请一杯，
    ;; 和他坐一会儿。不吃骰，免得一次陪伴又变成当晚必须排进计划的工作。
    (define (node-share-drink-after-crush)
      (node "请艾迪喝一杯"
        :anchor "老街酒馆"
        :subtitle "陪他坐一会儿"
        :requires (list (req-item "金钱" 15))
        :resolve (instant
          (outcome (lambda ()
              (set! shared-drink? #t)
              (restore-actor-composure! 'player 1)
              (play-dialogue!
                (line "世界" "露丝把酒放在艾迪面前。他看了很久。")
                (line "艾迪" "喝完了就得再买一杯。")
                (line "尼尔" "这一杯算我的。")
                (line "艾迪" "那我慢点喝。")))))))

    (define (node-eddie-after-crush)
      (if shared-drink?
          (at-anchor "老街酒馆"
            (note-node "艾迪·酒馆" "艾迪"
              "坐在靠墙那桌，杯里的酒一直没有喝完。"))
          (node-share-drink-after-crush)))

    ;; ── 日终 ────────────────────────────────────────
    ;; 日终规则按注册的倒序跑，「世界日历推进」是最后注册的，所以它先走：
    ;; 这条规则跑的时候 world-day 已经是第二天了，下面一律按新的一天算。
    ;;
    ;;   比赛日过完 → 排下一场（第四夜过完则收摊）
    ;;   新的一天正好是比赛日 → 夜次 +1
    (define-turn-rule "地下拳场排期"
      (lambda () (= stage 1))
      (lambda ()
        ;; 清空只发生在**比赛过完的第二天早上**，不再每天清一次。
        ;; 开票日押下的票必须活过这一夜——那正是开票日存在的意义。
        (if (> world-day next-fight-day)
            (begin
              (set! bet-side 0)
              (set! bet-amount 0)
              (set! bet-odds 0)
              (set! warmup-seen 0)
              (set! ringside-seen 0)
              (set! talked? #f)
              (set! watched? #f)
              (set! awaiting-signal? #f)
              (set! result-line "")
              (if (>= night last-night)
                  (shut-down!)
                  (set! next-fight-day (+ next-fight-day fight-interval))))
            #f)
        (if (and (= stage 1) (= world-day next-fight-day))
            (begin
              (set! night (+ night 1))
              ;; 前一天写的票，到了比赛当天才开始拦你回去睡。
              (if (> bet-side 0)
                  (rest-block! "拳赛" "你押了今晚的票，总得看完再回去睡"
                               "老街酒馆" "看比赛")
                  #f))
            #f)))

    ;; ── 卷宗 ────────────────────────────────────────
    ;; 这条线的压力全在「还剩几夜」上：拳场只开四个晚上，过了就查封。
    ;; 履历只记真正过去的那几夜，不记每天的输赢——那是钱的事，不是故事的事。
    (define journal (make-journal))

    ;; 这条线第一章一张卡：巷子 → 拳场 → 码头重逢。码头重逢划掉那一拍发成长。
    ;; 没插手（stage 5）就没有这张卡——那条路上玩家什么也没经历。
    (define (steps)
      (list (step "插手巷子里那件事" (member? stage (list 1 2 3)))
            (step "去酒馆后面看一场" (or (> night 0) (>= stage 2)))
            (step "码头上再见到他" (>= stage 3))))

    (define (dossier-entry)
      (cond
        ((= stage 1)
         (list (dossier "巷子里的人"
                 :kind '人物
                 :status (if (fight-night?) '进行中 '等着别人)
                 :now (cond
                        ((fight-night?) "今晚有一场。去酒馆后面，先看懂比赛再押钱")
                        ((eve?) "明晚有一场。今晚可以先去酒馆后面写票，赔率比明晚好")
                        (#t (string-append "下一场在第 " (number->string next-fight-day)
                                           " 天")))
                 :where "老街酒馆"
                 :clocks (list (heat-clk 'render-data))
                 :steps (steps)
                 :log (journal 'render-data))))
        ;; 只有撞上了才立卷宗。窗口开着但还没进过酒馆时，玩家根本没听说过这件事——
        ;; 卷宗里凭空多出一条卡，等于替他剧透一个他还没遇到的晚上。
        ((alley-live?)
         (list (dossier "巷子里的人"
                 :kind '人物
                 :status '进行中
                 :now "酒馆侧墙今晚有人在挨打；只有今晚"
                 :where "老街酒馆"
                 :steps (steps)
                 :log (journal 'render-data))))
        ((>= stage 2)
         (list (dossier "巷子里的人"
                 :kind '人物
                 :status (if (>= stage 3) '了结 '进行中)
                 :now (if (>= stage 3) "" "拳场查封了；过几天码头上还能碰见他")
                 :where (if (>= stage 3) "" "码头")
                 :steps (steps)
                 :log (journal 'render-data))))
        (else '())))

    ;; ── 对外 ────────────────────────────────────────
    (define (nodes-at location)
      (cond
        ((equal? location "老街酒馆")
         (cond
           ((alley-live?) (list (node-alley)))
           ((= stage 1) (list (ring-node)))
           (crushed? (list (node-eddie-after-crush)))
           (else '())))
        ((equal? location "码头")
         ;; stage 5 是这条线断掉的那一支，那时候玩家从没认识过他，码头上也就没有他。
         (cond
           ((reunion-open?) (list (node-reunion)))
           ((= stage 3) (list (node-eddie-at-dock)))
           (else '())))
        (else '())))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'dossier) (dossier-entry))
          ((equal? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((equal? msg 'stage) stage)
          ((equal? msg 'alley-pending?) (alley-pending?))
          ((equal? msg 'known?) (and (>= stage 1) (<= stage 3)))
          ((equal? msg 'hand-bad?) hand-bad?)
          ;; 第二章起：他手上还有没有另一条路。机器进老街那天读的就是这一个。
          ;; 怎么拿到的（培训、别的活、还是你替他找的门路）归那件事自己。
          ((equal? msg 'on-training-passed!) (set! way-out? #t))
          ((equal? msg 'has-way-out?) way-out?)
          ((equal? msg 'on-crushed!) (set! crushed? #t))
          ((equal? msg 'crushed?) crushed?)
          ;; 跳章调试：把巷子与拳场窗口全部收口，但保留「认识艾迪」。
          ;; stage 3 是码头重逢已看过，不会再往酒馆投入场节拍。
          ((equal? msg 'debug-finish-chapter1!)
           (set! stage 3)
           (set! alley-day 0)
           (set! next-fight-day 0)
           (set! night 0)
           (set! shut-day 0)
           (set! bet-side 0)
           (set! bet-amount 0)
           (set! awaiting-signal? #f)
           (set! way-out? #f)
           (set! crushed? #f)
           (set! shared-drink? #f))
          ((equal? msg 'save)
           (list (list "stage" stage)
                 (list "way-out" (if way-out? 1 0))
                 (list "crushed" (if crushed? 1 0))
                 (list "shared-drink" (if shared-drink? 1 0))
                 (list "journal" (journal 'save))
                 (list "alley-day" alley-day)
                 (list "next-fight-day" next-fight-day)
                 (list "bet-odds" bet-odds)
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
             (set! way-out? (= (assoc-get data "way-out" 0) 1))
             (set! crushed? (= (assoc-get data "crushed" 0) 1))
             (set! shared-drink? (= (assoc-get data "shared-drink" 0) 1))
             (if (and shared-drink? (not crushed?))
                 (error "艾迪存档错误：机器事故未发生却已在酒馆请过酒")
                 #t)
             (journal 'load! (assoc-get data "journal" '()))
             (set! alley-day (assoc-get data "alley-day" 0))
             (set! next-fight-day (assoc-get data "next-fight-day" 0))
             (set! bet-odds (assoc-get data "bet-odds" 0))
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
