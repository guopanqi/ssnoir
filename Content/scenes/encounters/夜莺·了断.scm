;; scenes/encounters/夜莺·了断.scm - 夜莺委托线·第三场
;; 对外契约:以 (end-encounter 'success) 或 (end-encounter 'fail) 结束。

;; ── 读镜像 ───────────────────────────────────────
(define nightingale-condition (let ((v (get-global '夜莺状态等级))) (if v v 0)))
(define nightingale-protection (let ((v (get-global '夜莺保护方案))) (if v v "无")))
(define installment-paid? (let ((v (get-global '夜莺已付首期))) (if v v #f)))
(define ransom-due (if installment-paid? 90 150))
(define crew-threshold (if (equal? nightingale-protection "码头") '脸熟 '自己人))

;; ── 核心时钟 ────────────────────────────────────
(define resolve-clk
  (make-clock "做个了断" 6 'segments
              "填满即可让这笔账一笔勾销。"))

(define press-clk
  (let ((base-max 5))
    (make-clock "收账人施压"
                (if (>= nightingale-condition 2) (- base-max 1) base-max)
                'segments
                "失手,她会被带走。")))

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
  (spotlight! "了断：自由" "收账人退了。雨停后,她第一次唱完整的一场。")
  (end-encounter 'success))

(define (finish-fail!)
  (spotlight! "了断：她跟他们走了" "她自己走出去,换你一条命。门在她身后关上。")
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
(define-rule "了断成功"
  (lambda () (resolve-clk 'full?))
  (lambda () (finish-success!)))

(define-rule "了断失败"
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
(define laozhou-used? #f)
(define laozhou-help?
  (let ((v (get-global 'laozhou-can-help))) (if v v #f)))

(define (node-pay-ransom)
  (node "付清赎身钱"
    :subtitle (if installment-paid?
                  (string-append "首期他们认了账,尾款只要 " (number->string ransom-due))
                  (string-append "交清 " (number->string ransom-due) " 金,买一个人的自由"))
    :requires (list (req-item "金钱" ransom-due))
    :resolve (instant
      (outcome "钱买的不是道具"
        (if installment-paid?
            "尾款拍在桌上。首期他们认过账,这一次买断的是剩下的自由。"
            "你把赎身钱拍在桌上。这一回,钱买的是一个人的自由。")
        (lambda () (finish-success!))
        'light))))

(define (node-call-crew)
  (node "码头兄弟到场"
    :subtitle (if (equal? nightingale-protection "码头")
                  "他们看过一次场,认这件事"
                  "码头的自己人,替你扛一轮")
    :requires (list (req-die))
    :resolve (instant (lambda () (set! crew-used? #t) (clock-tick-n! resolve-clk 2)))))

(define (node-use-pass)
  (action "程序干预"
    (list (req-item "办案通行证" 1))
    (instant
      (lambda ()
        (set! pass-used? #t)
        (set! pass-intervention? #t)
        (press-clk 'set! (max 0 (- (press-clk 'current) 2)))))))

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
    (if (>= (item-count "金钱") ransom-due) (list (node-pay-ransom)) '())
    (if (and (relation-at-least? "劳工" crew-threshold) (not crew-used?)) (list (node-call-crew)) '())
    (if (and (> (item-count "办案通行证") 0) (not pass-used?)) (list (node-use-pass)) '())
    (if (and laozhou-help? (not laozhou-used?)) (list (node-laozhou-ally)) '())))

;; ── 渲染 ─────────────────────────────────────────
(define (get-render-data)
  (container-with-clocks "夜莺·了断"
    (append
      (list (node-see-through) (node-talk) (node-loot))
      (map (lambda (e) (e 'render-data)) (live-thugs))
      (extra-nodes))
    (list (resolve-clk 'render-data) (press-clk 'render-data))))
