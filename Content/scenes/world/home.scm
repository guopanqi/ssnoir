;; scenes/world/home.scm - House Scene

(define home
  (let ()
    ;; ── Local State ────────────────────────────────
    (define has-flower? #f)
    (define has-gramophone? #f)
    (define playing-song "")

    ;; 房租：家自己完全拥有。rent-due = 距交租还剩几天（countdown）。
    ;; 交租 → rent-due += 6，rent-due-max 跟着跳（动态最大值）。归零没交 → 被赶出家（软罚）。
    (define rent-due 6)
    (define rent-due-max 6)
    (define rent-amount 40)
    (define rent-extend 6)
    (define evicted? #f)

    (define (rent-render-data)
      (list 'clock "房租到期" rent-due rent-due-max 'countdown))

    ;; ── Rules ──────────────────────────────────────
    (define-turn-rule "房租流逝"
      (lambda () (not evicted?))
      (lambda ()
        (set! rent-due (- rent-due 1))
        (if (<= rent-due 0)
            (begin
              (set! rent-due 0)
              (set! evicted? #t)
              (notify! "你交不出房租，房东把你的家锁了起来。"))
            (if (<= rent-due 2)
                (notify! "房租快到期了，记得回家找房东交租。")
                #f))))

    ;; ── Node Definitions ──────────────────────────
    (define (node-flower)
      (observe-action "一盆花" "一盆散发着淡淡微香的白色雏菊，正静静地盛开着。"))

    ;; 客厅：吃饭 / 喝酒，都是消耗资源换恢复。
    (define (node-eat)
      (action "吃饭"
              (list (req-item "食物" 1))
              (instant
                (lambda ()
                  (add-supplies! 3)
                  (notify! "你给自己做了顿饭，饱腹恢复了一些。")))))

    (define (node-drink-wine)
      (action "喝酒"
              (list (req-item '酒 1))
              (instant (lambda ()
                         (heal-party! 1)))))

    (define (node-living-room)
      (container "客厅"
        (list (node-eat) (node-drink-wine))))

    (define (format-song-name name)
      (if (equal? playing-song name)
          (string-append "-> " name)
          name))

    (define (node-gramophone)
      ;; 这会改变名字/ID, 会有一些麻烦, 但现在先不管
      (container "唱片机"
        (list
          (instant-action (format-song-name "《甜蜜蜜》")
                          (lambda () (set! playing-song "《甜蜜蜜》")))
          (instant-action (format-song-name "《怒放的生命》")
                          (lambda () (set! playing-song "《怒放的生命》")))
          (instant-action (format-song-name "《爵士舞曲》")
                          (lambda () (set! playing-song "《爵士舞曲》")))
          (instant-action "停止播放"
                          (lambda () (set! playing-song ""))))))

    ;; 房东：可主动交租。交一次租期延长，并解除被锁状态。
    (define (node-landlord)
      (action "找房东交租"
              (list (req-item "金钱" rent-amount))
              (instant
                (lambda ()
                  (set! rent-due (+ rent-due rent-extend))
                  (set! rent-due-max rent-due)
                  (set! evicted? #f)
                  (notify! (string-append "你交了 " (number->string rent-amount)
                                          " 金钱房租，租期延到 "
                                          (number->string rent-due) " 天。"))))))

    (define (node-sleep)
      (instant-action "睡觉"
                      (lambda () (end-turn!))))

    ;; 家：常驻显示房租 clock。被赶出后只剩门口 + 交租 + 睡觉。
    (define (home-children)
      (if evicted?
          (list
            (observe-action "锁着的家门" "房东把门锁了。先把房租交了才能回去。")
            (node-landlord)
            (node-sleep))
          (append
            (if has-flower? (list (node-flower)) '())
            (if has-gramophone? (list (node-gramophone)) '())
            (list (node-living-room) (node-landlord) (node-sleep)))))

    ;; ── Message Passing Interface ─────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list
             (container-with-clocks "家"
               (home-children)
               (list (rent-render-data)))))

          ((equal? msg 'has-gramophone?) has-gramophone?)
          ((equal? msg 'buy-gramophone!) (set! has-gramophone? #t))

          ((equal? msg 'has-flower?) has-flower?)
          ((equal? msg 'buy-flower!) (set! has-flower? #t))

          ((equal? msg 'save)
           (list
             (list "has-flower?" has-flower?)
             (list "has-gramophone?" has-gramophone?)
             (list "playing-song" playing-song)
             (list "rent-due" rent-due)
             (list "rent-due-max" rent-due-max)
             (list "evicted?" evicted?)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! has-flower? (assoc-get data "has-flower?" #f))
             (set! has-gramophone? (assoc-get data "has-gramophone?" #f))
             (set! playing-song (assoc-get data "playing-song" ""))
             (set! rent-due (assoc-get data "rent-due" 6))
             (set! rent-due-max (assoc-get data "rent-due-max" 6))
             (set! evicted? (assoc-get data "evicted?" #f))))

          (#t #f))))))
