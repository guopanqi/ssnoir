;; scenes/world/home.scm - 住所系统
;; 旅馆（默认·每 4 天交一次房租，交不上先动用老板的宽限）→ 公寓（140 金买断·资产中）。
;; 资产等级由拥有的住所推导，写入全局 '资产（供富商圈门槛用）。
;; 恢复：旅馆睡觉冷静 +1，自有住所睡觉回满（2）；门口露宿不回冷静。
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

    ;; 房租（仅旅馆）：固定 4 天的收租周期。平时没有交租动作；倒计时归零时老板自动上门，
    ;; 有钱就当场扣 30 金并重开下一周期，钱不够才进入宽限。
    ;;
    ;; 到期交不上不立刻赶人，而是动用**老板的宽限**：这是一条会记住你的信用线。
    ;;   宽限 2 → 他等你两天    宽限 1 → 他等你一天    宽限 0 → 当天锁门
    ;; 准时交一次，宽限 +1（上限 2）；拖到宽限期里才交，宽限 −1（下限 0）。
    ;; 所以第一次拖欠的代价不是钱，是**下一次没人等你了**——欠账的真实形状。
    (define rent-cycle 4)
    (define rent-amount 30)
    (define rent-clk
      (make-clock "房租到期" rent-cycle 'countdown
        (lambda (current max)
          (string-append "每 " (number->string max) " 天收一次，"
                         (number->string rent-amount) " 金。归零时老板自动上门收租。"))))
    (rent-clk 'set! rent-cycle)
    (define grace-max 2)
    (define grace grace-max)        ; 老板还愿意等你几天（信用）
    (define overdue? #f)            ; 已经到期没交，正在用宽限
    (define grace-clk
      (make-clock "宽限到期" grace-max 'countdown
        "宽限期内可以补交；归零仍未交上，老板会来锁门。"))
    (define flower-price 40)
    (define evicted? #f)

    ;; ── 资产推导 ────────────────────────────────────
    (define (sync-asset!)
      (set-global! '资产
        (if (equal? residence "公寓") "中" "低")))
    (sync-asset!)

    (define (in-hotel?) (equal? residence "旅馆"))

    (define (required-field data key)
      (let ((value (assoc-get data key 'missing)))
        (if (equal? value 'missing)
            (error (string-append "住所存档错误：缺少 " key))
            value)))

    (define (boolean-value? value)
      (or (equal? value #t) (equal? value #f)))

    ;; ── Rules ──────────────────────────────────────
    (define (evict!)
      (set! evicted? #t)
      (set! overdue? #f)
      (grace-clk 'reset!))

    (define (reset-rent-cycle!)
      (rent-clk 'set! rent-cycle)
      (set! overdue? #f)
      (grace-clk 'reset!)
      (set! evicted? #f))

    (define (rent-money-ready?)
      (>= (item-count "金钱") rent-amount))

    (define (rent-demand-line)
      (string-append (number->string rent-cycle) " 天到了。"
                     (number->string rent-amount) " 块。"))

    (define (collect-rent!)
      (if (rent-money-ready?)
          (begin
            (play-dialogue!
              (line "旅馆老板" (rent-demand-line))
              (line "尼尔" "数清楚。"))
            (remove-item! "金钱" rent-amount)
            (set! grace (min grace-max (+ grace 1)))
            (reset-rent-cycle!))
          (if (> grace 0)
              (begin
                (set! overdue? #t)
                (grace-clk 'set! grace)
                (play-dialogue!
                  (line "旅馆老板" (rent-demand-line))
                  (line "尼尔" "今晚拿不出来。")
                  (line "旅馆老板"
                    (string-append "我再等 " (number->string grace) " 天。到时候别让我再问。"))))
              (begin
                (play-dialogue!
                  (line "旅馆老板" (rent-demand-line))
                  (line "尼尔" "我没有。")
                  (line "旅馆老板" "那就把东西拿出来。门今晚要锁。"))
                (evict!)))))

    (define (expire-grace!)
      (play-dialogue!
        (line "旅馆老板" "宽限到头了。钱呢？")
        (line "尼尔" "还没有。")
        (line "旅馆老板" "那就到这儿。天黑以前把东西搬出去。"))
      (evict!))

    ;; 房租只在住旅馆且未被赶出时流逝。到期那天不锁门：先看老板还愿意等几天。
    (define-turn-rule "房租流逝"
      (lambda () (and (in-hotel?) (not evicted?)))
      (lambda ()
        (if overdue?
            (begin
              (grace-clk 'advance! -1)
              (if (grace-clk 'empty?)
                  (expire-grace!)
                  (notify! (string-append "老板又来敲了一次门。宽限还剩 "
                                          (number->string (grace-clk 'current)) " 天。"))))
            (begin
              (rent-clk 'advance! -1)
              (if (rent-clk 'empty?)
                  (collect-rent!)
                  (if (= (rent-clk 'current) 1)
                      (notify! "老板明晚来收房租。")
                      #f))))))

    (define-turn-rule "每日恢复次数重置"
      (lambda () (or drank-today? medicated-today?))
      (lambda ()
        (set! drank-today? #f)
        (set! medicated-today? #f)))

    (define (rent-clocks)
      (cond
        (evicted? '())
        (overdue? (list (grace-clk 'render-data)))
        (else (list (rent-clk 'render-data)))))

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
              ;; 旅馆只回 1 点，无法把前一天的所有消耗都抹平；
              ;; 公寓回满 2 点，「有个自己的地方」的差别落在这儿。
              (restore-actor-composure! 'player (if (in-hotel?) 1 2))
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
    (define (node-pay-rent)
      (node "补交房租"
        :subtitle (if evicted?
                      (string-append "把欠下的 " (number->string rent-amount)
                                     " 块交清，老板才会重新开门")
                      "宽限还没到头；现在补上，下一次老板会少等一天")
        :requires (list (req-item "金钱" rent-amount))
        :resolve (instant
          (outcome "补上房租"
            (lambda ()
              (set! grace (max 0 (- grace 1)))
              (reset-rent-cycle!))))))

    (define (rent-status-subtitle)
      (cond
        (evicted? (string-append "房门已经锁了；欠下的 "
                                 (number->string rent-amount) " 块仍要补上"))
        (overdue?
         (string-append (number->string rent-amount) " 块还没交；老板只再等 "
                        (number->string (grace-clk 'current)) " 天"))
        (else
         (string-append "每 " (number->string rent-cycle) " 天 "
                        (number->string rent-amount) " 块；老板会在 "
                        (number->string (rent-clk 'current)) " 天后上门"))))

    (define (node-rent-status)
      (node "房租"
        :subtitle (rent-status-subtitle)
        :clocks (rent-clocks)
        :resolve (observe
          (cond
            (evicted? "老板已经锁了房门。欠下的房租补齐以前，只能睡在门口。")
            (overdue? "租期已经过了。宽限到头以前补齐房租，门还不会锁。")
            (else "老板每四天上门收一次房租；账到期时会直接从手头的钱里扣。")))))

    (define (rent-nodes)
      (append
        (list (node-rent-status))
        (if (or overdue? evicted?) (list (node-pay-rent)) '())))

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
                  (observe-action "锁着的房门" "先把房租交了，或者干脆买下一处不用看人脸色的地方。"))
            (rent-nodes)
            (upgrade-nodes)
            (list (node-sleep-at-door)))
          (append
            (three-letters 'nodes-at "家")
            (list (node-hotel-lobby))
            (rent-nodes)
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
          (node "家" :subtitle residence :children (hotel-body))
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
             (list "rent-left"      (rent-clk 'save))
             (list "grace"          grace)
             (list "overdue?"       overdue?)
             (list "grace-left"     (grace-clk 'save))
             (list "evicted?"       evicted?)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! residence      (required-field data "residence"))
             (set! has-flower?    (required-field data "has-flower?"))
             (set! drank-today?   (required-field data "drank-today?"))
             (set! medicated-today? (required-field data "medicated-today?"))
             (rent-clk 'load!     (required-field data "rent-left"))
             (set! grace          (required-field data "grace"))
             (set! overdue?       (required-field data "overdue?"))
             (grace-clk 'load!    (required-field data "grace-left"))
             (set! evicted?       (required-field data "evicted?"))
             (if (member? residence (list "旅馆" "公寓"))
                 #t (error "住所存档错误：住所类型非法"))
             (if (and (number? grace) (>= grace 0) (<= grace grace-max))
                 #t (error "住所存档错误：老板宽限额度非法"))
             (if (and (boolean-value? has-flower?)
                      (boolean-value? drank-today?)
                      (boolean-value? medicated-today?)
                      (boolean-value? overdue?)
                      (boolean-value? evicted?))
                 #t (error "住所存档错误：布尔状态非法"))
             (cond
               ((equal? residence "公寓") #t)
               (evicted?
                (if (and (not overdue?) (rent-clk 'empty?) (grace-clk 'empty?))
                    #t (error "住所存档错误：锁门状态仍残留房租倒计时")))
               (overdue?
                (if (and (rent-clk 'empty?) (not (grace-clk 'empty?)))
                    #t (error "住所存档错误：宽限状态的倒计时不一致")))
               (else
                (if (and (not (rent-clk 'empty?)) (grace-clk 'empty?))
                    #t (error "住所存档错误：正常租期的倒计时不一致"))))
             (sync-asset!)))

          (#t #f))))))
