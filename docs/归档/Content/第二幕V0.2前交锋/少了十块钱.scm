;; 第二幕：初看只是十块工钱，争执升级后变成艾迪会不会失去工作。
(define 工钱 (make-clock "拿回工钱" 3 'gauge "核对账目并要求补发。" :beneficial-direction 'increase))
(define 争执 (make-clock "争执升级" 3 'pressure "满格后工头威胁解雇艾迪。" :beneficial-direction 'decrease))
(define 危机? #f)
(define 已结束? #f)
(define 核过工时? #f)
(define (结束! result)
  (if 已结束? (error "工钱纠纷：重复结算") #t)
  (set! 已结束? #t)
  (cond
    ((eq? result '凭证核清)
      (play-dialogue!
        (line "尼尔" "这是签过名的工时册。账房少算了整整半班。")
        (line "工头" "……让账房补上。下回记得留底。")
        (line "艾迪" "你用的是自己的本事。他还真没法再赖。")))
    ((eq? result '拿回工钱)
      (play-dialogue! (line "工头" "行，就差这十块。拿去。")
        (line "工头" "艾迪，明天你不用来了。停一天，想清楚怎么跟我说话。")
        (line "艾迪" "钱给那小子。我的事我自己担着。")))
    ((eq? result '保住工作)
      (play-dialogue! (line "艾迪" "他们不能这么欺负人。")
        (line "尼尔" "先别把饭碗赔进去。")
        (line "艾迪" "……我记下这笔账。")))
    ((eq? result '弗兰克调停)
      (play-dialogue! (line "弗兰克" "把账拿来，我和你核一遍。")
        (line "工头" "行。你说了算。")
        (line "弗兰克" "都回去干活。下次先看账。")))
    (else
      (play-dialogue! (line "世界" "工头收了账本，工友们各自散开。")
        (line "工头" "艾迪，你停两天班。再闹就别来了。")
        (line "艾迪" "我在旧街食堂。这事还没完。"))))
  (end-encounter result))
(define (推进! amount heat)
  (工钱 'advance! amount)
  (争执 'advance! heat)
  (cond
    ((and (not 危机?)
          (or (>= (工钱 'current) 2) (争执 'full?)))
      (set! 危机? #t)
      (play-dialogue!
        (line "工头" "账我可以再核。可你们这么闹，明天谁还来卸货？")
        (line "工头" "艾迪，你再带头，下次就别来领班了。")
        (line "年轻工人" "艾迪，我还想在这里做工。")
        (line "艾迪" "……我只是想把你那十块钱要回来。")))
    ((工钱 'full?) (结束! (if 核过工时? '凭证核清 '拿回工钱)))
    (else #f)))
(define (on-encounter-enter)
  (工钱 'load! 0) (争执 'load! 0)
  (set! 危机? #f) (set! 已结束? #f) (set! 核过工时? #f)
  (play-dialogue!
    (line "年轻工人" "我只想把那十块钱拿回来。")
    (line "艾迪" "那就把账拿出来，让大伙看清楚。")))
(define (on-encounter-collapse) (collapse-result '撤出))
(define (get-render-data)
  (container "少了十块钱"
    (append
      (clock-nodes (工钱 'render-data) (争执 'render-data))
      (list (node "退出这场争执" :subtitle "离开；艾迪自行应付，可能被停班"
        :resolve (instant (outcome (lambda () (结束! '撤出))))))
      (if 危机?
        (list
          (note-node "工头的威胁" "艾迪的工作" "他可能因为十块钱丢掉工作。")
          (if 核过工时?
            (node "请工友核签名" :subtitle "把已找到的工时交给工友核实，避免停班"
              :requires (list (req-die))
              :resolve (instant (outcome (lambda () (结束! '凭证核清)))))
            (note-node "尚无工时凭证" "没有核清账" "只有当面交涉，仍可能让艾迪受工头报复。"))
          (node "先让艾迪退开" :subtitle "保住眼前的工作，放弃这次工钱"
            :resolve (instant (outcome (lambda () (结束! '保住工作)))))
          (node "继续坚持补钱" :subtitle "冒险与工头争执，可能损失冷静"
            :risk '危险 :risk-target (list 'composure "player") :requires (list (req-die))
            :resolve (roll 'violence
              (outcome (lambda () (spend-composure! 2) (结束! '撤出)))
              (outcome (lambda () (结束! '保住工作)))
              (outcome (lambda () (结束! '拿回工钱))))))
        (list
          (node "核对账本" :subtitle "寻找被少算的装卸记录"
            :requires (list (req-die)) :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1) (推进! 0 1)))
              (outcome (lambda () (set! 核过工时? #t) (推进! 1 0)))
              (outcome (lambda () (set! 核过工时? #t) (推进! 2 0)))))
          (node "向工头交涉" :subtitle "直接替工友争取应得工钱"
            :requires (list (req-die)) :resolve (roll 'social
              (outcome (lambda () (spend-composure! 1) (推进! 0 1)))
              (outcome (lambda () (推进! 1 1)))
              (outcome (lambda () (推进! 2 0))))))))))
