;; D10—12 夜宵是独立私人片段；D13 试演是夜莺自己的剧情演出，当晚直接追查黑大衣。
(define 第二幕剧院
  (let ()
    (define (boolean? value) (or (equal? value #t) (equal? value #f)))
    (define promised? #f)
    (define private-seen? #f)
    (define performance-seen? #f)
    (define aftermath-seen? #f)
    (define audience-result "未发生")
    (define audience-intel 0)
    (define (on-audience-result! result)
      (if (and (list? result) (= (length result) 2)
               (member? (car result) '(车辆线索 错失车辆 倒下))
               (number? (cadr result)) (>= (cadr result) 0)) #t
        (error "最后一排：非法Scenario结果"))
      (if (equal? audience-result "未发生") #t (error "最后一排：重复结算"))
      (set! audience-result (symbol->string (car result)))
      ;; 情报物品已由Scenario发放，此处只记叙事事实，不重复给奖励。
      (set! audience-intel (cadr result)))
    (define (active?) (and (= (第一幕 'act) 2) (not (第二章 'started?))))
    (define (promise-trial!)
      (set! promised? #t)
      (play-dialogue!
        (line "夜莺" (string-append (第二幕-日期 13) "下午，赞助人来听试唱。你也来，好不好？"))
        (line "尼尔" "我会来。在后台侧幕找你。")))
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
    (define (performance!)
      (if performance-seen? (error "赞助人试唱：不可重复观看") #t)
      (set! performance-seen? #t)
      (play-dialogue!
        (line "世界" "赞助人刚坐好，监督就举起了手。夜莺没有等尼尔为她说话，自己走上舞台。")
        (line "夜莺" "时间不多，我明白。但我想让你们真正听见我。")
        (line "世界" "她把中段大胆地收进最后几句里。赞助人原本在看表，终于抬头望向了舞台。")
        (line "世界" "一个本来只打算听几分钟的人，起身为她鼓掌。夜莺向台下鞠躬，笑得发亮。")
        (line "尼尔" "你让他们都听进去了。")
        (line "夜莺" "他们终于知道我是谁了。"))
      (play-dialogue!
        (line "赞助人" "她的旧港经历很好，很有故事。宣传稿得再写得动人一点。")
        (line "经理" "当然！一个从旧港走到新城的姑娘，整个城市都会爱她。")
        (line "世界" "夜莺听见了，但没马上纠正。她仍望着方才为她鼓掌的观众席。"))
      (start-scenario "最后一排的观众" on-audience-result!))
    (define (aftermath!)
      (set! aftermath-seen? #t)
      (play-dialogue!
        (line "夜莺" "那天唱得真痛快。他们还问我愿不愿意多唱几场。")
        (line "尼尔" "他们说你是从旧港的苦日子里逃出来的。")
        (line "夜莺" "他们把故事讲得比我还会唱。不过这次，我想先把歌唱好。")))
    (define (arrivals-at loc)
      (if (and (active?) (equal? loc "剧院") (第一幕 'rehearsal-seen?))
        (append
          (if (and (= world-day 13) (not performance-seen?)) (list (arrival "赞助人试唱" performance!)) '())
          (if (and (>= world-day 14) performance-seen? (not aftermath-seen?)) (list (arrival "试唱余音" aftermath!)) '())) '()))
    (define (nodes-at loc)
      (if (and (active?) (equal? loc "剧院"))
        (append
          (if (and (>= world-day 10) (<= world-day 12) (not private-seen?))
            (list (node "和夜莺吃夜宵"
              :clocks (list (第二幕-期限钟 "夜宵机会" 12 10))
              :resolve (instant (outcome private!)))) '())
          (if (and (= world-day 13) (not performance-seen?))
            (list (node "赴试唱之约" :anchor "夜莺@剧院" :button-text "赴约"
              :required-before-rest "先看赞助人试唱并追查黑影"
              :resolve (instant (outcome performance!)))) '())
          ;; 节目单是试演时长的可见世界事实。
          (if (>= world-day 15)
            (list (at-anchor "剧院-后台" (note-node "首演节目单" "首演节目单"
              "夜莺的试演时间被压缩，但她仍让赞助人记住了这首歌。"))) '())) '()))
    (lambda args
      (cond
        ((eq? (car args) 'promise-trial!) (promise-trial!))
        ((eq? (car args) 'trial-pending?) (and (= world-day 13) (not performance-seen?)))
        ((eq? (car args) 'nodes-at) (nodes-at (cadr args)))
        ((eq? (car args) 'arrivals-at) (arrivals-at (cadr args)))
        ((eq? (car args) 'dossier)
          (if (= (第一幕 'act) 2)
            (append (list (dossier "夜莺：大剧院" :kind '人物 :primary #t
              :status (if 公告-夜莺看过? '了结 '进行中)
              :now (cond
                ((>= world-day 15) "首演海报把她的名字与旧港改造连在一起。剧院后台已有印好的节目单。")
                ((and (= world-day 13) (not performance-seen?))
                  "赞助人今天来剧院听试唱。你答应了到场，去后台侧幕找她。")
                (performance-seen?
                  "她只获得简短演出时间，却仍唱得出色。试唱已经结束。")
                ((not (第一幕 'rehearsal-seen?))
                  (if (= world-day 7)
                    "明天她在剧院正式彩排。你答应了到场，来侧门找她。"
                    "今天她在剧院正式彩排。你答应了到场，先来剧院侧门。"))
                (else (string-append "彩排已经结束。" (第二幕-日期 13)
                    "下午，赞助人来剧院听她试唱。你答应了到场。")))
              :where "剧院"))
              (if (equal? audience-result "未发生") '()
                (list (dossier "最后一排的观众" :kind '打算 :primary #f :status '进行中
                  :now (string-append
                    (cond
                      ((equal? audience-result "车辆线索") "黑大衣熟悉剧院内部通道，从侧门坐灰色轿车离开。联城车行，车牌末三位472，身份尚未查明。")
                      ((equal? audience-result "错失车辆") "黑大衣熟悉剧院内部通道，从侧门坐车离开。没记下车牌，身份尚未查明。")
                      (else "试唱后追查后排的黑大衣，途中倒下，未能查清他的离场去向。"))
                    (if (> audience-intel 0) "另听说换景机的磨损部件报修后仍未更换。" ""))
                  :where "剧院")))) '()))
        ((eq? (car args) 'save) (list (list "version" 2) (list "promised" promised?)
          (list "private" private-seen?) (list "performance" performance-seen?) (list "aftermath" aftermath-seen?)
          (list "audience-result" audience-result) (list "audience-intel" audience-intel)
          ))
        ((eq? (car args) 'load!)
          (let ((d (cadr args)))
            (set! audience-result (assoc-get d "audience-result" "未发生"))
            (set! audience-intel (assoc-get d "audience-intel" 0))
            (if (and (member? audience-result '("未发生" "车辆线索" "错失车辆" "倒下"))
                     (number? audience-intel) (>= audience-intel 0)
                     (or (not (equal? audience-result "未发生")) (= audience-intel 0))) #t
              (error "最后一排：非法存档结果"))
            (set! promised? (assoc-get d "promised" (assoc-get d "trial-promised" #f)))
            (set! private-seen? (assoc-get d "private" (assoc-get d "care" #f)))
            (set! performance-seen? (assoc-get d "performance"
              (member? (assoc-get d "sponsor-result" "未参与") '("修好设备" "替代伴奏" "争取清唱" "她自己唱完"))))
            (set! aftermath-seen? (assoc-get d "aftermath" (assoc-get d "sponsor-aftermath" #f)))
            (if (and (boolean? promised?) (boolean? private-seen?)
                     (boolean? performance-seen?) (boolean? aftermath-seen?)) #t
              (error "剧院：非法演出状态"))))
        (else (error "第二幕剧院：未知消息"))))))
