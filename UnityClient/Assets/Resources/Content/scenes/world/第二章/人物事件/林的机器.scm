;; scenes/world/第二章/人物事件/林的机器.scm - 林·第二章第一拍《装配》
;;
;; 第一章那台在工棚里散架的机器，现在是公司拿出来展示的项目，而且要装到老码头来。
;; 林负责装。他很兴奋——对他来说这是「终于能干活了」，不是「终于能省下人了」。
;;
;; 玩家能不能参与，取决于晚宴那晚有没有走向他（见 晚宴.scm 的「找林说话」）。
;; **没走向他，这一段照样发生**，只是你插不上手：机器照装，他照样往上走，
;; 只是没有人在旁边问他那句话。
;;
;; 三趟里前两趟是活，第三趟才是这条线真正要的东西：
;;   校准／试跑  —— 他要人搭把手。你去，他就更快装完
;;   问他那些人  —— 只有陪过两趟、他把你当自己人以后，这句话才问得出口
;; 那句话写回的是他自己的「人文关怀」——第一章就有这个事实，这一章接着往里写，
;; 不新开一个。第三章问的是同一个东西：机器成功以后，他有没有想过谁付代价。
;;
;; 这条线和艾迪那条压在同一段日子里。取舍不来自「今天开不开门」，来自骰子只有那么多：
;; 两条都推完要花掉五六天，而这几天你还得挣钱、恢复、交房租。
(define 林的机器
  (let ()
    (define 收工-第几天 8)
    (define 要几趟 2)              ; 前两趟是搭手；第三趟是那句话

    ;; 未开始 / 进行中 / 装完了
    (define 状态 "未开始")
    (define 搭手 0)
    (define 问过了? #f)
    (define 说过了? #f)            ; 他当面跟你提过这件事（＝你知情）

    (define (进行中?) (equal? 状态 "进行中"))
    (define (够熟了?) (>= 搭手 要几趟))
    (define (能插手?) (lin 'invited?))
    (define (收工日) (第二章 'day-of 收工-第几天))
    (define (还没收工?) (< world-day (收工日)))

    ;; ── 日历推着走 ──────────────────────────────────
    ;; 装配不等你。你没去过一次，它照样在第 8 天装完。
    (define (on-day-end!)
      (cond
        ((and (equal? 状态 "未开始") (第二章 'started?) (>= (第二章 'day) 2))
         (set! 状态 "进行中"))
        ((and (进行中?) (>= world-day (收工日)))
         (收工!))
        (else #f)))

    (define (收工!)
      (set! 状态 "装完了")
      (if 说过了?
          (begin
            ((第二章 'journal) 'add!
              (if 问过了?
                  "机器装完了。林把那个数字记下来了，虽然他没说要拿它做什么。"
                  "机器装完了。林很高兴，他说这回它能自己跑一整班。"))
            (spotlight! "机器装好了"
              (if 问过了?
                  "第一批设备装完了。你问过他那些人以后要去哪儿，他到现在还没给出答案。"
                  "第一批设备装完了。林说它能自己跑一整班，说的时候眼睛是亮的。")))
          #f))

    ;; ── 他跟你提这件事（＝你知情）────────────────────
    (define (arrivals-at location)
      (if (and (equal? location "试验场") (进行中?) (not 说过了?) (能插手?))
          (list (arrival "林的装配"
                  (lambda ()
                    (set! 说过了? #t)
                    (play-dialogue!
                      (line "林" "侦探。你来得正好，帮我扶一下这根轨道。")
                      (line "尼尔" "这台就是要装到码头去的？")
                      (line "林" "整条泊位。下个月第一班就归它跑。")
                      (line "林" "在那之前它得在这儿跑通。你有空就过来。"))
                    (spotlight! "林的装配"
                      "林在试验场调第一批设备。他要人搭把手，装完之前你随时能去。"))))
          '()))

    ;; ── 搭把手 ──────────────────────────────────────
    (define (node-help)
      (action "帮林扶轨道" (list (req-die))
        (instant
          (outcome "又对上一段"
            (lambda ()
              (set! 搭手 (+ 搭手 1))
              (if (够熟了?)
                  (play-banter!
                    (line "林" "行了。剩下的我自己能收尾。"))
                  (play-banter!
                    (line "林" "差半寸。半寸它就爬不上去。")))
              (result-note! (string-append "搭了 " (number->string 搭手) " 趟")))))))

    ;; 陪过两趟他才肯认真接这句话。问完这条线就到头了——它要的不是次数，是那一次。
    (define (node-ask)
      (instant-action "问他那些人"
        (lambda ()
          (set! 问过了? #t)
          (lin 'on-saw-cost!)
          (play-dialogue!
            (line "尼尔" "这一台顶几个人？")
            (line "林" "整班。十二个。")
            (line "尼尔" "那十二个人下礼拜干什么？")
            (line "世界" "他手里的扳手停在那儿，没有转下去。")
            (line "林" "……公司说转岗。")
            (line "尼尔" "我问的是他们干什么。"))
          ((第二章 'journal) 'add! "你在码头上问了林那十二个人的去处。他没答上来。")
          (spotlight! "十二个"
            "你问了他这台机器顶掉几个人。他答得出数字，答不出那些人去哪儿。"))))

    (define (nodes-at location)
      (if (and (equal? location "试验场") (进行中?) 说过了? (能插手?)
               (还没收工?))
          (cond
            (问过了? '())
            ((够熟了?) (list (node-ask)))
            (#t (list (node-help))))
          '()))

    ;; ── 卷宗 ────────────────────────────────────────
    (define (dossier-entry)
      (if (or (not 说过了?) (not (能插手?)))
          '()
          (cond
            ((进行中?)
             (list (dossier "码头上的机器"
                     :kind '人物
                     :status '进行中
                     :now (cond
                            (问过了? "他还没想好怎么答。等装完")
                            ((够熟了?) "他信得过你了。去试验场问他，那些人以后干什么")
                            (#t "去试验场帮他扶轨道"))
                     :where "试验场"
                     :clocks (list (日期倒计时 "离装完" (收工日) 6
                                     "装完就轮不到你插手了。")))))
            (#t
             (list (dossier "码头上的机器"
                     :kind '人物 :status '了结
                     :now (if 问过了?
                              "机器装好了。那个数字他记着。"
                              "机器装好了。没人问过他那些人去哪儿。")
                     :where ""))))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((equal? msg 'dossier) (dossier-entry))
          ((equal? msg 'on-day-end!) (on-day-end!))
          ((equal? msg 'state) 状态)
          ((equal? msg 'asked?) 问过了?)
          ((equal? msg 'save)
           (list (list "state" 状态) (list "helped" 搭手)
                 (list "asked" (if 问过了? 1 0))
                 (list "told" (if 说过了? 1 0))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! 状态 (assoc-get data "state" "未开始"))
             (set! 搭手 (assoc-get data "helped" 0))
             (set! 问过了? (= (assoc-get data "asked" 0) 1))
             (set! 说过了? (= (assoc-get data "told" 0) 1))))
          (else (error "林的机器：收到未知消息")))))))
