;; scenes/encounters/找上门的人.scm - 码头对峙（委托合同式交锋）
;; 码头上的麻烦找上了门。交锋约复发 3 次、逐次升级。
;;
;; 委托合同结构（参考 combat.scm / infiltration.scm）：
;;   胜利 = 填满「压住场子」(resolve-clk)；失败 = 「对方逼近」(press-clk) 满。
;;   多条路推进 resolve：压制打手(力量) / 看破底牌(敏锐) / 谈条件(交际)。
;;   活法解锁额外动作：劳工自己人→叫兄弟；官僚脸熟→亮官面；有钱→塞钱；老周好感够→老周增援。
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

;; ── 压力：每回合按在场打手数递进 ──────────────────
(define-turn-rule "对方逼近"
  (lambda () #t)
  (lambda ()
    (clock-tick-n! press-clk (length (live-thugs)))
    (if (press-clk 'full?)
        (begin
          (damage-party! 1)
          (end-encounter 'fail))
        #f)))

;; ── 胜负规则（动作后统一判定）────────────────────

(define-rule "压住了场子"
  (lambda () (resolve-clk 'full?))
  (lambda () (end-encounter 'success)))

(define-rule "逼到墙角"
  (lambda () (press-clk 'full?))
  (lambda ()
    (damage-party! 1)
    (end-encounter 'fail)))

;; ── 非动手的目标线 ──────────────────────────────
;; 看破底牌：敏锐。手里有「货单对不上」时更容易奏效。
(define (see-through-mods)
  (if (get-global "货单对不上") (list (modifier 1 "手里有把柄")) '()))

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

;; ── 活法解锁的额外动作（没混到位就不出现）──────────
(define crew-used? #f)
(define badge-used? #f)
(define bribe-used? #f)
(define laozhou-help?
  (let ((v (get-global 'laozhou-can-help))) (if v v #f)))

(define (node-call-crew)
  (action "叫码头兄弟"
    (list (req-die))
    (instant (lambda () (set! crew-used? #t) (clock-tick-n! resolve-clk 2)))))

(define (node-flash-badge)
  (action "亮官面关系"
    (list (req-die))
    (instant (lambda ()
               (set! badge-used? #t)
               (press-clk 'set! (max 0 (- (press-clk 'current) 3)))))))

(define (node-bribe)
  (action "塞钱消灾"
    (list (req-item "金钱" 30))
    (instant (lambda () (set! bribe-used? #t) (clock-tick-n! resolve-clk 2)))))

;; 老周盟友单位：好感够高才来，有自己的发难 / 掩护行动。
(define (node-laozhou-ally)
  (container-with-clocks "老周"
    (list
      (action "让老周发难"
        (list (req-die))
        (roll 'violence
          (lambda () #f)
          (lambda () (resolve-clk 'tick!))
          (lambda () (clock-tick-n! resolve-clk 2))))
      (action "让老周掩护"
        (list (req-die))
        (instant (lambda () (press-clk 'set! (max 0 (- (press-clk 'current) 2)))))))
    '()))

(define (extra-nodes)
  (append
    (if (and (relation-at-least? "劳工" '自己人) (not crew-used?)) (list (node-call-crew)) '())
    (if (and (relation-at-least? "官僚" '脸熟) (not badge-used?)) (list (node-flash-badge)) '())
    (if (and (>= (item-count "金钱") 30) (not bribe-used?)) (list (node-bribe)) '())
    (if laozhou-help? (list (node-laozhou-ally)) '())))

;; ── 渲染 ────────────────────────────────────────
(define (get-render-data)
  (container-with-clocks "码头对峙"
    (append
      (list (node-see-through) (node-talk) (node-loot))
      (map (lambda (e) (e 'render-data)) (live-thugs))
      (extra-nodes))
    (list (resolve-clk 'render-data) (press-clk 'render-data))))
