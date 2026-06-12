;; stdlib.scm - Standard Scheme library extensions for Schemy

(define (cadr xs)
  (car (cdr xs)))

(define (caddr xs)
  (car (cdr (cdr xs))))

(define (cadddr xs)
  (car (cdr (cdr (cdr xs)))))

(define and
  (lambda args
    (if (null? args)
        #t
        (if (car args)
            (apply and (cdr args))
            #f))))

(define or
  (lambda args
    (if (null? args)
        #f
        (if (car args)
            #t
            (apply or (cdr args))))))

(define (min a b)
  (if (< a b) a b))

(define (max a b)
  (if (> a b) a b))

(define (filter pred lst)
  (if (null? lst)
      '()
      (if (pred (car lst))
          (cons (car lst) (filter pred (cdr lst)))
          (filter pred (cdr lst)))))
