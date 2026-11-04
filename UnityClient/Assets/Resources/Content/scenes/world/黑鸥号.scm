;; 黑鸥号：抢卸夜班之后开放的独立地点，暂复用码头岸口的空间与机位。
;; 船上相识归此模块；泊位协商仍由第三幕拥有，不复制人物进度。
(define black-gull
  (let ()
    (define known? #f)
    (define boarded? #f)
    (define (invite!)
      (if known? #f
        (begin (set! known? #t)
          (play-dialogue!
            (line "艾迪" "收工了。岸口那艘黑鸥号，弗兰克常在船上。跟我去坐坐？")
            (line "世界" "艾迪指了指码头旁的舷梯。以后经过这里，也可以上船找他们。")))))
    (define (board!)
      (if (and known? (not boarded?)) #t (error "黑鸥号：不可重复初登船"))
      (set! boarded? #t)
      (let ((met? (第二幕生活 'met-frank?)))
        (第二幕生活 'meet-frank!)
        (play-dialogue!
          (line "世界" (if (equal? (第二幕节奏 'ship-work) "已结算")
            "艾迪带尼尔走上舷梯。舱门敞着，炉边有人修工具，桌上留着几只杯子。"
            "尼尔沿舷梯上船。弗兰克从炉边抬起头，招呼他坐下。"))
          (line "艾迪" (if (not (equal? (第二幕节奏 'ship-work) "已结算")) "尼尔，来坐。弗兰克在这儿。" (if met? "弗兰克，尼尔来了。抢卸那班我们搭的伙。" "弗兰克，这是尼尔。抢卸那班我们一起干的活。")))
          (line "弗兰克" "坐。杯子在炉子边。谁拿了扳手，走前给我放回来。")
          (line "尼尔" "这里总这么热闹？")
          (line "弗兰克" "总有人下工，总有人还没吃。你以后路过就上来。"))))
    (define (sit!)
      (play-dialogue!
        (line "世界" "尼尔在炉边坐下，听工人说起今天的点工。有人挪开工具，给他腾了一块桌面。")
        (line "弗兰克" "下回有活儿，我给你留个话。先暖和暖和。"))
      (restore-actor-composure! 'player 1))
    (lambda (msg . args)
      (cond
        ((eq? msg 'known?) known?)
        ((eq? msg 'discover!) (set! known? #t))
        ((eq? msg 'invite!) (invite!))
        ((eq? msg 'render-data)
          (list (place "黑鸥号" :anchor "码头-岸口"
            :children (append
              (list (node "黑鸥号炉边小坐" :name "炉边小坐" :anchor "码头-岸口"
                :subtitle "花一枚骰，和船上的人坐坐，恢复1心情"
                :use-limit 'daily-once :requires (list (req-die))
                :progress-target (list 'composure "player")
                :resolve (instant (outcome sit!))))
              (地点节点 "黑鸥号"))
            :arrivals (append (if (and known? (not boarded?)) (list (arrival "初登黑鸥号" board!)) '())
              (地点入场 "黑鸥号")))))
        ((eq? msg 'dossier)
          (if (and known? (not boarded?))
            (list (dossier "艾迪的船上邀约" :kind '人物 :primary #f
              :now (if (equal? (第二幕节奏 'ship-work) "已结算")
                "抢卸夜班结束，艾迪邀你去码头附近的黑鸥号坐坐，认识弗兰克。"
                "弗兰克住在码头附近的黑鸥号上，招呼你有空去坐坐。") :where "黑鸥号")) '()))
        ((eq? msg 'save) (list (list "known" known?) (list "boarded" boarded?)))
        ((eq? msg 'load!)
          (let ((data (car args)))
            (set! known? (assoc-get data "known" (equal? (第二幕节奏 'ship-work) "已结算")))
            (set! boarded? (assoc-get data "boarded" #f))
            (if (and (or (equal? known? #t) (equal? known? #f))
                     (or (equal? boarded? #t) (equal? boarded? #f))
                     (or (not boarded?) known?)) #t (error "黑鸥号：非法发现或登船状态"))))
        (else (error "黑鸥号：未知消息"))))))
