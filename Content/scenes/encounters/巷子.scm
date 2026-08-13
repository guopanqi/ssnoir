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
;; 第二幕·施压 —— CS2 的合同语法，不再有窗口和倒计时：
;;   目标钟（他撒手）+ 危险钟（巷口的人），每一手都同时推两边，
;;   「就到这儿」全程可用。唯一的问题是「现在够不够」。
;;
;; 对外契约：回传 (list 收获 越界? 额外? 熟脸增 劳工增)
;;   收获 ：他撒手钟的格数 0..6
;;   越界?：#t 表示巷口的人满格，你是被架出去的
;;   额外?：#t 表示搜身翻出了他没打算给任何人看的东西
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
(define key-eddie  (key? '钥匙-埃迪))
(define key-lottie (key? '钥匙-洛蒂))
(define key-owner  (key? '钥匙-酒馆老板))

(define alarm-max 6)
(define approach-max 10)
(define crowd-max 6)
(define yield-max 6)
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
  (make-clock "巷口的人" crowd-max 'segments
    "动静越大，站出来的人越多。满格就是越界——他们把你架出去，你什么也带不走。"))

(define yield-clk
  (make-clock "他撒手" yield-max 'segments
    "他肯给出来多少。随时可以带着现在这些走人——这一格数就是你今晚的收获。"))

(define act 1)
(define familiar-gain 0)
(define labor-gain 0)
(define searched? #f)
(define extra? #f)
(define finished? #f)
(define lights-out? #f)     ; 走廊现在黑着
(define lamp-locked? #f)    ; 灯已经被换过，这条路锁死
(define watch-away? #f)     ; 老头现在不在棚子口

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

(define (fail-act1!)
  (play-dialogue!
    (line "世界" "一扇门开了，接着是第二扇。有人举着灯站到走廊上，谁也没说话。")
    (line "世界" "你退回堆场的阴影里。今晚这片地方已经醒了。"))
  (set! finished? #t)
  (end-encounter (list 0 #t #f familiar-gain labor-gain)))

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
      (outcome "一连拧掉两盏，顺手记住了这一片的走法"
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
    :subtitle "交际；跟他搭话，把他支到堆场另一头去。他走了，往里摸就少一双眼睛"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他站起来往这边看"
        (lambda () (alarm+ 1) (check-alarm!)))
      (outcome "他往另一头走了几步"
        (lambda () (watch-clk 'tick!) (watch-check!)))
      (outcome "他一路跟着你的话走远了"
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
(define (crowd-start in-position?)
  (let ((base (+ (alarm-clk 'current)
                 (if in-position? 0 2)
                 (if (and in-position? (>= (arrival-clk 'current) 2)) -1 0))))
    (max 0 (min base (- crowd-max 1)))))

(define (begin-act2! in-position?)
  (set! act 2)
  (crowd-clk 'set! (crowd-start in-position?))
  (if in-position?
      (play-dialogue!
        (line "世界" "你贴着那扇没有窗的门站定，脚步声才从巷子那头过来。")
        (line "世界" "他一只手扶着墙，走到门口才看见你。")
        (line "尼尔" "莱恩。")
        (line "莱恩" "你他妈是谁——")
        (line "世界" "他认出你不是这条街上的人，转身就要走。巷子那头是墙。"))
      (play-dialogue!
        (line "世界" "脚步声先你一步到了门口。你还在堆场那头，只能从后面追出去。")
        (line "世界" "板条箱被你带倒了一摞。整条巷子都听见了。")
        (line "尼尔" "莱恩。")
        (line "莱恩" "你他妈是谁——")
        (line "世界" "他回头，看见的是一个跑着追过来的外地人。巷子那头是墙。")))
  (if (> (crowd-clk 'current) 0)
      (spotlight! "巷口"
        (if in-position?
            "你一路摸进来惊动的那些人，这会儿正站在巷口那头。没有人过来，也没有人走开。"
            "你追出来的动静把半条街叫醒了。巷口那头已经站了人，还在往这边聚。"))
      #f))

;; ============================================================
;; 第二幕·施压
;; ============================================================

(define (crowd+ n) (tick-n! crowd-clk n))
(define (yield+ n)
  (let ((old (yield-clk 'current)))
    (yield-clk 'advance! n)
    (let ((new (yield-clk 'current)))
      (if (and (< old 2) (>= new 2))
          (play-banter! (line "莱恩" "行——行。多少钱能了？你说个数，我这几天就能凑。"))
          #f)
      (if (and (< old 4) (>= new 4))
          (play-dialogue!
            (line "莱恩" "东西在我这儿。都在。")
            (line "尼尔" "拿出来。")
            (line "莱恩" "我又不是要她的命。我就是……要点钱。你知道港口现在什么样吗？")
            (line "莱恩" "拿去。跟她说，我不写了。"))
          #f)
      (if (and (< old 6) (>= new 6))
          (play-dialogue!
            (line "莱恩" "还有底片。在炉子后面。")
            (line "世界" "他自己爬过去掏出来，塞给你，然后一直坐在那儿没起来。"))
          #f))))

(define (node-talk)
  (node "摆事实"
    :subtitle "交际；慢，但整条巷子当没听见"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他打断你"
        (lambda () (spend-composure! 1) (crowd+ 1)))
      (outcome "他听进去了一句"
        (lambda () (yield+ 1)))
      (outcome "他自己接着说下去"
        (lambda () (yield+ 2))))))

(define (node-leverage)
  (node "拿住他的软处"
    :subtitle "见识；你刚从他住的地方走过来，什么都看见了。说出来不用抬手"
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "他装作没听懂"
        (lambda () (spend-composure! 1)))
      (outcome "他不吭声了"
        (lambda () (yield+ 1) (crowd+ 1)))
      (outcome "他知道你什么都清楚"
        (lambda () (yield+ 2) (crowd+ 1))))))

(define (node-press)
  (node "把他按到墙上"
    :subtitle "力量；最快的一手，巷口的人会记住你动了手"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "他滑了出去"
        (lambda () (crowd+ 2)))
      (outcome "他不动了"
        (lambda () (yield+ 2) (crowd+ 1)))
      (outcome "他开始求你"
        (lambda () (yield+ 3) (crowd+ 2))))))

(define (node-search)
  (node "搜他身上"
    :subtitle (if searched?
                  "他身上已经没有别的了"
                  "敏锐；不问他要，自己拿。最有收成，也最难看")
    :tags (list "高风险")
    :disabled searched?
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "只有一把钥匙"
        (lambda () (set! searched? #t) (crowd+ 2)))
      (outcome "翻出几张纸"
        (lambda () (set! searched? #t) (set! extra? #t) (yield+ 1) (crowd+ 2)))
      (outcome "翻出他没打算给任何人看的东西"
        (lambda ()
          (set! searched? #t)
          (set! extra? #t)
          (result-note! "拿到：一沓他自己写过、没有寄出的信")
          (yield+ 2)
          (crowd+ 2))))))

(define (node-leave)
  (node "就到这儿"
    :subtitle (cond
                ((>= (yield-clk 'current) 6) "他把底片也交了。没有别的可拿了")
                ((>= (yield-clk 'current) 4) "照片和信在你手里。这是体面收场的时候")
                ((>= (yield-clk 'current) 2) "他只答应了不再写。东西还在他那儿")
                (#t "他什么也没给。现在走，这件事就没完"))
    :resolve (instant
      (outcome "你松开手"
        (lambda () (finish! #f))))))

(define (finish! forced?)
  (if finished?
      #f
      (begin
        (set! finished? #t)
        (if forced?
            (begin
              (play-dialogue!
                (line "世界" "巷口站满了人。没有人喊，也没有人动手——他们只是围上来，一步一步。")
                (line "搬运工" "够了。他是我们的人。")
                (line "尼尔" "他敲诈一个姑娘。")
                (line "搬运工" "那也是我们的事。")
                (line "世界" "两只手扣住你的胳膊，把你架出了巷子。"))
              (injure!))
            #f)
        (end-encounter
          (list (if forced? 0 (yield-clk 'current))
                forced?
                (and extra? (not forced?))
                familiar-gain
                labor-gain)))))

(define-rule "围上来了"
  (lambda () (and (= act 2) (not finished?)))
  (lambda () (if (crowd-clk 'full?) (finish! #t) #f)))

(define (act2-nodes)
  (list
    (observe-action "你面前这个人"
      "三十多岁，酒气很重，背抵着墙。手上有码头留下的老茧，现在没处使。他比你想象的瘦，也比你想象的怕。")
    (node-talk)
    (node-leverage)
    (node-press)
    (node-search)
    (node-leave)))

;; ============================================================
;; 渲染
;; ============================================================

(define (get-render-data)
  (if (= act 1)
      (container "货栈后面"
        (append (apply clock-nodes (act1-clocks))
          (act1-nodes)))
      (container "巷子里"
        (append (clock-nodes (yield-clk 'render-data) (crowd-clk 'render-data))
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
(if key-eddie (approach-clk 'advance! 2) #f)
(if key-lottie (approach-clk 'advance! 2) #f)
