;; 警察局——内部只有值班警官贝恩斯；主线在这里的节点由第一章模块提供。

(define police-station
  (lambda args
    (let ((msg (car args)))
      (cond
        ((equal? msg 'render-data)
         (list
           (place "警察局"
             :children (append
                         (baines 'nodes-at "警察局")
                         (three-letters 'nodes-at "警察局")))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
        (else #f)))))
