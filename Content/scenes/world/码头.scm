;; scenes/world/码头.scm - 劳工路线
;; 工资、势力关系和老周关系分开：帮朋友意味着少上一班工。

(define dock
  (let ()
    (define laozhou-favor 0)
    (define laozhou-stage 0) ; 0=相识，1=信任，2=支线完成，3=已成为同伴
    (define cushy-available? #f)

    (define favor-max 6)
    (define trust-threshold 3)
    (define close-threshold 5)
    (define cushy-roll-table (list #t #f #f #f #f))

    ;; ── 老周养伤（支线终局，取代原先"好感刷满即入队"）────
    ;; 支线完成后某天，老周在搬运中受伤，接下来 injury-duration 天里
    ;; 你每天最多照顾一次；care-progress 只在结算时才揭晓，日常只给模糊的暗示。
    (define laozhou-injured? #f)
    (define laozhou-injury-done? #f) ; 这场养伤只发生一次，无论结果如何
    (define laozhou-recruit-lost? #f)
    (define injury-days 0)
    (define injury-duration 5)
    (define care-progress 0)
    (define cared-today? #f)
    (define injury-trigger-table (list #t #f #f)) ; 支线完成后，约 1/3 概率某天触发

    ;; 劳工关系敌视时，工作中/坏结果有概率惹出的麻烦；3 天不处理会有代价。
    (define dock-trouble
      (make-trouble "码头麻烦" 3
        (lambda ()
          (spend-up-to! "金钱" 10)
          (stress-current-actor! 1)
          (notify! "码头这摊麻烦没压住：路过时被人堵了个正着，钱和精气神一块儿搭了进去。"))))

    ;; 交锋解释器读取镜像状态。老周的援助来自人物支线，不与势力关系混用。
    (define (sync-laozhou!)
      (set-global! 'laozhou-favor laozhou-favor)
      (set-global! 'laozhou-can-help (>= laozhou-stage 2)))

    (define (bump-favor! n)
      (set! laozhou-favor (min favor-max (+ laozhou-favor n)))
      (if (and (= laozhou-stage 0) (>= laozhou-favor trust-threshold))
          (begin
            (set! laozhou-stage 1)
            (notify! "老周开始把你当自己人看，有件私事想请你帮忙。"))
          #f)
      (sync-laozhou!))

    ;; ── 生计工作 ──────────────────────────────────
    (define (maybe-notify-dock-trouble!)
      (if (maybe-trigger-trouble! dock-trouble "劳工" trouble-roll-table)
          (notify! "码头上有双眼睛老黏着你，怕是要生事。")
          #f))

    ;; 搬运是零门槛的起步工作：声望低的时候，好结果顺带混个脸熟（封顶到 work-relation-cap）；
    ;; 过了这一档，搬运就只管赚钱，不再管声望——往上得靠工头解锁的清闲活。
    (define (node-haul)
      (工作 "搬运" "劳工" '高 'violence
        (outcome "工钱丰厚" "扛了一整天货，汗把衬衫贴在背上，工钱倒给得痛快。"
          (lambda () (add-item! "金钱" 15) (grant-work-relation! "劳工")))
        (outcome "累到脱力" "工钱是拿到了，可腰背像散了架子。"
          (lambda () (add-item! "金钱" 8) (stress-current-actor! 1) (maybe-notify-dock-trouble!)))
        (outcome "砸伤了手" "货箱砸在手上，工头扭过头，只当没瞧见。"
          (lambda () (stress-current-actor! 1) (damage-party! 1) (maybe-notify-dock-trouble!)))))

    ;; 记账是搬熟了之后工头才让你碰的清闲活：工钱比搬运低，但换的是工头更信得过你，
    ;; 声望能继续往上走（封顶到 favor-relation-cap，比搬运能到的地方更高）。
    (define (node-foreman-ledger)
      (工作 "替工头记账" "劳工" '中 'knowledge
        (outcome "账目清楚" "账目按时交回，工头给了足额报酬。"
          (lambda () (add-item! "金钱" 10) (grant-favor-relation! "劳工")))
        (outcome "按日结算" "账算清了，拿到普通工钱。"
          (lambda () (add-item! "金钱" 5)))
        (outcome "记错一笔" "一笔账记岔了，只能自己赔上。"
          (lambda () (spend-up-to! "金钱" 5)))))

    (define (node-smuggle)
      (工作 "走私" "劳工" '非法 'sharpness
        (outcome "顺利出港" "货悄无声息地出了港。真正赚多少，要看你能找到什么销路。"
          (lambda ()
            (add-item! "私货" 2)
            (add-item! "情报" 1)))
        (outcome "险些暴露" "只保住了一件货。你绕了很远才甩掉巡警，整路神经紧绷。"
          (lambda ()
            (add-item! "私货" 1)
            (stress-current-actor! 1)))
        (outcome "被巡警撞见" "巡警扣下了货。你虽然脱了身，名字却被记进了值班记录。"
          (lambda ()
            (change-faction-relation! "官僚" -2)
            (stress-current-actor! 2)))
        "事败将得罪官僚"))

    (define (node-handle-trouble)
      (action "摆平码头的麻烦"
        (list (req-die))
        (roll 'social (lambda () (关系难度修正 "劳工"))
          (outcome "没压住" "对方不吃这一套，麻烦还在。"
            (lambda () #f))
          (outcome "摆平了" "你把事情按下去了，码头这边算是揭过。"
            (lambda () (dock-trouble 'resolve!)))
          (outcome "反倒卖了个好" "你不但按下了事，还顺带落了个人情。"
            (lambda () (dock-trouble 'resolve!))))))

    (define (node-sell-contraband-locally)
      (node "把私货散卖给水手"
        :subtitle "熟识的水手愿意零散收货，只是价钱压得很低"
        :requires (list (req-item "私货" 1))
        :resolve (instant
          (outcome "私货脱手" "水手把货塞进外套，留下了一小叠钱。"
            (lambda () (add-item! "金钱" 12))))))

    ;; ── 老周人物支线 ──────────────────────────────
    (define (node-help-laozhou-ledger)
      (action "帮老周查账"
        (list (req-die))
        (roll 'knowledge
          (outcome "没理出头绪" "你白耗了一下午，账页还是乱成一团。"
            (lambda () (stress-current-actor! 1)))
          (outcome "查清一笔" "你替老周理清了一笔旧账。没有工钱，但他记下了。"
            (lambda () (bump-favor! 1)))
          (outcome "找到漏洞" "你指出账里的漏洞，老周第一次认真打量了你。"
            (lambda () (bump-favor! 2) (grant-favor-relation! "劳工"))))))

    (define (finish-laozhou-section!)
      (if (not (= laozhou-stage 1))
          (error "老周支线：非法完成阶段")
          #t)
      (set! laozhou-stage 2)
      (set! laozhou-favor (max laozhou-favor close-threshold))
      (sync-laozhou!)
      (complete-section!)
      (change-faction-relation! "劳工" 2)
      (notify! "你替老周把话送到了。他从此愿意在码头冲突里替你出面。"))

    (define (node-laozhou-errand)
      (action "替老周跑一趟"
        (list (req-die))
        (roll 'social
          (outcome "没把话带到" "对方不肯见你，这一趟白跑了。"
            (lambda () (stress-current-actor! 1)))
          (outcome "事情办妥" "你把话带到，也替老周保住了一个人的脸面。"
            (lambda () (finish-laozhou-section!)))
          (outcome "两边都满意" "事情办得干净，老周不再只把你当临时帮工。"
              (lambda () (finish-laozhou-section!))))))

    ;; ── 老周养伤：支线的真正终局 ──────────────────
    (define (apply-care! delta)
      (set! care-progress (+ care-progress delta))
      (set! cared-today? #t))

    (define (node-tend-laozhou)
      (node "陪着老周"
        :subtitle "他伤着，身边没人照应；陪他说说话，总比一个人硬扛强"
        :disabled cared-today?
        :requires (list (req-die))
        :resolve
          (roll 'social
            (outcome "越帮越乱" "你手脚生疏，一不留神碰洒了药水。他没说什么，脸色却更难看了。"
              (lambda () (apply-care! -1)))
            (outcome "陪他说说话" "你陪他坐了半晌，说些不着边际的话哄他分神。他精神好了些。"
              (lambda () (apply-care! 1)))
            (outcome "哄他好好养" "你连哄带劝，总算让他按医嘱老老实实躺平，没再硬撑着起身。"
              (lambda () (apply-care! 2))))))

    (define (node-medicate-laozhou)
      (node "给老周用药"
        :subtitle "拿一份药品给他；比陪着他更管用，但这药你自己也用得上"
        :disabled cared-today?
        :requires (list (req-item "药品" 1))
        :resolve (instant
          (outcome "上了药" "药抹上去，他闷哼一声，总算能睡踏实。"
            (lambda () (apply-care! 2))))))

    (define (recruit-laozhou-recovered!)
      (recruit-companion! 'laozhou "老周"
        (list (list 'violence 1) (list 'knowledge 2) (list 'sharpness 0) (list 'social 1)))
      (set! laozhou-stage 3)
      (set! cushy-available? #f)
      (change-faction-relation! "劳工" 1)
      (notify! "老周答应跟你一起跑动。从明天起，他每天会多带来一颗行动骰。"))

    (define (recruit-laozhou-injured!)
      (recruit-companion! 'laozhou "老周"
        (list (list 'violence 0) (list 'knowledge 2) (list 'sharpness 0) (list 'social 2)))
      (set! laozhou-stage 3)
      (set! cushy-available? #f)
      (notify! "老周答应跟你一起跑动——腿脚不如从前，嘴皮子和脑子却比谁都灵。从明天起，他每天会多带来一颗行动骰。"))

    (define (finish-laozhou-injury!)
      (set! laozhou-injured? #f)
      (set! laozhou-injury-done? #t)
      (cond
        ((>= care-progress 6)
         (play-dialogue!
           (line "世界" "老周下床走了两步，站得挺稳。")
           (line "老周" "……欠你一条命。往后你说话，我认。")
           (line "老周" "总算没求着那些开保单的——这条命是你陪出来的，不是他们卖给我的。"))
         (recruit-laozhou-recovered!))
        ((>= care-progress 0)
         (play-dialogue!
           (line "世界" "老周下床走了两步，那条腿明显还是不利索，往后怕是扛不动整包的货了。")
           (line "老周" "废了半条腿，好赖是留住了。往后跑腿、递话，你只管吩咐。")
           (line "老周" "比把这条命抵给诊所换一张账单强。"))
         (recruit-laozhou-injured!))
        (else
         (play-dialogue!
           (line "世界" "老周这条腿最终还是没保住。医生动手的那晚，他没让任何人进屋。")
           (line "老周" "……这码头，我是真上不去了。你的好意，我心领了。")
           (line "老周" "早知道这样，去诊所也未必留得住——他们眼里，这条腿从来就不是命，是一笔账。"))
         (set! laozhou-recruit-lost? #t)
         (notify! "老周不会再回到码头跑动了。这份交情还在，但他不会再跟你出生入死。"))))

    (define (injury-hint-text)
      (cond
        ((>= care-progress 4) "他这两天精神好多了，看着是要缓过来了。")
        ((>= care-progress 1) "他还撑得住，但看得出没那么容易好。")
        ((>= care-progress -2) "他脸色不太好，这样下去怕是要留病根。")
        (else "他情况不太妙，再不管就要出大事了。")))

    ;; 支线完成后偶尔出现的低风险美差。它是关系回报，不再推进支线。
    (define (node-cushy)
      (node "帮老周带个话"
        :subtitle "老周留给你的美差，仅限今天"
        :tags (list "工作" "低风险")
        :clocks (list (list 'clock "转瞬即逝" 1 1 'countdown
                            "只在今天有效；结束一天后会消失。"))
        :requires (list (req-die))
        :resolve
          (roll 'social
            (outcome "扑了个空" "人没找着，白跑一趟。"
              (lambda () (set! cushy-available? #f) (stress-current-actor! 1)))
            (outcome "办妥了" "话带到了，拿到了辛苦钱。"
              (lambda () (set! cushy-available? #f) (add-item! "金钱" 10)))
            (outcome "顺带的好处" "事情办得漂亮，老周多塞了些报酬。"
              (lambda () (set! cushy-available? #f) (add-item! "金钱" 15))))))

    (define (laozhou-description)
      (cond
        ((= laozhou-stage 0) "老周守着账房。想让他信你，得少上一班工，替他查查旧账。")
        ((= laozhou-stage 1) "老周有件不方便自己出面的事，正在等你的答复。")
        (laozhou-injured? (injury-hint-text))
        (laozhou-recruit-lost? "老周还在码头，见面照旧打招呼，但那条腿废了，再也没提过跟你跑的事。")
        ((= laozhou-stage 2) "老周愿意在码头替你出面，日子照常过着。")
        ((equal? (actor-status 'laozhou) 'away)
         (string-append "老周压力已经到了 " (number->string (actor-stress 'laozhou))
                        "，暂时离队休息。压力归零后才会回来。"))
        (else
         (string-append "老周正在队伍里，当前压力 "
                        (number->string (actor-stress 'laozhou)) "。"))))

    (define (node-laozhou)
      (node "老周"
        :subtitle "码头账房"
        :clocks (if laozhou-injured?
                    (list (list 'clock "老周养伤" (- injury-duration injury-days) injury-duration 'segments
                                "这几天他好不好，全看你上不上心；照顾方式和结果只有到头才会揭晓。"))
                    (list (list 'clock "老周信任" laozhou-favor favor-max 'segments
                                "帮助老周不发工资；达到信任门槛后开启人物小节。")))
        :resolve (observe (laozhou-description))))

    (define-turn-rule "老周受伤"
      (lambda ()
        (and (= laozhou-stage 2) (not laozhou-injured?) (not laozhou-injury-done?)
             (random-choice injury-trigger-table)))
      (lambda ()
        (set! laozhou-injured? #t)
        (set! injury-days injury-duration)
        (set! care-progress 0)
        (set! cared-today? #f)
        (set! cushy-available? #f)
        (play-dialogue!
          (line "世界" "老周在搬货时闪了腰，一头栽在货堆边，半天没爬起来。")
          (line "老周" "……没事，歇两天就好。")
          (line "世界" "他嘴上这么说，脸却白得像纸。旁边没一个能搭把手的人。")
          (line "主角" "要不要去诊所看看？")
          (line "老周" "诊所？那地方认保单，不认人。")
          (line "老周" "开保险的跟开诊所的穿一条裤子——你没保单，他们连门都不让你进。")
          (line "老周" "有保单，他们就照着保单上的价钱往死里治，一张账单能顶我大半年工钱。")
          (line "老周" "咱们扛活的，谁保得起那玩意儿。伤着了，就是自己扛，或者求朋友帮衬一把。")
          (line "老周" "你要是得空……过来看一眼也行。"))
        (spotlight! "老周伤着了" "他这一跤摔得不轻，身边又没人。往后几天，他好不好全看你上不上心。")))

    (define-turn-rule "老周养伤推进"
      (lambda () laozhou-injured?)
      (lambda ()
        (if (not cared-today?)
            (set! care-progress (- care-progress 2))
            #f)
        (set! cared-today? #f)
        (set! injury-days (- injury-days 1))
        (if (<= injury-days 0)
            (finish-laozhou-injury!)
            #f)))

    (define-turn-rule "老周美差"
      (lambda () (and (= laozhou-stage 2) (not laozhou-injured?)))
      (lambda () (set! cushy-available? (random-choice cushy-roll-table))))

    (define-turn-rule "码头麻烦推进"
      (lambda () (dock-trouble 'active?))
      (lambda () (dock-trouble 'tick!)))

    ;; ── 组装 ──────────────────────────────────────
    (define (dock-children)
      (append
        (list (node-haul))
        (nightingale 'lead-nodes-at "码头")
        (if (relation-at-least? "劳工" '相识)
            (append
              (list (node-foreman-ledger))
              (if (and (< laozhou-stage 3) (not laozhou-injured?))
                  (list (node-help-laozhou-ledger))
                  '()))
            '())
        (nightingale 'route-nodes-at "码头")
        (list (node-laozhou))
        (if (= laozhou-stage 1) (list (node-laozhou-errand)) '())
        (if laozhou-injured?
            (append
              (list (node-tend-laozhou))
              (if (> (item-count "药品") 0) (list (node-medicate-laozhou)) '()))
            '())
        (if cushy-available? (list (node-cushy)) '())
        (if (relation-at-least? "劳工" '信任)
            (list (node-smuggle))
            '())
        (if (> (item-count "私货") 0) (list (node-sell-contraband-locally)) '())
        (if (dock-trouble 'active?) (list (node-handle-trouble)) '())))

    (sync-laozhou!)

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node "码头" :children (dock-children) :clocks (dock-trouble 'render-data))))
          ((equal? msg 'save)
           (list
             (list "laozhou-favor" laozhou-favor)
             (list "laozhou-stage" laozhou-stage)
             (list "cushy-available?" cushy-available?)
             (list "laozhou-injured?" laozhou-injured?)
             (list "laozhou-injury-done?" laozhou-injury-done?)
             (list "laozhou-recruit-lost?" laozhou-recruit-lost?)
             (list "injury-days" injury-days)
             (list "care-progress" care-progress)
             (list "cared-today?" cared-today?)
             (list "dock-trouble" (dock-trouble 'save))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! laozhou-favor (assoc-get data "laozhou-favor" 0))
             (set! laozhou-stage (assoc-get data "laozhou-stage" 0))
             (set! cushy-available? (assoc-get data "cushy-available?" #f))
             (set! laozhou-injured? (assoc-get data "laozhou-injured?" #f))
             (set! laozhou-injury-done? (assoc-get data "laozhou-injury-done?" #f))
             (set! laozhou-recruit-lost? (assoc-get data "laozhou-recruit-lost?" #f))
             (set! injury-days (assoc-get data "injury-days" 0))
             (set! care-progress (assoc-get data "care-progress" 0))
             (set! cared-today? (assoc-get data "cared-today?" #f))
             (dock-trouble 'load! (assoc-get data "dock-trouble" (list #f 0)))
             (if (and (= laozhou-stage 3) (not (has-companion? 'laozhou)))
                 (error "码头存档错误：老周已入队但队伍中没有老周")
                 #t)
             (if (and (< laozhou-stage 3) (has-companion? 'laozhou))
                 (error "码头存档错误：队伍中有老周但支线尚未招募")
                 #t)
             (sync-laozhou!)))
          ((equal? msg 'debug-favor) (bump-favor! 2))
          ((equal? msg 'debug-cushy) (set! cushy-available? #t))
          ((equal? msg 'debug-trigger-injury!)
           (begin
             (set! laozhou-stage (max laozhou-stage 2))
             (set! laozhou-injured? #t)
             (set! injury-days injury-duration)
             (set! care-progress 0)
             (set! cared-today? #f)))
          ((equal? msg 'debug-set-care!) (set! care-progress (cadr args)))
          ((equal? msg 'debug-finish-injury!) (finish-laozhou-injury!))
          (#t #f))))))
