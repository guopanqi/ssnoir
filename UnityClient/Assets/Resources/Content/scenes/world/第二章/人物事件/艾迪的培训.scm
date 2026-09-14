;; scenes/world/第二章/人物事件/艾迪的培训.scm - 艾迪·第二章第一拍《培训》
;;
;; 公司要在老码头装机器，先给工人发了培训通知。对艾迪来说这是一条出路，
;; 也是他这辈子第一次要靠脑子而不是靠这双手吃饭——而那只手在第一章已经坏过一次。
;;
;; 这条线**不给能力，不给地点权限**。它是这一版的对照组：一个不给你任何回报的人，
;; 你还去不去。所以两件事必须成立：
;;   一 去一趟不贵——凑够次数最多三趟，不必天天陪着；
;;   二 窗口会关，而且提前看得见——考试就那一天，过了就是过了。
;;
;; 它和晚宴是两种不同形状的窗口，故意的：晚宴是**固定日期的一晚**，这一条是
;; **一段可以自己安排的日子 + 一个截止**。
;;
;; **三件事必须分开，别混成一个状态：**
;;   事情发生了  他哪天报名、哪天考试，由日历推着走。你不去码头，它照样发生。
;;   你知情了    你走进码头、他跟你说过这件事。不知情不等于没发生。
;;   你参与了    你陪他练了几趟。
;; 混在一起就会变成「玩家不去，他的人生就停在原地等着」——那正是这条线要反对的东西。
;;
;; 手在第一章伤过的人要多陪一趟。第一章你从他身上赚得越狠，这一章他越需要人。
(define 艾迪的培训
  (let ()
    (define 准备天数 3)
    ;; 考试**当天**就考。所以能陪他练的是那天之前的日子：卡在 world-day < 考试日
    ;; 时才出现，结算在跨进考试日的那个日终发生。两处用的是同一条边界。

    ;; 未开始 → 进行中 → 通过 / 没过。整条线由日历推动。
    (define 状态 "未开始")
    (define 练了 0)
    (define 说过了? #f)          ; 他当面跟你说过这件事（＝你知情）

    (define (要几趟) (if (eddie 'hand-bad?) 3 2))
    (define (够了?) (>= 练了 (要几趟)))
    (define (他还在?) (eddie 'known?))
    (define (考试日) (+ (第二章 'phase-start-day) 准备天数))
    (define (进行中?) (equal? 状态 "进行中"))
    (define (还没考?) (< world-day (考试日)))

    ;; ── 日历推着走 ──────────────────────────────────
    ;; 由章节在日终统一调用（见 第二章.scm）。开门和结算都不问玩家在哪儿。
    (define (on-day-end!)
      (cond
        ((and (equal? 状态 "未开始") (equal? (第二章 'phase) "B") (他还在?))
         (set! 状态 "进行中"))
        ((and (进行中?) (>= world-day (考试日)))
         (结算!))
        (else #f)))

    (define (结算!)
      (if (够了?)
          (begin
            (set! 状态 "通过")
            (eddie 'on-training-passed!)
            (报一声! "他考过了"
              "艾迪拿到了培训名额。机器进来的时候，他至少还站在名单上。"
              "艾迪考过了培训。他往新机器那边挪了一步。"))
          (begin
            (set! 状态 "没过")
            (报一声! "他没考过"
              (if (> 练了 0)
                  "艾迪差一点。他说不怪你，说他本来就不是念书的料。"
                  "考试那天你不在。艾迪自己去的，回来什么也没说。")
              "艾迪没考过。他还在码头上等零活。"))))

    ;; 你没听说过这件事，就不会凭空知道它的结果。事情照样发生，只是没人告诉你。
    (define (报一声! 标题 正文 履历)
      (if 说过了?
          (begin
            ((第二章 'journal) 'add! 履历)
            (spotlight! 标题 正文))
          #f))

    ;; ── 他来找你（＝你知情）─────────────────────────
    ;; 只在窗口开着的时候说一次。晚了才走进码头，他说的话也不一样——
    ;; 事情已经往前走了，你才刚听说。
    (define (arrivals-at location)
      (if (and (equal? location "码头") (进行中?) (not 说过了?))
          (list (arrival "艾迪的培训通知"
                  (lambda ()
                    (set! 说过了? #t)
                    (play-dialogue!
                      (line "艾迪" "他们贴了张纸。说要办培训，学开新机器。")
                      (line "尼尔" "你报了？")
                      (line "艾迪" "报了。三天后考试。")
                      (line "艾迪" "我这手拿扳手行，拿笔不行。你识字快。")
                      (line "尼尔" "考试哪天？")
                      (line "艾迪" "机器进场前就得定名单。你有空就来码头。"))
                    (spotlight! "培训通知"
                      "艾迪报了名。三天后考试，在那之前可以去码头陪他练几趟。"))))
          '()))

    ;; ── 陪他练 ──────────────────────────────────────
    (define (node-practice)
      (at-anchor "码头-货堆"
       (action "陪艾迪练手" (list (req-die))
        (instant
          (outcome "又过了一遍"
            (lambda ()
              (set! 练了 (+ 练了 1))
              (if (够了?)
                  (play-banter!
                    (line "艾迪" "这几张我背下来了。真考的时候别慌就行。"))
                  (play-banter!
                    (line "艾迪" "……手抖不是紧张，是使不上劲。")))
              (result-note! (string-append "练了 " (number->string 练了) " 趟"))))))))

    (define (nodes-at location)
      (if (and (equal? location "码头") (进行中?) 说过了?
               (还没考?) (not (够了?)))
          (list (node-practice))
          '()))

    ;; ── 卷宗 ────────────────────────────────────────
    ;; 没人告诉过你的事不进卷宗——那张纸是他兜里的，不是你桌上的。
    (define (dossier-entry)
      (if (not 说过了?)
          '()
          (cond
            ((进行中?)
             (list (dossier "艾迪的手"
                     :kind '人物
                     :status '进行中
                     :now (cond
                            ((够了?) "他准备好了。剩下的看他自己")
                            ((不能再练?) "考试就在今天。来不及了")
                            (#t (string-append "去码头陪他练，还要 "
                                               (number->string (- (要几趟) 练了)) " 趟")))
                     :where (if (还没考?) "码头" "")
                     :clocks (list (日期倒计时 "离考试" (考试日) 准备天数
                                     "过了那天就没有下一场。")))))
            ((equal? 状态 "通过")
             (list (dossier "艾迪的手"
                     :kind '人物 :status '了结
                     :now "他拿到了名额。" :where "")))
            (#t
             (list (dossier "艾迪的手"
                     :kind '人物 :status '了结
                     :now "名额没了。他还在码头上等零活。" :where ""))))))

    (define (不能再练?) (and (not (还没考?)) (not (够了?))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((equal? msg 'dossier) (dossier-entry))
          ((equal? msg 'on-day-end!) (on-day-end!))
          ((equal? msg 'state) 状态)
          ((equal? msg 'save)
           (list (list "state" 状态) (list "practiced" 练了)
                 (list "told" (if 说过了? 1 0))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! 状态 (assoc-get data "state" "未开始"))
             (set! 练了 (assoc-get data "practiced" 0))
             (set! 说过了? (= (assoc-get data "told" 0) 1))))
          (else (error "艾迪的培训：收到未知消息")))))))
