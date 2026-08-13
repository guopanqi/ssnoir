;; 弗兰克（Frank Delaney）——码头工头、老街组织者。
;;
;; 本模块只拥有第一章「不开的船」人物线：摩托车疑点、统一货船抢修、扣船交锋、
;; 分钱、莱恩边界与首演援助状态。劳工公共声望仍由引擎统一持有；relationship 只记录
;; 这条人物线已经发生的离散事实，不是第二套可反复刷取的好感数值。

(define frank
  (let ()
    (define relationship "陌生")       ; 陌生 / 认识 / 认可 / 不信任
    (define repair-state "未开放")      ; 未开放 / 进行中 / 按时修好 / 勉强修好 / 未介入
    (define repair-participated? #f)
    (define repair-attempts 0)
    (define repair-duration 3)
    (define repair-deadline-day 0)
    (define repair-clk
      (make-clock "货船抢修" 6 'segments
        "统一抢修进度。抽水、补板、钢缆和货物固定都由弗兰克排在同一张班表上。"))

    (define hold-state "未发生")        ; 未发生 / 待安排 / 待处理 / 六类结算
    (define hold-open-day 0)
    (define hold-event-day 0)
    (define hold-participated? #f)
    (define hold-payment 0)
    (define distribution-viewed? #f)
    (define cigarette-talked? #f)

    (define motorcycle-seen? #f)
    (define motorcycle-suspicion "未注意") ; 未注意 / 怀疑 / 近似确认
    (define lyon-boundary "未提出")         ; 未提出 / 已提出 / 遵守 / 违背
    (define premiere-aid "未开放")          ; 未开放 / 可请求 / 已请求

    (define (required-field data key)
      (let ((value (assoc-get data key 'missing)))
        (if (equal? value 'missing)
            (error (string-append "弗兰克存档错误：缺少 " key))
            value)))

    (define (valid-relationship? value)
      (member? value (list "陌生" "认识" "认可" "不信任")))

    (define (valid-repair-state? value)
      (member? value (list "未开放" "进行中" "按时修好" "勉强修好" "未介入")))

    (define (valid-hold-state? value)
      (member? value
        (list "未发生" "待安排" "待处理" "全额到账" "部分到账" "以货抵债"
              "放船离开" "失控" "缺席")))

    (define (valid-suspicion? value)
      (member? value (list "未注意" "怀疑" "近似确认")))

    (define (valid-boundary? value)
      (member? value (list "未提出" "已提出" "遵守" "违背")))

    (define (valid-aid? value)
      (member? value (list "未开放" "可请求" "已请求")))

    (define (boolean? value)
      (or (equal? value #t) (equal? value #f)))

    (define (repair-settled?)
      (member? repair-state (list "按时修好" "勉强修好" "未介入")))

    (define (hold-settled?)
      (member? hold-state
        (list "全额到账" "部分到账" "以货抵债" "放船离开" "失控" "缺席")))

    (define (recognized?) (equal? relationship "认可"))

    (define (sync-globals!)
      (if (and (recognized?) (equal? premiere-aid "未开放"))
          (set! premiere-aid "可请求")
          #f)
      (set-global! '弗兰克关系 relationship)
      (set-global! '弗兰克抢修 repair-state)
      (set-global! '弗兰克扣船 hold-state)
      (set-global! '弗兰克摩托车 motorcycle-suspicion)
      (set-global! '弗兰克莱恩边界 lyon-boundary)
      (set-global! '弗兰克首演援助 premiere-aid))

    (define (meet!)
      (if (equal? relationship "陌生") (set! relationship "认识") #f)
      (if (and motorcycle-seen? (equal? motorcycle-suspicion "未注意"))
          (set! motorcycle-suspicion "怀疑")
          #f)
      (sync-globals!))

    (define (validate-state!)
      (if (valid-relationship? relationship) #t (error "弗兰克存档错误：关系状态非法"))
      (if (valid-repair-state? repair-state) #t (error "弗兰克存档错误：抢修状态非法"))
      (if (valid-hold-state? hold-state) #t (error "弗兰克存档错误：扣船状态非法"))
      (if (valid-suspicion? motorcycle-suspicion) #t (error "弗兰克存档错误：摩托车怀疑非法"))
      (if (valid-boundary? lyon-boundary) #t (error "弗兰克存档错误：莱恩边界非法"))
      (if (valid-aid? premiere-aid) #t (error "弗兰克存档错误：首演援助非法"))
      (if (and (boolean? repair-participated?) (boolean? hold-participated?)
               (boolean? distribution-viewed?) (boolean? cigarette-talked?)
               (boolean? motorcycle-seen?))
          #t (error "弗兰克存档错误：人物布尔状态类型非法"))
      (if (and (number? repair-attempts) (>= repair-attempts 0))
          #t (error "弗兰克存档错误：抢修参与次数非法"))
      (if (and (number? repair-deadline-day) (>= repair-deadline-day 0))
          #t (error "弗兰克存档错误：抢修期限非法"))
      (if (and (number? hold-open-day) (>= hold-open-day 0)
               (number? hold-event-day) (>= hold-event-day 0))
          #t (error "弗兰克存档错误：扣船日期非法"))
      (if (and (number? hold-payment) (>= hold-payment 0) (<= hold-payment 6))
          #t (error "弗兰克存档错误：扣船付款进度非法"))
      (if (and motorcycle-seen? (equal? motorcycle-suspicion "未注意"))
          #t
          (if (and (not motorcycle-seen?) (not (equal? motorcycle-suspicion "未注意")))
              (error "弗兰克存档错误：没有看见摩托车却已有怀疑") #t))
      (if (and (equal? motorcycle-suspicion "近似确认") (not (recognized?)))
          (error "弗兰克存档错误：关系不足却得到摩托车近似确认") #t)
      (cond
        ((equal? repair-state "未开放")
         (if (and (= repair-deadline-day 0) (= (repair-clk 'current) 0)
                  (not repair-participated?) (= repair-attempts 0)
                  (equal? hold-state "未发生"))
             #t (error "弗兰克存档错误：未开放抢修残留进度或后续事件")))
        ((equal? repair-state "进行中")
         (if (and (> repair-deadline-day world-day) (equal? hold-state "未发生"))
             #t (error "弗兰克存档错误：进行中抢修缺少有效期限或提前开放扣船")))
        ((repair-settled?)
         (if (and (= repair-deadline-day 0) (not (equal? hold-state "未发生")))
             #t (error "弗兰克存档错误：抢修已结算但扣船事件没有排期")))
        (else (error "弗兰克存档错误：无法校验抢修状态")))
      (if (equal? repair-participated? (> repair-attempts 0))
          #t (error "弗兰克存档错误：抢修参与与参与次数不一致"))
      (if (and (> (repair-clk 'current) 0) (not repair-participated?))
          (error "弗兰克存档错误：有抢修进度却没有参与记录") #t)
      (if (and (equal? repair-state "未介入") repair-participated?)
          (error "弗兰克存档错误：未介入结算却记录了抢修参与") #t)
      (if (and (member? repair-state (list "按时修好" "勉强修好")) (not repair-participated?))
          (error "弗兰克存档错误：参加型抢修结算没有参与记录") #t)
      (if (and (equal? repair-state "进行中") (repair-clk 'full?))
          (error "弗兰克存档错误：抢修进度已满却仍在进行中") #t)
      (cond
        ((equal? hold-state "未发生")
         (if (and (= hold-open-day 0) (= hold-event-day 0) (not hold-participated?))
             #t (error "弗兰克存档错误：未发生扣船残留日期或参与")))
        ((equal? hold-state "待安排")
         (if (and (> hold-open-day 0) (= hold-event-day 0) (not hold-participated?))
             #t (error "弗兰克存档错误：待安排扣船日期或参与状态错误")))
        ((equal? hold-state "待处理")
         (if (and (> hold-open-day 0) (> hold-event-day 0))
             #t (error "弗兰克存档错误：待处理扣船缺少日期")))
        ((hold-settled?)
         (if (and (> hold-open-day 0) (> hold-event-day 0))
             #t (error "弗兰克存档错误：扣船结算缺少发生日")))
        (else (error "弗兰克存档错误：无法校验扣船状态")))
      (if (and (equal? hold-state "缺席") hold-participated?)
          (error "弗兰克存档错误：缺席结算却记录了当场参与") #t)
      (if (and (hold-settled?) (not (equal? hold-state "缺席")) (not hold-participated?))
          (error "弗兰克存档错误：当场扣船结算没有参与记录") #t)
      (if (and (equal? hold-state "待处理") hold-participated?)
          (error "弗兰克存档错误：普通城市存档不能停在已进入的扣船交锋") #t)
      (cond
        ((member? hold-state (list "未发生" "待安排" "待处理"))
         (if (= hold-payment 0) #t (error "弗兰克存档错误：扣船结算前已有付款结果")))
        ((equal? hold-state "全额到账")
         (if (= hold-payment 6) #t (error "弗兰克存档错误：全额到账没有填满工钱")))
        ((equal? hold-state "部分到账")
         (if (and (>= hold-payment 3) (< hold-payment 6))
             #t (error "弗兰克存档错误：部分到账不在可接受区间")))
        ((equal? hold-state "缺席")
         (if (= hold-payment 3) #t (error "弗兰克存档错误：缺席结算没有保留部分付款")))
        (else #t))
      (if (and distribution-viewed? (not (and hold-participated? (hold-settled?))))
          (error "弗兰克存档错误：尚无可看的分钱场景却已标记看过") #t)
      (if (and cigarette-talked? (not (repair-settled?)))
          (error "弗兰克存档错误：抢修未结算却已经谈过老金牌") #t)
      (if (and repair-participated? (equal? relationship "陌生"))
          (error "弗兰克存档错误：参加过抢修却仍与弗兰克陌生") #t)
      (if (and (not (equal? premiere-aid "未开放")) (not (recognized?)))
          (error "弗兰克存档错误：未获认可却开放了首演援助") #t)
      (if (and (recognized?) (equal? premiere-aid "未开放"))
          (error "弗兰克存档错误：已获认可却未开放首演援助") #t)
      (if (and (member? lyon-boundary (list "已提出" "遵守" "违背")) (not (recognized?)))
          (error "弗兰克存档错误：未获认可却已有莱恩边界") #t))

    ;; ── 交割夜的摩托车疑点 ───────────────────────────
    (define (on-delivery-chase! noticed?)
      (if (or (equal? noticed? #t) (equal? noticed? #f))
          #t (error "弗兰克：摩托车注意状态必须是布尔量"))
      (if noticed? (set! motorcycle-seen? #t) #f)
      (sync-globals!))

    ;; ── 货船抢修 ─────────────────────────────────────
    (define (repair-days-left)
      (if (equal? repair-state "进行中")
          (max 0 (- repair-deadline-day world-day))
          0))

    (define (repair-frank-text)
      (cond
        ((= repair-attempts 1)
         "弗兰克把喘得最厉害的老工人支去清点工具，让他照样算一班；熟泵房的人全被叫到舱底。")
        ((= repair-attempts 2)
         "有人割伤手，弗兰克立刻换人，自己顶到钢缆边；面包和药送到了，还能站的人继续干。")
        ((= (modulo repair-attempts 2) 1)
         "一只没有厂牌的泵接上了旧管线。没人问它从哪来；弗兰克等众人说完，才把下一班写上木板。")
        (else
         "他记得谁会补船板、谁夜里眼睛不好。人群说完以后安静下来，等他把每个人放到该在的位置。")))

    (define (note-repair-work!)
      (set! repair-participated? #t)
      (set! repair-attempts (+ repair-attempts 1))
      (meet!)
      (play-banter! (line "世界" (repair-frank-text))))

    (define (settle-repair! result)
      (if (equal? repair-state "进行中") #t (error "弗兰克：抢修只能从进行中结算"))
      (if (member? result (list "按时修好" "勉强修好" "未介入"))
          #t (error "弗兰克：未知抢修结算"))
      (if (and (equal? result "按时修好") (not (repair-clk 'full?)))
          (error "弗兰克：进度未满却结算为按时修好") #t)
      (if (and (equal? result "未介入") repair-participated?)
          (error "弗兰克：参加过抢修却结算为未介入") #t)
      (set! repair-state result)
      (set! repair-deadline-day 0)
      (set! hold-state "待安排")
      (set! hold-open-day (+ world-day 1))
      (if repair-participated? (grant-favor-relation! "劳工") #f)
      (sync-globals!)
      (spotlight! "货船达到离港标准"
        (cond
          ((equal? result "按时修好")
           "泵压住了进水，补板和钢缆都按班表收尾。你留有完整抢修记录；代理明天就会来验船。")
          ((equal? result "勉强修好")
           "你参加过抢修，但进度没赶满。弗兰克带余下的人补到天亮，船勉强达到最低离港标准。")
          (else
           "你没有参加。弗兰克带码头上的人补到天亮，船仍达到最低离港标准；抢修记录不在你手里。"))))

    (define (advance-repair! n)
      (if (equal? repair-state "进行中") #t (error "弗兰克：货船抢修尚未开放"))
      (repair-clk 'advance! n)
      (if (repair-clk 'full?) (settle-repair! "按时修好") #f))

    (define (node-repair)
      (node "参加货船抢修"
        :subtitle "力量；一根统一进度。坏：受伤，中：+1 格，好：+2 格"
        :tags (list "限期" "高风险")
        :clocks (list
          (repair-clk 'render-data)
          (list 'clock "抢修窗口" (repair-days-left) repair-duration 'countdown
                "期限内可以反复投入行动；到期后弗兰克会带人补到最低离港标准。"))
        :requires (list (req-die))
        :resolve (roll 'violence
          (outcome "钢缆扫过跳板"
            (lambda () (note-repair-work!) (injure!)))
          (outcome "稳住一段进水"
            (lambda () (note-repair-work!) (advance-repair! 1)))
          (outcome "抢下关键一班"
            (lambda () (note-repair-work!) (advance-repair! 2))))))

    ;; ── 船修好了却不开 ───────────────────────────────
    (define (result-name symbol)
      (cond
        ((equal? symbol '全额到账) "全额到账")
        ((equal? symbol '部分到账) "部分到账")
        ((equal? symbol '以货抵债) "以货抵债")
        ((equal? symbol '放船离开) "放船离开")
        ((equal? symbol '失控) "失控")
        (else (error "弗兰克：扣船交锋返回了未登记结果"))))

    (define (apply-hold-relationship! result payment)
      (cond
        ((or (equal? result "全额到账") (equal? result "以货抵债"))
         (set! relationship "认可")
         (change-faction-relation! "劳工" 1))
        ((equal? result "部分到账")
         (if (equal? relationship "陌生") (set! relationship "认识") #f))
        ((equal? result "放船离开")
         (set! relationship "不信任")
         (change-faction-relation! "劳工" -1))
        ((equal? result "失控")
         (cond
           ((>= payment 3) (set! relationship "认可"))
           ((= payment 0) (set! relationship "不信任"))
           ((equal? relationship "陌生") (set! relationship "认识") #f)))
        (else (error "弗兰克：无法按扣船结果写回关系"))))

    (define (on-hold-result result)
      (if (equal? hold-state "待处理") #t (error "弗兰克：没有待结算的扣船交锋"))
      (if (and (list? result) (= (length result) 2))
          #t (error "弗兰克：扣船交锋应回传 (list 结算 工钱格数)"))
      (let ((final-result (result-name (car result)))
            (payment (cadr result)))
        (if (and (number? payment) (>= payment 0) (<= payment 6))
            #t (error "弗兰克：扣船交锋付款进度非法"))
        (set! hold-state final-result)
        (set! hold-payment payment)
        (apply-hold-relationship! final-result payment)
        (complete-section!)
        (sync-globals!)))

    (define (start-hold!)
      (if (and (equal? hold-state "待处理") (= world-day hold-event-day))
          #t (error "弗兰克：扣船交锋已经不在开放当天"))
      (set! hold-participated? #t)
      (meet!)
      (set-global! '扣船-抢修结果 repair-state)
      (set-global! '扣船-掌握情报 (> (item-count "情报") 0))
      (start-encounter "船修好了却不开" on-hold-result))

    (define (node-hold-entry)
      (encounter-action "去看那条不开的船"
        (lambda () (start-hold!))))

    (define (settle-hold-absence!)
      (if (equal? hold-state "待处理") #t (error "弗兰克：扣船缺席结算时事件并非待处理"))
      (if hold-participated? (error "弗兰克：已经进入扣船交锋却试图按缺席结算") #t)
      (set! hold-state "缺席")
      (set! hold-payment 3)
      (sync-globals!)
      (spotlight! "不开的船离港了"
        "你没有去泊位。弗兰克让跳板封了一整天，最后逼到一部分现金；代理带走船，余下欠款仍挂在失踪承包人名下。"))

    ;; ── 分钱与老金牌 ─────────────────────────────────
    (define (distribution-text)
      (cond
        ((equal? hold-state "全额到账")
         "钱箱先摆到伤者那一边。欠租、家里有人吃药的排在下一列，然后才按抢修班次点名。最后一叠没有写进正式账簿。")
        ((equal? hold-state "部分到账")
         "钱不够。弗兰克先付伤者和快被赶出房子的几家，出过班的人按剩下的数分；每个人都等他在账本上落笔。")
        ((equal? hold-state "以货抵债")
         "木箱拆开以后，药、罐头和能转卖的布匹先分给伤者和欠租家庭。其余按班次记账，账本仍在弗兰克手里。")
        ((equal? hold-state "放船离开")
         "桌上没有钱。弗兰克仍把伤者、欠租家庭和临时工的名字抄进一本没有封皮的账册，屋里的人等他决定下一笔从哪里补。")
        ((equal? hold-state "失控")
         (string-append
           "警卫清场前带回来的钱只有 " (number->string hold-payment)
           " 格。弗兰克先分给伤者和没有稳定班次的人，剩下的人等他把名字一笔一笔划过去。"))
        (else (error "弗兰克：缺席玩家不应进入分钱场景"))))

    (define (node-distribution)
      (instant-action "看弗兰克分钱"
        (lambda ()
          (play-dialogue!
            (line "世界" (distribution-text))
            (line "尼尔" "不按每个人的班次平均分？")
            (line "弗兰克" "每个人过的不是一样的日子。能等下一班的，先让不能等的拿。")
            (line "世界" "他说完继续点名。账本没有离开他的手。屋里也没有人催他。"))
          (set! distribution-viewed? #t))))

    (define (absence-observation)
      (observe-action "扣船后的传闻"
        "你没在场。工人说弗兰克封了一天跳板，逼到一部分现金；分钱时伤者、欠租家庭和没有固定班次的人排在最前面。"))

    (define (node-cigarettes)
      (instant-action "问那包老金牌"
        (lambda ()
          (meet!)
          (if motorcycle-seen?
              (play-dialogue!
                (line "世界" "一包红色老金牌从工具桌这头传到那头。每个人抽一根，又把烟盒递给下一个。")
                (line "尼尔" "交割那晚，也有人带着这个牌子。")
                (line "弗兰克" "工会房里一包烟能转十只手。它能把你领到一扇门，不能替你认出门里的人。")
                (line "尼尔" "还有一辆摩托车。")
                (line "弗兰克" "你那晚喊过自己是谁吗？一个本地人看见外地人追老街的人，还能先问什么？")
                (if (recognized?)
                    (line "弗兰克" "我让那辆车从你们中间过去。至于他为什么被追，我当时不知道，现在也不替他认。")
                    (line "弗兰克" "背影和烟盒都不是脸。你要问人，就继续问人。")))
              (play-dialogue!
                (line "世界" "一包红色老金牌从工具桌这头传到那头。每个人抽一根，又把烟盒递给下一个。")
                (line "尼尔" "交割那晚，也有人带着这个牌子。")
                (line "弗兰克" "工会房里一包烟能转十只手。它能把你领到一扇门，不能替你认出门里的人。")))
          (set! cigarette-talked? #t)
          (if motorcycle-seen?
              (set! motorcycle-suspicion (if (recognized?) "近似确认" "怀疑"))
              #f)
          (result-note! "调查方向：老街工会房间；老金牌不能证明骑手身份")
          (sync-globals!))))

    (define (node-confirm-motorcycle)
      (instant-action "再问那晚的摩托车"
        (lambda ()
          (play-dialogue!
            (line "尼尔" "那晚的车，是你骑的。")
            (line "弗兰克" "我让一个老街人从外地人手里多了一条路。")
            (line "尼尔" "你知道他在替谁拿钱？")
            (line "弗兰克" "不知道。知道了，我也不会替他做的事说情。"))
          (set! motorcycle-suspicion "近似确认")
          (sync-globals!))))

    ;; ── 莱恩与首演接口 ───────────────────────────────
    (define (lyon-entry-state)
      (cond
        ((equal? relationship "认可") "认可")
        ((equal? relationship "不信任") "不信任")
        (else "普通")))

    (define (prepare-lyon-entry!)
      (cond
        ((equal? relationship "认可")
         (if (equal? lyon-boundary "未提出")
             (begin
               (set! lyon-boundary "已提出")
               (play-dialogue!
                 (line "弗兰克" "看堆场的人今晚不会在。你可以进去。")
                 (line "弗兰克" "莱恩做的事下作。但你不能把他交给警察——老街的人，由老街自己处置。")))
             #f))
        ((equal? relationship "不信任")
         (play-remote-dialogue!
           (line "世界" "你还没走到堆场，沿路的窗已经一扇接一扇亮起来。有人提前放了风。")))
        (else #f))
      (sync-globals!))

    (define (on-lyon-result! handed-to-police?)
      (if (boolean? handed-to-police?)
          #t (error "弗兰克：莱恩去向必须明确说明是否交给警方"))
      (if (equal? lyon-boundary "已提出")
          (set! lyon-boundary (if handed-to-police? "违背" "遵守"))
          #f)
      (sync-globals!))

    (define (request-premiere-aid!)
      (if (equal? premiere-aid "可请求")
          (set! premiere-aid "已请求")
          (error "弗兰克：当前不能请求首演外圈援助"))
      (sync-globals!))

    (define (validate-premiere-request! requested?)
      (if (or (equal? requested? #t) (equal? requested? #f))
          #t (error "弗兰克：首演主线传入了非布尔援助状态"))
      (if (equal? requested? (equal? premiere-aid "已请求"))
          #t (error "弗兰克存档错误：人物线与首演主线的人手请求不一致")))

    (define (validate-chapter-end!)
      (validate-state!)
      (if (repair-settled?) #t (error "第一章结算错误：货船抢修尚未结算"))
      (if (hold-settled?) #t (error "第一章结算错误：不开的船仍未结算"))
      (if (equal? lyon-boundary "已提出")
          (error "第一章结算错误：弗兰克的莱恩边界尚未写回结果") #t))

    ;; ── 码头节点与日程 ───────────────────────────────
    (define (frank-description)
      (cond
        ((equal? relationship "认可")
         "码头工头。人们把班表、伤者和欠款都报到他这里，然后等他决定先办哪一件。")
        ((equal? relationship "不信任")
         "码头工头。他仍能让整条跳板停下来，但看见你时不再把账本摊开。")
        (else
         "码头工头。他不抬高声音；四周的人说完以后会自然安静，等他作决定。")))

    (define (node-frank)
      (node "弗兰克"
        :subtitle "Frank Delaney；码头工头、老街组织者"
        :resolve (observe (frank-description))))

    (define (dock-nodes)
      (append
        (if (equal? repair-state "进行中") (list (node-repair)) '())
        (if (equal? hold-state "待处理") (list (node-hold-entry)) '())
        (if (or (not (equal? repair-state "未开放")) (not (equal? hold-state "未发生")))
            (list (node-frank)) '())
        (if (and hold-participated? (hold-settled?) (not distribution-viewed?))
            (list (node-distribution)) '())
        (if (and (equal? hold-state "缺席") (not hold-participated?))
            (list (absence-observation)) '())
        (if (and (repair-settled?) (not cigarette-talked?))
            (list (node-cigarettes)) '())
        (if (and cigarette-talked? (recognized?) (equal? motorcycle-suspicion "怀疑"))
            (list (node-confirm-motorcycle)) '())))

    (define-turn-rule "货船抢修开放"
      (lambda () (and (equal? repair-state "未开放") (>= (three-letters 'story-stage) 2)))
      (lambda ()
        (set! repair-state "进行中")
        (set! repair-deadline-day (+ world-day repair-duration))
        (sync-globals!)
        (spotlight! "旧货船进水"
          "一艘进水的旧货船被拖回码头。船主只留三天抢修窗口；码头现在开放统一目标「参加货船抢修」。")))

    (define-turn-rule "货船抢修期限"
      (lambda () (and (equal? repair-state "进行中") (>= world-day repair-deadline-day)))
      (lambda ()
        (settle-repair! (if repair-participated? "勉强修好" "未介入"))))

    (define-turn-rule "不开的船等待合适白天"
      (lambda () (equal? hold-state "待安排"))
      (lambda ()
        (if (and (>= world-day hold-open-day) (not (rest-blocked?)))
            (begin
              (set! hold-state "待处理")
              (set! hold-event-day world-day)
              (sync-globals!)
              (spotlight! "船修好了却不开"
                "货运代理拒绝直接支付工钱。弗兰克已经扣下一件可随时装回的关键部件，并让工人封住跳板；事件只开放今天。"))
            #f)))

    (define-turn-rule "不开的船缺席结算"
      (lambda () (and (equal? hold-state "待处理") (> world-day hold-event-day)))
      (lambda () (settle-hold-absence!)))

    (sync-globals!)
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'dock-nodes) (dock-nodes))
          ((equal? msg 'relationship) relationship)
          ((equal? msg 'repair-state) repair-state)
          ((equal? msg 'hold-state) hold-state)
          ((equal? msg 'motorcycle-suspicion) motorcycle-suspicion)
          ((equal? msg 'lyon-boundary) lyon-boundary)
          ((equal? msg 'premiere-aid) premiere-aid)
          ((equal? msg 'premiere-aid-open?) (equal? premiere-aid "可请求"))
          ((equal? msg 'premiere-aid-requested?) (equal? premiere-aid "已请求"))
          ((equal? msg 'lyon-entry-state) (lyon-entry-state))
          ((equal? msg 'meet!) (meet!))
          ((equal? msg 'on-delivery-chase!) (on-delivery-chase! (cadr args)))
          ((equal? msg 'prepare-lyon-entry!) (prepare-lyon-entry!))
          ((equal? msg 'on-lyon-result!) (on-lyon-result! (cadr args)))
          ((equal? msg 'request-premiere-aid!) (request-premiere-aid!))
          ((equal? msg 'validate!) (validate-state!))
          ((equal? msg 'validate-premiere-request!) (validate-premiere-request! (cadr args)))
          ((equal? msg 'validate-chapter-end!) (validate-chapter-end!))
          ((equal? msg 'sync-globals!) (sync-globals!))
          ((equal? msg 'save)
           (list
             (list "relationship" relationship)
             (list "repair-state" repair-state)
             (list "repair-participated?" repair-participated?)
             (list "repair-attempts" repair-attempts)
             (list "repair-progress" (repair-clk 'save))
             (list "repair-deadline-day" repair-deadline-day)
             (list "hold-state" hold-state)
             (list "hold-open-day" hold-open-day)
             (list "hold-event-day" hold-event-day)
             (list "hold-participated?" hold-participated?)
             (list "hold-payment" hold-payment)
             (list "distribution-viewed?" distribution-viewed?)
             (list "cigarette-talked?" cigarette-talked?)
             (list "motorcycle-seen?" motorcycle-seen?)
             (list "motorcycle-suspicion" motorcycle-suspicion)
             (list "lyon-boundary" lyon-boundary)
             (list "premiere-aid" premiere-aid)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! relationship (required-field data "relationship"))
             (set! repair-state (required-field data "repair-state"))
             (set! repair-participated? (required-field data "repair-participated?"))
             (set! repair-attempts (required-field data "repair-attempts"))
             (repair-clk 'load! (required-field data "repair-progress"))
             (set! repair-deadline-day (required-field data "repair-deadline-day"))
             (set! hold-state (required-field data "hold-state"))
             (set! hold-open-day (required-field data "hold-open-day"))
             (set! hold-event-day (required-field data "hold-event-day"))
             (set! hold-participated? (required-field data "hold-participated?"))
             (set! hold-payment (required-field data "hold-payment"))
             (set! distribution-viewed? (required-field data "distribution-viewed?"))
             (set! cigarette-talked? (required-field data "cigarette-talked?"))
             (set! motorcycle-seen? (required-field data "motorcycle-seen?"))
             (set! motorcycle-suspicion (required-field data "motorcycle-suspicion"))
             (set! lyon-boundary (required-field data "lyon-boundary"))
             (set! premiere-aid (required-field data "premiere-aid"))
             (validate-state!)
             (sync-globals!)))
          (else #f))))))
