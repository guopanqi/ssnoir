;; 独立岗位委派交锋；不接管《勒索信》的城市结算。
;; 两名场景帮手各占一个岗位，不入队、不发骰、不在场内重新部署。
;; 阶段：部署 → 辨认 → 一回合拦截 →（有追踪接应才保留的）追逐。
;; 回传 (结果 部署)：截住取信者 / 追到线索 / 跟丢 / 倒下。
;; 成功或失败立即回传；最终协同对白在切场前播放，过程汇报使用空间 Bubble。
(define 阶段 '部署)
(define 已结束? #f)
(define 部署 '()) ; (("山姆" 观察) ("诺拉" 追踪))
(define 已排除 '())
(define 观察报告 "每回合随机观察一名未排除的对象，推进1格。")
(define 街段 0)
(define 候选人
  (list
    (list "背邮袋的老头" "旧车铃响着，他沿街和熟人点头" #f "勒索信-街沿")
    (list "穿制服的年轻人" "制服不合身，停在邮箱前迟迟不走" #t "勒索信")
    (list "门口抽烟的人" "靠在货栈门口，留意里面搬货的人" #f "勒索信-货栈门口")))
(define 观察钟
  (map (lambda (c)
    (list (car c) (make-clock (string-append "看明白：" (car c)) 4 'gauge
      "满4确认此人是否取信者；尼尔与观察帮手共享进展。"))) 候选人))
(define 拦截 (make-clock "截住取信者" 4 'gauge
  "本回合满4截住；独自好2中1，协同好3中2；坏不推进。"))
(define 追上 (make-clock "追上他" 6 'gauge "满6追到线索；尼尔仍须投入行动骰。"))
(define 逃脱 (make-clock "逃脱" 3 'gauge "追逐中每回合加1并换街景；满3跟丢。"))

(define (岗位有人? job)
  (not (null? (filter (lambda (entry) (eq? (cadr entry) job)) 部署))))
(define (岗位人员 job)
  (let ((entries (filter (lambda (entry) (eq? (cadr entry) job)) 部署)))
    (if (null? entries) "无人" (caar entries))))
(define (下位帮手) (if (null? 部署) "山姆" "诺拉"))
(define (观察钟于 c)
  (let ((clk (assoc-get 观察钟 (car c) #f)))
    (if clk clk (error "雨夜收网：未知观察对象"))))
(define (待辨认对象)
  (filter (lambda (c) (not (member? (car c) 已排除))) 候选人))
(define (要求阶段! expected)
  (if (and (not 已结束?) (eq? 阶段 expected)) #t
    (error "雨夜收网：当前阶段不能执行这个行动")))
(define (结束! reason title text)
  (if 已结束? (error "雨夜收网：重复结算") #t)
  (set! 已结束? #t)
  (spotlight! title text)
  (end-encounter (list reason 部署)))

(define (委派! job)
  (要求阶段! '部署)
  (if (and (< (length 部署) 2) (member? job (list '观察 '拦截 '追踪))
           (not (岗位有人? job))) #t
    (error "雨夜收网：岗位重复或委派配置错误"))
  (let ((name (下位帮手)))
    (set! 部署 (append 部署 (list (list name job))))
    (result-supplement! (string-append name "负责"
      (cond ((eq? job '观察) "观察") ((eq? job '拦截) "拦截") (else "追踪"))))
    (play-bubble! (line name
      (cond ((eq? job '观察) "我在报摊盯着，有动静就告诉你。")
            ((eq? job '拦截) "我守住这边。你一动手，我就出来。")
            (else "街口交给我，跑了也有人跟。")))))
  (if (= (length 部署) 2)
    (begin
      (set! 阶段 '辨认)
      (play-dialogue! (line "世界" "两人各就各位。你坐回报摊，留意邮箱旁的人。"))) #f))

(define (观察推进! c n)
  (要求阶段! '辨认)
  (if (member? (car c) 已排除) (error "雨夜收网：重复观察已排除对象") #t)
  (let ((clk (观察钟于 c)))
    (clk 'advance! n)
    (if (clk 'full?)
      (if (caddr c)
        (begin
          (set! 阶段 '拦截)
          (play-dialogue!
            (line "世界" "年轻人左右看了一眼，伸手取出信封。就是他。")
            (line "世界" "他转身离开邮箱。现在可以截住他。"))
          ;; 与原交锋幕转一致，发一手骰，不额外推进回合。
          (refresh-encounter-dice!))
        (begin
          (set! 已排除 (cons (car c) 已排除))
          (play-dialogue! (line "世界" (string-append (car c) "与取信无关。"))))) #f)))

(define (辨认动作 c title skill text)
  (node title :anchor (cadddr c) :subtitle text
    :requires (list (req-die))
    :resolve (roll skill
      (outcome (lambda () (spend-composure! 1)))
      (outcome (lambda () (观察推进! c 1)))
      (outcome (lambda () (观察推进! c 2))))))
(define (对象节点 c)
  (let ((name (car c)))
    (node name :anchor (cadddr c) :subtitle (cadr c)
      :children (append
        (list (clock-node (string-append "观察：" name) ((观察钟于 c) 'render-data)))
        (cond
          ((equal? name "背邮袋的老头")
           (list (辨认动作 c "听那辆车铃" 'sharpness "从报纸后留意旧车的动静")
                 (辨认动作 c "听摊主搭话" 'social "听摊主怎样招呼这个熟客")))
          ((equal? name "穿制服的年轻人")
           (list (辨认动作 c "看那身制服" 'sharpness "留意制服上有没有邮袋磨痕")
                 (辨认动作 c "对照投递规矩" 'knowledge "邮差不该一直守着私人邮箱")))
          (else
           (list (辨认动作 c "看他的鞋" 'sharpness "留意他是不是刚从码头过来")
                 (辨认动作 c "等有人叫他" 'social "听货栈里有没有人招呼他"))))))))

(define (街景锚点)
  (cond ((= 街段 0) "勒索信-巷口")
        ((= 街段 1) "勒索信-货栈区")
        ((= 街段 2) "勒索信-邮局后街")
        (else (error "雨夜收网：追逐街段越界"))))
(define (失手!)
  (要求阶段! '拦截)
  (if (岗位有人? '追踪)
    (begin
      (set! 阶段 '追逐)
      (set! 街段 0)
      (play-dialogue!
        (line "世界" "他挣开你的手，钻过路边的车流。")
        (line "世界" (string-append (岗位人员 '追踪) "守在街口，指向他拐进去的巷子。"))
        (line "世界" "你没有截住他，但踪迹还在。现在追上去。"))
      (play-bubble! (line (岗位人员 '追踪) "他进巷子了！跟我来！"))
      (refresh-encounter-dice!))
    (结束! '跟丢 "没人接住踪迹"
      "他挣脱后钻进车流。街口没有接应的人，你再也找不到那身制服。")))
(define (拦截推进! cooperative? n)
  (要求阶段! '拦截)
  (拦截 'advance! n)
  (if cooperative?
    (if (拦截 'full?)
      ;; 结算即退出，末句在切场前播放；不能把 Bubble 留给已消失的人物。
      (play-dialogue! (line (岗位人员 '拦截) "这边堵住了。他跑不了了。"))
      (play-bubble! (line (岗位人员 '拦截)
        (if (= n 0) "我守着这边，再试一次！" "退路封住了，再逼近一点！")))) #f)
  (if (拦截 'full?) (截住!) #f))
(define (截住!)
  (结束! '截住取信者 "截住了"
    "你扣住他的手腕，取回信封。他说自己只是跑腿的。这个说法还得查。"))
(define (拦截动作 cooperative?)
  (node (if cooperative? "协同拦截" "独自截人") :anchor "勒索信"
    :subtitle (if cooperative? "帮手封住退路；好3、中2，坏不推进" "独自逼近；好2、中1，坏扣冷静1")
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome (lambda ()
        (if cooperative? #f (spend-composure! 1))
        (if (hospitalization-pending?) #f (拦截推进! cooperative? 0))))
      (outcome (lambda () (拦截推进! cooperative? (if cooperative? 2 1))))
      (outcome (lambda () (拦截推进! cooperative? (if cooperative? 3 2)))))))

(define (追逐推进! n)
  (要求阶段! '追逐)
  (追上 'advance! n)
  (if (追上 'full?)
    (结束! '追到线索 "追到去向"
      "你看见他拐进一家车行的后门。门牌记下了，还不知道里面等他的是谁。") #f))
(define (追逐动作 risky?)
  (node (if risky? "抄近路追" "稳住踪迹") :anchor (街景锚点)
    :subtitle (if risky? "穿过遮挡抢近路；好3、中1，坏扣冷静2" "保持视线跟进；好2、中1，坏不推进")
    :requires (list (req-die))
    :resolve (roll (if risky? 'violence 'sharpness)
      (outcome (lambda () (if risky? (spend-composure! 2) #f)))
      (outcome (lambda () (追逐推进! 1)))
      (outcome (lambda () (追逐推进! (if risky? 3 2)))))))

;; 先推进已有追逐，最后处理观察；观察确认目标的同一回合不会立刻让他逃跑。
(define-opponent-rule "取信者拉开距离"
  (lambda () (and (not 已结束?) (eq? 阶段 '追逐)))
  (lambda ()
    (逃脱 'tick!)
    (if (逃脱 'full?)
      (结束! '跟丢 "踪迹断了" "他穿过邮局后街。等你转过墙角，街上已没有人。")
      (begin
        (set! 街段 (逃脱 'current))
        (play-bubble! (line (岗位人员 '追踪)
          (if (= 街段 1) "往货栈去了，别让货堆挡住你！" "他在邮局后面！这是最后一段！")))
        (spotlight! "他还在前面" (if (= 街段 1) "他钻进货栈区，你得换条路跟上。" "到了邮局后街，这是最后一段。"))))))
(define-opponent-rule "取信者离开邮箱"
  (lambda () (and (not 已结束?) (eq? 阶段 '拦截)))
  (lambda () (失手!)))
(define-opponent-rule "观察人员留意动静"
  (lambda () (and (not 已结束?) (eq? 阶段 '辨认) (岗位有人? '观察)))
  (lambda ()
    (let ((objects (待辨认对象)))
      (if (null? objects) (error "雨夜收网：没有可观察对象却尚未确认目标") #t)
      (let ((c (random-choice objects)))
        (set! 观察报告 (string-append "正在留意" (car c) "。"))
        (观察推进! c 1)
        (play-bubble! (line (岗位人员 '观察)
          (cond ((eq? 阶段 '拦截) "就是那个穿制服的！他取走了信！")
                ((member? (car c) 已排除) (string-append (car c) "不是取信的。"))
                (else (string-append "我再盯盯" (car c) "。")))))))))

(define (on-encounter-enter)
  (play-dialogue!
    (line "世界" "雨夜，信已经放进邮箱。山姆和诺拉等你安排。")
    (line "世界" "观察帮你认人，拦截帮你拿人，追踪留住失手后的去向。")
    (line "世界" "只有两个人，三个岗位只能覆盖两个。")))
(define (on-encounter-collapse) (collapse-result (list '倒下 部署)))

(define (帮手锚点 job)
  (if (eq? 阶段 '追逐)
    (cond ((= 街段 0) "雨夜收网-接应-巷口")
          ((= 街段 1) "雨夜收网-接应-货栈区")
          ((= 街段 2) "雨夜收网-接应-邮局后街")
          (else (error "雨夜收网：接应位置越界")))
    (cond ((eq? job '观察) "雨夜收网-观察")
          ((eq? job '拦截) "雨夜收网-拦截")
          ((eq? job '追踪) "雨夜收网-追踪")
          (else (error "雨夜收网：未知帮手岗位")))))
(define (帮手节点)
  (map (lambda (entry)
    (let ((name (car entry)) (job (cadr entry)))
      ;; 节点身份直接用人物名字，Bubble 按同名节点解析空间位置。
      (at-anchor (帮手锚点 job)
        (note-node name name
          (cond
            ((eq? 阶段 '追逐) "接着踪迹，指引下一段去向。")
            ((eq? job '观察) 观察报告)
            ((eq? job '拦截) "伏在近处，等你动手时封住另一边。")
            (else "守着街口；目标挣脱时接住去向。"))))))
    (filter (lambda (entry)
      (or (not (eq? 阶段 '追逐)) (eq? (cadr entry) '追踪))) 部署)))
(define (委派节点 job title text)
  (node title :subtitle text :resolve (instant (lambda () (委派! job)))))
(define (部署节点)
  (append
    (list (note-node "标注：等待委派" (string-append "安排" (下位帮手)) "每人负责一个岗位，本场不再调动。"))
    (if (岗位有人? '观察) '() (list (委派节点 '观察 "委派观察" "每回合随机推进一个对象的辨认1格")))
    (if (岗位有人? '拦截) '() (list (委派节点 '拦截 "委派拦截" "开放更可靠的协同拦截")))
    (if (岗位有人? '追踪) '() (list (委派节点 '追踪 "委派追踪" "拦截失手后仍可进入追逐")))))
(define (get-render-data)
  (if (eq? 阶段 '追逐)
    ;; 原巷子 Stage 的根身份与分区锚点，保持原来的 Portal 转场。
    (node "巷子" :anchor (街景锚点)
      :children (append
        (帮手节点)
        (map (lambda (n) (at-anchor (街景锚点) n))
             (clock-nodes (追上 'render-data) (逃脱 'render-data)))
        (list (追逐动作 #f) (追逐动作 #t))))
    (node "勒索信-报摊" :anchor "勒索信-报摊"
      :children (append
        (帮手节点)
        (cond
          ((eq? 阶段 '部署) (部署节点))
          ((eq? 阶段 '辨认)
           (map 对象节点 (待辨认对象)))
          ((eq? 阶段 '拦截)
           (append
             (list (note-node "标注：截人窗口" "取信者正要离开"
                     "本回合连续投骰截人；结束回合未满，他就逃跑。")
                   (at-anchor "勒索信" (clock-node "钟：当场拦截" (拦截 'render-data)))
                   (拦截动作 #f))
             (if (岗位有人? '拦截) (list (拦截动作 #t)) '())))
          (else (error "雨夜收网：未知阶段")))))))
