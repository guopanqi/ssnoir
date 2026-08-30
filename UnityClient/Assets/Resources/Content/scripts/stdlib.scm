;; stdlib.scm - SSNoir content-script helpers.
;;
;; Keep this file small and project-driven. Generic Scheme capabilities belong in
;; Schemy/init.ss or Builtins.cs; this layer is for helpers that content scripts
;; actually use.

(define (filter pred lst)
  (if (null? lst)
      '()
      (if (pred (car lst))
          (cons (car lst) (filter pred (cdr lst)))
          (filter pred (cdr lst)))))

(define (member? x lst)
  (if (null? lst)
      #f
      (if (equal? x (car lst))
          #t
          (member? x (cdr lst)))))

;; 从 assoc-list 中按 key 查找，找不到返回 default
(define (assoc-get alist key default)
  (if (null? alist)
      default
      (if (equal? (car (car alist)) key)
          (cadr (car alist))
          (assoc-get (cdr alist) key default))))
