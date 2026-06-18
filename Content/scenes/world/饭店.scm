;; scenes/world/饭店.scm

(define restaurant
  (let ()
    ;; ── Local State ───────────────────────────────────
    (define patronage-clock (make-clock "常客进度" 8 'segments))
    (define restaurant-stage 1)
    ;; stage: 1=常客积累中  2=情报已获

    ;; ── Node Helpers ──────────────────────────────────

    (define (node-eat)
      (action "用餐"
        (list (req-item "金钱" 12))
        (instant (lambda ()
          (patronage-clock 'tick!)
          (add-supplies! 3)
          (notify! "你点了一盘家常菜，老板娘殷勤地加了一道小菜。肚子填饱了，物资+3。")))))

    (define (node-get-info)
      (instant-action "和老板娘聊聊"
        (lambda ()
          (set! restaurant-stage 2)
          (mark-complete! 'restaurant-complete)
          (notify! "老板娘凑近低声说：'码头那边最近很乱，你要找人，去问老陈——他什么都知道。'"))))

    (define (node-restaurant-work)
      (roll-action "帮厨打杂" (list (req-die)) 'sharpness
        (lambda ()
          (stress-current-actor! 1)
          (notify! "手忙脚乱打翻了一盘菜，老板娘皱眉，没什么工钱。"))
        (lambda ()
          (patronage-clock 'tick!)
          (add-item! "金钱" 5)
          (notify! "度过了平稳的一天，老板娘结了点工钱。"))
        (lambda ()
          (patronage-clock 'tick!)
          (add-item! "金钱" 8)
          (notify! "客人夸你手脚快，老板娘多给了些打赏。"))))

    ;; ── Per-stage Children ────────────────────────────

    (define (stage-1-children)
      (if (patronage-clock 'full?)
          (list (node-get-info) (node-restaurant-work))
          (list (node-eat) (node-restaurant-work))))

    (define (node-restaurant-container)
      (cond
        ((= restaurant-stage 1)
         (container-with-clocks "饭店"
           (stage-1-children)
           (list (patronage-clock 'render-data))))
        (#t
         (container "饭店"
           (list
             (observe-action "老板娘" "老板娘冲你友好地点点头，没有更多要说的了。")
             (node-restaurant-work))))))

    ;; ── Message Passing Interface ─────────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node-restaurant-container)))

          ((equal? msg 'complete?)
           (= restaurant-stage 2))

          ((equal? msg 'save)
           (list
             (list "restaurant-stage"    restaurant-stage)
             (list "patronage-current"   (patronage-clock 'current))))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! restaurant-stage   (assoc-get data "restaurant-stage"  1))
             (patronage-clock 'set!   (assoc-get data "patronage-current" 0))))

          (#t #f))))))
