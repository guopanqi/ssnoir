;; scenes/encounters/首演之夜.scm - 第一章大型交锋「首演之夜」
;;
;; 设计见 docs/第一章·三封信.md §8。
;;
;; 结构：三个环上各有**一处大麻烦**，每一处都要连着几回合往里投骰子才压得下去。
;; 不是一堆两三格的小事——那样只是清单。玩家要的感觉是自顾不暇：
;; 三处同时烧，每一处都吞骰子，而你只有一个人。
;;
;;   中央 升降台卡在半程 0/8  限 4   —— 没有人手能替你，全靠你自己
;;   内环 台下的恐慌     0/10 限 5   —— 最大的一摊，也是人手最能顶的一摊
;;   外圈 那个人         0/6  限 6   —— 归零就是他够到她，最坏的结果
;;   中央 她要重新站上去 0/4         —— 升降台解开之后才出现，这是你今晚真正要的东西
;;
;; 六回合 × 四颗骰约 28 格产能，四处合计 28 格：账面刚好，期限一错开就不够。
;; 一个人手每回合替他那个环啃一格，六回合就是六格——**没有人帮忙，你压不住全部**。
;; 中央没有人手：保护她这件事，最后一手永远得你自己去。
;;
;; 空间是一座旧圆形剧场（早年办拳赛）：没有侧台、没有幕布、没有后台屏障，
;; 她在正中央，三百六十度都是人。三个环是串起来的：她被挤下台就退到内环，
;; 要走回舞台必须穿过人群；从外圈门进来的人能直接走到她跟前。
;;
;; 对外契约：以 'done 结束（成败不是二元的，四个向量写进 global 由故事解释）。
;; 城市输入（只在顶部读取一次）：三个环的准备格数，以及请到了谁。

;; ============================================================
;; 城市输入
;; ============================================================

(define (prep-of key)
  (let ((v (get-global key)))
    (if (number? v) v 0)))

