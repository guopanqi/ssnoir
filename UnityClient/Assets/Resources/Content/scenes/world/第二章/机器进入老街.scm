;; scenes/world/第二章/机器进入老街.scm - 锚点三《机器进入老街》
;;
;; 第二章的高潮，也是**整个游戏的第一次总检验**：你介入过这些人，现在第一次看看
;; 他们已经变成了什么样的人。所有人被同一件现实的事拉到一处——不是因为他们
;; 来参加剧情，是因为他们本来就都得在场。
;;
;; 这一场是**模块级兑现**的第一个真实样本。它不做「关系高→压力 -1」那种修正：
;;
;;   艾迪  他手上有没有另一条路 → 「艾迪在设备底下」**这一整块存不存在**。
;;         不是危险降低，是现场少一段：你走到本该撞见他的地方，只看见别人。
;;   林    他有没有真的看见后果 → 他主动告诉你哪儿会出事，还是你自己去查（更险）。
;;   弗兰克 他还坐不坐得下来 → 人群是等着看，还是一上来就带着砸设备的风险。
;;
;; 两份存档并排放，一眼就该看出这一场发生的事不一样——而不是去看后台数字。
;;
;; TODO：这一版是码头上的**一日场景**，用世界层的卡片写成。真正的形态是一场
;; 大型 Contract（encounters/），有自己的回合、压力钟和收场。等这三块兑现
;; 在实际游玩里验过之后再抬上去；先把「那一块在不在」做成真的。
(define 机器进入老街
  (let ()
    ;; 公布到进场六天（曾经三天）。艾迪考试在第 3 天、林那边试运行完还要陪老乔
    ;; 培训、调查线多半也正走到尾随——三天里全挤在一起，六天才排得开。
    (define 等待天数 6)

    ;; 未开始 / 已公布 / 今天 / 收场了
    (define 状态 "未开始")
    (define 到场? #f)
    (define 拦下艾迪? #f)
    (define 停了机? #f)
    (define 压住人群? #f)

    (define (已公布?) (equal? 状态 "已公布"))
    (define (今天?) (equal? 状态 "今天"))
    (define (进场日) (+ (第二章 'phase-start-day) 等待天数))

    ;; ── 三个人各自把什么带进这一场 ───────────────────
    ;; 读的都是人物模块上那一条跨章节事实，不读「第二章某个事件的第几个 flag」。
    (define (艾迪在场?) (and (eddie 'known?) (not (eddie 'has-way-out?))))
    (define (林肯说?) (lin 'humane?))
    (define (人群稳?) (frank 'at-table?))

    ;; ── 日历 ────────────────────────────────────────
    (define (公布!)
      (if (and (equal? 状态 "未开始") (equal? (第二章 'phase) "B"))
          (set! 状态 "已公布")
          (error "机器进入老街：只能在 Phase B 开始时公布")))

    (define (on-day-start!)
      (cond
        ((and (已公布?) (>= world-day (进场日)))
         (set! 状态 "今天")
         (sync-blockers!)
         (spotlight! "今天"
           "设备今天上岸。老街的人一早就站到了跳板两侧，警察也来了。"))
        (else #f)))

    ;; 堵一整天，不是只堵到你露个面。主轴上的一拍是「这件事你得处理完」，
    ;; 露面之后转身回家睡觉，等于把跳板上那一段睡过去了。
    ;;
    ;; 所以这一天的出口不是日终，是玩家自己按下的「看着这一天过完」。
    ;; 日终不再替他收场——否则阻塞和日终互相等，谁也走不动。
    (define (sync-blockers!)
      (rest-release! "第二章/机器进入老街")
      (cond
        ((and (今天?) (not 到场?))
         (rest-block! "第二章/机器进入老街" "机器今天上岸" "码头" "到跳板去"))
        ((今天?)
         (rest-block! "第二章/机器进入老街" "跳板上的事还没完" "码头" "看着这一天过完"))
        (#t #f)))

    ;; ── 到场 ────────────────────────────────────────
    ;; 不要骰：这是主轴上的必经拍，玩家没有「不去」这个选项，
    ;; 就不该再跟今天的城市生活抢那四颗骰——真花光了骰又睡不了觉，就是软锁。
    ;; 花骰的是跳板上那几张**可做可不做**的卡。
    (define (node-arrive)
      (instant-action "到跳板去"
        (lambda ()
          (set! 到场? #t)
          (sync-blockers!)
          (play-dialogue!
            (line "世界" "吊臂把第一台机器放到跳板上。它比想象中安静。")
            (line "世界" "两侧站满了人。警察在人群外围，手背在身后。")
            (line "林" "别让人靠太近。它启动的时候底下会走链条。"))
          (spotlight! "机器上岸了"
            (string-append
              "第一台设备落在跳板上。"
              (if (人群稳?)
                  "弗兰克的人站在前排，没有动。"
                  "人群往前挤，有人手里拎着东西。")
              (if (艾迪在场?) "你在人堆里看见了艾迪。" ""))))))

    ;; ── 艾迪那一整块：他有别的活路，这一段就根本不存在 ──
    (define (node-stop-eddie)
      (action "拦住艾迪" (list (req-die))
        (instant
          (outcome (lambda ()
              (set! 拦下艾迪? #t)
              (play-dialogue!
                (line "世界" "艾迪从人群里钻出去，手里攥着一根撬棍，往链条那头去。")
                (line "尼尔" "艾迪。")
                (line "艾迪" "它把我这份也顶了。培训我没考上。")
                (line "尼尔" "你砸了它，明天来的是警察，不是新岗位。")
                (line "世界" "他站在那儿，撬棍垂下来，没有再往前走。"))
              (spotlight! "你把他拦下了"
                "艾迪没有走到链条那头。他的手还在，工作没了。"))))))

    ;; ── 林：他肯不肯先开口 ──────────────────────────
    (define (node-stop-machine)
      (action "跟林停机" (list (req-die))
        (instant
          (outcome (lambda ()
              (set! 停了机? #t)
              (play-dialogue!
                (line "林" "第三节链条的护板还没上。人这么近，不能跑。")
                (line "尼尔" "那就停。")
                (line "林" "……停。今天不跑了。")
                (line "世界" "公司的人从后面挤过来，脸色很难看。"))
              (spotlight! "他自己停了机"
                "林当着所有人的面把机器停了。今天没有人受伤，公司记住了是谁按的闸。"))))))

    (define (node-check-machine)
      (roll-action "自己查设备" (list (req-die)) 'knowledge
        (outcome (lambda ()
            (spend-actor-composure! 'player 2)))
        (outcome (lambda ()
            (spend-actor-composure! 'player 1)))
        (outcome (lambda ()
            (set! 停了机? #t)
            (spotlight! "护板没上"
              "你自己看出第三节链条的护板没装上，喊停了这一趟。林在后面看着你。")))))

    ;; ── 弗兰克：他没坐下来的话，这一场就得你自己压 ──
    (define (node-hold-crowd)
      (roll-action "劝人群退" (list (req-die)) 'social
        (outcome (lambda ()
            (spend-actor-composure! 'player 2)))
        (outcome (lambda ()
            (spend-actor-composure! 'player 1)))
        (outcome (lambda ()
            (set! 压住人群? #t)
            (spotlight! "人群退开了"
              "你把前排劝开了半条跳板。警察的手从背后放了下来。")))))

    ;; ── 收场：这一天由玩家自己按下去 ────────────────
    ;; 它不是「结束回合」，是「我不打算再管了」。跳板上还摆着的每一张卡都是
    ;; 一次可以插手的机会；按下这张，就是选择站着看完。
    ;; 收场之后阻塞撤掉，今晚才睡得着。
    (define (node-watch-it-out)
      (instant-action "看着这一天过完"
        (lambda () (收场!))))

    (define (nodes-at location)
      (if (and (equal? location "码头") (今天?))
          (if 到场?
              (append
                ;; 这一整块是艾迪的模块级兑现：他有别的活路就根本不出现。
                (if (and (艾迪在场?) (not 拦下艾迪?)) (list (node-stop-eddie)) '())
                (if 停了机?
                    '()
                    (if (林肯说?) (list (node-stop-machine)) (list (node-check-machine))))
                (if (or (人群稳?) 压住人群?) '() (list (node-hold-crowd)))
                (list (node-watch-it-out)))
              (list (node-arrive)))
          '()))

    ;; ── 天黑 ────────────────────────────────────────
    (define (收场!)
      (set! 状态 "收场了")
      (追查 'close!)
      (林的机器 'close!)
      (sync-blockers!)
      (if (and (艾迪在场?) (not 拦下艾迪?))
          (eddie 'on-crushed!)
          #f)
      (第二章 'log! "机器进入老街" "第一批机器进了老码头。")
      (complete-task! "机器进入老街")
      (spotlight! "机器留下了"
        (string-append
          "设备卸完了，机器留在老码头。"
          (if 停了机? "今天它没有跑起来。" "它跑了一整个下午。")
          (cond
            ((not (艾迪在场?)) "艾迪不在这儿——他今天在别处上工。")
            (拦下艾迪? "艾迪站在人堆里看完了全程，手里的撬棍没有抡出去。")
            (#t "艾迪的手被链条压在了下面。这回是另一只。"))
          (if (or (人群稳?) 压住人群?)
              "人群散得还算安静。"
              "警察最后还是清了场。"))))

    ;; ── 卷宗 ────────────────────────────────────────
    ;; 锚点不自己往卷宗里投卡；公司公布进场日后，由章节协调器投入同名主线。

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) '())
          ((equal? msg 'dossier) '())
          ((equal? msg 'on-day-start!) (on-day-start!))
          ((equal? msg 'announce!) (公布!))
          ((equal? msg 'sync-blockers!) (sync-blockers!))
          ((equal? msg 'state) 状态)
          ((equal? msg 'announced?) (not (equal? 状态 "未开始")))
          ((equal? msg 'done?) (equal? 状态 "收场了"))
          ((equal? msg 'steps)
           (list (step "去码头跳板到场" 到场?)
                 (step "留在码头跳板收场" (equal? 状态 "收场了"))))
          ((equal? msg 'now)
           (cond ((已公布?) "倒计时归零那天去码头跳板；之前先办完艾迪、林和弗兰克的事")
                 ((今天?) (if 到场? "留在码头跳板，想插手的都办完，再按「看着这一天过完」" "今天。去码头"))
                 ((equal? 状态 "收场了") "机器留在老码头了")
                 ((equal? (第二章 'phase) "B") "新港计划已公布，公司正在准备部署")
                 (#t "机器还没到")))
          ((equal? msg 'where) (if (or (已公布?) (今天?)) "码头" ""))
          ((equal? msg 'clocks)
           (if (已公布?)
               (list (日期倒计时 "离机器进场" (进场日) 等待天数 "归零那天去码头跳板，所有人都会在。"))
               '()))
          ((equal? msg 'save)
           (list (list "state" 状态)
                 (list "came" (if 到场? 1 0))
                 (list "stopped-eddie" (if 拦下艾迪? 1 0))
                 (list "halted" (if 停了机? 1 0))
                 (list "held-crowd" (if 压住人群? 1 0))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! 状态 (assoc-get data "state" "未开始"))
             (set! 到场? (= (assoc-get data "came" 0) 1))
             (set! 拦下艾迪? (= (assoc-get data "stopped-eddie" 0) 1))
             (set! 停了机? (= (assoc-get data "halted" 0) 1))
             (set! 压住人群? (= (assoc-get data "held-crowd" 0) 1))))
          (else (error "机器进入老街：收到未知消息")))))))
