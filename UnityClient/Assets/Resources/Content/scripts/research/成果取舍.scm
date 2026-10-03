;; R13：完成才计成果点；不向库存发放奖励。
(set-actor-composure! 'player 5)
(define 完成? #f)
(define 小成果 (make-clock "小成果" 3 'gauge "完成值1成果点；半成品0。"))
(define 大成果 (make-clock "大成果" 6 'gauge "完成值2成果点；半成品0。"))
(define (成果点) (+ (if (小成果 'full?) 1 0) (if (大成果 'full?) 2 0)))
(define (成果结果 status) (list status (小成果 'current) (大成果 'current) (成果点) (actor-composure 'player)))
(define (成果结束!)
  (if 完成? (error "成果取舍：重复结算") #t)
  (set! 完成? #t)
  (spotlight! "成果结算" (string-append "已完成成果计" (number->string (成果点)) "点，半成品不计。"))
  (end-encounter (成果结果 'settled)))
(define (成果推进! clock n)
  (if (or 完成? (hospitalization-pending?)) #f
    (begin
      (clock 'advance! n)
      (if (clock 'full?) (spotlight! "成果已完成" "已完成的成果保留，到回合结束时结算。") #f))))
(define (成果坏!) (spend-composure! 1))
(define (成果风险中! clock) (spend-composure! 1) (成果推进! clock 1))
(define-opponent-rule "到期结算" (lambda () (not 完成?)) 成果结束!)
(define (on-encounter-collapse) (collapse-result (成果结果 'collapse)))
(define (成果动作 clock name fast?)
  (node name
    :subtitle (if fast? "坏：冷静−1；中：冷静−1、进度+1；好：进度+2。" "坏：冷静−1；中：无变化；好：进度+1。")
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome 成果坏!)
      (outcome (lambda () (if fast? (成果风险中! clock) #f)))
      (outcome (lambda () (成果推进! clock (if fast? 2 1)))))))
(define (get-render-data)
  (container "成果取舍"
    (append
      (list (note-node "标注：成果规则" "规则实验"
        "本回合结束结算。小成果3格值1，大成果6格值2，半成品0。单人四骰无法全拿。结束回合另花1冷静。"))
      (clock-nodes (小成果 'render-data) (大成果 'render-data))
      (if (小成果 'full?) '() (list (成果动作 小成果 "小成果稳做" #f) (成果动作 小成果 "小成果快做" #t)))
      (if (大成果 'full?) '() (list (成果动作 大成果 "大成果稳做" #f) (成果动作 大成果 "大成果快做" #t))))))
