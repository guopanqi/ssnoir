;; scenes/world/诊所.scm - 诊所（官僚）
;; 服务点：买药品（带回家用）/ 康复训练（少花钱，但占用行动骰）。用药动作在家里。

(define clinic
  (let ()

    (define (node-buy-medicine)
      (action "买药品"
        (list (req-item "金钱" 25))
        (instant (lambda ()
                   (add-item! "药品" 1)
                   (notify! "买了一份药，揣进兜里。")))))

    ;; 比买药省钱，但要投入一颗骰子，并且恢复量更低。
    (define (node-rehabilitation)
      (action "康复训练"
        (list (req-die) (req-item "金钱" 15))
        (instant
          (outcome "完成训练" "医生带着你活动伤处，一点点找回力气。"
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
