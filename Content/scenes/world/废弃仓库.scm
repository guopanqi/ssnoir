;; scenes/world/废弃仓库.scm - Abandoned Warehouse Sub-location

(define abandoned-warehouse
  (lambda args
    (let ()
      ;; ── Node Definitions ──────────────────────────
      (define (node-warehouse-job)
        (action "搬运废弃集装箱"
                (list (req-die))
                (roll 'violence
                      (lambda ()
                        (damage-party! 2)
                        (stress-current-actor! 2)
                        (notify! "集装箱意外倒塌！你受了重伤并且极度恐慌。生命值-2，压力+2，没有拿到报酬。"))
                      (lambda ()
                        (add-item! '金钱 20)
                        (damage-party! 1)
                        (notify! "搬运工作非常吃力，你不小心拉伤了肌肉。获得20金钱，生命值-1。"))
                      (lambda ()
                        (add-item! '金钱 50)
                        (notify! "凭着一身蛮力，你轻松完成了搬运。获得50金钱！")))))

      ;; ── Message Passing Interface ─────────────────
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list
             (container "废弃仓库"
               (list
                 (observe-action "阴暗的仓库" "这里堆满了锈迹斑斑的钢铁支架与废弃集装箱，地上随处可见散落的垃圾。")
                 (node-warehouse-job)))))

          ((equal? msg 'save)
           '())

          ((equal? msg 'load!)
           #f)

          (#t #f))))))
