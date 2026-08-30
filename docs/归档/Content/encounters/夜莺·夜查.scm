;; 夜莺·夜查——萨姆同行时的短交锋。
;; 主结构：抢时间。夜账满格即拿到记录；守夜人巡回满格即被撵出货栈。

(define records (make-clock "夜账" 5 'gauge
                            "填满 = 翻到那一行。"))
(define watchman (make-clock "守夜人的巡回" 4 'countdown
                             "每回合 +1；填满 = 他转回账房，把你们撵出去。"))
(define bribed? #f)
(define instinct-used? #f)
(define next-book-bonus? #f)

(define (clock-tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (clock-tick-n! clock (- n 1)))
      #f))

(define (book-modifiers)
  (if next-book-bonus?
      (list (modifier 1 "萨姆看出了补写的墨迹"))
      '()))

(define (consume-book-bonus!)
  (set! next-book-bonus? #f))

(define (finish-success!)
  (play-dialogue!
    (line "萨姆" "十一月九日,夜里加靠一班,不入港册,走的是老板名下的线。")
    (line "萨姆" "她说她在等船。这行字说,那晚的船,是来抓她的。"))
  (spotlight! "夜账里的那一行" "夜账翻到了底。货栈留下的记录,把她那晚的谎话和老板的船连在了一起。")
  (end-encounter 'success))

(define (finish-fail!)
  (injure!)
  (spend-actor-composure! 'player 2)
  (play-dialogue!
    (line "萨姆" "跑。答案还在里面,可今晚它不欠我们。"))
  (spotlight! "夜查：被撵出来" "守夜人回了账房。你们翻窗离开，身上留下了伤和一夜没睡的账；夜账改天还能再翻。")
  (end-encounter 'fail))

(define-rule "夜账找到了"
  (lambda () (records 'full?))
  (lambda () (finish-success!)))

(define-rule "守夜人回来了"
  (lambda () (watchman 'full?))
  (lambda () (finish-fail!)))

(define (node-search-records)
  (action "翻夜账"
    (list (req-die))
    (roll 'knowledge book-modifiers
      (outcome "纸页碰倒了墨水"
        (lambda () (consume-book-bonus!) (records 'tick!) (watchman 'tick!)))
      (outcome "翻到一页旧账"
        (lambda () (consume-book-bonus!) (records 'tick!)))
      (outcome "找对了装订册"
        (lambda () (consume-book-bonus!) (clock-tick-n! records 2))))))

(define (node-keep-watch)
  (action "望风"
    (list (req-die))
    (roll 'sharpness
      (outcome "喊得太急"
        (lambda () (set! next-book-bonus? #f)))
      (outcome "门外没动静"
        (lambda () #f))
      (outcome "他多绕了一圈"
        (lambda () (watchman 'set! (max 0 (- (watchman 'current) 1))))))))

(define (node-bribe-watchman)
  (node "打点守夜人"
    :subtitle (if (relation-at-least? "劳工" '相识)
                  "码头的人认得你的脸；不用花钱，整场一次"
                  "花 10 金；整场一次，让他多绕两圈")
    :disabled bribed?
    :requires (if (relation-at-least? "劳工" '相识) '() (list (req-item "金钱" 10)))
    :resolve (instant
      (outcome "他往外走了"
        (lambda ()
          (set! bribed? #t)
          (watchman 'set! (max 0 (- (watchman 'current) 2))))))))

;; 引擎没有安全的“动作结算后重掷”中断点。把同一判断做成行动前的场内技能：
;; 玩家显式指定下一次翻账，由萨姆给出 +1 可见修正，不篡改已结算的骰子。
(define (node-sam-instinct)
  (node "萨姆的直觉"
    :subtitle "整场一次；下一次翻夜账 +1，不占骰"
    :disabled instinct-used?
    :resolve (instant
      (outcome "看墨色"
        (lambda ()
          (set! instinct-used? #t)
          (set! next-book-bonus? #t)
          (play-banter!
            (line "萨姆" "等等。别看货位,看墨。补写的那行,墨色比前后都新。")))))))

(define-turn-rule "守夜人的脚步"
  (lambda () (and (not (records 'full?)) (not (watchman 'full?))))
  (lambda () (watchman 'tick!)))

(define (get-render-data)
  (container "夜查货栈"
    (append (clock-nodes (records 'render-data) (watchman 'render-data))
      (list
          (node-search-records) (node-keep-watch) (node-bribe-watchman) (node-sam-instinct)))))
