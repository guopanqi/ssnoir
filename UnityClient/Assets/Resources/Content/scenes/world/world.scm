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
(load-file "world/第一幕同行.scm")
(load-file "world/第二幕.scm")
(load-file "world/第二幕生活.scm")
(load-file "world/第二幕节奏.scm")
(load-file "world/第二幕剧院.scm")
(load-file "world/第二幕余波.scm")
(load-file "world/第二幕公告.scm")
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

;; 第一幕拥有五天生活与彩排日程，同行模块拥有彩排后的邀请及交锋余波。
;; 世界只负责日历、地点可见性与存档转发。
;; demo 短篇的三次逼近（夜莺 / 萨姆 / 陌生人藏身处）已经搬进 docs/归档/Content/，
;; 见那里的 README：留作写法参考，不进游戏，也不再被 --validate 解析。

;; ── 世界回合边界 ─────────────────────────────────
;; 日终先推进日期；所有“新的一天玩家才看见”的章节事件放到 on-turn-start，
;; 由引擎在新骰发放后调用，并在睡眠黑幕亮起后播放。
;; turn-rule 是头插登记的，谁先跑本来取决于文件加载顺序——同一天里
;; 「今天是第几天」和「今天该结算什么」的先后不能靠目录排列来决定，
;; 所以章节的日终由这里直接调用，而不是各自登记一条规则。
(define-turn-rule "世界日历推进"
  (lambda () #t)
  (lambda ()
    (set! world-day (+ world-day 1))
    (set-global! '世界日 world-day)))

(define-turn-start-rule "第二章新日"
  (lambda () #t)
  (lambda () (第二章 'on-day-start!)))

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
    :children
      (apply append (map (lambda (loc) (loc 'render-data)) (current-locations)))))

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
  ;; 涉及多个 owner 的不变量必须等各自状态都恢复后再校验。
  (frank 'validate!)
)
