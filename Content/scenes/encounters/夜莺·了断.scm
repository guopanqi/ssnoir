;; scenes/encounters/夜莺·了断.scm - 夜莺委托线·终场
;; 两阶段合同：第一幕谈判桌(不走钟，为第二幕挣修正)→ 第二幕摊牌(打手赛跑)。
;; 路线一(封口)/四(立案)/五(坐视)在城市侧结算，不进本场；本场只服务
;; 路线二(保底，标准变体)与路线三败(她已上船，无人变体)。
;; 对外契约:以 (end-encounter 'success) 或 (end-encounter 'fail) 结束。

;; ── 读镜像 ───────────────────────────────────────
(define departed? (let ((v (get-global '夜莺已送走))) (if v v #f)))
(define exposed? (let ((v (get-global '夜莺夹带暴露))) (if v v #f)))
(define safety (let ((v (get-global '夜莺状态等级))) (if v v 0)))
(define crew-threshold
  (if (equal? (let ((p (get-global '夜莺保护方案))) (if p p "无")) "码头") '相识 '核心))

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

;; ── 幕次状态 ─────────────────────────────────────
(define phase 1)
(define leverage? #f)      ; 第一幕谈出余地：他心虚了，第二幕失败钟上限 +1
(define ledger-used? #f)
(define pass-intervention? #f)

;; ── 第一幕：谈判桌（不走钟）────────────────────────
(define leverage-clk
  (make-clock "谈出余地" 4 'segments "他心虚了：第二幕失败钟上限 +1（这笔账经不起吵）。"))

(define collapse-clk
  (make-clock "谈崩" 4 'segments "谈崩即入第二幕，没有修正。"))

;; ── 第二幕：摊牌（打手赛跑，进幕时才创建，好读取第一幕的 leverage? 结果）──
(define resolve-clk #f)
(define press-clk #f)

(define (press-clk-title) (if departed? "他们要你付出代价" "他们要带她走"))
(define (press-clk-max)
  (+ 5
     (if leverage? 1 0)
     (if (and (not departed?) (>= safety 2)) -1 0)))

(define (enter-phase-2!)
  (set! resolve-clk (make-clock "压住场子" 6 'segments "填满即可把这一架压下去。"))
  (set! press-clk
        (make-clock (press-clk-title) (press-clk-max) 'segments
                    (if departed?
                        "填满后，你独自扛下代价——她已经安全。"
                        "填满后，她会被带走。")))
  (if (and departed? exposed?) (press-clk 'tick!) #f)
  (set! phase 2))

;; ── 敌人单位 ─────────────────────────────────────
(define thug-seq 0)
(define (next-thug-id) (set! thug-seq (+ thug-seq 1)) thug-seq)

(define (make-thug hp-max)
  (let ((id (next-thug-id)) (hp hp-max))
    (lambda (msg)
      (cond
        ((equal? msg 'dead?) (<= hp 0))
        ((equal? msg 'retire!) (set! hp 0))
        ((equal? msg 'render-data)
         (container-with-clocks (string-append "打手 " (number->string id))
           (list
             (action (string-append "压制 打手 " (number->string id))
               (list (req-die))
               (roll 'violence
                 (lambda () (press-clk 'tick!))
                 (lambda () (set! hp (- hp 1)) (resolve-clk 'tick!))
                 (lambda () (set! hp (- hp 2)) (clock-tick-n! resolve-clk 2)))))
           (list (list 'clock "打手" (if (< hp 0) 0 hp) hp-max 'segments))))
        (else #f)))))

(define thugs (list (make-thug 3) (make-thug 3)))

(define (live-thugs)
  (filter (lambda (e) (not (e 'dead?))) thugs))

(define (retreat-press! n)
  (press-clk 'set! (max 0 (- (press-clk 'current) n))))

;; ── 结算 ─────────────────────────────────────────
(define (finish-success!)
  (spotlight! "了断：自由" "收账人收了手。雨停下来那晚,她头一回把一整场唱完。")
  (end-encounter 'success))

(define (finish-fail!)
  (if departed?
      (begin
        (damage-party! 2)
        (spend-up-to! "金钱" (quotient (item-count "金钱") 2))
        (spotlight! "了断：你扛下了" "打完最后一拳,你自己爬起来。她已经安全,这笔账,记在你身上。"))
      (begin
        (damage-party! 1)
        (spotlight! "了断：她跟他们走了" "她自己迈出门去,拿一条命换你一条命。门在她身后合上,没再开。")))
  (end-encounter 'fail))

;; ── 第一幕推进与胜负 ─────────────────────────────
(define-rule "谈出余地"
  (lambda () (and (= phase 1) (leverage-clk 'full?)))
  (lambda ()
    (set! leverage? #t)
    (spotlight! "他心虚了" "这笔账,他自己也知道经不起吵。")
    (enter-phase-2!)))

(define-rule "谈崩"
  (lambda () (and (= phase 1) (collapse-clk 'full?)))
  (lambda ()
    (spotlight! "谈崩了" "他把椅子推开,站起身。'废话说完了?'")
    (enter-phase-2!)))

;; ── 第二幕推进与胜负 ─────────────────────────────
(define-turn-rule "对方逼近"
  (lambda () (= phase 2))
  (lambda ()
    (if pass-intervention?
        (set! pass-intervention? #f)
        (clock-tick-n! press-clk (length (live-thugs))))
    (if (press-clk 'full?)
        (finish-fail!)
        #f)))

(define-rule "了断成功"
  (lambda () (and (= phase 2) (resolve-clk 'full?)))
  (lambda () (finish-success!)))

(define-rule "了断失败"
  (lambda () (and (= phase 2) (press-clk 'full?)))
  (lambda () (finish-fail!)))

;; ── 第一幕：谈判动词 ──────────────────────────────
(define (node-bargain)
  (action "跟他谈价"
    (list (req-die))
    (roll 'social
      (lambda () (collapse-clk 'tick!))
      (lambda () (leverage-clk 'tick!))
      (lambda () (clock-tick-n! leverage-clk 2)))))

(define (node-see-through-motive)
  (action "看破他的心事"
    (list (req-die))
    (roll 'sharpness
      (lambda () (collapse-clk 'tick!))
      (lambda () (leverage-clk 'tick!))
      (lambda ()
        (clock-tick-n! leverage-clk 2)
        (spotlight! "他的心事" "死的那个人,是他一手带大的表弟。这笔账,他比谁都记得清楚。")))))

(define (node-expose-ledger)
  (action "点破这笔账见不得光"
    (list (req-die))
    (roll 'social
      (lambda () (set! ledger-used? #t) (clock-tick-n! leverage-clk 1))
      (lambda () (set! ledger-used? #t) (clock-tick-n! leverage-clk 2))
      (lambda ()
        (set! ledger-used? #t)
        (clock-tick-n! leverage-clk 2)
        (collapse-clk 'set! (max 0 (- (collapse-clk 'current) 1)))))))

(define (node-flip-table)
  (action "掀桌" #f
    (instant (lambda () (enter-phase-2!)))))

(define (phase1-nodes)
  (append
    (list (node-bargain) (node-see-through-motive))
    (if (and (> (item-count "欠账凭据") 0) (not ledger-used?))
        (list (node-expose-ledger))
        '())
    (list (node-flip-table))))

;; ── 第二幕：攻防动词(沿用抢人场攻防组) ─────────────
(define (node-hold-door)
  (action "抵住前门"
    (list (req-die))
    (roll 'violence
      (lambda () (stress-current-actor! 1))
      (lambda () (retreat-press! 1))
      (lambda () (retreat-press! 2)))))

(define (node-shout-down)
  (action "冲他们吼"
    (list (req-die))
    (roll 'social
      (lambda () (press-clk 'tick!))
      (lambda () (retreat-press! 1))
      (lambda () (set! pass-intervention? #t)))))

(define (node-loot)
  (action "趁乱摸点东西"
    (list (req-die))
    (roll 'sharpness
      (lambda () (press-clk 'tick!))
      (lambda () (add-item! "金钱" 8))
      (lambda () (add-item! "金钱" 8) (add-item! "情报" 1)))))

;; ── 她的在场文本 ─────────────────────────────────
(define (node-her-presence)
  (cond
    (departed?
     (observe-action "空座位" "她的位置空着,只留下一只箱子——她已经在船上。"))
    ((>= safety 2)
     (observe-action "她不在场" "她伤着,没有露面。"))
    (else
     (observe-action "她在场" "她递给你一样东西,不多说话,只是不肯躲远。"))))

;; ── 场内增益(读现有 global,各一次性,不是出口) ─────
(define crew-used? #f)
(define pass-used? #f)
(define laozhou-used? #f)
(define laozhou-help?
  (let ((v (get-global 'laozhou-can-help))) (if v v #f)))

(define (node-call-crew)
  (action "码头兄弟到场"
    (list (req-die))
    (instant (lambda ()
      (set! crew-used? #t)
      (let ((remaining (live-thugs)))
        (if (not (null? remaining)) ((car remaining) 'retire!) #f))
      (retreat-press! 1)))))

(define (node-use-pass)
  (action "程序干预"
    (list (req-item "办案通行证" 1))
    (instant (lambda () (set! pass-used? #t) (set! pass-intervention? #t)))))

(define (node-laozhou-ally)
  (container-with-clocks "老周"
    (list
      (instant-action "请老周出面"
        (lambda ()
          (set! laozhou-used? #t)
          (retreat-press! 2))))
    '()))

(define (extra-nodes)
  (append
    (if (and (relation-at-least? "劳工" crew-threshold) (not crew-used?)) (list (node-call-crew)) '())
    (if (and (> (item-count "办案通行证") 0) (not pass-used?)) (list (node-use-pass)) '())
    (if (and laozhou-help? (not laozhou-used?)) (list (node-laozhou-ally)) '())))

(define (phase2-nodes)
  (append
    (list (node-hold-door) (node-shout-down) (node-loot))
    (map (lambda (e) (e 'render-data)) (live-thugs))
    (list (node-her-presence))
    (extra-nodes)))

;; ── 渲染 ─────────────────────────────────────────
(define (get-render-data)
  (if (= phase 1)
      (container-with-clocks "夜莺·了断"
        (phase1-nodes)
        (list (leverage-clk 'render-data) (collapse-clk 'render-data)))
      (container-with-clocks "夜莺·了断"
        (phase2-nodes)
        (list (resolve-clk 'render-data) (press-clk 'render-data)))))
