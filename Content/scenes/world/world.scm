;; scenes/world/world.scm - 世界协调器（城市生活第一版）
;; 世界拥有日期与强制公共事件；地点只拥有自己的生活内容。

(load-file "world/人物/乔.scm")
(load-file "world/人物/弗兰克.scm")
(load-file "world/人物/阿瑟.scm")
(load-file "world/人物/沃尔特.scm")
(load-file "world/home.scm")
(load-file "world/码头.scm")
(load-file "world/老街酒馆.scm")
(load-file "world/三封信.scm")
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

;; ── 城市声望 ─────────────────────────────────────
;; 三派各有一条三档声望阶梯（相识/信任/核心，阈值 +2/+4/+6）。
;; 每档两项配置由内容定义、客户端据当前声望显示：
;;   relation-band-name:<势力>:<档>  该势力对这一档的定制称呼（面板档名与诱饵标题）
;;   relation-goal:<势力>:<档>       这一档解锁的具名诱饵
;; 每档只写一件确实能在 demo 里做的事；暂时做不到的，标"（demo 暂未开放）"，
;; 不堆第二个想法凑数——档位到了却只看见一句空话，比少写一档更打消玩家推进的意愿。
;; 门控仍用通用内部名（相识/信任/核心），见 engine.scm。
;;
;; 爬升方式随档位换（见 engine.scm 的 grant-work-relation!/grant-favor-relation!）：
;; 相识靠带薪工作混脸熟（到值 3 封顶）；信任靠不计报酬的帮忙类动作（到值 5 封顶）；
;; 核心只认事迹——人物小节/主线段落完成时才给，不封顶，是唯一能到核心的路。

;; 官僚〈秩序 · 程序 · 洗白〉：让麻烦消失、拿到程序特权。
(set-global! "relation-band-name:官僚:相识" "挂号")
(set-global! "relation-band-name:官僚:信任" "备案")
(set-global! "relation-band-name:官僚:核心" "有里子")
(set-global! "relation-goal:官僚:相识" "替阿瑟处理程序管不了的麻烦")
(set-global! "relation-goal:官僚:信任" "真出事时警察封锁线放行拦车·办通行证更快")
(set-global! "relation-goal:官僚:核心" "引荐市长秘书，遇事能求到更高处（demo 暂未开放）")

;; 劳工〈生存 · 暴力 · 销赃网〉：暴力援助与销赃/借钱网络。
(set-global! "relation-band-name:劳工:相识" "面熟")
(set-global! "relation-band-name:劳工:信任" "够朋友")
(set-global! "relation-band-name:劳工:核心" "自己人")
(set-global! "relation-goal:劳工:相识" "参与搁浅货船的紧急抢修")
(set-global! "relation-goal:劳工:信任" "替弗兰克查账并接触旧悬案")
(set-global! "relation-goal:劳工:核心" "走私工作；了断之日弗兰克带人到场")

;; 富商〈欲望 · 资本 · 科技圈层〉：资本/投资与上流圈层。
(set-global! "relation-band-name:富商:相识" "有往来")
(set-global! "relation-band-name:富商:信任" "座上宾")
(set-global! "relation-band-name:富商:核心" "合伙人")
(set-global! "relation-goal:富商:相识" "私货能出手·陪代理谈投资·接触保险核赔")
(set-global! "relation-goal:富商:信任" "私货满价收购·投资本金打折")
(set-global! "relation-goal:富商:核心" "引荐博士，牵出实验室科技线（demo 暂未开放）")

;; 第一章的节拍、到期日与必看事件全部由 three-letters 自己拥有
;; （见 world/三封信.scm）。世界只负责日历、地点可见性与存档转发，不解释故事。
;; demo 短篇的三次逼近（夜莺.scm / 萨姆.scm / 陌生人藏身处.scm）不再挂载，
;; 脚本留在仓库作为写法参考。

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
    ;; 开场三天刻意是紧的：只有旅馆、码头、老街酒馆、布告栏、公园、诊所。
    ;; 酒馆开着是因为夜莺在那儿唱歌——委托人必须找得到人；但酒馆内部分两批放开，
    ;; 能凭空变出钱的门路（放贷的）等老街一起开，别让它拆掉交割款的压力。
    (list old-street-tavern (lambda () (>= (three-letters 'story-stage) 1)))
    (list clinic          (lambda () #t))
    (list park            (lambda () #t))
    ;; 警察局与货运公司只在首演威胁明确后开放：前者提供后台保护，后者引出投资方的资本线。
    ;; 第二小节专注老街调查，不提前抛出无关地点。
    (list police-station  (lambda () (three-letters 'police-open?)))
    (list freight-company (lambda () (three-letters 'freight-open?)))
    (list residential-district
          (lambda () (or (three-letters 'old-street-open?) (joe 'residential-unlocked?))))
    (list theater         (lambda () (three-letters 'theater-open?)))
    (list insurance-company (lambda () (walter 'known?)))
    (list board           (lambda () #t))
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

;; ── 存档 ─────────────────────────────────────────
(define (world-save)
  (list
    (list "day" world-day)
    (list "home" (home 'save))
    (list "dock" (dock 'save))
    (list "old-street-tavern" (old-street-tavern 'save))
    (list "three-letters" (three-letters 'save))
    (list "clinic" (clinic 'save))
    (list "park" (park 'save))
    (list "police-station" (police-station 'save))
    (list "freight-company" (freight-company 'save))
    (list "joe" (joe 'save))
    (list "frank" (frank 'save))
    (list "arthur" (arthur 'save))
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
  (clinic 'load! (assoc-get data "clinic" '()))
  (park 'load! (assoc-get data "park" '()))
  (police-station 'load! (assoc-get data "police-station" '()))
  (freight-company 'load! (assoc-get data "freight-company" '()))
  (joe 'load! (assoc-get data "joe" '()))
  (frank 'load! (assoc-get data "frank" '()))
  (arthur 'load! (assoc-get data "arthur" '()))
  (walter 'load! (assoc-get data "walter" '()))
  (residential-district 'load! (assoc-get data "residential-district" '()))
  (theater 'load! (assoc-get data "theater" '()))
  (insurance-company 'load! (assoc-get data "insurance-company" '()))
  (board 'load! (assoc-get data "board" '()))
  (test 'load! (assoc-get data "test" '())))

;; 初始同步：新游戏没有存档数据时，也要阻塞第一晚的睡眠。
(three-letters 'sync-blockers!)
