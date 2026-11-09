(load-file "scripts/triggered-events.scm")
;; scenes/world/world.scm - 世界协调器（城市生活第一版）
;; 世界拥有日期与强制公共事件；地点只拥有自己的生活内容。

;; 模块清单先加载：它只定义函数，不引用任何模块，反过来被所有地点使用。
(load-file "world/模块清单.scm")
(load-file "world/地图.scm")
(load-file "world/人物/弗兰克.scm")
(load-file "world/人物/夜莺.scm")
(load-file "world/人物/林.scm")
(load-file "world/人物/贝恩斯.scm")
(load-file "world/人物/沃尔特.scm")
(load-file "world/home.scm")
(load-file "world/码头.scm")
(load-file "world/试验工棚.scm")
(load-file "world/老街酒馆.scm")
(load-file "world/咖啡馆.scm")
(load-file "world/第一幕.scm")
(load-file "world/人物/夜莺生活.scm")
(load-file "world/人物/露西起步.scm")
(load-file "world/成长/码头工作.scm")
(load-file "world/成长/调查职业.scm")
(load-file "world/事件/酒馆起步.scm")
(load-file "world/事件/大船靠岸.scm")
(load-file "world/事件/居民区相识.scm")
(load-file "world/事件/院里的炉火.scm")
(load-file "world/第一幕同行.scm")
(load-file "world/第二幕.scm")
(load-file "world/第二幕生活.scm")
(load-file "world/城市事件/城市事件.scm")
(load-file "world/城市事件/经理的首单.scm")
(load-file "world/城市事件/Tommy收账.scm")
(load-file "world/第二幕节奏.scm")

(load-file "world/第二幕剧院.scm")

(load-file "world/第三幕.scm")
(load-file "world/人物/艾迪生活.scm")
(load-file "world/人物/艾迪的房间.scm")
(load-file "world/两杯咖啡.scm")
(load-file "world/艾迪.scm")
(load-file "world/诊所.scm")
(load-file "world/警察局.scm")
(load-file "world/货运公司.scm")
(load-file "world/居民区.scm")
(load-file "world/剧院.scm")
(load-file "world/board.scm")
;; 第二章开出来的地方
(load-file "world/格兰德酒店.scm")
(load-file "world/港务技术区.scm")
(load-file "world/报社.scm")
;; 章节只暴露一个入口；它自己的事件由它自己加载。
(load-file "world/第二章/第二章.scm")
(load-file "world/前期存档迁移.scm")

