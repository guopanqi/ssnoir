;; 五回合、基础每回合四骰；第三回合交布景，第五回合交灯位。
;; 工作为冒险判定，坏无进展且冷静−1，中进1/好进2；打听消息稳妥、坏无额外代价。默认 violence=-1 / sharpness=0，随机骰单次期望0.75/1.017。
;; 含尾数浪费，6格布景/8格灯位独立完成约需8.25/8.17次投入，合计16.42次。
;; 总预算20骰，布景截止前12骰。此估算不等于期限成功率或实玩手感。
(define 乐段 (make-clock "歌曲进程" 5 'gauge "每回合结束前进一段；第三段交布景，第五段交灯位并收场。" :beneficial-direction 'neutral))
(define 布景 (make-clock "布景就位" 6 'gauge "第三回合结束前完成；未完成由场务补位。" :beneficial-direction 'increase))
(define 灯位 (make-clock "灯位标记" 8 'gauge "第五回合结束前完成；未完成由灯光师补位。" :beneficial-direction 'increase))
;; 可选收益占用同一骰池；每满4格获得一份情报，溢出进度计入下一份。
(define 客演消息 (make-clock "客演消息" 4 'gauge "可选：每满4格获得情报1，余量保留，可重复；报社每份收10美元。" :beneficial-direction 'increase))
(define 布景按时? #f)
(define 灯位按时? #f)
(define 已结束? #f)
(define (交付结果)
  (cond ((and 布景按时? 灯位按时?) '两项交付)
        ((or 布景按时? 灯位按时?) '一项交付)
        (else '无交付)))
(define 场务 (speaker "彩排场务" :name "场务" :appearance '成年男性))
(define (打听! n)
  (let ((总进度 (+ (客演消息 'current) n)))
    (客演消息 'advance! n)
    (if (>= 总进度 (客演消息 'max))
      (begin
        (add-item! "情报" 1)
        (客演消息 'set! (- 总进度 (客演消息 'max)))) #f)))
(define (收场!)
  (set! 已结束? #t)
  (play-dialogue!
    (line "世界" "最后一个和弦落下，场务接过推车，招呼其他人收拾侧幕。")
    (line 场务 (cond
      ((eq? (交付结果) '两项交付) "两处都接上了，整份工钱四十美元。")
      ((eq? (交付结果) '一项交付) "只接上了一处，算十美元。手套放回墙边。")
      (else "两处都是别人接的，这趟没有工钱。手套放回墙边吧。"))))
  (end-scenario (交付结果)))
(define-opponent-rule "歌曲向前"
  (lambda () (not 已结束?))
  (lambda () (乐段 'advance! 1)))
(define-opponent-rule "布景上场"
  (lambda () (and (not 已结束?) (= (乐段 'current) 3)))
  (lambda ()
    (set! 布景按时? (布景 'full?))
    (if 布景按时?
      (play-bubble! (line "世界" "侧幕一开，布景沿着通道平稳推上舞台。"))
      (play-dialogue!
        (line "世界" "场务打手势让琴师多绕两小节，带着另一名工人接过布景。换景迟了一拍，夜莺接着唱下去。")))))
(define-opponent-rule "灯位启用"
  (lambda () (and (not 已结束?) (= (乐段 'current) 5)))
  (lambda ()
    (set! 灯位按时? (灯位 'full?))
    (if 灯位按时?
      (play-bubble! (line "世界" "夜莺走到标记处，灯光正好落在她肩上。"))
      (play-dialogue! (line "世界" "灯光师从台边重新指了位置。夜莺挪了半步，赶上下一句。")))))
(define-opponent-rule "歌曲唱到终点"
  (lambda () (and (not 已结束?) (乐段 'full?))) 收场!)
(define (on-scenario-enter)
  (乐段 'load! 0) (布景 'load! 0) (灯位 'load! 0) (客演消息 'load! 0)
  (set! 已结束? #f) (set! 布景按时? #f) (set! 灯位按时? #f)
  (play-dialogue!
    (line "世界" "尼尔从侧门进来。场务正在分派推车，看见他，指了指搁在墙边的工作手套。")
    (line 场务 "找夜莺？她上台了。想挣份临工钱，就跟我们走这一遍换景。")
    (line 场务 "第三段用布景，第五段走灯位。整首五段。哪处没赶上，我们会接着做，歌不会停。")
    (line 场务 "两处都按时交，四十美元；只接上一处，十美元；都没接上就没工钱。")
    (line "世界" "侧幕后，工人们趁搬运的间隙聊着各家剧院的客演和人事。这些消息，报社的戏剧版也许会收。")
    (line "世界" "夜莺在台口看见尼尔戴上手套，笑了一下。琴师抬起手，第一段开始了。")))
(define (on-scenario-collapse) (collapse-result (交付结果)))
(define (get-render-data)
  (node "首演之夜" :name "第一次彩排"
    :children (append
      (list
        (note-node "彩排目标" "目标" "按时交付布景与灯位，争取整份工钱；空余时间可打听消息。")
        (note-node "彩排工钱" "工钱" "按时交付两项40美元，只交一项10美元，无交付0美元。漏项仍继续；情报另算。")
        (note-node "彩排安排" "换景安排" "回合结束推进歌曲。第三回合交布景，第五回合交灯位；未完成由工作人员补位。第五回合收场。每回合结束冷静−1。"))
      (clock-nodes (乐段 'render-data))
      (append
          (clock-nodes (客演消息 'render-data))
          (list
            (node "打听客演" :subtitle "可选，坏0／中1／好2；每满4格获情报1"
              :risk '稳妥 :progress-target (list 'clock "客演消息")
              :requires (list (req-die))
              :resolve (roll 'social
                (outcome (lambda () #f))
                (outcome (lambda () (打听! 1)))
                (outcome (lambda () (打听! 2)))))))
      (if (< (乐段 'current) 3)
        (clock-nodes (布景 'render-data))
        (list (note-node "布景交付结果" "布景交付"
          (if 布景按时? "已按时上场；灯位也按时交付可领整份工钱。" "未赶上，场务已补位；灯位按时交付仍可领10美元。"))))
      (if (< (乐段 'current) 5) (clock-nodes (灯位 'render-data)) '())
      (if (and (< (乐段 'current) 3) (not (布景 'full?)))
        (list
          (node "搬运布景"
            :subtitle "推车送景：坏冷静−1无进展；中进1，好进2"
            :risk '冒险 :risk-target (list 'composure "player")
            :progress-target (list 'clock "布景就位")
            :requires (list (req-die))
            :resolve (roll 'violence
              (outcome (lambda () (spend-composure! 1)))
              (outcome (lambda () (布景 'advance! 1)))
              (outcome (lambda () (布景 'advance! 2)))))) '())
      (if (and (< (乐段 'current) 5) (not (灯位 'full?)))
        (list
          (node "调整灯位" :subtitle "核对灯位：坏冷静−1无进展；中进1，好进2"
            :risk '冒险 :risk-target (list 'composure "player")
            :progress-target (list 'clock "灯位标记")
            :requires (list (req-die))
            :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1)))
              (outcome (lambda () (灯位 'advance! 1)))
              (outcome (lambda () (灯位 'advance! 2)))))) '()))))
