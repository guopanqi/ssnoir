;; 贝恩斯——辖区警察局的值班警官。
;;
;; 他熟悉辖区的人，也知道怎样迅速处置一场争执；但每次先问「还有谁知道」，
;; 因为总局、报社和市政厅知道以后，现场就不再只归他处理。
;; 他能做老练的警察，也会首先作为官僚体系的一员选择哪些事进入正式记录。
;;
;; 玩家第一次看懂他，多半是酒馆门前那场：他问谁知道，凭经验动手，却没查清旧账。
;; 巷子那件事还挂着时不播；处理完或错过之后，下一次进酒馆才播，只播一次。
;; 错过也不挡警局和后面的故事。码头收工的两句只记「岸口见过」，不算认识。
;;
;; 之后三拍,分别由不同的东西开门:
;;   《给你一个回执》 —— 小节二之后可去报案；他区分来访回执与正式立案。
;;   《五点以后》     —— 酒馆初见并报案后，可在酒馆看见他下班时仍识破一起错案。
;;   《让他们安静》   —— 报案之后他认识尼尔，才私下推来一件无法立案的事。
;;
;; 警署与市政不挂在任何一条圈内声誉上。这里发生的事只改变贝恩斯自己的状态——
;; 他认不认得你、觉不觉得你靠得住、欠不欠你一次。城里没有一条叫「官僚」的关系。
;;
;; 他欠你的那一次不兑现成人手——首演夜制度本来就会派警察来。它兑现成**余地**:
;; 首演准备里那张「让他们换便装」（三封信.scm 的内环异议）。制度给你警察，他给你余地。

