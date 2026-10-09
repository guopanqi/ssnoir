;; R50 动作回声：纯抽象交锋。仅上一手选中的目标影响下一手准备值。
;; 甲需3，完成效用1；乙需5，完成效用2；任一完成即结算。
;; 继续同目标 -1 准备值，切换目标 +1；首手0。不存在隐藏状态与剧情。
(define 已结束? #f)
(define 上一目标 #f)
(define 甲钟 (make-clock "甲" 3 'gauge "达到3立即兑现收益1。"))
(define 乙钟 (make-clock "乙" 5 'gauge "达到5立即兑现收益2。"))
(define (回声修正 id)
  (cond ((not 上一目标) 0)
        ((eq? id 上一目标) -1)
        (else 1)))
(define (完成! status value text)
  (if 已结束? (error "动作回声：重复结算") #t)
  (set! 已结束? #t)
  (spotlight! "动作回声" text)
  (end-encounter (list status value (甲钟 'current) (乙钟 'current))))
(define (推进! id clk n)
  (if 已结束? (error "动作回声：结束后行动") #t)
  (clk 'advance! n)
  (set! 上一目标 id)
  (cond ((甲钟 'full?) (完成! '甲 1 "甲先完成，收益1。"))
        ((乙钟 'full?) (完成! '乙 2 "乙先完成，收益2。"))
        (else #f)))
(define (动作 id label clk)
  (node (string-append "推进" label)
    :subtitle (string-append
      "坏0、中1、好2；本手准备值修正 "
      (number->string (回声修正 id))
      "。连续同目标−1，切换+1。")
    :clocks (list (clk 'render-data))
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (lambda () (list (modifier (回声修正 id) "上一手行动回声")))
      (outcome (lambda () (推进! id clk 0)))
      (outcome (lambda () (推进! id clk 1)))
      (outcome (lambda () (推进! id clk 2))))))
(define-opponent-rule "四骰窗口结束"
  (lambda () (not 已结束?))
  (lambda () (完成! 'timeout 0 "本轮结束，两项均未达到终点，收益0。")))
(define (on-encounter-enter)
  (play-dialogue!
    (line "规则" "甲到3兑现收益1；乙到5兑现收益2；任何一项完成即结束。")
    (line "规则" "四颗骰，每次行动坏0、中1、好2；继续相同目标下一手判定−1，换目标+1。")
    (line "规则" "当前修正在动作卡与赔率中公开，结束回合还未完成则收益0。")))
(define (on-encounter-collapse)
  (collapse-result (list 'collapse 0 (甲钟 'current) (乙钟 'current))))
(define (get-render-data)
  (container "动作回声"
    (append
      (list (note-node "标注：动作关系" "上一手决定下一手的判定"
        (if 上一目标
            (if (eq? 上一目标 '甲)
                "上一手：甲。继续甲−1，改做乙+1。"
                "上一手：乙。继续乙−1，改做甲+1。")
            "尚未行动，两项修正均为0。")))
      (clock-nodes (甲钟 'render-data) (乙钟 'render-data))
      (list (动作 '甲 "甲" 甲钟) (动作 '乙 "乙" 乙钟)))))
