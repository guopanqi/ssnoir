;; 悬案——隐藏胜利条件基准。先把旧账拼完整，再决定该找谁。

(define clue-clk (make-clock "旧账的缺口" 4 'segments "填满后才会出现真正的收口。"))
(define alarm-clk (make-clock "旧人察觉" 5 'segments "填满则证人与账页都会消失。"))

(define (tick-n! clk n)
  (if (<= n 0) #f (begin (clk 'tick!) (tick-n! clk (- n 1)))))

(define-turn-rule "旧人开始收口"
  (lambda () #t)
  (lambda () (alarm-clk 'tick!)))

(define-rule "悬案失败"
  (lambda () (alarm-clk 'full?))
  (lambda ()
    (spotlight! "账页不见了" "有人比你早一步收走了最后几页。弗兰克没有追问失败的细节。")
    (end-encounter 'fail)))

(define (node-ledger)
  (action "核对旧分账" (list (req-die))
    (roll 'knowledge
      (lambda () (alarm-clk 'tick!))
      (lambda () (clue-clk 'tick!))
      (lambda () (tick-n! clue-clk 2)))))

(define (node-witness)
  (action "找当年的装卸工" (list (req-die))
    (roll 'social
      (lambda () (spend-composure! 1))
      (lambda () (clue-clk 'tick!))
      (lambda () (tick-n! clue-clk 2)))))

(define (node-correct)
  (instant-action "去找当年的账房"
    (lambda ()
      (spotlight! "旧案有了结尾" "账房承认他改过分账名单。乔家当年也在被划掉的那一列。")
      (end-encounter 'success))))

(define (node-wrong)
  (instant-action "先去逼问船主"
    (lambda ()
      (spotlight! "找错了人" "船主把消息递了出去。真正改账的人当晚就离开了城。")
      (end-encounter 'fail))))

(define (get-render-data)
  (container "悬案"
    (append (clock-nodes (clue-clk 'render-data) (alarm-clk 'render-data))
      (if (clue-clk 'full?)
        (list (node-correct) (node-wrong))
        (list (node-ledger) (node-witness))))))
