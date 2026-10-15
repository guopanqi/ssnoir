;; 独立岗位委派交锋；不接管《勒索信》的城市结算。
;; 场景帮手各占一个岗位，不入队、不发骰、开始行动后不再调动。
;; 阶段：部署 → 辨认 → 一回合拦截 →（有追踪接应才保留的）追逐。
;; 回传 (结果 部署)：截住取信者 / 追到线索 / 跟丢 / 倒下。
;; 成功或失败立即回传；最终协同对白在切场前播放，过程汇报使用空间 Bubble。
(define 阶段 '部署)
(define 已结束? #f)
;; 仅确认当前布置；真正改变委派后重置，浏览岗位不会重置。
(define 已提醒待命? #f)
;; 正式入口由城市传来当前已招募人员；独立 Debug 则用原先两名测试帮手。
(define 城市人手 (get-global '雨夜收网帮手名单))
(define 帮手名单
  (if (and (list? 城市人手) (not (null? 城市人手))) 城市人手
    (list "山姆" "诺拉")))
(define 待命位置
  (let loop ((names 帮手名单) (index 0))
    (if (null? names) '()
      (cons (list (car names) (if (= index 0) "勒索信-报摊" "勒索信-街沿"))
        (loop (cdr names) (+ index 1))))))
;; 岗位定义：短名、场景标题、空间 Anchor、职责、到岗回应。
;; 名单与岗位定义决定菜单；部署只保存当前分配，不为人物组合另写分支。
(define 岗位定义
  (list
    (list '观察 (list "观察" "报摊观察" "雨夜收网-观察"
      "有一个帮手每回合帮你辨认可疑对象。" "我在报摊盯着，有动静就告诉你。"))
    (list '拦截 (list "拦截" "近处拦截" "雨夜收网-拦截"
      "认出目标后，有一个帮手协助拦截。" "我守住这边。你一动手，我就出来。"))
    (list '追踪 (list "追踪" "街口追踪" "雨夜收网-追踪"
      "假如拦截失败，不会直接失败，仍然能够追踪到他的痕迹。" "街口交给我，跑了也有人跟。"))))
(define 城市踩点 (get-global '雨夜收网踩点进度))
;; 正式路线始终保留观察；2格拦截，4格追踪。独立试演保留全部岗位。
(define 可用岗位
  (filter (lambda (entry)
    (or (not (member? "艾迪" 帮手名单))
        (eq? (car entry) '观察)
        (and (eq? (car entry) '拦截) (>= 城市踩点 2))
        (and (eq? (car entry) '追踪) (>= 城市踩点 4)))) 岗位定义))
(define (岗位数据 job)
  (let ((data (assoc-get 岗位定义 job #f)))
    (if data data (error "雨夜收网：未知岗位"))))
(define (人员岗位 name) (assoc-get 部署 name #f))
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
(define 拦截 (make-clock "截住取信者" 5 'gauge
  "本回合满5截住；独自好2中1，协同好3中2；坏不推进。"))
(define 追上 (make-clock "追上他" 8 'gauge "满8追到线索；尼尔仍须投入行动骰。"))
(define 逃脱 (make-clock "逃脱" 3 'gauge "追逐中每回合加1并换街景；满3跟丢。"))

(define (岗位有人? job)
  (not (null? (filter (lambda (entry) (eq? (cadr entry) job)) 部署))))
(define (岗位人员 job)
  (let ((entries (filter (lambda (entry) (eq? (cadr entry) job)) 部署)))
    (if (null? entries) "无人" (caar entries))))
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

(define (岗位名称 job) (car (岗位数据 job)))
(define (委派! job name)
  (要求阶段! '部署)
  (岗位数据 job)
  (if (assoc-get 可用岗位 job #f) #t (error "雨夜收网：该岗位尚未踩点发现"))
  (if (member? name 帮手名单) #t (error "雨夜收网：未知帮手"))
  (if (岗位有人? job) (error "雨夜收网：请先解除当前岗位人员") #t)
  (if (人员岗位 name) (error "雨夜收网：该人员已在其他岗位，请先解除") #t)
  (set! 部署 (append 部署 (list (list name job))))
  (set! 已提醒待命? #f)
  (play-bubble! (line name (list-ref (岗位数据 job) 4))))
(define (解除! job)
  (要求阶段! '部署)
  (岗位数据 job)
  (if (岗位有人? job) #t (error "雨夜收网：不能解除空岗位"))
  (let ((name (岗位人员 job)))
    (set! 部署 (filter (lambda (entry) (not (eq? (cadr entry) job))) 部署))
    (set! 已提醒待命? #f)
    ;; 人物节点已撤回，不向消失的空间人物发送 Bubble。
    (result-supplement! (string-append name "撤回待命"))))
(define (待命人员)
  (filter (lambda (name) (not (人员岗位 name))) 帮手名单))
(define (待命姓名)
  (let join ((names (待命人员)))
    (cond ((null? names) "") ((null? (cdr names)) (car names))
          (else (string-append (car names) "、" (join (cdr names)))))))
(define (开始行动!)
  (要求阶段! '部署)
  (if (and (not (null? (待命人员))) (not 已提醒待命?))
    (begin
      (set! 已提醒待命? #t)
      (result-supplement! "仍有帮手尚未委派"))
    (begin
      (set! 阶段 '辨认)
      (play-dialogue! (line "世界" "行动开始。你坐回报摊，留意邮箱旁的人。")))))

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
        (list (at-anchor (cadddr c)
                (clock-node (string-append "观察：" name) ((观察钟于 c) 'render-data))))
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
  ;; 真正的成果是抓住跑腿者、留下物证；信封是诱饵，没有现金奖励。
  (play-dialogue!
    (line "世界" "尼尔把取信的人堵在巷口，拿回假信封。")
    (line "尼尔" "谁叫你来取的？")
    (line "取信者" "我就是拿钱办事的。雇我的人是谁，我根本不知道。")
    (line "尼尔" "那你打算把信交给谁？")
    (line "取信者" "有人会来接。其他的我真不知道。")
    (line "世界" "尼尔从他的衣袋里翻出半包「老金牌」香烟。")
    (line "尼尔" "这种烟可不便宜。")
    (line "取信者" "不是我的，是别人给的。你要就拿走吧。"))
  (结束! '截住取信者 "截住了"
    "假信封拿回来了。跑腿者不知道雇主是谁，半包「老金牌」香烟是唯一物证。"))
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
    (begin
      (play-dialogue!
        (line "世界" "男人挤过街口，向老街跑去。尼尔紧追在后。")
        (line "世界" "男人翻过矮墙，衣袋里的烟盒掉在地上。")
        (line "世界" "等尼尔追到巷口，人已消失在交错的小巷里。")
        (line "世界" "尼尔捡起半包「老金牌」香烟。"))
      (结束! '追到线索 "追到去向"
        "取信者逃进老街的小巷，尼尔捡到了他掉落的「老金牌」香烟。"))
    #f))
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

;; 城市正式路线根据实际到场人员播集合戏；调试占位人手仍走简短中性开场。
;; 集合对白发生在部署之前，不在台词里预先替玩家决定岗位。
;; 集合戏只编排一次，人物在场时把他们的片段插入相应位置。
;; 片段返回 line 列表；以 append 合并，再一次性播放 Dialogue。
;; 以后增加人物，只补人物片段及回应，不复制整场对白。
(define (雨夜-艾迪开场)
  (if (member? "艾迪" 帮手名单)
    (append
      (list (line "世界"
        (if (member? "工友" 帮手名单)
          "雨夜。艾迪在屋檐下活动肩膀，工友站在旁边看着他。"
          "艾迪站在屋檐下，看着街对面的邮箱。")))
      (if (member? "工友" 帮手名单)
        (list
          (line "工友" "你这是干什么？")
          (line "艾迪" "活动活动。那小子什么时候来？我都快等不及了。"))
        (list
          (line "艾迪" "你说那小子会几点来？")
          (line "尼尔" "不知道。")
          (line "艾迪" "他带枪吗？")
          (line "尼尔" "不知道。")
          (line "艾迪" "咱们什么都不知道，太好了。"))))
    '()))

(define (雨夜-工友开场)
  (if (and (member? "工友" 帮手名单) (not (member? "艾迪" 帮手名单)))
    (list
      (line "世界" "工友把帽檐压低，望向街对面的邮箱。")
      (line "工友" "就是这儿？看起来跟平时没什么两样。"))
    '()))

(define (雨夜-公布计划)
  (append
    (list (line "尼尔" "我们还是按计划来，先看清楚谁去邮箱附近拿信，再按计划行动。"))
    (if (and (member? "艾迪" 帮手名单) (member? "工友" 帮手名单))
      (list (line "艾迪" "我知道，我知道。你给个信号，我就出来。"))
      '())
    (if (and (member? "工友" 帮手名单) (not (member? "艾迪" 帮手名单)))
      (list (line "工友" "我要在哪儿待着？"))
      '())
    (list (line "尼尔"
      (cond
        ((and (member? "艾迪" 帮手名单) (member? "工友" 帮手名单))
          "我先安排位置，你们在自己的位置上，看我的信号行事。")
        ((member? "工友" 帮手名单)
          "我去报摊那坐着，你一会听我安排。")
        (else
          "站好位置。剩下的等我的信号。"))))))

(define (雨夜-帮手回应)
  (append
    (if (member? "艾迪" 帮手名单)
      (list (line "艾迪" (if (member? "工友" 帮手名单) "太好了。" "好嘞。")))
      '())
    (if (member? "工友" 帮手名单)
      (list (line "工友" (if (member? "艾迪" 帮手名单) "好。" "行，我听你的。")))
      '())))

(define (雨夜-集合对白)
  (if (or (member? "艾迪" 帮手名单) (member? "工友" 帮手名单))
    (append
      (雨夜-艾迪开场)
      (雨夜-工友开场)
      (雨夜-公布计划)
      (雨夜-帮手回应))
    ;; 独立 Debug 的测试帮手仍可进入，不使用艾迪或工友的戏。
    (list (line "世界" "雨夜，信已经放进邮箱。先安排人员，再开始行动。"))))

(define (on-encounter-enter)
  ;; 冲印记录使符合描述的目标更容易辨认，但真正确认仍要看他取信。
  (if (get-global '照片取件特征已知)
    (begin
      ((观察钟于 (cadr 候选人)) 'load! 2)
      (play-dialogue!
        (line "尼尔" "照相馆说过：染金发、制服不合身。")
        (line "尼尔" "那个人看着像，但我得亲眼看到他取信。")))
    #f)
  (apply play-dialogue! (雨夜-集合对白)))
(define (on-encounter-collapse) (collapse-result (list '倒下 部署)))

(define (帮手锚点 job)
  (if (eq? 阶段 '追逐)
    (cond ((= 街段 0) "雨夜收网-接应-巷口")
          ((= 街段 1) "雨夜收网-接应-货栈区")
          ((= 街段 2) "雨夜收网-接应-邮局后街")
          (else (error "雨夜收网：接应位置越界")))
    (caddr (岗位数据 job))))
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
(define (委派卡 job name)
  (let ((current (人员岗位 name)))
    (node (string-append name "→" (岗位名称 job) "位")
      :anchor (帮手锚点 job)
      :subtitle (if current
        (string-append "已在" (cadr (岗位数据 current)) "；请先解除") "委派")
      :button-text "委派"
      :disabled (not (equal? current #f))
      :resolve (instant (lambda () (委派! job name))))))
(define (岗位节点 job)
  (let ((data (岗位数据 job)))
    (node (cadr data) :anchor (帮手锚点 job)
      :subtitle (if (岗位有人? job)
        (string-append "当前" (岗位名称 job) "位置：" (岗位人员 job)) "空岗，选择一人委派")
      :children (append
        (list (at-anchor (帮手锚点 job)
          (note-node (string-append "标注：" (cadr data))
            (if (岗位有人? job)
              (string-append "当前" (岗位名称 job) "位置：" (岗位人员 job)) (cadr data))
            (list-ref data 3))))
        (if (岗位有人? job)
          (list (node (string-append "解除" (岗位名称 job))
            :anchor (帮手锚点 job)
            :subtitle (string-append (岗位人员 job) "撤回待命")
            :button-text "解除"
            :resolve (instant (lambda () (解除! job)))))
          (map (lambda (name) (委派卡 job name)) 帮手名单))))))
(define (筹备人物节点)
  (map (lambda (name)
    (let ((job (人员岗位 name)))
      (at-anchor (if job (帮手锚点 job)
        (let ((anchor (assoc-get 待命位置 name #f)))
          (if anchor anchor (error "雨夜收网：帮手缺少待命锚点"))))
        (note-node name name
          (if job (string-append "负责" (岗位名称 job)) "待命，尚未委派"))))) 帮手名单))
(define (部署节点)
  (append
    (map (lambda (entry) (岗位节点 (car entry))) 可用岗位)
    (list (node "委派人员" :anchor "勒索信-报摊"
      :title (if 已提醒待命? "仍有人待命" "委派人员")
      :subtitle (cond
        (已提醒待命? (string-append (待命姓名) "尚未委派；仍要开始吗？"))
        ((null? (待命人员)) "人员已就位；开始后进入蹲守阶段")
        (else "当前为委派阶段；点岗位安排人手，再开始行动"))
      :button-text (if 已提醒待命? "仍然开始" "开始行动")
      :resolve (instant 开始行动!)))))
(define (get-render-data)
  (if (eq? 阶段 '部署)
    (node "雨夜收网-布置" :anchor "雨夜收网-布置"
      :children (append (筹备人物节点) (部署节点)))
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
          ((eq? 阶段 '辨认)
           (map 对象节点 (待辨认对象)))
          ((eq? 阶段 '拦截)
           (append
             (list (note-node "标注：截人窗口" "取信者正要离开"
                     "本回合连续投骰截人；结束回合未满，他就逃跑。")
                   (at-anchor "勒索信" (clock-node "钟：当场拦截" (拦截 'render-data)))
                   (拦截动作 #f))
             (if (岗位有人? '拦截) (list (拦截动作 #t)) '())))
          (else (error "雨夜收网：未知阶段"))))))))
