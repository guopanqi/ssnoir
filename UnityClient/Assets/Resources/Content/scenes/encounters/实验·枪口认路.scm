;; 第二轮：骰池在压制枪手与推进逃跑之间分配；所有行动均可重复。
(define 完了? #f)
(define 瞄准 '货棚)
(define 上次路线 '货棚)
(define 底片完好? #t)
(define 脱身 (make-clock "脱身" 14 'gauge "满14自动逃出；货棚保底片，踏入栈桥就会泡坏底片。"))
(define 火力 (make-clock "火力" 3 'readout "走枪口瞄准的路，每次花当前火力的冷静；压到0本回合安全。"))
(define 封锁 (make-clock "封锁" 2 'gauge "每回合加1；满2出口封死，脱身失败。"))
(define (结束! result text)
  (if 完了? (error "枪口认路：重复结束") #t)
  (set! 完了? #t) (spotlight! "脱身结果" text) (end-encounter result))
(define (检查!)
  (cond ((hospitalization-pending?) #f)
    ((脱身 'full?)
    (if 底片完好? (结束! '带底片脱身 "你逃出码头，拍下交易的底片还贴在胸口。")
      (结束! '失去底片脱身 "你逃出了码头，但海水毁掉了底片，交易证据没能保住。")))
    ((封锁 'full?) (结束! '被封锁 "探照灯从两头照来，出口封死，你没能逃出码头。"))))
(define (走! route n)
  (set! 上次路线 route)
  (if (equal? route '栈桥) (set! 底片完好? #f) #f)
  (if (equal? route 瞄准) (spend-actor-composure! 'player (火力 'current)) #f)
  (if (= n 0) (spend-actor-composure! 'player 1) #f)
  (if (hospitalization-pending?) #f
    (begin (脱身 'advance! n) (检查!))))
(define-opponent-rule "枪手换位与封锁"
  (lambda () (not 完了?))
  (lambda () (set! 瞄准 上次路线) (火力 'advance! 1) (封锁 'advance! 1) (检查!)))
(define (on-encounter-enter)
  (火力 'load! 2)
  (play-dialogue!
    (line "世界" "你要带拍下交易的底片逃出码头。枪手正盯着货棚出口。")
    (line "世界" "骰子可以用来压制枪手，也可以跑。栈桥更快，但一踏进去就毁掉底片。")
    (line "世界" "枪手下回合改盯你最后走的路，火力也会恢复一点。")))
(define (on-encounter-collapse) (collapse-result '倒下未脱身))
(define (get-render-data)
  (container "枪口认路"
    (append
      (list (note-node "标注：目标" "目标：逃出码头"
        (if 底片完好? "脱身满14自动逃出。走货棚保住证据；踏入栈桥就毁底片，只能保命。"
          "底片已被海水毁掉。继续推进脱身到14，保住自己。")))
      (clock-nodes (脱身 'render-data) (火力 'render-data) (封锁 'render-data))
      (list (note-node "标注：枪口" "枪口与回应"
        (string-append "当前盯着" (if (equal? 瞄准 '货棚) "货棚" "栈桥")
          "；每次走这条路花" (number->string (火力 'current)) "冷静。下回合盯你最后走的路，火力加1。"))
        (node "穿过货棚" :subtitle "保底片。坏：停步花1冷静；中进2；好进3。"
          :requires (list (req-die)) :resolve (roll 'sharpness
            (outcome (lambda () (走! '货棚 0)))
            (outcome (lambda () (走! '货棚 2)))
            (outcome (lambda () (走! '货棚 3)))))
        (node "冲下栈桥" :subtitle "毁底片。坏：停步花1冷静；中进3；好进4。"
          :requires (list (req-die)) :resolve (roll 'sharpness
            (outcome (lambda () (走! '栈桥 0)))
            (outcome (lambda () (走! '栈桥 3)))
            (outcome (lambda () (走! '栈桥 4))))))
      (if (火力 'empty?) '()
        (list (node "打落枪口" :subtitle "坏：花1冷静；中：火力减1；好：火力减2。"
          :requires (list (req-die)) :resolve (roll 'violence
            (outcome (lambda () (spend-actor-composure! 'player 1)))
            (outcome (lambda () (火力 'advance! -1)))
            (outcome (lambda () (火力 'advance! -2))))))))))
