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

;; 恢复性判定与 roll 同构，但不受角色的压力修正影响。
(define (recovery-roll skill fail-outcome neutral-outcome success-outcome)
  (list 'recovery-roll skill (lambda () '()) fail-outcome neutral-outcome success-outcome))

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

(define (recovery-roll-action name requires skill fail-outcome neutral-outcome success-outcome)
  (action name requires
    (recovery-roll skill
      (require-outcome fail-outcome "recovery-roll-action fail")
      (require-outcome neutral-outcome "recovery-roll-action neutral")
      (require-outcome success-outcome "recovery-roll-action success"))))

;; ── 工作（work）DSL ───────────────────────────────────
;; (工作 name faction risk skill 好-outcome 中-outcome 坏-outcome [subtitle])
;; (关系工作 name faction risk skill 好-outcome 中-outcome 坏-outcome [subtitle])
;;   faction: "官僚"/"劳工"/"富商"。普通工作不产关系；关系工作仅在好结果 +1。
;;   risk:    '低/'中/'高只决定风险标签与结果代价；'非法是难度标签，固定 -2
;;   好/中/坏: 每项工作显式传入三个 outcome，标题和描述直接用于轻型结算
;;   subtitle: 可选，只写“特别”的一句说明；一般风险由标签表达，不写 subtitle
;; 表现约定：每个工作都打“工作”标签（＝能赚钱）+ 一个风险标签，前端给风险标签配色，
;; 玩家一眼就能判断类型和大致风险。惩罚（钱/压力/健康、非法工作失败掉关系）写在各 outcome effect 里。
(define (工作-风险标签 risk)
  (cond ((equal? risk '低)   "低风险")
        ((equal? risk '中)   "中风险")
        ((equal? risk '高)   "高风险")
        ((equal? risk '非法) "非法")
        (else (error "工作: 未知风险等级（应为 低/中/高/非法）"))))

(define (工作-合法势力? faction)
  (or (equal? faction "官僚") (equal? faction "劳工") (equal? faction "富商")))

(define (工作-难度修正 risk)
  (cond ((or (equal? risk '低) (equal? risk '中) (equal? risk '高)) '())
        ((equal? risk '非法) (list (modifier -2 "非法")))
        (else (error "工作: 未知风险等级（应为 低/中/高/非法）"))))

(define (构造工作 name faction 产关系? risk skill 好-outcome 中-outcome 坏-outcome subtitle)
  (if (工作-合法势力? faction) #t (error "工作: 未知势力（应为 官僚/劳工/富商）"))
  (node name
        :subtitle subtitle
        :tags (list "工作" (工作-风险标签 risk))
        :requires (list (req-die))
        :resolve (roll skill
                       (lambda () (工作-难度修正 risk))
                       (require-outcome 坏-outcome "工作 坏")
                       (require-outcome 中-outcome "工作 中")
                       (if 产关系?
                           (outcome-append-effect
                             好-outcome
                             (lambda () (change-faction-relation! faction 1))
                             "关系工作 好")
                           (require-outcome 好-outcome "工作 好")))))

(define (工作 name faction risk skill 好-outcome 中-outcome 坏-outcome . extra)
  (构造工作 name faction #f risk skill 好-outcome 中-outcome 坏-outcome
            (if (null? extra) "" (car extra))))

(define (关系工作 name faction risk skill 好-outcome 中-outcome 坏-outcome . extra)
  (构造工作 name faction #t risk skill 好-outcome 中-outcome 坏-outcome
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
        ((equal? msg 'tick!)       (set! current (min (+ current 1) max)))
        ((equal? msg 'reset!)      (set! current 0))
        ((equal? msg 'full?)       (>= current max))
        ((equal? msg 'current)     current)
        ((equal? msg 'set!)        (set! current (car args)))
        ((equal? msg 'render-data) (list 'clock label current max style note))
        (else #f)))))

;; 关系 API — 三派：官僚 / 劳工 / 富商。底层连续整数（工作小步累积），
;; 折算成 5 个离散档位。档位阈值与范围以 RelationScale.cs 为唯一来源
;; （通过 native __relation-band-index 读取），这里只做名字 <-> 序号的映射。
(define (faction-relation faction)
  (let ((val (get-global (string-append "relation:" faction))))
    (if val val 0)))

;; 范围 [-10,10]，与 RelationScale.cs 保持一致。
(define (change-faction-relation! faction delta)
  (__change-faction-relation! faction delta))

;; 档位名（序号 0..4，与 RelationScale.BandNames 一一对应）。
(define relation-band-names (list '敌视 '冷淡 '中立 '脸熟 '自己人))

(define (relation-band faction)
  (list-ref relation-band-names (__relation-band-index faction)))

(define (band-index name)
  (cond ((equal? name '敌视)   0)
        ((equal? name '冷淡)   1)
        ((equal? name '中立)   2)
        ((equal? name '脸熟)   3)
        ((equal? name '自己人) 4)
        (else (error "band-index: unknown band"))))

;; 门控：某派关系达到指定档位（含更高）返回 #t。
(define (relation-at-least? faction band)
  (>= (__relation-band-index faction) (band-index band)))

;; --- New Team, Item, and Stress wrappers ---
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

;; 饱腹：吃食物恢复；每天睡觉 −1，归零扣健康（EndTurn 处理）。native 已 clamp 到 MaxSatiety。
(define (party-satiety)
  (__party-satiety))

(define (add-satiety! n)
  (__set-party-satiety! (+ (__party-satiety) n)))

(define (remove-satiety! n)
  (if (< (__party-satiety) n)
      (error "not enough satiety")
      (__set-party-satiety! (- (__party-satiety) n))))

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

(define (actor-stress actor-id)
  (__actor-stress actor-id))

(define (actor-status actor-id)
  (__actor-status actor-id))

(define (actor-stat actor-id stat-name)
  (__actor-stat actor-id stat-name))

(define (recruit-companion! actor-id name stats-alist)
  (__recruit-companion! actor-id name stats-alist))

(define (has-companion? actor-id)
  (__has-companion? actor-id))

(define (set-actor-stress! actor-id n)
  (__set-actor-stress! actor-id n))

(define (add-actor-stress! actor-id n)
  (__set-actor-stress! actor-id (+ (__actor-stress actor-id) n)))

(define (stress-current-actor! n)
  (add-actor-stress! (__current-actor) n))

;; 缓解压力（floor 到 0）。压力靠睡觉/喝酒/家里仪式/公园散步恢复。
(define (heal-stress! actor-id n)
  (set-actor-stress! actor-id (max 0 (- (actor-stress actor-id) n))))

(define (notify! text)
  (__notify! text))

;; 无法由状态变化自动推导的结算条目，例如“解锁：码头账房”。
(define (result-note! text)
  (__result-note! text))

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

;; 阻塞对话:点击推进、锁输入、冻结导航,演完才把控制权还给玩家。变参,每个都是 (line ...)。
(define (play-dialogue! . lines)
  (__play-dialogue! lines))

;; 命名动画(占位):目前只能在动作内调用,作为有序阻塞剧情步骤播放。
(define (play-animation! tag)
  (__play-animation! tag))

(define (advance-chapter!)
  (let ((current (get-global 'chapter)))
    (set-global! 'chapter (if current (+ current 1) 1))))
