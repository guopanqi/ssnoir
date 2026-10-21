(define dock
  (let ()
    (define shifts 0)
    (define (haul! grade)
      (set! shifts (+ shifts 1))
      (cond ((eq? grade '好) (add-item! "金钱" 20))
        ((eq? grade '中) (add-item! "金钱" 10) (spend-composure! 1))
        (else (spend-composure! 2)))
      (第一幕 'on-work! "码头" grade))
    (lambda args
      (cond
        ((eq? (car args) 'render-data)
          (list (place "码头" :children (append
            (list (工作 "搬运" '高 'violence
              (outcome (lambda () (haul! '好))) (outcome (lambda () (haul! '中)))
              (outcome (lambda () (haul! '坏))) "扛一班货，挣一晚的钱" :anchor "码头-货堆"))
            (地点节点 "码头")) :arrivals (地点入场 "码头"))))
        ((eq? (car args) 'save) (list (list "shifts" shifts)))
        ((eq? (car args) 'load!) (set! shifts (assoc-get (cadr args) "shifts" 0)))
        (else (error "码头：未知消息"))))))
