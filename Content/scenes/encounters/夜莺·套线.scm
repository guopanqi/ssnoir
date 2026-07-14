;; scenes/encounters/夜莺·套线.scm - 夜莺委托线·警局保护支线
;; 替探长弄到能按住收账人的把柄——套话、盯梢、取物,做他碍于身份不便亲自出面的事。
;; 对外契约:(end-encounter 'success) / (end-encounter 'fail)。成功后由 world 回调发把柄。

;; ── 核心时钟 ────────────────────────────────────
(define leverage-clk
  (make-clock "拿到把柄" 5 'segments
              "填满即拿到能钉住收账人的把柄,这趟成功。"))

(define heat-clk
  (make-clock "引起警觉" 5 'segments
              "填满,对方察觉了你的来意,把柄到不了手。"))

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

;; ── 结算 ────────────────────────────────────────
(define (finish-success!)
  (spotlight! "到手的把柄"
    "抽屉最底下,那张纸压在账本下面——收账人经手的脏事,白纸黑字。你把它收进怀里。剩下的,是探长的活儿了。")
  (end-encounter 'success))

(define (finish-fail!)
  (spotlight! "打草惊蛇"
    "对方的眼神变了。你还没碰到那样东西,风声已经先一步传了出去。这条路,今晚走到头了。")
  (end-encounter 'fail))

;; ── 时间压力与胜负规则 ──────────────────────────
(define-turn-rule "对方起疑"
  (lambda () #t)
  (lambda ()
    (heat-clk 'tick!)
    (if (heat-clk 'full?)
        (finish-fail!)
        #f)))

(define-rule "把柄到手"
  (lambda () (leverage-clk 'full?))
  (lambda () (finish-success!)))

(define-rule "警觉拉满"
  (lambda () (heat-clk 'full?))
  (lambda () (finish-fail!)))

;; ── 动作 ─────────────────────────────────────────
(define (node-sweet-talk)
  (node "套话"
    :subtitle "跟短租屋的老主顾攀谈,套出他的落脚细节"
    :tags (list "中风险")
    :requires (list (req-die))
    :resolve
      (roll 'social
        (lambda () (heat-clk 'tick!) (stress-current-actor! 1))
        (lambda () (leverage-clk 'tick!))
        (lambda () (clock-tick-n! leverage-clk 2)))))

(define (node-case-joint)
  (node "盯梢摸底"
    :subtitle "从进出、习惯和门锁,判断东西藏在哪"
    :tags (list "低风险")
    :requires (list (req-die))
    :resolve
      (roll 'sharpness
        (lambda () (heat-clk 'tick!))
        (lambda () (leverage-clk 'tick!))
        (lambda () (clock-tick-n! leverage-clk 2)))))

(define (node-snatch)
  (node "趁隙动手"
    :subtitle "撬开抽屉直接取物,快,但极容易惊动人"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve
      (roll 'sharpness
        (lambda () (clock-tick-n! heat-clk 2) (stress-current-actor! 1))
        (lambda () (leverage-clk 'tick!) (heat-clk 'tick!))
        (lambda () (clock-tick-n! leverage-clk 2) (heat-clk 'tick!)))))

;; 亮通行证:半真半假地摆出官面身份,压一压对方的警觉。用掉通行证。
(define (node-flash-pass)
  (node "亮一亮办案通行证"
    :subtitle "官面上的身份能唬住人,把对方的疑心压下去"
    :requires (list (req-item "办案通行证" 1))
    :resolve (instant
      (lambda ()
        (heat-clk 'set! (max 0 (- (heat-clk 'current) 2)))))))

;; ── 渲染 ─────────────────────────────────────────
(define (get-render-data)
  (container-with-clocks "夜莺·套线"
    (append
      (list (node-sweet-talk) (node-case-joint) (node-snatch))
      (if (> (item-count "办案通行证") 0) (list (node-flash-pass)) '()))
    (list (leverage-clk 'render-data) (heat-clk 'render-data))))
