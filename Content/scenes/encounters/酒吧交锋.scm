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

;; 路线三：酒吧常客
;; talked-set 按常客池下标持久追踪，跨回合保留翻面状态。
;; 窗口固定 3 人，每回合平移 1 格：1 人离开（清除其下标）→ 1 人新到（始终未打听）。
;; 保底天然成立：新到的人永远不在 talked-set 里。
(define patron-offset 0)
(define patron-count  3)
(define talked-set    '())   ; 已打听过的常客池下标列表

;; ── Helpers ────────────────────────────────────
(define (tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (tick-n! clock (- n 1)))
      #f))

(define (info-tick! n)
  (tick-n! info n)
  (if (info 'full?)
      (begin
        (notify! "你终于拼凑出了足够的信息。")
        (end-encounter 'success))
      #f))

(define (tension-tick! n)
  (tick-n! tension n)
  (if (tension 'full?)
      (begin
        (notify! "气氛彻底崩了，几个大汉站了起来。你夺门而出。")
        (end-encounter 'fail))
      #f))

(define (in-talked-set? idx)
  (if (null? talked-set)
      #f
      (if (equal? idx (car talked-set))
          #t
          (in-talked-set-inner? idx (cdr talked-set)))))

(define (in-talked-set-inner? idx lst)
  (if (null? lst)
      #f
      (if (equal? idx (car lst))
          #t
          (in-talked-set-inner? idx (cdr lst)))))

(define (remove-from-set lst val)
  (if (null? lst)
      '()
      (if (equal? val (car lst))
          (cdr lst)
          (cons (car lst) (remove-from-set (cdr lst) val)))))

;; ── 酒吧常客池（6人，窗口大小3，每回合轮换1人）─
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

;; 返回 (pool-idx patron-data) 对
(define (make-patron-entry offset-i)
  (let ((pool-idx (modulo (+ patron-offset offset-i) pool-size)))
    (list pool-idx (list-nth patron-pool pool-idx))))

(define (current-patron-entries)
  (list (make-patron-entry 0)
        (make-patron-entry 1)
        (make-patron-entry 2)))

;; ── Turn Rules ─────────────────────────────────
(define-turn-rule "气氛升温"
  (lambda () #t)
  (lambda () (tension-tick! 1)))

(define-turn-rule "话头消失"
  (lambda () tip-available)
  (lambda ()
    (set! tip-available #f)
    (notify! "那个话头没接上，时机过了。")))

;; 窗口平移：先清除离开那人的打听记录，再移动偏移。
;; 保底：新到的人（原偏移+3 位）的记录已在他上次离开时被清除。
(define-turn-rule "人员流动"
  (lambda () #t)
  (lambda ()
    (let ((leaving-idx (modulo patron-offset pool-size)))
      (set! talked-set    (remove-from-set talked-set leaving-idx))
      (set! patron-offset (+ patron-offset 1)))))

;; ── 路线一：点酒聆听 ─────────────────────────────
(define (node-order-drink)
  (action "点一杯酒"
    (list (req-item "金钱" 10))
    (instant (lambda ()
      (set! bar-ordered #t)
      (set! listen-uses 4)
      (add-supplies! 1)
      (notify! "你点了一杯烧酒，老板没多话，把杯子推过来。物资+1。")))))

(define (node-listen)
  (action (string-append "在吧台聆听 (×" (number->string listen-uses) ")")
    (list (req-die))
    (roll 'sharpness
      (lambda ()
        (set! listen-uses (- listen-uses 1))
        (if (= listen-uses 0) (set! bar-ordered #f) #f)
        (notify! "静下心来，却什么都没抓住。"))
      (lambda ()
        (set! listen-uses (- listen-uses 1))
        (if (= listen-uses 0) (set! bar-ordered #f) #f)
        (set! tip-available #t)
        (notify! "隐约听到了几个字，话头出现了。"))
      (lambda ()
        (set! listen-uses (- listen-uses 1))
        (if (= listen-uses 0) (set! bar-ordered #f) #f)
        (set! tip-available #t)
        (notify! "清楚地捕捉到了一个话头。")))))

(define (node-follow-up)
  (action "接话"
    (list (req-die))
    (roll 'sharpness
      (lambda ()
        (set! tip-available #f)
        (notify! "时机没对，对方回头走了。"))
      (lambda ()
        (set! tip-available #f)
        (info-tick! 1)
        (notify! "对方随口回了你一句，有点用。"))
      (lambda ()
        (set! tip-available #f)
        (info-tick! 2)
        (notify! "话头接上了，对方多说了不少。")))))

;; ── 路线二：向酒保买消息 ─────────────────────────
(define (node-buy-info)
  (action "向酒保买消息"
    (list (req-item "金钱" 15))
    (instant (lambda ()
      (info-tick! 2)
      (notify! "酒保把嘴凑过来，低声说了几句，干脆利落。")))))

;; ── 路线三：直接搭话 ─────────────────────────────
(define (make-patron-node entry)
  (let ((pool-idx (car entry))
        (patron   (cadr entry)))
    (let ((name  (car patron))
          (risk  (cadr patron))
          (skill (caddr patron)))
      (if (in-talked-set? pool-idx)
          (observe-action name "他只是看着你，没什么可说的了。")
          (action name
            (list (req-die))
            (if (equal? risk 'high)
                (roll skill
                  (lambda ()
                    (set! talked-set (cons pool-idx talked-set))
                    (tension-tick! 2)
                    (stress-current-actor! 1)
                    (notify! "对方猛地站起来，周围几个人都看过来了。"))
                  (lambda ()
                    (set! talked-set (cons pool-idx talked-set))
                    (tension-tick! 1)
                    (info-tick! 1)
                    (notify! "他皱眉，但还是扔出了一句话，不太友善。"))
                  (lambda ()
                    (set! talked-set (cons pool-idx talked-set))
                    (info-tick! 2)
                    (notify! "出乎意料，他说了不少有用的东西。")))
                (roll skill
                  (lambda ()
                    (set! talked-set (cons pool-idx talked-set))
                    (tension-tick! 1)
                    (notify! "他挥挥手，不耐烦地走开了。"))
                  (lambda ()
                    (set! talked-set (cons pool-idx talked-set))
                    (info-tick! 1)
                    (notify! "他随口说了几句，有点用。"))
                  (lambda ()
                    (set! talked-set (cons pool-idx talked-set))
                    (info-tick! 2)
                    (notify! "话匣子开了，颇有收获。")))))))))

(define (patron-nodes)
  (map make-patron-node (current-patron-entries)))

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
