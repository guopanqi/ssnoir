;; R25：线索有独立行动成本；目标身份整局固定，搜空也会提供信息。
(set-actor-composure! 'player 5)
(define 目标位置 (random-choice (list '甲 '乙)))
(define 已定位? #f)
(define 完成? #f)
(define 搜寻甲 (make-clock "搜寻甲" 3 'gauge "搜满才能确认这里是否有目标。"))
(define 搜寻乙 (make-clock "搜寻乙" 3 'gauge "搜满才能确认这里是否有目标。"))
(define 探查线索 (make-clock "探查线索" 1 'gauge "好档得到1线索，立即定位；不增加搜索进度。"))
(define (位置名字 方向) (if (eq? 方向 '甲) "甲" "乙"))
(define (定位搜索结果 status)
  (list status (搜寻甲 'current) (搜寻乙 'current) 目标位置
    已定位? (探查线索 'current) (actor-composure 'player)))
(define (定位搜索结束! status text)
  (if 完成? (error "定位搜索：重复结算") #t)
  (set! 完成? #t)
  (spotlight! 研究名称 text)
  (end-encounter (定位搜索结果 status)))
(define (定位揭晓! 来源)
  (if (or 完成? (hospitalization-pending?)) #f
    (begin
      (if 已定位? (error "定位搜索：重复定位") #t)
      (set! 已定位? #t)
      (spotlight! 来源
        (string-append "目标在" (位置名字 目标位置) "处。已有搜索进度保留。")))))
(define (定位搜索推进! 方向 n)
  (if (or 完成? (hospitalization-pending?)) #f
    (let ((钟 (if (eq? 方向 '甲) 搜寻甲 搜寻乙)))
      (钟 'advance! n)
      (if (钟 'full?)
        (if (eq? 方向 目标位置)
          (定位搜索结束! 'success "目标已经找到。")
          (定位揭晓! (string-append (位置名字 方向) "处搜空"))) #f))))
(define (定位坏!) (spend-composure! 1))
(define (定位风险中! 方向)
  (spend-composure! 1)
  (定位搜索推进! 方向 1))
(define (定位搜索动作 方向 钟)
  (if (or (钟 'full?) (and 已定位? (not (eq? 方向 目标位置)))) '()
    (let ((前缀 (位置名字 方向)))
      (list
        (node (string-append 前缀 "稳做") :subtitle "坏：冷静−1；中：无变化；好：搜索+1。"
          :requires (list (req-die))
          :resolve (roll 'knowledge
            (outcome 定位坏!) (outcome (lambda () #f))
            (outcome (lambda () (定位搜索推进! 方向 1)))))
        (node (string-append 前缀 "快做") :subtitle "坏：冷静−1；中：冷静−1、搜索+1；好：搜索+2。"
          :requires (list (req-die))
          :resolve (roll 'knowledge
            (outcome 定位坏!) (outcome (lambda () (定位风险中! 方向)))
            (outcome (lambda () (定位搜索推进! 方向 2)))))))))
(define-opponent-rule "搜索机会结束"
  (lambda () (not 完成?))
  (lambda () (定位搜索结束! 'timeout "这一回合结束，还没找到目标。")))
(define (on-encounter-collapse) (collapse-result (定位搜索结果 'collapse)))
(define (get-render-data)
  (container 研究名称
    (append
      (list
        (note-node "标注：搜索规则" "一回合，两处搜索"
          "甲乙各需3进度。目标只在其中一处，各占一半；找到即结束。搜空一处后，另一处确定有目标。")
        (note-node "标注：目标信息" (if 已定位? (string-append "目标在" (位置名字 目标位置) "处") "目标尚未定位")
          (if 已定位? "继续完成对应搜索。"
            (if 允许探查? "探查花一骰，好档定位目标；也可以直接搜索。" "搜满一处才知道有没有目标。"))))
      (clock-nodes (搜寻甲 'render-data) (搜寻乙 'render-data))
      (if (and 允许探查? (not 已定位?))
        (append
          (clock-nodes (探查线索 'render-data))
          (list
            (node "探查" :subtitle "坏：冷静−1；中：无变化；好：线索+1，定位目标。"
              :requires (list (req-die))
              :resolve (roll 'knowledge
                (outcome 定位坏!) (outcome (lambda () #f))
                (outcome (lambda ()
                  (if (or 完成? (hospitalization-pending?)) #f
                    (begin (探查线索 'advance! 1) (定位揭晓! "线索确认"))))))))) '())
      (定位搜索动作 '甲 搜寻甲) (定位搜索动作 '乙 搜寻乙))))
