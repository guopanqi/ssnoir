;; scenes/world/home.scm - 住所系统
;; 旅馆（默认·付房租）→ 公寓（购买·资产中）。
;; 资产等级由拥有的住所推导，写入全局 '资产（供富商圈门槛用）。
;; 恢复：旅馆睡觉冷静 +1，自有住所睡觉冷静 +2；门口露宿冷静 -1。
;; 喝酒/看花/收拾屋子恢复冷静。酒会让下一次城市骰池出现“宿醉”降质；住所中只能用药恢复健康。

(define home
  (let ()
    ;; ── Local State ────────────────────────────────
    (define residence "旅馆")        ; "旅馆" / "公寓"
    (define has-flower? #f)
    (define drank-today? #f)
    (define medicated-today? #f)
    (define tidied-today? #f)

    ;; 房租（仅旅馆）：rent-due = 距交租还剩几天。归零没交 → 被赶出（软罚）。
    (define rent-due 3)
    (define rent-due-max 3)
    (define rent-amount 40)
    (define rent-extend 3)
    (define flower-price 40)
    (define evicted? #f)

    ;; ── 资产推导 ────────────────────────────────────
    (define (sync-asset!)
      (set-global! '资产
        (if (equal? residence "公寓") "中" "低")))
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
              (notify! "房租凑不齐，旅馆老板一声不吭把门锁上了。"))
            (if (<= rent-due 2)
                (notify! "房租快到期了，记得交租。")
                #f))))

    (define-turn-rule "每日恢复次数重置"
      (lambda () (or drank-today? medicated-today? tidied-today?))
      (lambda ()
        (set! drank-today? #f)
        (set! medicated-today? #f)
        (set! tidied-today? #f)))

    (define (rent-render-data)
      (list 'clock "房租到期" rent-due rent-due-max 'countdown
            "归零后旅馆房门会被锁上；交租可延长三天。"))

    ;; ── 恢复类 ──────────────────────────────────────
    ;; 看花这类白天解压占一颗骰子（与工作争夺骰子池）；睡觉免费（回合结束）。
    ;; 喝酒不占骰子，走“花钱买酒”这条线：当场大量恢复冷静。
    ;; 效果只写这一份：酒馆当场点酒（'drink! 消息）与家中喝自带的酒共用同一次“当天第一杯”。
    (define (apply-drink-effect!)
      (set! drank-today? #t)
      (restore-actor-composure! 'player 2)
      (apply-hangover!))

    (define (node-drink)
      (node "喝酒"
        :subtitle (if drank-today?
                      "今天已经喝过了，再喝只会头疼"
                      "恢复 2 点冷静；代价留到下一次城市骰池")
        :disabled drank-today?
        :requires (list (req-item "酒" 1))
        :resolve (instant
          (outcome "借酒松神" "一杯下肚，绷着的神经松了扣。明早的头痛，会再来讨账。"
            (lambda () (apply-drink-effect!))))))

    ;; 用药：在住所中上药休养，不占用行动骰。
    (define (node-use-medicine)
      (node "用药"
        :subtitle (if medicated-today?
                      "一天上一次药就够了，伤口需要时间"
                      "处理伤口不占行动，但一天只能用一份")
        :disabled medicated-today?
        :requires (list (req-item "药品" 1))
        :resolve (instant
          (outcome "上了药" "重新缠好绷带，伤口总算消停些。"
            (lambda ()
              (set! medicated-today? #t)
              (heal-party! 3))))))

    (define (node-see-flower)
      (action "看花"
        (list (req-die))
        (instant
          (outcome "出神片刻" "白雏菊静静开着，不管窗外这座城多脏。你看了一会儿，胸口松了些。"
            (lambda () (restore-actor-composure! 'player 2))))))

    ;; 给低质量骰一个确定而克制的去处：不掷命运骰，只用时间换少量恢复。
    (define (node-tidy-room)
      (node "整理房间"
        :subtitle (if tidied-today?
                      "今天已经收拾过了"
                      "投入任意行动骰，固定恢复 1 点冷静；每天一次")
        :disabled tidied-today?
        :requires (list (req-die))
        :resolve (instant
          (outcome "收拾妥当" "把散乱的物件一件件归位，脑子里那些声音也跟着安静了一点。"
            (lambda ()
              (set! tidied-today? #t)
              (restore-actor-composure! 'player 1))))))

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
                "旅馆的床硬得像良心，可好歹遮风挡雨。"
                "这是你自己的地方，门一关，城就锁在外头了。")
            (lambda ()
              (restore-actor-composure! 'player (if (in-hotel?) 1 2))
              (if (has-companion? 'laozhou) (restore-actor-composure! 'laozhou 1) #f)
              (end-turn!))))))

    (define (node-sleep-at-door)
      (node "蜷缩在门口"
        :disabled (rest-blocked?)
        :tags (rest-tags)
        :resolve (instant
          (outcome "无处可去"
                   "门从里头锁死了。你缩在墙根挨了一夜，寒气顺着衣领一路往骨头里钻。"
            (lambda ()
              (spend-actor-composure! 'player 1)
              (end-turn!))))))

    ;; ── 交易 / 布置 / 升级 ──────────────────────────
    (define (node-pay-rent)
      (node "交租"
        :subtitle "再安心住三天，房门锁了也能重新进去"
        :requires (list (req-item "金钱" rent-amount))
        :resolve (instant
          (let ((rent-due-after (+ rent-due rent-extend)))
            (outcome "交了房租"
              (string-append "你交了 " (number->string rent-amount)
                             " 金钱房租，租期延到 "
                             (number->string rent-due-after) " 天。")
              (lambda ()
                (set! rent-due rent-due-after)
                (set! rent-due-max rent-due-after)
                (set! evicted? #f)))))))

    (define (node-buy-flower)
      (node "买一盆花"
        :subtitle "自己的窗台才摆得下这点闲心；烦闷时可以坐着看一会儿"
        :requires (list (req-item "金钱" flower-price))
        :resolve (instant
          (outcome "买了一盆花" "你买了一盆雏菊，摆上窗台，让这屋子有点活气。"
            (lambda () (set! has-flower? #t))))))

    (define (node-buy-apartment)
      (node "买下公寓"
        :subtitle "有个自己的家，不再交房租，也能睡得更安稳"
        :requires (list (req-item "金钱" 120))
        :resolve (instant
          (outcome "签下了公寓" "你签下了公寓。往后不必再看旅馆老板那张脸了。"
            (lambda ()
              (set! residence "公寓")
              (sync-asset!))))))

    ;; ── 组装子节点 ──────────────────────────────────
    ;; 客厅：日常恢复 + 已拥有的家具。
    (define (living-room-children)
      (append
        (list (node-drink) (node-use-medicine) (node-tidy-room))
        (if has-flower? (list (node-see-flower)) '())))

    (define (node-living-room)
      (container "客厅" (living-room-children)))

    ;; 订购：只属于自有住所。花买过即消失；买酒已挪到老街酒馆。
    (define (order-children)
      (if has-flower? '() (list (node-buy-flower))))

    (define (order-nodes)
      (if (null? (order-children)) '() (list (container "订购" (order-children)))))

    (define (upgrade-nodes)
      (if (equal? residence "旅馆") (list (node-buy-apartment)) '()))

    (define (hotel-body)
      (if evicted?
          (append
            (list (observe-action "锁着的房门" "先把房租交了，或者干脆买下一处不用看人脸色的地方。")
                  (node-pay-rent))
            (upgrade-nodes)
            (list (node-sleep-at-door)))
          (append
            (list (node-living-room))
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

          ;; 给其他地点（如老街酒馆的“点一杯酒”）查询/触发同一份每日一杯限制。
          ((equal? msg 'drank-today?) drank-today?)
          ((equal? msg 'drink!) (apply-drink-effect!))

          ((equal? msg 'save)
           (list
             (list "residence"      residence)
             (list "has-flower?"    has-flower?)
             (list "drank-today?" drank-today?)
             (list "medicated-today?" medicated-today?)
             (list "tidied-today?" tidied-today?)
             (list "rent-due"       rent-due)
             (list "rent-due-max"   rent-due-max)
             (list "evicted?"       evicted?)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! residence      (assoc-get data "residence" "旅馆"))
             ;; 旧档兼容：豪宅档位已删除，映射回公寓。
             (if (equal? residence "豪宅") (set! residence "公寓") #f)
             (set! has-flower?    (assoc-get data "has-flower?" #f))
             (set! drank-today?  (assoc-get data "drank-today?" #f))
             (set! medicated-today? (assoc-get data "medicated-today?" #f))
             (set! tidied-today? (assoc-get data "tidied-today?" #f))
             (set! rent-due       (assoc-get data "rent-due" 3))
             (set! rent-due-max   (assoc-get data "rent-due-max" 3))
             (set! evicted?       (assoc-get data "evicted?" #f))
             (sync-asset!)))

          (#t #f))))))
