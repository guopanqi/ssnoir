;; scenes/world/诊所.scm - Clinic Sub-location

(define clinic
  (lambda args
    (let ()
      ;; ── Node Definitions ──────────────────────────
      (define (node-buy-medkit)
        (action "购买急救包"
                (list (req-item '金钱 20))
                (instant (lambda ()
                           (add-item! '急救包 1)
                           (notify! "购买了1个急救包。")))))

      (define (node-buy-painkiller)
        (action "购买止痛药"
                (list (req-item '金钱 10))
                (instant (lambda ()
                           (add-item! '止痛药 1)
                           (notify! "购买了1个止痛药。")))))

      (define (node-heal-direct)
        (action "接受医师治疗"
                (list (req-item '金钱 30))
                (instant (lambda ()
                           (heal-party! 2)
                           (let ((actor (current-actor)))
                             (set-actor-stress! actor (max 0 (- (actor-stress actor) 1))))
                           (notify! "经过医生的专业包扎与心理疏导，生命值+2，压力-1。")))))

      ;; ── Message Passing Interface ─────────────────
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list
             (container "诊所"
               (list
                 (observe-action "社区诊所" "空气中弥漫着苏打水与消毒液的刺鼻气味，穿着白大褂的医生正在整理药柜。")
                 (node-buy-medkit)
                 (node-buy-painkiller)
                 (node-heal-direct)))))

          ((equal? msg 'save)
           '())

          ((equal? msg 'load!)
           #f)

          (#t #f))))))
