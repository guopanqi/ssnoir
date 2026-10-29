;; 贪婪：保留为独立Scenario原型；剧院只是当前情景，不接入正式彩排。
;; 决策结构与用户备注见 docs/制作/Scenario决策结构卡.md。
;; 五回合、三次短交付。
;; 三项各4格，总工作量12格；截止为2/3/5。所有工作从开场开放。
(define 乐段 (make-clock "歌曲进程" 5 'gauge "每回合结束前进一段；第2、3、5回合各交一次工作，第5回合收场。" :beneficial-direction 'neutral))
(define 首段布景 (make-clock "首段布景" 4 'gauge "第2回合结束交付；未完成由场务补位。" :beneficial-direction 'increase))
(define 侧灯标记 (make-clock "侧灯标记" 4 'gauge "第3回合结束交付；未完成由灯光师补位。" :beneficial-direction 'increase))
(define 尾段布景 (make-clock "尾段布景" 4 'gauge "第5回合结束交付；未完成由场务补位。" :beneficial-direction 'increase))
(define 消息 (make-clock "后台消息" 4 'gauge "可选：每满4格获得情报1，余量保留，可重复；报社每份收10美元。" :beneficial-direction 'increase))
;; Clock、截止、行动Id、技能、交付成功/错过的表现。交付后锁定，不保存第二份评分状态。
(define 工作表
  (list
    (list 首段布景 2 "推入首景" 'violence
      "侧幕拉开，第一组布景准时推到台口。"
      "场务让琴师多绕两小节，另找人接过首段布景。")
    (list 侧灯标记 3 "核对侧灯" 'sharpness
      "夜莺走到侧台的标记处，侧灯随她亮起。"
      "灯光师从台边重新指了位置，夜莺挪了半步接着唱。")
    (list 尾段布景 5 "准备尾景" 'violence
      "最后一段开始，尾景沿着轨道平稳滑进灯下。"
      "场务带着另一名工人补上尾景，夜莺唱完最后一句。")))
(define 已结束? #f)
(define 场务 (speaker "彩排场务" :name "场务" :appearance '成年男性))
(define (已交付数)
  (length (filter (lambda (项)
    (and (>= (乐段 'current) (cadr 项)) ((car 项) 'full?))) 工作表)))
(define (工钱)
  (let ((n (已交付数)))
    (cond ((= n 3) 40) ((= n 2) 15) ((= n 1) 5) (else 0))))
(define (结账!)
  (if 已结束? (error "短钟彩排：重复结账") #t)
  (set! 已结束? #t)
  (if (> (工钱) 0) (add-item! "金钱" (工钱)) #f))
(define (收场!)
  (结账!)
  (play-dialogue!
    (line "世界" "最后一个和弦落下，场务招呼大家把推车收回侧幕。")
    (line 场务 (string-append "这一遍结束了。按时交了"
      (number->string (已交付数)) "项，工钱" (number->string (工钱)) "美元。")))
  (end-scenario '彩排结束))
(define (打听! n)
  (let ((总进度 (+ (消息 'current) n)))
    (消息 'advance! n)
    (if (>= 总进度 (消息 'max))
      (begin (add-item! "情报" 1) (消息 'set! (- 总进度 (消息 'max)))) #f)))
(define (交付! 项)
  (if ((car 项) 'full?)
    (play-bubble! (line "世界" (list-ref 项 4)))
    (play-dialogue! (line "世界" (list-ref 项 5)))))
(define-opponent-rule "歌曲向前"
  (lambda () (not 已结束?))
  (lambda () (乐段 'advance! 1)))
(define-opponent-rule "首景上场"
  (lambda () (and (not 已结束?) (= (乐段 'current) 2)))
  (lambda () (交付! (list-ref 工作表 0))))
(define-opponent-rule "侧灯启用"
  (lambda () (and (not 已结束?) (= (乐段 'current) 3)))
  (lambda () (交付! (list-ref 工作表 1))))
(define-opponent-rule "尾景上场"
  (lambda () (and (not 已结束?) (= (乐段 'current) 5)))
  (lambda () (交付! (list-ref 工作表 2))))
(define-opponent-rule "歌曲结束"
  (lambda () (and (not 已结束?) (乐段 'full?))) 收场!)
(define (on-scenario-enter)
  (乐段 'load! 0) (消息 'load! 0)
  (map (lambda (项) ((car 项) 'load! 0)) 工作表)
  (set! 已结束? #f)
  (play-dialogue!
    (line "世界" "尼尔戴上工作手套。场务把两组推车分别指给他看，灯光师正在台边划线。")
    (line 场务 "第二段推首景，第三段走侧灯，第五段换尾景。你现在就能准备后面的活儿。")
    (line 场务 "三项都接上四十美元，两项十五，一项五。哪处漏了我们接，歌照唱。")
    (line "世界" "工人们边搬箱子边聊各家剧院的近况。琴声响起，夜莺走到台口。")))
(define (on-scenario-collapse)
  (结账!)
  (collapse-result '中途离场))
(define (工作节点 项)
  (let ((钟 (car 项)) (截止 (cadr 项)) (id (list-ref 项 2)) (技能 (list-ref 项 3)))
    (if (< (乐段 'current) 截止)
      (append
        (clock-nodes (钟 'render-data))
        (if (钟 'full?) '()
          (list (node id :subtitle "坏无进展冷静−1；中进1，好进2"
            :risk '冒险 :risk-target (list 'composure "player")
            :progress-target (list 'clock (cadr (钟 'render-data)))
            :requires (list (req-die))
            :resolve (roll 技能
              (outcome (lambda () (spend-composure! 1)))
              (outcome (lambda () (钟 'advance! 1)))
              (outcome (lambda () (钟 'advance! 2))))))))
      (list (note-node (string-append id "交付") (cadr (钟 'render-data))
        (if (钟 'full?) "已按时交付。" "未赶上，工作人员已补位。"))))))
(define (get-render-data)
  (node "首演之夜" :name "彩排（贪婪）"
    :children (append
      (list
        (note-node "短钟目标" "目标" "按时接上三次舞台交付，争取工钱；也可抽空打听消息。")
        (note-node "短钟工钱" "工钱"
          (string-append "一项5／两项15／三项40美元；已按时交付"
            (number->string (已交付数)) "项。"))
        (note-node "短钟安排" "换景安排" "第2回合交首景，第3回合交侧灯，第5回合交尾景并收场；漏项仍继续。回合结束冷静−1。"))
      (clock-nodes (乐段 'render-data) (消息 'render-data))
      (apply append (map 工作节点 工作表))
      (list (node "打听后台消息" :subtitle "可选，坏0／中1／好2；每满4格获情报1"
        :risk '稳妥 :progress-target (list 'clock "后台消息")
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome (lambda () #f))
          (outcome (lambda () (打听! 1)))
          (outcome (lambda () (打听! 2)))))))))
