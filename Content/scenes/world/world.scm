;; scenes/world/world.scm - World Coordinator

(load-file "world/home.scm")
(load-file "world/office.scm")
(load-file "world/club.scm")
(load-file "world/board.scm")
(load-file "world/merchant.scm")
(load-file "world/test.scm")

;; 世界级状态（真正跨地点共享的）
(define world-day 1)

;; 世界渲染：直接返回所有地点的节点，UI 负责导航焦点
(define (get-render-data)
  (let ((base-nodes (list
                      (container "家" (home 'render-data))
                      (container "办公室" (office 'render-data))
                      (container "黑市商人" (merchant 'render-data))
                      (container "测试" (test 'render-data))
                      (container "告示板" (board 'render-data)))))
    (if (>= (get-reputation "elites") 40)
        (cons (container "精英俱乐部" (club 'render-data)) base-nodes)
        base-nodes)))

;; 存档：返回带 key 的 assoc-list，方便将来扩展
(define (world-save)
  (list
    (list "day"    world-day)
    (list "home"   (home   'save))
    (list "office" (office 'save))
    (list "club"   (club   'save))
    (list "board"  (board  'save))
    (list "merchant" (merchant 'save))
    (list "test"   (test   'save))))

;; 读档：用 assoc-get 按 key 取值，缺失时有 default，健壮
(define (world-load! data)
  (set! world-day (assoc-get data "day" 1))
  (home   'load! (assoc-get data "home"   '()))
  (office 'load! (assoc-get data "office" '()))
  (club   'load! (assoc-get data "club"   '()))
  (board  'load! (assoc-get data "board"  '()))
  (merchant 'load! (assoc-get data "merchant" '()))
  (test   'load! (assoc-get data "test"   '())))
