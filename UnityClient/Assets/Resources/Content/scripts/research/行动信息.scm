;; R09：行动同时提供进度与信息；任何一条路线完成即可。
(define 路长A (random-choice (list 短路长度 长路长度)))
(define 路长B (random-choice (list 短路长度 长路长度)))
(define 已知A? 开局明示)
(define 已知B? 开局明示)
(define 完成? #f)
(define 路程A (make-clock "路程A" 长路长度 'gauge))
(define 路程B (make-clock "路程B" 长路长度 'gauge))
(define (信息结果 status)
  (list status (路程A 'current) 路长A 已知A? (路程B 'current) 路长B 已知B? (actor-composure 'player)))
(define (信息结束! status text)
  (if 完成? (error "行动信息研究：重复结算") #t)
  (set! 完成? #t)
  (spotlight! 研究名称 text)
  (end-encounter (信息结果 status)))
(define (信息推进! 方向 n)
  (if (or 完成? (hospitalization-pending?)) #f
    (let ((钟 (if (eq? 方向 'A) 路程A 路程B))
          (路长 (if (eq? 方向 'A) 路长A 路长B))
          (已知? (if (eq? 方向 'A) 已知A? 已知B?)))
      (钟 'advance! (min n (- 路长 (钟 'current))))
      (if (and (not 已知?) (>= (钟 'current) 揭示进度))
        (begin
          (if (eq? 方向 'A) (set! 已知A? #t) (set! 已知B? #t))
          (spotlight! (if (eq? 方向 'A) "路线A已摸清" "路线B已摸清")
            (if (= 路长 短路长度) "这是一条短路，总共需要3进度。已有进度保留。" "这是一条长路，总共需要6进度。已有进度保留。"))) #f)
      (if (>= (钟 'current) 路长) (信息结束! 'success "一条路线已经完成。") #f))))
(define (信息坏!) (spend-composure! 1))
(define (信息风险中! 方向)
  (spend-composure! 1)
  (信息推进! 方向 1))
(define (信息动作 名字 方向)
  (list
    (node (string-append "稳做" 名字) :subtitle "坏：冷静−1；中：无变化；好：进度+1。"
      :requires (list (req-die))
      :resolve (roll 'knowledge
        (outcome 信息坏!) (outcome (lambda () #f))
        (outcome (lambda () (信息推进! 方向 1)))))
    (node (string-append "快做" 名字) :subtitle "坏：冷静−1；中：冷静−1、进度+1；好：进度+2。"
      :requires (list (req-die))
      :resolve (roll 'knowledge
        (outcome 信息坏!) (outcome (lambda () (信息风险中! 方向)))
        (outcome (lambda () (信息推进! 方向 2)))))))
;; 可见钟的上限随信息变化，真实进度仍由make-clock持有并生成效果条。
(define (信息钟 名字 钟 路长 已知?)
  (list 'clock 名字 (钟 'current)
    (if 已知? 路长 揭示进度) 'gauge
    (if 已知? "完成任意一条路线即可；已有进度保留。" "这一格是摸清路长的检查点，还不是终点。")))
(define-opponent-rule "机会结束"
  (lambda () (not 完成?))
  (lambda () (信息结束! 'timeout "回合结束，没有路线完成。")))
(define (on-encounter-collapse) (collapse-result (信息结果 'collapse)))
(define (get-render-data)
  (container 研究名称
    (append
      (list (note-node "标注：行动信息规则" "规则实验"
        (if 开局明示
          "只需完成A或B中的任意一条。每条独立为短路3或长路6，各占一半。本场一回合；路长开局可见。"
          "只需完成A或B中的任意一条。每条独立为短路3或长路6，各占一半。首次推进后揭示路长，已有进度保留。本场一回合。")))
      (clock-nodes (信息钟 "路程A" 路程A 路长A 已知A?) (信息钟 "路程B" 路程B 路长B 已知B?))
      (信息动作 "A" 'A) (信息动作 "B" 'B))))
