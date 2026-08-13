;; scenes/infiltration.scm - 保险箱潜入场景
;; 核心机制：抢时间与风险管理
;; 玩家通过博弈环境系统（断电/切断监控/搜集工具），最终打开保险箱

;; ── Local State ────────────────────────────────

;; 核心进度条
(define alert (make-clock "警戒" 6 'segments))    ; 失败条件
(define safe (make-clock "保险箱" 8 'segments))    ; 胜利条件
(define power (make-clock "断电" 4 'segments))     ; 配电房进度
(define camera (make-clock "切断监控" 3 'segments)) ; 监控室进度
(define tool-prep (make-clock "道具准备" 2 'segments)) ; 储藏间进度

;; 倒计时
(define power-timer (make-clock "备用电源" 2 'countdown))
(define camera-timer (make-clock "监控恢复" 2 'countdown))

;; 环境状态
(define search-count 0)
(define security-online? #t)
(define guard-present? #t)
(define power-off? #f)
(define camera-off? #f)
(define power-room-locked? #f)

;; ── Helpers ────────────────────────────────────

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!)
             (clock-tick-n! clock (- n 1)))
      #f))

(define (reset-scene!)
  (alert 'reset!)
  (safe 'reset!)
  (power 'reset!)
  (power-timer 'reset!)
  (camera 'reset!)
  (camera-timer 'reset!)
  (tool-prep 'reset!)
  (set! search-count 0)
  (set! security-online? #t)
  (set! guard-present? #t)
  (set! power-off? #f)
  (set! camera-off? #f)
  (set! power-room-locked? #f))

;; ── Difficulty Modifiers ───────────────────────

(define (get-safe-modifiers)
  (append
    (if (and security-online? (not power-off?))
        (list (modifier -1 "监控在线")) '())
    (if (and guard-present? (not power-off?))
        (list (modifier -1 "守卫在场")) '())))

(define (get-power-modifiers)
  (if (and security-online? (not power-off?))
      (list (modifier -1 "监控在线")) '()))

(define (get-camera-modifiers)
  (if (and security-online? (not power-off?))
      (list (modifier -1 "监控在线")) '()))

(define (get-sharpness-modifiers)
  '())

;; ── Turn Rules ─────────────────────────────────

(define-turn-rule "备用电源倒计时"
  (lambda () power-off?)
  (lambda ()
    (power-timer 'tick!)
    (if (power-timer 'full?)
        (begin
          (clock-tick-n! alert 4)
          (set! power-off? #f)
          (set! security-online? (not camera-off?))
          (set! guard-present? #t)
          (set! power-room-locked? #t)
          (power-timer 'reset!))
        #f)))

(define-turn-rule "监控恢复倒计时"
  (lambda () camera-off?)
  (lambda ()
    (camera-timer 'tick!)
    (if (camera-timer 'full?)
        (begin
          (set! camera-off? #f)
          (set! security-online? (not power-off?))
          (camera-timer 'reset!))
        #f)))

;; ── Action Rules ───────────────────────────────

(define-rule "警戒满失败"
  (lambda () (alert 'full?))
  (lambda ()
    (injure!)
    (reset-scene!)
    (end-encounter)))

(define-rule "保险箱开启"
  (lambda () (safe 'full?))
  (lambda ()
    (set-global! 'infiltration-complete? #t)
    (end-encounter)))

(define-rule "道具准备完成"
  (lambda () (tool-prep 'full?))
  (lambda ()
    (add-item! '道具 2)
    (tool-prep 'reset!)))


;; ── Hall Nodes ─────────────────────────────────

(define (node-hall-crack)
  (action "打开保险箱"
    (list (req-die))
    (roll 'knowledge
      get-safe-modifiers
      (lambda ()
        (alert 'tick!)
        (spend-composure! 1))            ; 失败: +1警戒，冷静 -1
      (lambda () (safe 'tick!))               ; 中性: +1保险箱
      (lambda () (clock-tick-n! safe 2)))))   ; 成功: +2保险箱

(define (node-hall-tool)
  (action "工具辅助保险箱"
    (list (req-item '道具 1))
    (instant (lambda () (safe 'tick!)))))

;; ── Power Room Nodes ───────────────────────────

(define (node-power-cut)
  (action "断电"
    (list (req-die))
    (roll 'knowledge
      get-power-modifiers
      (lambda ()
        (alert 'tick!)
        (spend-composure! 1))              ; 失败: +1警戒，冷静 -1
      (lambda () (power 'tick!))                ; 中性: +1断电
      (lambda ()
        (clock-tick-n! power 2)                  ; 成功: +2断电
        (if (power 'full?)
            (begin
              (set! power-off? #t)
              (set! security-online? #f)
              (set! guard-present? #f))
            #f)))))

(define (node-power-tool)
  (action "工具辅助断电"
    (list (req-item '道具 1))
    (instant (lambda ()
               (power 'tick!)
               (if (power 'full?)
                   (begin
                     (set! power-off? #t)
                     (set! security-online? #f)
                     (set! guard-present? #f))
                   #f)))))

;; ── Camera Room Nodes ──────────────────────────

(define (node-camera-cut)
  (action "切断监控"
    (list (req-die))
    (roll 'knowledge
      get-camera-modifiers
      (lambda ()
        (alert 'tick!)
        (spend-composure! 1))              ; 失败: +1警戒，冷静 -1
      (lambda () (camera 'tick!))               ; 中性: +1监控
      (lambda ()
        (clock-tick-n! camera 2)                 ; 成功: +2监控
        (if (camera 'full?)
            (begin
              (alert 'tick!)
              (alert 'tick!)                      ; 切断瞬间: +2警戒
              (set! camera-off? #t)
              (set! security-online? #f))
            #f)))))

(define (node-camera-tool)
  (action "工具辅助切断监控"
    (list (req-item '道具 1))
    (instant (lambda ()
               (camera 'tick!)
               (if (camera 'full?)
                   (begin
                     (alert 'tick!)
                     (alert 'tick!)
                     (set! camera-off? #t)
                     (set! security-online? #f))
                   #f)))))

;; ── Storage Room Nodes ─────────────────────────

(define (node-tool-search)
  (action "寻找道具"
    (list (req-die))
    (roll 'sharpness
      get-sharpness-modifiers
      (lambda ()
        (alert 'tick!)
        (spend-composure! 1))              ; 失败: +1警戒，冷静 -1
      (lambda () (tool-prep 'tick!))            ; 中性: +1道具
      (lambda () (clock-tick-n! tool-prep 2))))) ; 成功: +2道具

(define (node-search-money)
  (action "翻找零钱"
    (list (req-die))
    (roll 'sharpness
      get-sharpness-modifiers
      (lambda ()
        (set! search-count (+ search-count 1))
        (alert 'tick!)
        (spend-composure! 1))              ; 失败: +1警戒，冷静 -1
      (lambda ()
        (set! search-count (+ search-count 1))
        (add-item! '金钱 5))
      (lambda ()
        (set! search-count (+ search-count 1))
        (add-item! '金钱 10))))) ; 成功: +10金钱

;; ── Render Data ────────────────────────────────

(define (node-power-room)
  (container-with-clocks "配电房"
    (if (not power-room-locked?)
        (list (node-power-cut) (node-power-tool))
        (list (observe-action "配电柜" "配电柜已被备用电源锁死，无法再次操作。")))
    (append
      (list (alert 'render-data) (power 'render-data))
      (if power-off? (list (power-timer 'render-data)) '()))))

(define (node-camera-room)
  (container-with-clocks "监控室"
    (if (not camera-off?)
        (list (node-camera-cut) (node-camera-tool))
        (list (observe-action "监控控制台" "监控系统已被切断，处于离线状态。")))
    (append
      (list (alert 'render-data) (camera 'render-data))
      (if camera-off? (list (camera-timer 'render-data)) '()))))

(define (node-storage)
  (container-with-clocks "道具储藏间"
    (append
      (list (node-tool-search))
      (if (< search-count 4)
          (list (node-search-money))
          (list (observe-action "零钱" "你已经翻找过这里，没什么剩下的了。"))))
    (list (alert 'render-data) (tool-prep 'render-data))))

(define (get-render-data)
  (container "大厅"
    (append
      (clock-nodes (alert 'render-data) (safe 'render-data))
      (list
      (node-hall-crack)
      (node-hall-tool)
      (node-power-room)
      (node-camera-room)
      (node-storage)))))
