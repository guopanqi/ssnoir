;; 弗兰克（Frank Delaney）——码头工头、老街组织者。
;;
;; 第一章他有一条不进卷宗的码头事件链。一个认为「这里的事由这里的人处理」的人，
;; 不会站在地图上等你接任务；第二天旧货船改泊，赶上码头才会看见他组织抢修。
;;   一、货船抢修、船修好了却不开、分钱——三拍都在表现他如何管这条街。
;;   二、巷子那一晚，他挡在你和莱恩中间。那一晚由《三封信》拥有，这里只收结果。
;;
;; 人物关系的核心事实是：**弗兰克认不认你这个人**。
;; 它由巷子那晚写入（当街把已经不还手的人往死里打＝不认），
;; 继续影响他自己的码头事件，不再折算成首演夜的自动战斗人手。
;;
;; 第二章他只有一件事写回这里：《别给他们想要的》那天街上有没有人先动手
;; （见 第二章/人物事件/别给他们想要的.scm）。调停成功，酒馆后屋从此对你开着
;; （后屋的生活内容在 老街酒馆.scm），而且机器进老街那天他还坐得下来；
;; 失败或没去，这条线中断，他更信「跟他们讲道理没用」。

(define frank
  (let ()
    (define approved? #f)          ; 弗兰克认可
    (define alley-settled? #f)     ; 巷子那晚已经结过
    (define met? #f)               ; 见过他本人（工会房间或码头）

    (define repair-state "未开放")  ; 未开放 / 进行中 / 已结束
    (define repair-days 3)
    (define repair-deadline-day 0)
    (define repair-joined? #f)
    (define repair-arrival-viewed? #f)
    (define repair-result "未结算") ; 未结算 / 按时修好 / 勉强修好 / 未介入
    (define repair-clk
      (make-clock "货船抢修" 4 'gauge
        "临时改泊的船进了水。抽水、补板、钢缆和货物固定都在同一张班表上。"))

    (define hold-state "未发生")    ; 未发生 / 待安排 / 待处理 / 已结算 / 缺席
    (define hold-open-day 0)
    (define hold-day 0)
    (define hold-paid? #f)          ; 这条街当天拿到了东西
    (define hold-payment 0)
    (define distribution-viewed? #f)

    (define paper-seen? #f)         ; 首演之后看见他在看报纸
    ;; 第二章：警察来老街带人那天的调停。未发生 / 等你 / 成功 / 失败 / 缺席。
    ;; 「等你」和「成功」时他人在酒馆后屋，不在居民区的工会房间。
    (define mediation "未发生")
    (define mediation-states (list "未发生" "等你" "成功" "失败" "缺席"))
    ;; 机器进老街那天，他还坐不坐得下来。没人在他面前做成过一次「不动手也能收场」，
    ;; 他自己会走到硬的那一边——所以这是「你有没有给过他第二种做法」，不是「你对他好不好」。
    ;; 第三章问的是同一件事：他最后是能代表这条街谈判的人，还是一个暴力头领。
    (define at-table? #f)

    (define (required-field data key)
      (let ((value (assoc-get data key 'missing)))
        (if (equal? value 'missing)
            (error (string-append "弗兰克存档错误：缺少 " key))
            value)))

    (define (boolean? value) (or (equal? value #t) (equal? value #f)))

    (define (repair-done?) (equal? repair-state "已结束"))
    (define (hold-settled?) (member? hold-state (list "已结算" "缺席")))

    ;; 货船在泊：那条大船停在码头航道上的日子（抢修中、修好等验船、扣船那天）。
    ;; 画面由 PropMotion.SyncAll 读这个键摆船；当天尚未看入场时仍留在画外，
    ;; 让进码头的 play-motion! 真正从 Offshore 播到 Berthed。错过当天后船照常在泊。
    (define (ship-berthed?)
      (or (and (equal? repair-state "进行中")
               (or repair-arrival-viewed?
                   (> world-day (- repair-deadline-day repair-days))))
          (member? hold-state (list "待安排" "待处理"))))

    (define (sync-globals!)
      (set-global! '弗兰克认可 approved?)
      (set-global! '货船在泊 (ship-berthed?)))

    (define (meet!)
      (set! met? #t)
      (sync-globals!))

    (define (validate-state!)
      (if (and (boolean? approved?) (boolean? alley-settled?) (boolean? met?)
               (boolean? repair-joined?) (boolean? repair-arrival-viewed?) (boolean? hold-paid?)
               (boolean? distribution-viewed?)
               (boolean? paper-seen?))
          #t (error "弗兰克存档错误：布尔状态类型非法"))
      (if (member? repair-state (list "未开放" "进行中" "已结束"))
          #t (error "弗兰克存档错误：抢修状态非法"))
      (if (member? repair-result (list "未结算" "按时修好" "勉强修好" "未介入"))
          #t (error "弗兰克存档错误：抢修结果非法"))
      (if (equal? repair-state "已结束")
          (if (equal? repair-result "未结算")
              (error "弗兰克存档错误：抢修结束却没有结果") #t)
          (if (equal? repair-result "未结算") #t
              (error "弗兰克存档错误：抢修尚未结束却已有结果")))
      (if (and (equal? repair-result "按时修好")
               (or (not repair-joined?) (not (repair-clk 'full?))))
          (error "弗兰克存档错误：按时修好与玩家贡献不一致") #t)
      (if (and (equal? repair-result "勉强修好") (not repair-joined?))
          (error "弗兰克存档错误：未参与却记成勉强修好") #t)
      (if (and (equal? repair-result "未介入") repair-joined?)
          (error "弗兰克存档错误：参与过却记成未介入") #t)
      (if (member? hold-state (list "未发生" "待安排" "待处理" "已结算" "缺席"))
          #t (error "弗兰克存档错误：扣船状态非法"))
      (if (member? mediation mediation-states)
          #t (error "弗兰克存档错误：调停状态非法"))
      (if (and at-table? (not (equal? mediation "成功")))
          (error "弗兰克存档错误：调停没成功却记着他坐得下来") #t)
      (if (and (number? hold-payment) (>= hold-payment 0) (<= hold-payment 6))
          #t (error "弗兰克存档错误：扣船付款进度非法"))
      (if (and approved? (not alley-settled?))
          (error "弗兰克存档错误：巷子未结算却已获认可") #t)
      (if (and (equal? repair-state "进行中") (<= repair-deadline-day world-day))
          (error "弗兰克存档错误：进行中的抢修没有有效期限") #t)
      (if (and (equal? repair-state "未开放") (not (equal? hold-state "未发生")))
          (error "弗兰克存档错误：抢修未开放却已排期扣船") #t)
      (if (and (repair-done?) (equal? hold-state "未发生"))
          (error "弗兰克存档错误：抢修结束却没有排期扣船") #t)
      (if (and (> (repair-clk 'current) 0) (not repair-joined?))
          (error "弗兰克存档错误：有抢修进度却没有参与记录") #t)
      (if (and distribution-viewed? (not (equal? hold-state "已结算")))
          (error "弗兰克存档错误：没有可看的分钱场景却已标记看过") #t)
      #t)

    ;; ── 巷子那一晚的唯一回执 ─────────────────────────
    ;; 参数是「当街收场」：你在他面前把一个已经不还手的人按住搜、拖、打到难看。
    (define (on-alley-result! rough?)
      (if (boolean? rough?) #t (error "弗兰克：巷子回执必须是布尔量"))
      (set! alley-settled? #t)
      (set! met? #t)
      (set! approved? (not rough?))
      (sync-globals!))

    ;; ── 货船抢修 ─────────────────────────────────────
    (define (repair-days-left)
      (if (equal? repair-state "进行中") (max 0 (- repair-deadline-day world-day)) 0))

    (define (settle-repair! completed-by-player?)
      (if (equal? repair-state "进行中") #t (error "弗兰克：抢修只能从进行中结算"))
      (set! repair-state "已结束")
      (set! repair-deadline-day 0)
      (set! repair-result
        (cond
          (completed-by-player? "按时修好")
          (repair-joined? "勉强修好")
          (else "未介入")))
      (set! hold-state "待安排")
      (set! hold-open-day (+ world-day 1))
      (sync-globals!)
      ;; 玩家没有参加，就没有理由在别处收到这条现场结算；后续扣船事件仍按城市
      ;; 自己的时间线发生。参加过的人才会收到自己做过的那班活最终怎样了。
      (if repair-joined?
          (begin
            (play-stage!
              (stage-parallel
                (stage-spawn "尼尔" "尼尔" -6 'middle)
                (stage-spawn "弗兰克" "弗兰克" 1 'middle)
                (stage-spawn "贝恩斯" "贝恩斯" 7 'front))
              (stage-say "贝恩斯" "岸口的人可以撤了？" "货船/码头/收工/01/贝恩斯")
              (stage-say "弗兰克" "再留十分钟。最后一班还没上来。" "货船/码头/收工/02/弗兰克")
              (stage-say "贝恩斯" "十分钟。" "货船/码头/收工/03/贝恩斯")
              (stage-move "贝恩斯" 14 0.35)
              (stage-remove "贝恩斯")
              (stage-say "弗兰克" "尼尔……你不是我这儿的人。" "货船/码头/收工/04/弗兰克")
              (stage-say "尼尔" "今天算是。" "货船/码头/收工/05/尼尔")
              (stage-say "弗兰克" "今天算。工钱去岸口领。" "货船/码头/收工/06/弗兰克"))
            (baines 'note-dock-seen!)
            (spotlight! "货船达到离港标准"
              (if (equal? repair-result "按时修好")
                  "泵压住了进水。你把最后一班抢了下来，代理明天来验船。"
                  "你下过舱，但没赶完。弗兰克带人补到天亮，船勉强达到离港标准。")))
          #f))

    (define (join-repair! n)
      (set! repair-joined? #t)
      (meet!)
      (repair-clk 'advance! n)
      (if (= n 0)
          (play-banter! (line "弗兰克" "先放下！那根绳滑了，下面的人怎么办？" "货船/码头/坏结果/01/弗兰克"))
          #f)
      (if (repair-clk 'full?) (settle-repair! #t) #f))

    (define (node-repair-clock)
      (clock-node "钟：货船抢修" (repair-clk 'render-data)))

    (define (node-repair)
      (工作 "参加货船抢修" '高 'violence
        (outcome (lambda ()
            (add-item! "金钱" 18)
            (join-repair! 2)))
        (outcome (lambda ()
            (add-item! "金钱" 10)
            (join-repair! 1)))
        ;; 坏结果沿用普通高风险工作的刻度：不挣钱、不推进，扣 2 点冷静。
        (outcome (lambda () (join-repair! 0) (spend-composure! 2)))
        (string-append "急活加价。还剩 " (number->string (repair-days-left)) " 天")))

    ;; ── 船修好了却不开 ───────────────────────────────
    (define (on-hold-result! result)
      (if (equal? hold-state "待处理") #t (error "弗兰克：没有待结算的扣船交锋"))
      (if (and (list? result) (= (length result) 2))
          #t (error "弗兰克：扣船交锋应回传 (list 'success/'fail 工钱格数)"))
      (let ((verdict (car result))
            (payment (cadr result)))
        (if (member? verdict (list 'success 'fail))
            #t (error "弗兰克：扣船交锋返回了未登记的结算"))
        (if (and (number? payment) (>= payment 0) (<= payment 6))
            #t (error "弗兰克：扣船交锋付款进度非法"))
        (set! hold-state "已结算")
        (set! hold-paid? (equal? verdict 'success))
        (set! hold-payment payment)
        (meet!)
        (sync-globals!)))

    (define (node-hold-entry)
      (encounter-action "去看那条不开的船"
        (lambda ()
          (meet!)
          (set-global! '扣船-抢修结果 repair-result)
          (set-global! '扣船-以货抵债 #f)
          (start-encounter "船修好了却不开" on-hold-result!))))

    ;; ── 分钱：他真正在做的事 ─────────────────────────
    (define (distribution-text)
      (cond
        ((get-global '扣船-以货抵债)
         "木箱拆开以后，药、罐头和能转卖的布匹先分给伤者和欠租的几家。其余按班次记账。")
        ((= hold-payment 6)
         "钱箱里是全数。伤者、欠租的几家和临时顶过班的人都在账册上，然后才按班次点名。")
        ((>= hold-payment 3)
         "钱箱里只有答应数目的一部分。伤者和欠租的排在前面；点到临时顶班的人时，已经没剩多少。")
        ((> hold-payment 0)
         "被警卫清场前抢下的钱摆在桌上，只够先付伤者和最急的几家。其他名字仍留在账册里。")
        (else
         "桌上没有钱。他仍把伤者、欠租的、临时顶过班的名字抄进一本没有封皮的账册。")))

    (define (node-distribution)
      (instant-action "看弗兰克分钱"
        (lambda ()
          (set! distribution-viewed? #t)
          (play-dialogue!
            (line "世界" (distribution-text))
            (line "世界" "到了后半段，一个人站起来说自己不在名单上。弗兰克翻了两页账册，说了一个日期和一个班次。那人坐下来了，没有再说话。")
            (line "世界" "最后一叠没有写进正式账簿。屋里没有人问为什么。")
            (line "尼尔" "谁定这个顺序？")
            (line "弗兰克" "我。"))
          ;; 货船这一节到看他分钱为止：扣船成败都算经历完；没到泊位（缺席）不发。
          (complete-task! "货船"))))

    ;; ── 第二章：警察来带人那天 ───────────────────────
    (define (on-mediation-summoned!)
      (if (equal? mediation "未发生") #t (error "弗兰克：调停只能从未发生开始"))
      (set! mediation "等你")
      (meet!))

    (define (on-mediation-result! result)
      (if (equal? mediation "等你") #t (error "弗兰克：没有等着结算的调停"))
      (if (member? result (list "成功" "失败" "缺席"))
          #t (error "弗兰克：调停结果只能是 成功 / 失败 / 缺席"))
      (set! mediation result)
      (set! at-table? (equal? result "成功"))
      (sync-globals!))

    (define (back-room?) (equal? mediation "成功"))
    (define (in-tavern?) (member? mediation (list "等你" "成功")))

    ;; ── 首演之后：他在看报纸 ─────────────────────────
    ;; 这一拍不给任何东西。它只是让玩家看见第二章从哪里开始长出来。
    (define (node-newspaper)
      (instant-action "他在看报纸"
        (lambda ()
          (set! paper-seen? #t)
          (play-dialogue!
            (line "世界" "他靠在缆桩上看报。头版之后那一整版都在写老街。")
            (line "弗兰克" "莱恩自己做的事，自己背。")
            (line "世界" "他把报纸翻过来，指着中间一段：旧码头治安恶化，城市需要整顿。")
            (line "弗兰克" "但这算什么？")
            (line "尼尔" "他们得有个说法。")
            (line "弗兰克" "他们有的从来不是说法。")
            (line "弗兰克" "他们只是终于找到一个理由，来说这条街该归谁管。")))))

    (define (validate-chapter-end!)
      (validate-state!)
      (if alley-settled? #t (error "第一章结算错误：巷子那晚没有写回弗兰克")))

    ;; ── 码头节点 ─────────────────────────────────────
    ;; 他多数日子里没有事给你做。那时候这张卡点进去是**空的**——
    ;; 而一个空容器读起来不像"今天没事"，像"是不是坏了"。
    ;; 所以没有动作的时候放一条标注：说清他此刻在干什么。
    ;; 玩家看见字，就知道自己没漏掉东西，这里今天确实没有他的事。
    ;;
    ;; 只在空的时候放。有事可做的日子不摆——那就成了每次都要先读一遍的墙纸。
    (define (node-frank-idle)
      (note-node "标注：弗兰克此刻" ""
        (cond
          ((equal? repair-state "进行中")
           "他在跳板边上写班表，一整天没离开这个泊位。现在跟他说话，他会让你等。")
          ((equal? hold-state "待安排")
           "工会房间的门虚掩着。里面在说船上的事，说到你能听见的时候都压低了。")
          ((>= (three-letters 'story-stage) 5)
           "他坐在工会房间那张长桌尽头，面前摊着报纸，没在看。")
          (#t
           "工会房间的门开着。他在对这个月的账，抬头看了你一眼，又低下去。"))))

    (define (node-frank)
      (let ((actions
              (if (and (equal? hold-state "已结算") (not distribution-viewed?))
                  (list (node-distribution))
                  '())))
        (node "弗兰克"
          :anchor "工会房间"
          :subtitle "Frank Delaney；码头工头、老街组织者"
          :children (if (null? actions)
                        (list (node-frank-idle))
                        actions))))

    (define (dock-nodes)
      (append
        ;; 抢修**摆在码头上，不摆在弗兰克底下**。它跟他有关系——班表是他排的——
        ;; 但它是这个空间里正在发生的一件事：一条进水的船停在泊位上，谁都看得见。
        ;; 挂进人物节点等于说"要先找到这个人才知道码头上出了事"，那不是真的。
        ;; 人物节点收的是**只跟他这个人有关**的事：扣船、分钱、他来找你。
        (if (equal? repair-state "进行中")
            (list
              (node "弗兰克" :anchor "码头"
                :resolve (note "弗兰克" "他在跳板边核对班表。"))
              (node-repair-clock) (node-repair))
            '())
        ;; 船边发生的两件事直接留在泊位；弗兰克本人常驻居民区的工会房间。
        (if (equal? hold-state "待处理") (list (node-hold-entry)) '())
        (if (and (not paper-seen?) (>= (three-letters 'story-stage) 5))
            (list (node-newspaper))
            '())))

    (define (residential-nodes)
      ;; 工会房间是居民区东侧回廊的终点，正式会面与分钱都在这里，而不是借码头地点投射。
      ;; 抢修期间他人就在泊位，不能同时把人物卡留在居民区。
      ;; 第二章他叫你去酒馆后屋之后，人就在后屋——一个人不能同时在两处。
      (if (and met? (not (equal? repair-state "进行中")) (not (in-tavern?)))
          (list (node-frank))
          '()))

    ;; 地点问的是同一个问题：你在这儿有什么？内部按地点自己分。
    (define (nodes-at location)
      (cond
        ((equal? location "码头") (dock-nodes))
        ((equal? location "码头居民区") (residential-nodes))
        (else '())))

    ;; 第一章一张卡《货船》：抢修 → 船修好了却不开 → 分钱。进水的船拖到泊位那天立卡，
    ;; 看他分钱那一拍了结；没去泊位（缺席）也了结，只是最后两项就那么留着。
    ;; 他没事给你做的日子不另立人物简介——码头/居民区的标注卡（node-frank-idle）
    ;; 已经说清他此刻在干什么。
    (define (ship-task-done?)
      (or distribution-viewed? (equal? hold-state "缺席")))

    (define (dossier-entry)
      (if (equal? repair-state "未开放")
          '()
          (list (dossier "货船"
                  :kind '人物
                  :status (cond
                            ((ship-task-done?) '了结)
                            ((equal? hold-state "待安排") '等着别人)
                            (#t '进行中))
                  :now (cond
                         ((ship-task-done?) "")
                         ((equal? repair-state "进行中") "他在码头排货船抢修的班表；船主只留三天")
                         ((equal? hold-state "待安排") "船修好了。等代理来验船")
                         ((equal? hold-state "待处理") "他扣下了船上的关键部件，工人正封着跳板；只有今天")
                         (#t "去工会房间，看他怎么分那笔钱"))
                  :where (cond
                           ((ship-task-done?) "")
                           ((member? hold-state (list "已结算")) "码头居民区")
                           (#t "码头"))
                  :clocks (if (equal? repair-state "进行中") (list (repair-clk 'render-data)) '())
                  :steps (list (step "货船抢修" (repair-done?))
                               (step "船修好了却不开" (equal? hold-state "已结算"))
                               (step "看弗兰克分钱" distribution-viewed?))))))

    ;; 只属于本场的调度简写。公共舞台提供 spawn/move/remove；三个人影穿场
    ;; 是这场戏的句法，不是引擎原语。
    ;; 纵队间距 5：立绘在台上约 5–6 个单位宽，之前 1.5 近乎完全重叠；5 加上
    ;; 三层前后错开与高低落差，读成跑在一起的一班人。
    ;; 速度 12 单位/秒：这场节奏着急，跑就是跑，可视区不到两秒穿过；
    ;; 之前近 30 单位/秒才是闪过去。时长按路程折算，保证三人同速；
    ;; 走位本身是匀速直线（见 StageState.CurrentStageX），不会越跑越慢，
    ;; 出框不停留，到位即 remove。
    ;; 拖尾一律落在行进方向的反侧，第二班（从右往左）之前错摆进了可视区。
    (define (cross-shadows prefix from to with-call?)
      (let* ((dir (if (> to from) 1 -1))
             (gap 5)
             (speed 12.0)
             (d1 (abs (- to from)))
             (d2 (+ d1 gap))
             (d3 (+ d1 (* 2 gap)))
             (t1 (/ d1 speed))
             (t2 (/ d2 speed))
             (t3 (/ d3 speed)))
        (list
          (stage-parallel
            (stage-spawn (string-append prefix "一") "码头工人_奔跑" from 'back)
            (stage-spawn (string-append prefix "二") "码头工人_奔跑" (- from (* dir gap)) 'middle)
            (stage-spawn (string-append prefix "三") "码头工人_奔跑" (- from (* dir 2 gap)) 'front))
          (if with-call?
              (stage-parallel
                (stage-move (string-append prefix "一") to t1)
                (stage-move (string-append prefix "二") to t2)
                (stage-move (string-append prefix "三") to t3)
                (stage-sound "码头/急活" 7))
              (stage-parallel
                (stage-move (string-append prefix "一") to t1)
                (stage-move (string-append prefix "二") to t2)
                (stage-move (string-append prefix "三") to t3)))
          (stage-parallel
            (stage-remove (string-append prefix "一"))
            (stage-remove (string-append prefix "二"))
            (stage-remove (string-append prefix "三"))))))

    (define (play-ship-arrival!)
      (apply play-stage!
        (append
          (list
            (stage-spawn "尼尔" "尼尔" -5 'middle)
            (stage-say "尼尔" "今天怎么回事？" "货船/码头/靠岸/01/尼尔"))
          (cross-shadows "第一班" -14 14 #t)
          (cross-shadows "第二班" 14 -14 #f)
          (list
            (stage-pose "尼尔" "侧身退")
            (stage-spawn "弗兰克" "弗兰克" 14 'front)
            (stage-move "弗兰克" 5 0.3)
            (stage-say "弗兰克" "东边缺两个人！钢缆别堆在跳板上！" "货船/码头/靠岸/02/弗兰克")
            (stage-say "弗兰克" "找活的？今天加钱。去那边报名字。" "货船/码头/靠岸/03/弗兰克")
            (stage-move "弗兰克" 14 0.25)
            (stage-remove "弗兰克")
            (stage-move "尼尔" 0 0.3)
            (stage-say "尼尔" "他是谁？" "货船/码头/靠岸/04/尼尔")
            (stage-spawn "码头工人" "码头工人" -14 'middle)
            (stage-move "码头工人" -5 0.3)
            (stage-say "码头工人" "进水了，临时拖来的。弗兰克在管，别挡跳板。" "货船/码头/靠岸/05/码头工人")
            (stage-move "码头工人" 14 0.35)
            (stage-remove "码头工人")))))

    (define (arrival-repair)
      (arrival "旧货船进水"
        (lambda ()
          (set! repair-arrival-viewed? #t)
          (sync-globals!)
          ;; 演出：切到泊位低机位，看那条船从画外压进来、蹭着停住、锚砸下去、吊杆摆向岸边；
          ;; 播完回原机位接对白。船之后一直停在航道上，直到扣船了结（见 ship-berthed?）。
          (play-motion! "码头/货船" "Berthed" "码头-靠岸")
          (play-ship-arrival!))))

    (define (arrivals-at location)
      (if (and (equal? location "码头")
               (equal? repair-state "进行中")
               (= world-day (- repair-deadline-day repair-days))
               (not repair-arrival-viewed?))
          (list (arrival-repair))
          '()))

    (define-turn-rule "货船抢修期限"
      (lambda () (and (equal? repair-state "进行中") (>= world-day repair-deadline-day)))
      (lambda () (settle-repair! #f)))

    (define-turn-start-rule "旧货船改泊"
      (lambda () (and (= world-day 2) (equal? repair-state "未开放")))
      (lambda ()
        (set! repair-state "进行中")
        (set! repair-deadline-day (+ world-day repair-days))
        (sync-globals!)))

    (define-turn-rule "不开的船等待合适白天"
      (lambda () (and (equal? hold-state "待安排")
                      (>= world-day hold-open-day)
                      (not (rest-blocked?))))
      (lambda ()
        (set! hold-state "待处理")
        (set! hold-day world-day)
        (sync-globals!)
        (spotlight! "船修好了却不开"
          "代理拒绝付工钱——承包人跑了。弗兰克扣下一件关键部件，让工人封住跳板；只有今天。")))

    (define-turn-rule "不开的船缺席结算"
      (lambda () (and (equal? hold-state "待处理") (> world-day hold-day)))
      (lambda ()
        (set! hold-state "缺席")
        (sync-globals!)
        (spotlight! "不开的船离港了"
          "你没有去泊位。弗兰克让跳板封了一整天，最后逼到一部分现金；代理带走了船。")))

    (sync-globals!)
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'dossier) (dossier-entry))
          ((equal? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((equal? msg 'approved?) approved?)
          ((equal? msg 'on-mediation-summoned!) (on-mediation-summoned!))
          ((equal? msg 'on-mediation-result!) (on-mediation-result! (cadr args)))
          ((equal? msg 'mediation) mediation)
          ((equal? msg 'back-room?) (back-room?))
          ((equal? msg 'at-table?) at-table?)
          ;; 跳章调试：保留「他认下了你」，同时把货船抢修与扣船窗口收口。
          ;; 这样第一次进码头不会补播第一章的货船入场。
          ((equal? msg 'debug-finish-chapter1!)
           (set! met? #t)
           (set! approved? #t)
           (set! alley-settled? #t)
           (set! repair-state "已结束")
           (set! repair-deadline-day 0)
           (set! repair-joined? #f)
           (set! repair-arrival-viewed? #t)
           (set! repair-result "未介入")
           (set! hold-state "缺席")
           (set! hold-open-day 0)
           (set! hold-day 0)
           (set! hold-paid? #f)
           (set! hold-payment 0)
           (set! distribution-viewed? #f)
           (set! paper-seen? #t)
           (set! mediation "未发生")
           (set! at-table? #f)
           (sync-globals!)
           (validate-state!))
          ;; 大船靠岸调试：重置事件链并让船在当前日抵港，随后正常进入码头看演出。
          ((equal? msg 'debug-reset-ship-repair!)
           (set! repair-state "未开放")
           (set! repair-deadline-day 0)
           (set! repair-joined? #f)
           (set! repair-arrival-viewed? #f)
           (set! repair-result "未结算")
           (repair-clk 'set! 0)
           (set! hold-state "未发生")
           (set! hold-open-day 0)
           (set! hold-day 0)
           (set! hold-paid? #f)
           (set! hold-payment 0)
           (set! distribution-viewed? #f)
           (set! repair-state "进行中")
           (set! repair-deadline-day (+ world-day repair-days))
           (sync-globals!)
           (validate-state!))
          ((equal? msg 'met?) met?)
          ((equal? msg 'repair-state) repair-state)
          ((equal? msg 'hold-state) hold-state)
          ((equal? msg 'meet!) (meet!))
          ((equal? msg 'on-alley-result!) (on-alley-result! (cadr args)))
          ((equal? msg 'validate!) (validate-state!))
          ((equal? msg 'validate-chapter-end!) (validate-chapter-end!))
          ((equal? msg 'sync-globals!) (sync-globals!))
          ((equal? msg 'save)
           (list
             (list "approved?" approved?)
             (list "mediation" mediation)
             (list "at-table?" at-table?)
             (list "alley-settled?" alley-settled?)
             (list "met?" met?)
             (list "repair-state" repair-state)
             (list "repair-deadline-day" repair-deadline-day)
             (list "repair-joined?" repair-joined?)
             (list "repair-arrival-viewed?" repair-arrival-viewed?)
             (list "repair-result" repair-result)
             (list "repair-progress" (repair-clk 'save))
             (list "hold-state" hold-state)
             (list "hold-open-day" hold-open-day)
             (list "hold-day" hold-day)
             (list "hold-paid?" hold-paid?)
             (list "hold-payment" hold-payment)
             (list "distribution-viewed?" distribution-viewed?)
             (list "paper-seen?" paper-seen?)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! approved? (required-field data "approved?"))
             ;; 第二章新加的字段用宽容读法：第一章存的档里没有它，缺了就是初始值。
             (set! mediation (assoc-get data "mediation" "未发生"))
             (set! at-table? (assoc-get data "at-table?" #f))
             (set! alley-settled? (required-field data "alley-settled?"))
             (set! met? (required-field data "met?"))
             (set! repair-state (required-field data "repair-state"))
             (set! repair-deadline-day (required-field data "repair-deadline-day"))
             (set! repair-joined? (required-field data "repair-joined?"))
             (set! repair-arrival-viewed? (assoc-get data "repair-arrival-viewed?" #f))
             (set! repair-result (required-field data "repair-result"))
             (repair-clk 'load! (required-field data "repair-progress"))
             (set! hold-state (required-field data "hold-state"))
             (set! hold-open-day (required-field data "hold-open-day"))
             (set! hold-day (required-field data "hold-day"))
             (set! hold-paid? (required-field data "hold-paid?"))
             (set! hold-payment (required-field data "hold-payment"))
             (set! distribution-viewed? (required-field data "distribution-viewed?"))
             (set! paper-seen? (required-field data "paper-seen?"))
             (validate-state!)
             (sync-globals!)))
          (else #f))))))
