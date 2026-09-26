;; scenes/world/home.scm - 住所系统
;; 租的房间（默认·每 4 天交一次房租，交不上先动用房东的宽限）→ 公寓（200 金买断）。
;;
;; 起点不是旅馆。侦探住廉价出租屋是这个类型的标配，而**出租屋里摆得下自己的东西**：
;; 花、床垫、书桌都是你自己搬进来的，不需要替旅馆圆场（曾经那条「这些家具都能随人搬走」
;; 的注释就是矛盾在冒烟）。买断买的是**另一处**，不是这一间的产权——成长感来自搬家本身，
;; 不来自一纸产权；将来空间模型换代也有个天然的时机。
;; 恢复（冷静上限 5）：睡觉 +2（租与买一样）；门口露宿不回冷静。
;; 浇水（龟背竹）投入 1 骰、暂时没有数值收益；酒馆一杯 +2（15 金）；
;; 听夜莺唱歌投入 1 骰恢复 1 点。睡觉抹不平一天，冷静是跨天的。
;; 伤势不会自己好（这是它和冷静唯一的分别）。三条路：弹簧床垫解锁养伤（1 骰、压 1）、
;; 用药（25 金的药，不占骰、压 1、每天一份）、诊所（1 骰 + 诊金、压 2）。
;; 钱只能让骰子更值钱，替代不了骰子：纯花钱那条（用药）故意最弱，重伤想快好
;; 就得把今天的骰子交出去。

