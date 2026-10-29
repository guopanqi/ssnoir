;; 独立试玩：距离与去向是两条轴。近道不需要准备，也不限次数；每次绕行必定失去视线。
;; 同一行动同时填满两钟时先判跟丢，避免用最后一次盲目绕行无视脱逃危险。
(define 已结束? #f)
(define 追逐进度
  (make-clock "追逐进度" 10 'gauge
    "填满且未跟丢，便能截住送信人拿到信件。"
    :beneficial-direction 'increase))
(define 脱逃危险
  (make-clock "脱逃危险" 6 'pressure
    "越高越难确认他的去向；满格立即跟丢。回合结束增加1格。"
    :beneficial-direction 'decrease))

(define (结束! 成功?)
  (if 已结束? (error "雨夜追信：重复结算") #t)
  (set! 已结束? #t)
  (if 成功?
      (spotlight! "拿到信件" "你在街口截住他，攥住了那只湿透的信封。")
      (spotlight! "跟丢了" "你冲出巷口。雨幕里没有他的身影，信件也随他消失了。"))
  (end-scenario (if 成功? '拿到信件 '没拿到信件)))

(define (检查结果!)
  (cond
    ((脱逃危险 'full?) (结束! #f))
    ((追逐进度 'full?) (结束! #t))
    (else #f)))

(define (追赶! 进度 危险)
  (追逐进度 'advance! 进度)
  (脱逃危险 'advance! 危险)
  (检查结果!))

(define-opponent-rule "送信人继续逃跑"
  (lambda () (not 已结束?))
  (lambda ()
    (脱逃危险 'advance! 1)
    (play-bubble! (line "世界" "他跑过下一个街口，雨水冲散了脚步声。"))
    (检查结果!)))

(define (on-scenario-enter)
  (set! 已结束? #f)
  (追逐进度 'load! 0)
  (脱逃危险 'load! 0)
  (play-dialogue!
    (line "世界" "送信人把信封塞进大衣，转身冲进雨里。你追了上去。")
    (line "尼尔" "穿过旁边的巷子能绕到前头。但进了巷子，就看不见他了。")))

(define (on-scenario-collapse)
  (collapse-result '没拿到信件))

(define (get-render-data)
  (node "巷子"
    :anchor "勒索信-巷口"
    :children (append
      (list
        (note-node "追信：目标" "目标" "在跟丢之前截住送信人，拿到信件。")
        (note-node "追信：回合规则" "送信人不停脚"
          "回合结束：脱逃危险+1，并支付通常的冷静代价。两钟同时满，算跟丢。"))
      (clock-nodes (追逐进度 'render-data) (脱逃危险 'render-data))
      (list
        (node "紧追不舍"
          :subtitle "沿街猛追：坏无进展且危险+1，中进1，好进2。"
          :risk '冒险
          :risk-target (list 'clock "脱逃危险")
          :progress-target (list 'clock "追逐进度")
          :requires (list (req-die))
          :resolve (roll 'violence
            (outcome (lambda () (追赶! 0 1)))
            (outcome (lambda () (追赶! 1 0)))
            (outcome (lambda () (追赶! 2 0)))))
        (node "确认去向"
          :subtitle "辨认雨中身影：坏冷静−1，中危险减1，好减2。"
          :risk '稳妥
          :progress-target (list 'clock "脱逃危险")
          :requires (list (req-die))
          :resolve (roll 'sharpness
            (outcome (lambda () (spend-actor-composure! 'player 1)))
            (outcome (lambda () (追赶! 0 -1)))
            (outcome (lambda () (追赶! 0 -2)))))
        (node "抄近道"
          :subtitle "绕巷失去视线：危险必增2；坏进0，中进2，好进3。"
          :risk '危险
          :risk-target (list 'clock "脱逃危险")
          :progress-target (list 'clock "追逐进度")
          :requires (list (req-die))
          :resolve (roll 'sharpness
            (outcome (lambda () (追赶! 0 2)))
            (outcome (lambda () (追赶! 2 2)))
            (outcome (lambda () (追赶! 3 2)))))))))
