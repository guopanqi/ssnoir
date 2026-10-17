;; 码头——普通生计由地点拥有；弗兰克、艾迪拥有自己的个人生活。
;;
;; 这里只有一种常驻工作：搬运。每次消耗一颗骰子，没有每日次数上限。
;;
;; 码头上的人不靠「熟悉度」显形，靠**搭档**：每班活结算时才抽今天跟谁一组——
;; 陌生人、乔、艾迪。抽在骰子落地之后，玩家扔骰前不知道是谁，所以搭档不是
;; 一个可以拿来分配骰子的修正值，而是那一晚发生的事的一部分。
;;
;; 每个搭档的签名永远一样：只改结果的后果，不碰判定；数字不随剧情变，
;; 变的只有台词。玩家是在一次次结果里认出这个人的，不是被告知的。
;;   陌生人  什么也不改；偶尔听见一句码头上的话
;;   乔      坏日子不伤人：他接了重的那头
;;   艾迪    好日子更好：他非要加第三车
;; 搭档不给阶段、不给关系：艾迪跟你搭一百次也不会因此变熟，他的线走自己的日历。
;; 乔唯一「动」的一次是第一次抽到他——那是介绍，之后每次都是签名本身。

(define dock
  (let ()
    ;; 码头按分区落卡（锚点在 city-box/prefabs/src/码头.py）：
    ;;   码头          泊位桥头——船边的事：抢修、不开的船、机器上岸
    ;;   码头-货堆     作业面上的货岛——扛包的活、在货堆那头的人
    ;;   码头-岸口     木栅门和点工棚——等活、顶班、巡警
    ;;   码头-账房     后街的账房——船位记录
    ;;   码头-巷口     平台后面两间小屋之间的缝——去货栈后面
    ;;   码头-三号货栈 北端工棚的院子——机器那头的告示、试运行、培训
    ;;   勒索信 / 勒索信-报摊  南端邮箱一角——踩点、投信
    ;; 没声明落点的卡收回桥头，不掉进网格；已声明的保留自己的落点。
    (define dock-anchor "码头")

    (define (anchor-at-dock node-data)
      (if (member? :anchor node-data)
          node-data
          (append node-data (list :anchor dock-anchor))))

    ;; ── 工作状态 ───────────────────────────
    (define opening-talk-heard? #f)
    ;; 仅在勒索信筹备中开放；每次搬运附加推进一格，完成后停止。
    (define worker-familiarity (make-clock "工友之间的消息" 6 'gauge
      "搬运时听工友聊天；满六格后可以歇下来聊聊。"))
    (define worker-asked? #f)
    (define exploration (make-clock "熟悉码头" 4 'gauge "2格发现搬运，4格找到信上的邮箱。"))
    (define mailbox-viewed? #f)
    (define worker-joined? #f)
    (define worker-shift (make-clock "帮艾迪调班" 4 'gauge
      "这班活替他做完，他才有空在取信那晚帮忙。"))
    ;; 搬运每次消耗骰子并正常结算工资；替班也消耗骰子，但不发工资。

    ;; ── 码头回声 ──────────────────────────
    ;; 跟陌生人一组时才可能听见：码头是背景，陌生人是背景里的声音。
    (define (dock-period) (if (第二章 'started?) 2 1))
    (define (echo . lines) (lambda () (apply play-bubble! lines)))
    (define dock-echoes
      (make-echo-pool dock-period
        (list 1
          (echo (line "世界" "点工棚里有人数了两遍人头，第二遍少了一个。")))
        (list 2
          (echo (line "世界" "三号货栈那头传来机器空转的声音，没人往那边看。")))))

    ;; ── 搭档 ──────────────────────────────
    ;; 权重是一张带重复的表。乔从第一天起就在池里，第一次抽到他就算认识；
    ;; 艾迪在码头重逢之后进池，另一只手也压坏了就出池。
    (define joe-met? #f)

    (define (partner-pool)
      (append
        (list "陌生人" "陌生人" "陌生人")
        (list "乔" "乔" "乔")
        (if (and (= (eddie 'stage) 3) (not (eddie 'crushed?)))
            (list "艾迪" "艾迪")
            '())))

    ;; 乔没有独立节点：他的泡泡一律用 :at 钉在岸口点工棚（他在那儿），
    ;; 不走节点名查找、不另立标注卡。
    (define (meet-joe!)
      (set! joe-met? #t)
      (play-bubble!
        (line "乔" "尼尔，下一趟跟我走。那边的绳没受潮。" :at "码头-岸口")
        (line "尼尔" "你什么时候记住我名字的？")
        (line "乔" "点工的喊得够响。" :at "码头-岸口")))

    ;; 一班活的结算走这里：先抽搭档，再按搭档算钱和冷静。钱和冷静的变动
    ;; 引擎自己会列出来，台词只说是谁、干了什么。
    ;; 序章与第一节：好 20 / 中 10；之后恢复搭档工价。
    ;; 基线：好 15 / 中 8、−1 冷静 / 坏 0、−2 冷静。
    (define (stranger-after! grade)
      (if (and (not (equal? grade '好)) (random-choice (list #t #f)))
          (dock-echoes 'try!)
          #f))

    ;; 乔：老手。话少，每句都是干活的分寸；把你当要在这儿干很久的人看，
    ;; 所以他管的是你明天还抬不抬得起胳膊，不是今天多挣几块。
    (define (joe-after! grade)
      (if (not joe-met?) (meet-joe!) #f)
      (cond
        ((equal? grade '好)
         (play-bubble!
           (line "乔" "你今天绳子打对了。" :at "码头-岸口")
           (line "尼尔" "你教的。")
           (line "乔" "教一次不算。明天再打一遍。" :at "码头-岸口")))
        ((equal? grade '中)
         (play-bubble!
           (line "乔" "肩膀换一边。" :at "码头-岸口")
           (line "尼尔" "这边还行。")
           (line "乔" "还行就是快不行了。换。" :at "码头-岸口")))
        (else
         (play-bubble!
           (line "乔" "放下。重的这头我来。" :at "码头-岸口")
           (line "尼尔" "工头看着呢。")
           (line "乔" "他看的是货有没有卸完。没看是谁卸的。" :at "码头-岸口")))))

    ;; 艾迪：拳手。把每一班当一场比赛打，赢了要赢得难看一点才痛快；
    ;; 输了不认，只说明天。他拿你当一起上场的人，不是一起干活的人。
    (define (eddie-after! grade)
      (let ((hand-bad? (eddie 'hand-bad?)))
        (cond
          ((equal? grade '好)
           (play-bubble!
             (line "艾迪" "还有一车。跟不跟？")
             (line "尼尔" "工头没派。")
             (line "艾迪" "派了就不值钱了。")
             (line "世界" (if hand-bad?
                              "他把第三车架在左肩上，右手只是搭着。"
                              "他把第三车架上肩，冲你笑了一下。"))))
          ((equal? grade '中)
           (play-bubble!
             (line "艾迪" "今天这批太轻。")
             (line "尼尔" "轻还不好？")
             (line "艾迪" (if hand-bad?
                              "轻的谁都能扛。我得扛别人扛不了的。"
                              "轻的不算数。"))))
          (else
           (play-bubble!
             (line "世界" "箱子从他那头滑下去。他没去捡，先看了一眼工头。")
             (line "艾迪" "算我的。")
             (line "尼尔" "两个人的班。")
             (line "艾迪" "那就明天两个人扳回来。"))))))

    ;; 签名数字全在这一张表里，一眼能对。每人只动一格：乔只管坏日子，
    ;; 艾迪只管好日子——一个人一个记忆点，别让它们互相稀释。
    ;;            好      中        坏
    ;;   陌生人   15      8 / −1    0 / −2
    ;;   乔       15      8 / −1    0 / 0     他接了重的那头，你一点没伤着
    ;;   艾迪     22      8 / −1    0 / −2    第三车记在你俩名下
    (define (worker-messages-active?)
      (and (= (three-letters 'story-stage) 1)
           (opening-life 'routes-open?)
           (not worker-asked?) (not worker-joined?)
           (not (worker-familiarity 'full?))))

    (define (finish-haul! grade)
      (let ((partner (random-choice (partner-pool))))
        (if (worker-messages-active?)
          (begin
            (worker-familiarity 'tick!)
            (if (worker-familiarity 'full?)
              (play-dialogue!
                (line "艾迪" "伙计们，歇会儿吧。到那边抽根烟。")
                (line "尼尔" "正好，肩膀都抬不动了。")) #f)) #f)
        (cond
          ((equal? grade '好)
           (add-item! "金钱" (if (< (three-letters 'story-stage) 2) 20
               (if (equal? partner "艾迪") 22 15))))
          ((equal? grade '中)
           (add-item! "金钱" (if (< (three-letters 'story-stage) 2) 10 8))
           (spend-composure! 1))
          ;; 高风险由更高报酬与力量检定表达；日常失手仍只扣 2 点冷静。
          ((equal? partner "乔") #f)
          (else (spend-composure! 2)))
        (cond
          ((< (three-letters 'story-stage) 2) #f)
          ((not opening-talk-heard?)
           (set! opening-talk-heard? #t)
           (play-bubble!
             (line "乔" "今天也没有案子？" :at "码头-货堆")
             (line "尼尔" "没有。")
             (line "乔" "那就搭把手。房东不收谜底，只收钱。" :at "码头-货堆")))
          ((equal? partner "乔") (joe-after! grade))
          ((equal? partner "艾迪") (eddie-after! grade))
          (else (stranger-after! grade)))))

    ;; ── 常驻日结 ──────────────────────
    (define (node-haul)
      (工作 "搬运" '高 'violence
        (outcome (lambda () (finish-haul! '好)))
        (outcome (lambda () (finish-haul! '中)))
        (outcome (lambda () (finish-haul! '坏)))
        "扛一班货，挣一晚的钱"
        :anchor "码头-货堆"))

    ;; 探索与搬运消息分别持有各自进度；解锁只发生一次。
    (define (explore! n)
      (let ((before (exploration 'current)))
        (exploration 'advance! n)
        (let ((after (exploration 'current)))
          (if (and (< before 2) (>= after 2))
            (play-bubble!
              (line "尼尔" "你好，请问这里……")
              (line "工头" "找活儿的？货堆那边。" :at "码头-货堆")
              (line "尼尔" "这里活儿危险些，钱也来得快。跟他们干几趟，也许能听见点消息。")) #f)
          (if (and (= (three-letters 'story-stage) 1)
                   (< before 4) (>= after 4))
            (play-bubble!
              (line "尼尔" "请问，后街有没有一个邮箱？")
              (line "工友" "报摊旁边，前面右转就是。" :at "勒索信-报摊")
              (line "尼尔" "就是这里。信上说把钱放进这只邮箱。" :at "勒索信")) #f))))
    (define (view-mailbox!)
      (set! mailbox-viewed? #t)
      (play-dialogue!
        (line "世界" "这里就是信上说的地方，他说要把钱装在信封里，放在邮箱里面，粘在内壁顶部。")
        (line "世界" "这里人来人往，离远了就很难看清楚动作。")
        (line "世界" "我还是要么凑钱，要么找找别的门路。")))

    (define (exploration-nodes)
      (append
        (if (exploration 'full?) '()
          (list (node "探索码头" :anchor "码头-货堆"
            :clocks (list (exploration 'render-data))
            :requires (list (req-die))
            :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1)))
              (outcome (lambda () (explore! 1)))
              (outcome (lambda () (explore! 2)))))))
        (if (and (= (three-letters 'story-stage) 1) (exploration 'full?))
          (list
            (if mailbox-viewed?
              (at-anchor "勒索信" (note-node "码头邮箱" "邮箱" "信上约定放钱的地方；信封要粘在内壁顶部。"))
              (anchored-instant-action "查看邮箱" "勒索信" view-mailbox!))) '())))
    (define (chat-with-workers!)
      (set! worker-asked? #t)
      (load-file "scripts/theatre/第一节生活.scm")
      (第一节-艾迪相识))

    (define (worker-ambush-nodes)
      (if (and (= (three-letters 'story-stage) 1) (>= (exploration 'current) 2))
        (append
          (if (worker-messages-active?)
            (list (at-anchor "码头-货堆" (clock-node "标注：工友之间的消息" (worker-familiarity 'render-data)))) '())
          ;; 满格后保留入口，直到玩家主动交谈；不会因换日而消失。
          (if (and (worker-familiarity 'full?) (not worker-asked?))
            (list (anchored-instant-action "和他们聊聊" "码头-货堆" chat-with-workers!)) '())
          (if (and worker-asked? (not worker-joined?))
            (list (node "帮艾迪调班" :anchor "码头-货堆"
              :subtitle "不领工资；做完后艾迪当晚会到场"
              :clocks (list (worker-shift 'render-data))
              :requires (list (req-die))
              :resolve (roll 'violence
                (outcome (lambda () (spend-composure! 1)))
                (outcome (lambda () (finish-worker-shift! 1)))
                (outcome (lambda () (finish-worker-shift! 2)))))) '())
          (if worker-joined?
            (list (at-anchor "码头-货堆" (note-node "艾迪的约定" "艾迪会到场"
              "踩点找出更多岗位；未踩点也能让他负责观察。"))) '())) '()))
    (define (finish-worker-shift! amount)
      (worker-shift 'advance! amount)
      (if (worker-shift 'full?)
        (begin (set! worker-joined? #t)
          (play-dialogue!
            (line "艾迪" "这批活干完，我就能把那晚的班换开。到时候我跟你去。")
            (line "尼尔" "行，我先去看看位置，再安排。"))) #f))

    ;; ── 组装 ─────────────────────────────────────
    (define (livelihood-nodes)
      (if (>= (exploration 'current) 2) (list (node-haul)) '()))

    (define (children)
      (map anchor-at-dock
        (append
          (exploration-nodes)
          (livelihood-nodes)
          (worker-ambush-nodes)
          (地点节点 "码头"))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "码头"
                        :children (children)
                        :arrivals (地点入场 "码头"))))
          ((equal? msg 'ambush-eddie?) worker-joined?)
          ((equal? msg 'eddie-met?) worker-asked?)
          ((equal? msg 'mailbox-known?) (exploration 'full?))
          ((equal? msg 'save)
           (list
             (list "opening-talk-heard?" opening-talk-heard?)
             (list "exploration" (exploration 'save))
             (list "mailbox-viewed?" mailbox-viewed?)
             (list "worker-familiarity" (worker-familiarity 'save))
             (list "worker-asked?" worker-asked?)
             (list "worker-joined?" worker-joined?)
             (list "worker-shift" (worker-shift 'save))
             (list "joe-met?" joe-met?)
             (list "echoes" (dock-echoes 'save))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! opening-talk-heard? (assoc-get data "opening-talk-heard?" #f))
             (exploration 'load! (assoc-get data "exploration" 0))
             (set! mailbox-viewed? (assoc-get data "mailbox-viewed?" #f))
             (worker-familiarity 'load! (assoc-get data "worker-familiarity" 0))
             (set! worker-asked? (assoc-get data "worker-asked?" #f))
             (set! worker-joined? (assoc-get data "worker-joined?" #f))
             (worker-shift 'load! (assoc-get data "worker-shift" 0))
             ;; 旧档：乔叫得出名字（joe-stage ≥ 1）就算认识。
             (set! joe-met? (assoc-get data "joe-met?"
                              (>= (assoc-get data "joe-stage" 0) 1)))
             (dock-echoes 'load! (assoc-get data "echoes" '()))))
          (else #f))))))
