;; scenes/world/第二章/第二章.scm - 第二章「他们要夜莺」协调器
;;
;; 设计见 docs/第二章-骨架.md。这里只有三件事：
;;   现在是哪个阶段、下一个锚点在哪一天、进入一个阶段时这座城市公开发生了什么。
;; 晚宴上说了什么话、艾迪的培训怎么推进、机器现场怎么打——都不在这里。
;; 每件事一个文件，写进下面那张事件表；章节只知道它们什么时候开门、结果写回什么。
;;
;; 章节自己加载自己的文件。世界只加载这一个入口，加一场人物事件不必回头改 world.scm。
;;
;; 时间只有一份：世界日。章节存的是起始日和阶段起始日，「章节第几天」由减法得出，
;; 不另养一根每天自己加一的章节时钟——两份日历迟早对不上，读档那一刻尤其。
;;
;; 阶段只有两个，中间只切换一次：
;;   A 成功以后        城市在扩张：上城打开、钱好赚、案子看起来结了
;;   B 真相与替换逼近  城市在收紧：有人不再见你，机器正在开进老街
;; 锚点不切换世界。切换 Phase 的是「玩家真的开始碰不该碰的东西」，那是 B 的入口条件。

(load-file "world/第二章/晚宴.scm")
(load-file "world/第二章/人物事件/艾迪的培训.scm")
(load-file "world/第二章/人物事件/林的机器.scm")
(load-file "world/第二章/人物事件/弗兰克的地界.scm")
(load-file "world/第二章/制造一个故事.scm")
(load-file "world/第二章/机器进入老街.scm")

