;; 三号货栈的试验工棚。所有人物与项目状态由 lin 拥有，地点只负责渲染。
;;
;; 工棚有自己的主锚点和机位，内部动作按工作区域分到子锚点
;; （控制台 / 工作台 / 旧轨道 / 围栏），卡片不会全部叠在一个点上。

(define test-workshop
  (let ()
    (define shed-anchor "三号货栈工棚")

    (define (require-workshop-anchor node-data)
      (if (member? :anchor node-data)
          node-data
          (error "试验工棚：工作节点必须显式指定功能锚点")))

    (define (children)
      (map require-workshop-anchor (地点节点 "三号货栈工棚")))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "三号货栈工棚"
                        :anchor shed-anchor
                        :children (children)
                        :arrivals (地点入场 "三号货栈工棚"))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (else (error "试验工棚：收到未知消息")))))))
