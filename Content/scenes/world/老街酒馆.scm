;; scenes/world/老街酒馆.scm - 老街酒馆（劳工势力 / 夜莺故事舞台）

(define old-street-tavern
  (let ()
    (define closed-days 0)
    (define closed-days-max 2)

    ;; ── 生计工作 ──────────────────────────────────
    (define (node-waiter)
      (关系工作 "服务员" "劳工" '低 'social
        (outcome "手脚麻利" "跑了一晚上堂子，酒客赏钱都算在工钱里。"
          (lambda () (add-item! "金钱" 8)))
        (outcome "普通一班" "今晚的客人不多，工头按日结。"
          (lambda () (add-item! "金钱" 5)))
        (outcome "打翻酒杯" "一个醉客借故发作，你赔了一杯，也被骂了一顿。"
          (lambda () (stress-current-actor! 1)))))

    ;; ── 氛围与歇业 ────────────────────────────────
    (define (node-atmosphere)
      (observe-action "酒馆内景"
        "木桌被油灯照得发黄，角落里坐着几个不吭声的水手。驻唱的歌女今晚还没开嗓。"))

    (define (node-closed)
      (observe-action "酒馆歇业"
        "门板从里面上了闩。老板贴着告示：家中有事，歇业数日。"))

    (define (tavern-clocks)
      (if (> closed-days 0)
          (list (list 'clock "酒馆歇业" closed-days closed-days-max 'countdown
                      "歇业期间不能在这里做工或打听消息。"))
          '()))

    ;; ── 组装 ──────────────────────────────────────
    (define (tavern-children)
      (if (> closed-days 0)
          (list (node-closed))
          (append
            (nightingale 'tavern-nodes)
            (if (let ((stage (get-global '夜莺阶段)))
                  (and stage (>= stage 2)))
                (list (node-waiter))
                '())
            (nightingale 'beat1-lead-nodes)   ; 花消息买线索：向酒馆老主顾买准话
            (list (nightingale 'node-gossip) (node-atmosphere)))))

    (define-turn-rule "老街酒馆停业倒计时"
      (lambda () (> closed-days 0))
      (lambda () (set! closed-days (- closed-days 1))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node "老街酒馆" :children (tavern-children) :clocks (tavern-clocks))))
          ((equal? msg 'set-closed!)
           (set! closed-days (cadr args))
           (set! closed-days-max (max closed-days-max closed-days)))
          ((equal? msg 'save)
           (list (list "closed-days" closed-days)
                 (list "closed-days-max" closed-days-max)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! closed-days (assoc-get data "closed-days" 0))
             (set! closed-days-max (assoc-get data "closed-days-max" 2))))
          (#t #f))))))
