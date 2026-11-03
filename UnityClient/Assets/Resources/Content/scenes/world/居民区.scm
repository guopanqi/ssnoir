;; 码头居民区——常驻小酒摊与各故事投射的调查内容。
(define residential-district
  (let ()
    (define stall-anchor "码头居民区-东侧门廊")

    (define (stall-nodes)
      (list
        (at-anchor stall-anchor
          (note-node "居民区·小酒摊" "小酒摊"
            "几个码头大汉围着木箱喝酒，咸花生散在纸上。摊主从酒桶里舀酒。"))
        (node "私酿酒"
          :anchor stall-anchor
          :subtitle "便宜的私酿酒，喝着呛人。恢复4点心情，下次掷城市骰时宿醉。"
          :progress-target (list 'composure "player")
          :requires (list (req-item "金钱" 10))
          :resolve (instant
            (outcome (lambda ()
              (restore-actor-composure! 'player 4)
              (apply-hangover!)))))))

    (define (fallback-anchor node-data)
      (if (member? :anchor node-data) node-data
        (append node-data (list :anchor "码头居民区"))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((eq? msg 'render-data)
            (list (place "码头居民区"
              :anchor "码头居民区"
              :children (append (stall-nodes)
                (map fallback-anchor (地点节点 "码头居民区")))
              :arrivals (地点入场 "码头居民区"))))
          ((eq? msg 'save) '())
          ((eq? msg 'load!) #t)
          (else (error "居民区：未知消息")))))))
