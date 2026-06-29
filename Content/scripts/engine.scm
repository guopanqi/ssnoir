;; engine.scm - Game and DSL Engine Definitions

;; Schemy does not treat colon-prefixed names as self-evaluating keywords.
(define :clocks ':clocks)
(define :children ':children)
(define :effect ':effect)
(define :requires ':requires)
(define :resolve ':resolve)
(define :tags ':tags)
(define :subtitle ':subtitle)

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
;; Returns a list: ('node name :subtitle subtitle :clocks clocks :children children :requires requires :resolve resolve :tags tags)
(define (node name . kwargs)
  (list 'node
        name
        :subtitle (get-kwarg kwargs ':subtitle "")
        :clocks (get-kwarg kwargs ':clocks '())
        :children (get-kwarg kwargs ':children '())
        :requires (get-kwarg kwargs ':requires #f)
        :resolve (get-kwarg kwargs ':resolve #f)
        :tags (get-kwarg kwargs ':tags '())))

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

(define (roll-action name requires skill fail-fn neutral-fn success-fn)
  (action name requires (roll skill fail-fn neutral-fn success-fn)))

;; Inventory helpers
(define (get-item item-id)
  (item-count item-id))

(define (consume-item! item-id n)
  (remove-item! item-id n))

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

(define (make-clock label max style)
  (let ((current 0))
    (lambda (msg . args)
      (cond
        ((equal? msg 'tick!)       (set! current (min (+ current 1) max)))
        ((equal? msg 'reset!)      (set! current 0))
        ((equal? msg 'full?)       (>= current max))
        ((equal? msg 'current)     current)
        ((equal? msg 'set!)        (set! current (car args)))
        ((equal? msg 'render-data) (list 'clock label current max style))
        (else #f)))))

;; Reputation API
(define (get-reputation faction)
  (let ((val (get-global (string-append "reputation:" faction))))
    (if val val 0)))

(define (change-reputation! faction delta)
  (let ((new-val (+ (get-reputation faction) delta)))
    (let ((clamped (if (< new-val -100) -100 (if (> new-val 100) 100 new-val))))
      (set-global! (string-append "reputation:" faction) clamped))))

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

(define (party-supplies)
  (__party-supplies))

(define (add-supplies! n)
  (__set-party-supplies! (min 6 (+ (__party-supplies) n))))

(define (remove-supplies! n)
  (if (< (__party-supplies) n)
      (error "not enough supplies")
      (__set-party-supplies! (- (__party-supplies) n))))

(define (party-health)
  (__party-health))

(define (growth-level)
  (__growth-level))

(define (set-growth-level! n)
  (__set-growth-level! n))

(define (damage-party! n)
  (__set-party-health! (- (__party-health) n)))

(define (heal-party! n)
  (__set-party-health! (min 8 (+ (__party-health) n))))

(define (current-actor)
  (__current-actor))

(define (actor-stress actor-id)
  (__actor-stress actor-id))

(define (actor-status actor-id)
  (__actor-status actor-id))

(define (actor-stat actor-id stat-name)
  (__actor-stat actor-id stat-name))

(define (set-actor-stress! actor-id n)
  (__set-actor-stress! actor-id n))

(define (add-actor-stress! actor-id n)
  (__set-actor-stress! actor-id (+ (__actor-stress actor-id) n)))

(define (stress-current-actor! n)
  (add-actor-stress! (__current-actor) n))

(define (notify! text)
  (__notify! text))

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

(define (upgrade-actor-stat! actor-id stat-id)
  (__upgrade-actor-stat! actor-id stat-id))
