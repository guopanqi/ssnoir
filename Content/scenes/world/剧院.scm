;; scenes/world/剧院.scm - Theater location

(define theater
  (let ()
    ;; ── Local State ────────────────────────────────
    (define gate-revealed #f)

    (define (node-theater-container)
      (list
        (container "剧院"
          (append
            (if (not gate-revealed)
                (list
                  (instant-action "查看大门"
                                  (lambda ()
                                    (set! gate-revealed #t)
                                    )))
                '())
            (if gate-revealed
                (list
                  (container "大门"
                    (list
                      (observe-action "夜莺" "夜莺：“你终于来了，我一直在等你。”")))
                  (observe-action "黑衣人匆匆走过" "他看了你一眼, 然后转身离开了"))
                '())))))

    ;; ── Message Passing Interface ─────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (node-theater-container))

          ((equal? msg 'save)
           (list
             (list "gate-revealed" gate-revealed)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! gate-revealed (assoc-get data "gate-revealed" #f))))

          (#t #f))))))
