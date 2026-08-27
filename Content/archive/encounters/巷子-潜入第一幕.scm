;; 归档：《巷子》原来的第一幕·环境潜入。
;;
;; 它被弗兰克的对峙替换掉了（见 encounters/巷子.scm）。留在这里是因为这套骨架
;; 本身是完整的、可以照抄的**环境潜入**写法，而不是一堆贴在玩家身上的加成按钮：
;;
;;   一根失败钟（惊动的人）、一根目标钟（摸到他门口），主动作「往里摸」两边都推；
;;   除了主场地，还有两个**去处**，各自握着一个改写全局规则的开关：
;;     走廊尽头的灯 —— 大开关。灭掉它，往里摸完全不惊动人，两个 −1 一起消失；
;;                     代价前置（灭的瞬间 +1）、限时（两回合）、且只有一次：
;;                     灯一亮回来 +2 惊动，这条路从此锁死。
;;     看堆场的棚子 —— 小开关。把老头引开只清掉他那一个 −1，但可以反复买。
;;   「由头」是不掷骰的推进出口，差骰子有地方去。
;;   压着这一切的是「他快回来了」：每回合末走一格，跟你做什么无关。
;;   题眼：两个开关都很值，但每个开关都要花你一个回合，而他不等你布置完。
;;
;; 本文件不加载、不参与 --validate，也不保证能原样跑起来。见 archive/README.md。

;; ── 第一幕 ──────────────────────────────────────
(define approach-clk
  (make-clock "摸到他门口" approach-max 'gauge
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
  (make-clock "走廊的气灯" 3 'gauge
    (lambda (current max)
      (cond
        (lamp-locked? "有人换上了新灯罩守在灯下。这条走廊今晚再也灭不了。")
        (lights-out? "灯全灭着。趁现在往里摸，不惊动任何人。")
        (#t "一盏一盏拧掉。全灭：这一片黑下来，往里摸不再惊动任何人。")))))

(define dark-clk
  (make-clock "这一片还黑着" dark-turns 'countdown
    "灯灭着还剩几个回合。归零：有人换上新灯罩站在灯下，惊动 +2，这条走廊今晚再也走不通。"))

(define watch-clk
  (make-clock "把老头引开" 2 'gauge
    (lambda (current max)
      (if watch-away?
          "他已经被支到堆场另一头去了。灯还是亮的——他只是一双眼睛。"
          "看堆场的老头坐在棚子口。满格他会离开一阵子，他回来了还能再引一次。"))))

(define watch-timer
  (make-clock "他就要回来了" watch-turns 'countdown
    "老头还离得开多久。归零他坐回棚子口，可以再引一次。"))

(define excuse-clk
  (make-clock "手上的由头" 2 'gauge
    "一件工服、一张送货单。用掉一个就能大方地办一件事，不必掷骰。"))


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

