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
  (list 'node
        name
        :subtitle (get-kwarg kwargs ':subtitle "")
        :clocks (get-kwarg kwargs ':clocks '())
        :children (get-kwarg kwargs ':children '())
        :requires (get-kwarg kwargs ':requires #f)
        :resolve (get-kwarg kwargs ':resolve #f)
        :tags (get-kwarg kwargs ':tags '())
        :disabled (get-kwarg kwargs ':disabled #f)))

;; ── 休息阻塞 ─────────────────────────────────────
;; 注册表只存在于当前解释器。world-load! 会先清空，再由各地点按存档状态同步。
(define rest-blockers '())

(define (remove-rest-blocker entries id)
  (if (null? entries)
      '()
      (if (equal? (car (car entries)) id)
          (remove-rest-blocker (cdr entries) id)
          (cons (car entries) (remove-rest-blocker (cdr entries) id)))))

(define (rest-block! id reason)
  (if (not (string? id)) (error "rest-block!: id must be a string") #t)
  (if (not (string? reason)) (error "rest-block!: reason must be a string") #t)
  (if (equal? id "") (error "rest-block!: id cannot be empty") #t)
  (if (equal? reason "") (error "rest-block!: reason cannot be empty") #t)
  (set! rest-blockers
        (cons (list id reason) (remove-rest-blocker rest-blockers id))))

(define (rest-release! id)
  (if (not (string? id)) (error "rest-release!: id must be a string") #t)
  (set! rest-blockers (remove-rest-blocker rest-blockers id)))

(define (rest-blocked?)
  (not (null? rest-blockers)))

(define (rest-block-reasons)
  (map cadr (reverse rest-blockers)))

(define (clear-rest-blockers!)
  (set! rest-blockers '()))

(define (end-turn!)
  (if (rest-blocked?)
      (error "end-turn!: required events remain unresolved")
      (__end-turn!)))

;; Action constructors
(define (instant effect)
  (list 'instant effect))

;; Outcome wraps an action effect with optional result presentation metadata.
;;
;; Supported forms:
;; (outcome title subtitle effect)
;; (outcome title subtitle effect 'light)
;; (outcome title subtitle effect 'heavy)
;;
(define (outcome title subtitle effect . modes)
  (if (> (length modes) 1)
      (error "outcome: expected at most one presentation mode")
      (let ((mode (if (null? modes) 'light (car modes))))
        (list 'outcome title subtitle mode effect))))

(define (outcome? value)
  (and (pair? value)
       (= (length value) 5)
       (equal? (car value) 'outcome)))

(define (require-outcome value who)
  (if (outcome? value)
      value
      (error (string-append who ": expected outcome"))))

;; 保留 outcome 的标题/描述/模式，在原效果之后追加一个效果。
(define (outcome-append-effect value extra-effect who)
  (let ((checked (require-outcome value who)))
    (let ((effect (list-ref checked 4)))
      (list 'outcome
            (list-ref checked 1)
            (list-ref checked 2)
            (list-ref checked 3)
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

;; Clock resolve constructor — wraps a make-clock render-data snapshot
(define (clock clock-data)
  (list 'clock clock-data))

;; Clock node: a display-only node that shows a spatial clock above its anchor
(define (clock-node name subtitle clock-data)
  (node name :subtitle subtitle :resolve (clock clock-data)))

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

(define (action name requires resolve)
  (node name :requires requires :resolve resolve))

(define (action-with-tags name tags requires resolve)
  (node name :tags tags :requires requires :resolve resolve))

(define (action-with-clocks name requires resolve clocks)
  (node name :requires requires :resolve resolve :clocks clocks))

;; Shorthands for simple actions
(define (instant-action name effect)
  (action name #f (instant effect)))

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
;; (工作 name faction risk skill 好-outcome 中-outcome 坏-outcome [subtitle])
;; (关系工作 name faction risk skill 好-outcome 中-outcome 坏-outcome [subtitle])
;; (非法工作 name faction risk skill 好-outcome 中-outcome 坏-outcome [subtitle])
;;   faction: "官僚"/"劳工"/"富商"。普通工作不产关系；关系工作仅在好结果 +1。
;;   risk:    只有 '低/'高，决定风险标签与结果代价。
;;   非法工作是独立维度：额外显示“非法”并固定难度 -2，不再冒充第三种风险档。
;;   好/中/坏: 每项工作显式传入三个 outcome，标题和描述直接用于轻型结算
;;   subtitle: 可选，只写“特别”的一句说明；一般风险由标签表达，不写 subtitle
;; 表现约定：每个工作都打“工作”标签（＝能赚钱）+ 一个风险标签，前端给风险标签配色，
;; 玩家一眼就能判断类型和大致风险。惩罚（钱/冷静/健康、非法工作失败掉关系）写在各 outcome effect 里。
(define (工作-风险标签 risk)
  (cond ((equal? risk '低)   "低风险")
        ((equal? risk '高)   "高风险")
        (else (error "工作: 未知风险等级（应为 低/高）"))))

(define (工作-合法势力? faction)
  (or (equal? faction "官僚") (equal? faction "劳工") (equal? faction "富商")))

;; 势力敌视时，该势力地点的判定统一 -1（可见修正，与非法的判定惩罚同构）。
(define (关系难度修正 faction)
  (if (equal? (relation-band faction) '敌视)
      (list (modifier -1 "势力敌视"))
      '()))

(define (工作-难度修正 risk faction illegal?)
  (append
    (begin (工作-风险标签 risk) '())
    (if illegal? (list (modifier -2 "非法")) '())
    (关系难度修正 faction)))

(define (构造工作 name faction 产关系? illegal? risk skill 好-outcome 中-outcome 坏-outcome subtitle)
  (if (工作-合法势力? faction) #t (error "工作: 未知势力（应为 官僚/劳工/富商）"))
  (node name
        :subtitle subtitle
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

(define (工作 name faction risk skill 好-outcome 中-outcome 坏-outcome . extra)
  (构造工作 name faction #f #f risk skill 好-outcome 中-outcome 坏-outcome
            (if (null? extra) "" (car extra))))

(define (关系工作 name faction risk skill 好-outcome 中-outcome 坏-outcome . extra)
  (构造工作 name faction #t #f risk skill 好-outcome 中-outcome 坏-outcome
            (if (null? extra) "" (car extra))))

(define (非法工作 name faction risk skill 好-outcome 中-outcome 坏-outcome . extra)
  (构造工作 name faction #f #t risk skill 好-outcome 中-outcome 坏-outcome
            (if (null? extra) "" (car extra))))

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
    (if (null? list-rules)
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

(define turn-rules '())

;; define-turn-rule registers a turn-end rule
(define (define-turn-rule name condition action)
  (set! turn-rules (cons (list name condition action) turn-rules)))

;; on-turn-end triggers all turn-end rules
(define (on-turn-end)
  (define (run-rules list-rules)
    (if (null? list-rules)
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

(define (make-clock label max style . note-args)
  (if (> (length note-args) 1)
      (error "make-clock: expected at most one note string")
      #t)
  (let ((current 0)
        (note (if (null? note-args) "" (car note-args))))
    (if (string? note) #t (error "make-clock: note must be a string"))
    (lambda (msg . args)
      (cond
        ((equal? msg 'tick!)
         (let ((before current))
           (set! current (min (+ current 1) max))
           (__record-clock-effect! label (- current before))))
        ((equal? msg 'reset!)
         (let ((before current))
           (set! current 0)
           (__record-clock-effect! label (- current before))))
        ((equal? msg 'full?)       (>= current max))
        ((equal? msg 'current)     current)
        ((equal? msg 'set!)
         (let ((before current))
           (set! current (car args))
           (__record-clock-effect! label (- current before))))
        ((equal? msg 'render-data) (list 'clock label current max style note))
        (else #f)))))

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
             (list (list 'clock label current max 'countdown
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

;; 声望 API — 三派：官僚 / 劳工 / 富商。底层连续整数（工作小步累积），
;; 折算成 6 个离散档位。档位阈值与范围以 RelationScale.cs 为唯一来源
;; （通过 native __relation-band-index 读取），这里只做名字 <-> 序号的映射。
;; 正面三档（相识/信任/核心）是门控用的通用内部名；各势力面板上的定制称呼
;; （挂号/面熟/有往来 …）在 world.scm 以 relation-band-name:<势力>:<档> 配置。
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
;;   拜过码头/有里子/合伙人（核心）：只认事迹——人物小节、主线段落完成时直接调用
;;     change-faction-relation!，不设上限，是唯一能越过 favor-relation-cap 的路。
(define work-relation-cap 3)   ; 带薪工作最多能混到相识刚过一点
(define favor-relation-cap 5)  ; 帮忙类动作最多能混到信任刚过一点，再往上得靠事迹

(define (grant-work-relation! faction)
  (if (< (faction-relation faction) work-relation-cap)
      (change-faction-relation! faction 1)
      (notify! (string-append faction "那边，普通做工已经混得再熟不过了——想更进一步，得接不计报酬的忙。"))))

(define (grant-favor-relation! faction)
  (if (< (faction-relation faction) favor-relation-cap)
      (change-faction-relation! faction 1)
      (notify! (string-append faction "那边，光帮忙已经到头了——真要再进一步，得替他们办成一件事。"))))

;; --- New Team, Item, and Composure wrappers ---
(define (item-count item-id)
  (__item-count item-id))

(define (has-item? item-id n)
  (>= (__item-count item-id) n))

(define (add-item! item-id n)
  (__set-item-count! item-id (+ (__item-count item-id) n)))

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

(define (damage-party! n)
  (__set-party-health! (- (__party-health) n)))

;; 恢复健康（native 已 clamp 到 MaxHealth）。健康只应由药品、康复训练等医疗行为恢复，不由睡觉恢复。
(define (heal-party! n)
  (__set-party-health! (+ (__party-health) n)))

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
;; 普通 play-banter! 仍严格要求说话人能锚定到当前画面。
(define (play-remote-banter! . lines)
  (__play-remote-banter! lines))

;; 阻塞对话:点击推进、锁输入、冻结导航,演完才把控制权还给玩家。变参,每个都是 (line ...)。
(define (play-dialogue! . lines)
  (__play-dialogue! lines))

;; 命名动画(占位):目前只能在动作内调用,作为有序阻塞剧情步骤播放。
(define (play-animation! tag)
  (__play-animation! tag))

(define (advance-chapter!)
  (let ((current (get-global 'chapter)))
    (set-global! 'chapter (if current (+ current 1) 1))))
