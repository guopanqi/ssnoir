;; scenes/world/夜莺.scm - 夜莺主线故事模块
;; 拥有 story-stage / condition-level / scene-flags 全部故事状态,通过消息接口与世界协调。

(define nightingale
  (let ()
    ;; ── 常量 ────────────────────────────────────────
    (define prepayment 30)
    (define ransom 150)              ; 节拍二文案里的名义数，节拍三涨成封口价
    (define first-installment 100)
    ;; 当掉戒指的价钱。它不当场折现，只是一个能一直揣在兜里的物件——
    ;; 值钱到足以让"留着"真的是个选择，而不是顺手就当了的小钱。
    (define ring-value 50)
    (define beat1-location-target 4)
    (define stranger-understanding-target 2)
    (define hush-price 400)             ; 封口总价，也是状态卡唯一显示的账目总额
    (define nightingale-daily-earning 40)
    (define berth-price-insurance 80)   ; 保险公司紧急转移条款的正规舱位
    (define truth-target 8)             ; 暗账查访格数(码头/警察局/货运三源分段推进)
    (define dock-truth-target 3)        ; 老人：分三层递进
    (define police-truth-target 3)      ; 案卷抄件：分三层递进
    (define freight-truth-target 2)     ; 船期与货栈：分两层递进
    (define case-progress-interval 3)   ; 证据交割后,每隔几天播报一次案情进展
    (define sam-pressure-hush-surcharge 100)  ; 没查真相时,萨姆仍在查案,封口钱额外加价
    (define sam-pressure-berth-surcharge 40)  ; 没查真相时,舱位的风险费加价
    (define letter-delay 2)             ; 结局 D 的信,几天后送到
    (define letter-money 40)            ; 信里附的钱

    ;; ── 状态 ────────────────────────────────────────
    (define story-stage 0)
    ;; 0=未开场 1=受托查探 2=账转你头(二层已揭) 3=知道真相(三层已揭)
    ;; 90=真名(A) 91=她被带走(B)
    ;; 93=远方的信(D1) 94=信和疤(D2) 95=案卷(F) 96=你没有去(B') 97=随案移交(G)
    (define condition-level 0)
    ;; 0=稳定 1=不安 2=受伤；3 仅可能来自旧存档，按受伤处理
    (define listened-today? #f)   ; 今天是否已听过歌
    (define trust 0)              ; 她对你的信任
    (define trust-threshold 3)
    (define tavern-inquiry-progress 0)
    (define dock-inquiry-progress 0)
    ;; 酒馆与码头各自拥有一条 0..4 的查访 Clock；每完成一条，陌生人了解 +1。
    (define beat1-early? #f)
    (define protection "无")
    (define installment-reserved? #f) ; 已交给夜莺保管，等收账人上门时再决定是否交出
    (define installment-paid? #f)
    (define truth-progress 0)  ; 暗账查访进度 0..truth-target(三源分段之和)
    (define dock-truth-progress 0)    ; 老人处已问到第几层
    (define police-truth-progress 0)  ; 案卷抄件已问到第几层
    (define freight-truth-progress 0) ; 船期货栈已问到第几层
    (define stance "无")   ; 无 / 体谅 / 责问——「撒谎的人」里选的姿态，只定基调不锁路线
    (define berth? #f)         ; 路线三①：是否已弄到舱位
    (define farewell? #f)      ; 路线三②：是否已送她上船
    (define hush-paid? #f)     ; 路线一：是否已付封口钱
    (define case-filed? #f)    ; 路线四：是否已立案送警
    (define surrendered? #f)   ; 将完整真相交给萨姆：她随案移交
    (define case-filed-day 0)      ; 路线四落定当天的 world-day（进展播报计时用）
    (define surrendered-day 0)     ; 路线五落定当天的 world-day（进展播报计时用）
    (define case-progress-shown 0)    ; 路线四已播报的案情进展条数
    (define surrender-progress-shown 0) ; 路线五已播报的案情进展条数
    (define ending-day 0)      ; 进入终值 stage 时的 world-day（结局信用）
    (define nightingale-earnings 0) ; 已放入封口钱的金额（首期 + 夜莺每日凑款）
    (define scene-flags '())

    ;; ── 内部工具 ────────────────────────────────────
    (define (member? x lst)
      (if (null? lst)
          #f
          (if (equal? x (car lst))
              #t
              (member? x (cdr lst)))))

    (define (flag-id flag)
      (cond
        ((or (equal? flag '已打听) (equal? flag "已打听")) "已打听")
        ((or (equal? flag '二层已揭) (equal? flag "二层已揭")) "二层已揭")
        ((or (equal? flag '三层已揭) (equal? flag "三层已揭")) "三层已揭")
        ((or (equal? flag '唱歌) (equal? flag "唱歌")) "唱歌")
        ;; 旧戒指 = 幕间已演过（收或不收都算）；收下戒指 = 戒指真的到过你手里。
        ;; 两者必须分开：结局 B 只在她当初被你推回去时，才把戒指留下来。
        ((or (equal? flag '旧戒指) (equal? flag "旧戒指")) "旧戒指")
        ((or (equal? flag '收下戒指) (equal? flag "收下戒指")) "收下戒指")
        ((or (equal? flag '撒谎的人) (equal? flag "撒谎的人")) "撒谎的人")
        ((or (equal? flag '她自己说了) (equal? flag "她自己说了")) "她自己说了")
        ((or (equal? flag '萨姆需要你) (equal? flag "萨姆需要你")) "萨姆需要你")
        ((or (equal? flag '案卷备妥) (equal? flag "案卷备妥")) "案卷备妥")
        ((or (equal? flag '结局信) (equal? flag "结局信")) "结局信")
        ((or (equal? flag '夜莺的消息) (equal? flag "夜莺的消息")) "夜莺的消息")
        ((or (equal? flag '萨姆登门) (equal? flag "萨姆登门")) "萨姆登门")
        (else (error "夜莺 flag 未登记"))))

    (define (has-flag? flag) (member? (flag-id flag) scene-flags))
    (define (set-flag! flag)
      (let ((id (flag-id flag)))
        (if (not (member? id scene-flags))
            (set! scene-flags (cons id scene-flags))
            #f)))

    (define (normalize-flags flags)
      (if (null? flags)
          '()
          (cons (flag-id (car flags)) (normalize-flags (cdr flags)))))

    ;; ── 节拍三派生谓词 ──────────────────────────────
    ;; 暗账满格且看过「撒谎的人」才算真正查明真相。
    (define (truth-known?) (and (>= truth-progress truth-target) (has-flag? '撒谎的人)))
    (define (trust-met?) (>= trust trust-threshold))
    (define (truth-lead?) (has-flag? '夜莺的消息))
    (define (truth-pending?) (and (truth-lead?) (< truth-progress truth-target)))
    ;; 节拍三开放：stage=3 且三层已揭（未看必看戏前，节拍三内容一律不出现）。
    (define (stage3-open?) (and (= story-stage 3) (has-flag? '三层已揭)))
    (define (singer-present?)
      (or (and (>= story-stage 1) (<= story-stage 3) (< condition-level 2))
          (= story-stage 90)
          (= story-stage 95)))
    ;; 路线一/三/四互斥：任一落定，其余路线的准备节点全部消失。
    (define (route-settled?) (or hush-paid? farewell? case-filed? surrendered?))
    ;; 立案交割是否已经就绪：需要老板那一半的证据，并且自己拼好了案卷。
    (define (case-ready?) (and (equal? (sam 'evidence) "老板")
                               (has-flag? '案卷备妥)))
    (define (case-evidence-submitted?) (equal? (sam 'evidence) "老板"))
    ;; 萨姆登门(必看)：三层已揭后的第一个清晨,他主动找上门,不必玩家自己去酒馆发现他。
    (define (sam-intro?) (has-flag? '萨姆登门))
    ;; 没查真相时,萨姆仍会按自己的节奏往下查——"干净"的路线要为这份持续的风险多付一点。
    (define (sam-pressure?) (and (stage3-open?) (sam-intro?) (not (truth-known?))))
    (define (hush-total) (if (sam-pressure?) (+ hush-price sam-pressure-hush-surcharge) hush-price))
    (define (hush-due) (max 0 (- (hush-total) nightingale-earnings)))
    (define (berth-price-effective) (if (sam-pressure?) (+ berth-price-insurance sam-pressure-berth-surcharge) berth-price-insurance))

    (define (stranger-understanding)
      (+ (if (>= tavern-inquiry-progress beat1-location-target) 1 0)
         (if (>= dock-inquiry-progress beat1-location-target) 1 0)))

    (define (sync-globals!)
      (set-global! '夜莺阶段 story-stage)
      (set-global! '夜莺状态等级 condition-level)
      (set-global! '夜莺状态 (condition-label))
      (set-global! '夜莺查访进度 (stranger-understanding))
      (set-global! '夜莺查访目标 stranger-understanding-target)
      (set-global! '夜莺主动上门 beat1-early?)
      (set-global! '夜莺保护方案 protection)
      (set-global! '夜莺二层已揭 (has-flag? '二层已揭))
      (set-global! '夜莺已打听 (has-flag? '已打听))
      (set-global! '夜莺真相 (truth-known?))
      (set-global! '夜莺已送走 farewell?)
      (set-global! '夜莺已付封口 hush-paid?)
      (set-global! '夜莺已立案 case-filed?)
      (set-global! '夜莺已移交 surrendered?)
      (set-global! '夜莺信任达标 (trust-met?))
      ;; 「了断」交锋要按这个基调分台词版本。只镜像这一个只读值,不搬整套状态。
      (set-global! '夜莺姿态 stance))

    (define (advance-stage! new-stage)
      (set! story-stage new-stage)
      ;; 结局 B：她自己走出去换你一条命，把戒指留了下来。当初你要是收下了，
      ;; 它早在你兜里（哪怕已经当掉），她没有第二枚可留。
      (if (and (= new-stage 91) (trust-met?) (not (has-flag? '收下戒指)))
          (begin
            (set-flag! '收下戒指)
            (add-item! "旧戒指" 1))
          #f)
      (sync-globals!))

    (define (gain-trust!)
      (set! trust (+ trust 1))
      (sync-globals!))

    (define (worsen-condition! n)
      (set! condition-level (min 2 (+ condition-level n)))
      (sync-globals!))

    (define (condition-label)
      (cond
        ((= condition-level 0) "稳定")
        ((= condition-level 1) "不安")
        ((= condition-level 2) "受伤")
        ((= condition-level 3) "受伤") ; 既有存档中的旧值按受伤呈现
        (else (error "夜莺状态等级非法"))))

    (define (stage2-open?)
      (and (= story-stage 2)
           (has-flag? '二层已揭)
           (equal? protection "无")))

    (define (set-protection! kind)
      (if (not (stage2-open?))
          (error "夜莺保护方案：当前不能落实保护")
          #t)
      (set! protection kind)
      (if (equal? kind "首期")
          (begin
            (set! installment-reserved? #f)
            (set! installment-paid? #t)
            (set! nightingale-earnings first-installment))
          #f)
      (if (equal? kind "首期") (set-global! '夜莺已付首期 #t) #f)
      (gain-trust!)
      (sync-globals!))

    ;; 首期直接记入唯一的封口钱进度，不另设折后总价。
    (define (ransom-hint)
      (if installment-paid?
          (string-append "首期已经记入封口钱,还差 " (number->string (hush-due)) "。")
          ""))

    (define (node-reserve-installment)
      (node "托夜莺保管首期"
        :subtitle (string-append "交出 " (number->string first-installment)
                                 " 金。收账人第 10 天上门时，你可以让她交钱，也可以取回这笔钱后交锋")
        :requires (list (req-item "金钱" first-installment))
        :resolve (instant
          (outcome "首期备好了"
            (lambda ()
              (set! installment-reserved? #t)
              (sync-globals!))))))

    (define (node-installment-reserved)
      (node "首期已备妥"
        :subtitle (string-append (number->string first-installment)
                                 " 金由夜莺保管。收账人第 10 天上门时，可交钱让他离开，或取回这笔钱后交锋")
        :tags (list "安全" "等待第 10 天")
        :disabled #t))

    (define (advance-beat1-location! location n)
      (let ((before (stranger-understanding)))
        (cond
          ((equal? location "酒馆")
           (let ((location-before tavern-inquiry-progress))
             (set! tavern-inquiry-progress
                   (min beat1-location-target (+ tavern-inquiry-progress n)))
             (record-clock-progress! "酒客的说法" (- tavern-inquiry-progress location-before))))
          ((equal? location "码头")
           (let ((location-before dock-inquiry-progress))
             (set! dock-inquiry-progress
                   (min beat1-location-target (+ dock-inquiry-progress n)))
             (record-clock-progress! "码头上的来路" (- dock-inquiry-progress location-before))))
          (else (error "夜莺节拍一：未知查访地点")))
        (set-flag! '已打听)
        (sync-globals!)
        (if (> (stranger-understanding) before)
            (if (>= (stranger-understanding) stranger-understanding-target)
                (spotlight! "藏身处揭晓" "酒客的描述和码头的来路对上了。陌生人的藏身处已经标在城市地图上。")
                (spotlight! "一条线索坐实" "这一处的说法已经能够互相印证。再查清另一处，就能找出陌生人的藏身处。"))
            #f)))

    (define (advance-truth! label detail)
      (let ((before truth-progress))
        (set! truth-progress (min truth-target (+ truth-progress 1)))
        (record-clock-progress! "那晚码头上发生了什么" (- truth-progress before)))
      (sync-globals!)
      (sync-blockers!)
      (if (= truth-progress 1)
          (play-dialogue!
            (line "夜莺" "你在查那晚。")
            (line "尼尔" "船期和案卷都对不上。")
            (line "夜莺" "旧账翻出来,不会让谁干净一点。")
            (line "夜莺" "你不必替我把那一晚也算清。")
            (line "尼尔" "可我得知道,第十七天到底该替谁挡。"))
          #f)
      (spotlight! label detail))

    ;; 三个来源各自的递进文案：按到访次序揭示,不看骰点好坏重复同一句。
    (define (dock-truth-steps)
      (list
        (list "争执声" "老人说,那晚栈桥上有争执声,压得很低。")
        (list "栈桥上的女人" "他记得那晚栈桥上,有个女人。")
        (list "水声" "老人压低声音说,那晚水声响过一次,像是有人跌下了栈桥。")))

    (define (police-truth-steps)
      (list
        (list "压着没立" "案卷抄件写着:案子在邻城压着,一直没正式立。")
        (list "腕上的抓痕" "验尸那栏写着一句:死者手腕上,有抓痕。")
        (list "不予采纳" "抄件末尾夹着一张字条:报案人是个女人,笔录只批了一句'情绪不稳,证词不予采纳'。")))

    (define (freight-truth-steps)
      (list
        (list "对不上的船期" "货栈记录里,那晚根本没有她说的那班船。")
        (list "没登记的船" "登记员想起来:那晚倒是有一班没登记在册的船,走的是老板名下的货运线。")))

    (define (advance-source-truth! source)
      (cond
        ((equal? source "码头")
         (let ((entry (list-ref (dock-truth-steps) dock-truth-progress)))
           (set! dock-truth-progress (+ dock-truth-progress 1))
           (advance-truth! (car entry) (cadr entry))))
        ((equal? source "警察局")
         (let ((entry (list-ref (police-truth-steps) police-truth-progress)))
           (set! police-truth-progress (+ police-truth-progress 1))
           (advance-truth! (car entry) (cadr entry))))
        ((equal? source "货运公司")
         (let ((entry (list-ref (freight-truth-steps) freight-truth-progress)))
           (set! freight-truth-progress (+ freight-truth-progress 1))
           (advance-truth! (car entry) (cadr entry))))
        (else (error "夜莺节拍三：未知查证来源"))))

    (define (beat1-ready?)
      (and (= story-stage 1)
           (>= (stranger-understanding) stranger-understanding-target)
           (not public-event-pending?)))

    (define (hideout-visible?) (beat1-ready?))

    ;; ── 阻塞同步 ────────────────────────────────────
    (define (sync-blockers!)
      (cond
        ((= story-stage 0)
         (rest-block! "夜莺/开场敲门" "有人敲门"))
        ((and (= story-stage 2) (not (has-flag? '二层已揭)))
         (rest-block! "夜莺/第二层揭开" "她等你回来"))
        ((and (= story-stage 3) (not (has-flag? '三层已揭)))
         (rest-block! "夜莺/第三层揭开" "她受伤了"))
        ((and (stage3-open?) (not (sam-intro?)))
         (rest-block! "夜莺/萨姆登门" "萨姆堵在门口"))
        ((and (stage3-open?) (truth-lead?) (not (truth-pending?)) (not (has-flag? '撒谎的人)))
         (rest-block! "夜莺/撒谎的人" "今晚问清楚"))
        ((and (stage3-open?) (trust-met?) (not (truth-known?)) (not (route-settled?))
              (>= world-day 16) (not public-event-pending?) (not (has-flag? '她自己说了)))
         (rest-block! "夜莺/她自己说了" "她有话要说"))
        (else
         (begin
           (rest-release! "夜莺/开场敲门")
           (rest-release! "夜莺/第二层揭开")
           (rest-release! "夜莺/第三层揭开")
           (rest-release! "夜莺/萨姆登门")
           (rest-release! "夜莺/撒谎的人")
           (rest-release! "夜莺/她自己说了")))))

    ;; ── 开场节点 ────────────────────────────────────
    (define (node-answer-door)
      (instant-action "雨夜来客"
        (lambda ()
          (play-remote-dialogue!
            (line "夜莺" "门外雨下得像不要钱。钱在桌上,预付的——我要你查清楚,是谁在盯我的梢。")
            (line "尼尔" "你是谁?")
            (line "夜莺" "他们都叫我夜莺。那只死鸟和字条,也是他们送的。先开门,行吗?"))
          (add-item! "金钱" prepayment)
          (advance-stage! 1)
          (rest-release! "夜莺/开场敲门")
          (spotlight! "雨夜来客" "她收起一把红伞，靠在门边，伞尖的水在地板上积成一小片。你把钱收进兜里。这不是委托,是一根钓钩,而你已经张了嘴。第三天以前,去酒馆和码头查清那个陌生人藏在哪里。"))))

    ;; ── 第二层揭开(交锋一后,必看) ──────────────────
    (define (node-reveal-layer-2)
      (instant-action "听她解释"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "我不是逃债。我签了十年卖身约,跑了七年。")
            (line "夜莺" "他们找上门,要么带我回去,要么拿一笔赎身钱换我。")
            (line "尼尔" "多少?")
            (line "夜莺" "一百五。这个数,搁你身上也一样压得慌。"))
          (set-flag! '二层已揭)
          (sync-globals!)
          (rest-release! "夜莺/第二层揭开")
          (spotlight! "一笔人的价钱" "一百五。一个人,明码标价。这数字现在既记在你的账本上,也记在她的命上。"))))

    ;; ── 第三层揭开(交锋二后,必看) ──────────────────
    (define (node-reveal-layer-3)
      (instant-action "听她说话"
        (lambda ()
          (if (>= condition-level 2)
              (play-dialogue!
                (line "夜莺" "那晚在码头……他追我,脚下一滑,栽进了水里。")
                (line "夜莺" "水把他带走了。老板就认定是我推的。")
                (line "尼尔" "所以你才躲到我这里。")
                (line "夜莺" "所以我再不敢跑。我一跑,那顶帽子就真扣实了。")
                (line "尼尔" "那就跟他们碰一场。")
                (line "夜莺" "他们不是一个人来的。你要碰,也别一个人碰——码头、警察局,能拉一个是一个。")
                (line "夜莺" "可我还是想自己凑一份钱。不图别的,不想让你把命也押在我这笔账上。"))
              (play-dialogue!
                (line "夜莺" "有件事,我一直没跟你说。")
                (line "夜莺" "逃走那晚,老板的心腹追到码头,自己踩空落了水。")
                (line "夜莺" "老板咬定是我推的。这就不是一笔账了,是一条命。")
                (line "尼尔" "那你更回不得。")
                (line "夜莺" "所以我才不该把这笔账全压在你身上。")
                (line "尼尔" "真要碰上,我们就跟他们碰一场。")
                (line "夜莺" "碰得过更好。碰不过,谁都没有回头路。钱我也会去凑——不是非它不可,是不想让你一个人扛。")))
          (set-flag! '三层已揭)
          (rest-release! "夜莺/第三层揭开")
          (spotlight! "水里的真相" "她推没推,不重要。老板要的是一个偿命的人。而这个人,现在也可以是你。"))))

    ;; ── 萨姆登门(三层已揭后,必看)────────────────────
    ;; 他主动找上门,不必玩家先去酒馆里发现这条线；听不听是你的事,他不会等你。
    (define (node-sam-visit)
      (instant-action "开门，是个陌生人"
        (lambda ()
          (play-dialogue!
            (line "萨姆" "萨缪尔·罗克,邻城的警探——现在不是了。夜莺那晚的事,不像她说的那么简单。")
            (line "萨姆" "那具尸首手腕上有抓痕,案子却被压成了醉酒失足。我把警徽留在了桌上。")
            (if (>= (sam 'sighting-count) 1)
                (line "萨姆" (sam 'sighting-recap))
                (line "萨姆" "这件事我查了七年,没查到尽头。"))
            (if (sam 'favor-done?)
                (line "萨姆" "登记房那句话,谢了。慢十分钟——她说她赶上了末班船,可那晚根本没有船能赶。")
                (line "萨姆" "案卷和船期总有一处会对不上。我只差一双肯翻旧账的手。"))
            (line "尼尔" "你想让我信一个丢了差事的人?")
            (line "萨姆" "不用信我,信你自己去查出来的。")
            (line "萨姆" "我在老街酒馆等你——你想听,随时来找我;你不想,我也会一个人查下去。"))
          (set-flag! '萨姆登门)
          (set-flag! '夜莺的消息)
          (sam 'debut!)
          (sync-globals!)
          (rest-release! "夜莺/萨姆登门")
          (spotlight! "不请自来的人" "他丢下一句话就走了。查不查是你的事,但他不会因为你不查就停下。"))))

    ;; ── 撒谎的人(暗账满格,必看) ─────────────────────
    ;; 两个对话选择不锁路线，只定姿态；两者殊途同归，都完成同一次揭开。
    (define (finish-lie-reveal!)
      (set-flag! '撒谎的人)
      (sync-globals!)
      (rest-release! "夜莺/撒谎的人")
      (spotlight! "撒谎的人" "她推没推,不重要。眼下要紧的是,你打算拿这件事做什么。"))

    (define (node-reveal-lie)
      (container "撒谎的人"
        (list
          (instant-action "你早该告诉我"
            (lambda ()
              (play-dialogue!
                (line "尼尔" "船期对不上。那晚你根本没在等船。")
                (line "夜莺" "……他抓住我的手腕,往船上拖。我甩开了。他自己没站稳。")
                (line "尼尔" "你早该告诉我。")
                (line "夜莺" "告诉你,你就不管了?"))
              (set! stance "责问")
              (finish-lie-reveal!)))
          (instant-action "换我也一样推"
            (lambda ()
              (play-dialogue!
                (line "尼尔" "船期对不上。那晚你根本没在等船。")
                (line "夜莺" "……他抓住我的手腕,往船上拖。我甩开了。他自己没站稳。")
                (line "尼尔" "换我也一样推。")
                (line "夜莺" "……谢谢你这么说。哪怕是骗我。"))
              (set! stance "体谅")
              (finish-lie-reveal!))))))

    (define (node-self-confession)
      (instant-action "听她说完"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "明天他就到了。有件事,我想自己告诉你,不想让你从别人嘴里听来。")
            (line "夜莺" "那晚在栈桥上,他抓住我的手腕,往船上拖。是我甩开的。他自己没站稳。")
            (line "夜莺" "我对你撒了谎。对所有人都撒了。跑了七年,这句话我头一回自己说出口。")
            (line "尼尔" "为什么现在说?")
            (line "夜莺" "因为明天不管怎么收场,我不想你是替一个你不认识的人挡的。")
            (line "夜莺" "现在你知道了。要把我交出去,也来得及。"))
          (set! truth-progress truth-target)
          (set-flag! '撒谎的人)
          (set-flag! '她自己说了)
          (set! stance "自白")
          (sync-globals!)
          (rest-release! "夜莺/她自己说了")
          (spotlight! "她自己说了"
            "没有人拆穿她。她把那晚的真相,连同把她交出去的权力,一起放进了你手里。"))))

    ;; ── 状态卡 ──────────────────────────────────────
    (define (situation-text)
      (cond
        ((= story-stage 0) "还没有发生什么。")
        ((= story-stage 1) "一个叫夜莺的歌女,在雨夜敲开了你的门。她撂下一笔预付钱,要你查清是谁在盯她的梢。")
        ((= story-stage 2) "收账人把账算到了你头上。夜莺认得他们:她从邻城歌厅逃出来,对方要一笔赎身钱。备好首期交给夜莺保管；第十天收账人上门时，再决定交钱还是动手。")
        ((= story-stage 3)
         (string-append
           "那晚,码头的水吞了一个人。老板一口咬定是夜莺推的——这不只是一笔账,是一条命。"
           (ransom-hint)))
        ((= story-stage 90) "她留在了这座城。真名,只在你耳边轻轻说过一次。")
        ((= story-stage 91)
         (if (trust-met?)
             "她跟他们走了——是她自己开的口,拿自己抵了你一条命。酒馆的台子上,再没人开嗓。"
             "他们把她带走了。你从头到尾没能站起来。酒馆的台子上,再没人开嗓。"))
        ((and (= story-stage 93) (not (has-flag? '结局信)))
         (if (truth-known?)
             "她安全了。船开的那晚,酒馆的灯灭了,再没人开嗓——她曾经只顾自己逃命,这一回,却是不想再连累你。"
             "她安全了。船开的那晚,酒馆的灯灭了,再没人开嗓。这几天,你在等一个消息。"))
        ((= story-stage 93)
         (if (truth-known?)
             "她安全了,你也站住了。那封没有署名的信,你已经拆开——这一次,她没有再瞒着你。"
             "她安全了,你也站住了。那封没有署名的信,你已经拆开——她始终在出力。"))
        ((and (= story-stage 94) (not (has-flag? '结局信)))
         (if (truth-known?)
             "她安全了,可这一架是你自己扛下来的。伤还没好利索——她说过不想让你把命押在她这笔账上,这句话,她没能兑现。"
             "她安全了,可这一架是你自己扛下来的。伤还没好利索。"))
        ((= story-stage 94)
         (if (truth-known?)
             "她安全了。信照样来了——这一次,她没有瞒着你为这件事付出了什么。"
             "她安全了。信照样来了——她不知道你为这件事付出了什么。"))
        ((= story-stage 95) "第 17 天来的不是老板,是巡警。她留下了,案子只是压着——你们都清楚这一点。")
        ((= story-stage 96) "你在街角坐到很晚。第二天,酒馆的门关着,没人跟你说发生了什么——你也没问。")
        ((= story-stage 97) "她跟着巡警走了。萨姆带着完整案卷，也带着亨利曾经活过的证词。")
        (else "……")))

    (define (nightingale-subtitle)
      (cond
        ((= story-stage 1) "受托查访")
        ((= story-stage 2)
         (string-append (condition-label) "；收账人第 10 天上门"))
        ((= story-stage 3) (string-append (condition-label) "；等一个了断"))
        ((= story-stage 90) "留在城里")
        ((= story-stage 91) "被带走")
        ((= story-stage 93) "已平安离开")
        ((= story-stage 94) "带伤扛下了")
        ((= story-stage 95) "案子压着")
        ((= story-stage 96) "没有回头")
        ((= story-stage 97) "随案移交")
        (else "雨夜来客")))

    (define (beat1-clock)
      (if (= story-stage 1)
          (list (list 'clock "对陌生人的了解" (stranger-understanding) stranger-understanding-target 'segments
                      "酒馆与码头的查访 Clock 每完成一条，增加 1 格；满格后揭晓藏身处。"))
          '()))


    (define (condition-clock)
      (if (and (>= story-stage 2) (> condition-level 0))
          (list (list 'clock "夜莺的处境" condition-level 2 'segments
                      (if (= condition-level 1)
                          "第一节没能抢在对方上门前解决。夜莺已经不安；第 17 天的带走时钟会少 1 格。"
                          "第二节失守后她受了伤；加上先前的不安，第 17 天的带走时钟会少 2 格。")))
          '()))

    (define (trust-clock)
      (if (and (>= story-stage 1) (<= story-stage 3))
          (list (list 'clock "夜莺对你的信任" trust trust-threshold 'segments
                      "主动上门、落实保护、护住她、收下她的戒指，都会让她更信你。"))
          '()))

    (define (truth-clock)
      (if (and (stage3-open?) (truth-pending?))
          (list (list 'clock "那晚码头上发生了什么" truth-progress truth-target 'segments
                      "先在码头、警察局或货运公司混出门路,才能找到人、调到卷宗或翻到旧记录。查不查,都能走到第 17 天。"))
          '()))

    (define (nightingale-earning-clock)
      (if (and (stage3-open?) (not (route-settled?)))
          (list (list 'clock "封口钱" nightingale-earnings (hush-total) 'countdown
                      (string-append "总额 " (number->string (hush-total)) " 金"
                                     (if (sam-pressure?)
                                         (string-append "(含萨姆仍在查案的额外 "
                                           (number->string sam-pressure-hush-surcharge) " 金)")
                                         "")
                                     "。夜莺每天会放入 "
                                     (number->string nightingale-daily-earning) " 金；现在交割，你还需 "
                                     (number->string (hush-due)) " 金。")))
          '()))

    (define (case-progress-clock)
      (cond
        ((not (stage3-open?)) '())
        (case-filed?
         (list (list 'clock "阿瑟的案情捎话" case-progress-shown (length (case-progress-texts)) 'segments
                     "每隔 3 天有新消息；满格后,就等第 17 天巡警上门。")))
        (surrendered?
         (list (list 'clock "萨姆的案情捎话" surrender-progress-shown (length (surrender-progress-texts)) 'segments
                     "每隔 3 天有新消息；满格后,就等第 17 天启程。")))
        (else '())))

    (define (node-ask-about-hush-money)
      (observe-action "问她封口钱"
        (string-append "她把今天挣到的钱放进信封。\"我能做的都在做。\"现在已有 "
                       (number->string nightingale-earnings) " 金；若现在交割，你还差 "
                       (number->string (hush-due)) " 金。")))

    (define (case-route-hint)
      (cond
        ((not (truth-known?)) "需要先查明真相")
        ((not (case-evidence-submitted?)) "需要把老板那一半交给萨姆")
        ((not (arthur 'can-escalate?)) "需要阿瑟把案子送进程序")
        ((not (case-ready?)) "需要拼好案卷")
        ((= (item-count "办案通行证") 0) "需要办案通行证")
        (else "材料备妥，去警察局立案")))

    (define (route-overview-text)
      (string-append
        "付钱封口 —— 还差 " (number->string (hush-due)) " 金\n"
        "交锋了断 —— 随时可走;城里攒下的一切都是场内的本钱\n"
        "送她离开 —— "
        (cond
          (berth? (if (<= world-day 16) "已有舱位,第16天前送船" "船已经开走"))
          ((walter 'can-arrange-berth?) "可请沃尔特办理舱位")
          (else "需要沃尔特读到她的保单"))
        "\n立案送警 —— " (case-route-hint)
        "\n完整移交 —— "
        (if (truth-known?)
            (if (equal? (sam 'evidence) "完整") "完整证据已经交给萨姆" "需要交给萨姆完整证据")
            "需要先查明真相")
        "\n坐视不管 —— "
        (if (truth-known?) "可以背过身去" "需要先查明真相——没查真相的人,没有背过身的台阶")))

    (define (node-route-overview)
      (observe-action "六条路的进展" (route-overview-text)))

    ;; 夜莺当前情境下能做的关键动作。开场动作只供客户端在新游戏时自动执行。
    (define (situation-nodes)
      (append
        (if (and (= story-stage 2) (not (has-flag? '二层已揭)))
            (list (node-reveal-layer-2))
            '())
        (if (and (stage2-open?) installment-reserved?)
            (list (node-installment-reserved))
            '())
        (if (and (stage2-open?) (not installment-reserved?))
            (list (node-reserve-installment))
            '())
        (if (and (= story-stage 3) (not (has-flag? '三层已揭)))
            (list (node-reveal-layer-3))
            '())
        (if (and (stage3-open?) (truth-lead?) (not (truth-pending?)) (not (has-flag? '撒谎的人)))
            (list (node-reveal-lie))
            '())
        (if (and (stage3-open?) (not (truth-known?)) (not (route-settled?)) (trust-met?)
                 (>= world-day 16) (not public-event-pending?))
            (list (node-self-confession))
            '())
        (route3-nodes)
        (letter-nodes)))

    ;; 夜莺是一个「可进入的容器」：进去才是与她的各种互动。
    ;; 处境正文做成一张 observe 子卡（她的处境）放在最上面——不能给容器本身加
    ;; :resolve，否则它会退化成动作、children 全部失效（NodeConverter 会在加载期拦下）。
    (define (render-data)
      (if (>= story-stage 1)
          (list (node "夜莺"
                 :subtitle (nightingale-subtitle)
                 :children (append
                             (list (observe-action "她的处境" (situation-text)))
                             (situation-nodes)
                             (route1-nodes)
                             (if (and (stage3-open?) (not (route-settled?)))
                                 (list (node-ask-about-hush-money))
                                 '())
                             (if (and (stage3-open?) (not (route-settled?)))
                                 (list (node-route-overview))
                                 '()))
                 :clocks (append (beat1-clock) (condition-clock) (trust-clock)
                                 (nightingale-earning-clock) (truth-clock) (case-progress-clock))))
          '()))

    ;; ── 节拍二·第 10 天：收账人上门时才做选择 ──────────
    ;; 不再提前交钱。攒着的钱是玩家完成目标的动力,直到收账人真的上门那一刻,
    ;; 才在两条路里选:
    ;;   交钱了事 —— 交出首期,收账人收手,跳过抢人;这笔钱记进日后的封口钱。
    ;;   动手     —— 进抢人交锋;赢了钱还在你手里,还从收账人身上多抢一笔。
    ;; 这样为攒钱努力的玩家不会觉得"钱白掏了",选择硬扛的还能拿到额外回报。
    (define beat2-fight-bonus 50)

    (define (node-beat2-pay)
      (node (if installment-reserved? "交出备好的首期" "当场交钱了事")
        :subtitle (if installment-reserved?
                      "让夜莺把备好的首期交出去；收账人收手离开，钱记进日后的封口钱"
                      (string-append "交出 " (number->string first-installment)
                                     " 金首期；收账人收手离开，钱记进日后的封口钱"))
        :requires (if installment-reserved? '() (list (req-item "金钱" first-installment)))
        :resolve (instant
          (outcome "钱交出去了"
            (lambda ()
              (set-protection! "首期")
              (on-public-event-result 'success))))))

    (define (node-beat2-fight)
      (encounter-action "拒付并动手"
        (lambda ()
          (if installment-reserved?
              (begin
                (add-item! "金钱" first-installment)
                (set! installment-reserved? #f)
                (sync-globals!))
              #f)
          (start-encounter "夜莺·抢人"
            (lambda (result)
              (if (equal? result 'success)
                  (begin
                    (add-item! "金钱" beat2-fight-bonus)
                    (notify! (string-append "收账人被撂在原地。你顺走了他身上的 "
                                            (number->string beat2-fight-bonus)
                                            " 金——这一场,你的钱一分没动,还多进了一笔。")))
                  #f)
              (on-public-event-result result))))))

    (define (beat2-pending-nodes)
      (append
        (if (or installment-reserved? (>= (item-count "金钱") first-installment))
            (list (node-beat2-pay))
            '())
        (list (node-beat2-fight))))

    ;; ── 节拍三·路线一：付封口钱 ────────────────────
    (define (node-hush-payment)
      (node "付封口钱了断"
        :subtitle (string-append "封口钱 " (number->string nightingale-earnings) "/"
                                 (number->string (hush-total)) "；你付 " (number->string (hush-due))
                                 " 金，买断的是卖身约和他们的追讨"
                                 (if (sam-pressure?)
                                     "；萨姆的调查,这笔钱买不断——多付的那部分,是绕不开他的代价"
                                     ""))
        :requires (list (req-die) (req-item "金钱" (hush-due)))
        :resolve (instant
          (outcome "钱送出去了"
            (lambda () (set! hush-paid? #t) (sync-globals!))))))

    (define (route1-nodes)
      (if (and (stage3-open?) (not (route-settled?)))
          (list (node-hush-payment))
          '()))

    ;; ── 节拍三·路线三：送她走，自己扛 ────────────────
    (define (node-insurance-berth)
      (node "委托保险安排舱位"
        :subtitle (string-append "沃尔特是保险公司的理赔调查员；夜莺本人名下的紧急转移条款，"
                                 (number->string (berth-price-effective)) "金"
                                 (if (sam-pressure?)
                                     "(萨姆还在查这件事,公司把风险费也算了进去)"
                                     ""))
        :requires (list (req-die) (req-item "金钱" (berth-price-effective)))
        :resolve (instant
          (outcome "公司舱位办妥"
            (lambda () (set! berth? #t) (sync-globals!))))))

    (define (detective-blocks-farewell?)
      ;; 真相揭开后，不能把人送走再假装什么也没发生；至少得向萨姆交代老板那一半。
      (and (truth-known?) (equal? (sam 'evidence) "无")))

    (define (node-farewell)
      (node "送她上船"
        :subtitle (if (detective-blocks-farewell?)
                      "萨姆已经盯上这桩案子。没有给他一个交代，他会在码头拦下夜莺。"
                      "跳板快收了。第 17 天的账，不会跟着她走，只会找上你。")
        :tags (if (detective-blocks-farewell?) (list "需要向萨姆交代") '())
        :disabled (detective-blocks-farewell?)
        :requires (list (req-die))
        :resolve (instant
          (lambda ()
            (if (truth-known?)
                (play-dialogue!
                  (line "夜莺" "跳板要收了。")
                  (line "夜莺" "我这一辈子,遇到麻烦只会自己跑。这回,你却先没打算丢下我。")
                  (line "夜莺" "谢谢你,把那句话听完了,还没走。")
                  (line "尼尔" "一路平安。")
                  (line "夜莺" "……我记住你了。真的记住了。"))
                (play-dialogue!
                  (line "夜莺" "跳板要收了。")
                  (line "夜莺" "……不必送我。")
                  (line "夜莺" "这个,你留着。想起我的时候,就想我一直好好的,也一直记挂着你。")))
            (set! farewell? #t)
            (sync-globals!)
            (spotlight! "船开了"
              (if (truth-known?)
                  "跳板上,她撑开那把红伞。船开了。第 17 天上门的人,由你一个人接。"
                  "跳板上,她撑开那把红伞。她把戒指塞回你手里,船开了。第 17 天上门的人,由你一个人接。"))))))

    (define (route3-nodes)
      (append
        (if (and (stage3-open?) berth? (<= world-day 16) (not (route-settled?)))
            (list (node-farewell))
            '())))

    ;; ── 节拍三·路线四：立案送警 ────────────────────
    (define (node-case-route-locked)
      (node "立案送警"
        :subtitle "阿瑟是辖区警察局的登记与档案职员；他还不会把跨城命案送进正式程序"
        :tags (list "需要阿瑟·熟")
        :disabled #t))

    (define (node-build-case)
      (action "拼合当晚案卷"
        (list (req-die))
        (roll 'knowledge
          (outcome "拼不出头绪" (lambda () (spend-composure! 1)))
          (outcome "拼出个大概" (lambda () (set-flag! '案卷备妥)))
          (outcome "拼得严丝合缝" (lambda () (set-flag! '案卷备妥))))))

    (define (node-file-case)
      (node "请阿瑟走程序"
        :subtitle "辖区警察局的登记与档案职员；通行证是由头，人情让材料不会停在收件桌上"
        :tags (if (> (item-count "办案通行证") 0) '() (list "需要办案通行证"))
        :requires (list (req-item "办案通行证" 1))
        :resolve (instant
          (outcome "案子立起来了"
            (lambda () (set! case-filed? #t) (set! case-filed-day world-day) (sync-globals!))))))

    ;; ── 节拍三·到期日分派 ───────────────────────────
    (define (node-hush-payoff-pending)
      (instant-action "看他烧掉那纸约"
        (lambda ()
          (play-dialogue!
            (line "世界" "他把那纸卖身约凑到烛火上。纸卷起来,黑掉,碎成灰。")
            (line "世界" "'账,清了。'他说完,转身走进雨里。")
            (line "夜莺" "钱是我们俩一起凑的。这一次,不是我一个人扛。")
            (line "尼尔" "账清了就好。"))
          (if (truth-known?)
              (begin
                (play-dialogue!
                  (line "世界" "那晚,老街酒馆的灯亮着。她把一整场都唱完了。")
                  (line "世界" "几天后,萨姆捎来一句话:邻城有人收到了一笔没署名的钱。亨利的妹妹那份。"))
                (spotlight! "封口：账清了"
                  (if (or (equal? stance "体谅") (equal? stance "自白"))
                      "没人再攥着那张纸。这一次,谁都没有一个人扛。"
                      "没人再攥着那张纸。话说开了,这笔账才算真的清。")))
              (begin
                (play-dialogue!
                  (line "世界" "临走前,你回头望了一眼——街角路灯下,站着一个不认识的人影,没有靠近,也没有走开。"))
                (spotlight! "封口：账清了" "钱账清了,可那道影子还在路灯下。这件事,不一定就这么完了。有些事,她始终没跟你说。")))
          (on-public-event-result 'success))))

    (define (node-case-filed-pending)
      (instant-action "看着巡警上门"
        (lambda ()
          (play-dialogue!
            (line "世界" "敲门的不是老板,是两个巡警。他们只问了几句,记了几笔,就走了。")
            (line "世界" "老板那一半,压不住了。她那一半,没人再提。")
            (line "世界" "那晚,老街酒馆照常开唱。她没问你做了什么,你也没说。"))
          (spotlight! "案子压着"
            (if (or (equal? stance "体谅") (equal? stance "自白"))
                "老板的账,记在了别处;她的那一半,还压在没人翻开的纸里——但她知道,你没让她一个人担这份沉默。"
                "老板的账,记在了别处;她的那一半,还压在没人翻开的纸里。这份沉默,她清楚是自己选的。"))
          (on-public-event-result 'success))))

    (define (node-surrender-pending)
      (instant-action "看着她跟巡警走"
        (lambda ()
          (play-dialogue!
            (line "世界" "第十七天，来的是萨姆和两名巡警。没有镣铐，只有一只装着案卷的牛皮袋。")
            (line "夜莺" "我还以为,你会躲开这一天。")
            (line "尼尔" (if (or (equal? stance "体谅") (equal? stance "自白")) "你没躲,我也不躲。" "该说清楚的,总得有人说清楚。"))
            (line "夜莺" "我会把话说完。亨利的妹妹,也该听见一个不是老板编的说法。")
            (line "夜莺" "这一次,是我自己走进去的。")
            (line "世界" "她走进雨里，没有回头。萨姆把伞往她那边偏了一点。")
            (line "世界" "那晚,老街酒馆的台子空着。老板擦着杯子,没人再点她那首歌。"))
          (spotlight! "随案移交"
            (if (or (equal? stance "体谅") (equal? stance "自白"))
                "案卷合上那天,你一个人坐在空舞台前。这一次,谁都没让她一个人扛。"
                "案卷合上那天,你一个人坐在空舞台前。这一次,说了算的是她自己。"))
          (on-public-event-result 'surrendered))))

    (define (node-encounter-entry)
      (encounter-action (public-event-action-name)
        (lambda ()
          (start-encounter "夜莺·了断" on-public-event-result))))

    (define (node-walk-away-pending)
      (node "去街角喝酒"
        :subtitle "你不去,今晚就没人站在她那边"
        :resolve (instant
          (lambda ()
            (play-dialogue!
              (line "世界" "你坐在街角的酒桌旁,酒喝得很慢。你没有去。"))
            (spotlight! "你没有去"
              (if (or (equal? stance "体谅") (equal? stance "自白"))
                  "杯子见了底,你还是没起身。你知道这不该由她一个人扛,可今晚,没人替她挡。"
                  "杯子见了底,你还是没起身。今晚,没人替她挡。"))
            (on-final-event-walked-away!)))))

    (define (beat3-pending-nodes)
      (cond
        (surrendered? (list (node-surrender-pending)))
        (hush-paid? (list (node-hush-payoff-pending)))
        (case-filed? (list (node-case-filed-pending)))
        (farewell? (list (node-encounter-entry)))
        (else
         (append
           (list (node-encounter-entry))
           (if (truth-known?) (list (node-walk-away-pending)) '())))))

    ;; ── 结局信（远方的信 / 信和疤，第 17 天 + letter-delay 天后一次性）──
    (define (node-open-letter)
      (instant-action "拆开那封信"
        (lambda ()
          (set-flag! '结局信)
          (add-item! "金钱" letter-money)
          (play-dialogue!
            (if (= story-stage 94)
                (line "夜莺" "信里没有署名,只夹着一叠钱。她不知道你为这件事付出了什么。")
                (line "夜莺" "信里没有署名,只夹着一叠钱。她始终在出力。")))
          (if (truth-known?)
              (play-dialogue!
                (line "夜莺" "信末只有一句:邻城那笔,我按月在还。你知道是还给谁的。"))
              #f)
          (spotlight! "远方的信" (string-append "信里没有署名,夹着 " (number->string letter-money) " 金。")))))

    (define (letter-nodes)
      (if (and (or (= story-stage 93) (= story-stage 94))
               (>= world-day (+ ending-day letter-delay))
               (not (has-flag? '结局信)))
          (list (node-open-letter))
          '()))

    ;; 敲门事件挂世界根节点：来的人找的是你，与夜莺那张卡无关，不该藏进她的 children。
    (define (world-nodes)
      (append
        (if (= story-stage 0) (list (node-answer-door)) '())
        (if (and (stage3-open?) (not (sam-intro?))) (list (node-sam-visit)) '())))

    (define-turn-rule "夜莺凑封口钱"
      (lambda ()
        (and (stage3-open?) (not (route-settled?))
             (< nightingale-earnings (hush-total))
             (not public-event-pending?)))
      (lambda ()
        (set! nightingale-earnings
              (min (hush-total) (+ nightingale-earnings nightingale-daily-earning)))
        (notify! (string-append "夜莺把今天挣到的 " (number->string nightingale-daily-earning)
                                " 金放进封口钱。现有 " (number->string nightingale-earnings)
                                "/" (number->string (hush-total)) "；你还需 "
                                (number->string (hush-due)) " 金。"))))

    (define-turn-rule "夜莺听歌每日重置"
      (lambda () listened-today?)
      (lambda () (set! listened-today? #f)))

    ;; 证据交割后到第 17 天到期演出前,案子并非静止——每隔几天播报一次进展。
    (define (case-progress-texts)
      (list
        "阿瑟捎话来:材料已经报上去,案子正按程序往上走。"
        "萨姆说,雇凶那条线已经有人接手去查,老板还蒙在鼓里。"
        "案卷批复只差一步,阿瑟说,快了。"))

    (define (surrender-progress-texts)
      (list
        "萨姆捎话:回邻城的手续在办,亨利的妹妹已经知会到了。"
        "他说,口供的措辞还在斟酌,不会让她一个人扛下所有。"
        "邻城那边把日子定了下来,就等第十七天启程。"))

    (define-turn-rule "夜莺案情进展播报"
      (lambda ()
        (and (stage3-open?) (not public-event-pending?)
             (or (and case-filed?
                      (< case-progress-shown (length (case-progress-texts)))
                      (>= world-day (+ case-filed-day (* (+ case-progress-shown 1) case-progress-interval))))
                 (and surrendered?
                      (< surrender-progress-shown (length (surrender-progress-texts)))
                      (>= world-day (+ surrendered-day (* (+ surrender-progress-shown 1) case-progress-interval)))))))
      (lambda ()
        (if case-filed?
            (begin
              (notify! (list-ref (case-progress-texts) case-progress-shown))
              (set! case-progress-shown (+ case-progress-shown 1)))
            (begin
              (notify! (list-ref (surrender-progress-texts) surrender-progress-shown))
              (set! surrender-progress-shown (+ surrender-progress-shown 1))))))

    (define-turn-rule "萨姆需要你"
      (lambda ()
        (and (sam 'along?) (>= police-truth-progress 2) (not (has-flag? '萨姆需要你))
             (stage3-open?) (truth-pending?) (not public-event-pending?)))
      (lambda ()
        (set-flag! '萨姆需要你)
        (play-dialogue!
          (line "萨姆" "最后一页调不出来了。有人打了招呼,邻城那边把原件收进了保险柜。")
          (line "萨姆" "抄件是死路。可押运原件的批条得走货栈——东西过一次夜,记录就留一行。")
          (line "萨姆" "我一个人翻不了货栈的夜账。这一段,得你陪我走。"))
        (spotlight! "他需要你" "案卷的最后一页锁在了程序外面。货栈的夜账里有它的影子——萨姆在等你一起去。")))

    ;; ── 节拍一：查访盯梢者 ──────────────────────────
    (define (node-confront-stalker)
      (node "主动出击"
        :subtitle "陌生人的藏身处；抢在他找上夜莺之前进行交锋"
        :tags (list "交锋")
        :resolve (instant
          (lambda ()
            (set! beat1-early? #t)
            (sync-globals!)
            (begin-public-event-early!)
            (start-encounter "夜莺·警告" on-public-event-result)))))

    (define (node-tavern-inquire-stalker)
      (node "向酒客打听陌生人"
        :subtitle "从酒客的闲话里拼凑他的外貌、口音和习惯"
        :tags (list "低风险")
        :clocks (list (list 'clock "酒客的说法" tavern-inquiry-progress beat1-location-target 'segments
                            "填满后，对陌生人的了解增加 1 格。"))
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "问得太急"
            (lambda () (spend-composure! 1)))
          (outcome "拼出轮廓"
            (lambda () (advance-beat1-location! "酒馆" 1)))
          (outcome "认出那张脸"
            (lambda () (advance-beat1-location! "酒馆" 2))))))

    (define (node-dock-inquire-stalker)
      (node "在码头打听陌生人"
        :subtitle "向船员和搬运工追查他从哪里来、把东西送去哪里"
        :tags (list "低风险")
        :clocks (list (list 'clock "码头上的来路" dock-inquiry-progress beat1-location-target 'segments
                            "填满后，对陌生人的了解增加 1 格。"))
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "没人愿意开口"
            (lambda () (spend-composure! 1)))
          (outcome "查到船期"
            (lambda () (advance-beat1-location! "码头" 1)))
          (outcome "追到去向"
            (lambda () (advance-beat1-location! "码头" 2))))))

    (define (beat1-nodes-at location)
      (cond
        ((and (equal? location "酒馆") (= story-stage 1)
              (< tavern-inquiry-progress beat1-location-target))
         (list (node-tavern-inquire-stalker)))
        ((and (equal? location "码头") (= story-stage 1)
              (< dock-inquiry-progress beat1-location-target))
         (list (node-dock-inquire-stalker)))
        (else '())))

    (define (hideout-nodes)
      (if (hideout-visible?) (list (node-confront-stalker)) '()))

    ;; ── 节拍三：暗账查访「那晚码头上发生了什么」──────
    ;; 三个来源横跨三条生活线，各自分层递进（不复读同一句），满格触发必看的「撒谎的人」。
    (define (node-dock-truth)
      (define (dock-truth-mods)
        (if (relation-at-least? "劳工" '相识)
            (list (modifier 1 "码头的人认得你"))
            '()))
      (node "询问码头老人"
        :subtitle (if (relation-at-least? "劳工" '相识)
                      "码头的人认得你,才肯带你去见那个还记得栈桥的人"
                      "得先在码头混个面熟,才有人肯把你带去见他")
        :tags (if (relation-at-least? "劳工" '相识) '() (list "需要劳工·相识"))
        :disabled (not (relation-at-least? "劳工" '相识))
        :clocks (list (list 'clock "老人的记忆" dock-truth-progress dock-truth-target 'segments
                            "问一次想起一层,想完为止。"))
        :requires (list (req-die))
        :resolve (roll 'social dock-truth-mods
          (lambda () (spend-composure! 1))
          (lambda () (advance-source-truth! "码头"))
          (lambda ()
            (if (sam 'along?)
                (play-banter!
                  (line "萨姆" "和案卷上对不上的,又多了一处。"))
                #f)
            (add-item! "情报" 1)
            (advance-source-truth! "码头")))))

    (define (node-police-truth)
      (node "调邻城的案卷抄件"
        :subtitle (string-append "阿瑟是辖区警察局的登记与档案职员；"
                    (if (relation-at-least? "官僚" '相识)
                        (if (sam 'along?)
                            "阿瑟调卷,萨姆读卷——他一眼能看出哪一页被抽换过"
                            "肯替你调一张抄件，但得你自己找出破绽")
                        "得先在警察局登记，他才会替你调邻城旧卷"))
        :tags (if (relation-at-least? "官僚" '相识) '() (list "需要官僚·相识"))
        :disabled (not (relation-at-least? "官僚" '相识))
        :clocks (list (list 'clock "案卷抄件" police-truth-progress police-truth-target 'segments
                            "翻一次找出一处破绽,翻完为止。"))
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (lambda () #f)
          (lambda () (advance-source-truth! "警察局"))
          (lambda () (advance-source-truth! "警察局")))))

    (define (node-freight-truth)
      (define (freight-truth-mods)
        (if (relation-at-least? "富商" '相识)
            (list (modifier 1 "货栈的人卖你个面子"))
            '()))
      (node "查船期与货栈记录"
        :subtitle (if (relation-at-least? "富商" '相识)
                      "货栈的人卖你个面子,让你翻一夜旧船期"
                      "得先让货运代理认得你,货栈的旧记录才会打开")
        :tags (if (relation-at-least? "富商" '相识) '() (list "需要富商·相识"))
        :disabled (not (relation-at-least? "富商" '相识))
        :clocks (list (list 'clock "船期与货栈" freight-truth-progress freight-truth-target 'segments
                            "翻一次核一层,核完为止。"))
        :requires (list (req-die))
        :resolve (roll 'knowledge freight-truth-mods
          (lambda () (spend-composure! 1))
          (lambda () (advance-source-truth! "货运公司"))
          (lambda () (advance-source-truth! "货运公司")))))

    (define (complete-freight-truth!)
      (let ((remaining (- freight-truth-target freight-truth-progress)))
        (set! freight-truth-progress freight-truth-target)
        (set! truth-progress (min truth-target (+ truth-progress remaining)))
        (record-clock-progress! "那晚码头上发生了什么" remaining)
        (sync-globals!)
        (sync-blockers!)))

    (define (node-night-check)
      (node "和萨姆夜查货栈"
        :subtitle "失败会被守夜人撵出来:受伤并损失冷静,夜账改天再翻"
        :tags (list "交锋")
        :requires (list (req-die))
        :resolve (instant
          (lambda ()
            (start-encounter "夜莺·夜查"
              (lambda (result)
                (if (equal? result 'success)
                    (complete-freight-truth!)
                    #f)))))))

    ;; ── 幕间场景(酒馆) ──────────────────────────────
    (define (node-old-ring)
      (container "旧戒指"
        (list
          (instant-action "收下"
            (lambda ()
              (set-flag! '旧戒指)
              (set-flag! '收下戒指)
              (add-item! "旧戒指" 1)
              (gain-trust!)
              (spotlight! "当了它" "她把戒指按进你掌心。'当了它,能顶一阵。'谢谢两个字,你没说出口。")))
          (instant-action "不收"
            (lambda ()
              (set-flag! '旧戒指)
              (spotlight! "不收" "你把戒指推了回去。她盯着你看了很久,才把它重新攥回掌心。"))))))

    ;; ── 结局留在酒馆的观察 ──────────────────────────
    (define (ending-tavern-node)
      (cond
        ((= story-stage 90)
         (observe-action "台上的夜莺"
           "她立在角落那盏旧油灯下,头一回把一整场唱完。收尾那一句,她望着你这边。"))
        ((= story-stage 91)
         (observe-action "空舞台"
           (if (trust-met?)
               (string-append "红伞不在了——她走的时候带上了它。"
                 (if (> (item-count "旧戒指") 0)
                     "酒馆的灯还亮着,台上却空了,再没人开嗓。那枚旧戒指还在你兜里——它的来历,如今只剩你一个人记得。"
                     "酒馆的灯还亮着,台上却空了,再没人开嗓。那枚旧戒指换成了钱,钱早花光了,来历只剩你一个人记得。"))
               "酒馆的灯还亮着,台上却空了。台侧那把红伞还立在原处——被带走的人,来不及取伞。")))
        ((or (= story-stage 93) (= story-stage 94))
         (observe-action "空舞台"
           "台上没有人唱歌了,但这寂静不一样——你知道她在哪儿,也知道她安好。"))
        ((= story-stage 95)
         (observe-action "台上的夜莺"
           "她还在台上唱,可这不是那一场——真名,她没再提起过。"))
        ((= story-stage 96)
         (observe-action "空舞台"
           "台上没有人唱歌了。老板擦着杯子,没人问起那晚发生了什么,你也没问。"))
        ((= story-stage 97)
         (observe-action "空舞台"
           "台上空着。她不是被老板带走的——红伞也跟着她走了。可她也没有被任何人的沉默留下。"))
        (else #f)))

    (define (song-text)
      (cond
        ((= story-stage 1) "她唱一支旧调子，眼睛不看客人，看门。唱到一半，门被风推开一条缝，她的声音停了半拍。")
        ((and (= story-stage 2) (= condition-level 0)) "她今晚的歌放得很低，像是唱给吧台后面那盏灯听的。收尾之前，她朝你这桌看了一眼。")
        ((and (= story-stage 2) (= condition-level 1)) "她只唱了两段就下了台。路过你桌边时脚步没停，只轻轻碰了一下桌角。")
        ((and (= story-stage 3) (= condition-level 0)) "她挑了支慢的。唱到中间有人喊换个曲子，她没理，把那支唱到了她想停的地方。")
        ((and (= story-stage 3) (= condition-level 1)) "她站上台，先看了一圈门和窗，才开口。歌是好歌，只是每一句都留着退路。")
        ((= story-stage 90) "她唱得比从前松了。收尾那句,她不再看门,看你。")
        ((= story-stage 95) "她还在唱。只是有几支歌,她再也没点过。")
        (else (error "夜莺听歌：当前状态没有可用文案"))))

    (define (node-listen-song)
      (node "听她唱一段"
        :subtitle (if listened-today?
                      "今晚这一段已经听过了"
                      "10 金；恢复 2 点冷静，每天一次。她唱歌的时候，这座城安静一点")
        :disabled listened-today?
        :requires (list (req-item "金钱" 10))
        :resolve (instant
          (outcome "听她唱了一段"
            (lambda ()
              (set! listened-today? #t)
              (restore-actor-composure! 'player 2))
            'light))))

    (define (node-empty-stage)
      (observe-action "没人开嗓的台子"
        "台上没人。她的红伞还立在台侧,伞面上的灰积了一层——伞在,人不在。老板没动它,客人也没人问。"))

    (define (tavern-nodes)
      (append
        (if (singer-present?)
            (list (node-listen-song))
            '())
        (if (and (= story-stage 3) (>= world-day 12) (not (has-flag? '旧戒指)))
            (list (node-old-ring))
            '())
        (if (and (= story-stage 3) (>= condition-level 2) (not (route-settled?)))
            (list (node-empty-stage))
            '())
        (if (ending-tavern-node)
            (list (ending-tavern-node))
            '())))

    ;; ── 跨地点节点收拢（警察局/码头/货运公司）────────────
    ;; 地点文件只认「我在哪、这批节点该插在列表的哪个槽位」，
    ;; 可见性判断全部收回夜莺自己算——谁拥有状态，谁决定这段状态驱动
    ;; 的节点该不该出现在别人的地盘上。route 槽承载节拍三的暗账查访与了断路线。
    (define (route-nodes-at location)
      (cond
        ((equal? location "码头")
         (if (and (stage3-open?) (truth-lead?) (truth-pending?) (not (route-settled?))
                  (< dock-truth-progress dock-truth-target))
             (list (node-dock-truth))
             '()))
        ((equal? location "警察局")
         (append
           (if (and (stage3-open?) (truth-lead?) (truth-pending?) (not (route-settled?))
                    (< police-truth-progress police-truth-target))
               (list (node-police-truth))
               '())
           (if (and (stage3-open?) (truth-known?) (case-evidence-submitted?)
                    (not (route-settled?)) (not (arthur 'can-escalate?)))
               (list (node-case-route-locked))
               '())
           (if (and (stage3-open?) (truth-known?) (case-evidence-submitted?) (arthur 'can-escalate?)
                    (not (route-settled?)) (not (case-ready?)))
               (list (node-build-case))
               '())
           (if (and (stage3-open?) (truth-known?) (case-evidence-submitted?) (arthur 'can-escalate?)
                    (not (route-settled?)) (case-ready?))
               (list (node-file-case))
               '())))
        ((equal? location "货运公司")
         (if (and (stage3-open?) (truth-lead?) (truth-pending?) (not (route-settled?))
                  (< freight-truth-progress freight-truth-target))
             (list (if (sam 'along?) (node-night-check) (node-freight-truth)))
             '()))
        ((equal? location "保险公司")
         (if (and (stage3-open?) (walter 'can-arrange-berth?)
                  (not berth?) (not (route-settled?)))
             (list (node-insurance-berth))
             '()))
        (else '())))

    ;; ── 交锋结果回调 ────────────────────────────────
    (define (on-bout-result bout result)
      (cond
        ((= bout 0)
         (if (not beat1-early?)
             (worsen-condition! 1)
             (gain-trust!))
         (advance-stage! 2)
         (sync-blockers!))
        ((= bout 1)
         (if (equal? result 'fail)
             (begin
               (worsen-condition! 2)
               (old-street-tavern 'set-closed! 2))
             (gain-trust!))
         (advance-stage! 3)
         (sync-blockers!))
        ((= bout 2)
         (if (> (+ (if hush-paid? 1 0) (if farewell? 1 0) (if case-filed? 1 0) (if surrendered? 1 0)) 1)
             (error "夜莺：节拍三出现非法的多路线同真组合")
             #t)
         (set! ending-day world-day)
         (cond
           (surrendered? (advance-stage! 97))
           (hush-paid? (advance-stage! 90))
           (case-filed? (advance-stage! 95))
           (farewell? (if (equal? result 'success) (advance-stage! 93) (advance-stage! 94)))
           ((equal? result 'walked-away) (advance-stage! 96))
           ((equal? result 'fail) (advance-stage! 91))
           ((equal? result 'success) (advance-stage! 90))
           (else (error "夜莺：了断结算收到未知 result")))
         (sync-blockers!))
        (else #f)))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data) (render-data))
          ((equal? msg 'world-nodes) (world-nodes))
          ((equal? msg 'tavern-nodes) (tavern-nodes))
          ((equal? msg 'beat1-nodes-at) (beat1-nodes-at (cadr args)))
          ((equal? msg 'hideout-visible?) (hideout-visible?))
          ((equal? msg 'hideout-nodes) (hideout-nodes))
          ((equal? msg 'route-nodes-at) (route-nodes-at (cadr args)))
          ((equal? msg 'story-stage) story-stage)
          ((equal? msg 'singer-present?)
           (singer-present?))
          ((equal? msg 'ring-value) ring-value)
          ((equal? msg 'stage3-open?) (stage3-open?))
          ((equal? msg 'sam-intro?) (sam-intro?))
          ((equal? msg 'truth-progress) truth-progress)
          ((equal? msg 'truth-lead?) (truth-lead?))
          ((equal? msg 'truth-known?) (truth-known?))
          ((equal? msg 'route-settled?) (route-settled?))
          ((equal? msg 'set-truth-lead!)
           (begin
             (if (or (not (stage3-open?)) (route-settled?))
                 (error "夜莺暗账：当前不能开启调查")
                 #t)
             (set-flag! '夜莺的消息)
             (sync-globals!)))
          ((equal? msg 'set-surrendered!)
           (begin
             (if (or (not (truth-known?)) (route-settled?))
                 (error "夜莺完整移交：当前状态不允许") #t)
             (set! surrendered? #t)
             (set! surrendered-day world-day)
             (sync-globals!)))
          ((equal? msg 'has-protection?) (not (equal? protection "无")))
          ((equal? msg 'sync-blockers!) (sync-blockers!))
          ((equal? msg 'on-bout-result) (on-bout-result (cadr args) (caddr args)))
          ((equal? msg 'beat2-pending-nodes) (beat2-pending-nodes))
          ((equal? msg 'beat3-pending-nodes) (beat3-pending-nodes))
          ((equal? msg 'save)
           (list
             (list "story-stage" story-stage)
             (list "condition-level" condition-level)
             (list "listened-today?" listened-today?)
             (list "trust" trust)
             (list "tavern-inquiry-progress" tavern-inquiry-progress)
             (list "dock-inquiry-progress" dock-inquiry-progress)
             (list "beat1-early?" beat1-early?)
             (list "protection" protection)
             (list "installment-reserved?" installment-reserved?)
             (list "installment-paid?" installment-paid?)
             (list "truth-progress" truth-progress)
             (list "dock-truth-progress" dock-truth-progress)
             (list "police-truth-progress" police-truth-progress)
             (list "freight-truth-progress" freight-truth-progress)
             (list "stance" stance)
             (list "berth?" berth?)
             (list "farewell?" farewell?)
             (list "hush-paid?" hush-paid?)
             (list "case-filed?" case-filed?)
             (list "surrendered?" surrendered?)
             (list "case-filed-day" case-filed-day)
             (list "surrendered-day" surrendered-day)
             (list "case-progress-shown" case-progress-shown)
             (list "surrender-progress-shown" surrender-progress-shown)
             (list "ending-day" ending-day)
             (list "nightingale-earnings" nightingale-earnings)
             (list "scene-flags" scene-flags)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! story-stage (assoc-get data "story-stage" 0))
             (set! condition-level (assoc-get data "condition-level" 0))
             (if (or (< condition-level 0) (> condition-level 3))
                 (error "夜莺存档错误：状态等级非法")
                 #t)
             (set! listened-today? (assoc-get data "listened-today?" #f))
             (set! trust (assoc-get data "trust" 0))
             (if (< trust 0)
                 (error "夜莺存档错误：信任非法")
                 #t)
             (set! tavern-inquiry-progress (assoc-get data "tavern-inquiry-progress" 0))
             (set! dock-inquiry-progress (assoc-get data "dock-inquiry-progress" 0))
             (set! beat1-early? (assoc-get data "beat1-early?" #f))
             (set! protection (assoc-get data "protection" "无"))
             (set! installment-reserved? (assoc-get data "installment-reserved?" #f))
             (set! installment-paid? (assoc-get data "installment-paid?" #f))
             (set! truth-progress (assoc-get data "truth-progress" 0))
             (set! dock-truth-progress (assoc-get data "dock-truth-progress" 0))
             (set! police-truth-progress (assoc-get data "police-truth-progress" 0))
             (set! freight-truth-progress (assoc-get data "freight-truth-progress" 0))
             (set! stance (assoc-get data "stance" "无"))
             (set! berth? (assoc-get data "berth?" #f))
             (set! farewell? (assoc-get data "farewell?" #f))
             (set! hush-paid? (assoc-get data "hush-paid?" #f))
             (set! case-filed? (assoc-get data "case-filed?" #f))
             (set! surrendered? (assoc-get data "surrendered?" #f))
             (set! case-filed-day (assoc-get data "case-filed-day" 0))
             (set! surrendered-day (assoc-get data "surrendered-day" 0))
             (set! case-progress-shown (assoc-get data "case-progress-shown" 0))
             (set! surrender-progress-shown (assoc-get data "surrender-progress-shown" 0))
             (set! ending-day (assoc-get data "ending-day" 0))
             (set! nightingale-earnings (assoc-get data "nightingale-earnings" 0))
             (set! scene-flags (normalize-flags (assoc-get data "scene-flags" '())))
             (sync-globals!)
             (sync-blockers!)))
          ((equal? msg 'debug-stage!) (advance-stage! (cadr args)))
          ((equal? msg 'debug-set-flag!) (set-flag! (cadr args)))
          ((equal? msg 'debug-set-truth!) (set! truth-progress (cadr args)) (sync-globals!))
          ((equal? msg 'debug-force-hush-paid!) (set! hush-paid? #t) (sync-globals!))
          ((equal? msg 'debug-force-farewell!) (set! farewell? #t) (sync-globals!))
          ((equal? msg 'debug-force-case-filed!) (set! case-filed? #t) (sync-globals!))
          (#t #f))))))
