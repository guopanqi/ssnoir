;; scenes/world/三封信.scm - 第一章「三封信」主线模块
;;
;; 设计见 docs/第一章·三封信.md。这个闭包拥有第一章的全部故事状态,
;; 世界协调器只负责日历、地点与存档转发,不解释故事。
;;
;; 章节的两个日子是钉死的:
;;   勒索日   = 登门当天 + 3(信上写的期限),小节一的到期
;;   首演之夜 = 第 22 天,全章可见,整章的总长上限
;; 中间的分界浮动:小节二由账单结清推进,越晚办完,小节三的准备天数越少。

(define three-letters
  (let ()
    ;; ── 常量 ────────────────────────────────────────
    (define prepayment 20)          ; 她先拿得出的部分,也够玩家续几天房租
    ;; 二十而不是三十:开局兜里 15,加上这笔就是 35——离信上那一百还差六成半。
    ;; 三十的时候是四十五,差不多一半,三天里随便干两班就够,那条死线不咬人。
    (define delivery-price 100)     ; 勒索信要求放进邮箱的总额
    (define nightingale-cover-max 30) ; 她能当掉的首饰值这么多——兜底到此为止
    (define letter-deadline 3)      ; 信上写的期限:三天后
    ;; 首演之夜的日子在第三封信落地那天才定下来,不是全章写死的绝对日。
    ;; 写死的时候倒计时算的是「22 减去你今天第几天」——决赛的紧张度成了前三小节的找零:
    ;; 玩得快的人剩十二天,磨蹭的人剩三天,而这跟他布置得完布置不完毫无关系。
    ;; 从第三封信起算,钟上的数字字面上就等于「我还有几天布置」。
    ;;
    ;; 五天里有三处莱恩调查和一轮剧院检查；每处都是 0/3，另有两个漏洞可修。
    ;; 主角每天四骰,而上一节那次「假的结束」刚把他的钱花光——房租、伤、吃饭
    ;; 全压在这五天。内容全部做得完，但会挤压生活与恢复；玩家决定调查到多深、堵哪个洞。
    (define premiere-prep-days 5)
    (define premiere-day 22)        ; 第三封信落地时改写;这个初值只是没走到那一步时的占位

    ;; ── 小节二·老街 ─────────────────────────────────
    ;; 目标只有一句话：查出那个抽「老金牌」香烟的人是谁。
    ;;
    ;; 这一节分两趟。第一趟尼尔独自去,东西两条路都走不动——不是失败,是没人开口。
    ;; 它有自己的短钟,填满即结束,产出不是线索,是「我一个人办不了这件事」。
    ;; 第二趟她跟着去。四个空间前沿原样复用,判定全部走 social:问题从「看不看得懂这栋楼」
    ;; 变成「这栋楼的人认不认她」。每推进一格出一段遭遇,坏结果不是走错路,是她被认出来。
    ;;
    ;; 东、西两条通路并行往里长，这一节的结构就只有这两条。每条通路由两个 0/4 的
    ;; 空间前沿组成：走完一段，旧动作消失，新动作出现在更深处的 Unity Anchor。
    ;; 走通东侧＝在工会房间见到弗兰克，排除他；走通西侧＝桥廊的住户叫出莱恩的名字。
    ;;
    ;; 这里曾经还套着第二层结构：先在门廊的杂货商奥托那儿刷一根 0/8 的信任钟，
    ;; 换两张写着门牌的地址纸条，再拿纸条去东、西两处开锁。它删掉了——
    ;; 一节里同时跑「两条路」和「钥匙配锁」，玩家读到的不是两个结构，是一条被拉长的路：
    ;; 走通了却还进不去，进不去的理由在另一个人身上。走到那儿本身就该是答案。
    (define warren-leg-max 4)       ; 每个空间前沿的长度；两段合计仍与旧 0/8 路线相同
    (define warren-alone-max 6)     ; 独自打听：2、4 格各见一次反应，6 格才得出结论
    ;; 名字到手之后，一个人再把东侧走通要花的功夫。
    ;;
    ;; 它必须和"带着她走东侧"一样贵（8 格），否则顺序本身会惩罚玩家：
    ;; 东侧两段合起来 8 格，先走西边拿到名字，东侧就塌成一根 6 格的独走钟——
    ;; 于是**先西后东是更省骰子的打法，而它恰好烧掉内容最多的那一边**
    ;; （门廊三段、回廊三段遭遇，全都要她在场才成立）。
    ;; 拉到 8 格之后，两种顺序花的骰子一样多，玩家凭故事选，不凭省。
    ;; 一个人一层一层敲门本来也不该比有人替你开口更快。
    (define frank-search-max 8)
    (define manager-fee 70)         ; 调查费只为找到名字结算；东路的回报是弗兰克本人

    ;; ── 平静期与小节三 ───────────────────────────────
    ;; ── 小节三·教训莱恩 ─────────────────────────────
    (define second-letter-day 7)    ; 第二封信钉死在第 7 天:经理由此登场
    (define patience-max 6)         ; 经理的耐心
    (define patience-interval 1)    ; 每次结束一天自己掉一格
    ;; 带着进展去见他能补回几格。补 2 太少：耐心一共 6 格、每天掉 1 格，
    ;; 补两格等于只买回两天，玩家读不出"让他觉得这件事在往前走"有什么用。
    (define patience-report 4)
    ;; 小节三是一段旅途:三道关口(进得去 → 找得着 → 落单),每一关都是一把锁配几把钥匙。
    ;; 钥匙全部来自前两节认识的人;没有钥匙也过得去,代价是惊动更多人。
    ;; 一路惊动的人数不在城里结算,直接成为巷子那一场「巷口的人」的起始格。
    ;; 小节三：经理雇的人去找莱恩,要三天。这三天是玩家自己的——挣钱、交租、
    ;; 在码头和老街过日子。而那些日子正是在配钥匙:你认识了谁,进场时就有哪几手。
    ;; 三天不可加速:一旦能花骰子催,最优解就是催,这段时间又变回一张跑腿卡。
    (define inquiry-days 2)

    ;; ── 平静期：假的结束 ────────────────────────────
    ;; 巷子之后到第三封信之间的三天。它不是又一段等待——小节三已经用过一次
    ;; 「等三天、不可加速」。这三天的目标是让玩家松手:经理结前半段报酬,
    ;; 城里能做的全是收尾性、消耗性的事。等第三封信落下来,他手上什么也不剩。
    ;; 旧账结了以后到第三封信之间，留两天让经理真的换人去办、夜莺继续排练。
    ;; 第三天新威胁才落下：这时经理重新找你是因为替代方案碰上了剧院内部的信，
    ;; 不是刚解雇你就把话收回。首演日从第三封信当天起算，准备期仍固定五天。
    (define quiet-days 2)           ; 旧账之后，第三天触发第三封信
    (define interim-fee 60)         ; 巷子办完后的收尾费
    ;; 卷宗那根钟的上限就是布置期本身,不再需要额外的显示窗口去截断绝对日算出来的剩余。
    (define (premiere-window) premiere-prep-days)

    ;; ── 小节四·第三封信 ─────────────────────────────
    ;; 剧院是一座旧圆形剧场(早年办拳赛):没有侧台、没有幕布、没有后台屏障,
    ;; 她在正中央,三百六十度都是人。城市侧在这张图上布置,首演之夜同一张图活过来。
    ;; 三个环是串起来的:她被挤下台就退到内环,要走回舞台必须穿过人群;
    ;; 从外圈门进来的人能直接走到她跟前。
    ;; 格子是布置,人是活的——人手不占准备格,那天晚上他们各按自己的脾气做事。
    ;; 这一节有两件并行的事：检查剧院，以及去三处寻找莱恩留下的线索。
    (define line-steps 3)
    (define repair-steps 3)         ; 每处漏洞都要积累修复，不是一骰即成

    ;; ── 状态 ────────────────────────────────────────
    ;; 0=未开场 1=小节一·勒索信 2=小节二·老街 3=平静期 4=小节三 5=首演之后
    ;; 后续小节在各自批次接入,不预留空壳。
    (define story-stage 0)
    ;; 履历：已经发生过什么，一拍一句，最新在前。显式写，不自动抽。
    ;; 第一章拆成四张卷宗卡（见文末「卷宗」），每张卡自己一份履历，写的时候点名。
    (define task-ids (list "勒索信" "老街" "莱恩" "首演之夜"))
    (define task-journals (map (lambda (id) (list id (make-journal))) task-ids))
    (define (task-journal id)
      (let ((found (assoc-get task-journals id #f)))
        (if found found (error (string-append "三封信：没有叫「" id "」的卷宗卡")))))
    (define (log! id text) ((task-journal id) 'add! text))
    (define delivery-day 0)         ; 勒索日的世界日,开场当天算出
    (define delivery-pending? #f)   ; 勒索日已到、尚未处理
    (define delivery-result "未定") ; 未定 / 拿到线索 / 跟丢
    (define delivery-money 0)       ; 投信那夜从街上捡回来的钱
    ;; 半包「老金牌」香烟和巷子里拿到的照片/底片都做成物品,不做成脚本内部的布尔量:
    ;; 它们在虚构里就是揣在兜里、能递给别人的东西,做成物品玩家才在物品栏里一直看得见。
    ;; 判断线：能不能放进兜里、递给别人、被人拿走。是就做物品;
    ;; 「已经查清了」「已经交给萨姆了」这种没有实体的,仍旧留在状态里。
    (define cigs-item "半包「老金牌」香烟")
    (define legacy-cigs-item "半包「老金牌」")
    (define (found-cigarettes?) (> (item-count cigs-item) 0))
    (define delivery-shortfall -1)  ; -1=尚未装包;0=玩家备足;正数=夜莺补上的差额
    (define condition-level 0)      ; 夜莺处境 0=稳定 1=不安 2=受伤
    ;; 她答应跟你回老街的日子。0＝还没开口；否则＝她说的那两天到期的世界日。
    ;; 不做成「去酒馆请她」的动作：那一趟跑腿不改变玩家要做的事，只是让他再读一次
    ;; 目标、再点一次对话。碰壁的那一刻她就在场，让她当场把话说完。
    (define singer-guiding-day 0)
    (define singer-wait-days 2)
    (define singer-guiding-told? #f) ; 她在巷口等你那条消息已经播过
    (define west-friction-cleared? #f) ; 西侧楼梯走完后出现的阻碍已经解决
    (define union-checked? #f)      ; 已在工会房间见过弗兰克，排除了他
    (define bridge-identified? #f)  ; 已从桥廊住户处问出莱恩
    (define envelope-thin? #f)      ; 投信那夜信封没凑够——他回去数过了
    (define patience-day 0)         ; 上一次经理耐心掉格的世界日
    (define manager-warning-level 0) ; 0=未提醒，1=四格谈话，2=两格谈话
    (define manager-phone-given? #f)
    (define report-pending "")      ; 等待向经理汇报："" / "查明" / "巷子失败"
    (define alley-thrown-out? #f)   ; 有一晚在弗兰克门口被送回路口
    (define inquiry-day 0)          ; 经理的人开始找莱恩那天
    (define inquiry-told? #f)       ; 三天到期的那条消息已经播过
    (define lesson-done? #f)        ; 巷子那一场已经了结
    (define lesson-day 0)           ; 了结当天的世界日
    (define lesson-route "无")      ; 巷子结果：谈 / 失败
    (define her-answer "无")        ; 听完她那句话，你回了什么：追究 / 摊牌 / 照帮 / 沉默
    ;; 三条调查线各自走到第几步（0..3）。走满即交出那一片。
    (define man-step 0)             ; 人：老街，莱恩不见了
    (define door-step 0)            ; 门：剧院后台，那个临时机工
    (define paper-step 0)           ; 码头：莱恩离开前在怕什么
    (define theater-step 0)         ; 剧院安全检查 0..3；与寻找莱恩分开
    ;; 剧院的两个漏洞：0 未发现 / 1 发现了没堵 / 2 堵上了
    (define hole-vent 0)            ; 台底那个封了一年的通风口
    (define hole-power 0)           ; 后廊那只没换过的配电箱
    (define visitor-shown 0)        ; 已经播过几次敲门
    (define material-settled? #f)   ; 小节二是否已经了结(查明身份)
    (define settle-route "无")      ; 查明 / 无
    (define lyon-fate "无")         ; 逃走 / 被释放 / 被扣押
    (define settled-day 0)          ; 小节二结算当天的世界日
    (define premiere-done? #f)      ; 首演之夜已结算
    (define scene-flags '())

    ;; ── flag 登记 ───────────────────────────────────
    ;; 未登记的 flag 直接报错,避免拼错字悄悄变成一个新状态。
    (define (flag-id flag)
      (cond
        ((or (equal? flag '勒索信已结算) (equal? flag "勒索信已结算")) "勒索信已结算")
        ((or (equal? flag '伤后探望) (equal? flag "伤后探望")) "伤后探望")
        ((or (equal? flag '初到居民区) (equal? flag "初到居民区")) "初到居民区")
        ((or (equal? flag '第二封信来电) (equal? flag "第二封信来电")) "第二封信来电")
        ((or (equal? flag '第二封信) (equal? flag "第二封信")) "第二封信")
        ((or (equal? flag '警察局开放) (equal? flag "警察局开放")) "警察局开放")
        ((or (equal? flag '她的过去) (equal? flag "她的过去")) "她的过去")
        ((or (equal? flag '留下的信) (equal? flag "留下的信")) "留下的信")
        ((or (equal? flag '她说起莱恩) (equal? flag "她说起莱恩")) "她说起莱恩")
        ((or (equal? flag '她问了你) (equal? flag "她问了你")) "她问了你")
        ((or (equal? flag '她剪底片) (equal? flag "她剪底片")) "她剪底片")
        ((or (equal? flag '第三封信来人) (equal? flag "第三封信来人")) "第三封信来人")
        ((or (equal? flag '前段报酬) (equal? flag "前段报酬")) "前段报酬")
        ((or (equal? flag '被经理解雇) (equal? flag "被经理解雇")) "被经理解雇")
        ((or (equal? flag '受邀看首演) (equal? flag "受邀看首演")) "受邀看首演")
        ((or (equal? flag '看彩排) (equal? flag "看彩排")) "看彩排")
        ((or (equal? flag '第三封信) (equal? flag "第三封信")) "第三封信")
        ((or (equal? flag '她不取消) (equal? flag "她不取消")) "她不取消")
        ((or (equal? flag '她的原则) (equal? flag "她的原则")) "她的原则")
        ((or (equal? flag '对不上) (equal? flag "对不上")) "对不上")
        ((or (equal? flag '薇拉) (equal? flag "薇拉")) "薇拉")
        ((or (equal? flag '灯亮起来) (equal? flag "灯亮起来")) "灯亮起来")
        ((or (equal? flag '是他) (equal? flag "是他")) "是他")
        ((or (equal? flag '结案) (equal? flag "结案")) "结案")
        (else (error "三封信 flag 未登记"))))

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

    ;; ── 派生 ────────────────────────────────────────
    (define (days-to-delivery) (max 0 (- delivery-day world-day)))
    (define (days-to-premiere) (max 0 (- premiere-day world-day)))
    (define (beat1-open?) (and (= story-stage 1) (not delivery-pending?)))

    ;; 她在哪，跟着剧情走，不是钉死在酒馆：
    ;;   入队期间——她就在你队伍里，任何地点都不该再发一张「夜莺」卡，
    ;;              否则会出现她一边陪你跑老街、一边在台上唱歌。
    ;;   小节二结算之后——首演进入排练，她的时间归剧院。
    ;;   在那之前——老街酒馆，那是她的活儿，也是玩家天天见得到她的地方。
    (define (singer-present?)
      (and (>= story-stage 1) (<= story-stage 4)
           (not premiere-done?)
           (not (has-companion? '夜莺))))

    (define (singer-at-theater?) (or (>= story-stage 3) material-settled?))
    (define (singer-location)
      (if (singer-at-theater?) "剧院" "老街酒馆"))

    ;; 老街居民区从小节一结算后开放；警察局与货运公司留到首演威胁明确后。
    ;; 酒馆是例外:她在那儿唱歌,开场就得能找到人(见 world.scm 的地点表)。
    (define (old-street-open?) (>= story-stage 2))
    ;; 小节二由投信后的来访开启：夜莺已经把方向和她自己的过去交给你，调查不能再等经理。
    (define (beat2-open?)
      (and (= story-stage 2) (has-flag? '伤后探望) (not material-settled?)))

    (define (second-letter-call-due?)
      (and (= story-stage 2) (has-flag? '伤后探望)
           (not (has-flag? '第二封信来电)) (not (has-flag? '第二封信))
           (>= world-day second-letter-day)))

    (define (second-letter-pending?)
      (and (= story-stage 2) (has-flag? '第二封信来电) (not (has-flag? '第二封信))))

    ;; 剧院从经理等候的那天起开放；那封信和经理都在那里，不该从世界根节点跳出来。
    (define (theater-open?) (or (>= story-stage 3) (has-flag? '第二封信) (second-letter-pending?)))
    ;; 小节三之后到第三封信之间的那几天:主线没有新压力。
    (define (quiet-period?)
      (and (= story-stage 3) lesson-done? (not (report-pending?))
           (or (equal? lesson-route "失败") (has-flag? '她剪底片))
           (not (has-flag? '第三封信))))
    (define (third-letter-due?)
      (and (= story-stage 3) lesson-done? (not (report-pending?))
           (or (equal? lesson-route "失败") (has-flag? '她剪底片))
           (not (has-flag? '第三封信))
           (>= (- world-day lesson-day) quiet-days)))
    (define (beat3-open?) (and (= story-stage 4) (not premiere-done?)))
    ;; 见过经理、读完第二封信后的次日，尼尔重新衡量保密与报案，警察局才出现在地图上。
    ;; 那扇门开着不等于有人管你。玩家可以走进去报案，
    ;; 拿到的是贝恩斯的四个程序问题和一张回执（见 人物/贝恩斯.scm）。
    ;; 「你不必去求」的反转因此不再靠锁门来保：你去过了，什么也没拿到；
    ;; 第三封信一出现，他自己找上门来。玩家心里有个具体的对照物，比锁着更狠。
    (define (police-open?) (has-flag? '警察局开放))
    (define (freight-open?) (beat3-open?))
    (define (condition-label)
      (cond
        ((= condition-level 0) "稳定")
        ((= condition-level 1) "不安")
        ((= condition-level 2) "受伤")
        (else (error "三封信：夜莺处境等级非法"))))

    (define (sync-globals!)
      (set-global! '第一章阶段 story-stage)
      (set-global! '夜莺处境 (condition-label))
      (set-global! '勒索信结果 delivery-result)
      (set-global! '小节二路线 settle-route)
      (set-global! '莱恩下落 lyon-fate)
      (set-global! '经理解雇过尼尔 (has-flag? '被经理解雇))
      ;; 首演交锋读两个漏洞各在哪一档。
      (set-global! '漏洞-通风口 hole-vent)
      (set-global! '漏洞-配电箱 hole-power))

    ;; 调试专用：把第一章直接放到「公开结案」那一刻，好让第二章能被单独手玩。
    ;; 它只写结束态本身——阶段、首演已结算、几个必看的 flag——不补演任何一场戏，
    ;; 所以它做不出一份「真玩过第一章」的存档，只能用来试第二章的东西。
    ;; 四张卷宗卡读的那些状态也要一并写到「了结」：不然跳过去以后
    ;; 勒索信、莱恩还挂着「进行中」，老街那张根本不出现。
    (define (debug-finish!)
      (set! story-stage 5)
      (set! premiere-done? #t)
      ;; 勒索信
      (set-flag! '勒索信已结算)
      (set-flag! '伤后探望)
      ;; 老街
      (warren-alone-clk 'set! (warren-alone-clk 'max))
      (set! singer-guiding-day 1)
      (set! bridge-identified? #t)
      (set! material-settled? #t)
      (set! settle-route "查明")
      ;; 莱恩
      (set! inquiry-day 1)
      (set! lesson-done? #t)
      (set! lesson-route "谈")
      (set-flag! '她剪底片)
      ;; 首演之夜
      (set-flag! '她不取消)
      (set! man-step line-steps)
      (set! door-step line-steps)
      (set! paper-step line-steps)
      (set! theater-step 3)
      (set-flag! '警察局开放)
      (set-flag! '第二封信)
      (set-flag! '第三封信)
      (set-flag! '灯亮起来)
      (set-flag! '是他)
      (set-flag! '结案)
      (sync-globals!)
      (sync-blockers!))

    (define (advance-stage! new-stage)
      (set! story-stage new-stage)
      (sync-globals!))

    (define (report-pending?)
      (not (equal? report-pending "")))

    (define (pending-report-action-name)
      (cond
        ((equal? report-pending "查明") "报告莱恩身份")
        ((equal? report-pending "巷子失败") "空手回去见经理")
        (else (error "三封信：没有可显示的待汇报结果"))))

    (define (pending-report-subtitle)
      (cond
        ((equal? report-pending "查明")
         "把莱恩的名字、来路和夜莺与他的关系交代给经理；不消耗行动骰")
        ((equal? report-pending "巷子失败")
         "东西没有拿到，莱恩也没有答应收手；不消耗行动骰")
        (else (error "三封信：没有可显示的待汇报说明"))))

    (define (report-critical?)
      (and (report-pending?) (<= (patience-clk 'current) 1)))

    (define (mark-report-pending! report-id)
      (if (report-pending?)
          (error "三封信：上一份结果尚未汇报，不能覆盖待汇报状态")
          #t)
      (if (member? report-id (list "查明" "巷子失败"))
          #t
          (error "三封信：试图登记未定义的汇报结果"))
      (set! report-pending report-id)
      (result-supplement! (string-append "可汇报：" (pending-report-action-name))))

    (define (worsen-condition! n)
      (set! condition-level (min 2 (+ condition-level n)))
      (sync-globals!))

    ;; ── 阻塞同步 ────────────────────────────────────
    ;; 必看的拍子当晚不看完不能睡。读档后由 world-load! 统一重新注册。
    ;;
    ;; 名单和下面的 cond 是一对：**每一个在 cond 里竖得起来的 id，都必须在名单里**。
    ;; 手抄一串 rest-release! 会漏——「三封信/灯亮起来」就漏过一次，
    ;; 于是首演之后那条阻塞永远撤不掉，玩家从第一章结案起就再也睡不了觉。
    ;; 所以这里改成一张表，撤销走 map，加一拍只在名单和 cond 里各加一行。
    (define 阻塞名单
      (list "三封信/开场敲门"
            "三封信/勒索信"
            "三封信/伤后探望"
            "三封信/第二封信"
            "三封信/她剪底片"
            "三封信/经理汇报"
            "三封信/第三封信"
            "三封信/她不取消"
            "三封信/首演"
            "三封信/灯亮起来"
            "三封信/是他"
            "三封信/结案"))

    (define (sync-blockers!)
      ;; 先撤掉本故事上一拍留下的阻塞，再按当前状态注册唯一有效的一项。
      ;; 否则投信完成后旧的「去码头盯邮箱」会残留；下一拍做完后便既不能休息，
      ;; 也没有强制剧情动作，形成软锁。
      (map rest-release! 阻塞名单)
      (cond
        ((= story-stage 0)
         (rest-block! "三封信/开场敲门" "有人敲门" "家" "有人敲门"))
        (delivery-pending?
         (rest-block! "三封信/勒索信" "去码头投信" "码头" "去码头盯着邮箱"))
        ((and (= story-stage 2) (not (has-flag? '伤后探望)))
         (rest-block! "三封信/伤后探望" "她在门外等你" "家" "她来看你"))
        ((second-letter-pending?)
         (rest-block! "三封信/第二封信" "剧院经理要见你" "剧院" "见剧院的经理"))
        ((and lesson-done? (not (equal? lesson-route "失败"))
              (not (has-flag? '她剪底片)))
         (rest-block! "三封信/她剪底片" "底片还在你身上" "剧院" "把底片交给她"))
        ((equal? report-pending "巷子失败")
         (rest-block! "三封信/经理汇报" "空手回去见经理" "剧院"
                      (pending-report-action-name)))
        ((report-critical?)
         (rest-block! "三封信/经理汇报" "经理的耐心已经见底" "剧院"
                      (pending-report-action-name)))
        ((third-letter-due?)
         (rest-block! "三封信/第三封信" "去剧院看信" "剧院" "去剧院看那封信"))
        ((and (beat3-open?) (not (has-flag? '她不取消)))
         (rest-block! "三封信/她不取消" "她在剧院等你" "剧院" "她要当面跟你讲"))
        (premiere-pending?
         (rest-block! "三封信/首演" "今晚首演" "剧院" "去剧院"))
        ((and (= story-stage 5) (not (has-flag? '灯亮起来)))
         (rest-block! "三封信/灯亮起来" "灯还没全亮" "剧院" "灯重新亮起来"))
        ((and (= story-stage 5) (not (has-flag? '是他)))
         (rest-block! "三封信/是他" "记者还围着她" "剧院" "什么也不说"))
        ((and (= story-stage 5) (not (has-flag? '结案)))
         (rest-block! "三封信/结案" "剧院外有人等你" "剧院" "散场之后"))
        (else #t)))

    ;; ── 开场：她找上门 ──────────────────────────────
    ;; 她不是经理介绍来的——老街的人脉听说那栋出租楼里住了个新来的侦探。
    ;; 一个穷歌女请得起的,刚好是一个穷侦探。
    ;;
    ;; 这一场要立三件事,一件也不能省:
    ;;   1. 她的害怕是真的。这是全章唯一一次她不表演,玩家的同情从这里来。
    ;;   2. 她要的不只是查案,是**保密**：别告诉经理,别报警,你替我送过去。
    ;;      理由完全合理(剧院知道了就会取消首演),所以玩家不会起疑——
    ;;      他只是第一次替她瞒住了一件事。
    ;;   3. 预付款不是现金,是她从身上摘下来的东西。她穿得起这身衣服,
    ;;      却拿不出一百金:她现在拥有的是别人愿意让她穿在身上的财富。
    ;;      那件东西是谁送的,这一节不说破(见主线卡上的「那枚胸针」)。
    (define (node-answer-door)
      (anchored-instant-action "有人敲门" "门口"
        (lambda ()
          ;; 楼下电铃求助先演，开门后再进入现有室内接案对白。
          (rainy-door-theatre!)
          (commission-dialogue!)
          (add-item! "金钱" prepayment)
          (set! delivery-day (+ world-day letter-deadline))
          (advance-stage! 1)
          (rest-release! "三封信/开场敲门")
          (log! "勒索信"
            (string-append
              "夜莺找上门：三天后往码头邮箱放一百金，她要知道写信的是谁。"
              "预付款是她从耳后摘下来的一枚胸针——当铺给了二十金，"
              "说做工是城里最好那家铺子出的，背面刻着一行很小的字，不是她的名字。"))
          (spotlight! "勒索信"
            "夜莺把手上的东西当了，也只凑出了二十块，你答应她，三天之内想办法凑够一百块，按勒索信说的，放到码头的邮箱里。"))))

    ;; ── 小节一·勒索信：一条不给钱的调查线 ───────────
    ;; 钱来自整座城市的现有工作。这里只加一条**踩点**：它一分钱不挣,投进去的骰子
    ;; 换的是投信那晚的形势。玩家每天要答的就是这一句——这颗骰子拿去挣钱,还是拿去踩点。
    ;; 一根 0/6 的钟,只在满格时兑现:你熟悉了邮箱周围的人和退路,
    ;; 第二幕追逐时多一条只有你知道的货栈边门。真邮差是勒索信交锋的基础免费排除项,
    ;; 不再由三格踩点解锁。
    ;; 六格是按小节一只有三天定的:0/8 在这个窗口里长得没人填得满。
    ;; 不做成两根 0/3:这是全章第一个进度结构,两根钟在教学位置上读起来太重。
    ;; 也不做成 0/2:两颗骰子就满,加上夜莺白送的那一格等于不用付钱——那不是投资,是签到。
    ;; 要改长短,改下面这一行的 6 和 play-mail-routine-complete-banter! 的段落。
    (define scout-max 6)

    (define mail-clock
      (make-clock "踩点" scout-max 'gauge
        (lambda (current max)
          (if (>= current max)
              "这一片你熟了。投信那晚追进货栈区时，你知道一扇锁舌损坏的边门。"
              "每次踩点填一格。满格后摸熟邮箱周围的人和退路。"))))

    (define (mail-routine-full?) (mail-clock 'full?))

    ;; 独自打听只有这一根钟。玩家起初只知道自己在找人；2、4 格时才逐渐看出
    ;; 这里的人在回避他，填满后才得出结论：必须让一个这里认得的人带路。
    (define warren-alone-clk
      (make-clock "打听进度" warren-alone-max 'gauge
        (lambda (current max)
          (if (>= current max)
              "能问的路都走过了。你需要一个这里认得的人带路。"
              "在门廊、楼梯和巷口打听那个人的下落。"))))

    ;; 老街的四根钟各有明确 owner：东西两条通路各两个空间前沿。
    ;; 前沿满格后由更深处的新前沿或永久结果取代；完成过的路线本身不留空节点。
    ;; 描述写的是「带着她走这一段会遇上什么」，不是这栋楼的建筑。
    (define east-entry-clk
      (make-clock "东侧门廊" warren-leg-max 'gauge
        "她走在你前面半步，不看两边。填满后你们会走到门廊尽头的杂货铺。"))

    (define east-gallery-clk
      (make-clock "东侧回廊" warren-leg-max 'gauge
        "认得她的人越来越多。填满后走到工会休息室，见一见弗兰克。"))

    (define west-stairs-clk
      (make-clock "西侧楼梯" warren-leg-max 'gauge
        "西边是她住过的那一侧。填满后会抵达有人守着的平台。"))

    (define west-bridge-clk
      (make-clock "上层桥廊" warren-leg-max 'gauge
        "这段路她不用你带。填满后走到桥廊公寓，这里的人记得每一张旧面孔。"))

    ;; 名字已经到手、夜莺已经走了，你还想见到弗兰克本人的那一趟。
    ;; 它不复用东侧那两根探索钟：那两根的内容是「带着她走这栋楼」，她已经不在了。
    (define frank-search-clk
      (make-clock "自己找上门" frank-search-max 'gauge
        "夜莺走了，这一趟没有人替你开口，只能一层一层敲过去。填满你就站在他面前。"))


    (define (delivery-fund-clock)
      (list
        (list 'clock "勒索款筹集" (min delivery-price (item-count "金钱"))
              delivery-price 'readout
              (string-append
                "投信时要从库存里拿出一百金。钱仍可挪作房租和生活开销。"
                "夜莺最多只能当掉 " (number->string nightingale-cover-max)
                " 金替你补：差在这个数以内，她当首饰，处境差一档；"
                "差得更多，信封就是薄的，写信的人清点之后会先动手。"))))

    ;; 普通进展只让 Clock 变化,不占用玩家注意力。只有首次跨到满格时,
    ;; 才用一段连续 banter 完整兑现这几晚的观察。
    (define (play-mail-routine-complete-banter!)
      (play-banter!
        (line "世界" "在邮箱斜对面的小摊前，那份旧报纸被翻得起了毛边。" "三封信/码头/踩点/01/世界")
        (line "世界" "真正的邮差只在下午露面。到了天黑，街对面的住户早已摸得一清二楚。" "三封信/码头/踩点/02/世界")
        (line "世界" "入夜后，邮箱前只剩一盏接触不良的路灯。只要有生面孔靠近，谁也藏不住。" "三封信/码头/踩点/03/世界")
        (line "世界" "邮箱后的暗巷通向货栈。边门的锁舌早就坏了，真要追起来，能抢在所有人前头。" "三封信/码头/踩点/04/世界")))

    (define (advance-mail-routine! delta)
      (let ((old (mail-clock 'current)))
        (mail-clock 'advance! delta)
        (let ((new (mail-clock 'current)))
          (if (and (< old scout-max) (>= new scout-max))
              (play-mail-routine-complete-banter!)
              #f))))

    ;; 踩点不是工作:它不发工钱,也不给势力关系。这是它和普通工作唯一也是最重要的区别。
    ;; 坏结果不推进并扣 1 冷静;中性推 1 格并扣 1 冷静;好结果推 2 格且不扣冷静。
    ;; 在雨里站一晚上是要还的。冷静只有 2 点,于是一天之内也踩不了几次点,
    ;; 身体自己就是这条线的节流阀,不必再写一条每日上限。
    ;; 动作的身份和目的始终不变：标题、副标题保持稳定，进度由时钟表达，满格才播 banter。

    (define (node-scout)
      (node "邮箱附近踩点"
        :anchor "勒索信-报摊"
        :subtitle "摸清取信的人和附近退路"
        :clocks (list (mail-clock 'render-data))
        :requires (list (req-die))
        :disabled (mail-routine-full?)
        :resolve (roll 'sharpness
          (outcome (lambda () (spend-composure! 2)))
          (outcome (lambda () (advance-mail-routine! 1) (spend-composure! 2)))
          (outcome (lambda () (advance-mail-routine! 2))))))

    ;; 这里曾经还有一格来自乔——码头上一起卸过两班货，他才肯聊邮箱的事。
    ;; 乔整个人物连同码头坍塌一起搁置了（见 docs/归档/Content），这一格跟着去掉：
    ;; 踩点自己就能填满六格，不缺这一格。

    ;; 这里曾经还有一张「问她邮箱那一片」——夜莺在那一带长大,给一格。删了:
    ;; 踩点这条线整个摆在码头,那张卡却要玩家跑去酒馆找一个跟勒索信无关的人,
    ;; 换回一格。跨地点的来回不该只值一格,玩家更可能压根找不到它。

    (define (beat1-dock-nodes)
      (if (= story-stage 1) (list (node-scout)) '()))

    ;; ── 勒索日 ──────────────────────────────────────
    ;; 到期当天不自动播放:它是必看事件,用阻塞休息逼玩家亲自去。
    (define (begin-delivery!)
      (if delivery-pending?
          (error "三封信：勒索日已经在等待处理")
          #t)
      (set! delivery-pending? #t)
      (sync-blockers!)
      (notify! "今天是勒索信约定的日期。"))

    ;; 她的兜底是有限的：一副首饰当出去就那么多钱，不是一张无限额的保险单。
    ;; 缺口在她能补的范围内 → 她当首饰，处境差一档。
    ;; 缺口超过她能补的 → 信封是薄的，写信的人清点之后开始在老街散照片，
    ;; 小节二一上来就少三天。代价全部落在城里，不进交锋。
    (define (nightingale-covers) (min delivery-shortfall nightingale-cover-max))
    (define (envelope-short) (max 0 (- delivery-shortfall nightingale-cover-max)))

    (define (prepare-delivery-money!)
      (let ((available (item-count "金钱")))
        (set! delivery-shortfall (max 0 (- delivery-price available)))
        (spend-up-to! "金钱" delivery-price)
        (if (> delivery-shortfall 0)
            (worsen-condition! 1)
            #f)
        (if (> (envelope-short) 0)
            (set! envelope-thin? #t)
            #f)
        ;; 交锋只读这一个数,两个里程碑由交锋自己按 4 / 8 判断——
        ;; 免得城市和交锋各存一份平行的"我知道多少"。
        (set-global! '踩点格数 (mail-clock 'current))))

    ;; 邮箱相见、投信与离场属于城市侧；剧场播完才切入蹲守交锋。
    (define (node-delivery-entry)
      (at-anchor "勒索信"
       (encounter-action "去码头盯着邮箱"
        (lambda ()
          (prepare-delivery-money!)
          (load-file "scripts/theatre/邮箱相见.scm")
          (邮箱相见-演出
            (cond ((= delivery-shortfall 0) '足额)
                  ((= (envelope-short) 0) '她补)
                  (else '塞报纸)))
          (start-encounter "勒索信" on-delivery-result)))))

    ;; 交锋回传：(list 人 钱)。四种组合都让故事往前走——交锋失败留疤,不阻断主线。
    ;; 摩托车是这一夜的固定过场，不回传：骑手是谁要等他自己在老街说。
    (define (on-delivery-result result)
      (if (not delivery-pending?)
          (error "三封信：没有待处理的勒索日")
          #t)
      (if (and (list? result) (= (length result) 2))
          #t
          (error "三封信：勒索信交锋应回传 (list 人 钱)"))
      (set! delivery-pending? #f)
      (let ((caught (car result))
            (money (cadr result)))
        ;; 钱不再是固定的三档：交锋里它是一路上抓回来的零头，任何 0..100 都合法。
        (if (and (>= money 0) (<= money delivery-price))
            #t
            (error "三封信：勒索信交锋返回了非法的追款金额"))
        ;; 倒下**只换文案，不换状态**：它记成跟丢，后面所有读 delivery-result 的地方
        ;; 因此一个分支也不用加。区别只在这一夜怎么讲——玩家刚从诊所醒过来，
        ;; 正等着知道昨晚发生了什么，那句话必须说对。
        (set! delivery-result
              (cond
                ((equal? caught '拿到线索) "拿到线索")
                ((member? caught (list '跟丢 '倒下)) "跟丢")
                (else (error "三封信：勒索信交锋返回了未登记的人物结果"))))
        (set! delivery-money money)
        ;; 追到他身后才拿得到他落下的东西：那半包烟是小节二唯一的具名抓手。
        ;; 跟丢则只剩一个方向，她的处境也跟着差一档。
        (if (equal? caught '拿到线索)
            (grant-story-item! cigs-item 1)
            (worsen-condition! 1))
        (set-flag! '勒索信已结算)
        (advance-stage! 2)
        (sync-globals!)
        (sync-blockers!)
        (log! "勒索信"
          (cond
            ((equal? caught '拿到线索)
             "邮箱那一夜：一辆摩托车截断了追逐。取信的人逃走，落下半包「老金牌」香烟。")
            ((equal? caught '倒下)
             "邮箱那一夜：你追上了他，也倒在了那条巷子里。钱被拿走了。")
            (#t "邮箱那一夜：钱被人取走，你跟丢了，只知道他逃向码头居民区。")))
        (spotlight! "投信之后"
          (cond
            ((equal? caught '拿到线索)
             "一个摩托驶过，那个人已经逃远了，你回过头来只捡到他落下的一包「老金牌」香烟。")
            ((equal? caught '倒下)
             "你累坏了，睁开眼的时候看到的只是诊所的天花板，他早就走远了。")
            (#t "你笨手笨脚地，一眨眼你要追的人就消失了。")))))

    ;; ── 结算后的人物戏(必看) ────────────────────────
    ;; 全章埋得最早、也最容易被错过的一格在这里：你把烟盒递过去，她认出来了。
    ;; 她只停了半秒，然后说「我不知道」。**不要揭穿**——尼尔不追问，玩家现在也读不出来。
    ;; 它要等到小节三莱恩说「第一封信以后她就知道」时才回头生效。
    (define (node-her-visit)
      (anchored-instant-action "她来看你" "门口"
        (lambda ()
          (if (found-cigarettes?)
              (play-dialogue!
                (line "夜莺" "你受伤了？" "三封信/家/伤后探望/01/夜莺")
                (line "尼尔" "不要紧。他身上掉了这个。" "三封信/家/伤后探望/有烟/02/尼尔")
                (line "世界" "那半包揉皱的香烟推到了桌上。她拿起来看了一眼，又轻轻放下，什么也没说。" "三封信/家/伤后探望/有烟/02/世界")
                (line "尼尔" "老街不进「老金牌」香烟。我要去码头居民区一趟。" "三封信/家/伤后探望/有烟/03/尼尔")
                (line "夜莺" "那里外人恐怕不好进去，他们不怎么认陌生人。" "三封信/家/伤后探望/有烟/03/夜莺")
                (line "尼尔" "那你跟我去。" "三封信/家/伤后探望/04/尼尔")
                (line "夜莺" "不，我不。" "三封信/家/伤后探望/04/夜莺")
                (line "世界" "那半包烟在桌上躺了一整夜。我知道那地方外人进不去，但那时候，我已经没有别的路可选了。" "三封信/家/伤后探望/收尾/01/世界"))
              (play-dialogue!
                (line "夜莺" "你受伤了？" "三封信/家/伤后探望/01/夜莺")
                (line "尼尔" "不要紧。如果我没记错的话，他往码头居民区去了。" "三封信/家/伤后探望/无烟/02/尼尔")
                (line "夜莺" "你要去吗？那里外人恐怕不好进去，他们不怎么认陌生人。" "三封信/家/伤后探望/无烟/02/夜莺")
                (line "尼尔" "我要去码头居民区一趟。" "三封信/家/伤后探望/无烟/03/尼尔")
                (line "尼尔" "那你跟我去。" "三封信/家/伤后探望/04/尼尔")
                (line "夜莺" "不，我不。" "三封信/家/伤后探望/04/夜莺")
                (line "世界" "我知道那地方外人进不去，但那时候，我已经没有别的路可选了。" "三封信/家/伤后探望/收尾/02/世界")))
          (set-flag! '伤后探望)
          (rest-release! "三封信/伤后探望")
          (sync-globals!)
          ;; 勒索信这一节到她来看你为止。
          (complete-task! "勒索信")
          (spotlight! "老街"
            "你决定独自前往码头居民区，人们口中的老街去瞧瞧。"))))

    ;; ── 小节二触发：第二封信(必看) ──────────────────
    ;; 信寄到了剧院,经理因此第一次介入。他出钱,也开始用他自己的方式处理——
    ;; 他的利益是首演成功、品牌不受损,和她的利益从此不完全重合。
    (define (begin-second-letter-call!)
      (set-flag! '第二封信来电)
      (play-remote-dialogue!
        (line "剧院接线员" "喂，是尼尔先生吗？剧院前台。经理留了急信，让您今天一早就过去。" "三封信/第二封信/来电/01/剧院接线员")
        (line "剧院接线员" "他说有顶要紧的事，请您务必尽快来一趟。" "三封信/第二封信/来电/02/剧院接线员"))
      (sync-blockers!))

    (define (node-second-letter)
      (instant-action "见剧院的经理"
        (lambda ()
          (play-remote-dialogue!
            (line "世界" "经理笑着迎上来，先替你拉开椅子。")
            (line "经理" "尼尔！夜莺老提起你。全城侦探的明日之星！")
            (line "经理" "她说你办事利落，连走路都像早知道答案。")
            (line "经理" "坐，坐。以后进剧院，报我的名字就是。"))
          (if (warren-done?)
              (play-remote-dialogue!
                (line "经理" "她今天又收到一封信。我正想请你来商量。")
                (line "尼尔" "我知道一个名字。莱恩，从老街桥廊出来的。" "三封信/第二封信/已查明/01/尼尔")
                (line "经理" "你看，我就知道请你来是对的。慢慢说。"))
              (play-remote-dialogue!
                (line "经理" "她今天又收到一封信。我正想请你来商量。")
                (line "尼尔" "我或许知道一点。但对我的委托人，私家侦探有保密的规矩。" "三封信/第二封信/主对白/01/尼尔")
                (line "经理" "当然。夜莺信得过你，我也信得过你。")
                (line "经理" "她是个好歌手，我盼着全城都能听见她唱。")
                (line "经理" "你尽管查。需要进后台、找人问话，就报我的名字。")))
          (set-flag! '第二封信)
          (patience-clk 'set! (patience-clk 'max))
          (set! patience-day world-day)
          (rest-release! "三封信/第二封信")
          (sync-globals!)
          (sync-blockers!)
          (log! "老街" "第二封信送到剧院。经理开始过问这件事。")
          ;; 若名字早已在桥廊叫出，这一趟就是汇报本身；不再让玩家读完经理的话，
          ;; 又点一次同地点的「报告莱恩身份」。
          (if (warren-done?)
              (begin
                (if (not (has-flag? '她说起莱恩))
                    (close-warren-with-her!)
                    #f)
                (settle-beat2!)
                (resolve-pending-report!))
              (spotlight! "第二封信"
                "经理请你查清写信的人。首演之前，他需要一个交代。")))))

    ;; ── 小节二·老街探索 ─────────────────────────────
    ;; 四个动作表示玩家当前能抵达的空间前沿。每段走完就被下一段或永久结果取代，
    ;; 因而卡片在 Unity 中也从一个 Anchor 移到另一个 Anchor；它们不是 UI 容器层级。
    (define (crossed? old new mark) (and (< old mark) (>= new mark)))
    (define (east-porch-done?) (east-entry-clk 'full?))
    (define (east-route-done?) (east-gallery-clk 'full?))
    (define (west-stairs-done?) (west-stairs-clk 'full?))
    (define (west-route-done?) (west-bridge-clk 'full?))
    (define (west-friction-pending?)
      (and (west-stairs-done?) (not west-friction-cleared?)))
    (define (warren-done?) bridge-identified?)
    ;; 第一趟：独自去，什么也问不出来。第二趟：她跟着，四个前沿才真正打开。
    (define (alone-trip-done?) (warren-alone-clk 'full?))
    (define (singer-guiding?)
      (and (> singer-guiding-day 0) (>= world-day singer-guiding-day)))
    (define (singer-waiting?)
      (and (> singer-guiding-day 0) (< world-day singer-guiding-day)))
    (define (alone-phase?) (and (beat2-open?) (not (singer-guiding?))))
    (define (warren-open?) (and (beat2-open?) (singer-guiding?)))

    ;; ── 带她走的那一路 ──────────────────────────────
    ;; 每条路线只在走满时给一段遭遇，不把每个进度格都变成一次播报。
    ;; 这栋楼要说的事只有一件：她不是从老街走出来的明星，她是一个正在把老街
    ;; 从自己身上剥掉的人。中途格子只推进时钟，走满或遇阻时再兑现关键事件。
    (define (warren-encounter! route n)
      (cond
        ((equal? route 'east-entry)
         (cond
           ((= n 1)
            (play-banter!
              (line "楼上的女人" "是你吗？我还当是认错了——" "三封信/居民区/东侧门廊/01/楼上的女人")))
           (else #f)))
        ((equal? route 'east-gallery)
         (cond
           ((= n 1)
            (play-banter!
              (line "世界" "你问了几次，都是一样的结果。直到她开口，人们才有所回应。" "三封信/居民区/东侧回廊/01/世界"))
            #f)
           (else #f)))
        ((equal? route 'west-stairs)
         (cond
           ;; 西侧总共只有四段遭遇，东侧有六段——而西侧还多一道坎、多掉一点冷静。
           ;; 这里保留一个环境拍子。
           ((= n 1)
           (play-remote-banter!
              (line "楼梯上的女人" "你看你看——那不就是她吗？她还回来了。" "三封信/居民区/西侧楼梯/01/楼梯上的女人")))
           (else #f)))
        ((equal? route 'west-bridge)
         (cond
           ((= n 1)
           (play-banter!
              (line "世界" "在那些你已经认不清方向的路口，她甚至不需要看，直接向右拐了进去。" "三封信/居民区/上层桥廊/01/世界"))
            #f)
           (else #f)))
        (else (error "三封信：老街遭遇收到未登记的路线"))))

    ;; 遭遇挂在时钟走满的那一下，不挂在中途的每一格。中途只是"还在走"，
    ;; 没有可说的事；走通了才是这一段真正发生了什么。
    (define (emit-encounters! route old new)
      (if (crossed? old new 4)
          (warren-encounter! route 1)
          #f))

    ;; 带着她走，坏结果不是走错路，是她被人认出来。
    (define (recognized!)
      (spend-composure! 1))

    (define (advance-east-entry! n)
      (let ((old (east-entry-clk 'current)))
        (east-entry-clk 'advance! n)
        (let ((new (east-entry-clk 'current)))
          (emit-encounters! 'east-entry old new)
          (if (crossed? old new 4)
              (spotlight! "门廊走通了"
                "杂货铺的灯亮着，柜台后面那条回廊一直通到里头。")
              #f))))

    (define (advance-east-gallery! n emit-encounter?)
      (let ((old (east-gallery-clk 'current)))
        (east-gallery-clk 'advance! n)
        (let ((new (east-gallery-clk 'current)))
          (if emit-encounter?
              (emit-encounters! 'east-gallery old new)
              #f)
          ;; 走通就是走到了：回廊尽头是工会房间，弗兰克本人就在里面。
          ;; 这里不再留一张"进去问"的卡——那张卡问的东西，你走到门口时已经问完了。
          (if (crossed? old new 4)
              (finish-union-inquiry!)
              #f))))

    (define (advance-west-stairs! n)
      (let ((old (west-stairs-clk 'current)))
        (west-stairs-clk 'advance! n)
        (let ((new (west-stairs-clk 'current)))
          (emit-encounters! 'west-stairs old new)
          (if (crossed? old new 4)
              (begin
                (spotlight! "楼梯口被堵住了"
                  "几个人把椅子横在楼梯上。他们不是在拦一个外来的侦探——他们看的是站在你旁边的她。"))
              #f))))

    (define (advance-west-bridge! n)
      (let ((old (west-bridge-clk 'current)))
        (west-bridge-clk 'advance! n)
        (let ((new (west-bridge-clk 'current)))
          (emit-encounters! 'west-bridge old new)
          ;; 同东侧：走到桥廊公寓门口，住户就把名字说出来了。
          (if (crossed? old new 4)
              (finish-bridge-inquiry!)
              #f))))

    ;; 经理的耐心：一根有脸的倒计时。它不是均匀流逝的——被具体的事扣掉,
    ;; 也能靠「让他觉得这件事是他推动的」补回来(他要的就是这个)。
    (define patience-clk
      (make-clock "经理的耐心" patience-max 'countdown
        "每次休息后都会减一格。归零他换人，委托到此为止。"))

    (define (node-east-entry)
      (node "带她穿过东侧门廊"
        :anchor "码头居民区-东侧门廊"
        :subtitle "她把首饰摘了，换了件旧外套"
        :tags (list "低风险")
        :clocks (list (east-entry-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome (lambda () (recognized!)))
          (outcome (lambda () (advance-east-entry! 1)))
          (outcome (lambda () (advance-east-entry! 2))))))

    (define (node-east-gallery)
      (node "带她走东侧回廊"
        :anchor "码头居民区-东侧回廊"
        :subtitle "常亮着灯，进出的都是送货的人"
        :tags (list "低风险")
        :clocks (list (east-gallery-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome (lambda () (recognized!)))
          (outcome (lambda () (advance-east-gallery! 1 #t)))
          (outcome (lambda () (advance-east-gallery! 2 #t))))))

    (define (node-west-stairs)
      (node "陪她上西侧楼梯"
        :anchor "码头居民区-西侧楼梯"
        :subtitle "住户共用的地盘，规矩比人情硬。她记得，你得听懂"
        :tags (list "低风险")
        :clocks (list (west-stairs-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (outcome (lambda () (recognized!)))
          (outcome (lambda () (advance-west-stairs! 1)))
          (outcome (lambda () (advance-west-stairs! 2))))))

    (define (node-west-bridge)
      (node "跟她走上层桥廊"
        :anchor "码头居民区-上层桥廊"
        :subtitle "楼梯口已经让开；这一段她住到十六岁，不用你带路"
        :tags (list "低风险")
        :clocks (list (west-bridge-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome (lambda () (recognized!)))
          (outcome (lambda () (advance-west-bridge! 1)))
          (outcome (lambda () (advance-west-bridge! 2))))))

    (define (resolve-west-friction! explain?)
      (set! west-friction-cleared? #t)
      (spotlight! "楼梯口让开了"
        "椅子拖回墙边。楼梯通向上层桥廊。")
      (if (has-flag? '她的过去)
          (sync-globals!)
          (begin
            (set-flag! '她的过去)
            (if explain?
                (play-banter!
                  (line "楼梯口的人" "她既然走了，就不该带着外人回来问路。" "三封信/居民区/西侧楼梯/阻碍/01/楼梯口的人")
                  (line "世界" "椅子拖回墙边。等那几个人走远，她才开口。" "三封信/居民区/西侧楼梯/阻碍/01/世界"))
                #f))))

    ;; 一道坎，两种过法：用嘴慢而体面，用手快而脏。
    ;; 这是这一节唯一的力量出口——也是夜莺完全帮不上忙的一手（她力量是 0）。
    (define (node-west-friction-talk)
      (node "跟横椅子的人说"
        :anchor "码头居民区-西侧楼梯"
        :subtitle "他们拦的是她，不是你。说通了，这楼里会记你一笔"
        :tags (list "高风险")
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome (lambda () (spend-composure! 2)))
          (outcome (lambda () (spend-composure! 2) (resolve-west-friction! #t)))
          (outcome (lambda () (resolve-west-friction! #t))))))

    (define (node-west-friction-shove)
      (node "硬挤过去"
        :anchor "码头居民区-西侧楼梯"
        :subtitle "快，但这栋楼会记住你是怎么上去的"
        :tags (list "高风险")
        :requires (list (req-die))
        :resolve (roll 'violence
          (outcome (lambda () (spend-composure! 3)))
          (outcome (lambda () (spend-composure! 2) (resolve-west-friction! #f)))
          (outcome (lambda () (resolve-west-friction! #f))))))


    ;; ── 小节二·第一趟：独自去 ───────────────────────
    ;; 入场只让尼尔看见这里的空间，不提前替玩家宣布「没人肯说」。
    ;; 任务在来之前已经进卷宗；对白过后玩家亲自从门廊开始打听。
    (define (first-resident-dialogue!)
      (play-dialogue!
        (line "尼尔" "从码头看，这里不过是几栋挤在一起的旧楼。" "三封信/居民区/初到/01/尼尔")
        (line "尼尔" "走近才看见，楼梯接着桥廊，每扇门后面还有别人的门。" "三封信/居民区/初到/02/尼尔")
        (line "尼尔" "先找个肯开口的人。烟的事，就从门廊问起。" "三封信/居民区/初到/03/尼尔")))

    (define (arrivals-at location)
      (if (and (equal? location "码头居民区")
               (alone-phase?)
               (not (alone-trip-done?))
               (not (has-flag? '初到居民区)))
          (list
            (arrival "初到码头居民区"
              (lambda ()
                (set-flag! '初到居民区)
                (first-resident-dialogue!))))
          '()))

    ;; 这一趟不产出线索。它只让玩家自己撞出一句话：这栋楼不对外人开口。
    ;; 因此三档结果全部填格——它不是判定「能不能查到」，是判定「你多快明白」。
    ;; 填满即结束，动作随即消失；不留一个永远可以再点一次的空节点。
    ;; 两次反应顺带把弗兰克立起来：先是一个被推过来的名字，
    ;; 然后那个名字变成一条规矩，你也发现有人一直跟在后面。
    ;; 全程不掷骰、不给状态、追不了——玩家唯一能做的就是记住那件太大的外套。
    ;; 它在巷子那晚兑现：围上来的年轻人里，站在弗兰克左手边的就是他。
    (define (alone-stonewall-scene! milestone)
      (cond
        ((= milestone 2)
         (play-banter!
           (line "尼尔" "这里有谁抽「老金牌」香烟？" "三封信/居民区/独自打听/碰壁01/尼尔")
           (line "门廊里的男人" "我们可抽不起这种烟。" "三封信/居民区/独自打听/碰壁01/门廊里的男人")))
        ((= milestone 4)
         (play-banter!
           (line "世界" "有个女人刚想接话，旁边的人不动声色地用手肘撞了她一下。" "三封信/居民区/独自打听/碰壁02/世界/01")
           (line "世界" "他声音压得很低：弗兰克可不爱看外人在这一带瞎打听。" "三封信/居民区/独自打听/碰壁02/世界/02")))
        (else #f)))

    (define (advance-alone! n)
      (let ((old (warren-alone-clk 'current)))
        (warren-alone-clk 'advance! n)
        (let ((new (warren-alone-clk 'current)))
          (if (crossed? old new 2) (alone-stonewall-scene! 2) #f)
          (if (crossed? old new 4) (alone-stonewall-scene! 4) #f)
          (if (crossed? old new warren-alone-max)
              (begin
                (set! singer-guiding-day (+ world-day singer-wait-days))
                (sync-globals!)
                (play-remote-dialogue!
                  (line "尼尔" "我转了一整圈，没人肯理我。得有个他们认得的面孔带路。" "三封信/居民区/独自打听/收场/01/尼尔")
                  (line "夜莺" "我不愿意回去……我就是从那里出来的。" "三封信/居民区/独自打听/收场/01/夜莺")
                  (line "尼尔" "那也只能是你带我进去。" "三封信/居民区/独自打听/收场/02/尼尔")
                  (line "夜莺" "等我两天，就两天。" "三封信/居民区/独自打听/收场/02/夜莺"))
                (spotlight! "两天以后"
                  "她会在巷口等你。"))
              (sync-globals!)))))

    (define (node-alone-warren)
      (node "独自在老街打听"
        :anchor "码头居民区-东侧门廊"
        :subtitle "在门廊、楼梯和巷口打听那个人的下落"
        :tags (list "低风险")
        :clocks (list (warren-alone-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome (lambda () (spend-composure! 1)))
          (outcome (lambda () (advance-alone! 1)))
          (outcome (lambda () (advance-alone! 2))))))

    ;; 物证的用处：柜台后的人认得这种烟，指出常抽它的弗兰克。
    ;; 不掷骰，直接推进东侧回廊两格——半程，不是整程：他只指出一个可疑的人，
    ;; 仍要你亲自走到回廊尽头判断弗兰克是不是写信的人。
    ;;
    ;; 代价写在明处：烟盒他留下。**它就是巷子里能扔到莱恩桌上的那半包**——
    ;; 用在这儿，那一手就没了。旧版本悄悄吃掉这件物证，副标题里一个字没写；
    ;; 玩家事后才发现自己弄丢了一张牌，那不是取舍，是坑。
    (define (node-show-cigs-at-counter)
      (node "把烟盒给柜台后的人看"
        :anchor "码头居民区-东侧门廊"
        :subtitle "物证；不掷骰。东侧回廊推进两格。烟盒他留下，你不能再带走"
        :requires (list (req-item cigs-item 1))
        :resolve (instant
          (outcome (lambda ()
              (play-banter!
                (line "柜台后的人" "这牌子我们不进。里头工会休息室那个弗兰克，倒常抽。" "三封信/居民区/东侧门廊/烟盒指路/01/柜台后的人/01")
                (line "尼尔" "他在里面？" "三封信/居民区/东侧门廊/烟盒指路/01/尼尔")
                (line "柜台后的人" "回廊走到底。" "三封信/居民区/东侧门廊/烟盒指路/01/柜台后的人/02"))
              (advance-east-gallery! 2 #f))))))

    ;; 东侧的收束：走到工会休息室，弗兰克本人确实抽「老金牌」香烟，但不是写信的人。
    (define (finish-union-inquiry!)
      (set! union-checked? #t)
      (frank 'meet!)
      (spotlight! "东侧走通了"
        "回廊尽头是一间休息室，墙上挂着一些工会的日程表，但现在没什么人。")
      (play-dialogue!
        (line "尼尔" "你抽「老金牌」香烟。")
        (line "弗兰克" "抽得起的时候抽。怎么，烟又惹事了？")
        (line "尼尔" "有人落下了这包烟。我在找他。")
        (line "弗兰克" "你觉得是我？")
        (line "尼尔" "不，不是你。"))
      (notify! "弗兰克记住了你")
      (sync-globals!))

    ;; 西路先给出莱恩的名字时，夜莺已经离开。东侧这一头仍然要走——**但不是点一下就到了**。
    ;; 一次性执行读起来像一段过场：你按了一个钮，然后认识了这条街上最有分量的人。
    ;; 名字已经不缺了，缺的是他本人，那就让它值一段功夫：一根 0/6 的钟，自己一层层敲。
    ;;
    ;; 不复用东侧那两根探索钟：它们的内容是「带着她走这栋楼」，她已经走了。
    ;; 也不再往里塞新的遭遇——这一趟没有人陪你说话，一句话都不该有。
    (define (advance-frank-search! n)
      (frank-search-clk 'advance! n)
      (if (frank-search-clk 'full?) (meet-frank!) #f))

    (define (meet-frank!)
      (set! union-checked? #t)
      (frank 'meet!)
      (play-dialogue!
        (line "世界" "东侧回廊尽头的门开着。里面在开会，没人请你坐。")
        (line "弗兰克" "桥廊那边把名字给你了。")
        (line "尼尔" "莱恩。")
        (line "弗兰克" "我知道他。那是两回事。")
        (line "尼尔" "谁管？")
        (line "弗兰克" "这条街自己管。"))
      (notify! "弗兰克记住了你")
      (sync-globals!))

    (define (node-search-for-frank)
      (node "自己走东侧那条回廊"
        :anchor "码头居民区-东侧门廊"
        :subtitle "她已经走了，这一趟只有你自己"
        :tags (list "低风险")
        :clocks (list (frank-search-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome (lambda () (spend-composure! 1)))
          (outcome (lambda () (advance-frank-search! 1)))
          (outcome (lambda () (advance-frank-search! 2))))))

    (define (finish-bridge-identity!)
      (set! bridge-identified? #t)
      ;; 东侧门廊已经走通过的话，那四格不白走：自己找上门的那趟从两格起步。
      (if (east-porch-done?) (frank-search-clk 'advance! 2) #f)
      (log! "老街" "老街桥廊的住户叫出了买「老金牌」香烟的人的名字：莱恩。")
      ;; 名字一叫出来，她就在场。这段承认不依赖第二封信是否抵达；
      ;; 那封信只决定经理何时把这个名字变成下一段委托。
      (sync-globals!)
      #f)

    ;; 西侧的收束：这一层的住户记得每一张旧面孔，其中一张就是她带来的。
    (define (finish-bridge-inquiry!)
      (spotlight! "西侧走通了"
        "桥廊尽头那间门板剥落的公寓，铁丝网上挂着生锈的锁。")
      (play-dialogue!
        (line "尼尔" "这一带有没有人抽「老金牌」香烟，穿旧西装，走路有点跛。" "三封信/居民区/桥廊公寓/01/尼尔")
        (line "桥廊住户" "你说的是莱恩吧。夜莺，你们不是认识吗？" "三封信/居民区/桥廊公寓/01/桥廊住户")
        (line "世界" "尼尔转过身去。那一瞬间，空气像是冻住了。" "三封信/居民区/桥廊公寓/01/世界"))
      (finish-bridge-identity!))

    ;; 查完以后留一张标注：名字是从这里问出来的，地点本身没有别的玩法了。
    (define (bridge-apartments-place)
      (node "桥廊公寓"
        :anchor "桥廊公寓"
        :children
          (list (note-node "标注：桥廊查到的名字" "莱恩"
                  "穿旧西装，从这里下去买「老金牌」香烟。他早已搬走，但这里的人还记得他。"))))


    ;; ── 小节二·人物戏 ──────────────────────────────
    ;; 酒馆里的人是否继续说下去，读的是玩家在老码头那个圈子里的实际位置。
    ;; 写短——这是一眼扫过去的东西,不是要人停下来读的段落。
    ;;
    ;; 它是标注不是卡：站在这儿就听得见，不必点、不花骰子。原来是一张 observe 卡，
    ;; 而酒馆那边另有一条常驻标注说着同一件事（"夜里这儿只剩两种人"）。
    ;; 两条合成一条，留下会变的这条：只在小节二出现，而且读你在老码头的位置。
    (define (node-lyon-talk)
      (note-node "标注：酒馆里的闲话" ""
        (if (frank 'met?)
            "「那些东西该烧掉。」有人把杯子推开，走了。"
            "几个人正说着什么，看见你就散了。")))

    ;; ── 小节二·收场：查明身份 ───────────────────────
    ;; 桥廊调查完成时已经叫出了名字。夜莺的身世不在同一拍倾倒，
    ;; 等玩家离开老街、结束这一天后再用一段短对白收束。
    ;; 这样无论先查到名字还是先收到第二封信，身份揭晓都只发生一次。
    ;; 正式开场和调试试演共用同一场线绘剧场；室内委托仍由下一步对白接续。
    (define (rainy-door-theatre!)
      (load-file "scripts/theatre/雨夜来访.scm")
      (雨夜来访-演出))

    ;; 开场委托（Debug 可单独试演）
    ;; 舞台：他靠墙想着穷日子（心里话）；她进来抱着自己、灯弱，一直保持到他伸手接下这桩事；
    ;; 她抬头，灯回来。中间几句不动。
    (define (commission-dialogue!)
      (play-dialogue!
        (line "尼尔" "敲门声响起来的时候，我正就着冷水咽下最后半块硬面包。那时候我很穷，没有名声，没有人找我来做什么，靠零工度日。我知道我需要一个机会，就在那时候……" "三封信/开场/引子/01/世界"
              :inner #t :pose "靠墙" :light 'ember)
        (line "夜莺" "这点钱连定金都不够，还要你垫着，我真不知道该怎么……" "三封信/开场/委托/01/夜莺"
              :pose "抱臂" :light 'faint :other :pose "基础" :light 'normal)
        (line "尼尔" "钱的事我来想办法。他拿不走这笔钱。" "三封信/开场/委托/01/尼尔")
        (line "尼尔" "尝到了甜头，就还会有下一次。我们得想个办法" "三封信/开场/委托/02/尼尔")
        (line "夜莺" "那我能做什么？" "三封信/开场/委托/02/夜莺")
        (line "尼尔" "照信里说的办。那天码头碰面，你把信封放进箱子里就走，我会在那儿看着。" "三封信/开场/委托/03/尼尔"
              :pose "伸手" :move 'in)
        (line "夜莺" "我都听你的。" "三封信/开场/委托/03/夜莺"
              :pose "仰头" :light 'normal)))

    ;; 她不取消（Debug 可单独试演）
    ;; 舞台：他抱臂问，她迎上去，站着说完那三句；他逼上去翻负片，她一震；她掩面求他留在后台，
    ;; 他攥拳退开、翻回黑夜；她仰头、灼，把"台下的灯"那几句说完；他一句"子弹"她灯颤；她恳求。
    (define (refusal-dialogue!)
      (play-dialogue!
        (line "尼尔" "有人写信要你的命。你还唱吗？" "三封信/拒绝取消/01/尼尔"
              :pose "抱臂")
        (line "夜莺" "当然。我等了这么多年。" "三封信/拒绝取消/01/夜莺"
              :pose "逼近" :move 'in)
        (line "夜莺" "在老街唱了六年，从来没人写信要我的命，因为没有人在乎我是死是活。" "三封信/拒绝取消/02/夜莺")
        (line "夜莺" "现在有人在乎了，这说明我已经走到了什么地方。" "三封信/拒绝取消/03/夜莺")
        (line "尼尔" "这只说明有人想让你死在台上。" "三封信/拒绝取消/02/尼尔"
              :pose "逼近" :move 'in :screen 'flash :screen 'negative)
        (line "夜莺" "今天如果取消了，我这一辈子都不会再有第二次机会。" "三封信/拒绝取消/04/夜莺"
              :shake #t)
        (line "夜莺" "那天晚上你留在后台，行吗？站在我能看见你的地方，别去台下。" "三封信/拒绝取消/05/夜莺"
              :pose "掩面" :move 'back :light 'faint)
        (line "尼尔" "留在后台，我就得照我的办法清场。" "三封信/拒绝取消/03/尼尔"
              :pose "攥拳" :move 'back :screen 'normal)
        (line "夜莺" "我在老街那些脏酒馆里熬了六年，就等今天这一晚上。" "三封信/拒绝取消/06/夜莺"
              :pose "仰头" :move 'in :light 'surge)
        (line "夜莺" "台下的灯照着我，所有人安安静静听我唱。只要有这一次，我就没白活。" "三封信/拒绝取消/07/夜莺")
        (line "尼尔" "哪怕站在光底下的人，可能会先吃到一颗子弹？" "三封信/拒绝取消/04/尼尔"
              :other :light 'normal :light 'flicker)
        (line "夜莺" "那就是莱恩，喝多了嘴碎，他没那个胆子。" "三封信/拒绝取消/08/夜莺")
        (line "夜莺" "再说了，不是还有你在后台守着吗？求你，这一场，让我自己走上去。" "三封信/拒绝取消/09/夜莺"
              :pose "恳求" :other :pose "摘帽")
        (line "世界" "她说这话时一直在扯裙子上的褶子，扯得比说话还用力。" "三封信/拒绝取消/01/世界")))

    ;; 尾声（Debug 可单独试演）
    ;; 舞台：她坐着，他靠墙。提到莱恩她的灯残烛一下；说门票时站起来抬头；他"下回吧"摘帽；
    ;; 她回眸——戏在那一眼上收。
    (define (closing-dialogue!)
      (play-dialogue!
        (line "夜莺" "你还没走。" "三封信/尾声/01/夜莺"
              :pose "坐")
        (line "尼尔" "来看看大明星。" "三封信/尾声/01/尼尔"
              :pose "靠墙")
        (line "夜莺" "我说过我会唱完的。" "三封信/尾声/02/夜莺")
        (line "尼尔" "你确实唱完了。莱恩也算彻底交代了。" "三封信/尾声/02/尼尔")
        (line "夜莺" "……他要是不发疯，也落不到今天。" "三封信/尾声/03/夜莺"
              :light 'ember)
        (line "夜莺" "首演的门票，我本来在兜里给你留了一张。可惜你只能站在侧台看。" "三封信/尾声/04/夜莺"
              :pose "仰头" :light 'normal)
        (line "尼尔" "下回吧。" "三封信/尾声/03/尼尔"
              :pose "摘帽" :move 'in)
        (line "夜莺" "……下回。" "三封信/尾声/05/夜莺"
              :pose "回眸" :light 'faint)))

    ;; 桥廊之后那段对白单独拎出来：Debug 面板可以不动任何状态就把它演一遍，
    ;; 舞台指示改了随时看效果，不用真把小节二玩到头。
    ;; 舞台：黑场开门。她躲（低头、弱）；他逼上去翻负片；她炸、灼、一震；他攥拳退开；
    ;; 她背过身讲往事、他点烟听着，翻回黑夜——她背着身把往事讲完，灯慢慢残烛；
    ;; "不想再记起"那句掩面、灯颤；他伸手走近，她灯燃回来；收尾是他的心里话。
    (define (bridge-aftermath-dialogue!)
      (play-dialogue!
        (line "世界" "走出那条桥廊，过道里的旧门在身后合上，只剩我们两个人。" "三封信/居民区/桥廊之后/01/世界"
              :light 'blackout)
        (line "尼尔" "那个莱恩到底是谁？" "三封信/居民区/桥廊之后/01/尼尔"
              :pose "抱臂")
        (line "夜莺" "我从前的男人……我想过很多可能，但真没料到会是他。" "三封信/居民区/桥廊之后/01/夜莺"
              :pose "低头" :light 'faint)
        (line "尼尔" "你从来没提过他。你还有多少事情是没料到的？" "三封信/居民区/桥廊之后/02/尼尔"
              :pose "逼近" :move 'in :screen 'flash :screen 'negative)
        (line "夜莺" "那都是五六年前的事了！难道我小时候挨过的饿、跟过谁，都得向你报备吗？" "三封信/居民区/桥廊之后/02/夜莺"
              :pose "逼近" :move 'in :light 'surge :shake #t)
        (line "尼尔" "说吧，他是个什么样的人？" "三封信/居民区/桥廊之后/03/尼尔"
              :pose "攥拳" :move 'back)
        (line "夜莺" "一个窝囊废。开个破修车铺，整天灌廉价烈酒，清醒时发誓带我进上城区，喝醉了就在走廊砸酒瓶骂街。" "三封信/居民区/桥廊之后/03/夜莺"
              :pose "背身" :move 'back :light 'normal :screen 'normal :other :pose "点烟")
        (line "夜莺" "我那时候太小，以为跟着个男人就能不受欺负。后来才明白，他除了把身上的霉运传染给我，什么用都没有。" "三封信/居民区/桥廊之后/04/夜莺"
              :light 'ember)
        (line "尼尔" "所以你扔下他跑了。" "三封信/居民区/桥廊之后/04/尼尔")
        (line "夜莺" "他恨我，是因为我站到了有聚光灯的舞台上，而他只能烂死在这条桥廊里。" "三封信/居民区/桥廊之后/05/夜莺")
        (line "尼尔" "所以勒索信是他寄的，对吗？" "三封信/居民区/桥廊之后/05/尼尔")
        (line "夜莺" "我不知道……他喝多了什么话都敢吹。尼尔，我不是有意瞒你，我只是……不想再记起那些事。" "三封信/居民区/桥廊之后/06/夜莺"
              :pose "掩面" :light 'faint :light 'flicker)
        (line "尼尔" "别慌。至少现在我们知道他是谁了。" "三封信/居民区/桥廊之后/06/尼尔"
              :pose "伸手" :move 'in :other :light 'relight :light 'normal)
        (line "尼尔" "老街的雾气在过道里弥漫开来。我知道那个名字已经成了一枚钉子，正扎在所有人的神经上。" "三封信/居民区/桥廊之后/收尾/01/世界"
              :inner #t)))

    (define (close-warren-with-her!)
      (if (has-companion? '夜莺)
          (begin
            (bridge-aftermath-dialogue!)
            (set-flag! '她说起莱恩)
            (dismiss-companion! '夜莺)
            (spotlight! "查明了"
              "莱恩。你们怀疑写信的人就是他。"))
          (begin
            (set-flag! '她说起莱恩)
            (spotlight! "查明了"
              "莱恩。你们怀疑写信的人就是他。"))))

    (define (settle-beat2!)
      (set! material-settled? #t)
      (set! settle-route "查明")
      (set! lyon-fate "逃走")
      (set! settled-day world-day)
      (mark-report-pending! "查明")
      (set-flag! '留下的信)
      (sync-globals!)
      (sync-blockers!))

    ;; 身份揭晓与夜莺的个人承认之间留出一个日终间隔；若第二封信已到，
    ;; 经理的汇报动作会先触发同一段收束，避免玩家永远看不到它。
    (define-turn-rule "离开老街后谈起莱恩"
      (lambda () (and bridge-identified? (not (has-flag? '她说起莱恩))))
      (lambda ()
        (close-warren-with-her!)
        (sync-globals!)
        (if (has-flag? '第二封信)
            (settle-beat2!)
            #f)))

    ;; 三天到了：经理的人带回地址。这条消息点亮出发卡,它自己不花骰子。
    ;; 两天到了：她自己在巷口等你。这条消息点亮东西两条通路，它不花骰子，
    ;; 也不需要玩家先跑一趟酒馆——约好的事到期就该发生。
    (define-turn-rule "她在巷口等你"
      (lambda () (and (beat2-open?) (singer-guiding?) (not singer-guiding-told?)))
      (lambda ()
        (set! singer-guiding-told? #t)
        (sync-globals!)
        (sync-blockers!)
        ;; 她真的入队：这一段她不是一个背景设定，是队伍里多出来的一个人。
        ;; 能力见引擎同伴能力总表——交际高得离谱，力量是零。这栋楼里能让人开口的是她，不是你。
        ;; 她的冷静＝她今天还能在这条街上待多久；掉到 0 她就走了，第二天睡醒才回来。
        (recruit-companion! '夜莺 "夜莺" (同伴能力 '夜莺))
        (spotlight! "两天以后"
          "她在巷口站着，穿着旧外套，把头发扎了起来，耳朵上什么也没戴。")
        (play-banter!
          (line "夜莺" "走吧，记得别叫我的名字，就假装不认识我。" "三封信/居民区/带路/01/夜莺"))))

    ;; 经理这句话里没有恶意——在他看来这只是正常处理一个勒索者。
    ;; 玩家此刻也不该开始把他当敌人；他只是不觉得这是件私事。
    ;; 真正让玩家动身的是紧接着的夜莺：她要你**抢在剧院的人前面**。
    (define-turn-rule "经理的人回话"
      (lambda () (and (lesson-beat?) (not inquiry-told?) (inquiry-done?)))
      (lambda ()
        (set! inquiry-told? #t)
        (sync-globals!)
        (play-remote-dialogue!
          (line "经理" "摸清楚了。货栈后头那片破修理棚，白天干活，晚上就睡在没窗的隔间。" "三封信/经理回话/01/经理")
          (line "经理" "下午我派两个弟兄过去，把底片和信硬搜出来。" "三封信/经理回话/02/经理")
          (line "尼尔" "你手下那些人下手没轻重。" "三封信/经理回话/01/尼尔")
          (line "经理" "我不管他们怎么弄，只要事情能办妥。谁去都一样。" "三封信/经理回话/03/经理"))
        (play-dialogue!
          (line "世界" "从经理办公室出来，夜莺在道具堆后面的阴影里堵住了我，脸色发白。" "三封信/后台拦截/01/世界")
          (line "夜莺" "你得赶在经理的人前头去。" "三封信/后台拦截/01/夜莺")
          (line "尼尔" "为什么？" "三封信/后台拦截/01/尼尔")
          (line "夜莺" "那帮人下手狠……莱恩要是被逼急了，什么疯事都干得出来。" "三封信/后台拦截/02/夜莺")
          (line "尼尔" "你是怕他把事情闹大，还是怕他挨打？" "三封信/后台拦截/02/尼尔")
          (line "世界" "她眼神躲闪了一下，用力咬着嘴唇。" "三封信/后台拦截/02/世界")
          (line "夜莺" "我只想要回那些东西。把这事彻底了结了，算我求你。" "三封信/后台拦截/03/夜莺"))))

    ;; ── 小节三：找到并教训莱恩 ───────────────────────
    ;; 城市侧只有两件事：等两天,然后挑一个晚上出发。
    ;; 没有任何小节三专属的跑腿卡——钥匙就是你在小节二认识了谁,
    ;; 这两天里在码头做工、在老街露脸,都在悄悄开锁。
    ;;
    ;; 衔接：小节二在桥廊上就已经收束完（她当场认下莱恩，然后离队）。
    ;; 所以这一节开场干干净净：你手上只有一个名字，去剧院交差。
    ;;
    ;; 老街的四把普通钥匙在进场前一次性镜像给交锋。
    ;; 有钥匙那一手不掷骰;没钥匙照样过得去,只是在弗兰克门口要多花两颗骰。
    (define (lesson-beat?) (and (= story-stage 3) (not lesson-done?)))
    (define (days-waited) (- world-day inquiry-day))
    (define (inquiry-done?) (>= (days-waited) inquiry-days))
    (define (alley-open?) (and (lesson-beat?) (inquiry-done?)))

    (define (sync-keys!)
      ;; 码头那边认不认得你——没有「老码头声誉」这种东西，码头认的是艾迪认不认你。
      (set-global! '钥匙-码头 (eddie 'known?))
      ;; 你在工会房间见过他本人，还是从门口第一次打照面——这两种不一样。
      (set-global! '钥匙-弗兰克 union-checked?)
      (set-global! '钥匙-桥廊熟脸 bridge-identified?)
      ;; 酒馆老板站不站你这边：你见过弗兰克本人，他才把你当回事。
      (set-global! '钥匙-酒馆老板 (frank 'met?)))

    ;; ── 巷子 ────────────────────────────────────────
    (define (node-alley-entry)
      (at-anchor "码头-巷口"
       (encounter-action "今晚去货栈后面"
        (lambda ()
          (sync-keys!)
          (play-dialogue!
            (line "世界" "天还没黑透，我顺着煤渣路走到堆场尽头。一辆黑色旧摩托停在入口，排气管还在冒着热气。" "三封信/巷子/入场/01/世界")
            (line "世界" "弗兰克就靠在车边抽烟。在这片地界上，每一步都得看他的规矩。" "三封信/巷子/入场/02/世界"))
          (if alley-thrown-out?
              (play-dialogue!
                (line "世界" "昨天送你出去的那几个人还在路口。他们看见你，没有动。" "三封信/巷子/入场/被赶/01/世界")
                (line "世界" "他们只是转过身，往堆场那头喊了一句。" "三封信/巷子/入场/被赶/02/世界"))
              #f)
          (start-encounter "巷子" on-lesson-result)))))

    ;; 交锋只有一种成功：莱恩交出铁盒，答应不再找夜莺。
    ;;   这一节一定要真正结案一次：底片到手、他停止勒索、经理满意、世界恢复正常。
    ;;   第三封信之所以能把人打懵，全靠这一次结得干净。
    ;;   '被赶出去 / '倒下 是最终失败：东西没有拿到，经理会解除雇佣；
    ;;   主线仍进入第三封信前的平静期，不允许回头重试。
    (define (on-lesson-result result)
      (if (symbol? result)
          #t
          (error "三封信：旧账交锋应回传收场 symbol"))
      (if (member? result (list '被赶出去 '倒下))
          (fail-lesson! (equal? result '倒下))
          (settle-lesson! result)))

    (define (fail-lesson! collapsed?)
      (set! alley-thrown-out? #t)
      (set! lesson-done? #t)
      (set! lesson-day world-day)
      (set! lesson-route "失败")
      ;; 失败线不获得弗兰克认可；人物模块现有回执用 rough?=#t 表达不认可。
      (frank 'on-alley-result! #t)
      (mark-report-pending! "巷子失败")
      (sync-globals!)
      (sync-blockers!)
      (log! "莱恩" "货栈后面那一夜：你没能让莱恩交出铁盒，经理的工作空着回去了。")
      (if collapsed?
          (spotlight! "空手离开巷子"
            "你先被人抬了出来。底片、照片和信仍在莱恩手里。回剧院向经理交代。")
          (spotlight! "空手离开巷子"
            "弗兰克结束了这场谈话。底片、照片和信仍在莱恩手里。回剧院向经理交代。")))

    (define (settle-lesson! result)
      (if (equal? result '谈)
          #t
          (error "三封信：巷子成功只能回传谈"))
      (set! lesson-done? #t)
      (set! lesson-day world-day)
      (set! lesson-route "谈")
      (frank 'on-alley-result! #f)
      ;; 莱恩屈服后，底片和照片就不再是他的筹码。
      (grant-story-item! "莱恩的底片与照片" 1)
      (sync-globals!)
      (sync-blockers!)
      (log! "莱恩" "货栈后面那一夜：莱恩交出铁盒，说不再找夜莺。")
      (spotlight! "旧账结了"
        (string-append
          "他自己把铁盒扔了过来，让你带话给她：我不找她了。"
          "底片和照片都在你口袋里。"
          "新行动：去剧院把底片交给夜莺。")))

    ;; ── 小节三的真正高潮：不是莱恩，是她 ─────────────
    ;; 这一场没有检定，也**暂时没有选择**：它就是一场演完的对白。
    ;;
    ;; 这里曾经摆出四张回答卡（追究／摊牌／照帮／沉默）。它们不改任何数值，
    ;; 唯一的下游是首演之夜她回头看你那一眼的一句话——一句话撑不起四张
    ;; 摆在世界树上、和"回家""买烟"并排的卡。而且那一拍的力量全部来自
    ;; "她抬头看着你"，世界树恰恰是把所有人都撤走的那个画面（见 docs/TODO.md
    ;; 「对白里的选择」）。等对白自己能收尾于一个选择，这四条再回来。
    ;;
    ;; 现在她问完，尼尔没有回答——不替玩家表态，也不假装那是个选择。
    ;; her-answer 仍然记成"沉默"，首演那一眼因此仍读得到对应的那句话。
    (define (node-give-negatives)
      (node "把底片交给她"
        :subtitle "需要：莱恩的底片与照片；交付后她会结清报酬"
        :requires (list (req-item "莱恩的底片与照片" 1))
        :resolve (instant
          (outcome (lambda ()
          (play-dialogue!
            (line "世界" "化妆镜前的黄灯泡滋滋作响。夜莺把铁盒放在膝上，一张一张往灯下举。" "三封信/底片/交割/01/世界")
            (line "世界" "逼仄的小酒吧，廉价的红裙子，还有那个搂着她大笑的修理工。她停顿了一下，拿起了剪刀。咔嚓，咔嚓。" "三封信/底片/交割/02/世界")
            (line "尼尔" "第一封信寄到的时候，你就知道是他。" "三封信/底片/交割/01/尼尔")
            (line "夜莺" "……我猜到了。" "三封信/底片/交割/01/夜莺")
            (line "尼尔" "那你还跟我装傻？" "三封信/底片/交割/02/尼尔")
            (line "夜莺" "我要是一开始就告诉你，这是我那个窝囊废前男友敲诈我……哪个大侦探肯替穷鬼跑腿？" "三封信/底片/交割/02/夜莺")
            (line "夜莺" "这是剩下的钱，我自己攒的，没走经理的账。" "三封信/底片/交割/03/夜莺")
            (line "世界" "我把烟点上，没接话。最后几张碎照片在炉子里烧成了灰烬。那时候所有人都以为，这桩陈年烂账已经彻底画上了句号。" "三封信/底片/交割/03/世界"))
          (remove-item! "莱恩的底片与照片" 1)
          (settle-alley-pay!)
          (set-flag! '她问了你)
          (apply-her-answer! "沉默"))))))

    (define (apply-her-answer! answer)
      (set! her-answer answer)
      (set-flag! '她剪底片)
      (rest-release! "三封信/她剪底片")
      (sync-globals!)
      (sync-blockers!)
      (complete-task! "莱恩")
      (spotlight! "旧账之后"
        "底片在剧院的炉子里烧着。报酬已经结清，这件事暂时结束了。"))

    ;; ── 经理的耐心 ──────────────────────────────────
    (define (patience-clock)
      (if (and (patience-running?) (> manager-warning-level 0))
          (list (patience-clk 'render-data)) '()))

    (define (patience-note)
      (cond
        ;; 归零后 fail-game! 仍会重建一次世界树，用来承载失败演出。
        ;; 这里不能返回空串，否则经理下面会生成一张完全空的 note，反而截断 Game Over。
        ((patience-clk 'empty?) "他已经找了别人。你不再负责这件委托。")
        ((= manager-warning-level 0) "他见到你，总是先笑着招呼。")
        ((<= (patience-clk 'current) 2) "他仍说信任你，但已经在安排后手。")
        ((<= (patience-clk 'current) 4) "他开始频繁问起调查进展。")
        (else "他又像初见时那样热络。")))

    ;; 查清身份时经理付调查费；巷子成功后，夜莺在接过底片时亲自结清收尾费。
    ;; 失败线没有收尾费，经理直接解除雇佣。
    (define (settle-inquiry-pay!)
      (add-item! "金钱" manager-fee)
      (result-supplement! (string-append "调查费：" (number->string manager-fee) " 金")))

    (define (settle-alley-pay!)
      (add-item! "金钱" interim-fee)
      (set-flag! '前段报酬)
      (result-supplement! (string-append "收尾费：" (number->string interim-fee) " 金")))

    ;; 已知姓名后的汇报，既可能来自桥廊调查完成后专程回剧院，
    ;; 也可能发生在第二封信当天的第一次见面。两种路径共用同一笔调查费和下一段的等待。
    (define (resolve-identity-report!)
      (patience-clk 'advance! patience-report)
      (if (not manager-phone-given?)
          (begin
            (set! manager-phone-given? #t)
            (home 'connect-phone!)
            (play-remote-dialogue!
              (line "经理" "我叫人给你屋里接了电话。往后找你方便。")
              (line "经理" "别谢我，朋友之间，这算什么。")))
          #f)
      (play-remote-dialogue!
        (line "尼尔" "写信的人叫莱恩。老街桥廊出来的，夜莺以前认识他。")
        (line "经理" "我早说过，你会是全城最好的侦探！")
        (line "经理" "名字和住处不是一回事。我让人去找他的门牌。")
        (line "尼尔" "那这一段的钱呢。")
        (line "经理" "查名字的钱现在结。让他不再写信，是下一段。"))
      (settle-inquiry-pay!)
      (set! inquiry-day world-day)
      (advance-stage! 3)
      ;; 名字交到经理手上，老街这一节结了。
      (complete-task! "老街"))

    (define (resolve-pending-report!)
      (if (report-pending?)
          #t
          (error "三封信：没有可向经理汇报的结果"))
      (cond
        ;; 查清身份就是一段完整工作，当场结调查费；地址与让他收手另算。
        ((equal? report-pending "查明")
         (resolve-identity-report!))
        ((equal? report-pending "巷子失败")
         (play-remote-dialogue!
           (line "尼尔" "东西没拿到。莱恩也没有答应收手。")
           (line "经理" "我很为难，尼尔。剧院得请别人接手了。")
           (line "尼尔" "你是在解雇我。")
           (line "经理" "这是工作上的决定。我们都是为了夜莺好。")
           (line "夜莺" "首演那晚你还会来吗？")
           (line "尼尔" "你的经理刚让我别再进这扇门。")
           (line "夜莺" "我问的是，你会不会来看我。")
           (line "尼尔" "我会。"))
         (set-flag! '被经理解雇)
         (set-flag! '受邀看首演)
         (log! "莱恩" "经理因巷子里的失败解雇了你。夜莺仍邀请你来看她的首演。")
         ;; 失败也是这一节的头：铁盒没拿到，但莱恩这一节到这儿结了。
         (complete-task! "莱恩"))
        (else (error "三封信：未登记的汇报结果")))
      (set! report-pending "")
      (sync-globals!)
      (sync-blockers!))

    (define (node-report)
      (if (not (report-pending?))
          (error "三封信：没有可向经理汇报的结果")
          (node (pending-report-action-name)
            :subtitle (pending-report-subtitle)
            :resolve (instant
              (outcome (lambda () (resolve-pending-report!)))))))

    ;; 耐心跑到你把巷子那一段交代给他为止——他说"那就到这里，接下来是我的事"，
    ;; 那句话就是这根钟停下的地方。往后事情看起来已经结束，你手上再没有东西
    ;; 可以拿去补它；这之后的时间压力换成首演的倒计时，不是他的脸色。
    ;; （办完了却不去告诉他，耐心照掉——那是你自己的事。）
    (define (patience-running?)
      (and (has-flag? '第二封信)
           (not (has-flag? '结案))
           (or (not lesson-done?)
               (and (not (equal? lesson-route "失败"))
                    (not (has-flag? '她剪底片))))))

    (define-turn-rule "经理的耐心"
      (lambda ()
        (and (patience-running?)
             (>= (- world-day patience-day) patience-interval)))
      (lambda ()
        (set! patience-day world-day)
        (patience-clk 'advance! -1)
        (sync-globals!)
        (if (patience-clk 'empty?)
            (begin
              (play-dialogue!
                (line "经理" "我很为难，尼尔。但这件事我必须交给别人。")
                (line "尼尔" "我还在查。")
                (line "经理" "我知道。我们都想帮夜莺，这绝不是针对你。")
                (line "经理" "只是剧院等不起了。我也得对他们有个交代。"))
              (fail-game! "你被换掉了"
                "剧院经理另请了一位侦探。委托到此为止——你再没有理由走进那扇门，也再没有人会告诉你后来发生了什么。"))
            (begin
              ;; 从 2 格掉到 1 格以后立刻锁住下一次休息；玩家手里若有结果，
              ;; 必须先去剧院汇报，不能让下一次日终抢在可用的补救行动前触发失败。
              (sync-blockers!)
              (cond
                ((and (= manager-warning-level 0) (<= (patience-clk 'current) 4))
                 (set! manager-warning-level 1)
                 (play-dialogue!
                   (line "经理" "尼尔，我们都是为了夜莺好。得一起把事办妥。")
                   (line "经理" "我知道你在忙，只是首演不会等我们。")))
                ((and (= manager-warning-level 1) (<= (patience-clk 'current) 2))
                 (set! manager-warning-level 2)
                 (play-dialogue!
                   (line "经理" "这件事要是办不好，连我都未必保得住位置。")
                   (line "经理" "我不是针对你，也从没怀疑过你的能力。")
                   (line "经理" "时间紧了。请你务必给我一个结果。")))
                (else #f))))))

    ;; 她第一次不在酒馆。人物节点仍留在老街(见 §1.5)，这只是一次性的事件。
    (define (node-rehearsal)
      (anchored-instant-action "去看她排练" "剧院-中央台"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "你来了。他们让我从台底下升上来——像变戏法一样。")
            (line "尼尔" "习惯吗。")
            (line "夜莺" "底下黑得很，什么也看不见，只能听着乐队数拍子。")
            (line "夜莺" "数到第四拍我就得笑着上来。")
            (line "夜莺" "在酒馆唱了六年，从来没有人要求我笑着出场。")
            (line "世界" "她说完自己笑了一下，转身回到那个圆台中间去。"))
          (set-flag! '看彩排)
          (sync-globals!))))

    ;; 平静期第三天，剧院派人来找你。没有这一条，那封信只会静静躺在剧院里：
    ;; 节点每帧实时判断所以确实在，但玩家不会收到任何通知，休息阻塞也不会注册。
    ;; 第二封信有对称的「紧急来电」，这一封当初漏了。
    (define (begin-third-letter-call!)
      (set-flag! '第三封信来人)
      (play-remote-dialogue!
        (line "剧院的跑腿" "尼尔先生！经理让您马上过去，夜莺小姐的化妆间出事了！" "三封信/第三封信/急信/01/剧院的跑腿")
        (line "尼尔" "她又收到信了？" "三封信/第三封信/急信/01/尼尔")
        (line "剧院的跑腿" "经理让您亲自去看。" "三封信/第三封信/急信/02/剧院的跑腿"))
      (sync-blockers!))

    ;; ── 小节四触发：第三封信(必看) ──────────────────
    ;; 不要钱,不提过去。只写一件事。信里提到一个只有内部人员才知道的
    ;; 排练细节——玩家和经理都认为是莱恩,勒索失败后升级到报复,合理推断。
    (define (node-third-letter)
      (instant-action "去剧院看那封信"
        (lambda ()
          ;; 第三封信到场前，成功线必须已经把底片交给夜莺，失败线必须已经
          ;; 向经理交代。这里不再替玩家自动补做上一拍。
          (if (report-pending?)
              (error "第三封信到场时仍压着待汇报结果")
              #f)
          (if (or (has-flag? '前段报酬) (has-flag? '被经理解雇))
              #t
              (error "第三封信到场时前段报酬尚未结清，且没有可补交的巷子汇报"))
    ;; 第三封信把勒索升级成了死亡威胁。它仍然能被解释成莱恩恼羞成怒，
    ;; 只是后台细节和前两封信的区别，让尼尔留下了说不清的疑虑。
          (if (has-flag? '被经理解雇)
              (play-remote-dialogue!
                (line "经理" "我找的那个人什么也没拿回来。现在她化妆间里又多了一封。" "三封信/第三封信/看信/解雇线/01/经理")
                (line "尼尔" "你不是说会找别人？" "三封信/第三封信/看信/解雇线/01/尼尔")
                (line "经理" "找了，顶个屁用！我现在需要一个至少知道前两封信的人。" "三封信/第三封信/看信/解雇线/02/经理")
                (line "尼尔" "信上写了什么？" "三封信/第三封信/看信/解雇线/02/尼尔")
                (line "经理" "一个字都没提钱！就说她今晚要是敢登台，就让她死在台上！" "三封信/第三封信/看信/01/经理")
                (line "经理" "那个开修车棚的穷鬼，这是想找死！" "三封信/第三封信/看信/02/经理")
                (line "尼尔" "上面写着她上台的时间，连第几首歌换什么衣服都列出来了。" "三封信/第三封信/看信/01/尼尔")
                (line "经理" "老街那帮要饭的，肯定有人在剧院帮他盯着！" "三封信/第三封信/看信/03/经理")
                (line "尼尔" "把演出停了。他现在不要钱，是要命。" "三封信/第三封信/看信/02/尼尔")
                (line "经理" "取消？！票全卖出去了，报社和市政厅的人都坐满了！照常唱！你去侧台给我盯紧了！" "三封信/第三封信/看信/04/经理"))
              (play-remote-dialogue!
                (line "经理" "压在镜子底下的。没有信封，没有邮戳，有人直接塞进去的。" "三封信/第三封信/看信/正常线/01/经理")
                (line "尼尔" "信上写了什么？" "三封信/第三封信/看信/正常线/01/尼尔")
                (line "经理" "一个字都没提钱！就说她今晚要是敢登台，就让她死在台上！" "三封信/第三封信/看信/01/经理")
                (line "经理" "那个开修车棚的穷鬼，这是想找死！" "三封信/第三封信/看信/02/经理")
                (line "尼尔" "上面写着她上台的时间，连第几首歌换什么衣服都列出来了。" "三封信/第三封信/看信/01/尼尔")
                (line "经理" "老街那帮要饭的，肯定有人在剧院帮他盯着！" "三封信/第三封信/看信/03/经理")
                (line "尼尔" "把演出停了。他现在不要钱，是要命。" "三封信/第三封信/看信/02/尼尔")
                (line "经理" "取消？！票全卖出去了，报社和市政厅的人都坐满了！照常唱！你去侧台给我盯紧了！" "三封信/第三封信/看信/04/经理")))
          ;; 所有人都太快了。这一段不做成玩法——它只是让玩家心里存一个疑问，
          ;; 兑现留到散场之后：第三封信一出现，突然什么都有了。
          (play-remote-dialogue!
            (line "世界" "我还没走出剧院，经理已经连打了好几个电话。" "三封信/第三封信/众生相/01/世界")
            (line "世界" "下午两点前，报社记者就堵在后门了。警察甚至主动派了人过来布控。" "三封信/第三封信/众生相/02/世界")
            (line "尼尔" "上礼拜我拿着勒索信去警局，你们连笔录都懒得记。" "三封信/第三封信/众生相/01/尼尔")
            (line "世界" "现在不一样了。全城的人都在等着看一出大明星遭狂徒恐吓的戏码。" "三封信/第三封信/众生相/03/世界"))
          (set-flag! '第三封信)
          ;; 这五天从今天起算。首演之夜这一场的压力应该由玩家有几天可查决定,
          ;; 不由他前三小节花了多久决定。
          (set! premiere-day (+ world-day premiere-prep-days))
          (advance-stage! 4)
          (rest-release! "三封信/第三封信")
          (sync-blockers!)
          (log! "首演之夜"
            "第三封信不要钱，要夜莺的命。所有人都说莱恩被逼急了。")
          (spotlight! "第三封信"
            (string-append
              "信上的威胁升级了。莱恩仍是所有人眼里最合理的嫌疑人。"
              "经理拒绝取消，而这一次城里所有人都愿意出力。"
              "距首演还有 " (number->string (days-to-premiere)) " 天。")))))

    ;; 经理是剧院里的常驻人物，不用一张办公室观察卡代替他。剧情动作都收在人物下面；
    ;; 当前没事可办时，只留一条不可点击的状态 note。
    (define (manager-status-text)
      (cond
        ((patience-running?) (patience-note))
        ((and (quiet-period?) (has-flag? '被经理解雇))
         "他已经把前两封信交给别人处理。首演的座位表上没有你的名字。")
        ((quiet-period?) "前两封信在他看来已经处理完了。他正在核首演的座位和来宾名单。")
        ((beat3-open?) "电话一通接一通。他关心的是首演能否照常，以及第二天的报纸会怎么写。")
        ((>= story-stage 5) "记者和警察都在等他说法。他已经开始替这一晚安排一个能见报的结尾。")
        (else "他在办公室核座位表，等下一件该过他手的事。")))

    (define (node-manager-note)
      (node "标注-经理状态"
        :resolve (note "" (manager-status-text))))

    (define (manager-children)
      (cond
        ((second-letter-pending?) (list (node-second-letter)))
        ((third-letter-due?) (list (node-third-letter)))
        ((report-pending?) (list (node-report)))
        (else
         (append
           (list (node-manager-note))
           ;; 薇拉不是剧院根上的一件待办。首演准备期间去见经理，才由他引见。
           (if (and (beat3-open?) (not (has-flag? '薇拉)))
               (list (node-vera))
               '())))))

    (define (node-manager)
      (node "经理"
        :subtitle "剧院经理；首演能否照常是他判断一切的尺度"
        :children (manager-children)))

    ;; ── 小节三·人物戏(必看) ─────────────────────────
    (define (node-she-refuses)
      (instant-action "她要当面跟你讲"
        (lambda ()
          ;; 舞台：这是她的原则戏，全章唯一一次 surge 给"台下的灯照着我"。
          ;; 她一开口就迎上去；他逼、她低头求；他退开；她走到中间把那句说完；最后再低头求一次。
          (refusal-dialogue!)
          (set-flag! '她不取消)
          (set-flag! '她的原则)
          (rest-release! "三封信/她不取消")
          (sync-globals!))))

    ;; 赞助公司的人。零机制,两句话——第二章的种子,第一章不解释。
    (define (node-vera)
      (instant-action "经理替你引见"
        (lambda ()
          (play-remote-dialogue!
            (line "经理" "薇拉女士，这位是尼尔，替我们查那几封信。")
            (line "薇拉" "你就是那位侦探。经理跟我提过。")
            (line "薇拉" "夜莺唱得很好。我很喜欢。")
            (line "尼尔" "您听过她唱？")
            (line "薇拉" "我的助理告诉过我她唱得很好。")
            (line "薇拉" "这样的孩子应该被更多人听见。有时候需要一点运气——运气也是可以安排的。"))
          (set-flag! '薇拉)
          (sync-globals!))))

    ;; 林线只借用已有的中央台准备钟，不创建另一套剧院流程。
    ;; 陌生相见只做人物回收；已认识林时，保守 / 精密验收分别推进 1 / 2 格。
    (define (mark-vera-met!)
      (set-flag! '薇拉)
      (sync-globals!))

    ;; ── 小节四·寻找莱恩 ─────────────────────────────
    ;; 三个地点各有一根 0/3：居民区查他是否仍会回来，码头查他是否离港，
    ;; 剧院后台查那份换装表究竟经了谁的手。每处填满才给总钟一格。
    (define (piece-man?) (>= man-step line-steps))
    (define (piece-door?) (>= door-step line-steps))
    (define (piece-paper?) (>= paper-step line-steps))
    (define (piece-count)
      (+ (if (piece-man?) 1 0) (if (piece-door?) 1 0) (if (piece-paper?) 1 0)))
    (define (pieces-full?) (= (piece-count) 3))
    ;; 城里的反应按首演前的日子发生，不拿调查进度当排队号码。
    ;; 第三封信次日贝恩斯上门，再下一天记者来；经理与薇拉发生在玩家去剧院时。
    (define (visitor-due?)
      (< visitor-shown
         (min 2 (max 0 (- premiere-prep-days (days-to-premiere))))))

    (define (emit-notes! notes)
      (if (null? notes)
          #f
          (begin (result-supplement! (car notes)) (emit-notes! (cdr notes)))))

    (define (bump-step! which)
      (cond
        ((equal? which 'man) (set! man-step (+ man-step 1)))
        ((equal? which 'door) (set! door-step (+ door-step 1)))
        ((equal? which 'paper) (set! paper-step (+ paper-step 1)))
        (#t (error "三封信：未知的调查线"))))

    ;; 漏洞：0 未发现 / 1 发现了没堵 / 2 堵上了。发现只在这里发生一次。
    (define (find-vent!)
      (if (= hole-vent 0) (set! hole-vent 1) #f))
    (define (find-power!)
      (if (= hole-power 0) (set! hole-power 1) #f))

    (define (step-done! which notes extra)
      (bump-step! which)
      (emit-notes! notes)
      (extra)
      (sync-globals!))

    ;; 同一地点始终只有这一张调查卡。跨格时发生什么由调查本身决定，
    ;; 不是每填一格就在城市里换出一张新待办。
    (define (investigation-node title anchor subtitle ability which on-progress)
      (node title
        :anchor anchor
        :subtitle subtitle
        :tags (list "低风险")
        :requires (list (req-die))
        :resolve (roll ability
          (outcome (lambda () (spend-composure! 1)))
          (outcome (lambda () (on-progress)))
          (outcome (lambda () (on-progress))))))

    (define (no-extra) #f)

    ;; ── 居民区：他是不是自己走的 ────────────────────
    (define (advance-man!)
      (cond
        ((= man-step 0)
         (step-done! 'man (list "三个晚上没人见莱恩回屋") no-extra))
        ((= man-step 1)
         (step-done! 'man
           (list "屋里衣服和烟都在，炉子上的壶烧干了") no-extra))
        (#t
         (step-done! 'man
           (list "工具、外套和换洗衣物都没动"
                 "屋子像他随时会回来，不像是准备逃走")
           no-extra))))

    (define (node-man-step)
      (investigation-node "沿街找莱恩" #f
        "问住户、看他的屋，再找收租的人" 'social 'man advance-man!))

    ;; ── 剧院后台：那份换装表经了谁的手 ──────────────
    (define (advance-door!)
      (cond
        ((= door-step 0)
         (step-done! 'door
           (list "换装表贴在后台，临时工和道具组都看过") no-extra))
        ((= door-step 1)
         (step-done! 'door
           (list "演出前换过两批临时工，没人记得全名") no-extra))
        (#t
         (step-done! 'door
           (list "没人能说清换装表何时离开过后台"
                 "这能解释信里的细节，却指不出是谁递出去的")
           no-extra))))

    (define (node-door-step)
      (investigation-node "核后台名册" "剧院-后台"
        "查临时工、住址和担保人的笔迹" 'knowledge 'door advance-door!))

    ;; 贝恩斯欠的那一次在这里兑现：档案里抄一个住址，对他只是举手之劳。
    ;; 它省掉的是一颗骰，不是一条别人没有的路。
    (define (node-baines-favor)
      (anchored-instant-action "让他查一个人" "门口"
        (lambda ()
          (play-remote-dialogue!
            (line "尼尔" "剧院上个月新进的一个机工。我要他住哪儿。" "三封信/后台名册/请托/01/尼尔")
            (line "贝恩斯" "名字。" "三封信/后台名册/请托/01/贝恩斯")
            (line "尼尔" "名册上写着，字迹很潦草。" "三封信/后台名册/请托/02/尼尔")
            (line "世界" "两个钟头以后，一张抄好的纸条送到了门房。" "三封信/后台名册/请托/01/世界")
            (line "世界" "上面只有一个地址，没有落款。" "三封信/后台名册/请托/02/世界"))
          (baines 'spend-favor!)
          (advance-door!)
          (result-supplement! "贝恩斯欠你的那一次用掉了"))))

    ;; ── 码头：他到底有没有离开 ──────────────────────
    (define (advance-paper!)
      (cond
        ((= paper-step 0)
         (step-done! 'paper
           (list "当天离港的船上，没有人卖过莱恩船票") no-extra))
        ((= paper-step 1)
         (play-dialogue!
           (line "弗兰克" "不用找了。那晚你拿走铁盒后，他就没露过面。" "三封信/寻找莱恩/弗兰克/01/弗兰克")
           (line "尼尔" "他没来找过船？" "三封信/寻找莱恩/弗兰克/01/尼尔")
           (line "弗兰克" "没有。铺子里的工具都在，外套也没拿。我也在找他。" "三封信/寻找莱恩/弗兰克/02/弗兰克"))
         (step-done! 'paper
           (list "弗兰克也没见过莱恩；他不像是自己走的") no-extra))
        (#t
         (step-done! 'paper
           (list "码头和车站都没有莱恩离开的记录"
                 "没人能证明他仍在城里，也没人能证明他走了")
           no-extra))))

    (define (node-paper-dock)
      (investigation-node "沿码头追问" "码头-账房"
        "问船位、问弗兰克，也问谁最后见过莱恩" 'social 'paper advance-paper!))

    ;; ── 小节四·两个漏洞 ─────────────────────────────
    ;; 发现来自检查；修复各有一根 0/3 Clock。没修完也不是白查——首演夜仍会
    ;; 按「已发现」给对应危机一半的起始进度。
    (define vent-repair-clk
      (make-clock "通风口修复" repair-steps 'gauge
        "填满后封死台底通往看台的旧风口。"))

    (define power-repair-clk
      (make-clock "配电箱更换" repair-steps 'gauge
        "填满后换掉后廊那只满场就跳闸的旧配电箱。"))

    (define (advance-vent-repair! n)
      (vent-repair-clk 'advance! n)
      (if (vent-repair-clk 'full?)
          (begin
            (set! hole-vent 2)
            (result-supplement! "通风口已经封死")))
      (sync-globals!))

    (define (advance-power-repair! n)
      (power-repair-clk 'advance! n)
      (if (power-repair-clk 'full?)
          (begin
            (set! hole-power 2)
            (result-supplement! "配电箱已经换好")))
      (sync-globals!))

    (define (node-fix-vent)
      (node "堵通风口"
        :anchor "剧院-中央台"
        :subtitle "把台底那个封了一年的口重新堵死"
        :clocks (list (vent-repair-clk 'render-data))
        :tags (list "低风险")
        :requires (list (req-die))
        :resolve (roll 'violence
          (outcome (lambda () (spend-composure! 1)))
          (outcome (lambda () (advance-vent-repair! 1)))
          (outcome (lambda () (advance-vent-repair! 2))))))

    (define (node-fix-power)
      (node "换配电箱"
        :anchor "剧院-外圈"
        :subtitle "把后廊那只旧配电箱整个换掉"
        :clocks (list (power-repair-clk 'render-data))
        :tags (list "低风险")
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (outcome (lambda () (spend-composure! 1)))
          (outcome (lambda () (advance-power-repair! 1)))
          (outcome (lambda () (advance-power-repair! 2))))))

    (define (hole-fix-nodes)
      (append
        (if (= hole-vent 1) (list (node-fix-vent)) '())
        (if (= hole-power 1) (list (node-fix-power)) '())))

    (define (advance-theater-check!)
      (cond
        ((= theater-step 0)
         (set! theater-step 1)
         (find-power!)
         (result-supplement! "后廊配电箱一满场就跳闸"))
        ((= theater-step 1)
         (set! theater-step 2)
         (find-vent!)
         (result-supplement! "台底通风口封板已经松了"))
        (#t
         (set! theater-step 3)
         (result-supplement! "灯桥棘齿被薄钢片别住，切口很齐")))
      (sync-globals!))

    (define (node-check-theater)
      (node "逐处检查"
        :anchor "剧院-内环"
        :subtitle "沿外圈、内环和台底走完一遍"
        :tags (list "低风险")
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome (lambda () (spend-composure! 1)))
          (outcome (lambda () (advance-theater-check!)))
          (outcome (lambda () (advance-theater-check!))))))

    ;; 地点只展示调查本身。具体动作收进调查容器，不再和人物、工作并排散在城市里。
    (define (resident-lyon-investigation)
      (container-with-clocks "在居民区寻找莱恩"
        (list (node-man-step))
        (list (local-investigation-clock "居民区调查" man-step))))

    ;; 调查容器跟它唯一那张子卡落在同一处：船位记录在账房，名册在舞台门。
    (define (dock-lyon-investigation)
      (at-anchor "码头-账房"
       (container-with-clocks "在码头寻找莱恩"
        (list (node-paper-dock))
        (list (local-investigation-clock "码头调查" paper-step)))))

    (define (backstage-lyon-investigation)
      (at-anchor "剧院-后台"
       (container-with-clocks "在后台寻找莱恩"
        (list (node-door-step))
        (list (local-investigation-clock "后台调查" door-step)))))

    (define (theater-investigation-children)
      (append
        (if (< theater-step 3) (list (node-check-theater)) '())
        (hole-fix-nodes)
        (if (or (> hole-vent 0) (> hole-power 0))
            (list (node-hole-notes))
            '())))

    (define (theater-check-clock)
      (list 'clock "检查剧院" theater-step 3 'gauge
            (if (= theater-step 3)
                "外圈、内环和台底都检查过了；已经发现的漏洞仍要逐一修复。"
                "检查外圈、内环和台底。")))

    (define (theater-investigation)
      (at-anchor "剧院-内环"
       (container-with-clocks "检查剧院"
        (theater-investigation-children)
        (list (theater-check-clock)))))

    ;; ── 小节四·把三处记录摆开 ───────────────────────
    ;; 三处都查清以后，回家把记录摆开。不给翻案，只让那点说不清的不安浮出来。
    (define (node-board)
      (anchored-instant-action "把线索摆开" "窗台"
        (lambda ()
          (spotlight! "对不上"
            (string-append
              "记录摊在桌上：一间没收拾的屋，一份空白的船位记录，一张换装表。"
              "莱恩没带衣服，没人见他买票，屋里的工具也都在。"
              "剧院的换装表经了许多人的手，灯桥上的薄钢片又切得太齐。"
              "可前两封信是他写的，第三封信也确实把每一处都指向他。"))
          (log! "首演之夜"
            "莱恩不见了。所有证据仍指向他，只有几处细节让尼尔觉得不安。")
          (set-flag! '对不上)
          (sync-globals!))))

    ;; ── 小节四·敲门的人 ─────────────────────────────
    ;; 城市人物按日子来，不按玩家拿到几片线索来排队。
    (define (play-visitor!)
      (set! visitor-shown (+ visitor-shown 1))
      (cond
        ((= visitor-shown 1)
         (if (baines 'receipt?)
             (play-remote-dialogue!
               (line "世界" "早上敲门的是贝恩斯。他没有脱帽，也没有等你请他坐。" "三封信/敲门的人/贝恩斯/01/世界")
               (line "贝恩斯" "第三封信归我登记。剧院的人说你已经看过了。" "三封信/敲门的人/贝恩斯/01/贝恩斯")
               (line "贝恩斯" "不用问了。经理、报社、市政厅都知道了。")
               (line "尼尔" "上星期我去找你们，只有一张回执。" "三封信/敲门的人/贝恩斯/01/尼尔")
               (line "贝恩斯" "上星期是勒索，现在是公共场所的死亡威胁。" "三封信/敲门的人/贝恩斯/02/贝恩斯")
               (line "尼尔" "所以现在值得写进卷宗了。" "三封信/敲门的人/贝恩斯/02/尼尔")
               (line "贝恩斯" "现在归我管。你查到什么，给我留一份。" "三封信/敲门的人/贝恩斯/03/贝恩斯"))
             (play-remote-dialogue!
               (line "世界" "早上敲门的是贝恩斯。他没有脱帽，也没有等你请他坐。" "三封信/敲门的人/贝恩斯/01/世界")
               (line "贝恩斯" "第三封信归我登记。剧院的人说你已经看过了。" "三封信/敲门的人/贝恩斯/01/贝恩斯")
               (line "贝恩斯" "不用问了。经理、报社、市政厅都知道了。")
               (line "尼尔" "那封勒索信，没有人收。" "三封信/敲门的人/贝恩斯/没报案/01/尼尔")
               (line "贝恩斯" "勒索不归我跑。死亡威胁归我。" "三封信/敲门的人/贝恩斯/没报案/01/贝恩斯")
               (line "尼尔" "所以现在值得写进卷宗了。" "三封信/敲门的人/贝恩斯/02/尼尔")
               (line "贝恩斯" "现在归我管。你查到什么，给我留一份。" "三封信/敲门的人/贝恩斯/03/贝恩斯"))))
        (#t
         (play-remote-dialogue!
           (line "记者" "我手上有个故事，就差你点个头。" "三封信/敲门的人/记者/01/记者")
           (line "尼尔" "什么故事。" "三封信/敲门的人/记者/01/尼尔")
           (line "记者" "莱恩。前两封信、旧情、后台细节，全对得上。" "三封信/敲门的人/记者/02/记者")
           (line "尼尔" "我没这么说。" "三封信/敲门的人/记者/02/尼尔")
           (line "记者" "你也没说不是。" "三封信/敲门的人/记者/03/记者")
           (line "尼尔" "这事没完，别急着印。" "三封信/敲门的人/记者/03/尼尔")
           (line "记者" "明天首演，读者只想看一个名字。" "三封信/敲门的人/记者/04/记者")
           (line "世界" "第二天报纸照样登了。我什么也没承认过。" "三封信/敲门的人/记者/01/世界")
           (line "世界" "那行大标题的意思只有一句：查完了，是他，结案。" "三封信/敲门的人/记者/02/世界")))))

    ;; ── 首演之夜 ────────────────────────────────────
    (define premiere-pending? #f)

    (define (begin-premiere!)
      (if premiere-pending?
          (error "三封信：首演之夜已经在等待处理")
          #t)
      (frank 'validate-chapter-end!)
      (set! premiere-pending? #t)
      (sync-globals!)
      (sync-blockers!)
      (notify! "今天是首演。天黑以前你得到剧院去。"))

    (define (node-premiere-entry)
      (encounter-action "去剧院"
        (lambda ()
          (play-remote-dialogue!
            (line "夜莺" "别去台下。站在我一转头就能看见你的地方。" "三封信/首演/登台前/01/夜莺")
            (line "尼尔" "我就在侧台盯着。" "三封信/首演/登台前/01/尼尔")
            (line "经理" "还有两分钟！各就各位！" "三封信/首演/登台前/01/经理"))
          (spotlight! "开演"
            "第三段开始时，全场的灯一起灭了。")
          (start-encounter "首演之夜" on-premiere-result))))

    ;; 交锋只回传 'done：四个向量走 global，由这里解释并写成结案。
    (define (on-premiere-result result)
      (if (not premiere-pending?)
          (error "三封信：没有待处理的首演")
          #t)
      (if (equal? result 'done) #t (error "三封信：首演交锋返回了未登记的结果"))
      (set! premiere-pending? #f)
      (set! premiere-done? #t)
      (if (get-global '首演-她受伤) (worsen-condition! 2) #f)
      (advance-stage! 5)
      (sync-globals!)
      (sync-blockers!))

    ;; ── 尾声一：灯重新亮起来(必看) ───────────────────
    ;; 第一章在情绪上是一次胜利。这一段就按胜利写，不留一点反讽的语气——
    ;; 反讽全部藏在事实里：警方还没查完，经理已经在跟记者说人抓到了。
    (define (node-lights-up)
      (anchored-instant-action "灯重新亮起来" "剧院-后台"
        (lambda ()
          (play-dialogue!
            (line "世界" "大幕落下。后台乱成一团，闪光灯闪得人眼睛发疼。" "三封信/首演抓获/01/世界")
            (line "世界" "一个满脸是血、浑身酒气的男人被两名警察死死按在地毯上——正是莱恩。" "三封信/首演抓获/02/世界")
            (line "世界" "他眼神发直，手里抓着一把不知从哪捡来的螺丝刀，嘴里含糊地嘟囔着什么。" "三封信/首演抓获/03/世界")
            (line "尼尔" "……他在后台晃荡了多久？" "三封信/首演抓获/01/尼尔")
            (line "贝恩斯" "人在配电房外抓到。记者让开，现场先留给我。")
            (line "经理" "人已经抓住了！剧院绝不向这种下三滥低头！" "三封信/首演抓获/01/经理"))
          (set-flag! '灯亮起来)
          (sync-globals!)
          (sync-blockers!)
          (spotlight! "记者围上来了"
            "他们要一个故事，而故事就躺在过道的地毯上。"))))

    ;; ── 尾声二：她说那句话(必看) ─────────────────────
    ;; 「是他」不给玩家选择——那是她的选择，不是你的。
    (define (her-last-look)
      (cond
        ((equal? her-answer "追究")
         "她看了你一眼。那一眼里没有歉意，只有一句「你早就知道我是什么人」。")
        ((equal? her-answer "摊牌")
         "她看了你一眼，很快移开。她知道你在想什么，也知道你不会说出来。")
        ((equal? her-answer "照帮")
         "她看了你一眼，停得比刚才久。那是这一晚她唯一一次没有摆好表情。")
        ((equal? her-answer "沉默")
         "她看了你一眼，什么也没读到，于是转回了镜头那边。")
        (#t "她看了你一眼。")))

    (define (node-stay-quiet)
      (anchored-instant-action "什么也不说" "剧院-后台"
        (lambda ()
          (play-dialogue!
            (line "世界" "我没有开口。那些疑点还不够推翻任何事。" "三封信/是他/沉默/01/世界")
            (line "世界" "莱恩写过两封信，今晚又在后台被当场逮住。" "三封信/是他/沉默/02/世界")
            (line "世界" "可那种顺理成章的感觉，反而让我心里发紧。" "三封信/是他/沉默/03/世界"))
          (set-flag! '是他)
          (finish-press!))))

    (define (finish-press!)
      (play-dialogue!
        (line "记者" "夜莺小姐！就是这个男人一直在威胁你吗？！" "三封信/是他/指认/01/记者")
        (line "世界" "夜莺抬起头，看着被按在地上的莱恩。莱恩抬起肿胀的眼睛，嘴唇动了动。" "三封信/是他/指认/01/世界")
        (line "世界" "她迎着刺眼的闪光灯站直了身子。" "三封信/是他/指认/02/世界")
        (line "夜莺" "……是他。" "三封信/是他/指认/01/夜莺" :light 'blackout)
        (line "世界" "快门声连成一片。" "三封信/是他/指认/03/世界")
        (line "世界" "莱恩被反剪着胳膊拖了出去。门关上的那一瞬间，他还在死死盯着她。" "三封信/是他/指认/04/世界"))
      (rest-release! "三封信/是他")
      (sync-globals!)
      (sync-blockers!)
      (log! "首演之夜" "首演之夜唱完了。莱恩被押着往外走，一直回头看她。")
      (spotlight! "第一章完"
        "莱恩被带走。姑娘没事，首演没有停。所有人都认定案子结了。"))

    ;; ── 公开结案(必看) ──────────────────────────────
    ;; 第一章在情绪上是一次公开的胜利。尼尔的疑虑不足以构成翻案，
    ;; 所以只留在对白与叙述里，不让它改变公众结论。
    (define (node-closing)
      (instant-action "散场之后"
        (lambda ()
          (play-remote-dialogue!
            (line "贝恩斯" "他身上有工具，人在配电房外抓到。够我扣人。")
            (line "尼尔" "他浑身都是酒气，连站都站不稳，怎么上的灯桥？" "三封信/散场结案/01/尼尔")
            (line "贝恩斯" "我没说他上过灯桥。那一段还得查。")
            (line "贝恩斯" "可市长在台下，局长明早要报告。记者连标题都写好了。")
            (line "贝恩斯" "你若找到能推翻这些证据的，明早带来。今晚别对记者说。"))
          (set-flag! '结案)
          (rest-release! "三封信/结案")
          (sync-globals!)
          (complete-task! "首演之夜")
          (spotlight! "第二天的头版"
            (string-append
              "头版写着：夜莺不惧威胁，华丽谢幕。"
              "警方封锁了老街。"
              (if (get-global '首演-她受伤)
                  "照片里看不出她手上的绷带。"
                  "照片里的她站在谢幕灯下。")
              "委托结束。"
              ;; 查到底的人回家时，那些调查记录仍摊在桌上；它们不构成翻案。
              (if (has-flag? '对不上)
                  "回到家，调查记录还摊在桌上。你没有把它们收起来。"
                  "")))
          ;; 舞台：收场戏，只有一处低下去（提到莱恩）、一处抬起来（门票）；
          ;; 他点烟，她背过身说"下回"——戏在她背上收。
          (closing-dialogue!))))

    ;; ── 状态卡 ──────────────────────────────────────
    (define (days-tail)
      (string-append " · 首演还有 " (number->string (days-to-premiere)) " 天"))

    (define (client-subtitle)
      (cond
        ((= story-stage 1) (string-append "酒馆驻唱" (days-tail)))
        ((= story-stage 2) (string-append "她的过去被人攥在手里" (days-tail)))
        ((= story-stage 3)
         (string-append
           (cond
             ((equal? lesson-route "失败") "东西没拿到，经理已经换人")
             (lesson-done? "莱恩交出了东西")
             (else "该去会一会莱恩"))
           (days-tail)))
        ((= story-stage 4) (string-append "有人要她死在台上" (days-tail)))
        ((= story-stage 5) "首演之后")
        (else "")))

;; 卷宗上这一行只写**你要做什么**，不写做了之后会发生什么。
;; "回家见夜莺"是任务；"听她讲码头居民区"是她要说的话——写在这儿等于替玩家
;; 先把那场戏剧透了一半，而且那句话还没发生。会发生什么由情势那段（situation-text）
;; 和真正演出来的对白负责，这一行只负责指路。
(define (current-objective)
      (cond
        ((= story-stage 1)
         (if delivery-pending?
             "去码头盯住邮箱，等人取信"
             "勒索日之前凑足100块钱"))
        ((= story-stage 2)
         (cond
           ((not (has-flag? '伤后探望))
            "回家见夜莺")
           ((second-letter-pending?)
            "去剧院见经理，查看第二封信")
           ((and bridge-identified? (not (has-flag? '第二封信)))
            "已查出莱恩；等剧院的下一步消息")
           (material-settled?
            "去剧院报告莱恩身份")
           ((singer-waiting?)
            "等夜莺安排好")
           ((alone-phase?)
            "去码头居民区追查「老金牌」香烟")
           ((and (has-companion? '夜莺) (equal? (actor-status '夜莺) "away"))
            "她今天不肯再进老街了；睡一觉，明天她才会再跟你去")
           (else
            "跟着夜莺探索老街东西两条路；西边桥廊通向写信人的身份")))
        ((= story-stage 3)
         (cond
           ((not lesson-done?)
           (if (inquiry-done?)
               "去码头，今晚到货栈后面找莱恩"
                "等经理的人查到莱恩住处"))
           ((equal? report-pending "巷子失败")
            "空手回去见经理")
           ((and lesson-done? (not (equal? lesson-route "失败"))
                 (not (has-flag? '她剪底片)))
            "去剧院把底片交给夜莺")
           ((third-letter-due?)
            "去剧院查看第三封信")
           ((equal? lesson-route "失败")
            "等待首演之夜")
           (else
            "前半段委托已了结；在首演前处理自己的生活")))
        ((= story-stage 4)
         (cond
           ((not (has-flag? '她不取消))
            "去剧院见夜莺，确认她是否登台")
           (premiere-pending?
            "首演就在今晚，去剧院")
           ((and (pieces-full?) (not (has-flag? '对不上)))
            "三处都查清了，回家把疑点摆开")
           (else
            "检查剧院；去居民区、后台和码头寻找莱恩")))
        ((= story-stage 5)
         (cond
           ((not (has-flag? '灯亮起来)) "留在剧院，等灯亮起来")
           ((not (has-flag? '是他)) "记者围着她，你还有一句话可以说")
           ((not (has-flag? '结案)) "去剧院外见贝恩斯")
           (#t "委托已完成")))
        (else (error "三封信：主线任务卡收到未登记的故事阶段"))))

    (define (situation-text)
      (cond
        ((= story-stage 1)
         (string-append
           "她在老街的酒馆唱歌，刚被一个剧院经理看中。首演是她等了多年的那一步——如果走得到的话。"
           "写信的人挑的就是这个时候。三天后，邮箱里得有一百金。"
           "她要你别报警，也别让剧院知道：他们不会去查是谁写的，他们会换一个不惹事的人。"))
        ((= story-stage 2)
         (if bridge-identified?
             (string-append
               "桥廊的住户叫出了那个名字：莱恩。夜莺承认她认识他。"
               (if (has-flag? '第二封信)
                   "把这件事告诉经理。"
                   "案子暂时停在这里，等剧院的下一步消息。"))
             (string-append
               "取信的人往码头居民区去了。那一片是她长大的地方，也是她再没回去过的地方。"
               (if (found-cigarettes?)
                   "取信的人逃走时落下半包烟——那个牌子在老街买不起。"
                   "那个人跟丢了，你手上只有一个方向。"))))
        ((= story-stage 3)
         (if lesson-done?
             (if (equal? lesson-route "失败")
                 (string-append
                   "你没能从莱恩手里拿到东西，经理把工作交给了别人。"
                   "夜莺仍邀请你来看首演；海报已经贴到剧院门外。")
                 (string-append
                   "莱恩交出了铁盒，也答应不再找她。"
                   "她仍在老街酒馆唱自己的班，只是经理来得越来越勤，首演的海报也已经贴到了门外。"))
             (string-append
               "名字有了：莱恩。和她一起在那层楼上长大，她走了，他留下了。"
               "他不敢回老街，住在码头货栈后面那片租屋里。剩下的事该由你去跟他说清楚。"
               (if (inquiry-done?)
                   "经理的人查到了具体哪一间。今晚随时可以过去——认识谁，决定你怎么进得去那片地方。"
                   "经理雇的人还在查他住哪一间。"))))
        ((= story-stage 4)
         (string-append
           "第三封信不要钱，只要她的命，而且知道只有后台的人才知道的事。"
           "经理不肯取消首演，夜莺也不肯——只剩五天检查剧院，并寻找莱恩的去向。"
           "居民区、剧院后台和码头各有一条线索；它们不能排除莱恩，却让人心里发紧。"
           "剧院是圆的，她在正当中；真正能提前改变现场的，是你查明并修好的漏洞。"))
        ((= story-stage 5)
         "报纸把这件事写完了。案子结了，她站上了她等了多年的那个位置。")
        (else "")))

    ;; 世界根节点不再单独展示主线钟；它们统一收在「三封信」任务卡上。
    (define (world-clocks)
      '())

    (define (delivery-deadline-clock)
      (list 'clock "勒索日" (days-to-delivery) letter-deadline 'countdown
            (if delivery-pending?
                "就是今天。还能先在城里做事；去码头投信后才能结束这一天。"
                (string-append
                  "信上写的日子。到期当天必须去码头盯住邮箱。"
                  "夜莺最多替你补 " (number->string nightingale-cover-max)
                  " 金，再多她也拿不出来。"))))

    ;; 等她回老街的日子已经由 singer-guiding-day 持有；这里仅把同一事实
    ;; 投影成卷宗可读的倒计时，不另建一根会和世界日脱节的存档时钟。
    (define (singer-wait-clock)
      (list 'clock "回老街的约定"
            (max 0 (- singer-guiding-day world-day))
            singer-wait-days 'countdown
            "她说两天后在巷口等你。"))

    ;; 巷子之后压力换人：经理不再催（他以为事情结束了），催你的是钉死的首演日。
    (define (premiere-clock)
      (list (list 'clock "首演之夜"
                  (days-to-premiere) (premiere-window) 'countdown
                  "钉死的日子。归零那天不管你准备到哪一步，幕都会拉开。")))

    ;; 总钟只回答「莱恩这条线查到了几处」。每个地点另有自己的局部钟；
    ;; 局部钟填满，才把一条真正能带走的线索交给总钟。
    (define (pieces-clock)
      (list 'clock "寻找莱恩" (piece-count) 3 'gauge
            (if (pieces-full?)
                "三处都查清了。把记录摆开，看看那些说不清的地方。"
                "去居民区、剧院后台和码头各查清一处。")))

    (define (local-investigation-clock label current)
      (list 'clock label current line-steps 'gauge
            "填满后得到一条关于莱恩去向的完整线索。"))

    ;; 漏洞不是进度，是三档状态，做成标注挂在剧院。
    (define (hole-line label hole)
      (cond
        ((= hole 2) (string-append label "：堵上了，那天晚上不用管它"))
        ((= hole 1) (string-append label "：查到了，还敞着"))
        (#t (string-append label "：还没人提起过这处"))))

    (define (node-hole-notes)
      (at-anchor "剧院-内环"
       (note-node "标注-剧院的漏洞" ""
        (string-append (hole-line "台底的通风口" hole-vent)
                       "；"
                       (hole-line "后廊的配电箱" hole-power)))))

    (define (card-clocks)
      (cond
        ;; 踩点那根钟不进卷宗：它挂在码头那张踩点卡上，站在那儿就看得见，
        ;; 而卷宗要回答的是「现在去干什么」——勒索日和勒索款筹集就够了。
        ((= story-stage 1)
         (append (list (delivery-deadline-clock))
                 (delivery-fund-clock)))
        ((and (= story-stage 2) (singer-waiting?))
         (list (singer-wait-clock)))
        ((and (= story-stage 2) (patience-running?)) (patience-clock))
        ((lesson-beat?)
         (append (if (inquiry-done?)
                     '()
                     (list (list 'clock "经理的人在找他"
                                 (max 0 (- inquiry-days (days-waited)))
                                 inquiry-days 'countdown
                                 "他雇的人去查莱恩住哪儿。归零那天消息回来。")))
                 (patience-clock)))
        ((and (= story-stage 3) (patience-running?)) (patience-clock))
        ((quiet-period?) (premiere-clock))
        ((beat3-open?)
         (append (premiere-clock)
                 (list (pieces-clock))))
        (else '())))

    ;; 案情摘要那一整段叙述删了：那是写给作者自己的设定，不是玩家隔三天回来要读的东西。
    ;; 该留的都变成了履历里的一句话（log!），一拍一句。
    (define (objective-place)
      (cond
        ((= story-stage 1) (if delivery-pending? "码头" "码头"))
        ((= story-stage 2)
         (cond
           ((not (has-flag? '伤后探望)) "家")
           ((second-letter-pending?) "剧院")
           ((and material-settled? (has-flag? '第二封信)) "剧院")
           ((alone-phase?) "码头居民区")
           (#t "码头居民区")))
        ((= story-stage 3)
         (cond
           ((and (not lesson-done?) (inquiry-done?)) "码头")
           ((equal? report-pending "巷子失败") "剧院")
           ((and lesson-done? (not (equal? lesson-route "失败"))
                 (not (has-flag? '她剪底片))) "剧院")
           ((third-letter-due?) "剧院")
           (#t "")))
        ((= story-stage 4) "剧院")
        ((= story-stage 5) "剧院")
        (else "")))

    ;; ── 卷宗：四张卡 ─────────────────────────────────
    ;; 第一章不是一张横跨全章的长卡，是四个小节各一张：勒索信 → 老街 → 莱恩 → 首演之夜。
    ;; 每张卡列这一节必经的事，做完划掉；最后一件划掉那一拍 complete-task!，卡收成 了结。
    ;; 上一张收的那一拍正是下一张开的那一拍，所以任何时候只有一张主要的卡开着；
    ;; 了结的卡留在面板底部当履历，不再上地图。
    ;; 可做可不做的（踩点、东侧见弗兰克）不列——列了就没有「最后一项」可言。
    (define (task-card id status now where clocks steps)
      (dossier id
        :kind '委托
        :primary #t
        :status status
        :now now
        :where where
        :clocks clocks
        :steps steps
        :log ((task-journal id) 'render-data)))

    ;; 勒索信：她登门，到投信之后她来看你。
    (define (ransom-done?) (has-flag? '伤后探望))
    (define (card-ransom)
      (task-card "勒索信"
        (if (ransom-done?) '了结 '进行中)
        (cond ((ransom-done?) "")
              ((= story-stage 1) (current-objective))
              (#t "回家见夜莺"))
        (cond ((ransom-done?) "")
              ((= story-stage 1) "码头")
              (#t "家"))
        (if (= story-stage 1) (card-clocks) '())
        (list (step "凑够一百块"
                    (or (has-flag? '勒索信已结算) (>= (item-count "金钱") delivery-price)))
              (step "投信之夜，盯住邮箱" (has-flag? '勒索信已结算))
              (step "回家见夜莺" (ransom-done?)))))

    ;; 老街：一个人进不去，她带你进去，桥廊叫出名字，交给经理。
    (define (warren-task-done?) (>= story-stage 3))
    (define (card-warren)
      (task-card "老街"
        (cond ((warren-task-done?) '了结)
              ((singer-waiting?) '等着别人)
              ((and material-settled? (not (has-flag? '第二封信))) '等着别人)
              (#t '进行中))
        (if (warren-task-done?) "" (current-objective))
        (if (warren-task-done?) "" (objective-place))
        (if (warren-task-done?) '() (card-clocks))
        (list (step "独自去码头居民区打听"
                    (or (alone-trip-done?) (> singer-guiding-day 0)))
              (step "等她两天" (singer-guiding?))
              (step "带她走老街，问出那个名字" bridge-identified?)
              (step "去剧院见经理，看第二封信" (has-flag? '第二封信))
              (step "向经理报告莱恩的身份" (warren-task-done?)))))

    ;; 莱恩：等地址，赶在剧院的人前头去，拿回铁盒交给她。失败线在被解雇那一拍结。
    (define (lyon-task-done?) (or (has-flag? '她剪底片) (has-flag? '被经理解雇)))
    (define (card-lyon)
      (task-card "莱恩"
        (cond ((lyon-task-done?) '了结)
              ((and (not lesson-done?) (not (inquiry-done?))) '等着别人)
              (#t '进行中))
        (if (lyon-task-done?) "" (current-objective))
        (if (lyon-task-done?) "" (objective-place))
        (if (lyon-task-done?) '() (card-clocks))
        (list (step "等经理的人查到他住哪儿" (inquiry-done?))
              (step "赶在剧院的人前头去货栈" lesson-done?)
              (step "让莱恩交出铁盒" (equal? lesson-route "谈"))
              (step "把底片交给夜莺" (has-flag? '她剪底片)))))

    ;; 首演之夜：莱恩那一节一结就开——海报已经贴出去了，日子钉死。
    ;; 五天里查不查、查到哪儿由玩家定；幕拉开那天没划掉的就那么留着。
    (define (premiere-task-open?) (or (>= story-stage 4) (quiet-period?)))
    (define (premiere-task-done?) (has-flag? '结案))
    (define (card-premiere)
      (task-card "首演之夜"
        (cond ((premiere-task-done?) '了结)
              ((and (= story-stage 3) (not (third-letter-due?))) '等着别人)
              (#t '进行中))
        (if (premiere-task-done?) "" (current-objective))
        (if (premiere-task-done?) "" (objective-place))
        (if (premiere-task-done?) '() (card-clocks))
        (list (step "去剧院看第三封信" (has-flag? '第三封信))
              (step "见夜莺，问她登不登台" (has-flag? '她不取消))
              (step "查莱恩的去向" (pieces-full?))
              (step "检查剧院" (= theater-step 3))
              (step "首演之夜" premiere-done?)
              (step "散场之后" (premiere-task-done?)))))

    (define (dossier-entry)
      (append
        (if (>= story-stage 1) (list (card-ransom)) '())
        (if (has-flag? '伤后探望) (list (card-warren)) '())
        (if (>= story-stage 3) (list (card-lyon)) '())
        (if (premiere-task-open?) (list (card-premiere)) '())))

    ;; 世界根上不再挂主线卡。
    (define (render-data) '())

    ;; ── 夜莺本人 ────────────────────────────────────
    ;; 只在剧院、且有话要说时出现（给负评 / 谈取消）；无事时整卡省略。
    (define (nightingale-anchor-name location)
      (cond
        ((equal? location "老街酒馆") "夜莺@酒馆")
        ((equal? location "剧院") "夜莺@剧院")
        (else (error "夜莺：人物节点所在地点没有登记空间锚点"))))

    ;; 「她今晚的样子」不做成卡：站在她面前就看得见的东西，不是查了才知道的。
    (define (nightingale-node location extra-children)
      (node "夜莺"
        :anchor (nightingale-anchor-name location)
        :subtitle (client-subtitle)
        :children extra-children))

    ;; 强制事件一律放进实际发生的地点。世界根节点不再承载剧情动作。
    (define (world-nodes)
      '())

    ;; 各地点向故事要自己这一拍的节点。地点不认识故事状态,只认自己的名字。
    (define (nodes-at location)
      (cond
        ((equal? location "家")
         (append
           (if (= story-stage 0) (list (node-answer-door)) '())
           (if (and (beat3-open?) (pieces-full?) (not (has-flag? '对不上)))
               (list (node-board))
               '())
           (if (and (beat3-open?) (= door-step 2) (baines 'owed?))
               (list (node-baines-favor))
               '())
           (if (and (= story-stage 2) (not (has-flag? '伤后探望)))
               (list (node-her-visit))
               '())
           '()))
        ((equal? location "码头")
         (append
           (if delivery-pending? (list (node-delivery-entry)) '())
           (beat1-dock-nodes)
           ;; 小节三出发口：货栈后面就在码头这一侧。三天到了才亮。
           (if (alley-open?) (list (node-alley-entry)) '())
           ;; 小节四·码头线：从找船位开始，弗兰克在中途实际出场。
           (if (and (beat3-open?) (not (piece-paper?)))
               (list (dock-lyon-investigation))
               '())))
        ((equal? location "老街酒馆")
         (if (beat2-open?) (list (node-lyon-talk)) '()))
        ;; 老街节点全部平铺在居民区下；探索过程消失后，只留下有后续玩法的人物与地点。
        ((equal? location "码头居民区")
         (append
           ;; 小节四·线一：他不见了。这条线全部在老街。
           (if (and (beat3-open?) (not (piece-man?)))
               (list (resident-lyon-investigation))
               '())
           (if (and (alone-phase?) (not (alone-trip-done?)))
               (list (node-alone-warren))
               '())
           (if (and (not (east-porch-done?))
                    (not bridge-identified?)
                    (or (warren-open?) (> (east-entry-clk 'current) 0)))
               (list (node-east-entry))
               '())
           (if (and (east-porch-done?) (not (east-route-done?)) (not bridge-identified?))
               (list (node-east-gallery))
               '())
           (if (and (east-porch-done?) (not (east-route-done?))
                    (not bridge-identified?) (found-cigarettes?))
               (list (node-show-cigs-at-counter))
               '())
           (if (and bridge-identified? (not union-checked?))
               (list (node-search-for-frank))
               '())
           (if (and (not (west-stairs-done?))
                    (or (warren-open?) (> (west-stairs-clk 'current) 0)))
               (list (node-west-stairs))
               '())
           (if (west-friction-pending?)
               (list (node-west-friction-talk) (node-west-friction-shove))
               '())
           (if (and west-friction-cleared? (not (west-route-done?)))
               (list (node-west-bridge))
               '())
           (if bridge-identified?
               (list (bridge-apartments-place))
               '())))
        ((equal? location "警察局") '())
        ((equal? location "剧院")
         (append
             (list (node-manager))
             (if (and (singer-present?) (equal? (singer-location) "剧院"))
                 (let ((kids (append
                              (if (and lesson-done? (not (equal? lesson-route "失败"))
                                        (not (has-flag? '她问了你)))
                                  (list (node-give-negatives))
                                  '())
                              ;; 第三封信后的谈话一次谈完登台与她的原则。
                              ;; 谢幕是正式演出固定的一部分，不在城市阶段提供取消分支。
                              (if (beat3-open?)
                                  (if (has-flag? '她不取消) '() (list (node-she-refuses)))
                                  '()))))
                   (if (null? kids)
                       '()
                       (list (nightingale-node "剧院" kids))))
                 '())
             (if premiere-pending? (list (node-premiere-entry)) '())
             (if (and (= story-stage 5) (not (has-flag? '灯亮起来)))
                 (list (node-lights-up))
                 '())
             (if (and (= story-stage 5) (has-flag? '灯亮起来) (not (has-flag? '是他)))
                 (list (node-stay-quiet))
                 '())
             (if (and (= story-stage 5) (has-flag? '是他) (not (has-flag? '结案)))
                 (list (node-closing))
                 '())
             (if (and (quiet-period?) (not (has-flag? '看彩排)))
                 (list (node-rehearsal))
                 '())
             (if (beat3-open?)
                (append
                  (if (null? (theater-investigation-children))
                      '()
                      (list (theater-investigation)))
                  (if (piece-door?)
                      '()
                      (list (backstage-lyon-investigation)))
                  '())
                '())))
        (else '())))

    ;; ── 日终 ────────────────────────────────────────
    ;; 世界日历规则先把 world-day 推到醒来后的新日期，本模块再据新日期触发当天事件。
    ;; 第二封信用电话自动表现；勒索日与首演只挂起必看事件，由阻塞休息逼玩家亲自前往。
    (define-turn-rule "第二封信紧急来电"
      (lambda () (second-letter-call-due?))
      (lambda () (begin-second-letter-call!)))

    (define-turn-rule "尼尔想到警察局"
      (lambda ()
        (and (has-flag? '第二封信)
             (not (has-flag? '警察局开放))))
      (lambda ()
        (play-dialogue!
          (line "尼尔" "（语音）第一封信的时候，她不让我报警。")
          (line "尼尔" "现在第二封信进了剧院，经理也知道了。")
          (line "尼尔" "这已经不只是替她守住一个秘密。")
          (line "尼尔" "警察未必肯管。至少该让他们留下一笔。"))
        (set-flag! '警察局开放)))

    ;; 拿到第 n 片的次日清晨有人来敲门。放在日终规则里，因为它不该打断玩家当天的动作。
    (define-turn-rule "有人来敲门"
      (lambda () (and (beat3-open?) (visitor-due?)))
      (lambda () (play-visitor!)))

    (define-turn-rule "第三封信送到"
      (lambda () (and (third-letter-due?) (not (has-flag? '第三封信来人))))
      (lambda () (begin-third-letter-call!)))

    (define-turn-rule "第一章定日事件"
      (lambda ()
        (or (and (= story-stage 1) (not delivery-pending?)
                 (>= world-day delivery-day))
            (and (= story-stage 4) (not premiere-pending?) (not premiere-done?)
                 (>= world-day premiere-day))))
      (lambda ()
        (if (= story-stage 1)
            (begin-delivery!)
            (begin-premiere!))))

    ;; ── 消息接口 ────────────────────────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data) (render-data))
          ((equal? msg 'world-nodes) (world-nodes))
          ((equal? msg 'world-clocks) (world-clocks))
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((equal? msg 'dossier) (dossier-entry))
          ((equal? msg 'story-stage) story-stage)
          ((equal? msg 'days-to-premiere) (days-to-premiere))
          ((equal? msg 'singer-present?) (singer-present?))
          ((equal? msg 'old-street-open?) (old-street-open?))
          ((equal? msg 'theater-open?) (theater-open?))
          ((equal? msg 'police-open?) (police-open?))
          ((equal? msg 'freight-open?) (freight-open?))
          ((equal? msg 'delivery-result) delivery-result)
          ((equal? msg 'has-flag?) (has-flag? (cadr args)))
          ;; 小节二重构时从这里取抓手：半包「老金牌」香烟是投信那夜唯一的具名物证。
          ((equal? msg 'found-cigarettes?) (found-cigarettes?))
          ((equal? msg 'sync-blockers!) (sync-blockers!))
          ((equal? msg 'sync-globals!) (sync-globals!))
          ((equal? msg 'debug-finish!) (debug-finish!))
          ((equal? msg 'debug-play-visitor!) (play-visitor!))
          ((equal? msg 'debug-enable-second-letter!) (set-flag! '第二封信))
          ((equal? msg 'debug-stage-bridge!) (bridge-aftermath-dialogue!))
          ((equal? msg 'debug-stage-resident-arrival!) (first-resident-dialogue!))
          ((equal? msg 'debug-theatre-rainy-door!) (rainy-door-theatre!))
          ((equal? msg 'debug-stage-commission!) (commission-dialogue!))
          ((equal? msg 'debug-stage-refusal!) (refusal-dialogue!))
          ((equal? msg 'debug-stage-closing!) (closing-dialogue!))
          ((equal? msg 'mark-vera-met!) (mark-vera-met!))
          ((equal? msg 'save)
           (list
             (list "story-stage" story-stage)
             (list "journals" (map (lambda (j) (list (car j) ((cadr j) 'save))) task-journals))
             (list "delivery-day" delivery-day)
             (list "delivery-pending?" delivery-pending?)
             (list "delivery-result" delivery-result)
             (list "delivery-money" delivery-money)
             (list "delivery-shortfall" delivery-shortfall)
             (list "mail-routine" (mail-clock 'save))
             (list "condition-level" condition-level)
             (list "alley-thrown-out?" alley-thrown-out?)
             (list "warren-alone" (warren-alone-clk 'save))
             (list "singer-guiding-day" singer-guiding-day)
             (list "singer-guiding-told?" singer-guiding-told?)
             (list "west-friction-cleared?" west-friction-cleared?)
             (list "east-entry" (east-entry-clk 'save))
             (list "east-gallery" (east-gallery-clk 'save))
             (list "west-stairs" (west-stairs-clk 'save))
             (list "west-bridge" (west-bridge-clk 'save))
             (list "frank-search" (frank-search-clk 'save))
             (list "union-checked?" union-checked?)
             (list "bridge-identified?" bridge-identified?)
             (list "patience" (patience-clk 'save))
             (list "patience-day" patience-day)
             (list "manager-warning-level" manager-warning-level)
             (list "manager-phone-given?" manager-phone-given?)
             (list "report-pending" report-pending)
             (list "inquiry-day" inquiry-day)
             (list "inquiry-told?" inquiry-told?)
             (list "lesson-done?" lesson-done?)
             (list "lesson-day" lesson-day)
             (list "lesson-route" lesson-route)
             (list "her-answer" her-answer)
             (list "man-step" man-step)
             (list "door-step" door-step)
             (list "paper-step" paper-step)
             (list "theater-step" theater-step)
             (list "hole-vent" hole-vent)
             (list "hole-power" hole-power)
             (list "vent-repair" (vent-repair-clk 'save))
             (list "power-repair" (power-repair-clk 'save))
             (list "visitor-shown" visitor-shown)
             (list "envelope-thin?" envelope-thin?)
             (list "material-settled?" material-settled?)
             (list "settle-route" settle-route)
             (list "lyon-fate" lyon-fate)
             (list "settled-day" settled-day)
             (list "premiere-day" premiere-day)
             (list "premiere-done?" premiere-done?)
             (list "premiere-pending?" premiere-pending?)
             (list "scene-flags" scene-flags)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! story-stage (assoc-get data "story-stage" 0))
             (let ((saved (assoc-get data "journals" '())))
               (map (lambda (j) ((cadr j) 'load! (assoc-get saved (car j) '()))) task-journals))
             (set! delivery-day (assoc-get data "delivery-day" 0))
             (set! delivery-pending? (assoc-get data "delivery-pending?" #f))
             (set! delivery-result (assoc-get data "delivery-result" "未定"))
             ;; 物品名补上「香烟」后，把旧存档中的旧 ID 转走，不能让它留成一件
             ;; 再也不能投入行动槽的孤儿物品。
             (let ((legacy-count (item-count legacy-cigs-item)))
               (if (> legacy-count 0)
                   (begin
                     (add-item! cigs-item legacy-count)
                     (remove-item! legacy-cigs-item legacy-count))
                   #f))
             (set! delivery-money (assoc-get data "delivery-money" 0))
             ;; 旧存档把这两样东西记成布尔量。读到 #t 而物品栏里没有,就补发一件;
             ;; 新存档里它们只存在于物品栏,这两行读完就再也不写回去。
             (if (and (assoc-get data "runner-cigarettes?" #f) (not (found-cigarettes?)))
                 (add-item! cigs-item 1)
                 #f)
             (if (and (assoc-get data "lesson-material?" #f)
                      (= (item-count "莱恩的照片与信") 0)
                      (= (item-count "莱恩的底片与照片") 0))
                 (add-item! "莱恩的照片与信" 1)
                 #f)
             (set! delivery-shortfall (assoc-get data "delivery-shortfall" -1))
             (if (or (< delivery-shortfall -1) (> delivery-shortfall delivery-price))
                 (error "三封信存档错误：勒索款筹集差额非法")
                 #t)
             (mail-clock 'load! (assoc-get data "mail-routine" 0))
             (set! condition-level (assoc-get data "condition-level" 0))
             (if (or (< condition-level 0) (> condition-level 2))
                 (error "三封信存档错误：夜莺处境等级非法")
                 #t)
             (set! alley-thrown-out? (assoc-get data "alley-thrown-out?" #f))
             (warren-alone-clk 'load! (assoc-get data "warren-alone" 0))
             (set! singer-guiding-day (assoc-get data "singer-guiding-day" 0))
             (set! singer-guiding-told? (assoc-get data "singer-guiding-told?" #f))
             (set! west-friction-cleared? (assoc-get data "west-friction-cleared?" #f))
             (east-entry-clk 'load! (assoc-get data "east-entry" 0))
             (east-gallery-clk 'load! (assoc-get data "east-gallery" 0))
             (west-stairs-clk 'load! (assoc-get data "west-stairs" 0))
             (west-bridge-clk 'load! (assoc-get data "west-bridge" 0))
             (frank-search-clk 'load! (assoc-get data "frank-search" 0))
             (set! union-checked? (assoc-get data "union-checked?" #f))
             (set! bridge-identified? (assoc-get data "bridge-identified?" #f))
             (patience-clk 'load! (assoc-get data "patience" patience-max))
             (set! patience-day (assoc-get data "patience-day" 0))
             (set! manager-warning-level (assoc-get data "manager-warning-level" 0))
             (set! manager-phone-given? (assoc-get data "manager-phone-given?" #f))
             (if (and (>= manager-warning-level 0) (<= manager-warning-level 2))
                 #t
                 (error "三封信存档错误：经理提醒阶段非法"))
             (set! report-pending (assoc-get data "report-pending" ""))
             (if (or (equal? report-pending "")
                     (equal? report-pending "查明")
                     (equal? report-pending "巷子失败"))
                 #t
                 (error "三封信存档错误：待汇报小节非法"))
             (set! inquiry-day (assoc-get data "inquiry-day" 0))
             (set! inquiry-told? (assoc-get data "inquiry-told?" #f))
             (set! lesson-done? (assoc-get data "lesson-done?" #f))
             (set! lesson-day (assoc-get data "lesson-day" 0))
             ;; 旧版本曾存过三种没有实际入口的成功标签；它们都等同于谈成。
             (let ((saved-route (assoc-get data "lesson-route" "无")))
               (set! lesson-route
                 (cond
                   ((member? saved-route (list "谈" "交换" "逼" "难看")) "谈")
                   ((member? saved-route (list "无" "失败")) saved-route)
                   (else (error "三封信存档错误：巷子结算状态非法")))))
             (set! her-answer (assoc-get data "her-answer" "无"))
             (set! man-step (assoc-get data "man-step" 0))
             (set! door-step (assoc-get data "door-step" 0))
             (set! paper-step (assoc-get data "paper-step" 0))
             (set! theater-step (assoc-get data "theater-step" 0))
             (if (or (< theater-step 0) (> theater-step 3))
                 (error "三封信存档错误：剧院检查进度非法")
                 #t)
             (set! hole-vent (assoc-get data "hole-vent" 0))
             (set! hole-power (assoc-get data "hole-power" 0))
             (vent-repair-clk 'load!
               (assoc-get data "vent-repair" (if (= hole-vent 2) repair-steps 0)))
             (power-repair-clk 'load!
               (assoc-get data "power-repair" (if (= hole-power 2) repair-steps 0)))
             (if (and (= hole-vent 2) (not (vent-repair-clk 'full?)))
                 (error "三封信存档错误：通风口已修复但修复钟未满")
                 #t)
             (if (and (= hole-power 2) (not (power-repair-clk 'full?)))
                 (error "三封信存档错误：配电箱已修复但更换钟未满")
                 #t)
             (set! visitor-shown (assoc-get data "visitor-shown" 0))
             (set! envelope-thin? (assoc-get data "envelope-thin?" #f))
             (set! material-settled? (assoc-get data "material-settled?" #f))
             (set! settle-route (assoc-get data "settle-route" "无"))
             (set! lyon-fate (assoc-get data "lyon-fate" "无"))
             (set! settled-day (assoc-get data "settled-day" 0))
             ;; 老存档里没有这一项:那时首演钉在第 22 天,退回去正好是当时的行为。
             (set! premiere-day (assoc-get data "premiere-day" 22))
             (set! premiere-done? (assoc-get data "premiere-done?" #f))
             (set! premiere-pending? (assoc-get data "premiere-pending?" #f))
             (set! scene-flags (normalize-flags (assoc-get data "scene-flags" '())))
             (sync-globals!)
             (sync-blockers!)))
          (#t #f))))))

;; 新游戏自动执行的开场动作。客户端读这个全局去找节点，不写死章节内容。
(set-global! '开场动作 "有人敲门")
