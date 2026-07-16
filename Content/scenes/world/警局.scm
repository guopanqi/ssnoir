;; 警局——内部只有职员阿瑟；编外侦探萨姆站在程序边缘。

(define police-station
  (lambda args
    (let ((msg (car args)))
      (cond
        ((equal? msg 'render-data)
         (list
           (container "警局"
             (append
               (nightingale 'lead-nodes-at "警局")
               (arthur 'nodes)
               (sam 'nodes)
               (nightingale 'route-nodes-at "警局")))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
        (else #f)))))
