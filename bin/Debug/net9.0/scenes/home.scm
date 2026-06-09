;; scenes/home.scm - House Scene

;; ── Local State ────────────────────────────────
(define unlock? #f)
(define knock-count 0)
(define dirty? #f)

;; ── Rules ──────────────────────────────────────
(define-rule "敲门三次开门"
  (lambda () (>= knock-count 3))
  (lambda () (set! unlock? #t)))

;; ── Node Definitions ──────────────────────────
(define (node-trash)
  (node "清理垃圾"
    :effect (lambda () (set! dirty? #f))))

(define (node-kick-bin)
  (node "踢垃圾桶"
    :effect (lambda () (set! dirty? #t))))

(define (node-knock)
  (node "敲门"
    :effect (lambda () (set! knock-count (+ knock-count 1)))))

(define (node-enter)
  (node "进门"
    :effect (lambda ()
              (set-global! 'location "office"))))

;; ── World Entrypoint ──────────────────────────
(define (get-world)
  (append
    (append
      (list (node-kick-bin))
      (if dirty? (list (node-trash)) '()))
    (list
      (node "家"
        :children
        (append
          (list (node-knock))
          (if unlock?
            (list (node-enter))
            '()))))))
