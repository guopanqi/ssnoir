;; scenes/encounters/巷子.scm - 第一章·小节三「教训莱恩」
;;
;; 两幕，同一个晚上。两幕各只有一个想法，骨架都很小。
;;
;; 第一幕·潜入 —— 照 infiltration 的骨架，是**环境潜入**不是 buff 按钮：
;;   一根失败钟（惊动的人）、一根目标钟（摸到他门口），主动作「往里摸」两边都推；
;;   除了堆场，还有两个**去处**，各自握着一个改写全局规则的开关：
;;     走廊尽头的灯 —— 大开关。灭掉它，往里摸完全不惊动人，两个 −1 一起消失；
;;                     代价前置（灭的瞬间 +1）、限时（两回合）、且**只有一次**：
;;                     灯一亮回来 +2 惊动，这条路从此锁死。对应 infiltration 的断电。
;;     看堆场的棚子 —— 小开关。把老头引开只清掉他那一个 −1，但**可以反复买**。
;;   「由头」是不掷骰的推进出口（往里走一段 / 换一盏灯），差骰子有地方去。
;;   压着这一切的是「他快回来了」：每回合末走一格，跟你做什么无关。
;;   两个开关都很值，但每个开关都要花你一个回合，而他不等你布置完——这才是这一幕的题。
;;   两头都不能不管：不动开关，灯亮着硬推惊动会先满；全花在布置上，他到家你还在堆场。
;;
;; 幕间 —— 巷口的人的起始格 = 惊动的人 ± 你是等在那儿还是追出去的。
;;         「你走到他面前的时候还剩下什么」的机械形式。
;;
;; 第二幕·小心翼翼地处理他 —— 目标不是一根从头填到尾的进度条，是**这个人的形态**：
;;   端着 → 翻脸 → 撒手。形态不额外占钟，它就是莱恩那张卡的面貌，
;;   标题、神态、能对他做的事整个换掉。推动形态的两根短钟同时只有一根在场：
;;     「他的面子」—— 拆掉他的表演。满：他不装了，承认前两封信都是他写的
;;     「他的话」　—— 他开始说真的。中途给出「第一封她就知道」
;;   第三形态没有钟，是三张一次性的卡：谈 / 逼 / 交换。选一张，这笔旧账就结了。
;;
;;   压力只有一根「老街看着」，只升不降。这不是老街在替勒索辩护——
;;   他们只是不会允许一个外人当街把自己人拖走、搜身、按在墙上。
;;   时间的代价不归它管：交锋每回合自己扣冷静，熬下去要还的是身体。
;;   于是"这颗 2 现在投给高风险卡，还是烂在手里等下一轮"变成一个真问题。
;;
;;   底片跟着屈服走：他撒手了，东西就是你的。不单列成目标，也不做成机会卡——
;;   把它做成一次抢，这一节就又变回追债了。
;;
;; 对外契约：回传 (list 收场 熟脸增 劳工增)
;;   收场 ：'谈 / '逼 / '交换 / '难看——他都屈服了，区别是你用什么方式结的
;;   熟脸增 / 劳工增：第一幕用谁的路走出来的关系，由《三封信》写回城市
;;
;; 城市输入：四把普通钥匙，以及弗兰克对玩家的离散态势。
;; 钥匙不给专属按钮，只改开局态势——省掉的是你本来要花在开场上的那两颗骰。

