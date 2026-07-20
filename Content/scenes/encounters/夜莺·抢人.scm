;; scenes/encounters/夜莺·抢人.scm - 夜莺委托线·第二场
;; 主结构:位移轨道。舞台 → 后台 → 后巷,她自动向前,一次只有当前段在场。
;; 目标不是打赢,是把她送出去。压力只有一条:领先距离每回合都在缩。
;; 对外契约:只回传 'success / 'fail;不改任何外部任务阶段。
;; 城市输入（只在顶部读取）:
;;   夜莺保护方案 - "警局" 时巡警守住舞台,整段跳过,追兵开局只有 1。
;;   资产 - "中" 表示已有公寓,后巷退路更短(阻碍 3 → 2)。

(define protection
  (let ((v (get-global '夜莺保护方案))) (if v v "无")))
(define apartment?
  (let ((v (get-global '资产))) (equal? v "中")))
(define police-opening? (equal? protection "警局"))

;; ---- 时钟 ----
;; 全场只有一条压力钟,而且是往上填的:填满 = 最坏的事发生。
;; 写成「领先距离」倒着走过一版,读起来永远要在脑子里翻一次符号;
;; 追兵逼近就是它本来的样子——格子越多越糟,和这场戏的感觉一致。
(define chase-clk
  (make-clock "追兵逼近" 6 'segments
              "唯一的压力条。回合末自动 +1;填满 = 他们在巷口截住她,她挨一下,被你背回住处,酒馆停业。上涨:回合末 +1、险路的坏结果 +1。压回:清掉一段 −2、回身拦一把 −1/−2、机关生效 −1。"))

(define case-clk
  (make-clock "她的箱子" 2 'segments
              "可选。她攒了七年的东西,就那一只箱子。丢下不影响脱身;带走了,后面的日子里她会提起它。"))

(define obstacle-clk #f)   ; 当前段的阻碍,换段时整只换掉

;; ---- 三段轨道 ----
;; 结果表全场统一,每段只换皮:换动词、换技能、换机关。

(define segment 0)         ; 0 舞台 / 1 后台 / 2 后巷 / 3 已送出
(define trap-armed? #f)    ; 本段的机关是否已经布下
(define trap-used? #f)     ; 本段的机关是否已经用掉(一段一个)

(define (segment-name)
  (cond ((= segment 0) "舞台")
        ((= segment 1) "后台")
        ((= segment 2) "后巷")
        (#t "外面")))

(define (obstacle-label)
  (cond ((= segment 0) "打手压住前门")
        ((= segment 1) "客人的混乱会暴露她")
        (#t "后门被堵死")))

(define (obstacle-max)
  (if (and (= segment 2) apartment?) 2 3))

(define (obstacle-note)
  (cond
    ((= segment 0) "清空后她离开舞台,追兵 −2。")
    ((= segment 1) "清空后她穿过后台,追兵 −2。")
    (#t (if apartment?
            "你的公寓就在巷口,这一段本来就短。清空后她到家,交锋结束。"
            "清空后她走进雨里,交锋结束。"))))

;; 险路:推进快,但会往回削距离。
(define (rough-name)
  (cond ((= segment 0) "正面撞开打手")
        ((= segment 1) "打灭灯闸")
        (#t "硬撬后门")))
(define (rough-skill)
  (cond ((= segment 0) 'violence)
        ((= segment 1) 'knowledge)
        (#t 'violence)))

;; 稳路:推进慢且不会更慢,坏结果只是白扔一颗骰。
(define (safe-name)
  (cond ((= segment 0) "绕吧台翻后柜")
        ((= segment 1) "大声引开注意")
        (#t "摸出一条暗路")))
;; 稳路的技能按动作本身该考什么定,不强求三段一致:
;; 翻后柜和摸暗路都是当场的眼力与手快('sharpness 敏锐),
;; 灯闸在哪、布景架怎么倒则是你事先就得知道的事('knowledge 见识)。
(define (safe-skill)
  (cond ((= segment 0) 'sharpness)
        ((= segment 1) 'social)
        (#t 'sharpness)))

(define (trap-name)
  (cond ((= segment 0) "掀翻长桌堵门")
        ((= segment 1) "放倒布景架")
        (#t "反锁后门")))

;; ---- 数值助手 ----

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

;; chase+ 是变糟,chase- 是变好。名字和方向一致,读代码时不用翻符号。
(define (chase+ n)
  (chase-clk 'set! (min 6 (+ (chase-clk 'current) n))))
(define (chase- n)
  (chase-clk 'set! (max 0 (- (chase-clk 'current) n))))
(define (caught?)
  (>= (chase-clk 'current) 6))

(define (enter-segment! n)
  (set! segment n)
  (set! trap-armed? #f)
  (set! trap-used? #f)
  (if (< n 3)
      (set! obstacle-clk (make-clock (obstacle-label) (obstacle-max) 'segments (obstacle-note)))
      #f))

;; 首调值:追兵 2/6。四个回合的余量,清一段压回 2——不断后也能走完,但没有一次失手的空间。
(chase-clk 'set! 2)
(enter-segment! 0)

;; 阿瑟把事情提级过:巡警守住前门,舞台这一段根本没打起来,她已经在后台,
;; 追兵也还没能真正咬上来。
(if police-opening?
    (begin (chase-clk 'set! 1) (enter-segment! 1))
    #f)

;; ---- 结算 ----

(define (finish-success!)
  (spotlight! "抢人：送出去了"
    (if apartment?
        "巷口的雨把脚步声吞了。你的门在追兵拐过街角以前就关上,锁舌落下去的声音比外面的雨还响。"
        "接应的人影把她带进雨里。收账人停在后门口,没有再追,只是记住了你的脸。"))
  (end-encounter 'success))

(define (finish-fail!)
  (damage-party! 1)
  (spotlight! "抢人：在巷口被截住"
    "他们从两头合拢。她挨了一下,没出声,手还攥着你的袖子。你把她背回住处,天亮以前酒馆的灯就灭了。")
  (end-encounter 'fail))

;; 机关只在追兵经过这一段时兑现:他们还散在后面,一张桌子拦不住谁;
;; 已经咬上来了,堵一下才真的换得到时间。判据写在机关副标题里,玩家自己算值不值。
(define (resolve-trap!)
  (if (and trap-armed? (>= (chase-clk 'current) 3))
      (begin (chase- 1) (result-note! "机关生效：追兵 −1"))
      #f))

(define (advance-dialogue!)
  (cond
    ((= segment 0)
     (play-dialogue!
       (line "夜莺" "他们把前门堵死了。")
       (line "主角" "那就不走前门。贴着幕布,别回头。")))
    (#t
     (play-dialogue!
       (line "夜莺" "后面是雨。")
       (line "主角" "雨好。雨里没人认得脚步声。")))))

(define (advance-segment!)
  (advance-dialogue!)
  (resolve-trap!)
  (chase- 2)
  (enter-segment! (+ segment 1)))

;; 回合末:她先走(如果这一段清干净了),追兵再统一压近一格。
;; 顺序有意义——机关的"追兵 ≥3"判在这一格之前,写清楚才不会让玩家算错保险。
(define-turn-rule "追兵继续压近"
  (lambda () (< segment 3))
  (lambda ()
    (if (obstacle-clk 'full?) (advance-segment!) #f)
    (if (= segment 3)
        (finish-success!)
        (begin
          (chase+ 1)
          (if (caught?) (finish-fail!) #f)))))

(define-rule "追兵咬住"
  (lambda () (and (< segment 3) (caught?)))
  (lambda () (finish-fail!)))

;; ---- 动作 ----

(define (node-rough)
  (node (rough-name)
    :subtitle "险路：推得快，砸了就把追兵放近一格"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll (rough-skill)
      (outcome "反被逼退" "你被顶回半步，落地的东西比你想的响。"
        (lambda () (chase+ 1) (spend-composure! 1)))
      (outcome "撕开一道口子" "不漂亮，但那道口子够她侧身。"
        (lambda () (obstacle-clk 'tick!)))
      (outcome "整个塌下去" "挡路的东西一起倒向另一边。前面一下就空了。"
        (lambda () (clock-tick-n! obstacle-clk 2))))))

(define (node-safe)
  (node (safe-name)
    :subtitle "稳路：最多不进不退，绝不把追兵放近"
    :tags (list "低风险")
    :requires (list (req-die))
    :resolve (roll (safe-skill)
      (outcome "白忙一趟" "没人上当。这一下什么都没换来。"
        (lambda () #f))
      (outcome "挪开一点" "只挪开一点，但确实挪开了。"
        (lambda () (obstacle-clk 'tick!)))
      (outcome "挪开一点" "干净利落，只是这条路本来就快不了。"
        (lambda () (obstacle-clk 'tick!))))))

(define (node-rearguard)
  (node "回身拦一把"
    :subtitle "不推进这一段；只把追兵按回去"
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "没拦住" "你判断错了领头的人。他从另一边绕了过去。"
        (lambda () (spend-composure! 1)))
      (outcome "绊住半步" "他们的队形慢了下来。"
        (lambda () (chase- 1)))
      (outcome "整队人停下" "你把最急的那个引进死角，后面的人全被堵在他背后。"
        (lambda () (chase- 2))))))

(define (node-trap)
  (node (trap-name)
    :subtitle "不判定。布在这一段；追兵经过这里时若已逼近到 3 格以上则追兵 −1，否则白布"
    :requires (list (req-die))
    :resolve (instant
      (outcome "布好了" "你把它架在他们必经的那条线上，然后没有再回头看。"
        (lambda () (set! trap-armed? #t) (set! trap-used? #t))))))

(define (node-case)
  (node "捎上箱子"
    :subtitle "不判定。她攒了七年的东西，就那一只箱子"
    :requires (list (req-die))
    :resolve (instant
      (outcome "又拿到一件" "你从她手里接过它，她没有争，也没有道谢。"
        (lambda ()
          (case-clk 'tick!)
          (if (case-clk 'full?)
              (begin (set-global! '夜莺带走箱子 #t) (result-note! "她的箱子带全了"))
              #f))))))

;; ---- 渲染 ----
;; 节点扁平:场景根下直接挂「当前段容器 + 全程动作 + 副目标」,不做多层分组。

(define (segment-children)
  (append
    (list (node-rough) (node-safe))
    (if trap-used? '() (list (node-trap)))))

(define (node-segment)
  (container (string-append "当前：" (segment-name) "　—　" (obstacle-label))
             (segment-children)))

(define (case-nodes)
  (if (case-clk 'full?) '() (list (node-case))))

(define (scene-nodes)
  (append
    (list (node-segment) (node-rearguard))
    (case-nodes)))

(define (get-render-data)
  (container-with-clocks
    (string-append "抢人：把她送出去（" (segment-name) "）")
    (scene-nodes)
    (list (chase-clk 'render-data)
          (obstacle-clk 'render-data)
          (case-clk 'render-data))))
