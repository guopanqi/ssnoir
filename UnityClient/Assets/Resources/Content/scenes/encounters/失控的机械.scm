;; 失控的机械——你第一次走进旧机械实验区那天。
;;
;; 一台本该自己搬货的机器正在撞轨道，而林还蹲在里面看。
;; 这一场没有目标钟，只有一根「机器还能撑住」的倒计时：它跟你做什么无关，
;; 每回合自己掉。你要在它掉完之前决定用哪种方式收场——四种方式各自说明你是谁，
;; 也各自决定林记住的是什么。
;;
;; 拉闸最安全，机器全毁，他没事，你什么也没看见。
;; 撑住最贵：吃冷静、可能见血，但他拿到了那几秒——「不是控制器。是轨道。」
;;
;; 对外契约：回传收场标签 '断闸 / '拖走 / '撑住 / '自己修；倒下回传 '倒下，
;; 它在林那边与 '拖走 记同一份状态，只有文案不同。见 world/人物/林.scm。

(define hold-max 3)
(define brace-max 2)

(define hold-clk
  (make-clock "机器还能撑住" hold-max 'countdown
    "轨道已经开裂。归零它就自己散了——这根钟跟你做什么无关。"))

(define brace-clk
  (make-clock "替他撑住的几秒" brace-max 'gauge
    "他要的不是安全，是再看几秒。填满他就看明白了。"))

(define finished? #f)

(define (finish! route title text)
  (if finished? (error "失控的机械：交锋已经结算") #t)
  (set! finished? #t)
  (spotlight! title text)
  (end-encounter route))

;; 这场没有失败分支。倒下**在状态上完全等同「拖走」**（林那边照样记 met-route "拖走"），
;; 只是这一次躺下的是你：讲成"你把他拖了出来"就是在骗玩家。
(define (on-encounter-collapse)
  (collapse-result '倒下))

(define (collapse!)
  (spend-actor-composure! 'player 2)
  ;; 这一下若把玩家打倒，不能再走普通 finish!：动作结束后的统一倒下流程
  ;; 会调用 on-encounter-collapse，先处理送医，再以「倒下」结果退出交锋。
  (if (hospitalization-pending?)
      #f
      (finish! '拖走 "它自己散了"
        "支架先塌，然后是整条臂。你在最后一刻把他从下面拽出来。他回头看那堆金属，什么也没说。")))

(define (check!)
  (if (or finished? (not (hold-clk 'empty?)))
      #f
      (collapse!)))

(define (on-encounter-enter)
  ;; 这是交锋初始盘面，不是玩家造成的 Clock 变化，不写进入场报告的效果条。
  (hold-clk 'load! hold-max)
  (play-dialogue!
    (line "世界" "机械区的卷帘门开着一半。里面有东西在猛烈地撞轨道，一下，又一下。")
    (line "世界" "一台三人高的装卸臂正在自己动。它抓空了，又抓一次，还是空的。")
    (line "世界" "轨道尽头蹲着一个人。他没有在躲——他在看。")
    (line "林" "别关！")
    (line "尼尔" "那玩意儿要塌了。")
    (line "林" "我知道。再给我一点时间。")))

(define (node-breaker)
  (instant-action "拉总闸"
    (lambda ()
      (finish! '断闸 "全停了"
        "灯先灭，然后整台机器安静下来，臂垂在半空。林从轨道那头走出来，看着它，很平静。他说：明天再说吧。"))))

(define (node-pull)
  (node "把他拖出去"
    :tags (list "高风险")
    :subtitle "武力；他会挣，因为他真的还差一点"
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome (lambda () (spend-composure! 2) (check!)))
      (outcome (lambda () (hold-clk 'advance! -1) (check!)))
      (outcome (lambda ()
          (finish! '拖走 "他被你拖了出来"
            "十几秒之后那条臂塌进轨道里。他站在外面看着，一直没说话。你救了他，他也一直记得他差一点就看明白了。"))))))

(define (node-brace)
  (node "替他撑住"
    :tags (list "高风险")
    :subtitle "武力；顶住那节支架，让它再跑几秒"
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome (lambda () (spend-composure! 2) (hold-clk 'advance! -1) (check!)))
      (outcome (lambda () (brace-clk 'tick!) (check-brace!)))
      (outcome (lambda () (brace-clk 'tick!) (hold-clk 'tick!) (check-brace!))))))

(define (check-brace!)
  (if (brace-clk 'full?)
      (finish! '撑住 "他看明白了"
        "机器最终还是散了。他从底下爬出来，满身机油，不看那堆废铁，只捡起一截断掉的金属：不是控制器。是轨道。")
      (check!)))

;; 见识够高才成立：你得看得懂那节轨道为什么会咬住。
(define (node-fix)
  (node "自己上手"
    :subtitle "见识；断掉那节轨道的供电，让臂停在原位"
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome (lambda () (hold-clk 'advance! -1) (check!)))
      (outcome (lambda () (hold-clk 'tick!) (check!)))
      (outcome (lambda ()
          (finish! '自己修 "它停在原位"
            "臂停在半空，没有塌。林从轨道那头绕出来，先看机器，再看你：你怎么知道是那一节？"))))))

(define-opponent-rule "轨道还在裂"
  (lambda () (not finished?))
  (lambda ()
    (hold-clk 'advance! -1)
    (check!)))

(define (get-render-data)
  (node "失控的机械"
    :anchor "三号货栈工棚"
    :children (append
      (clock-nodes (hold-clk 'render-data) (brace-clk 'render-data))
      (list (node-breaker) (node-pull) (node-brace) (node-fix)))))
