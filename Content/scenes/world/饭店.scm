;; scenes/world/饭店.scm - 老街饭摊（劳工）
;; 低风险生计：刷盘子（稳、钱少、压力低）＋ 买食物（补饱腹）。
;; 与码头形成对比：图个稳 vs 博一把。

(define diner
  (let ()

    ;; 刷盘子：低风险零工。好=钱+关系；坏=轻微压力。
    (define (node-dishwash)
      (工作 "刷盘子" "劳工" '低 'violence
        (outcome "额外赏钱" "一摞盘子刷得能照见人影，老板难得多赏了两个铜板。" ; 好
          (lambda () (add-item! "金钱" 6)))
        (outcome "按日结算" "刷了一天盘子，手泡得发白，拿到该拿的那份。"            ; 中
          (lambda () (add-item! "金钱" 3)))
        (outcome "摔碎餐具" "手一滑摔了个碗，老板的唾沫星子飞了半天。"              ; 坏
          (lambda () (stress-current-actor! 1)))))

    ;; 买食物：花钱补饱腹的物资，带回去吃（home 的"吃饭"消耗）。
    (define (node-buy-food)
      (action "买食物"
        (list (req-item "金钱" 6))
        (instant
          (outcome "买了些干粮" "买了些干粮，够对付几顿。揣着，饿不着。"
            (lambda () (add-item! "食物" 1))))))

    ;; 在饭摊当场吃：不带走、不占"食物"物资，图个快。
    (define (node-eat-here)
      (node "在饭摊直接吃"
        :subtitle "不用带回去，掏钱当场吃饱"
        :requires (list (req-item "金钱" 6))
        :resolve (instant
          (outcome "吃了顿热乎的" "老板给盛了一碗，站在摊子边就着热气吃完。"
            (lambda () (add-satiety! 3))))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (container "老街饭摊"
                   (list (node-dishwash) (node-eat-here) (node-buy-food)))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (#t #f))))))
