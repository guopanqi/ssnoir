;; scenes/world/test.scm - 调试台（新资源模型）
;; 仅在 chapter="test" 时出现。用于手动拨动新的货币 / 关系 / 身体状态。

(define test
  (let ()
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list
             (container "调试台"
               (list
                 (container "调试-物品"
                   (list
                     (instant-action "+50 金钱" (lambda () (add-item! "金钱" 50)))
                     (instant-action "+1 情报"  (lambda () (add-item! "情报" 1)))
                     (instant-action "+1 食物"  (lambda () (add-item! "食物" 1)))
                     (instant-action "+1 药品"  (lambda () (add-item! "药品" 1)))
                     (instant-action "+1 酒"    (lambda () (add-item! "酒" 1)))))
                 (container "调试-关系"
                   (list
                     (instant-action "劳工 +1" (lambda () (change-faction-relation! "劳工" 1)))
                     (instant-action "劳工 -1" (lambda () (change-faction-relation! "劳工" -1)))
                     (instant-action "官僚 +1" (lambda () (change-faction-relation! "官僚" 1)))
                     (instant-action "富商 +1" (lambda () (change-faction-relation! "富商" 1)))))
                 (container "调试-身体"
                   (list
                     (instant-action "饱腹 +3" (lambda () (add-satiety! 3)))
                     (instant-action "健康 +2" (lambda () (heal-party! 2)))
                     (instant-action "压力 -2" (lambda () (heal-stress! 'player 2)))))
                 (container "调试-成长"
                   (list
                     (instant-action "成长等级 +1"
                       (lambda () (set-growth-level! (+ (growth-level) 1))))))
                 (container "调试-码头"
                   (list
                     (instant-action "劳工→脸熟"   (lambda () (set-global! "relation:劳工" 3)))
                     (instant-action "劳工→自己人" (lambda () (set-global! "relation:劳工" 6)))
                     (instant-action "官僚→脸熟"   (lambda () (set-global! "relation:官僚" 3)))
                     (instant-action "老周好感 +2" (lambda () (dock 'debug-favor)))
                     (instant-action "强制刷新老周美差" (lambda () (dock 'debug-cushy)))
                     (instant-action "直接触发公共交锋" (lambda () (debug-trigger-public-event!)))))))))

          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (#t #f))))))
