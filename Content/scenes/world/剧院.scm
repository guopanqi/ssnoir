;; 剧院——小节二结算后开放。经理常驻；排练、后台与第三封信都在这里。
;; 地点本身不拥有故事状态，只把自己的名字报给第一章模块。

(define theater
  (let ()
    ;; 未指明区域的剧情和结算卡落在剧院主点；舞台布置等空间动作在内容里
    ;; 显式使用剧院-外圈/内环/中央台/后台，避免每个动作各占一个模型 Anchor。
    (define (anchor-at-theater node-data)
      (if (member? :anchor node-data)
          node-data
          (append node-data (list :anchor "剧院"))))
    (lambda args
      (let ((msg (car args)))
      (cond
        ((equal? msg 'render-data)
         (list (place "剧院"
                 :children (map anchor-at-theater
                             (append (three-letters 'nodes-at "剧院"))))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
        (else #f))))))
