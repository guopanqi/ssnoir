;; engine.scm - Game and DSL Engine Definitions

;; Schemy does not treat colon-prefixed names as self-evaluating keywords.
(define :clocks ':clocks)
(define :children ':children)
(define :effect ':effect)
(define :requires ':requires)
(define :resolve ':resolve)
(define :tags ':tags)
(define :subtitle ':subtitle)
(define :disabled ':disabled)
(define :anchor ':anchor)
(define :place ':place)
(define :carry-item ':carry-item)
(define :support ':support)
(define :arrivals ':arrivals)
(define :kind ':kind)
(define :primary ':primary)
(define :status ':status)
(define :now ':now)
(define :where ':where)
(define :log ':log)
(define :steps ':steps)
;; 台词的舞台指示
(define :pose ':pose)
(define :move ':move)
(define :light ':light)
(define :shake ':shake)
(define :screen ':screen)
(define :inner ':inner)
(define :other ':other)

;; Helper to extract keyword arguments from a list
(define (get-kwarg kwargs key default)
  (if (null? kwargs)
      default
      (if (null? (cdr kwargs))
          default
          (if (equal? (car kwargs) key)
              (cadr kwargs)
              (get-kwarg (cdr (cdr kwargs)) key default)))))

;; node constructor
;; Returns a node expression consumed by NodeConverter.
(define (node name . kwargs)
  (let ((anchor-name (get-kwarg kwargs ':anchor #f))
        (is-place    (get-kwarg kwargs ':place #f))
        (carry-item  (get-kwarg kwargs ':carry-item #f))
        (support     (get-kwarg kwargs ':support #f))
        (arrivals    (get-kwarg kwargs ':arrivals '())))
    (append
      (list 'node
            name
            :subtitle (get-kwarg kwargs ':subtitle "")
            :clocks (get-kwarg kwargs ':clocks '())
            :children (get-kwarg kwargs ':children '())
            :requires (get-kwarg kwargs ':requires #f)
            :resolve (get-kwarg kwargs ':resolve #f)
            :tags (get-kwarg kwargs ':tags '())
            :disabled (get-kwarg kwargs ':disabled #f))
      (if (equal? anchor-name #f)
          '()
          (list :anchor anchor-name))
      (if (equal? is-place #f)
          '()
          (list :place #t))
      (if (equal? carry-item #f)
          '()
          (list :carry-item carry-item))
      (if (equal? support #f)
          '()
          (list :support support))
      (if (null? arrivals)
          '()
          (list :arrivals arrivals)))))

;; ── 休息阻塞 ─────────────────────────────────────
;; 注册表只存在于当前解释器。world-load! 会先清空，再由各地点按存档状态同步。
(define rest-blockers '())

(define (remove-rest-blocker entries id)
  (if (null? entries)
      '()
      (if (equal? (car (car entries)) id)
          (remove-rest-blocker (cdr entries) id)
          (cons (car entries) (remove-rest-blocker (cdr entries) id)))))

(define (rest-block! id reason . target)
  (if (not (string? id)) (error "rest-block!: id must be a string") #t)
  (if (not (string? reason)) (error "rest-block!: reason must be a string") #t)
  (if (equal? id "") (error "rest-block!: id cannot be empty") #t)
  (if (equal? reason "") (error "rest-block!: reason cannot be empty") #t)
  (if (and (not (null? target)) (not (= (length target) 2)))
      (error "rest-block!: expected no target, or location and target-node")
      #t)
  (if (null? target)
      (set! rest-blockers
            (cons (list id reason) (remove-rest-blocker rest-blockers id)))
      (let ((location (car target)) (target-node (cadr target)))
        (if (not (string? location)) (error "rest-block!: location must be a string") #t)
        (if (not (string? target-node)) (error "rest-block!: target-node must be a string") #t)
        (if (equal? location "") (error "rest-block!: location cannot be empty") #t)
        (if (equal? target-node "") (error "rest-block!: target-node cannot be empty") #t)
        (set! rest-blockers
              (cons (list id reason location target-node) (remove-rest-blocker rest-blockers id)))
        (__register-rest-block! id reason location target-node))))

(define (rest-release! id)
  (if (not (string? id)) (error "rest-release!: id must be a string") #t)
  (set! rest-blockers (remove-rest-blocker rest-blockers id))
  (__release-rest-block! id))

(define (rest-blocked?)
  (not (null? rest-blockers)))

(define (rest-block-reasons)
  (map cadr (reverse rest-blockers)))

(define (clear-rest-blockers!)
  (set! rest-blockers '())
  (__clear-rest-blockers!))

(define (end-turn!)
  (if (rest-blocked?)
      (error "end-turn!: required events remain unresolved")
      (__end-turn!)))

;; 交锋内部的阶段切换可以换一手骰，但不把它算作一次休息：不推进回合规则，
;; 也不收取时间代价。仅交锋解释器注册了这条桥接。
(define (refresh-encounter-dice!)
  (__refresh-encounter-dice!))

;; 回合边界上由场景发起的强制行动。demands 是 ((actor-id count) ...)。
;; 它不产生新的玩家骰槽；只在内容内部指定谁的时间被占用，并排入一张锁输入的自动行动卡。
;; 落点必须显式声明：自动行动卡是空间里的一张卡，没有"先放网格以后再说"——
;; 漏写锚点会静默掉进网格，而构建与校验都不会拦，只能在这里挡。
(define (auto-action! name subtitle demands effect anchor)
  (if (not (string? anchor))
      (error "auto-action!: 必须显式声明落点锚点；自动行动卡不许掉进网格")
      (__auto-action! name subtitle anchor demands effect)))

;; 倒下协议。交锋不写新的送医路线，只声明这场在主角倒下时如何使用已有结算：
;;   (collapse-result 原有结果)  调用原有回调，按失败/既有收场推进
;;   (collapse-retry)            不调用回调，保留城市故事状态，出院后可重来
;; 每个交锋必须重定义 on-encounter-collapse；漏写时在真正倒下处直接报配置错误。
(define (hospitalization-pending?)
  (__hospitalization-pending?))

(define (collapse-result result)
  (list 'collapse-result result))

(define (collapse-retry)
  (list 'collapse-retry))

(define (on-encounter-collapse)
  (error "交锋没有声明 on-encounter-collapse：请返回 collapse-result 或 collapse-retry"))

;; Action constructors
(define (instant effect)
  (list 'instant effect))

;; 一个结算分支＝一个效果，仅此而已：
;;
;;   (outcome (lambda () ...))
;;
;; 它没有标题、没有文案。结果条上的每一行都由引擎按状态变化自动写（钟 / 物品 / 关系 /
;; 冷静 / 伤势），不需要、也不接受内容再复述一遍。要说的话走显式的表达：到阈值才说的用
;; banter / dialogue；引擎自动行说不出的事实（"解锁：码头账房"）才用 result-supplement!。
(define (outcome effect)
  (if (procedure? effect)
      (list 'outcome effect)
      (error "outcome: 只收一个效果过程 (outcome (lambda () ...))；不写标题、不写描述")))

(define (outcome? value)
  (and (pair? value)
       (= (length value) 2)
       (equal? (car value) 'outcome)))

(define (require-outcome value who)
  (if (outcome? value)
      value
      (error (string-append who ": expected outcome"))))

;; 在原效果之后追加一个效果。
(define (outcome-append-effect value extra-effect who)
  (let ((effect (cadr (require-outcome value who))))
    (outcome (lambda () (effect) (extra-effect)))))

;; Modifier constructor
(define (modifier value reason)
  (list 'modifier value reason))

;; Roll with optional difficulty modifier callback
;; (roll 'skill fail neutral success)         -> 4 args, no modifiers
;; (roll 'skill mod-fn fail neutral success)  -> 5 args, dynamic modifiers
(define (roll skill . branches)
  (if (= (length branches) 4)
      (list 'roll skill (car branches) (cadr branches) (caddr branches) (cadddr branches))
      (if (= (length branches) 3)
          (list 'roll skill (lambda () '()) (car branches) (cadr branches) (caddr branches))
          (error "roll: expected 4 args (skill fail neutral success) or 5 args (skill mod-fn fail neutral success)"))))

;; 恢复性判定与 roll 同构，节点类型不同只为内容语义区分，结算规则一致。
;; (recovery-roll 'skill fail neutral success)         -> 无修正
;; (recovery-roll 'skill mod-fn fail neutral success)  -> 动态难度修正
(define (recovery-roll skill . branches)
  (if (= (length branches) 4)
      (list 'recovery-roll skill (car branches) (cadr branches) (caddr branches) (cadddr branches))
      (if (= (length branches) 3)
          (list 'recovery-roll skill (lambda () '()) (car branches) (cadr branches) (caddr branches))
          (error "recovery-roll: expected 4 args (skill fail neutral success) or 5 args (skill mod-fn fail neutral success)"))))

(define (observe text)
  (list 'observe text))

;; ── 标注 ─────────────────────────────────────────────────────────
;; 空间里一段不可操作的说明。它不是卡：不能点、不能查看、不能导航进去，
;; 唯一的反馈是它自己变了。玩家看到的就是漂在场景里的一行字。
;;
;;   (note "标题" "正文")              纯文字
;;   (note "标题" "正文" 钟)           文字 + 读数
;;
;; 标题、正文都可以是空串（只想要一句话就把标题留空），但不能全空。
(define (note title text . clock-data)
  (if (null? clock-data)
      (list 'note title text)
      (list 'note title text (car clock-data))))

;; 标注节点。name 只是渲染树里的身份，不会显示出来。
;;
;; 挂在哪儿由锚点决定，和动作卡同一套规则：能解析到场景锚点就浮在那一处旁边、
;; 一根线指过去；解析不到就升到画面上方的场景标注带里，说这一整场在发生什么。
;; 要显式指定锚点，直接用 node：(node "标注：值班表" :anchor "看医生" :resolve (note ...))。
(define (note-node name title text . clock-data)
  (node name :resolve (if (null? clock-data)
                          (note title text)
                          (note title text (car clock-data)))))

;; Clock resolve constructor — wraps exactly one make-clock render-data snapshot.
;; 钟就是「带读数的标注」：读数取自钟，标题取钟的 label，正文取钟的 note。
(define (clock clock-data)
  (list 'clock clock-data))

;; Clock node: a display-only annotation. name is an internal tree identity and is not rendered.
(define (clock-node name clock-data)
  (node name :resolve (clock clock-data)))

;; 将一组钟各自立为一条标注。clock-node 本身始终只接受一根钟。
(define (clock-nodes . clock-datas)
  (map (lambda (clock-data)
         ;; 前缀只供渲染树唯一性检查使用；标注不渲染节点名。
         (clock-node (string-append "钟：" (cadr clock-data)) clock-data))
       clock-datas))

;; Cost/Requirement constructors
(define (req-die)
  (list 'die))

(define (req-item name qty)
  (list 'item name qty))

;; Container & Action helpers
(define (container name children)
  (node name :children children))

(define (container-with-clocks name children clocks)
  (node name :children children :clocks clocks))

;; ── 地点 ─────────────────────────────────────────
;; 世界地点。和 container 的区别只有一条：玩家走进去这件事引擎认得，于是可以挂
;; :arrivals。只有世界根的直接子节点能是 place，交锋树里不许出现。
;; 接受 node 的全部 kwargs（家要用 :subtitle 显示住所等级）。
(define (place name . kwargs)
  (apply node (cons name (append kwargs (list :place #t)))))

;; 一拍入场叙事。没有 condition——「这一拍在不在」由拼树时决定，和 children 一样：
;;   :arrivals (if (and (= stage 4) (not told?)) (list beat) '())
;; 一次性由内容自己置标记并跟着自己的 save 走；引擎不持有任何 arrival 状态。
;; 必须当场发生的遭遇可以把 start-encounter 放在 effect 的最后：客户端会先播完
;; arrival 的 dialogue / animation，再采用已经准备好的交锋快照。end-turn! 仍然禁止。
(define (arrival id effect)
  (list 'arrival id effect))

;; ── 卷宗 ─────────────────────────────────────────
;; 一条故事线在卷宗里的样子。拥有故事的模块回一条（或零条），世界只负责收集，
;; 和地点可见性一样——世界不解释故事。
;;
;; :now 是这一小节的整体目标，一句话——不是「下一步做什么」，那是 :steps 的事。
;; 说不清整体目标就留空字符串，面板不画这一行；别拿当前子项凑一句来重复。
;;
;; :clocks 直接放故事已经在用的钟（(某某-clk 'render-data)），不为卷宗新建一套——
;; 同一根钟在动作卡上和卷宗里必须长得一模一样。
;; 主线只有一条：当前这一章的那条必经线。它排在最前，也是钉住条的默认。
;; 一个存档里同时出现两条 主线 是内容写错了，不是引擎该兼容的情形。
;;
;; 主要 / 次要与 :kind 是两个轴：:kind 说这条线是什么来头，
;; :primary 说它是不是非走不可的主轴（阻塞性、必须到场，失败也算到过）。
;; 主轴（城市主轴的短条目、三封信）显式写 :primary #t；追查与人物事件不写，
;; 缺省就是次要。不要按 :kind 推导——三封信是委托却是主要，追查是委托却是次要。
;;
;; 一张卡就是一个小节（Task）。一条人物线或一章主线拆成几张卡，一张一张了结，
;; 不做一张横跨全章、只换 :now 的长卡——那样玩家看不见小节的边界，
;; 更看不见边界上发生了什么。
;;
;; :steps 是这一小节的子项清单：(step "文案" 已完成?) 的列表，按故事顺序写死，
;; 做完的划掉。界面只露到第一个没做完的那一项，后面的不给玩家看——卷宗不知道未来；
;; 所以子项的完成条件必须按顺序单调（前一项没完成，后一项不该先完成）。子项只列这一节**必经**的事；可做可不做的留在地点动作上，
;; 不然「最后一项划掉即了结」这条约定就站不住。
;; 最后一项划掉的那一拍，内容调 complete-task! 发放这一节固定的成长，并把卡收成 了结。
;; 卡上不写「完成后得到什么」——奖励是做完之后自然到手的东西，不是挂在卡上的诱饵。
;; 小节也可能被世界推着了结（日子到了、失败了）：那时没划掉的子项就那么留着，照样了结。
(define dossier-kinds  (list '主线 '委托 '人物 '城市))
(define dossier-states (list '进行中 '等着别人 '了结))

(define (step text done?)
  (if (and (string? text) (not (equal? text ""))) #t (error "step: 文案必须是非空字符串"))
  (if (or (equal? done? #t) (equal? done? #f)) #t (error (string-append "step " text "：已完成? 应为 #t / #f")))
  (list 'step text done?))

(define (dossier id . kwargs)
  (if (and (string? id) (not (equal? id ""))) #t (error "dossier: 标识必须是非空字符串"))
  (let ((kind   (get-kwarg kwargs ':kind '委托))
        (status (get-kwarg kwargs ':status '进行中))
        (now    (get-kwarg kwargs ':now ""))
        (where  (get-kwarg kwargs ':where ""))
        (primary (get-kwarg kwargs ':primary #f)))
    (if (member? kind dossier-kinds)
        #t
        (error (string-append "dossier " id "：:kind 应为 主线 / 委托 / 人物 / 城市")))
    (if (member? status dossier-states)
        #t
        (error (string-append "dossier " id "：:status 应为 进行中 / 等着别人 / 了结")))
    (if (string? now) #t (error (string-append "dossier " id "：:now 必须是字符串")))
    (if (string? where) #t (error (string-append "dossier " id "：:where 必须是字符串")))
    (if (or (equal? primary #t) (equal? primary #f))
        #t
        (error (string-append "dossier " id "：:primary 应为 #t / #f")))
    (list 'dossier id
          :kind kind
          :primary primary
          :status status
          :now now
          :where where
          :clocks (get-kwarg kwargs ':clocks '())
          :steps (let ((steps (get-kwarg kwargs ':steps '())))
                   (map (lambda (s)
                          (if (and (list? s) (= (length s) 3) (equal? (car s) 'step))
                              s
                              (error (string-append "dossier " id "：:steps 只接受 (step 文案 已完成?)"))))
                        steps))
          :log (get-kwarg kwargs ':log '()))))

;; 一条线的履历。内容自己持有一份，跟自己的存档走。
;;
;; 由内容**显式**写一条：((某某-journal) 'add! "……")，写在现在调 spotlight! 的那些地方。
;; 不从判定结果自动抽——自动生成的日志一定啰嗦，而且会在不该有条目的地方冒出来。
;; 一拍一句，说已经发生了什么，不说接下来做什么（那是 :now 的事）。
(define (make-journal)
  (let ((entries '()))   ; 最新在前
    (lambda (msg . args)
      (cond
        ((equal? msg 'add!)
         (let ((text (car args)))
           (if (and (string? text) (not (equal? text "")))
               #t
               (error "journal 'add!：正文必须是非空字符串"))
           (set! entries (cons (list (get-global '世界日) text) entries))))
        ((equal? msg 'render-data)
         (map (lambda (e) (list 'journal-entry (car e) (cadr e))) entries))
        ((equal? msg 'empty?) (null? entries))
        ((equal? msg 'save) entries)
        ((equal? msg 'load!)
         (let ((data (car args)))
           (set! entries (if (list? data) data '()))))
        (else (error "make-journal：未知消息（'add! / 'render-data / 'empty? / 'save / 'load!）"))))))

(define (action name requires resolve)
  (node name :requires requires :resolve resolve))

(define (action-with-tags name tags requires resolve)
  (node name :tags tags :requires requires :resolve resolve))

(define (action-with-clocks name requires resolve clocks)
  (node name :requires requires :resolve resolve :clocks clocks))

;; Shorthands for simple actions
(define (instant-action name effect)
  (action name #f (instant effect)))

(define (anchored-instant-action name anchor effect)
  (node name :anchor anchor :requires #f :resolve (instant effect)))

;; 给一张已经拼好的卡补落点。只用于构造器没有 :anchor 位的写法
;; （roll-action / note-node / clock-node / encounter-action 这些），
;; 有 :anchor 位的（node / 工作 / investigation-node）直接写在构造里。
(define (at-anchor anchor node-data)
  (if (member? :anchor node-data)
      (error (string-append "at-anchor: 节点已经有落点了：" (cadr node-data)))
      (append node-data (list :anchor anchor))))

(define (instant-action-with-tags name tags effect)
  (action-with-tags name tags #f (instant effect)))

(define (encounter-action name effect)
  (instant-action-with-tags name (list "交锋") effect))

(define (observe-action name text)
  (action name #f (observe text)))

(define (roll-action name requires skill fail-outcome neutral-outcome success-outcome)
  (action name requires
    (roll skill
      (require-outcome fail-outcome "roll-action fail")
      (require-outcome neutral-outcome "roll-action neutral")
      (require-outcome success-outcome "roll-action success"))))

(define (recovery-roll-action name requires skill . branches)
  (if (= (length branches) 4)
      (action name requires
        (recovery-roll skill
          (car branches)
          (require-outcome (cadr branches) "recovery-roll-action fail")
          (require-outcome (caddr branches) "recovery-roll-action neutral")
          (require-outcome (cadddr branches) "recovery-roll-action success")))
      (if (= (length branches) 3)
          (action name requires
            (recovery-roll skill
              (require-outcome (car branches) "recovery-roll-action fail")
              (require-outcome (cadr branches) "recovery-roll-action neutral")
              (require-outcome (caddr branches) "recovery-roll-action success")))
          (error "recovery-roll-action: expected 6 args (name requires skill fail neutral success) or 7 args (name requires skill mod-fn fail neutral success)"))))

;; ── 工作（work）DSL ───────────────────────────────────
;; (工作 name risk skill 好-outcome 中-outcome 坏-outcome [subtitle] [:anchor name] [:clocks clocks])
;; (非法工作 name risk skill 好-outcome 中-outcome 坏-outcome [subtitle] [:anchor name] [:clocks clocks])
;;   工作只换钱，不攒任何圈子的声誉——「势力关系」这层已经拆掉：你和谁的关系，
;;   就是你和那一片的关系，写在人物模块里（艾迪＝码头，弗兰克＝老街，沃尔特＝上城）。
;;   risk:    只有 '低/'高，决定风险标签与结果代价。
;;   非法工作是独立维度：额外显示“非法”并固定难度 -2，不再冒充第三种风险档。
;;   好/中/坏: 每项工作显式传入三个 outcome（各只是一个效果）
;;   subtitle: 可选，只写“特别”的一句说明；一般风险由标签表达，不写 subtitle
;; 表现约定：每个工作都打“工作”标签（＝能赚钱）+ 一个风险标签，前端给风险标签配色，
;; 玩家一眼就能判断类型和大致风险。惩罚（钱/冷静/伤势）写在各 outcome effect 里。
(define (工作-风险标签 risk)
  (cond ((equal? risk '低)   "低风险")
        ((equal? risk '高)   "高风险")
        (else (error "工作: 未知风险等级（应为 低/高）"))))

(define (工作-难度修正 risk illegal?)
  (append
    (begin (工作-风险标签 risk) '())
    (if illegal? (list (modifier -2 "非法")) '())))

(define (构造工作 name illegal? risk skill 好-outcome 中-outcome 坏-outcome subtitle anchor clocks)
  (node name
        :subtitle subtitle
        :anchor anchor
        :clocks clocks
        :tags (if illegal?
                  (list "工作" (工作-风险标签 risk) "非法")
                  (list "工作" (工作-风险标签 risk)))
        :requires (list (req-die))
        :resolve (roll skill
                       (lambda () (工作-难度修正 risk illegal?))
                       (require-outcome 坏-outcome "工作 坏")
                       (require-outcome 中-outcome "工作 中")
                       (require-outcome 好-outcome "工作 好"))))

;; 工作包装只接受一条可选副标题，以及 :anchor / :clocks 两组明确关键字。
;; 未登记参数直接报错，不能静默吞掉锚点或时钟配置。
(define (工作-关键字参数合法? args)
  (cond
    ((null? args) #t)
    ((< (length args) 2) #f)
    ((equal? (car args) :anchor)
     (and (string? (cadr args)) (工作-关键字参数合法? (cddr args))))
    ((equal? (car args) :clocks)
     (and (list? (cadr args)) (工作-关键字参数合法? (cddr args))))
    (else #f)))

(define (工作-附加参数 extra)
  (let ((subtitle (if (and (not (null? extra)) (string? (car extra))) (car extra) ""))
        (kwargs (if (and (not (null? extra)) (string? (car extra))) (cdr extra) extra)))
    (if (工作-关键字参数合法? kwargs)
        (list subtitle
              (get-kwarg kwargs :anchor #f)
              (get-kwarg kwargs :clocks '()))
        (error "工作: 附加参数应为 [subtitle] [:anchor 锚点名] [:clocks 时钟列表]"))))

(define (工作 name risk skill 好-outcome 中-outcome 坏-outcome . extra)
  (let ((args (工作-附加参数 extra)))
    (构造工作 name #f risk skill 好-outcome 中-outcome 坏-outcome
              (car args) (cadr args) (caddr args))))

(define (非法工作 name risk skill 好-outcome 中-outcome 坏-outcome . extra)
  (let ((args (工作-附加参数 extra)))
    (构造工作 name #t risk skill 好-outcome 中-outcome 坏-outcome
              (car args) (cadr args) (caddr args))))

;; ── 随身动作 ─────────────────────────────────────────
;; 烟、酒这类你自己带进交锋的东西。它们不属于任何一场——没有哪个交锋脚本声明它们，
;; 是引擎在每一场的树上补一份（见 SceneManager.RebuildRenderTree）。只在手里真有那件
;; 东西的时候出现。
;;
;; 客户端把它们画成右下角的普通动作卡。物品和行动骰都是明确的消耗槽；两个槽填满后
;; 玩家再按执行，不用引线暗示物品来源，也不会在最后一个资源落槽时自动生效。
;;
;; 要投骰，但不掷骰：骰面完全不参与结算。所以这是全场唯一一处**烂骰子和好骰子等价**
;; 的地方，一颗 1 点骰投在这儿换回来的和 6 点一样多。手气差的那一轮，抽根烟不是浪费，
;; 是分诊。
(define (随身动作 name item amount title effect)
  (node name
    ;; 它属于哪件物品。客户端据此把卡留在随身区，而不是排进场上的卡片区。
    :carry-item item
    :requires (list (req-item item 1) (req-die))
    :resolve (instant
      (outcome (lambda ()
          (remove-item! item 1)
          (restore-actor-composure! 'player amount)
          (effect))))))

(define (carry-nodes)
  (append
    ;; 交锋每回合自动流失 1，一根烟买回两个回合——这是它的单位。
    (if (has-item? "香烟" 1)
        (list (随身动作 "抽烟" "香烟" 2 "抽了一口"
                (lambda () #f)))
        '())
    ;; 酒回得多，代价推到明天：下一次城市骰池里有一格带宿醉。
    (if (has-item? "酒" 1)
        (list (随身动作 "喝酒" "酒" 3 "灌了一口"
                (lambda () (apply-hangover!))))
        '())))

;; ── 关系支援 ─────────────────────────────────────────
;; 人物关系推进到某一步，给玩家一条能带进**任何一场**交锋的支援。它是独立的一层：
;; 不属于哪张行动卡，也不认识这一场的剧情钟，只操作所有交锋共有的东西——骰子、人、冷静。
;; 所以交锋脚本一行都不用改；哪一场要为它配合什么，说明动词选错了。
;;
;; 规则（第一版）：只在交锋里出现；一场只带一个（队伍的 carried support）；每场一次；
;; 不占骰、不结束回合；用过当场变灰，下一场恢复。
;; 「每场一次」由引擎在执行支援卡时消费（SceneManager.ExecuteAction），内容不写、也写不了；
;; 卡上只需 :disabled (support-used?) 让它变灰。
;;
;; 支援卡和烟酒走同一个 encounter-action-nodes 入口；它没有需求槽，因此标准动作卡
;; 直接显示执行钮。用过后卡仍保留，但明确进入禁用状态。
;;
;;   (grant-support! "弗兰克")      关系写回处调用；第一条自动成为带进交锋的那一个
;;   (has-support? "弗兰克")
;;   (set-carried-support! "弗兰克")  以后有了选人的界面再用
(define (grant-support! id) (__grant-support! id))
(define (has-support? id) (__has-support? id))
(define (set-carried-support! id) (__set-carried-support! id))
(define (carried-support) (__carried-support))
(define (support-used?) (__support-used?))

;; 支援叫来的临时帮手：入队并当场发一颗骰，本回合结束（或交锋结束）由引擎自动送走。
(define (summon-helper! actor-id name stats-alist)
  (__summon-helper! actor-id name stats-alist))

;; ── 同伴能力总表 ─────────────────────────────────────
;; 全游戏同伴与临时帮手的基础能力只在这里定义一处。
;; recruit / summon 处只许引用 (同伴能力 '夜莺)，不许现写四项。
;; 交锋是独立解释器，看不到 world/人物 模块；两个解释器都加载本文件，
;; 所以总表只能落在这里（support-catalog 同理）。
;; 每人只有一个特长：单峰，其余 0/1。场景要加强某人，用固定骰/修正/自动动作，
;; 不动这里的底值。
(define 同伴能力表
  (list (list '夜莺 (list (list 'violence 0) (list 'knowledge 1)
                         (list 'sharpness 1) (list 'social 3)))
        (list '老街帮手 (list (list 'violence 2) (list 'knowledge 0)
                             (list 'sharpness 1) (list 'social 0)))))

(define (同伴能力 id)
  (let ((row (assoc-get 同伴能力表 id #f)))
    (if row row (error (string-append "同伴能力：没有登记的同伴 "
                                      (symbol->string id))))))

;; 弗兰克《叫个人来》——「人手」这一形状：那个人的人自己入场，占一个骰位，用他自己的技能。
;; 帮手是老街的人：力量有数，交际是零。他那颗骰投在体力动作上准备值高，投在谈判上就是废骰——
;; 限制来自他的技能表，不来自规则。同伴的骰投出坏结果扣的是他的冷静，不是你的：
;; 最难看的那一下由弗兰克的人顶，这就是这条支援的意思。
(define (support-frank)
  (node "叫个人来"
    :support "弗兰克"
    :disabled (or (support-used?) (has-companion? '老街帮手))
    :resolve (instant
      (outcome (lambda ()
          (summon-helper! '老街帮手 "老街帮手" (同伴能力 '老街帮手))
          (result-supplement! "帮手入队：本回合一颗骰")
          (play-banter! (line "世界" "有人从后面应了一声，走过来站到你身边。")))))))

;; 成长面板上那一行字：这条支援叫什么、是干什么用的。加一条支援时这里和 support-nodes 各登记一次。
;; (support-info "弗兰克") → ("叫个人来" "叫一个老街的帮手来一回合……")
(define support-catalog
  (list (list "弗兰克" (list "叫个人来" "叫一个老街的帮手来一回合：多一颗骰，用他自己的技能。每场一次。"))))

(define (support-info id)
  (let ((row (assoc-get support-catalog id #f)))
    (if row row (error (string-append "support-info：没有登记的支援 " id)))))

;; 引擎在每一场交锋的树旁取一份。带了谁就出谁的卡；没带就是空。
(define (support-nodes)
  (let ((id (carried-support)))
    (cond
      ((equal? id #f) '())
      ((equal? id "弗兰克") (list (support-frank)))
      (else (error (string-append "support-nodes：没有登记的支援 " id))))))

;; 不属于具体场景、但会在每场交锋里出现的动作统一从这里注入。随身物品和人物支援只是
;; 两种出现条件；交给客户端之后都是普通动作卡，不再各自发明一套交互。
(define (encounter-action-nodes)
  (append (carry-nodes) (support-nodes)))

;; Inventory helpers
(define (get-item item-id)
  (item-count item-id))

(define (consume-item! item-id n)
  (remove-item! item-id n))

;; 罚款/赔偿用：最多扣 n，不够就扣光（不报错，floor 到 0）。
(define (spend-up-to! item-id n)
  (remove-item! item-id (min n (item-count item-id))))

;; Rule system
(define rules '())

;; define-rule registers a rule
(define (define-rule name condition action)
  (set! rules (cons (list name condition action) rules)))

;; on-action triggers all rules
(define (on-action)
  (define (run-rules list-rules)
    (if (or (null? list-rules) (hospitalization-pending?))
        #t
        (begin
          (let ((rule (car list-rules)))
            (let ((name (car rule))
                  (cond-fn (cadr rule))
                  (act-fn (caddr rule)))
              (if (cond-fn)
                  (act-fn)
                  #f)))
          (run-rules (cdr list-rules)))))
  (run-rules rules))

;; 玩家真的走进一个地点（点开地点卡 / 回家 / 退回世界层）时触发，早于该地点的入场节拍。
;; 规则只收地点名，不产生结算：它是给「你离开过这里」这类事实用的（家里的睡觉锁）。
(define enter-place-rules '())

(define (define-enter-place-rule name action)
  (set! enter-place-rules (cons (list name action) enter-place-rules)))

(define (on-enter-place place-name)
  (define (run list-rules)
    (if (null? list-rules)
        #t
        (begin
          ((cadr (car list-rules)) place-name)
          (run (cdr list-rules)))))
  (run enter-place-rules))

;; 交锋载入完成后的显式入口。交锋脚本可重定义它来安排开场演出；
;; 世界动作的 on-action 仍属于世界自己，不能借场景切换的时机去跑交锋规则。
(define (on-encounter-enter) #t)

(define turn-rules '())

;; define-turn-rule registers a turn-end rule
(define (define-turn-rule name condition action)
  (set! turn-rules (cons (list name condition action) turn-rules)))

;; on-turn-end triggers all turn-end rules
(define (on-turn-end)
  (define (run-rules list-rules)
    (if (or (null? list-rules) (hospitalization-pending?))
        #t
        (begin
          (let ((rule (car list-rules)))
            (let ((name (car rule))
                  (cond-fn (cadr rule))
                  (act-fn (caddr rule)))
              (if (cond-fn)
                  (act-fn)
                  #f)))
          (run-rules (cdr list-rules)))))
  (run-rules turn-rules))

;; 交锋回应与世界日终是两套生命周期。每条 opponent rule 是一个因果批，按书写顺序执行；
;; 后一条可以读取前一条提交后的状态。一条规则内部登记的 beat 属于同一批，不应互相依赖。
(define opponent-rules '())
(define opponent-rule-queue '())

(define (define-opponent-rule name condition action)
  (set! opponent-rules (cons (list name condition action) opponent-rules)))

(define (__begin-opponent-rules!)
  (set! opponent-rule-queue (reverse opponent-rules)))

(define (__opponent-rules-pending?)
  (not (null? opponent-rule-queue)))

(define (__run-next-opponent-rule!)
  (if (null? opponent-rule-queue)
      (error "__run-next-opponent-rule!: no pending rule")
      (let ((rule (car opponent-rule-queue)))
        (set! opponent-rule-queue (cdr opponent-rule-queue))
        (if ((cadr rule))
            ((caddr rule))
            #f))))

;; 一件玩家能够感知的对方行动。thunk 是唯一真相：其中的钟、冷静、伤势和 banter
;; 由引擎捕获成纯展示步骤，作者不再重复填写 delta。
(define (beat! anchor text thunk)
  (__opponent-beat! anchor text thunk))

;; 由日期算出来的倒计时。它**不持有格数**：格数就是「到期日减今天」，
;; 所以永远不会和日历跑偏，读档也不必恢复它——属于上面说的第一类例外
;; （渲染别处已有的真相）。有截止日的窗口一律用它，不要另开一根自己每天减一的钟。
(define (日期倒计时 标签 到期日 跨度 备注)
  (list 'clock 标签 (max 0 (- 到期日 (get-global '世界日))) 跨度 'countdown 备注))

;; 局部整数时钟。交锋里的一次性时钟和故事模块里要存档的时钟共用这一个对象——
;; 格数、上限、备注、进退和存档都收在闭包里，改上限只改 make-clock 那一行。
;;
;; 样式声明的是**这是什么状态**，不是画成什么形状；画法由渲染层按样式和上限决定：
;;   'gauge      一格一格的量：现在有多少 / 总共多少，满或空会触发事情。画成一排格子。
;;               **它不含方向。**调查进度从 0 填到满是 gauge，生命值从满打到 0 也是
;;               gauge，将来加了回血就是同一条往回涨。往哪边走由脚本自己决定，
;;               样式不替你规定——这样全游戏所有「有几格」的东西共用一种读法。
;;   'countdown  时间在逼近，归零触发。这一条才是有方向的：它只往下走，而且推它的
;;               不是你。画成空心表盘，亮着的扇区＝还剩多少，中心写剩余数字；
;;               上限 ≤6 分段，再多就是连续的一圈。
;;   'readout    当前是多少，满/空都不触发任何事。画成纯文字「当前/上限」。
;; 判据是一句话：**满或空会触发事情的，才是钟**。不触发的写 'readout。
;; gauge 与 countdown 之间只问一件事：这条线是**你手里的量**，还是**在逼近你的时间**。
;;
;;   (make-clock 标签 上限 样式)
;;   (make-clock 标签 上限 样式 备注)       备注是字符串
;;   (make-clock 标签 上限 样式 (lambda (current max) → 字符串))
;;                                          备注随格数变化（满格前后说不同的话）时用这个
;;
;; 消息一览（传错名字或参数个数会直接报错，错误信息里带着这张表，不必翻实现）：
;;   (clk 'tick!)          +1
;;   (clk 'advance! n)     +n，n 可负；不传 n 等同 'tick!
;;   (clk 'set! n)         直接置为 n
;;   (clk 'reset!)         归零
;;   (clk 'current)        当前格数        (clk 'max)     上限
;;   (clk 'full?)          是否满格        (clk 'empty?)  是否为零
;;   (clk 'remaining)      距满格还差几格
;;   (clk 'render-data)    一条 clock tuple；:clocks 要的是列表，单个钟写 (list (clk 'render-data))
;;   (clk 'save)           存档值          (clk 'load! n) 读档，越界报错
;;
;; 进退一律 clamp 到 0..上限，并自动写进结算效果条（动作外调用不产生结果行）。
(define clock-styles '(gauge countdown readout))

(define clock-messages
  "'tick! 'advance! 'set! 'reset! 'current 'max 'full? 'empty? 'remaining 'render-data 'save 'load!")

(define (make-clock label max style . note-args)
  (if (string? label) #t (error "make-clock: 标签必须是字符串"))
  (if (and (number? max) (> max 0)) #t (error "make-clock: 上限必须是正整数"))
  (if (member? style clock-styles)
      #t
      (error "make-clock: 未知样式（应为 'gauge / 'countdown / 'readout）"))
  (if (> (length note-args) 1)
      (error "make-clock: 备注最多一个")
      #t)
  (let ((current 0)
        (note (if (null? note-args) "" (car note-args))))
    (if (or (string? note) (procedure? note))
        #t
        (error "make-clock: 备注必须是字符串或 (lambda (current max) → 字符串)"))
    ;; 形参 max 遮住了内置的 max 函数，所以下界自己写。
    (define (clamp n) (if (< n 0) 0 (min n max)))
    (define (move-to! n)
      (let ((before current))
        (set! current (clamp n))
        (__record-clock-effect! label (- current before))))
    ;; 参数取一个；没传就用默认值。传多了直接报错，别让手滑悄悄溜过去。
    (define (one-arg args default who)
      (cond
        ((null? args) default)
        ((null? (cdr args)) (car args))
        (else (error (string-append "make-clock：" label " 的 " who " 只收一个参数")))))
    (lambda (msg . args)
      (cond
        ((equal? msg 'tick!)       (move-to! (+ current 1)))
        ((equal? msg 'advance!)    (move-to! (+ current (one-arg args 1 "'advance!"))))
        ((equal? msg 'reset!)      (move-to! 0))
        ((equal? msg 'set!)
         (let ((n (one-arg args #f "'set!")))
           (if (number? n) #t (error (string-append "make-clock：" label " 的 'set! 需要一个数字")))
           (move-to! n)))
        ((equal? msg 'current)     current)
        ((equal? msg 'max)         max)
        ((equal? msg 'full?)       (>= current max))
        ((equal? msg 'empty?)      (<= current 0))
        ((equal? msg 'remaining)   (- max current))
        ((equal? msg 'render-data)
         (list 'clock label current max style
               (if (procedure? note) (note current max) note)))
        ((equal? msg 'save) current)
        ((equal? msg 'load!)
         (let ((n (one-arg args #f "'load!")))
           (if (and (number? n) (>= n 0) (<= n max))
               #t
               (error (string-append "make-clock 存档错误：" label " 的格数非法")))
           (set! current n)))
        (else
         (error (string-append "make-clock：" label " 收到未知消息。可用消息："
                               clock-messages)))))))

;; --- New Team, Item, and Composure wrappers ---
(define (item-count item-id)
  (__item-count item-id))

(define (has-item? item-id n)
  (>= (__item-count item-id) n))

;; ── 物品容量 ────────────────────────────────────────
;; 绝大多数东西不设上限：线索、钱、剧情物，多一件只是多一件。
;; 烟不一样——它买得到也买得起，能囤就等于冷静随时可以拿钱换，交锋里那点压力也就不成立了。
;; 所以这里不是给物品系统加一层通用容量，而是一件件点出「这东西不许囤」的那几样。
(define item-capacities '(("香烟" 5)))

;; 没上限的返回 #f。
(define (item-capacity item-id)
  (assoc-get item-capacities item-id #f))

;; 引擎按这张表在物品格上画容量刻度（见 SceneManager.BuildPresentationSnapshot）。
(define (item-capacity-table) item-capacities)

(define (item-full? item-id)
  (let ((cap (item-capacity item-id)))
    (if cap (>= (__item-count item-id) cap) #f)))

;; 装不下的部分直接丢掉，但一定要说一声：静默吞掉会让玩家以为钱白花了却不知道为什么。
;; 买东西的卡自己该在满了的时候就灰掉（见 item-full?），走到这儿来才发现满，已经晚了一步。
(define (add-item! item-id n)
  (let* ((cap (item-capacity item-id))
         (target (+ (__item-count item-id) n))
         (final (if cap (min target cap) target)))
    (__set-item-count! item-id final)
    (if (< final target)
        (notify! (string-append item-id "带不了更多了，最多 " (number->string cap) " 个"))
        #f)))

;; 剧情直接交到玩家手里的具名物品。工作报酬、购买、调试与旧存档迁移仍用
;; add-item!；只有需要玩家明确知道「线索进了物品栏」的内容走这里。
(define (grant-story-item! item-id n)
  (if (and (string? item-id) (not (equal? item-id ""))
           (number? n) (> n 0))
      #t
      (error "grant-story-item!: 物品名必须是非空字符串，数量必须大于零"))
  (add-item! item-id n)
  (notify!
    (string-append "获得：" item-id
                   (if (= n 1) "" (string-append " ×" (number->string n))))))

(define (remove-item! item-id n)
  (if (< (__item-count item-id) n)
      (error "not enough item")
      (__set-item-count! item-id (- (__item-count item-id) n))))

(define (party-health)
  (__party-health))

(define (growth-level)
  (__growth-level))

(define (set-growth-level! n)
  (__set-growth-level! n))

;; 一张卷宗卡（一个小节）了结：固定一点成长。约定见 dossier 的注释——
;; 每张卡都这么发，玩家靠重复体验建立预期，不靠卡上写明。
;; 在卡的最后一个子项划掉、或世界把这一节推到头的那一拍调；
;; 放在那一拍的对白之后，否则通知会被整段对白盖掉。
;; 能不能重复了结由拥有这张卡的状态机负责；这里不做去重兼容。
(define (complete-task! id)
  (if (and (string? id) (not (equal? id ""))) #t (error "complete-task!: 卡的标识必须是非空字符串"))
  (set-growth-level! (+ (growth-level) 1))
  (notify! (string-append "〈" id "〉了结。成长 +1。")))

;; ── 伤势 ──────────────────────────────────────────────────────────
;; 队伍只有一条身体轴：0 完好 / 1–3 轻伤（命中的能力 −1）/ 4–6 重伤（不扣能力，封一颗行动骰）。
;; 到达 7/7 当场倒下并送医，结算后伤势与冷静归零，并在受伤部位留下永久疤痕。
;; 内容层不选部位——第一次受伤由引擎随机命中一项能力，之后的伤害都加深同一处。
;; 规则与档位见 Injury.cs（唯一来源；docs/城市生活设计.md 已不存在）。

;; 一般坏结果：劳作失手、挨一下。身上没伤是轻伤，带着伤就是加重。
(define (injure!)
  (__injure! 1))

;; 明确的重创：枪伤、坠落、被几个人围住打。能把完好的人一次打进重伤段。
(define (injure-badly!)
  (__injure! 3))

;; 治疗。刻度本身就是康复进度，降到 0 即痊愈。
(define (heal-injury! n)
  (__heal-injury! n))

(define (injury-severity)
  (__injury-severity))

;; 档位名（序号 0..2，与 Injury.InjuryBand 一一对应）。内容判断档位时不写魔数。
(define injury-band-names (list '完好 '轻伤 '重伤))

(define (injury-band)
  (list-ref injury-band-names (__injury-band-index)))

;; ── 疤痕 ──────────────────────────────────────────────────────────
;; 每一次倒下送医，都在当时伤着的那个部位永久留下一道疤：那项能力从此 −1，可叠加，
;; 治不掉、也不进伤势刻度。内容层只能读，不能发也不能抹——疤只由倒下产生（见 Scar.cs）。
;; 拿它来写人物认得出来的东西：跛着的腿、见不得光的那只眼、别人先看一眼再开口。

;; 身上疤的总数。
(define (scar-count)
  (__scar-count))

;; 某一处的疤有几道。部位名："手" "头" "眼" "脸"。
(define (scars-at part)
  (__scar-count part))

(define (has-scar? part)
  (> (__scar-count part) 0))

;; 终止本局游戏。标题和说明由内容声明，客户端只忠实呈现状态。
(define (fail-game! title description)
  (__fail-game! title description))


(define (current-actor)
  (__current-actor))

(define (actor-composure actor-id)
  (__actor-composure actor-id))

(define (actor-status actor-id)
  (__actor-status actor-id))

(define (actor-stat actor-id stat-name)
  (__actor-stat actor-id stat-name))

(define (recruit-companion! actor-id name stats-alist)
  (__recruit-companion! actor-id name stats-alist))

(define (set-actor-permanent-die-penalty! actor-id label penalty)
  (__set-actor-permanent-die-penalty! actor-id label penalty))

;; 让同伴离队。一场交锋临时请来的人必须在结算时离队，否则存档会当场报错。
(define (dismiss-companion! actor-id)
  (__dismiss-companion! actor-id))

;; 定制某个人物的骰池：几颗骰 + 恒定点数（0 = 正常掷骰）。恒定点数必须带可见标签，
;; 它会显示成骰位上的一枚徽章。
(define (set-actor-die-profile! actor-id slot-count fixed-value label)
  (__set-actor-die-profile! actor-id slot-count fixed-value label))

(define (has-companion? actor-id)
  (__has-companion? actor-id))

(define (set-actor-composure! actor-id n)
  (__set-actor-composure! actor-id n))

;; 花冷静（floor 到 0）：失败、交锋伤害等一切"变糟"的效果。
(define (spend-actor-composure! actor-id n)
  (__spend-actor-composure! actor-id n))

(define (spend-composure! n)
  (spend-actor-composure! (__current-actor) n))

;; 恢复冷静（clamp 到上限）：睡觉/喝酒/家里仪式/听歌/香烟。
(define (restore-actor-composure! actor-id n)
  (set-actor-composure! actor-id (+ (actor-composure actor-id) n)))

;; 喝酒的延期代价：下一次城市掷骰时，骰池中的一格会带“宿醉”降质。
(define (apply-hangover!)
  (__apply-hangover!))

(define (notify! text)
  (__notify! text))

;; 结算补充行：引擎自动行（钟 / 物品 / 关系 / 冷静 / 伤势）说不出的东西，
;; 才配写这一行。例如"解锁：码头账房""他欠你一次"——离散状态，无自动行。
;; 自动行已有的（推进了几格、多少钱、回几点），复述一遍就是噪音，直接删。
;; 句子、描写、人物的话不进这里：到阈值才说的用 banter / dialogue。
(define (result-supplement! text)
  (__result-supplement! text))

;; 剧情局部整数时钟在动作结果中的统一记录入口。
;; 动作外（读档、日终规则）调用时不产生结果行。
(define (record-clock-progress! label delta)
  (__record-clock-effect! label delta))

(define (spotlight! title subtitle)
  (__spotlight! title subtitle))

(define (play-narration! id)
  (__play-narration! id))

;; 一条台词:(line 说话人 文本) / (line 说话人 文本 语音) / (line 说话人 文本 语音 停留秒)
;; 停留秒仅 banter 使用;<=0 表示按文本长度自动估算。
;; 绑定语音时,客户端总会至少等到音频播完;显式停留秒只能延长,不能截断语音。
;;
;; 位置参数之后可以接舞台指示（只对阻塞对话生效），写成关键字对：
;;   (line "夜莺" "……" "语音id" :pose "逼近" :move 'in :light 'surge :shake #t)
;; 谁说话谁亮、听的人压暗、上台通电点亮是默认规则，不用写。
;; :pose  立绘变体名（资源 Portraits/Neon/<说话人>_<姿势>，"基础" 回招牌姿势）
;; :move  in（逼近中间）/ back（退回边上）
;; :light 灯。亮度状态 normal / surge（灼：光晕撑开、颜色洗台）/ faint（弱：只剩细管）/ ember（残烛：暗橙钨丝）
;;        电流状态 still / pulse（电流沿管子缓慢游走）/ racing（狂飙）——和亮度是两个轴，可以各写一个；
;;        事件 flicker（颤）/ relight（燃：全灭后从脚到头重新点亮）/ blackout（黑：整台黑半秒）只发生一次
;; :shake 一震
;; :screen 整个画面。状态 normal / negative（负片：环境全白、灯管变黑，白热化）保持到下次改变；
;;        事件 flash（白闪一帧：枪声、闪光灯、耳光）只发生一次
;; :inner #t：心里话，没说出口；对白框换成没有框的样子，舞台照常认说话人
;; :other 分隔符，不带值：写在它后面的 :pose/:move/:light/:shake 落到台上另一个人身上
;;        (line "夜莺" "……" "语音id" :pose "背身" :other :pose "点烟")
;; 每一项都是终态，没写的保持上一句。
(define (line speaker text . rest)
  (let* ((split (split-line-args rest))
         (positional (car split))
         (stage (cdr split))
         (voice (if (null? positional) "" (car positional)))
         (more  (if (null? positional) '() (cdr positional)))
         (dwell (if (null? more) 0 (car more))))
    (list speaker text voice dwell stage)))

;; 把 (语音 停留 :pose ...) 切成 (位置参数 . 舞台指示)：遇到第一个关键字符号就切。
(define 台词舞台关键字 (list :pose :move :light :shake :screen :inner :other))
(define (split-line-args args)
  (define (keyword? x)
    (and (symbol? x)
         (let loop ((ks 台词舞台关键字))
           (cond ((null? ks) #f)
                 ((equal? (car ks) x) #t)
                 (else (loop (cdr ks)))))))
  (let loop ((rest args) (positional '()))
    (cond ((null? rest) (cons (reverse positional) '()))
          ((keyword? (car rest)) (cons (reverse positional) rest))
          (else (loop (cdr rest) (cons (car rest) positional))))))

;; 非阻塞插话/斗嘴:游戏照常进行,气泡在角色处自动计时消失。变参,每个都是 (line ...)。
(define (play-banter! . lines)
  (__play-banter! lines))

;; 显式场外插话:未在场的说话人以不可交互的侧边临时卡承接。
;; 普通 play-banter! 默认说话人在场；若无法锚定，也会临时降级为场外卡，但客户端会报警。
(define (play-remote-banter! . lines)
  (__play-remote-banter! lines))

;; 阻塞对话:立绘舞台 + 对白框;全屏点击推进,锁输入、冻结导航,演完才把控制权还给玩家。
;; 变参,每个都是 (line ...)。说话人立绘按 Resources/Portraits/<说话人> 查找。
(define (play-dialogue! . lines)
  (__play-dialogue! lines))

;; 人物的标志色：立绘上点缀色那几根管子的颜色，随剧情变。传 CSS 十六进制色（"#8A5A2B"），
;; 舞台在几秒里把颜色过渡过去；存档里跟着走。林从原教旨的冷蓝慢慢变暖，就写在他的事件里。
(define (set-portrait-accent! 人 色)
  (set-global! (string-append "立绘色/" 人) 色))

;; 阻塞对话(场外):使用同一立绘舞台,但明确声明说话人不在场。
;; 普通 play-dialogue! 找不到锚点时舞台仍会继续显示并警告；明确不在场时用本接口表达意图。
(define (play-remote-dialogue! . lines)
  (__play-remote-dialogue! lines))

;; 视频过场：tag 认 Unity 场景里的 CutsceneSequence（机位 + 视频文件）。只能在动作内调用，
;; 作为有序阻塞剧情步骤播放。视频昂贵，少用；场景里东西自己动（唱片机、吊灯）走实时动画通道，
;; 不走这里。
(define (play-video! tag)
  (__play-video! tag))

;; 场景演出：用“地点/道具”稳定 ID 指定一件会动的道具（CityBox motion 通道，clip 名 道具__状态）播到目标状态；
;; 若目标 clip 标为 once，则每次都完整播放，结束后自动回到它声明的 from 状态。
;; 阻塞步骤：机位切到 Camera_<机位>（省略则不换机位）→ 播 当前状态→目标状态 的过渡 → 回原机位，
;; 全程不需要点击。道具之后就停在目标状态上，这一场里不会自己复原。
;;   (play-motion! "首演之夜/大吊灯" "Fallen" "首演之夜-吊灯")
(define (play-motion! 道具 状态 . 机位)
  (__play-motion! 道具 状态 (if (null? 机位) "" (car 机位))))

(define (advance-chapter!)
  (let ((current (get-global 'chapter)))
    (set-global! 'chapter (if current (+ current 1) 1))))
