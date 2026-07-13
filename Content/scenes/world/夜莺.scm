;; scenes/world/夜莺.scm - 夜莺最小主线故事模块
;; 拥有 story-stage / condition-level / scene-flags 全部故事状态,通过消息接口与世界协调。

(define nightingale
  (let ()
    ;; ── 常量 ────────────────────────────────────────
    (define prepayment 30)
    (define ransom 150)
    (define first-installment 60)
    (define ring-value 15)
    (define beat1-target 4)

    ;; ── 状态 ────────────────────────────────────────
    (define story-stage 0)
    ;; 0=未开场 1=受托查探 2=账转你头(二层已揭) 3=知道真相(三层已揭)
    ;; 90=了断·成功 91=了断·她被带走 92=中途放她走
    (define condition-level 0)
    ;; 0=稳定 1=不安 2=恐惧 3=被迫转移
    (define beat1-progress 0)
    ;; 查访进度 0..4。第一版不限制同一来源重复贡献，试玩后再决定是否收紧。
    (define beat1-early? #f)
    (define protection "无")
    (define installment-paid? #f)
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
        ((or (equal? flag '软选择) (equal? flag "软选择")) "软选择")
        ((or (equal? flag '留下) (equal? flag "留下")) "留下")
        ((or (equal? flag '旧戒指) (equal? flag "旧戒指")) "旧戒指")
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

    (define (sync-globals!)
      (set-global! '夜莺阶段 story-stage)
      (set-global! '夜莺状态等级 condition-level)
      (set-global! '夜莺状态 (condition-label))
      (set-global! '夜莺查访进度 beat1-progress)
      (set-global! '夜莺查访目标 beat1-target)
      (set-global! '夜莺主动上门 beat1-early?)
      (set-global! '夜莺保护方案 protection)
      (set-global! '夜莺已打听 (has-flag? '已打听)))

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

    (define (asset-at-least-apartment?)
      (equal? (get-global '资产) "中"))

    (define (ransom-hint)
      (if installment-paid?
          (string-append "首期他们认了账,尾款只剩 " (number->string (- ransom first-installment)) "。")
          ""))

    (define (advance-beat1! label detail)
      (set! beat1-progress (min beat1-target (+ beat1-progress 1)))
      (set-flag! '已打听)
      (sync-globals!)
      (spotlight! label detail))

    (define (beat1-ready?)
      (and (= story-stage 1)
           (>= beat1-progress beat1-target)
           (not public-event-pending?)))

    ;; ── 阻塞同步 ────────────────────────────────────
    (define (sync-blockers!)
      (cond
        ((= story-stage 0)
         (rest-block! "夜莺/开场敲门" "有人在敲门,先去看看是谁。"))
        ((and (= story-stage 2) (not (has-flag? '二层已揭)))
         (rest-block! "夜莺/第二层揭开" "她在等你回来。"))
        ((and (= story-stage 3) (not (has-flag? '三层已揭)))
         (rest-block! "夜莺/第三层揭开" "她伤着,有话要说。"))
        (else
         (begin
           (rest-release! "夜莺/开场敲门")
           (rest-release! "夜莺/第二层揭开")
           (rest-release! "夜莺/第三层揭开")))))

    ;; ── 开场节点 ────────────────────────────────────
    (define (node-answer-door)
      (instant-action "回应敲门声"
        (lambda ()
          (play-dialogue!
            (line "回应敲门声" "门外雨很大。我把预付金放在桌上了,你查清楚是谁在盯我的梢。")
            (line "主角" "你是谁?")
            (line "回应敲门声" "他们都叫我夜莺。死鸟和字条也是他们给的——先开门,好吗?"))
          (add-item! "金钱" prepayment)
          (advance-stage! 1)
          (rest-release! "夜莺/开场敲门")
          (spotlight! "雨夜来客" "你收了钱。这不是委托,是一根把你拖进深水里的线。四天之内,先查出盯梢的人落脚在哪。"))))

    ;; ── 第二层揭开(交锋一后,必看) ──────────────────
    (define (node-reveal-layer-2)
      (instant-action "听她解释"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "我不是逃债。我签了十年卖身约,跑了七年。")
            (line "夜莺" "他们找我,要么带我回去,要么收一笔赎身钱。")
            (line "主角" "多少?")
            (line "夜莺" "一百五。对你来说也是天价。"))
          (set-flag! '二层已揭)
          (rest-release! "夜莺/第二层揭开")
          (spotlight! "一笔人的价钱" "一百五。这个数字现在写在你的账本上,也写在她的命上。"))))

    ;; ── 第三层揭开(交锋二后,必看) ──────────────────
    (define (node-reveal-layer-3)
      (instant-action "听她说话"
        (lambda ()
          (if (>= condition-level 2)
              (play-dialogue!
                (line "夜莺" "那晚在码头,老板的心腹追我,自己失足滑下去。")
                (line "夜莺" "他淹死了。老板咬定是我推的。")
                (line "主角" "所以你才躲到我这里。")
                (line "夜莺" "所以我才不敢再跑。我跑了,就真成凶手了。"))
              (play-dialogue!
                (line "夜莺" "我有件事没告诉你。")
                (line "夜莺" "逃走那晚,老板的心腹追到码头,失足落水死了。")
                (line "夜莺" "老板咬定是我推的。这不是一笔账,是一条命。")
                (line "主角" "那你更不能回去。")
                (line "夜莺" "所以我才要你了断这件事。")))
          (set-flag! '三层已揭)
          (rest-release! "夜莺/第三层揭开")
          (spotlight! "水里的真相" "无论她推没推,老板都需要一个人来偿命。现在这个人也可以是你。"))))

    ;; ── 状态卡 ──────────────────────────────────────
    (define (situation-text)
      (cond
        ((= story-stage 0) "还没有发生什么。")
        ((= story-stage 1) "一个叫夜莺的歌女在雨夜敲了你的门。她预付了一笔钱,要你查清盯梢的人。")
        ((= story-stage 2) (if (has-flag? '留下)
                               "收账人把账转到了你头上。她本来可以走,却因为你的一句话留了下来。第十天前,最直接的办法是交给她一笔首期赎身钱。没有钱,也许可以找一个安全住处,或者让警局、码头的人在酒馆附近露面。"
                               "收账人把账转到了你头上。夜莺知道他们是谁:她从邻城歌厅逃出来,对方要赎身钱。第十天前,最直接的办法是交给她一笔首期赎身钱。没有钱,也许可以找一个安全住处,或者让警局、码头的人在酒馆附近露面。"))
        ((= story-stage 3)
         (string-append
           (if (has-flag? '留下)
               "那晚码头有人落水死了。她留下了,但麻烦还没有结束。"
               "那晚码头有人落水死了。老板咬定是夜莺推的——这不只是一笔账,是一条命。")
           (ransom-hint)))
        ((= story-stage 90) "她留在了这座城。真名只在你耳边说了一次。")
        ((= story-stage 91) "她跟他们走了,换你一条命。酒馆再没有人唱歌。")
        ((= story-stage 92) "你让她走了。欠她的和没欠她的,都再没法还。")
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
        ((= story-stage 92) "已离开")
        (else "雨夜来客")))

    (define (goal-note)
      (if (= story-stage 0)
          '()
          (list (list 'clock "夜莺的目标" 0 1 'countdown
                      (cond
                        ((= story-stage 1)
                         (string-append "第 4 天他会上门。在那之前查出他的落脚处("
                                        (number->string beat1-progress) "/"
                                        (number->string beat1-target) ")。"))
                        ((= story-stage 2)
                         (if (equal? protection "无")
                             (string-append "第 10 天他们会来。先把首期赎身钱 "
                                            (number->string first-installment)
                                            " 交给夜莺,至少能让他们收手一次。")
                             (string-append "第 10 天他们会来。已落实保护: " protection "。")))
                        ((= story-stage 3)
                         (string-append "在了断之日前:凑够赎身钱,或让这座城站在你这边,或找到官面上的办法。他们要的是一条命的交代——这次不能输。"
                                        (ransom-hint)))
                        (else ""))))))

    (define (beat1-clock)
      (if (= story-stage 1)
          (list (list 'clock "查访盯梢者" beat1-progress beat1-target 'segments
                      "填满后可以主动去找收账人；未查清则第 4 天他会找上门。"))
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
          (list (list 'clock (string-append "夜莺状态：" (condition-label))
                      condition-level 3 'segments
                      "前面的小节失利会让她更不安。最终路线会读取这个状态,调整成本、风险或可用性。"))
          '()))

    ;; 夜莺当前情境下能做的关键动作。原先这些浮在世界根（world-nodes），
    ;; 现并入夜莺自身——同一个夜莺，情境不同则可做的动作不同。
    ;; 「回应敲门声」仍留在世界根：stage 0 时夜莺节点尚未出现，无处可挂。
    (define (situation-nodes)
      (append
        (if (beat1-ready?) (list (node-confront-stalker)) '())
        (if (and (= story-stage 2) (not (has-flag? '二层已揭)))
            (list (node-reveal-layer-2))
            '())
        (if (and (= story-stage 3) (not (has-flag? '三层已揭)))
            (list (node-reveal-layer-3))
            '())))

    ;; 夜莺是一个「可进入的容器」：进去才是与她的各种互动。
    ;; 处境正文做成一张 observe 子卡（她的处境）放在最上面——不能给容器本身加
    ;; :resolve，否则它会退化成动作、children 全部失效（见 SCHEMY.md 节点形状约束）。
    (define (render-data)
      (if (>= story-stage 1)
          (list (node "夜莺"
                 :subtitle (nightingale-subtitle)
                 :children (append
                             (list (observe-action "她的处境" (situation-text)))
                             (situation-nodes)
                             (stage2-world-nodes))
                 :clocks (append (goal-note) (beat1-clock) (protection-clock) (condition-clock))))
          '()))

    ;; ── 节拍一：花消息买线索 ──────────────────────
    ;; 这是「向酒馆老主顾买准话」，与夜莺本人无关，因此挂在老街酒馆（beat1-lead-nodes），
    ;; 不进夜莺容器。只在 beat 1、且手里有情报可花时出现。
    (define (node-buy-lead)
      (node "花消息买线索"
        :subtitle "手里的消息换一句准话；不占行动骰"
        :requires (list (req-item "情报" 1))
        :resolve (instant
          (lambda ()
            (advance-beat1! "买来的准话" "你把消息递给一个老主顾。他压低声音回你一句:外地人这几天在老街进出。")))))

    (define (beat1-lead-nodes)
      (if (and (= story-stage 1)
               (< beat1-progress beat1-target)
               (> (item-count "情报") 0))
          (list (node-buy-lead))
          '()))

    ;; ── 节拍二：落实临时保护 ────────────────────────
    (define (node-pay-installment)
      (node "交首期赎身钱"
        :subtitle (string-append "交给她 " (number->string first-installment)
                                 " 金；她知道该把钱送到谁手上")
        :requires (list (req-die) (req-item "金钱" first-installment))
        :resolve (instant
          (outcome "首期交出" "她收下钱,没有说谢。'我知道该送到哪里。第十天以前,他们至少会先认这笔账。'"
            (lambda () (set-protection! "首期"))))))

    (define (node-apartment-protection)
      (node "让她住进你的公寓"
        :subtitle (if (asset-at-least-apartment?)
                      "他们来时找不到人"
                      "需要先买下公寓；旅馆房间藏不住人")
        :disabled (not (asset-at-least-apartment?))
        :requires (list (req-die))
        :resolve (instant
          (outcome "人去楼空" "你把备用钥匙交给她。酒馆照常开门，但她不会再睡在后台那张窄床上。"
            (lambda ()
              (if (not (asset-at-least-apartment?))
                  (error "让她住进你的公寓：没有公寓")
                  #t)
              (set-protection! "公寓"))))))

    (define (node-police-protection)
      (node "申请巡警照看酒馆"
        :subtitle (if (relation-at-least? "官僚" '脸熟)
                      "用程序把酒馆摆到明面上"
                      "你现在只是陌生人；需要先在警局混个脸熟")
        :tags (list "中风险")
        :disabled (not (relation-at-least? "官僚" '脸熟))
        :requires (list (req-die))
        :resolve
          (roll 'knowledge
            (outcome "材料被压下" "值班警员把申请塞回抽屉:这种小事明天再说。"
              (lambda () (stress-current-actor! 1)))
            (outcome "巡警记下地址" "你把盯梢和卖身约写进备案。巡警未必想管,但他们会在那晚绕过酒馆。"
              (lambda () (set-protection! "警局")))
            (outcome "探长批了字" "探长在申请上批了两个字。到了第十天,收账人会先看到门口的巡警。"
              (lambda () (set-protection! "警局"))))))

    (define (node-dock-protection)
      (node "请码头兄弟看场"
        :subtitle (if (relation-at-least? "劳工" '脸熟)
                      "让几个靠得住的人第十天守在酒馆附近"
                      "你现在谁都不认识；需要先在码头混个脸熟")
        :tags (list "中风险")
        :disabled (not (relation-at-least? "劳工" '脸熟))
        :requires (list (req-die))
        :resolve
          (roll 'social
            (outcome "没人接话" "他们听完只是低头喝酒。夜莺的麻烦,还没变成他们的麻烦。"
              (lambda () (stress-current-actor! 1)))
            (outcome "有人答应" "两个码头汉子答应那晚在酒馆门口抽烟。话不多,够用了。"
              (lambda () (set-protection! "码头")))
            (outcome "老街认下" "消息传得很快。到了第十天,收账人会发现酒馆门口不只你一个人。"
              (lambda () (set-protection! "码头"))))))

    (define (stage2-world-nodes)
      (if (stage2-open?)
          (list (node-pay-installment))
          '()))

    (define (resolve-protected-beat2!)
      (cond
        ((equal? protection "首期")
         (spotlight! "首期" "收账人来了,也收住了手。他点了点那笔首期:'老板要亲自来做个了断。'"))
        ((equal? protection "公寓")
         (spotlight! "人去楼空" "他们推开酒馆后门时,后台那张窄床是空的。收账人沉着脸:老板要亲自来做个了断。"))
        ((equal? protection "警局")
         (spotlight! "巡警在场" "收账人看见巡警在街角站着,把话咽了回去。'好。那就等老板亲自来。'"))
        ((equal? protection "码头")
         (spotlight! "门口有人" "几个码头汉子抱着胳膊站在酒馆门口。收账人算了算成本,转身走了。"))
        (else
         (error "resolve-protected-beat2!: no protection"))))

    ;; ── 注入世界根的紧急节点 ────────────────────────
    ;; 只剩开场敲门：此时夜莺容器尚未出现，只能挂在世界根。
    ;; 其余情境动作已并入夜莺容器自身（见 situation-nodes）。
    (define (world-nodes)
      (if (= story-stage 0) (list (node-answer-door)) '()))

    ;; ── 节拍一：查访盯梢者 ──────────────────────────
    (define (node-confront-stalker)
      (encounter-action "去找收账人"
        (lambda ()
          (set! beat1-early? #t)
          (sync-globals!)
          (begin-public-event-early!)
          (start-encounter "夜莺·警告" on-public-event-result))))

    (define (node-inquire-stalker)
      (action "打听盯梢的人"
        (list (req-die))
        (roll 'social
          (lambda ()
            (stress-current-actor! 1)
            (set-flag! '已打听))
          (lambda ()
            (set-flag! '已打听)
            (advance-beat1! "外地人" "酒客只确认了一件事:那人是外地来的,用的不是本地名字。"))
          (lambda ()
            (set-flag! '已打听)
            (add-item! "情报" 1)
            (advance-beat1! "另一个名字" "水手漏了口风:那人在码头打听她时,用的是邻城歌厅的名字。")))))

    (define (node-dock-inquire-stalker)
      (action "在码头打听盯梢的人"
        (list (req-die))
        (roll 'social
          (lambda ()
            (stress-current-actor! 1))
          (lambda ()
            (advance-beat1! "坐船来的" "有搬运工记得那张脸:他不是本地人,是坐夜船来的。"))
          (lambda ()
            (advance-beat1! "落脚处" "一个水手说漏了嘴:那人夜里常在酒馆后街的短租屋出入。")))))

    (define (node-police-inquire-stalker)
      (action "查旅店登记簿"
        (list (req-die))
        (roll 'knowledge
          (lambda ()
            #f)
          (lambda ()
            (advance-beat1! "登记簿" "外地人都得登记。你在一本皱巴巴的登记簿里找到了他用过的假名。"))
          (lambda ()
            (advance-beat1! "短租屋" "登记簿边角夹着一张房钱收据。地址在酒馆后街。")))))

    (define (node-gossip)
      (if (and (= story-stage 1) (< beat1-progress beat1-target))
          (node-inquire-stalker)
          (工作 "打听消息" "劳工" '中 'social
            (outcome "听到风声" "水手和搬运工嘴里漏出一句:有人在码头用另一个名字打听她。"
              (lambda () (add-item! "情报" 1)))
            (outcome "没有收获" "酒客们今天嘴都很紧,或许是你问得太直接。"
              (lambda () #f))
            (outcome "引人注目" "你问得太急,角落里有个人放下杯子看了你一眼。"
              (lambda () (stress-current-actor! 1))))))

    ;; ── 幕间场景(酒馆) ──────────────────────────────
    (define (node-singing)
      (instant-action "听她唱一首歌"
        (lambda ()
          (set-flag! '唱歌)
          (spotlight! "一首歌" "她唱的是一首旧情歌。唱到一半停了,说等这件事过去再唱完。"))))

    (define (node-soft-choice)
      (container "她要走"
        (list
          (instant-action "留下"
            (lambda ()
              (set-flag! '软选择)
              (set-flag! '留下)
              (sync-globals!)
              (spotlight! "一句话" "她说:'你不开口,我就走。'你开口了。她低下头,没再说谢谢。")))
          (instant-action "让她走"
            (lambda ()
              (set-flag! '软选择)
              (advance-stage! 92)
              (cancel-public-events!)
              (spotlight! "怅然" "她留下预付金,只带走了自己的外套。你数了数,钱一分没动。"))))))

    (define (node-old-ring)
      (container "旧戒指"
        (list
          (instant-action "收下"
            (lambda ()
              (set-flag! '旧戒指)
              (add-item! "金钱" ring-value)
              (spotlight! "当了它" "她把戒指放在你手心。'当了它,能顶一点。'你说不出谢谢。")))
          (instant-action "不收"
            (lambda ()
              (set-flag! '旧戒指)
              (spotlight! "不收" "你让她自己收着。她看了你很久,把戒指攥回掌心。"))))))

    ;; ── 结局留在酒馆的观察 ──────────────────────────
    (define (ending-tavern-node)
      (cond
        ((= story-stage 90)
         (observe-action "台上的夜莺"
           "她站在酒馆角落那盏旧灯下,第一次唱完整的一场。唱完,她看向你这边。"))
        ((= story-stage 91)
         (observe-action "空舞台"
           (if (has-flag? '旧戒指)
               "酒馆的灯还亮着,台上却再没有人唱歌。那枚旧戒指的事,只剩你记得。"
               "酒馆的灯还亮着,台上却再没有人唱歌。")))
        ((= story-stage 92)
         (observe-action "空座位"
           "她坐过的那把椅子还摆在原地。老板说:那人不会再来了。"))
        (else #f)))

    (define (tavern-nodes)
      (append
        (if (and (>= story-stage 2) (>= world-day 5) (not (has-flag? '唱歌)))
            (list (node-singing))
            '())
        (if (and (>= story-stage 2) (>= world-day 7) (not (has-flag? '软选择)))
            (list (node-soft-choice))
            '())
        (if (and (= story-stage 3) (>= world-day 12) (not (has-flag? '旧戒指)))
            (list (node-old-ring))
            '())
        (if (ending-tavern-node)
            (list (ending-tavern-node))
            '())))

    ;; ── 交锋结果回调 ────────────────────────────────
    (define (on-bout-result bout result)
      (cond
        ((= bout 0)
         (if (and (equal? result 'fail) (not (get-global '夜莺已先离场)))
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
         (sync-blockers!))
        ((= bout 2)
         (if (equal? result 'fail)
             (advance-stage! 91)
             (advance-stage! 90))
         (sync-blockers!))
        (else #f)))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data) (render-data))
          ((equal? msg 'world-nodes) (world-nodes))
          ((equal? msg 'tavern-nodes) (tavern-nodes))
          ((equal? msg 'beat1-lead-nodes) (beat1-lead-nodes))
          ((equal? msg 'node-gossip) (node-gossip))
          ((equal? msg 'node-dock-inquire-stalker) (node-dock-inquire-stalker))
          ((equal? msg 'node-police-inquire-stalker) (node-police-inquire-stalker))
          ((equal? msg 'node-dock-protection) (node-dock-protection))
          ((equal? msg 'node-police-protection) (node-police-protection))
          ((equal? msg 'node-apartment-protection) (node-apartment-protection))
          ((equal? msg 'has-protection?) (not (equal? protection "无")))
          ((equal? msg 'resolve-protected-beat2!) (resolve-protected-beat2!))
          ((equal? msg 'sync-blockers!) (sync-blockers!))
          ((equal? msg 'on-bout-result) (on-bout-result (cadr args) (caddr args)))
          ((equal? msg 'save)
           (list
             (list "story-stage" story-stage)
             (list "condition-level" condition-level)
             (list "beat1-progress" beat1-progress)
             (list "beat1-early?" beat1-early?)
             (list "protection" protection)
             (list "installment-paid?" installment-paid?)
             (list "scene-flags" scene-flags)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! story-stage (assoc-get data "story-stage" 0))
             (set! condition-level (assoc-get data "condition-level" 0))
             (if (or (< condition-level 0) (> condition-level 3))
                 (error "夜莺存档错误：状态等级非法")
                 #t)
             (set! beat1-progress (assoc-get data "beat1-progress" 0))
             (set! beat1-early? (assoc-get data "beat1-early?" #f))
             (set! protection (assoc-get data "protection" "无"))
             (set! installment-paid? (assoc-get data "installment-paid?" #f))
             (set! scene-flags (normalize-flags (assoc-get data "scene-flags" '())))
             (sync-globals!)
             (sync-blockers!)))
          ((equal? msg 'debug-stage!) (advance-stage! (cadr args)))
          (#t #f))))))
