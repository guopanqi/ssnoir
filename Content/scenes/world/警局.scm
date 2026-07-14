;; scenes/world/警局.scm - 官僚路线
;; 用时间处理文书、陪探长办案，换取具体而有限的程序手段。

(define police-station
  (let ()
    (define detective-stage 0) ; 0=初识，1=走访后待整理，2=人物小节完成
    (define pass-cooldown 0)
    (define pass-cooldown-max 3)          ; 基础冷却
    (define pass-cooldown-trusted-max 1)  ; 官僚·信任：探长愿意更快再签一张
    (define pass-cooldown-current-max pass-cooldown-max) ; 本轮实际用的冷却上限，供时钟显示

    ;; 官僚关系敌视时，文书工作中/坏结果有概率惹出的麻烦；3 天不处理会有代价。
    (define police-trouble
      (make-trouble "警局麻烦" 3
        (lambda ()
          (spend-up-to! "金钱" 10)
          (stress-current-actor! 1)
          (notify! "一张说不清来路的罚单堵在了门口，不出这笔钱事情不算完。"))))

    (define (maybe-notify-police-trouble!)
      (if (maybe-trigger-trouble! police-trouble "官僚" trouble-roll-table)
          (notify! "警局里有人成心跟你过不去，怕是要惹麻烦。")
          #f))

    (define (has-investigation-pass?)
      (> (item-count "办案通行证") 0))

    (define (issue-investigation-pass!)
      (if (has-investigation-pass?)
          (error "办理办案通行证：已持有一张")
          #t)
      (if (> pass-cooldown 0)
          (error "办理办案通行证：仍在冷却中")
          #t)
      (add-item! "办案通行证" 1)
      (set! pass-cooldown-current-max
            (if (relation-at-least? "官僚" '信任) pass-cooldown-trusted-max pass-cooldown-max))
      (set! pass-cooldown pass-cooldown-current-max))

    (define (node-paperwork)
      (关系工作 "整理警局文书" "官僚" '低 'knowledge
        (outcome "条目清楚" "案卷归了类，值班警员难得从鼻子里哼出半句夸奖。"
          (lambda () (add-item! "金钱" 6)))
        (outcome "按时交差" "一下午全耗在发霉的纸堆里，换来几个辛苦钱。"
          (lambda () (add-item! "金钱" 3) (maybe-notify-police-trouble!)))
        (outcome "抄错编号" "编号抄岔了，只能从头返工。"
          (lambda () (stress-current-actor! 1) (maybe-notify-police-trouble!)))))

    (define (node-handle-trouble)
      (action "摆平警局的麻烦"
        (list (req-die))
        (roll 'social (lambda () (关系难度修正 "官僚"))
          (outcome "没压住" "对方不吃这一套，麻烦还在。"
            (lambda () #f))
          (outcome "摆平了" "你把事情按下去了，警局这边算是揭过。"
            (lambda () (police-trouble 'resolve!)))
          (outcome "反倒卖了个好" "你不但按下了事，还顺带落了个人情。"
            (lambda () (police-trouble 'resolve!))))))

    (define (finish-detective-section!)
      (if (not (= detective-stage 1))
          (error "探长支线：非法重复完成")
          #t)
      (set! detective-stage 2)
      (complete-section!)
      (change-faction-relation! "官僚" 2)
      (notify! "探长记住了你。程序上的通行证和延期，他都愿意给。"))

    (define (finish-detective-visit!)
      (if (not (= detective-stage 0))
          (error "陪探长走访：支线阶段错误")
          #t)
      (set! detective-stage 1)
      (notify! "证人的话还没有整理成正式口供，探长请你再帮一次。"))

    (define (node-accompany-detective)
      (define (dock-witness-mods)
        (if (relation-at-least? "劳工" '相识)
            (list (modifier 1 "码头工人认得你"))
            '()))
      (action "陪探长走访"
        (list (req-die))
        (roll 'social dock-witness-mods
          (outcome "碰了一鼻子灰" "证人在码头讨生活，见了警察更不肯开口。"
            (lambda () (stress-current-actor! 1)))
          (outcome "找到证人" "你替探长缓和了气氛，码头上的证人终于愿意开口。"
            (lambda () (finish-detective-visit!)))
          (outcome "问到关键处" "你找准了码头人的说话方式，探长顺势拿到了口供。"
            (lambda () (finish-detective-visit!) (grant-favor-relation! "官僚"))))))

    (define (node-file-statement)
      (action "替探长整理口供"
        (list (req-die))
        (roll 'knowledge
          (outcome "材料退回" "几处说法对不上，材料被退了回来。"
            (lambda () (stress-current-actor! 1)))
          (outcome "口供入档" "你把散乱的话整理成了能进入正式记录的口供。"
            (lambda () (finish-detective-section!)))
          (outcome "留下余地" "口供写得清楚，也替证人避开了不必要的麻烦。"
            (lambda () (finish-detective-section!))))))

    ;; 官僚敌视时，探长不再为你走这两条程序便利（延期/通行证）——
    ;; 二者都只是省事的工具，不是唯一手段，禁了也不会让人卡关；
    ;; 但整理文书这类正经差事仍然照办，敌视要能靠做工挣回来才讲得通。
    (define (police-hostile?)
      (equal? (relation-band "官僚") '敌视))

    (define (node-delay-event)
      (node "请探长延期一天"
        :subtitle (if (police-hostile?)
                      "探长现在不肯为你破例"
                      "")
        :tags (if (police-hostile?) (list "官僚敌视") '())
        :disabled (police-hostile?)
        :requires (list (req-die))
        :resolve (instant
          (outcome "压下一天" "探长打了几个电话，把码头的手续拖后了一天。"
            (lambda () (delay-public-event-one-day!))))))

    (define (pass-status-text)
      (cond
        ((police-hostile?) "探长现在不肯为你签发这张证件。")
        ((has-investigation-pass?) "你已经持有一张；用掉以前不能再办。")
        ((> pass-cooldown 0)
         (string-append "警局还要等 " (number->string pass-cooldown) " 天才能再签发。"))
        ((relation-at-least? "官僚" '信任)
         "探长信得过你，消耗一次行动就能取得一张一次性通行证，冷却也只要 1 天。")
        (else "消耗一次行动，取得一张可在不同场合使用的一次性通行证。")))

    (define (node-investigation-pass)
      (node "办理办案通行证"
        :subtitle (pass-status-text)
        :requires (list (req-die))
        :clocks (if (> pass-cooldown 0)
                    (list (list 'clock "再次签发" pass-cooldown pass-cooldown-current-max 'countdown
                                "归零后可以再次办理；最多持有一张。"))
                    '())
        :disabled (or (police-hostile?) (has-investigation-pass?) (> pass-cooldown 0))
        :resolve (instant
          (outcome "通行证办妥" "凭这张证件，你可以在需要时以协助办案的名义要求通行或调查。"
            (lambda () (issue-investigation-pass!))))))

    (define (detective-description)
      (cond
        ((= detective-stage 0) "探长忙着翻案卷。在官面上挂了号以后，也许能陪他出去走一趟。")
        ((= detective-stage 1) "走访拿到了证词，但还需要有人把它整理成正式口供。")
        (else "探长已经认得你。程序上有余地时，他愿意替你说句话。")))

    (define (police-children)
      (append
        (list (node-paperwork)
              (observe-action "探长" (detective-description)))
        (nightingale 'lead-nodes-at "警局")
        (if (and (= detective-stage 0) (relation-at-least? "官僚" '相识))
            (list (node-accompany-detective))
            '())
        (if (= detective-stage 1) (list (node-file-statement)) '())
        (nightingale 'route-nodes-at "警局")
        (if (and (>= detective-stage 2) (public-event-can-delay?))
            (list (node-delay-event))
            '())
        (if (>= detective-stage 2)
            (list (node-investigation-pass))
            '())
        (if (police-trouble 'active?) (list (node-handle-trouble)) '())))

    (define-turn-rule "办案通行证再次签发"
      (lambda () (> pass-cooldown 0))
      (lambda () (set! pass-cooldown (- pass-cooldown 1))))

    (define-turn-rule "警局麻烦推进"
      (lambda () (police-trouble 'active?))
      (lambda () (police-trouble 'tick!)))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (container-with-clocks "警局" (police-children) (police-trouble 'render-data))))
          ((equal? msg 'save)
           (list (list "detective-stage" detective-stage)
                 (list "pass-cooldown" pass-cooldown)
                 (list "pass-cooldown-current-max" pass-cooldown-current-max)
                 (list "police-trouble" (police-trouble 'save))))
          ((equal? msg 'load!)
           (begin
             (set! detective-stage
                   (assoc-get (cadr args) "detective-stage" 0))
             (set! pass-cooldown
                   (assoc-get (cadr args) "pass-cooldown" 0))
             (set! pass-cooldown-current-max
                   (assoc-get (cadr args) "pass-cooldown-current-max" pass-cooldown-max))
             (police-trouble 'load! (assoc-get (cadr args) "police-trouble" (list #f 0)))))
          ((equal? msg 'debug-finish-section)
           (begin
             (if (= detective-stage 0) (set! detective-stage 1) #f)
             (if (= detective-stage 1) (finish-detective-section!) #f)))
          (#t #f))))))