;; ── 世界级状态 ───────────────────────────────────
(define world-day 1)
(set-global! '世界日 world-day)

;; ── 圈内声誉 ─────────────────────────────────────
;; 两个圈子各有一条三档声誉阶梯（相识/信任/核心，阈值 +2/+4/+6）。
;; 它记的是「你的名声在哪个圈子里传开了」——不是阵营，也没有成员名单；人物只是入口。
;; 市政、警署与医院不在这里：那些事由具名人物状态承担（贝恩斯认不认你、欠不欠你一次）。
;;
;; 每档两项配置由内容定义、客户端据当前声誉显示：
;;   relation-band-name:<圈子>:<档>  该圈子对这一档的定制称呼（面板档名与诱饵标题）
;;   relation-goal:<圈子>:<档>       这一档解锁的具名诱饵
;; 没有「圈子声誉」这一层：你和一个人的关系就是你和那一片的关系
;; （艾迪＝码头，弗兰克＝老街，沃尔特＝上城），各写在人物模块里。

;; 第一幕拥有公共演出日程；人物生活、工作成长、口碑和具体事件各有自己的拥有者。
;; 世界只负责日历、地点可见性与存档转发。
;; demo 短篇的三次逼近（夜莺 / 萨姆 / 陌生人藏身处）已经搬进 docs/归档/Content/，
;; 见那里的 README：留作写法参考，不进游戏，也不再被 --validate 解析。

;; ── 世界回合边界 ─────────────────────────────────
;; 日终推进日期；on-turn-start 更新新日状态，不等玩家走出住所。
;; 醒来经历留在新日表现中；公共消息由世界根 arrival、人物现场消息由地点 arrival 告知。
;; turn-rule 是头插登记的，谁先跑本来取决于文件加载顺序——同一天里
;; 「今天是第几天」和「今天该结算什么」的先后不能靠目录排列来决定，
;; 所以章节的日终由这里直接调用，而不是各自登记一条规则。
(define-turn-rule "世界日历推进"
  (lambda () #t)
  (lambda ()
    (set! world-day (+ world-day 1))
    (set-global! '世界日 world-day)))

(define-turn-start-rule "城市事件新日"
  (lambda () #t)
  (lambda () (调查职业 'on-day-start!) (城市事件 'on-day-start!)))

(define-turn-start-rule "第二章新日"
  (lambda () #t)
  (lambda () (第二章 'on-day-start!)))

(define-turn-start-rule "人物新日互动"
  (lambda () #t)
  (lambda () (dispatch-interactions! (list (list "timing" "新日开始")))))

;; ── 调试台：跳章 ─────────────────────────────────
;;
;; 跳章不是「把某个开关拨到第二章」。玩家从第一章走过来的时候，世界不只是
;; 多了几个 flag，它还**过了两个星期**：房租收过一轮、码头靠岸转过几圈、
;; 布告栏换过几批委托、人物那些「到某天就过去了」的窗口早就关上了。
;; 日历停在第 1 天而故事停在第二章，是两份互相矛盾的世界状态——
;; 第一章遗留的定日事件会在第二章头几天陆续冒出来，看起来就像「乱触发」。
;;
;; 所以跳章统一走这里：先把第一章按已结案收口，再把日历推到一个说得过去的
;; 日子，然后才让第二章开场。**这是唯一一处允许直接改 world-day 的地方**，
;; 而且只有调试台会调用它。
;;
;; 用跳章、不用「准备好的存档」：存档的键名归内容所有，改一次内容就可能读不回来；
;; 跳章只依赖那几条 debug 消息，内容怎么改它都还在。
(define 第一章天数 14)   ; 正常打完第一章大致要这么多天；只用于跳章，不参与任何结算

(define (debug-skip-days! n)
  (set! world-day (+ world-day n))
  (set-global! '世界日 world-day))

(define (debug-enter-chapter2-with-lin! core?)
  ;; Debug 跳章是全量预设，不继承当前测试局的阻塞。
  ;; 随后由第一、二章的合法结束态重建它们真正需要的阻塞。

  (第一幕 'debug-finish!)
  (debug-skip-days! 第一章天数)
  (if core? (第二章 'debug-cast-core-lin!) (第二章 'debug-cast!))
  (第二章 'debug-start!)
  )

(define (debug-enter-chapter2!) (debug-enter-chapter2-with-lin! #f))
(define (debug-enter-chapter2-lin-core!) (debug-enter-chapter2-with-lin! #t))

(define (debug-enter-chapter2-phase-b-with-lin! core?)

  (第一幕 'debug-finish!)
  (debug-skip-days! 第一章天数)
  (if core? (第二章 'debug-cast-core-lin!) (第二章 'debug-cast!))
  (第二章 'debug-phase-b!)
  )

(define (debug-enter-chapter2-phase-b!) (debug-enter-chapter2-phase-b-with-lin! #f))
(define (debug-enter-chapter2-phase-b-lin-core!) (debug-enter-chapter2-phase-b-with-lin! #t))

;; 准备到「货船刚靠岸」的状态。调试者随后正常进入码头，
;; 让正式入场路径触发靠岸演出，而不是从世界地图上硬播一段地点镜头。
(define (debug-prepare-ship-repair!)
  (frank 'debug-reset-ship-repair!))

;; ── 地点可见性 ───────────────────────────────────
;; 地图上有哪些地方、凭什么进得去，写在 world/地图.scm。

;; ── 世界渲染 ─────────────────────────────────────
(define (get-render-data)
  (node "世界"
    :arrivals (地点入场 "世界")
    :children
      (append (地点节点 "世界") (apply append (map (lambda (loc) (loc 'render-data)) (current-locations))))))

;; ── 卷宗 ─────────────────────────────────────────
;; 谁有一条线要摆进卷宗，写在 world/模块清单.scm；世界只收集与排序，不解释故事。
(define (get-dossier) (模块卷宗))

;; ── 存档 ─────────────────────────────────────────
;; 存什么、键名叫什么，写在 world/模块清单.scm；世界只管自己的日历，再把其余转发过去。
(define (world-save)
  (cons (list "day" world-day) (模块存档)))

(define (world-load! data)
  (if (assoc-get data "three-letters" #f)
      (error "旧版第一章存档请使用归档版本读取；新第一幕请开始新游戏。") #t)
  ;; 只恢复剧情状态；必须处理事件由随后构建的世界树派生，不重放对白。
  (set! world-day (assoc-get data "day" 1))
  (set-global! '世界日 world-day)
  (模块读档! data)
  (前期存档迁移! data)

  ;; 退役演出服只迁移已入行事实，不恢复入口、不重发报酬。
  (if (assoc-get data "manager-first-job" #f) #t
    (let* ((events (assoc-get (assoc-get data "city-events" '()) "events" '()))
           (old-stage (assoc-get (assoc-get events "经理的演出服" '()) "stage"
             (assoc-get (assoc-get data "act-two-city" '()) "contract" "未提起"))))
      (if (equal? old-stage "完成")
        (经理的首单 'load! (list (list "stage" "已交差") (list "result" "成功"))) #f)))
  (if (or (equal? (assoc-get (经理的首单 'save) "stage" "") "已交差")
          (equal? (assoc-get (乔的手提箱 'save) "stage" "") "完成"))
    (begin (set-global! '身份 "侦探")
      (if (and (get-global '侦探入行日) (> (get-global '侦探入行日) 0)) #t (set-global! '侦探入行日 world-day)))
    (begin (set-global! '身份 "") (set-global! '侦探入行日 0)))
  ;; 涉及多个 owner 的不变量必须等各自状态都恢复后再校验。
  (frank 'validate!)
)

;; 声明顺序为稳定选择顺序；一处检查最多呈现一段旁侧互动。
(define interaction-dispatching? #f)
(define (dispatch-interactions! context)
  (if interaction-dispatching? (error "触发事件：不允许递归派发") #t)
  (set! interaction-dispatching? #t)
  (let ((entries (apply append (map (lambda (module) (module 'interactions)) (担任 '互动)))))
    (if (= (length entries) (length (unique-interaction-ids entries))) #t
      (error "触发事件：重复Id"))
    ;; 过期收尾不依赖今天是否有主线对白；选择与播放仍让主线优先。
    (map (lambda (entry) (entry 'refresh!)) entries)
    (let ((played? (if (__interaction-allowed?) (try-interactions! entries context) #f)))
      (set! interaction-dispatching? #f) played?)))
(define (unique-interaction-ids entries)
  (if (null? entries) '()
    (let ((id ((car entries) 'id)))
      (cons id (filter (lambda (other) (not (equal? id other)))
        (unique-interaction-ids (cdr entries)))))))
