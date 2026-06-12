;; scenes/world/club.scm - Elites Club Scene

(define club
  (let ()
    ;; ── Node Definitions ──────────────────────────
    (define (node-club-desc)
      (observe-action "俱乐部大厅" "金碧辉煌的走廊铺着红色地毯，雪茄的烟雾在水晶吊灯下弥漫。只有真正的上流权贵才能涉足于此。"))

    (define (node-mayor-vip)
      (observe-action "市长贵宾室" "市政厅的高官们正聚在里面轻声商讨着什么，门外的保镖警惕地看着你。"))

    ;; ── Message Passing Interface ─────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list
             (node-club-desc)
             (node-mayor-vip)))

          ((equal? msg 'save)
           '())

          ((equal? msg 'load!)
           #f)

          (#t #f))))))
