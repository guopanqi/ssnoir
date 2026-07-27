;; 码头居民区——乔的家庭生活与伤病发生在这里；小节二起也是老街调查的主场。

(define residential-district
  (lambda args
    (let ((msg (car args)))
      (cond
        ((equal? msg 'render-data)
         (list (container "码头居民区"
                 (append (three-letters 'nodes-at "居民区")
                         (joe 'residential-nodes)))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
        (else #f)))))
