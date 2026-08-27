;; scenes/encounters/巷子.scm - 第一章·小节三「教训莱恩」
;;
;; 两幕，同一个晚上。两幕各只有一个想法，骨架都很小。
;;
;; 第一幕·谈妥一条界线 —— 一根双向轨道记录弗兰克的判断。游标从中点起步；
;;   完整回答他的三个问题，正好抵达「相信你」。答坏会把游标推向「失去耐心」。
;;   玩家也可以用行动骰作出保证，较快换取信任；每作一项保证，跨幕共用的
;;   「老街的耐心」就少一格。到右端才进第二幕，到左端直接失败。
;;
;; 幕间 —— 弗兰克依据谈判结果把莱恩叫出来，不是被打退才让路。
;;   「照刚才说的办」把第一幕亲口接受的约束带进第二幕。
;;
;; 第二幕·小心翼翼地处理他 —— 目标不是一根从头填到尾的进度条，是**这个人的形态**：
;;   端着 → 翻脸。形态不额外占钟，它就是莱恩那张卡的面貌，
;;   标题、神态、能对他做的事整个换掉。推动形态的两根短钟同时只有一根在场：
;;     「他的面子」—— 拆掉他的表演。满：他不装了，承认前两封信都是他写的
;;     「他的话」　—— 他开始说真的。中途给出「第一封她就知道」；满格时他直接
;;                     交出铁盒，目标达成，交锋成功结束，不再追加处置选择。
;;
;;   底片跟着屈服走：他撒手了，东西就是你的。不单列成目标，也不做成机会卡——
;;   把它做成一次抢，这一节就又变回追债了。
;;
;; 对外契约：成功回传 '谈；任一幕耐心归零回传 '被赶出去，明天可以再来。
;;
;; 城市输入：码头是否认得你，以及你是否在工会房间见过弗兰克。

