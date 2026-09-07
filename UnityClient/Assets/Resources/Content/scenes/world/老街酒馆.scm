;; scenes/world/老街酒馆.scm - 老街酒馆（老码头 / 夜莺故事舞台）

(define old-street-tavern
  (let ()
    (define closed-days 0)
    (define closed-days-max 2)

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
         (result-note! "升职：领班"))
        ((and (foreman?) (tavern-rank-clk 'empty?))
         (set! tavern-rank "服务员")
         (result-note! "降职：服务员"))
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
            (result-note! (string-append "今晚之内：" tavern-trouble)))
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
          (outcome "拿了这笔钱"
            (lambda ()
              (add-item! "金钱" loan-principal)
              (set! loan-owed loan-repay)
              (loan-clk 'set! (loan-clk 'max)))))))

    (define (node-repay-loan)
      (node "还清欠款"
        :subtitle (string-append "把欠的 " (number->string loan-owed) " 金钱一次结清")
        :requires (list (req-item "金钱" loan-owed))
        :resolve (instant
          (outcome "还清了这笔债"
            (lambda ()
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
    (define (node-waiter)
      (关系工作 "服务员" "老码头" '低 'social
        (outcome "手脚麻利"
          (lambda ()
            (add-item! "金钱" 8)
            (change-tavern-rank! 1)))
        (outcome "普通一班"
          (lambda () (add-item! "金钱" 5)))
        (outcome "打翻酒杯"
          (lambda ()
            (add-item! "金钱" 3)
            (spend-composure! 1)
            (change-tavern-rank! -1)))
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
      (关系工作 "领班" "老码头" '低 'social
        (outcome "里外都稳住"
          (lambda ()
            (add-item! "金钱" 13)
            (change-tavern-rank! 1)))
        (outcome "带完一班"
          (lambda ()
            (add-item! "金钱" 8)
            (maybe-start-tavern-trouble!)))
        (outcome "场面失控"
          (lambda ()
            (add-item! "金钱" 5)
            (spend-composure! 1)
            (change-tavern-rank! -1)
            (maybe-start-tavern-trouble!)))
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
          (outcome "没能收住"
            (lambda () (spend-composure! 1)))
          (outcome "暂且压下"
            (lambda () (resolve-tavern-trouble! "压下")))
          (outcome "处理干净"
            (lambda () (resolve-tavern-trouble! "干净"))))))

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
          (outcome "买了烟"
            (lambda () (add-item! "香烟" 1))))))

    ;; 当场点一杯：效果与在家喝自带的酒完全一样，共用同一次“当天第一杯”（home 的 drank-today?）。
    ;; :resolve 用 outcome 包一层，结果才会像判定一样以锚定卡片弹出，而不是只飘过一条 notify!。
    (define (node-drink-here)
      (node "点一杯酒"
        :anchor "老街酒馆-购买"
        ;; 只有灰掉的时候才写一句——不写玩家就不知道为什么点不动。
        :subtitle (if (home 'drank-today?) "今天已经喝过了" "")
        :disabled (home 'drank-today?)
        :requires (list (req-item "金钱" 25))
        :resolve (instant
          (outcome "借酒松神"
            (lambda () (home 'drink!))
            'light))))

    ;; ── 赌钱 ──────────────────────────────────────
    ;; 这里曾有一张「去地下酒吧押一把」：花一颗骰 + 20 金，摇一次，0/20/60。
    ;; 它其实只是另一份工作，方差大一点而已——骰子花在下注上，赌博就没有赌博。
    ;; 老街只留一个赌钱的地方，而且是有人的那个：酒馆后面的地下拳场（见 艾迪.scm），
    ;; 骰子花在看懂比赛上，钱才花在票上。

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
            (地点节点 "酒馆")
            ;; 麻烦留着时暂停新一班：玩家可以立刻处理，也可以离开、
            ;; 在日终承担后果，但不能无视问题继续刷领班班次。
            (list (if (equal? tavern-trouble "无")
                      (if (foreman?) (node-foreman) (node-waiter))
                      (node-tavern-trouble))
                  (node-drink-here)
                  (node-buy-cigarettes))
            (if (three-letters 'old-street-open?)
                (list (loan-shark-container))
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
                   :arrivals (地点入场 "酒馆"))))
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
                 (list "tavern-trouble-outcome" tavern-trouble-outcome)))
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
             (if (member? tavern-trouble-outcome tavern-trouble-outcomes)
                 #t
                 (error "老街酒馆存档错误：领班麻烦收场非法"))
             ;; 麻烦还压在手里的时候不可能同时有收场：这两个状态互斥。
             (if (and (not (equal? tavern-trouble "无"))
                      (not (equal? tavern-trouble-outcome "未了")))
                 (error "老街酒馆存档错误：麻烦未了却记着收场")
                 #t)))
          (#t #f))))))
