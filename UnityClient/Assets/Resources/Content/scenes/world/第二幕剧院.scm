;; D9—10 可争取试演时长；D10 夜宵是独立私人片段，D11 夜莺主动上台。
(define 第二幕剧院
  (let ()
    (define promised? #f)
    (define introduced? #f)
    (define private-seen? #f)
    (define program-locked? #f)
    (define preparation "未开始") ; 未开始／筹备中／已备好／放下
    (define version "未定")       ; 完整试唱／压缩试唱，唱法由夜莺自己决定
    (define performance-seen? #f)
    (define aftermath-seen? #f)
    (define (active?) (and (= (第一幕 'act) 2) (not (第二章 'started?))))
    (define (prep-open?) (and (active?) (>= world-day 9) (<= world-day 10) (equal? preparation "筹备中")))
    (define (promise-trial!)
      (set! promised? #t)
      (play-dialogue!
        (line "夜莺" "大后天下午，赞助人来听试唱。你也来，好不好？")
        (line "尼尔" "我会来。在后台侧幕找你。")))
    (define (introduce!)
      (set! introduced? #t)
      (set! preparation "筹备中")
      (play-dialogue!
        (line "世界" "舞台监督把夜莺的歌名写进节目表，却只留了很短一格时间。")
        (line "夜莺" "我练了整首歌。来试演，却只能唱一半？")
        (line "尼尔" "要不我去帮你问问能不能留出完整时间？")
        (line "夜莺" "好。但别替我挑歌，我想自己决定唱给他们听什么。")))
    (define (private!)
      (set! private-seen? #t)
      (play-dialogue!
        (line "世界" "排练散场，夜莺在演出服外裹上旧大衣，拎着一包三明治来到侧门。")
        (line "夜莺" "先吃点东西。你明天还要上工。")
        (line "世界" "她将面包掰成两半，给尼尔一半。街对面的剧院正给新海报安装灯。")
        (line "夜莺" "你看见了吗？他们把我的名字印得那么大。")
        (line "尼尔" "你看起来很高兴。")
        (line "夜莺" "当然。站在那台上，有时候我觉得什么都能要到。")
        (line "尼尔" "那你还想要什么？")
        (line "夜莺" "还不知道。但肯定不止眼前这些。")
        (line "世界" "两个人安静地吃了一会儿。剧院的灯慢慢暗下去。")))
    (define (lock-program!)
      (set! program-locked? #t)
      (play-dialogue!
        (line "经理" "明天客人提前到。节目表今晚敲定，能争出时间就让她唱完。")
        (line "夜莺" "我知道。我要他们记住我的声音。")))
    (define (on-prep-result! result)
      (if (member? result '(完整试唱 压缩试唱)) #t (error "试演Scenario：非法结果"))
      (set! preparation (if (eq? result '完整试唱) "已备好" "放下")))
    (define (enter-preparation!)
      (if (prep-open?) #t (error "试演Scenario：不在进入窗口"))
      (start-scenario "赞助人试演" on-prep-result!))
    (define (settle-version!)
      (if (equal? version "未定")
        (set! version (if (equal? preparation "已备好") "完整试唱" "压缩试唱")) #f))
    (define (performance!)
      (settle-version!)
      (set! performance-seen? #t)
      (play-dialogue!
        (line "世界" "赞助人刚坐好，监督就举起了手。夜莺没有等尼尔为她说话，自己走上舞台。")
        (line "夜莺" (if (equal? version "完整试唱")
          "请听完这一首。它曾在很小的酒馆里唱过，如今我想把它唱给你们。"
          "时间不多，我明白。但我想让你们真正听见我。"))
        (line "世界" (if (equal? version "完整试唱")
          "第一段唱得轻，第二段突然亮起来。她完整唱完，台下有人忘记了翻节目单。"
          "她把中段大胆地收进最后几句里。赞助人原本在看表，终于抬头望向了舞台。"))
        (line "世界" "一个本来只打算听几分钟的人，起身为她鼓掌。夜莺向台下鞠躬，笑得发亮。")
        (line "尼尔" "你让他们都听进去了。")
        (line "夜莺" "他们终于知道我是谁了。"))
      (play-dialogue!
        (line "赞助人" "她的旧港经历很好，很有故事。宣传稿得再写得动人一点。")
        (line "经理" "当然！一个从旧港走到新城的姑娘，整个城市都会爱她。")
        (line "世界" "夜莺听见了，但没马上纠正。她仍望着方才为她鼓掌的观众席。")))
    (define (aftermath!)
      (set! aftermath-seen? #t)
      (play-dialogue!
        (line "夜莺" "昨天唱得真痛快。他们还问我愿不愿意多唱几场。")
        (line "尼尔" "他们说你是从旧港的苦日子里逃出来的。")
        (line "夜莺" "他们把故事讲得比我还会唱。不过这次，我想先把歌唱好。")))
    (define (arrivals-at loc)
      (if (and (active?) (equal? loc "剧院") (第一幕 'rehearsal-seen?))
        (append
          (if (and (>= world-day 9) (<= world-day 10) (not introduced?)) (list (arrival "两份谱子" introduce!)) '())
           
           
          (if (and (= world-day 11) (not performance-seen?)) (list (arrival "赞助人试唱" performance!)) '())
          (if (and (>= world-day 12) (not aftermath-seen?)) (list (arrival "试唱余音" aftermath!)) '())) '()))
    (define (nodes-at loc)
      (if (and (active?) (equal? loc "剧院"))
        (append
          (if (and (= world-day 10) (not private-seen?))
            (list (instant-action "和夜莺吃夜宵" private!)) '())
          (if (prep-open?)
            (list (node "帮她争取时间" :anchor "剧院-后台" :button-text "介入"
              :subtitle "替她争取唱完整首歌的时间"
              :resolve (instant (outcome enter-preparation!)))) '())
          (if (and promised? (= world-day 11) (not performance-seen?))
            (list (node "赴试唱之约" :anchor "夜莺@剧院" :button-text "赴约"
              :required-before-rest "先赴夜莺的试唱之约"
              :resolve (instant (outcome performance!)))) '())
          ;; 节目单是演唱版本的可见世界事实，不只在回顾台词里换一句。
          (if (and (>= world-day 13) (not (equal? version "未定")))
            (list (at-anchor "剧院-后台" (note-node "首演节目单" "首演节目单"
              (if (equal? version "完整试唱") "夜莺获得了完整的试唱时长。节目单保留她自己的演出介绍。"
                "夜莺的试演被压缩，但她仍让赞助人记住了这首歌。")))) '())) '()))
    (define-turn-start-rule "旧歌筹备日程"
      (lambda () #t)
      (lambda ()
        (if (>= world-day 11) (settle-version!) #f)))
    (lambda args
      (cond
        ((eq? (car args) 'promise-trial!) (promise-trial!))
        ((eq? (car args) 'trial-pending?) (and promised? (= world-day 11) (not performance-seen?)))
        ((eq? (car args) 'sponsor-result) version)
        ((eq? (car args) 'nodes-at) (nodes-at (cadr args)))
        ((eq? (car args) 'arrivals-at) (arrivals-at (cadr args)))
        ((eq? (car args) 'dossier)
          (if (= (第一幕 'act) 2)
            (list (dossier "夜莺：大剧院" :kind '人物 :primary #t
              :status (if 公告-夜莺看过? '了结 '进行中)
              :now (cond
                ((>= world-day 13) "首演海报把她的名字与旧港改造连在一起。剧院后台已有印好的节目单。")
                ((and (= world-day 11) (not performance-seen?)) "今天去剧院听试唱。你答应了到场。")
                ((equal? version "完整试唱") "她赢得完整的演出时间，试唱已经结束。")
                ((equal? version "压缩试唱") "她只获得简短演出时间，却仍唱得出色。")
                ((equal? preparation "已备好")
                  (string-append "完整试唱的时间已经争取到，" (第二幕-日期 11) "听她自己作决定。"))
                ((equal? preparation "放下")
                  (string-append "筹备已放下。" (第二幕-日期 11) "试唱时，她会决定怎么唱。"))
                ((and introduced? (= world-day 10)) "经理今天要敲定节目表。今晚之前，还可以继续为旧曲排练争取时间。")
                (introduced?
                  (string-append "她想把整首歌唱完。" (第二幕-日期 10)
                    "收工前，可以到后台帮她争取完整试唱的条件。"))
                (else (string-append "你答应了去看正式彩排。"
                  (第二幕-日期 11) "，她会向赞助人试唱。")))
              :where "剧院")) '()))
        ((eq? (car args) 'save) (list (list "version" 2) (list "promised" promised?) (list "intro" introduced?)
          (list "private" private-seen?) (list "locked" program-locked?) (list "preparation" preparation)
          (list "singing" version) (list "performance" performance-seen?) (list "aftermath" aftermath-seen?)
          ))
        ((eq? (car args) 'load!)
          (let ((d (cadr args)))
            (set! promised? (assoc-get d "promised" (assoc-get d "trial-promised" #f)))
            (set! introduced? (assoc-get d "intro" #f))
            (set! private-seen? (assoc-get d "private" (assoc-get d "care" #f)))
            (set! program-locked? (assoc-get d "locked" #f))
            (set! preparation (assoc-get d "preparation" "未开始"))
            (set! version (assoc-get d "singing"
              (if (member? (assoc-get d "sponsor-result" "未参与") '("未参与")) "未定" "压缩试唱")))
            (set! performance-seen? (assoc-get d "performance"
              (member? (assoc-get d "sponsor-result" "未参与") '("修好设备" "替代伴奏" "争取清唱" "她自己唱完"))))
            (set! aftermath-seen? (assoc-get d "aftermath" (assoc-get d "sponsor-aftermath" #f)))
            (if (>= world-day 11) (settle-version!) #f)
            (if (and (member? preparation '("未开始" "筹备中" "已备好" "放下"))
                     (member? version '("未定" "完整试唱" "压缩试唱"))) #t (error "剧院：非法演出状态"))))
        (else (error "第二幕剧院：未知消息"))))))
