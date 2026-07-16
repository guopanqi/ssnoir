;; scenes/world/world.scm - 世界协调器（城市生活第一版）
;; 世界拥有日期与强制公共事件；地点只拥有自己的生活内容。

(load-file "world/人物/乔.scm")
(load-file "world/人物/弗兰克.scm")
(load-file "world/人物/阿瑟.scm")
(load-file "world/人物/沃尔特.scm")
(load-file "world/人物/萨姆.scm")
(load-file "world/home.scm")
(load-file "world/码头.scm")
(load-file "world/老街酒馆.scm")
(load-file "world/夜莺.scm")
(load-file "world/饭店.scm")
(load-file "world/诊所.scm")
(load-file "world/公园.scm")
(load-file "world/警局.scm")
(load-file "world/货运公司.scm")
(load-file "world/居民区.scm")
(load-file "world/保险公司.scm")
(load-file "world/陌生人藏身处.scm")
(load-file "world/board.scm")
(load-file "world/test.scm")

;; ── 世界级状态 ───────────────────────────────────
(define world-day 1)

;; ── 城市声望 ─────────────────────────────────────
;; 三派各有一条三档声望阶梯（相识/信任/核心，阈值 +2/+4/+6）。
;; 每档两项配置由内容定义、客户端据当前声望显示：
;;   relation-band-name:<势力>:<档>  该势力对这一档的定制称呼（面板档名与诱饵标题）
;;   relation-goal:<势力>:<档>       这一档解锁的具名诱饵
;; 每档只写一件确实能在 demo 里做的事；暂时做不到的，标"（demo 暂未开放）"，
;; 不堆第二个想法凑数——档位到了却只看见一句空话，比少写一档更打消玩家推进的意愿。
;; 门控仍用通用内部名（相识/信任/核心），见 engine.scm。
;;
;; 爬升方式随档位换（见 engine.scm 的 grant-work-relation!/grant-favor-relation!）：
;; 相识靠带薪工作混脸熟（到值 3 封顶）；信任靠不计报酬的帮忙类动作（到值 5 封顶）；
;; 核心只认事迹——人物小节/主线段落完成时才给，不封顶，是唯一能到核心的路。

;; 官僚〈秩序 · 程序 · 洗白〉：让麻烦消失、拿到程序特权。
(set-global! "relation-band-name:官僚:相识" "挂号")
(set-global! "relation-band-name:官僚:信任" "备案")
(set-global! "relation-band-name:官僚:核心" "有里子")
(set-global! "relation-goal:官僚:相识" "替阿瑟处理程序管不了的麻烦")
(set-global! "relation-goal:官僚:信任" "请阿瑟提级、延期并办理通行证")
(set-global! "relation-goal:官僚:核心" "引荐市长秘书，遇事能求到更高处（demo 暂未开放）")

;; 劳工〈生存 · 暴力 · 销赃网〉：暴力援助与销赃/借钱网络。
(set-global! "relation-band-name:劳工:相识" "面熟")
(set-global! "relation-band-name:劳工:信任" "够朋友")
(set-global! "relation-band-name:劳工:核心" "拜过码头")
(set-global! "relation-goal:劳工:相识" "参与搁浅货船的紧急抢修")
(set-global! "relation-goal:劳工:信任" "替弗兰克查账并接触旧悬案")
(set-global! "relation-goal:劳工:核心" "走私工作；了断之日弗兰克带人到场")

;; 富商〈欲望 · 资本 · 科技圈层〉：资本/投资与上流圈层。
(set-global! "relation-band-name:富商:相识" "有往来")
(set-global! "relation-band-name:富商:信任" "座上宾")
(set-global! "relation-band-name:富商:核心" "合伙人")
(set-global! "relation-goal:富商:相识" "私货能出手·陪代理谈投资·接触保险核赔")
(set-global! "relation-goal:富商:信任" "私货满价收购·投资本金打折")
(set-global! "relation-goal:富商:核心" "引荐博士，牵出实验室科技线（demo 暂未开放）")

