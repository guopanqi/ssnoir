;; scenes/encounters/追击黑衣人.scm
;; 双时钟赛跑：追击进度(10) vs 逃脱倒计时(6)
;; 每回合结束敌人自动 +1；玩家用行动骰推进追击进度
;; 追击进度先满 → success；逃脱先满 → fail

;; ── Clocks ─────────────────────────────────────
(define chase  (make-clock "追击进度"   6 'segments))
(define escape (make-clock "逃脱倒计时"  6 'countdown))

;; ── Helpers ────────────────────────────────────
(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

;; 推进追击进度，满则胜利
(define (chase-tick! n)
  (clock-tick-n! chase n)
  (if (chase 'full?)
      (begin
        (notify! "你一把揪住了黑衣人！")
        (end-encounter 'success))
      #f))

;; 推进敌人逃脱，满则失败
(define (enemy-tick!)
  (escape 'tick!)
  (if (escape 'full?)
      (begin
        (notify! "黑衣人消失在人群中，线索断了。")
        (end-encounter 'fail))
      #f))

;; ── Turn Rule ──────────────────────────────────
(define-turn-rule "黑衣人奔跑"
  (lambda () #t)
  (lambda ()
    (notify! "黑衣人加速逃跑！")
    (enemy-tick!)))

;; ── Actions ────────────────────────────────────

;; 安全 —— 全力奔跑（violence）
;; fail: +0  neutral: +1  success: +1
(define (node-sprint)
  (action "全力奔跑"
    (list (req-die))
    (roll 'violence
      (lambda () '())
      (lambda ()
        (notify! "脚下打滑，没能追上。"))
      (lambda ()
        (chase-tick! 1)
        (notify! "稳步追赶，拉近了一些距离。"))
      (lambda ()
        (chase-tick! 1)
        (notify! "全力冲刺！明显逼近了！")))))

;; 中险 —— 翻越障碍（violence）
;; fail: +0 + 敌人+1  neutral: +1  success: +2
(define (node-vault)
  (action "翻越障碍"
    (list (req-die))
    (roll 'violence
      (lambda () '())
      (lambda ()
        (enemy-tick!)
        (notify! "翻越失败，黑衣人趁机拉开了距离！"))
      (lambda ()
        (chase-tick! 1)
        (notify! "勉强翻过，没有落后。"))
      (lambda ()
        (chase-tick! 2)
        (notify! "一跃而过，迅速逼近！")))))

;; 高险 —— 抄小巷（sharpness）
;; fail: 敌人+1 + 压力+1  neutral: +1  success: +3
(define (node-shortcut)
  (action "抄小巷"
    (list (req-die))
    (roll 'sharpness
      (lambda () '())
      (lambda ()
        (enemy-tick!)
        (stress-current-actor! 1)
        (notify! "死路！黑衣人跑远了，你喘着粗气。"))
      (lambda ()
        (chase-tick! 1)
        (notify! "小巷穿过，没有优势，但没有落后。"))
      (lambda ()
        (chase-tick! 2)
        (notify! "完美抄截！黑衣人就在眼前！")))))

;; ── Render ─────────────────────────────────────
(define (get-render-data)
  (list
    (container-with-clocks "追击途中"
      (list
        (node-sprint)
        (node-vault)
        (node-shortcut))
      (list
        (chase  'render-data)
        (escape 'render-data)))))
