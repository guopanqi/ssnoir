;; scenes/encounters/交割.scm - 第一章·小节一「交割日」
;;
;; 主结构：位移轨道。追逐分三段街景（巷口 → 货栈区 → 邮局后街），
;; 每结束一个回合就永久驶过一段——移动本身停不下来，问题不是「能不能前进」，
;; 而是「这一段来得及制造什么影响」。
;;
;; 加料：多目标取舍。两个目标各有自己的钟，共用同一批骰子，三段街景填不满两根：
;;   拦下他（主）——由每段的追击动作喂。拦下了才有那场扭打，才有他身上掉出来的东西。
;;   抢回信封（副）——由每段的机会动作喂。钱在别处兑现：房租、生活、下一段路。
;; 主副之间没有换算，谁也不喂谁。玩家真正要答的是「今晚你要人还是要钱」。
;;
;; 考点：城市生活的兑现。邮务差事让玩家提前识破假邮差，等于白送一段街景的自由；
;; 在码头认识乔，则让货栈里的搬运工愿意听玩家招呼。
;;
;; 对外契约：回传 (list 人 钱)——
;;   人：'拦下 / '跟丢
;;   钱：追回的金额（0 / 20 / 40 / 60 / 80），由「抢回信封」的格数换算
;; 两个轴都不阻断主线；结算文案与小节二的起步条件由《三封信》解释。
;;
;; 城市输入（只在顶部读取一次）：
;;   识破假邮差 —— 拦下他开局领先 1 格。

