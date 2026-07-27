;; scenes/encounters/交割.scm - 第一章·小节一「交割日」
;;
;; 主结构：位移轨道。追逐分三段街景（巷口 → 货栈区 → 邮局后街），
;; 每结束一个回合就永久驶过一段——移动本身停不下来，问题不是「能不能前进」，
;; 而是「这一段来得及制造什么影响」。每段两个局部对象共用同一批骰子，
;; 未填满的进度随街景一起消失。
;;
;; 考点：城市准备的兑现。前几天在码头投进去的骰子，在这里变成起跑位置。
;;
;; 对外契约：回传 '好 / '中 / '坏 三档之一（不是成败二元）——
;;   好：追上他，看清脸，追回一部分钱
;;   中：人跑了，但从他身上扯下了东西
;;   坏：人和钱都没留住
;; 三档都让故事往前走；失败留疤，不阻断主线。
;;
;; 城市输入（只在顶部读取一次）：
;;   码头准备 0..6 —— 每 3 格换 1 格起跑领先。

(define prep
  (let ((v (get-global '码头准备))) (if v v 0)))

(define catch-clk
  (make-clock "追上他" 6 'segments
              "三段街景结束时按这条钟结算：满 5 格以上追到人，2–4 格只能扯下点东西，1 格以下人和钱都没了。"))

(catch-clk 'set! (quotient prep 3))

(define seg 0)
(define finished? #f)
(define face-seen? #f)
(define chase-clk #f)
(define chance-clk #f)

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

(define (catch+ n) (clock-tick-n! catch-clk n))

;; ============================================================
;; 三段街景
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
    ((= n 1) "抄货堆翻过去")
    ((= n 2) "扑上去")
    (else (error "交割：未知追击对象"))))

(define (chance-name n)
  (cond
    ((= n 0) "记住他的样子")
    ((= n 1) "让搬运工挡住他")
    ((= n 2) "抓住车后架")
    (else (error "交割：未知机会对象"))))

(define (enter-segment! n)
  (set! seg n)
  (set! chase-clk
        (make-clock (chase-name n) 3 'segments
                    "填满：追上他 +2。这一段驶过之后，没填满的进度就没了。"))
  (set! chance-clk
        (make-clock (chance-name n) 2 'segments
                    "填满：追上他 +1，另外还留下点别的。这一段驶过之后作废。")))

;; ── 追击动作（每段一个，技能各不相同）──────────────
(define (node-chase)
  (cond
    ((= seg 0)
     (node "咬住他的车"
       :subtitle "力量；坏：冷静 −1，中：+1 格，好：+2 格"
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome "他甩开了半条街" "自行车拐进积水，泥点全甩在你脸上。你慢了一步。"
           (lambda () (spend-composure! 1)))
         (outcome "跟住了" "你贴着墙根跑，车铃声始终在前面十几步。"
           (lambda () (chase-clk 'tick!)))
         (outcome "抓到了后轮" "你抄近路截在巷子另一头，手指擦到了后轮的挡泥板。"
           (lambda () (clock-tick-n! chase-clk 2))))))
    ((= seg 1)
     (node "抄货堆翻过去"
       :subtitle "敏锐；坏：冷静 −1，中：+1 格，好：+2 格"
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome "跳板断了" "跳板从中间裂开，你半条腿陷进货箱缝里。"
           (lambda () (spend-composure! 1)))
         (outcome "翻了过去" "你踩着麻袋堆翻过去，落地时他还在视野里。"
           (lambda () (chase-clk 'tick!)))
         (outcome "抄到了前头" "你从货堆顶上直接跳下来，落在他要经过的那条道上。"
           (lambda () (clock-tick-n! chase-clk 2))))))
    ((= seg 2)
     (node "扑上去"
       :subtitle "力量；坏：健康 −1，中：+1 格，好：+2 格"
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome "扑空了" "你扑出去，肩膀先撞上砖墙。他的车没停。"
           (lambda () (damage-party! 1)))
         (outcome "拽住了他" "你的手扣住他的外套，两个人一起往前踉跄了几步。"
           (lambda () (chase-clk 'tick!)))
         (outcome "把车掀了" "你侧身撞过去，自行车翻倒在地，他滚了一圈才爬起来。"
           (lambda () (clock-tick-n! chase-clk 2))))))
    (else (error "交割：未知追击动作"))))

;; ── 机会动作（每段一个，填满另有所得）──────────────
(define (node-chance)
  (cond
    ((= seg 0)
     (node "记住他的样子"
       :subtitle "敏锐；填满还会让你看清他的特征"
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome "只看见黑布" "他脸上蒙着一块黑布，雨里什么也看不清。"
           (lambda () #f))
         (outcome "记住了衣着" "深色外套，裤脚沾着灰白的粉末。你把它记下了。"
           (lambda () (chance-clk 'tick!)))
         (outcome "看清了半张脸" "风把黑布掀起一角。那张脸年轻得出乎意料。"
           (lambda () (clock-tick-n! chance-clk 2))))))
    ((= seg 1)
     (node "让搬运工挡住他"
       :subtitle "交际；填满会替你拦下他半拍"
       :requires (list (req-die))
       :resolve (roll 'social
         (outcome "没人抬头" "你喊了一声。没有一个人抬头——他们只是让开了路。"
           (lambda () #f))
         (outcome "有人挪了脚" "一个搬运工往路中间挪了半步，车把不得不歪了一下。"
           (lambda () (chance-clk 'tick!)))
         (outcome "整排人堵上来" "你喊的是他们听得懂的那句话。一排推车横过了巷子。"
           (lambda () (clock-tick-n! chance-clk 2))))))
    ((= seg 2)
     (node "抓住车后架"
       :subtitle "敏锐；填满还会从他身上扯下东西"
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome "手滑了" "指尖擦过金属，什么也没抓住。"
           (lambda () #f))
         (outcome "抓住了后架" "你的手扣住车后架，被拖着跑了十几步。"
           (lambda () (chance-clk 'tick!)))
         (outcome "扯下一块布" "你抓住的是他的外套下摆。布撕开了，一角留在你手里。"
           (lambda () (clock-tick-n! chance-clk 2))))))
    (else (error "交割：未知机会动作"))))

;; ── 段末结算：把局部进度折成追击进度 ────────────────
(define (resolve-segment!)
  (if (chase-clk 'full?) (catch+ 2) #f)
  (if (chance-clk 'full?)
      (begin
        (catch+ 1)
        (if (= seg 0) (set! face-seen? #t) #f))
      #f))

;; ============================================================
;; 结算
;; ============================================================

(define (tier)
  (let ((n (catch-clk 'current)))
    (cond
      ((>= n 5) '好)
      ((>= n 2) '中)
      (else '坏))))

(define recovered-money 40)

(define (finish!)
  (if finished?
      #f
      (begin
        (set! finished? #t)
        (let ((result (tier)))
          (cond
            ((equal? result '好)
             (begin
               (add-item! "金钱" recovered-money)
               (play-dialogue!
                 (line "主角" "谁让你来取的？")
                 (line "取信人" "有人给我钱，让我把信封拿到货栈后面去。就这些。我不认识他。")
                 (line "主角" "货栈后面。哪一片？")
                 (line "取信人" "码头那边。居民区。"))
               (spotlight! "交割：按住了他"
                 (string-append "你把他按在货栈的墙上。他不是写信的人，只是收钱跑腿的。"
                                "信封里的钱追回了一部分，"
                                (number->string recovered-money)
                                " 金。他嘴里吐出来的方向，指着码头居民区。"))))
            ((equal? result '中)
             (spotlight! "交割：跟丢了"
               "他在邮局后街拐了个弯，就没了。你手里攥着从他身上扯下来的东西——一角深色的布，和沾在上面的灰白粉末。方向是码头居民区。"))
            (else
             (begin
               (damage-party! 1)
               (spotlight! "交割：人和钱都没了"
                 "自行车拐进雨里，再没出来。邮箱空了，钱也空了。你只知道他往哪个方向去——码头居民区，老街那一片。"))))
          (end-encounter result)))))

;; ============================================================
;; 回合推进
;; ============================================================

;; 移动停不下来：每结束一个回合就驶过一段街景，未填满的局部进度作废。
(define-turn-rule "驶过这一段街景"
  (lambda () (not finished?))
  (lambda ()
    (resolve-segment!)
    (if (= seg 2)
        (finish!)
        (enter-segment! (+ seg 1)))))

;; 追上他提前满格就不必跑完三段。
(define-rule "追上即结束"
  (lambda () (not finished?))
  (lambda ()
    (if (catch-clk 'full?)
        (begin (resolve-segment!) (finish!))
        #f)))

(enter-segment! 0)

;; ============================================================
;; 渲染
;; ============================================================

;; 他是场上唯一的人，也是结算时唯一会开口的人——台词要有锚点，
;; 这张卡就是那个锚点（说话人只解析队员与场景节点名）。
(define (node-runner)
  (container "取信人"
    (list (observe-action "你能看清多少"
            (if face-seen?
                "深色外套，裤脚上沾着灰白的粉末。风掀开过一次黑布——那张脸年轻得出乎意料，不像写那封信的人。"
                "一件深色外套，脸上蒙着黑布。他骑得很稳，像走过这条路很多次。")))))

(define (prep-note)
  (cond
    ((>= prep 6) "码头那几天没白跑：巷子的走向、邮箱的视野、哪条道是死的，你全知道。")
    ((>= prep 3) "你踩过一部分点，至少不至于跟丢在第一个岔口。")
    ((>= prep 1) "你对这一片只有个模糊印象。")
    (else "你对这一片一无所知——那几天你都花在别的事情上了。")))

(define (get-render-data)
  (container-with-clocks
    (string-append "交割：" (seg-name seg))
    (list
      (observe-action "你从哪儿起跑" (prep-note))
      (node-runner)
      (node-chase)
      (node-chance))
    (list (catch-clk 'render-data)
          (chase-clk 'render-data)
          (chance-clk 'render-data)
          (list 'clock "还剩几段街" (- 3 seg) 3 'countdown
                "每结束一个回合就永久驶过一段。没填满的对象随街景消失。"))))