(define 第二章
  (let ()
    ;; ── 事件表 ──────────────────────────────────────
    ;; 存档键 + 事件。加一场事件：上面加一行 load-file，这里加一行，别处都不动。
    ;; 顺序＝日终结算的先后，也＝卡片在地点里的先后。它是被写下来的，不是
    ;; 文件加载顺序碰出来的——同一天里谁先发生，必须看得见。
    (define (事件表)
      (list (list "banquet" 晚宴)
            (list "eddie-training" 艾迪的培训)
            (list "lin-machine" 林的机器)
            (list "frank-turf" 弗兰克的地界)
            (list "make-a-story" 制造一个故事)
            (list "machines-arrive" 机器进入老街)))

    (define (事件们) (map cadr (事件表)))
    ;; 卷宗里只有这一条主线，锚点自己不再往卷宗里投卡——否则同一件事会摆两遍。
    ;; 卷宗里那一句主线跟着当前锚点走。锚点是主线必经的事件，按顺序排在这里；
    ;; 谁还没结，谁就是「现在这一段」。
    (define (锚点们) (list 晚宴 制造一个故事 机器进入老街))
    (define (当前锚点)
      (define (走 as)
        (if (null? as)
            机器进入老街                      ; 都结了，停在最后一个上
            (if (equal? ((car as) 'where) "")
                (走 (cdr as))
                (car as))))
      (走 (锚点们)))

    ;; ── 排期 ────────────────────────────────────────
    ;; 章节只说锚点哪天开门；那一天里发生什么归锚点自己。
    ;; 表里几行就是几个锚点，章节本身不知道「一共有几个」，也不该知道。
    (define 晚宴-第几天 3)

    ;; ── 状态 ────────────────────────────────────────
    (define 阶段 "未开始")        ; 未开始 / A / B
    (define 起始日 0)             ; 第一章结案的次日；0＝还没开始
    (define 阶段起始日 0)
    (define journal (make-journal))

    (define (开始了?) (not (equal? 阶段 "未开始")))
    ;; 章节第几天：起始那天是第 1 天。
    (define (第几天) (if (开始了?) (+ 1 (- world-day 起始日)) 0))
    ;; 章节第 n 天是世界的哪一天。事件问它，不要自己拿别的日子倒推。
    (define (第几天的日子 n) (+ 起始日 (- n 1)))
    (define (晚宴日) (第几天的日子 晚宴-第几天))

    ;; ── 进入阶段 ────────────────────────────────────
    ;; 公共变化写在这里，**只在真的跨过去的那一刻发生一次**。
    ;; 读档不会走到这儿：读档只恢复「已经是 A 了」，不重放进入 A 那天的通知与结算。
    ;; Phase B 不是「做完第二个锚点」自动来的，是玩家在那一晚真的碰到了
    ;; 不该碰的东西——所以由《制造一个故事》收场时调用，而不是排在日历上。
    (define (进入阶段-B!)
      (if (equal? 阶段 "A")
          (begin
            (set! 阶段 "B")
            (set! 阶段起始日 world-day)
            (journal 'add! "有人在第三封信到之前就排好了它该怎么见报。这件事没完。")
            (spotlight! "这件事没完"
              "第三封信不是莱恩写的。从今天起，上城那些好说话的人开始躲着你，而机器已经在往老街开。"))
          (error "第二章：只能从 A 进入 B")))

    (define (进入阶段-A!)
      (set! 阶段 "A")
      (set! 起始日 world-day)
      (set! 阶段起始日 world-day)
      (journal 'add! "报纸把首演那一晚写成了她的胜利。街上安静下来，钱头一次不那么紧。")
      (spotlight! "成功以后"
        "案子结了，报酬到手。夜莺突然成了全城都在谈的名字——两天后有一场晚宴，她要你陪她去。"))

    ;; ── 日终 ────────────────────────────────────────
    ;; **由世界的日历规则在推进日期之后直接调用**（见 world.scm），不自己登记
    ;; 日终规则。turn-rule 是头插的，谁先跑取决于文件加载顺序；同一天里
    ;; 「日期推进 → 章节开场 → 事件结算」这个次序太重要，不能交给目录结构决定。
    (define (on-day-end!)
      (if (and (not (开始了?)) (three-letters 'has-flag? '结案))
          (进入阶段-A!)
          #f)
      (map (lambda (e) (e 'on-day-end!)) (事件们))
      #t)

    ;; ── 卷宗 ────────────────────────────────────────
    (define (主线卷宗)
      (if (开始了?)
          (list (dossier "他们要夜莺"
                  :kind '主线
                  :status '进行中
                  :now ((当前锚点) 'now)
                  :where ((当前锚点) 'where)
                  :clocks ((当前锚点) 'clocks)
                  :log (journal 'render-data)))
          '()))

    (lambda args
      (let ((msg (car args)))
        (cond
          ;; 三条汇总全部走事件表，章节内部不再各写一份名单。
          ((equal? msg 'nodes-at) (收集内容 (事件们) 'nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) (收集内容 (事件们) 'arrivals-at (cadr args)))
          ((equal? msg 'dossier) (append (主线卷宗) (收集内容-无参 (事件们) 'dossier)))
          ((equal? msg 'on-day-end!) (on-day-end!))
          ((equal? msg 'started?) (开始了?))
          ;; 调试台专用：不等第一章结案，直接开场。
          ((equal? msg 'debug-start!) (if (开始了?) #f (进入阶段-A!)))
          ;; 调试用：给第二章补上它要读的第一章底子。第二章的人物线各自有前提——
          ;; 艾迪要认得你，弗兰克要认下过你；林那条留给晚宴，那是玩家自己的选择。
          ((equal? msg 'debug-cast!)
           (eddie 'debug-met!)
           (frank 'debug-approve!))
          ;; 调试用：直接站到 Phase B 的第一天。
          ((equal? msg 'debug-phase-b!)
           (if (开始了?) #f (进入阶段-A!))
           (if (equal? 阶段 "A")
               (begin (晚宴 'debug-settle!) (制造一个故事 'debug-settle!) (进入阶段-B!))
               #f))
          ((equal? msg 'phase) 阶段)
          ((equal? msg '进入阶段-B!) (进入阶段-B!))
          ((equal? msg 'sync-blockers!)
           (晚宴 'sync-blockers!)
           (制造一个故事 'sync-blockers!)
           (机器进入老街 'sync-blockers!))
          ((equal? msg 'day) (第几天))
          ((equal? msg 'day-of) (第几天的日子 (cadr args)))
          ((equal? msg 'phase-start-day) 阶段起始日)
          ((equal? msg 'banquet-day) (晚宴日))
          ((equal? msg 'journal) journal)
          ((equal? msg 'save)
           (append
             (list (list "phase" 阶段)
                   (list "start-day" 起始日)
                   (list "phase-start-day" 阶段起始日)
                   (list "journal" (journal 'save)))
             (map (lambda (row) (list (car row) ((cadr row) 'save))) (事件表))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! 阶段 (assoc-get data "phase" "未开始"))
             (set! 起始日 (assoc-get data "start-day" 0))
             (set! 阶段起始日 (assoc-get data "phase-start-day" 0))
             (journal 'load! (assoc-get data "journal" '()))
             (map (lambda (row) ((cadr row) 'load! (assoc-get data (car row) '())))
                  (事件表))
             #t))
          (else (error "第二章：收到未知消息")))))))
