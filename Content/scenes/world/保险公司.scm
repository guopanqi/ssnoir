;; 保险公司——沃尔特的核赔支线。

(define insurance-company
  (let ()
    ;; 沃尔特的观察与核赔都在这栋楼里完成；没有独立房间模型前，统一投到主点。
    (define (anchor-at-insurance-company node-data)
      (if (member? :anchor node-data)
          node-data
          (append node-data (list :anchor "保险公司"))))
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "保险公司"
                   :children (map anchor-at-insurance-company (walter 'nodes)))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
          (else #f))))))
