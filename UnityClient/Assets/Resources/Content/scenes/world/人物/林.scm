;; 林——公司自动化项目的工程师，第一章。
;;
;; 他不是这场变化的发明者。港口自动化、旧城改造、砍人工，公司酝酿了很多年，
;; 有整支研发队伍和既定的城市计划。林做的是解掉其中一个卡了很久的技术问题：
;; 让只能在理想条件下运行的机器，开始能适应老码头这种乱、旧、随时出意外的现场。
;; 他没有制造这场改变，他让它突然变得可行。
;;
;; 他相信一件事：**能被解决的问题就值得解决**。机器有一种现实里很少见的诚实——
;; 齿轮不会因为你的身份改变咬合方式。所以失败对他不是羞辱，是"这里还有一个
;; 我没理解的问题"。他的盲点也在这儿：他习惯把"能不能工作"当成最重要的问题，
;; 还没真正想过"它工作以后，谁付代价"。
;;
;; 第一章三拍：
;;   0《失控的机械》 —— 第一次进机械区那天的遭遇。收场记住你是哪种人。
;;   1 准备期      —— 第一次走进工棚时他自己开口：他要做什么，哪天做。
;;                    然后工棚里就只剩活：校正轨道（累计）／造控制器零件（成色）。
;;                    穿插《那我们呢》：工人问了，尼尔接不接话。
;;   2《无人班次》  —— 测试之夜。
;;   3 尾声        —— 《十二个》《我没这么写》。
;;
;; DEMO 临时收口：第 2、3 拍尚未完成实际游玩测试，当前不向玩家开放。
;; 测试日只会触发“测试改期”的固定收尾，不进入《无人班次》，也不伪造测试结果。
;; 交锋、结算和尾声实现保留在下方，恢复前必须完成调试与整段试玩。
;;
;; 第一章只留两个布尔，第二章再读：
;;   技术负责人 —— 这一夜漂不漂亮，决定公司以后绕不绕得开他
;;   人文关怀   —— 他是否开始怀疑"能不能做到"是唯一的问题
;; 人文关怀有三个入口（工人那句、坚持停机、尾声那个数字），任意一个即可。
;; **绝不能只留"停机"一个入口**——停机会压低技术权威，两个布尔就变成互斥的，
;; "高权威 + 高关怀"那一格永远出不来，而那一格正是这条线以后要用的。

