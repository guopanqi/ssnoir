;; scenes/encounters/夜莺·抢人.scm - 夜莺委托线·第二场
;;
;; 两幕结构：
;;   第一幕固定在公寓。搜查逼近时，玩家在离开、处理搜查者、布置假人和
;;   收拾箱子之间分配骰子；只有假人和箱中物会结转到第二幕。
;;   第二幕逐回合换景。送酒车依次穿过鱼市、电车路口和警察封锁线；
;;   每段只有一个回合、两个对象，未填满的局部进度随街景永久消失。
;;
;; 夜莺不是玩家菜单里的能力。每个回合开始时，她会随机推进一个仍然有效的
;; 场景对象一格，玩家看见她已经做了什么，再决定四颗骰子如何分配。
;;
;; 对外契约：只回传 'success / 'fail；不修改外部故事阶段。
;; 城市输入（只在顶部读取）：
;;   资产      - "中" 时场景发生在玩家自己的公寓，离开路线开局 +1。
;;   官僚关系  - 达到「信任」时，警察封锁线直接放行并拦住追车。

(define own-apartment?
  (equal? (let ((v (get-global '资产))) (if v v "低")) "中"))
(define police-help?
  (relation-at-least? "官僚" '信任))

;; ============================================================
;; 通用状态与助手
;; ============================================================

(define act 1)
(define finished? #f)
(define nightingale-note "")

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

(define (clock-sub! clock n)
  (clock 'set! (max 0 (- (clock 'current) n))))

;; 搜查压力的每一个入口都在加格后立即结算。不能只依赖动作后 rule：
;; 这样坏结果填满时，交锋在当前动作里就结束，不会留下满格但仍可行动的状态。
(define (advance-search! n)
  (clock-tick-n! search-clk n)
  (if (search-clk 'full?)
      (finish-fail!)
      #f))

(define (node-nightingale)
  (container "夜莺"
    (list (observe-action "她刚刚做了什么" nightingale-note))))

;; ============================================================
;; 第一幕：公寓
;; ============================================================

(define search-clk
  (make-clock "搜查逼近" 6 'segments
              "「有人正在搜查」每回合 +1；具体的「搜查者」出现后，未被引开时再 +1。填满 = 他们撞开房门，夜莺受伤，本场失败。"))
(define exit-clk
  (make-clock "从后窗离开" 9 'segments
              "填满 = 立即从后窗下楼，坐进酒馆的送酒车，进入追车。"))
(define searcher-clk
  (make-clock "搜查者" 2 'segments
              "第一回合结束后出现。填满 = 他循着假线索去了楼上，本幕每回合不再额外增加 1 格搜查逼近。不会影响第二幕。"))
(define decoy-clk
  (make-clock "床边的两个假人" 2 'segments
              "可选。填满 = 追兵晚一拍认出被骗；第二幕追兵以 3/6 而不是 4/6 开始。"))
(define luggage-clk
  (make-clock "箱子里的两个位置" 2 'segments
              "可选。每格选择装贵重物品或路障材料；前者成功逃脱后换成 10 金，后者让第二幕的低骰确定推进一个物理对象。"))

(define valuables-packed 0)
(define roadblocks-packed 0)
(define roadblocks-used 0)
(define apartment-turns 0)
(define searcher-present? #f)

(search-clk 'set! 1)
(if own-apartment?
    (exit-clk 'tick!)
    #f)

(define (act1-nightingale-options)
  (append
    (if (not (exit-clk 'full?)) (list 'exit) '())
    (if (and searcher-present? (not (searcher-clk 'full?))) (list 'searcher) '())
    (if (not (decoy-clk 'full?)) (list 'decoy) '())))

(define (nightingale-act-in-apartment!)
  (let ((target (random-choice (act1-nightingale-options))))
    (cond
      ((equal? target 'exit)
       (exit-clk 'tick!)
       (set! nightingale-note "她踢掉鞋，已经把后窗下面的消防梯放低了一截。离开公寓 +1。")
       (play-dialogue!
         (line "夜莺" "消防梯还撑得住。重的东西先递给我。")))
      ((equal? target 'searcher)
       (searcher-clk 'tick!)
       (set! nightingale-note "她把唱机推到门边，让那个人听错了房间。搜查者 +1。")
       (play-dialogue!
         (line "夜莺" "让他以为声音在楼上。别让他看见门缝。")))
      ((equal? target 'decoy)
       (decoy-clk 'tick!)
       (set! nightingale-note "她把自己的外套披到枕头上，床边已经有了一个坐着的人影。假人 +1。")
       (play-dialogue!
         (line "夜莺" "灯别关。隔着窗帘，他们会以为这里还有人。")))
      (else (error "抢人第一幕：夜莺收到未知目标")))))

(define (searcher-arrive!)
  (set! searcher-present? #t)
  (play-dialogue!
    (line "世界" "楼道里的脚步停在这一层。有人挨着门牌，一间一间往这里查。")
    (line "夜莺" "这个人刚才已经经过一次。")
    (line "尼尔" "那就让他下一次经过时，往楼上走。")))

(define (node-background-search)
  (node "有人正在搜查"
    :subtitle "无法处理；每次结束回合，搜查逼近 +1"
    :tags (list "持续压力")
    :disabled #t))

(define (node-handle-searcher)
  (node "把他引去楼上"
    :subtitle "社交；坏：搜查逼近 +1，中：搜查者 +1，好：搜查者 +2"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他停在门外"
        (lambda () (advance-search! 1)))
      (outcome "声音去了楼上"
        (lambda () (searcher-clk 'tick!)))
      (outcome "线索全指向楼上"
        (lambda () (clock-tick-n! searcher-clk 2))))))

(define (node-leave-apartment)
  (node "清出后窗的路"
    :subtitle "敏锐；坏：搜查逼近 +1，中：离开 +1，好：离开 +2"
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "玻璃碰了窗框"
        (lambda () (advance-search! 1)))
      (outcome "挪开窗边家具"
        (lambda () (exit-clk 'tick!)))
      (outcome "消防梯落下去了"
        (lambda () (clock-tick-n! exit-clk 2))))))

(define (node-build-decoys)
  (node "把床铺成两个人影"
    :subtitle "见识；坏：搜查逼近 +1，中：假人 +1，好：假人 +2"
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "影子歪得不像人"
        (lambda () (advance-search! 1)))
      (outcome "垫出一副肩膀"
        (lambda () (decoy-clk 'tick!)))
      (outcome "两个人还留在屋里"
        (lambda () (clock-tick-n! decoy-clk 2))))))

(define (luggage-full?) (luggage-clk 'full?))

(define (node-pack-valuables)
  (node "装进贵重物品"
    :subtitle "任意骰；占箱子 1 格。成功逃脱后换成 10 金"
    :disabled (luggage-full?)
    :requires (list (req-die))
    :resolve (instant
      (outcome "压进箱底"
        (lambda ()
          (set! valuables-packed (+ valuables-packed 1))
          (luggage-clk 'tick!))
        'light))))

(define (node-pack-roadblock)
  (node "装进能扔的东西"
    :subtitle "任意骰；占箱子 1 格。第二幕可与任意骰配合，确定推进物理对象 2 格"
    :disabled (luggage-full?)
    :requires (list (req-die))
    :resolve (instant
      (outcome "捆成一包"
        (lambda ()
          (set! roadblocks-packed (+ roadblocks-packed 1))
          (luggage-clk 'tick!))
        'light))))

(define (node-searcher)
  (container-with-clocks "搜查者"
    (if (searcher-clk 'full?) '() (list (node-handle-searcher)))
    (list (searcher-clk 'render-data))))

(define (node-exit)
  (container-with-clocks "后窗与消防梯"
    (if (exit-clk 'full?) '() (list (node-leave-apartment)))
    (list (exit-clk 'render-data))))

(define (node-decoys)
  (container-with-clocks "床边的两个假人"
    (if (decoy-clk 'full?) '() (list (node-build-decoys)))
    (list (decoy-clk 'render-data))))

(define (node-luggage)
  (container-with-clocks "敞开的箱子"
    (if (luggage-full?)
        '()
        (list (node-pack-valuables) (node-pack-roadblock)))
    (list (luggage-clk 'render-data))))

(define (act1-nodes)
  (append
    (list (node-nightingale) (node-background-search) (node-exit))
    (if searcher-present? (list (node-searcher)) '())
    (list (node-decoys) (node-luggage))))

;; ============================================================
;; 第二幕：送酒车
;; ============================================================

(define pursuit-clk
  (make-clock "追兵逼近" 6 'segments
              "每驶过一个路段 +1；部分坏结果也会推进。填满 = 追车贴上来，夜莺受伤，本场失败。"))

(define road 0) ; 0 鱼市 / 1 电车路口 / 2 警察封锁线
(define road-left-clk #f)
(define road-right-clk #f)
(define road-left-resolved? #f)
(define road-right-resolved? #f)
(define skip-current-pursuit? #f)
(define delay-next-pursuit? #f)

(define (road-name)
  (cond
    ((= road 0) "鱼市")
    ((= road 1) "电车路口")
    ((= road 2) "警察封锁线")
    (else (error "抢人第二幕：未知路段"))))

(define (road-left-name)
  (cond
    ((= road 0) "横在街心的鱼摊")
    ((= road 1) "电车前方的空隙")
    ((= road 2) "封锁线的木侧栏")
    (else (error "抢人第二幕：未知左侧对象"))))

(define (road-right-name)
  (cond
    ((= road 0) "卸货巷里的木架")
    ((= road 1) "道口信号箱")
    ((= road 2) "检查口的警察")
    (else (error "抢人第二幕：未知右侧对象"))))

(define (road-left-note)
  (cond
    ((= road 0) "填满 = 撞翻鱼摊，追兵 −2、劳工关系 −1。短而确定，账留在老街。")
    ((= road 1) "填满 = 抢过电车前方，追兵立即 −1。适合追兵已经贴近时救急。")
    ((= road 2) "填满 = 撞开侧栏，本路段追兵不再自动 +1、官僚关系 −1。")
    (else (error "抢人第二幕：未知左侧说明"))))

(define (road-right-note)
  (cond
    ((= road 0) "填满 = 你亲手掀倒卸货架，追兵 −2，不伤劳工关系；坏结果会受伤。")
    ((= road 1) "填满 = 切断信号线；警察封锁线路段的追兵自动推进取消。现在不减追兵。")
    ((= road 2) "填满 = 把追车引进检查口，追兵 −2；坏结果会令追兵 +1、官僚关系 −1。")
    (else (error "抢人第二幕：未知右侧说明"))))

(define (road-left-max) 2)
(define (road-right-max) 3)

(define (pursuit+ n)
  (pursuit-clk 'set! (min 6 (+ (pursuit-clk 'current) n))))

(define (pursuit- n)
  (clock-sub! pursuit-clk n))

(define (caught?) (pursuit-clk 'full?))

(define (roadblocks-left)
  (- roadblocks-packed roadblocks-used))

(define (roadblock-usable-on-right?)
  (not (= road 2)))

(define (roadblock-clock-data)
  (if (> roadblocks-packed 0)
      (list (list 'clock "路障材料已使用" roadblocks-used roadblocks-packed 'segments
                  "每份材料仍需投入任意骰；确定推进一个物理对象 2 格。用完即止。"))
      '()))

(define (nightingale-act-on-road!)
  (let ((target (random-choice (list 'left 'right))))
    (if (equal? target 'left)
        (begin
          (road-left-clk 'tick!)
          (set! nightingale-note
            (cond
              ((= road 0) "她把送酒车贴向鱼摊边缘，给你留出撞过去的角度。鱼摊 +1。")
              ((= road 1) "她开始加速，车头已经探进电车前方的空隙。抢过电车 +1。")
              (#t "她把车头对准木侧栏，没有踩刹车。木侧栏 +1。")))
          (play-dialogue!
            (line "夜莺"
              (cond
                ((= road 0) "鱼摊会替我们挡一会儿。看准时机。")
                ((= road 1) "电车前面还有一条缝。别让我一个人赌它。")
                (#t "侧栏不结实。你只要让我再靠近一点。")))))
        (begin
          (road-right-clk 'tick!)
          (set! nightingale-note
            (cond
              ((= road 0) "她打开你这一侧的车门，让卸货架从手边掠过。木架 +1。")
              ((= road 1) "她把车贴近信号箱，距离刚好够你伸手。信号箱 +1。")
              (#t "她朝检查口按了两下喇叭，警察已经看向后面的追车。检查口 +1。")))
          (play-dialogue!
            (line "夜莺"
              (cond
                ((= road 0) "右边的木架能倒。抓稳，别把自己也甩出去。")
                ((= road 1) "信号箱就在手边。你知道该拔哪根吗？")
                (#t "让他们看后面那辆车。现在。"))))))))

(define (enter-road! n)
  (set! road n)
  (set! road-left-resolved? #f)
  (set! road-right-resolved? #f)
  (set! skip-current-pursuit? delay-next-pursuit?)
  (set! delay-next-pursuit? #f)
  (set! road-left-clk (make-clock (road-left-name) (road-left-max) 'segments (road-left-note)))
  (set! road-right-clk (make-clock (road-right-name) (road-right-max) 'segments (road-right-note)))
  (nightingale-act-on-road!))

(define (resolve-road-left!)
  (cond
    ((= road 0)
     (pursuit- 2)
     (change-faction-relation! "劳工" -1)
     (result-note! "鱼摊被撞翻：追兵 −2，劳工关系 −1"))
    ((= road 1)
     (pursuit- 1)
     (result-note! "抢过电车：追兵 −1"))
    ((= road 2)
     (set! skip-current-pursuit? #t)
     (change-faction-relation! "官僚" -1)
     (result-note! "撞开侧栏：本路段追兵不再逼近，官僚关系 −1"))
    (else (error "抢人第二幕：左侧对象在未知路段结算"))))

(define (resolve-road-right!)
  (cond
    ((= road 0)
     (pursuit- 2)
     (result-note! "卸货架倒下：追兵 −2"))
    ((= road 1)
     (set! delay-next-pursuit? #t)
     (result-note! "信号线切断：警察封锁线路段的追兵自动推进取消"))
    ((= road 2)
     (pursuit- 2)
     (result-note! "追车被引进检查口：追兵 −2"))
    (else (error "抢人第二幕：右侧对象在未知路段结算"))))

(define (resolve-road-completions!)
  (if (and (road-left-clk 'full?) (not road-left-resolved?))
      (begin (set! road-left-resolved? #t) (resolve-road-left!))
      #f)
  (if (and (road-right-clk 'full?) (not road-right-resolved?))
      (begin (set! road-right-resolved? #t) (resolve-road-right!))
      #f))

(define (node-fish-market)
  (node "让她撞翻鱼摊"
    :subtitle "敏锐；坏：追兵 +1，中：鱼摊 +1，好：鱼摊 +2；填满还会令劳工关系 −1"
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "车尾擦过棚柱"
        (lambda () (pursuit+ 1)))
      (outcome "逼开一条窄缝"
        (lambda () (road-left-clk 'tick!)))
      (outcome "摊子横进街心"
        (lambda () (clock-tick-n! road-left-clk 2))))))

(define (node-drop-loading-rack)
  (node "探出车外掀倒木架"
    :subtitle "暴力；坏：受伤，中：木架 +1，好：木架 +2；填满不损害劳工关系"
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "肩膀撞上砖墙"
        (lambda () (injure!)))
      (outcome "扯松一根支腿"
        (lambda () (road-right-clk 'tick!)))
      (outcome "整排木架倒下"
        (lambda () (clock-tick-n! road-right-clk 2))))))

(define (node-cut-before-tram)
  (node "抢过电车前方"
    :subtitle "敏锐；坏：追兵 +1，中：空隙 +1，好：空隙 +2；填满立即令追兵 −1"
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "慢了半个车身"
        (lambda () (pursuit+ 1)))
      (outcome "车头挤进轨道"
        (lambda () (road-left-clk 'tick!)))
      (outcome "铃声落在身后"
        (lambda () (clock-tick-n! road-left-clk 2))))))

(define (node-cut-signal)
  (node "切断道口信号线"
    :subtitle "见识；坏：冷静 −1，中：信号箱 +1，好：信号箱 +2；填满保护下一路段"
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "抓错了那根线"
        (lambda () (spend-composure! 1)))
      (outcome "撬开信号箱"
        (lambda () (road-right-clk 'tick!)))
      (outcome "道口灯全亮了"
        (lambda () (clock-tick-n! road-right-clk 2))))))

(define (node-break-police-barrier)
  (node "撞开封锁线侧栏"
    :subtitle "暴力；坏：受伤，中：侧栏 +1，好：侧栏 +2；填满还会令官僚关系 −1"
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "木杆扫进车窗"
        (lambda () (injure!)))
      (outcome "撞松一根立柱"
        (lambda () (road-left-clk 'tick!)))
      (outcome "栏杆飞进雨里"
        (lambda () (clock-tick-n! road-left-clk 2))))))

(define (node-lure-into-checkpoint)
  (node "把追车引进检查口"
    :subtitle "社交；坏：追兵 +1、官僚关系 −1，中：检查口 +1，好：检查口 +2"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "警察先盯住了你们"
        (lambda ()
          (pursuit+ 1)
          (change-faction-relation! "官僚" -1)))
      (outcome "让岗哨看见后车"
        (lambda () (road-right-clk 'tick!)))
      (outcome "追车自己撞进哨卡"
        (lambda () (clock-tick-n! road-right-clk 2))))))

(define (node-road-left-action)
  (cond
    ((= road 0) (node-fish-market))
    ((= road 1) (node-cut-before-tram))
    ((= road 2) (node-break-police-barrier))
    (else (error "抢人第二幕：未知左侧动作"))))

(define (node-road-right-action)
  (cond
    ((= road 0) (node-drop-loading-rack))
    ((= road 1) (node-cut-signal))
    ((= road 2) (node-lure-into-checkpoint))
    (else (error "抢人第二幕：未知右侧动作"))))

(define (road-left-roadblock-name)
  (cond
    ((= road 0) "路障撞鱼摊")
    ((= road 1) "路障抢电车")
    ((= road 2) "路障撞侧栏")
    (else (error "抢人第二幕：未知左侧路障动作"))))

(define (road-right-roadblock-name)
  (cond
    ((= road 0) "路障砸木架")
    ((= road 1) "路障断信号")
    ((= road 2) "路障引追车")
    (else (error "抢人第二幕：未知右侧路障动作"))))

(define (node-use-roadblock target title)
  (node title
    :subtitle (string-append "任意骰；消耗 1 份路障材料，确定推进 2 格。剩余 "
                 (number->string (roadblocks-left)) " 份")
    :disabled (<= (roadblocks-left) 0)
    :requires (list (req-die))
    :resolve (instant
      (outcome "从车后抛下去"
        (lambda ()
          (set! roadblocks-used (+ roadblocks-used 1))
          (clock-tick-n! target 2))
        'light))))

(define (road-left-actions)
  (if road-left-resolved?
      '()
      (append
        (list (node-road-left-action))
        (if (> (roadblocks-left) 0)
            (list (node-use-roadblock road-left-clk (road-left-roadblock-name)))
            '()))))

(define (road-right-actions)
  (if road-right-resolved?
      '()
      (append
        (list (node-road-right-action))
        (if (and (> (roadblocks-left) 0) (roadblock-usable-on-right?))
            (list (node-use-roadblock road-right-clk (road-right-roadblock-name)))
            '()))))

(define (node-road-left)
  (container-with-clocks (road-left-name)
    (road-left-actions)
    (list (road-left-clk 'render-data))))

(define (node-road-right)
  (container-with-clocks (road-right-name)
    (road-right-actions)
    (list (road-right-clk 'render-data))))

(define (road-nodes)
  (list (node-nightingale) (node-road-left) (node-road-right)))

(define (route-clock-data)
  (list 'clock "冲出搜捕范围" road 3 'segments
        "每次结束回合驶过当前路段；局部对象未填满的进度随街景永久消失。通过第三段且追兵未满即成功。"))

;; ============================================================
;; 转场与结算
;; ============================================================

(define (finish-success!)
  (if (> valuables-packed 0)
      (add-item! "金钱" (* valuables-packed 10))
      #f)
  (set! finished? #t)
  (play-dialogue!
    (line "夜莺" "后面没灯了。")
    (line "尼尔" "你可以慢一点。")
    (line "夜莺" "你先把手松开再说。"))
  (spotlight! "抢人：甩掉了"
    (string-append
      "送酒车停在雨里的旧仓库后面。追车没有再出现。"
      (if (> valuables-packed 0)
          (string-append "箱子也在；里面带出了价值 "
                         (number->string (* valuables-packed 10)) " 金的东西。")
          "箱子很轻，可两个人都还在。")))
  (end-encounter 'success))

(define (finish-fail!)
  (if finished?
      #f
      (begin
        (set! finished? #t)
        (injure!)
        (if (= act 1)
            (begin
              (play-dialogue!
                (line "世界" "门锁断了。楼道里的人已经看见你们。")
                (line "夜莺" "走后窗。现在。"))
              (spotlight! "抢人：门被撞开"
                "夜莺在楼梯上挨了一下；送酒车没能开出两条街。"))
            (begin
              (play-dialogue!
                (line "夜莺" "他们追上来了。弃车。"))
              (spotlight! "抢人：追车贴上来"
                "追车从侧面撞上送酒车。她挨了一下，没出声；你们弃车钻进雨巷，天亮以前谁也没能回酒馆。")))
        (end-encounter 'fail))))

(define (begin-chase!)
  (play-dialogue!
    (line "夜莺" "楼下那辆送酒的车。钥匙在我这儿。")
    (line "尼尔" "你会开？")
    (line "夜莺" "你还有时间换人吗？"))
  (set! act 2)
  (pursuit-clk 'set! (if (decoy-clk 'full?) 3 4))
  (enter-road! 0))

(define (police-release!)
  (pursuit- 2)
  (set! nightingale-note
    "岗哨认出了你，抬杆放行，又把栏杆落在追车前面。追兵 −2。")
  (play-dialogue!
    (line "世界" "岗哨看清你的脸，抬起栏杆。送酒车过去以后，栏杆又落了下来。")
    (line "夜莺" "你在警察局到底留了多少张脸？")
    (line "尼尔" "今晚刚好够用。"))
  (finish-success!))

(define (advance-road!)
  (if (not skip-current-pursuit?)
      (pursuit+ 1)
      #f)
  (if (caught?)
      (finish-fail!)
      (if (= road 2)
          (finish-success!)
          (let ((next-road (+ road 1)))
            (if (and (= next-road 2) police-help?)
                (police-release!)
                (enter-road! next-road))))))

;; 单一回合规则把顺序写死，避免多个 turn-rule 的注册逆序让压力先后不清：
;; 第一幕只在玩家主动结束回合时推进搜查。退路由玩家动作填满时立即转场；
;; 若尚未填满，夜莺在回合推进后仍可能补上最后一格并完成撤离。
;; 第二幕结算当前路段后立刻换景，未完成的局部进度随旧 Clock 一起丢弃。
(define-turn-rule "公寓与追车继续推进"
  (lambda () (not finished?))
  (lambda ()
    (cond
      ((= act 1)
       (begin
         (set! apartment-turns (+ apartment-turns 1))
         (advance-search! 1)
         (if (and (not finished?) searcher-present? (not (searcher-clk 'full?)))
             (advance-search! 1)
             #f)
         (if finished?
             #f
             (begin
               (if (= apartment-turns 1) (searcher-arrive!) #f)
               (if (exit-clk 'full?)
                   (begin-chase!)
                   (begin
                     (nightingale-act-in-apartment!)
                     (if (exit-clk 'full?) (begin-chase!) #f)))))))
      ((= act 2) (advance-road!))
      (else (error "抢人：未知幕")))))

(define-rule "动作后的即时结算"
  (lambda () (not finished?))
  (lambda ()
    (cond
      ((and (= act 1) (search-clk 'full?)) (finish-fail!))
      ((and (= act 1) (exit-clk 'full?)) (begin-chase!))
      ((= act 2)
       (begin
         (resolve-road-completions!)
         (if (caught?) (finish-fail!) #f)))
      (else #f))))

;; 入场时夜莺已经在行动，玩家拿到第一把骰时就能看见她先碰了哪个对象。
(nightingale-act-in-apartment!)

;; ============================================================
;; 渲染
;; ============================================================

(define (act1-title)
  (if own-apartment?
      "抢人：你的公寓"
      "抢人：借住公寓"))

(define (get-render-data)
  (if (= act 1)
      (container (act1-title)
        (append (clock-nodes (search-clk 'render-data))
          (act1-nodes)))
      (container (string-append "抢人：" (road-name))
        (append (apply clock-nodes
                  (append (list (pursuit-clk 'render-data) (route-clock-data)) (roadblock-clock-data)))
          (road-nodes)))))
