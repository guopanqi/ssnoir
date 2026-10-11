;; scenes/world/老街酒馆.scm - 老街酒馆（老码头 / 夜莺故事舞台）

(define old-street-tavern
  (let ()
    (define closed-days 0)
    (define closed-days-max 2)

    ;; 服务员的浮动不来自工价，而来自今晚碰上的人。小费是好结果的
    ;; 偶发上浮，但普通班次不留下需要再花骰子或钱处理的麻烦。
    ;; 小费照常给，但只演前三次：第一次让玩家读懂这笔钱，后两次只有双句闲话。
    (define tip-scenes-seen 0)
    (define tip-roll (list #t #f))

    ;; ── 露丝 ─────────────────────────────────────────────
    ;; 0 还只是给你送普通酒的女招待；1 已推销特选酒；2 十杯已满，知道她叫露丝。
    ;; 她不是又一个进酒馆就发任务的人。先有酒，再从那杯酒里长出一个人。
    (define ruth-stage 0)
    (define ruth-quota
      (make-clock "十杯份额" 10 'gauge
        (lambda (current max)
          (if (home 'drank-today?)
              "酒与普通的一样；今天再点也不会更放松。"
              "酒与普通的一样；十杯卖不完，老板会换人。"))))
    (define special-drink-price 40)
    (define special-drink-markup 25)

    (define (offer-special-drink!)
      (set! ruth-stage 1)
      (play-dialogue!
        (line "世界" "女招待放下酒，却没立刻走。她像在背一句不熟的台词。")
        (line "女招待" "本店还有特选酒。四十块一杯。")
        (line "尼尔" "瓶子不一样？")
        (line "女招待" "杯子不一样。")
        (line "女招待" "老板把十杯记在我名下。卖不完，就换个会笑的来。")))

    (define (tell-ruth-story!)
      (set! ruth-stage 2)
      ;; 酒馆留下每杯普通酒本来的 15 金；退的是她在十杯上的全部抽成。
      (add-item! "金钱" (* special-drink-markup (ruth-quota 'max)))
      (play-dialogue!
        (line "世界" "她收走第十个空杯，又把一只信封推到你面前。")
        (line "女招待" "二百五十。十杯酒多出来的那部分。")
        (line "尼尔" "老板肯？")
        (line "女招待" "他拿到了酒钱，也记下十杯。这些是我的抽成。")
        (line "尼尔" "你费这么大劲，又把它还我？")
        (line "女招待" "你在等一个答案，不是在买我。我叫露丝。")
        (line "露丝" "五年前，我弟弟拿了我的房钱。我当着满酒馆的人叫他贼。")
        (line "露丝" "第二天，当铺把母亲的表送了回来。钱是他付的。")
        (line "露丝" "那晚他跟一条沿岸货船走了。临走告诉酒保，回来就到这里找我。")
        (line "尼尔" "五年了。")
        (line "露丝" "水手回城，总有人先来这儿喝一杯。我得站在看得见门的地方。")))

    (define (node-special-drink)
      (node "点特选酒"
        :anchor "老街酒馆-购买"
        :clocks (list (ruth-quota 'render-data))
        :requires (list (req-item "金钱" special-drink-price))
        :resolve (instant
          (outcome (lambda ()
              ;; 只有当天第一杯有恢复；后续的钱只买到同样的酒。
              (if (not (home 'drank-today?)) (home 'drink!) #f)
              (ruth-quota 'advance! 1)
              (if (ruth-quota 'full?) (tell-ruth-story!) #f))))))

    (define (node-ruth)
      (note-node "标注：露丝守着门" "露丝"
        "她在吧台这一头收拾杯子，目光不时越过你，落到门上。"))

    ;; ── 酒馆职级 ───────────────────────────────────────
    ;; 职级是离散变量，考核钟只记录离下一次升降职还有多远：
    ;; 服务员时填满即升领班；领班时从满格往下掉，归零即降回服务员。
    ;;
    ;; 四格不是三格：一班活干好干坏是 ±1，一件麻烦拖到打烊是 −2。三格的话，
    ;; 拖一次麻烦就够把人从满格打到只剩一格，两次直接降职——那不是考核，是开除。
    ;; 四格才让"两班的功抵一次砸锅"成立，玩家有得补。
    (define tavern-rank "服务员")
    (define tavern-rank-clk
      (make-clock "酒馆职级" 4 'gauge
        (lambda (current max)
          (if (equal? tavern-rank "服务员")
              "当前职级：服务员。考核填满即升为领班。"
              "当前职级：领班。考核归零即降回服务员。"))))

    ;; 领班每班都可能把一件现场麻烦留给玩家。它不另开地点或界面，
    ;; 只是酒馆里一张当天有效的行动卡。
    ;;
    ;; 死线用钟，不用「限时」这个 tag。tag 只是一个形容词，玩家读不出还剩多久；
    ;; 而这里本来就有一条真的死线，它该长成死线的样子。钟直接挂在这张卡上
    ;; （:clocks 徽章），不再另立一条标注——原来那两处说的是同一件事。
    (define tavern-trouble "无")
    (define tavern-trouble-clk
      (make-clock "打烊前" 1 'countdown
        "今晚打烊前不处理：冷静 −2，职级考核 −2。"))

    ;; 麻烦当天的收场，日终统一结算职级（见「领班麻烦结算」）。
    ;;   "未了"   还压在手里（也是结算完的静止态）
    ;;   "压下"   暂且压住，不功不过
    ;;   "干净"   处理干净
    (define tavern-trouble-outcomes (list "未了" "压下" "干净"))
    (define tavern-trouble-outcome "未了")
    (define tavern-trouble-roll (list #t #f #f))
    (define tavern-trouble-kinds (list "醉客闹事" "账目短缺" "后门斗殴"))

    (define (foreman?)
      (equal? tavern-rank "领班"))

    (define (change-tavern-rank! delta)
      (tavern-rank-clk 'advance! delta)
      (cond
        ((and (not (foreman?)) (tavern-rank-clk 'full?))
         (set! tavern-rank "领班")
         (result-supplement! "升职：领班"))
        ((and (foreman?) (tavern-rank-clk 'empty?))
         (set! tavern-rank "服务员")
         (result-supplement! "降职：服务员"))
        (else #f)))

    (define (node-tavern-rank)
      (clock-node "标注：酒馆职级" (tavern-rank-clk 'render-data)))

    (define (start-tavern-trouble!)
      (if (equal? tavern-trouble "无")
          (begin
            (set! tavern-trouble (random-choice tavern-trouble-kinds))
            (set! tavern-trouble-outcome "未了")
            ;; 满格起、往下走：这是一条死线，不是玩家在推进的东西。
            (tavern-trouble-clk 'set! (tavern-trouble-clk 'max))
            (result-supplement! (string-append "今晚之内：" tavern-trouble)))
          #f))

    (define (maybe-start-tavern-trouble!)
      (if (and (equal? tavern-trouble "无")
               (random-choice tavern-trouble-roll))
          (start-tavern-trouble!)
          #f))

    (define (resolve-tavern-trouble! how)
      (set! tavern-trouble "无")
      (set! tavern-trouble-outcome how)
      (tavern-trouble-clk 'reset!))

    (define (trouble-skill)
      (cond
        ((equal? tavern-trouble "醉客闹事") 'social)
        ((equal? tavern-trouble "账目短缺") 'knowledge)
        ((equal? tavern-trouble "后门斗殴") 'violence)
        (else (error "老街酒馆：未知的领班麻烦"))))

    ;; ── 世界回声 ─────────────────────────────────────
    ;; 按剧情时期分桶的无名闲话：每次喝酒或干完一班有一半机会听见一条，听过的不放回。
    ;; 它们只恢复 1 点冷静，没有任务或后续。往桶里加就是了。
    (define (atmosphere-period)
      (cond
        ((第二章 'started?) 3)
        ((<= (three-letters 'story-stage) 2) 1)
        (else 2)))

    (define (echo . lines)
      (lambda ()
        (restore-actor-composure! 'player 1)
        (apply play-bubble! lines)))

    (define tavern-echoes
      (make-echo-pool atmosphere-period
        (list 1
          (echo
            (line "世界" "靠窗那桌把三块工牌排在杯子旁边。")
            (line "世界" "“今天只点了两个班。”")
            (line "世界" "“第三个人呢？”")
            (line "世界" "“第三个人在这儿喝酒。”"))
          (echo
            (line "世界" "吧台尽头有人把硬币按面值排成一行，数到第三遍。")
            (line "世界" "“房东周五来。”")
            (line "世界" "“那你还差一杯的钱。”"))
          (echo
            (line "世界" "门口的雨衣滴了一地水，没人去擦。")
            (line "世界" "“早班船没进港。”")
            (line "世界" "“那就没人付晚上的账。”")))
        (list 2
          (echo
            (line "世界" "两个剧院杂工在吧台边分一盘冷肉。")
            (line "世界" "“散场以后，台上比街上还黑。”")
            (line "世界" "“所以我从不等谢幕。”"))
          (echo
            (line "世界" "有人把一张撕过的戏票夹在杯垫下面。")
            (line "世界" "“前排的票。她只坐了一幕。”")
            (line "世界" "“那一幕值这个价。”"))
          (echo
            (line "世界" "角落那桌压低了嗓子，杯子却越推越响。")
            (line "世界" "“警察局今晚多点了两个人。”")
            (line "世界" "“多两个人，少两条街。”")))
        (list 3
          (echo
            (line "世界" "一张培训通知在桌上传了半圈。")
            (line "世界" "“会开机器的留下。”")
            (line "世界" "“谁教机器认得我们？”"))
          (echo
            (line "世界" "一个人把工具袋放在椅子上，像给它也点了一杯。")
            (line "世界" "“新港那边按小时算，不按班。”")
            (line "世界" "“按小时，他们就能把小时切得更薄。”"))
          (echo
            (line "世界" "吧台上摆着一份晚报，港口那一版被人折成了正面。")
            (line "世界" "“照片里没有一个人。”")
            (line "世界" "“那是他们想要的照片。”")))))

    (define (maybe-play-atmosphere!)
      (if (random-choice (list #t #f))
          (tavern-echoes 'try!)
          #f))

    ;; ── 常客 ─────────────────────────────────────────
    ;; 三个会在这儿待几天的人。随机只决定你哪次去碰上他来；他对你的态度是拍数，
    ;; 见一次推一拍，永不倒退，所以他做的事是可以被记住的：莫里斯永远付账，
    ;; 帕克永远分你半个三明治，科尔第一晚请全场、第二晚问你借烟。
    ;; 一次结果只演一段，人排在回声前面（见 meet-or-echo!）。
    ;; 三个人都用 play-bubble!：他们在场时有一条同名标注（见 regular-notes），气泡从那儿冒。

    ;; 莫里斯：跑沿岸货船跑了三十年的老水手，上岸等下一条船。他的习惯是替年轻人付账。
    (define morris
      (make-regular "莫里斯"
        :window (lambda () (not (第二章 'started?)))
        :stay 3
        :beats (list
          (beat (lambda ()
                  (play-bubble!
                    (line "莫里斯" "新来的。你端盘子的样子像在甲板上走。")
                    (line "尼尔" "地不晃。")
                    (line "莫里斯" "那杯记我账上。三十年前也有人这么替我记过。")))
                (lambda () (if (home 'drank-today?) #f (home 'drink!))))
          (beat (lambda ()
                  (play-bubble!
                    (line "莫里斯" "沿岸货船开春回港，一年就那一趟。")
                    (line "莫里斯" "船上的人回城，头一杯都在这儿喝。")
                    (line "尼尔" "你在等谁？")
                    (line "莫里斯" "等一条肯要老人的船。")))
                (lambda () (restore-actor-composure! 'player 1)))
          (beat (lambda ()
                  (play-bubble!
                    (line "莫里斯" "明早的船。烟在船上不让点。")
                    (line "莫里斯" "拿着。别在甲板上抽。")
                    (line "尼尔" "我不上船。")
                    (line "莫里斯" "那就更该拿着。")))
                (lambda () (if (item-full? "香烟") #f (add-item! "香烟" 1)))))))

    ;; 帕克：剧院的杂工，散场后来这儿吃他从后台带出来的三明治。他总把一半推给你。
    (define parker
      (make-regular "帕克"
        :window (lambda () (and (>= (three-letters 'story-stage) 3)
                                (not (第二章 'started?))))
        :stay 2
        :beats (list
          (beat (lambda ()
                  (play-bubble!
                    (line "帕克" "后台的三明治。她们从来不吃第二片。")
                    (line "帕克" "你那份。别客气，客气就凉了。")))
                (lambda () (restore-actor-composure! 'player 1)))
          (beat (lambda ()
                  (play-bubble!
                    (line "帕克" "今晚换了三次布景，掌声一次没等到。")
                    (line "尼尔" "他们不给杂工鼓掌。")
                    (line "帕克" "他们不知道幕是谁拉的。吃吧，还是一半。")))
                (lambda () (restore-actor-composure! 'player 1))))))

    ;; 科尔：码头的老搬运工，被机器替下来。第一晚拿遣散费请全场，第二晚兜里就空了。
    (define cole
      (make-regular "科尔"
        :window (lambda () (第二章 'started?))
        :stay 3
        :beats (list
          (beat (lambda ()
                  (play-bubble!
                    (line "科尔" "今晚这一圈我请。二十二年，他们折成一个信封。")
                    (line "尼尔" "你留点。")
                    (line "科尔" "留给谁？机器不喝酒。")))
                (lambda () (if (home 'drank-today?) #f (home 'drink!))))
          (beat (lambda ()
                  (play-bubble!
                    (line "科尔" "昨晚的钱昨晚就没了。有烟吗？")
                    (line "科尔" "明天去新港排队。他们说会开机器的留下。")
                    (line "尼尔" "你会吗？")
                    (line "科尔" "我会搬。")))
                (lambda () (if (> (item-count "香烟") 0) (remove-item! "香烟" 1) #f)))
          (beat (lambda ()
                  (play-bubble!
                    (line "科尔" "排到了。培训三天，不给工钱。")
                    (line "科尔" "第四天要是还站在这儿，你就当没见过我。")))))))

    (define tavern-regulars (list morris parker cole))

    (define (regular-notes)
      (map (lambda (r)
             (note-node (r 'name) (r 'name)
               (cond
                 ((equal? (r 'name) "莫里斯") "靠窗坐着，帽子扣在杯子旁边，眼睛在门上。")
                 ((equal? (r 'name) "帕克") "吧台边，油纸包着的三明治摊在面前。")
                 (else "角落那桌，一个人，杯子比昨晚多。"))))
           (filter (lambda (r) (r 'present?)) tavern-regulars)))

    ;; 一次结果只演一段：有人在、今天没见过 → 人；否则骰一条回声。
    (define (meet-or-echo!)
      (if (meet-regular! tavern-regulars) #t (maybe-play-atmosphere!)))

    ;; ── 高利贷 ────────────────────────────────────
    ;; 角落里放贷的：缺钱时立刻周转，代价是 1.5 倍连本带利，逾期利滚利。
    ;; 同一时间只能有一笔（loan-owed>0 表示有未清欠款）。
    (define loan-owed 0)
    (define loan-clk
      (make-clock "还款到期" 5 'countdown
        (lambda (current max)
          (string-append "欠 " (number->string loan-owed)
                         " 金钱；归零后每天利滚利，还要挨催。"))))
    (define loan-principal 40)   ; 到手本金
    (define loan-repay 60)       ; 到期应还

    (define (node-borrow-loan)
      (node "借一笔钱周转"
        :subtitle "角落里放贷的能立刻拿钱给你周转——借 40，五天后连本带利还 60；逾期利滚利，最好别拖"
        :tags (list "非法" "高利贷")
        :resolve (instant
          (outcome (lambda ()
              (add-item! "金钱" loan-principal)
              (set! loan-owed loan-repay)
              (loan-clk 'set! (loan-clk 'max)))))))

    (define (node-repay-loan)
      (node "还清欠款"
        :subtitle (string-append "把欠的 " (number->string loan-owed) " 金钱一次结清")
        :requires (list (req-item "金钱" loan-owed))
        :resolve (instant
          (outcome (lambda ()
              (set! loan-owed 0)
              (loan-clk 'reset!))))))

    (define (loan-shark-container)
      (node "放贷的"
        :anchor "老街酒馆"
        :subtitle (if (> loan-owed 0)
                      (string-append "他靠在角落记着账，你还欠 " (number->string loan-owed) " 金钱")
                      "角落里靠墙坐着个记账的，缺钱周转能找他——代价你懂")
        :children (list (if (> loan-owed 0) (node-repay-loan) (node-borrow-loan)))))

    (define-turn-rule "高利贷催收"
      (lambda () (> loan-owed 0))
      (lambda ()
        (if (not (loan-clk 'empty?))
            (loan-clk 'advance! -1)
            (begin
              ;; 逾期：利滚利 +20%（向上取整），并承受上门催收的压力。
              (set! loan-owed (+ loan-owed (quotient (+ loan-owed 4) 5)))
              (spend-composure! 2)
              (notify! (string-append "欠款逾期，利滚利涨到 " (number->string loan-owed)
                                      " 金钱。放贷的人开始上门催了。"))))))

    ;; ── 生计工作 ──────────────────────────────────
    ;; 酒馆这两张是**稳的那一头**：钱的上限低于码头，但下限不是零——
    ;; 砸了一晚也照样有一晚的工钱，坏结果只扣 1 点冷静。码头反过来：
    ;; 顶上去更高，掉下来是空手加两点冷静。
    ;; 于是骰子有了去处：烂骰面来酒馆保底，好骰面留给搬运和夜班。
    ;; 这才是「低风险」这个标签原来应该意味着的东西——在这以前它是句空话，
    ;; 领班的坏结果和高风险的搬运一样疼。
    (define (give-tip!)
      (add-item! "金钱" 3)
      (cond
        ((= tip-scenes-seen 0)
         (set! tip-scenes-seen 1)
         (play-dialogue!
           (line "世界" "穿旧西装的男人把两张钞票压在杯底。")
           (line "尼尔" "你多放了一张。")
           (line "客人" "我知道。今晚别叫醒我。")))
        ((= tip-scenes-seen 1)
         (set! tip-scenes-seen 2)
         (play-bubble!
           (line "客人" "零钱拿着。")
           (line "尼尔" "你还没喝完。")))
        ((= tip-scenes-seen 2)
         (set! tip-scenes-seen 3)
         (play-bubble!
           (line "水手" "找的钱归你。")
           (line "尼尔" "你给得不多。")))
        (else #f)))

    (define (maybe-give-tip!)
      (if (random-choice tip-roll)
          (begin (give-tip!) #t)
          #f))

    (define (node-waiter)
      (工作 "服务员" '低 'social
        (outcome (lambda ()
            (add-item! "金钱" 8)
            (change-tavern-rank! 1)
            (if (maybe-give-tip!) #f (meet-or-echo!))))
        (outcome (lambda ()
            (add-item! "金钱" 5)
            (meet-or-echo!)))
        (outcome (lambda ()
            (add-item! "金钱" 3)
            (spend-composure! 1)
            (change-tavern-rank! -1)
            (meet-or-echo!)))
        ;; 副标题保持一行以内：标题只说了"服务员"，得有一句说清干什么、图什么。
        "端盘子跑堂，挣一晚上的钱"
        :anchor "老街酒馆-工作"))

    ;; 领班这张卡以前是**全是坏处**的：钱没比搬运高，考核会掉，还要额外
    ;; 挨一件麻烦——而且那件麻烦连你带得好不好都不看，三档结果一律摇一次。
    ;; 「里外都稳住」的意思本该就是今晚什么也没炸，结果它照样给你留一件事。
    ;;
    ;; 改两处：
    ;;   一、麻烦只从「带完一班」和「场面失控」里长出来。骰得好就是没出事，
    ;;       这句话必须是真的，否则那一档只是名字好听。
    ;;   二、钱往上抬一档。领班扛着一件搬运没有的东西——麻烦会占掉你另一颗骰、
    ;;       甚至废掉第二天的班——那就该在同一个骰面上比搬运多挣。
    ;;       只有满骰面(6)那一格仍旧输给搬运和夜班：酒馆是稳的那一头，
    ;;       它买的是地板，不是天花板。
    (define (node-foreman)
      (工作 "领班" '低 'social
        (outcome (lambda ()
            (add-item! "金钱" 13)
            (change-tavern-rank! 1)
            (meet-or-echo!)))
        (outcome (lambda ()
            (add-item! "金钱" 8)
            (maybe-start-tavern-trouble!)
            (if (equal? tavern-trouble "无") (meet-or-echo!) #f)))
        (outcome (lambda ()
            (add-item! "金钱" 5)
            (spend-composure! 1)
            (change-tavern-rank! -1)
            (maybe-start-tavern-trouble!)
            (if (equal? tavern-trouble "无") (meet-or-echo!) #f)))
        "带班收钱，但现场麻烦也得由你收拾"
        :anchor "老街酒馆-工作"))

    ;; 收拾麻烦这张卡**当场不动职级**。职级是「今天这一班带得怎么样」，那是打烊时
    ;; 才有的结论；当场就加减，等于老板站在你旁边一件一件记分。卡只决定麻烦的收场，
    ;; 日终按收场结算考核（见「领班麻烦结算」）。
    ;; 没收住不算收场：麻烦还压在手里，只要还有骰子就能再来一次。
    (define (node-tavern-trouble)
      (node tavern-trouble
        :anchor "老街酒馆-工作"
        :subtitle "今晚打烊前得有个交代"
        :clocks (list (tavern-trouble-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll (trouble-skill)
          ;; 没收住只扣 1：真正的代价在打烊那一刻（−2 冷静、考核 −2），
          ;; 而不是在每一次没骰好上。否则一晚上试两次就够掏空满冷静的一半还多。
          (outcome (lambda () (spend-composure! 1)))
          (outcome (lambda () (resolve-tavern-trouble! "压下")))
          (outcome (lambda () (resolve-tavern-trouble! "干净"))))))

    ;; 这里曾有一张「打一壶酒」：25 金买一壶带走，回住所再喝。它和「点一杯酒」
    ;; 同价、同效果、共用同一次"当天第一杯"，区别只有"在哪儿喝"——而那个区别
    ;; 不改变任何决定。酒馆只留当场喝的那杯。
    ;; 家里那张「喝酒」跟着一起删了：酒买不到了，那张卡就永远是灰的。

    ;; 烟只在交锋里有用：兜里有烟，那一场的树上就多一张「抽烟」卡（见 engine.scm 的
;; carry-nodes）。投一颗行动骰抽掉，恢复 2 点冷静。交锋每回合自动流失 1，
;; 所以一根烟买回来的是两个回合——这是它值 15 金的理由，也是它的单位。
;; 它要投骰是故意的：走到旁边点一根烟本来就花掉了一段时间，角落里不该有免费按钮。
;; 骰面不参与结算，所以那是全场唯一一处该把最烂那颗骰扔进去的地方。
;; 开局自带两根（见 GameState），玩家不用先学规则再来买。
    ;; 买东西的卡不写副标题：标题已经说了买的是什么，价钱由需求格显示，
    ;; 烟能干什么在交锋里那张卡上写。这里再写一遍只是把两张购买卡撑成两段话。
    (define (node-buy-cigarettes)
      (node "买烟"
        :anchor "老街酒馆-购买"
        ;; 兜里揣不下第六包（见 engine.scm 的 item-capacities）。满了就灰掉，
        ;; 别让玩家付完 15 金才知道多买的那根凭空没了。
        :subtitle (if (item-full? "香烟") "身上带不下更多烟了" "")
        :disabled (item-full? "香烟")
        :requires (list (req-item "金钱" 15))
        :resolve (instant
          (outcome (lambda () (add-item! "香烟" 1))))))

    ;; 当场点一杯：效果与在家喝自带的酒完全一样，共用同一次“当天第一杯”（home 的 drank-today?）。
    ;; :resolve 用 outcome 包一层，结果才会像判定一样以锚定卡片弹出，而不是只飘过一条 notify!。
    (define (node-drink-here)
      (node "点一杯酒"
        :anchor "老街酒馆-购买"
        ;; 只有灰掉的时候才写一句——不写玩家就不知道为什么点不动。
        :subtitle (if (home 'drank-today?) "今天已经喝过了" "")
        :disabled (home 'drank-today?)
        :requires (list (req-item "金钱" 15))
        :resolve (instant
          (outcome (lambda ()
              (let ((ruth-offers-now? (= ruth-stage 0)))
                (home 'drink!)
                (if ruth-offers-now? (offer-special-drink!) #f)
                ;; 露丝第一次推酒已经占了这杯的表现，别再叠一段闲话。
                (if ruth-offers-now? #f (meet-or-echo!))))))))

    ;; ── 赌钱 ──────────────────────────────────────
    ;; 这里曾有一张「去地下酒吧押一把」：花一颗骰 + 20 金，摇一次，0/20/60。
    ;; 它其实只是另一份工作，方差大一点而已——骰子花在下注上，赌博就没有赌博。
    ;; 老街只留一个赌钱的地方，而且是有人的那个：酒馆后面的地下拳场（见 艾迪.scm），
    ;; 骰子花在看懂比赛上，钱才花在票上。

    ;; ── 酒馆后屋 ──────────────────────────────────
    ;; 第二章弗兰克那一拍（别给他们想要的）调停成功之后，后屋对你开着。
    ;; 第一章酒馆只是个调查地点；这是玩家第一次拥有**关系带来的据点**。
    ;; 它给的不是更高的钱，是更低的生存成本：有人认识你，所以吃饭便宜，零活不用抢。
    ;;
    ;; 到了 Phase B，后屋的活也跟着少——仓库被登记了，看仓库那份先没了。
    ;; 机器进来这件事，就该从你自己这层关系上被感觉到。
    (define ate-day 0)

    (define (ate-today?) (= ate-day world-day))

    (define (node-eat)
      (node "吃点东西"
        :subtitle (if (ate-today?) "今天已经吃过了" "老街熟人价，恢复 2 点冷静")
        :disabled (ate-today?)
        :requires (list (req-item "金钱" 6))
        :resolve (instant
          (outcome (lambda ()
              (set! ate-day world-day)
              (restore-actor-composure! 'player 2))))))

    ;; 后屋的活是关系给的，它给的不该只是又一份工钱：守仓库是一整夜没人来找你的
    ;; 安静，顺的那一晚连冷静都回来一点——老街上唯一一份**边挣钱边歇着**的工。
    (define (node-watch-warehouse)
      (工作 "看仓库" '低 'sharpness
        (outcome (lambda ()
            (add-item! "金钱" 10)
            (restore-actor-composure! 'player 1)))
        (outcome (lambda () (add-item! "金钱" 7)))
        (outcome (lambda ()
            (add-item! "金钱" 4)
            (spend-composure! 1)))
        "替弗兰克的人守一夜仓库；太平的一夜也是歇着"))

    (define (node-run-errand)
      (工作 "替人跑一趟" '低 'social
        (outcome (lambda () (add-item! "金钱" 9)))
        (outcome (lambda () (add-item! "金钱" 6)))
        (outcome (lambda ()
            (add-item! "金钱" 3)
            (spend-composure! 1)))
        "老街有人要送东西、带句话"))

    ;; 他多数日子里没有事给你做。空容器读起来像坏了，所以放一条标注说清他此刻在干什么。
    (define (node-frank-in-back-room)
      (note-node "标注：弗兰克在后屋" ""
        (if (equal? (第二章 'phase) "B")
            "他面前摊着一张从码头撕下来的培训名单，一个名字一个名字地看。"
            "他在桌子那头对账。有人进来说了句什么，他点了一下头。")))

    (define (back-room-container)
      (node "酒馆后屋"
        :anchor "老街酒馆"
        :children
        (append
          (list (node-frank-in-back-room) (node-eat))
          (if (equal? (第二章 'phase) "B")
              (list (node-run-errand))
              (list (node-watch-warehouse) (node-run-errand))))))

    ;; ── 歇业 ──────────────────────────────────────
    ;; 这里曾有一张「酒馆内景」的氛围卡，已删。原型阶段不摆纯氛围的观察卡：
    ;; 它不改变任何东西，也验证不了任何玩法，只是把真正要读的卡挤下去一格。
    ;; 歇业这张留着，因为它带功能信息：今天这儿什么也做不了。
    (define (node-closed)
      (node "酒馆歇业"
        :anchor "老街酒馆"
        :resolve (observe
          "门板从里面上了闩。老板贴着告示：家中有事，歇业数日。从门缝望进去,台侧那把红伞还立在原处,没人来取。")))

    ;; 这里曾有一条「酒馆夜里」的常驻标注：「夜里这儿只剩两种人：还没回家的，
    ;; 和不打算回家的。」写得不坏，但它从开门到闭幕一个字不变，而且和《三封信》
    ;; 的「酒馆里的闲话」说的是同一件事——这屋里都是些什么人。两条并排挂着，
    ;; 玩家读第二遍就不再读第三遍。留会变的那一条，删不变的这一条。
    ;; 这和上面那条规矩是同一条：纯氛围的东西换成标注的形状，也还是纯氛围。

    (define (tavern-clocks)
      (append
        (if (> closed-days 0)
            (list (list 'clock "酒馆歇业" closed-days closed-days-max 'countdown
                        "歇业期间不能在这里做工或打听消息。"))
            '())
        (if (> loan-owed 0)
            (list (loan-clk 'render-data))
            '())))

    ;; ── 组装 ──────────────────────────────────────
    ;; 小节一酒馆就开门（夜莺在这儿唱歌），但只开一半：台上的人、跑堂的活、
    ;; 喝一杯、买点东西。放贷的等老街一起开——开场借得到四十金，第一小节
    ;; 「三天凑一百」的压力就没了。
    ;; 地下拳场不受这条限制：借钱是确定的钱，赌是方差，在三天死线上加方差是加压不是减压。
    (define (tavern-children)
      (if (> closed-days 0)
          (list (node-closed))
          (append
            ;; 麻烦的死线钟挂在那张卡上，这里不再另立一条标注：同一件事说两遍，
            ;; 玩家还要自己认出它们是一件事。
            (list (node-tavern-rank))
            (regular-notes)
            (地点节点 "老街酒馆")
            ;; 麻烦留着时暂停新一班：玩家可以立刻处理，也可以离开、
            ;; 在日终承担后果，但不能无视问题继续刷领班班次。
            (list (cond
                    ((not (equal? tavern-trouble "无")) (node-tavern-trouble))
                    ((foreman?) (node-foreman))
                    (else (node-waiter)))
                  (node-drink-here)
                  (node-buy-cigarettes))
            (cond
              ((= ruth-stage 1) (list (node-special-drink)))
              ((= ruth-stage 2) (list (node-ruth)))
              (else '()))
            (if (three-letters 'old-street-open?)
                (list (loan-shark-container))
                '())
            (if (frank 'back-room?)
                (list (back-room-container))
                '()))))

    ;; 打烊。今晚那件麻烦到这儿有个结论，职级考核在这一刻结算——
    ;; 干净 +1，压下不功不过，拖着没管 −2（外加冷静 −2）。
    ;; 一班活干好干坏是 ±1（见两张工作卡）；砸在麻烦上更重，因为那是领班的本职。
    (define-turn-rule "领班麻烦结算"
      (lambda () (or (not (equal? tavern-trouble "无"))
                     (not (equal? tavern-trouble-outcome "未了"))))
      (lambda ()
        (if (equal? tavern-trouble "无")
            ;; 白天已经收场了：只结算考核。
            (begin
              (if (equal? tavern-trouble-outcome "干净")
                  (begin
                    (change-tavern-rank! 1)
                    (notify! (string-append "昨晚那件事你收拾干净了。职级考核进了一格。当前职级："
                                            tavern-rank "。")))
                  (notify! "昨晚那件事你压住了。没出岔子，也没人记你的功。"))
              (set! tavern-trouble-outcome "未了"))
            (begin
              (tavern-trouble-clk 'advance! -1)
              (if (tavern-trouble-clk 'empty?)
                  (begin
                    (spend-actor-composure! 'player 2)
                    ;; 日终规则不在行动结算里，只需改钟和通知。
                    (change-tavern-rank! -2)
                    (notify! (string-append tavern-trouble
                                            "拖到打烊。你失了冷静，职级考核退了两格。当前职级："
                                            tavern-rank "。"))
                    (resolve-tavern-trouble! "未了"))
                  #f)))))

    (define-turn-rule "老街酒馆停业倒计时"
      (lambda () (> closed-days 0))
      (lambda () (set! closed-days (- closed-days 1))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "老街酒馆"
                   :children (tavern-children)
                   :clocks (tavern-clocks)
                   :arrivals (地点入场 "老街酒馆"))))
          ((equal? msg 'set-closed!)
           (set! closed-days (cadr args))
           (set! closed-days-max (max closed-days-max closed-days)))
          ((equal? msg 'save)
           (list (list "closed-days" closed-days)
                 (list "closed-days-max" closed-days-max)
                 (list "loan-owed" loan-owed)
                 (list "loan-days" (loan-clk 'save))
                 (list "tavern-rank" tavern-rank)
                 (list "tavern-rank-progress" (tavern-rank-clk 'save))
                 (list "tavern-trouble" tavern-trouble)
                 (list "tavern-trouble-time" (tavern-trouble-clk 'save))
                 (list "tavern-trouble-outcome" tavern-trouble-outcome)
                 (list "ruth-stage" ruth-stage)
                 (list "ruth-quota" (ruth-quota 'save))
                 (list "echoes" (tavern-echoes 'save))
                 (list "regulars" (map (lambda (r) (list (r 'name) (r 'save))) tavern-regulars))
                 (list "tip-scenes-seen" tip-scenes-seen)
                 (list "ate-day" ate-day)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! closed-days (assoc-get data "closed-days" 0))
             (set! closed-days-max (assoc-get data "closed-days-max" 2))
             (set! loan-owed (assoc-get data "loan-owed" 0))
             (loan-clk 'load! (assoc-get data "loan-days" 0))
             (set! tavern-rank (assoc-get data "tavern-rank" "服务员"))
             (if (member? tavern-rank (list "服务员" "领班"))
                 #t
                 (error "老街酒馆存档错误：职级非法"))
             (tavern-rank-clk 'load!
               (assoc-get data "tavern-rank-progress"
                 (if (foreman?) (tavern-rank-clk 'max) 0)))
             (if (or (and (foreman?) (not (tavern-rank-clk 'empty?)))
                     (and (not (foreman?)) (not (tavern-rank-clk 'full?))))
                 #t
                 (error "老街酒馆存档错误：职级与考核边界矛盾"))
             (set! tavern-trouble (assoc-get data "tavern-trouble" "无"))
             (if (or (equal? tavern-trouble "无")
                     (member? tavern-trouble tavern-trouble-kinds))
                 #t
                 (error "老街酒馆存档错误：领班麻烦类型非法"))
             (tavern-trouble-clk 'load!
               (assoc-get data "tavern-trouble-time" 0))
             (set! tavern-trouble-outcome
               (assoc-get data "tavern-trouble-outcome" "未了"))
             (set! ruth-stage (assoc-get data "ruth-stage" 0))
             (ruth-quota 'load! (assoc-get data "ruth-quota" 0))
             ;; 旧档只有三个「听过没」的布尔；折成每桶一条已用的回声。
             (tavern-echoes 'load!
               (assoc-get data "echoes"
                 (list (list 1 (if (assoc-get data "early-talk-seen?" #f) (list 0) '()))
                       (list 2 (if (assoc-get data "late-talk-seen?" #f) (list 0) '()))
                       (list 3 (if (assoc-get data "chapter2-talk-seen?" #f) (list 0) '())))))
             (let ((saved (assoc-get data "regulars" '())))
               (map (lambda (r)
                      (let ((d (assoc-get saved (r 'name) #f)))
                        (if d (r 'load! d) #f)))
                    tavern-regulars))
             (set! tip-scenes-seen (assoc-get data "tip-scenes-seen" 0))
             (set! ate-day (assoc-get data "ate-day" 0))
             (if (and (>= tip-scenes-seen 0) (<= tip-scenes-seen 3))
                 #t
                 (error "老街酒馆存档错误：小费演出次数非法"))
             (if (member? ruth-stage (list 0 1 2))
                 #t
                 (error "老街酒馆存档错误：露丝阶段非法"))
             (if (or (and (= ruth-stage 2) (ruth-quota 'full?))
                     (and (< ruth-stage 2) (not (ruth-quota 'full?))))
                 #t
                 (error "老街酒馆存档错误：露丝阶段与十杯份额矛盾"))
             (if (member? tavern-trouble-outcome tavern-trouble-outcomes)
                 #t
                 (error "老街酒馆存档错误：领班麻烦收场非法"))
             ;; 麻烦还压在手里的时候不可能同时有收场：这两个状态互斥。
             (if (and (not (equal? tavern-trouble "无"))
                      (not (equal? tavern-trouble-outcome "未了")))
                 (error "老街酒馆存档错误：麻烦未了却记着收场")
                 #t)))
          (#t #f))))))
