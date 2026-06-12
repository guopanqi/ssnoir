;; scenes/world/office.scm - Office Scene

(define office
  (let ()
    ;; ── Local State ────────────────────────────────
    (define work-clock (make-clock "工作进度" 3 'segments))

    ;; ── Rules ──────────────────────────────────────
    (define-rule "工资发放"
      (lambda () (work-clock 'full?))
      (lambda ()
        (set-global! 'money (+ (get-global 'money) 50))
        (work-clock 'reset!)))

    ;; ── Node Definitions ──────────────────────────
    (define (node-work)
      (action "写代码"
              (list (req-die))
              (roll 'coding
                    (lambda () #f)
                    (lambda () (work-clock 'tick!))
                    (lambda () (begin (work-clock 'tick!)
                                      (work-clock 'tick!))))))

    ;; ── Message Passing Interface ─────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list
             (container-with-clocks "办公室区域"
               (list
                 (node-work))
               (list (work-clock 'render-data)))))

          ((equal? msg 'save)
           (list
             (list "work-clock" (work-clock 'current))))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (work-clock 'set! (assoc-get data "work-clock" 0))))

          (#t #f))))))
