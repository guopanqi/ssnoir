;; 第一章公共交锋：码头坍塌。
;;
;; 这场想考玩家的是**分诊**：情报不足、手不够，而所有人的命都在按回合减少。
;; 三片废墟各压着两个暗格，暗格里可能是人、可能是急救箱、也可能什么都没有。
;; 被困的人从第一回合就开始流失生命——包括你还没挖开、还不知道存在的那些。
;; 挖得慢，你会挖出一个已经不出声的人；挖得急，碎料会压死你正要救的人。
;;
;; 两个人在半路上，他们**真的入队**，带着自己的骰子和自己的技能：
;;   弗兰克（第一回合末到，第二回合起可用）2 颗骰，力量 3 · 交际 3——能扛能喊人，看不懂机械。
;;   林（第三回合末到，第四回合起可用）1 颗骰，见识 4，且骰点**恒定 4**——机器不会掷出烂骰，也不会有神来一手。
;; 所以"派谁去哪一处"就是这场的全部策略：卡上写着考什么技能，骰盘上写着谁擅长什么。
;;
;; 节拍：弗兰克第二回合到；林第四回合到。第四回合真班表也会揭开——班表上是四个人，
;; 实际是六个，多的两个不上册。若玩家在前三回合提前点完四人，真名单会立刻揭开，
;; 给他一个在机器抵达前完成全部救援的狭窄窗口。
;;
;; 主角每回合流失 1 点冷静。出口是交锋功能区里已有的抽烟（1 支烟回 2 点），
;; 也就是说这一晚的代价在你出门前就决定了：那 15 金你买没买烟。
;;
;; 对外契约（world/码头坍塌.scm 依赖）：
;;   end-encounter 只返回“小型”或“大型”。乔不在这场里——玩家赶到以前
;;   他已经被救出；本事件只触发他的事故后人物线，现场的任何结果都不改变那条故事。

;; ============================================================
;; 场面状态
;; ============================================================