(define (key? name)
  (let ((v (get-global name))) (if v v #f)))

(define frank-state
  (let ((value (get-global '态势-弗兰克)))
    (if value value "普通")))
(if (member? frank-state (list "认可" "普通" "不信任"))
    #t
    (error "巷子：缺少有效的弗兰克开局态势"))
(define key-joe    (key? '钥匙-乔))
(define key-otto   (key? '钥匙-奥托))
(define key-lottie (key? '钥匙-洛蒂))
(define key-owner  (key? '钥匙-酒馆老板))

(define alarm-max 6)
(define approach-max 10)
;; 老街看着只有五格，而且只升不降。格子少是故意的：每一次冒进都看得见地贵。
(define crowd-max 5)
(define face-max 5)        ; 形态一：拆掉他的表演
(define words-max 6)       ; 形态二：让他把话说完
(define arrival-turns 4)   ; 他走回来要几个回合——这根钟跟你做什么无关
(define dark-turns 2)      ; 灯灭之后还剩几个回合
(define watch-turns 3)     ; 老头被引开之后还剩几个回合

;; ── 两幕共用的失败钟 ────────────────────────────
(define alarm-clk
  (make-clock "惊动的人" alarm-max 'segments
    "这一片有多少人知道今晚有个外人在。满格：整排屋子的灯都亮了，今晚办不成事。"))

;; ── 第一幕 ──────────────────────────────────────
(define approach-clk
  (make-clock "摸到他门口" approach-max 'segments
    "从堆场到那扇没有窗的门。满格你就站在他回家的必经之路上。"))

;; 这一幕真正的压力源：它每回合走一格，你做什么都拦不住它。
;; 两个开关都很值，但每个开关都要花你回合——这就是这场潜入的题。
(define arrival-clk
  (make-clock "他快回来了" arrival-turns 'countdown
    (lambda (current max)
      (if (<= current 1)
          "巷子那头已经有脚步声了。这一回合结束他就到家——你还在半路上，就只能从后面追进去。"
          "他喝完最后一杯就往回走。归零时你没摸到门口，就是从堆场追出去的，巷口的人会多两格。"))))

;; 满格的钟不撤掉，只换一句话：灭着的时候它说你现在有什么，锁死之后它说这条路没了。
(define lamp-clk
  (make-clock "走廊的气灯" 3 'segments
    (lambda (current max)
      (cond
        (lamp-locked? "有人换上了新灯罩守在灯下。这条走廊今晚再也灭不了。")
        (lights-out? "灯全灭着。趁现在往里摸，不惊动任何人。")
        (#t "一盏一盏拧掉。全灭：这一片黑下来，往里摸不再惊动任何人。")))))

(define dark-clk
  (make-clock "这一片还黑着" dark-turns 'countdown
    "灯灭着还剩几个回合。归零：有人换上新灯罩站在灯下，惊动 +2，这条走廊今晚再也走不通。"))

(define watch-clk
  (make-clock "把老头引开" 2 'segments
    (lambda (current max)
      (if watch-away?
          "他已经被支到堆场另一头去了。灯还是亮的——他只是一双眼睛。"
          "看堆场的老头坐在棚子口。满格他会离开一阵子，他回来了还能再引一次。"))))

(define watch-timer
  (make-clock "他就要回来了" watch-turns 'countdown
    "老头还离得开多久。归零他坐回棚子口，可以再引一次。"))

(define excuse-clk
  (make-clock "手上的由头" 2 'segments
    "一件工服、一张送货单。用掉一个就能大方地办一件事，不必掷骰。"))

;; ── 第二幕 ──────────────────────────────────────
(define crowd-clk
  (make-clock "老街看着" crowd-max 'segments
    "一件私事正在变成「一个外人在老街欺负自己人」。满格：有人挡在你们中间，这事当街收场。"))

(define face-clk
  (make-clock "他的面子" face-max 'segments
    "他还端着「她终于怕了」这套说辞。满格他就不装了。"))

(define words-clk
  (make-clock "他的话" words-max 'segments
    "他开始说真的。让他说完，你才知道这笔旧账到底是谁欠谁的。"))

(define act 1)
(define form 1)             ; 莱恩的形态 1 端着 / 2 翻脸 / 3 撒手
(define familiar-gain 0)
(define labor-gain 0)
(define cigs-used? #f)      ; 那半包老金牌只能拍一次桌子
(define finished? #f)
(define lights-out? #f)     ; 走廊现在黑着
(define lamp-locked? #f)    ; 灯已经被换过，这条路锁死
(define watch-away? #f)     ; 老头现在不在棚子口

(define act1-blown? #f)     ; 潜入被彻底惊动：第二幕从最难的位置开始

(define (tick-n! clk n)
  (if (<= n 0) #f (begin (clk 'tick!) (tick-n! clk (- n 1)))))

;; ============================================================
;; 第一幕·潜入
;; ============================================================

;; 环境的两个开关。它们不是贴在你身上的加成，是改写全场规则的世界状态：
;; 灯灭着，往里摸完全不惊动人；老头不在，少一双看着你的眼睛。
(define (old-man-watching?)
  (and (not watch-away?) (not lights-out?)))

(define (alarm+ n)
  (if lights-out? #f (tick-n! alarm-clk n)))

(define (check-alarm!)
  (if (and (= act 1) (not finished?) (alarm-clk 'full?))
      (fail-act1!)
      #f))

;; 惊动满格不再是「今晚办不成事」。这一节必须真正结一次案——
;; 一条"白跑一趟"的分支会让第三封信那记闷棍轻掉一半。
;; 所以它改成最难的开局：整片地方都醒着，你只能直着走进棚子，
;; 一进门老街就已经在看你了，几乎注定要当街收场。
(define (fail-act1!)
  (play-dialogue!
    (line "世界" "一扇门开了，接着是第二扇。有人举着灯站到走廊上，谁也没说话。")
    (line "世界" "藏不住了。你从阴影里走出来，直接朝那盏亮着的棚子灯走过去。"))
  (set! act1-blown? #t)
  (begin-act2! #f))

;; 惊动先结算：同一手里灯先亮起来，就没有「到门口」这回事了。
(define (approach+ n)
  (if finished?
      #f
      (begin
        (approach-clk 'advance! n)
        (if (approach-clk 'full?) (begin-act2! #t) #f))))

;; ── 堆场：主动作与由头出口 ──────────────────────

(define (sneak-modifiers)
  (append
    (if lights-out? '() (list (modifier -1 "灯还亮着")))
    (if (old-man-watching?) (list (modifier -1 "老头在棚子口")) '())))

(define (node-sneak)
  (node "往里摸"
    :subtitle (cond
                (lights-out? "敏锐；这一片黑着——趁现在，走多远都没人看得见")
                ((old-man-watching?) "敏锐；灯亮着，老头就坐在棚子口，每走一步都有人可能抬头")
                (#t "敏锐；灯还亮着，但这会儿没人往这边看"))
    :requires (list (req-die))
    :resolve (roll 'sharpness sneak-modifiers
      (outcome "你碰翻了什么"
        (lambda () (alarm+ 2) (check-alarm!)))
      (outcome "又往里挪了一段"
        (lambda () (alarm+ 1) (check-alarm!) (approach+ 1)))
      (outcome "一口气穿过了堆场"
        (lambda () (alarm+ 1) (check-alarm!) (approach+ 2))))))

(define (node-use-excuse)
  (node "大方地走过去"
    :subtitle (if (excuse-clk 'empty?)
                  "手上没有由头。这么走过去只会被人叫住"
                  "用掉一个由头。不掷骰，直接往里一段")
    :disabled (excuse-clk 'empty?)
    :resolve (instant
      (outcome "没有人拦你"
        (lambda ()
          (excuse-clk 'advance! -1)
          (approach+ 1))))))

(define (node-grab-excuse)
  (node "顺一件工服"
    :subtitle "敏锐；工棚的架子上有工服和送货单。有了由头，办一件事不用掷骰"
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "架子上什么也没有"
        (lambda () (alarm+ 1) (check-alarm!)))
      (outcome "一件带油的工服"
        (lambda () (excuse-clk 'tick!)))
      (outcome "工服，还有一沓送货单"
        (lambda () (tick-n! excuse-clk 2))))))

;; ── 走廊尽头的灯：大开关，一次性 ────────────────

(define (lights-out!)
  (tick-n! alarm-clk 1)              ; 灭的瞬间总有人抬头，这一格躲不掉
  (set! lights-out? #t)
  (dark-clk 'set! dark-turns)
  (spotlight! "走廊黑了"
    "最后一盏灯芯灭下去，整条走廊连着堆场一起沉进黑里。有人在远处骂了一句，没有人过来。")
  (check-alarm!))

(define (lamp-check!)
  (if (and (not lights-out?) (not lamp-locked?) (lamp-clk 'full?))
      (lights-out!)
      #f))

(define (relight!)
  (set! lights-out? #f)
  (set! lamp-locked? #t)
  (tick-n! alarm-clk 2)
  (spotlight! "灯又亮了"
    "有人拎着新灯罩过来，一盏一盏点回去，点完就靠在柱子上不走了。这条走廊今晚到此为止。")
  (check-alarm!))

(define (lamp-modifiers)
  (if (old-man-watching?) (list (modifier -1 "老头在棚子口")) '()))

(define (node-douse)
  (node "摸上去拧掉气灯"
    :subtitle "见识；气灯的阀在灯柱背面。全灭之前不算数，灭了也只黑一阵子"
    :requires (list (req-die))
    :resolve (roll 'knowledge lamp-modifiers
      (outcome "你摸错了一个阀"
        (lambda () (alarm+ 1) (check-alarm!)))
      (outcome "灭了一盏"
        (lambda () (lamp-clk 'tick!) (lamp-check!)))
      (outcome "一连拧掉两盏"
        (lambda ()
          (tick-n! lamp-clk 2)
          (set! familiar-gain (+ familiar-gain 1))
          (lamp-check!))))))

(define (node-douse-excuse)
  (node "借着由头走到灯下"
    :subtitle (if (excuse-clk 'empty?)
                  "手上没有由头。这么站到灯下会被人问话"
                  "用掉一个由头。不掷骰，直接灭一盏")
    :disabled (excuse-clk 'empty?)
    :resolve (instant
      (outcome "换灯的人不会被多问"
        (lambda ()
          (excuse-clk 'advance! -1)
          (lamp-clk 'tick!)
          (lamp-check!))))))

(define (node-lamp-room)
  (container-with-clocks "走廊尽头的灯"
    (cond
      (lamp-locked?
       (list (observe-action "换过的灯罩"
               "新灯罩比原来的亮。换灯的人靠在柱子上抽烟，一直没走。")))
      (lights-out?
       (list (observe-action "黑着的走廊"
               "灯全灭了。趁这会儿，堆场那头没有一个人看得见你。")))
      (#t
       (list (node-douse) (node-douse-excuse))))
    (append
      (list (lamp-clk 'render-data))
      (if lights-out? (list (dark-clk 'render-data)) '()))))

;; ── 看堆场的棚子：小开关，可反复买 ──────────────

(define (watch-check!)
  (if (and (not watch-away?) (watch-clk 'full?))
      (begin
        (set! watch-away? #t)
        (watch-timer 'set! watch-turns)
        (result-note! "看堆场的老头提着灯到另一头去了"))
      #f))

(define (node-lure)
  (node "把老头引开"
    :subtitle "交际；把他支到堆场另一头，少一双眼睛"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他站起来往这边看"
        (lambda () (alarm+ 1) (check-alarm!)))
      (outcome "他往另一头走了几步"
        (lambda () (watch-clk 'tick!) (watch-check!)))
      (outcome "他跟着话走远了"
        (lambda ()
          (tick-n! watch-clk 2)
          (set! labor-gain (+ labor-gain 1))
          (watch-check!))))))

(define (node-watch-shed)
  (container-with-clocks "看堆场的棚子"
    (if watch-away?
        (list (observe-action "空着的棚子"
                "凳子上放着还没凉的杯子，收音机开着。他很快就会坐回来。"))
        (list (node-lure)))
    (append
      (list (watch-clk 'render-data))
      (if watch-away? (list (watch-timer 'render-data)) '()))))

;; ── 第一幕渲染 ──────────────────────────────────

(define (act1-nodes)
  (list
    (observe-action "堆场和那条缝"
      "堆到二层高的板条箱，中间只留一条走人的缝。走廊尽头有一排气灯，堆场口有个看夜的棚子。他那间在最里面，没有窗。")
    (node-sneak)
    (node-use-excuse)
    (node-grab-excuse)
    (node-lamp-room)
    (node-watch-shed)))

(define (act1-clocks)
  (list (arrival-clk 'render-data)
        (approach-clk 'render-data)
        (alarm-clk 'render-data)
        (excuse-clk 'render-data)))

;; 回合末依次：两个开关往回退，然后他又走近一段。
;; 灯是一次性的：亮回来就锁死，还要 +2 惊动——「趁现在」这三个字全在这里。
;; 老头只是回到棚子口，可以再引一次。他回家这一根谁也拦不住。
(define-turn-rule "开关在回位，而他在走回来"
  (lambda () (and (= act 1) (not finished?)))
  (lambda ()
    (if lights-out?
        (begin
          (dark-clk 'advance! -1)
          (if (dark-clk 'empty?) (relight!) #f))
        #f)
    (if (and watch-away? (not finished?))
        (begin
          (watch-timer 'advance! -1)
          (if (watch-timer 'empty?)
              (begin (set! watch-away? #f) (watch-clk 'reset!))
              #f))
        #f)
    (if (and (= act 1) (not finished?))
        (begin
          (arrival-clk 'advance! -1)
          (if (arrival-clk 'empty?) (begin-act2! #f) #f))
        #f)))

(define-rule "整排屋子都醒了"
  (lambda () (and (= act 1) (not finished?)))
  (lambda () (check-alarm!)))

;; ============================================================
;; 幕间
;; ============================================================

;; 第一幕交给第二幕两样东西：一路惊动了多少人，以及你是**等在那儿**还是**追出去的**。
;; 早到有余量（还剩两个回合以上）就能挑地方等，巷口少一格；没赶上多两格。
;; 第一幕惊动了多少人，决定这条街一开始有多少人在看你。
;; 惊动是 0/6、看着是 0/5，所以折半再算——不能照搬，否则潜入一失手第二幕就已经输了。
(define (crowd-start in-position?)
  (if act1-blown?
      (- crowd-max 1)
  (let ((base (+ (quotient (alarm-clk 'current) 2)
                 (if in-position? 0 2)
                 (if (and in-position? (>= (arrival-clk 'current) 2)) -1 0))))
    (max 0 (min base (- crowd-max 1))))))

(define (begin-act2! in-position?)
  (set! act 2)
  (crowd-clk 'set! (crowd-start in-position?))
  ;; 这个开场比「把底片交出来」重要：玩家第一句话就知道——**莱恩认为她早就知道**。
  (if in-position?
      (play-dialogue!
        (line "世界" "修理棚亮着一盏灯。他背对着门，手里在弄一台拆开的机器。")
        (line "世界" "周围几个老街人各干各的活，没人抬头。")
        (line "尼尔" "莱恩。")
        (line "世界" "他转过身，看了你一眼，就明白了。")
        (line "莱恩" "她终于告诉你我是谁了？")
        (line "尼尔" "信是你写的。")
        (line "莱恩" "你跑这么远，就为了问这个？"))
      (play-dialogue!
        (line "世界" "你从堆场那头绕过来的动静太大，棚子里的人早就都看过来了。")
        (line "尼尔" "莱恩。")
        (line "世界" "他把手里的东西放下，慢慢站直。他早看见你了。")
        (line "莱恩" "她终于告诉你我是谁了？")
        (line "尼尔" "信是你写的。")
        (line "莱恩" "你跑这么远，就为了问这个？")))
  (if (> (crowd-clk 'current) 0)
      (spotlight! "有人在看"
        (if in-position?
            "你一路摸进来惊动的那些人，这会儿在棚子外面各自忙着，眼睛却都朝这边。"
            "你弄出的动静把半条街叫起来了。棚子外面已经站了人，还在往这边看。"))
      #f))

;; ============================================================
;; 第二幕·小心翼翼地处理他
;; ============================================================

(define (crowd+ n) (tick-n! crowd-clk n))

;; ── 形态一 → 二：他不装了 ────────────────────────
(define (face+ n)
  (face-clk 'advance! n)
  (if (face-clk 'full?)
      (begin
        (set! form 2)
        (play-dialogue!
          (line "世界" "他停下来，看了你很久。那套「我无所谓」的架子自己塌了。")
          (line "莱恩" "行。是我。")
          (line "尼尔" "两封都是。")
          (line "莱恩" "两封都是。我要钱。你知道港口现在什么样吗？")
          (line "莱恩" "她住的那种地方，一个月的房钱够我过一年。"))
        (spotlight! "他不装了"
          "他不再演给你看，也不再演给这条街看。现在他说的是真的——而真话比谎话难听。"))
      #f))

;; ── 形态二：他说出那句改写案情的话 ──────────────
(define (words+ n)
  (let ((old (words-clk 'current)))
    (words-clk 'advance! n)
    (let ((new (words-clk 'current)))
      ;; 这一格是全章的转折，不能藏在满格里——多数人走不到满格。
      (if (and (< old 3) (>= new 3))
          (play-dialogue!
            (line "尼尔" "她为什么不直接告诉我是你。")
            (line "世界" "他愣了一下。然后笑了。")
            (line "莱恩" "她没告诉你？")
            (line "尼尔" "……")
            (line "莱恩" "第一封她就知道。")
            (line "莱恩" "「别再装作不认识那条河」——那是我们以前说的话。")
            (line "莱恩" "这条街上没有第二个人会这么写。她一眼就该认出来。"))
          #f)
      (if (words-clk 'full?)
          (begin
            (set! form 3)
            (play-dialogue!
              (line "莱恩" "那些照片不是你想的那种。")
              (line "莱恩" "她在酒馆唱歌。穿那条便宜裙子。跟一堆码头工人喝酒。跟我。")
              (line "莱恩" "上面还有她以前的名字。")
              (line "尼尔" "那你拿着它做什么。")
              (line "世界" "他很久没说话。")
              (line "莱恩" "证明她在这儿待过。"))
            (spotlight! "他说完了"
              "他手上已经没有别的话了。剩下的是你怎么结束这笔旧账。"))
          #f))))

;; ── 形态一的三张卡 ──────────────────────────────
(define (node-letters)
  (node "摊开那两封信"
    :subtitle "见识；纸就在你口袋里，他认得自己的字"
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "他说不是他写的"
        (lambda () (spend-composure! 1)))
      (outcome "他不看那张纸"
        (lambda () (face+ 1)))
      (outcome "他念出了自己写的话"
        (lambda () (face+ 2))))))

(define (node-cigs)
  (node "把烟盒扔到桌上"
    :subtitle (if cigs-used?
                  "烟盒已经在桌上了"
                  "不掷骰；他知道这半包是从哪儿捡回来的")
    :disabled cigs-used?
    :requires (list (req-item "半包「老金牌」" 1))
    :resolve (instant
      (outcome "他不说话了"
        (lambda ()
          (set! cigs-used? #t)
          (play-banter!
            (line "世界" "软了的烟盒落在工作台上，滚了半圈。")
            (line "莱恩" "……那小子把这个也给你了。"))
          (face+ 3))))))

(define (node-her-now)
  (node "提她现在的样子"
    :subtitle "交际；最快的一手。他会喊，整条街都听得见"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他冲你吼回来"
        (lambda () (spend-composure! 1) (crowd+ 2)))
      (outcome "他脸上挂不住"
        (lambda () (face+ 2) (crowd+ 1)))
      (outcome "他自己把话接了下去"
        (lambda () (face+ 3) (crowd+ 1))))))

;; ── 形态二的四张卡 ──────────────────────────────
(define (node-listen)
  (node "听他说完"
    :subtitle "交际；不打断。慢，可这条街当没听见"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他自己绕回去了"
        (lambda () (spend-composure! 1)))
      (outcome "他往下说了一段"
        (lambda () (words+ 1)))
      (outcome "他说了没打算说的"
        (lambda () (words+ 2))))))

(define (node-that-line)
  (node "追问那句话"
    :subtitle "见识；信上那句你当初以为只是恐吓的话"
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "他反问你信在哪儿"
        (lambda () (spend-composure! 1)))
      (outcome "他解释了半句"
        (lambda () (words+ 2)))
      (outcome "他把来龙去脉说了"
        (lambda () (words+ 3))))))

(define (node-strip)
  (node "拆穿他的委屈"
    :subtitle "交际；他不是受害者，说破它。快，也会招人"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他梗着脖子不认"
        (lambda () (spend-composure! 1) (crowd+ 1)))
      (outcome "他没话说了"
        (lambda () (words+ 2) (crowd+ 1)))
      (outcome "他自己认了"
        (lambda () (words+ 3) (crowd+ 1))))))

(define (node-workbench)
  (node "逼近工作台"
    :subtitle "力量；他一直用身子挡着那边。整条巷子都会看见"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "他把你推开"
        (lambda () (spend-composure! 1) (crowd+ 2)))
      (outcome "他退了半步"
        (lambda () (words+ 2) (crowd+ 2)))
      (outcome "他不敢再挡"
        (lambda () (words+ 3) (crowd+ 2))))))

;; ── 形态三：三张一次性的卡，选一张就结 ──────────
;; 三条都算他屈服，区别是你用什么方式结的——以及他会不会记着这件事。
(define (node-settle-talk)
  (node "就到这儿吧"
    :subtitle (if (<= (crowd-clk 'current) 2)
                  "交际；不再逼他。让他自己收手"
                  "这条街已经站起来了，谈不成了")
    :disabled (> (crowd-clk 'current) 2)
    :resolve (instant
      (outcome "他把底片扔过来"
        (lambda ()
          (play-dialogue!
            (line "尼尔" "你已经拿到你要的了。她不会回来。")
            (line "尼尔" "再写下去，下一个来的就不是我。")
            (line "世界" "他坐了很久，然后从炉子后面掏出一个铁盒，扔在你脚边。")
            (line "莱恩" "拿走。")
            (line "莱恩" "告诉她，我不找她了。"))
          (finish! '谈))))))

(define (node-settle-force)
  (node "按住他搜"
    :subtitle "力量；东西一定到手。整条街都会记住今晚"
    :tags (list "高风险")
    :resolve (instant
      (outcome "东西到手"
        (lambda ()
          (crowd+ 2)
          (play-dialogue!
            (line "世界" "你把他按在工作台上，从炉子后面摸出那个铁盒。")
            (line "莱恩" "东西拿走。")
            (line "莱恩" "但这事没完。"))
          (finish! '逼))))))

(define (node-settle-trade)
  (node "替他带句话"
    :subtitle "交际；他要的不是钱了。你成了两个人之间最后一次传话"
    :resolve (instant
      (outcome "他把铁盒推过来"
        (lambda ()
          (play-dialogue!
            (line "莱恩" "东西给你。")
            (line "莱恩" "但你得让她自己说。")
            (line "尼尔" "说什么。")
            (line "莱恩" "说我们结束了。她当年走的时候一个字都没留。")
            (line "世界" "他把铁盒推到工作台这一头，手一直没松开，直到你伸手去拿。"))
          (finish! '交换))))))

;; ── 收场 ────────────────────────────────────────
;; 老街站起来把你请出去，也算这笔旧账结了：东西你照样带走了，
;; 只是整条街看着你带走的。区别落在城市那一侧，不在这里。
(define (finish! route)
  (if finished?
      #f
      (begin
        (set! finished? #t)
        (if (equal? route '难看)
            (play-dialogue!
              (line "世界" "巷口的人围上来，一步一步。没有人喊。")
              (line "搬运工" "够了。有什么事出去解决。")
              (line "尼尔" "他敲诈一个姑娘。")
              (line "搬运工" "那也是我们的事。")
              (line "世界" "莱恩自己把铁盒扔了过来——他不想让这条街看下去。")
              (line "莱恩" "滚吧。"))
            #f)
        (end-encounter (list route familiar-gain labor-gain)))))

(define-rule "围上来了"
  (lambda () (and (= act 2) (not finished?)))
  (lambda () (if (crowd-clk 'full?) (finish! '难看) #f)))

(define (lyon-look)
  (cond
    ((= form 1) "三十多岁，酒气很重，背抵着墙。他在等你先开口——他觉得今晚是他赢了。")
    ((= form 2) "架子塌了以后，他看起来比刚才瘦。手上有码头留下的老茧，现在没处使。")
    (#t "他不再看你，也不再看巷口。他只是坐在那儿，等这件事过去。")))

(define (act2-nodes)
  (append
    (list (observe-action "你面前这个人" (lyon-look)))
    (cond
      ((= form 1) (list (node-letters) (node-cigs) (node-her-now)))
      ((= form 2) (list (node-listen) (node-that-line) (node-strip) (node-workbench)))
      (#t (list (node-settle-talk) (node-settle-force) (node-settle-trade))))))

(define (act2-clocks)
  (cond
    ((= form 1) (list (face-clk 'render-data) (crowd-clk 'render-data)))
    ((= form 2) (list (words-clk 'render-data) (crowd-clk 'render-data)))
    (#t (list (crowd-clk 'render-data)))))

;; ============================================================
;; 渲染
;; ============================================================

(define (get-render-data)
  (if (= act 1)
      (container "货栈后面"
        (append (apply clock-nodes (act1-clocks))
          (act1-nodes)))
      (container "修理棚"
        (append (apply clock-nodes (act2-clocks))
          (act2-nodes)))))

;; ── 开局 ────────────────────────────────────────
;; 他从第一个回合就在往回走。这根钟不归任何动作管。
(arrival-clk 'set! arrival-turns)

;; ── 开局态势由钥匙决定 ──────────────────────────
;; 钥匙不给专属按钮，只省掉你本来要花在开场上的那两颗骰。
;; 弗兰克认可：看堆场的人被提前调开；不信任：消息先一步传进堆场。
(if (equal? frank-state "认可")
    (begin (watch-clk 'set! (watch-clk 'max))
           (set! watch-away? #t)
           (watch-timer 'set! watch-turns))
    #f)
(if (equal? frank-state "不信任") (tick-n! alarm-clk 2) #f)
;; 酒馆老板：这一片的门道他都讲过，气灯的阀你不用现找。
(if key-owner (tick-n! lamp-clk 2) #f)
(if key-joe (excuse-clk 'tick!) #f)
(if key-otto (approach-clk 'advance! 2) #f)
(if key-lottie (approach-clk 'advance! 2) #f)
