;; scenes/encounters/夜莺·抢人.scm - 夜莺委托线·第二场
;; 对外契约:以 (end-encounter 'success) 或 (end-encounter 'fail) 结束。

;; ── 核心时钟 ────────────────────────────────────
(define resolve-clk
  (make-clock "守住她" 6 'segments
              "填满即可把他们挡在酒馆外。"))

(define press-clk
  (make-clock "对方抢人" 5 'segments
              "失手会让她受伤,酒馆也会被砸停。"))

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

;; ── 敌人单位 ─────────────────────────────────────
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
                 (lambda () (press-clk 'tick!))
                 (lambda () (set! hp (- hp 1)) (resolve-clk 'tick!))
                 (lambda () (set! hp (- hp 2)) (clock-tick-n! resolve-clk 2)))))
           (list (list 'clock "打手" (if (< hp 0) 0 hp) hp-max 'segments))))
        (else #f)))))

(define thugs (list (make-thug 3) (make-thug 3)))

(define (live-thugs)
  (filter (lambda (e) (not (e 'dead?))) thugs))

(define pass-intervention? #f)

(define (finish-success!)
  (spotlight! "抢人：挡住" "他们又退了。这一次砸碎了灯,也砸碎了退路。")
  (end-encounter 'success))

(define (finish-fail!)
  (spotlight! "抢人：失守" "他们拖走了她。你追出去时,只看见马车尾灯消失在雨里。")
  (end-encounter 'fail))

;; ── 压力：每回合按在场打手数递进 ──────────────────
(define-turn-rule "对方逼近"
  (lambda () #t)
  (lambda ()
    (if pass-intervention?
        (set! pass-intervention? #f)
        (clock-tick-n! press-clk (length (live-thugs))))
    (if (press-clk 'full?)
        (begin
          (damage-party! 1)
          (finish-fail!))
        #f)))

;; ── 胜负规则 ─────────────────────────────────────
(define-rule "守住"
  (lambda () (resolve-clk 'full?))
  (lambda () (finish-success!)))

(define-rule "失守"
  (lambda () (press-clk 'full?))
  (lambda ()
    (damage-party! 1)
    (finish-fail!)))

;; ── 非动手目标线 ─────────────────────────────────
(define (node-see-through)
  (action "看破底牌"
    (list (req-die))
    (roll 'sharpness
      (lambda () (press-clk 'tick!))
      (lambda () (resolve-clk 'tick!))
      (lambda () (clock-tick-n! resolve-clk 2)))))

(define (node-talk)
  (action "谈条件"
    (list (req-die))
    (roll 'social
      (lambda () (press-clk 'tick!) (stress-current-actor! 1))
      (lambda () (resolve-clk 'tick!))
      (lambda () (clock-tick-n! resolve-clk 2)))))

(define (node-loot)
  (action "趁乱摸点东西"
    (list (req-die))
    (roll 'sharpness
      (lambda () (press-clk 'tick!))
      (lambda () (add-item! "金钱" 8))
      (lambda () (add-item! "金钱" 8) (add-item! "情报" 1)))))

;; ── 活法解锁的额外动作 ───────────────────────────
(define crew-used? #f)
(define pass-used? #f)
(define bribe-used? #f)
(define laozhou-used? #f)
(define laozhou-help?
  (let ((v (get-global 'laozhou-can-help))) (if v v #f)))

(define (node-call-crew)
  (action "码头兄弟到场"
    (list (req-die))
    (instant (lambda () (set! crew-used? #t) (clock-tick-n! resolve-clk 2)))))

(define (node-use-pass)
  (action "程序干预"
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

;; ── 渲染 ─────────────────────────────────────────
(define (get-render-data)
  (container-with-clocks "夜莺·抢人"
    (append
      (list (node-see-through) (node-talk) (node-loot))
      (map (lambda (e) (e 'render-data)) (live-thugs))
      (extra-nodes))
    (list (resolve-clk 'render-data) (press-clk 'render-data))))
