;; scenes/office.scm - Office Scene

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

(define (node-go-home)
  (instant-action "回家"
    (lambda ()
      (set-global! 'location "home"))))

;; ── Render Data Entrypoint ────────────────────
(define (get-render-data)
  (list
    (container-with-clocks "办公室"
      (list
        (node-work)
        (node-go-home))
      (list (work-clock 'render-data)))))
