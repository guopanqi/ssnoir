;; stdlib.scm - DSL foundation

;; Schemy does not treat colon-prefixed names as self-evaluating keywords.
(define :children ':children)
(define :effect ':effect)

(define (cadr xs)
  (car (cdr xs)))

(define (caddr xs)
  (car (cdr (cdr xs))))

;; Helper to extract keyword arguments from a list
(define (get-kwarg kwargs key default)
  (if (null? kwargs)
      default
      (if (null? (cdr kwargs))
          default
          (if (equal? (car kwargs) key)
              (cadr kwargs)
              (get-kwarg (cdr (cdr kwargs)) key default)))))

;; node constructor
;; Returns a list: ('node name children effect)
(define node
  (lambda args
    (let ((name (car args))
          (kwargs (cdr args)))
      (list 'node
            name
            (get-kwarg kwargs ':children '())
            (get-kwarg kwargs ':effect #f)))))

;; Rule system
(define rules '())

;; define-rule registers a rule
(define (define-rule name condition action)
  (set! rules (cons (list name condition action) rules)))

;; on-action triggers all rules
(define (on-action)
  (define (run-rules list-rules)
    (if (null? list-rules)
        #t
        (begin
          (let ((rule (car list-rules)))
            (let ((name (car rule))
                  (cond-fn (cadr rule))
                  (act-fn (caddr rule)))
              (if (cond-fn)
                  (act-fn)
                  #f)))
          (run-rules (cdr list-rules)))))
  (run-rules rules))

(define (min a b)
  (if (< a b) a b))

(define (make-clock label max)
  (let ((current 0))
    (lambda (msg)
      (cond
        ((equal? msg 'tick!)       (set! current (min (+ current 1) max)))
        ((equal? msg 'reset!)      (set! current 0))
        ((equal? msg 'full?)       (>= current max))
        ((equal? msg 'render-data) (list 'clock label current max))
        (else #f)))))

(define (filter pred lst)
  (if (null? lst)
      '()
      (if (pred (car lst))
          (cons (car lst) (filter pred (cdr lst)))
          (filter pred (cdr lst)))))
