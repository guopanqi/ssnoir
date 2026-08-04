;; 警察局——内部只有职员阿瑟；主线在这里的节点由第一章模块提供。

(define police-station
  (lambda args
    (let ((msg (car args)))
      (cond
        ((equal? msg 'render-data)
         (list
           (container "警察局"
             (append
               (arthur 'nodes)
               (three-letters 'nodes-at "警察局")))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
        (else #f)))))
