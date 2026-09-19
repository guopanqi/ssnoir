;; scenes/world/货运公司.scm - 商业圈路线
;; 用时间寻找和改善投资机会，再用本金换取延迟回报；不提供直接战斗能力。

(define freight-company
  (let ()
    ;; 代理、合同和投资都是办公室里的桌面业务。只有已经闹到楼外的麻烦才落在门口；
    ;; 不声明落点的内容一律进办公室，避免新增业务卡悄悄退回网格。
    (define (anchor-at-office node-data)
      (if (member? :anchor node-data)
          node-data
          (append node-data (list :anchor "货运公司-办公室"))))

    (define agent-stage 0) ; 0=初识，1=应酬后待核对，2=项目开放，3=首个项目结算
    (define walter-reference-seen? #f)
    (define project-state "无") ; 无 / 已考察 / 已谈判 / 已投资
    (define project-quality 0)  ; 0=差，1=普通，2=好
    (define investment-clk
      (make-clock "投资结算" 3 'countdown
        "归零后返还本金与收益；项目期间不能重复投资。"))
    (define investment-principal 60)
    (define investment-principal-discount 15) ; 第一笔结清之后，代理人给的本金优惠
    (define agent-identity "撮合货主、船东与投资项目的货运代理")

    (define (investment-principal-due)
      (if (>= agent-stage 3)
          (max 0 (- investment-principal investment-principal-discount))
          investment-principal))

    (define (node-contract-work)
      (工作 "联络货主" '低 'social
        (outcome (lambda () (add-item! "金钱" 10)))
        (outcome (lambda () (add-item! "金钱" 6)))
        (outcome (lambda () (spend-composure! 1)))))

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
          (outcome (lambda () (spend-composure! 2)))
          (outcome (lambda () (finish-agent-dinner!)))
          (outcome (lambda () (finish-agent-dinner!))))))

    (define (node-review-agent-terms)
      (node "核对代理人的条件"
        :subtitle agent-identity
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome (lambda () (spend-composure! 2)))
          (outcome (lambda () (unlock-agent-project!)))
          (outcome (lambda () (unlock-agent-project!))))))

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
          (outcome (lambda () (set-assessed-project! 0)))
          (outcome (lambda () (set-assessed-project! 1)))
          (outcome (lambda () (set-assessed-project! 2))))))

    (define (node-negotiate-project)
      (action "谈投资条件"
        (list (req-die))
        (roll 'social
          (outcome (lambda () (spend-composure! 2)))
          (outcome (lambda ()
              (if (not (equal? project-state "已考察"))
                  (error "谈投资条件：项目状态错误")
                  #t)
              (set! project-state "已谈判")))
          (outcome (lambda ()
              (if (not (equal? project-state "已考察"))
                  (error "谈投资条件：项目状态错误")
                  #t)
              (set! project-quality (min 2 (+ project-quality 1)))
              (set! project-state "已谈判"))))))

    (define (node-invest)
      (node "投入货运项目"
        :subtitle (string-append agent-identity
                    (if (>= agent-stage 3)
                        "；他信得过你，这次本金打了折"
                        ""))
        :requires (list (req-item "金钱" (investment-principal-due)))
        :resolve (instant
          (outcome (lambda ()
              (if (and (not (equal? project-state "已考察"))
                       (not (equal? project-state "已谈判")))
                  (error "投入货运项目：项目尚未考察")
                  #t)
              (set! project-state "已投资")
              (investment-clk 'set! (investment-clk 'max)))))))

    (define (investment-return)
      (cond ((= project-quality 0) 50)
            ((= project-quality 1) 75)
            (else 90)))

    (define-turn-rule "货运投资结算"
      (lambda () (equal? project-state "已投资"))
      (lambda ()
        (investment-clk 'advance! -1)
        (if (investment-clk 'empty?)
            (let ((amount (investment-return)))
              (add-item! "金钱" amount)
              (notify! (string-append "货运项目结算，收回 "
                                      (number->string amount) " 金钱。"))
              (set! project-state "无")
              (set! project-quality 0)
              (investment-clk 'reset!)
              (if (= agent-stage 2)
                  (begin
                    (set! agent-stage 3)
                    (notify! "第一笔货运投资结清，你真正进入了代理人的生意圈。"))
                  #f))
            #f)))

    (define (agent-description)
      (cond
        ((= agent-stage 0) "代理人只和能把合同处理干净的人谈生意。")
        ((= agent-stage 1) "代理人给了你一份项目条件，等你看清里面的风险。")
        ((= agent-stage 2) "代理人愿意让你投第一笔钱。只有结算以后，这段生意才算走完。")
        (else "第一笔项目已经结清。代理人会继续提供同类投资机会。")))

    ;; 核赔的选择不只是一格关系：沃尔特会把你怎么办那份报告
    ;; 原样说给代理人。等玩家下次真走进货运公司时，这个名声才落地。
    (define (walter-reference-arrivals)
      (let ((result (walter 'claim-result)))
        (if (and (not walter-reference-seen?)
                 (member? result (list "坐实" "放水")))
            (list
              (arrival "沃尔特的介绍"
                (lambda ()
                  (set! walter-reference-seen? #t)
                  (play-dialogue!
                    (line "货运代理"
                      (if (equal? result "坐实")
                          "芬奇说，你看见一份表格太干净，会知道该往下查。"
                          "芬奇说，你知道有些数字对不上，也知道什么时候不把人逼到底。"))
                    (line "尼尔" "他还说了什么？")
                    (line "货运代理" "说你能把事办完。对我来说，这就够了。")))))
            '())))

    (define (investment-clocks)
      (if (equal? project-state "已投资")
          (list (investment-clk 'render-data))
          '()))

    (define (company-children)
      (map anchor-at-office
        (append
          (list (node-contract-work)
                (node "货运代理"
                  :subtitle agent-identity
                  :resolve (observe (agent-description))))
          ;; 代理人认的是沃尔特的引荐，不是哪个圈子的名声：这里开门就说明他引荐过了。
          (if (= agent-stage 0)
              (list (node-entertain-agent))
              '())
          (if (= agent-stage 1) (list (node-review-agent-terms)) '())
          (if (and (>= agent-stage 2) (equal? project-state "无"))
              (list (node-assess-project))
              '())
          (if (equal? project-state "已考察")
              (list (node-negotiate-project) (node-invest))
              '())
          (if (equal? project-state "已谈判")
              (list (node-invest))
              '())
          (three-letters 'nodes-at "货运公司"))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "货运公司"
                   :children (company-children)
                   :clocks (investment-clocks)
                   :arrivals (walter-reference-arrivals))))
          ((equal? msg 'save)
           (list
             (list "agent-stage" agent-stage)
             (list "walter-reference-seen?" walter-reference-seen?)
             (list "project-state" project-state)
             (list "project-quality" project-quality)
             (list "investment-days" (investment-clk 'save))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! agent-stage (assoc-get data "agent-stage" 0))
             (set! walter-reference-seen? (assoc-get data "walter-reference-seen?" #f))
             (set! project-state (assoc-get data "project-state" "无"))
             (set! project-quality (assoc-get data "project-quality" 0))
             (investment-clk 'load! (assoc-get data "investment-days" 0))))
          ((equal? msg 'debug-finish-section)
           (begin
             (if (= agent-stage 0) (set! agent-stage 1) #f)
             (if (= agent-stage 1) (unlock-agent-project!) #f)))
          (#t #f))))))
