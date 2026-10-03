;; R15：不同准备值与目标距离改变骰子的边际用途；不修改永久能力。
(set-actor-composure! 'player 5)
(define 完成? #f)
(define 工作甲 (make-clock "工作甲" 3 'gauge "完成需要3；无额外判定修正。"))
(define 工作乙 (make-clock "工作乙" 3 'gauge
  (if (> 熟练加成 0) "完成需要3；判定额外+1。" "完成需要3；无额外判定修正。")))
(define (分配结果 status) (list status (工作甲 'current) (工作乙 'current) (actor-composure 'player)))
(define (分配结束! status text)
  (if 完成? (error "骰子分配：重复结算") #t)
  (set! 完成? #t)
  (spotlight! 研究名称 text)
  (end-encounter (分配结果 status)))
(define (分配推进! clock n)
  (if (or 完成? (hospitalization-pending?)) #f
    (begin (clock 'advance! n)
      (if (and (工作甲 'full?) (工作乙 'full?))
        (分配结束! 'success "两项工作都已完成。") #f))))
(define (分配坏!) (spend-composure! 1))
(define (分配风险中! clock) (spend-composure! 1) (分配推进! clock 1))
(define (分配动作 clock name fast? bonus)
  (node name
    :subtitle (if fast? "坏：冷静−1；中：冷静−1、进度+1；好：进度+2。" "坏：冷静−1；中：无变化；好：进度+1。")
    :requires (list (req-die))
    :resolve (roll 'knowledge (lambda () (if (> bonus 0) (list (modifier bonus "熟练")) '()))
      (outcome 分配坏!)
      (outcome (lambda () (if fast? (分配风险中! clock) #f)))
      (outcome (lambda () (分配推进! clock (if fast? 2 1)))))))
(define-opponent-rule "单回合到期" (lambda () (not 完成?))
  (lambda () (分配结束! 'timeout "回合结束，还有工作未完成。")))
(define (on-encounter-collapse) (collapse-result (分配结果 'collapse)))
(define (get-render-data)
  (container 研究名称
    (append
      (list (note-node "标注：分配规则" "规则实验"
        (if (> 熟练加成 0)
          "一回合完成两项各3格的工作，完成立即结束。乙每次判定+1；其余收益相同。结束回合另花1冷静。"
          "一回合完成两项各3格的工作，完成立即结束。两项判定相同。结束回合另花1冷静。")))
      (clock-nodes (工作甲 'render-data) (工作乙 'render-data))
      (if (工作甲 'full?) '() (list (分配动作 工作甲 "甲稳做" #f 0) (分配动作 工作甲 "甲快做" #t 0)))
      (if (工作乙 'full?) '() (list (分配动作 工作乙 "乙稳做" #f 熟练加成) (分配动作 工作乙 "乙快做" #t 熟练加成))))))
