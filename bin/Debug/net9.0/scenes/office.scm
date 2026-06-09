;; scenes/office.scm - Office Scene

;; ── Local State ────────────────────────────────
(define work-clock (make-clock "工作进度" 3))

;; ── Clocks ─────────────────────────────────────
(define (get-clocks)
  (list (work-clock 'render-data)))

;; ── Rules ──────────────────────────────────────
(define-rule "工资发放"
  (lambda () (work-clock 'full?))
  (lambda ()
    (set-global! 'money (+ (get-global 'money) 50))
    (work-clock 'reset!)))

;; ── Node Definitions ──────────────────────────
(define (node-work)
  (node "写代码"
    :effect (lambda ()
              (work-clock 'tick!))))

(define (node-go-home)
  (node "回家"
    :effect (lambda ()
              (set-global! 'location "home"))))

;; ── World Entrypoint ──────────────────────────
(define (get-world)
  (list
    (node "办公室"
      :children
      (list
        (node-work)
        (node-go-home)))))
