;; 让他们安静——贝恩斯把一张地址推过桌子之后的那一晚。
;;
;; 目标不是「教训三个人」，是**让那条街安静下来**。所以只有一根目标钟：麻烦。
;; 怎么让它降下去，贝恩斯完全不关心——他七天以后只看有没有新的报案。
;;
;; 这里刻意不给三个混混各写一套弱点，那是三条内容线。场上有哪几张牌，由**你
;; 这些天真认识的人**决定（和《巷子》同一套钥匙语法）：码头认得你、老街混熟了、
;; 见过艾迪的场子，各开一张。什么都没有的玩家也打得完，只是手里只剩拳头和嘴。
;;
;; 手段标签不改流程，只改这件事**在别处**留下什么：
;;   暴力 —— 伤情报告；他要的结果拿到了，但医院也归市政府管
;;   交易 —— 花掉的钱和人情，记在老街那边
;;   施压 —— 只算延期
;; 判定方式：三种手段各记一笔，最后取笔数最多的那种；打平按暴力 > 施压 > 交易，
;; 因为他先看见的永远是留下痕迹的那一种。
;;
;; 对外契约：回传 (list 'success/'fail 手段)。见 world/人物/贝恩斯.scm 的 on-quiet-result。

(define (key? name)
  (let ((v (get-global name))) (if v v #f)))

(define key-dock  (key? '钥匙-码头))   ; 老码头·相识：码头那边肯替你带话
(define key-street (key? '钥匙-老街))  ; 老码头·信任：酒馆老板肯站在你这边
(define key-ring  (key? '钥匙-赌场))   ; 见过艾迪的场子：知道他们欠谁的钱

(define trouble-max 3)
(define heat-max 3)

(define trouble-clk
  (make-clock "麻烦" trouble-max 'countdown
    "老街这个星期还剩多少事。归零就算安静了——贝恩斯不问你是怎么做到的。"))

(define heat-clk
  (make-clock "事态恶化" heat-max 'gauge
    (lambda (current max)
      (cond
        ((>= current max) "巡警已经在路上了。")
        ((> current 0) "有人报了警，或者有人开始还手。")
        (else "现在还只是三个人和你。")))))

;; 手段计数
(define force-n 0)
(define trade-n 0)
(define press-n 0)

(define finished? #f)

(define (route)
  (cond
    ((and (>= force-n trade-n) (>= force-n press-n) (> force-n 0)) "暴力")
    ((and (>= press-n trade-n) (> press-n 0)) "施压")
    ((> trade-n 0) "交易")
    (else "")))

(define (finish! result title text)
  (if finished? (error "让他们安静：交锋已经结算") #t)
  (set! finished? #t)
  (spotlight! title text)
  (end-encounter (list result (route))))

(define (on-encounter-collapse)
  (collapse-result (list 'fail (route))))

(define (check!)
  (if finished?
      #f
      (cond
        ((trouble-clk 'empty?)
         (finish! 'success "那条街安静了"
           "第二天老街照常开门。收钱的那三个没有出现，商户也没有人提起为什么。"))
        ((heat-clk 'full?)
         (finish! 'fail "巡警到了"
           "两辆车停在街口。等笔录做完，这件事已经变成一份必须有人签字的报告。"))
        (else #f))))

(define (on-encounter-enter)
  ;; 交锋初始盘面不是玩家造成的 Clock 变化，不写入场报告的效果条（同 失控的机械）。
  (trouble-clk 'load! trouble-max)
  (play-dialogue!
    (line "世界" "地址是老街尽头一间修车棚。三个人坐在门口，其中一个数着零钱。")
    (line "世界" "隔壁杂货铺的卷帘门放下来一半，人还在里面。")
    (line "世界" "这条街这个星期已经报过两次警。第三次贝恩斯就得走程序。")))

;; ── 常驻的两张：拳头和嘴 ─────────────────────────
(define (node-drag)
  (node "把他拖出去"
    :tags (list "高风险")
    :subtitle "武力；当着整条街的面，让他们知道下次是什么代价"
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome (lambda () (heat-clk 'tick!) (spend-composure! 2) (check!)))
      (outcome (lambda () (set! force-n (+ force-n 1))
                   (trouble-clk 'advance! -1) (heat-clk 'tick!) (check!)))
      (outcome (lambda () (set! force-n (+ force-n 1))
                   (trouble-clk 'advance! -1) (check!))))))

(define (node-sit)
  (node "坐下来谈"
    :subtitle "他们要的是这个月的数，不是这条街"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome (lambda () (heat-clk 'tick!) (check!)))
      (outcome (lambda () (set! press-n (+ press-n 1)) (trouble-clk 'advance! -1) (check!)))
      (outcome (lambda () (set! trade-n (+ trade-n 1)) (trouble-clk 'advance! -1) (check!))))))

;; ── 钥匙开出来的三张 ─────────────────────────────
(define (node-dock)
  (node "让码头带句话"
    :subtitle "老大的哥哥在码头卸货，那边的话他不敢不听"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome (lambda () (check!)))
      (outcome (lambda () (set! press-n (+ press-n 1)) (trouble-clk 'advance! -1) (check!)))
      (outcome (lambda () (set! press-n (+ press-n 2))
                   (trouble-clk 'advance! -1) (heat-clk 'advance! -1) (check!))))))

(define (node-owner)
  (node "让老板拒收"
    :subtitle "见识；一家不交，整条街就都不交了"
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome (lambda () (heat-clk 'tick!) (check!)))
      (outcome (lambda () (set! press-n (+ press-n 1)) (trouble-clk 'advance! -1) (check!)))
      (outcome (lambda () (set! press-n (+ press-n 1)) (trouble-clk 'advance! -1) (check!))))))

;; 替他们把这个月的账平了。钱是真花的——这条路把代价挪到你自己的口袋里。
(define (node-debt)
  (node "替他平了赌账"
    :subtitle "他欠场子二十块；写票的认这笔钱，不认他"
    :requires (list (req-item "金钱" 20))
    :resolve (instant
      (outcome (lambda ()
          (set! trade-n (+ trade-n 2))
          (trouble-clk 'advance! -2)
          (check!))))))

(define (node-leave)
  (instant-action "今天到此为止"
    (lambda ()
      (if (< (trouble-clk 'current) trouble-max)
          (finish! 'fail "还差一口气"
            "你走的时候，数零钱的那个抬头看了你一眼。第二天杂货铺照样交钱。")
          (finish! 'fail "你没有下车"
            "你在街口站了一会儿就走了。这条街今晚和昨晚没有任何区别。")))))

;; ── 回合末 ──────────────────────────────────────
;; 你在这儿耗一晚上，别处的事不会等你。
;; 这里曾经在引擎的通用消耗之外，每回合再扣 1 点冷静。额外那层已删；
;; 现在只支付引擎统一的每回合 1 点，否则会把倒下线推到打不完的地方。
;; 这一场的升压交给 heat 那根钟。
(define-turn-rule "夜里越拖越难看"
  (lambda () (not finished?))
  (lambda ()
    (check!)))

(define (get-render-data)
  (container "让他们安静"
    (append
      (clock-nodes (trouble-clk 'render-data) (heat-clk 'render-data))
      (list (node-drag) (node-sit))
      (if key-dock (list (node-dock)) '())
      (if key-street (list (node-owner)) '())
      (if key-ring (list (node-debt)) '())
      (list (node-leave)))))