(define prep-outer (prep-of '准备-外圈))   ; 0..4
(define prep-inner (prep-of '准备-内环))   ; 0..4
(define prep-core (prep-of '准备-中央))    ; 0..2

(define aide-joe (if (get-global '人手-乔) #t #f))
(define aide-frank (if (get-global '人手-弗兰克) #t #f))
(define aide-police (if (get-global '人手-警察) #t #f))
(define aide-usher (if (get-global '人手-领班) #t #f))

(define (ring-prep ring)
  (cond ((equal? ring "外圈") prep-outer)
        ((equal? ring "内环") prep-inner)
        ((equal? ring "中央") prep-core)
        (#t (error "首演之夜：未知的环"))))

;; 准备每满 2 格：那一处开局就已经压下去一格，期限也多撑一个回合。
;; 布置不能替你把事办完，它只是让你没那么晚才动手。
(define (prep-steps ring)
  (let ((p (ring-prep ring)))
    (cond ((>= p 4) 2) ((>= p 2) 1) (#t 0))))

;; ============================================================
;; 一处麻烦
;; ============================================================

(define (ability-label a)
  (cond ((equal? a 'violence) "力量")
        ((equal? a 'social) "交际")
        ((equal? a 'sharpness) "敏锐")
        ((equal? a 'knowledge) "见识")
        (#t (error "首演之夜：未知的能力"))))

;; notes：(list (list 格数 说的话) ...)。一次推两格也不能漏掉中间那句，
;; 所以按区间 (from, to] 取，全部说出来——这根钟的每一格都得是一件具体的事。
(define (collect-notes notes from to)
  (if (null? notes)
      '()
      (let ((n (car (car notes))))
        (if (and (> n from) (<= n to))
            (cons (cadr (car notes)) (collect-notes (cdr notes) from to))
            (collect-notes (cdr notes) from to)))))

(define (make-trouble id ring need limit ability desc cost notes)
  (let ((prog (make-clock id need 'segments desc))
        (lim (if (> limit 0)
                 (make-clock "还剩" (+ limit (prep-steps ring)) 'countdown cost)
                 #f)))
    (prog 'set! (prep-steps ring))
    (if lim (lim 'set! (+ limit (prep-steps ring))) #f)
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'id) id)
          ((equal? msg 'ring) ring)
          ((equal? msg 'ability) ability)
          ((equal? msg 'desc) desc)
          ((equal? msg 'cost) cost)
          ((equal? msg 'progress) prog)
          ((equal? msg 'limit) lim)
          ((equal? msg 'notes-in) (collect-notes notes (cadr args) (caddr args)))
          ((equal? msg 'fill!) (prog 'advance! (cadr args)))
          ((equal? msg 'current) (prog 'current))
          ((equal? msg 'done?) (prog 'full?))
          ((equal? msg 'tick-limit!) (if lim (lim 'advance! -1) #f))
          ((equal? msg 'urgent?) (if lim (<= (lim 'current) 1) #f))
          ((equal? msg 'expired?) (if lim (lim 'empty?) #f))
          (#t (error "首演之夜：麻烦对象收到未知消息")))))))

;; ============================================================
;; 四处麻烦
;; ============================================================

(define lift-trouble
  (make-trouble "升降台卡在半程" "中央" 8 4 'violence
    "她半个人还卡在台面下，齿轮咬住了裙摆。没有人能替你干这个"
    "齿轮又转了半圈才停。她的腿伤了，今晚她不会再站上那个台子。"
    (list (list 2 "齿轮里那截裙摆，你用道具刀割断了")
          (list 4 "手闸在台底左边——你摸到了，台面松了半寸")
          (list 6 "台子撬起半尺，她的肩膀先出来了")
          (list 8 "她自己爬了上来，扶着台沿喘气"))))

(define panic-trouble
  (make-trouble "台下的恐慌" "内环" 10 5 'social
    "前排站上了椅子，环廊最窄那段已经堵死。这是今晚最大的一摊"
    "环廊那一段塌成了一团。有人被踩在台阶上，人群把她一起卷了出去。"
    (list (list 2 "第一排被你按回了椅子上")
          (list 4 "环廊最窄的那段疏开了，摔倒的人被扶起来")
          (list 6 "有人开始往七号门走，不再往中间挤")
          (list 8 "翻栏杆的那几个被拦了下来")
          (list 10 "场子稳住了。还有人站着，但没有人在跑"))))

;; 他第二回合才进场，所以期限按余下的回合算：4 回合，第五段结束前必须拦住。
(define runner-trouble
  (make-trouble "从三号门进来的那个人" "外圈" 6 4 'sharpness
    "他不慌，穿过人群，一直朝正中间去。归零就是他走到了她跟前"
    "他一直走到了台边。他够到她了。"
    (list (list 2 "你在人堆里认出了他——他是唯一一个不慌的")
          (list 4 "你挡在了他和台子中间")
          (list 6 "你在第二排把他按住了"))))

;; 升降台解开之后才出现。这是你今晚真正要的东西，没有期限——
;; 但演出只剩这几个回合，唱不完就是唱不完。
(define stand-trouble
  (make-trouble "她要重新站上去" "中央" 4 0 'social
    "灯回来了，乐队在等她。从台阶到台口那一段还堵着人"
    "她始终没能回到那束光里。"
    (list (list 1 "乐队看见她了，第一个音重新响起来")
          (list 2 "台阶那一段清开了")
          (list 3 "她走上了第一级")
          (list 4 "她站回原来的位置，把中断的那一段接了下去"))))

;; 这一处不吃布置的便宜：你几天前动的是升降台，不是她走回台上的那几步。
((stand-trouble 'progress) 'set! 0)

;; ============================================================
;; 场上状态
;; ============================================================

(define finished? #f)
(define turn 1)
(define active (list lift-trouble panic-trouble))
(define solved-ids '())
(define expired-ids '())
(define evidence-damaged? #f)   ; 弗兰克的人自己动手，那人身上的东西散了
(define usher-quit? #f)         ; 内环塌过一次，领班不干了
(define runner-in? #f)          ; 那个人已经进场
(define stand-open? #f)         ; 她已经上得来了

(define show-clk (make-clock "演出还剩" 6 'countdown
                             "唱完这几段就是谢幕。每一段结束，场上的期限一起往下走。"))
(show-clk 'set! 6)

(define (solved? id) (member? id solved-ids))
(define (expired? id) (member? id expired-ids))

(define (drop-active! id)
  (define (walk lst)
    (if (null? lst)
        '()
        (if (equal? ((car lst) 'id) id)
            (cdr lst)
            (cons (car lst) (walk (cdr lst))))))
  (set! active (walk active)))

(define (first-in-ring ring)
  (define (walk lst)
    (if (null? lst)
        #f
        (if (equal? ((car lst) 'ring) ring)
            (car lst)
            (walk (cdr lst)))))
  (walk active))

(define (ring-troubles ring)
  (define (walk lst)
    (if (null? lst)
        '()
        (if (equal? ((car lst) 'ring) ring)
            (cons (car lst) (walk (cdr lst)))
            (walk (cdr lst)))))
  (walk active))

;; ============================================================
;; 解决与归零
;; ============================================================

(define (solve! t)
  (set! solved-ids (cons (t 'id) solved-ids))
  (drop-active! (t 'id))
  ;; 她上得来了，今晚真正要办的那件事才出现。
  (if (and (equal? (t 'id) "升降台卡在半程") (not stand-open?))
      (begin
        (set! stand-open? #t)
        (set! active (append active (list stand-trouble)))
        (notify! "她站到了台边。乐队还在等——把她送回那束光里去。"))
      #f)
  (if (equal? (t 'id) "她要重新站上去") (finish!) #f))

(define (expire! t)
  (set! expired-ids (cons (t 'id) expired-ids))
  (drop-active! (t 'id))
  (cond
    ((equal? (t 'id) "升降台卡在半程")
     (damage-party! 1)
     (spend-composure! 1))
    ((equal? (t 'id) "台下的恐慌")
     (damage-party! 1)
     (set! usher-quit? #t))
    ((equal? (t 'id) "从三号门进来的那个人")
     (spend-composure! 2))
    (#t #f))
  ;; 她被人流卷走 / 腿伤了，那就没有"重新站上去"这回事了。
  (if (member? (t 'id) (list "升降台卡在半程" "台下的恐慌"))
      (begin
        (set! stand-open? #f)
        (drop-active! "她要重新站上去"))
      #f)
  (notify! (t 'cost)))

;; ============================================================
;; 动作
;; ============================================================

(define (trouble-clocks t)
  (if (t 'limit)
      (list ((t 'progress) 'render-data) ((t 'limit) 'render-data))
      (list ((t 'progress) 'render-data))))

(define (say-notes! lst)
  (if (null? lst)
      #t
      (begin (result-note! (car lst)) (say-notes! (cdr lst)))))

(define (push-trouble! t n)
  (let ((before (t 'current)))
    (t 'fill! n)
    (say-notes! (t 'notes-in before (t 'current)))
    (if (t 'done?) (solve! t) #f)))

(define (trouble-node t)
  (node (t 'id)
    :subtitle (string-append (t 'desc) " · " (ability-label (t 'ability)))
    :tags (if (t 'urgent?) (list "来不及了") '())
    :clocks (trouble-clocks t)
    :requires (list (req-die))
    :resolve (roll (t 'ability)
      (outcome "没按住"
        (lambda ()
          (if (equal? (t 'ability) 'violence)
              (damage-party! 1)
              (spend-composure! 1))))
      (outcome "往前推了一步"
        (lambda () (push-trouble! t 1)))
      (outcome "一下子推开了"
        (lambda () (push-trouble! t 2))))))

(define (ring-nodes ring empty-name empty-text)
  (let ((ts (ring-troubles ring)))
    (if (null? ts)
        (list (observe-action empty-name empty-text))
        (map trouble-node ts))))

;; ============================================================
;; 人手：每回合各按自己的方式啃一格
;; ============================================================

(define (aide-push! ring speaker text)
  (let ((t (first-in-ring ring)))
    (if t
        (begin
          (play-remote-banter! (line speaker text))
          (push-trouble! t 1))
        #f)))

;; 弗兰克的人不等你下令：眼看压不住了他们自己上，人是按住了，
;; 那人身上的东西也散了。
(define (frank-turn!)
  (let ((t (first-in-ring "外圈")))
    (if (and t (t 'urgent?))
        (begin
          (set! evidence-damaged? #t)
          (play-remote-banter! (line "弗兰克的人" "这个我们自己来。你别过来。"))
          (push-trouble! t ((t 'progress) 'remaining)))
        (aide-push! "外圈" "弗兰克的人" "后廊这头站满了，谁也过不去。"))))

(define (aides-turn!)
  (if aide-joe
      (aide-push! "外圈" "乔" "推车我一辆一辆横过来了，门先别管。")
      #f)
  (if aide-frank (frank-turn!) #f)
  (if aide-police
      (aide-push! "内环" "警察" "按程序疏散，先清环廊——台上的事不归我们。")
      #f)
  (if (and aide-usher (not usher-quit?))
      (aide-push! "内环" "领班" "这一排从七号门出去，那边是空的。")
      #f)
  (if (and aide-usher usher-quit?)
      (play-remote-banter! (line "领班" "我不管了。我也是花钱雇来的。"))
      #f))

;; ============================================================
;; 回合推进
;; ============================================================

(define (tick-limits!)
  (define (walk lst)
    (if (null? lst)
        #t
        (begin
          ((car lst) 'tick-limit!)
          (walk (cdr lst)))))
  (walk active)
  (define (sweep lst)
    (if (null? lst)
        #t
        (begin
          (if ((car lst) 'expired?) (expire! (car lst)) #f)
          (sweep (cdr lst)))))
  (sweep active))

;; 他从外面进来要一点时间。第二回合起，外圈那一处才开始烧。
(define (spawn-runner!)
  (if (not runner-in?)
      (begin
        (set! runner-in? #t)
        (set! active (append active (list runner-trouble)))
        (notify! "三号门那边有人逆着人流往里走。他不慌。"))
      #f))

(define-turn-rule "演出往下走"
  (lambda () (not finished?))
  (lambda ()
    (aides-turn!)
    (if finished?
        #f
        (begin
          (tick-limits!)
          (show-clk 'advance! -1)
          (set! turn (+ turn 1))
          (if (> turn 6)
              (finish!)
              (if (= turn 2) (spawn-runner!) #f))))))

;; ============================================================
;; 结算：四个向量，不是成败
;; ============================================================

(define (her-hurt?)
  (or (expired? "升降台卡在半程") (expired? "从三号门进来的那个人")))

(define (show-level)
  (cond
    ((solved? "她要重新站上去") "完整谢幕")
    ((expired? "从三号门进来的那个人") "严重中断")
    ((expired? "升降台卡在半程") "严重中断")
    ((expired? "台下的恐慌") "严重中断")
    ((and stand-open? (>= (stand-trouble 'current) 2)) "勉强收尾")
    (#t "严重中断")))

(define (caught?) (solved? "从三号门进来的那个人"))
(define (has-evidence?) (and (caught?) (not evidence-damaged?)))

(define (helpers-text)
  (string-append
    (if aide-joe "乔守了一夜的门。" "")
    (if aide-frank "弗兰克的人压住了后廊那一头。" "")
    (if aide-police "警察按程序清了环廊。" "")
    (if (and aide-usher (not usher-quit?)) "领班把人一排一排引了出去。" "")
    (if (and (not aide-joe) (not aide-frank) (not aide-police) (not aide-usher))
        "没有人替你分担——这一晚从头到尾只有你自己。"
        "")))

(define (closing-text)
  (string-append
    (cond
      ((expired? "从三号门进来的那个人")
       "那个人一直走到了台边才被拦下。她是被人从台阶下面抬出去的。")
      ((expired? "升降台卡在半程")
       "台子卡死在半程。等他们把她抬出来，她的腿已经不能站了。")
      ((expired? "台下的恐慌")
       "环廊塌成一团，人群把她一起卷了出去。等灯全亮，台上是空的。")
      ((solved? "她要重新站上去")
       "她从台底那道扶梯徒手爬上来，站回原来的位置，把中断的那一段接了下去。")
      (#t "她站在台边，一直没能走回那束光里。乐队等到最后收了乐器。"))
    (cond
      ((equal? (show-level) "完整谢幕") "掌声比预定的谢幕长了很久。")
      ((equal? (show-level) "勉强收尾") "掌声稀稀落落，但她站到了最后一个音。")
      (#t "台下的人已经走了大半。"))
    (cond
      ((and (caught?) (has-evidence?)) "后台那个人被按住了，他手上的东西也留了下来。")
      ((caught?) "后台那个人被按住了，可他手上什么也没剩下。")
      (#t "那个人混在散场的人里走脱了。"))
    (helpers-text)))

(define (finish!)
  (if finished?
      #f
      (begin
        (set! finished? #t)
        (set-global! '首演-她受伤 (her-hurt?))
        (set-global! '首演-完成度 (show-level))
        (set-global! '首演-抓到人 (caught?))
        (set-global! '首演-有物证 (has-evidence?))
        (set-global! '首演-有人被踩 (expired? "台下的恐慌"))
        (set-global! '首演-警察到场 aide-police)
        (set-global! '首演-老街到场 (or aide-joe aide-frank))
        (if (her-hurt?) (damage-party! 1) #f)
        (spotlight! (if (her-hurt?) "首演之夜：人群压了上来" "首演之夜：她唱完了")
                    (closing-text))
        (end-encounter 'done))))

;; ============================================================
;; 渲染
;; ============================================================

(define (situation-text)
  (string-append
    "灯灭在第三段舞台升起的时候。烟从台底涌出来，有人在黑暗里喊了一声。"
    (if (> prep-outer 0) "你走过的那几扇门今晚都算数。" "外圈的门你一扇也没看过。")
    (if (> prep-inner 0) "你清过的那段环廊还撑着。" "")
    (if (> prep-core 0) "升降台的行程你动过手脚，它没有一路卡到底。" "")
    "三个圈子同时出事，而你只有一个人——先去哪儿，就是今晚全部的问题。"
    (if usher-quit? "领班已经撂挑子了。" "")))

(define (get-render-data)
  (container-with-clocks "首演之夜"
    (list
      (observe-action "此刻的场面" (situation-text))
      (container "外圈的门与后廊"
        (ring-nodes "外圈" "外圈眼下无事" "门都插着，后廊那头没有动静。"))
      (container "内环走道与看台"
        (ring-nodes "内环" "内环眼下无事" "人还坐着，环廊是通的。"))
      (container "中央台与升降口"
        (ring-nodes "中央" "中央眼下无事" "台子空着。她不在上面。")))
    (list (show-clk 'render-data))))
