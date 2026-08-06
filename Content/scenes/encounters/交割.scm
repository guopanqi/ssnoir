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
;; 第二幕·追逐 —— 位移轨道。三段街景（巷口 → 货栈区 → 邮局后街），
;;   每结束一个回合就永久驶过一段，未完成的局部进度随街景离开。
;;   每段是一个具体的障碍、几种代价不同的解法，不是同一个追击动作做三遍。
;;   两个目标各有自己的钟，共用同一批骰子，三段街景填不满两根：
;;     拦下他（主）——由追击动作喂。拦下了才有那场扭打，才有他身上掉出来的东西。
;;     捡回散掉的钱（副）——信封在他翻过推车时就散了，钱一路撒在街上；
;;                          每弯一次腰，他就远一点。钱在别处兑现：房租、明天的饭。
;;   主副之间没有换算，谁也不喂谁。玩家真正要答的是「今晚你要人还是要钱」。
;;   拦下他**不附带**找回任何钱——他手里攥着的只是一把空纸，否则副钟就没有意义了。
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
;; 第一幕：一个人一张卡，卡上一根 0/3 的短钟和**两种弄明白他的办法**。
;; 两种办法故意不同技能——如果全是「看清」，三张卡就是同一个动作抄三遍，
;; 骰子往哪儿放没有区别。眼睛之外还有嘴（过去搭一句）和脑子（这个点该谁当班），
;; 于是「手上这几颗骰的点数适合干什么」变成一个真问题。
;; 钟填满就得出结论：是他就直接追出去（结论本身就是推进，不再补点一次「起身」），
;; 不是他就划掉，接着看下一个。
;; 排除到只剩最后一个时，那张卡变成不花骰子的一次执行——排除法自己就是一条兑现路线。
;; 压力全部由窗口承担：两回合，走完信封就被取走，巷口那一段也没了。
;;
;; 对外契约：回传 (list 人 钱)——
;;   人：'拦下 / '跟丢
;;   钱：捡回的金额（0 / 20 / 40 / 60 / 80），由「捡回散掉的钱」的格数换算
;; 两个轴都不阻断主线；结算文案与小节二的起步条件由《三封信》解释。

