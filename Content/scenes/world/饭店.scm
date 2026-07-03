;; scenes/world/饭店.scm - 老街饭摊（劳工）
;; 低风险生计：刷盘子（稳、钱少、压力低）＋ 买食物（补饱腹）。
;; 与码头形成对比：图个稳 vs 博一把。

(define diner
  (let ()

    ;; 刷盘子：低风险零工。好=钱+关系；坏=轻微压力。
    (define (node-dishwash)
      (工作 "刷盘子" "劳工" '低 'violence
        (outcome "额外赏钱" "一摞盘子刷得锃亮，老板多给了两个铜板。" ; 好
          (lambda () (add-item! "金钱" 6)))
        (outcome "按日结算" "刷了一天盘子，拿到该拿的。"            ; 中
          (lambda () (add-item! "金钱" 3)))
        (outcome "摔碎餐具" "摔了个碗，被数落了半天。"              ; 坏
          (lambda () (stress-current-actor! 1)))))

    ;; 买食物：花钱补饱腹的物资。
    (define (node-buy-food)
      (action "买食物"
        (list (req-item "金钱" 6))
        (instant (lambda ()
                   (add-item! "食物" 1)
                   (notify! "买了些干粮，够对付几顿。")))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (container "老街饭摊"
                   (list (node-dishwash) (node-buy-food)))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (#t #f))))))
