;; 前置投资免维护，但缩短总工期；临时维护保留工期，牺牲每轮一骰。
(define 完了? #f)
(define 接头稳? #f)
(define 焊死? #f)
(define 已垫? #f)
(define 录音 (make-clock "录音" 10 'gauge "满10录下交易；接头没稳时不能录，回合末丢2格。"))
(define 搜线 (make-clock "搜线" 3 'gauge "每回合加1；焊接噪声另加1；满3对方挂断。"))
(define (结束! result text)
  (if 完了? (error "只够一条线：重复结束") #t)
  (set! 完了? #t)
  (spotlight! "电话里的买卖" text)
  (end-encounter (list result (录音 'current) 焊死?)))
(define (录! n)
  (录音 'advance! n)
  (if (= n 0) (spend-actor-composure! 'player 1) #f)
  (if (录音 'full?) (结束! '完整录音 "报价、货主和船名都留在磁带上。你拔掉了接头。") #f))
(define-opponent-rule "接头与搜线"
  (lambda () (not 完了?))
  (lambda ()
    (if 接头稳? #f (录音 'advance! -2))
    (搜线 'advance! 1)
    (if (搜线 'full?) (结束! '断线 "听筒里一声轻响，对方挂断了。磁带只剩半段交易。")
        (set! 接头稳? 焊死?))))
(define (on-encounter-enter)
  (play-dialogue!
    (line "世界" "你要录下电话里的交易作为证据。对方挂断前，尽量把磁带录完整。")
    (line "世界" "你在酒店杂物间接上电话线，铜接头一直松动。")
    (line "世界" "每回合用一骰稳住，或花两骰焊死。焊声会让对方早一回合挂断。")))
(define (on-encounter-collapse) (collapse-result (list '倒下 (录音 'current) 焊死?)))
(define (get-render-data)
  (container "只够一条线"
    (append
      (list (note-node "标注：目标" "目标：录下交易"
        "稳住电话接头，录满十格后自动带走磁带；对方挂断前也能撤离，留下部分录音。"))
      (clock-nodes (录音 'render-data) (搜线 'render-data))
      (list (note-node "标注：接头" "电话接头"
        (cond (焊死? "已焊死，之后不用维护。")
              (接头稳? "本回合已稳住；下回合还要一颗骰。")
              (已垫? "已垫好焊点，另花一骰可焊死，也可以继续手动稳住。")
              (else "松动。先稳住或焊好，才能录音。"))))
      (if (or 焊死? 接头稳?) '()
          (list (node "稳住接头" :subtitle "一骰：本回合可录音；下回合需重做。"
            :requires (list (req-die)) :resolve (instant (lambda () (set! 接头稳? #t))))))
      (if (or 焊死? 已垫?) '()
          (list (node "垫好焊点" :subtitle "一骰：准备永久接线，下一步再花一骰。"
            :requires (list (req-die)) :resolve (instant (lambda () (set! 已垫? #t))))))
      (if (and 已垫? (not 焊死?))
          (list (node "焊死接头" :subtitle "一骰：永久可录音；搜线加1，期限缩短。"
            :requires (list (req-die)) :resolve (instant (lambda ()
              (set! 焊死? #t) (set! 接头稳? #t) (搜线 'advance! 1)
              (if (搜线 'full?) (结束! '断线 "焊声惊动了对方。电话立刻被挂断了。") #f))))) '())
      (if 接头稳?
          (list (node "录下交易" :subtitle "坏：停录、花1冷静；中：录1；好：录2。"
            :requires (list (req-die)) :resolve (roll 'knowledge
              (outcome (lambda () (录! 0)))
              (outcome (lambda () (录! 1)))
              (outcome (lambda () (录! 2)))))) '())
      (list (instant-action "拔掉接头" (lambda () (结束! '撤离 "你拔掉接头，带走了已经录下的那一段。")))))))
