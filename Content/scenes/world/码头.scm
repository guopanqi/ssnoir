;; scenes/world/码头.scm - 码头（劳工）
;; 工作梯：搬运（高·人人可做）→ 记账（中·需脸熟·产情报）→ 走私（越界·需自己人）
;; 老周从开局常驻；反复做记账工作积累个人好感。
;; 好感够高：交锋增援 / 每回合小概率美差 →「找上门的人」复发交锋（约 3 次）。

(define dock
  (let ()

    (define ledger-clue "货单对不上")   ; 具名线索（global flag，记账产出）
    (define bout-rest-blocker-id "码头/找上门")
    (define bout-rest-blocker-reason "码头：必须处理找上门的人")

    ;; ── 状态 ────────────────────────────────────
    (define laozhou-favor 0)             ; 老周好感（0..favor-max，独立于劳工势力关系）
    (define bout-count 0)                ; 「找上门的人」已发生次数（0..3）
    (define threat-cleared? #f)          ; 弧线是否收尾
    (define cushy-available? #f)         ; 本回合是否刷出老周的美差
    (define smuggle-cooldown 0)          ; 码头风声：>0 时走私暂闭（天）
    (define pending-bout? #f)            ; 是否有一场交锋正等着你去应付
    (define bout-interval 6)
    (define next-bout (make-clock "找上门" bout-interval 'segments)) ; 内部记录已经过去的天数

    (define favor-max 6)
    (define familiar-threshold 3)        ; 好感 ≥ 此 → 交锋增援
    (define close-threshold 5)           ; 好感 ≥ 此 → 每回合美差
    ;; 交心后每个新回合独立抽一次，20% 出现。收益高但仍占一颗行动骰。
    (define cushy-roll-table (list #t #f #f #f #f))

    ;; ── 好感读写（镜像到 global，供交锋读取）──────────
    (define (sync-favor!)
      (set-global! 'laozhou-favor laozhou-favor)
      (set-global! 'laozhou-can-help (>= laozhou-favor familiar-threshold)))
    (sync-favor!)

    (define (bump-favor! n)
      (set! laozhou-favor (min favor-max (+ laozhou-favor n)))
      (sync-favor!))

    (define (sync-rest-blocker!)
      (if pending-bout?
          (rest-block! bout-rest-blocker-id bout-rest-blocker-reason)
          (rest-release! bout-rest-blocker-id)))

    ;; ── 工作 ────────────────────────────────────
    ;; 搬运：高风险零工。好=大钱+关系；中=钱但压力涨；坏=见血伤健康。
    (define (node-haul)
      (工作 "搬运" "劳工" '高 'violence
        (outcome "工钱丰厚" "扛了一整天货，工钱给得爽快。"
          (lambda () (add-item! "金钱" 15)))
        (outcome "累到脱力" "搬了一天货，拿到该拿的工钱，可腰背像散了架。"
          (lambda () (add-item! "金钱" 8) (stress-current-actor! 1)))
        (outcome "砸伤了手" "货箱砸到了手，见了血。工头只当没看见。"
          (lambda () (stress-current-actor! 1) (damage-party! 1)))))

    ;; 记账：中风险熟手活，需脸熟。钱不多，主要价值是积累老周的个人好感。
    (define (node-ledger)
      (工作 "记账" "劳工" '中 'knowledge
        (outcome "摸到账目门道" "账目对上了，你也摸到了一些门道。"
          (lambda ()
            (add-item! "金钱" 10)
            (add-item! "情报" 1)
            (bump-favor! 2)
            (if (get-global ledger-clue)
                #t
                (begin
                  (set-global! ledger-clue #t)
                  (spotlight! "货单对不上"
                              "有几个箱子的编号，账上根本没有。你记下了。")))))
        (outcome "账目无误" "账算清了，没出什么岔子。"
          (lambda ()
            (add-item! "金钱" 5)
            (bump-favor! 1)))
        (outcome "记错了账" "一笔账记岔了，赔了钱。"
          (lambda () (spend-up-to! "金钱" 5)))))

    ;; 走私：越界的活，需自己人；码头起风声时暂闭。好=大钱+情报；坏=掉关系+压力。
    (define (node-smuggle)
      (工作 "走私" "劳工" '越界 'sharpness
        (outcome "顺利出港" "货顺利出了港。没人问，你也没问。"
          (lambda () (add-item! "金钱" 40) (add-item! "情报" 1)))
        (outcome "有惊无险" "有点惊险，但货送到了。"
          (lambda () (add-item! "金钱" 25)))
        (outcome "丢了半箱" "出了纰漏，货丢了半箱。码头上有人开始防着你。"
          (lambda ()
            (change-faction-relation! "劳工" -2)
            (stress-current-actor! 2)))))

    ;; ── 老周常驻节点：好感时钟 + 美差 ────────────────
    ;; 美差：好感够高后每回合小概率刷出。每次刷出的机会只在当天有效，
    ;; 做掉立即消失；错过则下次回合结算时被新的随机结果覆盖。
    (define (node-cushy)
      (node "帮老周带个话"
        :subtitle "老周留给你的美差，仅限今天"
        :tags (list "工作" "低风险")
        :clocks (list (list 'clock "转瞬即逝" 1 1 'countdown
                            "只在今天有效；结束一天后，这份工作就会消失。"))
        :requires (list (req-die))
        :resolve
          (roll 'social
            (outcome "扑了个空" "人没找着，白跑一趟。"
              (lambda ()
                (set! cushy-available? #f)
                (stress-current-actor! 1)))
            (outcome "办妥了" "话带到了，拿到了辛苦钱。"
              (lambda ()
                (set! cushy-available? #f)
                (add-item! "金钱" 12)))
            (outcome-append-effect
              (outcome "顺带的好处" "话带到了，老周塞给你比预想多的一沓。"
                (lambda ()
                  (set! cushy-available? #f)
                  (add-item! "金钱" 18)))
              (lambda () (change-faction-relation! "劳工" 1))
              "老周美差 好"))))

    (define (laozhou-description)
      (cond
        (threat-cleared?
         "老周靠着货堆抽烟：那帮人再不敢来了。有你这朋友，值。")
        ((>= laozhou-favor close-threshold)
         "老周见你过来，往旁边挪了挪，给你留出一个位置。")
        ((>= laozhou-favor familiar-threshold)
         "老周远远冲你点了点头。码头上真出事时，他应该愿意搭把手。")
        (else
         "老周守着账房和货单。想跟他混熟，先把记账的活做好。")))

    (define (node-laozhou)
      (node "老周"
        :subtitle "码头账房"
        :clocks (list (list 'clock "老周好感" laozhou-favor favor-max 'segments
                            "记账会积累好感；3 格时老周会在交锋中增援，5 格后可能介绍美差。"))
        :resolve (observe (laozhou-description))))

    ;; ── 交锋入口 + 结算回调 ─────────────────────────
    (define (on-bout-result result)
      (set! pending-bout? #f)
      (sync-rest-blocker!)
      (set! bout-count (+ bout-count 1))
      (if (equal? result 'success)
          (begin
            (add-item! "金钱" 20)
            (add-item! "情报" 1)
            (change-faction-relation! "劳工" 1)
            (notify! "你压住了场子。他们悻悻地退了。"))
          (begin
            (set! smuggle-cooldown 3)
            (add-actor-stress! 'player 1)
            (change-faction-relation! "劳工" -1)
            (notify! "没压住。码头上起了风声，一时半会儿碰不了走私了。")))
      (if (>= bout-count 3)
          (begin
            (set! threat-cleared? #t)
            (if (equal? result 'success)
                (begin (set-growth-level! (+ (growth-level) 1))
                       (notify! "这事总算了了。你在码头上，也算立住了。"))
                (notify! "他们不再来了——代价是这片水域记住了你的教训。")))
          (next-bout 'reset!)))

    (define (node-bout-entry)
      (instant-action "有人来找你（去应付）"
        (lambda () (start-encounter "找上门的人" on-bout-result))))

    ;; ── 回合规则：交锋排期 / 风声消退 / 美差 ──────────
    (define-turn-rule "找上门推进"
      (lambda () (and (not threat-cleared?)
                      (< bout-count 3) (not pending-bout?)))
      (lambda ()
        (next-bout 'tick!)
        (if (next-bout 'full?)
            (begin
              (set! pending-bout? #t)
              (sync-rest-blocker!)
              (set-global! 'bout-idx bout-count)
              (next-bout 'reset!))
            #f)))

    (define-turn-rule "码头风声消退"
      (lambda () (> smuggle-cooldown 0))
      (lambda () (set! smuggle-cooldown (- smuggle-cooldown 1))))

    (define-turn-rule "老周美差"
      (lambda () (>= laozhou-favor close-threshold))
      (lambda ()
        ;; on-turn-end 之后立即重建的是新一天快照，因此这里等价于回合开始抽取。
        ;; 直接覆盖旧值，保证上一回合没做的美差到期。
        (set! cushy-available? (random-choice cushy-roll-table))))

    ;; ── 组装 ────────────────────────────────────
    (define (smuggle-nodes)
      (if (and (relation-at-least? "劳工" '自己人) (<= smuggle-cooldown 0))
          (list (node-smuggle))
          '()))

    ;; 交锋排期从开局就在码头运行，只在码头地点显示。
    (define (dock-bout-clocks)
      (cond
        (pending-bout?
         (list (list 'clock "找上门" 0 bout-interval 'countdown
                     "事情已经发生：在码头应付找上门的人。")))
        ((or threat-cleared? (>= bout-count 3)) '())
        (else
         (list (list 'clock "找上门"
                     (- bout-interval (next-bout 'current)) bout-interval 'countdown
                     "归零后，对方会再次找上码头。")))))

    (define (dock-local-clocks)
      (append
        (dock-bout-clocks)
        (if (> smuggle-cooldown 0)
            (list (list 'clock "码头风声"
                        smuggle-cooldown 3 'countdown
                        "归零后风声散去，走私工作恢复。"))
            '())))

    (define (dock-children)
      (append
        (if pending-bout? (list (node-bout-entry)) '())
        (list (node-haul))
        (if (relation-at-least? "劳工" '脸熟) (list (node-ledger)) '())
        (list (node-laozhou))
        (if cushy-available? (list (node-cushy)) '())
        (smuggle-nodes)))

    ;; ── Message Passing Interface ─────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node "码头"
                   :children (dock-children)
                   :clocks (dock-local-clocks))))

          ((equal? msg 'save)
           (list
             (list "laozhou-favor"    laozhou-favor)
             (list "bout-count"       bout-count)
             (list "threat-cleared?"  threat-cleared?)
             (list "cushy-available?" cushy-available?)
             (list "smuggle-cooldown" smuggle-cooldown)
             (list "pending-bout?"    pending-bout?)
             (list "next-bout"        (next-bout 'current))))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! laozhou-favor    (assoc-get data "laozhou-favor" 0))
             (set! bout-count       (assoc-get data "bout-count" 0))
             (set! threat-cleared?  (assoc-get data "threat-cleared?" #f))
             (set! cushy-available? (assoc-get data "cushy-available?" #f))
             (set! smuggle-cooldown (assoc-get data "smuggle-cooldown" 0))
             (set! pending-bout?    (assoc-get data "pending-bout?" #f))
             (next-bout 'set! (assoc-get data "next-bout" 0))
             (sync-favor!)
             (sync-rest-blocker!)))

          ;; 调试：拨高老周好感（测增援 / 美差门槛）。
          ((equal? msg 'debug-favor) (bump-favor! 2))
          ((equal? msg 'debug-cushy) (set! cushy-available? #t))
          ((equal? msg 'debug-pending-bout)
           (begin
             (set! pending-bout? #t)
             (sync-rest-blocker!)
             (set-global! 'bout-idx bout-count)))
          ((equal? msg 'debug-clear-smuggle) (set! smuggle-cooldown 0))

          (#t #f))))))
