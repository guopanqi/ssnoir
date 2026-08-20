;; scenes/encounters/首演之夜.scm - 第一章大型交锋「演出必须继续」
;;
;; 设计见 docs/三封信-第一章方案.md §8。这里只说结构。
;;
;; 玩家从头到尾只做一件事：处理问题。演出始终在继续——乐队没停过，
;; 她的声音断了又接上来。演出的推进是看见和听见的（音乐进下一段、灯换一次色、
;; 她从台底站起来走回中心），它给玩家的不是新任务，是新的麻烦，
;; 以及旧麻烦变得更要命。
;;
;;   一根压力钟  夜莺的安危 2/10(+内环准备)
;;               满格 = 她真的出事了；撑满六回合就算完成。
;;               它**不自走**——每一格都有来处。
;;               **你从来不需要清空这块板。**
;;
;;   每一处危机是一个 container，里面是处理它的几种办法：
;;     临时（只有放血型有）难度带正修正、失手不收费；一格进度也不推，
;;                         只买下这一回合，一回合买一次。差骰子的去处。
;;     低风险             慢（中 +1 / 好 +2），失手扣你自己的冷静。
;;     高风险             快（中 +2 / 好 +3），失手扣冷静**并且**推安危钟一格。
;;
;;   两种危机模式，差别只在"不管它会怎样"：
;;     放血  每回合推安危钟一格，不会落地。可以先忍着。
;;     倒坠  自走钟走空就落地，砸在两处：场子和她。**倒坠没有临时。**
;;           往下掉的东西你拖不住——给它配临时，拖就永远比修便宜。
;;
;;   同一处危机的几个动作**吃不同的能力**——一幕两处就摊开了三种，
;;   所以"我这几颗骰是什么"和"我先救哪一处"是两个独立的问题。
;;
;;              临时           低风险          高风险
;;   标记        —              刹住吊杆 见识   冲上台 力量
;;   烟          先挡一挡 交际   一排排带 交际   掀开边门 力量
;;   黑场        点应急灯 见识   顺线查那路 见识 硬合闸 力量
;;   灯环        —              一根根绞 力量   爬上去解扣 敏锐
;;
;;   一幕  标记被挪过     倒坠  坠落倒计时 3 · 修复 0/6   中央
;;   一幕  烟灌进看台     放血  每回合 +1 · 疏开 0/6     内环
;;   二幕  半个场子黑着   放血  每回合 +1 · 照明 0/8     外圈
;;   二幕  顶灯环松了     倒坠  坠落倒计时 4 · 修复 0/12  中央
;;
;;   两次坠落，一小一大：第一次擦过去，是**误导**——表层看是"有人想砸她"，
;;   而你在刹吊杆的过程中读到"标记是新粘的、旧胶印还在半尺外"，
;;   于是它变成"有人接触过舞台机械，而且知道今晚的走位"。
;;   第二次才是要命的那一下。
;;   三幕  不新增危机，也不多出任何新卡。变的是她的位置——
;;         她走回舞台正中，而那一圈灯架就在她头顶。
;;
;;   落地不是二元的：**已经推掉的每 4 格少挨一格压力，推过一半她就不会伤着。**
;;   绞到第十格和一格没绞不该是同一件事。"让危险错过她"因此不是一张单独的卡，
;;   是你前面那几颗骰真的推进去了的结果。
;;
;;   副目标  那个背影 0/5。一根**累计**的钟，但只在门开着的那一回合
;;           出现在板上——随机开两次，每次一回合。凑不满就下次接着凑，
;;           两次都过去还没满，他今晚就走了。
;;           不碰压力钟；这是全场唯一保存到第二章的东西。
;;
;; 对外契约：以 'done 结束（成败不是二元的）。
;; 城市输入（只在顶部读取一次）：三个环的准备格数，以及请到了谁。
;; 写回全局：'首演-她受伤（本章收场文本要读）、'首演-认出黑衣人（第二章要读）。

;; ============================================================
;; 城市输入
;; ============================================================

(define (prep-of key)
  (let ((v (get-global key)))
    (if (number? v) v 0)))

