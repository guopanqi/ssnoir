;; R07：共用惯用收益，只改变目标之间的结算关系。
(define 完成? #f)
(define 目标A (make-clock "目标A" A上限 'gauge (if (eq? 研究模式 '收尾) "必需目标；完成就结束交锋。" "完成保留；未完成部分按本场规则结算。")))
(define 目标B (make-clock "目标B" B上限 'gauge (if (eq? 研究模式 '收尾) "额外目标；必须在A完成之前处理。" "完成保留；未完成部分按本场规则结算。")))
(define 期限 (make-clock "已过回合" 回合上限 'gauge "回合末加1；引擎另花1冷静。"))
(define (顺序结果 status)
  (list status (目标A 'current) (目标B 'current)
    (min 回合上限 (+ 1 (期限 'current))) (actor-composure 'player)))
(define (顺序结束! status text)
  (if 完成? (error "顺序研究：重复结算") #t)
  (set! 完成? #t)
  (spotlight! 研究名称 text)
  (end-encounter (顺序结果 status)))
(define (顺序检查!)
  (if (or 完成? (hospitalization-pending?)) #f
    (cond
      ((and (目标A 'full?) (or (eq? 研究模式 '收尾) (目标B 'full?)))
        (顺序结束! 'success
          (if (and (eq? 研究模式 '收尾) (目标B 'full?)) "必需与额外目标都完成。"
            (if (eq? 研究模式 '收尾) "必需目标完成，额外目标的机会已经结束。" "两个目标都完成。"))))
      ((期限 'full?) (顺序结束! 'timeout "期限已到，目标未完成。")))))
(define (顺序推进! 钟 n)
  (if (or 完成? (hospitalization-pending?)) #f
    (begin (钟 'advance! n) (顺序检查!))))
(define (顺序风险中! 钟)
  (spend-composure! 1)
  (顺序推进! 钟 1))
(define (顺序坏!) (spend-composure! 1))
(define (顺序动作 前缀 钟)
  (if (钟 'full?) '()
    (list
      (node (string-append "稳做" 前缀) :subtitle "坏：冷静−1；中：无变化；好：进度+1。"
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (outcome 顺序坏!) (outcome (lambda () #f))
          (outcome (lambda () (顺序推进! 钟 1)))))
      (node (string-append "快做" 前缀) :subtitle "坏：冷静−1；中：冷静−1、进度+1；好：进度+2。"
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (outcome 顺序坏!) (outcome (lambda () (顺序风险中! 钟)))
          (outcome (lambda () (顺序推进! 钟 2))))))))
(define-opponent-rule "成果验收"
  (lambda () (not 完成?))
  (lambda ()
    (if (and (eq? 研究模式 '分段) 半成品清零)
      (begin
        (if (目标A 'full?) #f (目标A 'reset!))
        (if (目标B 'full?) #f (目标B 'reset!))) #f)
    (期限 'advance! 1)
    (顺序检查!)))
(define (on-encounter-collapse) (collapse-result (顺序结果 'collapse)))
(define (get-render-data)
  (container 研究名称
    (append
      (list (note-node "标注：顺序规则" "规则实验"
        (if (eq? 研究模式 '收尾)
          "A是必需目标，完成A立即结束。B是额外目标，必须在A完成前处理。本场一回合；两个都完成算额外成果。"
          (if 半成品清零
            (string-append "A需" (number->string A上限) "进度，B需" (number->string B上限) "进度。回合末，未完成的目标归零；完成保留。两回合内完成两项。")
            (string-append "A需" (number->string A上限) "进度，B需" (number->string B上限) "进度。所有进度跨回合保留。两回合内完成两项。")))))
      (clock-nodes (目标A 'render-data) (目标B 'render-data) (期限 'render-data))
      (顺序动作 "A" 目标A) (顺序动作 "B" 目标B))))
