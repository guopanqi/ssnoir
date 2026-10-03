;; 分段付款改变强抢的结果，不只是解锁同一个按钮。
(define 完了? #f)
(define 已付? #f)
(define 已抢? #f)
(define 付出 0)
(define 接近 (make-clock "接近出口" 3 'gauge "付款后回合末前进1；满3平安交人。到2可近身抢人。"))
(define 惊动 (make-clock "惊动" 2 'gauge "满2对方带走人质；远处失手更容易惊动。"))
(define 期限 (make-clock "期限" 4 'gauge "每回合加1，满4绑匪开车离开。"))
(define (结束! result text)
  (if 完了? (error "信封换人：重复结束") #t)
  (set! 完了? #t)
  (spotlight! "仓库交割" text)
  (end-encounter (list result 付出)))
(define (查惊动!)
  (if (惊动 'full?) (结束! '人质被带走 "绑匪把人推进车里。你交出去的钱也没有回来。") #f))
(define (抢! 档)
  (set! 已抢? #t)
  (if (>= (接近 'current) 2)
      (cond ((= 档 2) (结束! '抢回人质 "你把人拽到门外。剩下的信封还在你口袋里。"))
            ((= 档 1) (spend-actor-composure! 'player 2)
              (结束! '抢回人质 "你挨了一记枪托，但把人带出了仓库。"))
            (else (接近 'advance! -1) (惊动 'advance! 1)
              (spend-actor-composure! 'player 1) (查惊动!)))
      (cond ((= 档 2) (接近 'advance! 1) (spend-actor-composure! 'player 1))
            ((= 档 1) (惊动 'advance! 1) (spend-actor-composure! 'player 1) (查惊动!))
            (else (惊动 'advance! 2) (spend-actor-composure! 'player 2) (查惊动!)))))
(define-opponent-rule "交钱后放近一步"
  (lambda () (and (not 完了?) 已付?))
  (lambda ()
    (接近 'advance! 1)
    (if (接近 'full?) (结束! '完成交割 "信封落进车窗，人质终于走到了你身边。") #f)))
(define-opponent-rule "绑匪准备离开"
  (lambda () (not 完了?))
  (lambda ()
    (set! 已付? #f) (set! 已抢? #f)
    (期限 'advance! 1)
    (if (期限 'full?) (结束! '人质被带走 "车灯亮起，绑匪不再等你的决定。") #f)))
(define (on-encounter-enter)
  (play-dialogue!
    (line "世界" "你要把人质救出来。可以付钱换人，也可以等他靠近后抢回来。")
    (line "世界" "绑匪要三只信封，每只五金。每收一只，才让人向出口走一步。")
    (line "世界" "他离门越近，越容易抢回来。交出去的钱不会退。")))
(define (on-encounter-collapse) (collapse-result (list '倒下 付出)))
(define (get-render-data)
  (container "信封换人"
    (append
      (list (note-node "标注：目标" "目标：救出人质"
        "让人质走到出口，或强抢救回。交钱更稳，动手能省钱；惊动绑匪或拖过期限会失去人质。"))
      (clock-nodes (接近 'render-data) (惊动 'render-data) (期限 'render-data))
      (list (note-node "标注：抢人风险" "枪口下的人"
        (if (>= (接近 'current) 2)
          "近处：好救人；中救人花2冷静；坏退1、惊动1、花1冷静。"
          "远处：好靠近1、花1冷静；中惊动1、花1冷静；坏惊动2、花2冷静。")))
      (if 已付? '()
        (list (node "递出信封" :subtitle "一骰、五金；回合末人质前进1。"
          :requires (list (req-die) (req-item "金钱" 5))
          :resolve (instant (lambda () (set! 已付? #t) (set! 付出 (+ 付出 5)))))))
      (if 已抢? '()
        (list (node "冲上去抢人" :subtitle "每回合一次；到出口前两步时，失手代价降低。"
          :requires (list (req-die)) :resolve (roll 'violence
            (outcome (lambda () (抢! 0)))
            (outcome (lambda () (抢! 1)))
            (outcome (lambda () (抢! 2)))))))
      (list (instant-action "放弃交割"
        (lambda () (结束! '放弃 "你退到街上。仓库门在你身后关了。")))))))
