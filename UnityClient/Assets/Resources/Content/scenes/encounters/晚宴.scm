;; 第二章《晚宴》：前三支舞熟悉房间，后三支舞把时间交给具体的人。
;; 落点按厅里的分区挂（沙龙 / 吧台 / 舞池 / 露台 / 卡座），几张卡共用一个锚点；
;; 前半的分区到后半换成具体的人，玩家看到的是同一块地方换了谈话对象。

(define act 1)
(define turn 1)
(define finished? #f)
(define banquet-turns 6)
(define banquet-clk
  (make-clock "晚宴还剩" banquet-turns 'countdown
    "每支舞结束少一格；归零时晚宴散场。"))
(banquet-clk 'set! banquet-turns)
(define familiar-max 12)
(define familiar-clk
  (make-clock "熟悉这个房间" familiar-max 'gauge
    "填满即可提前接触具体宾客；第三支舞后也会自然熟悉。"))
(define vera-clk (make-clock "与薇拉深谈" 4 'gauge "填满才算真正谈成。"))
(define freight-clk (make-clock "与代理人深谈" 4 'gauge "填满才算真正谈成。"))
(define police-clk (make-clock "与贝恩斯深谈" 4 'gauge "填满才算真正谈成。"))
(define reporter-clk (make-clock "与记者深谈" 4 'gauge "填满才算真正谈成；错过今晚就没有第二次。"))
(define halfway '())

(define (on-encounter-enter)
  (recruit-companion! '夜莺 "夜莺"
    (list (list 'violence 0) (list 'knowledge 1) (list 'sharpness 4) (list 'social 4)))
  (set-actor-die-profile! '夜莺 2 0 "")
  (play-dialogue!
    (line "世界" "厅里没有空地，只有一圈一圈正在说话的人。")
    (line "夜莺" "先别找谁。我们得先学会怎么站在这里。")
    (line "世界" "贝恩斯穿着那件只在正式场合穿的外套，从人群里抬了抬杯。")
    (line "贝恩斯" "听说你一直想当侦探。")
    (line "尼尔" "谁说的？")
    (line "贝恩斯" "整间屋子。解决夜莺那件事的侦探——他们现在这么介绍你。")
    (line "世界" "他没等你回话，已经被另一个人拦住了。")))

(define (results)
  (append
    (if (vera-clk 'full?) (list '薇拉) '())
    (if (freight-clk 'full?) (list '货运代理) '())
    (if (police-clk 'full?) (list '贝恩斯) '())
    (if (reporter-clk 'full?) (list '记者) '())))

(define (dismiss-her!)
  (if (has-companion? '夜莺) (dismiss-companion! '夜莺) #f))

(define (on-encounter-collapse)
  (dismiss-her!)
  (collapse-result (results)))

(define (advance-familiar! n text)
  (familiar-clk 'advance! n)
  (result-note! text))

(define (crowd-action name anchor subtitle skill)
  (node name
    :anchor anchor
    :subtitle subtitle
    :clocks (list (familiar-clk 'render-data))
    :requires (list (req-die))
    :resolve (roll skill
      (outcome "没接上话" (lambda () (spend-composure! 1)))
      (outcome "跟上了" (lambda () (advance-familiar! 1 "你们跟上了这里说话的节奏")))
      (outcome "站稳了" (lambda () (advance-familiar! 2 "这一圈人开始主动问你们的名字"))))))

(define (node-rest name anchor amount subtitle)
  (node name
    :anchor anchor
    :subtitle subtitle
    :requires (list (req-die))
    :resolve (instant
      (outcome "缓过来了"
        (lambda () (restore-actor-composure! (current-actor) amount))))))

(define (mention-half! current text)
  (if (and (= current 2) (not (member? text halfway)))
      (begin (set! halfway (cons text halfway)) (play-banter! (line "尼尔" text)))
      #f))

(define (push-talk! clk text)
  (clk 'advance! 1)
  (mention-half! (clk 'current) text))

(define (push-vera!)
  (vera-clk 'advance! 1)
  (mention-half! (vera-clk 'current) "她开始把你当成可以交代事情的人")
  (if (vera-clk 'full?)
      (play-banter!
        (line "薇拉" "你把每句话都掂过一遍。")
        (line "尼尔" "您也一样。")
        (line "薇拉" "那就够了。下次公司有事，我会先来找你。"))
      #f))

(define (talk-node name subtitle skill clk thought)
  (node name
    :anchor "晚宴-卡座"
    :subtitle subtitle
    :tags (list "低风险")
    :clocks (list (clk 'render-data))
    :disabled (clk 'full?)
    :requires (list (req-die))
    :resolve (roll skill
      (outcome "话停在这里" (lambda () (spend-composure! 1)))
      (outcome "谈深一层" (lambda () (push-talk! clk thought)))
      (outcome "谈深一层" (lambda () (push-talk! clk thought))))))

(define (vera-node)
  (node "和薇拉谈"
    :anchor "晚宴-卡座"
    :subtitle "她不浪费一句话，也不替你填沉默"
    :tags (list "高风险")
    :clocks (list (vera-clk 'render-data))
    :disabled (vera-clk 'full?)
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "话停在这里" (lambda () (spend-composure! 2)))
      (outcome "谈深一层" (lambda () (push-vera!)))
      (outcome "谈深一层" (lambda () (push-vera!))))))

(define (freight-node)
  (node "陪他喝一杯"
    :anchor "晚宴-吧台"
    :subtitle "高风险；谈深 2 格，失手倒退 1 格并损失 2 点冷静"
    :tags (list "高风险")
    :clocks (list (freight-clk 'render-data))
    :disabled (freight-clk 'full?)
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "酒喝急了"
        (lambda ()
          (freight-clk 'advance! -1)
          (spend-composure! 2)))
      (outcome "酒过一轮" (lambda () (freight-clk 'advance! 2)))
      (outcome "他肯开口了" (lambda () (freight-clk 'advance! 2))))))

;; 散场。没有胜负，时间到了就结束，所以收尾不是一张成败卡：
;; 这几句讲**她的那一晚**，固定不变——你谈成了谁对她没有影响。
;; 你的那一晚由城市侧那张聚光卡讲（见 world/第二章/晚宴.scm）。
(define (finish!)
  (if finished?
      #f
      (begin
        (set! finished? #t)
        (play-dialogue!
          (line "世界" "最后一支曲子停了。人开始往门口走。")
          (line "世界" "她在厅那头，被四五个人围着，笑得比进门时松。")
          (line "世界" "有人把她的外套送到她手上。她穿过整个厅走过来，一路被拦下三次。")
          (line "夜莺" "你怎么还站在这儿。")
          (line "尼尔" "等你。")
          (line "夜莺" "我以为你早走了。")
          (line "世界" "车在门口。她上车之前，回头看了那间厅一眼。"))
        (dismiss-her!)
        (end-encounter (results)))))

(define (enter-act-two!)
  (set! act 2)
  (set! turn 1)
  ;; 即使玩家前三回合连续失手，在场待过三支舞后也已不再是生客。
  (familiar-clk 'set! familiar-max)
  (dismiss-her!)
  (play-dialogue!
    (line "世界" "第三支舞停下时，已经有人隔着半个厅向她举杯。")
    (line "夜莺" "你去吧。我认得这里了。")
    (line "世界" "她说完就被下一圈人接了过去。")))

(define-turn-rule "舞曲往下走"
  (lambda () (not finished?))
  (lambda ()
    (set! turn (+ turn 1))
    (banquet-clk 'advance! -1)
    (if (= act 1)
        (cond
          ((= turn 2)
           (auto-action! "主人过来敬酒" "他留住你们，说起夜莺昨晚的掌声"
             (list (list 'player 1) (list '夜莺 1))
             (lambda () (familiar-clk 'advance! 3)))
           (play-banter!
             (line "主人" "昨晚那阵掌声，我隔着两条街都听见了。")
             (line "夜莺" "那您今晚站得近些。")))
          ((= turn 3)
           (auto-action! "她接下这支舞" "夜莺自己走进舞池，像是早知道该在什么时候伸手"
             (list (list '夜莺 1))
             (lambda () (familiar-clk 'advance! 3)))
           (play-banter!
             (line "舞伴" "夜莺小姐，这一支舞能留给我吗？")
             (line "夜莺" "您问得正是时候。")))
          ((> turn 3) (enter-act-two!))
          (else #f))
        (if (> turn 3) (finish!) #f))))

(define (act-one-nodes)
  (append
    (clock-nodes (banquet-clk 'render-data) (familiar-clk 'render-data))
    (list
      (crowd-action "加入赞助人" "晚宴-沙龙" "听清他们怎样称呼彼此，再接住一句话" 'social)
      (crowd-action "听他们谈钱" "晚宴-吧台" "那些数字说得很轻，像不值得压低声音" 'knowledge)
      (crowd-action "走进舞池" "晚宴-舞池" "跟上拍子，也让别人有机会看清你们" 'sharpness)
      (node-rest "去露台透气" "晚宴-露台" 1 "花一颗行动骰，恢复 1 点冷静"))))

(define (act-two-nodes)
  (append
    ;; 四条深谈进度各自只被自己那一张卡推进，钟已经挂在各自的 node 上
    ;; （vera-node / freight-node / talk-node 的 :clocks）；不在这里重复摆一遍。
    ;; 「晚宴还剩」是整场共用的时间，留在空间级。
    (clock-nodes (banquet-clk 'render-data))
    (list
      (vera-node)
      (freight-node)
      (talk-node "和贝恩斯谈" "他愿意谈程序，不愿意谈谁让程序转得更快" 'knowledge police-clk
        "他记得每一份手续，却不肯说谁先打过电话")
      (talk-node "和记者谈" "他明早离城，今晚只够决定要不要交换姓名" 'sharpness reporter-clk
        "他不是在听故事；他在判断哪一种故事有人肯买")
      (node-rest "在餐桌边停下" "晚宴-沙龙" 1 "花一颗行动骰，恢复 1 点冷静"))))

(define (get-render-data)
  ;; 根节点名就是地点名，对应 Anchor_晚宴；分幕是内部状态，不进名字。
  (container "晚宴"
    ;; 熟悉度是解锁条件，不再和夜莺离场绑在同一个换幕节点上。
    (if (and (= act 1) (not (familiar-clk 'full?)))
        (act-one-nodes)
        (act-two-nodes))))
