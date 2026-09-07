;; scenes/world/上城酒吧.scm - 上城的酒店酒吧（第二章 Phase A 开放）
;;
;; 第二章「世界打开」那一面的地方：钱好赚，事情好像都很容易办。
;; 这里没有故事状态——谁在这儿有卡片写在 world/模块清单.scm。
;;
;; Unity 里还没有这个地方的模型，所以不声明锚点，卡片走网格布局。
;; 模型做出来之后在这里补一个主锚点名即可。

(define uptown-bar
  (let ()
    ;; 上城的活：报酬明显高于码头，代价是它属于商业圈的人情，办砸了名声也传得快。
    (define (node-errand)
      (关系工作 "替人跑一趟" "商业圈" '低 'social
        (outcome "办得漂亮"
          (lambda () (add-item! "金钱" 45)))
        (outcome "办完了"
          (lambda () (add-item! "金钱" 28)))
        (outcome "话传拧了"
          (lambda () (spend-composure! 2)))
        "上城的委托：送一份东西，说几句得体的话"))

    (define (node-drink)
      (action "在这儿喝一杯" (list (req-die) (req-item "金钱" 20))
        (instant
          (outcome "酒是好酒"
            (lambda ()
              (remove-item! "金钱" 20)
              (restore-actor-composure! 'player 3)
              (apply-hangover!))))))

    (define (children)
      (append (list (node-errand) (node-drink))
              (地点节点 "上城")))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "上城酒吧" :children (children) :arrivals (地点入场 "上城"))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (else #f))))))
