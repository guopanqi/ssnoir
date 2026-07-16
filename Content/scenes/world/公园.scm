;; scenes/world/公园.scm - 公园（无势力）
;; 城市生活的对冲面：散步恢复冷静。占一颗骰子——歇一天就少赚一天，
;; 让“恢复”和“赚钱”在骰子池里正面争夺。这里不恢复健康，只恢复冷静。

(define park
  (let ()

    ;; 散步：花一颗骰子恢复冷静，效果随判定浮动（坏 0 / 中 +1 / 好 +2）。
    ;; 失败只损失这次行动，不会让处境继续恶化。
    (define (node-walk)
      (recovery-roll-action "散步"
        (list (req-die))
        'sharpness
        (outcome "心不在焉" "走是走了，脑子里那些事却怎么也甩不掉。" ; 坏
          (lambda () #f))
        (outcome "松了口气" "沿着湖边走了一圈，风把脑子里的杂音吹散了些。" ; 中
          (lambda () (restore-actor-composure! 'player 1)))
        (outcome "神清气爽" "阳光、湖水、远处飘来的乐声。你久违地觉得轻快。" ; 好
          (lambda () (restore-actor-composure! 'player 2)))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (container "公园" (append (list (node-walk)) (walter 'park-nodes)))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (#t #f))))))