(define baines
  (let ()
    ;; 0 还没推地址 / 1 地址推过来了 / 2 成功 / 3 失败后收回地址
    (define quiet-stage 0)
    (define receipt? #f)          ; 那张回执还在尼尔口袋里
    (define route "")             ; 暴力 / 交易 / 施压——你用什么方式让他们安静的
    (define owed? #f)             ; 他欠你一次（首演那晚用掉）
    (define off-duty-seen? #f)    ; 《五点以后》已经看过
    (define street-seen? #f)      ; 酒馆门前那场已经看过
    (define street-day 0)         ; 两场酒馆戏至少隔一天
    (define dock-seen? #f)        ; 货船收工时见过他。只改称呼，不算认识
    (define street-armed? #f)     ; 调试：下一次进酒馆强制播，不进存档
    ;; 第二章的职业身份：未开放 / 请来 / 有效 / 暂停。
    ;; 当前只写到「有效」，但状态一次定对，后面追查线可以直接把它改成「暂停」。
    (define registration "未开放")
    (define identity "辖区警察局的值班警官")

    ;; ── 常驻:文书 ────────────────────────────────────
    ;; 文书仍是一份日结工作，但不再用重复打工次数解锁人物剧情。
    (define (node-paperwork)
      (工作 "整理警察局文书" '低 'knowledge
        (outcome (lambda () (add-item! "金钱" 10)))
        (outcome (lambda () (add-item! "金钱" 6)))
        (outcome (lambda () (spend-composure! 1)))
        :anchor "警察局-卷宗室"))

    ;; 贝恩斯是**人物**，不是一张观察卡。
    ;;
    ;; 他原来写成 (observe …)：点开只读到一段关于他的描写，而这个地点里所有
    ;; 真能做的事——文书、报案、那张地址——全都和他平级摆在外面。人站在旁边，
    ;; 他的事情摆在他身外。夜莺和弗兰克都不是这么写的（见 三封信.scm 的
    ;; nightingale-node、弗兰克.scm 的 node-frank）：人物是一张走过去的卡，
    ;; 他身上的事是他的 children。
    ;;
    ;; 原来那四段观察文字压成了副标题——一句话说清他此刻是什么样子，
    ;; 和夜莺 client-subtitle 一个写法。剩下的话由对白去说，那才是他开口的地方。
    (define (self-subtitle)
      (cond
        ((= quiet-stage 2) "他记得你替他办成过那件事，也记得你是用什么方式办的")
        ((= quiet-stage 3) "那张地址收回了卷宗底下；他只按程序处理")
        (receipt? "他知道那封信，但还没有把它当成正式案件")
        (street-seen? "门前见过他；那场斗殴没有写进正式卷宗")
        (dock-seen? "岸口见过他；在警局他先问谁已经知道")
        (else identity)))

    ;; 他那两张卡都是阶段性的：回执拿过、地址也发过之后，这张卡点进去就是空的。
    ;; 空容器读起来不像"今天没他的事"，像"是不是坏了"。所以没有动作的时候
    ;; 放一条标注，说清他此刻是什么样子——玩家看见字就知道自己没漏掉东西。
    ;; 只在空的时候放：有事可做的日子不摆，否则它就是每次都得先读一遍的墙纸。
    ;; （同 弗兰克.scm 的 node-frank-idle。）
    (define (node-self-idle)
      (note-node "标注：贝恩斯此刻" ""
        (cond
          ((= quiet-stage 2)
           "他记得你替他办成过那件事，也记得你是用什么方式办的。这不影响他对你的态度——他只关心那条街这个星期安不安静。")
          ((= quiet-stage 3)
           "那张地址收回了卷宗底下。事情闹大以后，他只按程序处理，不再提这件事。")
          (receipt?
           "他认得你，也记得那封信。来访回执还在你手里，正式案卷尚未立起。")
          (street-seen?
           "酒馆门前那位警官坐在值班台后。你没在这里看见那场斗殴的案卷。")
          (dock-seen?
           "码头抢修那天在岸口撤人的胖子，现在坐在值班台后面。他很少抬头。")
          (#t
           "值班的位子后面坐着一个胖警官。他一整天都在写字，很少抬头。"))))

    (define (node-self children)
      (node "贝恩斯"
        :anchor "警察局-值班台"
        :subtitle (self-subtitle)
        :children (if (null? children)
                      (list (node-self-idle))
                      children)))

    ;; ── 第一拍:《给你一个回执》 ───────────────────────
    ;; 他先问消息传到了哪里，再把尼尔的来访和正式立案分开。
    ;; 玩家此时还不知道他是在分配人手，还是在控制辖区留下的记录。

    (define (play-report!)
      (play-dialogue!
        (cond
          (street-seen? (line "世界" "值班台后面是酒馆门前那个人。"))
          (dock-seen? (line "世界" "值班台后面是码头上那个警官。"))
          (else (line "世界" "值班的位子后面坐着一个胖子，眼镜滑到鼻子下面。")))
        (cond
          (street-seen? (line "尼尔" "门前见过。有人写信勒索我的委托人。"))
          (dock-seen? (line "尼尔" "岸口见过。有人写信勒索我的委托人。"))
          (else (line "尼尔" "有人写信勒索我的委托人。")))
        (line "贝恩斯" "还有谁知道？")
        (line "尼尔" "夜莺、剧院经理，还有我。")
        (line "贝恩斯" "信呢？")
        (line "尼尔" "不在我手上。")
        (line "贝恩斯" "我记下你来过。信拿来，或者有人再受威胁，我再立案。")
        (line "世界" "他盖了个章，撕下一张来访回执推过来。")
        (line "尼尔" "你已经知道有人在勒索她。")
        (line "贝恩斯" "我知道你这么说。那是两回事。")))

    (define (node-report-case)
      (node "去报案"
        :anchor "警察局-值班台"
        :subtitle "把勒索信告诉贝恩斯，拿一张来访回执"
        :resolve (instant
          (outcome (lambda ()
              (set! receipt? #t)
              (play-report!)
              (result-supplement! "拿到来访回执；尚未立案"))))))

    ;; ── 第二拍:《让他们安静》 ─────────────────────────
    ;; 三个刚假释出来的小混混又在老街收钱。商户知道是谁,没人愿意作证,其中一个的
    ;; 假释监管文件还在搬档案时丢了——按程序他现在没有足够东西把人关回去。
    ;; 于是他不下委托,只把一张地址推过桌子。
    (define (node-address)
      (node "桌上那张地址"
        :anchor "警察局-值班台"
        :subtitle "他不说是委托；他说这是你的职业"
        :tags (list "交锋")
        :resolve (instant (lambda ()
          ;; 城里已有的关系决定场内有哪几张牌——不给三个混混各写一套弱点,
          ;; 玩家能用的是他这些天真认识的人：码头＝艾迪，老街＝弗兰克。
          (set-global! '钥匙-码头 (eddie 'known?))
          (set-global! '钥匙-老街 (frank 'met?))
          (set-global! '钥匙-赌场 (eddie 'known?))
          (start-encounter "让他们安静" on-quiet-result)))))

    (define (invite!)
      (set! quiet-stage 1)
      (play-dialogue!
        (line "贝恩斯" "老街那三个，上个月刚放出来。又在向商户收钱。")
        (line "尼尔" "你知道是谁，为什么不抓？")
        (line "贝恩斯" "商户不作证。一人的监管文件搬档案时丢了。")
        (line "贝恩斯" "我知道他们在干什么，眼下却没有能写进报告的证据。")
        (line "世界" "他从卷宗底下抽出一张地址，推到桌边。")
        (line "贝恩斯" "这不是警局的委托。你可以不接。")
        (line "尼尔" "接了之后呢？")
        (line "贝恩斯" "让这条街安静几天。我会知道有没有用。")))

    ;; ── 第二章 Phase A：《手续已经好了》 ───────────────
    ;; 媒体和晚宴先把尼尔叫成侦探，贝恩斯数日后只是把这个既成事实纳入程序。
    ;; 这不是执照模拟：它表示警局肯向委托人确认「有这个人」。
    (define (registered?) (equal? registration "有效"))

    (define (node-registration)
      (node "手续已经好了"
        :anchor "警察局-值班台"
        :subtitle "贝恩斯说手续已经办好了"
        :resolve (instant
          (outcome (lambda ()
              (play-dialogue!
                (line "世界" "贝恩斯桌上摆着一张已经盖过章的表，墨还没干透。")
                (line "尼尔" "这就办好了？")
                (line "贝恩斯" "章在这儿。")
                (line "尼尔" "我问过不止一次。每次都说还缺人作保。")
                (line "尼尔" "以前怎么没这么快。")
                (line "贝恩斯" "以前没人知道你是谁，我拿什么替你说话。")
                (line "贝恩斯" "现在全城都知道你是谁。用不着再找一个人说第二遍。"))
              (set! registration "有效")
              (sync-blockers!)
              (home 'connect-phone!)
              (board 'open-line!)
              (spotlight! "侦探委托"
                "你已在警局登记为私人调查员。正式调查委托现在会打到家里的电话上。"))))))

    ;; 三个手段标签不是三条剧情线,它们各自在别处兑现:
    ;;   暴力 —— 三份伤情报告。他要的结果拿到了,但他记得你是怎么办的。
    ;;   交易 —— 花掉的钱和欠下的人情记在老街那边。
    ;;   施压 —— 只算延期。记一个 flag,那三个人以后还会回来。
    (define (on-quiet-result result)
      (if (and (list? result) (= (length result) 2))
          #t
          (error "贝恩斯：《让他们安静》应回传 (list 结果 手段)"))
      (if (member? (cadr result) (list "暴力" "交易" "施压" ""))
          #t
          (error "贝恩斯：《让他们安静》返回了未登记的手段"))
      (if (equal? (car result) 'success)
          (begin
            (set! quiet-stage 2)
            (set! route (cadr result))
            (set! owed? #t)
            (cond
              ((equal? route "暴力")
               (play-dialogue!
                 (line "贝恩斯" "我说让他们安静。")
                 (line "尼尔" "他们安静了。")
                 (line "贝恩斯" "你给我制造了三份伤情报告。")
                 (line "尼尔" "不是一样？")
                 (line "贝恩斯" "医院也归市政府管。"))
               (result-supplement! "他欠你一次。这一次记得不太痛快。"))
              ((equal? route "交易")
               (play-dialogue!
                 (line "贝恩斯" "他们为什么突然这么懂事？")
                 (line "尼尔" "你不是说别问？")
                 (line "贝恩斯" "我没说过。")
                 (line "世界" "他看了你两秒，然后低头继续写。"))
               (result-supplement! "他欠你一次。"))
              (else
               (play-dialogue!
                 (line "贝恩斯" "有效。")
                 (line "贝恩斯" "能维持多久？")
                 (line "尼尔" "不知道。")
                 (line "贝恩斯" "那就不算解决，只算延期。")
                 (line "世界" "他还是把那张地址收进了抽屉。"))
               (set-flag! '老街的三个人还会回来)
               (result-supplement! "他欠你一次。那三个人只是走开了。")))
            (complete-task! "让他们安静"))
          (begin
            (play-dialogue!
              (line "贝恩斯" "巡警的报告我看了。")
              (line "尼尔" "事情闹大了。")
              (line "贝恩斯" "闹大了我就得走程序。走了程序，我这个星期就没有别的时间了。")
              (line "世界" "他把那张地址收回卷宗底下，没再提。"))
            ;; 失败是终局：这张地址不会再发一次。
            ;; 失败也算经历完，这一节照样结。
            (set! quiet-stage 3)
            (complete-task! "让他们安静"))))

    ;; ── 第三拍:《五点以后》 ───────────────────────────
    ;; 下班后仍熟悉辖区的人：他阻止巡警把错的人写进盗窃案，
    ;; 但理由里既有办案经验，也有不愿让错案进入自己辖区卷宗的算计。
    (define (node-off-duty)
      (node "靠窗那桌"
        :subtitle "贝恩斯没穿制服，面前摆着吃的"
        :resolve (instant
          (outcome (lambda ()
              (set! off-duty-seen? #t)
              (play-dialogue!
                (line "世界" "贝恩斯脱了制服外套，正吃晚饭。一个巡警押着年轻人进来。")
                (line "巡警" "码头账房丢了钱。我在门口逮到他。")
                (line "贝恩斯" "放开。钱丢的时候，他在这儿给我倒酒。")
                (line "巡警" "您确定？")
                (line "贝恩斯" "那时我刚下班。账房后门那个跑腿，才是你该找的。")
                (line "世界" "巡警松了手，转身往码头走。贝恩斯接着吃。")
                (line "尼尔" "你怎么记得这么清楚？")
                (line "贝恩斯" "我认识这条街。别把错的人写进我的报告。"))
              (result-supplement! "贝恩斯记得街上的每个人"))))))

    ;; ── 街面初见：酒馆门前 ───────────────────────────
    ;; 一次性入场。贝恩斯不查清谁先动手，就凭街面经验制住要掏家伙的人。
    ;; 他防住了下一下，却没有回答两人最初为何打起来；私人号码留到离场才说。
    (define (play-street!)
      (play-stage!
        (stage-parallel
          (stage-spawn "酒客" "酒客甲_瘫坐" 4 'middle)
          (stage-spawn "另一酒客" "酒客乙_半躺" 8 'back)
          (stage-spawn "酒保" "黑影" -8 'middle)
          (stage-spawn "贝恩斯" "贝恩斯" -15 'front)
          (stage-pause 0.6))
        (stage-say "酒客" "他拿了我的工钱！你让他把钱拿出来！")
        (stage-say "另一酒客" "我没碰你的钱。酒保看见了！")
        (stage-say "酒保" "我只看见他们砸了我的杯子。")
        (stage-move "贝恩斯" 0 0.75)
        (stage-say "贝恩斯" "还有谁知道？")
        (stage-say "酒保" "只给你打了电话。")
        (stage-say "酒客" "警官，你听我说，是他先——")
        (stage-parallel
          (stage-move "贝恩斯" 3 0.18)
          (stage-sound "老街酒馆/按住了" 4))
        (stage-say "世界" "话没说完，贝恩斯一拳打在他脸上。")
        (stage-say "酒客" "你疯了？我才是来讨钱的！")
        (stage-say "世界" "他袖口里掉出一只铜指虎。")
        (stage-say "贝恩斯" "上回砸酒馆，你也说自己是来讨钱的。")
        (stage-say "另一酒客" "那我的事呢？他刚才说我偷了钱。")
        (stage-say "贝恩斯" "我没说你没偷。把杯子的钱留下，走。")
        (stage-say "世界" "贝恩斯把铜指虎揣进口袋，推着挨打的人往街上走。")
        (stage-move "贝恩斯" -5 0.7)
        (stage-say "贝恩斯" "酒保，下回还打我那个号码。总机派人来得慢。")
        (stage-pause 0.5)))

    (define (street-due?)
      (and (not street-seen?)
           (or street-armed?
               (not (eddie 'alley-pending?)))))

    (define (arrival-street)
      (arrival "酒馆门前"
        (lambda ()
          (set! street-armed? #f)
          (set! street-seen? #t)
          (set! street-day world-day)
          (play-street!))))

    (define (debug-arm-street!)
      (set! street-seen? #f)
      (set! street-armed? #t))

    (define (note-dock-seen!)
      (set! dock-seen? #t))

    (define (arrivals-at location)
      (if (and (equal? location "老街酒馆") (street-due?))
          (list (arrival-street))
          '()))

    ;; 第一章一张卡《让他们安静》：他推过来那张地址时立卡，交锋结了就了结。
    ;; 第二章「贝恩斯叫你去警局拿那张纸」是演出，玩家不用做什么，不立卡。
    (define (dossier-entry)
      (if (>= quiet-stage 1)
          (list (dossier "让他们安静"
                  :kind '人物
                  :status (if (>= quiet-stage 2) '了结 '进行中)
                  :now (cond
                         ((= quiet-stage 1) "去警察局，照他桌上那张地址办：让老街那三个人安静下来")
                         (#t ""))
                  :where (if (= quiet-stage 1) "警察局" "")
                  :steps (list (step "他推过来一张地址" (>= quiet-stage 1))
                               (step "让老街那三个人安静" (= quiet-stage 2)))))
          '()))

    (define (nodes-at location)
      (cond
        ((equal? location "警察局")
         ;; 人物卡里只装**他的事**：跟他说话、他推给你的东西。
         ;; 生计工作留在地点层，和老街酒馆一个样——职级钟和服务员/领班都摆在
         ;; 酒馆上，不塞进哪个人肚子里。文书是这个地方的活，不是贝恩斯的随身物品；
         ;; 埋进人物卡里等于每天赚钱前先点开一个人。
         (append
           (list (node-self
                   (append
                     (if (等口信?) (list (node-registration-countdown)) '())
                     (if (equal? registration "请来") (list (node-registration)) '())
                     (if (or receipt? (three-letters 'has-flag? '第三封信))
                         '()
                         (list (node-report-case)))
                     (if (= quiet-stage 1) (list (node-address)) '()))))
           (list (node-paperwork))))
        ;; 报案后回酒馆即可看见他下班的一面，不用先完成另一条支线。
        ((equal? location "老街酒馆")
         (if (and (not off-duty-seen?) receipt? street-seen? (> world-day street-day))
             (list (node-off-duty))
             '()))
        (else '())))

    ;; 报案之后他认得尼尔，才私下推来程序处理不了的街面事。
    (define-turn-rule "贝恩斯推过来一张地址"
      (lambda ()
        (and (= quiet-stage 0) receipt?))
      (lambda () (invite!)))

    ;; 晚宴后隔一天来口信。它不是任务，不进卷宗（不发成长、没有子项），是一段必经的
    ;; 演出：晚宴上提过的事，一天里在警局贝恩斯那儿挂着倒计时；口信一到就堵住休息，
    ;; 直到你去警局把那张纸拿了。曾经既看不见倒计时也不堵人——那张纸接通的是家里的
    ;; 电话和上城委托，忘了去等于整条委托线悄悄没开。
    (define 口信间隔 1)
    (define (等口信?) (and (equal? registration "未开放")
                           (第二章 'started?)
                           (equal? (晚宴 'result) "已结束")))
    (define (口信日) (+ (第二章 'banquet-day) 口信间隔))

    (define (sync-blockers!)
      (rest-release! "贝恩斯/登记")
      (if (equal? registration "请来")
          (rest-block! "贝恩斯/登记" "贝恩斯有张纸要给你" "警察局" "手续已经好了")
          #f))

    (define-turn-rule "贝恩斯叫你去警局"
      (lambda ()
        (and (等口信?) (>= world-day (口信日))))
      (lambda ()
        (set! registration "请来")
        (sync-blockers!)
        (play-remote-dialogue!
          (line "世界" "一个巡警在楼下等你。")
          (line "巡警" "贝恩斯让你去警局一趟。不是问话。他只是有张纸要给你。"))))

    ;; 倒计时挂在他的值班台上，不进卷宗：这一天他在办那份手续，办完会叫你。
    (define (node-registration-countdown)
      (node "钟：贝恩斯在办手续"
        :anchor "警察局-值班台"
        :resolve (clock (日期倒计时 "手续办好" (口信日) 口信间隔
                          "晚宴上说起的那份备案。办好了他会叫你来拿。"))))

    (lambda args
      (let ((msg (car args)))
        (cond
           ((equal? msg 'nodes-at) (nodes-at (cadr args)))
           ((equal? msg 'dossier) (dossier-entry))
           ((equal? msg 'arrivals-at) (arrivals-at (cadr args)))
           ((equal? msg 'debug-arm-street!) (debug-arm-street!))
           ((equal? msg 'note-dock-seen!) (note-dock-seen!))
           ((equal? msg 'street-seen?) street-seen?)
           ((equal? msg 'dock-seen?) dock-seen?)
           ((equal? msg 'known?) (or street-seen? receipt? (> quiet-stage 0)))
          ((equal? msg 'receipt?) receipt?)
          ((equal? msg 'owed?) owed?)
          ((equal? msg 'registered?) (registered?))
          ((equal? msg 'registration) registration)
          ((equal? msg 'sync-blockers!) (sync-blockers!))
          ;; 首演那晚用掉他那一次：一次就没了，别让它变成常驻特权。
          ((equal? msg 'spend-favor!) (set! owed? #f))
          ((equal? msg 'route) route)
          ((equal? msg 'save)
           (list (list "quiet-stage" quiet-stage)
                 (list "receipt" (if receipt? 1 0))
                 (list "route" route)
                 (list "owed" (if owed? 1 0))
                 (list "off-duty-seen" (if off-duty-seen? 1 0))
                 (list "street-seen" (if street-seen? 1 0))
                 (list "street-day" street-day)
                 (list "dock-seen" (if dock-seen? 1 0))
                 (list "registration" registration)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! quiet-stage (assoc-get data "quiet-stage" 0))
             (set! receipt? (= (assoc-get data "receipt" 0) 1))
             (set! route (assoc-get data "route" ""))
             (set! owed? (= (assoc-get data "owed" 0) 1))
             (set! off-duty-seen? (= (assoc-get data "off-duty-seen" 0) 1))
             (set! street-seen? (= (assoc-get data "street-seen" 0) 1))
             (set! street-day (assoc-get data "street-day" 0))
             (set! dock-seen? (= (assoc-get data "dock-seen" 0) 1))
             (set! registration (assoc-get data "registration" "未开放"))
             (if (member? registration (list "未开放" "请来" "有效" "暂停"))
                 #t
                 (error "贝恩斯存档错误：私人调查员登记状态非法"))
             #t))
          (else #f))))))
