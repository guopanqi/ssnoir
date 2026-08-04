;; 码头——普通生计由地点拥有；乔与弗兰克各自拥有个人生活。

(define dock
  (let ()
    (define (node-haul)
      (工作 "搬运" "劳工" '高 'violence
        (outcome "扛完一整班"
          (lambda () (add-item! "金钱" 15) (grant-work-relation! "劳工") (joe 'on-haul!)))
        (outcome "勉强做完"
          (lambda () (add-item! "金钱" 8) (spend-composure! 1) (joe 'on-haul!) (joe 'on-haul-neutral!)))
        (outcome "货箱脱手"
          (lambda () (spend-composure! 2) (joe 'on-haul-fail!)))))

    ;; 新机器——一次性的选择，接了才有进度。
    ;; 这不是一套局势系统，是给后续章节留的一颗种子：玩家缺钱的时候，码头正好有一份好活，
    ;; 卸的是以后要顶掉这些人的机器。选择记进全局 flag，第二章再读。
    ;; 0 还没出现 / 1 摆在那儿等人接 / 2 已经接下 / 3 卸完了
    (define machine-stage 0)
    (define machine-clk
      (make-clock "卸完这批机器" 3 'segments
        "装卸队按整活儿结账；三班卸完才付得齐。"))

    (define (machine-sync!)
      (set-global! 'unloaded-new-machines (= machine-stage 3)))

    (define (finish-machine-work!)
      (set! machine-stage 3)
      (add-item! "金钱" 25)
      (machine-sync!)
      (spotlight! "机器上了岸"
        (string-append
          "最后一箱机件落在码头上，工头当场把整活儿的钱数给你。\n"
          "卸货的人散开的时候没人说话。那几台东西要干的，正是他们的活。")))

    (define (advance-machine! n)
      (machine-clk 'advance! n)
      (if (machine-clk 'full?) (finish-machine-work!) #f))

    (define (node-machine-offer)
      (node "接下卸机器的活"
        :subtitle "工钱比扛货高一截；卸的是要顶掉这些人的新机器"
        :resolve (instant
          (outcome "跟工头点了头"
            (lambda ()
              (set! machine-stage 2)
              (machine-clk 'reset!)
              (play-dialogue!
                (line "工头" "三班，卸完为止。工钱按整活儿算，比扛货高一截。")
                (line "工头" "弗兰克那边怎么说我不管。船已经靠上了。")))))))

    (define (node-machine-work)
      (node "卸新机器的货"
        :subtitle "钢制机件比货箱沉得多"
        :tags (list "工作" "高风险")
        :clocks (list (machine-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'violence (lambda () (关系难度修正 "劳工"))
          (outcome "吊索崩了"
            (lambda () (spend-composure! 2)))
          (outcome "卸下几箱机件"
            (lambda () (add-item! "金钱" 10) (spend-composure! 1) (advance-machine! 1)))
          (outcome "抬下一整台"
            (lambda () (add-item! "金钱" 18) (advance-machine! 2))))))

    (define (machine-nodes)
      (cond
        ((= machine-stage 1) (list (node-machine-offer)))
        ((= machine-stage 2) (list (node-machine-work)))
        (else '())))

    ;; 交割那几天不出现——小节一的钱紧是教学，不该被一份高薪活抹平。
    ;; 等玩家开始在老街过日子，"今天靠什么挣钱"才真正是个问题。
    (define-turn-rule "新机器到港"
      (lambda () (and (= machine-stage 0) (>= world-day 5)))
      (lambda ()
        (set! machine-stage 1)
        (spotlight! "货栈里的板条箱"
          (string-append
            "新到的机器停在三号货栈，板条箱比人还高。扛货的人绕着它走。\n"
            "装卸队在招人，工钱开得很高——码头上没几个人愿意接这份活。"))))

    (define (node-sell-contraband-locally)
      (node "把私货散卖给水手"
        :subtitle "保底渠道，价钱很低"
        :requires (list (req-item "私货" 1))
        :resolve (instant
          (outcome "私货脱手"
            (lambda () (add-item! "金钱" 12))))))

    (define (children)
      (append
        (list (node-haul))
        (three-letters 'nodes-at "码头")
        (joe 'dock-nodes)
        (frank 'dock-nodes)
        (machine-nodes)
        (if (> (item-count "私货") 0) (list (node-sell-contraband-locally)) '())))

    (machine-sync!)
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data) (list (container "码头" (children))))
          ((equal? msg 'save)
           (list (list "machine-stage" machine-stage)
                 (list "machine-progress" (machine-clk 'save))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! machine-stage (assoc-get data "machine-stage" 0))
             (machine-clk 'load! (assoc-get data "machine-progress" 0))
             (machine-sync!)))
          (else #f))))))
