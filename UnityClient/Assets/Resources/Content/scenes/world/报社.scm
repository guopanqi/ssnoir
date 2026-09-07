;; scenes/world/报社.scm - 报社（Phase B 开放）
;;
;; 「故事是怎么被写出来的」这件事的现场。第二章的调查从这里往下走。
;; Unity 里还没有模型锚点，卡片走网格布局。

(define newsroom
  (let ()
    ;; 翻旧报：一个便宜的、随时能做的调查动作。它给的是背景，不是证据。
    (define (node-archive)
      (roll-action "翻旧报" (list (req-die)) 'knowledge
        (outcome "眼睛看花了"
          (lambda () (spend-actor-composure! 'player 1)))
        (outcome "翻到几条"
          (lambda () (result-note! "首演那几天的版面记下来了")))
        (outcome "看出规律"
          (lambda ()
            (result-note! "同一批稿子出自同一个人")
            (add-item! "情报" 1)))))

    (define (children)
      (append (list (node-archive)) (地点节点 "报社")))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "报社" :children (children) :arrivals (地点入场 "报社"))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (else #f))))))
