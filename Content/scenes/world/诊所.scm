;; scenes/world/诊所.scm - 诊所（官僚）
;; 服务点：买药品（带回家用）/ 康复训练（少花钱，但占用行动骰）。用药动作在家里。

(define clinic
  (let ()

    ;; 官僚敌视时诊所涨价（约 +50%），但绝不拒诊——治病是唯一手段，没有替代路径，
    ;; 关系再差也不能把它彻底禁掉，最多让它变贵。
    (define (hostile-markup base)
      (if (equal? (relation-band "官僚") '敌视)
          (+ base (quotient base 2))
          base))

    (define (node-buy-medicine)
      (node "买药品"
        :subtitle (if (equal? (relation-band "官僚") '敌视)
                      "医生知道你现在不受待见，这份药比平时贵一截"
                      "")
        :requires (list (req-item "金钱" (hostile-markup 25)))
        :resolve (instant
          (outcome "抓了一份药" "抓了一份药，揣进兜里，留着熬不住的时候。"
            (lambda () (add-item! "药品" 1))))))

    ;; 比买药省钱，但要投入一颗骰子，并且恢复量更低。
    (define (node-rehabilitation)
      (node "康复训练"
        :subtitle (if (equal? (relation-band "官僚") '敌视)
                      "医生知道你现在不受待见，这份诊金比平时贵一截"
                      "")
        :requires (list (req-die) (req-item "金钱" (hostile-markup 15)))
        :resolve (instant
          (outcome "完成训练" "医生按着你活动伤处，疼得龇牙，力气却一点点找了回来。"
            (lambda () (heal-party! 2))))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (container "诊所"
                   (list (node-buy-medicine) (node-rehabilitation)))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (#t #f))))))
