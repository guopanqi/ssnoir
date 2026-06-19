;; init.ss — core macros loaded automatically by every Interpreter instance.
;; Order matters: later macros may depend on earlier ones.

;; ── Step 1: simple let (bootstrap — needed before letrec/let* exist) ──────────
(define-macro let
  (lambda args
    (define specs (car args))
    (define bodies (cdr args))
    (if (null? specs)
      `(begin ,@bodies)
      (begin
        (define spec1 (car specs))
        (define spec_rest (cdr specs))
        `(let ,spec_rest
           ((lambda ,(list (car spec1)) ,@bodies)
            ,(car (cdr spec1))))))))

;; ── Step 2: cond ─────────────────────────────────────────────────────────────
(define-macro cond
  (lambda args
    (if (= 0 (length args))
      '(if #f #f)
      (begin
        (define first (car args))
        (define rest (cdr args))
        (define test (if (equal? (car first) 'else) '#t (car first)))
        (define exprs (cdr first))
        `(if ,test
           (begin ,@exprs)
           (cond ,@rest))))))

;; ── Step 3: letrec — uses lambda + set!, no higher macros needed ──────────────
(define-macro letrec
  (lambda args
    (define specs (car args))
    (define bodies (cdr args))
    (define vars  (map car specs))
    (define sets  (map (lambda (s) (list 'set! (car s) (car (cdr s)))) specs))
    (define undefs (map (lambda (v) #f) vars))
    `((lambda ,vars ,@sets ,@bodies) ,@undefs)))

;; ── Step 4: let* — sequential bindings ───────────────────────────────────────
(define-macro let*
  (lambda args
    (define specs (car args))
    (define bodies (cdr args))
    (if (null? specs)
      `(begin ,@bodies)
      `(let (,(car specs))
         (let* ,(cdr specs) ,@bodies)))))

;; ── Step 5: redefine let to support named let ─────────────────────────────────
;;   (let loop ((i 0)) body)  →  (letrec ((loop (lambda (i) body))) (loop 0))
(define-macro let
  (lambda args
    (if (symbol? (car args))
      (begin
        (define name  (car args))
        (define specs (car (cdr args)))
        (define bodies (cdr (cdr args)))
        (define vars  (map car specs))
        (define inits (map (lambda (s) (car (cdr s))) specs))
        `(letrec ((,name (lambda ,vars ,@bodies)))
           (,name ,@inits)))
      (begin
        (define specs (car args))
        (define bodies (cdr args))
        (if (null? specs)
          `(begin ,@bodies)
          (begin
            (define spec1 (car specs))
            (define spec_rest (cdr specs))
            `(let ,spec_rest
               ((lambda ,(list (car spec1)) ,@bodies)
                ,(car (cdr spec1))))))))))

;; ── Step 6: and / or ─────────────────────────────────────────────────────────
(define-macro and
  (lambda args
    (cond
      ((null? args) '#t)
      ((null? (cdr args)) (car args))
      (else `(if ,(car args) (and ,@(cdr args)) #f)))))

(define-macro or
  (lambda args
    (cond
      ((null? args) '#f)
      ((null? (cdr args)) (car args))
      (else
        `(let ((__or__ ,(car args)))
           (if __or__ __or__ (or ,@(cdr args))))))))

;; ── Step 7: when / unless ────────────────────────────────────────────────────
(define-macro when
  (lambda args
    `(if ,(car args) (begin ,@(cdr args)) '())))

(define-macro unless
  (lambda args
    `(if ,(car args) '() (begin ,@(cdr args)))))

;; ── Step 8: common cXr combinations ─────────────────────────────────────────
(define (cadr x)   (car (cdr x)))
(define (cddr x)   (cdr (cdr x)))
(define (caar x)   (car (car x)))
(define (cdar x)   (cdr (car x)))
(define (caddr x)  (car (cdr (cdr x))))
(define (cdddr x)  (cdr (cdr (cdr x))))
(define (caadr x)  (car (car (cdr x))))
(define (cdadr x)  (cdr (car (cdr x))))
(define (cadar x)  (car (cdr (car x))))
(define (cddar x)  (cdr (cdr (car x))))
(define (cadddr x) (car (cdr (cdr (cdr x)))))
