;; scenes/world/码头.scm

(define dock
  (let ()
    ;; ── Local State ───────────────────────────────────
    (define dock-stage 1)
    ;; stage: 1=初到  2=见到老陈

    ;; ── Node Helpers ──────────────────────────────────

    (define (node-find-chen)
      (instant-action "寻找老陈"
        (lambda ()
          (set! dock-stage 2)
          (notify! "码头上人来人往。你凭着纸条上的描述找到了一个沉默的老人。他点点头："我知道你为什么来。""))))

    (define (node-dock-container)
      (cond
        ((= dock-stage 1)
         (container "码头"
           (list
             (observe-action "夜晚的码头" "船只轻轻摇晃，远处的灯光倒映在水面上。这里的夜，很安静。")
             (node-find-chen))))
        (#t
         (container "码头"
           (list
             (observe-action "老陈" "老陈："你找到我了。接下来的事情……就看你的了。""))))))

    ;; ── Message Passing Interface ─────────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node-dock-container)))

          ((equal? msg 'save)
           (list (list "dock-stage" dock-stage)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! dock-stage (assoc-get data "dock-stage" 1))))

          (#t #f))))))
