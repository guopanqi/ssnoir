;; 城市事件唯一装配入口。地点无需登记逐个委托；清单决定加载、抽取和存档。
(load-file "world/城市事件/乔的手提箱.scm")
(load-file "world/城市事件/海伦的委托.scm")
(define 城市事件
  (let ()
    (define 上次检查日 0)
    (define (清单)
      (list (list "乔的手提箱" 乔的手提箱 '定向)
            (list "海伦的委托" 海伦的委托 '随机)))
    (define (汇总 msg . args)
      (apply append (map (lambda (row) (apply (cadr row) (cons msg args))) (清单))))
    (define (新日!)
      ;; 先结算已有事件，再抽新事件；同日重复调用不重复抽取。
      (map (lambda (row) ((cadr row) 'on-day-start!)) (清单))
      (if (> world-day 上次检查日)
        (begin
          (set! 上次检查日 world-day)
          ;; 身份只代表入行；罗科案交差后才有陌生客户慕名来访。
          (if (第三幕 'reported?)
            (let ((候选 (filter (lambda (row) (and (eq? (caddr row) '随机) ((cadr row) 'eligible?))) (清单))))
              ;; 测试阶段必定出现一个；池空即停止，不循环、不补发。
              (if (null? 候选) #f ((cadr (random-choice 候选)) 'activate!))) #f)) #f))
    (lambda (msg . args)
      (cond
        ((eq? msg 'on-day-start!) (新日!))
        ((eq? msg 'arrivals-at) (海伦的委托 'arrivals-at (car args)))
        ((eq? msg 'nodes-at) (汇总 'nodes-at (car args)))
        ((eq? msg 'dossier) (汇总 'dossier))
        ((eq? msg 'save)
          (list (list "last-day" 上次检查日)
            (list "events" (map (lambda (row) (list (car row) ((cadr row) 'save))) (清单)))))
        ((eq? msg 'load!)
          (let* ((data (car args)) (events (assoc-get data "events" '())))
            (set! 上次检查日 (assoc-get data "last-day" 0))
            (if (and (number? 上次检查日) (>= 上次检查日 0)) #t (error "城市事件：非法检查日期"))
            (map (lambda (row) ((cadr row) 'load! (assoc-get events (car row) '()))) (清单))
            (map (lambda (row) (if (or (equal? (car row) "经理的演出服") (assoc-get (清单) (car row) #f)) #t
              (error "城市事件：存档包含已移除事件，必须明确迁移"))) events)))
        (else (error "城市事件：未知消息"))))))
