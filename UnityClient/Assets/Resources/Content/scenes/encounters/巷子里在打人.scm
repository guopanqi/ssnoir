;; scenes/encounters/巷子里在打人.scm - 第一章·艾迪线的开场
;;
;; 这是玩家前期第一场多目标 combat：三个打手各有一条自己的生命进度和一条出手倒计时。
;; 每回合末，还站着的人都会把自己的倒计时走一格；归零就出手，然后重新拉满。
;;
;; **催你的是艾迪，不是伤**。这一场有两股压力，它们不该做同一件事：
;;   艾迪那根倒计时 —— 负责"快"。它在画面上，看得见，走到底这一场就输了。
;;   三个人的拳头   —— 负责"疼"。它决定你敢不敢挨，而不是决定你有几个回合。
;; 曾经两股都在催：三人合计每回合能打掉四点冷静，满冷静撑不过两回合就开始进伤势，
;; 而伤势是那条不会自己好的轴。于是这一场读起来不是"救不救得下来"，是"值不值得受这个伤"。
;;
;; 三人现在**各打各的**，不再是同一记拳头换三个名字：
;;   毡帽   快而轻（2 回合一拳，1 点）——他是节奏，不是威胁。放着不管会一直啄你。
;;   卷袖子 快而轻，血最薄——最便宜的那个减压来源：三格血换掉一个持续出拳的人。
;;   皮夹克 慢而重（3 回合一拳，3 点）——全场唯一一记真的疼的拳。要么在他归零前放倒他，
;;          要么认下这一下。这就是这一场的主要决策。
;; 总生命十二格，三四颗骰不可能一轮清场；打倒一个人是**看得见的减压**，这是先打谁的理由。
;; 不在教学位置叠弱点、阶段或额外可操作钟。
;;
;; 对外契约：回传 'success / 'fail。见 world/艾迪.scm 的 on-alley-result。

(define coat-life-max 6)
(define hat-life-max 3)
(define sleeves-life-max 3)

;; 艾迪还能撑多久。它必须在画面上：这一场真正的时限是他，不是你的冷静。
;; 原先它是个藏在脚本里的 round-count，玩家只能在输掉的那一刻才知道有这个东西。
(define eddie-rounds 5)
(define eddie-clk
  (make-clock "艾迪还撑得住" eddie-rounds 'countdown
    "每回合末走一格。归零时你还没清场，他就撑不住了。"))

