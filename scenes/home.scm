;; scenes/home.scm - House Scene

;; ── Local State ────────────────────────────────
(define unlock? #f)
(define knock-count 0)
(define trash-count 0)

;; ── Rules ──────────────────────────────────────
(define-rule "敲门三次开门"
  (lambda () (>= knock-count 3))
  (lambda () (set! unlock? #t)))

;; ── Node Definitions ──────────────────────────
(define (make-trash-nodes n)
  (if (<= n 0)
      '()
      (cons (node "清理垃圾"
                  :effect (lambda ()
                            (set! trash-count (- trash-count 1))))
            (make-trash-nodes (- n 1)))))

(define (node-kick-bin)
  (node "踢垃圾桶"
    :effect (lambda () (set! trash-count (+ trash-count 1)))))

(define (node-knock)
  (node "敲门"
    :effect (lambda () (set! knock-count (+ knock-count 1)))))

(define (node-enter)
  (node "进门2"
    :effect (lambda ()
              (set-global! 'location "office"))))

;; ── Render Data Entrypoint ────────────────────
(define (get-render-data)
  (append
    (cons (node-kick-bin)
          (make-trash-nodes trash-count))
    (list
      (node "家"
        :children
        (append
          (list (node-knock))
          (if unlock?
            (list (node-enter))
            '()))))))
