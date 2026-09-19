;; 沃尔特（Walter Finch）——保险公司的理赔调查员。上流社会那一面里和你有往来的人。
;;
;; 认识他的唯一入口是晚宴：那晚在吧台陪他喝到底，他才记得你。没跟他谈，就没有这条线——
;; 「你站在谁旁边，另一边的门那晚就不开」说的就是这个。
;;
;; 之后两天他在格兰德酒店大堂等你，把码头那桩疑似骗保的案子交给你：一张卡《核赔》，
;; 交锋见 encounters/核赔.scm。坐实还是放水由玩家在交锋里定，它决定后面那份活的性质。
;;
;; 了结的回报是一份稳定的侦探活：酒店大堂常驻一张「替沃尔特跑外勤」。坐实→他信你办事，
;; 活重钱多；放水→他也知道你会放水，活轻钱少，但失手不扣冷静。这张卡不随码头的班次
;; 一起减少——上城的活不受机器影响，这个对比是有意的。
;;
;; 保险公司不再是地图上的地点：它从头到尾只有一张卡，一栋楼配一张卡就是它单调的根源。
;; 他的一切都在酒店大堂发生。
(define walter
  (let ()
    ;; 0 没谈上 / 1 晚宴谈成，等他找你 / 2 待核赔 / 3 有往来
    (define stage 0)
    (define met-day 0)            ; 晚宴那一天；两天后他在大堂等你
    (define claim-result "无")    ; 无 / 坐实 / 放水
    (define manifest? #f)         ; 那批货的舱单到手没有
    (define manifest-clk
      (make-clock "找舱单" 4 'gauge
        "翻两天账房的记录，或者拿一份情报跟他换。"))
    (define identity "保险公司的理赔调查员")
    (define journal (make-journal))
    (define 等几天 2)

    (define (找你的日子) (+ met-day 等几天))

    ;; ── 晚宴 ────────────────────────────────────────
    (define (on-banquet-talk!)
      (if (= stage 0)
          (begin
            (set! stage 1)
            (set! met-day world-day)
            (journal 'add! "晚宴的吧台上，你陪沃尔特·芬奇喝到了最后。他说过两天来找你。"))
          #f))

    ;; ── 大堂里那一拍 ────────────────────────────────
    (define (托付!)
      (set! stage 2)
      (journal 'add! "他在酒店大堂把码头那桩理赔交给了你。")
      (play-dialogue!
        (line "世界" "大堂靠窗的位子上，一个穿着整齐的男人朝你抬了抬手。晚宴那晚他就坐在吧台尽头。")
        (line "沃尔特" "沃尔特·芬奇。我替保险公司跑外勤，那晚说过的。")
        (line "尼尔" "你说意外不会先敲门。")
        (line "沃尔特" "保单至少会在事后出现。所以我来了。")
        (line "沃尔特" "码头有个人申请伤残理赔。表格很完整，他的伤却干净得让我起疑。")
        (line "沃尔特" "我需要一个不是保险公司的人去看一眼。你愿意替我看看吗？"))
      (spotlight! "核赔"
        "沃尔特把码头那桩疑似骗保的案子交给了你。陪他去核一次赔，报告怎么写由你定。"))

    (define (lobby-arrivals)
      (if (and (= stage 1) (>= world-day (找你的日子)))
          (list (arrival "沃尔特在大堂" 托付!))
          '()))

    ;; ── 核赔 ────────────────────────────────────────
    (define (on-claim-result result)
      (if (or (equal? result 'confirmed) (equal? result 'mercy))
          (begin
            (set! stage 3)
            (set! claim-result (if (equal? result 'confirmed) "坐实" "放水"))
            (journal 'add!
              (if (equal? result 'confirmed)
                  "报告坐实了骗保。沃尔特在表格上打了勾，之后的外勤他先想到你。"
                  "报告留了一处空白。沃尔特没有追问，之后的外勤他也不派重的给你。"))
            (complete-task! "核赔")
            (play-remote-dialogue!
              (line "沃尔特"
                (if (equal? result 'confirmed)
                    "你看见一份表格太干净，会知道该往下查。这种人我用得着。"
                    "你知道有些数字对不上，也知道什么时候不把人逼到底。这种人我也用得着，只是不派重活。"))
              (line "沃尔特" "以后有外勤，我在大堂留话。")
              (line "沃尔特" "另外，货运公司那边有个代理人，我把你的名字给他了。"))
            (notify! "沃尔特留下货运代理的地址。货运公司开放。"))
          #f))

    ;; ── 舱单 ────────────────────────────────────────
    ;; 核赔不是一场免费的交锋：先进场的是时间或情报，交锋只是收尾。
    ;; 两条路都落在码头账房（船位记录本来就在他手里）：
    ;;   翻记录  花骰子，一颗一格往上推，好结果两格；
    ;;   拿情报换 账房认货不认人，一份情报当场换走，不占骰。
    (define (manifest-done!)
      (if manifest?
          #f
          (begin
            (set! manifest? #t)
            (journal 'add! "那批货的舱单到手了。拿着它再去找沃尔特。")
            (spotlight! "舱单到手"
              "那批货的舱单在你手里了。回格兰德酒店找沃尔特，陪他去核这次赔。"))))

    (define (node-search-records)
      (工作 "翻账房的记录" '低 'knowledge
        (outcome (lambda ()
            (manifest-clk 'advance! 2)
            (if (manifest-clk 'full?) (manifest-done!) #f)))
        (outcome (lambda ()
            (manifest-clk 'advance! 1)
            (if (manifest-clk 'full?) (manifest-done!) #f)))
        (outcome (lambda () (spend-composure! 1)))
        "一页一页翻那批货的进出记录"
        :anchor "码头-账房"
        :clocks (list (manifest-clk 'render-data))))

    (define (node-trade-tip)
      (node "拿情报跟他换"
        :anchor "码头-账房"
        :subtitle "账房倒卖船期消息，认货不认人；一份情报换舱单"
        :requires (list (req-item "情报" 1))
        :resolve (instant
          (outcome (lambda () (manifest-done!))))))

    (define (manifest-nodes)
      ;; 同一分区共用一个锚点：两张卡都挂账房，不包容器。
      (if (and (= stage 2) (not manifest?))
          (list (node-search-records) (node-trade-tip))
          '()))

    (define (node-claim)
      (node "陪沃尔特核赔"
        :anchor "格兰德酒店-大厅"
        :subtitle (if manifest? identity "先拿到那批货的舱单")
        :disabled (not manifest?)
        :tags (list "交锋")
        :resolve (instant (lambda () (start-encounter "核赔" on-claim-result)))))

    ;; ── 外勤：核赔之后的稳定活 ──────────────────────
    ;; 报酬夹在码头搬运和「替客人解围」之间：比码头高一档，但不是上城的价。
    ;; 坐实那条失手扣冷静；放水那条活轻，失手只是白跑。
    (define (node-fieldwork)
      (if (equal? claim-result "坐实")
          (工作 "替沃尔特跑外勤" '低 'sharpness
            (outcome (lambda () (add-item! "金钱" 30)))
            (outcome (lambda () (add-item! "金钱" 20)))
            (outcome (lambda () (spend-composure! 1)))
            "他派给你的都是表格太干净的那种"
            :anchor "格兰德酒店-大厅")
          (工作 "替沃尔特跑外勤" '低 'sharpness
            (outcome (lambda () (add-item! "金钱" 22)))
            (outcome (lambda () (add-item! "金钱" 15)))
            (outcome (lambda () #f))
            "他派给你的都是不会出事的那种"
            :anchor "格兰德酒店-大厅")))

    (define (nodes-at location)
      (cond
        ((equal? location "格兰德酒店")
         (cond
           ((= stage 2) (list (node-claim)))
           ((= stage 3) (list (node-fieldwork)))
           (else '())))
        ((equal? location "码头")
         (manifest-nodes))
        (else '())))

    (define (arrivals-at location)
      (if (equal? location "格兰德酒店") (lobby-arrivals) '()))

    ;; ── 卷宗 ────────────────────────────────────────
    (define (steps)
      (list (step "在酒店大堂听他说那桩理赔" (>= stage 2))
            (step "拿到那批货的舱单" manifest?)
            (step "陪沃尔特核赔" (>= stage 3))))

    (define (dossier-entry)
      (cond
        ((or (= stage 1) (= stage 2))
         (list (dossier "核赔"
                 :kind '人物 :status '进行中
                 :now ""
                 :where "格兰德酒店"
                 :steps (steps)
                 :log (journal 'render-data))))
        ((= stage 3)
         (list (dossier "核赔" :kind '人物 :status '了结
                 :now "" :where ""
                 :steps (steps)
                 :log (journal 'render-data))))
        (else '())))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'on-banquet-talk!) (on-banquet-talk!))
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((equal? msg 'dossier) (dossier-entry))
          ((equal? msg 'known?) (>= stage 1))
          ((equal? msg 'can-arrange-berth?) (= stage 3))
          ((equal? msg 'claim-result) claim-result)
           ;; 调试台 / 试跑：跳过晚宴、大堂与找舱单，直接站到核赔门口进场。
           ((equal? msg 'debug-enter!)
            (if (= stage 0) (begin (set! stage 1) (set! met-day world-day)) #f)
            (if (= stage 1) (托付!) #f)
            (set! manifest? #t)
            (start-encounter "核赔" on-claim-result))
           ((equal? msg 'save)
            (list (list "stage" stage)
                  (list "met-day" met-day)
                  (list "manifest?" (if manifest? 1 0))
                  (list "manifest-progress" (manifest-clk 'save))
                 (list "claim-result" claim-result)
                 (list "journal" (journal 'save))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
              (set! stage (assoc-get data "stage" 0))
              (set! met-day (assoc-get data "met-day" 0))
              (set! manifest? (= (assoc-get data "manifest?" 0) 1))
              (manifest-clk 'load! (assoc-get data "manifest-progress" 0))
             (set! claim-result (assoc-get data "claim-result" "无"))
             (journal 'load! (assoc-get data "journal" '()))))
          (else #f))))))
