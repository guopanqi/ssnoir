;; 乔（Joe Doyle）——码头搬运工、单亲父亲。
;; 日常只显示名字“乔”；全名只在正式介绍中出现。
;;
;; 人物只拥有关系、伤势、照料与同伴资格。码头坍塌的公共状态和救援结果由 dock-collapse 拥有。

(define joe
  (let ()
    ;; 0陌生 / 1熟悉中 / 2等待请托 / 3接送中 / 4关系已建立
    ;; 5养伤中 / 6痊愈待邀请 / 7残疾待邀请 / 8死亡 / 9已入队 / 10未介入残疾
    (define stage 0)
    (define favor-clk
      (make-clock "与乔熟悉起来" 8 'segments
        "继续在码头搬运，让乔慢慢认得你；填满后他会请你帮一件私事。"))
    (define child-clk
      (make-clock "接送孩子" 4 'segments
        "填满后乔会来道谢；这件事一直与主线期限争用行动骰。"))
    (define child-cared-today? #f)

    (define injury-duration 4)
    (define injury-days 0)
    (define injury-started-today? #f)
    (define injury-grade "无") ; 无 / 严重；坍塌只负责触发，现场结果不改变伤势
    (define injury-location-known? #f)
    (define care-progress 0)
    (define care-heal-target 8)
    (define cared-today? #f)
    (define medicated-today? #f)
    (define identity "码头搬运工，独自抚养孩子")

    (define (required-field data key)
      (let ((value (assoc-get data key 'missing)))
        (if (equal? value 'missing)
            (error (string-append "乔存档错误：缺少 " key))
            value)))

    (define (boolean-value? value)
      (or (equal? value #t) (equal? value #f)))

    (define (known?) (> (favor-clk 'current) 0))
    (define (relationship-established?) (child-clk 'full?))
    (define (premiere-aide-eligible?)
      (or (= stage 6) (= stage 7) (= stage 9)))

    (define (familiar-stage?)
      (and (known?) (not (= stage 0)) (not (= stage 1))))

    (define (maybe-haul-banter!)
      (if (random-choice (list #t #f #f))
          (cond
            ((= stage 1)
             (play-banter!
               (line "乔" (random-choice (list
                 "这批压秤。腰上使不上劲，就用腿。"
                 "工头今天心情好，趁早多搬两趟。"
                 "你手上的茧还嫩。撑过头一个月就好了。")))))
            ((and (familiar-stage?) (<= stage 4))
             (play-banter!
               (line "乔" (random-choice (list
                 "今晚回去得给孩子听写。他的字比我的好，随他妈。"
                 "干完这班就收。晚饭凉了再热，就不是那个味了。"
                 "孩子问你是谁。我说，是个顺路的朋友。")))))
            (else #f))
          #f))

    (define (advance-favor! n)
      (if (= stage 1) (favor-clk 'advance! n) #f)
      (if (and (= stage 1) (favor-clk 'full?))
          (begin
            (set! stage 2)
            (notify! "乔有件家里的事想请你帮忙。"))
          #f))

    (define (on-haul!)
      (cond
        ((= stage 0)
         (set! stage 1)
         (favor-clk 'set! 1)
         (notify! "码头的乔开始认得你了。"))
        ((= stage 1) (advance-favor! 1))
        ((<= stage 4) (maybe-haul-banter!))
        (else #f)))

    (define (can-catch?)
      (and (>= stage 3) (<= stage 4)))

    (define (on-haul-neutral!)
      (if (and (can-catch?) (random-choice (list #t #f #f)))
          (play-banter! (line "乔" "喏，水。别一口闷。"))
          #f))

    (define (on-haul-fail!)
      (on-haul!)
      (if (and (can-catch?) (random-choice (list #t #t #f #f)))
          (play-banter!
            (line "乔" "手别抽——先歇一会儿。")
            (line "乔" "裂的是箱角，货没事。工头那边我来解释。"))
          #f))

    (define (dock-presence-text)
      (cond
        ((= stage 0) "一个沉默的搬运工，下工后总是走得很急。")
        ((= stage 1) "你们在同一班货上见过几次。他已经会在递绳子时叫你的名字。")
        ((= stage 2) "乔几次想开口，又把话咽了回去。")
        ((= stage 3) "乔的下午班排得很满。孩子放学那段路，只能托给你。")
        ((= stage 4) "乔记着你替他照看孩子的情分。临时急货一来，他还是第一个在工头的单子上签了名。")
        (else (error "乔：不该在码头渲染当前人物阶段"))))

    (define (node-joe-at-dock)
      (node "乔"
        :anchor "乔"
        :subtitle identity
        :clocks (if (or (= stage 1) (= stage 2))
                    (list (favor-clk 'render-data))
                    '())
        :resolve (observe (dock-presence-text))))

    (define (node-share-meal)
      (node "和乔吃顿饭"
        :anchor "乔"
        :subtitle identity
        :resolve (instant (lambda ()
          (if (= stage 2) #t (error "乔请托：人物阶段错误"))
          (set! stage 3)
          (play-dialogue!
            (line "乔" "档案上写的是乔·多伊尔。码头的人只叫我乔。")
            (line "乔" "我这几天都是下午班。孩子放学后没人接，你要是正好有空……")
            (line "尼尔" "把地址给我。"))
          (spotlight! "接送孩子"
            "居民区出现了一件每天最多处理一次的生活小事。它会和所有期限争用同一批行动骰。")))))

    (define (finish-childcare!)
      (if (not (child-clk 'full?))
          #f
          (begin
            (if (= stage 3) #t (error "乔接送：完成时人物阶段错误"))
            (set! stage 4)
            (add-item! "酒" 3)
            (complete-section!)
            (play-remote-dialogue!
              (line "乔" "没什么拿得出手的。这几瓶酒你留着。")
              (line "乔" "你替我少误了几班工，也替孩子少等了几次空门。"))
            (spotlight! "乔的谢礼" "乔送来三瓶酒。它们也是交锋里能救急的缓冲。"))))

    (define (advance-childcare! n)
      (if (= stage 3) #t (error "乔接送：人物阶段错误"))
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
          (outcome "一路不太顺"
            (lambda ()
              (spend-composure! 1)
              (advance-childcare! 1)))
          (outcome "平安送到"
            (lambda () (advance-childcare! 1)))
          (outcome "路上很顺"
            (lambda ()
              (restore-actor-composure! 'player 1)
              (advance-childcare! 1))))))

    (define (injury-window-text)
      (if injury-started-today?
          (string-append
            "乔的伤势是" injury-grade "。送回来的当天不计入倒计时；从明天起有四个完整日终，"
            "每天可以照顾一次、用药一次。整天不管，伤势会恶化。")
          (string-append
            "乔的伤势是" injury-grade "。伤势没有停下来等人；还剩 "
            (number->string injury-days)
            " 个完整日终。每天可以照顾一次、用药一次，整天不管会继续恶化。")))

    (define (reveal-injury-location!)
      (if (= stage 5) #t (error "乔的下落：人物不在养伤中"))
      (if injury-location-known? (error "乔的下落：已经问过乔被送去了哪里") #t)
      (set! injury-location-known? #t)
      (result-note! "开放：居民区·乔的住处")
      (play-dialogue!
        (line "尼尔" "乔呢？")
        (line "乔的工友" "从断梁底下拖出来了。腿伤得厉害，送回居民区了。")
        (line "尼尔" "我认得他家那扇门。"))
      (spotlight! "乔被送回家"
        (injury-window-text)))

    (define (node-ask-about-joe)
      (node "乔的工友"
        :anchor "乔"
        :subtitle "他站在乔原来歇脚的位置；问他乔被送去了哪里"
        :resolve (instant
          (outcome "问到了乔的下落"
            (lambda () (reveal-injury-location!))))))

    (define (node-joe-absence)
      (node "最近没看到乔"
        :anchor "乔"
        :subtitle "乔原来歇脚的位置空了下来"
        :resolve (observe
          "工头的排班板上没了乔的名字。有人说他从旧栈桥下活着出来，腿却再也扛不了货。之后，码头上没人见过他。")))

    (define (dock-nodes)
      (cond
        ((and (>= stage 0) (<= stage 4)
              (member? (dock-collapse 'state) (list "未浮现" "可发生")))
         ;; 等待请托（stage 2）时「和乔吃顿饭」取代「乔」观察卡：熟悉度已满，
         ;; 观察卡只剩一句铺垫，而那个节拍正是这顿饭要演的事。
         (if (= stage 2)
             (list (node-share-meal))
             (list (node-joe-at-dock))))
        ((and (= stage 5) (not injury-location-known?))
         (list (node-ask-about-joe)))
        ((= stage 10)
         (list (node-joe-absence)))
        (else '())))

    (define (begin-injury!)
      (if (and (= stage 4) (relationship-established?))
          #t
          (error "乔伤势：只有关系已建立时才能开放私人照料"))
      (set! stage 5)
      (set! injury-days injury-duration)
      (set! injury-started-today? #t)
      (set! injury-grade "严重")
      (set! injury-location-known? #f)
      (set! care-progress 0)
      (set! cared-today? #f)
      (set! medicated-today? #f))

    (define (on-dock-collapse!)
      (if (<= stage 4)
          #t
          (error "乔：码头坍塌试图重复结算人物命运"))
      (if (and (= stage 4) (relationship-established?))
          ;; 已经接过孩子，玩家认得乔家；先在码头问到去向，再开放居民区照料。
          ;; 玩家是否参加公共救援不影响这里：乔已经先被救出，固定以严重伤势进入照料。
          (begin-injury!)
          (begin
            ;; 陌生或尚未完成接送时，事故切断未完成的私人线；第一章固定留下半残疾。
            (set! stage 10)
            (set! injury-grade "无")
            (set! injury-location-known? #f)
            (set! injury-days 0)
            (set! injury-started-today? #f)
            (set! care-progress 0)
            (set! cared-today? #f)
            (set! medicated-today? #f))))

    (define (care-hint)
      (cond
        ((>= care-progress care-heal-target) "乔的脸色有了血色。那条腿保住了。")
        ((>= care-progress 4) "热退了一些。他醒着的时候，已经能跟孩子说几句话。")
        ((>= care-progress 0) "他还撑得住，但那条腿离真正好起来差得很远。")
        ((>= care-progress -2) "他的脸色越来越差。再拖下去，怕是熬不过这几夜。")
        (else "乔越来越少说话。屋里安静得让人不敢久看。")))

    (define (finish-injury! reason)
      (if (= stage 5) #t (error "乔伤势：结算时人物不在养伤中"))
      (cond
        ((>= care-progress care-heal-target)
         (set! stage 6)
         (if (equal? reason '照料完成)
             (play-dialogue!
               (line "乔" "这条腿还能站住。往后你要跑什么事，叫我。"))
             (play-remote-dialogue!
               (line "乔" "这条腿还能站住。往后你要跑什么事，叫我。"))))
        ((>= care-progress 0)
         (set! stage 7)
         (play-remote-dialogue!
           (line "乔" "命留下了，腿没全留下。往后我走得慢一点，事还是能办。")))
        (else
         (set! stage 8)
         (play-dialogue!
           (line "世界" "第四天清晨，乔没有再醒来。")
           (line "世界" "码头当天就补上了他的班，像补上一行漏写的数字。"))))
      (set! injury-days 0)
      (set! injury-started-today? #f)
      (set! cared-today? #f)
      (set! medicated-today? #f)
      (complete-section!))

    (define (apply-care! method n)
      (if (= stage 5) #t (error "乔照料：人物不在养伤中"))
      (if injury-started-today? (error "乔照料：送回当天不计入四天照料窗口") #t)
      (cond
        ((equal? method '照顾)
         (if cared-today? (error "乔照料：今天已经照顾过乔") #t)
         (set! cared-today? #t))
        ((equal? method '用药)
         (if medicated-today? (error "乔照料：今天已经给乔用过药") #t)
         (set! medicated-today? #t))
        (else (error "乔照料：未知维护手段")))
      (set! care-progress (+ care-progress n))
      (if (>= care-progress care-heal-target) (finish-injury! '照料完成) #f))

    (define (node-care)
      (node "照顾乔"
        :subtitle (cond
                    (injury-started-today? "今天刚把他送回家；完整照料从明天开始")
                    (cared-today? "今天已经照顾过他")
                    (else "花一颗行动骰陪他熬过今天；每天一次"))
        :tags (list "低风险")
        :disabled (or injury-started-today? cared-today?)
        :requires (list (req-die))
        :resolve (roll 'social
          (outcome "帮不上忙"
            (lambda () (apply-care! '照顾 0) (spend-composure! 1)))
          (outcome "陪他熬过一天"
            (lambda () (apply-care! '照顾 1)))
          (outcome "照料妥当"
            (lambda () (apply-care! '照顾 2))))))

    (define (node-medicate)
      (node "给乔用药"
        :subtitle (cond
                    (injury-started-today? "今天刚把他送回家；完整照料从明天开始")
                    (medicated-today? "今天已经用过药")
                    (else "消耗一份药品，稳定推进照料；每天一次，可与照顾并用"))
        :tags (list "低风险")
        :disabled (or injury-started-today? medicated-today?)
        :requires (list (req-item "药品" 1))
        :resolve (instant
          (outcome "用过药了"
            (lambda () (apply-care! '用药 2))))))

    (define (node-joe-injured)
      (node "乔"
        :subtitle (string-append identity "；" injury-grade "伤；" (care-hint))
        :clocks (list
          (list 'clock "伤势倒计时" injury-days injury-duration 'countdown
                "送回当天不扣；之后每个完整日终减一，归零时按实际照料进度结算。")
          (list 'clock "照料进度" (max 0 care-progress) care-heal-target 'segments
                "填满即痊愈；期满时不满但不为负是残疾，为负则乔会死。整天不管会倒扣 2。"))
        :children (list (node-care) (node-medicate))))

    (define (node-ask-joe-to-join)
      (node "邀请乔一起做事"
        :subtitle identity
        :resolve (instant (lambda ()
          (if (or (= stage 6) (= stage 7))
              #t
              (error "邀请乔入队：人物阶段错误"))
          (let ((disabled? (= stage 7)))
            (recruit-companion! 'joe "乔"
              (list (list 'violence 1) (list 'knowledge 1) (list 'sharpness 1) (list 'social 1)))
            (if disabled?
                (set-actor-permanent-die-penalty! 'joe "残疾" -1)
                #f)
            (set! stage 9)
            (play-dialogue!
              (line "乔" "我没钱还你。可我还有时间，也认得这座城里不少人。")
              (line "尼尔" "那就从明天开始。"))
            (spotlight! "多一个人" (if disabled?
              "乔加入了队伍。他每天带来一颗行动骰，但残疾会令这颗骰永久 −1。"
              "乔加入了队伍。从明天起，他每天带来一颗行动骰。")))))))

    (define (node-joe-recovered)
      (node "乔"
        :subtitle (if (= stage 6)
                      "腿伤已经痊愈；他在家门口慢慢把重心压回那条腿上"
                      "腿留下永久损伤；他走得慢了，仍能替人办事")
        :children (list (node-ask-joe-to-join))))

    (define (residential-nodes)
      (append
        (if (= stage 3) (list (node-childcare)) '())
        (if (and (= stage 5) injury-location-known?) (list (node-joe-injured)) '())
        (if (or (= stage 6) (= stage 7)) (list (node-joe-recovered)) '())
        (if (and (= stage 8) (relationship-established?) injury-location-known?)
            (list (observe-action "乔空下来的住处"
                    "门一直关着。孩子暂时住在邻居家，码头已经把乔的班补给了另一个人。"))
            '())))

    (define (final-state)
      (cond
        ((= stage 5) "养伤中")
        ((= stage 6) "痊愈待邀请")
        ((= stage 7) "残疾待邀请")
        ((= stage 8) "死亡")
        ((= stage 9) "已入队")
        ((= stage 10) "未介入残疾")
        (else "尚未结算")))

    (define (validate-state!)
      (if (and (number? stage) (>= stage 0) (<= stage 10))
          #t
          (error "乔存档错误：人物阶段非法"))
      (if (member? injury-grade (list "无" "严重"))
          #t
          (error "乔存档错误：初始伤势非法"))
      (if (boolean-value? injury-location-known?)
          #t
          (error "乔存档错误：是否已知养伤地点不是布尔值"))
      (if (number? care-progress) #t (error "乔存档错误：照料进度不是数字"))
      (if (and (number? injury-days) (>= injury-days 0) (<= injury-days injury-duration))
          #t
          (error "乔存档错误：伤势倒计时非法"))
      (cond
        ((= stage 0)
         (if (= (favor-clk 'current) 0) #t (error "乔存档错误：陌生状态已有熟悉进度")))
        ((= stage 1)
         (if (and (> (favor-clk 'current) 0) (not (favor-clk 'full?)))
             #t
             (error "乔存档错误：熟悉中进度非法")))
        ((= stage 2)
         (if (favor-clk 'full?) #t (error "乔存档错误：等待请托但熟悉进度未满")))
        ((= stage 3)
         (if (and (favor-clk 'full?) (not (child-clk 'full?)))
             #t
             (error "乔存档错误：接送中进度非法")))
        ((= stage 4)
         (if (and (relationship-established?) (not injury-location-known?))
             #t
             (error "乔存档错误：关系已建立状态残留事故后住处信息")))
        ((= stage 5)
         (if (and (relationship-established?)
                  (> injury-days 0)
                  (not (equal? injury-grade "无"))
                  (< care-progress care-heal-target))
             #t
             (error "乔存档错误：养伤状态不完整")))
        ((or (= stage 6) (= stage 7) (= stage 8) (= stage 9))
         (if (not (equal? injury-grade "无"))
             #t
             (error "乔存档错误：事故后命运缺少初始伤势")))
        ((= stage 10)
         (if (and (equal? injury-grade "无") (not injury-location-known?))
             #t
             (error "乔存档错误：未介入残疾不应持有私人伤势或住处信息")))
        (else (error "乔存档错误：无法校验人物阶段")))
      (if (and injury-location-known?
               (not (member? stage (list 5 6 7 8 9))))
          (error "乔存档错误：事故前或未介入状态不应知道养伤地点")
          #t)
      (if (and (member? stage (list 6 7 9)) (not injury-location-known?))
          (error "乔存档错误：事故后可行动状态缺少养伤地点记录")
          #t)
      (if (= stage 5)
          #t
          (if (and (= injury-days 0) (not injury-started-today?)
                   (not cared-today?) (not medicated-today?))
              #t
              (error "乔存档错误：非养伤状态残留每日伤势数据")))
      (if (and (= stage 9) (not (has-companion? 'joe)))
          (error "乔存档错误：已入队但队伍中没有 joe") #t)
      (if (and (not (= stage 9)) (has-companion? 'joe))
          (error "乔存档错误：队伍中有 joe 但人物阶段不是已入队") #t))

    ;; 送回当天只清掉“刚开始”标记；之后正好推进四个完整日终。
    (define-turn-rule "乔的伤势推进"
      (lambda () (= stage 5))
      (lambda ()
        (if injury-started-today?
            (set! injury-started-today? #f)
            (begin
              (if (not (or cared-today? medicated-today?))
                  (set! care-progress (- care-progress 2))
                  #f)
              (set! cared-today? #f)
              (set! medicated-today? #f)
              (set! injury-days (- injury-days 1))
              (if (<= injury-days 0) (finish-injury! '到期) #f)))))

    (define-turn-rule "乔接送次数重置"
      (lambda () child-cared-today?)
      (lambda () (set! child-cared-today? #f)))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'on-haul!) (on-haul!))
          ((equal? msg 'on-haul-neutral!) (on-haul-neutral!))
          ((equal? msg 'on-haul-fail!) (on-haul-fail!))
          ((equal? msg 'known?) (known?))
          ((equal? msg 'favor) (favor-clk 'current))
          ((equal? msg 'can-catch?) (can-catch?))
          ((equal? msg 'relationship-established?) (relationship-established?))
          ((equal? msg 'premiere-aide-eligible?) (premiere-aide-eligible?))
          ((equal? msg 'final-state) (final-state))
          ((equal? msg 'dock-nodes) (dock-nodes))
          ((equal? msg 'residential-nodes) (residential-nodes))
          ((equal? msg 'residential-unlocked?)
           (or (= stage 3)
               (and (= stage 5) injury-location-known?)
               (= stage 6) (= stage 7)
               (and (= stage 8) (relationship-established?) injury-location-known?)))
          ((equal? msg 'on-dock-collapse!) (on-dock-collapse!))
          ((equal? msg 'save)
           (list
             (list "stage" stage)
             (list "favor" (favor-clk 'save))
             (list "child-progress" (child-clk 'save))
             (list "child-cared-today?" child-cared-today?)
             (list "injury-days" injury-days)
             (list "injury-started-today?" injury-started-today?)
             (list "injury-grade" injury-grade)
             (list "injury-location-known?" injury-location-known?)
             (list "care-progress" care-progress)
             (list "cared-today?" cared-today?)
             (list "medicated-today?" medicated-today?)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! stage (required-field data "stage"))
             (favor-clk 'load! (required-field data "favor"))
             (child-clk 'load! (required-field data "child-progress"))
             (set! child-cared-today? (required-field data "child-cared-today?"))
             (set! injury-days (required-field data "injury-days"))
             (set! injury-started-today? (required-field data "injury-started-today?"))
             (set! injury-grade (required-field data "injury-grade"))
             (set! injury-location-known? (required-field data "injury-location-known?"))
             (set! care-progress (required-field data "care-progress"))
             (set! cared-today? (required-field data "cared-today?"))
             (set! medicated-today? (required-field data "medicated-today?"))
             (validate-state!)))
          ((equal? msg 'validate!) (validate-state!))
          ((equal? msg 'debug-favor!) (advance-favor! (cadr args)))
          ((equal? msg 'debug-establish!)
           (if (<= stage 4)
               (begin
                 (favor-clk 'set! (favor-clk 'max))
                 (child-clk 'set! (child-clk 'max))
                 (set! stage 4))
               (error "乔调试：事故后不能重新建立关系")))
          (else (error "乔：收到未知消息")))))))
