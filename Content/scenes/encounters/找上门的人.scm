;; scenes/encounters/找上门的人.scm - 码头对峙（委托合同式交锋）
;; 码头上的麻烦找上了门。交锋约复发 3 次、逐次升级。
;;
;; 委托合同结构（参考 combat.scm / infiltration.scm）：
;;   胜利 = 填满「压住场子」(resolve-clk)；失败 = 「对方逼近」(press-clk) 满。
;;   多条路推进 resolve：压制打手(力量) / 看破底牌(敏锐) / 谈条件(交际)。
;;   活法解锁额外动作：劳工自己人→叫兄弟；老周支线→一次免费援助；
;;   一次性办案通行证→程序干预；有钱→塞钱。富商不提供直接战力。
;;   非终局：无论成败/认怂都 (end-encounter …) 回城，健康可掉但不会 game over。

;; ── 升级：读世界镜像的 bout 序号（0/1/2）决定难度 ──
(define bout-idx (let ((b (get-global 'bout-idx))) (if b b 0)))

;; ── 核心时钟 ────────────────────────────────────
(define resolve-clk
  (make-clock "压住场子" (+ 5 bout-idx) 'segments
              "填满即可赢下这次对峙并回到城市。"))
(define press-clk
  (make-clock "对方逼近" (- 6 bout-idx) 'segments
              "每次结束回合都会推进；填满会受伤并失去这次机会。"))

;; 每场公共交锋都可尝试，直到取得为止。须从主目标中分出骰子，才能抢到具名线索。
(define invoice-clk
  (make-clock "异常货单" 3 'segments
              "可选目标；填满后即使本场最终失败，货单也会带回城市。"))

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

;; ── 敌人单位（照抄 combat.scm 敌人闭包）────────────
(define thug-seq 0)
(define (next-thug-id) (set! thug-seq (+ thug-seq 1)) thug-seq)

(define (make-thug hp-max)
  (let ((id (next-thug-id)) (hp hp-max))
    (lambda (msg)
      (cond
        ((equal? msg 'dead?) (<= hp 0))
        ((equal? msg 'render-data)
         (container-with-clocks (string-append "打手 " (number->string id))
           (list
             (action (string-append "压制 打手 " (number->string id))
               (list (req-die))
               (roll 'violence
                 (lambda () (press-clk 'tick!))                       ; 失败：场面更乱
                 (lambda () (set! hp (- hp 1)) (resolve-clk 'tick!))  ; 中性
                 (lambda () (set! hp (- hp 2)) (clock-tick-n! resolve-clk 2))))) ; 成功
           (list (list 'clock "打手" (if (< hp 0) 0 hp) hp-max 'segments))))
        (else #f)))))

(define thugs
  (if (= bout-idx 0)
      (list (make-thug 3))
      (list (make-thug 3) (make-thug 3))))

(define (live-thugs)
  (filter (lambda (e) (not (e 'dead?))) thugs))

(define pass-intervention? #f)

(define (finish-confrontation-success!)
  (spotlight! "码头对峙：成功" "你压住了场子。对方暂时退去，码头重新恢复了秩序。")
  (end-encounter 'success))

(define (finish-confrontation-fail!)
  (spotlight! "码头对峙：失败" "你被逼出了场子。冲突已经结束，但码头会承受这次失利的后果。")
  (end-encounter 'fail))

;; ── 压力：每回合按在场打手数递进 ──────────────────
(define-turn-rule "对方逼近"
  (lambda () #t)
  (lambda ()
    ;; 出示通行证的这一行动用于建立程序控制，不再额外触发对方推进。
    (if pass-intervention?
        (set! pass-intervention? #f)
        (clock-tick-n! press-clk (length (live-thugs))))
    (if (press-clk 'full?)
        (begin
          (damage-party! 1)
          (finish-confrontation-fail!))
        #f)))

;; ── 胜负规则（动作后统一判定）────────────────────

(define-rule "压住了场子"
  (lambda () (resolve-clk 'full?))
  (lambda () (finish-confrontation-success!)))

(define-rule "逼到墙角"
  (lambda () (press-clk 'full?))
  (lambda ()
    (damage-party! 1)
    (finish-confrontation-fail!)))

;; ── 非动手的目标线 ──────────────────────────────
;; 看破底牌：敏锐。若已经在本场抢到货单，后续看破会更容易。
(define (see-through-mods)
  (if (equal? (get-global '异常货单状态) "持有")
      (list (modifier 1 "手里有异常货单"))
      '()))

(define (node-see-through)
  (action "看破底牌"
    (list (req-die))
    (roll 'sharpness see-through-mods
      (lambda () (press-clk 'tick!))                    ; 失败
      (lambda () (resolve-clk 'tick!))                  ; 中性
      (lambda () (clock-tick-n! resolve-clk 2)))))      ; 成功

;; 谈条件：交际。谈崩了会更紧张、涨压力。
(define (node-talk)
  (action "谈条件"
    (list (req-die))
    (roll 'social
      (lambda () (press-clk 'tick!) (stress-current-actor! 1))
      (lambda () (resolve-clk 'tick!))
      (lambda () (clock-tick-n! resolve-clk 2)))))

;; 捞物资：紧张里也能顺手摸点东西，博一手。
(define (node-loot)
  (action "趁乱摸点东西"
    (list (req-die))
    (roll 'sharpness
      (lambda () (press-clk 'tick!))
      (lambda () (add-item! "金钱" 8))
      (lambda () (add-item! "金钱" 8) (add-item! "情报" 1)))))

;; 可选次要目标。取得后立刻写入 GameState，主交锋随后失败也不会丢失。
;; 每场公共交锋都可尝试，直到取得为止——准备越充分，越腾得出骰子去抢。
(define (invoice-available?)
  (equal? (get-global '异常货单状态) "未出现"))

(define (node-seize-invoice)
  (action "抢下异常货单"
    (list (req-die))
    (roll 'sharpness
      (lambda () (press-clk 'tick!))
      (lambda () (invoice-clk 'tick!))
      (lambda () (clock-tick-n! invoice-clk 2)))))

(define-rule "取得异常货单"
  (lambda () (and (invoice-available?) (invoice-clk 'full?)))
  (lambda ()
    (set-global! '异常货单状态 "持有")
    (spotlight! "异常货单" "你把散落的货单塞进怀里。上面的编号明显有问题。")))

;; ── 活法解锁的额外动作（没混到位就不出现）──────────
(define crew-used? #f)
(define pass-used? #f)
(define bribe-used? #f)
(define laozhou-used? #f)
(define laozhou-help?
  (let ((v (get-global 'laozhou-can-help))) (if v v #f)))

(define (node-call-crew)
  (action "叫码头兄弟"
    (list (req-die))
    (instant (lambda () (set! crew-used? #t) (clock-tick-n! resolve-clk 2)))))

(define (node-use-pass)
  (action "出示办案通行证"
    (list (req-item "办案通行证" 1))
    (instant
      (lambda ()
        (set! pass-used? #t)
        (set! pass-intervention? #t)
        (press-clk 'set! (max 0 (- (press-clk 'current) 2)))))))

(define (node-bribe)
  (action "塞钱消灾"
    (list (req-item "金钱" 30))
    (instant (lambda () (set! bribe-used? #t) (clock-tick-n! resolve-clk 2)))))

;; 老周真正替玩家出一次力：一次免费援助，不再完全消耗玩家自己的骰子。
(define (node-laozhou-ally)
  (container-with-clocks "老周"
    (list
      (instant-action "请老周出面"
        (lambda ()
          (set! laozhou-used? #t)
          (resolve-clk 'tick!)
          (press-clk 'set! (max 0 (- (press-clk 'current) 1))))))
    '()))

(define (extra-nodes)
  (append
    (if (and (relation-at-least? "劳工" '自己人) (not crew-used?)) (list (node-call-crew)) '())
    (if (and (> (item-count "办案通行证") 0) (not pass-used?)) (list (node-use-pass)) '())
    (if (and (>= (item-count "金钱") 30) (not bribe-used?)) (list (node-bribe)) '())
    (if (and laozhou-help? (not laozhou-used?)) (list (node-laozhou-ally)) '())))

;; ── 渲染 ────────────────────────────────────────
(define (get-render-data)
  (container-with-clocks "码头对峙"
    (append
      (list (node-see-through) (node-talk) (node-loot))
      (if (invoice-available?) (list (node-seize-invoice)) '())
      (map (lambda (e) (e 'render-data)) (live-thugs))
      (extra-nodes))
    (append
      (list (resolve-clk 'render-data) (press-clk 'render-data))
      (if (invoice-available?) (list (invoice-clk 'render-data)) '()))))
