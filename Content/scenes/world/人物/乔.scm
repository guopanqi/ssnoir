;; 乔（Joe Doyle）——码头搬运工、单亲父亲。
;; 日常只显示名字“乔”；全名只在正式介绍中出现。

(define joe
  (let ()
    ;; 0陌生 / 1熟悉 / 2开口求助 / 3接送孩子 / 4感谢后等待受伤
    ;; 5养伤 / 6痊愈待邀请 / 7残疾待邀请 / 8死亡 / 9已入队
    (define stage 0)
    (define favor-clk
      (make-clock "与乔熟悉起来" 8 'segments
        "继续在码头搬运，让乔慢慢认得你；填满后他会请你帮一件私事。"))
    (define child-clk
      (make-clock "接送孩子" 4 'segments
        "填满后乔会来道谢；这件事一直与主线期限争用行动骰。"))
    (define child-cared-today? #f)
    (define injury-days 0)
    (define injury-duration 4)
    (define injury-started-today? #f)
    (define care-progress 0)
    (define care-heal-target 8)
    (define cared-today? #f)
    ;; 受伤是伪随机：进入等待期后每天掷一次，没中就把第二天的概率抬高一档，
    ;; 命中概率 (2 + injury-pity)/6 → 1/3、1/2、2/3、5/6，第五天必中。
    ;; 纯随机会出现"一整局都没伤"的空转，这条线是主线的一部分，不能全靠运气。
    (define injury-pity 0)
    (define injury-pity-max 4)
    (define identity "码头搬运工，独自抚养孩子")

    (define (familiar-stage?)
      (or (= stage 3) (= stage 4) (= stage 6) (= stage 7) (= stage 9)))

    (define (maybe-haul-banter!)
      (if (random-choice (chance-table 2 6))
          (cond
            ((= stage 1)
             (play-banter!
               (line "乔" (random-choice (list
                 "这批压秤。腰上使不上劲,就用腿。"
                 "工头今天心情好,趁早多搬两趟。"
                 "你手上的茧还嫩。撑过头一个月就好了。")))))
            ((familiar-stage?)
             (play-banter!
               (line "乔" (random-choice (list
                 "今晚回去得给孩子听写。他的字比我的好,随他妈。"
                 "干完这班就收。晚饭凉了再热,就不是那个味了。"
                 "孩子问你是谁。我说,是个顺路的朋友。")))))
            (else #f))
          #f))

    (define (advance-favor! n)
      (if (or (= stage 1) (= stage 2))
          (favor-clk 'advance! n)
          #f)
      (if (and (= stage 1) (favor-clk 'full?))
          (begin
            (set! stage 2)
            (notify! "乔有件家里的事想请你帮忙。"))
          #f))

    (define (on-haul!)
      (begin
        (maybe-haul-banter!)
        (if (= stage 0)
            (begin
              (set! stage 1)
              (favor-clk 'set! 1)
              (notify! "码头的乔开始认得你了。"))
            (advance-favor! 1))))

    (define (can-catch?)
      (and (>= stage 3) (< stage 5)))

    (define (on-haul-neutral!)
      (if (and (can-catch?) (random-choice (chance-table 2 6)))
          (play-banter! (line "乔" "喏,水。别一口闷。"))
          #f))

    (define (on-haul-fail!)
      (on-haul!)
      (if (and (can-catch?) (random-choice (chance-table 3 6)))
          (begin
            (play-banter!
              (line "乔" "手别抽——先歇一会儿。")
              (line "乔" "裂的是箱角,货没事。工头那边我来解释。")))
          #f))

    (define (node-joe-at-dock)
      (node "乔"
        :subtitle identity
        :clocks (if (>= stage 1)
                    (list (favor-clk 'render-data))
                    '())
        :resolve (observe (cond
          ((= stage 0) "一个沉默的搬运工，下工后总是走得很急。")
          ((= stage 1) "你和乔在同一班货上见过许多次，已经能彼此叫出名字。")
          ((= stage 2) "乔几次想开口，又把话咽了回去。")
          ((= stage 3) "乔的下午班排得很满。孩子放学那段路，只能托给你。")
          ((= stage 4) "乔记着你替他照看孩子的情分，日子暂时恢复了原样。")
          ((= stage 5) "乔伤了腿，没有再来码头。")
          ((= stage 6) "乔已经能重新站稳。他说欠你的，总得有机会还。")
          ((= stage 7) "乔活了下来，但那条腿再也使不上从前的力。")
          ((= stage 8) "乔的位置空着。工头第二天就把另一个人的名字写了上去。")
          (else "乔现在跟着你跑动；每天会带来一颗行动骰。")))))

    (define (node-ask-joe-to-join)
      (node "邀请乔一起做事"
        :subtitle identity
        :resolve (instant (lambda ()
          (if (and (not (= stage 6)) (not (= stage 7)))
              (error "邀请乔入队：人物阶段错误")
              #t)
          (let ((disabled? (= stage 7)))
            (recruit-companion! 'joe "乔"
              (list (list 'violence 1) (list 'knowledge 1) (list 'sharpness 1) (list 'social 1)))
            (if disabled?
                (set-actor-permanent-die-penalty! 'joe "残疾" -1)
                #f)
            (set! stage 9)
            (play-dialogue!
              (line "乔" "我没钱还你。可我还有时间，也认得这座城里不少人。")
              (line "主角" "那就从明天开始。"))
            (spotlight! "多一个人" (if disabled?
              "乔加入了队伍。他每天带来一颗行动骰，但残疾会令这颗骰永久 −1。"
              "乔加入了队伍。从明天起，他每天带来一颗行动骰。")))))))

    ;; 养伤期间乔不在码头，他的节点整个挪到居民区。
    (define (dock-nodes)
      (append
        (if (= stage 5) '() (list (node-joe-at-dock)))
        (if (= stage 2) (list (node-share-meal)) '())
        (if (or (= stage 6) (= stage 7)) (list (node-ask-joe-to-join)) '())))

    (define (node-share-meal)
      (node "和乔吃顿饭"
        :subtitle identity
        :resolve (instant (lambda ()
          (if (not (= stage 2)) (error "乔请托：人物阶段错误") #t)
          (set! stage 3)
          (play-dialogue!
            (line "乔" "档案上写的是乔·多伊尔。码头的人只叫我乔。")
            (line "乔" "我这几天都是下午班。孩子放学后没人接，你要是正好有空……")
            (line "主角" "把地址给我。"))
          (spotlight! "接送孩子" "居民区出现了一件每天最多处理一次的生活小事。它会和所有期限争用同一批行动骰。")))))

    (define (finish-childcare!)
      (if (not (child-clk 'full?)) #f
          (begin
            (set! stage 4)
            (add-item! "酒" 3)
            (complete-section!)
            (play-remote-dialogue!
              (line "乔" "没什么拿得出手的。这几瓶酒你留着。")
              (line "乔" "你替我少误了几班工，也替孩子少等了几次空门。"))
            (spotlight! "乔的谢礼" "乔送来三瓶酒。它们也是交锋里能救急的缓冲。"))))

    (define (advance-childcare! n)
      (child-clk 'advance! n)
      (set! child-cared-today? #t)
      (finish-childcare!))

    (define (node-childcare)
      (node "接乔的孩子放学"
        :subtitle (string-append identity "；" (if child-cared-today? "今天已经接过了" "每天最多一次"))
        :tags (list "低风险")
        :disabled child-cared-today?
        :clocks (list (child-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "晚了一步"
            (lambda () (set! child-cared-today? #t) (spend-composure! 1)))
          (outcome "平安送到"
            (lambda () (advance-childcare! 1)))
          (outcome "路上很顺"
            (lambda () (advance-childcare! 2))))))

    ;; 填满即痊愈，不等第四天：满格后继续挂着，只会被"当天不管 -2"把已经满的条子重新掏空。
    ;; n 恒 >= 0：照顾失败只是没有进展（代价是白扔一颗骰子加 1 点冷静），不倒扣进度。
    ;; 进度跌成负数只有一条路——整天完全不管他。
    (define (apply-care! n)
      (set! care-progress (+ care-progress n))
      (set! cared-today? #t)
      (if (>= care-progress care-heal-target) (finish-injury!) #f))

    (define (care-hint)
      (cond
        ((>= care-progress 8) "乔的脸色有了些血色。那条腿也许能保住。")
        ((>= care-progress 1) "他还撑得住，但离真正好起来差得很远。")
        ((>= care-progress -2) "他脸色不太好，这样下去怕是要留下永久损伤。")
        (else "乔越来越少说话。屋里安静得让人不敢久看。")))

    (define (node-care)
      (node "照顾他"
        :subtitle "花一颗行动骰陪他熬过今天"
        :tags (list "低风险")
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "帮不上忙"
            (lambda () (apply-care! 0) (spend-composure! 1)))
          (outcome "陪他熬过一天"
            (lambda () (apply-care! 1)))
          (outcome "照料妥当"
            (lambda () (apply-care! 2))))))

    (define (node-medicate)
      (node "给他用药"
        :subtitle "消耗一份药品，稳定增加照顾进度"
        :tags (list "低风险")
        :requires (list (req-item "药品" 1))
        :resolve (instant
          (outcome "用过药了"
            (lambda () (apply-care! 2))))))

    ;; 养伤期的乔本人是一个节点，照顾与用药是他名下的两个动作。
    ;; 照料进度做成可见时钟：条子钳到 0 以上，跌破 0 的死亡区间由 care-hint 的文字承担。
    (define (node-joe-injured)
      (node "乔"
        :subtitle (string-append identity "；" (care-hint))
        :clocks (list
          (list 'clock "乔的伤势" (- injury-duration injury-days) injury-duration 'segments
                "倒计时；归零那天按照料进度结算。照料进度提前填满则不必等到期满。")
          (list 'clock "照料进度" (max 0 care-progress) care-heal-target 'segments
                "填满即痊愈；期满时不满但不为负是残疾，为负则乔会死。当天完全不管他会倒扣 2。"))
        :children (append
                    (list (node-care))
                    (if (> (item-count "药品") 0) (list (node-medicate)) '()))))

    (define (residential-nodes)
      (append
        (if (= stage 3) (list (node-childcare)) '())
        (if (= stage 5) (list (node-joe-injured)) '())))

    (define (finish-injury!)
      (cond
        ((>= care-progress care-heal-target)
         (set! stage 6)
         (play-dialogue!
           (line "乔" "这条腿还能站住。往后你要跑什么事，叫我。")))
        ((>= care-progress 0)
         (set! stage 7)
         (play-dialogue!
           (line "乔" "命留下了，腿没全留下。往后我走得慢一点，事还是能办。")))
        (else
         (set! stage 8)
         (play-dialogue!
           (line "世界" "第四天清晨，乔没有再醒来。")
           (line "世界" "码头当天就补上了他的班，像补上一行漏写的数字。"))))
      (set! injury-days 0)
      (set! injury-started-today? #f)
      (set! cared-today? #f)
      (complete-section!))

    ;; 造一张长度 total、其中 hits 项为 #t 的概率表，交给 random-choice 均匀抽。
    (define (chance-table hits total)
      (if (= total 0)
          '()
          (cons (> hits 0) (chance-table (- hits 1) (- total 1)))))

    (define (injury-due?)
      (if (random-choice (chance-table (+ 2 injury-pity) 6))
          #t
          (begin
            (set! injury-pity (min injury-pity-max (+ injury-pity 1)))
            #f)))

    (define-turn-rule "乔受伤"
      (lambda () (and (= stage 4) (injury-due?)))
      (lambda ()
        (set! stage 5)
        (set! injury-days injury-duration)
        (set! injury-started-today? #t)
        (set! care-progress 0)
        (set! cared-today? #f)
        (play-dialogue!
          (line "世界" "乔在搬货时伤了腿。工头把他送回居民区，没提工伤，也没提赔偿。")
          (line "乔" "别去找保险公司。他们先算这条腿值多少钱，再决定我值不值得救。"))
        (spotlight! "乔受伤了" "四天内每天都能照顾他，照顾几次取决于你愿意分出多少行动骰。第四天会按实际照顾情况结算。")))

    (define-turn-rule "乔的伤势推进"
      (lambda () (= stage 5))
      (lambda ()
        (if injury-started-today?
            (set! injury-started-today? #f)
            (begin
              (if (not cared-today?) (set! care-progress (- care-progress 2)) #f)
              (set! cared-today? #f)
              (set! injury-days (- injury-days 1))
              (if (<= injury-days 0) (finish-injury!) #f)))))

    (define-turn-rule "乔每日次数重置"
      (lambda () child-cared-today?)
      (lambda () (set! child-cared-today? #f)))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'on-haul!) (on-haul!))
          ((equal? msg 'on-haul-neutral!) (on-haul-neutral!))
          ((equal? msg 'on-haul-fail!) (on-haul-fail!))
          ((equal? msg 'known?) (>= stage 1))
          ((equal? msg 'favor) (favor-clk 'current))
          ((equal? msg 'can-catch?) (can-catch?))
          ((equal? msg 'dock-nodes) (dock-nodes))
          ((equal? msg 'residential-nodes) (residential-nodes))
          ((equal? msg 'residential-unlocked?) (>= stage 3))
          ((equal? msg 'save)
           (list (list "stage" stage) (list "favor" (favor-clk 'save))
                 (list "child-progress" (child-clk 'save))
                 (list "child-cared-today?" child-cared-today?)
                 (list "injury-days" injury-days) (list "care-progress" care-progress)
                 (list "injury-started-today?" injury-started-today?)
                 (list "cared-today?" cared-today?)
                 (list "injury-pity" injury-pity)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! stage (assoc-get data "stage" 0))
             (favor-clk 'load! (assoc-get data "favor" 0))
             (child-clk 'load! (assoc-get data "child-progress" 0))
             (set! child-cared-today? (assoc-get data "child-cared-today?" #f))
             (set! injury-days (assoc-get data "injury-days" 0))
             (set! care-progress (assoc-get data "care-progress" 0))
             (set! injury-started-today? (assoc-get data "injury-started-today?" #f))
             (set! cared-today? (assoc-get data "cared-today?" #f))
             (set! injury-pity (assoc-get data "injury-pity" 0))
             (if (and (= stage 9) (not (has-companion? 'joe)))
                 (error "乔存档错误：已入队但队伍中没有 joe") #t)
             (if (and (not (= stage 9)) (has-companion? 'joe))
                 (error "乔存档错误：队伍中有 joe 但人物阶段不是已入队") #t)))
          ((equal? msg 'debug-favor!) (advance-favor! (cadr args)))
          ((equal? msg 'debug-injure!)
           (set! stage 5)
           (set! injury-days injury-duration)
           (set! injury-started-today? #t))
          (else #f))))))
