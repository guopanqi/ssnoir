;; scenes/world/市集.scm
;; 码头市集 — wraps the existing merchant scene.
;; merchant.scm is still loaded and saved separately; market just surfaces it.

(define market
  (let ()
    (define (node-market-container)
      (container "码头市集"
        (merchant 'render-data)))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node-market-container)))

          ((equal? msg 'save)   '())
          ((equal? msg 'load!)  #f)
          (#t #f))))))
