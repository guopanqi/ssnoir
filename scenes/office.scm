;; scenes/office.scm - Office Scene

;; ── Local State ────────────────────────────────
(define work-clock (make-clock "工作进度" 3))

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

;; ── Render Data Entrypoint ────────────────────
(define (get-render-data)
  (list
    (work-clock 'render-data)
    (node "办公室"
      :children
      (list
        (node-work)
        (node-go-home)))))
