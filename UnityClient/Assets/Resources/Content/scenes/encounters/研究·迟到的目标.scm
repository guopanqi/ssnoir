;; R49：迟到的目标（纯抽象原型）。
;; 四颗行动骰：前两颗可以准备甲／乙／丙；第二次准备后随机指定唯一有效目标。
;; 余下骰子只能推进被指定目标。目标各需4格，所有准备进度保留。
;; 这不是探查：玩家不能花骰子提早得知真目标；判定概率与真目标无关。
(define 结束? #f)
(define 准备数 0)
(define 真目标 #f)
(define 甲钟 (make-clock "甲" 4 'gauge "准备阶段可投入；揭示后若甲是真目标则继续推进。"))
(define 乙钟 (make-clock "乙" 4 'gauge "准备阶段可投入；揭示后若乙是真目标则继续推进。"))
(define 丙钟 (make-clock "丙" 4 'gauge "准备阶段可投入；揭示后若丙是真目标则继续推进。"))
(define 准备钟 (make-clock "已准备行动" 2 'gauge "前两颗骰用于准备；两步后自动揭示目标。"))
(define (目标钟 id)
  (cond ((eq? id '甲) 甲钟) ((eq? id '乙) 乙钟) ((eq? id '丙) 丙钟)
        (else (error "迟到的目标：非法目标"))))
(define (结束! status text)
  (if 结束? (error "迟到的目标：重复结算") #t)
  (set! 结束? #t)
  (spotlight! "迟到的目标" text)
  (end-encounter (list status 真目标 (甲钟 'current) (乙钟 'current) (丙钟 'current))))
(define (检查完成!)
  (if (and 真目标 ((目标钟 真目标) 'full?))
      (结束! 'success "被指定目标已完成。") #f))
(define (准备完成!)
  (if (= 准备数 2)
      (begin
        (set! 真目标 (random-choice (list '甲 '甲 '乙 '丙)))
        (spotlight! "目标揭示" (string-append "唯一有效目标：" (symbol->string 真目标)))
        (检查完成!)) #f))
(define (准备结算! clk gain)
  (if (or 真目标 结束?) (error "迟到的目标：准备阶段已经结束") #t)
  (clk 'advance! gain)
  (set! 准备数 (+ 准备数 1))
  (准备钟 'advance! 1)
  (准备完成!))
(define (推进结算! clk gain)
  (if (or (not 真目标) 结束?) (error "迟到的目标：尚不可推进") #t)
  (clk 'advance! gain)
  (检查完成!))
(define (准备卡 name clk)
  (node (string-append "准备" name)
    :subtitle "坏0，中1，好2；目标尚未确定。"
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome (lambda () (准备结算! clk 0)))
      (outcome (lambda () (准备结算! clk 1)))
      (outcome (lambda () (准备结算! clk 2))))))
(define (推进卡 name clk)
  (node (string-append "推进" name)
    :subtitle "仅被指定目标有效；坏0，中1，好2。"
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome (lambda () (推进结算! clk 0)))
      (outcome (lambda () (推进结算! clk 1)))
      (outcome (lambda () (推进结算! clk 2))))))
(define-opponent-rule "行动窗口到期"
  (lambda () (not 结束?))
  (lambda () (结束! 'timeout "回合已结束，目标未完成。")))
(define (on-encounter-enter)
  (play-dialogue!
    (line "规则" "甲有一半概率被指定，乙与丙各四分之一。三个目标都需4格。")
    (line "规则" "前两次行动用于准备，随后公布唯一有效目标；剩余骰只能投入它。")
    (line "规则" "行动结果坏0、中1、好2；本轮结束仍未达标则失败。")))
(define (on-encounter-collapse)
  (collapse-result (list 'collapse 真目标 (甲钟 'current) (乙钟 'current) (丙钟 'current))))
(define (get-render-data)
  (container "迟到的目标"
    (append
      (list (note-node "标注：规则" "目标：揭示后完成唯一目标"
        (if 真目标
            (string-append "唯一有效目标是" (symbol->string 真目标) "；达到4格立即成功。")
            "先分配两次准备：甲概率1/2，乙丙各1/4；随后揭示，达4格成功。")))
      (clock-nodes (准备钟 'render-data) (甲钟 'render-data) (乙钟 'render-data) (丙钟 'render-data))
      (if 真目标
          (list (推进卡 (symbol->string 真目标) (目标钟 真目标)))
          (list (准备卡 "甲" 甲钟) (准备卡 "乙" 乙钟) (准备卡 "丙" 丙钟))))))