(define recognized-fake?
  (let ((v (get-global '识破假邮差))) (if v v #f)))

(define catch-target 5)
(define money-target 4)
(define money-per-grid 20)

(define catch-clk
  (make-clock "拦下他" catch-target 'segments
              "主目标。填满就是把他按住了——那会打起来，你也会挨一下，但他身上的东西归你。没填满他就骑走了。"))

(define money-clk
  (make-clock "抢回信封" money-target 'segments
              "副目标。每格 20 金；信封在拉扯里散了，捡回多少算多少。这笔钱只在别处兑现，追不回来就是没了。"))

(catch-clk 'set! (if recognized-fake? 1 0))

(define seg 0)
(define finished? #f)
(define chase-clk #f)
(define chance-clk #f)

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

(define (catch+ n) (clock-tick-n! catch-clk n))
(define (money+ n) (clock-tick-n! money-clk n))

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

;; 机会动作全部指向信封：它们不帮你拦人，只帮你把钱抢回来。
;; 两条轨道不互相换算——这一场问的就是「今晚你要人还是要钱」。
(define (chance-name n)
  (cond
    ((= n 0) "扯开他的挎包")
    ((= n 1) "让搬运工截住车头")
    ((= n 2) "在雨里把钱捡回来")
    (else (error "交割：未知机会对象"))))

(define (enter-segment! n)
  (set! seg n)
  (set! chase-clk
        (make-clock (chase-name n) 3 'segments
                    "填满：拦下他 +2。这一段驶过之后，没填满的进度就没了。"))
  (set! chance-clk
        (make-clock (chance-name n) 2 'segments
                    "填满：抢回信封 +2。这一段驶过之后作废。")))

;; ── 追击动作（每段一个，技能各不相同）──────────────
(define (node-chase)
  (cond
    ((= seg 0)
     (node "咬住他的车"
       :subtitle "力量；坏：冷静 −1，中：+1 格，好：+2 格"
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome "他甩开了半条街"
           (lambda () (spend-composure! 1)))
         (outcome "跟住了"
           (lambda () (chase-clk 'tick!)))
         (outcome "抓到了后轮"
           (lambda () (clock-tick-n! chase-clk 2))))))
    ((= seg 1)
     (node "抄货堆翻过去"
       :subtitle "敏锐；坏：冷静 −1，中：+1 格，好：+2 格"
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome "跳板断了"
           (lambda () (spend-composure! 1)))
         (outcome "翻了过去"
           (lambda () (chase-clk 'tick!)))
         (outcome "抄到了前头"
           (lambda () (clock-tick-n! chase-clk 2))))))
    ((= seg 2)
     (node "扑上去"
       :subtitle "力量；坏：健康 −1，中：+1 格，好：+2 格"
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome "扑空了"
           (lambda () (damage-party! 1)))
         (outcome "拽住了他"
           (lambda () (chase-clk 'tick!)))
         (outcome "把车掀了"
           (lambda () (clock-tick-n! chase-clk 2))))))
    (else (error "交割：未知追击动作"))))

;; ── 机会动作（每段一个，填满另有所得）──────────────
(define (node-chance)
  (cond
    ((= seg 0)
     (node "扯开他的挎包"
       :subtitle "敏锐；填满：抢回信封 +2。这一颗骰子就不在拦他身上了"
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome "只扯到带子"
           (lambda () #f))
         (outcome "掀开了包盖"
           (lambda () (chance-clk 'tick!)))
         (outcome "抓出一把"
           (lambda () (clock-tick-n! chance-clk 2))))))
    ((= seg 1)
     (node "让搬运工截住车头"
       :subtitle "交际；填满：抢回信封 +2"
       :requires (list (req-die))
       :resolve (roll 'social
         (outcome "没人抬头"
           (lambda () #f))
         (outcome "有人挪了脚"
           (lambda () (chance-clk 'tick!)))
         (outcome "整排人堵上来"
           (lambda () (clock-tick-n! chance-clk 2))))))
    ((= seg 2)
     (node "在雨里把钱捡回来"
       :subtitle "敏锐；填满：抢回信封 +2。他还在往前骑——你弯下腰的时候就追不上了"
       :requires (list (req-die))
       :resolve (roll 'sharpness
         (outcome "全泡了"
           (lambda () #f))
         (outcome "捡回几张"
           (lambda () (chance-clk 'tick!)))
         (outcome "抢在水冲走之前"
           (lambda () (clock-tick-n! chance-clk 2))))))
    (else (error "交割：未知机会动作"))))

;; ── 段末结算：局部进度各自折进自己那条轨道 ──────────
;; 追击喂「拦下他」，机会喂「抢回信封」。两条互不换算。
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
  (damage-party! 1))

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

;; 移动停不下来：每结束一个回合就驶过一段街景，未填满的局部进度作废。
(define-turn-rule "驶过这一段街景"
  (lambda () (not finished?))
  (lambda ()
    (resolve-segment!)
    (if (= seg 2)
        (finish!)
        (enter-segment! (+ seg 1)))))

;; 拦下他提前满格就不必跑完三段——但这一段没捡的钱也就跟着没了。
(define-rule "拦下即结束"
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
            (cond
              ((>= (catch-clk 'current) 3)
               "你贴得够近了：制服是借来的，袖子长出一截。风掀开过一次黑布——那张脸年轻得出乎意料，不像写那封信的人。")
              ((>= (catch-clk 'current) 1)
               "一件深色外套罩在邮差制服外头，脸上蒙着黑布。他骑得很稳，像走过这条路很多次。")
              (else
               "前面只剩一个背影和一盏晃动的车灯。他和这条街上任何一个赶路的人没有分别。"))))))

(define (start-note)
  (if recognized-fake?
      "你在邮务站记住了取信时间。真正的邮差早已来过；这个人刚碰邮箱，你就开始追了。拦下他开局 +1。"
      "你直到他骑上车才意识到时间不对。追逐从零开始。"))

(define (get-render-data)
  (container-with-clocks
    (string-append "交割：" (seg-name seg))
    (list
      (observe-action "你从哪儿起跑" (start-note))
      (node-runner)
      (node-chase)
      (node-chance))
    (list (catch-clk 'render-data)
          (money-clk 'render-data)
          (chase-clk 'render-data)
          (chance-clk 'render-data)
          (list 'clock "还剩几段街" (- 3 seg) 3 'countdown
                "每结束一个回合就永久驶过一段。没填满的对象随街景消失。"))))
