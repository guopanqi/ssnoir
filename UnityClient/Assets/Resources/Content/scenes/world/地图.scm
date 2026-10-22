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
  (or (and (get-global '演示地点全开) (not (eq? (地点-本体 entry) board)))
      (and ((cadr entry)) ((caddr entry)) ((cadddr entry)))))

(define (地图)
  (list
    (地点 home)
    (地点 old-street-tavern (lambda () (第一幕 'opened?)))
    (地点 dock (lambda () (第一幕 'work-open?)))
    (地点 cafe (lambda () (第一幕 'work-open?)))
    (地点 test-workshop (lambda () (and (lin 'workshop-open?) (not (林的机器 'workshop-vacated?)))))
    (地点 clinic (lambda () (clinic 'visible?)))
    (地点 police-station (lambda () (第二章 'started?)))
    (地点 freight-company (lambda () (walter 'can-arrange-berth?)))
    (地点 residential-district (lambda () (or (>= (第一幕 'act) 2) (第二章 'started?))))
    (地点 theater (lambda () (or (第一幕 'theater-open?) (第二章 'started?))))
    (地点 board             (lambda () #f))
    ;; ── 第二章开出来的地方 ─────────────────────────
    ;; 旧照片委托已退役；酒店保留旧第二章的独立调试入口。
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
