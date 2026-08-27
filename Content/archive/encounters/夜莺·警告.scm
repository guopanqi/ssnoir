;; scenes/encounters/夜莺·警告.scm - 夜莺委托线·第一场「照面」
;; 主结构:话题拔河。你顶着"替外地人找门路的本地掮客"的假身份和收账人喝酒对谈。
;;   对话的基本单位是「话题」——一根 8 位的双头条(0..7):
;;   0 端 = 他收口,7 端 = 他松口。一次只开一个话题。
;;   引擎 Clock 是单向的,所以指针用 'set! 移动,备注写明两端含义与当前局势。
;; 对外契约:确认身份后才允许收手并 success;被识破为 fail(身份已满则算狼狈的 success)。
;; 城市输入（只读）:
;;   夜莺主动上门 - #t 主动上门,她不在场;#f 他找上门,她在场(开局多一个疑问且疑心 +1)。
;; 城市输出:
;;   收账人识破过你 - 被打出去时置 #t,第三场「了断」据此收紧当面逼账。

(define active-visit?
  (let ((v (get-global '夜莺主动上门))) (if v v #f)))
(define she-here? (not active-visit?))

;; ============================================================
;; 全场时钟
;; ============================================================

(define identity-clk
  (make-clock "他的身份" 3 'gauge
              "主目标。成败只看这条:满后解锁「见好就收」。只有话题谈到他松口才会推进。"))

(define money-clk
  (make-clock "再多拿一点" 4 'gauge
              "副目标。满格一次性取得「欠账凭据」与 20 金;错过不补。每次只进一格——两个钱话题各一格,其余要靠空间线一件一件摸。"))

(define suspicion-clk
  (make-clock "他的疑心" 6 'gauge
              "唯一的危险钟,不会自己走。只有三件事推它,每次都只 +1:话题谈崩、疑问露馅、段二的坏结果。到 3 他改成每回合问你一句;满了他会撕破脸把你打出去——而且从此把她一起重新算账。"))

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

(define (suspicion+ n) (clock-tick-n! suspicion-clk n))
(define (suspicion- n)
  (suspicion-clk 'set! (max 0 (- (suspicion-clk 'current) n))))

;; ============================================================
;; 段落
;; ============================================================

(define stage 1)   ; 1 应付 / 2 上心

;; 段二他更信你,机制上就该更好办,不该更贵:
;; 段一的话题开在 2(僵持),段二开在 3——他已经朝你这边偏了。
;; 于是段二的话题更省骰子,而不是"换套衣服再加钱"。
;; 玩家走到这里冷静通常已经在掉了,压力本来就在,不必再加码。
(define (topic-open-pos) (if (= stage 2) 3 2))
(define topic-max 7)

;; ============================================================
;; 话题的数据结构
;; ============================================================
;; (id 短名 全名 线 报偿 开场白 占上风 僵持 起戒心 松口 谈崩)
;; 线: 'identity 身份线 / 'money 钱线 / 'question 他的疑问
;; 报偿: 松口时推进的格数(疑问用 0,另有专门结算)

(define (t-id t)   (list-ref t 0))
(define (t-tag t)  (list-ref t 1))
(define (t-name t) (list-ref t 2))
(define (t-line t) (list-ref t 3))
(define (t-gain t) (list-ref t 4))
(define (t-open t) (list-ref t 5))
(define (t-up t)   (list-ref t 6))
(define (t-mid t)  (list-ref t 7))
(define (t-down t) (list-ref t 8))
(define (t-win t)  (list-ref t 9))
(define (t-lose t) (list-ref t 10))

;; ---- 段一「应付」:他还当你是掮客 ----

(define topic-fake-name
  (list '假名字 "假名字" "拿假名字试他" 'identity 1
    "你报了一个歌厅管事的名字，说是共同的熟人。他把杯子放下了。"
    "他纠正了你半个字。那半个字比你编的整句话都值钱。"
    "他没接话，只是把你的名字在嘴里念了一遍。"
    "他笑了一下。那不是被逗乐的笑。"
    "他终于说出了那个人在歌厅里管什么。你记住了。"
    "他不再顺着这个名字往下说。这条路走死了。"))

(define topic-accent
  (list '衣着 "衣着" "顺着衣着口音问" 'identity 1
    "你说他这身料子不像本地铺子出的。他低头看了看袖口。"
    "他开始讲那座城的天气，讲得比必要的多。"
    "他只承认自己不是本地人。仅此而已。"
    "他把袖子往下拉了拉，盖住了那颗扣子。"
    "红泥、袖扣、口音——三样对上同一座城。他自己说出了城名。"
    "他把话岔到了酒上。衣服的事，不谈了。"))

(define topic-toll
  (list '带路钱 "带路钱" "索要一笔带路钱" 'money 1
    "你说本地的规矩是先给带路钱。他挑了挑眉毛。"
    "他开始问这笔钱该怎么算、给谁。"
    "他没说给，也没说不给。"
    "他把账袋往自己那边挪了半寸。"
    "他数出一叠钱推过来，还多问了一句你住哪儿。"
    "他把账袋按住了。'外地人的规矩，我懂得比你多。'"))

;; ---- 段二「上心」:他放下了杯子 ----

(define topic-hall
  (list '歌厅 "歌厅" "拿歌厅名字试他" 'identity 1
    "这一次你报的是真名字。他没有立刻回话。"
    "他开始说那家歌厅的台子有多大、灯有多亮。"
    "他既没认，也没否认。"
    "他把手从桌上收了回去。"
    "他承认了。老板、歌厅、还有他自己站在哪一层。"
    "他站起来又坐下。'这个名字，你从哪儿听来的？'"))

(define topic-ransom
  (list '赎身 "赎身" "把赎身钱摆上桌" 'money 1
    "你把那三个字说出来。桌上安静了一下。"
    "他开始算那笔数目——算得很熟，像算过很多遍。"
    "他盯着你，等你先说下一句。"
    "他的手指停在杯沿上，不动了。"
    "他把整笔账都摊开了：第七年、数目、谁在等这笔钱。"
    "'这三个字，不是你这种人该问的。'"))

(define topic-above
  (list '上头 "上头" "问他上头还有谁" 'identity 1
    "你问他这趟是谁派的。他没有立刻否认有人派他。"
    "他开始抱怨:上头一句话，他就得跨一座城。"
    "他把杯子转了半圈，没说话。"
    "'跑腿的问跑腿的，问不出东西。'"
    "他说出了老板的排场，也说出了自己在那排场里站哪儿。"
    "'上头是谁，跟你没关系。'他把话收住了。"))

;; 身份需要 3 格,每个身份话题只给 1 格。段一两个 + 段二两个 = 4 格,
;; 留一格余量:段二那条路上任何一个话题谈崩,都还有另一条能走完。
;; 只有一个来源的话,那一格就是全场唯一的独木桥。
(define (pool-a) (list topic-fake-name topic-accent topic-toll))
(define (pool-b) (list topic-hall topic-above topic-ransom))
(define (current-pool) (if (= stage 1) (pool-a) (pool-b)))

;; ---- 他的疑问:开在你劣势位的话题 ----

(define question-table
  (list
    (list '掌柜 "掌柜的" "「你说你常来，掌柜的怎么不认得你？」" 'question 0
      "他往吧台那边扬了扬下巴。'你说你常来。'"
      "掌柜的正忙着擦杯子，没往这边看。你的说法暂时立得住。"
      "他等着你把话说完。"
      "掌柜的抬头看了你一眼，又低下头去。那一眼太长了。"
      "他自己替你圆了：'这条街上认脸的人本来也不多。'"
      "'我在这儿喝了两晚上，掌柜的记得我。'他把杯子推开了。")
    (list '口音 "口音" "「你的口音，不是这条街的。」" 'question 0
      "他忽然打断你，学了一句你刚才的尾音。"
      "你把话头引到别处，他跟着走了。"
      "他还在等一个解释。"
      "他又学了一遍那个尾音，这回没笑。"
      "'口音这东西，跟着饭碗走。'他点了点头，放过了。"
      "'你不是这条街的。'他说的是陈述句。")
    (list '替谁 "替谁" "「你到底替谁问？」" 'question 0
      "他把两只手都放到了桌上。"
      "你报了一个不痛不痒的名号，他姑且信了。"
      "他没有移开视线。"
      "他往后靠了靠，像是在重新看你这个人。"
      "'外地人。'他自己接了下去，'这条街上做这行的，也就那么几个。'"
      "'那就是没有人。'他把账本合上了。")
    (list '她的眼神 "眼神" "「她刚才为什么看你？」" 'question 0
      "他没有回头，只是问。她的手在杯子上收紧了一下。"
      "你说她大概是把你认成了别人。他哼了一声，接受了。"
      "他等着。她也等着。"
      "他这才回头看了她一眼，又转回来看你。"
      "'这地方的女人看谁都那个眼神。'他自己找了台阶。"
      "他看着她，又看着你。'你们两个，认识多久了？'")))

;; ============================================================
;; 拔河状态
;; ============================================================
;; 一次只开一个主动话题;他的疑问另占一个位置,同一时刻最多挂一个。

(define topic #f)        ; 当前主动话题
(define topic-pos 0)
(define question #f)     ; 当前疑问
(define question-pos 0)

(define done-topics '())    ; 已入账,不可重开
(define soured-topics '())  ; 谈崩或收手过,重开带 −1

(define (member-id? id lst) (member? id lst))
(define (done? t) (member-id? (t-id t) done-topics))
(define (soured? t) (member-id? (t-id t) soured-topics))

(define (sour! t)
  (if (soured? t) #f (set! soured-topics (cons (t-id t) soured-topics))))

;; 重开一个说崩过的话题,他听过这话:可见的 −1,而不是直接涨疑心。
;; 惩罚重复用难度,契约上永远有路,骰运差不会把局锁死。
(define (topic-mods t)
  (lambda () (if (soured? t) (list (modifier -1 "他听过这话")) '())))

(define (zone-line t pos)
  (cond ((>= pos 5) (t-up t))
        ((>= pos 3) (t-mid t))
        (#t (t-down t))))

;; ============================================================
;; 结算
;; ============================================================

(define money-rewarded? #f)

(define (finish-success!)
  (spotlight! "照面：带着答案离开"
    (if she-here?
        "你把杯子扣在桌上，起身。她没有抬头，手指从杯壁上松开。老板、歌厅和收账人的身份已经对上。"
        "你把杯子扣在桌上，起身。他还当你是那个替外地人找门路的掮客。老板、歌厅和收账人的身份已经对上。"))
  (end-encounter 'success))

(define (finish-exposed!)
  (define won? (identity-clk 'full?))
  (injure!)
  (spend-actor-composure! 'player 2)
  (spend-up-to! "金钱" 15)
  (set-global! '收账人识破过你 #t)
  (spotlight!
    (if won? "照面：答案到手，人被打出去" "照面：没能确认身份")
    (cond
      ((and won? she-here?)
       "他把你按在墙上搜走一笔钱，再一脚踹出门外。她始终没有出声。你挨了打，但老板、歌厅和收账人的身份已经对上——代价是他从今往后会把她一起重新算账。")
      (she-here?
       "他把你按在墙上搜走一笔钱，再一脚踹出门外。她始终没有出声。你没拿到答案，却让他记住了你们两张脸。")
      (won?
       "他把你按在墙上搜走一笔钱，再一脚踹出门外。你挨了打，但他说漏的那些话已经足够确认身份。")
      (#t
       "他把你按在墙上搜走一笔钱，再一脚踹出门外。雨还在下。你什么也没带走。")))
  (end-encounter (if won? 'success 'fail)))

(define-rule "被识破"
  (lambda () (suspicion-clk 'full?))
  (lambda () (finish-exposed!)))

(define (money-reward-due?) (and (money-clk 'full?) (not money-rewarded?)))

(define (give-money-reward!)
  (set! money-rewarded? #t)
  (add-item! "欠账凭据" 1)
  (add-item! "金钱" 20)
  (play-banter!
    (line "收账人" "拿着。别让我再看见你为这点钱开口。")))

(define (stage-turn-due?) (and (= stage 1) (>= (identity-clk 'current) 2)))

;; 段转:不是结算,是换一桌话。段一没谈完的话题作废。
(define (do-stage-turn!)
  (set! stage 2)
  (set! topic #f)
  (set! topic-pos 0)
  (play-banter!
    (line "收账人" "你问得比一个跑腿的多。")
    (line "收账人" "坐下。这杯我请。")))

;; 引擎的 EndTurn 只跑 (on-turn-end),不跑 (on-action)——所以 define-rule
;; 在回合末不会被检查。话题是可以在回合末冷掉并结算的(指针滑到 0 或 4),
;; 那一下产生的时钟变化必须在这里自己收尾,否则段转和副目标会一直拖到
;; 玩家的下一个动作之后才发生。
(define (settle-clocks!)
  (if (money-reward-due?) (give-money-reward!) #f)
  (if (stage-turn-due?) (do-stage-turn!) #f))

(define-rule "副目标完成"
  (lambda () (money-reward-due?))
  (lambda () (give-money-reward!)))

(define-rule "他放下了杯子"
  (lambda () (stage-turn-due?))
  (lambda () (do-stage-turn!)))

;; ============================================================
;; 他的疑问
;; ============================================================

(define turn-count 0)

;; 平时每 2 回合一句,他起了疑心(≥3)之后每回合一句;同一时刻最多挂一个,
;; 旧疑问没结算之前不出新的。
;; 判据只看疑心、不看段落:推进主线永远不该招来更多盘问,
;; 盘问变密只能是你自己失手的后果。
(define (question-due?)
  (and (not question)
       (not (null? (question-queue)))
       (if (>= (suspicion-clk 'current) 3)
           #t
           (= (modulo turn-count 2) 0))))

(define used-questions '())

;; 按表出,但跳过已经问过的。被动变体开局就用掉了「她的眼神」,
;; 后面不能再问一遍——他不会把同一句原样问两次。
(define (question-queue)
  (filter (lambda (q) (not (member? (t-id q) used-questions))) question-table))

(define (open-question! q)
  (set! question q)
  (set! question-pos 1)   ; 疑问出生在他占优的一侧
  (set! used-questions (cons (t-id q) used-questions))
  (play-banter! (line "收账人" (t-open q))))

(define (draw-question!)
  (open-question! (car (question-queue))))

;; 她在场时,他第一句问的就是她。
(if she-here?
    (begin
      (suspicion-clk 'tick!)
      (open-question! (list-ref question-table 3)))
    #f)

;; ============================================================
;; 回合末
;; ============================================================
;; 挂着没谈完的话会冷掉:两边的指针各 −1。不理他的疑问,它会自己滑向露馅。

(define (settle-topic!)
  (cond
    ((>= topic-pos topic-max)
     (play-banter! (line "收账人" (t-win topic)))
     (if (equal? (t-line topic) 'identity)
         (clock-tick-n! identity-clk (t-gain topic))
         (clock-tick-n! money-clk (t-gain topic)))
     (set! done-topics (cons (t-id topic) done-topics))
     (set! topic #f))
    ((<= topic-pos 0)
     (play-banter! (line "收账人" (t-lose topic)))
     (sour! topic)
     (suspicion+ 1)
     (set! topic #f))
    (#t #f)))

(define (settle-question!)
  (cond
    ((>= question-pos topic-max)
     (play-banter! (line "收账人" (t-win question)))
     (suspicion- 1)
     (set! question #f))
    ((<= question-pos 0)
     (play-banter! (line "收账人" (t-lose question)))
     (suspicion+ 1)
     (set! question #f))
    (#t #f)))

(define-turn-rule "话会冷掉"
  (lambda () #t)
  (lambda ()
    (set! turn-count (+ turn-count 1))
    (if topic (begin (set! topic-pos (- topic-pos 1)) (settle-topic!)) #f)
    (if question (begin (set! question-pos (- question-pos 1)) (settle-question!)) #f)
    (settle-clocks!)
    (if (suspicion-clk 'full?)
        (finish-exposed!)
        (if (question-due?) (draw-question!) #f))))

;; ============================================================
;; 推条
;; ============================================================
;; 三种手法,每颗骰选一种。推完他都要回一句——台词只看指针当前落在哪一区,
;; 与你是怎么推到那里的无关。这样每个话题只写三句区间台词就够了。

(define (push-topic! n)
  (set! topic-pos (max 0 (min topic-max (+ topic-pos n))))
  (play-banter! (line "收账人" (zone-line topic topic-pos)))
  (settle-topic!))

(define (push-question! n)
  (set! question-pos (max 0 (min topic-max (+ question-pos n))))
  (play-banter! (line "收账人" (zone-line question question-pos)))
  (settle-question!))

;; ---- 主动话题的三个动词 ----

(define (node-follow t)
  (node (string-append "顺着说·" (t-tag t))
    :subtitle "稳：坏 −1 / 中 +1 / 好 +2"
    :tags (list "低风险")
    :requires (list (req-die))
    :resolve (roll 'social (topic-mods t)
      (outcome "话说岔了"
        (lambda () (push-topic! -1)))
      (outcome "跟上了"
        (lambda () (push-topic! 1)))
      (outcome "他自己往下说了"
        (lambda () (push-topic! 2))))))

(define (node-press t)
  (node (string-append "施压·" (t-tag t))
    :subtitle "烫：坏 −2 / 中 +1 / 好 +2"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'knowledge (topic-mods t)
      (outcome "压过头了"
        (lambda () (push-topic! -2)))
      (outcome "他让了半步"
        (lambda () (push-topic! 1)))
      (outcome "他整个让开了"
        (lambda () (push-topic! 2))))))

(define (node-drink t)
  (node (string-append "拿酒垫话·" (t-tag t))
    :subtitle "不判定，但要钱：叫一轮酒把话续上。手气差的回合的出口"
    :requires (list (req-die) (req-item "金钱" 4))
    :resolve (instant
      (outcome "杯子满上了"
        (lambda () (spend-up-to! "金钱" 4) (push-topic! 1))))))

(define (node-drop-topic t)
  (node (string-append "放下·" (t-tag t))
    :subtitle "不占骰。主动收手，不结算；以后可以重开，但他听过这话"
    :resolve (instant
      (outcome "话头放下了"
        (lambda () (sour! t) (set! topic #f) (set! topic-pos 0))))))

(define (node-topic-open t)
  (container-with-clocks
    (string-append "话题·" (t-tag t))
    (list (node-follow t) (node-press t) (node-drink t) (node-drop-topic t))
    (list (list 'clock (string-append "拔河：" (t-tag t)) topic-pos topic-max 'gauge
                "0 端他收口，7 端他松口；回合末未谈完 −1。"))))

;; ---- 疑问的三个动词 ----

(define (node-q-follow q)
  (node (string-append "圆话·" (t-tag q))
    :subtitle "稳：坏 −1 / 中 +1 / 好 +2"
    :tags (list "低风险")
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "话说岔了"
        (lambda () (push-question! -1)))
      (outcome "圆了半句"
        (lambda () (push-question! 1)))
      (outcome "他自己替你圆了"
        (lambda () (push-question! 2))))))

(define (node-q-press q)
  (node (string-append "压问·" (t-tag q))
    :subtitle "烫：坏 −2 / 中 +1 / 好 +2"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "压过头了"
        (lambda () (push-question! -2)))
      (outcome "他让了半步"
        (lambda () (push-question! 1)))
      (outcome "他整个让开了"
        (lambda () (push-question! 2))))))

(define (node-q-drink q)
  (node (string-append "拿酒挡问·" (t-tag q))
    :subtitle "不判定，但要钱：叫一轮酒把这句盖过去"
    :requires (list (req-die) (req-item "金钱" 4))
    :resolve (instant
      (outcome "这句被酒盖过去了"
        (lambda () (spend-up-to! "金钱" 4) (push-question! 1))))))

(define (node-question-open q)
  (container-with-clocks
    (string-append "疑问·" (t-tag q))
    (list (node-q-follow q) (node-q-press q) (node-q-drink q))
    (list (list 'clock (string-append "拔河：疑问" (t-tag q)) question-pos topic-max 'gauge
                "开在他占优的一侧；拉到 7 圆过去、疑心 −1；滑到 0 露馅、疑心 +2。"))))

;; ============================================================
;; 开话题
;; ============================================================

(define (node-start-topic t)
  (node (t-name t)
    :subtitle (string-append
                (if (equal? (t-line t) 'identity) "身份线" "钱线")
                "；开话题不判定，指针置 "
                (number->string (topic-open-pos))
                (if (= stage 2) "（他已经朝你这边偏了）" "")
                (if (soured? t) "。他听过这话：全部判定 −1" ""))
    :tags (if (soured? t) (list "他听过这话 · 难度−1") '())
    :requires (list (req-die))
    :resolve (instant
      (outcome "话头递出去了"
        (lambda ()
          (set! topic t)
          (set! topic-pos (topic-open-pos)))))))

(define (open-topic-nodes lst)
  (cond ((null? lst) '())
        ((done? (car lst)) (open-topic-nodes (cdr lst)))
        (#t (cons (node-start-topic (car lst)) (open-topic-nodes (cdr lst))))))

(define (node-topic-menu)
  (container
    (if (= stage 1) "话题：应付" "话题：上心")
    (open-topic-nodes (current-pool))))

;; ============================================================
;; 空间线：不受话题规则约束，可反复用
;; ============================================================

(define (node-scan-room)
  (node "打量这间屋子"
    :subtitle "空间线；可反复使用——屋子不会因为你看过一次就变样"
    :tags (list "低风险")
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "什么也没看出来"
        (lambda () (spend-composure! 1)))
      (outcome "记住了几处"
        (lambda () (money-clk 'tick!)))
      (outcome "整间屋子都在你脑子里"
        (lambda () (money-clk 'tick!))))))

(define (node-sleight)
  (node "袖里乾坤，顺手拿"
    :subtitle "空间线；可反复使用，但手伸出去就有被按住的一刻"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "手伸早了"
        (lambda () (suspicion+ 1)))
      (outcome "摸到一件"
        (lambda () (money-clk 'tick!)))
      (outcome "两只手都没空着"
        (lambda () (money-clk 'tick!))))))

(define (node-space)
  (container "这间屋子" (list (node-scan-room) (node-sleight))))

;; ============================================================
;; 在场的人：动作内 banter 的锚点
;; ============================================================
;; 说话人解析走「队员 Id → 队员 Name → 场景节点 Name」。收账人和夜莺都不是队员,
;; 树里必须有同名节点,否则动作内的 banter 会直接报错(动作外才退化成侧边卡)。

(define (node-collector)
  (observe-action "收账人"
    (if (= stage 1)
        "邻城口音，衣着比这条街上任何人都齐整。他还当你是个替外地人找门路的掮客。他不威胁人——他只是看你的时间越来越长。"
        "他放下了杯子。这一段他更愿意说，但每一句说错的代价也更高。")))

(define (node-her)
  (observe-action "夜莺"
    "她坐在斜对面那张桌子，攥着杯子的手没有松开过。"))

;; ============================================================
;; 离开
;; ============================================================

(define (node-leave)
  (node "见好就收"
    :subtitle (if (identity-clk 'full?)
                  "不占骰。带走已经拿到的一切，起身离开"
                  "身份还没确认——现在走，这一趟就白来了")
    :disabled (not (identity-clk 'full?))
    :resolve (instant
      (lambda () (finish-success!)))))

;; ============================================================
;; 渲染
;; ============================================================
;; 没开话题时场上是"开话题"列表;开了之后只显示当前话题容器。
;; 疑问、空间线、离开节点始终在。

(define (people-nodes)
  (if she-here?
      (list (node-collector) (node-her))
      (list (node-collector))))

(define (talk-nodes)
  (if topic (list (node-topic-open topic)) (list (node-topic-menu))))

(define (question-nodes)
  (if question (list (node-question-open question)) '()))

(define (money-line-nodes)
  (if (money-clk 'full?) '() (list (node-space))))

(define (scene-nodes)
  (append
    (people-nodes)
    (talk-nodes)
    (question-nodes)
    (money-line-nodes)
    (list (node-leave))))

(define (get-render-data)
  (container
    (if (= stage 1) "照面：应付" "照面：上心")
    (append (clock-nodes (identity-clk 'render-data) (money-clk 'render-data) (suspicion-clk 'render-data))
      (scene-nodes))))
