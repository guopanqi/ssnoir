;; scenes/home.scm - House Scene

;; ── Local State ────────────────────────────────
(define unlock? #f)
(define knock-count 0)
(define trash-count 0)
(define has-flower? #f)

;; ── Rules ──────────────────────────────────────
(define-rule "敲门三次开门"
  (lambda () (>= knock-count 3))
  (lambda () (set! unlock? #t)))

;; ── Node Definitions ──────────────────────────
(define (make-trash-nodes n)
  (if (<= n 0)
      '()
      (cons (instant-action "清理垃圾"
                            (lambda ()
                              (set! trash-count (- trash-count 1))))
            (make-trash-nodes (- n 1)))))

(define (node-kick-bin)
  (instant-action "踢垃圾桶"
    (lambda () (set! trash-count (+ trash-count 1)))))

(define (node-knock)
  (instant-action "敲门"
    (lambda () (set! knock-count (+ knock-count 1)))))

(define (node-enter)
  (instant-action "进门2"
    (lambda ()
      (set-global! 'location "office"))))

(define (node-buy-flower)
  (action "买一盆花"
          (list (req-item "金钱" 15))
          (instant (lambda ()
                     (set! has-flower? #t)))))

(define (node-flower)
  (observe-action "一盆花" "一盆散发着淡淡微香的白色雏菊，正静静地盛开着。"))

(define (node-buy-wine)
  (action "买一瓶酒"
          (list (req-item "金钱" 10))
          (instant (lambda ()
                     (set-global! (string-append "item:" "酒") (+ (get-item "酒") 1))))))

(define (node-drink-wine)
  (action "喝酒"
          (list (req-item "酒" 1))
          (instant (lambda ()
                     (set-global! 'health (min 100 (+ (get-global 'health) 10)))))))

;; ── Render Data Entrypoint ────────────────────
(define (get-render-data)
  (append
    (cons (node-kick-bin)
          (make-trash-nodes trash-count))
    (list
      (container "家"
        (append
          (list (node-knock))
          (append
            (if unlock? (list (node-enter)) '())
            (append
              (list (node-buy-flower))
              (append
                (if has-flower? (list (node-flower)) '())
                (list (node-buy-wine)
                      (node-drink-wine))))))))))
