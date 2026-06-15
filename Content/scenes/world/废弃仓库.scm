;; scenes/world/废弃仓库.scm

(define abandoned-warehouse
  (let ()
    ;; ── Local State ───────────────────────────────────
    (define investigate-clock (make-clock "调查进度" 4 'segments))
    (define warehouse-stage 1)
    ;; stage: 1=调查中  2=联络人出现  3=情报已购

    ;; ── Node Helpers ──────────────────────────────────

    (define (node-search-clues)
      (roll-action "搜寻线索" (list (req-die)) 'sharpness
        (lambda ()
          (notify! "仓库里一片寂静，什么异常都没找到。"))
        (lambda ()
          (investigate-clock 'tick!)
          (notify! "你发现了一些可疑痕迹，还需要继续深挖。"))
        (lambda ()
          (investigate-clock 'tick!)
          (investigate-clock 'tick!)
          (notify! "烟蒂、脚印、还有一截绳子——线索正在汇聚。"))))

    (define (node-contact-appears)
      (instant-action "跟踪线索"
        (lambda ()
          (set! warehouse-stage 2)
          (notify! "一个黑影从破损的天花板下走出来，低声说：'你在找什么？'"))))

    (define (node-pay-contact)
      (action "支付情报费（150金）"
        (list (req-item "金钱" 150))
        (instant (lambda ()
          (set! warehouse-stage 3)
          (notify! "联络人接过钱，把一张纸条塞进你手里：'码头，去找老陈。'")))))

    (define (node-warehouse-work)
      (roll-action "搬运货物" (list (req-die)) 'violence
        (lambda ()
          (stress-current-actor! 2)
          (notify! "货物太重，你拉伤了腰，勉强撑完一天，什么都没挣到。"))
        (lambda ()
          (add-item! "金钱" 30)
          (notify! "完成了几趟，监工数了点工钱给你。"))
        (lambda ()
          (add-item! "金钱" 60)
          (notify! "手脚麻利，监工满意，工钱一分不少。"))))

    ;; ── Per-stage Children ────────────────────────────

    (define (stage-1-children)
      (list
        (if (investigate-clock 'full?)
            (node-contact-appears)
            (node-search-clues))
        (node-warehouse-work)))

    (define (stage-2-children)
      (list (node-pay-contact) (node-warehouse-work)))

    (define (node-warehouse-container)
      (cond
        ((= warehouse-stage 1)
         (container-with-clocks "废弃仓库"
           (stage-1-children)
           (list (investigate-clock 'render-data))))
        ((= warehouse-stage 2)
         (container "废弃仓库"
           (stage-2-children)))
        (#t
         (container "废弃仓库"
           (list (observe-action "联络人留下的纸条"
                   "纸条上只有两个字：老陈。"))))))

    ;; ── Message Passing Interface ─────────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node-warehouse-container)))

          ((equal? msg 'complete?)
           (= warehouse-stage 3))

          ((equal? msg 'save)
           (list
             (list "warehouse-stage"     warehouse-stage)
             (list "investigate-current" (investigate-clock 'current))))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! warehouse-stage    (assoc-get data "warehouse-stage"     1))
             (investigate-clock 'set! (assoc-get data "investigate-current" 0))))

          (#t #f))))))
