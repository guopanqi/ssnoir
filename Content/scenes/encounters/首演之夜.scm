;; scenes/encounters/首演之夜.scm - 第一章大型交锋「首演之夜」
;;
;; 主结构：双战线，共享同一批骰子。
;;   台上 —— 恐慌自动上涨，动作是控场、疏散、护住她；失手 = 她受伤、演出中断。
;;   后台 —— 执行者在跑，他也在推自己的进度；动作是追、封、搜物证；
;;           失手 = 人跑了，什么也没留下。
;; 两线的失败后果不对称，所以分诊是真的分诊，不是对称刷格。
;;
;; 考点：混乱中的取舍——保护她还是追人，控场还是保物证，靠警方还是靠老街。
;;
;; 公司安排的是「可控惊扰」，但涉及真实的物理危险：停电会踩踏，烟会引发
;; 恐慌，执行者动作过大会真的伤人。玩家读不出这一层，但它决定场内的写法：
;; 真的会有人受伤。
;;
;; 对外契约：以 'done 结束（成败不是二元的，四个向量写进 global 由故事解释）。
;; 城市输入（只在顶部读取一次）：小节三的五项准备。

(define prep-found-lyon (if (get-global '准备-找到莱恩) #t #f))
(define prep-police (if (get-global '准备-警方到场) #t #f))
(define prep-backstage (if (get-global '准备-后台已封) #t #f))
(define prep-staging (if (get-global '准备-登台已改) #t #f))
(define prep-decoy (if (get-global '准备-诱饵) #t #f))
(define frank-here (relation-at-least? "劳工" '核心))

(define finished? #f)
(define turns 0)

;; ============================================================
;; 两条战线
;; ============================================================

;; 台上：恐慌自动走。填满 = 踩踏，她在混乱里受伤。
(define panic-clk
  (make-clock "台下的恐慌" 8 'segments
              "每回合自动 +1。填满 = 人群挤向出口，她在混乱里受伤，演出到此为止。"))

;; 台上的目标：把场面按住，让她能唱完。
(define stage-clk
  (make-clock "让她唱完" 6 'segments
              "填满 = 场面按住了，她从备用扶梯回到台上，把中断的演出唱完。"))

;; 后台：执行者在跑，他也在推自己的进度。
(define runner-clk
  (make-clock "他正在脱身" 6 'segments
              "每回合自动 +1（封住的出口会让他慢下来）。填满 = 他从后巷走脱，什么也没留下。"))

(define catch-clk
  (make-clock "堵住他" 5 'segments
              "填满 = 他被按住；若此时物证也在手里，这一晚就交代得过去了。"))

(define evidence-clk
  (make-clock "留下的物证" 3 'segments
              "配电间的工具、伪造的通行证、他手上的东西。填满 = 物证到手，与抓不抓得住他各算各的。"))

;; ── 准备兑现为起始场面（改场面，不改骰子）──────────
(if prep-staging (stage-clk 'set! 2) #f)          ; 她的位置改过，开局就顺
(if prep-police (panic-clk 'set! 0) (panic-clk 'set! 1))
(if prep-backstage (catch-clk 'set! 2) #f)        ; 封住的出口把他往你这边赶
(if prep-decoy (runner-clk 'set! 0) (runner-clk 'set! 1))
(if prep-found-lyon (evidence-clk 'tick!) #f)     ; 你知道该找什么

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

(define (panic+ n)
  (clock-tick-n! panic-clk n)
  (if (panic-clk 'full?) (finish!) #f))

(define (runner+ n)
  (clock-tick-n! runner-clk n)
  #f)

;; ============================================================
;; 台上的动作
;; ============================================================

(define (node-calm-house)
  (node "压住台下"
    :subtitle "交际；坏：恐慌 +1，中：让她唱完 +1，好：+2 并压回一格恐慌"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "前排先站了起来" "你喊的话被淹没了。前排有人站起来，后排跟着站起来。"
        (lambda () (panic+ 1)))
      (outcome "有人坐回去了" "你站到能被看见的地方，把声音压得很平。靠近你的几排安静下来。"
        (lambda () (stage-clk 'tick!)))
      (outcome "场子稳住了" "你让领座的人把灯打开一半。人群需要的只是看得见彼此。"
        (lambda ()
          (clock-tick-n! stage-clk 2)
          (stage-clk 'tick!)
          (panic-clk 'set! (max 0 (- (panic-clk 'current) 1))))))))

(define (node-open-exits)
  (node "把边门打开疏散"
    :subtitle "力量；坏：健康 −1，中：恐慌 −1，好：恐慌 −2"
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "被人流挤在门上" "门是往里开的。你被挤在门板和人之间，肋骨那边响了一声。"
        (lambda () (damage-party! 1)))
      (outcome "开了一扇" "你把插销砸开，让最挤的那一片先出去。"
        (lambda () (panic-clk 'set! (max 0 (- (panic-clk 'current) 1)))))
      (outcome "两扇都开了" "两侧边门同时打开，人流分成两股，谁也没有被踩到。"
        (lambda () (panic-clk 'set! (max 0 (- (panic-clk 'current) 2))))))))

(define (node-guard-her)
  (node "守在她够得着的地方"
    :subtitle "敏锐；坏：冷静 −1，中：让她唱完 +1，好：+2"
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "你被挤开了" "有人从侧面撞过来，等你站稳，台口已经隔了三排人。"
        (lambda () (spend-composure! 1)))
      (outcome "她看见你了" "你站到侧台的灯下。她在烟里看了你一眼，没有停。"
        (lambda () (stage-clk 'tick!)))
      (outcome "她知道该怎么走了" "你抬手指了指备用扶梯。她点了下头——她记得那个位置，你们改过路线。"
        (lambda () (clock-tick-n! stage-clk 2))))))

;; ============================================================
;; 后台的动作
;; ============================================================

(define (node-chase-runner)
  (node "追进后台"
    :subtitle "力量；坏：健康 −1，中：堵住他 +1，好：+2"
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "他推倒了道具架" "整排道具架砸下来。你抬手挡了一下，手背立刻见了血。"
        (lambda () (damage-party! 1)))
      (outcome "追到了走廊" "他在换装间那道门前慢了半步，你把距离缩短了。"
        (lambda () (catch-clk 'tick!)))
      (outcome "把他逼进死角" "你抄了配电间那条路——那条路你走过一遍。他撞上了堵死的门。"
        (lambda () (clock-tick-n! catch-clk 2))))))

(define (node-seal-exit)
  (node "封住后巷的门"
    :subtitle "见识；坏：他脱身 +1，中：堵住他 +1，好：+1 并让他慢一格"
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "锁扣是坏的" "那道门的锁三年前就坏了。你花掉的时间全喂了它。"
        (lambda () (runner+ 1)))
      (outcome "门插上了" "你把消防栓的横杆卸下来，横在门后。"
        (lambda () (catch-clk 'tick!)))
      (outcome "两头都堵上了" "你把后巷两头的门都别死。他现在只能往有人的地方跑。"
        (lambda ()
          (catch-clk 'tick!)
          (runner-clk 'set! (max 0 (- (runner-clk 'current) 1))))))))

(define (node-collect-evidence)
  (node "先把东西留下来"
    :subtitle "敏锐；追不追得上他另算——物证是另一件事"
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "被人踩了过去" "地上那点东西被撤场的人踩进了地板缝里。"
        (lambda () #f))
      (outcome "捡到了工具" "配电间门口掉着一把改锥，柄上缠着胶布。你用手帕把它包起来。"
        (lambda () (evidence-clk 'tick!)))
      (outcome "捡到了通行证" "一张后台通行证，名字是印上去的，油墨还没干透。谁也不会给临时工印这种东西。"
        (lambda () (clock-tick-n! evidence-clk 2))))))

;; 老街的人到场：一次性，不掷骰。城市攒下的东西场内亮出即生效。
(define frank-used? #f)

(define (node-frank-help)
  (node "让弗兰克的人堵住后巷"
    :subtitle "一次性；不掷骰。他带来的人认得这一片的每一条巷子"
    :disabled frank-used?
    :resolve (instant
      (outcome "后巷被人站满了" "弗兰克带来的人往巷口一站，谁也过不去。'你要的那个人，跑不了。'"
        (lambda ()
          (set! frank-used? #t)
          (clock-tick-n! catch-clk 2)
          (runner-clk 'set! (max 0 (- (runner-clk 'current) 2))))
        'light))))

;; ============================================================
;; 结算：四个向量，不是成败
;; ============================================================

(define (hurt?) (panic-clk 'full?))
(define (caught?) (catch-clk 'full?))
(define (has-evidence?) (evidence-clk 'full?))

(define (show-level)
  (cond
    ((stage-clk 'full?) "完整谢幕")
    ((>= (stage-clk 'current) 3) "勉强收尾")
    (else "严重中断")))

(define (helpers)
  (append
    (if prep-police (list "警察") '())
    (if frank-used? (list "老街的人") '())))

(define (helpers-text)
  (let ((hs (helpers)))
    (cond
      ((null? hs) "没有人替你分担——这一晚从头到尾只有你自己。")
      ((null? (cdr hs)) (string-append "到场帮过忙的：" (car hs) "。"))
      (else "到场帮过忙的：警察，还有老街的人。"))))

(define (finish!)
  (if finished?
      #f
      (begin
        (set! finished? #t)
        ;; 四个向量写进 global，由故事模块解释和结案。
        (set-global! '首演-她受伤 (hurt?))
        (set-global! '首演-完成度 (show-level))
        (set-global! '首演-抓到人 (caught?))
        (set-global! '首演-有物证 (has-evidence?))
        (set-global! '首演-警察到场 prep-police)
        (set-global! '首演-老街到场 frank-used?)
        (if (hurt?) (damage-party! 1) #f)
        (if (hurt?)
            (spotlight! "首演之夜：人群压了上来"
              (string-append
                "灯灭的时候人群一起站了起来。等灯再亮，前排的椅子已经翻了。"
                "她被人挤下了台阶，手腕擦破了一大片。演出到此为止。"
                (helpers-text)))
            (spotlight! "首演之夜：她唱完了"
              (string-append
                "烟散了以后，她从侧面那道备用扶梯徒手爬了上来，站回原来的位置。"
                "乐队愣了两拍才跟上。中断的那一段重新响起来，一直唱到最后。"
                (cond
                  ((equal? (show-level) "完整谢幕") "掌声比预定的谢幕长了很久。")
                  ((equal? (show-level) "勉强收尾") "掌声稀稀落落，但她站到了最后一个音。")
                  (else "台下的人已经走了大半，她还是唱完了。"))
                (helpers-text))))
        (end-encounter 'done))))

;; ============================================================
;; 回合推进
;; ============================================================

;; 两条战线同时自动走：台下的恐慌和他脱身的进度。封住的出口让他慢一点。
(define-turn-rule "台上与后台同时推进"
  (lambda () (not finished?))
  (lambda ()
    (set! turns (+ turns 1))
    (panic+ 1)
    (if finished?
        #f
        (begin
          (runner+ (if prep-backstage 1 2))
          (if (or (runner-clk 'full?) (stage-clk 'full?))
              (finish!)
              #f)))))

(define-rule "动作后的即时结算"
  (lambda () (not finished?))
  (lambda ()
    (if (or (panic-clk 'full?) (stage-clk 'full?) (runner-clk 'full?))
        (finish!)
        #f)))

;; ============================================================
;; 渲染
;; ============================================================

(define (node-stage-front)
  (container-with-clocks "台上"
    (list (node-calm-house) (node-open-exits) (node-guard-her))
    (list (panic-clk 'render-data) (stage-clk 'render-data))))

(define (node-backstage)
  (container-with-clocks "后台"
    (append
      (list (node-chase-runner) (node-seal-exit) (node-collect-evidence))
      (if frank-here (list (node-frank-help)) '()))
    (list (runner-clk 'render-data) (catch-clk 'render-data)
          (evidence-clk 'render-data))))

(define (opening-note)
  (string-append
    "舞台升起到一半，灯全灭了。烟从台侧涌出来，有人在黑暗里喊了一声。"
    (if prep-decoy "他比预定的时间早动了手——你的假消息起了作用，人比原计划少。" "")
    (if prep-police "后台门口的两个警察已经动了。" "")
    (if prep-backstage "你封过的那几道门，今晚都算数。" "")))

(define (get-render-data)
  (container "首演之夜"
    (list
      (observe-action "刚刚发生了什么" (opening-note))
      (node-stage-front)
      (node-backstage))))
