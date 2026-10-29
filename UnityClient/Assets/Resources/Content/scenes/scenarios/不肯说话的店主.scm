;; B：独立试玩。顾虑决定两种问法的收益；观察消耗任意一颗骰子并确定揭示。
(define 已结束? #f)
(define 已观察? #f)
(define 顾虑 '隐私)
(define 记录 (make-clock "取得记录" 5 'gauge "满格取得一条可核实的相纸记录。" :beneficial-direction 'increase))
(define 戒备 (make-clock "店主戒备" 3 'pressure "满格，店主拒绝继续谈话。" :beneficial-direction 'decrease))
(define 打烊 (make-clock "距离打烊" 2 'countdown "每回合末−1；归零仍未取得记录则失败。" :beneficial-direction 'increase))

(define (结束! 成功? 原因)
  (if 已结束? (error "店主实验：重复结算") #t)
  (set! 已结束? #t)
  (spotlight! (if 成功? "取得记录" "没拿到记录") 原因)
  (end-scenario (if 成功? '取得记录 '没取得记录)))

(define (检查!)
  (cond
    ((戒备 'full?) (结束! #f "店主把账本收进柜子，示意你离开。"))
    ((记录 'full?) (结束! #t "店主翻到一页订单，让你记下相纸批次和冲印日期。"))
    (else #f)))

(define (结算! 进展 戒心)
  (记录 'advance! 进展)
  (戒备 'advance! 戒心)
  (检查!))

(define (对路? 方法)
  (or (and (eq? 顾虑 '隐私) (eq? 方法 '保密))
      (and (eq? 顾虑 '牵连) (eq? 方法 '相纸))))

(define (问话! 方法 档)
  (cond
    ((eq? 方法 '直接)
     (结算! 档 (if (= 档 0) 1 0)))
    ((对路? 方法)
     (结算! (if (= 档 0) 0 (+ 档 1)) (if (= 档 0) 1 0)))
    (else (结算! (if (= 档 2) 1 0) 1))))

(define (问话节点 名 方法 技能 描述)
  (node 名
    :subtitle 描述
    :risk '冒险
    :risk-target (list 'clock "店主戒备")
    :progress-target (list 'clock "取得记录")
    :requires (list (req-die))
    :resolve (roll 技能
      (outcome (lambda () (问话! 方法 0)))
      (outcome (lambda () (问话! 方法 1)))
      (outcome (lambda () (问话! 方法 2))))))

(define (观察!)
  (if 已观察? (error "店主实验：重复观察") #t)
  (set! 已观察? #t)
  (play-dialogue!
    (line "世界" (if (eq? 顾虑 '隐私)
      "他始终把账本上的姓名遮在手底下，又看了看旁边的客人。他担心的是泄露客户隐私。"
      "他把照片放到账本旁，反复比对裁边。他担心你凭一张照片，就把勒索的事算到店里。"))))

(define-opponent-rule "照相馆即将打烊"
  (lambda () (not 已结束?))
  (lambda ()
    (打烊 'advance! -1)
    (if (打烊 'empty?)
        (结束! #f "店主熄掉柜台灯。今晚的谈话到此为止。")
        (play-bubble! (line "世界" "店主把柜台上的散页收齐，朝墙上的钟看了一眼。")))))

(define (on-scenario-enter)
  (set! 已结束? #f)
  (set! 已观察? #f)
  (set! 顾虑 (random-choice (list '隐私 '牵连)))
  (记录 'load! 0)
  (戒备 'load! 0)
  (打烊 'load! 2)
  (play-dialogue!
    (line "世界" "照相馆快打烊了。你拿出那张勒索照片，问店主相纸的来处。")
    (line "店主" "这种纸又不是我们一家在用。我要打烊了。")))

(define (on-scenario-collapse) (collapse-result '没取得记录))

(define (get-render-data)
  (node "照相馆交涉"
    :anchor "照相馆交涉"
    :children (append
      (list
        (note-node "店主实验：目标" "目标" "打烊前，取得一条可核实的相纸采购或冲印记录。")
        (note-node "店主实验：反应" "店主的反应"
          (if (eq? 顾虑 '隐私)
              "他合上账本，目光扫过店里另一位客人。"
              "他反复检查照片边缘，追问你从哪里得到照片。"))
        (note-node "店主实验：规则" "问话规则"
          "直接问：中+1、好+2；切中顾虑：中+2、好+3；两者坏结果戒备+1。未切中：好仅+1，其余不推进，均戒备+1。回合末打烊−1，照常支付冷静。"))
      (if 已观察?
          (list (note-node "店主实验：已知顾虑" "已确认的顾虑"
            (if (eq? 顾虑 '隐私) "他在保护客户隐私，保证保密更有效。" "他怕被牵连，对照相纸更有效。")))
          '())
      (clock-nodes (记录 'render-data) (戒备 'render-data) (打烊 'render-data))
      (list
        (问话节点 "直接询问" '直接 'social "请他查一条记录，不碰其他客户资料。")
        (问话节点 "保证保密" '保密 'social "承诺不泄露客户姓名，缓解隐私顾虑。")
        (问话节点 "对照相纸" '相纸 'knowledge "核对裁边与压纹，说明并非凭空牵连。"))
      (if 已观察? '()
          (list (node "观察反应"
            :subtitle "花任意一颗骰，确定顾虑；不作判定。"
            :requires (list (req-die))
            :resolve (instant (lambda () (观察!)))))))))