;; 夜莺委托线的三次交锋。正式主线接入后逐个替换。
(define public-event-count 0)
(define public-event-max 3)
(define public-event-pending? #f)
(define public-event-delay-used? #f)

;; 故事节拍表：间隔 (2 7 7) 对应第 3 / 10 / 17 天上门。
;; 开场后第 2 个日终触发第一场，之后两场仍落在第 10 / 17 天。
(define public-event-intervals '(2 7 7))

(define (current-public-event-interval)
  (list-ref public-event-intervals public-event-count))

(define public-event-blocker-id "世界/夜莺公共事件")

(define (public-event-blocker-reason)
  (cond
    ((= public-event-count 0) "酒馆附近有人盯梢,必须过去看看。")
    ((= public-event-count 1) "收账人堵在门口,躲不掉。")
    ((= public-event-count 2) "了断之日到了,必须去。")
    (else "必须处理眼前的麻烦。")))

(define (public-event-clock-title)
  (cond
    ((= public-event-count 0) "盯梢的人")
    ((= public-event-count 1) "收账人再来")
    ((= public-event-count 2) "了断之日")
    (else "公共事件")))

(define (public-event-clock-note)
  (let ((stage (let ((v (get-global '夜莺阶段))) (if v v 0)))
        (beat1-progress (let ((v (get-global '夜莺查访进度))) (if v v 0)))
        (beat1-target (let ((v (get-global '夜莺查访目标))) (if v v 2))))
    (cond
      ((= public-event-count 0)
       (cond
         ((= stage 0) "雨夜有人敲门,先去看看是谁。")
         ((and (= stage 1) (< beat1-progress beat1-target)) "完成饭店与码头两处查访，拼出陌生人的藏身处。")
         (else "陌生人的藏身处已经揭晓。可以主动出击，也可以等他上门。")))
      ((= public-event-count 1)
       "收账人已经撂话。第 10 天到期；时钟显示距到期的剩余天数。至少要凑出一笔首期赎身钱,让他们先收手。")
      ((= public-event-count 2)
       "老板第 17 天亲自上门。付清封口钱、送她上船、让案子立起来,或者备好一场硬仗——路都摆在夜莺的卡上。")
      (else "归零后必须亲自处理。"))))

(define (public-event-pending-note)
  (cond
    ((= public-event-count 0) "盯梢的人已经上门,先处理才能睡。")
    ((= public-event-count 1) "收账人堵在门口,先处理才能睡。")
    ((= public-event-count 2) "了断之日到了,先处理才能睡。")
    (else "事情已经发生:必须先处理,才能结束一天。")))

(define (public-event-action-name)
  (cond
    ((= public-event-count 0) "赶去酒馆")
    ((= public-event-count 1) "迎上去")
    ((= public-event-count 2) "做个了断")
    (else "处理公共事件")))

;; ── 公共事件接口 ─────────────────────────────────
(define (sync-public-event-blocker!)
  (if public-event-pending?
      (rest-block! public-event-blocker-id (public-event-blocker-reason))
      (rest-release! public-event-blocker-id)))

;; 此时公共事件标题/备注/动作名函数均已定义，再创建时钟变量。
(define (reset-public-event-clock!)
  (set! next-public-event
        (make-clock (public-event-clock-title)
                    (current-public-event-interval)
                    'segments
                    (public-event-clock-note))))

(define next-public-event
  (make-clock (public-event-clock-title)
              (current-public-event-interval)
              'segments
              (public-event-clock-note)))

(define (public-event-active?)
  (< public-event-count public-event-max))

(define (public-event-can-delay?)
  (and (public-event-active?)
       (not public-event-pending?)
       (not public-event-delay-used?)
       (> (next-public-event 'current) 0)))

;; 具体官僚手段：每个小节至多延后一天，不能取消事件。
(define (delay-public-event-one-day!)
  (if (not (public-event-can-delay?))
      (error "delay-public-event-one-day!: event cannot be delayed now")
      #t)
  (next-public-event 'set! (max 0 (- (next-public-event 'current) 1)))
  (set! public-event-delay-used? #t)
  (notify! "阿瑟替你改了一张日期。他们会晚一天上门。"))

(define (set-public-event-pending!)
  (if (or public-event-pending? (not (public-event-active?)))
      (error "set-public-event-pending!: invalid public event state")
      #t)
  (set! public-event-pending? #t)
  (set-global! 'bout-idx public-event-count)
  (set-global! '夜莺主动上门 #f)
  (sync-public-event-blocker!))

;; 节拍一主动结算：玩家查清落脚处后主动上门。
;; 仍然走同一套 pending / encounter callback,保证存档与结算路径一致。
(define (begin-public-event-early!)
  (if (or public-event-pending? (not (public-event-active?)))
      (error "begin-public-event-early!: invalid public event state")
      #t)
  (set! public-event-pending? #t)
  (set-global! 'bout-idx public-event-count)
  (set-global! '夜莺主动上门 #t)
  (sync-public-event-blocker!))

(define (on-public-event-result result)
  (if (not public-event-pending?)
      (error "on-public-event-result: no pending public event")
      #t)
  (set! public-event-pending? #f)
  (sync-public-event-blocker!)
  (nightingale 'on-bout-result public-event-count result)
  (complete-section!)
  (set! public-event-count (+ public-event-count 1))
  (set! public-event-delay-used? #f)
  (if (public-event-active?)
      (reset-public-event-clock!)
      #f))

;; 节拍三·路线五(坐视不管)专用结算:与 on-public-event-result 同构,
;; 但背过身去不算经历完一段处境,不奖励成长点(不调 complete-section!)。
(define (on-final-event-walked-away!)
  (if (not public-event-pending?)
      (error "on-final-event-walked-away!: no pending public event")
      #t)
  (set! public-event-pending? #f)
  (sync-public-event-blocker!)
  (nightingale 'on-bout-result public-event-count 'walked-away)
  (set! public-event-count (+ public-event-count 1))
  (set! public-event-delay-used? #f)
  (if (public-event-active?)
      (reset-public-event-clock!)
      #f))

;; 取消后续公共事件(中途放她走)。回到无剧情静默状态。
(define (cancel-public-events!)
  (set! public-event-count public-event-max)
  (set! public-event-pending? #f)
  (sync-public-event-blocker!)
  (next-public-event 'set! 0))

(define (current-encounter-name)
  (cond
    ((= public-event-count 0) "夜莺·警告")
    ((= public-event-count 1) "夜莺·抢人")
    ((= public-event-count 2) "夜莺·了断")
    (else (error "current-encounter-name: public-event-count 超出范围"))))

;; 统一返回节点列表：节拍三按路线分派可能产出 0/1/2 个待办节点
;; （了断交锋入口 + 查明真相时的“去饭店喝酒”），节拍一/二的场景各自只有一个。
(define (node-public-event)
  (cond
    ((and (= public-event-count 1) (nightingale 'has-protection?))
     (list (instant-action "处理收账人再来"
             (lambda ()
               (nightingale 'resolve-protected-beat2!)
               (on-public-event-result 'success)))))
    ((= public-event-count 2)
     (nightingale 'beat3-pending-nodes))
    (else
     (list (encounter-action (public-event-action-name)
             (lambda ()
               (start-encounter (current-encounter-name) on-public-event-result)))))))

(define (public-event-clocks)
  (cond
    ((not (public-event-active?)) '())
    (public-event-pending?
     (let ((interval (current-public-event-interval)))
       (list (list 'clock (public-event-clock-title) 0 interval 'countdown
                   (public-event-pending-note)))))
    (else
     (let ((interval (current-public-event-interval)))
       (list (list 'clock (public-event-clock-title)
                   (- interval (next-public-event 'current))
                   interval 'countdown
                   (public-event-clock-note)))))))

;; 调试台使用。正式内容只通过日终规则推进。
(define (debug-trigger-public-event!)
  (if public-event-pending?
      #t
      (set-public-event-pending!)))

;; ── 日终规则 ─────────────────────────────────────
(define-turn-rule "世界日历推进"
  (lambda () #t)
  (lambda () (set! world-day (+ world-day 1))))

(define-turn-rule "公共事件推进"
  (lambda () (and (public-event-active?) (not public-event-pending?)))
  (lambda ()
    (next-public-event 'tick!)
    (if (next-public-event 'full?)
        (set-public-event-pending!)
        #f)))

;; ── 地点可见性 ───────────────────────────────────
(define location-predicates
  (list
    (list home            (lambda () #t))
    (list dock            (lambda () #t))
    (list old-street-tavern (lambda () #t))
    (list diner           (lambda () #t))
    (list clinic          (lambda () #t))
    (list park            (lambda () #t))
    (list police-station  (lambda () #t))
    (list freight-company (lambda () #t))
    (list residential-district (lambda () (joe 'residential-unlocked?)))
    (list insurance-company (lambda () (walter 'known?)))
    (list stranger-hideout (lambda () (nightingale 'hideout-visible?)))
    (list board           (lambda () #t))
    (list test            (lambda () (equal? (get-global 'chapter) "test")))))

(define (filter-locations entries)
  (if (null? entries)
      '()
      (let ((entry (car entries)))
        (if ((cadr entry))
            (cons (car entry) (filter-locations (cdr entries)))
            (filter-locations (cdr entries))))))

(define (current-locations)
  (filter-locations location-predicates))

;; ── 世界渲染 ─────────────────────────────────────
(define (get-render-data)
  (node "世界"
    :children
      (append
        (nightingale 'world-nodes)
        (if public-event-pending? (node-public-event) '())
        (nightingale 'render-data)
        (apply append (map (lambda (loc) (loc 'render-data)) (current-locations))))
    :clocks (public-event-clocks)))

;; ── 存档 ─────────────────────────────────────────
(define (world-save)
  (list
    (list "day" world-day)
    (list "public-event-count" public-event-count)
    (list "public-event-pending?" public-event-pending?)
    (list "public-event-delay-used?" public-event-delay-used?)
    (list "next-public-event" (next-public-event 'current))
    (list "home" (home 'save))
    (list "dock" (dock 'save))
    (list "old-street-tavern" (old-street-tavern 'save))
    (list "nightingale" (nightingale 'save))
    (list "diner" (diner 'save))
    (list "clinic" (clinic 'save))
    (list "park" (park 'save))
    (list "police-station" (police-station 'save))
    (list "freight-company" (freight-company 'save))
    (list "joe" (joe 'save))
    (list "frank" (frank 'save))
    (list "arthur" (arthur 'save))
    (list "walter" (walter 'save))
    (list "sam" (sam 'save))
    (list "residential-district" (residential-district 'save))
    (list "insurance-company" (insurance-company 'save))
    (list "stranger-hideout" (stranger-hideout 'save))
    (list "board" (board 'save))
    (list "test" (test 'save))))

(define (world-load! data)
  (clear-rest-blockers!)
  (set! world-day (assoc-get data "day" 1))
  (set! public-event-count (assoc-get data "public-event-count" 0))
  (set! public-event-pending? (assoc-get data "public-event-pending?" #f))
  (set! public-event-delay-used? (assoc-get data "public-event-delay-used?" #f))
  ;; next-public-event 的 max/标题由当前节拍决定。旧实现只恢复 current，
  ;; 导致读档后第三节拍仍沿用第一节拍的 3 格上限，三次日终便错误到期。
  (if (public-event-active?)
      (begin
        (reset-public-event-clock!)
        (next-public-event 'set! (assoc-get data "next-public-event" 0)))
      (next-public-event 'set! 0))
  (home 'load! (assoc-get data "home" '()))
  (dock 'load! (assoc-get data "dock" '()))
  (old-street-tavern 'load! (assoc-get data "old-street-tavern" '()))
  (nightingale 'load! (assoc-get data "nightingale" '()))
  (diner 'load! (assoc-get data "diner" '()))
  (clinic 'load! (assoc-get data "clinic" '()))
  (park 'load! (assoc-get data "park" '()))
  (police-station 'load! (assoc-get data "police-station" '()))
  (freight-company 'load! (assoc-get data "freight-company" '()))
  (joe 'load! (assoc-get data "joe" '()))
  (frank 'load! (assoc-get data "frank" '()))
  (arthur 'load! (assoc-get data "arthur" '()))
  (walter 'load! (assoc-get data "walter" '()))
  (sam 'load! (assoc-get data "sam" '()))
  (residential-district 'load! (assoc-get data "residential-district" '()))
  (insurance-company 'load! (assoc-get data "insurance-company" '()))
  (stranger-hideout 'load! (assoc-get data "stranger-hideout" '()))
  (board 'load! (assoc-get data "board" '()))
  (test 'load! (assoc-get data "test" '()))
  (sync-public-event-blocker!)
  (nightingale 'sync-blockers!))

;; 初始同步:新游戏没有存档数据时,也要阻塞第一晚的睡眠。
(nightingale 'sync-blockers!)
