;; 解除持续威胁，同时永久关闭廉价退出；承诺改变后半场题目。
(define 完了? #f)
(define 门封? #f)
(define 证人 (make-clock "证人肯走" 10 'gauge "满10愿意跟你离开；好2、中1、坏花1冷静。"))
(define 追兵 (make-clock "楼梯追兵" 2 'gauge "未封门时每回合加1，满2抓走证人；封门永久停止。"))
(define 梯子 (make-clock "逃生梯" 2 'gauge "封门后每花一骰修1；满2可安全下楼。"))
(define (结束! result text)
  (if 完了? (error "封死退路：重复结束") #t)
  (set! 完了? #t) (spotlight! "屋顶出口" text)
  (end-encounter (list result 门封?)))
(define-opponent-rule "追兵冲上楼梯"
  (lambda () (and (not 完了?) (not 门封?)))
  (lambda () (追兵 'advance! 1)
    (if (追兵 'full?) (结束! '证人被抓 "楼梯门被踹开。证人被拖下了楼。") #f)))
(define (on-encounter-enter)
  (play-dialogue!
    (line "世界" "你要救走屋顶的证人。先让她相信你，再找路带她离开。")
    (line "世界" "证人缩在屋顶边缘。你得说服她跟你走，追兵正在上楼。")
    (line "世界" "封死楼梯门能挡住追兵，却也堵死退路。逃生梯还断着两处。")))
(define (on-encounter-collapse) (collapse-result (list '倒下 门封?)))
(define (get-render-data)
  (container "封死退路"
    (append
      (list (note-node "标注：目标" "目标：带人离开"
        (if (证人 'full?) (if 门封? (if (梯子 'full?) "证人肯走，梯子也修好了。沿梯子下楼带她离开。" "证人肯走，但楼梯封死了。修好逃生梯下楼，或花三冷静带她跳下。") "证人已经肯走。带她走楼梯，就能安全离开。") "先说服证人，再带她离开屋顶。追兵上来就会抓走她；封门会挡住追兵，也堵死退路。")))
      (clock-nodes (证人 'render-data) (追兵 'render-data) (梯子 'render-data))
      (list (note-node "标注：屋顶退路" "楼梯门"
        (if 门封? "永久封死。修好梯子才能安全走；跳下去花3冷静。"
          "尚可下楼；封门要一骰，之后不能再打开。")))
      (if (证人 'full?) '()
        (list (node "让她相信你" :subtitle "好：愿走2；中：愿走1；坏：花1冷静。"
          :requires (list (req-die)) :resolve (roll 'social
            (outcome (lambda () (spend-actor-composure! 'player 1)))
            (outcome (lambda () (证人 'advance! 1)))
            (outcome (lambda () (证人 'advance! 2)))))))
      (if 门封? '()
        (list (node "楔死楼梯门" :subtitle "一骰：追兵永远停下，但楼梯出口也封死。"
          :requires (list (req-die)) :resolve (instant (lambda () (set! 门封? #t))))))
      (if (and 门封? (not (梯子 'full?)))
        (list (node "接好逃生梯" :subtitle "一骰修1；需要修两次，才能安全离开。"
          :requires (list (req-die)) :resolve (instant (lambda () (梯子 'advance! 1))))) '())
      (if (证人 'full?)
        (if 门封?
          (append
            (if (梯子 'full?) (list (instant-action "沿梯子下楼"
              (lambda () (结束! '带走证人 "你扶着证人沿逃生梯下楼，枪手还困在门后。")))) '())
            (list (instant-action "带她跳下去" (lambda ()
              (spend-actor-composure! 'player 3)
              (结束! '带走证人 "你抱着她落在遮雨棚上，肩膀撞上了铁架。")))))
          (list (instant-action "带她走楼梯"
            (lambda () (结束! '带走证人 "你扶着她冲下楼梯，在下一层躲开了追兵。"))))) '())
      (list (instant-action "独自离开" (lambda ()
        (if 门封? (spend-actor-composure! 'player 3) #f)
        (结束! '放弃证人 "你一个人离开了屋顶，证人还在等你。")))))))
