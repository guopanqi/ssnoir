;; scenes/office.scm - Office Scene

;; ── Local State ────────────────────────────────
(define working-count 0)
(define tired? #f)

;; ── Rules ──────────────────────────────────────
(define-rule "工作三次变累"
  (lambda () (>= working-count 3))
  (lambda () (set! tired? #t)))

;; ── Node Definitions ──────────────────────────
(define (node-work)
  (node "写代码"
    :effect (lambda () 
              (set! working-count (+ working-count 1))
              (set-global! 'money (+ (get-global 'money) 10)))))

(define (node-rest)
  (node "休息"
    :effect (lambda () 
              (set! working-count 0)
              (set! tired? #f))))

(define (node-go-home)
  (node "回家"
    :effect (lambda ()
              (set-global! 'location "home"))))

;; ── World Entrypoint ──────────────────────────
(define (get-world)
  (list
    (node "办公室"
      :children
      (append
        (if tired?
            (list (node-rest))
            (list (node-work)))
        (list (node-go-home))))))
