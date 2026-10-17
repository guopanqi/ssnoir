;; 街角咖啡馆——低风险收入与第一节侦探委托的自然入口。
;; 不设置独立的任务链。帮工本身就让尼尔接触来往的客人，
;; 由照片委托模块持有小道消息及后续剧情。
(define cafe-work
  (let ()
    (define calm-rest-day 0) ; 好结果每日最多恢复一点冷静

    (define (work-result! money rumour calm?)
      (add-item! "金钱" money)
      (if (and calm? (not (= calm-rest-day world-day)))
          (begin
            (set! calm-rest-day world-day)
            (restore-actor-composure! 'player 1))
          #f)
      (photo-case 'hear-rumours! rumour))

    (define (nodes-at location)
      (if (equal? location "街角咖啡馆")
          (list
            (工作 "咖啡馆帮工" '低 'social
              (outcome (lambda () (work-result! 6 2 #t)))
              (outcome (lambda () (work-result! 4 1 #f)))
              (outcome (lambda () (work-result! 2 1 #f)))
              "小额收入；好结果今日可恢复1冷静"))
          '()))

    (lambda args
      (cond
        ((eq? (car args) 'nodes-at) (nodes-at (cadr args)))
        ((eq? (car args) 'save)
         (list (list "calm-rest-day" calm-rest-day)))
        ((eq? (car args) 'load!)
         (set! calm-rest-day (assoc-get (cadr args) "calm-rest-day" 0)))
        (else (error "咖啡馆：未知消息"))))))

;; 咖啡馆暂无独立 3D 锚点；由现有地点系统将动作卡放在网格区域。
(define cafe (纯投射地点 "街角咖啡馆"))
