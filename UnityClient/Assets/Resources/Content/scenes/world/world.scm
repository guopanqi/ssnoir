;; scenes/world/world.scm - 世界协调器（城市生活第一版）
;; 世界拥有日期与强制公共事件；地点只拥有自己的生活内容。

(load-file "world/人物/弗兰克.scm")
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
(load-file "world/test.scm")

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
;; demo 短篇的三次逼近（夜莺 / 萨姆 / 陌生人藏身处）已经搬进 Content/archive/，
;; 见那里的 README：留作写法参考，不进游戏，也不再被 --validate 解析。

;; ── 日终规则 ─────────────────────────────────────
(define-turn-rule "世界日历推进"
  (lambda () #t)
  (lambda ()
    (set! world-day (+ world-day 1))
    (set-global! '世界日 world-day)))

;; ── 地点可见性 ───────────────────────────────────
(define location-predicates
  (list
    (list home            (lambda () #t))
    (list dock            (lambda () #t))
    (list test-workshop   (lambda () (lin 'workshop-open?)))
    ;; 开场三天刻意是紧的：只有住处、码头、老街酒馆、公园。
    ;; 酒馆开着是因为夜莺在那儿唱歌——委托人必须找得到人；但酒馆内部分两批放开，
    ;; 能凭空变出钱的门路（放贷的）等老街一起开，别让它拆掉勒索款筹集的压力。
    (list old-street-tavern (lambda () (>= (three-letters 'story-stage) 1)))
    ;; 诊所自己决定何时出现在地图上：第一次受伤那一刻（见 world/诊所.scm）。
    (list clinic          (lambda () (clinic 'visible?)))
    (list park            (lambda () #t))
    ;; 警察局在经理拿出第二封信后的次日开放。货运公司有两个合理入口：
    ;; 首演威胁明确，或更早替沃尔特办完核赔、由他介绍货运代理。
    (list police-station  (lambda () (three-letters 'police-open?)))
    (list freight-company
          (lambda ()
            (or (three-letters 'freight-open?)
                (walter 'can-arrange-berth?))))
    (list residential-district
          (lambda () (three-letters 'old-street-open?)))
    (list theater         (lambda () (three-letters 'theater-open?)))
    (list insurance-company (lambda () (walter 'known?)))
    ;; 布告栏暂时收起来：第一章的钱和活已经够多，它只是又一处要玩家自己去翻的地方。
    ;; 模块仍然加载、仍然存档，也许第二章再放出来。
    (list board           (lambda () #f))
    (list test            (lambda () (equal? (get-global 'chapter) "test")))))

(define (filter-locations entries)
  (if (null? entries)
      '()
      (let ((entry (car entries)))
        (if ((cadr entry))
            (cons (car entry) (filter-locations (cdr entries)))
            (filter-locations (cdr entries))))))

(define (current-locations)
  (filter-locations location-predicates))

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
;; 拥有故事的模块各回自己那一条（零条或一条），世界只收集与排序，不解释故事——
;; 和地点可见性一样。顺序＝这个表的顺序，客户端在此之上把「了结」的沉到最后。
(define dossier-owners
  (list three-letters frank lin eddie))

(define (collect-dossier owners)
  (if (null? owners)
      '()
      (append ((car owners) 'dossier) (collect-dossier (cdr owners)))))

(define (get-dossier) (collect-dossier dossier-owners))

;; ── 存档 ─────────────────────────────────────────
(define (world-save)
  (list
    (list "day" world-day)
    (list "home" (home 'save))
    (list "dock" (dock 'save))
    (list "old-street-tavern" (old-street-tavern 'save))
    (list "three-letters" (three-letters 'save))
    (list "eddie" (eddie 'save))
    (list "clinic" (clinic 'save))
    (list "park" (park 'save))
    (list "police-station" (police-station 'save))
    (list "freight-company" (freight-company 'save))
    (list "frank" (frank 'save))
    (list "lin" (lin 'save))
    (list "baines" (baines 'save))
    (list "walter" (walter 'save))
    (list "residential-district" (residential-district 'save))
    (list "theater" (theater 'save))
    (list "insurance-company" (insurance-company 'save))
    (list "board" (board 'save))
    (list "test" (test 'save))))

(define (world-load! data)
  (clear-rest-blockers!)
  (set! world-day (assoc-get data "day" 1))
  (set-global! '世界日 world-day)
  (home 'load! (assoc-get data "home" '()))
  (dock 'load! (assoc-get data "dock" '()))
  (old-street-tavern 'load! (assoc-get data "old-street-tavern" '()))
  (three-letters 'load! (assoc-get data "three-letters" '()))
  (eddie 'load! (assoc-get data "eddie" '()))
  (clinic 'load! (assoc-get data "clinic" '()))
  (park 'load! (assoc-get data "park" '()))
  (police-station 'load! (assoc-get data "police-station" '()))
  (freight-company 'load! (assoc-get data "freight-company" '()))
  (frank 'load! (assoc-get data "frank" '()))
  (lin 'load! (assoc-get data "lin" '()))
  (baines 'load! (assoc-get data "baines" '()))
  (walter 'load! (assoc-get data "walter" '()))
  (residential-district 'load! (assoc-get data "residential-district" '()))
  (theater 'load! (assoc-get data "theater" '()))
  (insurance-company 'load! (assoc-get data "insurance-company" '()))
  (board 'load! (assoc-get data "board" '()))
  (test 'load! (assoc-get data "test" '()))
  ;; 涉及多个 owner 的不变量必须等各自状态都恢复后再校验。
  (frank 'validate!)
  (three-letters 'sync-globals!))

;; 初始同步：新游戏没有存档数据时，也要阻塞第一晚的睡眠。
(three-letters 'sync-blockers!)