(define (key? name)
  (let ((v (get-global name))) (if v v #f)))

(define key-dock   (key? '钥匙-码头))
(define key-frank  (key? '钥匙-弗兰克))

;; 老街的耐心从第一幕直接继承到第二幕。保证和冒进都花同一份余地。
(define patience-max 5)
(define lyon-stage-max 5)  ; 每一阶段的防线；满格后才换成下一种样子

;; ── 第一幕 ──────────────────────────────────────
;; 十二格钟从中点起步。完整回答三问的最佳路线恰好能抵达右端。
(define trust-clk
  (make-clock "弗兰克的判断" 12 'gauge
    "左端：你把外面的麻烦带进来。右端：你能按老街的规矩办完这件事。"))
(trust-clk 'advance! 6)
(define talk-turn 0)
(define question-2-open? #f)
(define question-3-open? #f)
(define employer-clk
  (make-clock "解答" 4 'gauge "填满：说清你和剧院经理的关系。"))
(define standing-clk
  (make-clock "解答" 4 'gauge "填满：说明这已经不是一对旧情人的私事。"))
(define outcome-clk
  (make-clock "解答" 4 'gauge "填满：说清你要带走什么、留下什么。"))
(define promise-count 0)
(define promised-no-first? #f)
(define promised-no-search? #f)
(define promised-no-manager? #f)

;; ── 两幕共用 ────────────────────────────────────
(define crowd-clk
  (make-clock "老街的耐心" patience-max 'countdown
    "保证会缩短它；第二幕里的冒进继续消耗同一份耐心。归零：这件事当街收场。"))
(crowd-clk 'set! patience-max)

(define lyon-clk
  (make-clock "莱恩的防线" lyon-stage-max 'gauge
    (lambda (current max)
      (cond
        ((= form 1) "拆掉他的架子，让他承认两封信都是自己写的。")
        ((= form 2) "让他把旧账说到底，不再把委屈藏在勒索后面。")
        ((= form 3) "逼他承认这件事该停下，并把铁盒交出来。")
        (else (error "巷子：莱恩处于未知状态"))))))

(define act 1)
(define form 1)             ; 莱恩的样子：1 端着 / 2 翻脸 / 3 屈服前
(define cigs-used? #f)      ; 那半包老金牌只能拍一次桌子
(define finished? #f)
(define dock-word-used? #f) ; 码头替你说的那一句，只有一句

(define (tick-n! clk n)
  (if (<= n 0) #f (begin (clk 'tick!) (tick-n! clk (- n 1)))))

;; ============================================================
;; 第一幕·谈妥一条界线
;; ============================================================

(define (crowd+ n) (crowd-clk 'advance! (- 0 n)))

(define (check-crowd!)
  (if (or finished? (not (crowd-clk 'empty?)))
      #f
      (thrown-out!)))

;; 真正的失败：你被架出老街。这一节没结，明天还能再来——
;; 代价是经理的耐心，以及你在这条街上今晚欠下的那点脸。
(define (thrown-out!)
  (set! finished? #t)
  (if (= act 1)
      (play-dialogue!
        (line "世界" "三四个人从两边围过来，谁也没喊。他们只是把你往煤渣路上推。")
        (line "弗兰克" "别弄伤他。")
        (line "弗兰克" "送到路口就行。")
        (line "世界" "身后那扇门关上了。走廊的灯一盏接一盏灭掉。"))
      (play-dialogue!
        (line "弗兰克" "够了。今晚问到这里。")
        (line "世界" "他挡到你和莱恩中间。铁盒仍在炉子后面。")
        (line "弗兰克" "送他到路口。")))
  (spotlight! "今晚到此为止"
    "你被送回煤渣路的路口。堆场那头没有一点声音——他们在等你走远。")
  (end-encounter '被赶出去))

;; 倒下**在状态上完全等同「被赶出去」**（案子没结、明天再去、经理耐心 −1），
;; 只是这一次你不是被送回路口的，是被抬出去的。文案见 三封信 的 on-lesson-result。
(define (on-encounter-collapse)
  (collapse-result '倒下))

;; ============================================================
;; 幕间
;; ============================================================

;; 第一幕答得越完整，第二幕容错越高；约束换来的信任在这里兑现。
(define (begin-act2!)
  (set! act 2)
  (if (> promise-count 0)
      (spotlight! "照约定办"
        (cond
          ((= promise-count 3)
           "你作了三项保证。老街只再给你两格耐心。")
          ((= promise-count 2)
           "你作了两项保证。老街只再给你三格耐心。")
          (promised-no-first?
           "你答应不先动手。这里留给你的余地只有四格。")
          (promised-no-search?
           "你答应让他自己交出东西。这里留给你的余地只有四格。")
          (else
           "你答应不拿经理压他。这里留给你的余地只有四格。")))
      #f))

;; ============================================================
;; 第二幕·小心翼翼地处理他
;; ============================================================

;; 第一幕的谈判骨架。
(define (trust+ n)
  (if finished?
      #f
      (begin
        (trust-clk 'advance! n)
        (cond
          ((trust-clk 'full?) (frank-agrees!))
          ((trust-clk 'empty?) (thrown-out!))
          (else #f)))))

(define (topic-complete-banter! topic)
  (cond
    ((equal? topic '雇主)
     (play-banter!
       (line "弗兰克" "经理掏钱，不等于他替你挑人。")
       (line "尼尔" "我知道。")
       (line "弗兰克" "那就别让我看见他的手伸进来。")))
    ((equal? topic '夜莺)
     (play-banter!
       (line "弗兰克" "她走了，也还是从这条街走出去的。")
       (line "尼尔" "我不是来替谁抹掉她。")))
    ((equal? topic '收场)
     (play-banter!
       (line "弗兰克" "只拿该拿的，别让这条街替你收场。")
       (line "尼尔" "这正是我的打算。")))
    (else (error "巷子：未知已完成议题"))))

(define (answer+ clk n topic)
  (let ((was-full? (clk 'full?)))
    (clk 'advance! n)
    ;; 议题本身需要说完整才构成一项可信的解释；中途的零散进展不改变判断。
    ;; 同一议题溢出的点数不会重复结算，因为满格后它会从树里移除。
    (if (and (not was-full?) (clk 'full?))
        (begin (topic-complete-banter! topic) (trust+ 2))
        #f)))

(define (answer-failed! n)
  ;; 说不通不仅消耗自己，也让弗兰克更确信你不值得放行。
  (trust+ -1)
  (spend-composure! n))

(define (node-state-employer)
  (node "说清谁付的钱"
    :subtitle "社会；经理付钱，不等于他替你决定怎么收场"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "话说得太细" (lambda () (answer-failed! 2)))
      (outcome "他听懂了区别" (lambda () (answer+ employer-clk 1 '雇主)))
      (outcome "案子早于经理的人" (lambda () (answer+ employer-clk 2 '雇主))))))

(define (node-state-timeline)
  (node "从头讲起"
    :subtitle "见识；接案、查信、走桥廊都早于经理的人"
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "只能证明你跑得快" (lambda () (answer-failed! 2)))
      (outcome "时间对得上" (lambda () (answer+ employer-clk 1 '雇主)))
      (outcome "你不是临时找来的" (lambda () (answer+ employer-clk 2 '雇主))))))

(define (node-dock-testimony)
  (node "让码头作证"
    :subtitle "社会；卸货的人肯替你担一句，但你得让他现在开口"
    :disabled dock-word-used?
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "没人替你接话" (lambda () (set! dock-word-used? #t) (answer-failed! 2)))
      (outcome "班表上有你的名字"
        (lambda () (set! dock-word-used? #t) (answer+ employer-clk 2 '雇主)))
      (outcome "他肯替你担保"
        (lambda () (set! dock-word-used? #t) (answer+ employer-clk 3 '雇主))))))

(define (node-open-letters)
  (node "把两封信摊开"
    :subtitle "见识；索钱之后是要命，已经不是旧情人的口角"
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "他没有接" (lambda () (answer-failed! 2)))
      (outcome "他读完第一封" (lambda () (answer+ standing-clk 1 '夜莺)))
      (outcome "他把两封都读完" (lambda () (answer+ standing-clk 2 '夜莺))))))

(define (node-count-her-in)
  (node "把她算回来"
    :subtitle "社会；莱恩留下算这里的人，她走出去也不能除名"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "像拿出身压他" (lambda () (answer-failed! 2)))
      (outcome "他记起那一层楼" (lambda () (answer+ standing-clk 1 '夜莺)))
      (outcome "她也有资格讨说法" (lambda () (answer+ standing-clk 2 '夜莺))))))

(define (node-limit-purpose)
  (node "只拿信和底片"
    :subtitle "社会；把今晚要带走的东西说清楚"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "像进门前的好听话" (lambda () (answer-failed! 2)))
      (outcome "目标说清了" (lambda () (answer+ outcome-clk 1 '收场)))
      (outcome "你要东西，不要人" (lambda () (answer+ outcome-clk 2 '收场))))))

(define (node-state-consequence)
  (node "把后果说到底"
    :subtitle "社会；今晚不解决，明天来的就不是你"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他听成了威胁" (lambda () (answer-failed! 2)))
      (outcome "他知道这是实话" (lambda () (answer+ outcome-clk 1 '收场)))
      (outcome "你是可控的那个" (lambda () (answer+ outcome-clk 2 '收场))))))

(define (make-promise! which trust-n)
  (cond
    ((equal? which '不先动手) (set! promised-no-first? #t))
    ((equal? which '不搜身) (set! promised-no-search? #t))
    ((equal? which '不用经理) (set! promised-no-manager? #t))
    (else (error "巷子：未知保证")))
  (set! promise-count (+ promise-count 1))
  (crowd+ 1)
  (check-crowd!)
  (trust+ trust-n))

(define (node-promise-no-first)
  (node "保证不先动手"
    :subtitle "约束自己：第二幕不先动手；武力；少一格耐心"
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "他不信你收得住手" (lambda () (make-promise! '不先动手 1)))
      (outcome "他暂且记下" (lambda () (make-promise! '不先动手 3)))
      (outcome "这句话有分量" (lambda () (make-promise! '不先动手 4))))))

(define (node-promise-no-search)
  (node "保证不搜身"
    :subtitle "约束自己：第二幕不搜身；社会；少一格耐心"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "像进门前的空话" (lambda () (make-promise! '不搜身 1)))
      (outcome "他接受这条界线" (lambda () (make-promise! '不搜身 3)))
      (outcome "东西只能由莱恩交" (lambda () (make-promise! '不搜身 4))))))

(define (node-promise-no-manager)
  (node "保证不用经理压他"
    :subtitle "约束自己：第二幕不借经理施压；社会；少一格耐心"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他听见的还是经理" (lambda () (make-promise! '不用经理 1)))
      (outcome "今晚只算你的话" (lambda () (make-promise! '不用经理 3)))
      (outcome "剧院的人不会进来" (lambda () (make-promise! '不用经理 4))))))

(define (frank-agrees!)
  (play-dialogue!
    (line "世界" "弗兰克很久没有说话。巷口的人还在原处，没人替你催。")
    (line "弗兰克" "你把该说的都说了。剩下那件事，别把它办成别人的账。")
    (line "尼尔" "我只拿该拿的。")
    (line "世界" "他把手套折好，侧身让出通往修理棚的路。")
    (line "弗兰克" "莱恩。出来。")
    (line "世界" "椅脚在水泥地上刮了一声。莱恩从里面走出来。")
    (line "莱恩" "她终于告诉你我是谁了？")
    (line "弗兰克" "你问。照刚才说的办。"))
  (begin-act2!))

;; ── 三阶段：同一根防线满格，才换到下一种样子 ──────
(define (lyon-look)
  (cond
    ((= form 1) "他倚着工作台，装作这件事只是夜莺终于回来还债。铁盒还藏在炉子后面。")
    ((= form 2) "那套架子已经塌了。他承认信是自己写的，却还把七年的旧账攥在手里。")
    ((= form 3) "莱恩看着工作台上的铁盒，没有再拿它当筹码；巷口的人都在等他先松手。")
    (else (error "巷子：莱恩处于未知状态"))))

(define (advance-lyon! n)
  (lyon-clk 'advance! n)
  (if (lyon-clk 'full?)
      (cond
        ((= form 1)
         (set! form 2)
         (lyon-clk 'set! 0)
         (play-dialogue!
           (line "世界" "他停下来，看了你很久。那套「我无所谓」的架子自己塌了。")
           (line "莱恩" "行。是我。两封都是。")
           (line "莱恩" "我要钱。你知道港口现在什么样吗？")
           (line "莱恩" "她住的那种地方，一个月的房钱够我过一年。")))
        ((= form 2)
         (set! form 3)
         (lyon-clk 'set! 0)
         (play-dialogue!
           (line "尼尔" "她为什么不直接告诉我是你。")
           (line "世界" "他愣了一下。然后笑了。")
           (line "莱恩" "第一封她就知道。那是我们以前说的话。")
           (line "莱恩" "她一眼就该认出来。")))
        ((= form 3)
         (play-dialogue!
           (line "莱恩" "那些照片不是你想的那种。她在酒馆唱歌，跟码头的人喝酒。跟我。")
           (line "尼尔" "那你拿着它做什么。")
           (line "莱恩" "证明她在这儿待过。")
           (line "世界" "他把铁盒从炉子后面拿出来，推到工作台这一头。")
           (line "莱恩" "拿走。告诉她，我不再找她了。"))
         (spotlight! "东西到手了"
           "底片、照片和剩下的信都在铁盒里。莱恩答应不再找夜莺。")
         (finish! '谈))
        (else (error "巷子：莱恩处于未知状态")))
      #f))

;; ── 形态一的三张卡 ──────────────────────────────
(define (node-letters)
  (node "摊开那两封信"
    :subtitle "见识；低风险，纸就在口袋里，他认得自己的字"
    :tags (list "低风险")
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "他说不是他写的"
        (lambda () (spend-composure! 1)))
      (outcome "他不看那张纸"
        (lambda () (advance-lyon! 1)))
      (outcome "他念出了自己写的话"
        (lambda () (advance-lyon! 2))))))

(define (node-cigs)
  (node "把烟盒扔到桌上"
    :subtitle (if cigs-used?
                  "烟盒已经在桌上了"
                  "低风险；不掷骰，他知道这半包从哪儿来的")
    :tags (list "低风险")
    :disabled cigs-used?
    :requires (list (req-item "半包「老金牌」" 1))
    :resolve (instant
      (outcome "他不说话了"
        (lambda ()
          (set! cigs-used? #t)
          (play-banter!
            (line "世界" "软了的烟盒落在工作台上，滚了半圈。")
            (line "莱恩" "……那小子把这个也给你了。"))
          (advance-lyon! 2))))))

(define (node-her-now)
  (node "提她现在的样子"
    :subtitle "社会；高风险，失败才惊动老街，最快的一手"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他冲你吼回来"
        (lambda () (spend-composure! 2) (crowd+ 1)))
      (outcome "他脸上挂不住"
        (lambda () (advance-lyon! 2)))
      (outcome "他自己把话接了下去"
        (lambda () (advance-lyon! 3))))))

;; ── 形态二的四张卡 ──────────────────────────────
(define (node-listen)
  (node "听他说完"
    :subtitle "社会；低风险，不打断，这条街当没听见"
    :tags (list "低风险")
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他自己绕回去了"
        (lambda () (spend-composure! 1)))
      (outcome "他往下说了一段"
        (lambda () (advance-lyon! 1)))
      (outcome "他说了没打算说的"
        (lambda () (advance-lyon! 2))))))

(define (node-that-line)
  (node "追问那句话"
    :subtitle "见识；低风险，追问信上那句河边的暗语"
    :tags (list "低风险")
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "他反问你信在哪儿"
        (lambda () (spend-composure! 1)))
      (outcome "他解释了半句"
        (lambda () (advance-lyon! 1)))
      (outcome "他把来龙去脉说了"
        (lambda () (advance-lyon! 2))))))

(define (node-strip)
  (node "拆穿他的委屈"
    :subtitle "社会；高风险，失败才惊动老街；说破他的委屈"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他梗着脖子不认"
        (lambda () (spend-composure! 2) (crowd+ 1)))
      (outcome "他没话说了"
        (lambda () (advance-lyon! 2)))
      (outcome "他自己认了"
        (lambda () (advance-lyon! 3))))))

(define (node-workbench)
  (node "逼近工作台"
    :subtitle "力量；高风险，失败才惊动老街；逼近工作台"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "他把你推开"
        (lambda () (spend-composure! 2) (crowd+ 2)))
      (outcome "他退了半步"
        (lambda () (advance-lyon! 2)))
      (outcome "他不敢再挡"
        (lambda () (advance-lyon! 3))))))

;; ── 形态三：逼他自己把铁盒推过来 ──────────────────
(define (node-leave-him-a-way-out)
  (node "给他留条路"
    :subtitle "社会；低风险，承认他有旧账，但这件事到今晚为止"
    :tags (list "低风险")
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他又把话咽回去" (lambda () (spend-composure! 1)))
      (outcome "他松了点手" (lambda () (advance-lyon! 1)))
      (outcome "他不再拿铁盒当筹码" (lambda () (advance-lyon! 2))))))

(define (node-name-the-price)
  (node "说清该留下什么"
    :subtitle "见识；低风险，照片能留下，威胁和底片不能"
    :tags (list "低风险")
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "他只听见你要拿走东西" (lambda () (spend-composure! 1)))
      (outcome "他看着那只铁盒" (lambda () (advance-lyon! 1)))
      (outcome "他知道你没打算羞辱他" (lambda () (advance-lyon! 2))))))

(define (node-reach-for-the-box)
  (node "伸手拿铁盒"
    :subtitle "力量；高风险，失败才惊动老街；逼他松手"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "他按住铁盒不放" (lambda () (spend-composure! 2) (crowd+ 2)))
      (outcome "他退开了" (lambda () (advance-lyon! 2)))
      (outcome "他的手先松开" (lambda () (advance-lyon! 3))))))

;; ── 收场 ────────────────────────────────────────
;; 老街站起来把你请出去，也算这笔旧账结了：东西你照样带走了，
;; 只是整条街看着你带走的。区别落在城市那一侧，不在这里。
(define (finish! route)
  (if finished?
      #f
      (begin
        (set! finished? #t)
        (if (equal? route '难看)
            (play-dialogue!
              (line "世界" "巷口的人围上来，一步一步。没有人喊。")
              (line "搬运工" "够了。有什么事出去解决。")
              (line "尼尔" "他敲诈一个姑娘。")
              (line "搬运工" "那也是我们的事。")
              (line "世界" "莱恩自己把铁盒扔了过来——他不想让这条街看下去。")
              (line "莱恩" "滚吧。"))
            #f)
        (end-encounter route))))

(define-rule "围上来了"
  (lambda () (not finished?))
  (lambda () (check-crowd!)))

;; 第一幕的问题按回合累积。每一项尚未解释清楚的真实担忧，都会在回合末
;; 把弗兰克的判断向左推一格；第二、第三问分别在第一、第二回合后出现。
(define (unresolved-question-count)
  (+ (if (employer-clk 'full?) 0 1)
     (if (and question-2-open? (not (standing-clk 'full?))) 1 0)
     (if (and question-3-open? (not (outcome-clk 'full?))) 1 0)))

(define-turn-rule "弗兰克继续追问"
  (lambda () (and (= act 1) (not finished?)))
  (lambda ()
    (trust+ (- 0 (unresolved-question-count)))
    (if (= act 1)
        (begin
          (set! talk-turn (+ talk-turn 1))
          (cond
            ((= talk-turn 1)
             (set! question-2-open? #t)
             (play-dialogue!
               (line "弗兰克" "就算你不是跟下午那两个一拨的。")
               (line "弗兰克" "她离开几年，现在派人回来找旧账。这跟你有什么关系？")))
            ((= talk-turn 2)
             (set! question-3-open? #t)
             (play-dialogue!
               (line "弗兰克" "好。信是他写的，这件事该有个说法。")
               (line "弗兰克" "你问完，准备把他交给谁？")))
            (else #f)))
        #f)))

;; 时间的代价不归「老街看着」管：那根钟只记这条街的注意力。
;; 在门口耗一晚、在棚子里熬一晚，要还的是身体。
;;
;; 这里曾经有一条「熬下去」，每回合再扣 1 点冷静。冷静击穿改成一比一进伤势之后
;; 它就得删：引擎每回合那 1 点本身就是身体的代价（见 SceneManager.EncounterTurnComposureCost），
;; 满冷静五个回合、之后每回合 1 点伤。再叠一层就是把这条账单翻倍，
;; 而这一场本来就要跑五回合左右——那不是压力，是打不完。
;; 「这一场更耗」由老街看着自己表达，它已经在做这件事。

(define (act2-scene-node)
  ;; 当前场面是站在棚里就能看见的，不应伪装成要花行动观察的卡。
  (note-node "场面：修理棚" "莱恩的样子" (lyon-look)))

(define (act2-nodes)
  (append
    (list (act2-scene-node))
    (cond
      ((= form 1) (list (node-letters) (node-cigs) (node-her-now)))
      ((= form 2) (list (node-listen) (node-that-line) (node-strip) (node-workbench)))
      ((= form 3) (list (node-leave-him-a-way-out)
                         (node-name-the-price)
                         (node-reach-for-the-box)))
      (else (error "巷子：莱恩处于未知状态")))))

(define (act2-clocks)
  (list (lyon-clk 'render-data) (crowd-clk 'render-data)))

;; ============================================================
;; 渲染
;; ============================================================

(define (topic-progress-node topic clk)
  ;; 容器卡上的 :clocks 不会随导航带入子场景；议题内用标注保留自己的进度。
  (clock-node (string-append "议题进度：" topic) (clk 'render-data)))

(define (awaiting-next-question?)
  ;; 当前已提出的问题都答完了，但弗兰克会在本回合末才提出下一件事。
  (and (employer-clk 'full?)
       (or (not question-2-open?) (standing-clk 'full?))
       (or (not question-3-open?) (outcome-clk 'full?))))

(define (node-frank-considers-next-question)
  (observe-action "弗兰克还在想"
    "他没有让开路，只是在掂量下一件该问的事。本回合结束后，他会继续问。"))

(define (act1-nodes)
  (let ((promises
          (append
            (if promised-no-first? '() (list (node-promise-no-first)))
            (if promised-no-search? '() (list (node-promise-no-search)))
            (if promised-no-manager? '() (list (node-promise-no-manager))))))
    (append
      (if (employer-clk 'full?)
          '()
          (list
            (node "你替谁办事？"
              :subtitle "未解答时，每回合使弗兰克的判断恶化一格"
              :children (append (list (topic-progress-node "你替谁办事？" employer-clk)
                                      (node-state-employer) (node-state-timeline))
                                (if key-dock (list (node-dock-testimony)) '()))
              :clocks (list (employer-clk 'render-data)))))
      (if (or (not question-2-open?) (standing-clk 'full?))
          '()
          (list
            (node "这为何归你管？"
              :subtitle "未解答时，每回合使弗兰克的判断恶化一格"
              :children (list (topic-progress-node "这为何归你管？" standing-clk)
                              (node-open-letters) (node-count-her-in))
              :clocks (list (standing-clk 'render-data)))))
      (if (or (not question-3-open?) (outcome-clk 'full?))
          '()
          (list
            (node "问完以后怎么办？"
              :subtitle "未解答时，每回合使弗兰克的判断恶化一格"
              :children (list (topic-progress-node "问完以后怎么办？" outcome-clk)
                              (node-limit-purpose) (node-state-consequence))
              :clocks (list (outcome-clk 'render-data)))))
      ;; 这个空档不是「只剩保证可点」：新的议题会在回合末提出。
      (if (awaiting-next-question?)
          (list (node-frank-considers-next-question))
          '())
      ;; 约束是同一轮谈判的可选承诺，不应与三道主问题并列铺在场景中。
      ;; 任一承诺兑现后移出；全数兑现时连容器一起省略，避免留下空容器。
      (if (null? promises) '() (list (container "三项保证" promises))))))

(define (act1-clocks)
  (list (trust-clk 'render-data) (crowd-clk 'render-data)))

(define (get-render-data)
  (if (= act 1)
      (container "堆场路口"
        (append (apply clock-nodes (act1-clocks))
          (act1-nodes)))
      (container "修理棚"
        (append (apply clock-nodes (act2-clocks))
          (act2-nodes)))))

;; ── 开局 ────────────────────────────────────────
;; 码头声誉在第一问开放一次现场作证；见过弗兰克只改变开场口气。

(define (on-encounter-enter)
  (play-dialogue!
    (line "世界" "煤渣路走到尽头，堆场口停着一辆摩托车，前灯还热着。")
    (if key-frank
        (line "世界" "弗兰克从摩托车旁抬起头。工会房间那次以后，他已经认得你。")
        (line "世界" "路被人挡住了。他刚从车上下来，正在把手套摘掉。"))
    (if key-frank
        (line "弗兰克" "桥廊那边把名字给你了。")
        (line "弗兰克" "上次差点撞着你。"))
    (if key-frank
        (line "尼尔" "所以你知道我为什么来。")
        (line "尼尔" "……那晚是你。"))
    (line "弗兰克" "一个本地人看见外地人追老街的人，还能先做什么？")
    (line "尼尔" "我找莱恩。")
    (line "弗兰克" "不在。")
    (line "尼尔" "你知道我问的是谁。")
    (line "弗兰克" "知道。")
    (line "世界" "两边的屋檐下有人站着，没有走开，也没有靠近。")
    (line "弗兰克" "下午来了两个人。鞋太亮，不像来找活。")
    (line "弗兰克" "你跟他们，是一拨的吗？")))
