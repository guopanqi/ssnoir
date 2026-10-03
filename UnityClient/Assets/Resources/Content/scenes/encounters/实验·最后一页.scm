;; 同一页既是证据，也是指认来源的痕迹。保护来源要多花一次行动。
(define 页 0)
(define 遮过? #f)
(define 完了? #f)
(define 暴露名字 '())
(define 抄本 (make-clock "抄本证据" 6 'gauge "三页分别价值1、2、3；抄写一骰，遮名字另需一骰。"))
(define 泄露 (make-clock "来源泄露" 3 'gauge "没遮名字就抄，泄露一位来源；未处理原页会被搜走。"))
(define 已处理 (make-clock "已处理页" 3 'gauge "本场只有一回合；抄下或撕掉都处理一页。"))
(define (名字 n) (list-ref '("艾米" "乔" "露丝") n))
(define (来源牵连 n)
  (list-ref '("艾米让你藏过一夜。" "你答应乔不提名字。" "露丝明天要作证。") n))
(define (页名 n) (list-ref '("小额付款" "收账记录" "总账签字") n))
(define (翻页!)
  (set! 页 (+ 页 1)) (set! 遮过? #f) (已处理 'advance! 1))
(define (记泄露! n)
  (set! 暴露名字 (cons (名字 n) 暴露名字)) (泄露 'advance! 1))
(define (结束!)
  (if 完了? (error "最后一页：重复结束") #t)
  ;; 没毁掉的原件也暴露来源，不能靠只带走抄本绕过选择。
  (define (搜走! n)
    (if (< n 3)
        (begin
          (if (and (= n 页) 遮过?) #f (记泄露! n))
          (搜走! (+ n 1))) #f))
  (搜走! 页)
  (set! 完了? #t)
  (spotlight! "纸袋里的抄本"
    (if (泄露 'empty?) "来源的名字全留在灰里。你带走的证据少了几页。"
        "抄本交到了编辑手里。但纸上的名字也把来源交了出去。"))
  (end-encounter (list '交出抄本 (抄本 'current) (reverse 暴露名字))))
(define-opponent-rule "搜查队推开门"
  (lambda () (not 完了?)) (lambda () (结束!)))
(define (on-encounter-enter)
  (play-dialogue!
    (line "世界" "你要把账目交给编辑揭发收账人，也尽量别把提供账目的人牵连进去。")
    (line "世界" "三页账目躺在炉边。搜查队已经走上楼梯。")
    (line "世界" "里面有人替收账人办事。名字出去，他们就会找上来源。")
    (line "世界" "抄一页用一颗骰，遮名字再用一颗。撕页免费，但证据也没了。")
    (line "世界" "只有这一回合。没处理的原件，会连名字一起被搜走。")))
(define (on-encounter-collapse) (collapse-result '倒下))
(define (get-render-data)
  (container "最后一页"
    (append
      (list (note-node "标注：目标" "目标：送出账目"
        (if (= 页 3) "原件已处理。送出抄本结束；带走多少证据、暴露哪些来源由你决定。" "搜查队到来前抄下账目、送给编辑。尽量保护来源；撕页能保人，但会失去证据。")))
      (clock-nodes (抄本 'render-data) (泄露 'render-data) (已处理 'render-data))
      (list (note-node "标注：纸页" "眼前这页"
        (if (= 页 3) "原件已全部处理，可以送出抄本。"
            (string-append (页名 页) "，证据价值" (number->string (+ 页 1)) "。来源：" (名字 页)
              (if 遮过? "。名字已遮住。" "。名字尚未遮住。") (来源牵连 页)))))
      (if (< 页 3)
          (append
            (if 遮过? '()
                (list (node "遮掉名字" :subtitle "一骰：遮住本页的来源，不增加证据。"
                  :requires (list (req-die)) :resolve (instant (lambda () (set! 遮过? #t))))))
            (list
              (node "抄下这页" :subtitle "抄好烧原页；未遮名字就泄露来源。"
                :requires (list (req-die)) :resolve (instant (lambda ()
                  (抄本 'advance! (+ 页 1))
                  (if 遮过? #f (记泄露! 页))
                  (翻页!))))
              (instant-action "撕掉这页" (lambda () (翻页!))))) '())
      (list (instant-action "送出抄本" (lambda () (结束!)))))))
