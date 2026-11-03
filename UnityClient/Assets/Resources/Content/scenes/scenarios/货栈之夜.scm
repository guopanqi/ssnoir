;; 独立交锋：报道交易OR → 旧约主目标＋可选封存材料 → 实际签付。
;; 两阶段共用四回合，不重发骰池。世界只接收结果，不在场内推进城市日。
(define 罗科 (speaker "货栈罗科" :name "罗科" :appearance '成年男性))
(define 记者 (speaker "货栈记者" :name "记者" :appearance '成年男性))
(define 阶段 1)
(define 已结束? #f)
(define 记者在场? #t)
(define 交易已阻止? #f)
(define 隐私已暴露? #f)
(define 已交付款凭据? #f)
(define 已交恐吓证词? #f)
(define 已作见证? #f)
(define 原先外流? #f)
(define 封存款 0)
(define 争取记者 (make-clock "争取记者" 4 'gauge "完成使记者拒买报道但留场；可作见证，也可能看见私人材料。" :beneficial-direction 'increase))
(define 撤回交易 (make-clock "迫使罗科撤回交易" 4 'gauge "完成使记者离开；隐私较安全，但失去现场第三方。" :beneficial-direction 'increase))
(define 解除关系 (make-clock "终止旧代理关系" 4 'gauge "主要目标。完成后可签字结算；不要求同时完成封存材料。" :beneficial-direction 'increase))
(define 私人材料 (make-clock "封存私人材料" 2 'gauge "可选。完成后交涉准备值加1，并降低结算报价；不是第二个必做目标。" :beneficial-direction 'increase))
(define 碰面余时 (make-clock "碰面余时" 4 'countdown "两阶段共用四回合。回合结束减1；归零未签付则转回城市补救。" :beneficial-direction 'increase))
(define (议定金额) (+ (if 已交付款凭据? 20 40) (if (私人材料 'full?) 0 20)))
(define (直接报价) (if (私人材料 'full?) 40 60))
(define (需付现金 amount) (max 0 (- amount 封存款)))
(define (结果 kind price payer)
  (list kind 交易已阻止? 隐私已暴露? 已交付款凭据? 已交恐吓证词?
    (私人材料 'full?) price payer))
(define (结束! kind price payer)
  (if 已结束? (error "货栈之夜：重复结束") #t)
  (set! 已结束? #t)
  (if (equal? kind "完成")
    (begin
      (if (> 封存款 0) (remove-item! "罗科结算款" 1) #f)
      (if (> 封存款 price)
        (add-item! "金钱" (- 封存款 price)) #f)
      (play-dialogue!
        (line "世界" "罗科在核过的余额、解除旧代理关系与停止纠缠的记录上签了字。收款与离城安排也写进同一份记录。")
        (line 罗科 "账到这里。之后让她的剧院来办。")
        (line "尼尔" "我会核实你离城，再向经理交报告。")))
    (play-dialogue!
      (line "世界" (if 交易已阻止?
        "报道交易已经告吹，这个成果没有消失。但今晚没能落实结算，后面的事要请经理另作安排。"
        "碰面没有谈成。记者带走了材料，尼尔还得回城处理外流与旧约。"))))
  (end-scenario (结果 kind price payer)))
(define (进入旧约!)
  (set! 阶段 2)
  (play-dialogue!
    (line "世界" (if 记者在场? "记者合上公文包，决定不买这个故事，但还留在旁边听。" "罗科撤回了交易，记者收起东西离开。"))
    (line 罗科 "报社不买了，行。她欠的账，你准备怎么算？")
    (line "世界" "罗科拿起索款信里提过的旧信件，把它压在账单下面。这不是一份新合同，却是他还不肯放下的筹码。")
    (line "尼尔" "报道停了，旧账还得结。但私人信件不能拿来继续逼她。")))
(define (推进报道! target n)
  (if (= 阶段 1) #t (error "货栈：报道阶段已结束"))
  (let ((c (if (eq? target '记者) 争取记者 撤回交易)))
    (c 'advance! n)
    (if (c 'full?)
      (begin (set! 交易已阻止? #t) (set! 记者在场? (eq? target '记者)) (进入旧约!)) #f)))
(define (判定推进! target grade)
  (if (eq? grade '坏) (spend-composure! 1)
    (let ((n (if (eq? grade '好) 2 1)))
      (cond ((member? target '(记者 罗科)) (推进报道! target n))
            ((eq? target '解除) (解除关系 'advance! n))
            ((eq? target '封存) (私人材料 'advance! n))
            (else (error "货栈：未知行动目标"))))))
(define (提交凭据! ledger?)
  (if ledger?
    (begin (set! 已交付款凭据? #t)
      (if (= 阶段 1) (推进报道! '记者 2) (解除关系 'advance! 2)))
    (begin (set! 已交恐吓证词? #t)
      (if (= 阶段 1) (推进报道! '罗科 2) (私人材料 'advance! 2))))
  (play-dialogue! (line "尼尔" (if ledger?
    "这笔款有签收，清单却没扣。要写报道、要结账，都得把它算进去。"
    "门卫愿意为恐吓作证。欠款另算，但这些私人材料不能再用来纠缠她。"))))
(define (请记者见证!)
  (if (and (= 阶段 2) 记者在场? (not 已作见证?)) #t (error "货栈：记者当前无法作见证"))
  (set! 已作见证? #t)
  (if (not (私人材料 'full?)) (set! 隐私已暴露? #t) #f)
  (解除关系 'advance! 2)
  (play-dialogue!
    (line "世界" (if (私人材料 'full?)
      "私人信件已经收好，记者只见证结算与停止纠缠的条款。"
      "记者核对条款时，看见了未收起的私人信件。他记下了内容；不买罗科的报道，并不等于替夜莺保守所有秘密。"))))
(define (现金结算! price) (结束! "完成" price "现金"))
(define (代付结算!)
  (if (解除关系 'full?) #t (error "货栈：尚未谈妥代付结算"))
  (结束! "完成" (议定金额) "代付"))
(define (行动 id description skill target help)
  (node id :subtitle description :risk '冒险
     :requires (list (req-die)) :risk-target (list 'composure "player")
    :progress-target (list 'clock (cond ((eq? target '记者) "争取记者") ((eq? target '罗科) "迫使罗科撤回交易") ((eq? target '解除) "终止旧代理关系") (else "封存私人材料")))
    :resolve (roll skill
      (lambda () (if (> help 0) (list (modifier help "已有筹码")) '()))
      (outcome (lambda () (判定推进! target '坏)))
      (outcome (lambda () (判定推进! target '中)))
      (outcome (lambda () (判定推进! target '好))))))
(define (付款卡 id amount)
  (node id :subtitle (string-append "结算" (number->string amount) "美元；扣封存款后付" (number->string (需付现金 amount)) "美元")
    :requires (if (> (需付现金 amount) 0) (list (req-item "金钱" (需付现金 amount))) '())
    :resolve (instant (outcome (lambda () (现金结算! amount))))))
(define-opponent-rule "碰面时间过去"
  (lambda () (not 已结束?))
  (lambda () (碰面余时 'advance! -1)
    (if (碰面余时 'empty?) (结束! "超时" 0 "未支付") #f)))
(define (on-scenario-enter)
  (set! 原先外流? (get-global '罗科资料已外流))
  (set! 封存款 (if (> (item-count "罗科结算款") 0) 40 0))
  (碰面余时 'load! 4)
  (if 原先外流?
    (begin (set! 阶段 2) (set! 记者在场? #f)
      (play-dialogue!
        (line "世界" "小报来人已经离开，今晚不能收回已经流出的资料。罗科仍在货栈，旧账与停止纠缠的条件还没谈妥。")
        (line 罗科 "现在倒肯来算账了？")))
    (play-dialogue!
      (line "世界" "货栈办公室里，罗科把旧合同和来往信件放在桌上，记者正准备买下这个故事。")
      (line "尼尔" "先别收。这笔账和索款信，不是他说的那么一回事。")
      (line 记者 "我可以听。说服我，或者让他自己撤回。"))))
(define (on-scenario-collapse) (collapse-result (结果 "倒下" 0 "未支付")))
(define (get-render-data)
  (container "货栈之夜"
    (append
      (list (note-node "货栈：目标" "目标" (if (= 阶段 1)
        "阻止报道交易：争取记者或迫使罗科撤回，完成任一条即可。"
        "主要目标：谈妥终止旧代理关系。封存私人材料可改善条件，不要求两项都满。"))
        (note-node "货栈：规则" "碰面窗口" "两阶段共用四回合，转折不重发骰；回合末余时减1，归零转回城市补救。坏判定耗1心情。")
        (clock-node "货栈：时间" (碰面余时 'render-data)))
      (if 原先外流?
        (list (note-node "货栈：既有外流" "已发生的后果" "资料已流出，另有20美元补救费从报酬扣除；本场不能倒写成已阻止报道。")) '())
      (if (= 阶段 1)
        (list
          (clock-node "货栈：记者进度" (争取记者 'render-data))
          (clock-node "货栈：撤回进度" (撤回交易 'render-data))
          (行动 "说明报道漏洞" "好2中1；记者拒买后仍留场，可作见证" 'knowledge '记者 0)
          (行动 "要求撤回交易" "好2中1；罗科撤回后记者离开" 'social '罗科 0))
        (append
          (list (clock-node "货栈：旧约进度" (解除关系 'render-data))
            (clock-node "货栈：材料进度" (私人材料 'render-data))
            (note-node "货栈：局面" "眼前的条件"
              (string-append (if 记者在场? "记者仍在；可见证，也会留意私人材料。" "记者已离开，不能作现场见证。")
                "谈妥后结算" (number->string (议定金额)) "美元；也可接受现价" (number->string (直接报价)) "美元。封存款" (number->string 封存款) "美元优先支付。")))
          (if (not (解除关系 'full?))
            (list (行动 "商定解除条款" "好2中1；封存材料、付款凭据各加1准备值" 'social '解除 (+ (if (私人材料 'full?) 1 0) (if 已交付款凭据? 1 0)))) '())
          (if (not (私人材料 'full?))
            (append (list (行动 "核对封存材料" "好2中1；封存后交涉更有利、报价降低" 'knowledge '封存 0))
              (if 已交恐吓证词?
                (list (node "要求交回信件" :subtitle "已提交的恐吓证词派上用场；一枚骰封存2格"
                  :requires (list (req-die)) :resolve (instant (outcome (lambda () (私人材料 'advance! 2)))))) '())) '())
          (if (and 记者在场? (not 已作见证?) (not (解除关系 'full?)))
            (list (node "请记者见证" :subtitle "一枚骰推进旧约2格；未封存的信件会被看见"
              :requires (list (req-die)) :resolve (instant (outcome 请记者见证!)))) '())
          (if 记者在场?
            (list (instant-action "请记者先离开" (lambda () (set! 记者在场? #f)
              (play-dialogue! (line 记者 "好。剩下的账你们自己谈。"))))) '())
          (if (解除关系 'full?)
            (list (付款卡 "签字付清" (议定金额))
              (node "请经理代付" :subtitle (string-append "一枚骰落实见证；从报酬扣余款" (number->string (需付现金 (议定金额))) "美元")
                :requires (list (req-die)) :resolve (instant (outcome 代付结算!))))
            (list (付款卡 "接受当前报价" (直接报价))))))
      (list
        (node "提交付款凭据" :subtitle "老街酒馆可查；推进报道2格或旧约2格，并核减金额"
          :disabled 已交付款凭据?
          :progress-target (list 'clock (if (= 阶段 1) "争取记者" "终止旧代理关系"))
          :requires (list (req-item "罗科付款凭据" 1)) :resolve (instant (outcome (lambda () (提交凭据! #t)))))
        (node "提交恐吓证词" :subtitle "剧院侧门可取；推进撤回2格或封存2格"
          :disabled 已交恐吓证词?
          :progress-target (list 'clock (if (= 阶段 1) "迫使罗科撤回交易" "封存私人材料"))
          :requires (list (req-item "罗科恐吓证词" 1)) :resolve (instant (outcome (lambda () (提交凭据! #f)))))))))
