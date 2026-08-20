;; 保险公司——沃尔特的核赔支线。

(define insurance-company
  (lambda args
    (let ((msg (car args)))
      (cond
        ((equal? msg 'render-data)
         (list (place "保险公司" :children (walter 'nodes))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
        (else #f)))))
