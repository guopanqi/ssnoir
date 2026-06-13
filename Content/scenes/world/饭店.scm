;; scenes/world/饭店.scm - Restaurant Sub-location

(define restaurant
  (lambda args
    (let ()
      ;; ── Node Definitions ──────────────────────────
      (define (node-eat-rice)
        (action "点一盘炒饭"
                (list (req-item '金钱 15))
                (instant (lambda ()
                           (heal-party! 1)
                           (let ((actor (current-actor)))
                             (set-actor-stress! actor (max 0 (- (actor-stress actor) 1))))
                           (notify! "热腾腾的炒饭下肚，身体暖洋洋的。生命值+1，压力-1。")))))

      (define (node-feast)
        (action "享用豪华大餐"
                (list (req-item '金钱 40))
                (instant (lambda ()
                           (heal-party! 3)
                           (let ((actor (current-actor)))
                             (set-actor-stress! actor (max 0 (- (actor-stress actor) 2))))
                           (notify! "精致而丰盛的美食极大地慰藉了身心。生命值+3，压力-2。")))))

      ;; ── Message Passing Interface ─────────────────
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list
             (container "饭店"
               (list
                 (observe-action "街角餐馆" "店里弥漫着家常饭菜的香气，几张油光发亮的木桌旁坐满了食客。")
                 (node-eat-rice)
                 (node-feast)))))

          ((equal? msg 'save)
           '())

          ((equal? msg 'load!)
           #f)

          (#t #f))))))