(define prep-outer (prep-of '准备-外圈))   ; 0..4  门与后廊
(define prep-inner (prep-of '准备-内环))   ; 0..4  走道与看台
(define prep-core (prep-of '准备-中央))    ; 0..2  升降台行程与台底通风口
;; 准备阶段你让了她几次。它不是好感度，是她今晚会为这一夜付出多少：
;; 让得越多，她越不肯下台——演出更完整，她也在最亮的地方待得更久。
(define her-night (prep-of '首演-她的一夜))  ; 0..4
(define encore-cancelled?
  (let ((v (get-global '首演-取消谢幕))) (if v v #f)))

;; 每 2 格算一步。布置替不了你把事办完，它只是让你不至于太晚才动手。
(define (steps-of n) (cond ((>= n 4) 2) ((>= n 2) 1) (#t 0)))
(define outer-steps (steps-of prep-outer))
(define inner-steps (steps-of prep-inner))

;; 三个环各兑现成一件不同的事，不是同一种加成换三个名字：
;;   外圈满格 —— 后廊和配电箱都清过了，那一路电今晚烧不起来（黑场不上板）
;;   内环准备 —— 疏散路线、引座的人、钉死的栏杆，全是为了人群：恐慌更耐打
;;   中央满格 —— 台底那个封了一年的通风口被你封死了（烟不上板）
(define blackout-cancelled? (>= prep-outer 4))
(define smoke-cancelled? (>= prep-core 2))
(define stage-max (+ 10 inner-steps))

(define joe-final-state (get-global '乔最终状态))
(define aide-joe-requested (if (get-global '人手-乔) #t #f))
(define (joe-final-state-can-act?)
  (member? joe-final-state (list "痊愈待邀请" "残疾待邀请" "已入队")))
(define aide-joe
  (if aide-joe-requested
      (if (joe-final-state-can-act?)
          #t
          (error "首演之夜：乔被列为外圈人手，但人物最终状态不能行动"))
      #f))
(define aide-frank (if (get-global '人手-弗兰克) #t #f))
(define aide-police (if (get-global '人手-警察) #t #f))
(define aide-usher (if (get-global '人手-领班) #t #f))

;; ============================================================
;; 一根压力钟：夜莺的安危
;; ============================================================
;;
;; 这一晚要保的不是演出，是她。所有的危险最后都折算到同一个地方——
;; 东西掉下来、线路烧着、烟漫过台口、你自己冒险搞出来的乱子，
;; 都是让她离出事更近一步。满格就是她真的出事了。
;;
;; **它不自走。**每一格都有来处：某处放血、某次高风险失手、某处落地。
;; 玩家永远算得出自己还剩几格余地。

(define stage-clk
  (make-clock "夜莺的安危" stage-max 'segments
    "东西掉下来、线路烧着、烟漫到台口。满格就是她真的出事了。"))
(stage-clk 'set! 2)

(define (push-stage! n) (stage-clk 'advance! n))

(define (collapsed?) (stage-clk 'full?))

;; ============================================================
;; 一处危机
;; ============================================================
;;
;; 两种模式共用一个闭包：
;;   'fall  倒坠 —— fall 钟自己往下走（countdown），走空就落地
;;   'bleed 放血 —— 在板上一回合就推恐慌一格，不会落地
;; 两种都有一根很长的 fix 钟；填满就是你把它按住了。

(define (ability-label a)
  (cond ((equal? a 'violence) "力量")
        ((equal? a 'social) "交际")
        ((equal? a 'sharpness) "敏锐")
        ((equal? a 'knowledge) "见识")
        (#t (error "首演之夜：未知的能力"))))

;; notes：(list (list 格数 说的话) ...)。一次推两格也不能漏掉中间那句，
;; 所以按区间 (from, to] 全部说出来——每一格都得是一件具体的事。
(define (collect-notes notes from to)
  (if (null? notes)
      '()
      (let ((n (car (car notes))))
        (if (and (> n from) (<= n to))
            (cons (cadr (car notes)) (collect-notes (cdr notes) from to))
            (collect-notes (cdr notes) from to)))))

(define (make-crisis id kind ring fix-need fall-need head-start
                     ease-name ease-desc ease-ab
                     low-name low-desc low-ab
                     high-name high-desc high-ab
                     desc notes)
  (let ((fix (make-clock "修复进度" fix-need 'segments desc))
        (fall (if (> fall-need 0)
                  (make-clock "坠落倒计时" fall-need 'countdown
                              "归零就落下来，砸在场子里。")
                  #f))
        (eased? #f))
    (if (> head-start 0) (fix 'set! head-start) #f)
    (if fall (fall 'set! fall-need) #f)
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'id) id)
          ((equal? msg 'kind) kind)
          ((equal? msg 'ring) ring)
          ((equal? msg 'desc) desc)
          ((equal? msg 'ease-name) ease-name)
          ((equal? msg 'ease-desc) ease-desc)
          ((equal? msg 'ease-ab) ease-ab)
          ((equal? msg 'low-name) low-name)
          ((equal? msg 'low-desc) low-desc)
          ((equal? msg 'low-ab) low-ab)
          ((equal? msg 'high-name) high-name)
          ((equal? msg 'high-desc) high-desc)
          ((equal? msg 'high-ab) high-ab)
          ((equal? msg 'fix) fix)
          ((equal? msg 'fall) fall)
          ((equal? msg 'bleed?) (equal? kind 'bleed))
          ((equal? msg 'notes-in) (collect-notes notes (cadr args) (caddr args)))
          ((equal? msg 'push!) (fix 'advance! (cadr args)))
          ((equal? msg 'current) (fix 'current))
          ((equal? msg 'held?) (fix 'full?))
          ((equal? msg 'tick-fall!) (if fall (fall 'advance! -1) #f))
          ((equal? msg 'buy-time!) (if fall (fall 'advance! (cadr args)) #f))
          ((equal? msg 'urgent?) (if fall (<= (fall 'current) 1) #f))
          ((equal? msg 'landed?) (if fall (fall 'empty?) #f))
          ;; 「这一回合先压住了」——只管到本回合末，不推进任何进度。
          ((equal? msg 'eased?) eased?)
          ((equal? msg 'ease!) (set! eased? #t))
          ((equal? msg 'clear-ease!) (set! eased? #f))
          (#t (error "首演之夜：危机对象收到未知消息")))))))

;; ============================================================
;; 四处危机
;; ============================================================

;; 中央那两处永远是你自己的骰子——没有人会去她身边。
(define mark-crisis
  (make-crisis "标记被挪过" 'fall "中央" 6 3 (* 2 prep-core)
    "" "" 'knowledge
    "刹住吊杆" "侧台那排刹车够得到，一道一道锁住它" 'knowledge
    "冲上台" "当众冲上去把她拽开。三百个人看着你" 'violence
    "她按走位标记站定，而那组吊杆正冲着那个位置降"
    (list (list 2 "你摸到侧台那排刹车——她脚下那个标记是新粘的")
          (list 4 "旧胶印还在半尺外。有人把她的位置挪过来了")
          (list 6 "吊杆锁死在半空。她还站在原地把那一句唱完了"))))

(define smoke-crisis
  (make-crisis "烟灌进看台" 'bleed "内环" 6 0 0
    "先挡一挡" "让引座的把人往侧道带，撑过这一段" 'social
    "一排排带" "一排一排往七号门引，不让任何人跑起来" 'social
    "掀开边门" "把封着的边门整扇掀掉，烟走得快，人也看见了" 'violence
    "台底那个口封了一年，今晚是活的。烟漫过第一排"
    (list (list 2 "第一排被你带到了侧道上")
          (list 4 "七号门推开了，烟开始往外走")
          (list 6 "看台重新看得见人脸。还有人咳，但没有人在跑"))))

(define blackout-crisis
  (make-crisis "半个场子黑着" 'bleed "外圈" 8 0 (* 2 outer-steps)
    "点应急灯" "把环廊的马灯都提出来，先有个亮" 'knowledge
    "顺线查那路" "顺着线找烧断的那一路，甩掉它再合闸" 'knowledge
    "硬合闸" "不查了，摸黑把整排闸刀推上去" 'violence
    "烟熏进后廊的配电箱，半边灯跳了闸"
    (list (list 2 "你摸到配电箱，铜片上一层焦")
          (list 4 "烧断的那一路被你甩了出去")
          (list 6 "环廊的壁灯一盏一盏回来了")
          (list 8 "场子重新亮起来。台上台下都看得见了"))))

(define rig-crisis
  (make-crisis "顶灯环松了" 'fall "中央" 12 4 0
    "" "" 'violence
    "一根根绞" "一根钢索一根钢索地重新吃上力" 'violence
    "爬上去解扣" "上灯桥，在窄梁上把被别住的棘齿解开" 'sharpness
    "整整一圈灯架挂在半空，一格一格往下沉"
    (list (list 2 "你爬上灯桥，绞盘的棘齿被人别过")
          (list 4 "第一根钢索重新咬住了")
          (list 6 "灯环停了一下，又沉下去半寸")
          (list 8 "第二根上去了。你的手在抖")
          (list 10 "三根钢索都吃上了力")
          (list 12 "灯环绞回了原位，锁扣咔一声合上"))))

;; ============================================================
;; 场上状态
;; ============================================================

(define finished? #f)
(define turn 1)
(define act 1)
;; 第一幕开局就在板上的两处。中央满格＝台底那个口你封死了，烟就不上板。
(define active
  (append (list mark-crisis)
          (if smoke-cancelled? '() (list smoke-crisis))))
(define held-ids '())        ; 你按住的
(define landed-ids '())      ; 砸下来的
(define her-hurt? #f)        ; 她被砸着了（吊杆落地 / 灯环砸在她身上）
(define she-stands? #f)      ; 第三幕她走回了舞台正中
(define usher-quit? #f)      ; 领班撂挑子了

(define show-clk
  (make-clock "演出倒计时" 6 'countdown
    "撑到最后一个音，两根临界钟都没满，就算完成。你不需要清空这块板。"))
(show-clk 'set! 6)

(define (held? id) (member? id held-ids))
(define (landed? id) (member? id landed-ids))

(define (drop-active! id)
  (define (walk lst)
    (if (null? lst)
        '()
        (if (equal? ((car lst) 'id) id)
            (cdr lst)
            (cons (car lst) (walk (cdr lst))))))
  (set! active (walk active)))

(define (find-active id)
  (define (walk lst)
    (if (null? lst)
        #f
        (if (equal? ((car lst) 'id) id) (car lst) (walk (cdr lst)))))
  (walk active))

(define (active-id? id) (if (find-active id) #t #f))

(define (spawn! c note)
  (set! active (append active (list c)))
  (notify! note))

;; ============================================================
;; 按住与落地
;; ============================================================

(define (hold! c)
  (set! held-ids (cons (c 'id) held-ids))
  (drop-active! (c 'id)))

;; 灯环落地的后果分三档：你给过她暗号 / 她正站在下面 / 台上没人。
;; 落地不是二元的。**你已经推掉的每 4 格，少挨一格压力；推过一半，她不会伤着。**
;; 这样那根很长的钟中间的每一格都算数——绞到第十格和一格没绞，
;; 不该是同一件事。原来那两张"让它落得不要紧"的卡就是这条规则的手工版本，
;; 现在它由进度自己算出来。
(define (landing-relief c) (quotient (c 'current) 4))

(define (landing-damage c base)
  (max 1 (- base (landing-relief c))))

(define (landing-spared? c)
  (>= (* 2 (c 'current)) ((c 'fix) 'max)))

(define (rig-land!)
  (push-stage! (landing-damage rig-crisis 4))
  (cond
    ((and she-stands? (landing-spared? rig-crisis))
     (spotlight! "她还站着"
       (string-append
         "你绞住的那几根钢索把它拽偏了。灯环塌了半边，砸在她身后三尺的地方。"
         "短暂的黑，全场寂静。灯重新亮起来，她还站着，把中断的那一句接了下去。"
         "台下以为这是编排好的——掌声比预定的谢幕长了很久。")))
    (she-stands?
     (set! her-hurt? #t)
     (notify! "灯环整个砸在舞台正中。她是被人从底下抬出去的。"))
    ((landing-spared? rig-crisis)
     (notify! "灯环塌了半边，砸在空台边上。"))
    (#t
     (notify! "灯环整个砸在空台上，火星溅了半个前排。"))))

(define (land! c)
  (set! landed-ids (cons (c 'id) landed-ids))
  (drop-active! (c 'id))
  (cond
    ((equal? (c 'id) "标记被挪过")
     (push-stage! (landing-damage c 3))
     (spend-actor-composure! 'player 1)
     (if (landing-spared? c)
         (notify! "吊杆擦着她的肩膀砸在台板上，木屑溅起来。她没停。")
         (begin
           (set! her-hurt? #t)
           (notify! "那组吊杆整个砸下来。她被压在下面。"))))
    ((equal? (c 'id) "顶灯环松了") (rig-land!))
    (#t #f)))

;; ============================================================
;; 危机卡
;; ============================================================

(define (clocks-of c)
  (if (c 'fall)
      (list ((c 'fix) 'render-data) ((c 'fall) 'render-data))
      (list ((c 'fix) 'render-data))))

(define (say-notes! lst)
  (if (null? lst)
      #t
      (begin (result-note! (car lst)) (say-notes! (cdr lst)))))

(define (push-crisis! c n)
  (let ((before (c 'current)))
    (c 'push! n)
    (say-notes! (c 'notes-in before (c 'current)))
    (if (c 'held?) (hold! c) #f)))

(define (crisis-tag c)
  (cond
    ((c 'eased?) (list "这阵压住"))
    ((c 'urgent?) (list "来不及了"))
    ((c 'bleed?) (list "一直在流"))
    (#t '())))

;; 每一处危机是一个 container，里面是处理它的几种办法。
;;
;;   临时（只有放血型有）—— 难度带正修正、失手不额外收费，是**差骰子的去处**。
;;       一格进度也不推，只买下这一回合：这一回合它不流。一回合只买得到一次。
;;   低风险 —— 慢（中 +1 / 好 +2），失手扣你自己的冷静。
;;   高风险 —— 快（中 +2 / 好 +3），失手**照样扣冷静，还额外推安危钟一格**。
;;       它不是"把代价换个地方付"，是"多付一份"。所以它在场子还宽松的时候
;;       只是多疼一点，在钟快满的时候可能直接把这一晚断送掉。
;;       同一张卡，价钱随局面变。
;;
;; **倒坠型没有临时。**往下掉的东西你拖不住：在它落地之前弄好，
;; 或者接受它会落地然后去想办法让它落得不要紧（第三幕灯环的另外三条路）。
;; 给倒坠也配一个临时，拖就永远比修便宜，那根很长的钟就没人去推了。

(define (ease-modifier) (list (modifier 2 "只顶这一会儿")))

(define (ease-node c)
  (node (c 'ease-name)
    :subtitle (string-append (c 'ease-desc) " · " (ability-label (c 'ease-ab)))
    :tags (list "临时")
    :requires (list (req-die))
    :resolve (roll (c 'ease-ab)
      (lambda () (ease-modifier))
      (outcome "没顶住" (lambda () #t))
      (outcome "顶住这阵"
        (lambda () (c 'ease!) (result-note! "这一回合它不流")))
      (outcome "多争了一会儿"
        (lambda ()
          (c 'ease!)
          (push-stage! -1)
          (result-note! "场面松了一口气"))))))

(define (low-node c)
  (node (c 'low-name)
    :subtitle (string-append (c 'low-desc) " · " (ability-label (c 'low-ab)))
    :tags (list "低风险")
    :clocks (list ((c 'fix) 'render-data))
    :requires (list (req-die))
    :resolve (roll (c 'low-ab)
      (outcome "没按住" (lambda () (spend-composure! 1)))
      (outcome "按住一点" (lambda () (push-crisis! c 1)))
      (outcome "按下去了" (lambda () (push-crisis! c 2))))))

(define (high-node c)
  (node (c 'high-name)
    :subtitle (string-append (c 'high-desc) " · " (ability-label (c 'high-ab)))
    :tags (list "高风险")
    :clocks (list ((c 'fix) 'render-data))
    :requires (list (req-die))
    :resolve (roll (c 'high-ab)
      (outcome "反倒更险了"
        (lambda () (spend-composure! 1) (push-stage! 1)))
      (outcome "推开一截" (lambda () (push-crisis! c 2)))
      (outcome "一下子推开" (lambda () (push-crisis! c 3))))))

(define (crisis-node c)
  (node (c 'id)
    :subtitle (c 'desc)
    :tags (crisis-tag c)
    :clocks (clocks-of c)
    :children
    (append
      (if (and (c 'bleed?) (not (c 'eased?))) (list (ease-node c)) '())
      (list (low-node c) (high-node c)))))

;; ============================================================
;; 副目标：二层环廊上的人
;; ============================================================
;;
;; 一根**累计**的钟，但它不是常驻的——它只在门开着的那一回合出现在板上。
;; 门随机开两次：第一次第 2 或第 3 回合，之后隔两三回合他再露一面，
;; 每次只开一个回合。你可以往里投任意多颗骰，凑不满就下次接着凑；
;; 两次窗口都过去还没满，他就走了，今晚再没有第二个机会。
;;
;; 做成累计钟而不是一次判定：一次判定要么中要么不中，玩家没得盘算；
;; 累计钟让"这一回合我抽几颗骰给他"变成一个真的决定，
;; 而窗口的稀缺保证它永远和救场抢同一颗骰。
;;
;; 它不碰压力钟。全场唯一不影响输赢的东西，也是唯一保存到第二章的东西。

(define shadow-clk
  (make-clock "那个背影" 5 'segments
    "有人喊看见莱恩了。一个穿着他那件外套的男人从侧门闪过去。凑满就是你追上了他。"))

(define door1-turn (random-choice (list 2 3)))
(define door2-turn (+ door1-turn (random-choice (list 2 3))))
(define door-open? #f)
(define shadow-gone? #f)

(define (shadow-done?) (shadow-clk 'full?))

(define (push-shadow! n)
  (let ((before (shadow-clk 'current)))
    (shadow-clk 'advance! n)
    (if (and (>= (shadow-clk 'current) 3) (< before 3))
        (result-note! "那件外套的袖口是新的")
        #f)
    (if (shadow-done?)
        (begin
          (set! door-open? #f)
          (result-note! "外套是准备好的。那个人不是莱恩"))
        #f)))

(define (shadow-node)
  (node (if (= turn door1-turn) "追那个人" "堵侧门")
    :subtitle (if (= turn door1-turn)
                  "有人喊那是莱恩。他往侧门去了 · 敏锐"
                  "他要从后廊那一头出去 · 敏锐")
    :tags (list "只此一回合")
    :clocks (list (shadow-clk 'render-data))
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "跟丢了" (lambda () #t))
      (outcome "看清一点" (lambda () (push-shadow! 1)))
      (outcome "追近了" (lambda () (push-shadow! 2))))))

(define (door-nodes)
  (if (and door-open? (not (shadow-done?)) (not shadow-gone?))
      (list (shadow-node))
      '()))

(define (open-doors!)
  (set! door-open? #f)
  (if (or (shadow-done?) shadow-gone?)
      #f
      (if (= turn door1-turn)
          (begin
            (set! door-open? #t)
            (play-remote-banter!
              (line "领班" "有人看见他了——那件外套，是莱恩。")
              (line "世界" "一个男人贴着侧门往后廊去了。")))
          (if (= turn door2-turn)
              (begin
                (set! door-open? #t)
                (play-remote-banter!
                  (line "世界" "那件外套又晃过一次，这回在后廊那一头。")))
              #f))))

;; 窗口只开一个回合。第二扇也关上还没凑满，他今晚就走了。
(define (close-doors!)
  (if (and door-open? (not (shadow-done?)))
      (begin
        (set! door-open? #f)
        (if (>= turn door2-turn)
            (begin
              (set! shadow-gone? #t)
              (notify! "那件外套混进散场的人里走了。你连他的脸都没看清。"))
            (notify! "他闪进侧门，不见了。")))
      #f))

;; ============================================================
;; 人手：每回合替你按一处，自己挑，挑定不换
;; ============================================================

(define aide-jobs '())   ; ((名字 危机id) ...)

(define (job-of name)
  (define (walk lst)
    (if (null? lst)
        #f
        (if (equal? (car (car lst)) name) (cadr (car lst)) (walk (cdr lst)))))
  (walk aide-jobs))

(define (clear-job entries name)
  (if (null? entries)
      '()
      (if (equal? (car (car entries)) name)
          (clear-job (cdr entries) name)
          (cons (car entries) (clear-job (cdr entries) name)))))

(define (set-job! name id)
  (set! aide-jobs (cons (list name id) (clear-job aide-jobs name))))

;; 没有人会去中央。她身边那几处永远是你自己的骰子。
(define (aide-workable)
  (define (walk lst)
    (if (null? lst)
        '()
        (if (equal? ((car lst) 'ring) "中央")
            (walk (cdr lst))
            (cons ((car lst) 'id) (walk (cdr lst))))))
  (walk active))

(define (aide-turn! name text)
  (let ((current (job-of name)))
    (let ((id (if (and current (active-id? current))
                  current
                  (let ((pool (aide-workable)))
                    (if (null? pool) #f (random-choice pool))))))
      (if id
          (begin
            (set-job! name id)
            (play-remote-banter! (line name text))
            (push-crisis! (find-active id) 1))
          #f))))

(define (aides-turn!)
  (if aide-joe (aide-turn! "乔" "这一头交给我，你别回头。") #f)
  (if aide-frank (aide-turn! "弗兰克的人" "后廊这段站满了，谁也过不去。") #f)
  (if aide-police (aide-turn! "警察" "按程序来，先清这一段。") #f)
  (if (and aide-usher (not usher-quit?)) (aide-turn! "领班" "这一排从七号门出去。") #f)
  ;; 一旦有人推他，他就不干了。
  (if (and aide-usher (not usher-quit?) (>= (stage-clk 'current) 6))
      (begin
        (set! usher-quit? #t)
        (play-remote-banter! (line "领班" "我不管了。我也是花钱雇来的。")))
      #f))

;; ============================================================
;; 回合推进
;; ============================================================

(define (bleed!)
  (define (walk lst)
    (if (null? lst)
        #t
        (begin
          (if (and ((car lst) 'bleed?) (not ((car lst) 'eased?)))
              (push-stage! 1)
              #f)
          (walk (cdr lst)))))
  (walk active))

(define (clear-eases!)
  (define (walk lst)
    (if (null? lst)
        #t
        (begin ((car lst) 'clear-ease!) (walk (cdr lst)))))
  (walk active))

(define (fall-tick!)
  (define (walk lst)
    (if (null? lst)
        #t
        (begin
          ((car lst) 'tick-fall!)
          (walk (cdr lst)))))
  (walk active)
  (define (sweep lst)
    (if (null? lst)
        #t
        (begin
          (if ((car lst) 'landed?) (land! (car lst)) #f)
          (sweep (cdr lst)))))
  (sweep active))

;; 内部麦里舞台监督一直在让她下来，她一直不肯。这几句不挂任何数值——
;; 它们不是机制的皮，是她这个人：她等了六年，谁也别想让她在今晚下台。
(define mic-lines
  (list
    (list "夜莺，下来。" "我没事。")
    (list "上面还挂着东西。" "我知道。")
    (list "经理说停。" "让他说。")
    (list "这不是逞强的时候。" "让他们看着。")
    (list "求你了。" "还有两段。")
    (list "……" "我唱完这一段。")))

(define (play-mic! n)
  (if (and (>= n 1) (<= n (length mic-lines)))
      (let ((pair (list-ref mic-lines (- n 1))))
        (play-remote-banter!
          (line "舞台监督" (car pair))
          (line "夜莺" (cadr pair))))
      #f))

(define (on-encounter-enter) (play-mic! 1))

(define (enter-act-two!)
  (set! act 2)
  (spotlight! "第二段"
    (string-append
      "乐队接了下去，她在台面下把那一句唱完了。"
      "配电箱那边冒出一股焦味，半边灯跳了。"
      "抬头看，灯桥上有人刚下来。"))
  (if blackout-cancelled?
      (notify! "后廊的配电箱你清过。那一路电撑住了。")
      (spawn! blackout-crisis "半个场子黑了。台下开始站起来找门。"))
  (spawn! rig-crisis "整整一圈灯架松了，一格一格往下沉。"))

;; 让到三格以上，她伤着也会自己走回去。这就是那几次让步的兑现——
;; 演出因此更可能完整，而她也因此在台上多站了整整一段。
(define (she-insists?) (>= her-night 3))

(define (enter-act-three!)
  (set! act 3)
  (if (and her-hurt? (not (she-insists?)))
      (spotlight! "第三段"
        "台上没有人。乐队把那一段又弹了一遍，然后停了。")
      (begin
        (set! she-stands? #t)
        (spotlight! "她走回中心"
          (string-append
            (if her-hurt?
                "有人要扶她下去，她把手推开了。"
                "她从台面下爬出来，整了整衣服，")
            "一步一步走回舞台正中，把断掉的那一段接了下去。台下第一次安静。"
            "——而那一圈灯架就在她头顶。")))))

(define-turn-rule "演出往下走"
  (lambda () (not finished?))
  (lambda ()
    (close-doors!)
    (aides-turn!)
    (if finished?
        #f
        (begin
          (bleed!)
          (fall-tick!)
          (clear-eases!)
          (show-clk 'advance! -1)
          (if (collapsed?)
              (finish!)
              (begin
                (set! turn (+ turn 1))
                (if (> turn 6)
                    (finish!)
                    (begin
                      (if (= turn 3) (enter-act-two!) #f)
                      (if (= turn 5) (enter-act-three!) #f)
                      (play-mic! turn)
                      (open-doors!)))))))))

;; ============================================================
;; 结算
;; ============================================================
;;
;; 只有一件事保存到第二章：二层那个人。其余的一切——她伤没伤、
;; 演出撑到哪一步、灯环掉在哪儿——都在这一晚的收场文本里交代完。
;; ('首演-她受伤 是同一晚的收场文本要读的，不跨章。)

(define (show-level)
  (cond
    ((collapsed?) "她出事了")
    ((and she-stands? (not her-hurt?)) "完整谢幕")
    (she-stands? "勉强收尾")
    (#t "演出中断")))

(define (closing-text)
  (string-append
    (cond
      ((stage-clk 'full?)
       "最后是从台上把她抬下来的。乐队还没停，台下已经站起来一半。")
      ((and she-stands? (landed? "顶灯环松了"))
       "灯环塌了半边，砸在她身后三尺的地方。灯回来的时候她还站着，把最后一句唱完了。")
      (her-hurt?
       "她是被人从台上抬下去的。乐队等到最后收了乐器。")
      (she-stands?
       "她站回原来的位置，把中断的那一段接了下去。")
      (#t "台上一直空着。乐队等到最后收了乐器。"))
    (cond
      ((and (equal? (show-level) "完整谢幕") encore-cancelled?)
       "没有谢幕。灯一暗她就从后廊出去了，掌声追着她，没追上。")
      ((equal? (show-level) "完整谢幕")
       "谢幕的灯只打她一个人。掌声比预定的长了很久，长到乐队都开始互相看。")
      ((equal? (show-level) "勉强收尾") "掌声稀稀落落，但她站到了最后一个音。")
      (#t "台下的人已经走了大半。"))
    (if (shadow-done?)
        "你在后廊追上了那件外套。他挣脱了，可你看清了：外套是新的，袖口一点磨损都没有——那不是莱恩，那是有人替莱恩准备的一件衣服。"
        "那件外套混在散场的人里走脱了。所有人都说那是莱恩，你也没有话反驳。")))

(define (finish!)
  (if finished?
      #f
      (begin
        (set! finished? #t)
        (set-global! '首演-她受伤 (or her-hurt? (collapsed?)))
        (set-global! '首演-认出黑衣人 (shadow-done?))
        (spotlight! (cond
                      ((collapsed?) "首演之夜：她出事了")
                      (her-hurt? "首演之夜：她被抬下去了")
                      (she-stands? "首演之夜：她唱完了")
                      (#t "首演之夜：台上空着"))
                    (closing-text))
        (end-encounter 'done))))

;; ============================================================
;; 渲染
;; ============================================================

(define (situation-text)
  (string-append
    (cond
      ((= act 1) "舞台升起的时候一声巨响，灯灭了半边，烟从台底涌上来。她自己从台面爬了出来，按标记走回中央——那个标记不在原来的位置。")
      ((= act 2) "烟漫过第一排，半边灯还黑着。乐队没有停。")
      (#t "她在台上。你在她三步之外，扶着一台还在漏的机器。"))
    "演出没有停——你不安排任何东西，你只有四颗骰子和几处压不完的麻烦。"
    (if (> prep-core 0) "升降台的行程你动过手脚，她升上来的时候没卡住。" "")
    (if (> prep-inner 0) "你清过的那几条疏散路线还撑着。" "")
    (if usher-quit? "领班已经撂挑子了。" "")))

(define (crisis-nodes) (map crisis-node active))

(define (get-render-data)
  ;; 首演发生在剧院内；交锋根节点必须占用场所主点，不能因没有
  ;; Anchor_首演之夜 而退回网格布局。
  (node "首演之夜"
    :anchor "剧院"
    :children
    (append
      (clock-nodes (show-clk 'render-data)
                   (stage-clk 'render-data))
      ;; 场面是站在这儿就看得见的，所以它是标注不是卡：不能点、不吃骰子、不占一格版面。
      (list (note-node "标注：此刻的场面" "" (situation-text)))
      (crisis-nodes)
      (door-nodes))))
