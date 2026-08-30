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
                     (instant-action "+500 金钱" (lambda () (add-item! "金钱" 500)))
                     (instant-action "+1 药品"  (lambda () (add-item! "药品" 1)))))
                 (container "调试-关系"
                   (list
                     (instant-action "老码头 +1" (lambda () (change-faction-relation! "老码头" 1)))
                     (instant-action "老码头 -1" (lambda () (change-faction-relation! "老码头" -1)))
                     (instant-action "商业圈 +1" (lambda () (change-faction-relation! "商业圈" 1)))))
                 (container "调试-身体"
                   (list
                     (instant-action "受伤 +1" (lambda () (injure!)))
                     (instant-action "重创 +3" (lambda () (injure-badly!)))
                     (instant-action "治疗 -3" (lambda () (heal-injury! 3)))
                     (instant-action "冷静 +2" (lambda () (restore-actor-composure! 'player 2)))
                     (instant-action "冷静 -2" (lambda () (spend-actor-composure! 'player 2)))))
                 (container "调试-成长"
                   (list
                     (instant-action "成长等级 +1"
                       (lambda () (set-growth-level! (+ (growth-level) 1))))))
                 (container "调试-码头"
                   (list
                     (instant-action "老码头→面熟"     (lambda () (set-global! "relation:老码头" 2)))
                     (instant-action "老码头→够朋友"   (lambda () (set-global! "relation:老码头" 4)))
                     (instant-action "老码头→自己人" (lambda () (set-global! "relation:老码头" 6)))
                     (instant-action "商业圈→有往来"   (lambda () (set-global! "relation:商业圈" 2)))))))))

          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (#t #f))))))
