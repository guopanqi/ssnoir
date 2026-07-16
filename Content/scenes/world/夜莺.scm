;; scenes/world/夜莺.scm - 夜莺主线故事模块
;; 拥有 story-stage / condition-level / scene-flags 全部故事状态,通过消息接口与世界协调。

(define nightingale
  (let ()
    ;; ── 常量 ────────────────────────────────────────
    (define prepayment 30)
    (define ransom 150)              ; 节拍二文案里的名义数，节拍三涨成封口价
    (define first-installment 100)
    (define ring-value 15)
    (define beat1-location-target 4)
    (define stranger-understanding-target 2)
    (define hush-price 480)             ; 封口总价
    (define hush-price-discounted 420)  ; 付过首期，仍由她承担一半左右
    (define hush-contribution-target 240)
    (define nightingale-daily-earning 40)
    (define rumor-price 50)
    (define berth-price-insurance 80)   ; 保险公司紧急转移条款的正规舱位
    (define truth-target 4)             ; 暗账查访格数
    (define letter-delay 2)             ; 结局 D 的信,几天后送到
    (define letter-money 40)            ; 信里附的钱

    ;; ── 状态 ────────────────────────────────────────
    (define story-stage 0)
    ;; 0=未开场 1=受托查探 2=账转你头(二层已揭) 3=知道真相(三层已揭)
    ;; 90=真名(A) 91=她被带走(B)
    ;; 93=远方的信(D1) 94=信和疤(D2) 95=案卷(F) 96=你没有去(B') 97=随案移交(G)
    (define condition-level 0)
    ;; 0=稳定 1=不安 2=恐惧 3=被迫转移
    (define diner-inquiry-progress 0)
    (define dock-inquiry-progress 0)
    ;; 饭店与码头各自拥有一条 0..4 的查访 Clock；每完成一条，陌生人了解 +1。
    (define beat1-early? #f)
    (define protection "无")
    (define installment-paid? #f)
    (define truth-progress 0)  ; 暗账查访进度 0..truth-target
    (define berth? #f)         ; 路线三①：是否已弄到舱位
    (define farewell? #f)      ; 路线三②：是否已送她上船
    (define hush-paid? #f)     ; 路线一：是否已付封口钱
    (define case-filed? #f)    ; 路线四：是否已立案送警
    (define surrendered? #f)   ; 将完整真相交给萨姆：她随案移交
    (define ending-day 0)      ; 进入终值 stage 时的 world-day（结局信用）
    (define stage3-start-day 0)
    (define nightingale-earnings 0) ; 她替封口钱凑出的部分
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
        ((or (equal? flag '旧戒指) (equal? flag "旧戒指")) "旧戒指")
        ((or (equal? flag '撒谎的人) (equal? flag "撒谎的人")) "撒谎的人")
        ((or (equal? flag '案卷备妥) (equal? flag "案卷备妥")) "案卷备妥")
        ((or (equal? flag '结局信) (equal? flag "结局信")) "结局信")
        ((or (equal? flag '跟梢的人) (equal? flag "跟梢的人")) "跟梢的人")
        ((or (equal? flag '夜莺的消息) (equal? flag "夜莺的消息")) "夜莺的消息")
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
    (define (truth-lead?) (has-flag? '夜莺的消息))
    (define (truth-pending?) (and (truth-lead?) (< truth-progress truth-target)))
    ;; 节拍三开放：stage=3 且三层已揭（未看必看戏前，节拍三内容一律不出现）。
    (define (stage3-open?) (and (= story-stage 3) (has-flag? '三层已揭)))
    ;; 路线一/三/四互斥：任一落定，其余路线的准备节点全部消失。
    (define (route-settled?) (or hush-paid? farewell? case-filed? surrendered?))
    ;; 立案交割是否已经就绪：自己拼过案卷，或节拍二走过阿瑟的提级方案。
    (define (case-ready?) (and (equal? (sam 'evidence) "老板")
                               (or (has-flag? '案卷备妥) (equal? protection "警局"))))
    (define (case-evidence-submitted?) (equal? (sam 'evidence) "老板"))
    (define (hush-total) (if installment-paid? hush-price-discounted hush-price))
    (define (hush-due) (max 0 (- (hush-total) nightingale-earnings)))

    (define (stranger-understanding)
      (+ (if (>= diner-inquiry-progress beat1-location-target) 1 0)
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
      (set-global! '夜莺已移交 surrendered?))

    (define (advance-stage! new-stage)
      (set! story-stage new-stage)
      (sync-globals!))

    (define (worsen-condition! n)
      (set! condition-level (min 3 (+ condition-level n)))
      (sync-globals!))

    (define (condition-label)
      (cond
        ((= condition-level 0) "稳定")
        ((= condition-level 1) "不安")
        ((= condition-level 2) "恐惧")
        ((= condition-level 3) "被迫转移")
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
      (if (equal? kind "首期") (set! installment-paid? #t) #f)
      (if (equal? kind "首期") (set-global! '夜莺已付首期 #t) #f)
      (sync-globals!))

    ;; 首期折扣现在体现在封口价上，而不是名义赎身钱的尾款。
    (define (ransom-hint)
      (if installment-paid?
          (string-append "首期他们认了账,封口钱只要 " (number->string hush-price-discounted) "。")
          ""))

    (define (advance-beat1-location! location n)
      (let ((before (stranger-understanding)))
        (cond
          ((equal? location "饭店")
           (let ((location-before diner-inquiry-progress))
             (set! diner-inquiry-progress
                   (min beat1-location-target (+ diner-inquiry-progress n)))
             (record-clock-progress! "饭店里的说法" (- diner-inquiry-progress location-before))))
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
                (spotlight! "藏身处揭晓" "饭店的描述和码头的来路对上了。陌生人的藏身处已经标在城市地图上。")
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
            (line "主角" "船期和案卷都对不上。")
            (line "夜莺" "旧账翻出来,不会让谁干净一点。")
            (line "夜莺" "你不必替我把那一晚也算清。")
            (line "主角" "可我得知道,第十七天到底该替谁挡。"))
          #f)
      (spotlight! label detail))

    (define (beat1-ready?)
      (and (= story-stage 1)
           (>= (stranger-understanding) stranger-understanding-target)
           (not public-event-pending?)))

    (define (hideout-visible?) (beat1-ready?))

    ;; ── 阻塞同步 ────────────────────────────────────
    (define (sync-blockers!)
      (cond
        ((= story-stage 0)
         (rest-block! "夜莺/开场敲门" "有人在敲门,先去看看是谁。"))
        ((and (= story-stage 2) (not (has-flag? '二层已揭)))
         (rest-block! "夜莺/第二层揭开" "她在等你回来。"))
        ((and (= story-stage 3) (not (has-flag? '三层已揭)))
         (rest-block! "夜莺/第三层揭开" "她伤着,有话要说。"))
        ((and (stage3-open?) (truth-lead?) (not (truth-pending?)) (not (has-flag? '撒谎的人)))
         (rest-block! "夜莺/撒谎的人" "有些话,今晚必须当面问她。"))
        (else
         (begin
           (rest-release! "夜莺/开场敲门")
           (rest-release! "夜莺/第二层揭开")
           (rest-release! "夜莺/第三层揭开")
           (rest-release! "夜莺/撒谎的人")))))

    ;; ── 开场节点 ────────────────────────────────────
    (define (node-answer-door)
      (instant-action "雨夜来客"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "门外雨下得像不要钱。钱在桌上,预付的——我要你查清楚,是谁在盯我的梢。")
            (line "主角" "你是谁?")
            (line "夜莺" "他们都叫我夜莺。那只死鸟和字条,也是他们送的。先开门,行吗?"))
          (add-item! "金钱" prepayment)
          (advance-stage! 1)
          (rest-release! "夜莺/开场敲门")
          (spotlight! "雨夜来客" "你把钱收进兜里。这不是委托,是一根钓钩,而你已经张了嘴。第三天以前,去饭店和码头查清那个陌生人藏在哪里。"))))

    ;; ── 第二层揭开(交锋一后,必看) ──────────────────
    (define (node-reveal-layer-2)
      (instant-action "听她解释"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "我不是逃债。我签了十年卖身约,跑了七年。")
            (line "夜莺" "他们找上门,要么带我回去,要么拿一笔赎身钱换我。")
            (line "主角" "多少?")
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
                (line "主角" "所以你才躲到我这里。")
                (line "夜莺" "所以我再不敢跑。我一跑,那顶帽子就真扣实了。")
                (line "主角" "那就跟他们碰一场。")
                (line "夜莺" "他们不是一个人来的。你要碰,也别一个人碰——码头、警局,能拉一个是一个。")
                (line "夜莺" "可我还是想自己凑一份钱。不图别的,不想让你把命也押在我这笔账上。"))
              (play-dialogue!
                (line "夜莺" "有件事,我一直没跟你说。")
                (line "夜莺" "逃走那晚,老板的心腹追到码头,自己踩空落了水。")
                (line "夜莺" "老板咬定是我推的。这就不是一笔账了,是一条命。")
                (line "主角" "那你更回不得。")
                (line "夜莺" "所以我才不该把这笔账全压在你身上。")
                (line "主角" "真要碰上,我们就跟他们碰一场。")
                (line "夜莺" "碰得过更好。碰不过,谁都没有回头路。钱我也会去凑——不是非它不可,是不想让你一个人扛。")))
          (set-flag! '三层已揭)
          (rest-release! "夜莺/第三层揭开")
          (spotlight! "水里的真相" "她推没推,不重要。老板要的是一个偿命的人。而这个人,现在也可以是你。"))))

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
                (line "主角" "船期对不上。那晚你根本没在等船。")
                (line "夜莺" "……他抓住我的手腕,往船上拖。我甩开了。他自己没站稳。")
                (line "主角" "你早该告诉我。")
                (line "夜莺" "告诉你,你就不管了?"))
              (finish-lie-reveal!)))
          (instant-action "换我也一样推"
            (lambda ()
              (play-dialogue!
                (line "主角" "船期对不上。那晚你根本没在等船。")
                (line "夜莺" "……他抓住我的手腕,往船上拖。我甩开了。他自己没站稳。")
                (line "主角" "换我也一样推。")
                (line "夜莺" "……谢谢你这么说。哪怕是骗我。"))
              (finish-lie-reveal!))))))

    ;; ── 状态卡 ──────────────────────────────────────
    (define (situation-text)
      (cond
        ((= story-stage 0) "还没有发生什么。")
        ((= story-stage 1) "一个叫夜莺的歌女,在雨夜敲开了你的门。她撂下一笔预付钱,要你查清是谁在盯她的梢。")
        ((= story-stage 2) "收账人把账算到了你头上。夜莺认得他们:她从邻城歌厅逃出来,对方要一笔赎身钱。第十天前,可以替她垫一笔首期赎身钱,也可以请阿瑟把这件事提到巡警会出面的优先级。")
        ((= story-stage 3)
         (string-append
           "那晚,码头的水吞了一个人。老板一口咬定是夜莺推的——这不只是一笔账,是一条命。"
           (ransom-hint)))
        ((= story-stage 90) "她留在了这座城。真名,只在你耳边轻轻说过一次。")
        ((= story-stage 91) "她跟他们走了,拿自己抵了你一条命。酒馆的台子上,再没人开嗓。")
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
        ((= story-stage 96) "你在饭店坐到很晚。第二天,酒馆的门关着,没人跟你说发生了什么——你也没问。")
        ((= story-stage 97) "她跟着巡警走了。萨姆带着完整案卷，也带着亨利曾经活过的证词。")
        (else "……")))

    (define (nightingale-subtitle)
      (cond
        ((= story-stage 1) "受托查访")
        ((= story-stage 2)
         (if (equal? protection "无")
             (string-append (condition-label) "；等一个临时保护")
             (string-append (condition-label) "；已有保护：" protection)))
        ((= story-stage 3) (string-append (condition-label) "；等一个了断"))
        ((= story-stage 90) "留在城里")
        ((= story-stage 91) "被带走")
        ((= story-stage 93) "已平安离开")
        ((= story-stage 94) "带伤扛下了")
        ((= story-stage 95) "案子压着")
        ((= story-stage 96) "没有回头")
        ((= story-stage 97) "随案移交")
        (else "雨夜来客")))

    (define (stage3-goal-text)
      (if (route-settled?)
          (cond
            (hush-paid? "封口钱已经付清。第 17 天,他会当面烧掉那纸约。")
            (farewell? "她已经上船。第 17 天,账不会跟着她走,只会找上你。")
            (case-filed? "案子已经立起来。第 17 天,来的会是巡警,不是老板。")
            (surrendered? "完整真相已经交给萨姆。第 17 天,她会随案离开。")
            (else ""))
          (string-append
            "第 17 天前,这件事要有个了断——不作任何准备,任由那天当面碰上,就是硬碰硬；"
            "也可以花钱买断:封口总价 " (number->string (hush-total)) " 金,夜莺已凑 "
            (number->string nightingale-earnings) "，你还需 " (number->string (hush-due)) "；"
            (if berth? "舱位已经弄到,第 16 天前可以送她上船；" "也可以弄条船送她走,自己扛下这笔账；")
            (if (truth-known?)
                (string-append (case-route-note) "；你也可以什么都不做。")
                "。"))))

    (define (case-route-note)
      (cond
        ((= (sam 'stage) 0) "立案送警：去警局外见萨姆")
        ((not (equal? (sam 'evidence) "老板")) "立案送警：把老板那一半证据交给萨姆")
        ((not (arthur 'can-escalate?))
         "立案送警：先替阿瑟处理那件程序管不了的麻烦")
        ((not (case-ready?))
         "立案送警：去警局拼案卷，再带通行证请阿瑟送进程序")
        ((= (item-count "办案通行证") 0)
         "立案送警：先请阿瑟办理一张通行证")
        (else "立案送警：带通行证请阿瑟把案子送进程序")))

    (define (goal-note)
      (cond
        ((= story-stage 0) '())
        ((>= story-stage 90) '())
        (else
         (list (list 'clock "夜莺的目标" 0 1 'countdown
                     (cond
                       ((= story-stage 1)
                        (string-append "第 3 天他会上门。在那之前完成饭店与码头的查访，对陌生人的了解("
                                       (number->string (stranger-understanding)) "/"
                                       (number->string stranger-understanding-target) ")。"))
                       ((= story-stage 2)
                        (if (equal? protection "无")
                            (string-append "第 10 天他们会来。先把首期赎身钱 "
                                           (number->string first-installment)
                                           " 交给夜莺,至少能让他们收手一次。")
                            (string-append "第 10 天他们会来。已落实保护: " protection "。")))
                       ((= story-stage 3) (stage3-goal-text))
                       (else "")))))))

    (define (beat1-clock)
      (if (= story-stage 1)
          (list (list 'clock "对陌生人的了解" (stranger-understanding) stranger-understanding-target 'segments
                      "饭店与码头的查访 Clock 每完成一条，增加 1 格；满格后揭晓藏身处。"))
          '()))

    (define (protection-clock)
      (if (= story-stage 2)
          (list (list 'clock "临时保护"
                      (if (equal? protection "无") 0 1)
                      1
                      'segments
                      (if (equal? protection "无")
                          "落实任一保护方案后,第 10 天会以场景结算,不会进入抢人交锋。"
                          (string-append "已落实: " protection))))
          '()))

    (define (condition-clock)
      (if (>= story-stage 1)
          (list (list 'clock (string-append "夜莺的不安：" (condition-label))
                      condition-level 3 'segments
                      "主动出击不会增加；等他上门，无论交锋成败都会增加 1。后续小节失败还会继续恶化。"))
          '()))

    (define (truth-clock)
      (if (and (stage3-open?) (truth-pending?))
          (list (list 'clock "那晚码头上发生了什么" truth-progress truth-target 'segments
                      "先在码头、警局或货运公司混出门路,才能找到人、调到卷宗或翻到旧记录。查不查,都能走到第 17 天。"))
          '()))

    (define (nightingale-earning-clock)
      (if (and (stage3-open?) (not (route-settled?)))
          (list (list 'clock "夜莺凑出的封口钱" nightingale-earnings hush-contribution-target 'countdown
                      "她每天都在接活凑钱。她凑到的部分会直接抵掉你要付的封口钱。"))
          '()))

    (define (truth-lead-clock)
      (if (and (stage3-open?) (not (truth-lead?)) (not (route-settled?)))
          (list (list 'clock "她没说完的那一夜" 0 1 'countdown
                      (if (>= world-day (+ stage3-start-day 2))
                          "有人跟着你。截住他，看看他到底想卖什么。"
                          "夜莺的说辞有些含混。先把眼前的账和日子过下去。")))
          '()))

    ;; 夜莺当前情境下能做的关键动作。开场动作只供客户端在新游戏时自动执行。
    (define (situation-nodes)
      (append
        (if (and (= story-stage 2) (not (has-flag? '二层已揭)))
            (list (node-reveal-layer-2))
            '())
        (if (and (= story-stage 3) (not (has-flag? '三层已揭)))
            (list (node-reveal-layer-3))
            '())
        (if (and (stage3-open?) (truth-lead?) (not (truth-pending?)) (not (has-flag? '撒谎的人)))
            (list (node-reveal-lie))
            '())
        (route3-nodes)
        (letter-nodes)))

    ;; 夜莺是一个「可进入的容器」：进去才是与她的各种互动。
    ;; 处境正文做成一张 observe 子卡（她的处境）放在最上面——不能给容器本身加
    ;; :resolve，否则它会退化成动作、children 全部失效（见 SCRIPTING.md 节点形状约束）。
    (define (render-data)
      (if (>= story-stage 1)
          (list (node "夜莺"
                 :subtitle (nightingale-subtitle)
                 :children (append
                             (list (observe-action "她的处境" (situation-text)))
                             (situation-nodes)
                             (stage2-world-nodes)
                             (route1-nodes))
                 :clocks (append (goal-note) (beat1-clock) (protection-clock)
                                 (condition-clock) (nightingale-earning-clock) (truth-lead-clock) (truth-clock))))
          '()))

    ;; ── 节拍二：落实临时保护 ────────────────────────
    (define (node-pay-installment)
      (node "交首期赎身钱"
        :subtitle (string-append "交给她 " (number->string first-installment)
                                 " 金；她知道该把钱送到谁手上")
        :requires (list (req-item "金钱" first-installment))
        :resolve (instant
          (outcome "首期交出" "她收下钱,没道谢。'我知道该送到谁手上。第十天以前,他们总得先认这笔账。'"
            (lambda () (set-protection! "首期"))))))

    ;; 阿瑟的人物线独立于主线；节拍二只读取已经建立的关系。
    (define (node-police-protection)
      (node "请阿瑟把夜莺的事提级"
        :subtitle (string-append "辖区警局的登记与档案职员；"
                    (if (arthur 'can-escalate?)
                        "欠你一次程序内的方便，能让巡警第十天守在酒馆外"
                        "还不会为你的事改动警局的优先级"))
        :disabled (not (arthur 'can-escalate?))
        :requires (list (req-die))
        :resolve (instant
          (outcome "事情被提级" "阿瑟把登记单挪进另一叠文件。第十天，酒馆门外会有穿制服的人。"
            (lambda () (set-protection! "警局"))))))

    (define (stage2-world-nodes)
      (if (stage2-open?)
          (list (node-pay-installment))
          '()))

    (define (resolve-protected-beat2!)
      (cond
        ((equal? protection "首期")
         (spotlight! "首期" "收账人来了,也收住了手。他点了点那笔首期:'老板要亲自来做个了断。'"))
        ((equal? protection "警局")
         (spotlight! "巡警在场" "收账人看见巡警在街角站着,把话咽了回去。'好。那就等老板亲自来。'"))
        (else
         (error "resolve-protected-beat2!: no protection"))))

    ;; ── 节拍三·路线一：付封口钱 ────────────────────
    (define (node-hush-payment)
      (node "付封口钱了断"
        :subtitle (string-append "封口总价 " (number->string (hush-total)) "，夜莺已凑 "
                                 (number->string nightingale-earnings) "；你付 " (number->string (hush-due))
                                 " 金，买断的不只是卖身约,还有他们和警察都不再追查这件事"
                                 (if installment-paid? "（首期他们认过账，这个数已经打了折）" ""))
        :requires (list (req-die) (req-item "金钱" (hush-due)))
        :resolve (instant
          (outcome "钱送出去了" "钱送出去了。第 17 天,他会当面烧掉那纸约。"
            (lambda () (set! hush-paid? #t) (sync-globals!))))))

    (define (route1-nodes)
      (if (and (stage3-open?) (not (route-settled?)))
          (list (node-hush-payment))
          '()))

    ;; ── 节拍三·路线三：送她走，自己扛 ────────────────
    (define (node-insurance-berth)
      (node "通过保险公司安排舱位"
        :subtitle "沃尔特是保险公司的理赔调查员；夜莺本人名下的紧急转移条款，80金"
        :requires (list (req-die) (req-item "金钱" berth-price-insurance))
        :resolve (instant
          (outcome "公司舱位办妥" "沃尔特把盖过章的船票递给你。投保人和受益人那两栏，写的都是夜莺自己。"
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
                  (line "主角" "一路平安。")
                  (line "夜莺" "……我记住你了。真的记住了。"))
                (play-dialogue!
                  (line "夜莺" "跳板要收了。")
                  (line "夜莺" "……不必送我。")
                  (line "夜莺" "这个,你留着。想起我的时候,就想我一直好好的,也一直记挂着你。")))
            (set! farewell? #t)
            (sync-globals!)
            (spotlight! "船开了"
              (if (truth-known?)
                  "船开了。第 17 天上门的人,由你一个人接。"
                  "她把戒指塞回你手里,船开了。第 17 天上门的人,由你一个人接。"))))))

    (define (route3-nodes)
      (append
        (if (and (stage3-open?) berth? (<= world-day 16) (not (route-settled?)))
            (list (node-farewell))
            '())))

    ;; ── 节拍三·路线四：立案送警 ────────────────────
    (define (node-case-route-locked)
      (node "立案送警"
        :subtitle "阿瑟是辖区警局的登记与档案职员；他还不会把跨城命案送进正式程序"
        :tags (list "需要阿瑟·熟")
        :disabled #t))

    (define (node-build-case)
      (action "把那晚的案卷拼起来"
        (list (req-die))
        (roll 'knowledge
          (outcome "拼不出头绪" "线索太散,拼不出一份站得住的案卷。" (lambda () (spend-composure! 1)))
          (outcome "拼出个大概" "你把零碎的线索理成了一份说得过去的案卷。" (lambda () (set-flag! '案卷备妥)))
          (outcome "拼得严丝合缝" "案卷拼得严丝合缝,连日期都对得上。" (lambda () (set-flag! '案卷备妥))))))

    (define (node-file-case)
      (node "请阿瑟把案子送进程序"
        :subtitle "辖区警局的登记与档案职员；通行证是由头，人情让材料不会停在收件桌上"
        :tags (if (> (item-count "办案通行证") 0) '() (list "需要办案通行证"))
        :requires (list (req-item "办案通行证" 1))
        :resolve (instant
          (outcome "案子立起来了" "阿瑟收下通行证，把材料放进提级案卷。老板那一半从此不能再被当作普通纠纷。"
            (lambda () (set! case-filed? #t) (sync-globals!))))))

    ;; ── 节拍三·到期日分派 ───────────────────────────
    (define (node-hush-payoff-pending)
      (instant-action "看他烧掉那纸约"
        (lambda ()
          (play-dialogue!
            (line "世界" "他把那纸卖身约凑到烛火上。纸卷起来,黑掉,碎成灰。")
            (line "世界" "'账,清了。'他说完,转身走进雨里。"))
          (on-public-event-result 'success))))

    (define (node-case-filed-pending)
      (instant-action "看着巡警上门"
        (lambda ()
          (play-dialogue!
            (line "世界" "敲门的不是老板,是两个巡警。他们只问了几句,记了几笔,就走了。")
            (line "世界" "老板那一半,压不住了。她那一半,没人再提。"))
          (on-public-event-result 'success))))

    (define (node-surrender-pending)
      (instant-action "看着她跟巡警走"
        (lambda ()
          (play-dialogue!
            (line "世界" "第十七天，来的是萨姆和两名巡警。没有镣铐，只有一只装着案卷的牛皮袋。")
            (line "夜莺" "我会把话说完。亨利的妹妹，也该听见一个不是老板编的说法。")
            (line "世界" "她走进雨里，没有回头。萨姆把伞往她那边偏了一点。"))
          (on-public-event-result 'surrendered))))

    (define (node-encounter-entry)
      (encounter-action (public-event-action-name)
        (lambda ()
          (start-encounter "夜莺·了断" on-public-event-result))))

    (define (node-walk-away-pending)
      (node "去饭店喝酒"
        :subtitle "你不去,今晚就没人站在她那边"
        :resolve (instant
          (lambda ()
            (play-dialogue!
              (line "世界" "你坐在饭店的角落,酒喝得很慢。你没有去。"))
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
          (spotlight! "远方的信" (string-append "信里没有署名,夹着 " (number->string letter-money) " 金。")))))

    (define (letter-nodes)
      (if (and (or (= story-stage 93) (= story-stage 94))
               (>= world-day (+ ending-day letter-delay))
               (not (has-flag? '结局信)))
          (list (node-open-letter))
          '()))

    ;; ── 注入世界根的紧急节点 ────────────────────────
    ;; 开场敲门与节拍三的跟梢者挂在世界根：两者都不是夜莺本人能主动给出的动作。
    (define (node-catch-stalker)
      (action "截住跟梢的人"
        (list (req-die))
        (roll 'sharpness
          (lambda () (spend-composure! 1))
          (lambda ()
            (set-flag! '跟梢的人)
            (spotlight! "一张名片" "他钻进人群前丢下一张名片。背面只写着一句：夜莺的事，五十金。"))
          (lambda ()
            (set-flag! '跟梢的人)
            (play-dialogue!
              (line "主角" "跟了两条街，够了。")
              (line "世界" "那人把领口竖高：我不是来找你麻烦的。我手里有夜莺那晚的东西。五十金，买不买随你。"))
            (spotlight! "有人卖消息" "他知道得太多，又说得太少。五十金，买一条会咬人的线。")))))

    (define (node-buy-truth-lead)
      (node "买下关于夜莺的消息"
        :subtitle "花 50 金。他说，码头有个老人、邻城有份案卷、货栈留着那晚的船期"
        :tags (list "消息价 50 金")
        :requires (list (req-item "金钱" rumor-price))
        :resolve (instant
          (outcome "水面下的线" "他收了钱，只留下一句：去问该问的人，翻该翻的纸。别指望夜莺替你把路指出来。"
            (lambda ()
              (set-flag! '夜莺的消息)
              (sync-globals!))))))

    (define (world-nodes)
      (append
        (if (= story-stage 0) (list (node-answer-door)) '())
        (if (and (stage3-open?) (not (truth-lead?)) (not (has-flag? '跟梢的人))
                 (>= world-day (+ stage3-start-day 2)))
            (list (node-catch-stalker))
            '())
        (if (and (stage3-open?) (has-flag? '跟梢的人) (not (truth-lead?)))
            (list (node-buy-truth-lead))
            '())))

    (define-turn-rule "夜莺凑封口钱"
      (lambda ()
        (and (stage3-open?) (not (route-settled?))
             (< nightingale-earnings hush-contribution-target)
             (not public-event-pending?)))
      (lambda ()
        (set! nightingale-earnings
              (min hush-contribution-target (+ nightingale-earnings nightingale-daily-earning)))
        (notify! "夜莺又接了一晚的活，把凑到的钱放在桌上。她说，这样你就不必事事都自己扛。")))

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

    (define (node-diner-inquire-stalker)
      (node "在饭店打听陌生人"
        :subtitle "向不认识他的食客拼凑外貌、口音和习惯"
        :tags (list "低风险")
        :clocks (list (list 'clock "饭店里的说法" diner-inquiry-progress beat1-location-target 'segments
                            "填满后，对陌生人的了解增加 1 格。"))
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "问得太急" "你把几桌客人问得起了戒心，只能先退开。"
            (lambda () (spend-composure! 1)))
          (outcome "拼出轮廓" "有人记得他的外套，有人记得他的口音。零碎说法开始对得上。"
            (lambda () (advance-beat1-location! "饭店" 1)))
          (outcome "认出那张脸" "一个跑堂见过他几次，把他的作息和常坐的位置都说清楚了。"
            (lambda () (advance-beat1-location! "饭店" 2))))))

    (define (node-dock-inquire-stalker)
      (node "在码头打听陌生人"
        :subtitle "向船员和搬运工追查他从哪里来、把东西送去哪里"
        :tags (list "低风险")
        :clocks (list (list 'clock "码头上的来路" dock-inquiry-progress beat1-location-target 'segments
                            "填满后，对陌生人的了解增加 1 格。"))
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "没人愿意开口" "码头上的人看了看你，又看了看彼此。你只换来一阵沉默。"
            (lambda () (spend-composure! 1)))
          (outcome "查到船期" "搬运工记得那张脸：他是坐夜船来的，而且没打算久留。"
            (lambda () (advance-beat1-location! "码头" 1)))
          (outcome "追到去向" "水手说出了他下船后的路线，终点是老街后面一间短租屋。"
            (lambda () (advance-beat1-location! "码头" 2))))))

    (define (beat1-nodes-at location)
      (cond
        ((and (equal? location "饭店") (= story-stage 1)
              (< diner-inquiry-progress beat1-location-target))
         (list (node-diner-inquire-stalker)))
        ((and (equal? location "码头") (= story-stage 1)
              (< dock-inquiry-progress beat1-location-target))
         (list (node-dock-inquire-stalker)))
        (else '())))

    (define (hideout-nodes)
      (if (hideout-visible?) (list (node-confront-stalker)) '()))

    ;; ── 节拍三：暗账查访「那晚码头上发生了什么」──────
    ;; 复用节拍一的查访语法：三个来源横跨三条生活线，满格触发必看的「撒谎的人」。
    (define (node-dock-truth)
      (define (dock-truth-mods)
        (if (relation-at-least? "劳工" '相识)
            (list (modifier 1 "码头的人认得你"))
            '()))
      (node "问那晚在码头的老人"
        :subtitle (if (relation-at-least? "劳工" '相识)
                      "码头的人认得你,才肯带你去见那个还记得栈桥的人"
                      "得先在码头混个面熟,才有人肯把你带去见他")
        :tags (if (relation-at-least? "劳工" '相识) '() (list "需要劳工·相识"))
        :disabled (not (relation-at-least? "劳工" '相识))
        :requires (list (req-die))
        :resolve (roll 'social dock-truth-mods
          (lambda () (spend-composure! 1))
          (lambda () (advance-truth! "争执声" "老人说,那晚栈桥上有争执声,压得很低。"))
          (lambda () (add-item! "情报" 1) (advance-truth! "栈桥上的女人" "他记得那晚栈桥上,有个女人。")))))

    (define (node-police-truth)
      (node "调邻城的案卷抄件"
        :subtitle (string-append "阿瑟是辖区警局的登记与档案职员；"
                    (if (relation-at-least? "官僚" '相识)
                        "肯替你调一张抄件，但得你自己找出破绽"
                        "得先在警局登记，他才会替你调邻城旧卷"))
        :tags (if (relation-at-least? "官僚" '相识) '() (list "需要官僚·相识"))
        :disabled (not (relation-at-least? "官僚" '相识))
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (lambda () #f)
          (lambda () (advance-truth! "压着没立" "案卷抄件写着:案子在邻城压着,一直没正式立。"))
          (lambda () (advance-truth! "腕上的抓痕" "验尸那栏写着一句:死者手腕上,有抓痕。")))))

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
        :requires (list (req-die))
        :resolve (roll 'knowledge freight-truth-mods
          (lambda () (spend-composure! 1))
          (lambda () (advance-truth! "对不上的船期" "货栈记录里,那晚根本没有她说的那班船。"))
          (lambda () (advance-truth! "查无此船" "船期表翻了三遍:那晚,压根没有那班船。")))))

    ;; ── 幕间场景(酒馆) ──────────────────────────────
    (define (node-old-ring)
      (container "旧戒指"
        (list
          (instant-action "收下"
            (lambda ()
              (set-flag! '旧戒指)
              (add-item! "金钱" ring-value)
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
           (if (has-flag? '旧戒指)
               "酒馆的灯还亮着,台上却空了,再没人开嗓。那枚旧戒指的来历,如今只剩你一个人记得。"
               "酒馆的灯还亮着,台上却空了,再没人开嗓。")))
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
           "台上空着。她不是被老板带走的——可她也没有被任何人的沉默留下。"))
        (else #f)))

    (define (tavern-nodes)
      (append
        (if (and (= story-stage 3) (>= world-day 12) (not (has-flag? '旧戒指)))
            (list (node-old-ring))
            '())
        (if (ending-tavern-node)
            (list (ending-tavern-node))
            '())))

    ;; ── 跨地点节点收拢（警局/码头/货运公司）────────────
    ;; 三个地点文件只认「我在哪、这批节点该插在列表的哪个槽位」，
    ;; 可见性判断全部收回夜莺自己算——谁拥有状态，谁决定这段状态驱动
    ;; 的节点该不该出现在别人的地盘上。分成 lead（查访盯梢/临时保护，
    ;; 节拍一二）和 route（暗账查访与了断路线，节拍三）两槽，
    ;; 对应各地点原本把这两批内容分开插入列表两处的顺序。
    (define (lead-nodes-at location)
      (cond
        ((equal? location "警局")
         (if (stage2-open?) (list (node-police-protection)) '()))
        (else '())))

    (define (route-nodes-at location)
      (cond
        ((equal? location "码头")
         (if (and (stage3-open?) (truth-lead?) (truth-pending?) (not (route-settled?)))
             (list (node-dock-truth))
             '()))
        ((equal? location "警局")
         (append
           (if (and (stage3-open?) (truth-lead?) (truth-pending?) (not (route-settled?)))
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
         (if (and (stage3-open?) (truth-lead?) (truth-pending?) (not (route-settled?)))
             (list (node-freight-truth))
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
             #f)
         (advance-stage! 2)
         (sync-blockers!))
        ((= bout 1)
         (if (equal? result 'fail)
             (begin
               (worsen-condition! 2)
               (old-street-tavern 'set-closed! 2))
             #f)
         (advance-stage! 3)
         (set! stage3-start-day world-day)
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
          ((equal? msg 'lead-nodes-at) (lead-nodes-at (cadr args)))
          ((equal? msg 'route-nodes-at) (route-nodes-at (cadr args)))
          ((equal? msg 'story-stage) story-stage)
          ((equal? msg 'truth-progress) truth-progress)
          ((equal? msg 'truth-lead?) (truth-lead?))
          ((equal? msg 'truth-known?) (truth-known?))
          ((equal? msg 'route-settled?) (route-settled?))
          ((equal? msg 'set-surrendered!)
           (begin
             (if (or (not (truth-known?)) (route-settled?))
                 (error "夜莺完整移交：当前状态不允许") #t)
             (set! surrendered? #t)
             (sync-globals!)))
          ((equal? msg 'has-protection?) (not (equal? protection "无")))
          ((equal? msg 'resolve-protected-beat2!) (resolve-protected-beat2!))
          ((equal? msg 'sync-blockers!) (sync-blockers!))
          ((equal? msg 'on-bout-result) (on-bout-result (cadr args) (caddr args)))
          ((equal? msg 'beat3-pending-nodes) (beat3-pending-nodes))
          ((equal? msg 'save)
           (list
             (list "story-stage" story-stage)
             (list "condition-level" condition-level)
             (list "diner-inquiry-progress" diner-inquiry-progress)
             (list "dock-inquiry-progress" dock-inquiry-progress)
             (list "beat1-early?" beat1-early?)
             (list "protection" protection)
             (list "installment-paid?" installment-paid?)
             (list "truth-progress" truth-progress)
             (list "berth?" berth?)
             (list "farewell?" farewell?)
             (list "hush-paid?" hush-paid?)
             (list "case-filed?" case-filed?)
             (list "surrendered?" surrendered?)
             (list "ending-day" ending-day)
             (list "stage3-start-day" stage3-start-day)
             (list "nightingale-earnings" nightingale-earnings)
             (list "scene-flags" scene-flags)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! story-stage (assoc-get data "story-stage" 0))
             (set! condition-level (assoc-get data "condition-level" 0))
             (if (or (< condition-level 0) (> condition-level 3))
                 (error "夜莺存档错误：状态等级非法")
                 #t)
             (set! diner-inquiry-progress (assoc-get data "diner-inquiry-progress" 0))
             (set! dock-inquiry-progress (assoc-get data "dock-inquiry-progress" 0))
             (set! beat1-early? (assoc-get data "beat1-early?" #f))
             (set! protection (assoc-get data "protection" "无"))
             (set! installment-paid? (assoc-get data "installment-paid?" #f))
             (set! truth-progress (assoc-get data "truth-progress" 0))
             (set! berth? (assoc-get data "berth?" #f))
             (set! farewell? (assoc-get data "farewell?" #f))
             (set! hush-paid? (assoc-get data "hush-paid?" #f))
             (set! case-filed? (assoc-get data "case-filed?" #f))
             (set! surrendered? (assoc-get data "surrendered?" #f))
             (set! ending-day (assoc-get data "ending-day" 0))
             (set! stage3-start-day (assoc-get data "stage3-start-day" world-day))
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
