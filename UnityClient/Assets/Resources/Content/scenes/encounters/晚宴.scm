;; 第二章《晚宴》：先融入房间里的人群，再把剩下的时间交给具体的人。
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
(define familiar-max 3)
(define familiar-clk
  (make-clock "熟悉这个房间" familiar-max 'gauge
    "融入任意三群人即可接触具体宾客。"))
;; 第一幕按三支舞（三回合）设计。主人敬酒（第二回合）和她接舞（第三回合）各给每群 +1，
;; 玩家自己的 9 颗自由骰（平均推 1 格出头）投向哪几群由他自己定：专投三群，每群还差 4，
;; 第三支舞前后能全填满；分散投的靠第三回合结束的强制换幕兜底。
;; 每群 6 格保证一群不会被第一回合两次「站稳了」一下填满。
(define crowd-max 6)
(define patrons-clk (make-clock "融入赞助人" crowd-max 'gauge "填满算真正融入这一圈。"))
(define theater-clk (make-clock "融入剧院那桌" crowd-max 'gauge "填满算真正融入这一桌。"))
(define business-clk (make-clock "听懂生意人" crowd-max 'gauge "填满算听懂他们的规矩。"))
(define dancers-clk (make-clock "跟上舞池宾客" crowd-max 'gauge "填满算跟上舞池的节奏。"))
(define deep-talk-max 6)
(define vera-clk (make-clock "与薇拉深谈" deep-talk-max 'gauge "填满才算真正谈成。"))
(define walter-clk (make-clock "与沃尔特深谈" deep-talk-max 'gauge "填满才算真正谈成。"))
(define police-clk (make-clock "与贝恩斯深谈" deep-talk-max 'gauge "填满才算真正谈成。"))
(define reporter-clk (make-clock "与记者深谈" deep-talk-max 'gauge "填满才算真正谈成；错过今晚就没有第二次。"))
(define halfway '())

(define (on-encounter-enter)
  (recruit-companion! '夜莺 "夜莺" (同伴能力 '夜莺))
  (set-actor-die-profile! '夜莺 2 0 "")
  (play-dialogue!
    (line "世界" "厅里没有空地，只有一圈一圈正在说话的人。")
    (line "夜莺" "先别找谁。我们得先学会怎么站在这里。")
    (line "世界" "贝恩斯穿着那件只在正式场合穿的外套，从人群里抬了抬杯。")
    (line "贝恩斯" "听说你一直想当侦探。")
    (line "尼尔" "谁说的？")
    (line "贝恩斯" "整间屋子。解决夜莺那件事的侦探——他们现在这么介绍你。")
    (line "世界" "他没等你回话，已经被另一个人拦住了。"))
  (if (get-global '经理解雇过尼尔)
      (play-dialogue!
        (line "经理" "诸位，这是我的老朋友尼尔！我早知道他会出头。")
        (line "尼尔" "上回你可没这么说。")
        (line "经理" "那是工作，朋友。你不会还记着吧？"))
      (play-dialogue!
        (line "经理" "诸位，这是我的老朋友尼尔！我早知道他会出头。"))))

(define (results)
  (append
    (if (vera-clk 'full?) (list '薇拉) '())
    (if (walter-clk 'full?) (list '沃尔特) '())
    (if (police-clk 'full?) (list '贝恩斯) '())
    (if (reporter-clk 'full?) (list '记者) '())))

(define (dismiss-her!)
  (if (has-companion? '夜莺) (dismiss-companion! '夜莺) #f))

(define (on-encounter-collapse)
  (dismiss-her!)
  (collapse-result (results)))

;; 融入一群人的那一下，是这群人开口对你说话——不是旁白替他们描述。
;; 各群按各自的心思说：赞助人在掂你值不值得投，剧院那桌只认夜莺，
;; 生意人奉承得最熟练，舞池宾客只管这一支舞好不好看。
;; 卡和自动动作都可能是填满的那一下，所以定义在一处。
(define patrons-full-line
  (line "赞助人" "侦探先生，我们这一圈不常有新面孔。改天来我办公室坐坐——我那儿也有几件事，警察不太方便管。"))
(define theater-full-line
  (line "剧院那桌" "夜莺小姐，昨晚第二幕那一段，您是故意压低了唱的吧？全场都往前探了半寸。"))
(define business-full-line
  (line "生意人" "您这样的人该在我们这边。做侦探能挣几个钱？您把眼睛借给我们一个季度，就是另一番光景了。"))
(define dancers-full-line
  (line "舞伴" "您带得比看上去稳。下一支曲子慢，别急着退到墙边去。"))

;; 结果里只留钟的推进行，不另写「跟上了他们的话」这类描述——钟本身已经在说这件事。
;; 要说的话只在钟填满那一下说，用一句 banter 显式地说，不藏在结果行里。
(define (advance-crowd! clk n fill-line enter-now?)
  (let ((was-full? (clk 'full?)))
    (clk 'advance! n)
    (if (and (not was-full?) (clk 'full?))
        (begin
          (familiar-clk 'advance! 1)
          (play-banter! fill-line))
        #f)
    (if (and enter-now? (familiar-clk 'full?))
        (enter-act-two!)
        #f)))

;; 别人替你办的事（主人敬酒、她接舞）让厅里每一群人都熟一分：被主人当众留住、
;; 看她跳一支舞，四群人都看见了你们。只推某一群的话，玩家看见的是骰子没了、
;; 「熟悉这个房间」不动，像被抢了时间；全填满又替玩家做了选择。每群 +1 两头都不占。
(define (advance-all-crowds!)
  (advance-crowd! patrons-clk 1 patrons-full-line #f)
  (advance-crowd! theater-clk 1 theater-full-line #f)
  (advance-crowd! business-clk 1 business-full-line #f)
  (advance-crowd! dancers-clk 1 dancers-full-line #f))

(define (crowd-action name anchor subtitle skill clk fill-line)
  (node name
    :anchor anchor
    :subtitle subtitle
    :clocks (list (clk 'render-data))
    :disabled (clk 'full?)
    :requires (list (req-die))
    :resolve (roll skill
      (outcome (lambda () (spend-composure! 1)))
      (outcome (lambda () (advance-crowd! clk 1 fill-line #t)))
      (outcome (lambda () (advance-crowd! clk 2 fill-line #t))))))

(define (node-rest name anchor amount subtitle)
  (node name
    :anchor anchor
    :subtitle subtitle
    :requires (list (req-die))
    :resolve (instant
      (outcome (lambda () (restore-actor-composure! (current-actor) amount))))))

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
      (outcome (lambda () (spend-composure! 1)))
      (outcome (lambda () (push-talk! clk thought)))
      (outcome (lambda () (push-talk! clk thought))))))

(define (vera-node)
  (node "和薇拉谈"
    :anchor "晚宴-卡座"
    :subtitle "她不浪费一句话，也不替你填沉默"
    :tags (list "高风险")
    :clocks (list (vera-clk 'render-data))
    :disabled (vera-clk 'full?)
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome (lambda () (spend-composure! 2)))
      (outcome (lambda () (push-vera!)))
      (outcome (lambda () (push-vera!))))))

;; 吧台尽头那个穿着整齐的人是沃尔特·芬奇，保险公司跑外勤的。
;; 他喝得快，跟不上就是失礼；跟上了，他记得你——过两天他会在大堂找你。
(define (walter-node)
  (node "陪他喝一杯"
    :anchor "晚宴-吧台"
    :subtitle "保险公司的沃尔特。他喝得快，跟不上就是失礼；跟上了话也说得快"
    :tags (list "高风险")
    :clocks (list (walter-clk 'render-data))
    :disabled (walter-clk 'full?)
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome (lambda () (spend-composure! 2)))
      (outcome (lambda () (walter-clk 'advance! 2)))
      (outcome (lambda () (walter-clk 'advance! 2))))))

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
  (if (= act 2)
      #f
      (begin
        (set! act 2)
        ;; 即使玩家前三回合连续失手，在场待过三支舞后也已不再是生客。
        (familiar-clk 'set! familiar-max)
        (dismiss-her!)
        (play-dialogue!
          (line "世界" "又一支舞停下时，已经有人隔着半个厅向她举杯。")
          (line "夜莺" "你去吧。我认得这里了。")
          (line "夜莺" "那边四个——薇拉、沃尔特、警官、记者。想谈就去，别等散场。")
          (line "世界" "她说完就被下一圈人接了过去。"))
        (spotlight! "下半场"
          "厅里的人你都认得了。赞助人的薇拉、吧台的沃尔特、警察贝恩斯、明早离城的记者——想深谈就过去，每个人都只有今晚。"))))

(define-opponent-rule "舞曲往下走"
  (lambda () (not finished?))
  (lambda ()
    (set! turn (+ turn 1))
    (banquet-clk 'advance! -1)
    (if (banquet-clk 'empty?)
        (finish!)
        (if (= act 1)
        (cond
           ((= turn 2)
            ;; 主人迎上来敬酒：近端沙龙是主人迎宾的那一圈，他在那儿留住你们。
            (auto-action! "主人过来敬酒" "他留住你们，说起夜莺昨晚的掌声"
              (list (list 'player 1) (list '夜莺 1))
              (auto-dialogue
                (line "主人" "昨晚那阵掌声，我隔着两条街都听见了。")
                (line "夜莺" "那您今晚站得近些。"))
              advance-all-crowds!
              "晚宴-沙龙")
            (if (familiar-clk 'full?) (enter-act-two!) #f))
           ((= turn 3)
            ;; 她被邀进舞池：人已经在舞池中央，卡就挂在舞池。
            (auto-action! "她接下这支舞" "夜莺自己走进舞池，像是早知道该在什么时候伸手"
              (list (list '夜莺 1))
              (auto-dialogue
                (line "舞伴" "夜莺小姐，这一支舞能留给我吗？")
                (line "夜莺" "您问得正是时候。"))
              advance-all-crowds!
              "晚宴-舞池")
            (if (familiar-clk 'full?) (enter-act-two!) #f))
          ((> turn 3) (enter-act-two!))
          (else #f))
        #f))))

(define (act-one-nodes)
  (append
    (clock-nodes (banquet-clk 'render-data) (familiar-clk 'render-data))
    (list
      (crowd-action "加入赞助人" "晚宴-沙龙" "听清他们怎样称呼彼此，再接住一句话" 'social
        patrons-clk patrons-full-line)
      (crowd-action "加入剧院那桌" "晚宴-卡座" "他们聊的是角色，留意的是谁能接住场面" 'social
        theater-clk theater-full-line)
      (crowd-action "听他们谈钱" "晚宴-吧台" "那些数字说得很轻，像不值得压低声音" 'knowledge
        business-clk business-full-line)
      (crowd-action "走进舞池" "晚宴-舞池" "跟上拍子，也让别人有机会看清你们" 'sharpness
        dancers-clk dancers-full-line)
      (node-rest "去露台透气" "晚宴-露台" 1 "花一颗行动骰，恢复 1 点冷静"))))

(define (act-two-nodes)
  (append
    ;; 四条深谈进度各自只被自己那一张卡推进，钟已经挂在各自的 node 上
    ;; （vera-node / walter-node / talk-node 的 :clocks）；不在这里重复摆一遍。
    ;; 「晚宴还剩」是整场共用的时间，留在空间级。
    (clock-nodes (banquet-clk 'render-data))
    (list
      (vera-node)
      (walter-node)
      (talk-node "和贝恩斯谈" "他愿意谈程序，不愿意谈谁让程序转得更快" 'knowledge police-clk
        "他记得每一份手续，却不肯说谁先打过电话")
      (talk-node "和记者谈" "他明早离城，今晚只够决定要不要交换姓名" 'sharpness reporter-clk
        "他不是在听故事；他在判断哪一种故事有人肯买")
      (node-rest "在餐桌边停下" "晚宴-沙龙" 1 "花一颗行动骰，恢复 1 点冷静"))))

(define (get-render-data)
  ;; 根节点名就是地点名，对应 Anchor_晚宴；分幕是内部状态，不进名字。
  (container "晚宴"
    ;; 熟悉度是解锁条件，不再和夜莺离场绑在同一个换幕节点上。
    (if (= act 1)
        (act-one-nodes)
        (act-two-nodes))))
