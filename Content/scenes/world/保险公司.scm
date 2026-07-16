;; 保险公司——沃尔特的核赔支线与夜莺正规舱位。

(define insurance-company
  (lambda args
    (let ((msg (car args)))
      (cond
        ((equal? msg 'render-data)
         (list (container "保险公司"
                 (append (walter 'nodes) (nightingale 'route-nodes-at "保险公司")))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
        (else #f)))))
