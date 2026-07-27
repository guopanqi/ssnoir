;; scenes/encounters/夜莺·了断.scm - 夜莺委托线·终场(第 17 天)
;; 主结构:两幕。第一幕在老板进门以前把老街的眼睛拉过来,第二幕当面了断。
;; 设计原则:每个麻烦都是一个具体的人——命数、下一招还有几回合、那一招落地是什么,
;;           全部明牌。玩家永远在"拆哪一招"和"硬吃哪一招"之间选,而不是在猜。
;; 数值骨架(改动它以前先读这两条):
;;   1. 压制和击倒都要判定,分流靠难度与风险:压制 +1 修正、失手不额外疼,是差骰的去处,
;;      但只买这一轮;击倒平难度、打空了要挨一下,是好骰的去处,买的是往后每一轮。
;;      别把压制做成不判定的免费动作,也别让击倒的失败零成本——那两种改法都会让一边永远压倒另一边。
;;   2. 打手不是目标,是税:不管他们,每回合都在抽你的冷静/健康/金钱。第一幕的骰子要在
;;      压制、击倒、老街的眼睛之间分,老板到场 5 回合自动走,时间不是你能谈的东西。
;; 对外契约:只回传 'success / 'fail;不写任何世界状态。
;; 城市输入（只在顶部读取）:
;;   夜莺保护方案 - "首期" 时收账人收过钱,使眼色的节奏更慢(每 3 回合)。
;;   夜莺真相     - 暗账查明,第二幕解锁「说出那晚的真相」。
;;   夜莺已送走   - 她已上船,进入无人变体:失败钟指向你自己。
;;   夜莺姿态     - "体谅" / "责问" / "自白",只决定真相那段台词用哪一版。
;;   夜莺信任达标 - 失败时她会自己开口走出去，而非被拖走。
;;   收账人识破过你 - 第一场被打出去过,第二幕当面逼账带 −1 可见修正。
;;   劳工关系     - 到「核心」则弗兰克带人到场,开场缠住刀疤和三只手。

