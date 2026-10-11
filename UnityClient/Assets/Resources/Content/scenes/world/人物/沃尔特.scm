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
;;
;; ── 裙带 ──
;; 外勤做得越多，他越把你当自己人；他把你当自己人，钱就会从别的名目流到你手里。
;; 这条线是上城的「关系掩盖能力」：外勤卡本身一个字不动——骰子、三档、钱都照旧——
;; 好处全是**结算之后**另起的一段：一句他的话，然后一笔别的名目的钱。玩家看得出
;; 那笔钱不是干活挣的，是关系给的。
;;
;; 推进不靠日子，靠一个玩家看不见的交情数：每趟外勤都算，办得好算得多。
;;   0 用得着你   外勤照常
;;   1 报销费     办砸了也有钱：他签字，公司报销你的「差旅」
;;   2 顾问费     办好了另有一笔「外部顾问费」，他自己抽一份
;;   3 项目       签了协议之后，趟趟都有「项目分配」，怎么骰都一样——能力已经不重要了
;; 阶段 3 要玩家亲手签：一张单独的入口卡，不签就停在 2，他不催。
;; 签了之后你们是利益共同体，他的麻烦就是你的麻烦——那些事留给后面长（见 'accomplice?）。
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
    ;; 裙带：交情是隐性的，不画钟；阶段是它的可见形状。
    (define 交情 0)
    (define 裙带 0)                ; 0 用得着你 / 1 报销费 / 2 顾问费 / 3 项目
    (define 报销费 12)
    (define 顾问费 15)
    (define 顾问费-他的份 5)
    (define 项目费 20)
    (define 项目费-他的份 8)
    (define 门槛-报销 4)
    (define 门槛-顾问 10)
    (define 门槛-项目 16)
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
            (complete-task! "陪沃尔特验货")
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
    ;; 三档 outcome 里只有这份活本身；裙带的那一段在 (裙带-之后! 档) 里另起。
    (define (node-fieldwork)
      (if (equal? claim-result "坐实")
          (工作 "替沃尔特跑外勤" '低 'sharpness
            (outcome (lambda () (add-item! "金钱" 30) (裙带-之后! '好)))
            (outcome (lambda () (add-item! "金钱" 20) (裙带-之后! '中)))
            (outcome (lambda () (spend-composure! 1) (裙带-之后! '坏)))
            "他派给你的都是表格太干净的那种"
            :anchor "格兰德酒店-大厅")
          (工作 "替沃尔特跑外勤" '低 'sharpness
            (outcome (lambda () (add-item! "金钱" 22) (裙带-之后! '好)))
            (outcome (lambda () (add-item! "金钱" 15) (裙带-之后! '中)))
            (outcome (lambda () (裙带-之后! '坏)))
            "他派给你的都是不会出事的那种"
            :anchor "格兰德酒店-大厅")))

    ;; ── 裙带 ────────────────────────────────────────
    (define (放水?) (equal? claim-result "放水"))

    ;; 过阶只在外勤结算之后发生，且一次只过一阶：阶段 3 不在这里，要玩家签。
    (define (升到报销费!)
      (set! 裙带 1)
      (journal 'add! "沃尔特给了你一张空白的报销单。外勤办砸了，钱也照样有。")
      (play-dialogue!
        (line "世界" "他把一张印着公司抬头的单子推过来。金额那一栏是空的。")
        (line "沃尔特" "差旅报销。外勤总有开销，公司不看细目，只看我的签字。")
        (line "尼尔" "我今天没花什么。")
        (line "沃尔特" (if (放水?)
                           "你知道什么该写、什么不该写。这一栏也一样。"
                           "那就写你该花的。办事的人不该自己贴钱。"))
        (line "沃尔特" "填好交给前台就行。别写太整。")))

    (define (升到顾问费!)
      (set! 裙带 2)
      (journal 'add! "公司的调查预算里多了一个「外部顾问」。名字是你的，签字是他的。")
      (play-dialogue!
        (line "世界" "他这次没推单子，只把一张名片翻过来，背面写着一个数字。")
        (line "沃尔特" "公司每季有一笔调查预算，批给外部顾问。我有权批。")
        (line "尼尔" "顾问做什么？")
        (line "沃尔特" "你已经在做了。区别是从今天起它有个名目。")
        (line "尼尔" "你自己呢？")
        (line "沃尔特" (if (放水?)
                           "批的人当然有一份。你不会把这句话写进报告的。"
                           "批的人当然有一份。你办事我放心，这话我说过。"))
        (line "沃尔特" "我们这一行，钱不是挣的，是分的。")))

    (define (签协议!)
      (set! 裙带 3)
      (journal 'add! "你在「外部调查顾问服务协议」上签了字。从此每一趟外勤都是项目。")
      (play-dialogue!
        (line "世界" "三页纸。第三页最下面有一条空线。")
        (line "沃尔特" "外部调查顾问服务协议。你不用读，我读过了。")
        (line "尼尔" "项目是什么？")
        (line "沃尔特" "公司要查的案子。哪些派给你，我来分配。")
        (line "沃尔特" "你会发现，分配比查案重要得多。")
        (line "世界" "他把笔递过来。笔很沉。")
        (line "沃尔特" "签了以后，我的事就是你的事。反过来也一样。")))

    (define (记交情! grade)
      (set! 交情 (+ 交情 (if (equal? grade '好) 2 1)))
      (cond
        ((and (= 裙带 0) (>= 交情 门槛-报销)) (升到报销费!) #t)
        ((and (= 裙带 1) (>= 交情 门槛-顾问)) (升到顾问费!) #t)
        (else #f)))

    ;; 别的名目：每一笔都单独进账，引擎自动行会把它们和外勤本身的钱分开列出来。
    (define (报销!)
      (add-item! "金钱" 报销费)
      (result-supplement! "名目：差旅报销")
      (play-bubble! (line "沃尔特" "单子我签了。事情办没办成，公司不问。")))

    (define (顾问费!)
      (add-item! "金钱" 顾问费)
      (remove-item! "金钱" 顾问费-他的份)
      (result-supplement! "名目：外部顾问费；沃尔特的份已扣")
      (play-bubble! (line "沃尔特" "顾问费到了。我的那份我自己拿了。")))

    (define (项目费!)
      (add-item! "金钱" 项目费)
      (remove-item! "金钱" 项目费-他的份)
      (result-supplement! "名目：项目分配；沃尔特的份已扣"))

    (define (裙带-闲话! grade)
      (if (random-choice (list #t #f))
          (play-bubble!
            (line "沃尔特"
              (cond
                ((= 裙带 1) "报销单记得交。公司月底结。")
                ((= 裙带 2) (if (equal? grade '好) "你办事，我批钱。挺好。" "顾问不用趟趟都对。"))
                (else (if (equal? grade '坏) "项目的事，怎么写都是对的。" "下个项目已经在我桌上了。")))))
          #f))

    ;; 外勤结算之后另起的一段。先看今天有没有别的名目的钱，再记交情、看过不过阶。
    ;; 过阶的对白会盖掉闲话，所以过了阶就不再说闲话。
    (define (裙带-之后! grade)
      (cond
        ((= 裙带 0) #f)
        ((= 裙带 1) (if (equal? grade '坏) (报销!) #f))
        ((= 裙带 2)
         (cond ((equal? grade '好) (顾问费!))
               ((equal? grade '坏) (报销!))
               (else #f)))
        (else
         (项目费!)
         (if (equal? grade '坏) (报销!) #f)))
      (if (记交情! grade) #f (if (> 裙带 0) (裙带-闲话! grade) #f)))

    ;; 阶段 3 的门：单独一张入口卡，不吃骰，签了就回不去。
    (define (node-sign)
      (node "签沃尔特的协议"
        :anchor "格兰德酒店-大厅"
        :subtitle "外部调查顾问服务协议。他说你不用读"
        :tags (list "不可撤销")
        :resolve (instant (outcome (lambda () (签协议!))))))

    ;; 他本人是大堂里站着就看得见的人：banter 的气泡要落在他头上，节点名就得是「沃尔特」。
    (define (node-walter)
      (at-anchor "格兰德酒店-大厅"
       (note-node "沃尔特" "沃尔特"
        (cond
          ((= 裙带 0) "靠窗的位子。他面前摊着表格，抬头看你的时候先看表。")
          ((= 裙带 1) "靠窗的位子。他面前多了一叠公司抬头的空白单子。")
          ((= 裙带 2) "靠窗的位子。侍者不用他叫就把咖啡续上了。")
          (else "靠窗的位子。他看你的时候不再看表了。")))))

    (define (裙带-nodes)
      (append
        (list (node-walter))
        (if (and (= 裙带 2) (>= 交情 门槛-项目))
            (list (node-sign))
            '())))

    (define (nodes-at location)
      (cond
        ((equal? location "格兰德酒店")
         (cond
           ((= stage 2) (list (node-claim)))
           ((= stage 3) (append (list (node-fieldwork)) (裙带-nodes)))
           (else '())))
        ((equal? location "码头")
         (manifest-nodes))
        (else '())))

    (define (arrivals-at location)
      (if (equal? location "格兰德酒店") (lobby-arrivals) '()))

    ;; ── 卷宗 ────────────────────────────────────────
    ;; Now 按进度说下一步：大堂见面 → 码头账房拿舱单 → 带舱单回大堂核赔。
    ;; 拿舱单那一步地点是码头，where 跟着走。
    (define (steps)
      (list (step "在酒店大堂接下码头那桩验货" (>= stage 2))
            (step "去码头账房拿那批货的舱单" manifest?)
            (step "去格兰德酒店大厅陪沃尔特核赔" (>= stage 3))))

    (define (claim-now)
      (cond ((= stage 1)
             (if (>= world-day (找你的日子))
                 "去格兰德酒店大堂见沃尔特"
                 (string-append "第 " (number->string (找你的日子))
                                " 天去格兰德酒店大堂见沃尔特")))
            ((not manifest?) "去码头账房拿那批货的舱单")
            (#t "带舱单去格兰德酒店大厅陪沃尔特核赔")))

    (define (claim-where)
      (if (and (= stage 2) (not manifest?)) "码头" "格兰德酒店"))

    (define (dossier-entry)
      (cond
        ((or (= stage 1) (= stage 2))
         (list (dossier "陪沃尔特验货"
                 :kind '人物 :status '进行中
                 :now (claim-now)
                 :where (claim-where)
                 :steps (steps)
                 :log (journal 'render-data))))
        ((= stage 3)
         (append
           (list (dossier "陪沃尔特验货" :kind '人物 :status '了结
                   :now "" :where ""
                   :steps (steps)
                   :log (journal 'render-data)))
           (if (>= 裙带 1)
               (list (dossier "跑沃尔特的外勤" :kind '人物 :status '进行中
                       :now (cond ((>= 裙带 3) "")
                                  ((and (= 裙带 2) (>= 交情 门槛-项目))
                                   "去大堂签沃尔特的长期协议")
                                  (#t "去大堂替沃尔特跑外勤，交情够了升一级"))
                       :where "格兰德酒店"
                       :steps (list (step "拿到空白报销单" (>= 裙带 1))
                                    (step "去大堂跑外勤攒交情"
                                          (or (>= 裙带 3)
                                              (and (= 裙带 2) (>= 交情 门槛-项目))))
                                    (step "去大堂签长期协议" (>= 裙带 3)))
                       :log (journal 'render-data)))
               '())))
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
          ;; 签了协议：利益共同体。后面长出来的麻烦读这个。
          ((equal? msg 'accomplice?) (= 裙带 3))
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
                 (list "交情" 交情)
                 (list "裙带" 裙带)
                 (list "journal" (journal 'save))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
              (set! stage (assoc-get data "stage" 0))
              (set! met-day (assoc-get data "met-day" 0))
              (set! manifest? (= (assoc-get data "manifest?" 0) 1))
              (manifest-clk 'load! (assoc-get data "manifest-progress" 0))
             (set! claim-result (assoc-get data "claim-result" "无"))
             (set! 交情 (assoc-get data "交情" 0))
             (set! 裙带 (assoc-get data "裙带" 0))
             (if (member? 裙带 (list 0 1 2 3)) #t (error "沃尔特存档错误：裙带阶段非法"))
             (if (and (>= 裙带 1) (< 交情 门槛-报销)) (error "沃尔特存档错误：交情够不上阶段") #t)
             (journal 'load! (assoc-get data "journal" '()))))
          (else #f))))))
