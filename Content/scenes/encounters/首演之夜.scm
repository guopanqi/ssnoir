;; scenes/encounters/首演之夜.scm - 第一章大型交锋「演出必须继续」
;;
;; 设计见 docs/三封信-第一章方案.md §8。这里只说结构。
;;
;; 玩家从头到尾只做一件事：处理问题。演出始终在继续——乐队没停过，
;; 她的声音断了又接上来。演出的推进是看见和听见的（音乐进下一段、灯换一次色、
;; 她从台底站起来走回中心），它给玩家的不是新任务，是新的麻烦，
;; 以及旧麻烦变得更要命。
;;
;;   一条命  夜莺还撑得住 8/10
;;           **它是往下掉的**：开场就缺了两格（今晚从一开始就不干净），
;;           见底 = 她真的出事了；带着还没见底的它撑满六回合就算完成。
;;           它**不自走**——掉下去的每一格都有来处。
;;           **你从来不需要清空这块板。**
;;
;;   平衡基准：**这一晚按"城里五天该做的都做了"来配数字。**
;;   两个漏洞都堵上、拼片查齐，板上的活刚好够你干完——但只是刚好，
;;   每一回合都得放掉点什么。少做一样，缺口就实打实地摊在这六回合里；
;;   什么都没做，是一场你注定要挑"哪一处不救"的仗。
;;   准备**从不**把危机从板上拿掉：拿掉一张卡就等于拿掉一次分诊。
;;   它让同一处便宜一半、流得慢一半——而且**在卡面上说出来**：
;;   每一处沾着准备的危机都带一张便签和一句话，说你那五天对它做过什么，
;;   或者没做什么。起手格数是给数字看的，便签是给玩家看的。
;;
;;   每一处危机是一个 container，里面是处理它的几种办法：
;;     顶一会儿（只有放血型有）一格进度也不推，只买下这一回合：这一回合它不扣她。
;;                         判定和别的动作一样，没有额外修正——它不是"更容易的选项"，
;;                         是**换一种收益**：拿这一颗骰换一回合她不掉格，而不是换进度。
;;     低风险             慢（中 +1 / 好 +2），失手扣你自己的冷静。
;;     高风险             快（中 +2 / 好 +3），失手扣冷静**并且**再扣她一格。
;;
;;   两种危机模式，差别只在"不管它会怎样"：
;;     放血  在板上一回合就从她那条上扣一格，不会落地。可以先忍着。
;;     倒坠  自走钟走空就落地，砸在两处：场子和她。**倒坠没有"顶一会儿"。**
;;           往下掉的东西你拖不住——给它配一个拖延，拖就永远比修便宜。
;;
;;   同一处危机的几个动作**吃不同的能力**——一幕两处就摊开了三种，
;;   所以"我这几颗骰是什么"和"我先救哪一处"是两个独立的问题。
;;
;;              顶一会儿        低风险            高风险
;;   吊杆        —              刹住吊杆 见识     拽住配重绳 力量
;;   烟          先挡一挡 交际   一排排带 交际     掀开边门 力量
;;   黑场        点应急灯 见识   顺线查那路 见识   硬合闸 力量
;;   灯环        —              一根根绞 力量     爬上去解扣 敏锐
;;   挤门        喊住前几排 交际 拉住要倒的 敏锐   顶住门框 力量
;;
;;   一处新麻烦几乎每回合都到，因为四颗骰一回合就能清掉一处。
;;   板上常驻三处以上，你才真的在选"先救哪一处"。
;;
;;   一回合  吊杆冲着她降     倒坠  砸下来 3 · 弄好 0/12  中央
;;   一回合  烟灌进看台       放血  弄好 0/12            内环
;;   二回合  半个场子黑着     放血  弄好 0/14            外圈
;;   三回合  顶上那圈灯架松了 倒坠  砸下来 4 · 弄好 0/20  中央
;;   四回合  人往七号门挤     放血  弄好 0/8             内环
;;           ——次生的：只在烟或黑场那时候还挂在板上才来。
;;           前面按住了，它就不发生；前面拖着，它替你把账算清。
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
;;   副目标  追上他 0/8。一根**累计**的钟，但只在门开着的那几回合
;;           出现在板上——随机开两次，每次两回合。凑不满就下次接着凑，
;;           两次都过去还没满，他今晚就走了。
;;           八格意味着它要吃掉三四颗骰：它必须贵到和救场真的抢手。
;;           不碰夜莺那条；这是全场唯一保存到第二章的东西。
;;
;; 对外契约：以 'done 结束（成败不是二元的）。
;; 城市输入（只在顶部读取一次）：两个漏洞各在哪一档、查到几片，以及请到了谁。
;; 写回全局：'首演-她受伤（本章收场文本要读）、'首演-认出黑衣人（第二章要读）。

