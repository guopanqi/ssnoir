;; 弗兰克（Frank Delaney）——码头灰色秩序的实际领袖。

(define frank
  (let ()
    ;; 0等待触发 / 1货船抢修 / 2认识 / 3悬案开放 / 4深熟 / 5错过抢修
    (define stage 0)
    (define favor-clk
      (make-clock "与弗兰克建立信任" 4 'segments
        "替弗兰克查账建立私人信任；填满后他会让你接触旧悬案。"))
    (define repair-clk
      (make-clock "投入工作" 12 'segments
        "投入人手抽水、补漏并重新固定钢缆；填满后货船脱险，弗兰克会记住你。"))
    (define repair-duration 4)
    (define repair-deadline-day 0)
    (define smuggled-today? #f)
    (define identity "码头领袖，掌握分账与灰色门路")
    (define frank-trouble
      (make-trouble "弗兰克的麻烦" 3
        (lambda ()
          (spend-up-to! "金钱" 15)
          (spend-actor-composure! 'player 1)
          (notify! "巡警顺着走私记录找上门来。你花了钱才让这件事停在门外。"))))

    (define (sync!)
      (set-global! 'frank-final-help (= stage 4)))

    (define (repair-days-left)
      (if (= stage 1)
          (max 0 (- repair-deadline-day world-day))
          0))

    (define (finish-ship-repair!)
      (set! stage 2)
      (favor-clk 'reset!)
      (set! repair-deadline-day 0)
      (change-faction-relation! "劳工" 1)
      (complete-section!)
      (play-dialogue!
        (line "世界" "水泵终于压住了舱底的进水。拖船拉紧钢缆，那艘货船一寸一寸离开沉桩。")
        (line "弗兰克" "今天在这儿撑到最后的人，我都记得。你叫什么？"))
      (spotlight! "货船脱险" "抢修完成。弗兰克记住了你，从此愿意让你经手码头上更重要的事。"))

    (define (advance-ship-repair! n)
      (if (not (= stage 1))
          (error "弗兰克抢修：货船尚未搁浅")
          #t)
      (repair-clk 'advance! n)
      (if (repair-clk 'full?) (finish-ship-repair!) #f))

    (define (node-grounded-cargo-ship)
      (node "一艘货船搁浅"
        :subtitle "弗兰克正在码头调集人手；货船撞上沉桩，船腹进水，钢缆随时可能崩断"
        :tags (list "工作" "高风险")
        :clocks (list
          (repair-clk 'render-data)
          (list 'clock "货船沉没" (repair-days-left) repair-duration 'countdown
                "每结束一天减少一格；归零时抢修失败，事件从码头消失。"))
        :requires (list (req-die))
        :resolve (roll 'violence
          (outcome "钢缆甩脱"
            (lambda () (spend-composure! 2)))
          (outcome "稳住漏口"
            (lambda () (spend-composure! 1) (advance-ship-repair! 1)))
          (outcome "抢下关键处"
            (lambda () (advance-ship-repair! 2))))))

    (define (advance-favor! n)
      (favor-clk 'advance! n)
      (if (and (= stage 2) (favor-clk 'full?))
          (begin (set! stage 3) (notify! "弗兰克愿意让你知道一桩旧悬案。"))
          #f))

    (define (node-ledger)
      (node "替弗兰克查账"
        :subtitle identity
        :tags (list "低风险")
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (outcome "没查明白"
            (lambda () (spend-composure! 1)))
          (outcome "理清一笔"
            (lambda () (advance-favor! 1)))
          (outcome "找出暗扣"
            (lambda () (advance-favor! 2))))))

    (define (on-cold-case-result result)
      (if (equal? result 'success)
          (begin
            (set! stage 4)
            (change-faction-relation! "劳工" 2)
            (complete-section!)
            (notify! "旧案有了交代。弗兰克从此把你当作真正能托底的人。"))
          #f)
      (sync!))

    (define (node-cold-case)
      (node "替弗兰克了结旧案"
        :subtitle identity
        :tags (list "交锋")
        :resolve (instant (lambda () (start-encounter "悬案" on-cold-case-result)))))

    (define (node-smuggle)
      (node "替弗兰克走私"
        :subtitle (string-append identity "；" (if smuggled-today? "今天已经走过一批货" "每天一次"))
        :tags (list "工作" "高风险" "非法")
        :disabled smuggled-today?
        :requires (list (req-die))
        :resolve (roll 'sharpness (lambda () (list (modifier -2 "非法")))
          (outcome "被巡警记下"
            (lambda ()
              (set! smuggled-today? #t)
              (change-faction-relation! "官僚" -2)
              (frank-trouble 'start!)
              (spend-composure! 2)))
          (outcome "险着出港"
            (lambda () (set! smuggled-today? #t) (add-item! "私货" 1) (spend-composure! 1)))
          (outcome "顺利出港"
            (lambda () (set! smuggled-today? #t) (add-item! "私货" 2) (add-item! "情报" 1))))))

    (define (node-handle-trouble)
      (action "摆平走私留下的麻烦" (list (req-die))
        (roll 'social (lambda () (关系难度修正 "官僚"))
          (outcome "没压住" (lambda () #f))
          (outcome "暂时按下" (lambda () (frank-trouble 'resolve!)))
          (outcome "撕掉记录" (lambda () (frank-trouble 'resolve!))))))

    (define (dock-nodes)
      (append
        (if (= stage 1)
            (list (node-grounded-cargo-ship))
            '())
        (if (or (= stage 2) (= stage 3) (= stage 4))
            (list (node "弗兰克"
                    :subtitle identity
                    :clocks (if (= stage 2)
                                (list (favor-clk 'render-data))
                                '())
                    :resolve (observe (if (= stage 4)
                        "弗兰克·德莱尼掌握码头的分账、走私和谁能在这里站住脚。"
                        "弗兰克不抬高声音，也能让码头上的争吵停下来。"))))
            '())
        (if (= stage 2) (list (node-ledger)) '())
        (if (= stage 3) (list (node-cold-case)) '())
        (if (= stage 4) (list (node-smuggle)) '())
        (if (frank-trouble 'active?) (list (node-handle-trouble)) '())))

    ;; 关系条件在当天行动中达到后，不立刻把抢修卡塞进码头；睡到下一天，
    ;; 再以过场宣布事故并开放事件。
    (define-turn-rule "货船搁浅过场"
      (lambda () (and (= stage 0) (>= (faction-relation "劳工") 3)))
      (lambda ()
        (play-dialogue!
          (line "弗兰克" "港外那条货船撞上沉桩了。船腹在进水，钢缆也撑不了多久。")
          (line "主角" "你要人手？")
          (line "弗兰克" "要能下舱、能上泵、也能在钢缆崩断时不先跑的人。码头已经封了。"))
        (set! stage 1)
        (repair-clk 'reset!)
        (set! repair-deadline-day (+ world-day repair-duration))
        (spotlight! "货船搁浅" "清晨的事故已经封住码头。弗兰克正在调集人手，抢修从今天开始。")))

    (define-turn-rule "货船抢修期限"
      (lambda () (and (= stage 1) (<= (repair-days-left) 0)))
      (lambda ()
        (set! stage 5)
        (set! repair-deadline-day 0)
        (notify! "货船的船腹彻底失守。弗兰克让所有人撤下跳板，抢修事件已经结束。")))

    (define-turn-rule "弗兰克每日次数重置"
      (lambda () smuggled-today?)
      (lambda () (set! smuggled-today? #f)))

    (define-turn-rule "弗兰克的麻烦推进"
      (lambda () (frank-trouble 'active?))
      (lambda () (frank-trouble 'tick!)))

    (sync!)
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'dock-nodes) (dock-nodes))
          ((equal? msg 'can-final-help?) (= stage 4))
          ((equal? msg 'save)
           (list (list "stage" stage) (list "favor" (favor-clk 'save))
                 (list "repair-progress" (repair-clk 'save))
                 (list "repair-deadline-day" repair-deadline-day)
                 (list "smuggled-today?" smuggled-today?) (list "trouble" (frank-trouble 'save))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! stage (assoc-get data "stage" 0))
             (favor-clk 'load! (assoc-get data "favor" 0))
             (repair-clk 'load! (assoc-get data "repair-progress" 0))
             ;; 旧存档只有剩余天数；读入时一次性换算为绝对截止日，之后只保存新字段。
             (set! repair-deadline-day
                   (assoc-get data "repair-deadline-day"
                     (if (= stage 1)
                         (+ world-day (assoc-get data "repair-days" repair-duration))
                         0)))
             (if (and (= stage 1) (<= repair-deadline-day world-day))
                 (error "弗兰克存档错误：抢修进行中但截止日缺失或已经过期")
                 #t)
             (set! smuggled-today? (assoc-get data "smuggled-today?" #f))
             (frank-trouble 'load! (assoc-get data "trouble" (list #f 0)))
             (sync!)))
          (else #f))))))
