;; scenes/world/老街酒馆.scm - 老街酒馆（劳工势力 / 夜莺故事舞台）

(define old-street-tavern
  (let ()
    (define closed-days 0)
    (define closed-days-max 2)

    ;; ── 高利贷 ────────────────────────────────────
    ;; 角落里放贷的：缺钱时立刻周转，代价是 1.5 倍连本带利，逾期利滚利。
    ;; 同一时间只能有一笔（loan-owed>0 表示有未清欠款）。
    (define loan-owed 0)
    (define loan-days 0)
    (define loan-principal 40)   ; 到手本金
    (define loan-repay 60)       ; 到期应还
    (define loan-term 5)         ; 到期天数

    (define (node-borrow-loan)
      (node "借一笔钱周转"
        :subtitle "角落里放贷的能立刻拿钱给你周转——借 40，五天后连本带利还 60；逾期利滚利，最好别拖"
        :tags (list "非法" "高利贷")
        :resolve (instant
          (outcome "拿了这笔钱" "他数出四十块推过来，只说了一句：五天，六十，一个子儿都不能少。"
            (lambda ()
              (add-item! "金钱" loan-principal)
              (set! loan-owed loan-repay)
              (set! loan-days loan-term))))))

    (define (node-repay-loan)
      (node "还清欠款"
        :subtitle (string-append "把欠的 " (number->string loan-owed) " 金钱一次结清")
        :requires (list (req-item "金钱" loan-owed))
        :resolve (instant
          (outcome "还清了这笔债" "钱码在桌上，他一张张点过，收进抽屉。这一笔，算是揭过去了。"
            (lambda ()
              (set! loan-owed 0)
              (set! loan-days 0))))))

    (define (loan-shark-container)
      (node "放贷的"
        :subtitle (if (> loan-owed 0)
                      (string-append "他靠在角落记着账，你还欠 " (number->string loan-owed) " 金钱")
                      "角落里靠墙坐着个记账的，缺钱周转能找他——代价你懂")
        :children (list (if (> loan-owed 0) (node-repay-loan) (node-borrow-loan)))))

    (define-turn-rule "高利贷催收"
      (lambda () (> loan-owed 0))
      (lambda ()
        (if (> loan-days 0)
            (set! loan-days (- loan-days 1))
            (begin
              ;; 逾期：利滚利 +20%（向上取整），并承受上门催收的压力。
              (set! loan-owed (+ loan-owed (quotient (+ loan-owed 4) 5)))
              (spend-composure! 1)
              (notify! (string-append "欠款逾期，利滚利涨到 " (number->string loan-owed)
                                      " 金钱。放贷的人开始上门催了。"))))))

    ;; ── 生计工作 ──────────────────────────────────
    (define (node-waiter)
      (关系工作 "服务员" "劳工" '低 'social
        (outcome "手脚麻利" "跑了一晚上堂子，酒客赏钱都算在工钱里。"
          (lambda () (add-item! "金钱" 8)))
        (outcome "普通一班" "今晚客人稀稀落落，工头按日头结了账。"
          (lambda () (add-item! "金钱" 5)))
        (outcome "打翻酒杯" "一个醉客借故发作，你赔了一杯，也被骂了一顿。"
          (lambda () (spend-composure! 1)))))

    ;; 打酒：可带回住所或交锋中即时饮用，恢复量高于香烟，但会留下宿醉。
    (define (node-buy-liquor)
      (node "打一壶酒"
        :subtitle "给夜里留点松快，也能稍微垫垫肚子"
        :requires (list (req-item "金钱" 25))
        :resolve (instant
          (outcome "打了一壶酒" "打了一壶酒，带回去搁着，留着夜里。"
            (lambda () (add-item! "酒" 1))))))

    ;; 烟可在交锋里即时救急，恢复 2 点冷静，故贵于城市内的恢复手段。
    (define (node-buy-cigarettes)
      (node "买烟"
        :subtitle "15 金；交锋中可在功能区抽烟，恢复 2 点冷静"
        :requires (list (req-item "金钱" 15))
        :resolve (instant
          (outcome "买了烟" "烟和火柴塞进了口袋。真到顶不住时，它们能替你撑半步。"
            (lambda () (add-item! "香烟" 1))))))

    ;; 当场点一杯：效果与在家喝自带的酒完全一样，共用同一次“当天第一杯”（home 的 drank-today?）。
    ;; :resolve 用 outcome 包一层，结果才会像判定一样以锚定卡片弹出，而不是只飘过一条 notify!。
    (define (node-drink-here)
      (node "点一杯酒"
        :subtitle (if (home 'drank-today?)
                      "今天已经喝过了，再喝只会头疼"
                      "不带走，当场喝掉：恢复 2 点冷静，下一次城市骰池会有宿醉")
        :disabled (home 'drank-today?)
        :requires (list (req-item "金钱" 25))
        :resolve (instant
          (outcome "借酒松神" "就着吧台喝了一杯，绷着的神经松了扣。明早的头痛，会再来讨账。"
            (lambda () (home 'drink!))
            'light))))

    ;; ── 地下酒吧（劳工·核心）──────────────────────
    ;; 自己人之后才请得进来的门路：押上本钱赌一把，输赢自己认。
    (define (node-underground-bar)
      (node "去地下酒吧押一把"
        :subtitle "只有自己人才请得进来；押上的本钱，输赢自己认"
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
        (if (three-letters 'singer-present?)
            "油灯把木桌照得发黄，烟味浮在半空散不开。角落里几个水手闷头喝酒，谁也不吭声；台侧立着一把红伞——台上的歌女，今晚还没开嗓。"
            "油灯把木桌照得发黄，烟味浮在半空散不开。角落里几个水手闷头喝酒，谁也不吭声；台上的歌女，今晚还没开嗓。")))

    (define (node-closed)
      (observe-action "酒馆歇业"
        "门板从里面上了闩。老板贴着告示：家中有事，歇业数日。从门缝望进去,台侧那把红伞还立在原处,没人来取。"))

    (define (tavern-clocks)
      (append
        (if (> closed-days 0)
            (list (list 'clock "酒馆歇业" closed-days closed-days-max 'countdown
                        "歇业期间不能在这里做工或打听消息。"))
            '())
        (if (> loan-owed 0)
            (list (list 'clock "还款到期" (max loan-days 0) loan-term 'countdown
                        (string-append "欠 " (number->string loan-owed)
                                       " 金钱；归零后每天利滚利，还要挨催。")))
            '())))

    ;; ── 组装 ──────────────────────────────────────
    (define (tavern-children)
      (if (> closed-days 0)
          (list (node-closed))
          (append
            (three-letters 'nodes-at "酒馆")
            (list (node-waiter) (node-drink-here) (node-buy-liquor) (node-buy-cigarettes) (loan-shark-container))  ; 酒馆常驻：值班当差 + 当场点酒 + 打酒带走 + 买烟 + 放贷的
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
                 (list "closed-days-max" closed-days-max)
                 (list "loan-owed" loan-owed)
                 (list "loan-days" loan-days)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! closed-days (assoc-get data "closed-days" 0))
             (set! closed-days-max (assoc-get data "closed-days-max" 2))
             (set! loan-owed (assoc-get data "loan-owed" 0))
             (set! loan-days (assoc-get data "loan-days" 0))))
          (#t #f))))))
