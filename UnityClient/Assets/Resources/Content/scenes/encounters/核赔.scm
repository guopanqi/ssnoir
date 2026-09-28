;; 核赔——条款谱系基准。查清事实后，玩家决定坐实还是放水。

(define evidence-clk (make-clock "事实" 4 'gauge
  (tr "填满后可以决定怎样写进理赔报告。" "Fill this to decide what goes into the claim report.") (tr "事实" "Evidence")))
(define suspicion-clk (make-clock "沃尔特的怀疑" 4 'gauge
  (tr "放水前若填满，沃尔特会接管调查。" "If this fills before you let the claim pass, Walter takes over.") (tr "沃尔特的怀疑" "Walter's Suspicion")))

;; 报告没有时限。倒下只中断这一次核对，不替玩家选择坐实骗保或替人放水。
(define (on-encounter-collapse)
  (collapse-retry))

(define (tick-n! clk n)
  (if (<= n 0) #f (begin (clk 'tick!) (tick-n! clk (- n 1)))))

(define-opponent-rule "沃尔特同步核对"
  (lambda () (not (evidence-clk 'full?)))
  (lambda () (suspicion-clk 'tick!)))

(define (node-inspect)
  (node "核对伤势与工时"
    :title (tr "核对伤势与工时" "Check Injury and Hours")
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (lambda () (suspicion-clk 'tick!))
      (lambda () (evidence-clk 'tick!))
      (lambda () (tick-n! evidence-clk 2)))))

(define (node-listen)
  (node "听报案人把话说完"
    :title (tr "听报案人把话说完" "Hear the Claimant Out")
    :requires (list (req-die))
    :resolve (roll 'social
      (lambda () (spend-composure! 2))
      (lambda () (evidence-clk 'tick!))
      (lambda () (tick-n! evidence-clk 2) (suspicion-clk 'set! (max 0 (- (suspicion-clk 'current) 1)))))))

(define (node-confirm)
  (node "坐实骗保"
    :title (tr "坐实骗保" "Confirm Fraud")
    :resolve (instant (lambda ()
      (spotlight! (tr "报告成立" "Claim Confirmed")
        (tr "那名码头工确实装了伤。他骗到的不是发财钱，只是一笔医药费。沃尔特在表格上打了勾。"
            "The dockworker faked his injury. All he stood to gain was his medical bill. Walter checked a box on the form."))
      (end-encounter 'confirmed)))))

(define (node-mercy)
  (node "替他放过这一笔"
    :title (tr "替他放过这一笔" "Let the Claim Pass")
    :subtitle (tr "高风险；沃尔特的怀疑填满后将接管调查"
                  "High risk; Walter takes over if his suspicion fills")
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (lambda ()
        (spotlight! (tr "没有瞒过去" "Caught Out")
          (tr "沃尔特礼貌地收走报告，自己补上了最后一页。"
              "Walter politely took the report and filled in the last page himself."))
        (end-encounter 'confirmed))
      (lambda ()
        (spotlight! (tr "留下一处空白" "A Blank Space")
          (tr "沃尔特看了很久，没有追问那处对不上的数字。"
              "Walter studied it for a long time but did not ask about the figures that failed to add up."))
        (end-encounter 'mercy))
      (lambda ()
        (spotlight! (tr "报告写圆了" "The Report Holds")
          (tr "那名工人的医药费留在了保单里。沃尔特知道你改了什么，却没能证明。"
              "The worker's medical bill stayed in the claim. Walter knew what you had changed, but could not prove it."))
        (end-encounter 'mercy)))))

(define (get-render-data)
  (node "核赔"
    :title (tr "核赔" "The Claim")
    :children (append (clock-nodes
      (evidence-clk 'render-data)
      (suspicion-clk 'render-data))
      (if (evidence-clk 'full?)
        (if (suspicion-clk 'full?)
            (list
              (note-node "标注：核赔事实" (tr "核对所得" "Findings")
                (tr "伤势有伪装；索赔只够支付医药费。" "The injury was faked; the claim only covers medical bills."))
              (node-confirm))
            (list
              (note-node "标注：核赔事实" (tr "核对所得" "Findings")
                (tr "伤势有伪装；索赔只够支付医药费。" "The injury was faked; the claim only covers medical bills."))
              (node-confirm) (node-mercy)))
        (list (node-inspect) (node-listen))))))
