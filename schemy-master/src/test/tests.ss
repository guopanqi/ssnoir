;; tests.ss — test suite for the Schemy fork

;; ── Helpers ───────────────────────────────────────────────────────────────────

(define (test-equal actual expected)
  (assert (equal? actual expected)
          (string-append "expected " (if (equal? expected #t) "#t"
                                       (if (equal? expected #f) "#f" "value"))
                         " but got something else")))

;; ── Original tests ────────────────────────────────────────────────────────────

(define (test-arithmetic)
  (assert (= (+ 1 2) 3))
  (assert (= (- 2 1) 1))
  (assert (= (* 2 3) 6))
  (assert (= (/ 4 2) 2))
  (assert (= 1 1))
  (assert (not (= 1 2)))
  (assert (< 1 2))
  (assert (not (> 1 2))))

(define (test-list-ops)
  (define ls (list 1 2 3 4))
  (assert (list? ls))
  (assert (not (list? 1)))
  (assert (= 4 (length ls)))
  (assert (= (car ls) 1))
  (assert (= (cadr ls) 2))
  (assert (= (caddr ls) 3))
  (assert (= (cadddr ls) 4))
  (assert (equal? ls '(1 2 3 4)))
  (assert (equal? ls (range 1 5)))
  (assert (null? (list)))
  (assert (not (null? (list 1))))
  (assert (equal? (reverse ls) '(4 3 2 1)))
  (assert (equal? (map (lambda (x) (* x 2)) ls) '(2 4 6 8))))

(define (test-closures)
  (define (create-student name age)
    (define (get) (list name age))
    (define (set-name! v) (set! name v))
    (define (set-age! v)  (set! age v))
    (list get set-name! set-age!))
  (define john (create-student "john" 18))
  (assert (equal? '("john" 18) ((list-ref john 0))))
  ((list-ref john 2) 19)
  (assert (equal? '("john" 19) ((list-ref john 0)))))

(define (test-tail-recursion)
  (define (sum-up-to n acc)
    (if (= n 0) acc (sum-up-to (- n 1) (+ acc n))))
  (assert (= 1250025000 (sum-up-to 50000 0)) "tail recursion"))

;; ── New: truthiness ───────────────────────────────────────────────────────────

(define (test-truthiness)
  (assert (not #f))
  (assert (not (not #t)))
  ;; In Scheme only #f is false — numbers, strings, '() are all truthy
  (assert (not (not 0)))
  (assert (not (not "")))
  (assert (not (not '())))
  (assert (equal? (not 42) #f))
  (assert (equal? (not #f) #t)))

;; ── New: let / letrec / let* ─────────────────────────────────────────────────

(define (test-let-forms)
  ;; basic let
  (assert (= 20 (let ((a 4) (b 5)) (* a b))))
  ;; let* — b can reference a
  (assert (= 20 (let* ((a 4) (b (* a 5))) b)))
  ;; letrec — mutual recursion
  (assert (= #t (letrec ((even? (lambda (n) (if (= n 0) #t (odd?  (- n 1)))))
                          (odd?  (lambda (n) (if (= n 0) #f (even? (- n 1))))))
                  (even? 10))))
  ;; named let — sum 0+1+2+3+4 = 10
  (assert (= 10 (let loop ((i 0) (acc 0))
                  (if (= i 5) acc (loop (+ i 1) (+ acc i))))))
  ;; named let for iteration
  (assert (equal? '(1 2 3)
                  (let loop ((n 3) (acc '()))
                    (if (= n 0) acc (loop (- n 1) (cons n acc)))))))

;; ── New: and / or ─────────────────────────────────────────────────────────────

(define (test-and-or)
  (assert (equal? (and) #t))
  (assert (equal? (and 1 2 3) 3))
  (assert (equal? (and 1 #f 3) #f))
  (assert (equal? (or) #f))
  (assert (equal? (or #f #f 3) 3))
  (assert (equal? (or 1 2) 1))
  ;; short-circuit: second arg not evaluated
  (define x 0)
  (and #f (set! x 1))
  (assert (= x 0))
  (or 42 (set! x 1))
  (assert (= x 0)))

;; ── New: when / unless ───────────────────────────────────────────────────────

(define (test-when-unless)
  (define result '())
  (when #t   (set! result (cons 'when-true result)))
  (when #f   (set! result (cons 'when-false result)))
  (unless #f (set! result (cons 'unless-false result)))
  (unless #t (set! result (cons 'unless-true result)))
  (assert (equal? result '(unless-false when-true))))

;; ── New: dotted rest args ─────────────────────────────────────────────────────

(define (test-dot-rest)
  ;; (lambda (x . rest) ...)
  (define (f x . rest) (cons x rest))
  (assert (equal? (f 1) '(1)))
  (assert (equal? (f 1 2 3) '(1 2 3)))
  ;; (define (g a b . rest) ...)
  (define (g a b . rest) (list a b rest))
  (assert (equal? (g 1 2) '(1 2 ())))
  (assert (equal? (g 1 2 3 4) '(1 2 (3 4))))
  ;; pure variadic
  (define (h . args) args)
  (assert (equal? (h 1 2 3) '(1 2 3))))

;; ── New: apply / append / map ────────────────────────────────────────────────

(define (test-variadic-builtins)
  ;; apply: (apply f a1 ... list)
  (assert (= 10 (apply + '(1 2 3 4))))
  (assert (= 10 (apply + 1 2 '(3 4))))
  (assert (= 10 (apply + 1 2 3 '(4))))
  ;; append: variadic
  (assert (equal? '(1 2 3 4 5 6) (append '(1 2) '(3 4) '(5 6))))
  (assert (equal? '(1 2) (append '() '(1 2))))
  ;; map: multiple lists
  (assert (equal? '(11 22 33) (map + '(1 2 3) '(10 20 30)))))

;; ── New: number primitives ───────────────────────────────────────────────────

(define (test-number-prims)
  (assert (= (modulo 10 3) 1))
  (assert (= (modulo -10 3) 2))   ; sign follows divisor
  (assert (= (remainder -10 3) -1)) ; sign follows dividend
  (assert (= (quotient 10 3) 3))
  (assert (= (abs -5) 5))
  (assert (= (abs  5) 5))
  (assert (= (min 3 1 2) 1))
  (assert (= (max 3 1 2) 3)))

;; ── New: predicates ──────────────────────────────────────────────────────────

(define (test-predicates)
  (assert (number?    42))
  (assert (number?    3.14))
  (assert (not (number? "x")))
  (assert (procedure? car))
  (assert (procedure? (lambda (x) x)))
  (assert (not (procedure? 42)))
  (assert (pair? '(1 2)))
  (assert (not (pair? '())))
  (assert (not (pair? 1))))

;; ── Run all ───────────────────────────────────────────────────────────────────

(test-arithmetic)
(test-list-ops)
(test-closures)
(test-tail-recursion)
(test-truthiness)
(test-let-forms)
(test-and-or)
(test-when-unless)
(test-dot-rest)
(test-variadic-builtins)
(test-number-prims)
(test-predicates)

;; Symbols accessible from C# integration tests
(define ANSWER-TO-THE-ULTIMATE-QUESTION-OF-LIFE-UNIVERSE-AND-EVERYTHING 42)
(define (TIMES-TWO x) (* 2 x))

"all tests passed"
