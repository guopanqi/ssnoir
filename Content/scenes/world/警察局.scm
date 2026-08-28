;; 警察局——内部只有值班警官贝恩斯；主线在这里的节点由第一章模块提供。

(define police-station
  (let ()
    ;; 值班台、卷宗与报案窗口暂时共用警局主点。这里集中兜底，新增警局业务节点
    ;; 必须显式覆盖，不能重新落进网格。
    (define (anchor-at-police-station node-data)
      (if (member? :anchor node-data)
          node-data
          (append node-data (list :anchor "警察局"))))
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list
             (place "警察局"
               :children (map anchor-at-police-station
                           (append
                             (baines 'nodes-at "警察局")
                             (three-letters 'nodes-at "警察局"))))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
          (else #f))))))
