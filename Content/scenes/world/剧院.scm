;; scenes/world/剧院.scm - Theater location
;; 剧院承载：经理（委托人 + 耐心 clock + 交付回血）、夜莺、看门人，
;; 以及"黑衣人的足迹"——它向世界层发信号生成追击节点（见 world.scm）。

(define theater
  (let ()
    ;; ── Local State ────────────────────────────────
    (define theater-stage 1)
    ;; 1=未进门  2=已进门(待简报)  3=已简报(足迹可查)  4=足迹已查(追击在世界层)

    ;; 看门人：进门前是检定行动，进门后翻成观察（同名 → 同 anchor）。
    (define (node-gatekeeper-action)
      (action "看门人"
        (list (req-die))
        (roll 'sharpness
          (outcome "态度恶劣" "看门人上下打量你，没好气地放你进去。"
            (lambda ()
              (set! theater-stage 2)
              (stress-current-actor! 1)))
          (outcome "勉强放行" "看门人敷衍地摆摆手，你推门走了进去。"
            (lambda ()
              (set! theater-stage 2)))
          (outcome "客气放行" "看门人很客气地为你侧身开门。"
            (lambda ()
              (set! theater-stage 2))))))

    (define (node-gatekeeper-observe)
      (observe-action "看门人" "他守在门口，已经认得你了，不再多问。"))

    ;; 经理：有待交付进展时是"交付"行动，否则是观察（始终同名"经理"）。
    (define (node-manager)
      (if (has-pending-report?)
          (instant-action "经理"
            (lambda () (deliver-report!)))
          (observe-action "经理"
            "经理叼着雪茄。'有进展了再来找我，别空着手。'")))

    ;; 夜莺：简报前可对话（推进剧情），之后变观察。
    (define (node-nightingale-talk)
      (instant-action "夜莺"
        (lambda ()
          (set! theater-stage 3)
          (notify! "夜莺压低声音说起那封勒索信：有人翻出了她的旧账，要她在码头的邮箱里放钱。"))))

    (define (node-nightingale-observe)
      (observe-action "夜莺" "夜莺整理着演出服，时不时望向门口。"))

    ;; 黑衣人的足迹：点一次 → 世界层生出追击节点，自己翻成观察。
    (define (node-trail)
      (instant-action "黑衣人的足迹"
        (lambda ()
          (set! theater-stage 4)
          (reveal-chase!)
          (notify! "送信的黑影刚走不久，泥印还湿着——他往剧院外的小巷去了。"))))

    (define (node-trail-done)
      (observe-action "渐淡的足迹" "泥印一路延向剧院外的小巷。"))

    (define (node-gate name-children)
      (container "大门" name-children))

    ;; ── Per-stage Children ────────────────────────
    (define (theater-children)
      (cond
        ;; 追击完成 → 前情结束，回到稳定状态：门开着，直接找经理/夜莺，不再绕弯。
        ((equal? chase-state "done")
         (list (node-gate (list (node-nightingale-observe) (node-manager)))))
        ((= theater-stage 1)
         (list (node-gatekeeper-action)))
        ((= theater-stage 2)
         (list
           (node-gatekeeper-observe)
           (node-gate (list (node-nightingale-talk) (node-manager)))))
        ((= theater-stage 3)
         (list
           (node-gatekeeper-observe)
           (node-gate (list (node-nightingale-observe) (node-manager)))
           (node-trail)))
        (#t
         (list
           (node-gatekeeper-observe)
           (node-gate (list (node-nightingale-observe) (node-manager)))
           (node-trail-done)))))

    (define (node-theater-container)
      (container-with-clocks "剧院"
        (theater-children)
        (list (patience-render-data))))

    ;; ── Message Passing Interface ─────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node-theater-container)))

          ((equal? msg 'save)
           (list
             (list "theater-stage" theater-stage)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! theater-stage (assoc-get data "theater-stage" 1))))

          (#t #f))))))