(define first-payment?
  (equal? (let ((v (get-global '夜莺保护方案))) (if v v "无")) "首期"))
(define truth-known?
  (let ((v (get-global '夜莺真相))) (if v v #f)))
(define alone?
  (let ((v (get-global '夜莺已送走))) (if v v #f)))
(define stance
  (let ((v (get-global '夜莺姿态))) (if v v "无")))
(define trust-met?
  (let ((v (get-global '夜莺信任达标))) (if v v #f)))
(define seen-through?
  (let ((v (get-global '收账人识破过你))) (if v v #f)))
(define frank-here?
  (relation-at-least? "劳工" '核心))

(define act 1)

;; ============================================================
;; 第一幕
;; ============================================================

(define boss-arrival-clk
  (make-clock "老板到场" 5 'segments
              "自动 +1/回合。满 → 他推门进来,第一幕结束。没倒下的打手跟他一起进来;收账人无论如何在这时合上账本走人。"))

(define crowd-clk
  (make-clock "老街的眼睛" 4 'segments
              "可选。攒到 3 格以上,第二幕里老板每动一次粗都会让他自己更难看:当着老街动粗,他在输。"))

;; ---- 打手 ----
;; 每个打手是同一套语法:命数 + 一个出招倒数 + 一句写明落地效果的招名。
;; 压制换时间,击倒换命数。两者都要判定,差别在**难度和风险**:
;;   压制:+1 修正,只是把人顶开,坏结果不额外疼。差一点的骰子也敢用,但只买这一轮。
;;   击倒:平难度,打空了他会回敬你一下(冷静 −1)。好骰子的去处,买的是往后每一轮。
;; 所以"反正击倒更划算"不成立:低骰去压制的成功率明显更高,高骰才配去换命数。

(define (make-thug name hp-max period first-in tell tell-note effect)
  (let ((hp hp-max)
        (countdown first-in))
    (lambda (msg)
      (cond
        ((equal? msg 'name) name)
        ((equal? msg 'hp) hp)
        ((equal? msg 'dead?) (<= hp 0))
        ((equal? msg 'reset!) (set! countdown period))
        ((equal? msg 'rush!) (set! countdown (max 1 (- countdown 1))))
        ((equal? msg 'tick!)
         (set! countdown (- countdown 1))
         (if (<= countdown 0)
             (begin (effect) (set! countdown period) #t)
             #f))
        ((equal? msg 'render-data)
         (container-with-clocks name
           (list
             (node (string-append "压制 " name)
                   :subtitle "顶开就行，比打倒他容易；他的出招重新起算"
                   :requires (list (req-die))
                   :resolve (roll 'violence
                     (lambda () (list (modifier 1 "只是顶开他，不用打倒")))
                     (outcome "他没停手" "你扑了个空,他反手把你推开。"
                       (lambda () #f))
                     (outcome "把他顶回去" (string-append name "被你顶在柱子上,那一招得重新起。")
                       (lambda () (set! countdown period)))
                     (outcome "顶得他撞翻了凳子" (string-append name "一时爬不起来,手里的势头全散了。")
                       (lambda () (set! hp (- hp 1)) (set! countdown period)))))
             (action (string-append "击倒 " name)
                     (list (req-die))
                     (roll 'violence
                       (outcome "他站得很稳" "你的拳头落在他肩上,像打在门板上——他顺手回敬了一下。"
                         (lambda () (spend-composure! 1)))
                       (outcome "打实了一下" "他闷哼一声,退了半步。"
                         (lambda () (set! hp (- hp 1))))
                       (outcome "打得他跪下" "他的膝盖先着地,手里的东西滚到桌下。"
                         (lambda () (set! hp (- hp 2)))))))
           (list (list 'clock "命数" hp hp-max 'segments "归零即退场。击倒要好骰子，但他一倒，往后每一轮都少一份账。")
                 (list 'clock tell countdown period 'countdown tell-note))))
        (#t #f)))))

(define thugs
  (list
    (make-thug "刀疤" 2 2 2 "闷拳" "落地:冷静 −2。"
               (lambda () (spend-actor-composure! 'player 2)))
    (make-thug "大个子" 3 3 3 "抡椅子" "落地:健康 −1。"
               (lambda () (damage-party! 1)))
    (make-thug "三只手" 2 2 4 "摸口袋" "落地:金钱 −5。"
               (lambda () (spend-up-to! "金钱" 5)))))

;; 弗兰克带人到场:他不上你的场面,只在门口把两个人缠住。大个子还是你的事。
(if frank-here?
    (set! thugs (filter (lambda (t) (equal? (t 'name) "大个子")) thugs))
    #f)

(define (live-thugs)
  (filter (lambda (t) (not (t 'dead?))) thugs))

;; ---- 收账人 ----
;; 打不着他。他不出手,只是让别人快一点——所以他是个节奏问题,不是战斗问题。

(define collector-gone? #f)
(define collector-period (if first-payment? 3 2))
(define collector-countdown collector-period)

(define (collector-tick!)
  (set! collector-countdown (- collector-countdown 1))
  (if (<= collector-countdown 0)
      (begin
        (rush-all-thugs! (live-thugs))
        (set! collector-countdown collector-period)
        #t)
      #f))

(define (rush-all-thugs! lst)
  (if (null? lst)
      #f
      (begin ((car lst) 'rush!) (rush-all-thugs! (cdr lst)))))

(define (node-collector)
  (container-with-clocks "收账人"
    (list
      (observe-action "他在数什么"
        "他没动手,只是站在桌边翻账本。每翻过一页,场上就有人加快一步。打他没有用——他不是来打架的。")
      (node "亮出欠账凭据"
        :subtitle "第一场从他账袋里摸出来的那张纸；用掉它，他就收手"
        :requires (list (req-item "欠账凭据" 1))
        :resolve (instant
          (outcome "他合上了账本" "他看了那张纸很久,然后把账本夹回腋下。'这笔不该我背。'他从后门走了。"
            (lambda ()
              (remove-item! "欠账凭据" 1)
              (set! collector-gone? #t))))))
    (list (list 'clock "使眼色" collector-countdown collector-period 'countdown
                (if first-payment?
                    "落地:全体打手的出招倒数 −1。他收过你的首期,做事没那么起劲。"
                    "落地:全体打手的出招倒数 −1。")))))

;; ---- 一次性道具 ----

(define table-used? #f)
(define drink-used? #f)

(define (node-flip-table)
  (node "掀翻长桌"
    :subtitle "不判定，一次性。全体打手的出招重新起算"
    :requires (list (req-die))
    :resolve (instant
      (outcome "桌子横在中间" "杯子和牌一起砸在地上。他们要绕过去,得重新找路。"
        (lambda ()
          (set! table-used? #t)
          (reset-all-thugs! (live-thugs)))))))

(define (reset-all-thugs! lst)
  (if (null? lst)
      #f
      (begin ((car lst) 'reset!) (reset-all-thugs! (cdr lst)))))

;; 你打不着收账人,但能泼他一脸。给没摸到凭据的玩家留的第二个答案。
(define (node-throw-drink)
  (node "泼收账人一脸酒"
    :subtitle "不判定，一次性。他的使眼色倒数 +2"
    :requires (list (req-die))
    :resolve (instant
      (outcome "他在抹脸" "酒顺着他的下巴往下淌。他先要看得见,才顾得上给谁使眼色。"
        (lambda ()
          (set! drink-used? #t)
          (set! collector-countdown (+ collector-countdown 2)))))))

;; ---- 老街的眼睛 ----

(define badge-used? #f)

(define (node-buy-round)
  (node "敬全场一杯"
    :subtitle "不判定。让所有人愿意多看一眼；老街的眼睛 +1"
    :requires (list (req-die) (req-item "金钱" 6))
    :resolve (instant
      (outcome "杯子举起来了" "老板娘把酒挨桌斟过去。有人开始正眼看这张台子。"
        (lambda () (spend-up-to! "金钱" 6) (crowd-clk 'tick!))))))

(define (node-speak-up)
  (node "站上台面说破"
    :subtitle "把这笔账当着所有人念一遍"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "没人接话" "你的声音停在半空。有人低头喝酒,像什么都没听见。"
        (lambda () (spend-composure! 1)))
      (outcome "有人抬了头" "后排有人放下杯子。"
        (lambda () (crowd-clk 'tick!)))
      (outcome "整间屋子安静了" "你把数目念出来的时候,连后厨都停了。"
        (lambda () (crowd-clk 'tick!) (crowd-clk 'tick!))))))

(define (node-badge)
  (node "亮办案通行证"
    :subtitle "不判定，不消耗。让每张脸都意识到会被记下；老街的眼睛 +2"
    :requires (list (req-die) (req-item "办案通行证" 1))
    :resolve (instant
      (outcome "他们看清了那张纸" "没人相信这张纸能管今晚的事,但也没人愿意第一个上镜。"
        (lambda () (set! badge-used? #t) (crowd-clk 'tick!) (crowd-clk 'tick!))))))

(define (crowd-nodes)
  (append
    (list (node-buy-round) (node-speak-up))
    (if badge-used? '() (list (node-badge)))))

(define (node-crowd)
  (container "老街的眼睛" (crowd-nodes)))

;; ---- 第一幕 → 第二幕 ----

(define (enter-act-two!)
  (play-dialogue!
    (line "收账人" "我的部分到此为止。剩下的,他自己来。")
    (line "主角" "把门留着。今晚谁都别想从后面走。"))
  (set! act 2)
  (set! collector-gone? #t))

;; ============================================================
;; 第二幕
;; ============================================================

(define boss-clk
  (make-clock "老板" 8 'segments
              "填满 = 他认账、收手,今晚到此为止。填到 4 格他会撕破脸,出招间隔缩到 1 回合。"))

(define drag-clk
  (if alone?
      (make-clock "你撑不住" 4 'segments
                  "填满 = 你被按在地上,今晚由不得你说话。她已经上了船,这一次赌的是你自己。")
      (make-clock "她被拖走" 4 'segments
                  "填满 = 他们把她从后门带走,交锋结束。她被带回邻城,这一次没有第二个第七年。")))

;; 老板的招是一个固定循环。每一招都是一张预告卡:还有几回合、落地什么、怎么拆。
;; 硬吃是合法打法——不拆,照付,把骰子花在别处。
(define move-index 0)      ; 0 砸场子 / 1 示意拖她 / 2 亲自动手
(define move-countdown 0)
(define boss-hits-this-move 0)
(define rage? #f)
(define turning-point-done? #f)
(define glass-used? #f)
(define truth-used? #f)

(define (move-name i)
  (cond ((= i 0) "砸场子") ((= i 1) "示意拖她") (#t "亲自动手")))

(define (move-base-countdown i)
  (if rage? 1 (if (= i 1) 3 2)))

;; 无人可派时跳过「示意拖她」——她已经走了,或者场上没人可使唤。
(define (move-available? i)
  (if (= i 1) (and (not alone?) (not (null? (live-thugs)))) #t))

(define (next-move-index i)
  (let ((n (modulo (+ i 1) 3)))
    (if (move-available? n) n (modulo (+ n 1) 3))))

(define (arm-move! i)
  (set! move-index i)
  (set! move-countdown (move-base-countdown i))
  (set! boss-hits-this-move 0))

(define (cancel-move!)
  (arm-move! (next-move-index move-index)))

(define (move-note)
  (cond
    ((= move-index 0) "落地:健康 −1、冷静 −1。拆法:交际判定压回去。")
    ((= move-index 1) "落地:拖走 +2。拆法:撂倒场上正在执行的那个打手——他一倒,这一招自己作废。")
    (#t "落地:健康 −2。拆法:武力判定挡下来。")))

;; 人心兑现:他每当着老街动一次粗,自己就更难看一分。
(define (crowd-backlash!)
  (if (>= (crowd-clk 'current) 3)
      (begin (boss-clk 'tick!) (result-note! "当着老街动粗，他在输"))
      #f))

(define (land-move!)
  (cond
    ((= move-index 0)
     (damage-party! 1)
     (spend-actor-composure! 'player 1)
     (crowd-backlash!))
    ((= move-index 1)
     (if alone?
         (begin (damage-party! 1) (spend-actor-composure! 'player 1) (drag-clk 'tick!))
         (begin (drag-clk 'tick!) (drag-clk 'tick!)))
     (crowd-backlash!))
    (#t
     (damage-party! 1)
     (damage-party! 1)
     (crowd-backlash!)))
  (cancel-move!))

;; 抢拍:任何一招的倒数期间,只要在他身上累计砸够两下,他就会踉跄,这一招作废。
(define (hit-boss! n)
  (clock-tick-n! boss-clk n)
  (set! boss-hits-this-move (+ boss-hits-this-move n))
  (if (>= boss-hits-this-move 2)
      (begin (result-note! "他踉跄了一下，这一招没能落地") (cancel-move!))
      #f)
  (check-rage!))

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

(define (check-rage!)
  (if (and (not rage?) (>= (boss-clk 'current) 4))
      (begin
        (set! rage? #t)
        (set! move-countdown (min move-countdown 1))
        (play-dialogue!
          (line "老板" "我给过你台阶。")
          (line "老板" "现在我不给了。")
          (line "主角" "那就别给。省得我还得谢你。")))
      #f))

;; ---- 第二幕的节点 ----

(define (node-boss-move)
  (cond
    ((= move-index 0)
     (node "压住他的砸场子"
       :subtitle "拆招：把话压回去，让他下不了手"
       :requires (list (req-die))
       :resolve (roll 'social
         (outcome "他不听" "他把你的话当成了噪音。"
           (lambda () (spend-composure! 1)))
         (outcome "他停了手" "他抬起的手停在半空,又放了下来。"
           (lambda () (cancel-move!)))
         (outcome "他被说得下不来" "满屋子的人都在看他要不要真砸。他没砸。"
           (lambda () (cancel-move!) (boss-clk 'tick!))))))
    ((= move-index 1)
     (observe-action "怎么拆「示意拖她」"
       "这一招不冲你来。撂倒场上正在执行的那个打手——他一倒,这一招自己作废。"))
    (#t
     (node "挡下他亲自动手"
       :subtitle "拆招：接住这一下"
       :requires (list (req-die))
       :resolve (roll 'violence
         (outcome "没接住" "他的手比你想的快。"
           (lambda () (spend-composure! 1)))
         (outcome "接住了" "你把他的手腕别到一边。"
           (lambda () (cancel-move!)))
         (outcome "接住还反了一下" "你顺着他的力道把他带得扑了个空。"
           (lambda () (cancel-move!) (boss-clk 'tick!))))))))

(define (node-boss)
  (container-with-clocks
    (if rage? "老板（撕破脸）" "老板")
    (list
      (observe-action (string-append "他的下一招：" (move-name move-index))
        (string-append (move-note) " 不拆也行——落地照付,把骰子花在别处。"))
      (node-boss-move))
    (list (boss-clk 'render-data)
          (list 'clock (move-name move-index) move-countdown (move-base-countdown move-index)
                'countdown (move-note)))))

(define (node-fist)
  (node "拳头说话"
    :subtitle "不讲道理的那条路；两下就能打断他正在起的招"
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "打空了" "他往后一让,你的拳头擦着他的耳朵过去。"
        (lambda () (spend-composure! 1)))
      (outcome "打实了" "他的嘴角裂了。"
        (lambda () (hit-boss! 1)))
      (outcome "打得他扶住桌子" "他半边身子压在桌沿上,喘了两口。"
        (lambda () (hit-boss! 2))))))

(define (node-confront)
  (node "当面逼账"
    :subtitle (if seen-through?
                  "把数目一笔一笔算给他听；他早从收账人那儿听过你的底"
                  "把数目一笔一笔算给他听")
    :requires (list (req-die))
    :resolve (roll 'social
      (lambda ()
        (if seen-through? (list (modifier -1 "他早知道你是谁")) '()))
      (outcome "他笑了" "他让你把话说完,然后当没听见。"
        (lambda () (spend-composure! 1)))
      (outcome "他答不上来" "有一笔他算不清。他自己也知道。"
        (lambda () (hit-boss! 1)))
      (outcome "他被账压住" "你把第七年那一条念出来的时候,他没有再看你。"
        (lambda () (hit-boss! 2))))))

(define (node-truth)
  (node "说出那晚的真相"
    :subtitle "一次性。暗账里那笔钱到底进了谁的口袋——说出来，他这一招就起不来了"
    :requires (list (req-die))
    :resolve (instant
      (outcome "满屋子都听见了" "他张了张嘴。这一次,他没有账本可翻。"
        (lambda ()
          (set! truth-used? #t)
          (if (or (equal? stance "体谅") (equal? stance "自白"))
              (play-dialogue!
                (line "主角" "她瞒着我,我知道为什么。她怕的是这个。")
                (line "主角" "那笔钱进了你的口袋。她替你担了七年。"))
              (play-dialogue!
                (line "主角" "她对我撒了谎。可撒谎的人不止她一个。")
                (line "主角" "那笔钱进了你的口袋。七年,你一个字都没提过。")))
          (hit-boss! 2)
          (cancel-move!))))))

(define (node-shield-her)
  (node "把她拉到身后"
    :subtitle "不判定。她被拖走的进度 −1；护着她的时候你腾不出手——健康 −1"
    :requires (list (req-die))
    :resolve (instant
      (outcome "她在你身后" "她的手抓着你的后襟,没有出声。有人的拳头结结实实落在你背上。"
        (lambda ()
          (drag-clk 'set! (max 0 (- (drag-clk 'current) 1)))
          (damage-party! 1))))))

(define (node-glass)
  (node "摔了他的酒杯"
    :subtitle "不判定，一次性。让他的当前招倒数 +1"
    :requires (list (req-die))
    :resolve (instant
      (outcome "杯子碎在他脚边" "他低头看了看鞋面。就这一下,他停了停。"
        (lambda () (set! glass-used? #t) (set! move-countdown (+ move-countdown 1)))))))

;; ---- 固定转折 ----
;; 拖走到 2 的时候停一拍。这不是难度事件,是让玩家把姿态说出口。

(define (node-turning-point)
  (container "她在看你"
    (list
      (instant-action "先护住她"
        (lambda ()
          (set! turning-point-done? #t)
          (set-global! '了断姿态 "体谅")
          (drag-clk 'set! (max 0 (- (drag-clk 'current) 1)))
          (play-dialogue!
            (line "主角" "先别说话。站到我后面来。")
            (line "夜莺" "你不问我为什么骗你?")
            (line "主角" "今晚不问。"))))
      (instant-action "让她自己开口"
        (lambda ()
          (set! turning-point-done? #t)
          (set-global! '了断姿态 "责问")
          (boss-clk 'tick!)
          (play-dialogue!
            (line "主角" "你自己说。当着他们的面。")
            (line "夜莺" "……第七年。我数过。")
            (line "夜莺" "我一天都没数错过。")))))))

;; ============================================================
;; 结算
;; ============================================================

(define (finish-success!)
  (spotlight! "了断：他认了这笔账"
    (if (>= (crowd-clk 'current) 3)
        "他把账本推回桌上,当着满屋子的人说了那句认。老街记得今晚谁在场——这件事往后不必再由你一个人说。"
        "他把账本推回桌上,声音低得只有你们几个听见。没人替你作证,但账是清了。"))
  (end-encounter 'success))

(define (finish-fail!)
  (spotlight! "了断：没能拦住"
    (if alone?
        "他们把你按在台阶上。天亮以前,老街酒馆的灯灭了;她在船上,不知道今晚这里发生了什么。"
        (if trust-met?
            (begin
              (play-dialogue!
                (line "夜莺" "住手。账是我的。")
                (line "夜莺" "十七天前是我把它放到他桌上的。现在我拿回来。")
                (line "世界" "她从台侧取下那把红伞,没有撑开,跟在他们身后走进雨里。"))
              "后门开了又合。你站在满地的碎杯子里。")
            (begin
              (play-dialogue!
                (line "世界" "两个人架住她的胳膊。她回头看了你一眼,像是想说什么,门在她身后合上了。"))
              "后门开了又合。你站在满地的碎杯子里。"))))
  (end-encounter 'fail))

(define-rule "他认账"
  (lambda () (and (= act 2) (boss-clk 'full?)))
  (lambda () (finish-success!)))

(define-rule "拦不住了"
  (lambda () (and (= act 2) (drag-clk 'full?)))
  (lambda () (finish-fail!)))

;; ---- 回合末 ----

(define (tick-thugs! lst)
  (if (null? lst)
      #f
      (begin ((car lst) 'tick!) (tick-thugs! (cdr lst)))))

(define-turn-rule "第一幕：场面继续走"
  (lambda () (= act 1))
  (lambda ()
    (tick-thugs! (live-thugs))
    (if collector-gone? #f (collector-tick!))
    (boss-arrival-clk 'tick!)
    (if (boss-arrival-clk 'full?)
        (begin (enter-act-two!) (arm-move! 0))
        #f)))

(define-turn-rule "第二幕：他和他的人"
  (lambda () (= act 2))
  (lambda ()
    (tick-thugs! (live-thugs))
    (set! move-countdown (- move-countdown 1))
    (if (<= move-countdown 0) (land-move!) #f)
    (if (drag-clk 'full?) (finish-fail!) #f)))

;; ============================================================
;; 渲染
;; ============================================================
;; 节点扁平:场景根下直接挂各单位卡、老板卡、道具、人心容器,不做多层分组。

(define (thug-nodes)
  (map (lambda (t) (t 'render-data)) (live-thugs)))

(define (act-one-tools)
  (append
    (if table-used? '() (list (node-flip-table)))
    (if (or drink-used? collector-gone?)
        '()
        (list (node-throw-drink)))))

(define (act-one-nodes)
  (append
    (thug-nodes)
    (if collector-gone? '() (list (node-collector)))
    (act-one-tools)
    (list (node-crowd))))

(define (act-two-verbs)
  (append
    (list (node-fist) (node-confront))
    (if (and truth-known? (not truth-used?)) (list (node-truth)) '())
    (if alone? '() (list (node-shield-her)))
    (if glass-used? '() (list (node-glass)))))

(define (act-two-nodes)
  (append
    (list (node-boss))
    (thug-nodes)
    (act-two-verbs)))

;; 转折卡把这一拍单独占住:拖走到 2 时它顶在最前面,选完才回到常规场面。
(define (turning-point-due?)
  (and (= act 2)
       (not alone?)
       (not turning-point-done?)
       (>= (drag-clk 'current) 2)))

(define (scene-nodes)
  (cond
    ((turning-point-due?) (list (node-turning-point)))
    ((= act 1) (act-one-nodes))
    (#t (act-two-nodes))))

(define (act-one-clocks)
  (list (boss-arrival-clk 'render-data) (crowd-clk 'render-data)))

(define (act-two-clocks)
  (list (boss-clk 'render-data) (drag-clk 'render-data) (crowd-clk 'render-data)))

(define (get-render-data)
  (container-with-clocks
    (if (= act 1) "了断：他进门以前" "了断：对面")
    (scene-nodes)
    (if (= act 1) (act-one-clocks) (act-two-clocks))))
