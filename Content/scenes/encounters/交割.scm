;; scenes/encounters/交割.scm - 第一章·小节一「交割日」
;;
;; 两幕，同一个晚上。分两幕，是因为这一场里最有戏的不是追逐，是**等待**：
;; 追逐的决策很薄（他跑你追，剩下的只是快慢），而蹲守要回答一个真问题——
;; 这些人里哪一个是他，你什么时候起身。两幕各问一种问题，
;; 所以哪怕主目标只有一根钟，这一场也不会单调。
;;
;; 第一幕·蹲守 —— 三个人先后接近邮箱。每个人是一张卡，卡上一根短钟「看明白他」，
;;   填满就知道他是不是取信人。窗口是一根短钟「他随时会动手」，每回合走一格。
;;   走完，信封已经被取走，你没看见是谁——照样进第二幕，只是巷口那一段已经过去了。
;;   **这一幕没有空结局，只有位置好坏。**
;;   不用「每回合扣冷静」：缓冲只有 2 点，逐回合流失会让交锋反复撞穿倒下线，
;;   这条已经在城市生活设计 §2.2 整体取消。冷静只掉在白看一眼这类明确写出的坏结果上。
;;   早辨认和晚辨认不额外挂第二幕的加成或惩罚——窗口本身就是压力，再叠一层是罚两次。
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
;; 城市输入：踩点格数（0..6）。它是这一场唯一的兑现，而且是结构性的，不是加数值：
;;   ≥3「认得那张脸」—— 第一幕：真邮差变成一个**不花骰子、不掉冷静**就能认出的人
;;   =6「这一片你熟了」—— 第二幕：货栈区多一条只有你知道的边门
;; 认人兑现在第一幕，认地兑现在第二幕，一格不越界。
;;
;; 踩点认得的人**不在开局被自动划掉**，而是变成一个不花骰子的执行动作。
;; 自动划掉，玩家只看见场上少了几张卡，感觉不到那几天蹲在雨里换来了什么；
;; 让他自己按一下「你认得他」，兑现才落在他手上。
;; 注意踩点从不直接指认取信人——它只让你**免费排除错的人**。
;;
;; 第一幕：一个人一张卡，卡上一根 0/4 的短钟和**两种弄明白他的办法**。
;; 两种办法故意不同技能——如果全是「看清」，三张卡就是同一个动作抄三遍，
;; 骰子往哪儿放没有区别。眼睛之外还有嘴（过去搭一句）和脑子（这个点该谁当班），
;; 于是「手上这几颗骰的点数适合干什么」变成一个真问题。
;; 钟填满就得出结论：是他就直接追出去（结论本身就是推进，不再补点一次「起身」），
;; 不是他就划掉，接着看下一个。
;; 即使只剩最后一个，也必须把他认清；排除别人不能替代识别本人。
;; 压力全部由窗口承担：两回合，走完信封就被取走，巷口那一段也没了。
;;
;; 对外契约：回传 (list 人 钱 注意到摩托车?)——
;;   人：'拦下 / '跟丢
;;   钱：拿回的金额（0 / 50 / 100）
;;   注意到摩托车?：前灯切进追逐时，玩家是否看清足以在后来认出绑绳与背影的细节
;; 两个轴都不阻断主线；结算文案与小节二的起步条件由《三封信》解释。

