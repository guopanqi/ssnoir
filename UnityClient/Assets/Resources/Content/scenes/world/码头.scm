;; 码头——普通生计由地点拥有；弗兰克拥有自己的个人生活。
;;
;; 这里放着城市生活里三种不同形状的活，它们不是"报酬不同的同一份工作"：
;;   搬运   ——常驻日结。Phase B 起每天只能做一次，码头不再容得下整天的零工。
;;   夜班   ——只在有船靠岸的那一晚出现。钱多、险，而且熬了一夜当晚睡不好。
;;   顶班   ——不结钱，只换人情。带薪工作到「面熟」就封顶，想再往上只有这条路。
;; 三张卡各自回答一个不同的问题：今天保底挣多少 / 要不要冒这一夜 / 要不要今天不挣钱。

(define dock
  (let ()
    ;; 码头按分区落卡（锚点在 city-box/prefabs/src/码头.py）：
    ;;   码头          泊位桥头——船边的事：靠岸钟、夜班、抢修、不开的船、机器上岸
    ;;   码头-货堆     作业面上的货岛——扛包的活、在货堆那头的人
    ;;   码头-岸口     木栅门和点工棚——等活、顶班、巡警
    ;;   码头-账房     后街的账房——船位记录
    ;;   码头-巷口     平台后面两间小屋之间的缝——去货栈后面
    ;;   码头-三号货栈 北端工棚的院子——机器那头的告示、试运行、培训
    ;;   勒索信 / 勒索信-报摊  南端邮箱一角（嵌套 Prefab 自己的锚点）——踩点、投信
    ;; 没声明落点的卡收回桥头，不掉进网格；已声明的保留自己的落点。
    (define dock-anchor "码头")

    (define (anchor-at-dock node-data)
      (if (member? :anchor node-data)
          node-data
          (append node-data (list :anchor dock-anchor))))

    ;; ── 靠岸周期 ────────────────────────────────────
    ;; 每三天有一晚有船靠岸，那一晚码头开夜班。它是第一章里唯一一件
    ;; "今天和昨天不一样"的事：有船的那天，高点数的骰子值得留给夜班；
    ;; 没船的日子就老老实实白天搬。倒计时从泊位锚点伸出来，所以这件事是可以计划的。
    (define berth-cycle 2)          ; 归零那天有船 → 实际周期三天
    (define berth-clk
      (make-clock "下一班船靠岸" berth-cycle 'countdown
        "归零的那一晚码头开夜班：钱比白天多，也比白天险。"))
    (berth-clk 'set! berth-cycle)

    (define (berthed?) (berth-clk 'empty?))

    ;; 泊位循环是码头里那一处的状态，不应占 Place 标签。clock-node 是标注而非卡，
    ;; 经 anchor-at-dock 固定到现有的「码头」泊位锚点。
    (define (berth-status-node)
      (clock-node "钟：下一班船靠岸" (berth-clk 'render-data)))

    (define-turn-rule "码头靠岸周期"
      (lambda () #t)
      (lambda ()
        (if (berthed?)
            (berth-clk 'set! berth-cycle)   ; 昨夜那条船天亮就开走了
            (berth-clk 'advance! -1))))

    ;; ── 每日一次的额度 ──────────────────────────────
    (define night-shift-today? #f)  ; 今天熬过夜班：当晚睡觉只回 1 点（见 home.scm）
    (define haul-day 0)             ; Phase B 起，普通搬运每天只能做一次

    (define-turn-rule "码头每日额度重置"
      (lambda () night-shift-today?)
      (lambda () (set! night-shift-today? #f)))

    ;; ── 常驻日结 ────────────────────────────────────
    (define (node-haul)
      (工作 "搬运" "老码头" '高 'violence
        (outcome "扛完一整班"
          (lambda ()
            (set! haul-day world-day)
            (add-item! "金钱" 15)
            (grant-work-relation! "老码头")))
        (outcome "勉强做完"
          (lambda ()
            (set! haul-day world-day)
            (add-item! "金钱" 8)
            (spend-composure! 1)))
        ;; 高风险由更高报酬与力量检定表达；日常失手仍只扣 2 点冷静。
        (outcome "货箱脱手"
          (lambda ()
            (set! haul-day world-day)
            (spend-composure! 2)))
        "扛一班货，挣一晚的钱"
        :anchor "码头-货堆"))

    ;; ── 夜班：只在有船的那一晚 ──────────────────────
    ;; 报酬明显高于白天，所以有船的那天它会吃掉玩家的好骰子——这正是它存在的理由。
    ;; 但它不是白拿的：熬过通宵的当晚，睡觉只回 1 点。这一张把"多挣的钱"和
    ;; "少回的冷静"摆在同一个决定里，而不是又给一份更大的报酬。
    (define (node-night-shift)
      (工作 "夜班卸船" "老码头" '高 'violence
        (outcome "整夜没停手"
          (lambda ()
            (add-item! "金钱" 22)
            (set! night-shift-today? #t)
            (grant-work-relation! "老码头")))
        (outcome "熬到天亮"
          (lambda ()
            (add-item! "金钱" 12)
            (set! night-shift-today? #t)
            (spend-composure! 1)))
        (outcome "半夜出了事"
          (lambda ()
            (set! night-shift-today? #t)
            (spend-composure! 2)))
        "半夜靠岸的船不等人，钱给得比白天多；熬过这一夜，当晚睡不好"))

    ;; ── 顶班：不结钱，只换人情 ──────────────────────
    ;; 老码头唯一能爬过「面熟」的路。带薪工作到值 3 就封顶
    ;; （见 engine.scm 的 grant-work-relation!），要「够朋友」，
    ;; 就得有几天你在码头待了一整天却一分钱没拿。
    ;; 认识你之后才有人来求你顶班，所以它挂在「面熟」后面。
    (define (node-cover-shift)
      (node "替人顶一班"
        :anchor "码头-岸口"
        :tags (list "人情" "低风险")
        :subtitle "有人今晚走不开。顶下来不结钱，但这条街会记着"
        :requires (list (req-die))
        :resolve (roll 'violence
          (outcome "白站了一夜"
            (lambda () (spend-composure! 1)))
          (outcome "替他站到收工"
            (lambda () (grant-favor-relation! "老码头")))
          (outcome "顺手把他的账也平了"
            (lambda ()
              (grant-favor-relation! "老码头")
              (restore-actor-composure! 'player 1))))))

    ;; ── 组装 ────────────────────────────────────────
    (define (livelihood-nodes)
      (append
        (if (and (equal? (第二章 'phase) "B") (= haul-day world-day))
            '()
            (list (node-haul)))
        (if (berthed?) (list (node-night-shift)) '())
        ;; 「替人顶一班」这一版不摆出来：它的全部回报是老码头声誉，而声誉现在还兑现不出
        ;; 什么（面板也一并收起来了，见 NavigationDrawer.ShowRelationPanel）。一张花掉一颗骰、
        ;; 一分钱不结、换回来的东西玩家又看不见的卡，只会让人以为自己漏掉了什么。
        ;; 卡本身留着——声誉真有东西可换的时候，把下面这行换回原来的门槛判断就回来了。
        '()))

    (define (children)
      (map anchor-at-dock
        (append
          (list (berth-status-node))
          (livelihood-nodes)
          (地点节点 "码头"))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "码头"
                        :children (children)
                        :arrivals (地点入场 "码头"))))
          ;; 住所据此决定今晚睡觉回几点（见 home.scm 的 node-sleep）。
          ((equal? msg 'night-shift-today?) night-shift-today?)
          ((equal? msg 'save)
           (list (list "berth" (berth-clk 'save))
                 (list "night-shift-today?" night-shift-today?)
                 (list "haul-day" haul-day)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (berth-clk 'load! (assoc-get data "berth" berth-cycle))
             (set! night-shift-today? (assoc-get data "night-shift-today?" #f))
             (set! haul-day (assoc-get data "haul-day" 0))))
          (else #f))))))
