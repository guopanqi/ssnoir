;; 沃尔特（Walter Finch）——保险公司的理赔调查员。

(define walter
  (let ()
    ;; 0未登场 / 1待核赔 / 2有往来
    (define stage 0)
    (define claim-result "无")
    (define identity "保险公司的理赔调查员")

    (define (park-nodes)
      (if (and (= stage 0) (>= world-day 8))
          (list
            (node "和公园里的陌生人谈谈"
              :subtitle "一名穿着整齐、手里夹着保险宣传册的男人"
              :resolve (instant (lambda ()
                (set! stage 1)
                (play-dialogue!
                  (line "沃尔特" "沃尔特·芬奇。我替保险公司跑外勤。意外不会先敲门，保单至少会在事后出现。")
                  (line "主角" "我暂时不打算买保险。")
                  (line "沃尔特" "可惜。不过我听说，你替人查一些不愿意见光的事。")
                  (line "沃尔特" "码头有个人申请伤残理赔。表格很完整，他的伤却完整得让我起疑。你愿意替我看看吗？"))
                (spotlight! "沃尔特·芬奇" "你在公园认识了保险公司的理赔调查员沃尔特。保险公司开放，那里有一桩疑似骗保的案子。")))))
          '()))

    (define (on-claim-result result)
      (if (or (equal? result 'confirmed) (equal? result 'mercy))
          (begin
            (set! stage 2)
            (set! claim-result (if (equal? result 'confirmed) "坐实" "放水"))
            (change-faction-relation! "富商" (if (equal? result 'confirmed) 2 1))
            (complete-section!)
            (notify! "沃尔特记住了你处理这份理赔的方式。"))
          #f))

    (define (node-claim)
      (node "陪沃尔特核查一份伤残理赔"
        :subtitle identity
        :tags (list "交锋")
        :resolve (instant (lambda () (start-encounter "核赔" on-claim-result)))))

    (define (nodes)
      (append
        (if (>= stage 1)
            (list (node "沃尔特"
                    :subtitle identity
                    :resolve (observe (cond
                      ((= stage 1) "沃尔特永远礼貌，也永远像在心里填写另一张表。")
                      (else "核赔结束了。沃尔特记得你是怎样处理这份报告的。")))))
            '())
        (if (= stage 1) (list (node-claim)) '())))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'park-nodes) (park-nodes))
          ((equal? msg 'nodes) (nodes))
          ((equal? msg 'known?) (>= stage 1))
          ((equal? msg 'can-arrange-berth?) (= stage 2))
          ((equal? msg 'save) (list (list "stage" stage) (list "claim-result" claim-result)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! stage (assoc-get data "stage" 0))
             (set! claim-result (assoc-get data "claim-result" "无"))))
          (else #f))))))
