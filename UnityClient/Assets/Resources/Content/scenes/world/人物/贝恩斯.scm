;; 贝恩斯——辖区警察局的值班警官。
;;
;; 他不认为自己的工作是实现正义,他认为自己的工作是让这座城市每天还能继续运转。
;; 正义不是无限资源:城里每天几百件事,警局十二个人,预算就那么多。所以他上班的时候
;; 很认真——程序正确、证据够了就抓人、有人真要杀人他会拔枪——但他拒绝假装每一件
;; 报上来的事都会有人去查。
;;
;; 他有三拍,分别由不同的东西开门:
;;   《给你一个回执》 —— 小节二之后,警察局开着,去不去随玩家。四个程序问题读的是
;;                       玩家这几天真干过的事,所以敷衍的材料是玩家自己提供的。
;;   《让他们安静》   —— 他认得你办事靠得住就出现。这是贝恩斯自己的交好线，
;;                       是他自己数出来的：替警察局把文书办利落三回，他才肯把
;;                       一件程序管不了的事推给你。
;;   《五点以后》     —— 他欠你一次之后才出现。在酒馆，他已经下班了。
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
    ;; 第二章的职业身份：未开放 / 请来 / 有效 / 暂停。
    ;; 当前只写到「有效」，但状态一次定对，后面追查线可以直接把它改成「暂停」。
    (define registration "未开放")
    (define paperwork-clk
      (make-clock "办事印象" 3 'gauge
        "只有把文书办得利落，才会让贝恩斯把程序管不了的事交给你。"))
    (define (reliable?) (paperwork-clk 'full?))
    (define identity "辖区警察局的值班警官")

    ;; ── 常驻:文书 ────────────────────────────────────
    ;; 低风险的桌面文书活:一份体面的日结,不属于任何圈子——警察局不是一个会传你名声的圈子,
    ;; 它只是一个人在数你办砸过几回。坏结果只是返工伤神(冷静 -1),不倒扣任何东西：
    ;; 搞错日期是返工不是失职。
    ;; 只有「办得利落」计进履历:按序归档是交了差,不是办得漂亮。三回之后他才开口。
    (define (node-paperwork)
      (工作 "整理警察局文书" '低 'knowledge
        (outcome (lambda ()
            (add-item! "金钱" 10)
            (let ((was-full? (paperwork-clk 'full?)))
              (paperwork-clk 'tick!)
              (if (and (not was-full?) (paperwork-clk 'full?))
                  (notify! "贝恩斯把你归好的卷宗推到一边，没说话。他记住了这只手。")
                  #f))))
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
        (receipt? "眼镜滑到鼻子下面。他不觉得你的案子不重要，是排不上")
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
           "眼镜滑到鼻子下面，桌上堆着卷宗。他不觉得你的案子不重要，他觉得它现在排不上。")
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
    ;; 尼尔来留一笔，贝恩斯给一张回执就把人打发走。不复述玩家先前的经历；
    ;; 这一拍只为第三封信之后警察局主动出人的那一刻留下对照。

    (define (node-report-case)
      (node "去报案"
        :anchor "警察局-值班台"
        :subtitle "去警察局留一笔；他未必会管"
        :resolve (instant
          (outcome (lambda ()
              (set! receipt? #t)
              (play-dialogue!
                (line "世界" "值班的位子后面坐着一个胖子，眼镜滑到鼻子下面。")
                (line "尼尔" "有人写信勒索我的委托人。")
                (line "贝恩斯" "信在哪里？")
                (line "尼尔" "不在我手上。")
                (line "世界" "他盖了个章，撕下一张纸条推过来。")
                (line "贝恩斯" "有进一步情况再来。")
                (line "尼尔" "就这样？")
                (line "贝恩斯" "下一位。"))
              (result-supplement! "你拿到一张回执。案子没有立。"))))))

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
        (line "贝恩斯" "老街那三个，上个月刚放出来。")
        (line "尼尔" "抓回去。")
        (line "贝恩斯" "商户不作证。其中一个的监管文件搬档案的时候丢了。")
        (line "世界" "他从卷宗底下抽出一张纸，写了个地址，推过来。")
        (line "贝恩斯" "最近去看看他们。")
        (line "尼尔" "然后呢？")
        (line "贝恩斯" "让他们安静。")
        (line "尼尔" "怎么安静？")
        (line "贝恩斯" "这类事你不是已经在做了吗。")))

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
              (spotlight! "侦探委托"
                "你已在警局登记为私人调查员。家里的联络电话已经接通，上城客户的调查委托会直接找上门。"))))))

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
            ;; 失败是终局：办事印象不会被扣回，但这张地址也不会再发一次。
            ;; 失败也算经历完，这一节照样结。
            (set! quiet-stage 3)
            (complete-task! "让他们安静"))))

    ;; ── 第三拍:《五点以后》 ───────────────────────────
    ;; 他没穿制服，桌上有吃的。有人喊他，他连头都不抬——然后那边真掏了刀，
    ;; 他立刻站起来，分开、收刀、叫巡警，回来接着吃已经凉了的东西，一句抱怨都没有。
    ;; 这一拍玩家不做任何决定，所以它是演出，不是交锋：他真的把警察当工作，
    ;; 但这不意味着工作是假的——他只是拒绝把整个人格都献给它。
    (define (node-off-duty)
      (node "靠窗那桌"
        :subtitle "贝恩斯没穿制服，面前摆着吃的"
        :resolve (instant
          (outcome (lambda ()
              (set! off-duty-seen? #t)
              (play-dialogue!
                (line "世界" "他没穿制服外套，眼镜推在额头上，桌上摆着一份还冒气的东西。")
                (line "尼尔" "警官。")
                (line "贝恩斯" "五点以后不是。")
                (line "世界" "门口那边两个人开始互相推搡。有人回头喊了一声。")
                (line "酒客" "警官！")
                (line "贝恩斯" "打死人再叫我。")
                (line "尼尔" "你认真的？")
                (line "贝恩斯" "非常认真。"))
              (play-dialogue!
                (line "世界" "然后那边掏了刀。")
                (line "世界" "他放下叉子，站起来，走过去。")
                (line "世界" "他把两个人分开，把刀收进口袋，让酒保去街口叫巡逻的。")
                (line "世界" "他站在那儿等人来，等笔录记完，等两个人被带走。")
                (line "世界" "回到桌边的时候，那份东西已经凉了。"))
              (play-dialogue!
                (line "尼尔" "你不是下班了。")
                (line "贝恩斯" "他掏刀了。")
                (line "世界" "他坐下来，接着吃。一句抱怨也没有。"))
              (result-supplement! "你见过他下班的样子了。"))))))

    ;; 第一章一张卡《让他们安静》：他推过来那张地址时立卡，交锋结了就了结。
    ;; 文书那三回不立卡——那时候玩家还不知道这会通向什么；卡开的时候第一项已经划掉，
    ;; 读起来就是「原来那几趟文书是为这个」。
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
                  :steps (list (step "替警察局把文书办利落" (reliable?))
                               (step "他推过来一张地址" (>= quiet-stage 1))
                               (step "让老街那三个人安静" (>= quiet-stage 2)))))
          '()))

    (define (nodes-at location)
      (cond
        ((equal? location "警察局")
         ;; 人物卡里只装**他的事**：跟他说话、他推给你的东西。
         ;; 生计工作留在地点层，和老街酒馆一个样——职级钟和服务员/领班都摆在
         ;; 酒馆上，不塞进哪个人肚子里。文书是这个地方的活，不是贝恩斯的随身物品；
         ;; 埋进人物卡里等于每天赚钱前先点开一个人。
         ;; 办事印象那根钟跟着工作走，不跟着人走：它记的就是这张卡的战绩，
         ;; 要摆在你按下去的地方（同 老街酒馆 的「酒馆职级」）。
         (append
           (list (node-self
                   (append
                     (if (等口信?) (list (node-registration-countdown)) '())
                     (if (equal? registration "请来") (list (node-registration)) '())
                     (if (or receipt? (three-letters 'has-flag? '第三封信))
                         '()
                         (list (node-report-case)))
                     (if (= quiet-stage 1) (list (node-address)) '()))))
            (if (not (reliable?))
                (list (node "钟：贝恩斯的办事印象"
                        :anchor "警察局-卷宗室"
                        :resolve (clock (paperwork-clk 'render-data))))
                '())
           (list (node-paperwork))))
        ;; 酒馆那张只在他下班以后、而且你已经替他办成过那件事之后才在:
        ;; 门是他欠你的那一次——不是一条声誉，是他自己知道欠着。
        ((equal? location "老街酒馆")
         (if (and (not off-duty-seen?) owed?)
             (list (node-off-duty))
             '()))
        (else '())))

    ;; 门槛只是「他认得你办事靠得住」：不要求先报案，也不被第三封信截断。
    ;; 这是贝恩斯自己的交好线，不是夜莺主线的旁支时限任务。
    (define-turn-rule "贝恩斯推过来一张地址"
      (lambda ()
        (and (= quiet-stage 0) (reliable?)))
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
           ((equal? msg 'known?) (or receipt? (reliable?) (> quiet-stage 0)))
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
                 (list "registration" registration)
                 (list "paperwork-done" (paperwork-clk 'save))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! quiet-stage (assoc-get data "quiet-stage" 0))
             (set! receipt? (= (assoc-get data "receipt" 0) 1))
             (set! route (assoc-get data "route" ""))
             (set! owed? (= (assoc-get data "owed" 0) 1))
             (set! off-duty-seen? (= (assoc-get data "off-duty-seen" 0) 1))
             (set! registration (assoc-get data "registration" "未开放"))
             (if (member? registration (list "未开放" "请来" "有效" "暂停"))
                 #t
                 (error "贝恩斯存档错误：私人调查员登记状态非法"))
             (paperwork-clk 'load! (assoc-get data "paperwork-done" 0))))
          (else #f))))))
