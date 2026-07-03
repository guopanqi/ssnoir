;; scenes/world/world.scm - 世界协调器（城市生活第一版）
;; 主线已移除。这一版只验证城市生活模拟：地点默认开放，睡觉推进日历。
;; 傍晚结算 / 涌现事件 / 关系·资产门都从这里扩展。

(load-file "world/home.scm")
(load-file "world/码头.scm")
(load-file "world/饭店.scm")
(load-file "world/诊所.scm")
(load-file "world/银行.scm")
(load-file "world/公园.scm")
(load-file "world/test.scm")

;; ── 世界级状态 ───────────────────────────────────
(define world-day 1)

;; 睡觉推进一天（Team.EndTurn 已处理饱腹/压力/重掷骰；这里管世界日历）。
;; 傍晚结算的钩子先占位：以后在这里生成新闻 / 涌现事件 / 势力状态变化。
(define-turn-rule "世界日历推进"
  (lambda () #t)
  (lambda ()
    (set! world-day (+ world-day 1))))

;; ── 地点可见性表 ─────────────────────────────────
;; 默认开放。将来某地点要关系/资产门，把 (lambda () #t) 换成条件即可。
;; test 仅调试模式（chapter="test"）出现。
(define location-predicates
  (list
    (list home   (lambda () #t))
    (list dock   (lambda () #t))
    (list diner  (lambda () #t))
    (list clinic (lambda () #t))
    (list bank   (lambda () #t))
    (list park   (lambda () #t))
    (list test   (lambda () (equal? (get-global 'chapter) "test")))))

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
  (container "世界"
    (apply append (map (lambda (loc) (loc 'render-data)) (current-locations)))))

;; ── 存档 ─────────────────────────────────────────
(define (world-save)
  (list
    (list "day"    world-day)
    (list "home"   (home   'save))
    (list "dock"   (dock   'save))
    (list "diner"  (diner  'save))
    (list "clinic" (clinic 'save))
    (list "bank"   (bank   'save))
    (list "park"   (park   'save))
    (list "test"   (test   'save))))

(define (world-load! data)
  (clear-rest-blockers!)
  (set! world-day (assoc-get data "day" 1))
  (home   'load! (assoc-get data "home"   '()))
  (dock   'load! (assoc-get data "dock"   '()))
  (diner  'load! (assoc-get data "diner"  '()))
  (clinic 'load! (assoc-get data "clinic" '()))
  (bank   'load! (assoc-get data "bank"   '()))
  (park   'load! (assoc-get data "park"   '()))
  (test   'load! (assoc-get data "test"   '())))
