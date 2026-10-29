;; 门房自主循环：看门两回合、离开核查一回合；离开时可私下查记录。
;; 观察稳妥，查阅冒险：两者中1/好2，查阅坏结果冷静−1；观察修正−1，查阅无修正。
;; 玩家不驱动阶段变化；额外消息每满4格获得一份情报，可重复。
(define 线索 (make-clock "确认陌生人去向" 12 'gauge "调查男子的外貌、出没时间与去向。填满得到可继续追查的地点。" :beneficial-direction 'increase))
(define 看门阶段 (make-clock "门房正在看门" 2 'gauge "每回合结束推进1格；满后离开核查，开放一回合私下查记录。" :beneficial-direction 'neutral))
(define 核查阶段 (make-clock "门房离开核查" 1 'gauge "每回合结束推进1格；满后门房回来，私下查记录关闭。" :beneficial-direction 'neutral))
(define 消息 (make-clock "后台消息" 4 'gauge "额外收益：每满4格获得情报1，余量保留，可重复；每份可在报社卖10美元。" :beneficial-direction 'increase))
(define 在岗? #t)
(define (当前阶段钟) (if 在岗? 看门阶段 核查阶段))
(define 已结束? #f)
(define 门房 (speaker "侧门核查门房" :name "门房" :appearance '成年男性))
(define (结束! 来源)
  (if 已结束? (error "侧门调查：重复结算") #t)
  (set! 已结束? #t)
  (play-dialogue!
        (line "世界" (if (eq? 来源 '记录)
          "值班簿上的车号与停靠时间对上了：陌生男子几次乘旧旅馆的接送车来，打听夜莺离开的时间。"
          "尼尔记下灰帽子男子的袖口破口、出没时间，以及他乘坐的旧旅馆接送车车号。"))
        (line "尼尔" "去旧旅馆查车和司机。还不能说他就是雨夜那个人。"))
      (spotlight! "找到追查方向" "陌生男子与旧旅馆接送车有关。可以继续查车和司机，尚不确认身份。")
  (end-scenario '取得可靠线索))
(define (调查! n 来源)
  (线索 'advance! n)
  (if (线索 'full?) (结束! 来源) #f))
(define (打听! n)
  (let ((总进度 (+ (消息 'current) n)))
    (消息 'advance! n)
    (if (>= 总进度 (消息 'max))
      (begin (add-item! "情报" 1) (消息 'set! (- 总进度 (消息 'max)))) #f)))
(define-opponent-rule "门房工作推进"
  (lambda () (not 已结束?))
  (lambda () ((当前阶段钟) 'advance! 1)))
(define-opponent-rule "门房自行换岗"
  (lambda () (and (not 已结束?) ((当前阶段钟) 'full?)))
  (lambda ()
    (set! 在岗? (not 在岗?))
    ((当前阶段钟) 'reset!)
    (play-dialogue! (line "世界" (if 在岗?
      "门房回到侧门，收起台上的值班簿，继续接待来客。"
      "门房拿着待核对的名单去了后台，值班簿暂时留在门边桌上。")))))
(define (on-scenario-enter)
  (线索 'load! 0) (消息 'load! 0) (看门阶段 'load! 0) (核查阶段 'load! 0)
  (set! 在岗? #t) (set! 已结束? #f)
  (play-dialogue!
    (line "世界" "有人接连在侧门打听夜莺。尼尔观察来往的人，门房正在登记访客。")
    (line "世界" "门房每守两轮排练，就去后台核查一轮，再回侧门。只有他离开时，尼尔能私下查桌上的值班簿。")
    (line "世界" "后台的工人们还聊着其他剧院的消息，报社也许愿意花钱收。")))
(define (on-scenario-collapse) (collapse-result '线索不足))
(define (get-render-data)
  (node "首演之夜" :name "侧门查阅窗口"
    :children (append
      (list
        (note-node "窗口目标" "目标" "查明陌生人的去向；也可抽空打听额外消息。")
        (note-node "门房循环" "门房的工作" "看门两回合、离开核查一回合，反复交替；离开时可私下查记录。每回合结束冷静−1。"))
      (clock-nodes (线索 'render-data) ((当前阶段钟) 'render-data) (消息 'render-data))
      (if 在岗? '()
        (list
          (node "查出入记录" :subtitle "坏无进展冷静−1；中进1，好进2"
            :clocks (list (clock-priority
              (list 'clock "门房回来" (核查阶段 'remaining) (核查阶段 'max) 'countdown
                "归零后门房回来看门，私下查阅关闭。") 'secondary))
            :risk '冒险 :risk-target (list 'composure "player")
            :progress-target (list 'clock "确认陌生人去向")
            :requires (list (req-die))
            :resolve (roll 'knowledge
              (outcome (lambda () (spend-composure! 1)))
              (outcome (lambda () (调查! 1 '记录)))
              (outcome (lambda () (调查! 2 '记录)))))))
      (list
        (node "在侧门观察" :subtitle "人影难辨，判定−1；坏0，中1，好2"
          :risk '稳妥 :progress-target (list 'clock "确认陌生人去向")
          :requires (list (req-die))
          :resolve (roll 'sharpness (lambda () (list (modifier -1 "人影难辨")))
            (outcome (lambda () #f))
            (outcome (lambda () (调查! 1 '观察)))
            (outcome (lambda () (调查! 2 '观察)))))
        (node "打听后台消息" :subtitle "额外收益，坏0／中1／好2；每满4格获情报1"
          :risk '稳妥 :progress-target (list 'clock "后台消息")
          :requires (list (req-die))
          :resolve (roll 'social
            (outcome (lambda () #f))
            (outcome (lambda () (打听! 1)))
            (outcome (lambda () (打听! 2)))))))))
