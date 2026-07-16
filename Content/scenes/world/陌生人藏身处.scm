;; 陌生人的藏身处——完成饭店与码头两条查访 Clock 后才出现在城市里。

(define stranger-hideout
  (let ()
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node "陌生人的藏身处"
                   :subtitle "收账人的临时落脚处"
                   :children (nightingale 'hideout-nodes))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (else #f))))))