;; ============================================================
;; 城市输入
;; ============================================================

(define (prep-of key)
  (let ((v (get-global key)))
    (if (number? v) v 0)))

;; 城市侧这五天做的事，进到这里就是调查与修复。
;;
;; 一、两个漏洞。各有三档：0＝你根本不知道它 1＝你查到了但没动它 2＝你堵上了。
;;    堵上＝那处今晚照样出事，但它已经被你削掉一半，而且流得慢一半；
;;    只是知道＝你摸清了它长什么样，一上手快一截，可它照原样流。
;;    「知道」和「堵上」不是同一件事，这是这一节唯一要求玩家分辨的东西。
;;    两样都不做，这两处就是满格、每回合都在推夜莺那条——那才是难的来处。
;; 二、莱恩调查 0..3。你查得越完整，越早知道有人正在借他的名字行动；
;;    那件外套出现时，「追上他」从对应格数开始。
(define hole-vent (prep-of '漏洞-通风口))    ; 0 未知 / 1 已知 / 2 已堵
(define hole-power (prep-of '漏洞-配电箱))   ; 0 未知 / 1 已知 / 2 已堵
(define pieces (prep-of '调查-拼片))         ; 0..3

;; 堵上＝一半的活已经干完了；只是知道＝四分之一。
(define (hole-head-start hole full)
  (cond ((>= hole 2) (quotient full 2))
        ((= hole 1) (quotient full 4))
        (#t 0)))

;; 堵上的那一处流得慢一半：隔一回合才推夜莺那条一格。
(define (hole-bleed-every hole) (if (>= hole 2) 2 1))

;; 准备不能只活在起手格数里——玩家要在卡面上看见自己那五天干了什么，
;; 也要看见自己没干什么。所以三档各有各的便签和各自的一句话。
(define (hole-tags hole sealed known unknown)
  (cond ((>= hole 2) (list sealed))
        ((= hole 1) (list known))
        (#t (list unknown))))

;; 正式演出包含固定谢幕；玩家要争取的是让她安全走到那里。
(define stage-max 10)

;; ============================================================
;; 一条命：夜莺还撑得住
;; ============================================================
;;
;; 这一晚要保的不是演出，是她。所有的危险最后都折算到同一个地方——
;; 东西掉下来、线路烧着、烟漫过台口、你自己冒险搞出来的乱子，
;; 都是从这条上面扣掉一格。见底就是她真的出事了。
;;
;; **它像一条命，从满往下掉，不是从空往上填。**这一晚玩家要守的是一个
;; 正在变少的东西，而不是要避免填满一个仪表——同一套数字，读起来完全不同：
;; "还剩四格"是你随时算得出的余地，"已经六格"要在心里再减一次。
;; 样式仍是 gauge：engine.scm 里 gauge 本来就不含方向，生命值从满打到 0 也是它；
;; countdown 是留给「在逼近你的时间」的，而这条的每一格都是你自己或场子扣的。
;;
;; **它不自走。**掉下去的每一格都有来处：某处放血、某次高风险失手、某处落地。

(define stage-clk
  (make-clock "夜莺还撑得住" stage-max 'gauge
    "东西掉下来、线路烧着、烟漫到台口，都从这条上扣。见底就是她真的出事了。"))
;; 开场就缺两格：幕还没开，这一晚已经不干净了。
(stage-clk 'set! (- stage-max 2))

;; n 是扣掉几格。整场只有这一个入口，所以"她还剩多少"永远只有一处会动。
(define (hurt-her! n) (stage-clk 'advance! (- n)))
(define (relieve-her! n) (stage-clk 'advance! n))

(define (collapsed?) (stage-clk 'empty?))

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

(define (make-crisis id kind ring fix-need fall-need head-start bleed-every
                     land-base prep-tags prep-note
                     ease-name ease-desc ease-ab
                     low-name low-desc low-ab
                     high-name high-desc high-ab
                     desc notes)
  (let ((fix (make-clock "弄好了多少" fix-need 'gauge desc))
        (fall (if (> fall-need 0)
                  (make-clock "砸下来" fall-need 'countdown
                              "还剩这几回合它就掉下来了。")
                  #f))
        (eased? #f)
        (bleed-count 0))
    (if (> head-start 0) (fix 'set! head-start) #f)
    (if fall (fall 'set! fall-need) #f)
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'id) id)
          ((equal? msg 'kind) kind)
          ((equal? msg 'ring) ring)
          ((equal? msg 'desc) desc)
          ;; 城里那五天在这张卡上长什么样。钟的起手格数是数字，这两样是玩家看得见的。
          ((equal? msg 'prep-tags) prep-tags)
          ((equal? msg 'prep-note) prep-note)
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
          ((equal? msg 'bleed-every) bleed-every)
          ((equal? msg 'land-base) land-base)
          ;; 这一回合它流不流。堵上过的那一处 bleed-every 是 2，隔一回合才推一格。
          ((equal? msg 'bleed-due!)
           (if (equal? kind 'bleed)
               (begin
                 (set! bleed-count (+ bleed-count 1))
                 (if (>= bleed-count bleed-every)
                     (begin (set! bleed-count 0) #t)
                     #f))
               #f))
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
;; 五处危机
;; ============================================================
;;
;; 修复钟的长度是照四颗骰配的：一回合四次行动、一次平均推两格出头，
;; 所以十二格的一处大概要你半场的骰子。做完准备的人把烟和黑场各砍掉一半，
;; 全场的活刚好摊得开；什么都没做的人，同样六回合要干将近两倍的事。

;; 中央那两处永远是你自己的骰子——没有人会去她身边。
(define mark-crisis
  (make-crisis "吊杆冲着她降" 'fall "中央" 12 3 0 1
    3 '() ""
    "" "" 'knowledge
    "刹住吊杆" "侧台那排刹车够得到，一道一道锁住它" 'knowledge
    "拽住配重绳" "从侧台扯住下坠的配重绳，用身体吃住那股力" 'violence
    "她按走位标记站定，而那组吊杆正冲着那个位置降"
    (list (list 2 "你摸到侧台那排刹车——她脚下那个标记是新粘的")
          (list 4 "旧胶印还在半尺外。有人把她的位置挪过来了")
          (list 6 "第一道刹车咬住了，吊杆慢下来一点")
          (list 8 "第二道也锁上了。它还在动，只是没那么快")
          (list 10 "配重绳缠上立柱，你整个人吊在上面")
          (list 12 "吊杆锁死在半空。她还站在原地把那一句唱完了"))))

(define smoke-crisis
  (make-crisis "烟灌进看台" 'bleed "内环" 12 0
    (hole-head-start hole-vent 12) (hole-bleed-every hole-vent) 0
    (hole-tags hole-vent "台底你封过" "台底你查过" "台底你没碰过")
    (cond ((>= hole-vent 2) "。你上周封过一层铁皮，烟走得慢了，但没堵死")
          ((= hole-vent 1) "。你下去看过，知道它通到哪儿、该把人往哪条侧道带")
          (#t "。台底哪条道通哪儿，你今晚现摸"))
    "先挡一挡" "让引座的把人往侧道带，撑过这一段" 'social
    "一排排往外带" "一排一排往七号门引，不让任何人跑起来" 'social
    "掀开边门" "把封着的边门整扇掀掉，烟走得快，人也看见了" 'violence
    "台底那个口封了一年，今晚是活的。烟漫过第一排"
    (list (list 2 "第一排被你带到了侧道上")
          (list 4 "引座的接手了前半边，没有人跑起来")
          (list 6 "七号门推开了，烟开始往外走")
          (list 8 "边门也掀了，两股风对上，烟压到台口以下")
          (list 10 "后半边看台空了出来")
          (list 12 "看台重新看得见人脸。还有人咳，但没有人在跑"))))

(define blackout-crisis
  (make-crisis "半个场子黑着" 'bleed "外圈" 14 0
    (hole-head-start hole-power 14) (hole-bleed-every hole-power) 0
    (hole-tags hole-power "配电箱你换过" "配电箱你查过" "配电箱你没碰过")
    (cond ((>= hole-power 2) "。你换过后廊那只箱子，烧的是旁边那一路，没全丢")
          ((= hole-power 1) "。你开过那只箱子，记得哪一路接哪一排灯")
          (#t "。这箱子里哪根线通哪儿，你得摸黑现认"))
    "点应急灯" "把环廊的马灯都提出来，先有个亮" 'knowledge
    "顺着线找" "顺着线找烧断的那一路，甩掉它再合闸" 'knowledge
    "硬合闸" "不查了，摸黑把整排闸刀推上去" 'violence
    "烟熏进后廊的配电箱，半边灯跳了闸"
    (list (list 2 "环廊的马灯提出来了，先有个亮")
          (list 4 "你摸到配电箱，铜片上一层焦")
          (list 6 "烧断的那一路被你甩了出去")
          (list 8 "第一排闸刀推上去，后廊亮了")
          (list 10 "环廊的壁灯一盏一盏回来了")
          (list 12 "台口的脚灯也回来了")
          (list 14 "场子重新亮起来。台上台下都看得见了"))))

(define rig-crisis
  (make-crisis "顶上那圈灯架松了" 'fall "中央" 20 4 0 1
    4 '() ""
    "" "" 'violence
    "一根根绞回去" "一根钢索一根钢索地重新吃上力" 'violence
    "爬上去解扣" "上灯桥，在窄梁上把被别住的棘齿解开" 'sharpness
    "整整一圈灯架挂在半空，一格一格往下沉"
    (list (list 2 "你爬上灯桥，绞盘的棘齿被人别过")
          (list 4 "第一根钢索重新咬住了")
          (list 6 "灯环停了一下，又沉下去半寸")
          (list 8 "第二根上去了。你的手在抖")
          (list 10 "三根钢索都吃上了力。它不再往下走")
          (list 12 "你把别住的棘齿一颗一颗解开")
          (list 14 "第四根钢索绞回了槽里")
          (list 16 "灯环被抬起来半寸")
          (list 18 "只剩最后那道锁扣")
          (list 20 "灯环绞回了原位，锁扣咔一声合上"))))

;; 次生的一处：它不是又一场事故，是前面两处没按住的账。
;; 烟或黑场到第四回合还挂在板上，人就往七号门挤。
(define crush-crisis
  (make-crisis "人往七号门挤" 'bleed "内环" 8 0 0 1
    0 (list "前面拖出来的") "。烟和黑没按住，人就自己找门去了"
    "喊住前几排" "站到栏杆上喊，让前面几排先别动" 'social
    "拉住要倒的" "从人流里挑出快站不住的那个，先把他拽出来" 'sharpness
    "顶住门框" "整个人卡进门框，把那股人流劈成两股" 'violence
    "七号门口挤成一团，有人已经站不住了"
    (list (list 2 "你把要倒的那个人从人流里拽了出来")
          (list 4 "门口那股挤压松开了一线")
          (list 6 "人流被劈成两股，一股走边门")
          (list 8 "队排起来了。没有人再往前顶"))))

;; ============================================================
;; 场上状态
;; ============================================================

(define finished? #f)
(define turn 1)
(define act 1)
;; 开局就在板上的两处。堵没堵过那个口，烟都在这儿——
;; 堵过的话它只剩一半，而且隔一回合才流。
(define active (list mark-crisis smoke-crisis))
(define held-ids '())        ; 你按住的
(define landed-ids '())      ; 砸下来的
(define her-hurt? #f)        ; 她被砸着了（吊杆落地 / 灯环砸在她身上）
(define she-stands? #f)      ; 第三幕她走回了舞台正中

(define show-clk
  (make-clock "唱完" 6 'countdown
    "她还要唱这几回合。撑到最后一个音，这一晚就算你保住了。"))
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
  (hurt-her! (landing-damage rig-crisis (rig-crisis 'land-base)))
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
    ((equal? (c 'id) "吊杆冲着她降")
     (hurt-her! (landing-damage c (c 'land-base)))
     (spend-actor-composure! 'player 2)
     (if (landing-spared? c)
         (notify! "吊杆擦着她的肩膀砸在台板上，木屑溅起来。她没停。")
         (begin
           (set! her-hurt? #t)
           (notify! "那组吊杆整个砸下来。她被压在下面。"))))
    ((equal? (c 'id) "顶上那圈灯架松了") (rig-land!))
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
    ((c 'eased?) (list "这回合压住了"))
    ((c 'urgent?) (list "马上就要砸下来"))
    ;; 「它一直在扣她」已经写在副标题的第一句里，不必再挂一张牌重复一遍。
    (#t '())))

;; 每一处危机是一个 container，里面是处理它的几种办法。
;;
;; 副标题的第一件事是**这张卡会把什么加到你身上**——每回合扣一格、
;; 归零就砸下来、成了推几格、失手赔什么。氛围放在效果后面。
;; 玩家在四张卡之间选，比的是效果；效果读不出来，选就是瞎选。
;;
;;   顶一会儿（只有放血型有）—— 一格进度也不推，只买下这一回合：这一回合它不扣她。
;;       **判定没有额外修正。**它不是一条更好过的路，是另一种收益：
;;       同样一颗骰，换一回合她不掉格而不是换进度。一回合只买得到一次。
;;   低风险 —— 慢（中 +1 / 好 +2），失手扣你自己的冷静。
;;   高风险 —— 快（中 +2 / 好 +3），失手**照样扣冷静，还额外扣她一格**。
;;       它不是"把代价换个地方付"，是"多付一份"。所以它在她还剩得多的时候
;;       只是多疼一点，在她快见底的时候可能直接把这一晚断送掉。
;;       同一张卡，价钱随局面变。
;;
;; **倒坠型没有"顶一会儿"。**往下掉的东西你拖不住：在它落地之前弄好，
;; 或者接受它会落地然后让它落得不要紧（推得越满砸得越轻）。
;; 给倒坠也配一个拖延，拖就永远比修便宜，那根很长的钟就没人去推了。

;; 一处危机不管它会怎样——这句话必须写在卡面上，而不是留给玩家推。
(define (crisis-effect c)
  (cond
    ((and (c 'bleed?) (> (c 'bleed-every) 1)) "放着不管，隔一回合扣夜莺 1 格")
    ((c 'bleed?) "放着不管，每回合扣夜莺 1 格")
    (#t (string-append "掉下来最多扣夜莺 "
                       (number->string (c 'land-base))
                       " 格；你弄好得越多，砸得越轻"))))

(define (ease-node c)
  (node (c 'ease-name)
    :subtitle (string-append "这一回合它不扣夜莺，但一点也没弄好 · "
                             (ability-label (c 'ease-ab)) " · " (c 'ease-desc))
    :requires (list (req-die))
    :resolve (roll (c 'ease-ab)
      (outcome "没顶住" (lambda () #t))
      (outcome "顶住了"
        (lambda () (c 'ease!) (result-note! "这一回合它不扣夜莺")))
      (outcome "顶住了，还缓了一口气"
        (lambda ()
          (c 'ease!)
          (relieve-her! 1)
          (result-note! "场面松了一口气"))))))

(define (low-node c)
  (node (c 'low-name)
    :subtitle (string-append "弄好 1–2 格；失手你自己掉 2 点冷静 · "
                             (ability-label (c 'low-ab)) " · " (c 'low-desc))
    :tags (list "稳当")
    :clocks (list ((c 'fix) 'render-data))
    :requires (list (req-die))
    :resolve (roll (c 'low-ab)
      (outcome "没弄成" (lambda () (spend-composure! 2)))
      (outcome "好了一点" (lambda () (push-crisis! c 1)))
      (outcome "好了一截" (lambda () (push-crisis! c 2))))))

(define (high-node c)
  (node (c 'high-name)
    :subtitle (string-append "弄好 2–3 格；失手你掉 2 点冷静，夜莺再扣 1 格 · "
                             (ability-label (c 'high-ab)) " · " (c 'high-desc))
    :tags (list "冒险")
    :clocks (list ((c 'fix) 'render-data))
    :requires (list (req-die))
    :resolve (roll (c 'high-ab)
      (outcome "弄砸了，她更险"
        (lambda () (spend-composure! 2) (hurt-her! 1)))
      (outcome "好了一截" (lambda () (push-crisis! c 2)))
      (outcome "好了一大截" (lambda () (push-crisis! c 3))))))

(define (crisis-node c)
  (node (c 'id)
    :subtitle (string-append (crisis-effect c) " · " (c 'desc) (c 'prep-note))
    :tags (append (crisis-tag c) (c 'prep-tags))
    :clocks (clocks-of c)
    :children
    (append
      (if (and (c 'bleed?) (not (c 'eased?))) (list (ease-node c)) '())
      (list (low-node c) (high-node c)))))

;; ============================================================
;; 副目标：二层环廊上的人
;; ============================================================
;;
;; 一根**累计**的钟，但它不是常驻的——它只在门开着的那几回合出现在板上。
;; 门随机开两次：第一次第 2 或第 3 回合，之后隔两三回合他再露一面，
;; 每次开两个回合。你可以往里投任意多颗骰，凑不满就下次接着凑；
;; 两次窗口都过去还没满，他就走了，今晚再没有第二个机会。
;;
;; 做成累计钟而不是一次判定：一次判定要么中要么不中，玩家没得盘算；
;; 累计钟让"这一回合我抽几颗骰给他"变成一个真的决定，
;; 而窗口的稀缺保证它永远和救场抢同一颗骰。
;;
;; 八格、一次成功推一到两格：它要吃掉三四颗骰。这个价钱是故意的——
;; 便宜的副目标等于没有副目标，玩家会顺手把它做掉，什么都没放弃。
;; 城里查到的每一片拼片直接落成这里的起手格数：你在城里花掉的那一天，
;; 就是今晚少抽出去的那一颗骰。
;;
;; 它不碰夜莺那条。全场唯一不影响输赢的东西，也是唯一保存到第二章的东西。

(define shadow-clk
  (make-clock "追上他" 8 'gauge
    "有人喊看见莱恩了。一个穿着他那件外套的男人从侧门闪过去。填满就是你抓着他了。"))
;; 三处莱恩调查不再替玩家修舞台机械；它让尼尔更早看出冒充者的破绽。
(shadow-clk 'set! pieces)

;; 每扇门开两个回合：door1-turn 与它的下一回合，door2-turn 与它的下一回合。
(define door1-turn (random-choice (list 2 3)))
(define door2-turn (+ door1-turn (random-choice (list 2 3))))
(define shadow-gone? #f)

(define (in-window? t)
  (or (= t door1-turn) (= t (+ door1-turn 1))
      (= t door2-turn) (= t (+ door2-turn 1))))

(define (first-window? t) (< t door2-turn))

;; 他还有几回合就走脱了。这是这件事剩下的时间，所以是一根钟，不是一张便签。
(define (window-left)
  (cond ((= turn door1-turn) 2)
        ((= turn (+ door1-turn 1)) 1)
        ((= turn door2-turn) 2)
        ((= turn (+ door2-turn 1)) 1)
        (#t 0)))

(define (shadow-done?) (shadow-clk 'full?))

(define (push-shadow! n)
  (let ((before (shadow-clk 'current)))
    (shadow-clk 'advance! n)
    (if (and (>= (shadow-clk 'current) 4) (< before 4))
        (result-note! "那件外套的袖口是新的")
        #f)
    (if (shadow-done?)
        (result-note! "外套是准备好的。那个人不是莱恩")
        #f)))

(define window-clk
  (make-clock "跟丢" 2 'countdown
    "再过这几回合他就混进人群里，今晚就找不着了。"))
(window-clk 'set! 2)

;; 城里查莱恩查到的东西，在这张卡上要看得见——不是只体现在钟的起手位置。
(define (shadow-prep-note)
  (cond ((>= pieces 3) "。你翻过他这几天的行踪，知道他根本不在城里")
        ((= pieces 2) "。你翻过他的行踪，那几天对不上")
        ((= pieces 1) "。你听过一嘴：这几天没人真见着莱恩")
        (#t "。你对这个人一无所知")))

(define (shadow-node)
  (node (if (first-window? turn) "追上去" "堵侧门")
    :subtitle (string-append
                (if (first-window? turn)
                    "有人喊那是莱恩。他往侧门去了"
                    "他要从后廊那一头出去")
                (shadow-prep-note) " · 敏锐")
    :clocks (list (window-clk 'render-data) (shadow-clk 'render-data))
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "又被他甩开" (lambda () #t))
      (outcome "近了一点" (lambda () (push-shadow! 1)))
      (outcome "近了一大截" (lambda () (push-shadow! 2))))))

(define (door-nodes)
  (if (and (in-window? turn) (not (shadow-done?)) (not shadow-gone?))
      (list (shadow-node))
      '()))

;; 只在窗口的第一回合说话；第二回合门还开着，但不再重复提醒。
(define (open-doors!)
  (window-clk 'set! (max 1 (window-left)))
  (if (or (shadow-done?) shadow-gone?)
      #f
      (begin
        (if (= turn door1-turn)
            (play-remote-banter!
              (line "领班" "有人看见他了——那件外套，是莱恩。")
              (line "世界" "一个男人贴着侧门往后廊去了。"))
            #f)
        (if (= turn door2-turn)
            (play-remote-banter!
              (line "世界" "那件外套又晃过一次，这回在后廊那一头。"))
            #f))))

;; 窗口在它的第二回合末关上。第二扇也关上还没凑满，他今晚就走了。
(define (close-doors!)
  (if (or (shadow-done?) shadow-gone?)
      #f
      (begin
        (if (= turn (+ door1-turn 1))
            (notify! "他闪进侧门，不见了。")
            #f)
        (if (= turn (+ door2-turn 1))
            (begin
              (set! shadow-gone? #t)
              (notify! "那件外套混进散场的人里走了。你连他的脸都没看清。"))
            #f))))

;; ============================================================
;; 回合推进
;; ============================================================

(define (bleed!)
  (define (walk lst)
    (if (null? lst)
        #t
        (begin
          (if (and (not ((car lst) 'eased?)) ((car lst) 'bleed-due!))
              (hurt-her! 1)
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

;; 一处新麻烦几乎每回合都到。两处一起砸下来玩家只会瘫掉，
;; 一次一处才逼得出「这一颗骰给谁」。
(define (enter-act-two!)
  (set! act 2)
  (spotlight! "第二段"
    (string-append
      "乐队接了下去，她在台面下把那一句唱完了。"
      "配电箱那边冒出一股焦味，半边灯跳了。"))
  (if (>= hole-power 2)
      (notify! "后廊那只配电箱你换过，烧的是旁边那一路。半边灯还是黑了，但没全丢。")
      #f)
  (spawn! blackout-crisis "半个场子黑了。台下开始站起来找门。"))

(define (drop-the-rig!)
  (spotlight! "灯桥上有人"
    "抬头看，灯桥上有人刚下来。那一圈灯架整个松了，一格一格往下沉。")
  (spawn! rig-crisis "整整一圈灯架挂在半空。"))

;; 次生危机只在你确实没按住前面的时候来。
;; 它不是"第四回合的固定事件"，是账单。
(define (maybe-crush!)
  (if (or (active-id? "烟灌进看台") (active-id? "半个场子黑着"))
      (spawn! crush-crisis "看不见路的人都往七号门去了。门口挤成一团。")
      (notify! "七号门那边一直是通的。引座的把人一排排放了出去。")))

(define (enter-act-three!)
  (set! act 3)
  (set! she-stands? #t)
  (spotlight! "她走回中心"
    (string-append
      (if her-hurt?
          "有人要扶她下去，她把手推开了。"
          "她从台面下爬出来，整了整衣服，")
      "一步一步走回舞台正中，把断掉的那一段接了下去。台下第一次安静。"
      "——而那一圈灯架就在她头顶。")))

(define-turn-rule "演出往下走"
  (lambda () (not finished?))
  (lambda ()
    (close-doors!)
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
                      (if (= turn 2) (enter-act-two!) #f)
                      (if (= turn 3) (drop-the-rig!) #f)
                      (if (= turn 4) (maybe-crush!) #f)
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
      ((collapsed?)
       "最后是从台上把她抬下来的。乐队还没停，台下已经站起来一半。")
      ((and she-stands? (landed? "顶上那圈灯架松了"))
       "灯环塌了半边，砸在她身后三尺的地方。灯回来的时候她还站着，把最后一句唱完了。")
      (her-hurt?
       "她是被人从台上抬下去的。乐队等到最后收了乐器。")
      (she-stands?
       "她站回原来的位置，把中断的那一段接了下去。")
      (#t "台上一直空着。乐队等到最后收了乐器。"))
    (cond
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

;; 主线回调只接受 done；倒下把现有的两个结果向量压到最差值，然后仍走同一结案。
(define (on-encounter-collapse)
  (set-global! '首演-她受伤 #t)
  (set-global! '首演-认出黑衣人 #f)
  (collapse-result 'done))

;; ============================================================
;; 渲染
;; ============================================================

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
      (crisis-nodes)
      (door-nodes))))
