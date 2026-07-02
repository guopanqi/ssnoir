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
(load-file "world/老街市集.scm")
(load-file "world/老街居民区.scm")
(load-file "world/酒吧.scm")

;; 世界级状态（真正跨地点共享的）
(define world-day 1)

;; ── 压力层：经理耐心（案件 deadline）───────────────────
;; 每天（睡觉）-1；归零 = 案件失败。耐心只能回剧院找经理"交付"完成的小节来恢复。
(define manager-patience 8)
(define manager-patience-max 8)
(define case-failed? #f)

;; 待交付的小节数：完成里程碑 +1；回剧院找经理交付 → 耐心 +report-reward、计数 -1。
(define pending-reports 0)
(define report-reward 4)

(define (patience-render-data)
  (list 'clock "经理耐心" manager-patience manager-patience-max 'countdown))

;; 里程碑完成：积累一个"待交付进展"（不直接回血，要跑一趟剧院交付）。
(define (add-pending-report!)
  (set! pending-reports (+ pending-reports 1))
  (notify! "案件有了进展。回剧院向经理交付，能恢复他的耐心。"))

(define (has-pending-report?) (> pending-reports 0))

;; 在剧院找经理交付一个小节。
(define (deliver-report!)
  (if (has-pending-report?)
      (begin
        (set! pending-reports (- pending-reports 1))
        (set! manager-patience (min manager-patience-max (+ manager-patience report-reward)))
        (notify! (string-append "你向经理交付了进展，他的耐心恢复到 "
                                (number->string manager-patience) "/"
                                (number->string manager-patience-max) "。")))
      #f))

;; ── Progression 层 ───────────────────────────────────
;; 地点完成时统一调用 mark-complete!，不直接散写 set-global!
(define (mark-complete! flag)
  (set-global! flag #t)
  (add-pending-report!)
  (check-world-unlocks!))

;; ── 经理耐心每日流逝（睡觉触发 on-turn-end）────────────
(define-turn-rule "经理耐心流逝"
  (lambda () (not case-failed?))
  (lambda ()
    (set! manager-patience (- manager-patience 1))
    (if (<= manager-patience 0)
        (begin
          (set! manager-patience 0)
          (set! case-failed? #t)
          (spotlight! "经理失去了耐心"
                      "电话那头沉默了片刻。'算了，这案子我另请高明。'"))
        (if (<= manager-patience 2)
            (notify! "经理的耐心快用完了，回去交付点进展吧。")
            #f))))

;; ── 黑衣人追击：世界层节点。剧院只发信号，世界管显隐与生命周期 ──
;; 状态用字符串（存档友好）："hidden" → "available" → "done"
(define chase-state "hidden")

(define (reveal-chase!) (set! chase-state "available"))

(define (node-chase)
  (encounter-action "黑衣人"
    (lambda ()
      (start-encounter "追击黑衣人"
        (lambda (result)
          (set! chase-state "done")
          (damage-party! 1)
          (add-actor-stress! 'player 1)
          (advance-chapter!)
          (add-pending-report!)
          (notify! "你没能抓住他，但从他身上扯下了点东西。顺着这条线查下去吧。"))))))

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
    (list board               (lambda () #f))
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
;; 黑衣人追击节点是地点们的兄弟，由世界按 chase-state 显隐。
(define (get-render-data)
  (container "世界"
    (append
      (apply append (map (lambda (loc) (loc 'render-data)) (current-locations)))
      (if (equal? chase-state "available")
          (list (node-chase))
          '()))))

;; 存档：返回带 key 的 assoc-list，方便将来扩展
(define (world-save)
  (list
    (list "day"               world-day)
    (list "manager-patience"  manager-patience)
    (list "case-failed?"      case-failed?)
    (list "pending-reports"   pending-reports)
    (list "chase-state"       chase-state)
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
    (list "residential" (residential         'save))
    (list "bar"         (bar                 'save))))

;; 读档：用 assoc-get 按 key 取值，缺失时有 default，健壮
(define (world-load! data)
  (set! world-day (assoc-get data "day" 1))
  (set! manager-patience (assoc-get data "manager-patience" manager-patience-max))
  (set! case-failed? (assoc-get data "case-failed?" #f))
  (set! pending-reports (assoc-get data "pending-reports" 0))
  (set! chase-state (assoc-get data "chase-state" "hidden"))
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
  (residential         'load! (assoc-get data "residential" '()))
  (bar                 'load! (assoc-get data "bar"         '())))
