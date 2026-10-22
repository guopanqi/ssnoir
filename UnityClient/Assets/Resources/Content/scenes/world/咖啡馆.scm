;; 街角咖啡馆——低风险收入与露西的日常片段。
;; 不设置独立的任务链。帮工本身就让尼尔接触来往的客人，
;; 工作资源由这里结算，一次性人物片段由第一幕拥有。
(define cafe-work
  (let ()
    (define calm-rest-day 0) ; 好结果每日最多恢复一点冷静

    ;; 工资、冷静先结算，人物片段不额外收费。
    ;; calm 为正＝好结果回冷静（当日限一次）；为负＝坏结果掉冷静；0＝不动。
    (define (work-result! money grade calm)
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
      (第一幕 'on-work! "街角咖啡馆" grade)
      (第二幕 'on-cafe-work!))

    (define (nodes-at location)
      (if (equal? location "街角咖啡馆")
          (list
            (at-anchor "街角咖啡馆"
              (note-node "露西·咖啡馆" "露西" ""))
            (工作 "咖啡馆帮工" '稳妥 'social
              (outcome (lambda () (work-result! 10 '好 1)))
              (outcome (lambda () (work-result! 4 '中 0)))
              (outcome (lambda () (work-result! 0 '坏 -1)))
              "安稳的工作，工资微薄，但偶尔能够令人感觉到平静。"
              :risk-target (list 'composure "player")
              :progress-target (list 'item "金钱")))
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
