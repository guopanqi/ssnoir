;; 城市事件：乔的手提箱。没有期限，行动换取固定报酬。
(define 手提箱的乔 (speaker "手提箱的乔" :name "乔" :appearance '成年男性))
(define 乔的手提箱
  (let ()
    (define 阶段 "未出现")
    (define 搜寻 (make-clock "寻找乔的手提箱" 4 'gauge "搜寻填满后取得手提箱，交还乔可得20美元。" :beneficial-direction 'increase))
    (define (交谈!)
      (if (equal? 阶段 "待交谈") #t (error "手提箱：当前不可交谈"))
      (set! 阶段 "寻找中")
      (play-dialogue!
        (line 手提箱的乔 "我把手提箱落在码头卸货棚了。棕色皮的，提手上缠着胶布。")
        (line 手提箱的乔 "里头是我的推销样品。替我找回来，我付你二十块。你找到了就带回住处，我过来取。")))
    (define (推进! n)
      (if (equal? 阶段 "寻找中") #t (error "手提箱：当前不可寻找"))
      (搜寻 'advance! n)
      (if (搜寻 'full?)
        (begin (set! 阶段 "已找到") (add-item! "乔的手提箱" 1)
          (play-dialogue! (line "世界" "卸货棚的空木箱后面，露出一只缠着胶布的提手。箱子上的姓名是乔。"))) #f))
    (define (归还!)
      (if (and (equal? 阶段 "已找到") (> (item-count "乔的手提箱") 0)) #t (error "手提箱：需要找到并交付手提箱"))
      (set! 阶段 "完成")
      (add-item! "金钱" 20)
      (play-dialogue! (line 手提箱的乔 "就是它！我明天总算不用空着手上门了。")
        (line "世界" "乔检查了箱扣，把约好的二十美元交给尼尔。")))
    (define (nodes-at 地点)
      (cond
        ((and (equal? 地点 "家") (member? 阶段 '("待交谈" "寻找中" "已找到")))
          (list (node "城市事件-乔" :display 'inline :name "乔：丢失的手提箱" :anchor "家" :children
            (if (equal? 阶段 "待交谈")
              (list (node "询问乔的手提箱" :button-text "交谈" :resolve (instant (outcome 交谈!))))
              (list (node "归还乔的手提箱" :button-text "交付" :name "归还手提箱" :subtitle "交还手提箱，领取20美元"
                :requires (list (req-item "乔的手提箱" 1)) :resolve (instant (outcome 归还!))))))))
        ((and (equal? 地点 "码头") (equal? 阶段 "寻找中"))
          (list (node "搜寻乔的手提箱" :name "卸货棚：寻找手提箱" :anchor "码头-货堆"
            :subtitle "检查卸货棚；坏结果损失1心情。找到后交还乔，可得20美元。"
            :clocks (list (搜寻 'render-data)) :risk '冒险
            :risk-target (list 'composure "player") :progress-target (list 'clock "寻找乔的手提箱")
            :requires (list (req-die)) :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1)))
              (outcome (lambda () (推进! 1))) (outcome (lambda () (推进! 2)))))))
        (else '())))
    (lambda (msg . args)
      (cond
        ((eq? msg 'eligible?) (equal? 阶段 "未出现"))
        ((eq? msg 'activate!) (if (equal? 阶段 "未出现") (set! 阶段 "待交谈") (error "手提箱：重复出现")))
        ((eq? msg 'on-day-start!) #t)
        ((eq? msg 'nodes-at) (nodes-at (car args)))
        ((eq? msg 'dossier)
          (if (equal? 阶段 "未出现") '()
            (list (dossier "乔：丢失的手提箱" :kind '委托 :where "家"
              :status (if (equal? 阶段 "完成") '了结 '进行中)
              :now (cond ((equal? 阶段 "待交谈") "乔上门打听一只丢失的手提箱，可以问问。")
                         ((equal? 阶段 "寻找中") "去码头卸货棚寻找棕色手提箱，交还乔可得20美元。这件事没有期限。")
                         ((equal? 阶段 "已找到") "手提箱已找到，带回家交还乔可得20美元。")
                         (else "手提箱已归还，收到20美元。"))))))
        ((eq? msg 'save) (list (list "stage" 阶段) (list "progress" (搜寻 'save))))
        ((eq? msg 'load!) (let ((data (car args)))
          (set! 阶段 (assoc-get data "stage" "未出现"))
          (if (member? 阶段 '("未出现" "待交谈" "寻找中" "已找到" "完成")) #t (error "手提箱：非法阶段"))
          (搜寻 'load! (assoc-get data "progress" 0))))
        (else (error "手提箱：未知消息"))))))
