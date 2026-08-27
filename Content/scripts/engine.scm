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
(define :arrivals ':arrivals)
(define :kind ':kind)
(define :status ':status)
(define :now ':now)
(define :where ':where)
(define :log ':log)

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

;; Outcome wraps an action effect with optional result presentation metadata.
;;
;; Supported forms:
;; (outcome title effect)
;; (outcome title effect 'light)
;; (outcome title effect 'heavy)
;;
(define (outcome title effect . modes)
  (if (> (length modes) 1)
      (error "outcome: expected at most one presentation mode")
      (let ((mode (if (null? modes) 'light (car modes))))
        (list 'outcome title mode effect))))

(define (outcome? value)
  (and (pair? value)
       (= (length value) 4)
       (equal? (car value) 'outcome)))

(define (require-outcome value who)
  (if (outcome? value)
      value
      (error (string-append who ": expected outcome"))))

;; 保留 outcome 的标题/模式，在原效果之后追加一个效果。
(define (outcome-append-effect value extra-effect who)
  (let ((checked (require-outcome value who)))
    (let ((effect (list-ref checked 3)))
      (list 'outcome
            (list-ref checked 1)
            (list-ref checked 2)
            (lambda ()
              (effect)
              (extra-effect))))))

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
;; 每条线只有一句 :now。它不是任务描述，是玩家隔三天回来要读的那一句：
;; 第二人称，说得出下一步该做什么。写不出这一句，说明那一拍的目标本身没想清楚，
;; 那是内容的问题，别靠面板多列两行来遮。
;;
;; :clocks 直接放故事已经在用的钟（(某某-clk 'render-data)），不为卷宗新建一套——
;; 同一根钟在动作卡上和卷宗里必须长得一模一样。
(define dossier-kinds  (list '委托 '人物 '城市))
(define dossier-states (list '进行中 '等着别人 '了结))

(define (dossier id . kwargs)
  (if (and (string? id) (not (equal? id ""))) #t (error "dossier: 标识必须是非空字符串"))
  (let ((kind   (get-kwarg kwargs ':kind '委托))
        (status (get-kwarg kwargs ':status '进行中))
        (now    (get-kwarg kwargs ':now ""))
        (where  (get-kwarg kwargs ':where "")))
    (if (member? kind dossier-kinds)
        #t
        (error (string-append "dossier " id "：:kind 应为 委托 / 人物 / 城市")))
    (if (member? status dossier-states)
        #t
        (error (string-append "dossier " id "：:status 应为 进行中 / 等着别人 / 了结")))
    (if (string? now) #t (error (string-append "dossier " id "：:now 必须是字符串")))
    (if (string? where) #t (error (string-append "dossier " id "：:where 必须是字符串")))
    (list 'dossier id
          :kind kind
          :status status
          :now now
          :where where
          :clocks (get-kwarg kwargs ':clocks '())
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
;; (工作 name faction risk skill 好-outcome 中-outcome 坏-outcome [subtitle] [:anchor name])
;; (关系工作 name faction risk skill 好-outcome 中-outcome 坏-outcome [subtitle]
;;           [:anchor name] [:clocks clocks])
;; (非法工作 name faction risk skill 好-outcome 中-outcome 坏-outcome [subtitle]
;;           [:anchor name] [:clocks clocks])
;;   faction: "老码头"/"商业圈"，不属于任何圈子的活写 "无"。
;;            普通工作不产声誉；关系工作仅在好结果 +1。
;;   risk:    只有 '低/'高，决定风险标签与结果代价。
;;   非法工作是独立维度：额外显示“非法”并固定难度 -2，不再冒充第三种风险档。
;;   好/中/坏: 每项工作显式传入三个 outcome，标题和描述直接用于轻型结算
;;   subtitle: 可选，只写“特别”的一句说明；一般风险由标签表达，不写 subtitle
;; 表现约定：每个工作都打“工作”标签（＝能赚钱）+ 一个风险标签，前端给风险标签配色，
;; 玩家一眼就能判断类型和大致风险。惩罚（钱/冷静/伤势、非法工作失败掉关系）写在各 outcome effect 里。
(define (工作-风险标签 risk)
  (cond ((equal? risk '低)   "低风险")
        ((equal? risk '高)   "高风险")
        (else (error "工作: 未知风险等级（应为 低/高）"))))

(define (工作-合法势力? faction)
  (or (equal? faction "老码头") (equal? faction "商业圈") (equal? faction "无")))

;; 圈子敌视时，该圈子地点的判定统一 -1（可见修正，与非法的判定惩罚同构）。
(define (关系难度修正 faction)
  (if (and (not (equal? faction "无"))
           (equal? (relation-band faction) '敌视))
      (list (modifier -1 "势力敌视"))
      '()))

(define (工作-难度修正 risk faction illegal?)
  (append
    (begin (工作-风险标签 risk) '())
    (if illegal? (list (modifier -2 "非法")) '())
    (关系难度修正 faction)))

(define (构造工作 name faction 产关系? illegal? risk skill 好-outcome 中-outcome 坏-outcome subtitle anchor clocks)
  (if (工作-合法势力? faction) #t (error "工作: 未知圈子（应为 老码头/商业圈/无）"))
  (if (and 产关系? (equal? faction "无")) (error "关系工作: 不能挂在「无」上") #t)
  (node name
        :subtitle subtitle
        :anchor anchor
        :clocks clocks
        :tags (if illegal?
                  (list "工作" (工作-风险标签 risk) "非法")
                  (list "工作" (工作-风险标签 risk)))
        :requires (list (req-die))
        :resolve (roll skill
                       (lambda () (工作-难度修正 risk faction illegal?))
                       (require-outcome 坏-outcome "工作 坏")
                       (require-outcome 中-outcome "工作 中")
                       (if 产关系?
                           (outcome-append-effect
                             好-outcome
                             (lambda () (grant-work-relation! faction))
                             "关系工作 好")
                           (require-outcome 好-outcome "工作 好")))))

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

(define (工作 name faction risk skill 好-outcome 中-outcome 坏-outcome . extra)
  (let ((args (工作-附加参数 extra)))
    (构造工作 name faction #f #f risk skill 好-outcome 中-outcome 坏-outcome
              (car args) (cadr args) (caddr args))))

(define (关系工作 name faction risk skill 好-outcome 中-outcome 坏-outcome . extra)
  (let ((args (工作-附加参数 extra)))
    (构造工作 name faction #t #f risk skill 好-outcome 中-outcome 坏-outcome
              (car args) (cadr args) (caddr args))))

(define (非法工作 name faction risk skill 好-outcome 中-outcome 坏-outcome . extra)
  (let ((args (工作-附加参数 extra)))
    (构造工作 name faction #f #t risk skill 好-outcome 中-outcome 坏-outcome
              (car args) (cadr args) (caddr args))))

;; ── 随身动作 ─────────────────────────────────────────
;; 烟、酒这类你自己带进交锋的东西。它们不属于任何一场——没有哪个交锋脚本声明它们，
;; 是引擎在每一场的树上补一份（见 SceneManager.RebuildRenderTree）。只在手里真有那件
;; 东西的时候出现。
;;
;; 它们**不是场上的卡**：客户端把它们画成右下角一条常驻的小挂件（标题 + 一个骰位），
;; 没有副标题也没有执行钮，骰子放进去就用掉。角落里那种一按就生效的按钮已经删掉了——
;; 玩家没法从一个按钮上看出"这里能放骰子"，而放东西的表现形式必须处处一致。
;;
;; 要投骰，但不掷骰：骰面完全不参与结算。所以这是全场唯一一处**烂骰子和好骰子等价**
;; 的地方，一颗 1 点骰投在这儿换回来的和 6 点一样多。手气差的那一轮，抽根烟不是浪费，
;; 是分诊。
(define (随身动作 name item amount title subtitle effect)
  (node name
    ;; 它属于哪件物品。客户端据此把这个小挂件从那件物品上引出来（一根引线），
    ;; 而不是排进场上的卡片区——随身的东西不是这一场的事。
    :carry-item item
    :tags (list "随身")
    :subtitle subtitle
    ;; 只要一颗骰。那件东西本身不做成物品槽：这个挂件就是从它上面长出来的，
    ;; 还要玩家把烟拖进它自己长出来的槽里，是绕一圈说同一句话。
    :requires (list (req-die))
    :resolve (instant
      (outcome title
        (lambda ()
          (remove-item! item 1)
          (restore-actor-composure! 'player amount)
          (effect))))))

(define (carry-nodes)
  (append
    ;; 交锋每回合自动流失 1，一根烟买回两个回合——这是它的单位。
    (if (has-item? "香烟" 1)
        (list (随身动作 "抽烟" "香烟" 2 "抽了一口"
                "投一颗行动骰，用掉一根烟；恢复 2 点冷静。骰面不算数"
                (lambda () #f)))
        '())
    ;; 酒回得多，代价推到明天：下一次城市骰池里有一格带宿醉。
    (if (has-item? "酒" 1)
        (list (随身动作 "喝酒" "酒" 3 "灌了一口"
                "投一颗行动骰，喝掉这瓶；恢复 3 点冷静，酒劲留到明天"
                (lambda () (apply-hangover!))))
        '())))

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

;; 麻烦追踪器：势力敌视时工作坏/中结果有概率触发一次，持续 max 天未处理则触发 on-expire。
;; 消息：'active? 'start! 'resolve! 'tick! 'render-data 'save 'load!
(define (make-trouble label max on-expire)
  (let ((active? #f) (current 0))
    (lambda (msg . args)
      (cond
        ((equal? msg 'active?) active?)
        ((equal? msg 'start!)
         (if active? #t (begin (set! active? #t) (set! current 0))))
        ((equal? msg 'resolve!) (begin (set! active? #f) (set! current 0)))
        ((equal? msg 'tick!)
         (if active?
             (begin
               (set! current (+ current 1))
               (if (>= current max)
                   (begin (set! active? #f) (set! current 0) (on-expire))
                   #f))
             #f))
        ((equal? msg 'render-data)
         (if active?
             ;; 内部 current 是「已经拖了几天」，表盘要的是「还剩几天」，这里翻过来。
             (list (list 'clock label (- max current) max 'countdown
                         "势力敌视惹出的麻烦，尽快处理，否则会有代价。"))
             '()))
        ((equal? msg 'save) (list active? current))
        ((equal? msg 'load!)
         (let ((data (car args)))
           (set! active? (car data))
           (set! current (cadr data))))
        (else #f)))))

;; 20% 概率触发一次麻烦（1/5，参照码头美差的写法）。
(define trouble-roll-table (list #t #f #f #f #f))

;; 势力敌视、麻烦未激活、且命中概率时触发；返回 #t/#f 供调用方决定是否 notify!。
(define (maybe-trigger-trouble! tracker faction chance-table)
  (if (and (equal? (relation-band faction) '敌视)
           (not (tracker 'active?))
           (random-choice chance-table))
      (tracker 'start!)
      #f))

;; 圈内声誉 API — 两个圈子：老码头 / 商业圈。它记的是「你的名声在哪个圈子里传开了」，
;; 不是阵营归属：没有成员名单，人物只是走进这个圈子的入口。市政、警署与医院不在此列，
;; 它们由具名人物状态承担（见 人物/贝恩斯.scm）。
;; 底层连续整数（工作小步累积），折算成 6 个离散档位。档位阈值与范围以 RelationScale.cs
;; 为唯一来源（通过 native __relation-band-index 读取），这里只做名字 <-> 序号的映射。
;; 正面三档（相识/信任/核心）是门控用的通用内部名；各圈子面板上的定制称呼
;; （面熟/够朋友/有往来 …）在 world.scm 以 relation-band-name:<圈子>:<档> 配置。
(define (faction-relation faction)
  (let ((val (get-global (string-append "relation:" faction))))
    (if val val 0)))

;; 范围 [-10,10]，与 RelationScale.cs 保持一致。
(define (change-faction-relation! faction delta)
  (__change-faction-relation! faction delta))

;; 档位名（序号 0..5，与 RelationScale.BandNames 一一对应）。
(define relation-band-names (list '敌视 '冷淡 '中立 '相识 '信任 '核心))

(define (relation-band faction)
  (list-ref relation-band-names (__relation-band-index faction)))

(define (band-index name)
  (cond ((equal? name '敌视) 0)
        ((equal? name '冷淡) 1)
        ((equal? name '中立) 2)
        ((equal? name '相识) 3)
        ((equal? name '信任) 4)
        ((equal? name '核心) 5)
        (else (error "band-index: unknown band"))))

;; 门控：某派关系达到指定档位（含更高）返回 #t。
(define (relation-at-least? faction band)
  (>= (__relation-band-index faction) (band-index band)))

;; ── 声望增长来源分档 ──────────────────────────────
;; 爬升方式随档位换，不是从头到尾刷同一种动作就能通关：
;;   面熟（相识）：带薪工作的好结果——值 < work-relation-cap 时才生效，见 构造工作；
;;   够朋友（信任）：不计报酬的帮忙类动作——值 < favor-relation-cap 时才生效，见 grant-favor-relation!；
;;   自己人/有里子/合伙人（核心）：只认事迹——人物小节、主线段落完成时直接调用
;;     change-faction-relation!，不设上限，是唯一能越过 favor-relation-cap 的路。
(define work-relation-cap 3)   ; 带薪工作最多能混到相识刚过一点
(define favor-relation-cap 5)  ; 帮忙类动作最多能混到信任刚过一点，再往上得靠事迹

;; 封顶时**不再提示**"想更进一步，得接不计报酬的忙"：那句话指的是「替人顶一班」
;; 那类帮忙卡，而它这一版没有摆出来（见 码头.scm）。指着一张玩家找不到的卡说话，
;; 比什么也不说更让人摸不着头脑。等帮忙类动作回来，把这条提示一起还回来。
(define (grant-work-relation! faction)
  (if (< (faction-relation faction) work-relation-cap)
      (change-faction-relation! faction 1)
      #f))

(define (grant-favor-relation! faction)
  (if (< (faction-relation faction) favor-relation-cap)
      (change-faction-relation! faction 1)
      (notify! (string-append faction "那边，光帮忙已经到头了——真要再进一步，得替他们办成一件事。"))))

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

;; 完成一个不可重复的故事小节，获得一点成长。
;; 小节是否允许完成由拥有该状态的主线/人物状态机负责；这里不做去重兼容。
(define (complete-section!)
  (set-growth-level! (+ (growth-level) 1))
  (notify! "完成一个故事小节。获得 1 点成长。"))

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

;; 恢复冷静（clamp 到上限）：睡觉/喝酒/家里仪式/公园散步/香烟。
(define (restore-actor-composure! actor-id n)
  (set-actor-composure! actor-id (+ (actor-composure actor-id) n)))

;; 喝酒的延期代价：下一次城市掷骰时，骰池中的一格会带“宿醉”降质。
(define (apply-hangover!)
  (__apply-hangover!))

(define (notify! text)
  (__notify! text))

;; 无法由状态变化自动推导的结算条目，例如“解锁：码头账房”。
(define (result-note! text)
  (__result-note! text))

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
(define (line speaker text . rest)
  (let* ((voice (if (null? rest) "" (car rest)))
         (more  (if (null? rest) '() (cdr rest)))
         (dwell (if (null? more) 0 (car more))))
    (list speaker text voice dwell)))

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

;; 阻塞对话(场外):使用同一立绘舞台,但明确声明说话人不在场。
;; 普通 play-dialogue! 找不到锚点时舞台仍会继续显示并警告；明确不在场时用本接口表达意图。
(define (play-remote-dialogue! . lines)
  (__play-remote-dialogue! lines))

;; 命名动画(占位):目前只能在动作内调用,作为有序阻塞剧情步骤播放。
(define (play-animation! tag)
  (__play-animation! tag))

(define (advance-chapter!)
  (let ((current (get-global 'chapter)))
    (set-global! 'chapter (if current (+ current 1) 1))))
