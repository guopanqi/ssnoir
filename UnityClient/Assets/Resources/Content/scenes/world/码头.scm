;; 码头——第一章新版城市生计。旧《三封信》的邮箱与诱捕内容已经退役。
;; 反正要出汗：好结果不耗心情；中结果耗1，失败额外再耗1；三档都有工资。
;; 艾迪每天首次失手后恢复1心情，不修改判定概率。
(define dock
  (let ()
    ;; 地点只拥有工资；工作目标由码头工作、人物存在由艾迪生活拥有。


    (define (on-haul! grade)
      (cond
        ((eq? grade '好) (add-item! "金钱" 16))
        ((eq? grade '中) (add-item! "金钱" 10) (spend-composure! 1))
        (else (add-item! "金钱" 6) (spend-composure! 2)))
      (码头工作 'on-haul!))

    (define (haul-node)
      (with-interaction "码头搬运" (工作 "搬运" '危险 'violence
        (outcome (lambda () (on-haul! '好)))
        (outcome (lambda () (on-haul! '中)))
        (outcome (lambda () (on-haul! '坏)))
        "替货船搬运木箱，赚取工钱，也耗心情。"
        :anchor "码头-货堆"
        :risk-target (list 'composure "player")
        :progress-target (list 'item "金钱"))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((eq? msg 'render-data)
           (list (place "码头"
             :children (append
               (list (haul-node))
               (map (lambda (n)
                      (if (member? :anchor n) n
                        (append n (list :anchor "码头"))))
                 (地点节点 "码头")))
             :arrivals (地点入场 "码头"))))
          ((eq? msg 'eddie-met?) (码头工作 'met?))
          ((eq? msg 'save) '())
          ((eq? msg 'load!) #t)
          (else (error "码头：未知消息")))))))
