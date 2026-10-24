;; D9—10可进入独立筹备交锋；回合不会推进城市日期，D11按结果演出。
(define 第二幕剧院
  (let ()
    (define promised? #f)
    (define introduced? #f)
    (define private-seen? #f)
    (define program-locked? #f)
    (define preparation "未开始") ; 未开始／筹备中／已备好／放下
    (define version "未定")       ; 只保留旧歌／改编两个演出结果
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
        (line "世界" "两份谱子并排放在钢琴上。新的边角平整，旧的留着一圈酒渍。")
        (line "夜莺" "这段新编曲真好听。伴奏一响，我都像换了个人。")
        (line "尼尔" "老街的那首呢？")
        (line "夜莺" "导演想把旧港那段剪掉，说赞助人未必爱听。")
        (line "夜莺" "我还是想把两版都试完。可乐团只排新谱，后台也没有整段空档。")
        (line "尼尔" "我去后台问问，能不能挤出一段时间。")
        (line "夜莺" "别为难自己。我也去找导演，总不能让你一个人为我的事跑。")))
    (define (private!)
      (set! private-seen? #t)
      (play-dialogue!
        (line "世界" "排练散场，夜莺在演出服外裹上旧大衣，拎着一包三明治来到侧门。")
        (line "夜莺" "你还没吃吧。坐下，分你一半。")
        (line "夜莺" "这里连换下来的衣服都有人收走熨好。刚来那天，我都不知道该把外套放哪儿。")
        (line "尼尔" "你不是一直想来这里吗？")
        (line "夜莺" "是啊。新裙子还是照着我量的，针脚都看不见。我有点舍不得走了。")
        (line "夜莺" "你呢？还在码头替人搬东西？")
        (line "尼尔" "有活就接。想接点调查，还没几个人肯找我。")
        (line "夜莺" "别把自己累坏了。我还想让你来听我唱呢。")))
    (define (lock-program!)
      (set! program-locked? #t)
      (play-dialogue!
        (line "经理" "明天的节目表今晚得定下来。旧曲能排齐，我就留时间；排不齐，先按新编曲走。")
        (line "夜莺" "明白。把两份谱都留着，我都能唱。")))
    (define (on-prep-result! result)
      (if (member? result '(保留旧歌 接受改编)) #t (error "旧歌交锋：非法结果"))
      (set! preparation (if (eq? result '保留旧歌) "已备好" "放下")))
    (define (enter-preparation!)
      (if (prep-open?) #t (error "旧歌交锋：不在进入窗口"))
      (start-encounter "赞助人试演" on-prep-result!))
    (define (settle-version!)
      (if (equal? version "未定")
        (set! version (if (equal? preparation "已备好") "保留旧歌" "接受改编")) #f))
    (define (performance!)
      (settle-version!)
      (set! performance-seen? #t)
      (play-dialogue!
        (line "世界" "赞助人在第一排坐定。夜莺站在侧幕旁，把选好的谱子交给伴奏。")
        (line "夜莺" (if (equal? version "保留旧歌")
          "两版我都试过了。今天唱旧的这一版，从头到尾唱完。"
          "今天唱新编的这一版。我喜欢那段前奏，想让他们也听听。"))
        (line "世界" (if (equal? version "保留旧歌")
          "熟悉的前奏响起。她没有略去旧港那一段，唱完才放下握着谱架的手。"
          "乐团接起新编的前奏。她跟上新的节拍，最后一个音落下，仍望着台下笑。"))
        (line "尼尔" "你站上去了。")
        (line "夜莺" "是啊。尼尔，我还想再唱一整晚。")))
    (define (aftermath!)
      (set! aftermath-seen? #t)
      (play-dialogue!
        (line "夜莺" (if (equal? version "保留旧歌")
          "旧港那几句，我都唱出来了。他们听没听进去，我不知道。可我想留着。"
          "新编曲唱起来真过瘾。旧歌我也想留在节目里，慢慢来。"))
        (line "世界" "剧院侧门的小摊快收了。夜莺把一块热面包掰成两半，推给尼尔一半。")
        (line "夜莺" "我以为你早走了。今天又去码头了？")
        (line "尼尔" "那边有人可能要丢工作。")
        (line "夜莺" "先吃点东西，你明天还要上工。看见我的新海报了吗？他们把名字印得真大。很漂亮，是不是？")
        (line "尼尔" "你真的很高兴。")
        (line "夜莺" "当然！我在酒馆等了那么多年。有时走上舞台，我觉得什么都能要到。")
        (line "尼尔" "那你还想要什么？")
        (line "夜莺" "还不知道。但肯定不止这些。")))
    (define (arrivals-at loc)
      (if (and (active?) (equal? loc "剧院") (第一幕 'rehearsal-seen?))
        (append
          (if (and (>= world-day 9) (<= world-day 10) (not introduced?)) (list (arrival "两份谱子" introduce!)) '())
          (if (and (>= world-day 9) (<= world-day 10) (not private-seen?)) (list (arrival "收工以后" private!)) '())
          (if (and (= world-day 10) (not program-locked?)) (list (arrival "节目表敲定" lock-program!)) '())
          (if (and (= world-day 11) (not performance-seen?)) (list (arrival "赞助人试唱" performance!)) '())
          (if (and (>= world-day 12) (not aftermath-seen?)) (list (arrival "试唱余音" aftermath!)) '())) '()))
    (define (nodes-at loc)
      (if (and (active?) (equal? loc "剧院"))
        (append
          (if (prep-open?)
            (list (node "帮她准备旧歌" :anchor "剧院-后台" :button-text "介入"
              :subtitle "进入交锋，争取完整试唱条件"
              :resolve (instant (outcome enter-preparation!)))) '())
          (if (and promised? (= world-day 11) (not performance-seen?))
            (list (node "赴试唱之约" :anchor "夜莺@剧院" :button-text "赴约"
              :required-before-rest "先赴夜莺的试唱之约"
              :resolve (instant (outcome performance!)))) '())
          ;; 节目单是演唱版本的可见世界事实，不只在回顾台词里换一句。
          (if (and (>= world-day 13) (not (equal? version "未定")))
            (list (at-anchor "剧院-后台" (note-node "首演节目单" "首演节目单"
              (if (equal? version "保留旧歌") "夜莺——旧港原曲，完整演唱。末段的歌词留在印好的节目单里。"
                "夜莺——剧院新编曲。节目单列着乐团和编曲者的新署名。")))) '())) '()))
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
                ((equal? version "保留旧歌") "她选择唱完整旧歌，试唱已经结束。")
                ((equal? version "接受改编") "她选择了新编曲，试唱已经结束。")
                ((equal? preparation "已备好")
                  (string-append "旧曲与舞台时间都已排妥，" (第二幕-日期 11) "听她自己作决定。"))
                ((equal? preparation "放下")
                  (string-append "筹备已放下。" (第二幕-日期 11) "试唱时，她会决定怎么唱。"))
                ((and introduced? (= world-day 10)) "经理今天要敲定节目表。今晚之前，还可以继续为旧曲排练争取时间。")
                (introduced?
                  (string-append "她想把两版都试完。" (第二幕-日期 10)
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
              (if (member? (assoc-get d "sponsor-result" "未参与") '("未参与")) "未定" "接受改编")))
            (set! performance-seen? (assoc-get d "performance"
              (member? (assoc-get d "sponsor-result" "未参与") '("修好设备" "替代伴奏" "争取清唱" "她自己唱完"))))
            (set! aftermath-seen? (assoc-get d "aftermath" (assoc-get d "sponsor-aftermath" #f)))
            (if (>= world-day 11) (settle-version!) #f)
            (if (and (member? preparation '("未开始" "筹备中" "已备好" "放下"))
                     (member? version '("未定" "保留旧歌" "接受改编"))) #t (error "剧院：非法演出状态"))))
        (else (error "第二幕剧院：未知消息"))))))
