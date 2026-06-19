;; stdlib.scm — optional standard library for the Schemy fork.
;;
;; This file is NOT loaded automatically. Load it explicitly:
;;   (load "path/to/stdlib.scm")        ; if using ReadOnlyFileSystemAccessor
;;   interpreter.Evaluate(File.OpenText("stdlib.scm")); ; from C#
;;
;; Requires: interpreter already has init.ss loaded (always true).

;; ── List ─────────────────────────────────────────────────────────────────────

(define (filter pred lst)
  (cond
    ((null? lst) '())
    ((pred (car lst)) (cons (car lst) (filter pred (cdr lst))))
    (else (filter pred (cdr lst)))))

(define (for-each proc lst)
  (when (not (null? lst))
    (proc (car lst))
    (for-each proc (cdr lst))))

(define (assoc key alist)
  (cond
    ((null? alist) #f)
    ((equal? (caar alist) key) (car alist))
    (else (assoc key (cdr alist)))))

(define (assq key alist)
  (cond
    ((null? alist) #f)
    ((eq? (caar alist) key) (car alist))
    (else (assq key (cdr alist)))))

(define (member x lst)
  (cond
    ((null? lst) #f)
    ((equal? x (car lst)) lst)
    (else (member x (cdr lst)))))

(define (memq x lst)
  (cond
    ((null? lst) #f)
    ((eq? x (car lst)) lst)
    (else (memq x (cdr lst)))))

(define (list-tail lst k)
  (if (= k 0) lst
    (list-tail (cdr lst) (- k 1))))

(define (last lst)
  (if (null? (cdr lst)) (car lst) (last (cdr lst))))

(define (take lst n)
  (if (or (= n 0) (null? lst))
    '()
    (cons (car lst) (take (cdr lst) (- n 1)))))

(define (drop lst n)
  (if (or (= n 0) (null? lst))
    lst
    (drop (cdr lst) (- n 1))))

(define (flatten lst)
  (cond
    ((null? lst) '())
    ((list? (car lst)) (append (flatten (car lst)) (flatten (cdr lst))))
    (else (cons (car lst) (flatten (cdr lst))))))

;; ── Higher-order ──────────────────────────────────────────────────────────────

(define (fold-left f init lst)
  (if (null? lst)
    init
    (fold-left f (f init (car lst)) (cdr lst))))

(define (fold-right f init lst)
  (if (null? lst)
    init
    (f (car lst) (fold-right f init (cdr lst)))))

(define (every pred lst)
  (or (null? lst)
      (and (pred (car lst)) (every pred (cdr lst)))))

(define (any pred lst)
  (and (not (null? lst))
       (or (pred (car lst)) (any pred (cdr lst)))))

;; ── Numeric ───────────────────────────────────────────────────────────────────

(define (iota count . args)
  (let ((start (if (null? args) 0 (car args)))
        (step  (if (or (null? args) (null? (cdr args))) 1 (cadr args))))
    (let loop ((i 0) (acc '()))
      (if (= i count)
        (reverse acc)
        (loop (+ i 1) (cons (+ start (* i step)) acc))))))

(define (gcd a b)
  (if (= b 0) (abs a) (gcd b (modulo a b))))

(define (lcm a b)
  (quotient (* (abs a) (abs b)) (gcd a b)))
