;; 夜莺——她拥有的只有一样东西：**她和尼尔之间还剩多少「我们」**。
;;
;; 第一章她需要他。第二章她走进了自己盼了六年的地方，而他越来越看得见那地方背后是什么。
;; 所以这一章不给她好结局或坏结局，只让那道缝隙一格一格张开——第三章才问真正的问题：
;; 她最后会不会自己开口。
;;
;; 这个模块只放**跨章节的事实**，不放任何一章的过程。哪天她在剧院排练、某场谈话有没有发生，
;; 属于那件事自己的文件；这里只收「后面几章还会问」的那一两条。
;; 接口刻意开得很窄：一条线上每多一条消息，就多一个以后要维护的承诺。

(define nightingale
  (let ()
    ;; 站在一起的次数减去走开的次数。正数＝还是「我们」，负数＝已经不是了。
    ;; 第三章读它，第二章只往里写。
    (define 我们 0)

    (define (记!  n) (set! 我们 (+ 我们 n)))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'on-banquet-stayed!) (记! 1))
          ((equal? msg 'on-drifted-apart!) (记! -1))
          ((equal? msg 'together) 我们)
          ((equal? msg 'save) (list (list "together" 我们)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! 我们 (assoc-get data "together" 0))))
          (else (error "夜莺：收到未知消息")))))))
