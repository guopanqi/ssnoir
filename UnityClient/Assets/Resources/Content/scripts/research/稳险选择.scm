(load-file "scripts/research/common.scm")
(define-opponent-rule "期限推进"
  (lambda () (not 完了?))
  (lambda () (期限 'advance! 1) (研究检查!)))
(define (get-render-data)
  (研究盘面 "两个动作的判定概率相同。稳进坏1、中2、好3；冒进坏0、中1、好5。"
    '()
    (list
      (node "稳进" :subtitle "坏1、中2、好3。" :requires (list (req-die))
        :resolve (roll 'knowledge
          (outcome (lambda () (研究推进! 1)))
          (outcome (lambda () (研究推进! 2)))
          (outcome (lambda () (研究推进! 3)))))
      (node "冒进" :subtitle "坏0、中1、好5。" :requires (list (req-die))
        :resolve (roll 'knowledge
          (outcome (lambda () (研究推进! 0)))
          (outcome (lambda () (研究推进! 1)))
          (outcome (lambda () (研究推进! 5))))))))
