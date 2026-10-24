(load-file "scripts/theatre/酒馆最后一夜.scm")
(load-file "scripts/theatre/第一幕对白.scm")
;; 第一章五幕中的前两幕：城市谋生、人物相识、酒馆告别与大剧院首次彩排。
;; 旧「三封信」主线已退役；世界日期推进，人物生活不等待玩家清空支线。
(define 第一幕
  (let ()
    (define opened? #f)
    (define tavern-seen? #f)
    (define lucy-stage 0)
    (define eddie-stage 0)
    (define singer-stage 0)
    (define lucy-day 0)
    (define eddie-day 0)
    (define singer-day 0)
    (define listened-day 0)
    (define listen-count 0)
    (define singer-mistake-heard? #f)
    (define singer-care-seen? #f)
    (define singer-contact-day 0)
    (define singer-contact-days 0)
    (define eddie-work-count 0)
    (define lucy-work-count 0)
    (define drink-day 0)
    (define drink-state "未邀请")
    (define drank? #f)
    (define farewell-seen? #f)
    (define farewell-done? #f)
    (define rehearsal-seen? #f)
    (define rehearsal-done? #f)
    (define notice-seen? #f)
    (define act 1)

    (define (start!)
      (if opened? (error "第一幕：重复开场") #t)
      (set! opened? #t)
      (第一幕-清晨))

    ;; 返回本次是否播放人物剧情，避免工作结果继续连播另一阶段的人物片段。
    (define (on-work! location grade)
      (cond
        ((equal? location "街角咖啡馆")
         (set! lucy-work-count (+ lucy-work-count 1))
         (if (not (= lucy-day world-day))
           (begin
             (set! lucy-day world-day)
             (cond
               ((= lucy-stage 0)
                (set! lucy-stage 1)
                (第一幕-露西初识) #t)
               ((and (= act 1) (= lucy-stage 1) (>= lucy-work-count 3))
                (set! lucy-stage 2) (第一幕-露西打字) #t)
               (else #f))) #f))
        ((equal? location "码头")
         (set! eddie-work-count (+ eddie-work-count 1))
         (if (not (= eddie-day world-day))
           (begin
             (set! eddie-day world-day)
             (cond
               ((= eddie-stage 0) (set! eddie-stage 1)
                (第一幕-艾迪教搬运) #t)
               ((and (= eddie-stage 1) (>= eddie-work-count 3))
                (set! eddie-stage 2) (第一幕-艾迪再见) #t)
               ((and (= eddie-stage 2) (>= eddie-work-count 3))
                (set! eddie-stage 3)
                (set! drink-day world-day)
                (set! drink-state "待答复")
                (第一幕-艾迪邀请) #t)
               (else #f))) #f))
        (else (error "第一幕：未知工作地点"))))

    ;; 接触按实际交谈发生的日期去重；听歌次数本身不增加熟悉程度。
    (define (record-singer-contact!)
      (if (not (= singer-contact-day world-day))
        (begin (set! singer-contact-day world-day)
          (set! singer-contact-days (+ singer-contact-days 1))) #f))

    ;; 返回本次是否已有夜莺互动；正式事件和听歌气泡不叠加、不补播。
    (define (on-tavern-work! activity)
      (if (and opened? tavern-seen? (= act 1) (<= world-day 6)
               (not (= singer-day world-day)))
        (let ((played?
          (cond
            ((= singer-stage 0)
             (set! singer-stage 1) (第一幕-夜莺台边) #t)
            ((and (= singer-stage 1) (member? activity '("听歌" "看场")))
             (set! singer-stage 2) (set! singer-mistake-heard? #t)
             (第一幕-夜莺收工 (equal? activity "看场")) #t)
            ((and (= singer-stage 2) (equal? activity "看场"))
             (set! singer-stage 3) (第一幕-夜莺解围) #t)
            ((and (>= singer-stage 2) (equal? activity "喝酒")
                  (not singer-care-seen?))
             (set! singer-care-seen? #t) (第一幕-夜莺关怀) #t)
            (else #f))))
          (if played?
            (begin (set! singer-day world-day) (record-singer-contact!)) #f)
          played?)
        #f))

    (define (listen!)
      (if (and (= act 1) (<= world-day 5) tavern-seen?
               (not (= listened-day world-day))) #t
          (error "听夜莺唱歌：不在驻唱时间或今天已经听过"))
      (set! listened-day world-day)
      (set! listen-count (+ listen-count 1))
      (restore-actor-composure! 'player 1)
      (if (on-tavern-work! "听歌") #f
        (if (= singer-day world-day) #f
          (let ((talk?
            (cond
              ((= listen-count 1) (第一幕-听歌初次) #t)
              ((= listen-count 2) (第一幕-听歌再来) #t)
              ((= listen-count 3) (第一幕-听歌回扣 singer-mistake-heard?) #t)
              ((and (= listen-count 4) (>= singer-contact-days 3))
               (第一幕-听歌熟客) #t)
              (else #f))))
            (if talk?
              (begin (set! singer-day world-day) (record-singer-contact!)) #f)))))

    (define (farewell!)
      (if (or (not (= world-day 6)) farewell-seen?)
        (error "第一幕：告别演出不在观看窗口") #t)
      (set! farewell-seen? #t)
      (set! farewell-done? #t)
      ;; 转幕由告别事件统一拥有；地图与事项都读取 act，不再等清晨另行推进。
      (set! act 2)
      (酒馆最后一夜-演出 (>= singer-stage 1) (> eddie-stage 0) (> lucy-stage 0))
      (if (= singer-stage 0)
        (play-dialogue! (line "尼尔" "我叫尼尔。你的歌，我想再听一次。")) #f)
      (play-dialogue!
        (line "夜莺" "后天第一次正式彩排。你来看看，好吗？")
        (line "尼尔" "大剧院？")
        (line "夜莺" "对。来侧门，跟他们说是找我的。")
        (line "尼尔" "后天我一定到。"))
      (if (= singer-stage 0) (set! singer-stage 1) #f)
      ;; 演出结束后先提交新地图与事项，再显示第二幕公告。
      (focus-node! "老街酒馆")
      (spotlight! "第二幕" "夜莺在酒馆的最后一场演出结束了。明天她去大剧院，后天正式彩排；码头工人常去的旧街，就在工棚后面。"))
    (define (rehearsal!)
      (if (or (not (= world-day 8)) rehearsal-seen?)
        (error "第二幕：第一次彩排不在观看窗口") #t)
      (set! rehearsal-seen? #t)
      (set! rehearsal-done? #t)
      (第一幕-彩排)
      (第二幕生活 'meet-manager!)
      (第二幕剧院 'promise-trial!))

    (define (arrivals-at location)
      (cond
        ((and opened? (equal? location "老街酒馆") (not tavern-seen?))
         (list (arrival "酒馆与夜莺" (lambda ()
           (set! tavern-seen? #t)
           (第一幕-酒馆)))))
        (else '())))

    ;; 日期是唯一事实来源；换日后节点消失，不另存一份倒计时状态。
    (define (drink-deadline-clock)
      (clock-priority
        (list 'clock "今晚有效" (if (= drink-day world-day) 1 0) 1 'countdown
          "换日后邀请与赴约机会失效。")
        'secondary))

    (define (nodes-at location)
      (append
        (if (and (equal? location "家") (not opened?))
          (list (node "旅馆清晨" :required-before-rest "先起床看看"
             :resolve (instant start!))) '())
        (if (and (equal? location "码头") (equal? drink-state "待答复")
                 (= drink-day world-day))
          (list (at-anchor "码头-货堆" (container-with-clocks "艾迪的邀请" (map (lambda (n) (at-anchor "码头-货堆" (append n (list :clocks (list (drink-deadline-clock)))))) (list
            (instant-action "去灌两杯" (lambda ()
              (set! drink-state "已答应")
              (play-dialogue! (line "艾迪" "痛快，吧台见。"))))
            (instant-action "今天算了" (lambda ()
              (set! drink-state "婉拒")
              (play-dialogue! (line "艾迪" "得，那我先去了。改天再喝。")))))) (list (drink-deadline-clock))))) '())
        (if (and (equal? location "老街酒馆") (= drink-day world-day)
                 (equal? drink-state "已答应"))
          (list (node "与艾迪喝酒" :anchor "老街酒馆"
            :button-text "赴约" :required-before-rest "先赴与艾迪喝酒的约"
            :resolve (instant (outcome (lambda ()
              (set! drink-state "赴约") (set! drank? #t)
              (第一幕-艾迪喝酒)
              (on-tavern-work! "喝酒")))) :clocks (list (drink-deadline-clock)))) '())

        (if (and (equal? location "老街酒馆") tavern-seen?
                 (= act 1) (<= world-day 6))
          (list (at-anchor "夜莺@酒馆"
             (note-node "夜莺驻唱" "夜莺"
               "今晚还在这里唱歌；再过几天，她就要去大剧院了。"))) '())
        (if (and (equal? location "老街酒馆") tavern-seen?
                 (= act 1) (<= world-day 5))
          (list (node "听夜莺唱歌" :anchor "夜莺@酒馆"
            :subtitle (if (= listened-day world-day)
              "今天已经听过；明天还可以再来"
              "花一枚行动骰，稳定恢复1点冷静；每天一次")
            :disabled (= listened-day world-day)
            :requires (list (req-die))
            :resolve (instant (outcome listen!)))) '())
        (if (and (equal? location "老街酒馆") (= world-day 6)
                 (not farewell-seen?))
          (list (node "夜莺最后一场演出" :anchor "夜莺@酒馆"
            :subtitle "今晚必须听完她的告别演出，然后才能结束这一日"
            :required-before-rest "先到酒馆参加夜莺的告别演出"
            :resolve (instant (outcome farewell!)))) '())
        (if (and (equal? location "剧院") (= world-day 8)
                 (not rehearsal-seen?))
          (list (node "观看正式彩排" :name "赴彩排之约" :anchor "夜莺@剧院"
            :button-text "赴约" :required-before-rest "先赴夜莺的彩排之约"
            :resolve (instant (outcome rehearsal!)))) '())
        (if (and (equal? location "剧院") (>= world-day 7))
          (list (at-anchor "夜莺@剧院"
            (note-node "夜莺排练" "夜莺"
              (cond ((and (= world-day 8) (not rehearsal-seen?)) "正式彩排就要开始。你答应了来听。")
                ((第二幕剧院 'trial-pending?) "试唱要开始了。她在后台等你赴约。")
                ((<= world-day 11) "她正和钢琴师练习试唱的曲子。")
                (else "她在后台准备首演。"))))) '())))

    (define-turn-start-rule "第一章前两幕日程"
      (lambda () #t)
      (lambda ()
        (if (and (> drink-day 0) (> world-day drink-day)
                 (member? drink-state '("待答复" "已答应")))
          (set! drink-state "错过") #f)
        (if (and (= world-day 5) (= act 1))
          (play-dialogue!
            (line "世界" "酒馆门外贴着告示：明晚，夜莺最后一次在这里演出；后天她去大剧院，隔天正式彩排。"))
          #f)
        (if (and (>= world-day 9) (not rehearsal-done?))
          (set! rehearsal-done? #t) #f)
        (if (and (>= world-day 13) (not notice-seen?))
          (begin
            (set! notice-seen? #t)
            (play-dialogue!
              (line "世界" "早报头版写着：旧港改造计划启动，大剧院首演助阵。")
              (line "世界" "酒馆角落里有人碰着酒杯。吧台边，一个装卸工把报纸翻回头版，又看了一遍。")))
          #f)))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((eq? msg 'nodes-at) (nodes-at (cadr args)))
          ((eq? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((eq? msg 'opened?) opened?)
          ((eq? msg 'work-open?) tavern-seen?)
          ((eq? msg 'theater-open?) (>= act 2))
          ((eq? msg 'rehearsal-seen?) rehearsal-seen?)
          ((eq? msg 'act) act)
          ((eq? msg 'eddie-met?) (> eddie-stage 0))
          ((eq? msg 'eddie-stage) eddie-stage)
          ((eq? msg 'lucy-met?) (> lucy-stage 0))
          ((eq? msg 'singer-stage) singer-stage)
          ((eq? msg 'listened-day) listened-day)
          ((eq? msg 'farewell-seen?) farewell-seen?)
          ((eq? msg 'on-work!) (on-work! (cadr args) (caddr args)))
          ((eq? msg 'on-tavern-work!)
            (on-tavern-work! (cadr args)))
          ((eq? msg 'debug-finish!)
           (set! opened? #t) (set! tavern-seen? #t)
           (set! farewell-done? #t) (set! rehearsal-done? #t)
           (set! act 2))
          ((eq? msg 'dossier)
           (list (dossier "活下去" :kind '主线 :primary #t
             :status (if (= act 1) '进行中 '了结)
             :now (cond
               ((not opened?) "起床，看看手里的钱。")
               ((not tavern-seen?) "去酒馆打听零工。")
               ((and (= act 2) (= world-day 7)) "夜莺去大剧院了。那里招临工，旧街也有新机会。")
               ((and (= act 2) (= world-day 8)) "今天夜莺在大剧院第一次正式彩排。可以去看看她。")
               ((and (= act 2) (= world-day 9)) "大船今天临时靠岸。码头高薪夜班正在招人。")
               ((and (= act 2) (= world-day 10)) "码头夜班的工资出了争议，夜莺的试演也临近了。")
               ((and (= act 2) (= world-day 11)) "今天剧院有赞助人试演。码头的争议仍在。")
               ((and (= act 2) (= world-day 12)) "这两天都很忙。也许可以去剧院找夜莺坐一会儿。")
               ((and (= act 2) (>= world-day 13)) "夜莺的首演海报与旧港改造出现在同一份公告上。")
               ((= act 2) "酒馆告别演出已经结束。尼尔仍得挣钱，也想找到自己的出路。")
               ((= world-day 6) "夜莺今晚在酒馆唱最后一场。")
               (else "在城市工作，与认识的人相处。"))
             :where (cond ((not opened?) "家")
               ((not tavern-seen?) "老街酒馆")
               ((and (= act 1) (= world-day 6)) "老街酒馆") (else "")))))
          ((eq? msg 'save)
           (list
             (list "opened" opened?) (list "tavern" tavern-seen?)
             (list "lucy" lucy-stage) (list "eddie" eddie-stage)
             (list "singer" singer-stage) (list "lucy-day" lucy-day)
             (list "eddie-day" eddie-day) (list "singer-day" singer-day)
             (list "listened-day" listened-day) (list "listen-count" listen-count)
             (list "singer-mistake-heard" singer-mistake-heard?)
             (list "singer-care-seen" singer-care-seen?)
             (list "singer-contact-day" singer-contact-day)
             (list "singer-contact-days" singer-contact-days)
             (list "eddie-count" eddie-work-count) (list "lucy-count" lucy-work-count)
             (list "drink-day" drink-day) (list "drink-state" drink-state)
             (list "drank" drank?)
             (list "farewell-seen" farewell-seen?) (list "farewell-done" farewell-done?)
             (list "rehearsal-seen" rehearsal-seen?) (list "rehearsal-done" rehearsal-done?)
             (list "notice" notice-seen?)
             (list "act" act)))
          ((eq? msg 'load!)
           (let ((data (cadr args)))
             (set! opened? (assoc-get data "opened" #f))
             (set! tavern-seen? (assoc-get data "tavern" #f))
             (set! lucy-stage (assoc-get data "lucy" 0))
             (set! eddie-stage (assoc-get data "eddie" 0))
             (set! singer-stage (assoc-get data "singer" 0))
             (set! lucy-day (assoc-get data "lucy-day" 0))
             (set! eddie-day (assoc-get data "eddie-day" 0))
             (set! singer-day (assoc-get data "singer-day" 0))
             (set! listened-day (assoc-get data "listened-day" 0))
             (set! listen-count (assoc-get data "listen-count" 0))
             (set! singer-mistake-heard? (assoc-get data "singer-mistake-heard" #f))
             (set! singer-care-seen? (assoc-get data "singer-care-seen" #f))
             (set! singer-contact-day (assoc-get data "singer-contact-day" 0))
             (set! singer-contact-days (assoc-get data "singer-contact-days" 0))
             (set! eddie-work-count (assoc-get data "eddie-count" 0))
             (set! lucy-work-count (assoc-get data "lucy-count" 0))
             (set! drink-day (assoc-get data "drink-day" 0))
             (set! drink-state (assoc-get data "drink-state" "未邀请"))
             (set! drank? (assoc-get data "drank" #f))
             (set! farewell-seen? (assoc-get data "farewell-seen" #f))
             (set! farewell-done? (assoc-get data "farewell-done" #f))
             (set! rehearsal-seen? (assoc-get data "rehearsal-seen" #f))
             (set! rehearsal-done? (assoc-get data "rehearsal-done" #f))
             (set! notice-seen? (assoc-get data "notice" #f))
             (set! act (assoc-get data "act" 1))
             ;; 第二幕已发生D6必经相识；旧档向前修复，不保留陌生人彩排剧本。
             (if (and (= act 2) (= singer-stage 0)) (set! singer-stage 1) #f)
             (if (and (member? act '(1 2))
                      (member? lucy-stage '(0 1 2))
                      (member? eddie-stage '(0 1 2 3))
                      (member? singer-stage '(0 1 2 3))
                      (member? listen-count '(0 1 2 3 4 5))
                      (member? singer-contact-days '(0 1 2 3 4 5 6))
                      (member? singer-contact-day '(0 1 2 3 4 5 6))
                      (or (not singer-mistake-heard?) (>= singer-stage 2))
                      (member? drink-state '("未邀请" "待答复" "已答应"
                                             "赴约" "婉拒" "错过")))
                #t (error "第一幕存档：阶段非法"))
             (if (and tavern-seen? (not opened?))
               (error "第一幕存档：开场顺序矛盾") #t)))
          (else (error "第一幕：未知消息")))))))
(set-global! '开场动作 "旅馆清晨")
