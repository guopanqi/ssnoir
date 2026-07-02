;; scenes/encounters/酒吧交锋.scm
;; 三路线：点酒聆听 / 向酒保买消息 / 直接搭话
;; 双时钟：打听进度(4) vs 气氛紧张(4)，气氛从 bar-heat 起步，每回合自动+1

;; ── Clocks ─────────────────────────────────────
(define info    (make-clock "打听进度" 4 'segments))
(define tension (make-clock "气氛紧张" 4 'segments))
(tension 'set! (or (get-global 'bar-current-heat) 0))

;; ── State ──────────────────────────────────────
;; 路线一：点酒聆听
(define bar-ordered   #f)
(define listen-uses   0)
(define tip-available #f)

;; 路线三：3 个独立槽位，各自倒计时
;; 初始错开（第1/2/3回合各有1人换）；到期后随机新计时器（2-4回合）。
;; 池下标间隔 2 以避免初始重复：0 / 2 / 4
(define slot-0-idx   0)  (define slot-0-timer 3)
(define slot-1-idx   2)  (define slot-1-timer 1)
(define slot-2-idx   4)  (define slot-2-timer 2)

;; 已打听过的池下标；槽位换人时清除该下标。新到的人始终未被打听——保底成立。
(define talked-set '())

;; ── Helpers ────────────────────────────────────
(define (tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (tick-n! clock (- n 1)))
      #f))

(define (info-tick! n)
  (tick-n! info n)
  (if (info 'full?)
      (begin
        (spotlight! "情报到手" "你终于拼凑出了足够的信息。")
        (end-encounter 'success))
      #f))

(define (tension-tick! n)
  (tick-n! tension n)
  (if (tension 'full?)
      (begin
        (spotlight! "局势失控" "气氛彻底崩了，几个大汉站了起来。你夺门而出。")
        (end-encounter 'fail))
      #f))

(define (list-contains? lst val)
  (if (null? lst) #f
      (if (equal? val (car lst)) #t
          (list-contains? (cdr lst) val))))

(define (in-talked-set? idx)
  (list-contains? talked-set idx))

(define (remove-from-set lst val)
  (if (null? lst)
      '()
      (if (equal? val (car lst))
          (cdr lst)
          (cons (car lst) (remove-from-set (cdr lst) val)))))

;; ── 酒吧常客池（6人）────────────────────────────
;; (name risk skill): risk = medium/high, skill = sharpness/violence
(define patron-pool
  (list
    (list "老渔夫"     'medium 'sharpness)
    (list "码头工头"   'medium 'violence)
    (list "外乡商人"   'medium 'sharpness)
    (list "醉水手"     'high   'violence)
    (list "沉默的壮汉" 'high   'violence)
    (list "戴帽女人"   'high   'sharpness)))

(define pool-size 6)

(define (list-nth lst n)
  (if (= n 0) (car lst) (list-nth (cdr lst) (- n 1))))

;; 找一个当前不在任何槽位的池下标
(define (collect-available i acc)
  (if (= i pool-size)
      acc
      (if (or (= i slot-0-idx) (= i slot-1-idx) (= i slot-2-idx))
          (collect-available (+ i 1) acc)
          (collect-available (+ i 1) (cons i acc)))))

(define (find-fresh-patron)
  (random-choice (collect-available 0 '())))

;; ── Turn Rules ─────────────────────────────────
(define-turn-rule "气氛升温"
  (lambda () #t)
  (lambda () (tension-tick! 1)))

(define-turn-rule "话头消失"
  (lambda () tip-available)
  (lambda ()
    (set! tip-available #f)
    (notify! "那个话头没接上，时机过了。")))

;; 每个槽位独立倒计时。到期：清除打听记录 → 换新人 → 随机新计时器（2-4回合）。
;; 顺序处理确保每次 find-fresh-patron 都能见到已更新的槽位，避免重复。
(define-turn-rule "人员流动"
  (lambda () #t)
  (lambda ()
    (set! slot-0-timer (- slot-0-timer 1))
    (if (<= slot-0-timer 0)
        (begin
          (set! talked-set   (remove-from-set talked-set slot-0-idx))
          (set! slot-0-idx   (find-fresh-patron))
          (set! slot-0-timer (random-choice '(2 3 3 4))))
        #f)
    (set! slot-1-timer (- slot-1-timer 1))
    (if (<= slot-1-timer 0)
        (begin
          (set! talked-set   (remove-from-set talked-set slot-1-idx))
          (set! slot-1-idx   (find-fresh-patron))
          (set! slot-1-timer (random-choice '(2 3 3 4))))
        #f)
    (set! slot-2-timer (- slot-2-timer 1))
    (if (<= slot-2-timer 0)
        (begin
          (set! talked-set   (remove-from-set talked-set slot-2-idx))
          (set! slot-2-idx   (find-fresh-patron))
          (set! slot-2-timer (random-choice '(2 3 3 4))))
        #f)))

;; ── 路线一：点酒聆听 ─────────────────────────────
(define (node-order-drink)
  (action "点一杯酒"
    (list (req-item "金钱" 10))
    (instant (outcome "烧酒一杯"
      "老板没多话，把杯子推过来。饱腹+1。"
      (lambda ()
        (set! bar-ordered #t)
        (set! listen-uses 4)
        (add-supplies! 1))))))

(define (node-listen)
  (action (string-append "在吧台聆听 (×" (number->string listen-uses) ")")
    (list (req-die))
    (roll 'sharpness
      (outcome "没抓到什么"
        "静下心来，却什么都没捕捉到。"
        (lambda ()
          (set! listen-uses (- listen-uses 1))
          (if (= listen-uses 0) (set! bar-ordered #f) #f)))
      (outcome "话头出现"
        "隐约听到了几个字，或许值得搭话。"
        (lambda ()
          (set! listen-uses (- listen-uses 1))
          (if (= listen-uses 0) (set! bar-ordered #f) #f)
          (set! tip-available #t)))
      (outcome "清晰捕捉"
        "一个话头清楚地浮现，时机刚好。"
        (lambda ()
          (set! listen-uses (- listen-uses 1))
          (if (= listen-uses 0) (set! bar-ordered #f) #f)
          (set! tip-available #t))))))

(define (node-follow-up)
  (action "接话"
    (list (req-die))
    (roll 'sharpness
      (outcome "时机没对"
        "话头没接上，对方回头走了。"
        (lambda ()
          (set! tip-available #f)))
      (outcome "随口一句"
        "对方回了你一句，说了一点有用的东西。"
        (lambda ()
          (set! tip-available #f)
          (info-tick! 1)))
      (outcome "话头接上"
        "话头接上了，对方多说了不少。"
        (lambda ()
          (set! tip-available #f)
          (info-tick! 2))))))

;; ── 路线二：向酒保买消息 ─────────────────────────
(define (node-buy-info)
  (action "向酒保买消息"
    (list (req-item "金钱" 15))
    (instant (outcome "买到消息"
      "酒保把嘴凑过来，低声说了几句，干脆利落。"
      (lambda ()
        (info-tick! 2))))))

;; ── 路线三：直接搭话 ─────────────────────────────
(define (make-patron-node pool-idx)
  (let ((patron (list-nth patron-pool pool-idx)))
    (let ((name  (car patron))
          (risk  (cadr patron))
          (skill (caddr patron)))
      (if (in-talked-set? pool-idx)
          (observe-action name "他只是看着你，没什么可说的了。")
          (action name
            (list (req-die))
            (if (equal? risk 'high)
                (roll skill
                  (outcome "话不投机"
                    "对方猛地站起来，周围几个人都看过来了。"
                    (lambda ()
                      (set! talked-set (cons pool-idx talked-set))
                      (tension-tick! 2)
                      (stress-current-actor! 1))
                    'heavy)
                  (outcome "皱眉搭话"
                    "他皱眉，但还是扔出了一句话，不太友善。"
                    (lambda ()
                      (set! talked-set (cons pool-idx talked-set))
                      (info-tick! 1)
                      (tension-tick! 1)))
                  (outcome "出乎意料"
                    "他开口了，说的比你预想的多。"
                    (lambda ()
                      (set! talked-set (cons pool-idx talked-set))
                      (info-tick! 2))))
                (roll skill
                  (outcome "不耐烦"
                    "他挥挥手，不耐烦地走开了。"
                    (lambda ()
                      (set! talked-set (cons pool-idx talked-set))
                      (tension-tick! 1)))
                  (outcome "随口说了几句"
                    "他随口说了几句，有点用。"
                    (lambda ()
                      (set! talked-set (cons pool-idx talked-set))
                      (info-tick! 1)))
                  (outcome "话匣子开了"
                    "聊了不少，颇有收获。"
                    (lambda ()
                      (set! talked-set (cons pool-idx talked-set))
                      (info-tick! 2))))))))))

(define (patron-nodes)
  (list
    (make-patron-node slot-0-idx)
    (make-patron-node slot-1-idx)
    (make-patron-node slot-2-idx)))

;; ── Render ─────────────────────────────────────
(define (visible-action-nodes)
  (append
    (if (not bar-ordered) (list (node-order-drink)) '())
    (if (and bar-ordered (> listen-uses 0)) (list (node-listen)) '())
    (if tip-available (list (node-follow-up)) '())
    (list (node-buy-info))
    (patron-nodes)))

(define (get-render-data)
  (container-with-clocks "酒吧"
    (visible-action-nodes)
    (list
      (info    'render-data)
      (tension 'render-data))))
