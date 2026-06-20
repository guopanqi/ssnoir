;; scenes/world/test.scm - Test Area Sub-location

(define test
  (let ()
    ;; ── Local State ────────────────────────────────
    (define trash-count 0)

    (define (make-trash-nodes n)
      (if (<= n 0)
          '()
          (cons (instant-action (string-append "清理垃圾" (number->string n))
                                (lambda ()
                                  (set! trash-count (- trash-count 1))))
                (make-trash-nodes (- n 1)))))

    (define (node-kick-bin)
      (instant-action "踢垃圾桶"
        (lambda () (set! trash-count (+ trash-count 1)))))

    (define (node-odd-job)
      (instant-action "打零工"
        (lambda ()
          (add-item! '金钱 25))))

    (define (node-squander)
      (instant-action "花光所有钱"
        (lambda ()
          (remove-item! '金钱 (item-count '金钱)))))

    (define (node-rep-debugger)
      (container "声望测试面板"
        (list
          (instant-action "安抚工人 (+15声望)" (lambda () (change-reputation! "workers" 15)))
          (instant-action "激怒工人 (-35声望)" (lambda () (change-reputation! "workers" -35)))
          (instant-action "贿赂市长 (+15声望)" (lambda () (change-reputation! "mayor" 15)))
          (instant-action "得罪市长 (-15声望)" (lambda () (change-reputation! "mayor" -15)))
          (instant-action "讨好权贵 (+15声望)" (lambda () (change-reputation! "elites" 15)))
          (instant-action "疏远权贵 (-15声望)" (lambda () (change-reputation! "elites" -15))))))

    (define (node-growth-debugger)
      (container "成长测试面板"
        (list
          (instant-action "增加3点成长等级" (lambda () (set-growth-level! (+ (growth-level) 3)))))))

    ;; ── Message Passing Interface ─────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list
             (container "测试"
               (append
                 (cons (node-kick-bin)
                       (make-trash-nodes trash-count))
                  (list
                    (node-odd-job)
                    (node-squander)
                    (node-rep-debugger)
                    (node-growth-debugger))))))

          ((equal? msg 'save)
           (list
             (list "trash-count" trash-count)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! trash-count (assoc-get data "trash-count" 0))))

          (#t #f))))))
