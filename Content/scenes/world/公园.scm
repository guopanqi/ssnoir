;; scenes/world/公园.scm - 公园（无势力）
;; 城市生活的对冲面：散步解压。占一颗骰子——歇一天就少赚一天，
;; 让“恢复”和“赚钱”在骰子池里正面争夺。这里不恢复健康，只缓压力。

(define park
  (let ()

    ;; 散步：花一颗骰子解压，效果随判定浮动（坏 -1 / 中 -2 / 好 -3）。
    ;; 免费（不花钱），代价是少一个班次。这里只缓压力。
    (define (node-walk)
      (roll-action "散步"
        (list (req-die))
        'sharpness
        (outcome "心不在焉" "走是走了，脑子里那些事却怎么也甩不掉。" ; 坏
          (lambda () (heal-stress! 'player 1)))
        (outcome "松了口气" "沿着湖边走了一圈，风把脑子里的杂音吹散了些。" ; 中
          (lambda () (heal-stress! 'player 2)))
        (outcome "神清气爽" "阳光、湖水、远处飘来的乐声。你久违地觉得轻快。" ; 好
          (lambda () (heal-stress! 'player 3)))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (container "公园" (list (node-walk)))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (#t #f))))))
