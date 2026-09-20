;; scenes/world/地图.scm - 地图上现在有哪些地方
;;
;; 一个地方能不能走进去，是三个不同的问题，各有各的拥有者。混成一个 open? 布尔，
;; 第二章那些「门重新关上」的内容就没地方写了。
;;
;;   发现?  玩家知不知道有这么个地方。**只从假变真，不回头**——那条街你已经走过了。
;;          它属于让你知道这地方的那件事（诊所是第一次受伤，试验场是林开的口）。
;;   够格?  有没有人肯让你进。读的是人物或章节的事实，不是地点自己的状态。
;;   开着?  今天进不进得去。封锁、只办一晚的私人场合、罢工封路都写在这里，
;;          由负责那件事的模块回答。
;;
;; 三问都成立才在地图上。缺省都是 #t：**只写这个地方真的需要的那一问**，
;; 顺序就是上面这个顺序（发现 → 够格 → 开着），写几个算几个。
;;
;; 目前「发现了但今天进不去」的表现还没有——那样的地方暂时只是不显示。
;; 结构先分开，是因为第二章要演的正是这件事：以前替你开的门，现在有人替你关上，
;; 而玩家应该看得见它还在那儿、也看得见为什么进不去。等表现层有了这一态再接。

(define (地点 loc . 三问)
  (define (第几问 n)
    (if (> (length 三问) n) (list-ref 三问 n) (lambda () #t)))
  (list loc (第几问 0) (第几问 1) (第几问 2)))

(define (地点-本体 entry) (car entry))
(define (地点-进得去? entry)
  (and ((cadr entry)) ((caddr entry)) ((cadddr entry))))

(define (地图)
  (list
    ;; 顺序＝地图上的先后，别随手调。
    (地点 home)
    (地点 dock)
    ;; 工棚在第二章「工棚正在搬空」那一拍的次日才空：入场节拍里就把脚下的地点撤掉，
    ;; 引擎会当场中断。设备和图纸搬去港务技术区后，由技术区接替它的位置
    ;; （暂时也落在它的锚点上）。
    (地点 test-workshop     (lambda () (and (lin 'workshop-open?) (not (林的机器 'workshop-vacated?)))))
    ;; 开场三天刻意是紧的：只有住处、码头、老街酒馆。
    ;; 酒馆开着是因为夜莺在那儿唱歌——委托人必须找得到人；但酒馆内部分两批放开，
    ;; 能凭空变出钱的门路（放贷的）等老街一起开，别让它拆掉勒索款筹集的压力。
    (地点 old-street-tavern (lambda () (>= (three-letters 'story-stage) 1)))
    ;; 诊所自己决定何时出现在地图上：第一次受伤那一刻（见 world/诊所.scm）。
    (地点 clinic            (lambda () (clinic 'visible?)))
    ;; 警察局在经理拿出第二封信后的次日开放。
    (地点 police-station    (lambda () (three-letters 'police-open?)))
    ;; 货运公司有两个合理入口：首演威胁明确，或第二章替沃尔特办完核赔、由他引荐代理。
    ;; **这一条现在是名不副实的，留着是为了不改第一章的行为。**
    ;; freight-open? 读的是第一章第三节那个活动窗口（story-stage=4 且首演未结算），
    ;; 首演一过它就变回假——一个会反悔的条件不该叫「你知道有这么个地方」。
    ;; 两条路本来是「或」的关系，拆不进三问里（三问是「与」）。
    ;; 第二章要用商业圈的话必须在这里重新决定：现在的行为是首演之后它会关上，
    ;; 除非沃尔特引荐过你。那时候再拆成 沃尔特引荐＝发现、第一章窗口＝临时开着。
    (地点 freight-company   (lambda () (or (three-letters 'freight-open?)
                                           (walter 'can-arrange-berth?))))
    (地点 residential-district (lambda () (three-letters 'old-street-open?)))
    (地点 theater           (lambda () (three-letters 'theater-open?)))
    ;; 布告栏暂时收起来：第一章的钱和活已经够多，它只是又一处要玩家自己去翻的地方。
    ;; 模块仍然加载、仍然存档；第二章它会换个身份回来——家里装了电话之后，
    ;; 委托自己打上门，而不是又一块要玩家去翻的板子。
    (地点 board             (lambda () #f))
    ;; ── 第二章开出来的地方 ─────────────────────────
    ;; 格兰德酒店在第二章开场就打开：它是上城的公共客厅，不是另一间酒馆。
    (地点 grand-hotel     (lambda () (第二章 'started?)))
    ;; 林进入项目核心后才能替尼尔担保。外围线在码头看得见门禁，但不能进。
    (地点 port-technical-zone (lambda () (林的机器 'has-access?)))
    ;; 报社从那篇文章见报那天起开：在那之前你没有理由去翻别人的排字房。
    ;; 它不跟阶段走——追查那条线整个都不跟阶段走。
    (地点 newsroom        (lambda () (封面上的夜莺 'published?)))
    ))

(define (筛地点 entries)
  (if (null? entries)
      '()
      (let ((entry (car entries)))
        (if (地点-进得去? entry)
            (cons (地点-本体 entry) (筛地点 (cdr entries)))
            (筛地点 (cdr entries))))))

(define (current-locations) (筛地点 (地图)))
