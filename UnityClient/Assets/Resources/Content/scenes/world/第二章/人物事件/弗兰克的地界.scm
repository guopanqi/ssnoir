;; scenes/world/第二章/人物事件/弗兰克的地界.scm - 弗兰克·第二章《量地的人》
;;
;; Phase B 一开始，公司的人就带着尺子和本子进了老街：量仓库、登记门牌、贴培训通知。
;; 弗兰克第一次遇到一件他压不住的事——对方不违规、不动手、还带着市政的纸。
;;
;; 他前半章没有大剧情（那时候他只是在管这条街）。这一拍才是他的第二章：
;; **一个靠「出了事我真的会管」立起来的人，遇到一件他管不了的事。**
;;
;; 你不介入，他也会往前走——往硬的那边走。这条线唯一要写回人物模块的，
;; 就是那天他还坐不坐得下来。第三章问的是同一件事：他最后是能代表这条街
;; 谈判的人，还是一个地方上的暴力头领。
;;
;; 只有他认你这个人（第一章巷子那一晚）才插得上手。当街把已经不还手的人往死里打过的，
;; 这时候说什么他都不会听。
(define 弗兰克的地界
  (let ()
    (define 要几趟 2)
    (define 收口-第几天 5)         ; Phase B 第几天他自己拿主意

    ;; 未开始 / 进行中 / 劝住了 / 由他去了
    (define 状态 "未开始")
    (define 跟着看 0)
    (define 说过了? #f)

    (define (进行中?) (equal? 状态 "进行中"))
    (define (够熟了?) (>= 跟着看 要几趟))
    (define (插得上手?) (frank 'approved?))
    (define (关窗日) (+ (第二章 'phase-start-day) 收口-第几天))
    (define (还开着?) (< world-day (关窗日)))

    (define (on-day-end!)
      (cond
        ((and (equal? 状态 "未开始") (equal? (第二章 'phase) "B"))
         (set! 状态 "进行中"))
        ((and (进行中?) (>= world-day (关窗日)))
         (收口!))
        (else #f)))

    ;; 你没劝，他就自己拿主意。这不是惩罚，是他本来就会做的事。
    (define (收口!)
      (set! 状态 "由他去了")
      (frank 'on-pushed-to-force!)
      (if 说过了?
          (begin
            ((第二章 'journal) 'add! "弗兰克开始自己安排人手。他不再等谁来讲道理。")
            (spotlight! "他自己拿主意了"
              "弗兰克把话说死了：机器进来那天，谁也别想安安静静地卸货。"))
          #f))

    (define (arrivals-at location)
      (if (and (equal? location "居民区") (进行中?) (not 说过了?) (插得上手?))
          (list (arrival "量地的人"
                  (lambda ()
                    (set! 说过了? #t)
                    (play-dialogue!
                      (line "世界" "两个穿风衣的人在仓库墙上量尺寸，旁边站着个拿本子的。")
                      (line "弗兰克" "他们有市政的纸。我连轰都轰不走。")
                      (line "尼尔" "他们量的是仓库？")
                      (line "弗兰克" "仓库、码头、还有后面那排住的地方。")
                      (line "弗兰克" "你要是闲着，跟我走两趟，看看他们到底在量什么。"))
                    (spotlight! "量地的人"
                      "公司的人开始在老街量东西。弗兰克想弄清他们要动哪一块。"))))
          '()))

    (define (node-walk)
      (action "跟弗兰克走" (list (req-die))
        (instant
          (outcome "又记下一处"
            (lambda ()
              (set! 跟着看 (+ 跟着看 1))
              (if (够熟了?)
                  (play-banter!
                    (line "弗兰克" "住的那排也在里头。他们没打算只要码头。"))
                  (play-banter!
                    (line "弗兰克" "三号、五号、七号仓。都是干活的地方。")))
              (result-note! (string-append "走了 " (number->string 跟着看) " 趟")))))))

    (define (node-hold)
      (instant-action "劝他等等"
        (lambda ()
          (set! 状态 "劝住了")
          (frank 'on-stayed-at-table!)
          (play-dialogue!
            (line "弗兰克" "机器进来那天，我把人拉到跳板上，看他们怎么卸。")
            (line "尼尔" "然后警察来清场，报纸写老街闹事，公司拿到它要的那张纸。")
            (line "弗兰克" "那你说怎么办。")
            (line "尼尔" "你手里有他们量过哪几块地。先别动手，等一个能坐下来说话的场合。")
            (line "世界" "他把烟按灭了，没答应，也没再说要拉人。"))
          ((第二章 'journal) 'add! "你劝住了弗兰克。他答应先不动手，看看有没有能坐下来谈的场合。")
          (spotlight! "他先不动手"
            "弗兰克把那张量地的单子收进兜里。机器进来那天，他还愿意先看着。"))))

    (define (nodes-at location)
      (if (and (equal? location "居民区") (进行中?) 说过了? (插得上手?) (还开着?))
          (if (够熟了?) (list (node-hold)) (list (node-walk)))
          '()))

    (define (dossier-entry)
      (if (or (not 说过了?) (not (插得上手?)))
          '()
          (cond
            ((进行中?)
             (list (dossier "量地的人"
                     :kind '人物 :status '进行中
                     :now (if (够熟了?)
                              "你知道他们要动哪几块了。去居民区跟弗兰克把话说清"
                              (string-append "去居民区跟他走一趟，还要 "
                                             (number->string (- 要几趟 跟着看)) " 趟"))
                     :where "居民区"
                     :clocks (list (日期倒计时 "离他动手" (关窗日) 5
                                     "他等不了太久。")))))
            ((equal? 状态 "劝住了")
             (list (dossier "量地的人" :kind '人物 :status '了结
                     :now "他答应先不动手。" :where "")))
            (#t
             (list (dossier "量地的人" :kind '人物 :status '了结
                     :now "他开始自己安排人手了。" :where ""))))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((equal? msg 'dossier) (dossier-entry))
          ((equal? msg 'on-day-end!) (on-day-end!))
          ((equal? msg 'state) 状态)
          ((equal? msg 'save)
           (list (list "state" 状态) (list "walked" 跟着看)
                 (list "told" (if 说过了? 1 0))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! 状态 (assoc-get data "state" "未开始"))
             (set! 跟着看 (assoc-get data "walked" 0))
             (set! 说过了? (= (assoc-get data "told" 0) 1))))
          (else (error "弗兰克的地界：收到未知消息")))))))
