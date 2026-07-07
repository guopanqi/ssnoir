;; scenes/world/world.scm - 世界协调器（城市生活第一版）
;; 世界拥有日期、强制公共事件及跨地点的异常货单状态；地点只拥有自己的生活内容。

(load-file "world/home.scm")
(load-file "world/码头.scm")
(load-file "world/饭店.scm")
(load-file "world/诊所.scm")
(load-file "world/银行.scm")
(load-file "world/公园.scm")
(load-file "world/警局.scm")
(load-file "world/货运公司.scm")
(load-file "world/board.scm")
(load-file "world/test.scm")

;; ── 世界级状态 ───────────────────────────────────
(define world-day 1)

;; 三次相同交锋暂代三个主线小节。正式主线接入后逐个替换。
(define public-event-count 0)
(define public-event-max 3)
(define public-event-interval 6)
(define public-event-pending? #f)
(define public-event-delay-used? #f)
(define next-public-event (make-clock "码头公共事件" public-event-interval 'segments))

(define public-event-blocker-id "世界/码头公共事件")
(define public-event-blocker-reason "城市：必须处理码头公共事件")

;; 具名线索跨交锋和三个地点使用，归世界全局状态所有。
(define invoice-state-key '异常货单状态)

(define (ensure-invoice-state!)
  (if (get-global invoice-state-key)
      #t
      (set-global! invoice-state-key "未出现")))

(define (abnormal-invoice-state)
  (ensure-invoice-state!)
  (get-global invoice-state-key))

(define (abnormal-invoice-held?)
  (equal? (abnormal-invoice-state) "持有"))

(define (deliver-abnormal-invoice! recipient)
  (if (not (abnormal-invoice-held?))
      (error "deliver-abnormal-invoice!: abnormal invoice is not held")
      #t)
  (cond
    ((equal? recipient "老周") (set-global! invoice-state-key "交给老周"))
    ((equal? recipient "探长") (set-global! invoice-state-key "交给探长"))
    ((equal? recipient "货运代理") (set-global! invoice-state-key "卖给代理人"))
    (else (error "deliver-abnormal-invoice!: unknown recipient"))))

(ensure-invoice-state!)

;; ── 公共事件接口 ─────────────────────────────────
(define (sync-public-event-blocker!)
  (if public-event-pending?
      (rest-block! public-event-blocker-id public-event-blocker-reason)
      (rest-release! public-event-blocker-id)))

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
  (notify! "探长替你压了一天。码头的冲突会晚一天发生。"))

(define (set-public-event-pending!)
  (if (or public-event-pending? (not (public-event-active?)))
      (error "set-public-event-pending!: invalid public event state")
      #t)
  (set! public-event-pending? #t)
  (set-global! 'bout-idx public-event-count)
  (sync-public-event-blocker!))

(define (on-public-event-result result)
  (if (not public-event-pending?)
      (error "on-public-event-result: no pending public event")
      #t)
  (set! public-event-pending? #f)
  (sync-public-event-blocker!)
  ;; 探长担保只在失败时用掉：压下官方风声、减轻这场失利的后续。
  (let ((guaranteed? (and (equal? result 'fail) (get-global '探长愿意担保))))
    (if guaranteed? (set-global! '探长愿意担保 #f) #f)
    (dock 'on-public-event result guaranteed?))
  (complete-section!)
  (set! public-event-count (+ public-event-count 1))
  (set! public-event-delay-used? #f)
  (if (public-event-active?)
      (next-public-event 'reset!)
      #f))

(define (node-public-event)
  (encounter-action "处理码头公共事件"
    (lambda ()
      (start-encounter "找上门的人" on-public-event-result))))

(define (public-event-clocks)
  (cond
    ((not (public-event-active?)) '())
    (public-event-pending?
     (list (list 'clock "码头公共事件" 0 public-event-interval 'countdown
                 "事情已经发生：必须先处理，才能结束一天。")))
    (else
     (list (list 'clock "码头公共事件"
                 (- public-event-interval (next-public-event 'current))
                 public-event-interval 'countdown
                 "归零后必须亲自处理；准备只能削小压力，不能跳过。")))))

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
    ; (list diner           (lambda () #t))
    ; (list clinic          (lambda () #t))
    ; (list bank            (lambda () #t))
    ; (list park            (lambda () #t))
    ; (list police-station  (lambda () #t))
    ; (list freight-company (lambda () #t))
    ; (list board           (lambda () #t))
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
        (if public-event-pending? (list (node-public-event)) '())
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
    (list "diner" (diner 'save))
    (list "clinic" (clinic 'save))
    (list "bank" (bank 'save))
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
  (ensure-invoice-state!)
  (home 'load! (assoc-get data "home" '()))
  (dock 'load! (assoc-get data "dock" '()))
  (diner 'load! (assoc-get data "diner" '()))
  (clinic 'load! (assoc-get data "clinic" '()))
  (bank 'load! (assoc-get data "bank" '()))
  (park 'load! (assoc-get data "park" '()))
  (police-station 'load! (assoc-get data "police-station" '()))
  (freight-company 'load! (assoc-get data "freight-company" '()))
  (board 'load! (assoc-get data "board" '()))
  (test 'load! (assoc-get data "test" '()))
  (sync-public-event-blocker!))
