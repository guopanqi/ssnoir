;; scenes/world/home.scm - 住所系统
;; 租的房间（默认·每 4 天交一次房租，交不上先动用房东的宽限）→ 公寓（200 金买断）。
;;
;; 起点不是旅馆。侦探住廉价出租屋是这个类型的标配，而**出租屋里摆得下自己的东西**：
;; 花、床垫、书桌都是你自己搬进来的，不需要替旅馆圆场（曾经那条「这些家具都能随人搬走」
;; 的注释就是矛盾在冒烟）。买断买的是**另一处**，不是这一间的产权——成长感来自搬家本身，
;; 不来自一纸产权；将来空间模型换代也有个天然的时机。
;; 恢复（冷静上限 5）：睡觉 +2（租与买一样）；门口露宿不回冷静。
;; 看花 +1（不占钱、不会失败），酒馆一杯 +3（25 金，带宿醉）；
;; 日常投入行动骰的恢复以公园散步为主。睡觉抹不平一天，冷静是跨天的。
;; 酒会让下一次城市骰池出现“宿醉”降质。
;; 伤势不会自己好（这是它和冷静唯一的分别）。三条路：弹簧床垫解锁养伤（1 骰、压 1）、
;; 用药（25 金的药，不占骰、压 2、每天一份）、诊所（1 骰 + 诊金、压 2）。

