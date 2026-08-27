;; scenes/world/公园.scm - 公园（不属于任何圈子）
;; 城市生活的对冲面：散步恢复冷静。占一颗骰子——歇一天就少赚一天，
;; 让“恢复”和“赚钱”在骰子池里正面争夺。这里不处理伤势，只恢复冷静。
;;
;; 数值刻意压得低（上限 5，这里最多回 2）：一颗骰换不来一个满血的自己。
;; 交锋掏空之后要回满，得连着好几天既睡觉又散步，或者花钱去喝一杯。

(define park
  (let ()

    ;; 散步：花一颗骰子恢复冷静，效果随判定浮动（坏 0 / 中 +1 / 好 +2）。
    ;; 失败只损失这次行动，不会让处境继续恶化——这是它比「看花」值一点的地方：
    ;; 看花稳回 1，散步期望也接近 1，但有回 2 的那一面。
    ;;
    ;; 那 +1 的便签写「轻松」，不写「散步」。便签要回答的是**为什么这次容易**，
    ;; 而卡上已经写着散步了——把卡名再抄一遍等于占掉一枚便签说一句废话。
    ;; 它容易的真正理由是这是件不赶时间、没人盯着的事。
    (define (node-walk)
      (node "散步"
        :anchor "公园"
        :requires (list (req-die))
        :resolve (recovery-roll 'sharpness
          (lambda () (list (modifier 1 "轻松")))
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