(define scout
  (let ((v (get-global '踩点格数))) (if v v 0)))

(define (knows-face?) (>= scout 3))
(define (knows-block?) (>= scout 6))

(define catch-target 5)
(define money-target 4)
(define money-per-grid 20)
(define stakeout-turns 2)
;; 每个人 3 格。给得比"刚好够"多一点：这一幕才有"我这颗骰子先喂谁"的余地，
;; 而不是一颗骰子一个人。冷静不是这里的节流阀——烟和酒是用钱买缓冲的正经渠道，
;; 交锋不该按 2 点冷静的下限去反推格数。
(define look-target 3)

(define catch-clk
  (make-clock "拦下他" catch-target 'segments
              "主目标。填满就是把他按住了——那会打起来，你也会挨一下，但他身上的东西归你。没填满他就骑走了。"))

(define money-clk
  (make-clock "捡回散掉的钱" money-target 'segments
              "副目标。每格 20 金。信封散在街上，你每弯一次腰他就远一点。这笔钱只在别处兑现——房租，明天的饭。"))

(define act 1)
(define finished? #f)
(define seg 0)
(define chase-clk #f)
(define chance-clk #f)
(define stakeout-left stakeout-turns)
(define lost-first-segment? #f)

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

(define (catch+ n) (clock-tick-n! catch-clk n))
(define (money+ n) (clock-tick-n! money-clk n))

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
        (play-dialogue!
          (line "世界" "制服底下的裤脚是干的——他今天没在雨里跑过一整天。")
          (line "世界" "他的手刚伸进邮箱，你已经绕过了面摊的挡布。")
          (line "世界" "他回头看见你，一脚蹬开撑地的腿，车头拐进巷口。"))
        (begin-chase! #t))
      (begin
        (clear! (cand-name c))
        (result-note! (string-append "不是他。划掉：" (cand-name c))))))

;; 排除法的兑现：只剩最后一个人时，不必再花骰子确认——你已经知道了。
(define (last-one? c)
  (and (= (length (live-candidates)) 1) (not (recognized? c))))

;; 喂某个人那根钟。填满就直接出结论——不必玩家再点一次"下结论"。
(define (look+ c n)
  (let ((clk (look-clock (cand-name c))))
    (clock-tick-n! clk n)
    (if (clk 'full?) (reveal! c) #f)))

;; 每个人两种办法，故意跨技能。写法统一：坏 = 白花一颗骰、冷静 −1；中 = +1 格；好 = +2 格。
;; 好的那一档一颗骰子就够得出结论，所以"点数高的骰子放这儿"是有意义的。
(define (way c name skill subtitle bad mid good)
  (node (string-append (cand-name c) "·" name)
    :subtitle subtitle
    :requires (list (req-die))
    :resolve (roll skill
      (outcome bad (lambda () (spend-composure! 1)))
      (outcome mid (lambda () (look+ c 1)))
      (outcome good (lambda () (look+ c 2))))))

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
         (way c "想想这个点该谁当班" 'knowledge
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

;; 一个人一张卡，三种形态：
;;   踩点认得他        → 不花骰子、不掷判定，点一下直接出结论（那几天的兑现）
;;   排除到只剩他      → 同上（排除法的兑现）
;;   其余              → 一张带钟的卡，里面两种弄明白他的办法
(define (node-candidate c)
  (cond
    ((recognized? c)
     (node (string-append "你认得" (cand-name c))
       :subtitle (string-append (cand-desc c) "——这张脸你见过。不花骰子")
       :resolve (instant
         (outcome "一眼就认出来了，不是他"
           (lambda () (reveal! c))))))
    ((last-one? c)
     (node (string-append "只剩" (cand-name c) "了")
       :subtitle (string-append (cand-desc c) "——别人都排掉了。不花骰子")
       :resolve (instant
         (outcome "你从面摊后面站起来"
           (lambda () (reveal! c))))))
    (else
     (container-with-clocks
       (cand-name c)
       (ways c)
       (list ((look-clock (cand-name c)) 'render-data))))))

(define (act1-nodes)
  (map node-candidate (live-candidates)))

(define (act1-clocks)
  (list (catch-clk 'render-data)
        (money-clk 'render-data)
        (list 'clock "他随时会动手" stakeout-left stakeout-turns 'countdown
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

(define (chase-name n)
  (cond
    ((= n 0) "咬住他的车")
    ((= n 1) "别让他甩掉")
    ((= n 2) "把他按下来")
    (else (error "交割：未知追击对象"))))

(define (chance-name n)
  (cond
    ((= n 0) "散在摊子底下的钱")
    ((= n 1) "撒了一路的钱")
    ((= n 2) "冲进水沟的钱")
    (else (error "交割：未知机会对象"))))

(define (enter-segment! n)
  (set! seg n)
  (set! chase-clk
        (make-clock (chase-name n) 3 'segments
                    "填满：拦下他 +2。这一段驶过之后，没填满的进度就没了。"))
  (set! chance-clk
        (make-clock (chance-name n) 2 'segments
                    "填满：捡回散掉的钱 +2。这一段驶过之后作废。")))

(define (begin-chase! in-position?)
  (set! act 2)
  (if in-position?
      (spotlight! "巷口"
        "他没料到有人已经站起来了。你和他之间隔着半条街，还有一辆卖夜宵的推车。")
      (spotlight! "巷口"
        "等你挤出人群，他已经骑过了整条巷口。你只能从货栈那头追。"))
  (enter-segment! (if in-position? 0 1)))

;; ── 追击动作：每段两条路，快的伤身、稳的慢 ──────────
(define (node-chase-fast)
  (cond
    ((= seg 0)
     (node "翻过那辆推车"
       :subtitle "力量；坏：冷静 −1，中：+1 格，好：+2 格"
       :clocks (list (chase-clk 'render-data))
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome "踩翻了一摞碗" (lambda () (spend-composure! 1)))
         (outcome "翻了过去" (lambda () (chase-clk 'tick!)))
         (outcome "落地就在他后轮边上" (lambda () (clock-tick-n! chase-clk 2))))))
    ((= seg 1)
     (node "跟进那条黑巷"
       :subtitle "敏锐；坏：冷静 −1，中：+1 格，好：+2 格。看不见路，但这是最短的一条"
       :clocks (list (chase-clk 'render-data))
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome "撞在没看见的货堆上" (lambda () (spend-composure! 1)))
         (outcome "摸着墙跟上了" (lambda () (chase-clk 'tick!)))
         (outcome "从巷子另一头贴上了他" (lambda () (clock-tick-n! chase-clk 2))))))
    ((= seg 2)
     (node "扑上去"
       :subtitle "力量；坏：受伤，中：+1 格，好：+2 格"
       :clocks (list (chase-clk 'render-data))
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome "扑空了，肩膀先着地" (lambda () (injure!)))
         (outcome "拽住了他" (lambda () (chase-clk 'tick!)))
         (outcome "把车整个掀了" (lambda () (clock-tick-n! chase-clk 2))))))
    (else (error "交割：未知追击动作"))))

(define (node-chase-safe)
  (cond
    ((= seg 0)
     (node "绕过去"
       :subtitle "敏锐；慢，但干净。坏：无，中：+1 格，好：+1 格"
       :clocks (list (chase-clk 'render-data))
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome "绕远了半条街" (lambda () #f))
         (outcome "从摊子侧面绕了出去" (lambda () (chase-clk 'tick!)))
         (outcome "绕出去时他还在原地拐弯" (lambda () (chase-clk 'tick!))))))
    ((= seg 1)
     (node "贴着货堆推进"
       :subtitle "力量；稳。坏：冷静 −1，中：+1 格，好：+1 格"
       :clocks (list (chase-clk 'render-data))
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome "挤在两摞货中间动不了" (lambda () (spend-composure! 1)))
         (outcome "推开一条道" (lambda () (chase-clk 'tick!)))
         (outcome "一路推到了空地上" (lambda () (chase-clk 'tick!))))))
    ((= seg 2)
     (node "喊住他"
       :subtitle "交际；坏：冷静 −1，中：+1 格，好：+2 格。整条街都会记得今晚是谁在这儿喊"
       :clocks (list (chase-clk 'render-data))
       :requires (list (req-die))
       :resolve (roll 'social
         (outcome "没人回头，他也没有" (lambda () (spend-composure! 1)))
         (outcome "他迟疑了一下" (lambda () (chase-clk 'tick!)))
         (outcome "前面有人替你拦了半步" (lambda () (clock-tick-n! chase-clk 2))))))
    (else (error "交割：未知追击动作"))))

;; 8 格的兑现落在这儿，而且是**你手里多一张别人没有的牌，由你决定打不打**，
;; 不是系统自动送你跳过一段。玩家自己按下去的兑现，比自动生效的兑现记得住。
;; 它一手抵得上一整段街景——这就是那两天蹲出来的东西。
(define (node-side-door)
  (node "从货栈边门包抄"
    :subtitle "敏锐；只有摸熟这一片才知道这扇门。坏：冷静 −1，中：拦下他 +2，好：+3"
    :clocks (list (catch-clk 'render-data))
       :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "锁舌今晚偏偏是好的" (lambda () (spend-composure! 1)))
      (outcome "从边门穿了出去，正好在他前面" (lambda () (catch+ 2)))
      (outcome "他冲出货栈时，你已经站在路当中" (lambda () (catch+ 3))))))

;; ── 机会动作：全部指向散掉的钱 ──────────────────────
;; 它们不帮你拦人，只帮你把钱捡回来。两条轨道不互相换算——
;; 这一场问的就是「今晚你要人还是要钱」。
(define (node-chance)
  (cond
    ((= seg 0)
     (node "从摊子底下抢那几张"
       :subtitle "敏锐；填满：捡回散掉的钱 +2。这一颗骰子就不在拦他身上了"
       :clocks (list (chance-clk 'render-data))
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome "全被踩在脚底下" (lambda () #f))
         (outcome "抓起来几张" (lambda () (chance-clk 'tick!)))
         (outcome "连底下那一沓一起搂了出来" (lambda () (clock-tick-n! chance-clk 2))))))
    ((= seg 1)
     (node "掀开压住钱的板条箱"
       :subtitle "力量；填满：捡回散掉的钱 +2。钱吹进了货堆缝里，得自己动手"
       :clocks (list (chance-clk 'render-data))
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome "箱子纹丝不动，指甲翻了" (lambda () (spend-composure! 1)))
         (outcome "抠出几张" (lambda () (chance-clk 'tick!)))
         (outcome "整摞箱子推开，底下压着一叠" (lambda () (clock-tick-n! chance-clk 2))))))
    ((= seg 2)
     (node "在水沟里把钱捞回来"
       :subtitle "敏锐；填满：捡回散掉的钱 +2。他还在往前骑——你弯下腰的时候就追不上了"
       :clocks (list (chance-clk 'render-data))
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome "泡烂了，捞上来一把纸浆" (lambda () #f))
         (outcome "捞回几张" (lambda () (chance-clk 'tick!)))
         (outcome "赶在水冲走之前全捞了上来" (lambda () (clock-tick-n! chance-clk 2))))))
    (else (error "交割：未知机会动作"))))

(define (act2-nodes)
  (append
    (list (node-runner) (node-chase-fast) (node-chase-safe))
    (if (and (= seg 1) (knows-block?)) (list (node-side-door)) '())
    (list (node-chance))))

(define (act2-clocks)
  (list (catch-clk 'render-data)
        (money-clk 'render-data)
        (chase-clk 'render-data)
        (chance-clk 'render-data)
        (list 'clock "还剩几段街" (- 3 seg) 3 'countdown
              "每结束一个回合就永久驶过一段。没填满的对象随街景消失。")))

;; ── 段末结算：局部进度各自折进自己那条轨道 ──────────
(define (resolve-segment!)
  (if (chase-clk 'full?) (catch+ 2) #f)
  (if (chance-clk 'full?) (money+ 2) #f))

;; ============================================================
;; 结算
;; ============================================================

(define (caught?) (catch-clk 'full?))
(define (recovered-money) (* money-per-grid (money-clk 'current)))

;; 拦下他之后的那一下不掷骰：你按住了一个不想被按住的人，挨一记是既定代价，
;; 不是又一次运气。玩家用三段街景赢来的东西，不该被最后一掷推翻。
(define (play-scuffle!)
  (play-dialogue!
    (line "主角" "下来。")
    (line "取信人" "放手——我什么都不知道，我什么都不知道！")
    (line "世界" "他先动的手。车倒在两个人中间，你的肋下结结实实挨了一记车把。")
    (line "主角" "谁让你来取的？")
    (line "取信人" "一个先生。在老街的酒馆找的我，给了我钱。就这些，我不认识他。")
    (line "主角" "长什么样。")
    (line "取信人" "喝多了。穿得倒体面，袖口磨了。他给钱的时候顺手抽了两根烟给我，说留着。")
    (line "取信人" "那半包他忘在我这儿了。你拿去，别打了。"))
  (injure!))

(define (finish!)
  (if finished?
      #f
      (begin
        (set! finished? #t)
        (if (caught?) (play-scuffle!) #f)
        (if (> (recovered-money) 0)
            (add-item! "金钱" (recovered-money))
            #f)
        (end-encounter
          (list (if (caught?) '拦下 '跟丢) (recovered-money))))))

;; ============================================================
;; 回合推进
;; ============================================================

;; 第一幕：窗口在关。走完就是信封被取走了，你没看见是谁——照样进第二幕。
(define-turn-rule "邮箱前的一个钟头"
  (lambda () (and (= act 1) (not finished?)))
  (lambda ()
    (set! stakeout-left (- stakeout-left 1))
    (if (<= stakeout-left 0)
        (begin
          (play-dialogue!
            (line "世界" "面还没吃完，邮箱的盖子响了一下。")
            (line "世界" "等你抬头，只看见一个背影蹬上车，拐进了巷口。"))
          (set! lost-first-segment? #t)
          (begin-chase! #f))
        #f)))

;; 第二幕：移动停不下来。每结束一个回合就驶过一段街景，未填满的局部进度作废。
(define-turn-rule "驶过这一段街景"
  (lambda () (and (= act 2) (not finished?)))
  (lambda ()
    (resolve-segment!)
    (if (= seg 2)
        (finish!)
        (enter-segment! (+ seg 1)))))

;; 拦下他提前满格就不必跑完三段——但这一段没捡的钱也就跟着没了。
(define-rule "拦下即结束"
  (lambda () (and (= act 2) (not finished?)))
  (lambda ()
    (if (catch-clk 'full?)
        (begin (resolve-segment!) (finish!))
        #f)))

;; ============================================================
;; 渲染
;; ============================================================

;; 他是场上唯一的人，也是结算时唯一会开口的人——台词要有锚点，
;; 这张卡就是那个锚点（说话人只解析队员与场景节点名）。
;; 早先它是个空容器：点开什么也没有，白占一张卡。现在它是一张观察卡，
;; 身上挂着主钟「拦下他」——你追的那个东西和你离他还有多远，写在同一张卡上。
(define (runner-look n)
  (cond
    ((= n 0) "他一条腿蹬在地上，车头已经拐向巷子。邮袋是空的——真正的邮袋不会那么轻。")
    ((= n 1) "他在货堆之间拐来拐去，像是走过很多遍。车筐里的纸一路往外飘。")
    ((= n 2) "他开始喘了。后街是直的，没有第二个岔口——这是最后一段。")
    (else (error "交割：未知街景"))))

(define (node-runner)
  (node "取信人"
    :subtitle "穿着不合身的邮差制服，骑一辆不属于他的车"
    :clocks (list (catch-clk 'render-data))
    :resolve (observe (runner-look seg))))

(define (get-render-data)
  (if (= act 1)
      (container-with-clocks "交割：斜对过的面摊" (act1-nodes) (act1-clocks))
      (container-with-clocks (string-append "交割：" (seg-name seg))
                             (act2-nodes) (act2-clocks))))
