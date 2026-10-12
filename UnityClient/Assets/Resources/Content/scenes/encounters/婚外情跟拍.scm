;; 独立跟拍原型：未接入委托与照相馆。无额外时间期限。
;; 引擎原有回合冷静消耗仍适用；位置转换是真实的一骰动作。
(define finished? #f)
(define position '车里)
(define stay-turns 0)
(define car-trouble? #f)
(define tree-trouble? #f)
(define car-handled 0)
(define tree-handled 0)
(define truck-moved? #f)
(define situation (random-choice '(平静 送货车 亲密)))
(define waiter-called? #f)
(define waiter-active? #f)
(define waiter-position '车里)
(define photos (make-clock "有效照片" 12 'gauge "满12完成；树上好2中1坏警觉2，车里好1中0坏警觉1。"))
(define alertness (make-clock "对象警觉" 6 'gauge "满6暴露；到3后首次回合回应派服务员查看当时的位置。"))
(define tree-ready (make-clock "树上准备" 4 'gauge "前2格爬上枝杈，后2格稳住取景；每骰1格，满4才能拍。换位后保留。"))
(define cover (make-clock "打消怀疑" 4 'gauge "每骰掩饰1；满4服务员离开。留在被查位置每回合警觉加1。"))

(define (tree?) (equal? position '树上))
(define (blocked?) (and (not (tree?)) (equal? situation '送货车) (not truck-moved?)))
(define (trouble?) (if (tree?) tree-trouble? car-trouble?))
(define (finish! result)
  (if finished? (error "跟拍：重复结算") #t)
  (set! finished? #t)
  (spotlight! "跟拍结束" (if (equal? result '完成)
    "面孔和亲昵动作都拍清楚了。你收起相机离开。"
    "男人起身叫人。你带着已经拍到的照片离开。"))
  (end-encounter (list result (photos 'current) (alertness 'current))))
(define (check!)
  (cond ((photos 'full?) (finish! '完成))
        ((alertness 'full?) (finish! '暴露)) (else #f)))
(define (shoot! grade)
  (if (or (blocked?) (and (tree?) (not (tree-ready 'full?))))
    (error "跟拍：当前位置不能拍摄") #t)
  (let ((gain (cond ((equal? grade '好) (if (tree?) 2 1))
                   ((and (tree?) (equal? grade '中)) 1) (else 0)))
        (risk (cond ((equal? grade '坏) (if (tree?) 2 1))
                   ((and (tree?) (equal? grade '中)) 1) (else 0))))
    ;; 亲密时刻只在树上的俯视角拍得清；车窗被桌沿挡住。
    (if (and (tree?) (> gain 0) (equal? situation '亲密)) (set! gain (+ gain 1)) #f)
    (if (trouble?) (set! risk (+ risk 1)) #f)
    (photos 'advance! gain) (alertness 'advance! risk) (check!)))
(define (one-die title subtitle effect)
  (node title :subtitle subtitle :requires (list (req-die))
    :resolve (instant (outcome effect))))
(define (switch-node)
  (one-die (if (tree?) "回到车里" "走到树下")
    "换位花一骰；准备保留，原处麻烦仍在"
    (lambda () (set! position (if (tree?) '车里 '树上)) (set! stay-turns 0))))
(define (photo-node)
  (node (if (tree?) "树上拍摄" "车内拍摄")
    :tags (list (if (tree?) "高风险" "低风险"))
    :subtitle (if (tree?) "俯视餐桌，能拍清面孔与亲昵动作" "隔着车窗拍；角度受限，成果较少")
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome (lambda () (shoot! '坏)))
      (outcome (lambda () (shoot! '中)))
      (outcome (lambda () (shoot! '好))))))
(define (trouble-node)
  (one-die (if (tree?) "应付孩子" "应付路人")
    "需两骰打发；未处理时拍摄额外警觉1"
    (lambda ()
      (if (tree?)
        (begin (set! tree-handled (+ tree-handled 1))
          (if (>= tree-handled 2) (set! tree-trouble? #f) #f))
        (begin (set! car-handled (+ car-handled 1))
          (if (>= car-handled 2) (set! car-trouble? #f) #f))))))
(define (cover-node)
  (one-die "藏起相机" "服务员在查看；每骰掩饰1，满4离开"
    (lambda ()
      (cover 'advance! 1)
      (if (cover 'full?)
        (begin (set! waiter-active? #f)
          (play-bubble! (line "世界" "服务员没发现相机，回到饭店里。"))) #f))))

(define-opponent-rule "服务员查原位置"
  (lambda () (and (not finished?) waiter-active? (equal? position waiter-position)))
  (lambda () (alertness 'advance! 1) (check!)))
(define-opponent-rule "丈夫叫服务员"
  (lambda () (and (not finished?) (not waiter-called?) (>= (alertness 'current) 3)))
  (lambda ()
    (set! waiter-called? #t) (set! waiter-active? #t) (set! waiter-position position)
    (play-dialogue! (line "世界" "男人指了指窗外，服务员走出饭店。")
      (line "世界" "他查看你刚才的位置。换位能避开，掩饰能让他离开。"))))
(define-opponent-rule "停留引来注意"
  (lambda () (not finished?))
  (lambda ()
    (set! stay-turns (+ stay-turns 1))
    (if (and (>= stay-turns 2) (not (trouble?)))
      (begin
        (if (tree?)
          (begin (set! tree-trouble? #t) (set! tree-handled 0))
          (begin (set! car-trouble? #t) (set! car-handled 0)))
        (set! stay-turns 0)
        (play-bubble! (line "世界" (if (tree?)
          "孩子抬头看着树枝，问你在上面做什么。"
          "路人停在车旁，敲了敲窗，问你在等谁。")))) #f)))
(define-opponent-rule "街边场面变化"
  (lambda () (not finished?))
  (lambda () (set! situation (random-choice '(平静 送货车 亲密))) (set! truck-moved? #f)))
(define (on-encounter-enter)
  (play-dialogue!
    (line "世界" "两人在饭店窗边吃饭。你坐在街边的车里。")
    (line "世界" "车里可以直接拍。树上视野好，但要先爬上去稳住取景。")
    (line "世界" "留在同一位置两轮，会有人注意你。")))
(define (on-encounter-collapse)
  (collapse-result (list '倒下 (photos 'current) (alertness 'current))))
(define (get-render-data)
  (container "婚外情跟拍"
    (append
      (clock-nodes (photos 'render-data) (alertness 'render-data))
      (list (note-node "标注：当前位置" (if (tree?) "当前位置：树上" "当前位置：车里")
        (string-append "同处停留：" (number->string stay-turns)
          "轮；两轮引来麻烦。换位花一骰，已有麻烦保留。"))
        (note-node "标注：场面" "此刻的场面"
          (cond ((equal? situation '送货车) (if truck-moved?
                  "你已挪开车位，本轮车窗不再被挡。"
                  "送货车停在路边。车里不能拍；可挪车或换到树上。"))
                ((equal? situation '亲密) "女人握住男人的手。树上拍成额外加1；车里角度看不清。")
                (else "两人在桌边交谈。两处都没有临时遮挡。"))))
      (if tree-trouble? (list (note-node "标注：树下孩子" "孩子盯着树上"
        (string-append "树上拍摄额外警觉1；已应付" (number->string tree-handled) "/2。"))) '())
      (if car-trouble? (list (note-node "标注：车旁路人" "路人还在车旁"
        (string-append "车内拍摄额外警觉1；已应付" (number->string car-handled) "/2。"))) '())
      (if (tree?) (list (clock-node "标注：树上准备" (tree-ready 'render-data))) '())
      (if waiter-active?
        (list (clock-node "标注：服务员" (cover 'render-data))
          (note-node "标注：查看位置" "服务员的位置"
            (if (equal? waiter-position '车里) "还在查看车旁；在车里结束回合警觉加1。"
              "还在查看树下；在树上结束回合警觉加1。"))
          (cover-node)) '())
      (list (switch-node))
      (if (and (tree?) (not (tree-ready 'full?)))
        (list (one-die (if (< (tree-ready 'current) 2) "爬上枝杈" "稳住取景")
          "每骰准备1格；两步各2格，准备保留"
          (lambda () (tree-ready 'advance! 1)))) '())
      (if (trouble?) (list (trouble-node)) '())
      (if (blocked?) (list (one-die "挪动车位" "花一骰，本轮重新获得车内视线"
        (lambda () (set! truck-moved? #t)))) '())
      (if (or (blocked?) (and (tree?) (not (tree-ready 'full?)))) '()
        (list (photo-node))))))
