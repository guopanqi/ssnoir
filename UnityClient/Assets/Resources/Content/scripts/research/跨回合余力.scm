;; R11：推进和恢复共用骰子，既有冷静跨回合延续。
(set-actor-composure! 'player 初始冷静)
(define 完成? #f)
(define 目标 (make-clock "目标" 目标上限 'gauge "全部进度跨回合保留；完成立即结束。"))
(define 期限 (make-clock "已过回合" 回合上限 'gauge "每次结束回合加1；引擎先花1冷静。"))
(define (余力结果 status)
  (list status (目标 'current) (min 回合上限 (+ 1 (期限 'current))) (actor-composure 'player)))
(define (余力结束! status text)
  (if 完成? (error "跨回合余力：重复结算") #t)
  (set! 完成? #t)
  (spotlight! 研究名称 text)
  (end-encounter (余力结果 status)))
(define (余力检查!)
  (if (or 完成? (hospitalization-pending?)) #f
    (cond
      ((目标 'full?) (余力结束! 'success "目标已经完成。"))
      ((期限 'full?) (余力结束! 'timeout "期限已到，目标未完成。")))))
(define (余力推进! n)
  (if (or 完成? (hospitalization-pending?)) #f
    (begin (目标 'advance! n) (余力检查!))))
(define (余力坏!) (spend-composure! 1))
(define (余力风险中!)
  (spend-composure! 1)
  (余力推进! 1))
(define-opponent-rule "时间流逝"
  (lambda () (not 完成?))
  (lambda () (期限 'advance! 1) (余力检查!)))
(define (on-encounter-collapse) (collapse-result (余力结果 'collapse)))
(define (get-render-data)
  (container 研究名称
    (append
      (list
        (note-node "标注：余力规则" "规则实验"
          (string-append "初始冷静" (number->string 初始冷静) "，上限5。三回合内达到10进度；进度和冷静跨回合保留。整顿花一颗骰，好结果恢复2冷静。每次结束回合另花1冷静。"))
        (node "稳做" :subtitle "坏：冷静−1；中：无变化；好：进度+1。"
          :requires (list (req-die))
          :resolve (roll 'knowledge
            (outcome 余力坏!) (outcome (lambda () #f))
            (outcome (lambda () (余力推进! 1)))))
        (node "快做" :subtitle "坏：冷静−1；中：冷静−1、进度+1；好：进度+2。"
          :requires (list (req-die))
          :resolve (roll 'knowledge
            (outcome 余力坏!) (outcome 余力风险中!)
            (outcome (lambda () (余力推进! 2))))))
      (clock-nodes (目标 'render-data) (期限 'render-data))
      (if (>= (actor-composure 'player) 5) '()
        (list
          (node "整顿" :subtitle "坏：冷静−1；中：无变化；好：冷静+2，上限5。"
            :requires (list (req-die))
            :resolve (roll 'knowledge
              (outcome 余力坏!) (outcome (lambda () #f))
              (outcome (lambda () (restore-actor-composure! 'player 2))))))))))
