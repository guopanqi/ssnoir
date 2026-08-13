;; 教训——见好就收基准。随时可以离开，做得太轻或太重都有后果。

(define lesson-clk (make-clock "他记住教训" 4 'segments "2–3格是阿瑟想要的程度；4格会留下额外麻烦。"))
(define trouble-clk (make-clock "街面上的麻烦" 4 'segments "高风险动作和坏结果会推进。"))

(define (tick-n! clk n)
  (if (<= n 0) #f (begin (clk 'tick!) (tick-n! clk (- n 1)))))

(define-rule "教训失控"
  (lambda () (trouble-clk 'full?))
  (lambda ()
    (spotlight! "事情闹大了" "巡警赶到时，已经没人愿意承认这只是一次警告。")
    (end-encounter 'fail)))

(define (node-warn)
  (action "把话说清楚" (list (req-die))
    (roll 'social
      (lambda () (spend-composure! 1))
      (lambda () (lesson-clk 'tick!))
      (lambda () (tick-n! lesson-clk 2)))))

(define (node-pressure)
  (node "让他真正害怕"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'violence
      (lambda () (tick-n! trouble-clk 2))
      (lambda () (lesson-clk 'tick!) (trouble-clk 'tick!))
      (lambda () (tick-n! lesson-clk 2) (trouble-clk 'tick!)))))

(define (node-leave)
  (instant-action "就此收手"
    (lambda ()
      (cond
        ((< (lesson-clk 'current) 2)
         (spotlight! "太轻了" "他不觉得这算教训。阿瑟不会为一次没有结果的见面欠你人情。")
         (end-encounter 'fail))
        ((>= (lesson-clk 'current) 4)
         (spotlight! "压得太狠" "事情办成了，街面上也多了一件迟早会回来的麻烦。")
         (end-encounter 'success))
        (else
         (spotlight! "到此为止" "他听懂了，也还走得回家。阿瑟要的就是这种无法写进报告的结果。")
         (end-encounter 'success))))))

(define (get-render-data)
  (container "教训"
    (append (clock-nodes (lesson-clk 'render-data) (trouble-clk 'render-data))
      (list
          (node-warn) (node-pressure) (node-leave)))))
