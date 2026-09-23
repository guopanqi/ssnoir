;; scenes/world/第二章/人物事件/别给他们想要的.scm - 弗兰克·第二章 Phase A
;;
;; 首演之后，全城看到的是一个很简单的故事：老街养出来的暴力前男友，首演夜闯剧院。
;; 报纸接着把莱恩和老街绑在一起。于是 Phase A 的老街先变了：警察多了，记者来了，
;; 有人怪莱恩，也有人怪弗兰克当初护着他。
;;
;; 这一拍分两段：
;;   《你不是说他只是想要钱吗》 弗兰克让人捎话，你去酒馆后屋。纯人物戏：他问莱恩到底干了什么，
;;                               你能确定的只有勒索、威胁、首演夜去了剧院；第三封信——不知道。
;;   《别给他们想要的》         话没说完，外面吵起来了。警察来带一个年轻码头工人，两堆人绷着，
;;                               路边站着摄影师。交锋见 encounters/别给他们想要的.scm。
;;
;; 成功：年轻人当天回来，没人被打，没有照片。弗兰克第一次看见有件事你能做到、他做不到。
;;       酒馆后屋从此对你开着（后屋的生活内容在 老街酒馆.scm）。
;; 失败 / 没去：有人扔东西，有人被打伤，年轻人关一夜，第二天报纸上一张极糟的照片。
;;       这条线到此中断；弗兰克更信「跟他们讲道理没用」。
;;
;; 机器进入老街那天他还坐不坐得下来（frank 'at-table?），就由这一场写入。
;; 他没劝的话本来就会走到硬的那一边——这不是惩罚，是他本来就会做的事。
;;
;; 他叫你，不看巷子那晚你收得干不干净：他要的是莱恩的答案，不是人情。
(define 别给他们想要的
  (let ()
    (define 捎话-第几天 2)       ; 首演之后第二天有人敲门
    (define 窗口天数 4)          ; 之后四天里不去，街上那场在你缺席时发生

    ;; 未开始 / 等你 / 成功 / 失败 / 缺席
    (define 状态 "未开始")
    (define journal (make-journal))

    (define (等你?) (equal? 状态 "等你"))
    (define (到期日) (第二章 'day-of (+ 捎话-第几天 窗口天数)))
    (define (老街还在风口?) (and (not (equal? 状态 "未开始")) (equal? (第二章 'phase) "A")))

    ;; ── 日历 ────────────────────────────────────────
    (define (捎话!)
      (set! 状态 "等你")
      (frank 'on-mediation-summoned!)
      (play-remote-dialogue!
        (line "世界" "天没亮透就有人敲门。门外是个码头上见过的年轻人。")
        (line "码头工人" "弗兰克让我带句话。他在酒馆后屋，让你过去一趟。")
        (line "尼尔" "什么事？")
        (line "码头工人" "他没说。他只说，让你过去。"))
      (journal 'add! "弗兰克让人捎话，让你去酒馆后屋。")
      (spotlight! "弗兰克叫你"
        "报纸把莱恩和老街写成了一回事。弗兰克让人捎话，让你去老街酒馆的后屋。"))

    (define (缺席!)
      (set! 状态 "缺席")
      (frank 'on-mediation-result! "缺席")
      (journal 'add! "你没去老街。警察在酒馆门口带人，有人先动了手。第二天头版是那张照片。")
      (spotlight! "老街暴徒袭击警方"
        "你没去。警察在酒馆门口带走一个年轻人，有人扔了东西。第二天报纸上是那张照片。"))

    (define (on-day-start!)
      (cond
        ((and (equal? 状态 "未开始")
              (equal? (第二章 'phase) "A")
              (>= (第二章 'day) 捎话-第几天))
         (捎话!))
        ((and (等你?) (>= world-day (到期日)))
         (缺席!))
        (else #f)))

    ;; ── 交锋回执 ────────────────────────────────────
    (define (调停结果! result)
      (cond
        ((equal? result 'success)
         (set! 状态 "成功")
         (frank 'on-mediation-result! "成功")
         ;; 关系支援：从此任何一场交锋都能叫一个老街的人来（见 engine.scm 的 support-frank）。
         (grant-support! "弗兰克")
         (journal 'add! "警察来带人那天没人动手。年轻人做完笔录当天回来。弗兰克把后屋的门推开了。")
         (complete-task! "弗兰克叫你")
         (play-remote-dialogue!
           (line "世界" "天黑以后你回到酒馆。后屋的门开着。")
           (line "弗兰克" "他回来了。")
           (line "弗兰克" "我压得住他们。跟那边说话的，这条街上没有。")
           (line "弗兰克" "以后进来不用问。"))
         (spotlight! "关系支援：弗兰克"
           "《叫个人来》——以后任何一场交锋，都能叫一个老街的帮手来一回合。每场一次。"))
        ((equal? result 'fail)
         (set! 状态 "失败")
         (frank 'on-mediation-result! "失败")
         (journal 'add! "警察来带人那天有人先动了手。年轻人关了一夜，第二天头版是那张照片。")
         ;; 失败也算经历完：他看见了你做不到，这一节照样结。缺席不发。
         (complete-task! "弗兰克叫你")
         (play-remote-dialogue!
           (line "世界" "第二天的报纸摊在后屋桌上。照片里有人倒在地上，警察的手举在半空。")
           (line "弗兰克" "你看见了。")
           (line "弗兰克" "跟他们讲道理没用。")))
        (#t (error "别给他们想要的：交锋返回未知结果"))))

    ;; ── 酒馆里那扇门 ────────────────────────────────
    ;; 第一章所有人盯着你。现在还是有人盯着你，但没人拦。
    (define (node-back-room)
      (node "弗兰克"
        :anchor "老街酒馆"
        :subtitle "他在后屋等你"
        :children
          (list
            (encounter-action "去后屋"
              (lambda ()
                (play-dialogue!
                  (line "世界" "吧台后面的人抬头看了你一眼，又低下去。没人拦。")
                  (line "世界" "后屋只点了一盏灯。他坐在桌子那头，报纸叠在手边。")
                  (line "弗兰克" "他到底干了什么？")
                  (line "尼尔" "勒索她。威胁过她。首演那晚，他去了剧院。")
                  (line "弗兰克" "报纸上说的那封信呢。台上出的那件事呢。")
                  (line "尼尔" "不知道。这两件我不知道是不是他。")
                  (line "世界" "他没说话。过了一会儿，把报纸翻过去。")
                  (line "弗兰克" "你上回来抓人，我拦你，是因为你跑到我的地方抓我的人。")
                  (line "弗兰克" "不是因为他做什么都对。")
                  (line "世界" "外面忽然吵起来。有人在喊，然后是车门的声音。")
                  (line "弗兰克" "警察。第三回了。")
                  (line "世界" "他站起来，从你身边走出去。你跟着他。"))
                (start-encounter "别给他们想要的" 调停结果!))))))

    (define (nodes-at location)
      (append
        (if (and (等你?) (equal? location "老街酒馆"))
            (list (node-back-room))
            '())
        ;; Phase A 的老街只做氛围，不碰钱包：钱包上那一下留给 Phase B 的码头。
        (if (老街还在风口?)
            (cond
              ((equal? location "码头")
               (list (at-anchor "码头-岸口"
                       (note-node "标注：码头巡查" "巡警多了"
                         "两个巡警沿泊位走了一整个上午。有人卸货时不再说话。"))))
              ((equal? location "码头居民区")
               (list (note-node "标注：老街生人" "找故事的人"
                       "门廊下站着两个不是这儿的人，其中一个拿着本子。有人在骂莱恩，也有人在骂弗兰克。")))
              (#t '()))
            '())))

    ;; 一张卡：去后屋 → 街上那一场。捎话那天立卡，调停结了（或你缺席）就了结。
    (define (steps)
      (list (step "去老街酒馆的后屋见他" (member? 状态 (list "成功" "失败")))
            (step "别给他们想要的" (member? 状态 (list "成功" "失败")))))

    (define (dossier-entry)
      (cond
        ((等你?)
         (list (dossier "弗兰克叫你"
                 :kind '人物 :status '进行中
                 :now "去老街酒馆的后屋见弗兰克"
                 :where "老街酒馆"
                 :clocks (list (日期倒计时 "他等着" (到期日) 窗口天数
                                 "街上的事不等你。"))
                 :steps (steps)
                 :log (journal 'render-data))))
        ((member? 状态 (list "成功" "失败" "缺席"))
         (list (dossier "弗兰克叫你" :kind '人物 :status '了结
                 :now "" :where ""
                 :steps (steps)
                 :log (journal 'render-data))))
        (#t '())))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) '())
          ((equal? msg 'dossier) (dossier-entry))
          ((equal? msg 'on-day-start!) (on-day-start!))
          ((equal? msg 'state) 状态)
          ((equal? msg 'save) (list (list "state" 状态) (list "journal" (journal 'save))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! 状态 (assoc-get data "state" "未开始"))
             (journal 'load! (assoc-get data "journal" '()))
             (if (member? 状态 (list "未开始" "等你" "成功" "失败" "缺席"))
                 #t
                 (error "别给他们想要的存档错误：状态非法"))))
          (else (error "别给他们想要的：收到未知消息")))))))
