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
                 (encounter-action "潜入保险箱"
                   (lambda () (start-encounter "infiltration")))
                 (encounter-action "街头交锋"
                   (lambda () (start-encounter "combat")))))))

          ((equal? msg 'save)
           (list
             (list "completed-missions" completed-missions)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! completed-missions (assoc-get data "completed-missions" '()))))

          (#t #f))))))
