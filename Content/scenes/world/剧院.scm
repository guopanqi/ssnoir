;; 剧院——小节二结算后开放。经理常驻；排练、后台与第三封信都在这里。
;; 地点本身不拥有故事状态，只把自己的名字报给第一章模块。

(define theater
  (lambda args
    (let ((msg (car args)))
      (cond
        ((equal? msg 'render-data)
         (list (container "剧院"
                 (append (three-letters 'nodes-at "剧院") (lin 'theater-nodes)))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
        (else #f)))))
