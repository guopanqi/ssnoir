;; C：沿用雨夜追信，仅添加限时、限一次尝试的关闸行动。借用巷子舞台。
;; 同一行动同时填满两钟时先判跟丢，避免用最后一次盲目绕行无视脱逃危险。
(define 已结束? #f)
(define 已试关闸? #f)
(define 铁闸机会
  (make-clock "铁闸机会" 2 'countdown
    "前两回合可尝试关闸一次；回合末−1，归零后离开控制区域。"
    :beneficial-direction 'increase))
(define 封锁路线
  (make-clock "封锁路线" 2 'readout
    "接下来两次紧追不舍，中立或成功额外进1；失败照常，但也消耗一次。"
    :beneficial-direction 'increase))
(define 追逐进度
  (make-clock "追逐进度" 10 'gauge
    "填满且未跟丢，便能截住送信人拿到信件。"
    :beneficial-direction 'increase))
(define 脱逃危险
  (make-clock "脱逃危险" 6 'pressure
    "越高越难确认他的去向；满格立即跟丢。回合结束增加1格。"
    :beneficial-direction 'decrease))

(define (结束! 成功?)
  (if 已结束? (error "铁闸追信：重复结算") #t)
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

(define (紧追! 进度 危险)
  (let ((加成 (if (and (> 进度 0) (not (封锁路线 'empty?))) 1 0)))
    (if (not (封锁路线 'empty?)) (封锁路线 'advance! -1) #f)
    (追赶! (+ 进度 加成) 危险)))

(define (关闸! 档)
  (if (or 已试关闸? (铁闸机会 'empty?))
      (error "铁闸追信：关闸机会已失效") #t)
  (set! 已试关闸? #t)
  (if (= 档 2)
      (begin
        (封锁路线 'set! 2)
        (play-bubble! (line "世界" "铁闸砸在地上。送信人被迫转进装卸区，绕向远处的出口。")))
      #f)
  (追赶! (if (= 档 0) 0 1) (if (= 档 0) 1 0)))

(define (铁闸节点)
  (node "抢先关闸"
    :subtitle "仅试一次：坏危险+1；中进1；好进1并封路。"
    :risk '冒险
    :risk-target (list 'clock "脱逃危险")
    :progress-target (list 'clock "追逐进度")
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome (lambda () (关闸! 0)))
      (outcome (lambda () (关闸! 1)))
      (outcome (lambda () (关闸! 2))))))

(define-opponent-rule "送信人继续逃跑"
  (lambda () (not 已结束?))
  (lambda ()
    (脱逃危险 'advance! 1)
    (if (and (not 已试关闸?) (not (铁闸机会 'empty?)))
        (铁闸机会 'advance! -1) #f)
    (play-bubble! (line "世界" "他跑过下一个街口，雨水冲散了脚步声。"))
    (检查结果!)))

(define (on-scenario-enter)
  (set! 已结束? #f)
  (set! 已试关闸? #f)
  (铁闸机会 'load! 2)
  (封锁路线 'load! 0)
  (追逐进度 'load! 0)
  (脱逃危险 'load! 0)
  (play-dialogue!
    (line "世界" "送信人朝码头货运区跑去。铁闸下，一辆货车正在缓缓驶出。")
    (line "尼尔" "控制杆还在旁边。现在关闸，能逼他绕路。再跑过两条街，就碰不到它了。")))

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
      (if (and (not 已试关闸?) (not (铁闸机会 'empty?)))
          (append
            (list (note-node "铁闸：效果" "可封锁的出口"
              "关闸成功：接下来两次紧追不舍，中或好额外进1；坏不加成，也消耗次数。"))
            (clock-nodes (铁闸机会 'render-data))
            (list (铁闸节点)))
          '())
      (if (封锁路线 'empty?) '() (clock-nodes (封锁路线 'render-data)))
      (clock-nodes (追逐进度 'render-data) (脱逃危险 'render-data))
      (list
        (node "紧追不舍"
          :subtitle (if (封锁路线 'empty?)
            "沿街猛追：坏无进展且危险+1，中进1，好进2。"
            "封路生效：坏危险+1，中进2，好进3；耗一次。")
          :risk '冒险
          :risk-target (list 'clock "脱逃危险")
          :progress-target (list 'clock "追逐进度")
          :requires (list (req-die))
          :resolve (roll 'violence
            (outcome (lambda () (紧追! 0 1)))
            (outcome (lambda () (紧追! 1 0)))
            (outcome (lambda () (紧追! 2 0)))))
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
