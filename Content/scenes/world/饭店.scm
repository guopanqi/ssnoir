;; scenes/world/饭店.scm - 老街饭摊（劳工）
;; 低风险生计：刷盘子（稳、钱少、偶尔破防）。
;; 与码头形成对比：图个稳 vs 博一把。
;; 饱腹系统已移除（其"城市日常消耗"职能由房租接管），"买食物"/"在饭摊直接吃"随之删除。

(define diner
  (let ()

    ;; 刷盘子：低风险零工。好=钱+关系；坏=轻微破防。
    (define (node-dishwash)
      (工作 "刷盘子" "劳工" '低 'violence
        (outcome "额外赏钱" "一摞盘子刷得能照见人影，老板难得多赏了两个铜板。" ; 好
          (lambda () (add-item! "金钱" 6)))
        (outcome "按日结算" "刷了一天盘子，手泡得发白，拿到该拿的那份。"            ; 中
          (lambda () (add-item! "金钱" 3)))
        (outcome "摔碎餐具" "手一滑摔了个碗，老板的唾沫星子飞了半天。"              ; 坏
          (lambda () (spend-composure! 1)))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (container "老街饭摊"
                   (list (node-dishwash)))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (#t #f))))))
