;; 码头居民区——小节二起的老街调查主场。

(define residential-district
  (let ()
    ;; 路线、人物与发现地点各自声明专属 Anchor；其余剧情卡收回居民区主点。
    (define (anchor-at-residential-district node-data)
      (if (member? :anchor node-data)
          node-data
          (append node-data (list :anchor "码头居民区"))))
    (lambda args
      (let ((msg (car args)))
      (cond
        ((equal? msg 'render-data)
         (list (place "码头居民区"
                 :children (map anchor-at-residential-district
                             (append
                               (three-letters 'nodes-at "居民区")
                               (frank 'residential-nodes))))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
        (else #f))))))
