;; R13：完成前置主动开启当前回合的后续窗口。
(set-actor-composure! 'player 5)
(define 完成? #f)
(define 前置 (make-clock "前置" 3 'gauge "进度跨回合保留；完成立即开启后续。"))
(define 后续 (make-clock "后续" 4 'gauge (if 限定窗口? "开启后须在本回合完成。" "开启后仍可跨回合保留。")))
(define 期限 (make-clock "已过回合" 3 'gauge "第三次结束回合到期；每次另花1冷静。"))
(define (开窗结果 status)
  (list status (前置 'current) (后续 'current) (期限 'current) (actor-composure 'player)))
(define (开窗结束! status text)
  (if 完成? (error "启动窗口：重复结算") #t)
  (set! 完成? #t)
  (spotlight! 研究名称 text)
  (end-encounter (开窗结果 status)))
(define (开窗推进! n)
  (if (or 完成? (hospitalization-pending?)) #f
    (if (前置 'full?)
      (begin (后续 'advance! n)
        (if (后续 'full?) (开窗结束! 'success "前置与后续都已完成。") #f))
      (begin (前置 'advance! n)
        (if (前置 'full?)
          (spotlight! "后续已开启" (if 限定窗口? "本回合结束前达到4进度；否则窗口关闭。" "后续进度也可跨回合保留。")) #f)))))
(define (开窗坏!) (spend-composure! 1))
(define (开窗风险中!) (spend-composure! 1) (开窗推进! 1))
(define-opponent-rule "窗口与期限"
  (lambda () (not 完成?))
  (lambda ()
    (期限 'advance! 1)
    (cond
      ((and 限定窗口? (前置 'full?)) (开窗结束! 'window-expired "回合结束，后续窗口关闭。"))
      ((期限 'full?) (开窗结束! 'timeout "三回合已过，工作未完成。")))))
(define (on-encounter-collapse) (collapse-result (开窗结果 'collapse)))
(define (get-render-data)
  (container 研究名称
    (append
      (list
        (note-node "标注：启动规则" "规则实验"
          (if 限定窗口?
            "三回合内完成前置3与后续4。前置保留，完成立即开窗；后续必须本回合做完。多出的前置进度不转入后续。"
            "三回合内先完成前置3，再完成后续4。两段都保留；多出的前置进度不转入后续。"))
        (node (if (前置 'full?) "后续稳做" "前置稳做")
          :subtitle "坏：冷静−1；中：无变化；好：进度+1。"
          :requires (list (req-die))
          :resolve (roll 'knowledge
            (outcome 开窗坏!) (outcome (lambda () #f)) (outcome (lambda () (开窗推进! 1)))))
        (node (if (前置 'full?) "后续快做" "前置快做")
          :subtitle "坏：冷静−1；中：冷静−1、进度+1；好：进度+2。"
          :requires (list (req-die))
          :resolve (roll 'knowledge
            (outcome 开窗坏!) (outcome 开窗风险中!) (outcome (lambda () (开窗推进! 2))))))
      (clock-nodes (前置 'render-data) (后续 'render-data) (期限 'render-data)))))
