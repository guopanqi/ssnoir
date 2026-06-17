;; scenes/world/码头居民区.scm

(define residential
  (let ()
    (define (helen-apartment-nodes)
      (if (get-global 'helen-apartment-open)
          (list
            (container "海伦的公寓"
              (list
                (observe-action "公寓门口" "门缝里透出橘黄的灯光，隐约听见乐器声。"))))
          '()))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list
             (container "码头居民区"
               (append
                 (list (observe-action "昏黄的灯光" "低矮的砖房挨着砖房，有人在里面说话，听不清楚。"))
                 (helen-apartment-nodes)))))

          ((equal? msg 'save)   '())
          ((equal? msg 'load!)  #f)
          (#t #f))))))