(define home
  (let ()
    ;; ── Local State ────────────────────────────────
    (define residence "租的房间")    ; "租的房间" / "公寓"
    (define has-flower? #f)
    (define has-mattress? #f)
    (define has-phone? #f)
    (define has-gramophone? #f)
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
    (define apartment-price 200)
    (define gramophone-price 120)
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
    ;; （result-supplement! 只在动作结算内有效），扣钱本身又是静默的——不写进对白，
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

    ;; ── 恢复类 ──────────────────────────────────────
    ;; 白天解压里，听夜莺唱歌占一颗骰子（与工作争夺骰子池）；喝酒不占骰，
    ;; 走“花钱”那条线；睡觉免费（回合结束）。
    ;; 喝酒不占骰子，走“花钱买酒”这条线：当场大量恢复冷静。
    ;; 效果只写这一份：酒馆当场点酒（'drink! 消息）与家中喝自带的酒共用同一次“当天第一杯”。
    ;; 15 金回 2 点：比睡觉多花钱，换来不结束这一天。它不再附加宿醉。
    (define (apply-drink-effect!)
      (set! drank-today? #t)
      (restore-actor-composure! 'player 2))

    ;; 家里曾有一张「喝酒」：喝掉从酒馆打回来的那一壶。买酒的卡删掉之后（见
    ;; 老街酒馆.scm），「酒」这件物品没有来路了，这张卡只会一直灰在客厅里。
    ;; 效果本身没丢：酒馆那杯走的就是下面这个 apply-drink-effect!。

    ;; 用药：在住所中上药休养，不占用行动骰，压 1 点伤势。
    ;; 它是"花钱买时间"的那条路——不占骰子，但药得先花 25 金从诊所买回来。
    ;; 曾经压 2：那样一份药就抵得上一次看医生，钱一多伤势就不再吃日子，
    ;; 交锋的代价随之消失。急救只止一格，剩下的还得躺。
    (define (node-use-medicine anchor)
      (node "用药"
        :anchor anchor
        :subtitle (cond
                    ((equal? (injury-band) '完好) "身上没有需要处理的伤")
                    (medicated-today? "一天上一次药就够了，伤口需要时间")
                    (#t "不占行动骰，压 1 点伤势；一天只能用一份"))
        :disabled (or medicated-today? (equal? (injury-band) '完好))
        :requires (list (req-item "药品" 1))
        :resolve (instant
          (outcome (lambda ()
              (set! medicated-today? #t)
              (heal-injury! 1))))))

    ;; 养伤：弹簧床垫解锁的长期恢复通道。
    ;;   养伤   1 骰、0 金、−1
    ;;   用药   0 骰、25 金、−1（每天一份）
    ;;   看医生 1 骰、20 金、−2
    ;; 床垫不被动治伤：它只让玩家可以把今天的一颗骰换成一点康复。
    ;; 不设每日上限——骰子本身就是上限，多躺一颗骰就是少做一份工。
    (define (node-mend)
      (node "养伤"
        :anchor "租屋-床边"
        :subtitle (if (equal? (injury-band) '完好)
                      "身上没有需要处理的伤"
                      "投入一颗行动骰，压 1 点伤势；不花钱，只花今天")
        :disabled (equal? (injury-band) '完好)
        :requires (list (req-die))
        :resolve (instant
          (outcome (lambda () (heal-injury! 1))))))

    ;; 浇水投入一颗行动骰，每天一次，暂时没有数值收益。它是在家里花掉一点时间，
    ;; 换来的只有植物一天天长大；以后如果要加收益，也从这张卡扩展，不另开一条照料系统。
    ;;
    ;; 花是一株龟背竹。浇水的次数记在全局键 龟背竹 里（存档随全局键走），Unity 侧按它定屋里那盆的档：
    ;; 苗 → 半大（5 次）→ 成株（15 次），叶子一片片从茎上长出来（PropMotion.SyncAll 里的阈值和这里的
    ;; monstera-stage 必须一致）。长大不改任何数值——它和唱片机一样，是屋里"纯粹为了好一点"的东西，
    ;; 你只是看着它一天天变成一株像样的植物。
    (define (water-count) (or (get-global '龟背竹) 0))
    (define (monstera-stage)
      (let ((n (water-count)))
        (cond ((>= n 15) "成株") ((>= n 5) "半大") (else "苗"))))

    (define (node-water-plant)
      (node "浇水"
        :anchor "租屋-龟背竹"
        :subtitle (cond
                    (flower-today? "今天已经浇过了")
                    ((equal? (monstera-stage) "成株") "投入一颗骰；叶子已经遮住半扇窗")
                    ((equal? (monstera-stage) "半大") "投入一颗骰；裂叶正往窗边舒展")
                    (else "投入一颗骰；两片小叶还没裂口"))
        :disabled flower-today?
        :requires (list (req-die))
        :resolve (instant
          (outcome (lambda ()
              (set! flower-today? #t)
              (set-global! '龟背竹 (+ (water-count) 1))
              (play-motion! "租屋/浇水壶" "浇水" "租屋-浇水"))))))

    (define (note-mattress)
      (node "标注：弹簧床垫" :anchor "租屋-床边" :resolve
        (note "弹簧床垫" "躺下来养伤要花掉今天的一颗骰；睡一觉本身不治伤。")))

    (define (rest-tags)
      ;; 睡觉卡上只说锁的原因，不指去哪：具体去哪处理由边缘信标和卷宗主线指。
      ;; 事件自己的那句（"机器今天上岸"这类）是写给信标指路的，贴在卡上像谜语。
      (if (rest-blocked?)
          (list "不可休息" "存在还未处理的事件")
          '()))

    ;; 睡觉不治伤。弹簧床垫只解锁上面的主动养伤，不再附送被动恢复。

    ;; 连睡锁：睡过一觉，要先出过门才能再睡。手滑连点两下睡觉就是两天没了，
    ;; 而「出门再回来」是玩家有意识做的事，误触不会碰到它。家里能做的事本来就少，
    ;; 所以不拿「做过任何别的动作」当解锁条件——那一条还是可能被连点绕过。
    (define 刚睡过? #f)
    (define (sleep-tags)
      (append (rest-tags) (if 刚睡过? (list "先出门走走") '())))
    (define (sleep-locked?) (or (rest-blocked?) 刚睡过?))
    (define-enter-place-rule "出门后才能再睡"
      ;; 退回世界层也算出门：place-name 是 "世界"，本来就不等于 "家"。
      (lambda (place-name)
        (if (equal? place-name "家") #f (set! 刚睡过? #f))))

    (define (node-sleep)
      (node "睡觉"
        :anchor "床边"
        :subtitle (cond
                    (刚睡过? "刚醒。出门走走再回来睡")
                    (else "结束今天；恢复 2 点冷静"))
        :disabled (sleep-locked?)
        :tags (sleep-tags)
        :resolve (instant
          (outcome (lambda ()
              ;; 睡觉固定回 2 点，租的和买下的没有差别——那两者的差别在房租，
              ;; 不在睡得好不好。2 点远不足以抹平一天（顺的一天大约掉 2，糟的掉 4 以上），
              ;; 冷静因此是一条跨天的轴：交锋掏空之后要在城里养好几天才回得来。
              (restore-actor-composure! 'player 2)
              (set! 刚睡过? #t)
              (end-turn!))))))

    (define (node-sleep-at-door)
      (node "蜷缩在门口"
        :anchor "门口"
        :subtitle (if 刚睡过? "刚醒。出门走走再回来" "")
        :disabled (sleep-locked?)
        :tags (sleep-tags)
        :resolve (instant
          (outcome (lambda ()
              ;; 露宿不回复冷静，但也不再伤身，免得把玩家推向击穿受伤的死亡循环。
              (set! 刚睡过? #t)
              (end-turn!))))))

    ;; ── 交易 / 布置 / 升级 ──────────────────────────
    (define (node-pay-rent)
      (node "补交房租"
        :anchor "门口"
        :requires (list (req-item "金钱" rent-amount))
        :resolve (instant
          (outcome (lambda ()
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

    ;; 买东西的卡全挂在屋里的书桌上（翻着报纸广告页下单）。**不能挂在买回来的东西自己的
    ;; 锚点上**：花盆、唱片机按 presence 契约"有卡挂着就出现"，买之前把卡挂过去，东西就提前长出来了。
    ;; 买回来之后的卡（浇水、放唱片、养伤）才各自落到东西真正摆的位置。
    (define (node-buy-flower)
      (node "买一盆龟背竹"
        :anchor "租屋-书桌"
        :subtitle "两片小叶子的苗；每天浇水，看它慢慢长"
        :requires (list (req-item "金钱" flower-price))
        :resolve (instant
          (outcome (lambda () (set! has-flower? #t))))))

    (define (node-buy-mattress)
      (node "买弹簧床垫"
        :anchor "租屋-书桌"
        :subtitle "买回长期养伤的地方：投入一颗骰，压 1 点伤势"
        :requires (list (req-item "金钱" mattress-price))
        :resolve (instant
          (outcome (lambda () (set! has-mattress? #t))))))

    ;; 这里曾有「买书桌和打字机」→「誊清账目」：一份在家里就能接的文书活。
    ;; 删了。零风险、不出门、稳拿钱——它把侦探变成打字员，而且是又一份工作；
    ;; 工作的差别应当在效果上（码头伤身、酒店翻脸、酒馆管饭），不在多一份。

    ;; 经理在第一次汇报时替尼尔装电话；备案后，正式侦探委托才打到这条线上。
    ;; 身份暂停时只撤掉正式侦探委托，电话本身留下，以后仍可承载人物来电和其他工作。
    (define (node-telephone)
      (node "电话"
        :anchor "租屋-门"
        :subtitle (if (baines 'registered?)
                      "富裕客户的调查委托会打到这条线上"
                      "电话接通了，正式委托仍需警局备案")
        :children
          (if (baines 'registered?)
              (board 'phone-nodes)
              (list (note-node "标注：没有正式委托" "电话没响"
                      "正式客户不再通过这条线找你。")))))

    (define (node-buy-apartment)
      (node "买下公寓"
        :anchor "门口"
        :subtitle "有个自己的家，不再交房租，也能睡得更安稳"
        :requires (list (req-item "金钱" apartment-price))
        :resolve (instant
          (outcome (lambda ()
              (set! residence "公寓"))))))

    ;; 唱片机是屋里第一件纯粹为了"好一点"买的东西：不回冷静、不治伤、不省钱。
    ;; 架子上的唱片放哪张，城里就循环哪张（全局键 音乐，Unity 侧按它换曲；存档随全局键走）。
    ;; 「随机播放」在五张里一直随机；抬起唱针则回到城市默认声（城市-* 曲库，偶尔才响）。
    ;; 放唱片的卡都挂在 租屋-唱片机 上，收在「唱片机」容器里；以后加唱片只往 records 加一行，
    ;; wav 丢进 Resources/Music 同名（唱片-* 上架，城市-* 进默认声），不新加锚点。
    ;; 机器和架子按 presence 契约随容器的锚点出现。
    (define (node-buy-gramophone)
      (node "买台唱片机"
        :anchor "租屋-书桌"
        :subtitle "带五张唱片。屋里总得有点声音"
        :requires (list (req-item "金钱" gramophone-price))
        :resolve (instant
          (outcome (lambda () (set! has-gramophone? #t))))))

    ;; (clip 名 · 标题 · 一句话)。都挂在 租屋-唱片机 上，锚点写成字面量，发布器才对得上号。
    (define records
      (list (list "唱片-1" "《午夜列车》" "慢板钢琴，像雨点落在车窗上")
            (list "唱片-2" "《码头灯火》" "闷音小号，一段没人接的独白")
            (list "唱片-3" "《周六舞厅》" "弦乐三拍子，这城里曾经也有人跳舞")
            (list "唱片-4" "《慢雨蓝调》" "小调慢爵士，钢琴只弹二四拍")
            (list "唱片-5" "《影子脚步》" "低音踱步，秒针在响")))

    (define (record-playing) (get-global '音乐))

    (define (node-play-record rec)
      (let ((id (car rec)) (title (cadr rec)) (desc (caddr rec)))
        (node (string-append "放" title)
          :anchor "租屋-唱片机"
          :subtitle (if (equal? (record-playing) id) "正在转" desc)
          :disabled (equal? (record-playing) id)
          :resolve (instant
            (outcome (lambda () (set-global! '音乐 id)))))))

    (define (record-nodes) (map node-play-record records))

    ;; 随机播放不是一张唱片：全局键 音乐 填这个值，Unity 侧在唱片-* 里一直随机，
    ;; 和城市默认声（城市-*）是两个池子。抬起唱针回到城市默认声。
    (define shuffle-id "随机播放")

    (define (node-shuffle)
      (node "随机播放"
        :anchor "租屋-唱片机"
        :subtitle (if (equal? (record-playing) shuffle-id) "正在转" "交给架子，五张里随机来")
        :disabled (equal? (record-playing) shuffle-id)
        :resolve (instant
          (outcome (lambda () (set-global! '音乐 shuffle-id))))))

    (define (node-stop-record)
      (node "抬起唱针"
        :anchor "租屋-唱片机"
        :subtitle "让城市自己唱"
        :resolve (instant
          (outcome (lambda () (set-global! '音乐 #f))))))

    ;; 唱片机是一个容器：五张唱片、「随机播放」和「抬起唱针」都收在它下面，点开才聚焦到机器上。
    ;; 容器本身挂在 租屋-唱片机——机器的模型随这个锚点显隐，容器在树里，机器就在屋里。
    (define (node-gramophone)
      (node "唱片机"
        :anchor "租屋-唱片机"
        :subtitle (if (record-playing) "正在转" "唱针抬着。架子上五张唱片。")
        :children
          (append (record-nodes)
                  (list (node-shuffle))
                  (if (record-playing) (list (node-stop-record)) '()))))

    (define (gramophone-nodes)
      (if has-gramophone? (list (node-gramophone)) '()))

    ;; ── 组装 ────────────────────────────────────────
    ;; 家的树只有两层，外层是"每天都要点的"，门里是"自己的东西"：
    ;;
    ;;   家（世界层地点，家.blend：锚点 床边 / 窗台 / 门口）
    ;;   ├ 故事投射卡
    ;;   ├ 租屋 …………………………… 门卡挂「门口」；点进去穿门，进 Stage 租屋（郊野那排的独立 Prefab）
    ;;   │  ├ 标注：屋里 …………… 什么都没添时的唯一一条
    ;;   │  ├ 浇水 / 唱片机 / 放唱片 … 买回来才有；龟背竹和唱片机的模型按 presence 契约随这些卡出现
    ;;   │  ├ 用药 / 养伤 ……………… 租屋-床边
    ;;   │  ├ 电话 …………………………… 租屋-门（线从楼道接进来）
    ;;   │  └ 添置家具 …………………… 租屋-书桌；买的卡都在这儿，不挂到要买的东西头上
    ;;   ├ 房租标注 / 补交 …………… 家的第一层
    ;;   ├ 买下公寓 …………………… 家的第一层（门口）
    ;;   └ 睡觉
    ;;
    ;; Stage 的身份是节点名（Anchor_租屋 挂着 Portal），:anchor 只说门卡挂在家的哪儿——
    ;; 门卡得在门外看得见，门后的空间却在郊野，两者不可能是同一个锚点。交锋根容器名就是场景名，同一条规则。
    ;; 穿门一次约两秒，所以睡觉、房租这些每天点的留在门外。
    ;; 被锁在门外时没有 租屋：只剩楼下门厅（用随身的药）、补交房租、蜷缩在门口。
    ;; 买下公寓后暂时仍走进同一间（公寓自己的 Stage 还没做；做了以后门卡按 residence 换名字）。

    ;; 你自己搬进来的东西：租的房间里就摆得下，搬家时当然也跟着走。
    ;; 第一次收租时房东提过公寓以后，玩家才开始考虑往住处添东西——
    ;; 买下公寓不是添家具的前置条件，那是两笔各自成立的钱。
    (define (order-children)
      (append
        (if has-flower? '() (list (node-buy-flower)))
        (if has-mattress? '() (list (node-buy-mattress)))
        (if has-gramophone? '() (list (node-buy-gramophone)))))

    (define (order-nodes)
      (if (or (not apartment-offer-known?) (null? (order-children)))
          '()
          (list (node "添置家具" :anchor "租屋-书桌" :children (order-children)))))

    (define (things-nodes)
      (append (if has-flower? (list (node-water-plant)) '())
              (gramophone-nodes)))

    (define (room-nodes)
      (append
        (if (null? (things-nodes))
            (list (node "标注：屋里" :anchor "租屋" :resolve
                    (note "租的房间" "床、桌子、一扇窗。自己的东西还没搬进来几件。")))
            (things-nodes))
        (list (node-use-medicine "租屋-床边"))
        (if has-mattress? (list (node-mend) (note-mattress)) '())
        (if has-phone? (list (node-telephone)) '())
        (order-nodes)))

    (define (node-room)
      (node "租屋" :anchor "门口" :children (room-nodes)))

    ;; 被锁在门外时剩下的公共空间：楼下门厅。库存里的药是随身的，门锁住不该把它一起锁掉。
    (define (node-entry-hall)
      (node "楼下门厅" :anchor "门口" :children (list (node-use-medicine "门口"))))

    (define (upgrade-nodes)
      (if (and (renting?) apartment-offer-known?)
          (list (node-buy-apartment))
          '()))

    (define (home-body)
      (append
        (地点节点 "家")
        (if evicted? (list (node-entry-hall)) (list (node-room)))
        (if (renting?) (rent-nodes) '())
        (upgrade-nodes)
        (list (if evicted? (node-sleep-at-door) (node-sleep)))))

    ;; 容器名固定为"家"（导航按名字定位，不能随住所变），住所等级放 subtitle 显示。
    (define (residence-container)
      (place "家" :subtitle residence :children (home-body)))

    ;; ── Message Passing Interface ─────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (residence-container)))

          ;; 给其他地点（如老街酒馆的“点一杯酒”）查询/触发同一份每日一杯限制。
          ((equal? msg 'drank-today?) drank-today?)
          ((equal? msg 'drink!) (apply-drink-effect!))
          ;; 经理或警局接通联络线；重复调用不重置委托池。
          ((equal? msg 'connect-phone!)
           (if has-phone?
               #f
               (begin
                 (set! has-phone? #t)
                 (if (baines 'registered?) (board 'open-line!) #f))))

          ((equal? msg 'save)
           (list
             (list "residence"      residence)
             (list "has-flower?"    has-flower?)
             (list "has-mattress?"  has-mattress?)
             (list "has-phone?"     has-phone?)
             (list "has-gramophone?" has-gramophone?)
             (list "drank-today?" drank-today?)
             (list "flower-today?" flower-today?)
             (list "medicated-today?" medicated-today?)
             (list "apartment-offer-known?" apartment-offer-known?)
             (list "rent-left"      (rent-clk 'save))
             (list "grace"          grace)
             (list "overdue?"       overdue?)
             (list "grace-left"     (grace-clk 'save))
             (list "evicted?"       evicted?)
             (list "just-slept?"    刚睡过?)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! residence      (required-field data "residence"))
             (set! has-flower?    (required-field data "has-flower?"))
             (set! has-mattress?  (required-field data "has-mattress?"))
             (set! has-phone?     (required-field data "has-phone?"))
             ;; 旧档没有这一项：当作没买。等旧档不再需要就换回 required-field。
             (set! has-gramophone? (assoc-get data "has-gramophone?" #f))
             (set! drank-today?   (required-field data "drank-today?"))
             (set! flower-today?  (required-field data "flower-today?"))
             (set! medicated-today? (required-field data "medicated-today?"))
             (set! apartment-offer-known? (required-field data "apartment-offer-known?"))
             (rent-clk 'load!     (required-field data "rent-left"))
             (set! grace          (required-field data "grace"))
             (set! overdue?       (required-field data "overdue?"))
             (grace-clk 'load!    (required-field data "grace-left"))
             (set! evicted?       (required-field data "evicted?"))
             ;; 旧档没有这一项：当作没锁，读档醒来能直接睡。等旧档不再需要就换回 required-field。
             (set! 刚睡过?        (assoc-get data "just-slept?" #f))
             ;; 一次性改名迁移：起点从「旅馆」改叫「租的房间」（同一个东西，换了说法）。
             ;; 显式写在这儿而不是让 required-field 静默放行；等旧档不再需要就删掉这三行。
             (if (equal? residence "旅馆") (set! residence "租的房间") #f)
             (if (member? residence (list "租的房间" "公寓"))
                 #t (error "住所存档错误：住所类型非法"))
             (if (and (number? grace) (>= grace 0) (<= grace grace-max))
                 #t (error "住所存档错误：房东宽限额度非法"))
             (if (and (boolean-value? has-flower?)
                      (boolean-value? has-mattress?)
                      (boolean-value? has-phone?)
                      (boolean-value? has-gramophone?)
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
