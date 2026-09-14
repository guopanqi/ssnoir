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
(load-file "world/三封信.scm")
(load-file "world/艾迪.scm")
(load-file "world/诊所.scm")
(load-file "world/公园.scm")
(load-file "world/警察局.scm")
(load-file "world/货运公司.scm")
(load-file "world/居民区.scm")
(load-file "world/剧院.scm")
(load-file "world/保险公司.scm")
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
;; 每档只写一件确实能在 demo 里做的事。做不到的档位宁可写「尚无进一步关系」，
;; 也不拿以后的内容诱导玩家投资——空头承诺比少写一档更打消推进的意愿。
;; 两个圈子不必长得一样：老码头三档都有内容，商业圈第一章只认到座上宾。
;; 门控仍用通用内部名（相识/信任/核心），见 engine.scm。
;;
;; 爬升方式随档位换（见 engine.scm 的 grant-work-relation!/grant-favor-relation!）：
;; 相识靠带薪工作混脸熟（到值 3 封顶）；信任靠不计报酬的帮忙类动作（到值 5 封顶）；
;; 核心只认事迹——人物小节/主线段落完成时才给，不封顶，是唯一能到核心的路。

;; 老码头〈生存 · 组织 · 地方保护〉：做工建立面熟，具体事迹换来有边界的人手。
(set-global! "relation-band-name:老码头:相识" "面熟")
(set-global! "relation-band-name:老码头:信任" "够朋友")
(set-global! "relation-band-name:老码头:核心" "自己人")
(set-global! "relation-goal:老码头:相识" "码头开始把顶班这类零活转介给你")
(set-global! "relation-goal:老码头:信任" "老街和酒馆老板都认你的脸·去堆场那一夜手上多两把钥匙")
(set-global! "relation-goal:老码头:核心" "由重大事件获得：弗兰克肯为你组织人手")

;; 商业圈〈欲望 · 资本 · 上流圈层〉：资本、投资与代理人的引荐。
(set-global! "relation-band-name:商业圈:相识" "有往来")
(set-global! "relation-band-name:商业圈:信任" "座上宾")
(set-global! "relation-band-name:商业圈:核心" "合伙人")
(set-global! "relation-goal:商业圈:相识" "货运代理愿意引荐你")
(set-global! "relation-goal:商业圈:信任" "投资本金打折·拿得到预付与信用条件")
(set-global! "relation-goal:商业圈:核心" "尚无进一步关系")

;; 第一章的节拍、到期日与必看事件全部由 three-letters 自己拥有
;; （见 world/三封信.scm）。世界只负责日历、地点可见性与存档转发，不解释故事。
;; demo 短篇的三次逼近（夜莺 / 萨姆 / 陌生人藏身处）已经搬进 docs/归档/Content/，
;; 见那里的 README：留作写法参考，不进游戏，也不再被 --validate 解析。

;; ── 日终规则 ─────────────────────────────────────
;; 日终里唯一被写死的次序：日期先推进，然后章节结算。
;; turn-rule 是头插登记的，谁先跑本来取决于文件加载顺序——同一天里
;; 「今天是第几天」和「今天该结算什么」的先后不能靠目录排列来决定，
;; 所以章节的日终由这里直接调用，而不是各自登记一条规则。
(define-turn-rule "世界日历推进"
  (lambda () #t)
  (lambda ()
    (set! world-day (+ world-day 1))
    (set-global! '世界日 world-day)
    (第二章 'on-day-end!)))

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
  (clear-rest-blockers!)
  (three-letters 'debug-finish!)
  (debug-skip-days! 第一章天数)
  (if core? (第二章 'debug-cast-core-lin!) (第二章 'debug-cast!))
  (第二章 'debug-start!)
  (第二章 'sync-blockers!))

(define (debug-enter-chapter2!) (debug-enter-chapter2-with-lin! #f))
(define (debug-enter-chapter2-lin-core!) (debug-enter-chapter2-with-lin! #t))

(define (debug-enter-chapter2-phase-b-with-lin! core?)
  (clear-rest-blockers!)
  (three-letters 'debug-finish!)
  (debug-skip-days! 第一章天数)
  (if core? (第二章 'debug-cast-core-lin!) (第二章 'debug-cast!))
  (第二章 'debug-phase-b!)
  (第二章 'sync-blockers!))

(define (debug-enter-chapter2-phase-b!) (debug-enter-chapter2-phase-b-with-lin! #f))
(define (debug-enter-chapter2-phase-b-lin-core!) (debug-enter-chapter2-phase-b-with-lin! #t))

;; ── 地点可见性 ───────────────────────────────────
;; 地图上有哪些地方、凭什么进得去，写在 world/地图.scm。

;; ── 世界渲染 ─────────────────────────────────────
(define (get-render-data)
  (node "世界"
    :children
      (append
        (three-letters 'world-nodes)
        (three-letters 'render-data)
        (apply append (map (lambda (loc) (loc 'render-data)) (current-locations))))
    :clocks (three-letters 'world-clocks)))

;; ── 卷宗 ─────────────────────────────────────────
;; 谁有一条线要摆进卷宗，写在 world/模块清单.scm；世界只收集与排序，不解释故事。
(define (get-dossier) (模块卷宗))

;; ── 存档 ─────────────────────────────────────────
;; 存什么、键名叫什么，写在 world/模块清单.scm；世界只管自己的日历，再把其余转发过去。
(define (world-save)
  (cons (list "day" world-day) (模块存档)))

(define (world-load! data)
  ;; 顺序是有意的，三步不能换：
  ;;   一 清掉派生的运行时状态（休息阻塞会由各模块自己重新竖起来）
  ;;   二 恢复全部模块的状态
  ;;   三 才校验跨模块的不变量、重建派生状态
  ;; 读档只恢复「世界现在是什么样」，**不重放任何一天发生过的事**——
  ;; 进入阶段的通知、结算、发钱都写在各自的转场里，那里读档走不到。
  (clear-rest-blockers!)
  (set! world-day (assoc-get data "day" 1))
  (set-global! '世界日 world-day)
  (模块读档! data)
  ;; 涉及多个 owner 的不变量必须等各自状态都恢复后再校验。
  (frank 'validate!)
  (three-letters 'sync-globals!)
  ;; 休息阻塞是派生状态：上面清空过，这里由拥有必经拍的模块按恢复后的状态重新竖起来。
  (第二章 'sync-blockers!))

;; 初始同步：新游戏没有存档数据时，也要阻塞第一晚的睡眠。
(three-letters 'sync-blockers!)