(define turn 1)
(define finished? #f)

(define victims '())          ; 全部人形目标，含还没被挖出来的
(define rescued-heads 0)      ; 已送上救护车的人数
(define dead-heads 0)         ; 没能等到的人数
(define roster-heads 4)       ; 现场认为下面有几个人；虚报揭穿后变成 6
(define rescued-names '())    ; 天亮时由弗兰克逐一核对
(define dead-names '())
(define extras-revealed? #f)
(define large-announced? #f) ; 仅防止本场重复演出，不写入世界状态

(define frank-here? #f)
(define lin-here? #f)

(define tide-clk
  (make-clock "涨潮" 4 'countdown
    "每次结束回合减一。归零后水回到泥滩，此后每回合所有还压在下面的人多失一格生命。"))
(tide-clk 'set! (tide-clk 'max))

(define (tide-in?) (tide-clk 'empty?))
(define (life-drain) (if (tide-in?) 2 1))

;; 灾难规模的唯一判断处。世界模块只保存这里返回的结果，不再重复按人数派生。
(define (disaster-scale)
  (if (>= dead-heads 4) "大型" "小型"))

(define (announce-large-disaster!)
  ;; 舆论反应只能发生在第二次坍塌之后；此前即使已经死了四人，也先等真名单揭开。
  (if (and extras-revealed?
           (equal? (disaster-scale) "大型")
           (not large-announced?))
      (begin
        (set! large-announced? #t)
        (spotlight! "记者到了"
          "闪光灯挤到封锁线外。公司的人也到了，先问的不是名单，是谁能对报纸开口。")
        (cond
          (lin-here?
           (play-dialogue!
             (line "尼尔" "让他们离封锁线远点。这里还在救人。")
             (line "林" "他们已经在问，是旧码头害死人，还是机器来晚了。")
             (line "弗兰克" "先把名字带出去。别让公司替死人说话。")))
          (frank-here?
           (play-dialogue!
             (line "尼尔" "让他们离封锁线远点。这里还在救人。")
             (line "弗兰克" "公司的人也来了。他们会先找一句能登报的话。")
             (line "尼尔" "那就让他们等。名单还没点完。")))
          (else
           (play-remote-banter!
             (line "码头工人" "记者堵在外面。公司也派人来了。")
             (line "尼尔" "谁都别进来。先把下面的人抬出去。"))))
        #t)
      #f))

;; 名单是一对并排的钟：救出来的和没能等到的，上限都是现场认为的人数（会变）。
;; 你立刻知道时间正在杀人，但不知道死的是谁、在哪一片底下。
;;
;; 这两根钟必须挂在一张**卡**上：根容器不会被画出来，也禁止挂 :clocks。
(define (rescued-clock-data)
  (list 'clock "送上救护车" rescued-heads roster-heads 'segments
        "班表上说下面有这么多人。"))

(define (lost-clock-data)
  (list 'clock "没能等到" dead-heads roster-heads 'segments
        "有人的生命归零就涨一格。挖开以前，你不会知道是哪一片底下。"))

;; 同伴亲手执行动作时，偶尔用一句话显出各自的工作方式。尼尔不在这里开口；
;; 一半概率留白，避免弗兰克一回合两颗骰、林一颗骰把现场播报队列塞满。
(define (companion-action-banter! kind result)
  (if (random-choice (list #t #f))
      (cond
        ((equal? (current-actor) '弗兰克)
         (play-banter! (line "弗兰克"
           (cond
             ((and (equal? kind '开挖) (equal? result '坏))
              "停手。梁在走。人退半步，别把下面的气口堵死。")
             ((and (equal? kind '开挖) (equal? result '中))
              "这边留两个人撑住。其余的跟我挪下一层。")
             ((and (equal? kind '开挖) (equal? result '好))
              "缆绳吃住了。一起抬，听我的数。")
             ((and (equal? kind '救人) (equal? result '坏))
              "别拽他的胳膊。先托住背，再找腿卡在哪。")
             ((and (equal? kind '救人) (equal? result '中))
              "担架往前。给他留条能喘气的缝。")
             (else
              "接住他。两个人抬肩，一个人看脚下。")))))
        ((equal? (current-actor) '林)
         (play-banter! (line "林"
           (cond
             ((and (equal? kind '开挖) (equal? result '坏))
              "停机。载荷在偏，继续压只会让裂口往下走。")
             ((and (equal? kind '开挖) (equal? result '中))
              "行程还够。先把重量锁在这里，再换支点。")
             ((and (equal? kind '开挖) (equal? result '好))
              "读数稳了。它能把这一层完整托起来。")
             ((and (equal? kind '救人) (equal? result '坏))
              "回一寸。机器没失手，是我给的角度错了。")
             ((and (equal? kind '救人) (equal? result '中))
              "保持这个间隙。人进去，机器不要再动。")
             (else
              "重量离开他了。现在把担架送进来。")))))
        (else #f))
      #f))

;; ============================================================
;; 受困者
;; ============================================================

(define (make-victim label person-name heads life-max out-max skill verb voice frank-line)
  (let ((life (make-clock "生命" life-max 'countdown "每次结束回合减一；涨潮后减二。"))
        (out (make-clock "抬出来" out-max 'segments "填满＝送上救护车。"))
        (revealed? #f)
        ;; 还没被埋下去的人不掉命。第二次坍塌之前，滑道那两个还站在岸上。
        (armed? #t)
        (state 'trapped))
    (life 'set! life-max)
    (let ()
      (define (rescue!)
        (set! state 'out)
        (set! rescued-heads (+ rescued-heads heads))
        (set! rescued-names (append rescued-names (list person-name)))
        (result-note! (string-append label "：上了救护车"))
        (if frank-here?
            (play-banter! (line "弗兰克" frank-line))
            #f))

      (define (die!)
        (set! state 'dead)
        (set! dead-names (append dead-names (list person-name)))
        (set! dead-heads (+ dead-heads heads))
        (announce-large-disaster!))

      (define (drain! n)
        (if (and armed? (equal? state 'trapped))
            (begin
              (life 'advance! (- 0 n))
              (if (life 'empty?)
                  (begin (die!) #t)
                  #f))
            #f))

      (define (push! amount)
        (out 'advance! amount)
        (if (out 'full?) (rescue!) #f))

      ;; 一个人就是一张卡：卡名是他是谁，两个钟是他还剩多久、你挖到哪儿了，
      ;; 副标题是这一下怎么做。不再套一层容器——那会把进度藏进下一屏。
      (define (action-node)
        (node label
          :subtitle (string-append verb "；坏：他沉一格、你 −1 冷静　中：+1　好：+2")
          :tags (list "高风险")
          :clocks (list (life 'render-data) (out 'render-data))
          :requires (list (req-die))
          :resolve (roll skill
            (outcome "他从你手里滑回去"
              (lambda ()
                (let ((died? (drain! 1)))
                  (if died?
                      (spotlight! "没能拉住"
                        (string-append label "滑回缝里。你再喊，下面没有回应。"))
                      (companion-action-banter! '救人 '坏)))
                (spend-actor-composure! 'player 1)))
            (outcome "松了一寸"
              (lambda ()
                (push! 1)
                (companion-action-banter! '救人 '中)))
            (outcome "撬出一条缝"
              (lambda ()
                (push! 2)
                (companion-action-banter! '救人 '好))))))

      ;; 只有还能救的人留在牌面上。救走的、没能等到的都从场上撤掉——
      ;; 前者上了车，后者记在「没能等到」那根钟上，不必再占一张卡。
      (define (render)
        (if (and revealed? (equal? state 'trapped)) (list (action-node)) '()))

      (lambda (msg . args)
        (cond
          ((equal? msg 'label) label)
          ((equal? msg 'trapped?) (equal? state 'trapped))
          ((equal? msg 'armed?) armed?)
          ((equal? msg 'revealed?) revealed?)
          ((equal? msg 'life) (life 'current))
          ((equal? msg 'voice) voice)
          ((equal? msg 'reveal!) (set! revealed? #t))
          ((equal? msg 'disarm!) (set! armed? #f))
          ((equal? msg 'arm!) (set! armed? #t))
          ((equal? msg 'drain!) (drain! (car args)))
          ((equal? msg 'heal!) (life 'advance! (car args)))
          ((equal? msg 'render) (render))
          (else (error (string-append "码头坍塌：" label " 收到未知消息"))))))))

(define (register-victim! v)
  (set! victims (append victims (list v)))
  v)

(define (living-trapped)
  ;; 尚未发生第二次坍塌的人不属于当前名单，否则“提前点完四人”永远不会成立。
  (filter (lambda (v) (and (v 'armed?) (v 'trapped?))) victims))

;; ============================================================
;; 暗格：四个人，两处什么都没有
;; ============================================================

(define (shuffle lst)
  (if (null? lst)
      '()
      (let ((pick (random-choice lst)))
        (let loop ((rest lst) (dropped? #f) (acc '()))
          (if (null? rest)
              (cons pick (shuffle (reverse acc)))
              (if (and (not dropped?) (equal? (car rest) pick))
                  (loop (cdr rest) #t acc)
                  (loop (cdr rest) dropped? (cons (car rest) acc))))))))

(define slot-bag (shuffle (list '人 '人 '人 '人 '空 '空)))
(define slot-cursor 0)

(define (draw-slot-content!)
  (let ((content (list-ref slot-bag slot-cursor)))
    (set! slot-cursor (+ slot-cursor 1))
    content))

;; 滑道底下不赌：那两个人是弗兰克亲口说出来的，对象也早就建好了。
(define (make-known-slot victim)
  (make-slot-with '已知 (lambda () victim) ""))

;; empty-line 是挖到空处时的一句旁白，不是一张卡——翻开是空的，这件事说一次就够了。
(define (make-slot victim-thunk empty-line)
  (make-slot-with (draw-slot-content!) victim-thunk empty-line))

(define (make-slot-with content victim-thunk empty-line)
  (let ((victim #f)
        (opened? #f))
    (if (equal? content '人) (set! victim (register-victim! (victim-thunk))) #f)
    (if (equal? content '已知) (set! victim (victim-thunk)) #f)
    (let ()
      (define (open!)
        (set! opened? #t)
        (cond
          (victim
           (victim 'reveal!)
           (if (victim 'trapped?)
               (begin
                 (result-note! (string-append (victim 'label) "：还活着"))
                 (play-banter! (line "世界" (victim 'voice))))
               (begin
                 (result-note! (string-append (victim 'label) "：来晚了"))
                 (play-banter! (line "世界" "你把碎料掀开，叫了一声。下面没有人应。")))))
          (else
            (result-note! "底下没有人")
            (play-banter! (line "世界" empty-line)))))

      ;; 上面塌下来的一层压到的是整个暗格，不管你有没有挖开它。
      (define (hurt!)
        (if (and victim (victim 'trapped?)) (victim 'drain! 1) #f))

      (define (render)
        (if (and opened? victim) (victim 'render) '()))

      (lambda (msg . args)
        (cond
          ((equal? msg 'open!) (open!))
          ((equal? msg 'hurt!) (hurt!))
          ((equal? msg 'alive-inside?) (and victim (victim 'trapped?)))
          ;; 底下那个人还剩几格；没有人（或已经没救）时给一个不会被选中的大数。
          ((equal? msg 'life-inside) (if (and victim (victim 'trapped?)) (victim 'life) 99))
          ((equal? msg 'render) (render))
          (else (error "码头坍塌：暗格收到未知消息")))))))

;; ============================================================
;; 废墟
;; ============================================================

;; 每片废墟两种挖法，差别不在快慢而在**谁来付账**：
;;   稳着挖——慢一格，出岔子时是你自己替下面的人挨那一下（冷静 −1；冷静见底才转成伤势）。
;;   硬掀开——快一格，出岔子时整片压实，底下每个还活着的人各失一格生命。
;; 稳的那手考的是眼力和门道（各片不同），硬掀一律考力量——所以弗兰克天生是干这个的人。
(define (make-site name steady-skill steady-verb rough-verb calls slot-a slot-b)
  (let ((dig (make-clock "挖开" 6 'segments "填满＝看清这片底下压着什么。"))
        (open? #f))
    (let ()
      (define (open!)
        (set! open? #t)
        (slot-a 'open!)
        (slot-b 'open!))

      (define (advance! n)
        (dig 'advance! n)
        (if (and (dig 'full?) (not open?)) (open!) #f))

      (define (collapse!)
        (let ((a-died? (slot-a 'hurt!))
              (b-died? (slot-b 'hurt!)))
          (result-note! "碎料又压实了一层")
          (if (or a-died? b-died?)
              (spotlight! "下面安静了"
                "碎料压下去以后，刚才还在响的地方没有了回应。")
              (companion-action-banter! '开挖 '坏))))

      (define (steady-node)
        (node steady-verb
          :subtitle "坏：冷静 −1（你挨那一下）　中：挖开 +1　好：+2"
          ;; 进度挂在卡本身，不只挂在外面那张地点牌——玩家投骰的时候要看得见它。
          :clocks (list (dig 'render-data))
          :requires (list (req-die))
          :resolve (roll steady-skill
            (outcome "你替他们挡了一下"
              (lambda ()
                (spend-actor-composure! 'player 1)
                (result-note! "梁头砸下来，你先伸的手")
                (companion-action-banter! '开挖 '坏)))
            (outcome "腾出一个人的位置"
              (lambda ()
                (advance! 1)
                (companion-action-banter! '开挖 '中)))
            (outcome "掀开一整层"
              (lambda ()
                (advance! 2)
                (companion-action-banter! '开挖 '好))))))

      (define (rough-node)
        (node rough-verb
          :subtitle "坏：这片底下的人各失一格生命　中：+2　好：+3"
          :tags (list "高风险")
          :clocks (list (dig 'render-data))
          :requires (list (req-die))
          :resolve (roll 'violence
            (outcome "整片塌下去" (lambda () (collapse!)))
            (outcome "整片挪开一段"
              (lambda ()
                (advance! 2)
                (companion-action-banter! '开挖 '中)))
            (outcome "连底一起掀翻"
              (lambda ()
                (advance! 3)
                (companion-action-banter! '开挖 '好))))))

      (define (render)
        (if open?
            (append (slot-a 'render) (slot-b 'render))
            (list
              (container-with-clocks name (list (steady-node) (rough-node))
                (list (dig 'render-data))))))

      (lambda (msg . args)
        (cond
          ((equal? msg 'name) name)
          ((equal? msg 'open?) open?)
          ;; 声音是这片废墟唯一的对外通道，它的强弱就是底下那个人的命：
          ;; 还有力气 → 慢下来 → 快听不见。挖开以前，玩家只能靠它分诊。
          ((equal? msg 'urgency) (min (slot-a 'life-inside) (slot-b 'life-inside)))
          ((equal? msg 'call-line)
           (let ((life (min (slot-a 'life-inside) (slot-b 'life-inside))))
             (cond
               ((>= life 4) (car calls))
               ((>= life 2) (cadr calls))
               (else (caddr calls)))))
          ((equal? msg 'calling?)
           (and (not open?)
                (or (slot-a 'alive-inside?) (slot-b 'alive-inside?))))
          ((equal? msg 'dig!) (advance! (car args)))
          ((equal? msg 'render) (render))
          (else (error (string-append "码头坍塌：" name " 收到未知消息"))))))))

;; ============================================================
;; 现场布置
;; ============================================================

(define site-stack
  (make-site "塌落的货垛" 'knowledge "一袋一袋搬" "掀翻整垛"
    (list "货垛底下有人在敲板子。三下，停一停，再三下。"
          "货垛那边的敲击慢下来了，中间隔得越来越久。"
          "货垛底下还有一声，很轻。你不确定是不是听错了。")
    (make-slot
      (lambda ()
        (make-victim "麻袋下的老工" "老丹尼" 1 6 4 'violence "刨开麻袋"
                     "他把一只手伸出来，让你看见他在哪。"
                     "老丹尼。他上个月还说要熬到儿子毕业。"))
      "麻袋堆底下只有洒出来的咖啡豆，和工头那本班表册。四个名字，笔迹很稳。")
    (make-slot
      (lambda ()
        (make-victim "板条箱下的人" "韦德" 1 5 4 'sharpness "撬开板条箱"
                     "箱子把他架住了，他还能自己挪一点。"
                     "他叫韦德。上个月才从北边来，还没记住这里的规矩。"))
      "板条箱一只只撬开，里面全是货，码得很齐。")))

(define site-derrick
  (make-site "断掉的吊杆" 'knowledge "解开钢缆" "撞开配重"
    (list "钢缆那头有人在拽货网。拽得动，出不来。"
          "货网还在动，幅度比刚才小了一半。"
          "钢缆绷了一下，就不动了。再没有第二下。")
    (make-slot
      (lambda ()
        (make-victim "货网里的司索工" "埃利斯" 1 6 4 'social "隔着网跟他说"
                     "他被货网缠住了，越挣网收得越紧。"
                     "他是我带出来的。第一天我就教过他，别跟网较劲。"))
      "货网底下是散开的整袋糖，白得晃眼。没有人。")
    (make-slot
      (lambda ()
        (make-victim "配重后的司炉" "老盖尔" 1 5 4 'knowledge "卸掉配重"
                     "他缩在配重和墙中间，只有一条缝。"
                     "老盖尔。他每天最早到，锅炉是他一个人捂热的。"))
      "配重后面摆着一双靴子，整整齐齐，没人穿走。")))

(define site-shed
  (make-site "压平的分拣棚" 'sharpness "一片片揭开" "掀起铁皮"
    (list "分拣棚那边有个女人的声音，压得很低，听不清在说什么。"
          "那个声音在叫一个名字，一遍一遍，一次比一次短。"
          "分拣棚只剩下呼吸声，隔着铁皮几乎听不见。")
    (make-slot
      (lambda ()
        (make-victim "铁皮下的女工" "玛拉" 1 5 4 'knowledge "撬开那一片"
                     "铁皮正压着她的胸口，她说话已经很费劲。"
                     "玛拉。她男人去年也是在这片码头没的。"))
      "铁皮掀开，底下是翻倒的分拣台和一地咖啡豆。")
    (make-slot
      (lambda ()
        (make-victim "木架下的分拣工" "露丝" 1 6 4 'violence "搬开木架"
                     "木架卡住了她的肩膀，她还能回答你。"
                     "她是玛拉的妹妹。两个人今天排的是同一班。"))
      "木架底下只有一地麻绳和空袋子。")))

(define sites (list site-stack site-derrick site-shed))   ; 第二次坍塌后会追加滑道

;; ============================================================
;; 第二次坍塌：滑道，和两个不上册的人
;; ============================================================
;;
;; 这两个人从头到尾都在场上，只是钟没走——泥滩那头塌下去之前，他们还站在岸上干活。
;; 班表上没有他们的名字，所以现场也没人把他们算进那"四个人"里。

(define slide-abe
  (make-victim "泥里的阿贝" "阿贝" 1 6 4 'violence "把阿贝拉出来"
               "他半个身子在泥里，还能抬起一只手。"
               "阿贝。他上个月才把弟弟也带上码头。"))
(define slide-boy
  (make-victim "滑道下的男孩" "诺亚" 1 5 4 'sharpness "撬开滑道板"
               "那孩子不喊，只是一直看着你这边。"
               "刚满十六。工牌都还没给他办。"))
(slide-abe 'disarm!)
(slide-boy 'disarm!)
(register-victim! slide-abe)
(register-victim! slide-boy)

(define site-slide
  (make-site "泥滩里的滑道" 'sharpness "顺着滑道下去" "砸开滑道板"
    (list "滑道底下有东西在动，泥面上冒着泡。"
          "泥面上的泡少了。还有人在下面撑着。"
          "泥面平下来了。只有一只手还露在外面。")
    (make-known-slot slide-abe)
    (make-known-slot slide-boy)))

(define (reveal-extras!)
  (set! extras-revealed? #t)
  (set! roster-heads 6)
  (slide-abe 'arm!)
  (slide-boy 'arm!)
  (set! sites (append sites (list site-slide)))
  (play-dialogue!
    (line "世界" "泥滩那头闷响了一声。旧滑道连着半截栈桥滑进了泥里。")
    (line "弗兰克" "那边不该有人的。我数过出工的人——四个。")
    (line "弗兰克" "工头刚才才说实话。今天赶装船，他多叫了两个不上册的。")
    (line "尼尔" "名字。")
    (line "弗兰克" "阿贝，还有诺亚，刚满十六。他们一直在滑道上倒袋子。"))
  ;; 若原名单上的四个人已经全部死亡，必须先播完第二次坍塌，再让记者抵达。
  (announce-large-disaster!))

;; ============================================================
;; 弗兰克与林：他们是真的入队
;; ============================================================

(define (frank-arrive!)
  (recruit-companion! '弗兰克 "弗兰克"
    (list (list 'violence 3) (list 'knowledge 0)
          (list 'sharpness 1) (list 'social 3)))
  ;; 他带一队人来，所以他的行动本来就不止一次。
  (set-actor-die-profile! '弗兰克 2 0 "")
  (set! frank-here? #t)
  (add-item! "香烟" 1)
  (play-dialogue!
    (line "弗兰克" "工会房听见汽笛就散了会。我带了十一个人，都是干这行的。")
    (line "尼尔" "你的人听谁的。")
    (line "弗兰克" "现在听你的。你站在这儿指挥，他们都看见了。")
    (line "弗兰克" "拿着，别抖。手抖的人抬不动人。")))

(define (lin-arrive!)
  (recruit-companion! '林 "林"
    (list (list 'violence 0) (list 'knowledge 4)
          (list 'sharpness 2) (list 'social 1)))
  ;; 机器的性质写在骰面上：它不掷骰，每次都是同一个数。
  (set-actor-die-profile! '林 1 4 "机器")
  (set! lin-here? #t)
  (play-dialogue!
    (line "林" "公司的车堵在栈桥口。我把机器从货栈那头开过来了。")
    (line "尼尔" "它能做什么。")
    (line "林" "它每一次都一样。这是它唯一的好处，也是它唯一的坏处。")
    (line "弗兰克" "今晚我不跟你争这个。告诉我它能吃住多少重量。")))

(define (dismiss-helpers!)
  (if frank-here? (dismiss-companion! '弗兰克) #f)
  (if lin-here? (dismiss-companion! '林) #f))

;; ============================================================
;; 回合
;; ============================================================

;; 每回合末统一结算生命：先掉命，再报丧，再让还活着的地方出声。
(define (drain-all!)
  (let ((n (life-drain))
        (before dead-heads))
    (let loop ((rest victims))
      (if (null? rest)
          #f
          (begin ((car rest) 'drain! n) (loop (cdr rest)))))
    (if (> dead-heads before)
        (begin
          (spotlight! "有一处不再出声"
            (if (tide-in?)
                "水已经漫过泥滩。刚才还在响的地方安静下来，救护车的人摇了摇头，去了下一处。"
                "刚才还在响的地方安静下来了。担架队在原地站了两秒，然后去了下一处。"))
          (if lin-here?
              (play-banter!
                (line "弗兰克" "把名字记下来。等天亮了有人会说这只是个数字。")
                (line "林" "我去把行程再收一寸。"))
              (if frank-here?
                  (play-banter! (line "弗兰克" "把名字记下来。别让它变成一个数字。"))
                  #f)))
        #f)))

;; 呼救是诚实的：响过的地方底下真有活人；一片曾经响过的废墟不再响了，
;; 就是那里的人已经没了。每回合必出一句，而且总是**最危险的那一片**先出声——
;; 这是玩家在挖开之前唯一能拿到的分诊情报，不能靠掷骰决定给不给。
(define (rescue-call!)
  (let ((pool (filter (lambda (s) (s 'calling?)) sites)))
    (if (null? pool)
        #f
        (let loop ((rest (cdr pool)) (worst (car pool)))
          (if (null? rest)
              (play-banter! (line (worst 'name) (worst 'call-line)))
              (loop (cdr rest)
                    (if (< ((car rest) 'urgency) (worst 'urgency)) (car rest) worst)))))))

(define (turn-events!)
  (if (= turn 2) (frank-arrive!) #f)
  (if (= turn 4) (lin-arrive!) #f)
  (if (and (= turn 4) (not extras-revealed?)) (reveal-extras!) #f)
  (if (and (tide-in?) (= turn 5))
      (play-banter!
        (line "世界" "水回来了。它先漫过泥滩，然后从每一条缝里进去。"))
      #f))

;; ============================================================
;; 结算
;; ============================================================

(define (all-settled?)
  (and extras-revealed? (null? (living-trapped))))

;; TODO：暂不保存“工人 / 机器”救援归功。等后续剧情确有消费点时，再单独设计归功规则；
;; 不根据最后一格、投入比例或当前完成回合提前制造 Flag。

(define (join-names names)
  (cond
    ((null? names) "没有")
    ((null? (cdr names)) (car names))
    (else (string-append (car names) "、" (join-names (cdr names))))))

(define (roll-call!)
  (if (null? dead-names)
      (play-dialogue!
        (line "弗兰克" (string-append "救护车上的：" (join-names rescued-names) "。"))
        (line "弗兰克" "六个名字，都有人应。"))
      (play-dialogue!
        (line "弗兰克" (string-append "救护车上的：" (join-names rescued-names) "。"))
        (line "弗兰克" (string-append "没能等到的：" (join-names dead-names) "。")))))

(define (closing-text)
  (cond
    ((= dead-heads 0)
     "封锁线撤开的时候，最后一副担架已经上了车。班表上的每个名字都有了去处。")
    ((<= dead-heads 3)
     "天亮以前，救护车走完了最后一趟。有人没能等到；名字会写在工会房的墙上。")
    (else
     "潮水盖住了还没挖开的地方。剩下的事不再叫救援，叫打捞。")))

(define (finish!)
  (if finished? (error "码头坍塌：救援已经结算") #t)
  (set! finished? #t)
  (spotlight! "旧栈桥上天亮了" (closing-text))
  (roll-call!)
  ;; 收场只让在场的人开口。
  (cond
    (lin-here?
     (play-dialogue!
       (line "林" "它今天做了它该做的事。")
       (line "弗兰克" "它做了。可要是那根吊杆去年修过，今晚一个人都不用挖。")
       (line "林" "两句话都是真的。难的是它们能不能同时被人听见。")))
    (frank-here?
     (play-dialogue!
       (line "弗兰克" "记住今晚是谁把人挖出来的。是站在这儿的这些人。")
       (line "弗兰克" "明天他们会说这码头太旧、该换。你听见了，替我记着这一句。")))
    (else #f))
  (let ((result (disaster-scale)))
    (dismiss-helpers!)
    (end-encounter result)))

;; 你以为点完了名单，弗兰克就在这时候拿着真班表走过来。名单归零的那一刻最适合翻这一页。
(define-rule "名单点完了"
  (lambda ()
    (and (not finished?) frank-here? (not extras-revealed?) (null? (living-trapped))))
  (lambda () (reveal-extras!)))

(define-rule "现场再没有可救的人"
  (lambda () (and (not finished?) (all-settled?)))
  (lambda () (finish!)))

(define-turn-rule "现场推进"
  (lambda () (not finished?))
  (lambda ()
    ;; 这一晚在你身上留下的东西：每回合固定 1 点冷静，出口是功能区里的烟。
    (spend-actor-composure! 'player 1)
    (drain-all!)
    (if (all-settled?)
        (finish!)
        (begin
          (tide-clk 'advance! -1)
          (set! turn (+ turn 1))
          (turn-events!)
          (rescue-call!)))))

;; ============================================================
;; 渲染
;; ============================================================

;; 现场只留三根钟：水还有多久回来、救出来几个、没能等到几个。
;; 别的都不立卡——回合数、谁到了场、底下是什么样子，全靠对白和插话说。
(define (dashboard)
  (clock-nodes (tide-clk 'render-data) (rescued-clock-data) (lost-clock-data)))

(define (on-encounter-enter)
  (spotlight! "封锁线里面"
    "三号栈桥连着货垛、吊杆和分拣棚一起塌了下去。先前抬走了几个，班表上还有四个人在下面，没有一个知道在哪。潮水正在回来。"))

(define (get-render-data)
  (container "码头坍塌"
    (append
      (dashboard)
      (apply append (map (lambda (s) (s 'render)) sites)))))
