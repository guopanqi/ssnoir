;; 独立交锋原型：尚未接入城市委托、报酬或照相馆。
;; 无离场期限。场面每回合抽换；引擎原有回合冷静消耗仍然适用。
(define finished? #f)
(define waiter-called? #f)
(define waiter-active? #f)
(define situation (random-choice '(路人 遮挡 孩子 亲密)))
(define photos (make-clock "有效照片" 12 'gauge "满12完成取证；近处好2中1，远处好1。亲密时刻拍成额外加1。"))
(define alertness (make-clock "对象警觉" 6 'gauge "满6拍摄失败；达到3后首次回合回应派出服务员。"))
(define cover (make-clock "打消怀疑" 4 'gauge "服务员在场时，每颗骰掩饰1格；满4离开。未离开每回合警觉加1。"))

(define (finish! result)
  (if finished? (error "婚外情跟拍：重复结算") #t)
  (set! finished? #t)
  (spotlight! "跟拍结束"
    (if (equal? result '完成)
      "两人的脸和亲昵动作都拍清楚了。你收起相机离开。"
      "男人盯住了窗外，起身叫人。你带着已经拍到的照片离开。"))
  (end-encounter (list result (photos 'current) (alertness 'current))))

(define (check!)
  (cond ((photos 'full?) (finish! '完成))
        ((alertness 'full?) (finish! '暴露))
        (else #f)))

(define (shoot! near? grade)
  (let ((gain (cond ((equal? grade '好) (if near? 2 1))
                    ((and near? (equal? grade '中)) 1) (else 0)))
        (risk (cond ((equal? grade '坏) (if near? 2 1))
                    ((and near? (equal? grade '中)) 1) (else 0))))
    ;; 路人只挡远处；送货车只保护近处；孩子只扰近处。
    (if (and (not near?) (equal? situation '路人)) (set! gain 0) #f)
    (if (and (> gain 0) (equal? situation '亲密)) (set! gain (+ gain 1)) #f)
    (if (and near? (equal? situation '遮挡)) (set! risk (max 0 (- risk 1))) #f)
    (if (and near? (equal? situation '孩子)) (set! risk (+ risk 1)) #f)
    (photos 'advance! gain)
    (alertness 'advance! risk)
    (check!)))

(define (photo-node near?)
  (node (if near? "门廊拍摄" "隔街拍摄")
    :tags (list (if near? "高风险" "低风险"))
    :subtitle (if near? "靠近窗边，拍清面孔与动作" "隔街取景，距离远但不易暴露")
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome (lambda () (shoot! near? '坏)))
      (outcome (lambda () (shoot! near? '中)))
      (outcome (lambda () (shoot! near? '好))))))

(define (cover-node)
  (node "装作等人"
    :subtitle "收起相机翻看菜单；每骰掩饰1格"
    :requires (list (req-die))
    :resolve (instant (outcome (lambda ()
      (cover 'advance! 1)
      (if (cover 'full?)
        (begin (set! waiter-active? #f)
          (play-bubble! (line "世界" "服务员看了一会儿，转身回到店里。"))) #f))))))

(define-opponent-rule "服务员查看街外"
  (lambda () (and (not finished?) waiter-active?))
  (lambda ()
    (play-bubble! (line "世界" "服务员还在门口查看，男人不断往窗外看。"))
    (alertness 'advance! 1)
    (check!)))

;; 独立因果拍：新派出的服务员不在同一轮立刻增加警觉。
(define-opponent-rule "丈夫叫来服务员"
  (lambda () (and (not finished?) (not waiter-called?) (>= (alertness 'current) 3)))
  (lambda ()
    (set! waiter-called? #t)
    (set! waiter-active? #t)
    (play-dialogue!
      (line "世界" "男人指了指窗外。服务员放下托盘，走到门口。")
      (line "世界" "他开始查看街边。你可以掩饰，也可以继续抢拍。"))))

(define-opponent-rule "街边场面变化"
  (lambda () (not finished?))
  (lambda () (set! situation (random-choice '(路人 遮挡 孩子 亲密)))))

(define (on-encounter-enter)
  (play-dialogue!
    (line "世界" "饭店靠窗的桌边，男人正与一个女人吃饭。")
    (line "世界" "门廊能拍清面孔，街对面不易被发现。")
    (line "世界" "拍够有效照片再离开；别让他们发现相机。")))

(define (on-encounter-collapse)
  (collapse-result (list '倒下 (photos 'current) (alertness 'current))))

(define (get-render-data)
  (container "婚外情跟拍"
    (append
      (clock-nodes (photos 'render-data) (alertness 'render-data))
      (list (note-node "标注：拍摄场面" "此刻的场面"
        (cond
          ((equal? situation '路人) "路人在街对面停下，挡住远处镜头。隔街拍摄本回合没有成果。")
          ((equal? situation '遮挡) "送货车挡住门廊外的人影。近处拍摄的警觉代价减1。")
          ((equal? situation '孩子) "孩子在门廊旁盯着相机。近处每次拍摄额外增加1警觉。")
          (else "女人握住男人的手。两处拍摄只要拍成，有效照片额外加1。"))))
      (if waiter-active?
        (list (clock-node "标注：服务员怀疑" (cover 'render-data)) (cover-node)) '())
      (list (container "门廊近处" (list (photo-node #t)))
            (container "街对面" (list (photo-node #f)))))))
