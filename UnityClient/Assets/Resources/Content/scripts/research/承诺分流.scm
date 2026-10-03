(load-file "scripts/research/common.scm")
(define 快线? #f)
(define (研究附记) (list 快线?))
(define-opponent-rule "期限流逝"
  (lambda () (not 完了?))
  (lambda () (期限 'advance! 1) (研究检查!)))
(define (get-render-data)
  (研究盘面 "稳步坏中1、好2。花一骰开快线后只有强推：坏花2冷静，中2、好4。不可返回。"
    (list (note-node "标注：通道" "通道" (if 快线? "已开快线，不能稳步推进。" "尚未承诺，可以继续稳步或开快线。")))
    (if 快线?
      (list (node "强行推进" :subtitle "坏0、花2冷静；中2；好4。" :requires (list (req-die))
        :resolve (roll 'knowledge (outcome (lambda () (spend-actor-composure! 'player 2)))
          (outcome (lambda () (研究推进! 2))) (outcome (lambda () (研究推进! 4))))))
      (list (node "稳步推进" :subtitle "坏1、中1、好2。" :requires (list (req-die))
        :resolve (roll 'knowledge (outcome (lambda () (研究推进! 1)))
          (outcome (lambda () (研究推进! 1))) (outcome (lambda () (研究推进! 2)))))
        (node "开快线" :subtitle "一骰：换成高速高风险动作，不可返回。" :requires (list (req-die))
          :resolve (instant (lambda () (set! 快线? #t))))))))