;; 每个人两条钟：一条生命值，一条出手。
;;
;; 生命值是 gauge：一格一格的量，起手 6/6，挨一拳掉一格，归零他就站不住。
;; gauge 不含方向——同一种样式既画调查进度那种往上填的，也画这里往下掉的血。
;; 全游戏所有生命值都长这一个样子，玩家认一次就够了。
;;
;; 出手是 countdown，因为它真的是时间：你碰不到它，它每回合自己走一格，
;; 归零那一下你挨打——「2」就是「他还有两回合出手」。
(define coat-life
  (make-clock "生命" coat-life-max 'gauge "他的生命值。个子最大，撑得最久。"))
(define coat-attack
  (make-clock "皮夹克·出手" 3 'countdown "归零就出手：这一下最重。"))

(define hat-life
  (make-clock "生命" hat-life-max 'gauge "他的生命值。归零他就站不住。"))
(define hat-attack
  (make-clock "毡帽·出手" 2 'countdown "归零就出手：快，但不重。"))

(define sleeves-life
  (make-clock "生命" sleeves-life-max 'gauge "他的生命值。最不经打的一个。"))
(define sleeves-attack
  (make-clock "卷袖子·出手" 3 'countdown "归零就出手：快，但不重。"))

(define finished? #f)
(define round-count 0)

(define (finish! result title text)
  (if finished? (error "巷子里在打人：交锋已经结算") #t)
  (set! finished? #t)
  (spotlight! title text)
  (end-encounter result))

;; 倒下不是"你没插手"：你插了手，然后和艾迪躺在同一条巷子里。
;; 回传自己的收场标签，让艾迪那边讲对第二天他看见的是什么（见 on-alley-result）。
(define (on-encounter-collapse)
  (collapse-result '倒下))

(define (all-down?)
  (and (coat-life 'empty?) (hat-life 'empty?) (sleeves-life 'empty?)))

(define (check-victory!)
  (if (and (not finished?) (all-down?))
      (finish! 'success "三个人都倒了"
        "最后一个人撑着墙退出巷子。靠墙那个抹掉嘴角的血，自己站了起来。")
      #f))

(define (hit-enemy! life amount down-line)
  (if (life 'empty?)
      (error "巷子里在打人：试图攻击已经退场的人")
      #t)
  (life 'advance! (- amount))
  (if (life 'empty?)
      (play-banter! (line "世界" down-line))
      #f)
  (check-victory!))

(define (enemy-node title subtitle life attack down-line)
  ;; 一人一锚：卡钉在场上对应人形；生命/出手钟挂在这张人卡上，不另漂。
  (node title
    :anchor title
    :subtitle subtitle
    :clocks (list (life 'render-data) (attack 'render-data))
    :requires (list (req-die))
    :resolve (roll 'violence
      ;; 打空了不是白打一回：他顺势还你一下。扣的是冷静，不是拨快他的出手钟——
      ;; 代价当场结清，不改变"他还有几回合出手"这个玩家正在算的数。
      (outcome (lambda ()
          (spend-actor-composure! 'player 1)))
      (outcome (lambda () (hit-enemy! life 1 down-line)))
      (outcome (lambda () (hit-enemy! life 2 down-line))))))

;; 卡就是这个人。标题是他的名字，不是"对付他"——对方回合里说话、出拳、掉血的都是这张卡。
;; 三个人的生命钟都叫"生命"：钟的身份靠它挂在谁的卡上，不靠标签里再写一遍名字。
(define (node-coat)
  (enemy-node "皮夹克" "个子最大，挨打后仍能站住"
    coat-life coat-attack "皮夹克撞上砖墙，顺着墙根滑了下去。"))

(define (node-hat)
  (enemy-node "毡帽" "出手最快，已经向你走过来"
    hat-life hat-attack "毡帽掉在水沟里。他贴着墙，没有再起来。"))

(define (node-sleeves)
  (enemy-node "卷袖子" "脚步快，但他不经打"
    sleeves-life sleeves-attack "卷袖子捂住鼻子，跌跌撞撞地退到了巷口。"))

(define (standing? life) (not (life 'empty?)))

;; 还站着的人把出手倒计时走一格。
(define (approach! life attack)
  (if (and (standing? life) (not (attack 'empty?)))
      (attack 'advance! -1)
      #f))

;; 一个人出拳，自成一条规则、自成一拍：他开口，拳头落下，你的冷静跟着掉，他的钟重新拉满。
;; 谁在动不用写——他的出手钟变了、他说了话，客户端就知道亮哪张卡。
(define (define-strike! name life attack damage say attack-line)
  (define-opponent-rule (string-append name "出手")
    (lambda () (and (not finished?) (standing? life) (attack 'empty?)))
    (lambda ()
      (play-banter! (line name say))
      ;; 出手之后重新拉满：下一次又要等这么久。
      (attack 'set! (attack 'max))
      (spend-actor-composure! 'player damage)
      (play-banter! (line "世界" attack-line)))))

(define (on-encounter-enter)
  (set! finished? #f)
  (set! round-count 0)
  ;; 交锋初始盘面不是玩家造成的 Clock 变化，不写入场报告的效果条（同 失控的机械）。
  (eddie-clk 'load! eddie-rounds)
  ;; 起手蓄力错开，头两回合各只挨一记轻的；皮夹克那一下落在第三回合末——
  ;; 到那时你已经打了三轮，该决定的事早就该决定了。
  (coat-life 'load! coat-life-max)
  (coat-attack 'load! 3)      ; 满蓄力起步：第三回合末才落下
  (hat-life 'load! hat-life-max)
  (hat-attack 'load! 1)       ; 下一回合末就啄你一下
  (sleeves-life 'load! sleeves-life-max)
  (sleeves-attack 'load! 2)
  (play-dialogue!
    (line "世界" "酒馆侧墙那条巷子。三个人围着一个，靠墙那个已经不还手了。")
    (line "打人的" "不是说好第四回合吗。")
    (line "打人的" "第四回合。你他妈数得清吗。")
    (line "世界" "你走进去。戴毡帽的先转过身，另外两个才把目光从地上移开。")))

;; ── 对方回合 ─────────────────────────────────────
;; 规则按书写顺序一条一拍：先是三个人一起逼近（钟各走一格），然后倒计时到零的人依次出手
;; （快的先动，最重的那一下压轴），最后是艾迪那边又撑过去一回合。
;; 谁先谁后在这里就是因果：毡帽那一拳把你打倒了，后面的人就不再出手。

(define-opponent-rule "他们在逼近"
  (lambda () (not finished?))
  (lambda ()
    (approach! hat-life hat-attack)
    (approach! sleeves-life sleeves-attack)
    (approach! coat-life coat-attack)))

(define-strike! "毡帽" hat-life hat-attack 1
  "看哪儿呢。"
  "毡帽从侧面撞进来，肘子顶在你的肋下。")

(define-strike! "卷袖子" sleeves-life sleeves-attack 1
  "转过来。"
  "卷袖子趁你回头，一拳砸在你的耳根。")

(define-strike! "皮夹克" coat-life coat-attack 3
  "站好了。"
  "皮夹克迎面一拳，你的半边身子都麻了。")

(define-opponent-rule "艾迪撑不住了"
  (lambda () (not finished?))
  (lambda ()
    (set! round-count (+ round-count 1))
    ;; 倒计时要往下走。这里原来写的是 'tick!（+1）——钟起手就在满格，加一被夹回满格，
    ;; 于是它永远 empty? 不了，「艾迪撑不住了」这条路一次也没跑过。
    (eddie-clk 'advance! -1)
    (play-banter! (line "世界" "靠墙的人又矮下去一点。"))
    (if (and (not finished?) (eddie-clk 'empty?))
        (finish! 'fail "艾迪撑不住了"
          "你还在和面前的人纠缠。后面传来一声闷响，艾迪顺着墙倒下去，没有再动。")
        #f)))

;; 根名 = Camera_巷子里在打人 / Anchor_巷子里在打人，驱动镜头切入。
;; 三人各钉自己的人形锚；艾迪倒计时钉在靠墙的艾迪身上（clock-nodes 名前缀「钟：」对不上锚，须 at-anchor）。
(define (get-render-data)
  (node "巷子里在打人"
    :anchor "巷子里在打人"
    :children
      (append
        (map (lambda (n) (at-anchor "艾迪还撑得住" n))
             (clock-nodes (eddie-clk 'render-data)))
        (if (coat-life 'empty?) '() (list (node-coat)))
        (if (hat-life 'empty?) '() (list (node-hat)))
        (if (sleeves-life 'empty?) '() (list (node-sleeves))))))
