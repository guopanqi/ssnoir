;; scenes/world/银行.scm - 银行（官僚）
;; 只负责安全存放现金，不产生利息。为未来的现金风险预留位置。

(define bank
  (let ()
    (define balance 0)

    (define (node-deposit)
      (node "存款"
        :subtitle "把现金收进账户，安全存放，但不会生出利息"
        :requires (list (req-item "金钱" 100))
        :resolve (instant (lambda ()
                   (set! balance (+ balance 100))
                   (notify! (string-append "存入 100，储蓄账户余额 " (number->string balance) "。"))))))

    (define (node-withdraw)
      (instant-action "取款100"
        (lambda ()
          (cond
            ((>= balance 100)
             (set! balance (- balance 100))
             (add-item! "金钱" 100)
             (notify! (string-append "取出 100，储蓄账户余额 " (number->string balance) "。")))
            ((> balance 0)
             (add-item! "金钱" balance)
             (notify! (string-append "取出 " (number->string balance) "，账户清空。"))
             (set! balance 0))
            (#t (notify! "账户里没有钱。"))))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (container "银行"
                   (list (node-deposit) (node-withdraw)))))
          ((equal? msg 'save)
           (list (list "balance" balance)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! balance (assoc-get data "balance" 0))))
          (#t #f))))))
