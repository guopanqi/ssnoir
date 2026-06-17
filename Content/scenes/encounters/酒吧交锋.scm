;; scenes/encounters/酒吧交锋.scm
;; 外来人在码头酒吧打听消息。
;; 双时钟赛跑：打听进度(4) vs 气氛紧张(4)。
;; 每回合气氛自动 +1；打听满先 → success；气氛满先 → fail。

;; ── Clocks ─────────────────────────────────────
(define info    (make-clock "打听进度" 4 'segments))
(define tension (make-clock "气氛紧张" 4 'segments))

;; ── Helpers ────────────────────────────────────
(define (tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (tick-n! clock (- n 1)))
      #f))

(define (info-tick! n)
  (tick-n! info n)
  (if (info 'full?)
      (begin
        (notify! "你终于找对了人，对方压低声音说了几句关键的话。")
        (end-encounter 'success))
      #f))

(define (tension-tick! n)
  (tick-n! tension n)
  (if (tension 'full?)
      (begin
        (notify! "气氛彻底崩了，几个大汉站起来。你只好夺门而出。")
        (end-encounter 'fail))
      #f))

;; ── Turn Rule ──────────────────────────────────
(define-turn-rule "气氛升温"
  (lambda () #t)
  (lambda ()
    (tension-tick! 1)))

;; ── Actions ────────────────────────────────────
(define (node-probe)
  (action "旁敲侧击"
    (list (req-die))
    (roll 'sharpness
      (lambda ()
        (tension-tick! 1)
        (notify! "话题被岔开了，对方警觉地看了你一眼。"))
      (lambda ()
        (info-tick! 1)
        (notify! "对方聊了几句，没透露太多，但有点用。"))
      (lambda ()
        (info-tick! 2)
        (notify! "对方话匣子打开了，说了不少。")))))

(define (node-buy-round)
  (action "请一轮酒"
    (list (req-item "金钱" 15))
    (instant (lambda ()
      (info-tick! 1)
      (if (> (tension 'current) 0)
          (tension 'set! (- (tension 'current) 1))
          #f)
      (notify! "递上一轮酒，气氛稍稍缓和，有人多说了几句。")))))

;; ── Render ─────────────────────────────────────
(define (get-render-data)
  (container-with-clocks "酒吧"
    (list
      (node-probe)
      (node-buy-round))
    (list
      (info    'render-data)
      (tension 'render-data))))
