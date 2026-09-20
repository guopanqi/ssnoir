;; 码头——普通生计由地点拥有；弗兰克拥有自己的个人生活。
;;
;; 这里放着城市生活里两种不同形状的活：
;;   搬运      ——常驻日结。Phase B 起每天只能做一次。
;;   替乔顶班——只有乔提前说过的第二天才出现；不结钱，换来互相收班的交情。
;;
;; 乔的第一版只验证三拍：他记住你的名字 → 他替你收过一班 → 你替他收一班。
;; 没有可见关系数值，不往主线和人物命运写条件，也不因错过顶班而倒退。

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

    ;; ── 乔：在重复工作中熟起来 ─────────────
    ;; 阶段不是好感度，只是码头里能看见的社会事实：
    ;;   0 陌生——他是另一个来等点工的人
    ;;   1 脸熟——他叫得出尼尔的名字，倒霉时会伸手
    ;;   2 自己人——你们都替对方收过一班
    ;; 只数“不同日子”，同一天连搬几班不会更快混熟。
    (define joe-stage 0)
    (define haul-days 0)
    (define last-haul-day 0)
    (define joe-saved-shift? #f)
    (define joe-saved-day 0)
    ;; none / scheduled / helped / missed。scheduled 只活到 cover-day 当天结束。
    (define cover-state "none")
    (define cover-day 0)

    (define (joe-note-text)
      (cond
        ((= joe-stage 0) "点完工就去货堆，收工总是走得很急。")
        ((= joe-stage 1) "递绳子时，他已经会叫你的名字。")
        ((= joe-stage 2) "有事时，你们会替对方收完一班。")
        (else (error "码头·乔：未知关系阶段"))))

    (define (node-joe)
      ;; 乔还没有独立人形锚点；先站在岸口点工棚。
      (node "乔"
        :anchor "码头-岸口"
        :resolve (note "乔" (joe-note-text))))

    ;; 返回这是不是本日第一班。第三个不同工作日他才开始叫名字。
    (define (record-haul-day!)
      (if (= last-haul-day world-day)
          #f
          (begin
            (set! last-haul-day world-day)
            (set! haul-days (+ haul-days 1))
            (if (and (= joe-stage 0) (>= haul-days 3))
                (begin
                  (set! joe-stage 1)
                  (play-banter!
                    (line "乔" "尼尔，下一趟跟我走。那边的绳没受潮。")
                    (line "尼尔" "你什么时候记住我名字的？")
                    (line "乔" "点工的喊得够响。")))
                #f)
            #t)))

    (define (schedule-cover-if-ready! new-haul-day?)
      ;; 乔替你收班后至少隔一个日历日再开口，免得那次帮忙像当场交易。
      (if (and new-haul-day? joe-saved-shift?
               (> world-day joe-saved-day)
               (equal? cover-state "none"))
          (begin
            (set! cover-day (+ world-day 1))
            (set! cover-state "scheduled")
            (play-dialogue!
              (line "乔" "我明天下午得早走。孩子学校六点锁门。")
              (line "乔" "你要是来，替我接后半班。两点前。")
              (line "尼尔" "过了两点呢？")
              (line "乔" "工头会找别人。")))
          #f))

    (define (finish-haul-common!)
      (set! haul-day world-day)
      (let ((new-haul-day? (record-haul-day!)))
        (schedule-cover-if-ready! new-haul-day?)))

    (define (finish-bad-haul!)
      ;; 第三天刚叫上名字时不紧接着托底；让“脸熟”先单独成立一拍。
      (let ((already-familiar? (>= joe-stage 1)))
        (finish-haul-common!)
        (spend-composure! 2)
        (if (and already-familiar? (not joe-saved-shift?))
            (begin
              (set! joe-saved-shift? #t)
              (set! joe-saved-day world-day)
              (add-item! "金钱" 8)
              (play-banter!
                (line "乔" "手别动。越逞强，明天越抬不起来。")
                (line "尼尔" "后面还有半班。")
                (line "乔" "我收。工头问，就说这班是我们两个卸的。")))
            #f)))

    (define (cover-live?)
      (and (equal? cover-state "scheduled") (= world-day cover-day)))

    (define (node-cover-for-joe)
      (node "替乔顶班"
        :anchor "码头-岸口"
        :subtitle "替他接完后半班；工钱仍记在乔名下"
        :requires (list (req-die))
        :resolve (instant
          (outcome (lambda ()
              (set! cover-state "helped")
              (set! joe-stage 2)
              (result-supplement! "替乔收完后半班")
              (play-banter!
                (line "乔" "点名册上是我的名字。钱也记我账上。")
                (line "尼尔" "我知道。去接孩子吧。")
                (line "乔" "两点以后这边归你。别让工头加第三车。")))))))

    ;; 日历已在世界规则中先向前走一天；此时还没顶的那班已经有别人接手。
    (define-turn-rule "乔的顶班过去了"
      (lambda () (and (equal? cover-state "scheduled") (> world-day cover-day)))
      (lambda () (set! cover-state "missed")))

    ;; ── 常驻日结 ──────────────────────
    (define (node-haul)
      (工作 "搬运" '高 'violence
        (outcome (lambda ()
            (finish-haul-common!)
            (add-item! "金钱" 15)))
        (outcome (lambda ()
            (finish-haul-common!)
            (add-item! "金钱" 8)
            (spend-composure! 1)))
        ;; 高风险由更高报酬与力量检定表达；日常失手仍只扣 2 点冷静。
        (outcome (lambda () (finish-bad-haul!)))
        "扛一班货，挣一晚的钱"
        :anchor "码头-货堆"))

    ;; ── 组装 ─────────────────────────────────────
    (define (livelihood-nodes)
      (append
        (if (and (equal? (第二章 'phase) "B") (= haul-day world-day))
            '()
            (list (node-haul)))
        (if (cover-live?) (list (node-cover-for-joe)) '())))

    (define (children)
      (map anchor-at-dock
        (append
          (livelihood-nodes)
          ;; 顶班当天，岸口这个落点交给行动卡；做完后乔的标注随新快照回来。
          (if (cover-live?) '() (list (node-joe)))
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
             (list "joe-stage" joe-stage)
             (list "haul-days" haul-days)
             (list "last-haul-day" last-haul-day)
             (list "joe-saved-shift?" joe-saved-shift?)
             (list "joe-saved-day" joe-saved-day)
             (list "cover-state" cover-state)
             (list "cover-day" cover-day)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! haul-day (assoc-get data "haul-day" 0))
             (set! joe-stage (assoc-get data "joe-stage" 0))
             (set! haul-days (assoc-get data "haul-days" 0))
             (set! last-haul-day (assoc-get data "last-haul-day" 0))
             (set! joe-saved-shift? (assoc-get data "joe-saved-shift?" #f))
             (set! joe-saved-day (assoc-get data "joe-saved-day" 0))
             (set! cover-state (assoc-get data "cover-state" "none"))
             (set! cover-day (assoc-get data "cover-day" 0))))
          (else #f))))))
