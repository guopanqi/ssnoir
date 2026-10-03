;; R19：完整成果的回合容量与冷静延续/恢复组合；无新增奖励。
(set-actor-composure! 'player 3)
(define 完成? #f)
(define 工作甲 (make-clock "工作甲" 5 'gauge "完成后保留；未完成部分按回合规则结算。"))
(define 工作乙 (make-clock "工作乙" 3 'gauge "完成后保留；未完成部分按回合规则结算。"))
(define 期限 (make-clock "已过回合" 3 'gauge "第三次结束回合到期；每次另花1冷静。"))
(define (整段结果 status)
  (list status (工作甲 'current) (工作乙 'current)
    (min 3 (+ 1 (期限 'current))) (actor-composure 'player)))
(define (整段结束! status text)
  (if 完成? (error "整段留力：重复结算") #t)
  (set! 完成? #t)
  (spotlight! 研究名称 text)
  (end-encounter (整段结果 status)))
(define (整段检查!)
  (if (or 完成? (hospitalization-pending?)) #f
    (cond
      ((and (工作甲 'full?) (工作乙 'full?)) (整段结束! 'success "两项工作全部完成。"))
      ((期限 'full?) (整段结束! 'timeout "三回合已过，还有工作未完成。")))))
(define (整段推进! clock n)
  (if (or 完成? (hospitalization-pending?)) #f
    (begin (clock 'advance! n) (整段检查!))))
(define (整段坏!) (spend-composure! 1))
(define (整段风险中! clock) (spend-composure! 1) (整段推进! clock 1))
(define (整段动作 clock prefix)
  (if (clock 'full?) '()
    (list
      (node (string-append prefix "稳做") :subtitle "坏：冷静−1；中：无变化；好：进度+1。"
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (outcome 整段坏!) (outcome (lambda () #f))
          (outcome (lambda () (整段推进! clock 1)))))
      (node (string-append prefix "快做") :subtitle "坏：冷静−1；中：冷静−1、进度+1；好：进度+2。"
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (outcome 整段坏!) (outcome (lambda () (整段风险中! clock)))
          (outcome (lambda () (整段推进! clock 2))))))))
(define-opponent-rule "回合结算"
  (lambda () (not 完成?))
  (lambda ()
    (if 半成品清零?
      (begin
        (if (工作甲 'full?) #f (工作甲 'reset!))
        (if (工作乙 'full?) #f (工作乙 'reset!))) #f)
    (期限 'advance! 1)
    (整段检查!)))
(define (on-encounter-collapse) (collapse-result (整段结果 'collapse)))
(define (get-render-data)
  (container 研究名称
    (append
      (list
        (note-node "标注：整段规则" "三回合，两项都要完成"
          (if 半成品清零?
            "甲5格、乙3格。回合末未完成进度清零，完成项保留。"
            "甲5格、乙3格。所有进度跨回合保留。"))
        (note-node "标注：整段余力" "冷静跨回合延续"
          "初始3，上限5。整顿花骰，好档恢复2。结束回合另花1冷静。"))
      (clock-nodes (工作甲 'render-data) (工作乙 'render-data) (期限 'render-data))
      (整段动作 工作甲 "甲") (整段动作 工作乙 "乙")
      (if (>= (actor-composure 'player) 5) '()
        (list
          (node "整顿" :subtitle "坏：冷静−1；中：无变化；好：冷静+2，上限5。"
            :requires (list (req-die))
            :resolve (roll 'knowledge
              (outcome 整段坏!) (outcome (lambda () #f))
              (outcome (lambda () (restore-actor-composure! 'player 2))))))))))
