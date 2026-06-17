;; scenes/world/world.scm - World Coordinator

(load-file "world/home.scm")
(load-file "world/office.scm")
(load-file "world/club.scm")
(load-file "world/board.scm")
(load-file "world/merchant.scm")
(load-file "world/test.scm")
(load-file "world/剧院.scm")
(load-file "world/饭店.scm")
(load-file "world/诊所.scm")
(load-file "world/废弃仓库.scm")
(load-file "world/码头.scm")
(load-file "world/市集.scm")
(load-file "world/码头居民区.scm")
(load-file "world/酒吧.scm")

;; 世界级状态（真正跨地点共享的）
(define world-day 1)

;; ── Progression 层 ───────────────────────────────────
;; 地点完成时统一调用 mark-complete!，不直接散写 set-global!
(define (mark-complete! flag)
  (set-global! flag #t)
  (check-world-unlocks!))

;; 所有跨地点解锁逻辑集中在这里。新增章节解锁：往里加 if 块。
(define (check-world-unlocks!)
  (if (and (get-global 'restaurant-complete)
           (get-global 'warehouse-complete)
           (not (get-global 'dock-available)))
      (begin
        (set-global! 'dock-available #t)
        (notify! "码头的消息已经够用了——该去找老陈了。"))
      #f))

;; ── 地点可见性表 ─────────────────────────────────────
;; 新增地点加一行，改解锁条件改一行，current-locations 不用动。
;; office / club / test 仅测试模式出现（正常章节中谓词为 #f）。
(define (chapter>=? n)
  (>= (or (get-global 'chapter) 0) n))

(define location-predicates
  (list
    (list home                (lambda () #t))
    (list theater             (lambda () #t))
    (list board               (lambda () (chapter>=? 0)))
    (list office              (lambda () #f))
    (list club                (lambda () #f))
    (list test                (lambda () #f))
    (list restaurant          (lambda () (chapter>=? 1)))
    (list clinic              (lambda () (chapter>=? 1)))
    (list abandoned-warehouse (lambda () (chapter>=? 1)))
    (list dock                (lambda () (get-global 'dock-available)))
    (list market              (lambda () (get-global 'dock-market-open)))
    (list residential         (lambda () (get-global 'dock-residential-open)))
    (list bar                 (lambda () (get-global 'dock-bar-open)))))

(define (filter-locations entries)
  (if (null? entries)
      '()
      (let ((entry (car entries)))
        (if ((cadr entry))
            (cons (car entry) (filter-locations (cdr entries)))
            (filter-locations (cdr entries))))))

(define (current-locations)
  (let ((ch (get-global 'chapter)))
    (if (or (equal? ch "test") (equal? ch "all"))
        (map car location-predicates)
        (filter-locations location-predicates))))

;; 世界渲染：世界本身也是场景树根节点。
(define (get-render-data)
  (container "世界"
    (apply append (map (lambda (loc) (loc 'render-data)) (current-locations)))))

;; 存档：返回带 key 的 assoc-list，方便将来扩展
(define (world-save)
  (list
    (list "day"         world-day)
    (list "home"        (home                'save))
    (list "office"      (office              'save))
    (list "club"        (club                'save))
    (list "board"       (board               'save))
    (list "merchant"    (merchant            'save))
    (list "test"        (test                'save))
    (list "theater"     (theater             'save))
    (list "restaurant"  (restaurant          'save))
    (list "clinic"      (clinic              'save))
    (list "warehouse"   (abandoned-warehouse 'save))
    (list "dock"        (dock                'save))
    (list "bar"         (bar                 'save))))

;; 读档：用 assoc-get 按 key 取值，缺失时有 default，健壮
(define (world-load! data)
  (set! world-day (assoc-get data "day" 1))
  (home                'load! (assoc-get data "home"        '()))
  (office              'load! (assoc-get data "office"      '()))
  (club                'load! (assoc-get data "club"        '()))
  (board               'load! (assoc-get data "board"       '()))
  (merchant            'load! (assoc-get data "merchant"    '()))
  (test                'load! (assoc-get data "test"        '()))
  (theater             'load! (assoc-get data "theater"     '()))
  (restaurant          'load! (assoc-get data "restaurant"  '()))
  (clinic              'load! (assoc-get data "clinic"      '()))
  (abandoned-warehouse 'load! (assoc-get data "warehouse"   '()))
  (dock                'load! (assoc-get data "dock"        '()))
  (bar                 'load! (assoc-get data "bar"         '())))
