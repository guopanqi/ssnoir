;; scenes/encounters/勒索信.scm - 第一章·小节一「勒索信」
;;
;; 两幕，同一个晚上。分两幕，是因为这一场里最有戏的不是追逐，是**等待**：
;; 追逐的决策很薄（他跑你追，剩下的只是快慢），而蹲守要回答一个真问题——
;; 这些人里哪一个是他，你什么时候起身。两幕各问一种问题，
;; 所以哪怕主目标只有一根钟，这一场也不会单调。
;;
;; 第一幕·蹲守 —— 三个人先后接近邮箱。每个人是一张卡，卡上一根短钟「看明白他」，
;;   填满就知道他是不是取信人。认出人以前，追逐不会开始。
;;   每回合仍支付引擎统一的 1 点冷静；这一幕不再叠加本地回合伤害。
;;   额外冷静代价只来自白看一眼这类明确写出的坏结果。
;;
;; 第二幕·追逐 —— 三段街景（巷口 → 货栈区 → 邮局后街），每次休息才换景。
;;   「追上他」跨场景累积，填满立即成功；「逃脱」每次休息 +1，到 3 而仍未追上就失败。
;;   每段街景只改变可用的追逐手段和风险，不再另存一根会作废的局部追逐钟。
;;   钱不是一个可以规划的副目标，也没有钟。它是**冒进那一手的残渣**：
;;   三张冒进追击卡打出中档时，意思是你够到了他、抓下来的不是他——一叠钱撒在地上。
;;   于是场上多出一张一次性机会卡，只活到本回合结束（回合末就换街景，钱留在上一条街）。
;;   稳妥的那三张卡永远不掉钱。钱和人在虚构上就是同一件事的两面：
;;   你抓到了钱，说明你没抓到人。这样两个目标不必靠任何罚则去对立。
;;
;; 城市输入只保留踩点满格的兑现：
;;   =6「这一片你熟了」—— 第二幕的货栈区多一条只有你知道的边门。
;;
;; 真邮差是基础的免费排除项：沿街商户都认得他，不需要踩点到三格。
;; 但他不在开局自动消失；玩家仍然要进入他的 Container，亲手执行「认出老邮差」。
;;
;; 第一幕：尼尔始终坐在邮箱斜对面的摊位，报纸挡着脸。
;; 一个人一张卡，卡上一根 0/4 的短钟和**两种弄明白他的办法**。
;; 两种办法故意不同技能——如果全是「看清」，三张卡就是同一个动作抄三遍，
;; 骰子往哪儿放没有区别。眼睛之外还能听摊主和工头的话，或用常识对照邮务规矩；
;; 但所有手段都不许让尼尔离开摊位或主动盘问，否则蹲守的虚构就破了。
;; 于是「手上这几颗骰的点数适合干什么」变成一个真问题。
;; 钟填满就得出结论：是他就直接追出去（结论本身就是推进，不再补点一次「起身」），
;; 不是他就划掉，接着看下一个。
;; 即使只剩最后一个，也必须把他认清；排除别人不能替代识别本人。
;;
;; 对外契约：回传 (list 人 钱)——
;;   人：'拿到线索 / '跟丢
;;   钱：拿回的金额（0 / 50 / 100）
;; 两个轴都不阻断主线；结算文案与小节二的起步条件由《三封信》解释。

