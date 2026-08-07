;; scenes/world/home.scm - 住所系统
;; 旅馆（默认·每 4 天交一次房租，交不上先动用老板的宽限）→ 公寓（140 金买断·资产中）。
;; 资产等级由拥有的住所推导，写入全局 '资产（供富商圈门槛用）。
;; 恢复：旅馆睡觉冷静 +2，自有住所睡觉回满（3）；门口露宿不回冷静。
;; 喝酒/看花恢复冷静；日常投入行动骰的恢复以公园散步为主。
;; 酒会让下一次城市骰池出现“宿醉”降质。
;; 伤势：睡觉只压得动轻伤（每晚 1 点），重伤得去诊所；住所里的用药压 2 点、每天一份。

(define home
  (let ()
    ;; ── Local State ────────────────────────────────
    (define residence "旅馆")        ; "旅馆" / "公寓"
    (define has-flower? #f)
    (define drank-today? #f)
    (define medicated-today? #f)

    ;; 房租（仅旅馆）：一根固定 4 天的倒计时，交一次重置一次。不做"可提前交、
    ;; 上限跟着涨"的可变时钟——那样玩家攒够钱就一次买断好几个周期，房租不再是每周期
    ;; 都要重新面对的一件事，读起来也不像房租，像预付卡。
    ;;
    ;; 到期交不上不立刻赶人，而是动用**老板的宽限**：这是一条会记住你的信用线。
    ;;   宽限 2 → 他等你两天    宽限 1 → 他等你一天    宽限 0 → 当天锁门
    ;; 准时交一次，宽限 +1（上限 2）；拖到宽限期里才交，宽限 −1（下限 0）。
    ;; 所以第一次拖欠的代价不是钱，是**下一次没人等你了**——欠账的真实形状。
    (define rent-cycle 4)
    (define rent-amount 30)
    (define rent-left rent-cycle)   ; 距交租还剩几天
    (define grace-max 2)
    (define grace grace-max)        ; 老板还愿意等你几天（信用）
    (define overdue? #f)            ; 已经到期没交，正在用宽限
    (define grace-left 0)           ; 宽限还剩几天
    (define flower-price 40)
    (define evicted? #f)

    ;; ── 资产推导 ────────────────────────────────────
    (define (sync-asset!)
      (set-global! '资产
        (if (equal? residence "公寓") "中" "低")))
    (sync-asset!)

    (define (in-hotel?) (equal? residence "旅馆"))

    ;; ── Rules ──────────────────────────────────────
    (define (evict!)
      (set! evicted? #t)
      (set! overdue? #f)
      (set! grace-left 0)
      (notify! "房租没交上，旅馆老板一声不吭把门锁上了。"))

    ;; 房租只在住旅馆且未被赶出时流逝。到期那天不锁门：先看老板还愿意等几天。
    (define-turn-rule "房租流逝"
      (lambda () (and (in-hotel?) (not evicted?)))
      (lambda ()
        (if overdue?
            (begin
              (set! grace-left (- grace-left 1))
              (if (<= grace-left 0)
                  (evict!)
                  (notify! (string-append "老板又来敲了一次门。他还能等 "
                                          (number->string grace-left) " 天。"))))
            (begin
              (set! rent-left (- rent-left 1))
              (cond
                ((> rent-left 0)
                 (if (<= rent-left 1) (notify! "明天该交房租了。") #f))
                ((<= grace 0) (evict!))
                (else
                 (set! overdue? #t)
                 (set! grace-left grace)
                 (notify! (string-append "房租到期了。老板没说什么，只是又看了你一眼——他能等 "
                                         (number->string grace) " 天。"))))))))

    (define-turn-rule "每日恢复次数重置"
      (lambda () (or drank-today? medicated-today?))
      (lambda ()
        (set! drank-today? #f)
        (set! medicated-today? #f)))

    ;; 两根钟分工明确：一根是周期，一根是欠着的人情。宽限只在真的欠着的时候才出现，
    ;; 但它当前的额度平时就写在房租那根钟的备注里——玩家要能提前知道拖一天的后果。
    (define (rent-render-data)
      (list 'clock "房租到期" rent-left rent-cycle 'countdown
            (string-append "每 " (number->string rent-cycle) " 天一次，"
                           (number->string rent-amount) " 金。准时交，老板多等你一天（上限 "
                           (number->string grace-max) " 天）；拖到宽限里才交，他下次少等一天。"
                           "现在他愿意等 " (number->string grace) " 天。")))

    (define (grace-render-data)
      (list 'clock "老板还能等几天" grace-left grace-max 'countdown
            "归零还没交上就锁门。这一次拖过去了，下一次他会少等一天。"))

    (define (rent-clocks)
      (if overdue?
          (list (rent-render-data) (grace-render-data))
          (list (rent-render-data))))

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
          (outcome "借酒松神"
            (lambda () (apply-drink-effect!))))))

    ;; 用药：在住所中上药休养，不占用行动骰，压 2 点伤势。
    ;; 它是"花钱买时间"的那条路——不占骰子，但药得先花 25 金从诊所买回来。
    (define (node-use-medicine)
      (node "用药"
        :subtitle (cond
                    ((equal? (injury-band) '完好) "身上没有需要处理的伤")
                    (medicated-today? "一天上一次药就够了，伤口需要时间")
                    (#t "不占行动骰，压 2 点伤势；一天只能用一份"))
        :disabled (or medicated-today? (equal? (injury-band) '完好))
        :requires (list (req-item "药品" 1))
        :resolve (instant
          (outcome "上了药"
            (lambda ()
              (set! medicated-today? #t)
              (heal-injury! 2))))))

    (define (node-see-flower)
      (action "看花"
        (list (req-die))
        (instant
          (outcome "出神片刻"
            (lambda () (restore-actor-composure! 'player 2))))))

    (define (rest-tags)
      (if (rest-blocked?)
          (append (list "不可休息") (rest-block-reasons))
          '()))

    ;; 睡觉只养得好轻伤：擦伤磕碰过一夜就好一点，断了的地方不会。
    ;; 这条让轻伤成为"可以选择忍"的慢性成本，也让重伤必须真的花骰子和钱去治。
    (define (sleep-off-injury!)
      (if (equal? (injury-band) '轻伤) (heal-injury! 1) #f))

    (define (node-sleep)
      (node "睡觉"
        :disabled (rest-blocked?)
        :tags (rest-tags)
        :resolve (instant
          (outcome
            (if (in-hotel?) "睡了一夜" "安稳休息")
            (lambda ()
              ;; 旅馆 2、公寓 3（满）。旅馆原本只回 1 点，第二天一开局就欠着，
              ;; 每一天都比前一天更紧——那不是压力曲线，是慢性失血。
              ;; 公寓仍旧多回一点，「有个自己的地方」的差别落在这儿。
              (restore-actor-composure! 'player (if (in-hotel?) 2 3))
              (if (has-companion? 'joe) (restore-actor-composure! 'joe 1) #f)
              (sleep-off-injury!)
              (end-turn!))))))

    (define (node-sleep-at-door)
      (node "蜷缩在门口"
        :disabled (rest-blocked?)
        :tags (rest-tags)
        :resolve (instant
          (outcome "无处可去"
            (lambda ()
              ;; 露宿不回复冷静，但也不再伤身，免得把玩家推向击穿受伤的死亡循环。
              (end-turn!))))))

    ;; ── 交易 / 布置 / 升级 ──────────────────────────
    ;; 晚交 = 已经动用了宽限（正在宽限期里，或者已经被锁门）。
    (define (late?) (or overdue? evicted?))

    (define (node-pay-rent)
      (node "交租"
        :subtitle (cond
                    (evicted? "把欠的交清，房门重新打开——但老板下次不会再这么等你了")
                    (overdue? "已经欠着了；现在交上还能住，只是下次他少等你一天")
                    (#t (string-append "再安心住 " (number->string rent-cycle) " 天")))
        :requires (list (req-item "金钱" rent-amount))
        :resolve (instant
          (outcome "交了房租"
            (lambda ()
              (if (late?)
                  (set! grace (max 0 (- grace 1)))
                  (set! grace (min grace-max (+ grace 1))))
              (set! rent-left rent-cycle)
              (set! overdue? #f)
              (set! grace-left 0)
              (set! evicted? #f))))))

    (define (node-buy-flower)
      (node "买一盆花"
        :subtitle "自己的窗台才摆得下这点闲心；烦闷时可以坐着看一会儿"
        :requires (list (req-item "金钱" flower-price))
        :resolve (instant
          (outcome "买了一盆花"
            (lambda () (set! has-flower? #t))))))

    (define (node-buy-apartment)
      (node "买下公寓"
        :subtitle "有个自己的家，不再交房租，也能睡得更安稳"
        :requires (list (req-item "金钱" 140))
        :resolve (instant
          (outcome "签下了公寓"
            (lambda ()
              (set! residence "公寓")
              (sync-asset!))))))

    ;; ── 组装子节点 ──────────────────────────────────
    ;; 旅馆大厅是公共空间，只提供随身物品的使用；旅馆没有玩家自己的客厅。
    (define (node-hotel-lobby)
      (container "大厅" (list (node-drink) (node-use-medicine))))

    ;; 客厅只属于买下的公寓：日常恢复 + 已拥有的家具。
    (define (living-room-children)
      (append
        (list (node-drink) (node-use-medicine))
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
            (three-letters 'nodes-at "家")
            ;; 被赶出后仍保留大厅：库存里的酒和药是玩家随时可以使用的物品，
            ;; 房门锁住只应改变住宿方式，不应把公共空间里的物品使用入口一起删掉。
            (list (node-hotel-lobby)
                  (observe-action "锁着的房门" "先把房租交了，或者干脆买下一处不用看人脸色的地方。")
                  (node-pay-rent))
            (upgrade-nodes)
            (list (node-sleep-at-door)))
          (append
            (three-letters 'nodes-at "家")
            (list (node-hotel-lobby))
            (list (node-pay-rent))
            (upgrade-nodes)
            (list (node-sleep)))))

    (define (owned-body)
      (append
        (three-letters 'nodes-at "家")
        (list (node-living-room))
        (order-nodes)
        (upgrade-nodes)
        (list (node-sleep))))

    ;; 容器名固定为“家”（导航按名字定位，不能随住所变），住所等级放 subtitle 显示。
    (define (residence-container)
      (if (in-hotel?)
          (node "家" :subtitle residence :children (hotel-body) :clocks (rent-clocks))
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
             (list "rent-left"      rent-left)
             (list "grace"          grace)
             (list "overdue?"       overdue?)
             (list "grace-left"     grace-left)
             (list "evicted?"       evicted?)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! residence      (assoc-get data "residence" "旅馆"))
             ;; 旧档兼容：豪宅档位已删除，映射回公寓。
             (if (equal? residence "豪宅") (set! residence "公寓") #f)
             (set! has-flower?    (assoc-get data "has-flower?" #f))
             (set! drank-today?  (assoc-get data "drank-today?" #f))
             (set! medicated-today? (assoc-get data "medicated-today?" #f))
             ;; 旧档只有 rent-due（可变上限那一版）：把剩余天数搬过来，最多算一个周期。
             (set! rent-left      (min rent-cycle
                                       (assoc-get data "rent-left"
                                                  (assoc-get data "rent-due" rent-cycle))))
             (set! grace          (assoc-get data "grace" grace-max))
             (set! overdue?       (assoc-get data "overdue?" #f))
             (set! grace-left     (assoc-get data "grace-left" 0))
             (set! evicted?       (assoc-get data "evicted?" #f))
             (sync-asset!)))

          (#t #f))))))
