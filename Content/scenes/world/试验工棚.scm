;; 三号货栈的试验工棚。所有人物与项目状态由 lin 拥有，地点只负责渲染。

(define test-workshop
  (lambda args
    (let ((msg (car args)))
      (cond
        ((equal? msg 'render-data)
         (list (container "三号货栈工棚" (lin 'workshop-nodes))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
        (else (error "试验工棚：收到未知消息"))))))
