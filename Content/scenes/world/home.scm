;; scenes/world/home.scm - 住所系统
;; 旅馆（默认·付房租）→ 公寓（购买·资产中）→ 豪宅（购买·资产高）。
;; 资产等级由拥有的住所推导，写入全局 '资产（供富商圈门槛用）。
;; 恢复：旅馆睡觉压力 -1，自有住所睡觉压力 -2；门口露宿压力 +1。
;; 吃饭补饱腹，喝酒/唱片/花缓解压力。住所中只能用药恢复健康。

(define home
  (let ()
    ;; ── Local State ────────────────────────────────
    (define residence "旅馆")        ; "旅馆" / "公寓" / "豪宅"
    (define has-flower? #f)
    (define has-gramophone? #f)
    (define playing-song "")
    (define drank-today? #f)
    (define medicated-today? #f)

    ;; 房租（仅旅馆）：rent-due = 距交租还剩几天。归零没交 → 被赶出（软罚）。
    (define rent-due 3)
    (define rent-due-max 3)
    (define rent-amount 40)
    (define rent-extend 3)
    (define evicted? #f)

    ;; ── 资产推导 ────────────────────────────────────
    (define (sync-asset!)
      (set-global! '资产
        (cond ((equal? residence "豪宅") "高")
              ((equal? residence "公寓") "中")
              (else "低"))))
    (sync-asset!)

    (define (in-hotel?) (equal? residence "旅馆"))

    ;; ── Rules ──────────────────────────────────────
    ;; 房租只在住旅馆且未被赶出时流逝。
    (define-turn-rule "房租流逝"
      (lambda () (and (in-hotel?) (not evicted?)))
      (lambda ()
        (set! rent-due (- rent-due 1))
        (if (<= rent-due 0)
            (begin
              (set! rent-due 0)
              (set! evicted? #t)
              (notify! "你交不出房租，旅馆老板把门锁了。"))
            (if (<= rent-due 2)
                (notify! "房租快到期了，记得交租。")
                #f))))

    (define-turn-rule "每日恢复次数重置"
      (lambda () (or drank-today? medicated-today?))
      (lambda ()
        (set! drank-today? #f)
        (set! medicated-today? #f)))

    (define (rent-render-data)
      (list 'clock "房租到期" rent-due rent-due-max 'countdown
            "归零后旅馆房门会被锁上；交租可延长三天。"))

    ;; ── 恢复类 ──────────────────────────────────────
    (define (node-eat)
      (action "吃饭"
        (list (req-item "食物" 1))
        (instant (lambda ()
                   (add-satiety! 3)
                   (notify! "你给自己做了顿饭，饱腹恢复了一些。")))))

    ;; 看花 / 听唱片这类白天解压占一颗骰子（与工作争夺骰子池）；睡觉免费（回合结束）。
    ;; 喝酒不占骰子，走“花钱买酒”这条线：垫点饱腹，松松神经。
    (define (node-drink)
      (node "喝酒"
        :subtitle (if drank-today?
                      "今天已经喝过了，再喝只会头疼"
                      "一杯能让神经松下来，也稍微垫垫肚子")
        :disabled drank-today?
        :requires (list (req-item "酒" 1))
        :resolve (instant
          (outcome "借酒松神" "一杯下肚，紧绷的神经松了些，肚子也垫了垫。"
            (lambda ()
              (set! drank-today? #t)
              (add-satiety! 1)
              (heal-stress! 'player 2))))))

    ;; 用药：在住所中上药休养，不占用行动骰。
    (define (node-use-medicine)
      (node "用药"
        :subtitle (if medicated-today?
                      "一天上一次药就够了，伤口需要时间"
                      "处理伤口不占行动，但一天只能用一份")
        :disabled medicated-today?
        :requires (list (req-item "药品" 1))
        :resolve (instant (lambda ()
                   (set! medicated-today? #t)
                   (heal-party! 3)
                   (notify! "上了药、包扎好，伤口松快了些。")))))

    (define (node-see-flower)
      (action "看花"
        (list (req-die))
        (instant
          (outcome "出神片刻" "白色的雏菊静静开着。你出神看了一会儿，心里松快了些。"
            (lambda () (heal-stress! 'player 2))))))

    (define (format-song-name name)
      (if (equal? playing-song name) (string-append "-> " name) name))

    ;; 听唱片：占一颗骰子，比看花更能缓神（值回那台机器的钱）。
    (define (song-action name)
      (action (format-song-name name)
        (list (req-die))
        (instant
          (outcome "乐声流淌" "针尖落下，旧曲子转起来。你靠在椅背上，跟着晃了晃。"
            (lambda ()
              (set! playing-song name)
              (heal-stress! 'player 3))))))

    (define (node-gramophone)
      (container "唱片机"
        (list
          (song-action "《甜蜜蜜》")
          (song-action "《怒放的生命》")
          (song-action "《爵士舞曲》")
          (instant-action "停止播放"
            (lambda () (set! playing-song ""))))))

    (define (rest-tags)
      (if (rest-blocked?)
          (append (list "不可休息") (rest-block-reasons))
          '()))

    (define (node-sleep)
      (node "睡觉"
        :disabled (rest-blocked?)
        :tags (rest-tags)
        :resolve (instant
          (outcome
            (if (in-hotel?) "睡了一夜" "安稳休息")
            (if (in-hotel?)
                "旅馆的床不算舒服，但至少能遮风挡雨。"
                "这是属于你的住所，你终于能安稳睡下。")
            (lambda ()
              (heal-stress! 'player (if (in-hotel?) 1 2))
              (if (has-companion? 'laozhou) (heal-stress! 'laozhou 1) #f)
              (end-turn!))))))

    (define (node-sleep-at-door)
      (node "蜷缩在门口"
        :disabled (rest-blocked?)
        :tags (rest-tags)
        :resolve (instant
          (outcome "无处可去"
                   "房门锁着。你靠着墙蜷了一夜，寒气一直往衣服里钻。"
            (lambda ()
              (add-actor-stress! 'player 1)
              (end-turn!))))))

    ;; ── 交易 / 布置 / 升级 ──────────────────────────
    (define (node-pay-rent)
      (node "交租"
        :subtitle "再安心住三天，房门锁了也能重新进去"
        :requires (list (req-item "金钱" rent-amount))
        :resolve (instant (lambda ()
                   (set! rent-due (+ rent-due rent-extend))
                   (set! rent-due-max rent-due)
                   (set! evicted? #f)
                   (notify! (string-append "你交了 " (number->string rent-amount)
                                           " 金钱房租，租期延到 "
                                           (number->string rent-due) " 天。"))))))

    ;; 买酒：可反复购买的消耗品。带回家喝 → 少量饱腹 + 解压（不占骰子的解压路子）。
    (define (node-buy-liquor)
      (node "买酒"
        :subtitle "给夜里留点松快，也能稍微垫垫肚子"
        :requires (list (req-item "金钱" 8))
        :resolve (instant (lambda () (add-item! "酒" 1) (notify! "打了一壶酒，搁在柜子里。")))))

    (define (node-buy-flower)
      (node "买一盆花"
        :subtitle "窗台多点生气，烦闷时可以坐着看一会儿"
        :requires (list (req-item "金钱" 15))
        :resolve (instant (lambda () (set! has-flower? #t) (notify! "你买了一盆雏菊，摆在窗台。")))))

    (define (node-buy-gramophone)
      (node "买台唱片机"
        :subtitle "在家听几首旧歌，比看花更能让人放松"
        :requires (list (req-item "金钱" 60))
        :resolve (instant (lambda () (set! has-gramophone? #t) (notify! "一台旧唱片机，还能转。")))))

    (define (node-buy-apartment)
      (node "买下公寓"
        :subtitle "有个自己的家，不再交房租，也能睡得更安稳"
        :requires (list (req-item "金钱" 120))
        :resolve (instant (lambda ()
                   (set! residence "公寓")
                   (sync-asset!)
                   (notify! "你签下了公寓。不用再看旅馆老板的脸色了。")))))

    (define (node-buy-mansion)
      (node "买下豪宅"
        :subtitle "住进富人区，那些只看身份的门也会向你打开"
        :requires (list (req-item "金钱" 400))
        :resolve (instant (lambda ()
                   (set! residence "豪宅")
                   (sync-asset!)
                   (notify! "富人飞地的一栋豪宅。你成了这里的新住户。")))))

    ;; ── 组装子节点 ──────────────────────────────────
    ;; 客厅：日常恢复 + 已拥有的家具。
    (define (living-room-children)
      (append
        (list (node-eat) (node-drink) (node-use-medicine))
        (if has-flower? (list (node-see-flower)) '())
        (if has-gramophone? (list (node-gramophone)) '())))

    (define (node-living-room)
      (container "客厅" (living-room-children)))

    ;; 订购：购买入口（以后可能挪到市集）。买酒可反复买，花/唱片机买过即消失。
    (define (order-children)
      (append
        (list (node-buy-liquor))
        (if has-flower? '() (list (node-buy-flower)))
        (if has-gramophone? '() (list (node-buy-gramophone)))))

    (define (order-nodes)
      (if (null? (order-children)) '() (list (container "订购" (order-children)))))

    (define (upgrade-nodes)
      (cond ((equal? residence "旅馆") (list (node-buy-apartment)))
            ((equal? residence "公寓") (list (node-buy-mansion)))
            (else '())))

    (define (hotel-body)
      (if evicted?
          (list (observe-action "锁着的房门" "先把房租交了才能回去。")
                (node-pay-rent) (node-sleep-at-door))
          (append
            (list (node-living-room))
            (order-nodes)
            (list (node-pay-rent))
            (upgrade-nodes)
            (list (node-sleep)))))

    (define (owned-body)
      (append
        (list (node-living-room))
        (order-nodes)
        (upgrade-nodes)
        (list (node-sleep))))

    ;; 容器名固定为“家”（导航按名字定位，不能随住所变），住所等级放 subtitle 显示。
    (define (residence-container)
      (if (in-hotel?)
          (node "家" :subtitle residence :children (hotel-body) :clocks (list (rent-render-data)))
          (node "家" :subtitle residence :children (owned-body))))

    ;; ── Message Passing Interface ─────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (residence-container)))

          ((equal? msg 'save)
           (list
             (list "residence"      residence)
             (list "has-flower?"    has-flower?)
             (list "has-gramophone?" has-gramophone?)
             (list "playing-song"   playing-song)
             (list "drank-today?" drank-today?)
             (list "medicated-today?" medicated-today?)
             (list "rent-due"       rent-due)
             (list "rent-due-max"   rent-due-max)
             (list "evicted?"       evicted?)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! residence      (assoc-get data "residence" "旅馆"))
             (set! has-flower?    (assoc-get data "has-flower?" #f))
             (set! has-gramophone? (assoc-get data "has-gramophone?" #f))
             (set! playing-song   (assoc-get data "playing-song" ""))
             (set! drank-today?  (assoc-get data "drank-today?" #f))
             (set! medicated-today? (assoc-get data "medicated-today?" #f))
             (set! rent-due       (assoc-get data "rent-due" 3))
             (set! rent-due-max   (assoc-get data "rent-due-max" 3))
             (set! evicted?       (assoc-get data "evicted?" #f))
             (sync-asset!)))

          (#t #f))))))
