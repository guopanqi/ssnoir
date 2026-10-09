;; R54 · 追击较强战线：不引入叙事，观察对手按可见进度反应所致的分诊。
;; 两条替代终点：甲到3得1，乙到5得2，先完成任一项即结束。
;; 第2/4手结束后，若尚未完成，对手从当前进度较高的一条扣2（平手甲）。
;; 真目标、收益、入场信息与对手行动规则全部公开；没有隐藏情报。
(define 已结束? #f)
(define 已行动 0)
(define 甲钟 (make-clock "甲" 3 'gauge "到3立即兑现成果1；当前进度公开。"))
(define 乙钟 (make-clock "乙" 5 'gauge "到5立即兑现成果2；当前进度公开。"))
(define 回击轮次 (make-clock "本轮行动数" 4 'gauge "每使用两颗骰子、若尚未结束，对手攻击进度较高的一项；平手攻击甲。"))

(define (结束! status reward text)
  (if 已结束? (error "R54：重复结束") #t)
  (set! 已结束? #t)
  (spotlight! "追击领先目标" text)
  (end-encounter (list status reward (甲钟 'current) (乙钟 'current))))

(define (敌方反应!)
  (if (>= (甲钟 'current) (乙钟 'current))
      (begin
        (甲钟 'advance! -2)
        (result-supplement! "敌方反击甲：原进度较高或持平，扣除最多2。"))
      (begin
        (乙钟 'advance! -2)
        (result-supplement! "敌方反击乙：原进度较高，扣除最多2。"))))

(define (行动! clk n)
  (if 已结束? (error "R54：结束后行动") #t)
  (clk 'advance! n)
  (set! 已行动 (+ 已行动 1))
  (回击轮次 'advance! 1)
  (cond
    ((甲钟 'full?) (结束! '甲 1 "甲先完成，收益1。"))
    ((乙钟 'full?) (结束! '乙 2 "乙先完成，收益2。"))
    ((or (= 已行动 2) (= 已行动 4)) (敌方反应!))
    (else #f)))

(define (目标卡 name clk)
  (node (string-append "推进" name)
    :subtitle "坏0、中1、好2；第2/4手对手将扣领先目标2格（平手甲）。"
    :clocks (list (clk 'render-data))
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome (lambda () (行动! clk 0)))
      (outcome (lambda () (行动! clk 1)))
      (outcome (lambda () (行动! clk 2))))))

(define-opponent-rule "用完四骰的期限"
  (lambda () (not 已结束?))
  (lambda () (结束! 'timeout 0 "本轮到期，没有达成任何替代目标；收益0。")))

(define (on-encounter-enter)
  (play-dialogue!
    (line "规则" "只有一个目标必须完成：甲到3得1，乙到5得2。先完成任何一条就结束。")
    (line "规则" "第2和第4次行动后，若尚未达标，对手选择当前进度更高的目标扣2；平手攻击甲。")
    (line "规则" "玩家可以分散、集中或以一条线吸引对手，进度始终可见；一轮用完则到期。")))

(define (on-encounter-collapse)
  (collapse-result (list 'collapse 0 (甲钟 'current) (乙钟 'current))))

(define (get-render-data)
  (container "追击较强战线"
    (append
      (list (note-node "标注：敌方规则" "对手追击当前领先项目"
        (string-append "已用" (number->string 已行动)
          "／4颗骰，2与4手后扣领先进度2；进度持平打甲。")))
      (clock-nodes (甲钟 'render-data) (乙钟 'render-data) (回击轮次 'render-data))
      (list (目标卡 "甲" 甲钟) (目标卡 "乙" 乙钟)))))
