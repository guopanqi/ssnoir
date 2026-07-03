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
        (outcome "一无所获" "仓库里一片寂静，什么异常都没找到。"
          (lambda () (stress-current-actor! 1)))
        (outcome "发现痕迹" "你发现了一些可疑痕迹，还需要继续深挖。"
          (lambda () (investigate-clock 'tick!)))
        (outcome "线索汇聚" "烟蒂、脚印、还有一截绳子——线索正在汇聚。"
          (lambda ()
            (investigate-clock 'tick!)
            (investigate-clock 'tick!)))))

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
          (mark-complete! 'warehouse-complete)
          (notify! "联络人接过钱，把一张纸条塞进你手里：'码头，去找老陈。'")))))

    (define (node-warehouse-work)
      (roll-action "搬运货物" (list (req-die)) 'violence
        (outcome "拉伤了腰" "货物太重，你勉强撑完一天，什么都没挣到。"
          (lambda () (stress-current-actor! 2)))
        (outcome "勉强做完" "完成了几趟，监工数了点工钱给你。"
          (lambda ()
            (stress-current-actor! 1)
            (add-item! "金钱" 10)))
        (outcome "手脚麻利" "监工很满意，工钱一分不少。"
          (lambda () (add-item! "金钱" 15)))))

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
           (list
             (observe-action "联络人留下的纸条"
               "纸条上只有两个字：老陈。")
             (node-warehouse-work))))))

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
