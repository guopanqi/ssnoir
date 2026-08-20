;; scenes/world/公园.scm - 公园（无势力）
;; 城市生活的对冲面：散步恢复冷静。占一颗骰子——歇一天就少赚一天，
;; 让“恢复”和“赚钱”在骰子池里正面争夺。这里不处理伤势，只恢复冷静。

(define park
  (let ()

    ;; 散步：花一颗骰子恢复冷静，效果随判定浮动（坏 0 / 中 +1 / 好 +2）。
    ;; 失败只损失这次行动，不会让处境继续恶化。
    (define (node-walk)
      (node "散步"
        :anchor "公园"
        :requires (list (req-die))
        :resolve (recovery-roll 'sharpness
          (lambda () (list (modifier 1 "散步")))
          (outcome "心不在焉" ; 坏
            (lambda () (walter 'on-park-walk!)))
          (outcome "松了口气" ; 中
            (lambda ()
              (restore-actor-composure! 'player 1)
              (walter 'on-park-walk!)))
          (outcome "神清气爽" ; 好
            (lambda ()
              (restore-actor-composure! 'player 2)
              (walter 'on-park-walk!))))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "公园" :children (list (node-walk)))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (#t #f))))))
