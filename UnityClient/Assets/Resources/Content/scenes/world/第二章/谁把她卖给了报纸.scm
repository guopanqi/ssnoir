;; scenes/world/第二章/制造一个故事.scm - 锚点二《制造一个故事》
;;
;; 这一场是第二章唯一一次真正的世界转换。它不是「做完第二个锚点所以进入下一阶段」，
;; 而是玩家在这一晚**第一次碰到不该碰的东西**：有人在第三封信出现之前，
;; 就已经排好了那件事该怎么见报。
;;
;; 人物问题：尼尔愿不愿意帮夜莺成为她想成为的那个夜莺。
;; 她要你去经理那叠宣传材料里，把她过去的一点东西拿回来。你在那叠纸里
;; 会看见另一样东西——**看见是必然的，带不带得走是你的选择**：
;;   替她清干净  她信你多一分，你只看见那个日期，拿不出纸
;;   翻那叠稿子  你把排期表带走了，她也看见你在翻她的东西
;; 两条都进 Phase B。差别留到第三章：手里有没有那张纸，是妥协方案成不成立的条件之一。
;;
;; 必经拍用第一章那套写法：到日子她派人来叫你，睡不着，直到你去剧院。
(define 制造一个故事
  (let ()
    (define 开门-第几天 8)

    ;; 未开始 / 待去 / 在翻 / 清干净了 / 拿到纸了
    (define 状态 "未开始")

    (define (待去?) (equal? 状态 "待去"))
    (define (在翻?) (equal? 状态 "在翻"))
    (define (结了?) (or (equal? 状态 "清干净了") (equal? 状态 "拿到纸了")))
    (define (开门日) (第二章 'day-of 开门-第几天))

    ;; ── 必经拍：到日子就堵着 ─────────────────────────
    ;; 「在翻」那一格不指名任何一张卡：柜子前面的两张是这一场要玩家自己回答的
    ;; 问题，指路标记落在其中一张上等于替他选。指路只用在「你还没到场」的时候。
    (define (sync-blockers!)
      (rest-release! "第二章/制造一个故事")
      (cond
        ((待去?) (rest-block! "第二章/制造一个故事" "夜莺让人来叫你" "剧院" "见夜莺"))
        ((在翻?) (rest-block! "第二章/制造一个故事" "你还站在经理的柜子前"))
        (#t #f)))

    (define (on-day-end!)
      (if (and (equal? 状态 "未开始") (第二章 'started?)
               (>= world-day (开门日)))
          (begin
            (set! 状态 "待去")
            (sync-blockers!)
            (spotlight! "她让人来叫你"
              "剧院派人来了。夜莺有件旧东西压在经理那叠宣传材料里，她想拿回来。"))
          #f))

    ;; ── 那一晚 ──────────────────────────────────────
    (define (node-meet)
      (instant-action "见夜莺"
        (lambda ()
          (play-dialogue!
            (line "夜莺" "经理办公室里有一叠东西，我的旧照片在里头。")
            (line "尼尔" "让他给你不就完了。")
            (line "夜莺" "他会问我要来干什么。我不想让他知道我在乎。")
            (line "夜莺" "他今晚在楼下应酬。钥匙在化妆间镜子底下。")
            (line "尼尔" "我拿了就走。")
            (line "夜莺" "……别看别的。"))
          (set! 状态 "在翻")
          (sync-blockers!)
          (spotlight! "经理的柜子"
            "旧照片压在一叠宣传材料里。抽出来很快，可这叠纸里还有别的东西。"))))

    (define (收场! 新状态 标题 正文 履历)
      (set! 状态 新状态)
      (sync-blockers!)
      ((第二章 'journal) 'add! 履历)
      (spotlight! 标题 正文)
      (第二章 '进入阶段-B!))

    ;; 两张都不要骰：主轴上的必经拍不跟今天的城市生活抢那四颗骰。
    ;; 这一格里玩家没有「不选」的余地，真把骰花光了又睡不了觉，就是软锁。
    (define (node-clean)
      (instant-action "替她清干净"
        (lambda ()
          (nightingale 'on-banquet-stayed!)
          (play-dialogue!
            (line "世界" "照片、旧海报、一张六年前的酒馆演出单，你把它们抽出来。")
            (line "世界" "抽到最后一张时，你看见底下压着一份排期表。")
            (line "世界" "第三封信那件事排在上面。日期比信早了四天。")
            (line "尼尔" "……")
            (line "世界" "楼下有人上楼。你把柜子合上了。"))
          (收场! "清干净了" "你替她收拾干净了"
            "她的东西一张不剩地拿回来了。你看见了那个日期，但你两手空空。"
            "你替夜莺清空了经理的柜子。走之前你看见一份比信更早的排期表。"))))

    (define (node-dig)
      (instant-action "翻那叠稿子"
        (lambda ()
          (nightingale 'on-drifted-apart!)
          (play-dialogue!
            (line "世界" "照片在最上面。你把它放到一边，继续往下翻。")
            (line "世界" "一份排期表。第三封信那件事排在上面，日期比信早了四天。")
            (line "世界" "你把它折进内袋。回头的时候，她站在门口。")
            (line "夜莺" "我说了别看别的。")
            (line "尼尔" "有人比莱恩更早知道这件事。")
            (line "夜莺" "……那跟我没关系。"))
          (收场! "拿到纸了" "那张纸在你身上"
            "排期表折在你内袋里。她知道你翻了她的东西，也知道你带走了什么。"
            "你从经理的柜子里带走了一份排期表。夜莺看见了。"))))

    (define (nodes-at location)
      (if (equal? location "剧院")
          (cond
            ((待去?) (list (node-meet)))
            ((在翻?) (list (node-clean) (node-dig)))
            (#t '()))
          '()))

    ;; ── 卷宗 ────────────────────────────────────────
    ;; 锚点不自己往卷宗里投卡：它是主线的一段，由章节那条「他们要夜莺」代言。
    ;; 这里只答 'now / 'where / 'clocks，章节轮到它的时候来取。

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) '())
          ((equal? msg 'dossier) '())
          ((equal? msg 'on-day-end!) (on-day-end!))
          ((equal? msg 'sync-blockers!) (sync-blockers!))
          ;; 调试用：当作那一晚你替她清干净了，好让 Phase B 直接开始。
          ((equal? msg 'debug-settle!)
           (set! 状态 "清干净了")
           (sync-blockers!))
          ((equal? msg 'state) 状态)
          ;; 第三章要读的那一条：那张排期表在不在他身上。
          ((equal? msg 'has-schedule?) (equal? 状态 "拿到纸了"))
          ((equal? msg 'now)
           (cond ((待去?) "去剧院见夜莺。她有件旧东西想拿回来")
                 ((在翻?) "经理的柜子就在眼前。拿了就走，还是往下翻")
                 ((equal? 状态 "拿到纸了") "有人比莱恩更早知道第三封信")
                 ((结了?) "你看见过那个日期，可你拿不出东西")
                 (#t "夜莺那边这几天没有事")))
          ((equal? msg 'where) (if (or (待去?) (在翻?)) "剧院" ""))
          ((equal? msg 'clocks) '())
          ((equal? msg 'save) (list (list "state" 状态)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! 状态 (assoc-get data "state" "未开始"))))
          (else (error "制造一个故事：收到未知消息")))))))
