;; 第二幕剧院：第一次彩排之后的相处，与赞助人复排是不同事件。
;; 所有窗口随时间结束；不认识夜莺也可旁观复排，私人谈话只属于真正相识者。
(define 第二幕剧院
  (let ()
    (define care-seen? #f)
    (define listening "未听")
    (define sponsor-intro? #f)
    (define sponsor-result "未参与")
    (define sponsor-aftermath? #f)
    (define old-port-talk? #f)
    (define equipment-checked? #f)
    (define equipment-ready? #f)
    (define musician-booked? #f)
    (define (known?) (>= (第一幕 'singer-stage) 1))
    (define (in-theater? loc) (equal? loc "剧院"))
    (define (replay-time?)
      (and (= (第一幕 'act) 2) (>= world-day 10) (<= world-day 11)))
    (define (prep-time?)
      (and (= (第一幕 'act) 2) (>= world-day 8) (<= world-day 9)
           (第二幕生活 'theater-seen?)))
    (define (check-equipment! grade)
      (if equipment-checked? (error "试演准备：设备已检查") #t)
      (set! equipment-checked? #t)
      (cond
        ((eq? grade '好)
          (set! equipment-ready? #t)
          (play-dialogue!
            (line "工作人员" "这根线早就松了！我先标出来，试演当天一眼就能找到。")))
        ((eq? grade '中)
          (set! equipment-ready? #t)
          (spend-composure! 1)
          (play-dialogue!
            (line "世界" "尼尔费了不少工夫，把松动的接线一处处记在工作单上。")))
        (else
          (spend-composure! 1)
          (play-dialogue!
            (line "工作人员" "你没有找到毛病。下次如果出故障，就只能在现场排查。")))))
    (define (book-musician!)
      (if musician-booked? (error "试演准备：旧乐手已约好") #t)
      (remove-item! "金钱" 25)
      (set! musician-booked? #t)
      (play-dialogue!
        (line "世界" "尼尔在旧街食堂门外找到从前在酒馆伴奏的乐手。乐手数着二十五美元订金，把时间写进小本子。")
        (line "乐手" "试演那两天我都能来，真出岔子就叫我。")
        (line "尼尔" "希望用不上你。")
        (line "乐手" "希望如此。订金可不退。")))
    (define (show-care!)
      (set! care-seen? #t)
      (play-dialogue!
        (line "夜莺" "你的眼睛都快睁不开了。最近又去了码头？")
        (line "尼尔" "来看看你排得怎么样。")
        (line "夜莺" "我是问你，不是问我的排练。")
        (line "夜莺" "后台有热茶。你坐一会儿，等我收工。")))
    (define (ask-about-song!)
      (set! listening "待答复")
      (play-dialogue!
        (line "世界" "她让钢琴师把开头再弹一遍。")
        (line "世界" "这回她唱得轻些，像还站在酒馆的小台上。")
        (line "夜莺" "导演喜欢第一遍。你喜欢哪一遍？")
        (line "夜莺" "不用怕说错。")))
    (define (answer-song! chosen)
      (if (equal? listening "待答复") #t (error "再唱一遍：答案重复"))
      (set! listening chosen)
      (play-dialogue!
        (line "尼尔" (if (equal? chosen "酒馆")
          "第二遍像我第一次听到你唱歌的时候。"
          "第一遍更像你要去的地方。"))
        (line "夜莺" (if (equal? chosen "酒馆")
          "那地方我唱了好多年。不过我也想试试新的唱法。"
          "我很喜欢那个舞台。只是还没习惯别人替我决定怎么唱。"))))
    (define (on-sponsor-result! result)
      (if (member? result '(修好设备 替代伴奏 争取清唱 她自己唱完))
        #t (error "赞助人复排：未知结果"))
      (set! sponsor-result (symbol->string result)))
    (define (stand-back!)
      (set! sponsor-result "未介入")
      (play-dialogue!
        (line "世界" "尼尔没有走进后台。夜莺和工作人员仍在商量演出。")
        (line "夜莺" "如果设备不行，就让我清唱吧。")))
    (define (talk-about-past!)
      (set! old-port-talk? #t)
      (play-dialogue!
        (line "夜莺" "宣传的人说，我的故事最好从走进大剧院那天开始。")
        (line "尼尔" "那你以前那些年呢？")
        (line "夜莺" "他们说旧港不够体面。")
        (line "夜莺" "可要是没有那个酒馆，我连第一首歌都唱不出来。")
        (line "夜莺" "我还没想好，应该怎么和他们说。")))
    (define (replay-after!)
      (set! sponsor-aftermath? #t)
      (cond
        ((equal? sponsor-result "修好设备")
          (play-dialogue!
            (line "夜莺" "他们说机器终于修好了。你那天一直在后台忙。")
            (line "夜莺" "我唱第二段的时候，你甚至没听见。谢谢你。")))
        ((equal? sponsor-result "替代伴奏")
          (play-dialogue!
            (line "夜莺" "临时弹琴的那个人，手法和旧街的人很像。")
            (line "夜莺" "我差点忘了自己是在大剧院。")))
        ((equal? sponsor-result "争取清唱")
          (play-dialogue!
            (line "夜莺" "原来我也能不用伴奏，把那首歌唱完。")
            (line "夜莺" "但没有你开口，导演恐怕不肯让我试。")))
        (else
          (play-dialogue!
            (line "世界" "上一场试演，伴奏中途停下了。")
            (line "夜莺" "他们想结束，我还是把最后一段唱完了。")
            (line "夜莺" "结果怎么样，我现在也不想猜。")))))
    ;; 一条线收拢已公开的彩排日程和亲眼遇到的后台事件。
    ;; 不把后台故障、试演结果或私人谈话提前透露给玩家。
    (define (trial-known?) (or (第二幕生活 'theater-seen?) sponsor-intro?))
    (define (trial-done?) (not (equal? sponsor-result "未参与")))
    (define (trial-now)
      (cond
        (sponsor-aftermath? "夜莺已经说起试演的经过，她把歌唱完了。")
        ((equal? sponsor-result "修好设备") "你修好了伴奏设备，试演已结束。")
        ((equal? sponsor-result "替代伴奏") "你找到了替代伴奏，试演已结束。")
        ((equal? sponsor-result "争取清唱") "你替夜莺争取到清唱，试演已结束。")
        ((equal? sponsor-result "她自己唱完") "夜莺自己把歌唱完了。")
        ((equal? sponsor-result "未介入") "你决定不介入，夜莺和工作人员自行处理。")
        ((> world-day 11) "赞助人试演的日程已过，还没听到现场的消息。")
        (sponsor-intro?
          (string-append "伴奏设备出了故障。" (第二幕-日期 11) "试演窗口结束前，可到后台帮忙。"))
        ((< world-day 10)
          (string-append (第二幕-日期 10) "起有赞助人试演。"
            (if musician-booked? "你已花25美元约好老乐手。"
              (if equipment-ready? "设备线路已经查过。"
                "可以预先检查设备，或去旧街找乐手。"))))
        (else (string-append "大剧院正在举行赞助人试演，" (第二幕-日期 11) "结束。"))))
    (define (story-now)
      (cond
        ((and (>= world-day 13) 公告-夜莺看过?)
          (if (equal? 公告-夜莺回答 "未答复")
            "夜莺问你怎样看首演海报对旧港的描述，可以去剧院答复她。"
            "你已经回应了夜莺对首演宣传的疑虑。"))
        ((>= world-day 13) "晨报把夜莺的首演与旧港改造连在了一起。")
        ((and sponsor-intro? (not (trial-done?)) (<= world-day 11)) (trial-now))
        ((equal? listening "待答复") "夜莺又唱了一遍，正在等你说更喜欢哪一遍。今天可到剧院答复。")
        ((and (<= world-day 8) (not (第一幕 'rehearsal-seen?)))
          (string-append (第二幕-日期 8) "，夜莺在大剧院首次正式彩排，可以去看。"))
        ((trial-known?) (trial-now))
        ((第一幕 'rehearsal-seen?) "你看过夜莺的首次彩排。还没听到下一场的安排。")
        ((<= world-day 8) (string-append (第二幕-日期 8) "，夜莺在大剧院首次正式彩排，可以去看。"))
        (else "首次正式彩排已经结束，你没有到场。还没听到下一场的安排。")))
    (define (story-steps)
      (append
        (list (step
          (cond ((第一幕 'rehearsal-seen?) "到大剧院看过首次彩排")
            ((> world-day 8) "首次彩排已结束，未到场")
            (else (string-append (第二幕-日期 8) "去大剧院看首次彩排")))
          (or (第一幕 'rehearsal-seen?) (> world-day 8))))
        (if (equal? listening "待答复")
          (list (step "今天去剧院，告诉夜莺更喜欢哪一遍" #f)) '())
        (if (or (trial-known?) equipment-checked? musician-booked?)
          (list (step
            (cond
              (musician-booked? "已付25美元订金，请老乐手待命")
              (equipment-ready? "提前记下设备线路的位置")
              (equipment-checked? "提前检查过设备，但没有找到问题")
              ((< world-day 10) "试演前可选择花时间或金钱准备")
              (else "试演已开始，准备窗口关闭"))
            (or equipment-checked? musician-booked? (> world-day 9)))) '())
        (if (trial-known?)
          (list (step
            (cond ((and sponsor-intro? (not (trial-done?)) (<= world-day 11))
                "去大剧院后台，帮她解决伴奏故障")
              ((> world-day 11) "赞助人试演日程已结束")
              ((trial-done?) "赞助人试演已有结果")
              ((< world-day 10) (string-append (第二幕-日期 10) "起去大剧院看赞助人试演"))
              (else "今天可去大剧院看赞助人试演"))
            (or (trial-done?) (> world-day 11)))) '())
        (if sponsor-aftermath? (list (step "听夜莺说起试演经过" #t)) '())
        (if (>= world-day 13)
          (list (step "晨报刊出首演与旧港改造的联合宣传" #t)) '())
        (if (and (>= world-day 13) 公告-夜莺看过?)
          (list (step "回应夜莺对宣传海报的疑虑"
            (not (equal? 公告-夜莺回答 "未答复")))) '())))
    (define (nodes-at loc)
      (if (and (equal? loc "码头居民区") (prep-time?)
               (not musician-booked?))
        (list (node "约老乐手" :name "预订旧乐手"
          :subtitle "25美元及一骰；试演当天可直接请他救场"
          :requires (list (req-die) (req-item "金钱" 25))
          :resolve (instant (outcome book-musician!))))
        (if (not (in-theater? loc)) '()
        (append
          (if (and (prep-time?) (not equipment-checked?))
            (list (node "检查伴奏线路"
              :subtitle "花一骰检查设备；找到问题可缩短救场"
              :risk '冒险 :risk-target (list 'composure "player")
              :requires (list (req-die))
              :resolve (roll 'sharpness
                (outcome (lambda () (check-equipment! '坏)))
                (outcome (lambda () (check-equipment! '中)))
                (outcome (lambda () (check-equipment! '好)))))) '())
          (if (and (= world-day 9) (known?) (not care-seen?))
            (list (instant-action "与夜莺聊近况" show-care!)) '())
          (if (and (replay-time?) (equal? sponsor-result "未参与")
                   (not sponsor-intro?))
            (list (node "看看赞助人试演"
              :subtitle "试演进行中；可以去后台看看"
              :resolve (instant (outcome sponsor-intro!)))) '())
          (if (and (>= world-day 12) (<= world-day 13) (known?) (not sponsor-aftermath?)
                   (not (equal? sponsor-result "未参与")))
            (list (instant-action "问试演后来的事" replay-after!)) '())
          (if (and (= world-day 9) (known?)
                   (第一幕 'rehearsal-seen?) (equal? listening "未听"))
            (list (node "再唱一遍" :subtitle "她想听听你的意见"
              :resolve (instant (outcome ask-about-song!)))) '())
          (if (equal? listening "待答复")
            (list (container "你喜欢哪一遍？" (list
              (instant-action "酒馆里的那遍" (lambda () (answer-song! "酒馆")))
              (instant-action "大剧院里的那遍" (lambda () (answer-song! "剧院"))))))
            '())
          (if (and (replay-time?) sponsor-intro?
                   (equal? sponsor-result "未参与"))
            (list (container "赞助人试演" (list
              (node "帮她想办法"
                :clocks (list (第二幕-期限钟 "试演结束" 11 10))
                :resolve (instant (outcome (lambda ()
                  (start-encounter "赞助人试演" on-sponsor-result!)))))
              (node "让她自己处理"
                :clocks (list (第二幕-期限钟 "试演结束" 11 10))
                :resolve (instant (outcome stand-back!))))))
            '())
          (if (and (>= world-day 12) (<= world-day 13)
                   (known?) (not old-port-talk?))
            (list (node "她过去的歌" :subtitle "宣传人员想淡化她来自旧港的经历"
              :resolve (instant (outcome talk-about-past!)))) '())))))
    (define (sponsor-intro!)
      (set! sponsor-intro? #t)
      (play-dialogue!
        (line "世界" "赞助人要听夜莺试演。后台的伴奏机器却在反复报错。")
        (line "工作人员" "今天要是坏了，导演不会愿意等。")
        (line "夜莺" "我只想把这首歌唱完。")))
    (define (arrivals-at loc) '())
    (define-turn-start-rule "复排窗口自然结束"
      (lambda () #t)
      (lambda ()
        (if (and (>= world-day 12) (equal? sponsor-result "未参与"))
          (set! sponsor-result "错过") #f)
        (if (and (>= world-day 10) (equal? listening "待答复"))
          (set! listening "错过") #f)))
    (lambda args
      (let ((msg (car args)))
        (cond
          ((eq? msg 'nodes-at) (nodes-at (cadr args)))
          ((eq? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((eq? msg 'sponsor-result) sponsor-result)
          ((eq? msg 'equipment-ready?) equipment-ready?)
          ((eq? msg 'musician-booked?) musician-booked?)
          ((eq? msg 'dossier)
            (if (and (= (第一幕 'act) 2) (>= world-day 7))
              (list (dossier "夜莺：大剧院" :kind '人物 :primary #t
                :status (if (and (>= world-day 13) 公告-夜莺看过?
                                (not (equal? 公告-夜莺回答 "未答复"))) '了结 '进行中)
                :now (story-now) :steps (story-steps) :where "剧院"))
              '()))
          ((eq? msg 'save)
            (list (list "care" care-seen?)
                  (list "listening" listening)
                  (list "sponsor-intro" sponsor-intro?)
                  (list "sponsor-result" sponsor-result)
                  (list "sponsor-aftermath" sponsor-aftermath?)
                  (list "old-port-talk" old-port-talk?)
                  (list "equipment-checked" equipment-checked?)
                  (list "equipment-ready" equipment-ready?)
                  (list "musician-booked" musician-booked?)))
          ((eq? msg 'load!)
            (let ((data (cadr args)))
              (set! care-seen? (assoc-get data "care" #f))
              (set! listening (assoc-get data "listening" "未听"))
              (set! sponsor-intro? (assoc-get data "sponsor-intro" #f))
              (set! sponsor-result (assoc-get data "sponsor-result" "未参与"))
              (set! sponsor-aftermath? (assoc-get data "sponsor-aftermath" #f))
              (set! old-port-talk? (assoc-get data "old-port-talk" #f))
              (set! equipment-checked? (assoc-get data "equipment-checked" #f))
              (set! equipment-ready? (assoc-get data "equipment-ready" #f))
              (set! musician-booked? (assoc-get data "musician-booked" #f))
              (if (and equipment-ready? (not equipment-checked?))
                (error "试演准备：存档有设备成果却未检查") #t)
              (if (and (member? listening '("未听" "待答复" "酒馆" "剧院" "错过"))
                       (member? sponsor-result '("未参与" "未介入" "修好设备" "替代伴奏" "争取清唱" "她自己唱完" "错过")))
                #t (error "第二幕剧院：存档非法"))))
          (else (error "第二幕剧院：未知消息")))))))
