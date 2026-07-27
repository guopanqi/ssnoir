;; 警局——内部只有职员阿瑟；萨姆常驻老街酒馆，不占警局节点。

(define police-station
  (lambda args
    (let ((msg (car args)))
      (cond
        ((equal? msg 'render-data)
         (list
           (container "警局"
             (append
               (sam 'nodes-at "警局")
               (arthur 'nodes)
               (nightingale 'route-nodes-at "警局")))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
        (else #f)))))
