;; scenes/world/老街酒馆.scm - 老街酒馆（劳工势力 / 夜莺故事舞台）

(define old-street-tavern
  (let ()
    (define closed-days 0)
    (define closed-days-max 2)

    ;; ── 生计工作 ──────────────────────────────────
    (define (node-waiter)
      (关系工作 "服务员" "劳工" '低 'social
        (outcome "手脚麻利" "跑了一晚上堂子，酒客赏钱都算在工钱里。"
          (lambda () (add-item! "金钱" 8)))
        (outcome "普通一班" "今晚客人稀稀落落，工头按日头结了账。"
          (lambda () (add-item! "金钱" 5)))
        (outcome "打翻酒杯" "一个醉客借故发作，你赔了一杯，也被骂了一顿。"
          (lambda () (spend-composure! 1)))))

    ;; 打酒：在酒馆买一壶带回家。可反复购买，回住所喝当场恢复冷静（不占骰）。
    (define (node-buy-liquor)
      (node "打一壶酒"
        :subtitle "给夜里留点松快，也能稍微垫垫肚子"
        :requires (list (req-item "金钱" 8))
        :resolve (instant
          (outcome "打了一壶酒" "打了一壶酒，带回去搁着，留着夜里。"
            (lambda () (add-item! "酒" 1))))))

    ;; 一包四根。交锋里每根恢复 2 点冷静；一场 4–6 回合通常会烧掉大半包。
    (define (node-buy-cigarettes)
      (node "买一包烟"
        :subtitle "8 金 4 根；交锋中可在功能区抽一根，恢复 2 点冷静"
        :requires (list (req-item "金钱" 8))
        :resolve (instant
          (outcome "买了一包烟" "廉价烟草和火柴塞进了口袋。真到顶不住时，它们能替你撑半步。"
            (lambda () (add-item! "香烟" 4))))))

    ;; 当场点一杯：效果与在家喝自带的酒完全一样，共用同一次“当天第一杯”（home 的 drank-today?）。
    ;; :resolve 用 outcome 包一层，结果才会像判定一样以锚定卡片弹出，而不是只飘过一条 notify!。
    (define (node-drink-here)
      (node "点一杯酒"
        :subtitle (if (home 'drank-today?)
                      "今天已经喝过了，再喝只会头疼"
                      "不带走，当场喝掉：恢复 2 点冷静，下一次城市骰池会有宿醉")
        :disabled (home 'drank-today?)
        :requires (list (req-item "金钱" 8))
        :resolve (instant
          (outcome "借酒松神" "就着吧台喝了一杯，绷着的神经松了扣。明早的头痛，会再来讨账。"
            (lambda () (home 'drink!))
            'light))))

    ;; ── 地下酒吧（劳工·核心）──────────────────────
    ;; 拜过码头之后才请得进来的门路：押上本钱赌一把，输赢自己认。
    (define (node-underground-bar)
      (node "去地下酒吧押一把"
        :subtitle "只有拜过码头的人才请得进来；押上的本钱，输赢自己认"
        :tags (list "非法" "赌博")
        :requires (list (req-die) (req-item "金钱" 20))
        :resolve (roll 'sharpness (lambda () (list (modifier -2 "非法")))
          (outcome "输光了押注" "骰子不给面子，押上的钱全喂了庄家。"
            (lambda () #f))
          (outcome "堪堪回本" "起起落落一晚，原样把钱拿了回来。"
            (lambda () (add-item! "金钱" 20)))
          (outcome "赢了台面" "骰子一路顺，你把桌上的钱扫了大半。"
            (lambda () (add-item! "金钱" 60))))))

    ;; ── 氛围与歇业 ────────────────────────────────
    (define (node-atmosphere)
      (observe-action "酒馆内景"
        "油灯把木桌照得发黄，烟味浮在半空散不开。角落里几个水手闷头喝酒，谁也不吭声；台上的歌女，今晚还没开嗓。"))

    (define (node-closed)
      (observe-action "酒馆歇业"
        "门板从里面上了闩。老板贴着告示：家中有事，歇业数日。"))

    (define (tavern-clocks)
      (if (> closed-days 0)
          (list (list 'clock "酒馆歇业" closed-days closed-days-max 'countdown
                      "歇业期间不能在这里做工或打听消息。"))
          '()))

    ;; ── 组装 ──────────────────────────────────────
    (define (tavern-children)
      (if (> closed-days 0)
          (list (node-closed))
          (append
            (nightingale 'beat1-nodes-at "酒馆")
            (nightingale 'tavern-nodes)
            (sam 'nodes)
            (list (node-waiter) (node-drink-here) (node-buy-liquor) (node-buy-cigarettes))  ; 酒馆常驻：值班当差 + 当场点酒 + 打酒带走 + 买烟
            (if (relation-at-least? "劳工" '核心) (list (node-underground-bar)) '())
            (list (node-atmosphere)))))

    (define-turn-rule "老街酒馆停业倒计时"
      (lambda () (> closed-days 0))
      (lambda () (set! closed-days (- closed-days 1))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node "老街酒馆" :children (tavern-children) :clocks (tavern-clocks))))
          ((equal? msg 'set-closed!)
           (set! closed-days (cadr args))
           (set! closed-days-max (max closed-days-max closed-days)))
          ((equal? msg 'save)
           (list (list "closed-days" closed-days)
                 (list "closed-days-max" closed-days-max)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! closed-days (assoc-get data "closed-days" 0))
             (set! closed-days-max (assoc-get data "closed-days-max" 2))))
          (#t #f))))))
