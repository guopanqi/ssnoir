;; 老街居民区：人物在场与故事内容由所属模块提供。
(define residential-district
  (let ()
    (define (fallback-anchor node-data)
      (if (member? :anchor node-data) node-data
        (append node-data (list :anchor "码头居民区"))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((eq? msg 'render-data)
            (list (place "码头居民区" :name "老街居民区"
              :anchor "码头居民区"
              :children (append
                (frank 'presence-at "码头居民区")
                (map fallback-anchor (地点节点 "码头居民区")))
              :arrivals (地点入场 "码头居民区"))))
          ((eq? msg 'save) '())
          ((eq? msg 'load!) #t)
          (else (error "居民区：未知消息")))))))
