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

;; 世界级状态（真正跨地点共享的）
(define world-day 1)

;; 章节清单 —— 添加新章节只改这里
(define chapter-locations
  (list
    (list 0  home theater)
    (list 1  home theater restaurant clinic abandoned-warehouse)))

(define (find-chapter-locs ch lst)
  (if (null? lst)
      (list home theater)
      (if (= (car (car lst)) ch)
          (cdr (car lst))
          (find-chapter-locs ch (cdr lst)))))

;; 第一章码头解锁条件
(define (chapter-1-dock-unlocked?)
  (and (restaurant 'complete?) (abandoned-warehouse 'complete?)))

(define (current-locations)
  (let ((ch (get-global 'chapter)))
    (if (or (equal? ch #f) (equal? ch "test") (equal? ch "all"))
        (list home office merchant test board club theater restaurant clinic abandoned-warehouse dock)
        (let ((base (find-chapter-locs ch chapter-locations)))
          (if (and (= ch 1) (chapter-1-dock-unlocked?))
              (append base (list dock))
              base)))))

;; 世界渲染：只渲染当前章节的地点
(define (get-render-data)
  (apply append (map (lambda (loc) (loc 'render-data)) (current-locations))))

;; 存档：返回带 key 的 assoc-list，方便将来扩展
(define (world-save)
  (list
    (list "day"        world-day)
    (list "home"       (home             'save))
    (list "office"     (office           'save))
    (list "club"       (club             'save))
    (list "board"      (board            'save))
    (list "merchant"   (merchant         'save))
    (list "test"       (test             'save))
    (list "theater"    (theater          'save))
    (list "restaurant" (restaurant       'save))
    (list "clinic"     (clinic           'save))
    (list "warehouse"  (abandoned-warehouse 'save))
    (list "dock"       (dock             'save))))

;; 读档：用 assoc-get 按 key 取值，缺失时有 default，健壮
(define (world-load! data)
  (set! world-day (assoc-get data "day" 1))
  (home             'load! (assoc-get data "home"       '()))
  (office           'load! (assoc-get data "office"     '()))
  (club             'load! (assoc-get data "club"       '()))
  (board            'load! (assoc-get data "board"      '()))
  (merchant         'load! (assoc-get data "merchant"   '()))
  (test             'load! (assoc-get data "test"       '()))
  (theater          'load! (assoc-get data "theater"    '()))
  (restaurant       'load! (assoc-get data "restaurant" '()))
  (clinic           'load! (assoc-get data "clinic"     '()))
  (abandoned-warehouse 'load! (assoc-get data "warehouse"  '()))
  (dock             'load! (assoc-get data "dock"       '())))
