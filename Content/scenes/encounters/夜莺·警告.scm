;; scenes/encounters/夜莺·警告.scm - 夜莺委托线·第一场
;; 结构:对质赛跑。对外契约:以 (end-encounter 'success) 或 (end-encounter 'fail) 结束。

;; ── 入口状态 ────────────────────────────────────
(define active-visit?
  (let ((v (get-global '夜莺主动上门))) (if v v #f)))

;; ── 核心时钟 ────────────────────────────────────
(define truth-clk
  (make-clock "逼他说清楚" 5 'segments
              "填满后,他说出老板、卖身约和赎身钱。这场对质成功。"))

(define patience-clk
  (make-clock "收账人失去耐心" 5 'segments
              "填满后,他不再说话,直接动手。夜莺会受惊。"))

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

;; 主动:耐心从 0 开始,且有确定筹码。被动:耐心开局 +1,夜莺在场。
(if active-visit? #f (patience-clk 'tick!))
(set-global! '夜莺已先离场 #f)

(define fake-name-used? #f)
(define bribe-used? #f)
(define nightingale-left? #f)

;; ── 结算 ────────────────────────────────────────
(define (finish-success!)
  (spotlight! "警告：账转你头上"
    (if active-visit?
        "短租屋里,收账人终于开口:邻城歌厅老板派他来的。十年卖身约,跑了七年。要么带她回去,要么一百五赎身。临走前他说:'这账从今往后算你头上。'"
        "雨巷里,你稳住了场面。收账人说出老板、卖身约和一百五赎身钱。临走前,他看着你:'这账从今往后算你头上。'"))
  (end-encounter 'success))

(define (finish-fail!)
  (damage-party! 1)
  (spotlight! "警告：被动知道真相"
    (if nightingale-left?
        "收账人不再回答。他把你推进雨水里,低声报出十年卖身约和一百五赎身钱。夜莺已经离开巷口,没有看见你倒下。"
        "收账人不再回答。他把你推进雨水里,对夜莺说:'六天后,我来收钱。十年卖身约,一百五,你知道规矩。'你知道了真相,但姿态很狼狈。"))
  (end-encounter 'fail))

;; ── 压力推进与胜负规则 ──────────────────────────
(define-turn-rule "收账人压上来"
  (lambda () #t)
  (lambda ()
    (patience-clk 'tick!)
    (if (patience-clk 'full?)
        (finish-fail!)
        #f)))

(define-rule "逼问成功"
  (lambda () (truth-clk 'full?))
  (lambda () (finish-success!)))

(define-rule "收账人失控"
  (lambda () (patience-clk 'full?))
  (lambda () (finish-fail!)))

;; ── 收账人:主目标 ────────────────────────────────
(define (node-fake-name)
  (node "点破他的假名"
    :subtitle "确定推进真相"
    :tags (list "低风险")
    :resolve (instant
      (lambda ()
        (set! fake-name-used? #t)
        (truth-clk 'tick!)))))

(define (node-question-origin)
  (node "逼问来路"
    :subtitle "逼他说出老板是谁"
    :tags (list "中风险")
    :requires (list (req-die))
    :resolve
      (roll 'social
        (lambda () (patience-clk 'tick!) (stress-current-actor! 1))
        (lambda () (truth-clk 'tick!))
        (lambda () (clock-tick-n! truth-clk 2)))))

(define (node-read-body)
  (node "读他的细节"
    :subtitle "从手、鞋和口音判断他的来路"
    :tags (list "低风险")
    :requires (list (req-die))
    :resolve
      (roll 'sharpness
        (lambda () (patience-clk 'tick!))
        (lambda () (truth-clk 'tick!))
        (lambda () (clock-tick-n! truth-clk 2)))))

;; ── 桌边:高风险改变场面 ──────────────────────────
(define (node-slam-table)
  (node "拍桌子"
    :subtitle "能吓出真话，也会让场面更快失控"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve
      (roll 'violence
        (lambda () (clock-tick-n! patience-clk 2) (stress-current-actor! 1))
        (lambda () (truth-clk 'tick!) (patience-clk 'tick!))
        (lambda () (clock-tick-n! truth-clk 2) (patience-clk 'tick!)))))

(define (node-bribe-truth)
  (node "塞钱换话"
    :subtitle "花 15 金买话；他收了钱，也更看轻你"
    :requires (list (req-item "金钱" 15))
    :resolve
      (instant
        (lambda ()
          (set! bribe-used? #t)
          (clock-tick-n! truth-clk 2)
          (patience-clk 'tick!)))))

;; ── 夜莺:被动变体的保护动作 ──────────────────────
(define (node-let-her-leave)
  (node "让她先走"
    :subtitle "花一颗骰子让她离场；失败时她不会亲眼看到"
    :tags (list "低风险")
    :requires (list (req-die))
    :resolve
      (instant
        (lambda ()
          (set! nightingale-left? #t)
          (set-global! '夜莺已先离场 #t)))))

;; ── 空间树渲染 ──────────────────────────────────
(define (node-collector)
  (container-with-clocks "收账人"
    (append
      (if (and active-visit? (not fake-name-used?)) (list (node-fake-name)) '())
      (list (node-question-origin)
            (node-read-body)))
    (list (truth-clk 'render-data)
          (patience-clk 'render-data))))

(define (node-table)
  (container "桌边的场面"
    (append
      (list (node-slam-table))
      (if (and (>= (item-count "金钱") 15) (not bribe-used?))
          (list (node-bribe-truth))
          '()))))

(define (node-nightingale)
  (container "夜莺"
    (if (not nightingale-left?)
        (list (node-let-her-leave))
        (list (observe-action "她已离开" "她先离开了巷口。现在这场对质只剩你和收账人。")))))

(define (get-render-data)
  (container "夜莺·警告"
    (append
      (list (node-collector)
            (node-table))
      (if active-visit? '() (list (node-nightingale))))))
