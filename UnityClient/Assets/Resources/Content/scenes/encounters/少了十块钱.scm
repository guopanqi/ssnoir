;; 第二幕：初看只是十块工钱，争执升级后变成艾迪会不会失去工作。
(define 工钱 (make-clock "拿回工钱" 3 'gauge "核对账目并要求补发。" :beneficial-direction 'increase))
(define 争执 (make-clock "争执升级" 3 'pressure "满格后工头威胁解雇艾迪。" :beneficial-direction 'decrease))
(define 危机? #f)
(define 已结束? #f)
(define 有工时证据? #f)
(define (结束! result)
  (if 已结束? (error "工钱纠纷：重复结算") #t)
  (set! 已结束? #t)
  (cond
    ((eq? result '拿回工钱)
      (play-dialogue! (line "工头" "行，就差这十块。拿去。")
        (line "艾迪" "我就说账不对。谢了，尼尔。")))
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
        (line "艾迪" "这事还没完。"))))
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
    ((工钱 'full?) (结束! '拿回工钱))
    (else #f)))
(define (on-encounter-enter)
  (工钱 'load! 0) (争执 'load! 0)
  (set! 危机? #f) (set! 已结束? #f)
  (set! 有工时证据? (第二幕 'wage-evidence?))
  (play-dialogue!
    (line "世界" "工头合上账本。年轻工人说自己的十块钱没了。")
    (line "艾迪" "他干了一整天，你就给这么点？")
    (line "工头" "账上就写这么多。")))
(define (on-encounter-collapse) (collapse-result '撤出))
(define (get-render-data)
  (container "少了十块钱"
    (append
      (clock-nodes (工钱 'render-data) (争执 'render-data))
      (if 危机?
        (list
          (note-node "工头的威胁" "艾迪的工作" "他可能因为十块钱丢掉工作。")
          (node "先让艾迪退开" :subtitle "保住眼前的工作，放弃这次工钱"
            :resolve (instant (outcome (lambda () (结束! '保住工作)))))
          (node "请弗兰克出面" :subtitle "请求老街长辈来核账"
            :requires (list (req-die)) :resolve (roll 'social
              (outcome (lambda () (spend-composure! 1) (结束! '撤出)))
              (outcome (lambda () (结束! '弗兰克调停)))
              (outcome (lambda () (结束! '弗兰克调停)))))
          (node "继续坚持补钱" :subtitle "冒险与工头争执，可能损失冷静"
            :tags (list "高风险") :requires (list (req-die))
            :resolve (roll 'violence
              (outcome (lambda () (spend-composure! 2) (结束! '撤出)))
              (outcome (lambda () (结束! '保住工作)))
              (outcome (lambda () (结束! '拿回工钱))))))
        (append
          (if 有工时证据?
            (list (node "出示工时记录"
              :subtitle "提前核到签名；一骰即可让工头承认差额"
              :requires (list (req-die))
              :resolve (instant (outcome (lambda () (结束! '拿回工钱))))))
            '())
          (list
          (node "核对账本" :subtitle "寻找被少算的装卸记录"
            :requires (list (req-die)) :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1) (推进! 0 1)))
              (outcome (lambda () (推进! 1 0)))
              (outcome (lambda () (推进! 2 0)))))
          (node "向工头交涉" :subtitle "直接替工友争取应得工钱"
            :requires (list (req-die)) :resolve (roll 'social
              (outcome (lambda () (spend-composure! 1) (推进! 0 1)))
              (outcome (lambda () (推进! 1 1)))
              (outcome (lambda () (推进! 2 0)))))))))))
