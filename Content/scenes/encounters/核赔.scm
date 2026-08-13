;; 核赔——条款谱系基准。查清事实后，玩家决定坐实还是放水。

(define evidence-clk (make-clock "事实" 4 'segments "填满后可以决定怎样写进理赔报告。"))
(define suspicion-clk (make-clock "沃尔特的怀疑" 4 'segments "放水前若填满，沃尔特会接管调查。"))

(define (tick-n! clk n)
  (if (<= n 0) #f (begin (clk 'tick!) (tick-n! clk (- n 1)))))

(define-turn-rule "沃尔特同步核对"
  (lambda () (not (evidence-clk 'full?)))
  (lambda () (suspicion-clk 'tick!)))

(define (node-inspect)
  (action "核对伤势与工时" (list (req-die))
    (roll 'knowledge
      (lambda () (suspicion-clk 'tick!))
      (lambda () (evidence-clk 'tick!))
      (lambda () (tick-n! evidence-clk 2)))))

(define (node-listen)
  (action "听报案人把话说完" (list (req-die))
    (roll 'social
      (lambda () (spend-composure! 1))
      (lambda () (evidence-clk 'tick!))
      (lambda () (tick-n! evidence-clk 2) (suspicion-clk 'set! (max 0 (- (suspicion-clk 'current) 1)))))))

(define (node-confirm)
  (instant-action "坐实骗保"
    (lambda ()
      (spotlight! "报告成立" "那名码头工确实装了伤。他骗到的不是发财钱，只是一笔医药费。沃尔特在表格上打了勾。")
      (end-encounter 'confirmed))))

(define (node-mercy)
  (node "替他放过这一笔"
    :subtitle "高风险；沃尔特的怀疑越深，越难把报告写圆"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (lambda ()
        (spotlight! "没有瞒过去" "沃尔特礼貌地收走报告，自己补上了最后一页。")
        (end-encounter 'confirmed))
      (lambda ()
        (spotlight! "留下一处空白" "沃尔特看了很久，没有追问那处对不上的数字。")
        (end-encounter 'mercy))
      (lambda ()
        (spotlight! "报告写圆了" "那名工人的医药费留在了保单里。沃尔特知道你改了什么，却没能证明。")
        (end-encounter 'mercy)))))

(define (get-render-data)
  (container "核赔"
    (append (clock-nodes (evidence-clk 'render-data) (suspicion-clk 'render-data))
      (if (evidence-clk 'full?)
        (if (suspicion-clk 'full?)
            (list (node-confirm))
            (list (node-confirm) (node-mercy)))
        (list (node-inspect) (node-listen))))))
