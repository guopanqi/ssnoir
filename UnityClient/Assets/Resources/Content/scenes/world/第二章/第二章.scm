;; scenes/world/第二章/第二章.scm - 第二章城市时间轴协调器
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
;; 第二章有两条轴，它们**互不等待**：
;;
;;   城市主轴（自动）  首演成功 → 晚宴 → 文章刊登 → 新港计划公布 → 准备部署 → 机器进入老街
;;                     按日子往前走。必经的那几拍会堵住休息，直到玩家到场处理完事件；
;;                     做完之后城市继续走，不回头等任何人。
;;
;;   调查支线（玩家主动）  文章刊登之后才有得查。玩家主动设局、尾随和取证；
;;                     两处诱饵完成后，早报等待钟才按世界日走。不查就停在当前阶段，
;;                     第二章照样结束。它不是世界轴的锚点，也不参与 Phase 切换。
;;
;; 阶段只有两个，中间只切换一次，而且**由城市说了算**：
;;   A 成功以后        城市在扩张：上城打开、钱好赚、案子看起来结了
;;   B 新港计划公布后  城市在往前推：机器排上了日程，人物都被这件事牵动
;; 调查可以在 A 或 B 中发生，也可以整章不发生。世界不等它，它也不改写城市日历。

(load-file "world/第二章/晚宴.scm")
(load-file "world/第二章/新港计划.scm")
(load-file "world/第二章/人物事件/艾迪的培训.scm")
(load-file "world/第二章/人物事件/林的机器.scm")
(load-file "world/第二章/人物事件/别给他们想要的.scm")
(load-file "world/第二章/谁把她卖给了报纸.scm")
(load-file "world/第二章/追查.scm")
(load-file "world/第二章/机器进入老街.scm")

(define 第二章
  (let ()
    ;; ── 事件表 ──────────────────────────────────────
    ;; 存档键 + 事件。加一场事件：上面加一行 load-file，这里加一行，别处都不动。
    ;; 顺序＝日终结算的先后，也＝卡片在地点里的先后。它是被写下来的，不是
    ;; 文件加载顺序碰出来的——同一天里谁先发生，必须看得见。
    (define (事件表)
      (list (list "banquet" 晚宴)
            (list "new-harbor-plan" 新港计划)
            (list "eddie-training" 艾迪的培训)
            (list "lin-machine" 林的机器)
            (list "frank-mediation" 别给他们想要的)
            (list "newspaper" 封面上的夜莺)
            (list "investigation" 追查)
            (list "machines-arrive" 机器进入老街)))

    (define (事件们) (map cadr (事件表)))
    ;; ── 排期 ────────────────────────────────────────
    ;; 章节只说锚点哪天开门；那一天里发生什么归锚点自己。
    ;; 表里几行就是几个锚点，章节本身不知道「一共有几个」，也不该知道。
    (define 晚宴-第几天 3)
    ;; 新港计划公布的日子。它不是谁做完了什么换来的——公司按自己的日程办事。
    (define 新港计划-第几天 9)

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
    ;; Phase B 由城市自己的日程带来：新港计划公开发布。它不问玩家查到哪一步，
    ;; 也不因为玩家什么都没做就推迟——公司按自己的日子办事。
    (define (进入阶段-B!)
      (if (equal? 阶段 "A")
          (begin
            (set! 阶段 "B")
            (set! 阶段起始日 world-day)
            (机器进入老街 'announce!)
            (journal 'add! "新港计划正式通过。第一批设备三天后进入老码头。")
            (play-remote-dialogue!
              (line "世界" "天刚亮，报童的喊声从街口一直追到窗下。")
              (line "报童" "新港计划通过！第一批机器三天后进老码头！")
              (line "世界" "公告列出了封闭泊位、调岗和培训的日期。")
              (line "尼尔" "以前他们谈的是计划。现在纸上有日子了。"))
            (spotlight! "三天后"
              "第一批设备将在三天后进入老码头。城市已经开始为那一天腾地方。"))
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
      ;; 城市主轴上的世界变化：到日子就发生，不看玩家做过什么。
      (if (and (equal? 阶段 "A")
               (>= world-day (第几天的日子 新港计划-第几天)))
          (进入阶段-B!)
          #f)
      (map (lambda (e) (e 'on-day-end!)) (事件们))
      #t)

    ;; ── 卷宗 ────────────────────────────────────────
    ;; 城市轴不是一条等玩家推进的长任务。只有某个必经节点已经被明确预告，
    ;; 或事件已经发生、正等玩家到场时，才短暂投一条实名主线。事件之间可以没有主线。
    (define (主线卷宗)
      (define (一条 标题 事件)
        (list (dossier 标题
                :kind '主线
                :status '进行中
                :now (事件 'now)
                :where (事件 'where)
                :clocks (事件 'clocks)
                :log (journal 'render-data))))
      (cond
        ((and (开始了?) (not (equal? (晚宴 'result) "已结束")))
         (一条 "格兰德酒店晚宴" 晚宴))
        ((equal? (封面上的夜莺 'state) "待去")
         (一条 "封面上的夜莺" 封面上的夜莺))
        ((or (equal? (机器进入老街 'state) "已公布")
             (equal? (机器进入老街 'state) "今天"))
         (一条 "机器进入老街" 机器进入老街))
        (#t '())))

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
          ;; 跳章预设只负责叫各人物自己收束第一章。
          ;; 协调器不直接猜他们各自的 stage，否则内部状态一改，Debug 就又会制造半份存档。
          ((equal? msg 'debug-cast!)
           (lin 'debug-finish-chapter1!)
           (eddie 'debug-finish-chapter1!)
           (frank 'debug-finish-chapter1!))
          ((equal? msg 'debug-cast-core-lin!)
           (lin 'debug-finish-chapter1-core!)
           (eddie 'debug-finish-chapter1!)
           (frank 'debug-finish-chapter1!))
          ;; 调试用：直接站到 Phase B 的第一天。
          ((equal? msg 'debug-phase-b!)
           (if (开始了?) #f (进入阶段-A!))
           (if (equal? 阶段 "A")
               (begin
                 (晚宴 'debug-settle!)
                 (封面上的夜莺 'debug-settle!)
                 (进入阶段-B!)
                 ;; 正常日历会在切阶段后继续分发当天结算；调试直达没有那一层，
                 ;; 只唤醒随 Phase B 开门的两个人物事件，不能把所有日终事件再跑一遍。
                 ;; 弗兰克那一拍属于 Phase A，直达 B 就是跳过了它：他没坐下来。
                 (艾迪的培训 'on-day-end!)
                 (林的机器 'on-day-end!))
               #f))
          ((equal? msg 'phase) 阶段)
          ((equal? msg '进入阶段-B!) (进入阶段-B!))
          ((equal? msg 'sync-blockers!)
           (晚宴 'sync-blockers!)
           (封面上的夜莺 'sync-blockers!)
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
