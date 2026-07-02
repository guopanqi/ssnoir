;; scenes/world/酒吧.scm

(define bar
  (let ()
    ;; ── Local State ───────────────────────────────────
    (define bar-stage 1)
    ;; stage: 1=险象环生  2=拿到线索（海伦）

    (define bar-heat 0)
    ;; 外来人的"热度" 0~3：失败后持久+1，下次进门 tension 从这里开始。

    ;; ── Node Helpers ──────────────────────────────────

    (define (node-confrontation)
      (encounter-action "探听消息"
        (lambda ()
          (let ((actor-id (__current-actor)))   ; capture while action context is valid
            (set-global! 'bar-current-heat bar-heat)
            (start-encounter "酒吧交锋"
              (lambda (result)
                (if (equal? result 'success)
                    (begin
                      (set! bar-stage 2)
                      (set-global! 'helen-apartment-open #t)
                      (add-pending-report!)
                      (notify! "老混混低声说：'你找海伦？她在俱乐部，是个表演者。'"))
                    (begin
                      (set! bar-heat (min (+ bar-heat 1) 3))
                      (add-actor-stress! actor-id 1)
                      (notify! "你被轰了出去。明天再来，但他们不会忘记你。")))))))))

    (define (node-bar-container)
      (cond
        ((= bar-stage 1)
         (container "酒吧"
           (list
             (observe-action "酒吧内景" "昏黄的灯光，呛鼻的烟味，一张张粗粝的脸。外来人在这里不受欢迎。")
             (node-confrontation))))
        (#t
         (container "酒吧"
           (list
             (observe-action "老混混" "他往嘴里灌了口酒，不再说话。"))))))

    ;; ── Message Passing Interface ─────────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node-bar-container)))

          ((equal? msg 'complete?)
           (= bar-stage 2))

          ((equal? msg 'save)
           (list
             (list "bar-stage" bar-stage)
             (list "bar-heat"  bar-heat)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! bar-stage (assoc-get data "bar-stage" 1))
             (set! bar-heat  (assoc-get data "bar-heat"  0))))

          (#t #f))))))
