;; scenes/world/货运公司.scm - 富商路线
;; 用时间寻找和改善投资机会，再用本金换取延迟回报；不提供直接战斗能力。

(define freight-company
  (let ()
    (define agent-stage 0) ; 0=初识，1=应酬后待核对，2=项目开放，3=首个项目结算
    (define project-state "无") ; 无 / 已考察 / 已谈判 / 已投资
    (define project-quality 0)  ; 0=差，1=普通，2=好
    (define investment-days 0)
    (define investment-principal 60)
    (define investment-principal-discount 15) ; 富商·信任：代理人给的本金优惠
    (define agent-identity "撮合货主、船东与投资项目的货运代理")

    (define (investment-principal-due)
      (if (relation-at-least? "富商" '信任)
          (max 0 (- investment-principal investment-principal-discount))
          investment-principal))

    ;; 富商关系敌视时，联络工作中/坏结果有概率惹出的麻烦；3 天不处理会有代价。
    (define company-trouble
      (make-trouble "货运公司麻烦" 3
        (lambda ()
          (spend-up-to! "金钱" 12)
          (spend-composure! 1)
          (notify! "有人在公司门口泼了漆，清理这笔账只能自己出。"))))

    (define (maybe-notify-company-trouble!)
      (if (maybe-trigger-trouble! company-trouble "富商" trouble-roll-table)
          (notify! "货运公司这边有人在使绊子，怕是要惹麻烦。")
          #f))

    (define (node-contract-work)
      (关系工作 "联络货主" "富商" '低 'social
        (outcome "撮合成交" "你摸准双方的口风，把一批货和一条船接到了一起。"
          (lambda () (add-item! "金钱" 10)))
        (outcome "谈成一单" "条件不算漂亮，但双方都肯点头，你拿到一份普通佣金。"
          (lambda () (add-item! "金钱" 6) (maybe-notify-company-trouble!)))
        (outcome "两头落空" "货主和船东都不肯让步，你在两边之间白跑了一天。"
          (lambda () (spend-composure! 1) (maybe-notify-company-trouble!)))))

    (define (node-handle-trouble)
      (action "摆平货运公司的麻烦"
        (list (req-die))
        (roll 'social (lambda () (关系难度修正 "富商"))
          (outcome "没压住" "对方不吃这一套，麻烦还在。"
            (lambda () #f))
          (outcome "摆平了" "你把事情按下去了，这边算是揭过。"
            (lambda () (company-trouble 'resolve!)))
          (outcome "反倒卖了个好" "你不但按下了事，还顺带落了个人情。"
            (lambda () (company-trouble 'resolve!))))))

    (define (unlock-agent-project!)
      (if (not (= agent-stage 1))
          (error "货运代理支线：非法推进阶段")
          #t)
      (set! agent-stage 2)
      (notify! "代理人把你当成了可以谈生意的人，新的投资机会出现了。"))

    (define (finish-agent-dinner!)
      (if (not (= agent-stage 0))
          (error "陪货运代理应酬：支线阶段错误")
          #t)
      (set! agent-stage 1)
      (notify! "代理人给了你一份项目条件。看清其中的缺口，才算真正入场。"))

    (define (node-entertain-agent)
      (node "陪货运代理应酬"
        :subtitle agent-identity
        :requires (list (req-die) (req-item "金钱" 10))
        :resolve (roll 'social
          (outcome "话不投机" "钱花了，桌上的气氛却越来越冷。"
            (lambda () (spend-composure! 1)))
          (outcome "谈到生意" "代理人终于把一项货运周转的机会告诉了你。"
            (lambda () (finish-agent-dinner!)))
          (outcome "条件不错" "你听出了他真正缺的东西，也拿到了更好的开场条件。"
            (lambda () (finish-agent-dinner!) (grant-favor-relation! "富商"))))))

    (define (node-review-agent-terms)
      (node "核对代理人的条件"
        :subtitle agent-identity
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome "没看出问题" "条款绕得太深，你只能先放下。"
            (lambda () (spend-composure! 1)))
          (outcome "看清风险" "你找到了真正需要承担的风险，也知道该怎么谈。"
            (lambda () (unlock-agent-project!)))
          (outcome "抓住缺口" "你指出条款里的缺口，代理人终于把你当作谈判对手。"
            (lambda () (unlock-agent-project!) (grant-favor-relation! "富商"))))))

    (define (set-assessed-project! quality)
      (if (not (equal? project-state "无"))
          (error "考察投资项目：已有进行中的项目")
          #t)
      (set! project-state "已考察")
      (set! project-quality quality))

    (define (node-assess-project)
      (action "考察货运项目"
        (list (req-die))
        (roll 'sharpness
          (outcome "前景不佳" "仓储和回款都有问题，这个项目只能勉强周转。"
            (lambda () (set-assessed-project! 0)))
          (outcome "条件普通" "风险和收益都看得清楚，可以决定是否投入。"
            (lambda () (set-assessed-project! 1)))
          (outcome "找到缺口" "你找到一个能明显改善回报的周转缺口。"
            (lambda () (set-assessed-project! 2))))))

    (define (node-negotiate-project)
      (action "谈投资条件"
        (list (req-die))
        (roll 'social
          (outcome "没谈拢" "代理人不肯松口。时间花了，条件没有变化。"
            (lambda () (spend-composure! 1)))
          (outcome "接受条件" "双方按原来的条件成交，项目可以投入了。"
            (lambda ()
              (if (not (equal? project-state "已考察"))
                  (error "谈投资条件：项目状态错误")
                  #t)
              (set! project-state "已谈判")))
          (outcome "争到让步" "你抓住代理人急着周转的弱点，把回报条件往自己这边推了一步。"
            (lambda ()
              (if (not (equal? project-state "已考察"))
                  (error "谈投资条件：项目状态错误")
                  #t)
              (set! project-quality (min 2 (+ project-quality 1)))
              (set! project-state "已谈判"))))))

    (define (node-invest)
      (node "投入货运项目"
        :subtitle (string-append agent-identity
                    (if (relation-at-least? "富商" '信任)
                        "；他信得过你，这次本金打了折"
                        ""))
        :requires (list (req-item "金钱" (investment-principal-due)))
        :resolve (instant
          (outcome "本金入账" "钱被锁进货运周转里，三天后才知道结果。"
            (lambda ()
              (if (and (not (equal? project-state "已考察"))
                       (not (equal? project-state "已谈判")))
                  (error "投入货运项目：项目尚未考察")
                  #t)
              (set! project-state "已投资")
              (set! investment-days 3))))))

    (define (investment-return)
      (cond ((= project-quality 0) 50)
            ((= project-quality 1) 75)
            (else 90)))

    (define-turn-rule "货运投资结算"
      (lambda () (equal? project-state "已投资"))
      (lambda ()
        (set! investment-days (- investment-days 1))
        (if (<= investment-days 0)
            (let ((amount (investment-return)))
              (add-item! "金钱" amount)
              (notify! (string-append "货运项目结算，收回 "
                                      (number->string amount) " 金钱。"))
              (set! project-state "无")
              (set! project-quality 0)
              (set! investment-days 0)
              (if (= agent-stage 2)
                  (begin
                    (set! agent-stage 3)
                    (complete-section!)
                    (change-faction-relation! "富商" 2)
                    (notify! "第一笔货运投资结清，你真正进入了代理人的生意圈。"))
                  #f))
            #f)))

    (define-turn-rule "货运公司麻烦推进"
      (lambda () (company-trouble 'active?))
      (lambda () (company-trouble 'tick!)))

    ;; 满价要等信任：相识只换来一个还算过得去的渠道价，
    ;; 代理人真正信你之前，不会按最好的价钱收货。
    (define (contraband-sale-price)
      (if (relation-at-least? "富商" '信任) 25 18))

    (define (node-sell-contraband)
      (node "把私货卖给代理人"
        :subtitle (string-append agent-identity "；"
                    (if (relation-at-least? "富商" '信任)
                        "他信得过你，这次按最好的价钱收"
                        "他肯收，但价钱只是过得去——信得过你以后还能再往上走"))
        :requires (list (req-item "私货" 1))
        :resolve (instant
          (outcome "货已收下" "代理人验过货，按约定付了钱。"
            (lambda () (add-item! "金钱" (contraband-sale-price)))))))

    (define (node-find-project-with-intel)
      (node "凭消息找项目"
        :subtitle "用现成消息省去考察，找到一个回报普通但看得清的项目"
        :requires (list (req-item "情报" 1))
        :resolve (instant
          (outcome "项目找到了" "消息指出了一批正在找周转的货，风险和回报都算普通。"
            (lambda () (set-assessed-project! 1))))))

    (define (agent-description)
      (cond
        ((= agent-stage 0) "代理人只和能把合同处理干净的人谈生意。")
        ((= agent-stage 1) "代理人给了你一份项目条件，等你看清里面的风险。")
        ((= agent-stage 2) "代理人愿意让你投第一笔钱。只有结算以后，这段生意才算走完。")
        (else "第一笔项目已经结清。代理人会继续提供同类投资机会。")))

    (define (investment-clocks)
      (if (equal? project-state "已投资")
          (list (list 'clock "投资结算" investment-days 3 'countdown
                      "归零后返还本金与收益；项目期间不能重复投资。"))
          '()))

    (define (company-children)
      (append
        (list (node-contract-work)
              (node "货运代理"
                :subtitle agent-identity
                :resolve (observe (agent-description))))
        (if (and (= agent-stage 0) (relation-at-least? "富商" '相识))
            (list (node-entertain-agent))
            '())
        (if (= agent-stage 1) (list (node-review-agent-terms)) '())
        (if (and (>= agent-stage 2) (equal? project-state "无"))
            (append
              (list (node-assess-project))
              (if (> (item-count "情报") 0) (list (node-find-project-with-intel)) '()))
            '())
        (if (equal? project-state "已考察")
            (list (node-negotiate-project) (node-invest))
            '())
        (if (equal? project-state "已谈判")
            (list (node-invest))
            '())
        (if (and (> (item-count "私货") 0) (relation-at-least? "富商" '相识))
            (list (node-sell-contraband))
            '())
        (three-letters 'nodes-at "货运公司")
        (if (company-trouble 'active?) (list (node-handle-trouble)) '())))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node "货运公司"
                   :children (company-children)
                   :clocks (append (investment-clocks) (company-trouble 'render-data)))))
          ((equal? msg 'save)
           (list
             (list "agent-stage" agent-stage)
             (list "project-state" project-state)
             (list "project-quality" project-quality)
             (list "investment-days" investment-days)
             (list "company-trouble" (company-trouble 'save))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! agent-stage (assoc-get data "agent-stage" 0))
             (set! project-state (assoc-get data "project-state" "无"))
             (set! project-quality (assoc-get data "project-quality" 0))
             (set! investment-days (assoc-get data "investment-days" 0))
             (company-trouble 'load! (assoc-get data "company-trouble" (list #f 0)))))
          ((equal? msg 'debug-finish-section)
           (begin
             (if (= agent-stage 0) (set! agent-stage 1) #f)
             (if (= agent-stage 1) (unlock-agent-project!) #f)))
          (#t #f))))))