(define scout
  (let ((v (get-global '踩点格数))) (if v v 0)))

(define (knows-block?) (>= scout 6))

(define catch-target 6)
(define escape-target 3)
;; 钱不是目标，是「差一点抓住他」掉在地上的东西。一张机会卡最多 25，
;; 一晚上顶天一百——和以前的上限一样，但每一枚都是你自己换来的。
(define money-drop-full 25)
(define money-drop-part 15)
(define money-cap 100)
;; 每个人 4 格。给得比"刚好够"多一点：这一幕才有"我这颗骰子先喂谁"的余地，
;; 而不是一颗骰子一个人。冷静不是这里的节流阀——烟和酒是用钱买缓冲的正经渠道，
;; 交锋不该按冷静的上限去反推格数。
(define look-target 4)

(define catch-clk
  (make-clock "追上他" catch-target 'gauge
              "跨越三段街景的主目标。填满就把他按住；逃脱先满则跟丢。"))

(define escape-clk
  (make-clock "逃脱" escape-target 'gauge
              "每次休息推进一格并换到下一段街景。填满时若还没追上，他就跑了。"))

(define act 1)
(define finished? #f)
(define seg 0)
(define money-taken 0)       ; 这一晚一共从地上抓回多少
(define money-drop? #f)      ; 地上此刻有没有一叠钱；只活到本回合结束
(define investigation-attempts '())
;; 进第二幕前 音乐 的值（可能是某张唱片、随机播放，也可能没有）：收场时原样切回去。
;; 主题优先于一切，播完必须有人还——就是这里。
(define chase-saved-music #f)

;; 城市侧剧场负责相见与投信；交锋负责蹲守提示、幕转和收尾。
(define (on-encounter-enter)
  ;; 进场先收 音乐 的值：第二幕主题盖过去，收场（finish!/倒下）原样还回去。
  (set! chase-saved-music (get-global '音乐))
  (spotlight! "找出嫌疑人"
    "观察路过的人们的举止，分辨出嫌疑人！"))

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

(define (catch+ n) (clock-tick-n! catch-clk n))

;; ============================================================
;; 第一幕·蹲守
;; ============================================================

;; 只有三个人。五个太多——认人的问题不靠人多变难，靠"两个都像"变难。
;; 他伪装成邮差，所以场上有两个穿制服的，「认得那张脸」认的就是这一对里哪张脸是真的；
;; 第三个人负责让排除法不是白送：划掉一个还剩两个，仍然要付点什么才敢起身。
(define candidates
  (list
    (list "背邮袋的老头" "骑着辆旧车慢悠悠来的；沿街有人跟他点头" #f)
    (list "穿制服的年轻人" "制服不太合身；停在邮箱前，一直不下车" #t)
    (list "门口抽烟的人" "靠在货栈门口，烟一根接一根；站了快一个钟头了" #f)))

(define (cand-name c) (car c))
(define (cand-desc c) (cadr c))
(define (cand-target? c) (caddr c))

;; 每个人一根自己的短钟。填满 = 你弄明白了他是谁，不是"看见"了什么——
;; 所以喂它的动作不必都是眼睛。
(define look-clocks
  (map (lambda (c)
         (list (cand-name c)
               (make-clock (string-append "看明白：" (cand-name c)) look-target 'gauge
                           "填满就有结论：是他，或者不是他。")))
       candidates))

(define (look-clock name)
  (define (walk xs)
    (if (null? xs)
        (error "勒索信：未知的蹲守对象")
        (if (equal? (car (car xs)) name) (cadr (car xs)) (walk (cdr xs)))))
  (walk look-clocks))

(define cleared '())     ; 已经排除的名字

(define (cleared? name) (member? name cleared))

(define (clear! name)
  (if (cleared? name) #f (set! cleared (cons name cleared))))

(define (live-candidates)
  (filter (lambda (c) (not (cleared? (cand-name c)))) candidates))

(define (postman? c)
  (equal? (cand-name c) "背邮袋的老头"))

;; 得出结论**本身就是推进**：是他就直接进第二幕，不再要玩家补点一次「起身」。
;; 不是他 → 划掉，接着看下一个。
(define (reveal! c)
  (if (cand-target? c)
      (begin
        (begin-chase!))
      ;; 录音在划掉之前先选定：这一句一局里最多说两次(三个人里两个不是他)，
      ;; 同一条连放两遍，第二次听着就像卡带。两条不同的录音按第几次排除来选。
      (let ((take (if (null? cleared)
                      "勒索信/蹲守/认错人/01/尼尔"
                      "勒索信/蹲守/认错人/02/尼尔"))
            (text (if (null? cleared)
                      "抱歉，认错人了。"
                      "……脚步不对。不是他。")))
        (clear! (cand-name c))
        ;; 排除由版面变化宣告（候选人从名单上消失）＋尼尔这句 banter，不另写备注复述。
        (play-banter!
          (line "尼尔" text take)))))

;; 喂某个人那根钟。填满就直接出结论——不必玩家再点一次"下结论"。
(define (look+ c n)
  (let ((clk (look-clock (cand-name c))))
    (clock-tick-n! clk n)
    (if (clk 'full?) (reveal! c) #f)))

(define (attempt-count name entries)
  (if (null? entries)
      0
      (+ (if (equal? name (car entries)) 1 0)
         (attempt-count name (cdr entries)))))

(define (mark-investigation! c)
  (set! investigation-attempts (cons (cand-name c) investigation-attempts)))

(define (investigation-subtitle c base)
  (let ((attempts (attempt-count (cand-name c) investigation-attempts)))
    (cond
      ((= attempts 0) base)
      ((= attempts 1) "似乎有一些疑点，但又似乎不是")
      (else "有些不对劲，但还需要确认"))))

;; 每个人两种办法，故意跨技能。写法统一：坏 = 白花一颗骰、冷静 −1；中 = +1 格；好 = +2 格。
;; 副标题只写情景，不写技能：技能由 (roll skill) 声明，卡面自己会画出那颗药丸
;; （见 ActionNodeDrawer 的 DrawSkillPill）。在副标题里再抄一遍，等于把一行
;; 本来能讲这一手长什么样的字，让给了玩家已经看得见的东西。
;; 六张手法风险完全一样（坏都是 −2 冷静），所以这一幕不挂风险标签——
;; 标签是用来分辨的，六张全一样的标签只是噪音。要分辨的是技能，那颗药丸已经在了。
;; 好的那一档一颗骰子就够得出结论，所以"点数高的骰子放这儿"是有意义的。
(define (way c name skill subtitle bad mid good)
  ;; 六种手段的短名在本场全局唯一；不再把目标姓名拼进卡片标题，避免动作卡标题截断。
  ;; :anchor 与候选人同锚——展开后手法卡仍钉在这个人身上，不漂到 UI 网格。
  (node name
    :anchor (cand-zone c)
    :subtitle (investigation-subtitle c subtitle)
    :requires (list (req-die))
    :resolve (roll skill
      (outcome (lambda () (mark-investigation! c) (spend-composure! 2)))
      (outcome (lambda () (mark-investigation! c) (look+ c 1)))
      (outcome (lambda () (mark-investigation! c) (look+ c 2))))))

(define (ways c)
  (let ((n (cand-name c)))
    (cond
      ((equal? n "背邮袋的老头")
       (list
         (way c "听那辆车铃" 'sharpness
              "报纸后面也听得见那枚坏车铃"
              "他骑得太慢，铃始终没响" "铃里带着一个破音" "是骑旧了才会坏成这样")
         (way c "听摊主搭话" 'social
              "熟客进街，摊主总会先叫一声"
              "面摊正忙，没人抬头" "摊主冲他喊了声邮差" "摊主叫出了他的名字")))
      ((equal? n "穿制服的年轻人")
       (list
         (way c "看那身制服" 'sharpness
              "跑一天码头的制服不会这么干净"
              "隔着夜宵车的蒸汽，看不真切" "肩头没有邮袋磨过的印" "裤脚是干的，鞋底没有泥")
         (way c "对照投递规矩" 'knowledge
              "真邮差不会守着一只私人邮箱"
              "你记不清邮务站的排班" "他在等，不是在投递" "他看的不是门牌，是锁")))
      ((equal? n "门口抽烟的人")
       (list
         (way c "看他的鞋" 'sharpness
              "鞋说的话比嘴多"
              "他站在门槛的阴影里" "码头上走惯了的鞋" "鞋底磨得跟码头一个花纹")
         (way c "等有人叫他" 'social
              "码头上的名字从来不是小声叫的"
              "没人经过那个角落" "货栈里有人冲他挥了下手" "工头喊了个名字，他应了一声")))
      (else (error "勒索信：未知的蹲守对象")))))

;; 三个人始终是同一种结构：Container 内先显示辨认进度，再列两种办法。
;; 老邮差的 Container 里固定多一张即时动作：
;; 不花骰子，一次填满他的辨认钟，再走和其他手段相同的结论流程。
(define (node-recognize-postman c)
  (node "认出老邮差"
    :anchor (cand-zone c)
    :subtitle "沿街的人都认得他"
    :tags (list "不花骰子")
    :resolve (instant
      (outcome (lambda () (look+ c look-target))))))

;; 候选人一人一锚（钉在场上对应人形）；父节点与手法/认出卡共用该人锚，
;; 展开即「点这个人 → 两手动作挂在他身上」。根容器仍在报摊（尼尔蹲守位）。
(define (cand-zone c)
  (cond
    ((equal? (cand-name c) "背邮袋的老头") "勒索信-街沿")
    ((equal? (cand-name c) "穿制服的年轻人") "勒索信")
    ((equal? (cand-name c) "门口抽烟的人") "勒索信-货栈门口")
    (else (error "勒索信：未知的蹲守对象"))))

(define (node-candidate c)
  (node (cand-name c)
    :anchor (cand-zone c)
    :subtitle (cand-desc c)
    :children (append
      (list (clock-node (string-append "调查进度：" (cand-name c))
                        ((look-clock (cand-name c)) 'render-data)))
      (if (postman? c) (list (node-recognize-postman c)) '())
      (ways c))))

(define (act1-nodes)
  (map node-candidate (live-candidates)))

;; ============================================================
;; 第二幕·追逐：三段街景
;; ============================================================

(define (seg-name n)
  (cond
    ((= n 0) "巷口")
    ((= n 1) "货栈区")
    ((= n 2) "邮局后街")
    (else (error "勒索信：未知街景"))))

(define (enter-segment! n)
  (if (or (< n 0) (> n 2))
      (error "勒索信：追逐街景越界")
      (begin
        (set! seg n)
        ;; Act2 根名固定为「巷子」以走 Portal；根 :anchor 与子卡同落到分区，换段切镜。
        ;; spotlight 仍宣告段名。
        (spotlight! (seg-name n)
          (cond
            ((= n 0) "追上他！在他逃走之前")
            ((= n 1) "货栈区——贴着货堆还能包抄")
            ((= n 2) "邮局后街——这是最后一段")
            (else ""))))))

(define (begin-chase!)
  ;; 识破后的穿街追赶先在剧场中演完，随后切入第二幕的巷子空间。
  (load-file "scripts/theatre/冲进巷口.scm")
  (冲进巷口-演出)
  (set! act 2)
  ;; 第二幕全程追逐主题，盖过唱片机和城市默认声；进场时收的值，收场时原样还回去。
  (set-global! '音乐 "主题-追逐")
  ;; 从调查动作切进追逐时，刚花掉最后一颗骰。这里换一手，不把它误算成
  ;; 一次追逐回合：人不额外拉开、冷静不额外扣。
  (refresh-encounter-dice!)
  (enter-segment! 0))

;; ── 追击动作：每段两条路，快的伤身、稳的慢 ──────────
;; 快/稳这件事必须**一眼认得出**，所以它是标签，不是副标题里的一句形容。
;; 副标题讲的是这一手长什么样（"看不见路，但这是最短的一条"），
;; 风险讲的是打出坏结果要付什么——那是玩家分骰子之前就要先分辨的东西。
;; 口径和城市生活里的工作卡一致：高风险 = 坏结果 −2 冷静或见血；
;; 低风险 = 坏结果至多 −1。稳妥那两张原来也扣 2，标签就成了假话，一并改掉。
(define (node-chase-fast)
  (cond
    ((= seg 0)
     (node "翻过那辆推车"
       :anchor (act2-zone)
       :subtitle "冒进。够到了他，手没停住"
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome (lambda () (spend-composure! 2)))
         (outcome (lambda () (catch-clk 'tick!) (drop-money!)))
         (outcome (lambda () (catch+ 2))))))
    ((= seg 1)
     (node "跟进那条黑巷"
       :anchor (act2-zone)
       :subtitle "看不见路，但这是最短的一条"
       :tags (list "高风险")
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome (lambda () (spend-composure! 2)))
         (outcome (lambda () (catch-clk 'tick!) (drop-money!)))
         (outcome (lambda () (catch+ 2))))))
    ((= seg 2)
     (node "扑上去"
       :anchor (act2-zone)
       :subtitle "扑得着人，也可能只扑着一件外套"
       :tags (list "高风险")
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome (lambda () (injure!)))
         (outcome (lambda () (catch-clk 'tick!) (drop-money!)))
         (outcome (lambda () (catch+ 2))))))
    (else (error "勒索信：未知追击动作"))))

(define (node-chase-safe)
  (cond
    ((= seg 0)
     (node "绕过去"
       :anchor (act2-zone)
       :subtitle "慢，但不掉东西"
       :tags (list "低风险")
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome (lambda () #f))
         (outcome (lambda () (catch-clk 'tick!)))
         (outcome (lambda () (catch+ 2))))))
    ((= seg 1)
     (node "贴着货堆推进"
       :anchor (act2-zone)
       :subtitle "推开一条道，不快也不丢东西"
       :tags (list "低风险")
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome (lambda () (spend-composure! 1)))
         (outcome (lambda () (catch-clk 'tick!)))
         (outcome (lambda () (catch+ 2))))))
    ((= seg 2)
     (node "喊住他"
       :anchor (act2-zone)
       :subtitle "整条街都会记得今晚是谁在这儿喊"
       :tags (list "低风险")
       :requires (list (req-die))
       :resolve (roll 'social
         (outcome (lambda () (spend-composure! 1)))
         (outcome (lambda () (catch-clk 'tick!)))
         (outcome (lambda () (catch+ 2))))))
    (else (error "勒索信：未知追击动作"))))

;; 「这一片你熟了」的兑现落在这儿，而且是**你手里多一张别人没有的牌，由你决定打不打**，
;; 不是系统自动送你跳过一段。玩家自己按下去的兑现，比自动生效的兑现记得住。
;; 它一手抵得上一整段街景——这就是那两天蹲出来的东西。
(define (node-side-door)
  (node "从货栈边门包抄"
    :anchor (act2-zone)
    :subtitle "只有摸熟这一片的人才知道这扇门"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome (lambda () (spend-composure! 2)))
      (outcome (lambda () (catch+ 2)))
      (outcome (lambda () (catch+ 3))))))

;; ── 机会：地上那一叠 ────────────────────────────────
;; 它不是一个可以规划的副目标，是冒进那一手的残渣：你够到了他，抓下来的不是他。
;; 所以它没有钟——一张卡就是一个槽，一颗骰子，用掉就没了。
;; 也不写「逃脱 +1」这类惩罚：一颗本可以追人的骰子、不保证捡得着、只活一个回合，
;; 代价已经收够三遍。再挂一条明写的罚则就是收两次钱，还会让它读起来像陷阱。
;; 它活到本回合结束——回合就是你手上这几颗骰子的一次分配，取舍必须发生在这个窗口里。
(define (drop-money!)
  (if money-drop?
      #f
      (begin
        (set! money-drop? #t))))

(define (take-money! amount)
  (set! money-drop? #f)
  (let ((got (min amount (- money-cap money-taken))))
    (set! money-taken (+ money-taken got))
    (if (> got 0)
        (result-supplement! (string-append "拿回 " (number->string got) " 钱"))
        #f)))

;; 散钞必须落在独立世界锚（巷子 Prefab 的 Anchor_勒索信-散钞-*），
;; 不能与分区锚上的追击卡 / 逃脱·追上他钟抢同一屏位。
(define (act2-money-zone)
  (cond
    ((= seg 0) "勒索信-散钞-巷口")
    ((= seg 1) "勒索信-散钞-货栈区")
    ((= seg 2) "勒索信-散钞-邮局后街")
    (else (error "勒索信：未知散钞锚点"))))

(define (node-money-drop)
  (node "那叠散钞"
    :anchor (act2-money-zone)
    :tags (list "机会" "低风险")
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome (lambda () (set! money-drop? #f)))
      (outcome (lambda () (take-money! money-drop-part)))
      (outcome (lambda () (take-money! money-drop-full))))))

(define (act2-nodes)
  (append
    (list (node-chase-fast) (node-chase-safe))
    (if (and (= seg 1) (knows-block?)) (list (node-side-door)) '())
    (if money-drop? (list (node-money-drop)) '())))

;; 第二幕顶上只有两根，而且都是关于同一个人的：你要的，和你怕的。
;; 钱一根钟也没有——它在场上，不在顶栏。
(define (act2-clocks)
  (list (catch-clk 'render-data)
        (escape-clk 'render-data)))

;; ============================================================
;; 结算
;; ============================================================

(define (caught?) (catch-clk 'full?))
(define (recovered-money) money-taken)

(define (finish!)
  (if finished?
      #f
      (begin
        (set! finished? #t)
        ;; 追逐主题只活到这一夜：把进第二幕之前的值还回去（唱片、随机或城市默认声）。
        (set-global! '音乐 chase-saved-music)
        ;; 摩托车冲出来是这段追逐的固定收尾：它迫使你闪开，但不等于自动受伤。
        ;; 伤势只由具体行动的 outcome 产生，不能让所有追上路线都暗中追加一格伤势。
        (if (> (recovered-money) 0)
            (add-item! "金钱" (recovered-money))
            #f)
        (end-encounter
          (list (if (caught?) '拿到线索 '跟丢) (recovered-money))))))

;; 倒下不是跟丢：你追上了他，然后被撂在巷口。回传自己的收场标签，
;; 让三封信那边讲对这一夜发生了什么（见 on-delivery-result）。
;; 倒下同样走不出追逐主题，不断在这里还——finish! 那条路走不到。
(define (on-encounter-collapse)
  (set-global! '音乐 chase-saved-music)
  (collapse-result (list '倒下 0)))

;; ============================================================
;; 回合推进
;; ============================================================

;; 第二幕：每次休息都让他拉开一段距离，并把追逐带到下一段街景。
(define-opponent-rule "取信人拉开距离"
  (lambda () (and (= act 2) (not finished?)))
  (lambda ()
    (if (not (= (escape-clk 'current) seg))
        (error "勒索信：逃脱进度与当前街景不一致")
        #t)
    ;; 回合结束＝换街景。地上那一叠就留在上一条街了，不跟着你跑。
    (set! money-drop? #f)
    (escape-clk 'tick!)
    (if (escape-clk 'full?)
        (finish!)
        (enter-segment! (escape-clk 'current)))))

;; 「追上他」填满就立即结算，不必等到休息或跑完三段。
(define-rule "追上即结束"
  (lambda () (and (= act 2) (not finished?)))
  (lambda ()
    (if (catch-clk 'full?)
        (finish!)
        #f)))

;; ============================================================
;; 渲染
;; ============================================================

;; Act2 三段街景 → Stage「巷子」分区锚点（字面量，publish 静态扫描也能对上）。
(define (act2-zone)
  (cond
    ((= seg 0) "勒索信-巷口")
    ((= seg 1) "勒索信-货栈区")
    ((= seg 2) "勒索信-邮局后街")
    (else (error "勒索信：未知街景锚点"))))

(define (get-render-data)
  (if (= act 1)
      (node "勒索信-报摊"
        :anchor "勒索信-报摊"
        :children (cons
          (at-anchor "勒索信-报摊"
            (note-node "标注：尼尔在报摊蹲守" ""
              "尼尔拿报纸挡着脸，目光却悄悄跟着邮箱旁的人。"))
          (act1-nodes)))
      ;; Act2 是独立 Stage「巷子」。根名必须是「巷子」，才会命中
      ;; PortalIn_巷子 / Anchor_巷子 上的 StagePortalConfig，从码头邮箱一角穿门进入；
      ;; 若根直接叫「勒索信-巷口」等分区名，Portal 不会触发，镜头会跨城飞到 Stage 停泊位。
      ;; :anchor = 当前段分区：引擎在 Stage 根上优先用区内机位（Camera_勒索信-*），
      ;; 换段时随 act2-zone 更新；Camera_巷子 只作 Portal 落地/无 :anchor 时的走廊兜底。
      (let ((zone (act2-zone)))
        (node "巷子"
          :anchor zone
          :children (append
            (map (lambda (n) (at-anchor zone n))
                 (apply clock-nodes (act2-clocks)))
            (act2-nodes))))))
