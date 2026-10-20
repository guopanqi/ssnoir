;; 街角咖啡馆——低风险收入与第一节侦探委托的自然入口。
;; 不设置独立的任务链。帮工本身就让尼尔接触来往的客人，
;; 由照片委托模块持有小道消息及后续剧情。
(define cafe-work
  (let ()
    (define calm-rest-day 0) ; 好结果每日最多恢复一点冷静

    ;; 一次帮工只做三件事：进钱、进消息、动一点冷静。
    ;; calm 为正＝好结果回冷静（当日限一次）；为负＝坏结果掉冷静；0＝不动。
    (define (work-result! money rumour calm)
      (if (> money 0) (add-item! "金钱" money) #f)
      (cond
        ((> calm 0)
         (if (= calm-rest-day world-day)
             #f
             (begin
               (set! calm-rest-day world-day)
               (restore-actor-composure! 'player calm))))
        ((< calm 0) (spend-composure! (- calm)))
        (else #f))
      (if (> rumour 0) (photo-case 'hear-rumours! rumour) #f))

    (define (nodes-at location)
      (if (equal? location "街角咖啡馆")
          (list
            (工作 "咖啡馆帮工" '低 'social
              (outcome (lambda () (work-result! 10 2 1)))
              (outcome (lambda () (work-result! 4 1 0)))
              (outcome (lambda () (work-result! 0 0 -1)))
              "安稳的工作，工资微薄，但偶尔能够令人感觉到平静。"))
          '()))

    (lambda args
      (cond
        ((eq? (car args) 'nodes-at) (nodes-at (cadr args)))
        ((eq? (car args) 'save)
         (list (list "calm-rest-day" calm-rest-day)))
        ((eq? (car args) 'load!)
         (set! calm-rest-day (assoc-get (cadr args) "calm-rest-day" 0)))
        (else (error "咖啡馆：未知消息"))))))

;; 咖啡馆暂无独立 3D 锚点：先借照相馆的锚点，把帮工卡落在那一角。
(define cafe (纯投射地点 "街角咖啡馆" "街角咖啡馆"))
