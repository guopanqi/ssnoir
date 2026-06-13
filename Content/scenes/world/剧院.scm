;; scenes/world/剧院.scm - Theater location

(define theater
  (let ()
    ;; ── Local State ────────────────────────────────
    (define gate-revealed #f)
    (define nightingale-talked #f)

    (define (node-theater-container)
      (list
        (container "剧院"
          (append
            (if (not gate-revealed)
                (list
                  (instant-action "查看大门"
                                  (lambda ()
                                    (set! gate-revealed #t)
                                    (notify! "发现了大门和匆匆走过的黑衣人。"))))
                '())
            (if gate-revealed
                (append
                  (list
                    (container "大门"
                      (if (not nightingale-talked)
                          (list
                            (instant-action "和夜莺谈话"
                                            (lambda ()
                                              (set! nightingale-talked #t)
                                              (notify! "夜莺：“你终于来了，我一直在等你。”"))))
                          (list
                            (observe-action "夜莺" "夜莺：“快去追吧，别让他跑了。”")))))
                  (if (not nightingale-talked)
                      (list (observe-action "黑衣人" "黑衣人匆匆走过。"))
                      (list (observe-action "黑衣人留下的踪迹" "地上残留着潮湿的泥土，以及一串延伸向阴暗巷弄的脚印。"))))
                '())))))

    ;; ── Message Passing Interface ─────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (node-theater-container))

          ((equal? msg 'save)
           (list
             (list "gate-revealed" gate-revealed)
             (list "nightingale-talked" nightingale-talked)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! gate-revealed (assoc-get data "gate-revealed" #f))
             (set! nightingale-talked (assoc-get data "nightingale-talked" #f))))

          (#t #f))))))
