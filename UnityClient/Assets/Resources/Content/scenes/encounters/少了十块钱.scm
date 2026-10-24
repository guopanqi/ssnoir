;; 艾迪工钱：一条凭证目标和一条停班压力；中途工友退缩迫使改换行动。
;; 艾迪始终坚持原则。结果只有保住工作／被停班。
(define 凭证 (make-clock "核清工时" 6 'gauge "证明年轻工人的半班确实被漏掉。" :beneficial-direction 'increase))
(define 处分 (make-clock "停班威胁" 6 'pressure "工头打算以停班压住讨薪。" :beneficial-direction 'decrease))
(define 退缩? #f)
(define 弗兰克帮忙? #f)
(define 结束? #f)
(define (收场! result)
  (if 结束? (error "工钱：已经结案") #t)
  (set! 结束? #t)
  (play-dialogue!
    (line "世界" (if (eq? result '上工) "账房承认漏记了那半班。工头把钱补到年轻工人手中。" "工头将账本合上，把艾迪从接下来几天的排班划去。"))
    (line "艾迪" (if (eq? result '上工) "钱是他应得的。收好，继续干活。" "你先回去上工。这是我的班，我自己担着。")))
  (end-encounter result))
(define (检查!)
  (cond
    ((处分 'full?) (收场! '停班))
    ((凭证 'full?) (收场! '上工))
    ((and (not 退缩?) (>= (凭证 'current) 2))
      (set! 退缩? #t)
      (处分 'advance! 1)
      (play-dialogue!
        (line "世界" "工头把排班表摊开，在艾迪名字旁画了一道线。")
        (line "工头" "年轻人，你确定要让他替你闹？再闹，你俩明天都别来。")
        (line "年轻工人" "艾迪，我家里还等着我挣钱……我不敢签字。")
        (line "艾迪" "你不用替我出头。少你的工钱，总有人该说话。")))
    (else #f)))
(define (查证! progress cost)
  (凭证 'advance! progress)
  (处分 'advance! cost)
  (检查!))
(define (弗兰克调账!)
  (if (and 退缩? (not 弗兰克帮忙?)) #t (error "弗兰克已协助"))
  (set! 弗兰克帮忙? #t)
  (凭证 'advance! 2)
  (处分 'advance! -1)
  (play-dialogue!
    (line "弗兰克" "昨晚吊车班次有记录。拿那张来，不必逼着小伙子签字。")
    (line "世界" "他将写有卸货班次的复写纸摊在桌上。"))
  (检查!))
(define-opponent-rule "工头催促关账"
  (lambda () (not 结束?))
  (lambda () (处分 'advance! 1) (检查!)))
(define (on-encounter-enter)
  (凭证 'load! 0) (处分 'load! 0)
  (set! 退缩? #f) (set! 弗兰克帮忙? #f) (set! 结束? #f)
  (play-dialogue!
    (line "世界" "工资台前，几张皱巴巴的收条夹在账本里。艾迪挡住工头合上账本的手。")
    (line "艾迪" "少了十块钱。我们就在这儿核清楚。")))
(define (on-encounter-collapse) (collapse-result '停班))
(define (get-render-data)
  (container "少了十块钱" (append
    (clock-nodes (凭证 'render-data) (处分 'render-data))
    (list (note-node "工钱处境" "现在的局面"
      (if 退缩? "年轻工人担心失业，不愿当面作证。你得找到其他凭证。" "先核实工时；工头正在催着关账。")))
    (if (not 退缩?)
      (list (node "核对工时簿" :subtitle "查底账；失败冷静-1"
          :risk '稳妥 :progress-target (list 'clock "核清工时")
          :risk-target (list 'composure "player") :requires (list (req-die))
          :resolve (roll 'sharpness
            (outcome (lambda () (spend-composure! 1) (查证! 0 0)))
            (outcome (lambda () (查证! 1 0)))
            (outcome (lambda () (查证! 2 0)))))
        (node "请工友对证" :subtitle "直接找证人，可能激化争执"
          :risk '冒险 :progress-target (list 'clock "核清工时")
          :risk-target (list 'clock "停班威胁") :requires (list (req-die))
          :resolve (roll 'social
            (outcome (lambda () (查证! 0 1)))
            (outcome (lambda () (查证! 1 0)))
            (outcome (lambda () (查证! 2 0))))))
      (append
        (list (node "查吊车班次" :subtitle "不再逼证人；核对卸货记录"
          :risk '稳妥 :progress-target (list 'clock "核清工时")
          :requires (list (req-die))
          :resolve (roll 'sharpness
            (outcome (lambda () (查证! 0 0)))
            (outcome (lambda () (查证! 1 0)))
            (outcome (lambda () (查证! 2 0))))))
        (if (not 弗兰克帮忙?)
          (list (node "请弗兰克帮忙" :subtitle "一次协助：取得记录、缓和处分"
            :risk '稳妥 :progress-target (list 'clock "核清工时")
            :requires (list (req-die)) :resolve (instant (outcome 弗兰克调账!)))) '())))
    (list (instant-action "退出争执" (lambda () (收场! '停班)))))))