(define lin
  (let ()
    ;; ── 常量 ────────────────────────────────────────
    (define prep-days 3)          ; 他说三天后
    (define rail-max 3)
    (define parts-max 5)          ; 最多交五件；没凑齐也照常进入测试之夜
    (define part-int-max 2)       ; 一件零件：完整性两格
    (define part-flaw-max 2)      ; 瑕疵满两格当场报废

    ;; 工棚内按工作区域分锚点：同一件事的卡和钟必须挂在同一处，
    ;; 不同工作区则各自有明确的空间落点，避免所有节点叠在码头主锚点上。
    (define workshop-test-anchor "三号货栈工棚-控制台")
    (define workshop-rail-anchor "三号货栈工棚-旧轨道")
    (define workshop-part-anchor "三号货栈工棚-工作台")
    (define workshop-workers-anchor "三号货栈工棚-围栏")

    ;; ── 状态 ────────────────────────────────────────
    ;; 0 没遇见 / 1 工棚开了，还没去过 / 2 他已经开口，准备期 / 3 测试跑完 / 4 尾声或 Demo 收口
    (define stage 0)
    ;; 机械区不在开场那天开门。小节一那三天，玩家满脑子是一百金和勒索日；
    ;; 这时候码头上多一张陌生工程师的卡，他只会读成又一处跑腿点，
    ;; 而这一场恰恰是要他停下来看的。投信那一夜过去，城市才开始有别的事发生。
    (define zone-announced? #f)   ; 走进码头那天亲眼看见了机械区出事
    (define met-route "")         ; 断闸 / 拖走 / 撑住 / 自己修
    (define test-day 0)
    (define workers-asked? #f)    ; 《那我们呢》已经发生
    (define test-result "")       ; 完整自动通过 / 人工辅助完成 / 提前停机 / 出了事故
    (define manual-used 0)
    (define participated? #f)     ; 准备期真投入过
    (define authority? #f)        ; 技术负责人
    (define humane? #f)           ; 人文关怀
    ;; 旧版第二章曾用晚宴谈话开放公司试验场；新版改由第一章技术权威决定港务技术区权限。
    ;; invited? 暂留给既有晚宴状态与存档，已经不再控制地点开放。
    (define invited? #f)

    ;; 造零件：手上这一件 + 已经完成的
    (define part-int 0)
    (define part-flaw 0)
    (define part-bonus 0)         ; 上一件报废之后，他弄明白了；下一件 +1
    (define parts-good 0)
    (define parts-fair 0)
    (define last-flaw "")         ; 将就那几件里最后一条具名隐患

    (define rail-clk
      (make-clock "旧轨道校正" rail-max 'gauge
        (lambda (current max)
          (if (>= current max)
              "整条轨道量过一遍，偏差都标了。那一夜轨道那一处只是小麻烦。"
              "老码头的轨道是歪的。不校，测试那晚它会变成危机。"))))

    ;; 一件零件的两根钟。完整性满就当场结算，成色由瑕疵决定。
    (define part-int-clk
      (make-clock "手上这件·完整性" part-int-max 'gauge
        "两次做成就装得起来。装得起来不等于装得好。"))

    (define part-flaw-clk
      (make-clock "手上这件·瑕疵" part-flaw-max 'gauge
        (lambda (current max)
          (cond
            ((>= current max) "这一件废了。")
            ((> current 0) "带一条毛病。能用，夜里会咬你一口。")
            (else "到现在为止，干净的。")))))

    ;; ── 卷宗 ────────────────────────────────────────
    (define journal (make-journal))

    ;; 这条线只在他自己开口以后才进卷宗：机械区那一晚是一次偶遇，
    ;; 偶遇不该占一条线；到他说出"三天后再试一次"，它才成为一件你要不要管的事。
    (define (dossier-entry)
      (cond
        ((= stage 2)
         (list (dossier "三号货栈的那台机器"
                 :kind '人物
                 :status '进行中
                 :now (if (test-due?)
                          "正式测试原定今晚进行。去三号货栈工棚"
                          (string-append "测试之夜在第 " (number->string test-day)
                                         " 天。在那之前把控制器凑齐、把轨道校完"))
                 :where "三号货栈工棚"
                 :clocks (append
                           (list (rail-clk 'render-data))
                           (if (controller-done?)
                               '()
                               (list (part-int-clk 'render-data)
                                     (part-flaw-clk 'render-data))))
                 :log (journal 'render-data))))
        ((= stage 1)
         (list (dossier "三号货栈的那台机器"
                 :kind '人物
                 :status '进行中
                 :now "去码头尽头那间工棚，看看他到底在做什么"
                 :where "三号货栈工棚"
                 :log (journal 'render-data))))
        ((>= stage 3)
         (list (dossier "三号货栈的那台机器"
                 :kind '人物
                 :status (if (>= stage 4) '了结 '进行中)
                 :now (if (>= stage 4)
                          (if (equal? test-result "")
                              "正式测试改期了。林在等公司的新排期"
                              "那一夜过去了。")
                          "去工棚看看那一夜之后剩下什么")
                 :where (if (>= stage 4) "" "三号货栈工棚")
                 :log (journal 'render-data))))
        (else '())))

    ;; ── 判定 ────────────────────────────────────────
    (define (zone-open?) (>= (three-letters 'story-stage) 2))
    (define (workshop-open?) (>= stage 1))
    (define (known?) (>= stage 1))
    (define (prep-open?) (and (<= 1 stage) (<= stage 2)))
    (define (controller-done?) (>= (+ parts-good parts-fair) parts-max))
    ;; 只开一天：world-day 走过去，交锋就没了。
    (define (test-due?) (and (prep-open?) (= world-day test-day)))

    (define (controller-grade)
      (cond
        ;; 准备结果只改变交锋题面，不是交锋入场券。
        ((not (controller-done?)) "凑合")
        ((= parts-fair 0) "上好")
        ((<= parts-fair 1) "将就")
        (else "凑合")))

    (define (days-left) (- test-day world-day))

    ;; ── 遭遇零 ──────────────────────────────────────
    ;; 走进码头那一刻就撞上事故。它不是一张可以留到明天的任务卡：入场 dialogue
    ;; 播完立即进入交锋；交锋结算后才开放工棚与建设阶段。
    (define (arrival-runaway)
      (arrival "机械区出事了"
        (lambda ()
          (set! zone-announced? #t)
          (play-dialogue!
            (line "世界" "码头尽头那排卷帘门今天是开着的。你从这儿走过很多次，从来没见它开过。")
            (line "世界" "里面有东西在响。不是机器干活的那种响——一下，停一下，再一下，每次都撞在同一个地方。")
            (line "世界" "堆场上的人都停下来往那边看，没有人过去。")
            (line "工人" "别管。那玩意儿前两天就这样了。")
            (line "工人" "……不过今天里头有人。")
            (line "世界" "他说完就转身接着扛他的货。"))
          (start-encounter "失控的机械" on-runaway-result))))

    (define (on-runaway-result result)
      (if (member? result (list '断闸 '拖走 '撑住 '自己修 '倒下))
          #t
          (error "林：《失控的机械》返回了未登记的收场"))
      (set! stage 1)
      ;; 倒下**只换文案，不换状态**：它就记成拖走——那一晚的结局是一样的
      ;; （机器散了，人从底下被拖出来），区别只在被拖出来的是谁。
      ;; 不给它开第五条路线：met-route 是"林记住你是哪种人"，倒下不构成第五种人。
      (set! met-route (symbol->string-safe (if (equal? result '倒下) '拖走 result)))
      (cond
        ((equal? result '撑住)
         (play-dialogue!
           (line "林" "轨道。是轨道。")
           (line "尼尔" "有什么区别？")
           (line "林" "当然有区别。")
           (line "林" "控制器坏了是我算错了。轨道咬住是它没见过这种地方。")
           (line "世界" "他这才想起来看你一眼：你手怎么样？")
           (line "尼尔" "还在。")))
        ((equal? result '自己修)
         (play-dialogue!
           (line "林" "你怎么知道是那一节？")
           (line "尼尔" "它每次都在同一个地方咬。")
           (line "林" "……对。")
           (line "世界" "他看你的眼神变了——不是感激，是碰到同类的那种。")))
        ((equal? result '断闸)
         (play-dialogue!
           (line "林" "我知道你救了我。")
           (line "尼尔" "听着不像谢。")
           (line "林" "它撞了十七次，每次都在同一个地方。第十八次我就知道为什么了。")
           (line "世界" "他把手套摘下来，看着那堆停住的金属。")))
        ((equal? result '倒下)
         (play-dialogue!
           (line "世界" "支架砸下来的时候，他已经从底下爬出去了。你没有。")
           (line "林" "别动。已经有人去叫车了。")
           (line "尼尔" "机器呢。")
           (line "林" "散了。")
           (line "世界" "他蹲在你旁边，两只手都在抖，但说话还是那个调子。")
           (line "林" "……它撞了十七次，每次都在同一个地方。")
           (line "尼尔" "现在说这个。")
           (line "林" "不然说什么。")))
        (else
         (play-dialogue!
           (line "林" "再给我十秒就够了。")
           (line "尼尔" "再给你十秒你就在下面。")
           (line "林" "……也是。")
           (line "世界" "他坐在门口的水泥台上，很久没说话。"))))
      (set! test-day (+ world-day prep-days))
      (journal 'add!
        (cond
          ((equal? result '撑住)   "机械区那一晚：你替他顶住那节支架，他看明白了——是轨道，不是控制器。")
          ((equal? result '自己修) "机械区那一晚：你先看出是哪一节在咬。他看你的眼神变了。")
          ((equal? result '断闸)   "机械区那一晚：你拉了总闸。他说明天再说。")
          ((equal? result '倒下)   "机械区那一晚：臂架砸了下来，是他把你从底下拖出去的。")
          (#t "机械区那一晚：你把他从轨道下面拖了出来。他一直记得他差一点就看明白了。")))
      (sync-globals!)
      (spotlight! "三号货栈工棚"
        "机械区不是废墟。那一堆歪掉的金属本来应该自己把货搬完，而他还没打算收手。"))

    ;; ── 走进工棚那一刻 ──────────────────────────────
    ;; 不做成一张「和林谈谈」的卡：他没有在等你来问，他只是正好在干活，
    ;; 你推门进去，他抬头就说。说完工棚里就只剩活了。
    (define (arrival-briefing)
      (arrival "他要再试一次"
        (lambda ()
          (set! stage 2)
          (journal 'add! "他说他要再试一次，三天后，用三号货栈那条旧轨道。")
          (sync-globals!)
          (play-dialogue!
            (line "世界" "工棚的门没锁。他背对着门在拧一个底座，头也没回。")
            (line "林" "机器只认它见过的东西。老码头它一样也没见过。")
            (line "林" "轨道是歪的，货是乱的，人到处走。所以它一进来就废。")
            (line "林" "我做的就是让它能认没见过的东西。")
            (line "世界" "他这才转过身，手上还捏着扳手。")
            (line "林" (string-append (number->string (days-left))
                                      "天后他们让我再试一次。一整个夜班，四批货，没有人上手。"))
            (line "林" "跑完就不是我的事了。这是最后一次。")
            (line "世界" "他指了指门外那条轨道，把一叠图纸推到台子这一头。")
            (line "林" "轨道得先量出来，控制器的零件我一个人也做不完。")
            (line "林" "这些活公司出钱。你要来就来。"))
          (spotlight! "自动化测试"
            (string-append "还有 " (number->string (days-left)) " 天。那一夜在三号货栈，只有那一夜。")))))

    (define (arrivals-at location)
      (cond
        ((equal? location "三号货栈工棚")
         (if (= stage 1) (list (arrival-briefing)) '()))
        ((equal? location "码头")
         (if (and (= stage 0) (zone-open?) (not zone-announced?))
             (list (arrival-runaway))
             '()))
        (else '())))

    ;; ── 第二拍：准备期 ──────────────────────────────
    ;; 三样都是商业圈里的带薪临时活：林拿的是公司经费，他签得下来。
    ;; 报酬不是玩家来的理由，报酬是消掉"我还得挣房租"这个不来的理由。
    ;; 顺带一层这一章不点破的讽刺：你帮这个理想主义者干活，涨的是公司的脸熟。
    (define (node-rail)
      (关系工作 "校正旧轨道" "商业圈" '低 'sharpness
        (outcome "量到尽头"
          (lambda () (add-item! "金钱" 8) (mark-participated!) (rail-clk 'tick!)))
        (outcome "标了几处"
          (lambda () (add-item! "金钱" 6) (mark-participated!) (rail-clk 'tick!)))
        (outcome "白跑一趟"
          (lambda () (add-item! "金钱" 4) (spend-composure! 1)))
        "累计式；每一格都让测试夜的轨道那一处轻一点"
        :anchor workshop-rail-anchor
        :clocks (list (rail-clk 'render-data))))

    (define (mark-participated!)
      (set! participated? #t))

    ;; 造零件：完整性满就当场结算，瑕疵满两格当场报废。
    ;; 每一格瑕疵都要当场命名——那一夜出故障的就是它。
    (define (flaw-text n)
      (cond
        ((= n 1) "接线是临时搭的")
        ((= n 2) "限位开关没有备份")
        (else "散热片是从另一台上拆的")))

    (define (add-flaw! n)
      (part-flaw-clk 'advance! n)
      (set! part-flaw (part-flaw-clk 'current))
      (result-note! (flaw-text (part-flaw-clk 'current)))
      (settle-part!))

    (define (add-int!)
      (part-int-clk 'tick!)
      (set! part-int (part-int-clk 'current))
      (settle-part!))

    (define (reset-part!)
      (part-int-clk 'reset!)
      (part-flaw-clk 'reset!)
      (set! part-int 0)
      (set! part-flaw 0))

    (define (settle-part!)
      (cond
        ((part-flaw-clk 'full?)
         ;; 报废不是惩罚，是他最像他自己的时刻：失败在告诉你还有一个你没理解的问题。
         ;; 料是公司的，工时是你的——这一件不结钱。
         (set! part-bonus 1)
         (reset-part!)
         (play-banter! (line "林" "别扔。我知道为什么了。"))
         (result-note! "这一件报废了；下一件更有把握"))
        ((part-int-clk 'full?)
         (if (> (part-flaw-clk 'current) 0)
             (begin
               (set! parts-fair (+ parts-fair 1))
               (set! last-flaw (flaw-text (part-flaw-clk 'current)))
               (add-item! "金钱" 14)
               (result-note! "交了一件将就的：能装，带着毛病"))
             (begin
               (set! parts-good (+ parts-good 1))
               (add-item! "金钱" 20)
               (grant-work-relation! "商业圈")
               (result-note! "交了一件上好的")))
         (set! part-bonus 0)
         (reset-part!))
        (else #f)))

    ;; 和校正轨道一样是商业圈里的带薪临时活，但结账方式不同：轨道按班算，
    ;; 零件按件算——一件要两次做成才装得起来，钱在 settle-part! 里一次付清，
    ;; 报废的那件一分没有。也因此不能用 关系工作 包装（它按次给钱，还不接判定修正）。
    (define (node-part)
      (node "做控制器零件"
        :anchor workshop-part-anchor
        :tags (list "工作" "低风险")
        :subtitle (string-append "交件才结钱；已交 "
                                 (number->string (+ parts-good parts-fair))
                                 "/" (number->string parts-max)
                                 "，其中将就 " (number->string parts-fair) " 件")
        :clocks (list (part-int-clk 'render-data)
                      (part-flaw-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (lambda ()
            (append (关系难度修正 "商业圈")
                    (if (> part-bonus 0) (list (modifier 1 "他弄明白了")) '())))
          (outcome "毁了一块料"
            (lambda () (mark-participated!) (add-flaw! 2)))
          (outcome "装上了，但是凑合"
            (lambda () (mark-participated!) (add-flaw! 1) (add-int!)))
          (outcome "严丝合缝"
            (lambda () (mark-participated!) (add-int!))))))

    (define (note-countdown)
      (node "标注：自动化测试"
        :anchor workshop-test-anchor
        :resolve (note "自动化测试"
          (string-append "还有 " (number->string (days-left)) " 天。"))))

    ;; ── 《那我们呢》 ────────────────────────────────
    ;; 人文关怀的第一个入口，也是测试之前唯一的一个。
    (define (node-workers)
      (node "围栏外的两个工人"
        :anchor workshop-workers-anchor
        :subtitle "他们在看那台机器"
        :resolve (instant
          (lambda ()
            (set! workers-asked? #t)
            (set! humane? #t)
            (play-dialogue!
              (line "工人甲" "这东西真能自己干？")
              (line "林" "理论上。")
              (line "工人乙" "那我们干什么？")
              (line "林" "操作、维护、调度。会给你们转岗。")
              (line "工人乙" "谁教？")
              (line "世界" "林停了一下。")
              (line "林" "公司会安排。")
              (line "尼尔" "转岗要几个名额？")
              (line "林" "计划里有。")
              (line "尼尔" "几个。")
              (line "世界" "他张嘴，又停住。")
              (line "林" "……我不知道。这不是我这一块的事。")
              (line "尼尔" "那是谁那一块的事？")
              (line "世界" "他没有回答。他回去接着调那台机器，但慢了半拍。"))
            (sync-globals!)))))

    ;; ── 第三拍：测试之夜 ────────────────────────────
    ;; TODO(DEMO): 以下《无人班次》及尾声保留但暂不接入世界树。
    ;; 恢复时不能只把 node-test 换回去：还要逐路验证交锋结算、
    ;; 错过测试日的路径、技术负责人／人文关怀写回，以及两张尾声卡。
    (define (node-test)
      (node "今晚：无人班次"
        :anchor workshop-test-anchor
        :tags (list "交锋")
        :subtitle "四批货，没有人上手。今晚跑完就没有下一次了"
        :resolve (instant (lambda ()
          (set-global! '准备-轨道 (rail-clk 'current))
          (set-global! '控制器成色 (controller-grade))
          (set-global! '控制器隐患 (if (equal? last-flaw "") "接线是临时搭的" last-flaw))
          (start-encounter "无人班次" on-test-result)))))

    (define (on-test-result result)
      (if (and (list? result) (= (length result) 2))
          #t
          (error "林：《无人班次》应回传 (list 收场 人工次数)"))
      (set! stage 3)
      (set! test-result (symbol->string-safe (car result)))
      (set! manual-used (cadr result))
      (journal 'add! (string-append "测试之夜：" test-result "。"))
      ;; 技术权威：这一夜漂不漂亮，而且你真的出过力。
      (if (and participated?
               (member? test-result (list "完整自动通过" "人工辅助完成"))
               (not (equal? test-result "出了事故")))
          (set! authority? (equal? test-result "完整自动通过"))
          #f)
      ;; 停机是人文关怀的第二个入口。它会压低技术权威——所以另外两个入口必须留着。
      (if (equal? test-result "提前停机") (set! humane? #t) #f)
      (sync-globals!)
      (aftermath!)
      ;; 测试之夜是林这条线的结算：出事故也算经历完。没去的那条路
      ;; （「没有你他们也跑了」）不发——玩家根本没进过这一段。
      ;; 发点放在尾声之后，否则通知被 aftermath! 的对白与 spotlight 盖掉。
      (complete-section!))

    ;; Demo 版在测试日停在这里。结果留空，避免后续内容把
    ;; “没有跑过”误读成成功、失败或人工辅助完成。
    (define (close-demo-line!)
      (set! stage 4)
      (set! test-result "")
      (set! manual-used 0)
      (set! authority? #f)
      (journal 'add! "公司把正式测试并入下一轮项目审查，新排期还没下来。")
      (sync-globals!))

    (define (node-demo-close)
      (node "测试改期"
        :anchor workshop-test-anchor
        :subtitle "公司取消了今晚的正式测试"
        :resolve (instant
          (lambda ()
            (close-demo-line!)
            (play-dialogue!
              (line "世界" "那台机器罩上了帆布，控制台的电源断着。")
              (line "林" "公司把测试并进下一轮项目审查。今晚不跑了。")
              (line "尼尔" "什么时候？")
              (line "林" "新排期还没下来。")
              (line "世界" "他把运行表折好，压在记录本下面。"))
            (spotlight! "等待新排期"
              "机器没有失败，也没有通过。三号货栈的正式测试留到了下一次。")
            (complete-section!)
            (result-note! "林的测试留到下一次")))))

    ;; ── 尾声一：《十二个》 ──────────────────────────
    ;; 那个数字不写死，由这一夜算出来：自动跑完的批次 × 一批原本要几个人。
    (define (per-batch-hands) 4)

    (define (auto-batches)
      (max 0 (- 4 manual-used)))

    (define (hands-replaced)
      (* (auto-batches) (per-batch-hands)))

    (define (aftermath!)
      (cond
        ((equal? test-result "出了事故")
         (play-dialogue!
           (line "世界" "救护车走了以后，机械区安静下来。")
           (line "林" "是配重。我算的配重。")
           (line "尼尔" "你已经说了三遍了。")
           (line "林" "因为我算的配重。")
           (line "世界" "他一整夜没有离开控制台。"))
         (spotlight! "测试记录"
           "公司拿走了运行日志。项目照常推进——只是这一份记录里多了一行伤情。"))
        ((equal? test-result "提前停机")
         (play-dialogue!
           (line "林" "我们本来还差一批。")
           (line "尼尔" "那一批底下站着一个人。")
           (line "林" "它会停的。")
           (line "尼尔" "它没有必须停的理由。")
           (line "世界" "他想反驳，最后只是把记录本合上了。")
           (line "林" "……这次不算数了。")
           (line "世界" "他说这句话的时候，声音里没有他自己以为的那么确定。"))
         (spotlight! "测试记录" "夜班没有跑完。公司会再排一次——下一次不一定叫你。"))
        (else
         (play-dialogue!
           (line "世界" "最后一个货柜落稳。臂收回原位，机械区安静下来。")
           (line "林" "你看见了吗。")
           (line "尼尔" "看见了。")
           (line "世界" "他在记录本上写字，写得很快，笑得像个刚考完的人。"))
         (play-dialogue!
           (line "工人" "一晚上多少货？")
           (line "林" (string-append "四批。" (number->string (auto-batches)) "批是它自己跑的。"))
           (line "工人" "要是我们干呢？")
           (line "林" (string-append "大概" (number->string (hands-replaced)) "个人。"))
           (line "世界" "工人点点头。")
           (line "工人" "挺厉害。")
           (line "世界" "然后他走掉了。林看着他的背影，第一次没有立刻接着记数据。"))
         (spotlight! "他做到了"
           "机器跑完了一个夜班。公司拿到了它要的东西，林拿到了他要的答案。"))))

    ;; 尾声一的第三个入口：那个数字说出口之后，你接不接话。
    (define (node-number)
      (node "问他那个数字"
        :anchor workshop-test-anchor
        :resolve (instant
          (lambda ()
            (set! humane? #t)
            (play-dialogue!
              (line "尼尔" (string-append (number->string (hands-replaced)) "个人。他们去哪儿。"))
              (line "林" "转岗。")
              (line "尼尔" "你信这个词吗。")
              (line "世界" "他看着运行记录，没有翻页。")
              (line "林" "我信这台机器。")
              (line "林" "别的我没算过。")
              (line "世界" "他把记录本合上，夹在腋下。这一晚他没有再打开它。"))
            (set! stage 4)
            (sync-globals!)
            (result-note! "他把那个词说出口了，自己也听见了")))))

    ;; ── 尾声二：《我没这么写》 ──────────────────────
    ;; 帮没帮过都会来。帮过，他念的是你那一夜的数字；没帮过，是公司团队那次的。
    ;; 这一拍就是为了让玩家看见：你改变的是林，不是这件事会不会发生。
    (define (node-bulletin)
      (node "他手里那张宣传单"
        :anchor workshop-part-anchor          ; 他把它摊在工作台上
        :resolve (instant
          (lambda ()
            (set! stage 4)
            (play-dialogue!
              (line "世界" "他把一张公司的宣传单摊在工作台上，边角还卷着。")
              (line "林" "新型自动装卸系统，预计减少百分之六十人工需求。")
              (line "尼尔" "数字是真的吗？")
              (line "世界" "他没有马上回答。")
              (line "林" "……是真的。")
              (line "林" "我报告里没有这句话。")
              (line "尼尔" "他们改了？")
              (line "林" "他们没改。他们只是把它写成了另一件事。"))
            (sync-globals!)
            (result-note! "事实还是他的，意思已经不是了")))))

    ;; ── 组装 ────────────────────────────────────────
    (define (workshop-nodes)
      (cond
        ;; TODO(DEMO): 《无人班次》未试玩，暂以固定收尾取代 node-test。
        ((test-due?) (list (node-demo-close)))
        ((prep-open?)
         (append
           (list (note-countdown))
           (if (< (rail-clk 'current) rail-max) (list (node-rail)) '())
           (if (controller-done?) '() (list (node-part)))
           (if workers-asked? '() (list (node-workers)))))
        ((= stage 3)
         (append
           (if (member? test-result (list "完整自动通过" "人工辅助完成"))
               (list (node-number))
               '())
           (list (node-bulletin))))
        (else '())))

    ;; 遭遇零由码头 arrival 直接进入交锋，不在地点里留下可推迟的卡。
    (define (dock-nodes)
      '())

    (define (nodes-at location)
      (cond
        ((equal? location "三号货栈工棚") (workshop-nodes))
        ((equal? location "码头") (dock-nodes))
        (else '())))

    (define (symbol->string-safe v)
      (cond
        ((equal? v '断闸) "断闸")
        ((equal? v '拖走) "拖走")
        ((equal? v '撑住) "撑住")
        ((equal? v '自己修) "自己修")
        ((equal? v '完整自动通过) "完整自动通过")
        ((equal? v '人工辅助完成) "人工辅助完成")
        ((equal? v '提前停机) "提前停机")
        ((equal? v '出了事故) "出了事故")
        (else (error "林：无法登记的收场标签"))))

    (define (sync-globals!)
      (set-global! '林-技术负责人 authority?)
      (set-global! '林-人文关怀 humane?)
      (set-global! '林-测试结果 test-result))

    (define (debug-finish-chapter1! core?)
      (set! stage 4)
      (set! zone-announced? #t)
      (set! test-day 0)
      (set! test-result (if core? "完整自动通过" ""))
      (set! manual-used 0)
      (set! participated? core?)
      (set! authority? core?)
      (set! humane? #f)
      (set! invited? #f)
      (sync-globals!))

    ;; Demo 不再广播“测试之夜”，否则会向玩家许诺一场已隐藏的交锋。
    ;; 当天没去工棚的玩家在回合结束时收到改期消息；没有亲自
    ;; 走完收尾卡，就不发小节成长。
    (define-turn-rule "正式测试改期"
      (lambda () (and (prep-open?) (= world-day test-day)))
      (lambda ()
        (close-demo-line!)
        (notify! "三号货栈的正式测试改期了。公司把它并进下一轮项目审查，新排期还没下来。")))

    ;; 兼容在测试日之后才读入的旧进度：同样只收口，不补造一场测试。
    (define-turn-rule "错过改期消息"
      (lambda () (and (prep-open?) (> world-day test-day)))
      (lambda ()
        (close-demo-line!)
        (notify! "三号货栈的正式测试已经改期，新排期还没下来。")))

    (sync-globals!)

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'dossier) (dossier-entry))
          ((equal? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((equal? msg 'workshop-open?) (workshop-open?))
          ((equal? msg 'known?) (known?))
          ((equal? msg 'authority?) authority?)
          ((equal? msg 'humane?) humane?)
          ((equal? msg 'on-banquet-talk!) (set! invited? #t))
          ;; 第二章接着往「人文关怀」里写：他有没有真的看见机器成功之后落在谁身上。
          ;; 第一章已经有三个入口（工人那句、坚持停机、尾声那个数字），这是第四个，
          ;; 读的还是同一个事实——第三章问的是同一件事，不该有两份答案。
          ((equal? msg 'on-saw-cost!) (set! humane? #t))
          ((equal? msg 'invited?) invited?)
          ((equal? msg 'test-result) test-result)
          ;; 跳章调试：只构造一份安静、合法的第一章结束态。
          ;; 不补演机械区事故，也不伪造技术成功或人文关怀。
          ((equal? msg 'debug-finish-chapter1!)
           (debug-finish-chapter1! #f))
          ((equal? msg 'debug-finish-chapter1-core!)
           (debug-finish-chapter1! #t))
          ((equal? msg 'save)
           (list (list "stage" stage)
                 (list "journal" (journal 'save))
                 (list "zone-announced" (if zone-announced? 1 0))
                 (list "met-route" met-route)
                 (list "test-day" test-day)
                 (list "workers-asked" (if workers-asked? 1 0))
                 (list "test-result" test-result)
                 (list "manual-used" manual-used)
                 (list "participated" (if participated? 1 0))
                 (list "authority" (if authority? 1 0))
                 (list "humane" (if humane? 1 0))
                 (list "invited" (if invited? 1 0))
                 (list "rail" (rail-clk 'save))
                 (list "part-int" (part-int-clk 'save))
                 (list "part-flaw" (part-flaw-clk 'save))
                 (list "part-bonus" part-bonus)
                 (list "parts-good" parts-good)
                 (list "parts-fair" parts-fair)
                 (list "last-flaw" last-flaw)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! stage (assoc-get data "stage" 0))
             (journal 'load! (assoc-get data "journal" '()))
             (set! zone-announced? (= (assoc-get data "zone-announced" 0) 1))
             (set! met-route (assoc-get data "met-route" ""))
             (set! test-day (assoc-get data "test-day" 0))
             (set! workers-asked? (= (assoc-get data "workers-asked" 0) 1))
             (set! test-result (assoc-get data "test-result" ""))
             (set! manual-used (assoc-get data "manual-used" 0))
             (set! participated? (= (assoc-get data "participated" 0) 1))
             (set! authority? (= (assoc-get data "authority" 0) 1))
             (set! humane? (= (assoc-get data "humane" 0) 1))
             (set! invited? (= (assoc-get data "invited" 0) 1))
             (rail-clk 'load! (assoc-get data "rail" 0))
             (part-int-clk 'load! (assoc-get data "part-int" 0))
             (part-flaw-clk 'load! (assoc-get data "part-flaw" 0))
             (set! part-int (part-int-clk 'current))
             (set! part-flaw (part-flaw-clk 'current))
             (set! part-bonus (assoc-get data "part-bonus" 0))
             (set! parts-good (assoc-get data "parts-good" 0))
             (set! parts-fair (assoc-get data "parts-fair" 0))
             (set! last-flaw (assoc-get data "last-flaw" ""))
             (sync-globals!)))
          (else #f))))))
