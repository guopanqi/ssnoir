;; scenes/world/码头.scm - 劳工路线
;; 工资、势力关系和老周关系分开：帮朋友意味着少上一班工。

(define dock
  (let ()
    (define laozhou-favor 0)
    (define laozhou-stage 0) ; 0=相识，1=信任，2=支线完成，3=已成为同伴
    (define smuggle-cooldown 0)
    (define cushy-available? #f)

    (define favor-max 6)
    (define trust-threshold 3)
    (define close-threshold 5)
    (define cushy-roll-table (list #t #f #f #f #f))

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
    (define (node-haul)
      (工作 "搬运" "劳工" '高 'violence
        (outcome "工钱丰厚" "扛了一整天货，工钱给得爽快。"
          (lambda () (add-item! "金钱" 15)))
        (outcome "累到脱力" "拿到了工钱，可腰背像散了架。"
          (lambda () (add-item! "金钱" 8) (stress-current-actor! 1)))
        (outcome "砸伤了手" "货箱砸到手上，工头只当没看见。"
          (lambda () (stress-current-actor! 1) (damage-party! 1)))))

    (define (node-foreman-ledger)
      (工作 "替工头记账" "劳工" '中 'knowledge
        (outcome "账目清楚" "账目按时交回，工头给了足额报酬。"
          (lambda () (add-item! "金钱" 10)))
        (outcome "按日结算" "账算清了，拿到普通工钱。"
          (lambda () (add-item! "金钱" 5)))
        (outcome "记错一笔" "一笔账记岔了，只能自己赔上。"
          (lambda () (spend-up-to! "金钱" 5)))))

    (define (node-smuggle)
      (工作 "走私" "劳工" '越界 'sharpness
        (outcome "顺利出港" "货悄无声息地出了港。真正赚多少，要看你能找到什么销路。"
          (lambda ()
            (add-item! "私货" 2)
            (add-item! "情报" 1)))
        (outcome "有惊无险" "只保住了一件货，至少还能找地方脱手。"
          (lambda () (add-item! "私货" 1)))
        (outcome "被巡警撞见" "巡警扣下了货。你虽然脱了身，名字却被记进了值班记录。"
          (lambda ()
            (change-faction-relation! "官僚" -2)
            (stress-current-actor! 2)))))

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
            (lambda () (bump-favor! 2))))))

    (define (finish-laozhou-section!)
      (if (not (= laozhou-stage 1))
          (error "老周支线：非法完成阶段")
          #t)
      (set! laozhou-stage 2)
      (set! laozhou-favor (max laozhou-favor close-threshold))
      (sync-laozhou!)
      (complete-section!)
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

    (define (can-recruit-laozhou?)
      (and (= laozhou-stage 2)
           (or (= laozhou-favor favor-max)
               (equal? (abnormal-invoice-state) "交给老周"))))

    (define (recruit-laozhou!)
      (if (not (can-recruit-laozhou?))
          (error "请老周搭把手：招募条件未满足")
          #t)
      (if (has-companion? 'laozhou)
          (error "请老周搭把手：老周已经在队伍中")
          #t)
      (recruit-companion! 'laozhou "老周"
        (list
          (list 'violence 2)
          (list 'knowledge 3)
          (list 'sharpness 1)
          (list 'social 2)))
      (set! laozhou-stage 3)
      (set! cushy-available? #f)
      (notify! "老周答应跟你一起跑动。从明天起，他每天会多带来一颗行动骰。"))

    (define (node-recruit-laozhou)
      (action "请老周搭把手"
        (list (req-die))
        (instant
          (outcome "老周入队" "老周收起账本，决定以后不只在码头等你的消息。"
            (lambda () (recruit-laozhou!))))))

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

    (define (node-give-invoice)
      (action "把异常货单交给老周"
        (list (req-die))
        (instant
          (outcome "工人有了准备" "老周看完货单，把几个可信的人叫到了一边。"
            (lambda ()
              (deliver-abnormal-invoice! "老周")
              (bump-favor! 2)
              (change-faction-relation! "劳工" 1)
              (sync-laozhou!))))))

    (define (laozhou-description)
      (cond
        ((= laozhou-stage 0) "老周守着账房。想让他信你，得少上一班工，替他查查旧账。")
        ((= laozhou-stage 1) "老周有件不方便自己出面的事，正在等你的答复。")
        ((= laozhou-stage 2)
         (if (can-recruit-laozhou?)
             "老周已经准备好搭把手，只等你正式开口。"
             "老周愿意在码头替你出面。想让他跟着你跑，还得把信任磨满，或者把异常货单交给他。"))
        ((equal? (actor-status 'laozhou) 'away)
         (string-append "老周压力已经到了 " (number->string (actor-stress 'laozhou))
                        "，暂时离队休息。压力归零后才会回来。"))
        (else
         (string-append "老周正在队伍里，当前压力 "
                        (number->string (actor-stress 'laozhou)) "。"))))

    (define (node-laozhou)
      (node "老周"
        :subtitle "码头账房"
        :clocks (list (list 'clock "老周信任" laozhou-favor favor-max 'segments
                            "帮助老周不发工资；达到信任门槛后开启人物小节。"))
        :resolve (observe (laozhou-description))))

    ;; ── 公共事件对码头的持续影响 ──────────────────
    ;; guaranteed? = 探长担保生效（仅失败时）：压下官方风声、免掉压力，但劳工那边仍记账。
    (define (on-public-event result guaranteed?)
      (if (equal? result 'success)
          (begin
            (add-item! "金钱" 20)
            (add-item! "情报" 1)
            (change-faction-relation! "劳工" 1)
            (notify! "你压住了场子，对方暂时退了。"))
          (begin
            (change-faction-relation! "劳工" -1)
            (if guaranteed?
                (notify! "没压住，但探长的担保压下了码头的风声，走私没受影响。")
                (begin
                  (set! smuggle-cooldown 3)
                  (add-actor-stress! 'player 1)
                  (notify! "没压住。码头上起了风声，走私暂时停了。"))))))

    (define-turn-rule "码头风声消退"
      (lambda () (> smuggle-cooldown 0))
      (lambda () (set! smuggle-cooldown (- smuggle-cooldown 1))))

    (define-turn-rule "老周美差"
      (lambda () (= laozhou-stage 2))
      (lambda () (set! cushy-available? (random-choice cushy-roll-table))))

    ;; ── 组装 ──────────────────────────────────────
    (define (dock-clocks)
      (if (> smuggle-cooldown 0)
          (list (list 'clock "码头风声" smuggle-cooldown 3 'countdown
                      "归零后走私工作恢复。"))
          '()))

    (define (dock-children)
      (append
        (list (node-haul))
        (if (relation-at-least? "劳工" '脸熟)
            (append
              (list (node-foreman-ledger))
              (if (< laozhou-stage 3) (list (node-help-laozhou-ledger)) '()))
            '())
        (list (node-laozhou))
        (if (= laozhou-stage 1) (list (node-laozhou-errand)) '())
        (if (can-recruit-laozhou?) (list (node-recruit-laozhou)) '())
        (if cushy-available? (list (node-cushy)) '())
        (if (and (relation-at-least? "劳工" '自己人)
                 (<= smuggle-cooldown 0))
            (list (node-smuggle))
            '())
        (if (> (item-count "私货") 0) (list (node-sell-contraband-locally)) '())
        (if (abnormal-invoice-held?) (list (node-give-invoice)) '())))

    (sync-laozhou!)

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node "码头" :children (dock-children) :clocks (dock-clocks))))
          ((equal? msg 'on-public-event) (on-public-event (cadr args) (caddr args)))
          ((equal? msg 'save)
           (list
             (list "laozhou-favor" laozhou-favor)
             (list "laozhou-stage" laozhou-stage)
             (list "smuggle-cooldown" smuggle-cooldown)
             (list "cushy-available?" cushy-available?)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! laozhou-favor (assoc-get data "laozhou-favor" 0))
             (set! laozhou-stage (assoc-get data "laozhou-stage" 0))
             (set! smuggle-cooldown (assoc-get data "smuggle-cooldown" 0))
             (set! cushy-available? (assoc-get data "cushy-available?" #f))
             (if (and (= laozhou-stage 3) (not (has-companion? 'laozhou)))
                 (error "码头存档错误：老周已入队但队伍中没有老周")
                 #t)
             (if (and (< laozhou-stage 3) (has-companion? 'laozhou))
                 (error "码头存档错误：队伍中有老周但支线尚未招募")
                 #t)
             (sync-laozhou!)))
          ((equal? msg 'debug-favor) (bump-favor! 2))
          ((equal? msg 'debug-cushy) (set! cushy-available? #t))
          ((equal? msg 'debug-clear-smuggle) (set! smuggle-cooldown 0))
          (#t #f))))))
