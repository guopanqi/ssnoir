;; 第二轮：救人进度与枪口警戒共享四骰；交易、谈判、强抢、安抚均可重复。
(define 完了? #f)
(define 付出 0)
(define 交人 (make-clock "交人" 8 'gauge "满8自动救出人质；付钱保证2，谈判好2，强抢好3。"))
(define 警戒 (make-clock "警戒" 4 'gauge "满4绑匪带走人质；强抢必增加，付钱或安抚能降低。回合末加1。"))
(define 期限 (make-clock "离开" 3 'gauge "每回合加1；满3绑匪开车离开，救人失败。"))
(define (结束! result text)
  (if 完了? (error "信封换人：重复结束") #t)
  (set! 完了? #t) (spotlight! "救人结果" text)
  (end-encounter (list result 付出)))
(define (检查!)
  (cond ((hospitalization-pending?) #f)
    ((警戒 'full?) (结束! '人质被带走 "绑匪把人拖进车里，拒绝继续交割。你没能救出人质。"))
    ((交人 'full?) (结束! '救出人质 "你把人质带出了仓库。交出去的钱没有回来，剩下的钱仍在口袋里。"))
    ((期限 'full?) (结束! '人质被带走 "汽车发动，绑匪带着人质离开。你没能完成交割。"))))
(define (谈! n)
  (交人 'advance! n)
  (if (= n 0) (警戒 'advance! 1) #f) (检查!))
(define (抢! n alert)
  (警戒 'advance! alert) (交人 'advance! n)
  (if (= n 0) (spend-actor-composure! 'player 1) #f) (检查!))
(define-opponent-rule "绑匪越等越紧张"
  (lambda () (not 完了?))
  (lambda () (警戒 'advance! 1) (期限 'advance! 1) (检查!)))
(define (on-encounter-enter)
  (警戒 'load! 1)
  (play-dialogue!
    (line "世界" "你要把人质救出来。交人满八就完成，警戒满四绑匪就会带人走。")
    (line "世界" "交钱稳，谈判省钱，强抢快但会激怒绑匪。安抚能让枪口放低。")
    (line "世界" "每个动作都能反复投入骰子，但拖过三个回合，人质就被带走。")))
(define (on-encounter-collapse) (collapse-result (list '倒下未救人 付出)))
(define (get-render-data)
  (container "信封换人"
    (append
      (list (note-node "标注：目标" "目标：救出人质"
        "交人满8自动成功。分配骰子推进交割、控制警戒；警戒满4或离开满3都会失败。"))
      (clock-nodes (交人 'render-data) (警戒 'render-data) (期限 'render-data))
      (list
        (node "递出信封" :subtitle "一骰、五金：交人加2，警戒减1。"
          :requires (list (req-die) (req-item "金钱" 5)) :resolve (instant (lambda ()
            (set! 付出 (+ 付出 5)) (警戒 'advance! -1) (交人 'advance! 2) (检查!))))
        (node "谈交人条件" :subtitle "坏：警戒加1；中：交人加1；好：交人加2。"
          :requires (list (req-die)) :resolve (roll 'social
            (outcome (lambda () (谈! 0)))
            (outcome (lambda () (谈! 1)))
            (outcome (lambda () (谈! 2)))))
        (node "冲上去抢人" :subtitle "好：交人3警戒1；中：交人1警戒2；坏：警戒2。"
          :requires (list (req-die)) :resolve (roll 'violence
            (outcome (lambda () (抢! 0 2)))
            (outcome (lambda () (抢! 1 2)))
            (outcome (lambda () (抢! 3 1)))))
        (node "让枪口放低" :subtitle "坏：花1冷静；中：警戒减1；好：警戒减2。"
          :requires (list (req-die)) :resolve (roll 'social
            (outcome (lambda () (spend-actor-composure! 'player 1)))
            (outcome (lambda () (警戒 'advance! -1)))
            (outcome (lambda () (警戒 'advance! -2)))))))))