(define scout
  (let ((v (get-global '踩点格数))) (if v v 0)))

(define (knows-face?) (>= scout 3))
(define (knows-block?) (>= scout 6))

(define catch-target 6)
(define escape-target 3)
;; 钱不是目标，是「差一点抓住他」掉在地上的东西。一张机会卡最多 25，
;; 一晚上顶天一百——和以前的上限一样，但每一枚都是你自己换来的。
(define money-drop-full 25)
(define money-drop-part 15)
(define money-cap 100)
(define stakeout-turns 2)
;; 每个人 4 格。给得比"刚好够"多一点：这一幕才有"我这颗骰子先喂谁"的余地，
;; 而不是一颗骰子一个人。冷静不是这里的节流阀——烟和酒是用钱买缓冲的正经渠道，
;; 交锋不该按冷静的上限去反推格数。
(define look-target 4)

(define catch-clk
  (make-clock "追上他" catch-target 'segments
              "跨越三段街景的主目标。填满就把他按住；逃脱先满则跟丢。"))

(define escape-clk
  (make-clock "逃脱" escape-target 'segments
              "每次休息推进一格并换到下一段街景。填满时若还没追上，他就跑了。"))

(define act 1)
(define finished? #f)
(define seg 0)
(define money-taken 0)       ; 这一晚一共从地上抓回多少
(define money-drop? #f)      ; 地上此刻有没有一叠钱；只活到本回合结束
(define stakeout-left stakeout-turns)
(define investigation-attempts '())
(define motorcycle-intervened? #f)
(define motorcycle-noticed? #f)

;; 开场、幕转和收尾都由交割自己拥有；世界只负责进入本交锋。
(define (on-encounter-enter)
  (play-animation! "交割-投信"))

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
    (list "背邮包的老头" "车铃是坏的，慢悠悠地骑过来，一路和人点头" #f)
    (list "另一个穿制服的" "制服有点大，停在邮箱前一直没下车" #t)
    (list "门口抽烟的男人" "站了快一个钟头，烟一根接一根" #f)))

(define (cand-name c) (car c))
(define (cand-desc c) (cadr c))
(define (cand-target? c) (caddr c))

;; 每个人一根自己的短钟。填满 = 你弄明白了他是谁，不是"看见"了什么——
;; 所以喂它的动作不必都是眼睛。
(define look-clocks
  (map (lambda (c)
         (list (cand-name c)
               (make-clock (string-append "看明白：" (cand-name c)) look-target 'segments
                           "填满就有结论：是他，或者不是他。")))
       candidates))

(define (look-clock name)
  (define (walk xs)
    (if (null? xs)
        (error "交割：未知的蹲守对象")
        (if (equal? (car (car xs)) name) (cadr (car xs)) (walk (cdr xs)))))
  (walk look-clocks))

(define cleared '())     ; 已经排除的名字

(define (cleared? name) (member? name cleared))

(define (clear! name)
  (if (cleared? name) #f (set! cleared (cons name cleared))))

(define (live-candidates)
  (filter (lambda (c) (not (cleared? (cand-name c)))) candidates))

;; 踩点在这里兑现成「这个人你不必费神去认」。只有一个人:真邮差。
;; 「这一片你熟了」不再往第一幕加第二个免费名额——三个人里免费认出两个,
;; 等于直接把答案递到手上。它整份兑现移到第二幕的边门。
(define (recognized? c)
  (and (equal? (cand-name c) "背邮包的老头") (knows-face?)))

;; 踩过一两格但还没到「认得那张脸」：只给一句面熟，不给机械好处。
;; 半途的投入要看得见在往哪儿去，否则玩家读不出这根钟是不是白填的。
(define (half-familiar? c)
  (and (equal? (cand-name c) "背邮包的老头")
       (> scout 0)
       (not (knows-face?))))

;; 得出结论**本身就是推进**：是他就直接进第二幕，不再要玩家补点一次「起身」。
;; 不是他 → 划掉，接着看下一个。
(define (reveal! c)
  (if (cand-target? c)
      (begin
        (begin-chase! #t))
      (begin
        (clear! (cand-name c))
        (play-banter!
          (line "尼尔" "抱歉，认错人了。"))
        (result-note! (string-append "不是他。划掉：" (cand-name c))))))

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
      ((= attempts 1) "刚才那一下没白看，再核一处细节")
      (else "疑点已经浮出来，只差最后确认"))))

;; 每个人两种办法，故意跨技能。写法统一：坏 = 白花一颗骰、冷静 −1；中 = +1 格；好 = +2 格。
;; 好的那一档一颗骰子就够得出结论，所以"点数高的骰子放这儿"是有意义的。
(define (way c name skill subtitle bad mid good)
  ;; 六种手段的短名在本场全局唯一；不再把目标姓名拼进卡片标题，避免动作卡标题截断。
  (node name
    :subtitle (investigation-subtitle c subtitle)
    :requires (list (req-die))
    :resolve (roll skill
      (outcome bad (lambda () (mark-investigation! c) (spend-composure! 1)))
      (outcome mid (lambda () (mark-investigation! c) (look+ c 1)))
      (outcome good (lambda () (mark-investigation! c) (look+ c 2))))))

(define (ways c)
  (let ((n (cand-name c)))
    (cond
      ((equal? n "背邮包的老头")
       (list
         (way c "听那辆车的铃" 'sharpness
              "敏锐；车铃坏没坏，隔着雨也听得出来"
              "雨声盖住了" "铃响得不对劲" "那个破音你听准了")
         (way c "问面摊老板" 'social
              "交际；这条街上的人天天见他"
              "老板忙着下面，没搭理你" "老板含糊了一句" "老板一边捞面一边把他祖上都说了")))
      ((equal? n "另一个穿制服的")
       (list
         (way c "看他的制服" 'sharpness
              "敏锐；一整天在雨里跑的人，衣服是什么样，你见过"
              "他背对着你，看不清" "袖口的磨损不对" "裤脚是干的——这件事你记住了")
         (way c "查班次" 'knowledge
              "学识；邮务的班次是有规矩的，规矩比人可靠"
              "你不知道这一片怎么排班" "这个点不该有第二班" "这个点根本不该有人来取")))
      ((equal? n "门口抽烟的男人")
       (list
         (way c "看他脚下" 'sharpness
              "敏锐；站了多久，地上是有数的"
              "光线太暗" "烟头的数目对得上他站的时间" "他一直在等人，等的不是信")
         (way c "过去借个火" 'social
              "交际；离得近才闻得出一个人身上有什么味道"
              "他把脸别开了" "他手上没有邮袋的油墨味" "他自己先开口问了你两句")))
      (else (error "交割：未知的蹲守对象")))))

;; 一个人一张卡，两种形态：
;;   踩点认得他        → 不花骰子、不掷判定，点一下直接出结论（那几天的兑现）
;;   其余（包括最后一人）→ 一张带钟的卡，里面两种弄明白他的办法
(define (node-candidate c)
  (cond
    ((recognized? c)
     (node "认出老邮差"
       :subtitle (string-append (cand-desc c) "——这张脸你见过。不花骰子")
       :resolve (instant
         (outcome "一眼就认出，不是他"
           (lambda () (reveal! c))))))
    (else
     (container-with-clocks
       (cand-name c)
       (ways c)
       (list ((look-clock (cand-name c)) 'render-data))))))

(define (act1-nodes)
  (map node-candidate (live-candidates)))

;; 第一幕顶上只有窗口这一根。追人和钱这一幕一格也动不了，
;; 把它们摆在那里只是让玩家去读两根自己现在碰不到的钟。
(define (act1-clocks)
  (list (list 'clock "他随时会动手" stakeout-left stakeout-turns 'countdown
              "每结束一个回合走一格。走完信封就被取走了，你没看见是谁——巷口那一段也就没了。")))

;; ============================================================
;; 第二幕·追逐：三段街景
;; ============================================================

(define (seg-name n)
  (cond
    ((= n 0) "巷口")
    ((= n 1) "货栈区")
    ((= n 2) "邮局后街")
    (else (error "交割：未知街景"))))

(define (enter-segment! n)
  (if (or (< n 0) (> n 2))
      (error "交割：追逐街景越界")
      (set! seg n)))

(define (begin-chase! in-position?)
  (play-animation! "交割-追上他")
  (set! act 2)
  (if in-position?
      (spotlight! "巷口"
        "他拐进巷口。你隔着半条街和一辆夜宵车。")
      (spotlight! "货栈区"
        "他已穿过巷口。你只能从货栈那头追，他已经拉开一段。"))
  (if in-position?
      (enter-segment! 0)
      (begin
        (escape-clk 'tick!)
        (enter-segment! 1))))

;; ── 追击动作：每段两条路，快的伤身、稳的慢 ──────────
(define (node-chase-fast)
  (cond
    ((= seg 0)
     (node "翻过那辆推车"
       :subtitle "力量；冒进的一手。够到了他，抓下来的可能不是他"
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome "踩翻了一摞碗" (lambda () (spend-composure! 1)))
         (outcome "邮袋带子断了" (lambda () (catch-clk 'tick!) (drop-money!)))
         (outcome "落地就在他后轮边上" (lambda () (catch+ 2))))))
    ((= seg 1)
     (node "跟进那条黑巷"
       :subtitle "敏锐；看不见路，但这是最短的一条"
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome "撞在没看见的货堆上" (lambda () (spend-composure! 1)))
         (outcome "纸包撕开一角" (lambda () (catch-clk 'tick!) (drop-money!)))
         (outcome "从巷子另一头贴上了他" (lambda () (catch+ 2))))))
    ((= seg 2)
     (node "扑上去"
       :subtitle "力量；最后一段了。扑得着人，也可能只扑得着他的外套"
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome "扑空了，肩膀先着地" (lambda () (injure!)))
         (outcome "扯下他半个口袋" (lambda () (catch-clk 'tick!) (drop-money!)))
         (outcome "把车整个掀了" (lambda () (catch+ 2))))))
    (else (error "交割：未知追击动作"))))

(define (node-chase-safe)
  (cond
    ((= seg 0)
     (node "绕过去"
       :subtitle "敏锐；慢，但干净。手上不会沾东西"
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome "绕远了半条街" (lambda () #f))
         (outcome "从摊子侧面绕了出去" (lambda () (catch-clk 'tick!)))
         (outcome "他还在原地拐弯" (lambda () (catch-clk 'tick!))))))
    ((= seg 1)
     (node "贴着货堆推进"
       :subtitle "力量；推开一条道，不快也不丢东西"
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome "挤在两摞货中间动不了" (lambda () (spend-composure! 1)))
         (outcome "推开一条道" (lambda () (catch-clk 'tick!)))
         (outcome "一路推到了空地上" (lambda () (catch-clk 'tick!))))))
    ((= seg 2)
     (node "喊住他"
       :subtitle "交际；整条街都会记得今晚是谁在这儿喊"
       :requires (list (req-die))
       :resolve (roll 'social
         (outcome "没人回头，他也没有" (lambda () (spend-composure! 1)))
         (outcome "他迟疑了一下" (lambda () (catch-clk 'tick!)))
         (outcome "前面有人替你拦了半步" (lambda () (catch+ 2))))))
    (else (error "交割：未知追击动作"))))

;; 「这一片你熟了」的兑现落在这儿，而且是**你手里多一张别人没有的牌，由你决定打不打**，
;; 不是系统自动送你跳过一段。玩家自己按下去的兑现，比自动生效的兑现记得住。
;; 它一手抵得上一整段街景——这就是那两天蹲出来的东西。
(define (node-side-door)
  (node "从货栈边门包抄"
    :subtitle "敏锐；只有摸熟这一片的人才知道这扇门"
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "锁舌今晚偏偏是好的" (lambda () (spend-composure! 1)))
      (outcome "边门穿出，在他前面" (lambda () (catch+ 2)))
      (outcome "你已经站在路当中" (lambda () (catch+ 3))))))

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
        (set! money-drop? #t)
        (result-note! "钱撒在地上了——这一回合还捡得着"))))

(define (take-money! amount)
  (set! money-drop? #f)
  (let ((got (min amount (- money-cap money-taken))))
    (set! money-taken (+ money-taken got))
    (if (> got 0)
        (result-note! (string-append "抓回 " (number->string got) " 金"))
        #f)))

(define (node-money-drop)
  (node "地上那一叠"
    :subtitle "敏锐；弯这一次腰，他就远一点"
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "钞票被风卷走了"
        (lambda () (set! money-drop? #f)))
      (outcome "抓起了几张"
        (lambda () (take-money! money-drop-part)))
      (outcome "一把全搂了起来"
        (lambda () (take-money! money-drop-full))))))

(define (act2-nodes)
  (append
    (list (node-runner) (node-chase-fast) (node-chase-safe))
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

;; 拦下他之后的那一下不掷骰：你按住了一个不想被按住的人，挨一记是既定代价，
;; 不是又一次运气。玩家用三段街景赢来的东西，不该被最后一掷推翻。
(define (play-scuffle!)
  (play-dialogue!
    (line "尼尔" "下来。")
    (line "取信人" "放手——我什么都不知道，我什么都不知道！")
    (line "世界" "他先动的手。车倒在两个人中间，你的肋下结结实实挨了一记车把。")
    (line "尼尔" "谁让你来取的？")
    (line "取信人" "一个先生。在老街的酒馆找的我，给了我钱。就这些，我不认识他。")
    (line "尼尔" "长什么样。")
    (line "取信人" "喝多了。穿得倒体面，袖口磨了。他给钱的时候顺手抽了两根烟给我，说留着。")
    (line "取信人" "那半包他忘在我这儿了。你拿去，别打了。"))
  (injure!))

(define (play-lost-trail!)
  (play-dialogue!
    (line "世界" "取信人的车尾灯在邮局后街尽头晃了一下，黑巷就把他吞了。那辆摩托车没有回头。")
    (line "尼尔" "该死。")
    (line "世界" "你没拦下他，也没问到那个先生是谁。线头到这里，干干净净地断了。")))

;; 现有主线追的是取信跑腿，而不是设计稿早期称谓里的莱恩。摩托车只负责切进追逐、
;; 给被追者多出几步，不改写既有的「拦下 / 跟丢」双结算。
(define (play-motorcycle-interruption!)
  (if motorcycle-intervened?
      #f
      (begin
        (set! motorcycle-intervened? #t)
        (set! motorcycle-noticed? (or (>= (catch-clk 'current) 3) (knows-block?)))
        (play-dialogue!
          (line "世界" "你快贴上车尾时，一束摩托车前灯从侧巷直切过来。")
          (line "世界" "你只得闪开。取信人趁那几步钻向后街。")
          (line "世界"
            (if motorcycle-noticed?
                "骑手穿皮夹克，后架的绳结打得很低。你没看见脸；他没有停车，也没有回头确认撞没撞到人。"
                "前灯把骑手的脸压成一团黑影。你只看见皮夹克的背影，没看清车牌，也没看清他往哪边去了。"))))))

(define (finish!)
  (if finished?
      #f
      (begin
        (set! finished? #t)
        (play-motorcycle-interruption!)
        (if (caught?)
            (begin
              (play-animation! "交割-追上他")
              (play-scuffle!))
            (play-lost-trail!))
        (if (> (recovered-money) 0)
            (add-item! "金钱" (recovered-money))
            #f)
        (end-encounter
          (list (if (caught?) '拦下 '跟丢) (recovered-money) motorcycle-noticed?)))))

;; ============================================================
;; 回合推进
;; ============================================================

;; 第一幕：窗口在关。走完就是信封被取走了，你没看见是谁——照样进第二幕。
(define-turn-rule "邮箱前的一个钟头"
  (lambda () (and (= act 1) (not finished?)))
  (lambda ()
    (set! stakeout-left (- stakeout-left 1))
    (if (<= stakeout-left 0)
        (begin-chase! #f)
        #f)))

;; 第二幕：每次休息都让他拉开一段距离，并把追逐带到下一段街景。
(define-turn-rule "取信人拉开距离"
  (lambda () (and (= act 2) (not finished?)))
  (lambda ()
    (if (not (= (escape-clk 'current) seg))
        (error "交割：逃脱进度与当前街景不一致")
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

;; 他是场上唯一的人，也是结算时唯一会开口的人——台词要有锚点，
;; 这张卡就是那个锚点（说话人只解析队员与场景节点名）。
;; 早先它是个空容器：点开什么也没有，白占一张卡。现在它是一张观察卡，
;; 身上挂着跨场景主钟「追上他」——你追的那个东西和你离他还有多远，写在同一张卡上。
(define (runner-look n)
  (cond
    ((= n 0) "他一条腿蹬在地上，车头已经拐向巷子。邮袋是空的——真正的邮袋不会那么轻。")
    ((= n 1) "他在货堆之间拐来拐去，像是走过很多遍。车筐里的纸一路往外飘。")
    ((= n 2) "他开始喘了。后街是直的，没有第二个岔口——这是最后一段。")
    (else (error "交割：未知街景"))))

(define (node-runner)
  (node "取信人"
    :subtitle "穿着不合身的邮差制服，骑一辆不属于他的车"
    :resolve (observe (runner-look seg))))

(define (get-render-data)
  (if (= act 1)
      (node "交割：面摊"
        :anchor "交割：斜对过的面摊"
        :children (append (apply clock-nodes (act1-clocks))
                    (act1-nodes)))
      (container
        (string-append "交割：" (seg-name seg))
        (append (apply clock-nodes (act2-clocks))
              (act2-nodes)))))
