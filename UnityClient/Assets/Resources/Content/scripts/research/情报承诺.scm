(load-file "scripts/research/common.scm")
;; 两种隐藏条件各占一半，玩家看不到随机真值；只看到探查后的线索。
(define 条件 (random-choice (list 'A 'B)))
(define 已知? #f)
(define 方案 #f)
(define 失配? #f)
(define (研究附记) (list 方案 已知? 失配?))
(define (探查! n)
  (if (= n 0) #f
    (begin (if (= 信息有效 1) (set! 已知? #t) #f) (研究推进! (- n 1)))))
(define (选择! s)
  (set! 方案 s) (set! 失配? (not (equal? s 条件))))
(define (执行! n)
  (if (= n 0) #f
    (研究推进! (+ n (if 失配? 0 1)))))
(define-opponent-rule "期限流逝"
  (lambda () (not 完了?))
  (lambda () (期限 'advance! 1) (研究检查!)))
(define (get-render-data)
  (研究盘面 (if (= 信息有效 1) "条件A或B各半。探查中好揭示；押定不可换。匹配后中好额外推进1。" "条件A或B各半。探查不揭示；押定不可换。匹配后中好额外推进1。")
    (list (note-node "标注：线索" "线索"
        (if 已知? (if (equal? 条件 'A) "条件是A。" "条件是B。") "条件未知。"))
        (note-node "标注：方案" "方案"
          (if 方案 (if 失配? "方案失配：执行坏0、中1、好2。" "方案匹配：执行坏0、中2、好3。") "尚未押定，探查或直接选择。")))
    (if 方案
      (list (node "执行方案" :subtitle (if 失配? "坏0、中1、好2。" "坏0、中2、好3。") :requires (list (req-die))
        :resolve (roll 'knowledge (outcome (lambda () (执行! 0)))
          (outcome (lambda () (执行! 1))) (outcome (lambda () (执行! 2))))))
      (list
        (node "探查" :subtitle (if (= 信息有效 1) "坏无变化；中揭示；好揭示且目标加1。" "坏中无变化；好目标加1，无情报。")
          :requires (list (req-die)) :resolve (roll 'knowledge
            (outcome (lambda () (探查! 0))) (outcome (lambda () (探查! 1)))
            (outcome (lambda () (探查! 2)))))
        (node "押定A" :subtitle "一骰：不可逆选择方案A。" :requires (list (req-die))
          :resolve (instant (lambda () (选择! 'A))))
        (node "押定B" :subtitle "一骰：不可逆选择方案B。" :requires (list (req-die))
          :resolve (instant (lambda () (选择! 'B))))))))
