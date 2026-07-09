;; scenes/world/world.scm - 世界协调器（城市生活第一版）
;; 世界拥有日期与强制公共事件；地点只拥有自己的生活内容。

(load-file "world/home.scm")
(load-file "world/码头.scm")
(load-file "world/老街酒馆.scm")
(load-file "world/夜莺.scm")
(load-file "world/饭店.scm")
(load-file "world/诊所.scm")
(load-file "world/公园.scm")
(load-file "world/警局.scm")
(load-file "world/货运公司.scm")
(load-file "world/board.scm")
(load-file "world/test.scm")

;; ── 世界级状态 ───────────────────────────────────
(define world-day 1)

;; 夜莺委托线的三次交锋。正式主线接入后逐个替换。
(define public-event-count 0)
(define public-event-max 3)
(define public-event-pending? #f)
(define public-event-delay-used? #f)

;; 故事节拍表：间隔 (3 6 7) 对应第 4 / 10 / 17 天上门。
;; 开场后第 3 个日终触发第一场，之后两场间隔递增。
(define public-event-intervals '(3 6 7))

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
        (beat1-target (let ((v (get-global '夜莺查访目标))) (if v v 4))))
    (cond
      ((= public-event-count 0)
       (cond
         ((= stage 0) "雨夜有人敲门,先去看看是谁。")
         ((and (= stage 1) (< beat1-progress beat1-target)) "查出盯梢者的落脚处。填满后可以主动找上门。")
         (else "你已经摸到他的落脚处。可以主动去找他,也可以等他上门。")))
      ((= public-event-count 1)
       "收账人已经撂话。第 10 天前,至少要凑出一笔首期赎身钱,让他们先收手。")
      ((= public-event-count 2)
       "他们要的是一条命的交代。这次不能输。")
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
  (notify! "探长替你压了一天。他们会晚一天上门。"))

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

(define (node-public-event)
  (if (and (= public-event-count 1) (nightingale 'has-protection?))
      (instant-action "处理收账人再来"
        (lambda ()
          (nightingale 'resolve-protected-beat2!)
          (on-public-event-result 'success)))
      (encounter-action (public-event-action-name)
        (lambda ()
          (start-encounter (current-encounter-name) on-public-event-result)))))

(define (public-event-clocks)
  (let ((interval (current-public-event-interval)))
    (cond
      ((not (public-event-active?)) '())
      (public-event-pending?
       (list (list 'clock (public-event-clock-title) 0 interval 'countdown
                   (public-event-pending-note))))
      (else
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
        (if public-event-pending? (list (node-public-event)) '())
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
    (list "board" (board 'save))
    (list "test" (test 'save))))

(define (world-load! data)
  (clear-rest-blockers!)
  (set! world-day (assoc-get data "day" 1))
  (set! public-event-count (assoc-get data "public-event-count" 0))
  (set! public-event-pending? (assoc-get data "public-event-pending?" #f))
  (set! public-event-delay-used? (assoc-get data "public-event-delay-used?" #f))
  (next-public-event 'set! (assoc-get data "next-public-event" 0))
  (home 'load! (assoc-get data "home" '()))
  (dock 'load! (assoc-get data "dock" '()))
  (old-street-tavern 'load! (assoc-get data "old-street-tavern" '()))
  (nightingale 'load! (assoc-get data "nightingale" '()))
  (diner 'load! (assoc-get data "diner" '()))
  (clinic 'load! (assoc-get data "clinic" '()))
  (park 'load! (assoc-get data "park" '()))
  (police-station 'load! (assoc-get data "police-station" '()))
  (freight-company 'load! (assoc-get data "freight-company" '()))
  (board 'load! (assoc-get data "board" '()))
  (test 'load! (assoc-get data "test" '()))
  (sync-public-event-blocker!)
  (nightingale 'sync-blockers!))

;; 初始同步:新游戏没有存档数据时,也要阻塞第一晚的睡眠。
(nightingale 'sync-blockers!)
