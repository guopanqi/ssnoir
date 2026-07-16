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
                     (instant-action "健康 +2" (lambda () (heal-party! 2)))
                     (instant-action "冷静 +2" (lambda () (restore-actor-composure! 'player 2)))
                     (instant-action "冷静 -2" (lambda () (spend-actor-composure! 'player 2)))))
                 (container "调试-成长"
                   (list
                     (instant-action "成长等级 +1"
                       (lambda () (set-growth-level! (+ (growth-level) 1))))))
                 (container "调试-码头"
                   (list
                     (instant-action "劳工→面熟"     (lambda () (set-global! "relation:劳工" 2)))
                     (instant-action "劳工→够朋友"   (lambda () (set-global! "relation:劳工" 4)))
                     (instant-action "劳工→拜过码头" (lambda () (set-global! "relation:劳工" 6)))
                     (instant-action "官僚→挂号"     (lambda () (set-global! "relation:官僚" 2)))
                     (instant-action "富商→有往来"   (lambda () (set-global! "relation:富商" 2)))
                     (instant-action "老周好感 +2" (lambda () (dock 'debug-favor)))
                     (instant-action "强制刷新老周美差" (lambda () (dock 'debug-cushy)))
                     (instant-action "直接触发公共交锋" (lambda () (debug-trigger-public-event!)))))
                 (container "调试-老周养伤"
                   (list
                     (instant-action "触发老周受伤" (lambda () (dock 'debug-trigger-injury!)))
                     (instant-action "养伤进度设为 +6(痊愈)" (lambda () (dock 'debug-set-care! 6)))
                     (instant-action "养伤进度设为 +2(留伤)" (lambda () (dock 'debug-set-care! 2)))
                     (instant-action "养伤进度设为 -4(没保住)" (lambda () (dock 'debug-set-care! -4)))
                     (instant-action "立即结算养伤" (lambda () (dock 'debug-finish-injury!)))))
                 (container "调试-夜莺节拍三"
                   (list
                     (instant-action "拨到节拍三(已查明真相)"
                       (lambda ()
                         (nightingale 'debug-stage! 3)
                         (nightingale 'debug-set-flag! '三层已揭)
                         (nightingale 'debug-set-truth! 4)
                         (nightingale 'debug-set-flag! '撒谎的人)
                         (nightingale 'sync-blockers!)))
                     (instant-action "拨到节拍三(未查真相)"
                       (lambda ()
                         (nightingale 'debug-stage! 3)
                         (nightingale 'debug-set-flag! '三层已揭)
                         (nightingale 'sync-blockers!)))
                     (instant-action "+500 金钱(测试封口/舱位)" (lambda () (add-item! "金钱" 500)))))))))

          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (#t #f))))))