(define home
  (let ()
    ;; ── Local State ────────────────────────────────
    (define residence "租的房间")    ; "租的房间" / "公寓"
    (define has-flower? #f)
    (define has-mattress? #f)
    (define has-typewriter? #f)
    (define has-phone? #f)
    (define drank-today? #f)
    (define flower-today? #f)
    (define medicated-today? #f)
    (define apartment-offer-known? #f) ; 第一次收租时，房东才告诉玩家可以买断公寓

    ;; 房租（仅租房时）：固定 4 天的收租周期。平时没有交租动作；倒计时归零时房东自动上门，
    ;; 有钱就当场扣 40 金并重开下一周期，钱不够才进入宽限。
    ;;
    ;; 到期交不上不立刻赶人，而是动用**房东的宽限**：这是一条会记住你的信用线。
    ;;   宽限 2 → 他等你两天    宽限 1 → 他等你一天    宽限 0 → 当天锁门
    ;; 准时交一次，宽限 +1（上限 2）；拖到宽限期里才交，宽限 −1（下限 0）。
    ;; 所以第一次拖欠的代价不是钱，是**下一次没人等你了**——欠账的真实形状。
    (define rent-cycle 4)
    (define rent-amount 40)
    (define rent-clk
      (make-clock "房租到期" rent-cycle 'countdown
        (lambda (current max)
          (string-append (if (<= current 1) "就是明晚。" (string-append "还剩 " (number->string current) " 天。"))
                         "每 " (number->string max) " 天收一次，"
                         (number->string rent-amount) " 金。归零时房东自动上门收租。"))))
    (rent-clk 'set! rent-cycle)
    (define grace-max 2)
    (define grace grace-max)        ; 房东还愿意等你几天（信用）
    (define overdue? #f)            ; 已经到期没交，正在用宽限
    (define grace-clk
      (make-clock "宽限到期" grace-max 'countdown
        (lambda (current max)
          (string-append (if (<= current 1) "最后一天。" (string-append "还剩 " (number->string current) " 天。"))
                         "宽限期内可以补交；归零仍未交上，房东会来锁门。"))))
    (define flower-price 40)
    (define mattress-price 60)
    (define typewriter-price 80)
    (define phone-price 100)
    (define apartment-price 200)
    (define evicted? #f)

    (define (renting?) (equal? residence "租的房间"))

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

    ;; 买断不是开局就摆在面前的商品。房东第一次上门收租时才顺口告诉玩家有这条路；
    ;; 此后报价进入住所的常驻选择，不会在每次收租时重复念。
    (define (play-rent-dialogue! lines)
      (if apartment-offer-known?
          (apply play-dialogue! lines)
          (begin
            (apply play-dialogue!
              (append lines
                (list
                  (line "房东"
                    (string-append
                      "等你手头有了 " (number->string apartment-price)
                      " 块，我还有间公寓可以卖你。买下以后，不再收租。")))))
            (set! apartment-offer-known? #t))))

    ;; 收租、补交、宽限用尽，三条路都只在对白里交代。日终规则跑不出结算行
    ;; （result-note! 只在动作结算内有效），扣钱本身又是静默的——不写进对白，
    ;; 玩家就只看见金钱方块少了一截，不知道是谁拿走的、下次什么时候再来。
    (define (collect-rent!)
      (if (rent-money-ready?)
          (let ((credit-up? (< grace grace-max)))
            (play-rent-dialogue!
              (append
                (list
                  (line "房东" (rent-demand-line))
                  (line "尼尔" "数清楚。")
                  (line "世界" (string-append "他点了两遍，钱进了外套内袋。下一次收租在 "
                                              (number->string rent-cycle) " 天后。")))
                ;; 准时交回来的那一天信用才涨。不说，玩家不会知道房东为什么忽然肯多等。
                (if credit-up?
                    (list (line "房东" "这回痛快。往后你要是晚一天，我不至于当天就锁门。"))
                    '())))
            (remove-item! "金钱" rent-amount)
            (set! grace (min grace-max (+ grace 1)))
            (reset-rent-cycle!))
          (if (> grace 0)
              (begin
                (set! overdue? #t)
                (grace-clk 'set! grace)
                (play-rent-dialogue!
                  (list
                    (line "房东" (rent-demand-line))
                    (line "尼尔" "今晚拿不出来。")
                    (line "房东"
                      (string-append "我再等 " (number->string grace) " 天。到时候别让我再问。")))))
              (begin
                (play-rent-dialogue!
                  (list
                    (line "房东" (rent-demand-line))
                    (line "尼尔" "我没有。")
                    (line "房东" "那就把东西拿出来。门今晚要锁。")))
                (evict!)))))

    (define (expire-grace!)
      (play-dialogue!
        (line "房东" "宽限到头了。钱呢？")
        (line "尼尔" "还没有。")
        (line "房东" "那就到这儿。天黑以前把东西搬出去。"))
      (evict!))

    ;; 房租只在租房且未被赶出时流逝。到期那天不锁门：先看房东还愿意等几天。
    (define-turn-rule "房租流逝"
      (lambda () (and (renting?) (not evicted?)))
      (lambda ()
        (if overdue?
            (begin
              (grace-clk 'advance! -1)
              (if (grace-clk 'empty?)
                  (expire-grace!)
                  (notify! (string-append "房东又来敲了一次门。宽限还剩 "
                                          (number->string (grace-clk 'current)) " 天。"))))
            (begin
              (rent-clk 'advance! -1)
              (if (rent-clk 'empty?)
                  (collect-rent!)
                  (if (= (rent-clk 'current) 1)
                      (notify! "房东明晚来收房租。")
                      #f))))))

    (define-turn-rule "每日恢复次数重置"
      (lambda () (or drank-today? medicated-today? flower-today?))
      (lambda ()
        (set! drank-today? #f)
        (set! medicated-today? #f)
        (set! flower-today? #f)))

    ;; ── 恢复类 ──────────────────────────────────────    ;; 白天解压里，散步占一颗骰子（与工作争夺骰子池）；看花和喝酒不占骰，走"花钱"那条线；
    ;; 睡觉免费（回合结束）。
    ;; 喝酒不占骰子，走“花钱买酒”这条线：当场大量恢复冷静。
    ;; 效果只写这一份：酒馆当场点酒（'drink! 消息）与家中喝自带的酒共用同一次“当天第一杯”。
    ;; 25 金回 3 点，是全城最快的一条恢复——它就是"钱换恢复"那条路的价目。
    ;; 代价不在当场：宿醉会让下一次城市骰池里的一格降质。
    (define (apply-drink-effect!)
      (set! drank-today? #t)
      (restore-actor-composure! 'player 3)
      (apply-hangover!))

    ;; 家里曾有一张「喝酒」：喝掉从酒馆打回来的那一壶。买酒的卡删掉之后（见
    ;; 老街酒馆.scm），「酒」这件物品没有来路了，这张卡只会一直灰在客厅里。
    ;; 效果本身没丢：酒馆那杯走的就是下面这个 apply-drink-effect!。

    ;; 用药：在住所中上药休养，不占用行动骰，压 2 点伤势。
    ;; 它是"花钱买时间"的那条路——不占骰子，但药得先花 25 金从诊所买回来。
    (define (node-use-medicine)
      (node "用药"
        :anchor "床边"
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

    ;; 养伤：弹簧床垫解锁的长期恢复通道。
    ;;   养伤   1 骰、0 金、−1
    ;;   用药   0 骰、25 金、−2（每天一份）
    ;;   看医生 1 骰、20 金、−2
    ;; 床垫不被动治伤：它只让玩家可以把今天的一颗骰换成一点康复。
    ;; 不设每日上限——骰子本身就是上限，多躺一颗骰就是少做一份工。
    (define (node-mend)
      (node "养伤"
        :anchor "床边"
        :subtitle (if (equal? (injury-band) '完好)
                      "身上没有需要处理的伤"
                      "投入一颗行动骰，压 1 点伤势；不花钱，只花今天")
        :disabled (equal? (injury-band) '完好)
        :requires (list (req-die))
        :resolve (instant
          (outcome "躺了大半天"
            (lambda () (heal-injury! 1))))))

    ;; 看花**不占行动骰**，每天一次，稳回 1 点。
    ;;
    ;; 它曾经也要投一颗骰。那样它和公园散步就是同一笔交易——一颗骰换恢复——
    ;; 只是一个稳回 1、一个 0/1/2。期望几乎一样，而散步还顺带推沃尔特那条线，
    ;; 于是「花 40 金买一盆花」买回来的只有方差变小，读起来就是白花钱。
    ;;
    ;; 散步是把骰子换成冷静，花是把钱换成每天一点不花骰的冷静恢复。
    (define (node-see-flower)
      (node "看花"
        :anchor "窗台"
        :subtitle (if flower-today? "今天已经看过了" "不占行动骰，每天一次；坐下来出神片刻，回 1 点冷静")
        :disabled flower-today?
        :resolve (instant
          (outcome "出神片刻"
            (lambda ()
              (set! flower-today? #t)
              (restore-actor-composure! 'player 1))))))

    (define (note-mattress)
      (note-node "标注：弹簧床垫" "弹簧床垫"
        "躺下来养伤要花掉今天的一颗骰；睡一觉本身不治伤。"))

    (define (rest-tags)
      (if (rest-blocked?)
          (append (list "不可休息") (rest-block-reasons))
          '()))

    ;; 睡觉不治伤。弹簧床垫只解锁上面的主动养伤，不再附送被动恢复。

    (define (node-sleep)
      (node "睡觉"
        :anchor "床边"
        :subtitle (if (dock 'night-shift-today?)
                      "结束今天；恢复 1 点冷静"
                      "结束今天；恢复 2 点冷静")
        :disabled (rest-blocked?)
        :tags (rest-tags)
        :resolve (instant
          (outcome
            (if (dock 'night-shift-today?) "天亮才躺下" "睡了一夜")
            (lambda ()
              ;; 睡觉固定回 2 点，租的和买下的没有差别——那两者的差别在房租，
              ;; 不在睡得好不好。2 点远不足以抹平一天（顺的一天大约掉 2，糟的掉 4 以上），
              ;; 冷静因此是一条跨天的轴：交锋掏空之后要在城里养好几天才回得来。
              ;; 熬过码头夜班的那天只回 1：夜班多给的那笔钱，一部分是从这里扣的。
              (restore-actor-composure! 'player (if (dock 'night-shift-today?) 1 2))
              (end-turn!))))))

    (define (node-sleep-at-door)
      (node "蜷缩在门口"
        :anchor "门口"
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
        :anchor "门口"
        :requires (list (req-item "金钱" rent-amount))
        :resolve (instant
          (outcome "补上房租"
            (lambda ()
              ;; 两种补交是两回事，不能共用一段话：被锁在门外之后交，是把门换回来；
              ;; 宽限期里交，是信用掉一格——晚交的代价不当场说出来，玩家永远不会知道
              ;; 房东下次为什么少等他一天。
              (if evicted?
                  (play-dialogue!
                    (line "尼尔" "钱。开门。")
                    (line "世界" "他数完，把门上那把锁摘了下来。")
                    (line "房东" "东西都还在里头。别让我锁第二次。"))
                  (play-dialogue!
                    (line "尼尔" "钱在这儿。")
                    (line "房东" "总算。")
                    (line "世界" "他收了钱，没再说别的。往后他肯等你的日子，少了一天。")))
              (set! grace (max 0 (- grace 1)))
              (reset-rent-cycle!))))))

    ;; 房租是跨导航持续跟踪的倒计时，用 clock-node 立成常驻标注，不占可交互版面；
    ;; 补交房租仍是一张动作卡。被赶出后没有钟可挂，退成一段说明文字。
    (define (rent-status-node)
      (cond
        (evicted?
         (node "标注：房租" :anchor "门口" :resolve
           (note "房门已锁"
             (string-append "欠下的 " (number->string rent-amount) " 块补清，"
                            "房东才会重新开门。"))))
        (overdue?
         (node "标注：房租" :anchor "门口" :resolve (clock (grace-clk 'render-data))))
        (else
         (node "标注：房租" :anchor "门口" :resolve (clock (rent-clk 'render-data))))))

    (define (rent-nodes)
      (append
        (list (rent-status-node))
        (if (or overdue? evicted?) (list (node-pay-rent)) '())))

;; 家里现在只有三个锚点：床边 / 窗台 / 门口（模型 City.fbx 里的 Anchor_*）。
    ;; 添置家具这一支买的东西各自落到它真正会摆的地方——床垫在床边、书桌在窗台、
    ;; 电话在门口（线从楼道接进来，机子挂在玄关）。买它的那张卡和买回来之后长出来的
    ;; 那张卡挂同一个锚点：花了钱，画面上就该是那个位置起了变化。
    (define (node-buy-flower)
      (node "买一盆花"
        :anchor "窗台"
        :subtitle "自己的窗台才摆得下这点闲心；烦闷时可以坐着看一会儿"
        :requires (list (req-item "金钱" flower-price))
        :resolve (instant
          (outcome "买了一盆花"
            (lambda () (set! has-flower? #t))))))

    (define (node-buy-mattress)
      (node "买弹簧床垫"
        :anchor "床边"
        :subtitle "买回长期养伤的地方：投入一颗骰，压 1 点伤势"
        :requires (list (req-item "金钱" mattress-price))
        :resolve (instant
          (outcome "换了床垫"
            (lambda () (set! has-mattress? #t))))))

    (define (node-buy-typewriter)
      (node "买书桌和打字机"
        :anchor "窗台"
        :subtitle "在家承接誊清账目和文书的活"
        :requires (list (req-item "金钱" typewriter-price))
        :resolve (instant
          (outcome "安置了书桌"
            (lambda () (set! has-typewriter? #t))))))

    (define (node-install-phone)
      (node "装一部电话"
        :anchor "门口"
        :subtitle "临时委托会直接打到家里"
        :requires (list (req-item "金钱" phone-price))
        :resolve (instant
          (outcome "电话接通了"
            (lambda ()
              (set! has-phone? #t)
              (board 'open-line!))))))

    (define (node-typing-work)
      (关系工作 "誊清账目" "商业圈" '低 'knowledge
        (outcome "账目清楚" (lambda () (add-item! "金钱" 14)))
        (outcome "按页誊完" (lambda () (add-item! "金钱" 8)))
        (outcome "数字抄错了" (lambda () (spend-composure! 1)))
        "坐在自己的书桌前接一份文书活" :anchor "窗台"))

    (define (node-telephone)
      (node "电话"
        :anchor "门口"
        :subtitle "城里的临时委托会打到这条线上"
        :children (board 'phone-nodes)))

    (define (node-buy-apartment)
      (node "买下公寓"
        :anchor "门口"
        :subtitle "有个自己的家，不再交房租，也能睡得更安稳"
        :requires (list (req-item "金钱" apartment-price))
        :resolve (instant
          (outcome "签下了公寓"
            (lambda ()
              (set! residence "公寓"))))))

    ;; ── 组装子节点 ──────────────────────────────────
    ;; 被锁在门外时剩下的公共空间：楼下门厅。只提供随身物品的使用，没有你自己的地方。
    (define (node-entry-hall)
      (node "楼下门厅" :anchor "门口" :children (list (node-use-medicine))))

    ;; 你自己搬进来的东西：租的房间里就摆得下，搬家时当然也跟着走。
    ;; 第一次收租时房东提过公寓以后，玩家才开始考虑往住处添东西——
    ;; 买下公寓不是添家具的前置条件，那是两笔各自成立的钱。
    (define (portable-furniture-children)
      (append
        (list (node-use-medicine))
        (if has-flower? (list (node-see-flower)) '())
        (if has-mattress? (list (node-mend) (note-mattress)) '())
        (if has-typewriter? (list (node-typing-work)) '())))

    (define (node-rented-room)
      (node "房间"
        :anchor "床边"
        :children (portable-furniture-children)))

    ;; 电话是固定线路，只属于买下的公寓。
    (define (living-room-children)
      (append
        (portable-furniture-children)
        (if has-phone? (list (node-telephone)) '())))

    (define (node-living-room)
      (node "客厅" :anchor "窗台" :children (living-room-children)))

    ;; 可搬动家具与公寓报价同时开放；固定电话仍要有自己的公寓才能安装。
    (define (order-children)
      (append
        (if has-flower? '() (list (node-buy-flower)))
        (if has-mattress? '() (list (node-buy-mattress)))
        (if has-typewriter? '() (list (node-buy-typewriter)))
        (if (or (renting?) has-phone?) '() (list (node-install-phone)))))

    (define (order-nodes)
      (if (or (not apartment-offer-known?) (null? (order-children)))
          '()
          (list (node "添置家具" :anchor "窗台" :children (order-children)))))

    (define (upgrade-nodes)
      (if (and (renting?) apartment-offer-known?)
          (list (node-buy-apartment))
          '()))

    (define (rented-body)
      (if evicted?
          (append
            (地点节点 "家")
            ;; 被赶出后仍保留大厅：库存里的酒和药是玩家随时可以使用的物品，
            ;; 房门锁住只应改变住宿方式，不应把公共空间里的物品使用入口一起删掉。
            (list (node-entry-hall))
            (rent-nodes)
            (upgrade-nodes)
            (list (node-sleep-at-door)))
          (append
            (地点节点 "家")
            (list (node-rented-room))
            (rent-nodes)
            (order-nodes)
            (upgrade-nodes)
            (list (node-sleep)))))

    (define (owned-body)
      (append
        (地点节点 "家")
        (list (node-living-room))
        (order-nodes)
        (upgrade-nodes)
        (list (node-sleep))))

    ;; 容器名固定为“家”（导航按名字定位，不能随住所变），住所等级放 subtitle 显示。
    (define (residence-container)
      (if (renting?)
          (place "家" :subtitle residence :children (rented-body))
          (place "家" :subtitle residence :children (owned-body))))

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
             (list "has-mattress?"  has-mattress?)
             (list "has-typewriter?" has-typewriter?)
             (list "has-phone?"     has-phone?)
             (list "drank-today?" drank-today?)
             (list "flower-today?" flower-today?)
             (list "medicated-today?" medicated-today?)
             (list "apartment-offer-known?" apartment-offer-known?)
             (list "rent-left"      (rent-clk 'save))
             (list "grace"          grace)
             (list "overdue?"       overdue?)
             (list "grace-left"     (grace-clk 'save))
             (list "evicted?"       evicted?)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! residence      (required-field data "residence"))
             (set! has-flower?    (required-field data "has-flower?"))
             (set! has-mattress?  (required-field data "has-mattress?"))
             (set! has-typewriter? (required-field data "has-typewriter?"))
             (set! has-phone?     (required-field data "has-phone?"))
             (set! drank-today?   (required-field data "drank-today?"))
             (set! flower-today?  (required-field data "flower-today?"))
             (set! medicated-today? (required-field data "medicated-today?"))
             (set! apartment-offer-known? (required-field data "apartment-offer-known?"))
             (rent-clk 'load!     (required-field data "rent-left"))
             (set! grace          (required-field data "grace"))
             (set! overdue?       (required-field data "overdue?"))
             (grace-clk 'load!    (required-field data "grace-left"))
             (set! evicted?       (required-field data "evicted?"))
             ;; 一次性改名迁移：起点从「旅馆」改叫「租的房间」（同一个东西，换了说法）。
             ;; 显式写在这儿而不是让 required-field 静默放行；等旧档不再需要就删掉这三行。
             (if (equal? residence "旅馆") (set! residence "租的房间") #f)
             (if (member? residence (list "租的房间" "公寓"))
                 #t (error "住所存档错误：住所类型非法"))
             (if (and (number? grace) (>= grace 0) (<= grace grace-max))
                 #t (error "住所存档错误：房东宽限额度非法"))
             (if (and (boolean-value? has-flower?)
                      (boolean-value? has-mattress?)
                      (boolean-value? has-typewriter?)
                      (boolean-value? has-phone?)
                      (boolean-value? drank-today?)
                      (boolean-value? medicated-today?)
                      (boolean-value? apartment-offer-known?)
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
             #t))

          (#t #f))))))
