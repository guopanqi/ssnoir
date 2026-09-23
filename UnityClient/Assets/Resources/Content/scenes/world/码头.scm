;; 码头——普通生计由地点拥有；弗兰克、艾迪拥有自己的个人生活。
;;
;; 这里只有一种活：搬运，常驻日结，Phase B 起每天只能做一次。
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

    ;; ── 每日一次的额度 ──────────────────────
    (define haul-day 0)             ; Phase B 起，普通搬运每天只能做一次

    ;; ── 码头回声 ──────────────────────────
    ;; 跟陌生人一组时才可能听见：码头是背景，陌生人是背景里的声音。
    (define (dock-period) (if (第二章 'started?) 2 1))
    (define (echo . lines) (lambda () (apply play-banter! lines)))
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

    (define (joe-note-text)
      (if joe-met?
          "递绳子时，他会叫你的名字。"
          "点完工就去货堆，收工总是走得很急。"))

    (define (node-joe)
      ;; 乔还没有独立人形锚点；先站在岸口点工棚。
      (node "乔"
        :anchor "码头-岸口"
        :resolve (note "乔" (joe-note-text))))

    (define (meet-joe!)
      (set! joe-met? #t)
      (play-banter!
        (line "乔" "尼尔，下一趟跟我走。那边的绳没受潮。")
        (line "尼尔" "你什么时候记住我名字的？")
        (line "乔" "点工的喊得够响。")))

    ;; 一班活的结算走这里：先抽搭档，再按搭档算钱和冷静。钱和冷静的变动
    ;; 引擎自己会列出来，台词只说是谁、干了什么。
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
         (play-banter!
           (line "乔" "你今天绳子打对了。")
           (line "尼尔" "你教的。")
           (line "乔" "教一次不算。明天再打一遍。")))
        ((equal? grade '中)
         (play-banter!
           (line "乔" "肩膀换一边。")
           (line "尼尔" "这边还行。")
           (line "乔" "还行就是快不行了。换。")))
        (else
         (play-banter!
           (line "乔" "放下。重的这头我来。")
           (line "尼尔" "工头看着呢。")
           (line "乔" "他看的是货有没有卸完。没看是谁卸的。")))))

    ;; 艾迪：拳手。把每一班当一场比赛打，赢了要赢得难看一点才痛快；
    ;; 输了不认，只说明天。他拿你当一起上场的人，不是一起干活的人。
    (define (eddie-after! grade)
      (let ((hand-bad? (eddie 'hand-bad?)))
        (cond
          ((equal? grade '好)
           (play-banter!
             (line "艾迪" "还有一车。跟不跟？")
             (line "尼尔" "工头没派。")
             (line "艾迪" "派了就不值钱了。")
             (line "世界" (if hand-bad?
                              "他把第三车架在左肩上，右手只是搭着。"
                              "他把第三车架上肩，冲你笑了一下。"))))
          ((equal? grade '中)
           (play-banter!
             (line "艾迪" "今天这批太轻。")
             (line "尼尔" "轻还不好？")
             (line "艾迪" (if hand-bad?
                              "轻的谁都能扛。我得扛别人扛不了的。"
                              "轻的不算数。"))))
          (else
           (play-banter!
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
    (define (finish-haul! grade)
      (let ((partner (random-choice (partner-pool))))
        (set! haul-day world-day)
        (cond
          ((equal? grade '好)
           (add-item! "金钱" (if (equal? partner "艾迪") 22 15)))
          ((equal? grade '中)
           (add-item! "金钱" 8)
           (spend-composure! 1))
          ;; 高风险由更高报酬与力量检定表达；日常失手仍只扣 2 点冷静。
          ((equal? partner "乔") #f)
          (else (spend-composure! 2)))
        (cond
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

    ;; ── 组装 ─────────────────────────────────────
    (define (livelihood-nodes)
      (if (and (equal? (第二章 'phase) "B") (= haul-day world-day))
          '()
          (list (node-haul))))

    (define (children)
      (map anchor-at-dock
        (append
          (livelihood-nodes)
          (list (node-joe))
          (地点节点 "码头"))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "码头"
                        :children (children)
                        :arrivals (地点入场 "码头"))))
          ((equal? msg 'save)
           (list
             (list "haul-day" haul-day)
             (list "joe-met?" joe-met?)
             (list "echoes" (dock-echoes 'save))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! haul-day (assoc-get data "haul-day" 0))
             ;; 旧档：乔叫得出名字（joe-stage ≥ 1）就算认识。
             (set! joe-met? (assoc-get data "joe-met?"
                              (>= (assoc-get data "joe-stage" 0) 1)))
             (dock-echoes 'load! (assoc-get data "echoes" '()))))
          (else #f))))))
