;; scenes/world/board.scm - Noticeboard Sub-location

(define board
  (let ()
    ;; Noticeboard state if any
    (define completed-missions '())

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list
             (container "告示板"
               (list
                 (observe-action "褪色的旧告示"
                   "几张褪色的旧告示。测试用的委托暂时撤下了。")))))

          ((equal? msg 'save)
           (list
             (list "completed-missions" completed-missions)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! completed-missions (assoc-get data "completed-missions" '()))))

          (#t #f))))))
