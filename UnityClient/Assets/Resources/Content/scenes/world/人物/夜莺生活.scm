;; 夜莺的早期相处与持续存在；章节只负责公共演出日程。
(define 夜莺生活
  (let ()
    (define 相识 (make-clock "酒馆里的消息" 2 'gauge "点杯酒，在吧台听取街面上的消息。" :beneficial-direction 'increase))
    (define 日常开始日 0)
    (define (daily-open?) (> 日常开始日 0))
    (define (begin-daily!)
      (if (daily-open?) #f
        (begin (set! 日常开始日 world-day) (set! singer-stage 1) (set! singer-day world-day)
          (第一幕 'schedule-farewell! (+ world-day 3)))))
    (define singer-stage 0)
    (define singer-day 0)
    (define listened-day 0)
    (define listen-count 0)
    (define singer-mistake-heard? #f)
    (define singer-practice-seen? #f)
    (define singer-seat-seen? #f)
    (define singer-care-seen? #f)
    ;; 返回本次是否已有夜莺互动；正式事件和听歌气泡不叠加、不补播。
    (define (on-tavern-work! activity)
      (if (and (daily-open?) (第一幕 'opened?) (第一幕 'work-open?) (= (第一幕 'act) 1)
               (not (第一幕 'farewell-pending?)) (not (= singer-day world-day)))
        (let ((played?
          (cond
            ((and (equal? activity "看场") (not singer-practice-seen?))
             (set! singer-practice-seen? #t) (第一幕-夜莺台边) #t)
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
            (set! singer-day world-day) #f)
          played?)
        #f))

    (define (listen!)
      (if (and (daily-open?) (= (第一幕 'act) 1) (第一幕 'work-open?)
               (not (= listened-day world-day))) #t
          (error "听夜莺唱歌：不在驻唱时间或今天已经听过"))
      (set! listened-day world-day)
      (set! listen-count (+ listen-count 1))
      (restore-actor-composure! 'player 1)
      #t)
    (define (listen-reaction!)
      (if (on-tavern-work! "听歌") #t
        (if (= singer-day world-day) #f
          (let ((talk?
            (cond
              ;; 面试在三天后发生，留座不能等待第四天才第一次可见。
              ((and (>= listen-count 2) (not singer-seat-seen?))
               (set! singer-seat-seen? #t)
               (第一幕-听歌熟客 singer-mistake-heard?) #t)
              ((= listen-count 1) (第一幕-听歌初次) #t)
              ((= listen-count 3) (第一幕-听歌回扣 singer-mistake-heard?) #t)
              (else #f))))
            (if talk?
              (set! singer-day world-day) #f)
            talk?))))

    (define 伤口照顾
      (make-triggered-event "夜莺睡前照顾" "房间睡前" ""
        (lambda (ctx) (and (第三幕 'investigating?) (> (injury-severity) 0)))
        (lambda (ctx)
          (let ((first? (= (assoc-get (伤口照顾 'save) "count" 0) 0)))
            (spotlight-page! ""
              (list
                (list "今晚，夜莺和你一起吃晚饭。" "EventSheets/夜莺包扎/晚饭")
                (list "她放下叉子，注意到你的袖口还在渗血。" "EventSheets/夜莺包扎/伤口")
                (list "她打开随身的小包，取出纱布，替你重新包好了伤口。" "EventSheets/夜莺包扎/包扎"))
              (lambda () (heal-injury! 1)))
            (if first?
              (play-dialogue!
                (line "夜莺" "下次早点告诉我。别等到吃饭的时候。")
                (line "尼尔" "好。现在可以吃了吗？")
                (line "夜莺" "吃吧，还没凉透。")) #f))
          #t)
        4 2 35
        (list (lambda () (and (第三幕 'started?) (not (第三幕 'investigating?)))) '() #f)))
    (define 睡前宵夜
      (make-triggered-event "夜莺睡前宵夜" "房间睡前" ""
        (lambda (ctx) (and (第三幕 'investigating?) (not (第二幕剧院 'supper-seen?))))
        (lambda (ctx) (第二幕剧院 'supper!) #t) 0 1 100
        (list (lambda () (and (第三幕 'started?) (not (第三幕 'investigating?)))) '() #f)))
    (define 相处互动
      (make-triggered-event "夜莺酒馆相处" "行动结算" ""
        (lambda (ctx) (member? (assoc-get ctx "activity" "") '("酒馆看场" "酒馆喝酒" "酒馆买烟" "听夜莺唱歌")))
        (lambda (ctx)
          (let ((activity (assoc-get ctx "activity" "")))
            (if (equal? activity "听夜莺唱歌") (listen-reaction!)
              (on-tavern-work! (cond ((equal? activity "酒馆看场") "看场")
                ((equal? activity "酒馆喝酒") "喝酒") (else "买烟"))))))
        0 0 100))
    (define (on-activity! activity)
      (if (and (equal? activity "喝酒") (= (第一幕 'act) 1)) (begin (相识 'advance! 1) (if (相识 'full?) (酒馆起步 'on-news!) #f)) #f)
      #f)
    ;; 位置只由工作与故事阶段决定，呈现和听歌使用同一个判断。
    (define (where-now)
      (if (> (第一幕 'act) 1) "剧院" "老街酒馆"))
    (define (theatre-note)
      (cond ((第二章 'started?) "演出带来了新的邀约，后台不再只有排练的事。")
        ((第三幕 'started?) "她在后台工作。黑衣人的事已经交给你调查。")
        (else "彩排中。她在侧幕旁等候，听见琴师起调就走回台上。")))
    (define (presence-at location)
      (cond
        ((and (equal? location "老街酒馆") (equal? (where-now) "老街酒馆") (第一幕 'work-open?)
              (or (daily-open?) (酒馆起步 'met?)))
          (list (at-anchor "夜莺@酒馆"
            (if (daily-open?)
              (note-node "夜莺驻唱" "夜莺"
                (if (第一幕 'farewell-pending?)
                  "她还在酒馆，正和琴师核对最后一场的曲目。前排给你留了位置。"
                  "她在木台上试音，唱累了便退到吧台侧面喝水润嗓。"))
              (note-node "尚未相识的歌女" "夜莺" "夜莺坐在吧台侧面润嗓子，看见你时微微点了点头。")))))
        ((and (equal? location "老街酒馆") (daily-open?) (equal? (where-now) "剧院"))
          (list (at-anchor "夜莺@酒馆" (note-node "夜莺·去向" "夜莺" "最后一场已经唱完，夜莺转去大剧院排练了。吧台侧面的高脚凳空着。"))))
        ((and (equal? location "剧院") (equal? (where-now) "剧院"))
          (list (at-anchor "夜莺@剧院" (note-node "夜莺排练" "夜莺" (theatre-note)))))
        (else '())))
    (define (nodes-at location)
      (append
        (if (and (equal? location "老街酒馆") (= (第一幕 'act) 1) (第一幕 'work-open?) (not (相识 'full?)) (not (daily-open?)))
          (list (at-anchor "老街酒馆-购买" (clock-node "夜莺相识目标" (相识 'render-data)))) '())
        (presence-at location)
        (if (and (equal? location (where-now)) (equal? location "老街酒馆") (daily-open?) (not (第一幕 'farewell-pending?)))
          (list (node "听夜莺唱歌" :interaction-activity "听夜莺唱歌" :anchor "夜莺@酒馆"
            :subtitle (if (= listened-day world-day)
              "今天已经听过；明天还可以再来"
              "听她唱歌，缓口气；一枚骰子恢复1心情。")
            :disabled (= listened-day world-day)
            :use-limit 'daily-once :requires (list (req-die))
            :resolve (instant (outcome listen!)))) '())))
    (lambda (msg . args)
      (cond
        ((eq? msg 'interactions) (list 相处互动 伤口照顾 睡前宵夜))
        ((eq? msg 'on-tavern-work!) (on-activity! (car args)))
        ((eq? msg 'request-ready?) (酒馆起步 'ready?))
        ((eq? msg 'daily-open?) (daily-open?))
        ((eq? msg 'begin-daily!) (begin-daily!))
        ((eq? msg 'stage) singer-stage)
        ((eq? msg 'listened-day) listened-day)
        ((eq? msg 'meet!) (begin-daily!))
        ((eq? msg 'nodes-at) (nodes-at (car args)))
        ((eq? msg 'dossier)
          (if (or (not (第一幕 'work-open?)) (> (第一幕 'act) 1)) '()
            (list (dossier "酒馆里的夜莺" :kind '人物 :where "老街酒馆"
              :now (cond ((第一幕 'farewell-pending?) "她通过了面试，今晚在酒馆唱最后一场。已答应到场，休息前去赴约。")
                ((daily-open?) "送她回家后，听歌与日常相处已经开放。她也在准备剧院面试，过几天会有消息。")
                ((and (first-act-walk 'met?) (not (first-act-walk 'walked?))) "已答应送夜莺回家。去酒馆履行约定，休息前必须处理。")
                ((酒馆起步 'met?) "你们已认识。她仍在酒馆驻唱，过几天再来留意她的近况。")
                (else "她在酒馆驻唱。先喝酒打听消息；替酒保处理醉客的争吵后，接下看场工作，慢慢认识她。"))
              :clocks (cond ((not (相识 'full?)) (list (相识 'render-data)))
                ((and (酒馆起步 'settled?) (not (酒馆起步 'met?))) (list (酒馆起步 'work-clock)))
                (else '()))))))
        ((eq? msg 'save) (list (list "interactions" (interaction-save (list 相处互动 伤口照顾 睡前宵夜))) (list "daily-start" 日常开始日) (list "social" (相识 'save)) (list "singer" singer-stage) (list "singer-day" singer-day) (list "listened-day" listened-day) (list "listen-count" listen-count) (list "singer-seat-seen" singer-seat-seen?) (list "singer-practice-seen" singer-practice-seen?) (list "singer-mistake-heard" singer-mistake-heard?) (list "singer-care-seen" singer-care-seen?)))
        ((eq? msg 'load!) (let ((data (car args)))
          (interaction-load! (list 相处互动 伤口照顾 睡前宵夜) (assoc-get data "interactions" '()))
          (相识 'load! (assoc-get data "social" 0))
          (set! singer-stage (assoc-get data "singer" 0))
          (set! 日常开始日 (assoc-get data "daily-start" (if (> singer-stage 0) world-day 0)))
          (set! singer-day (assoc-get data "singer-day" 0))
          (set! listened-day (assoc-get data "listened-day" 0))
          (set! listen-count (assoc-get data "listen-count" 0))
          (set! singer-seat-seen? (assoc-get data "singer-seat-seen" #f))
          (set! singer-practice-seen? (assoc-get data "singer-practice-seen" #f))
          (set! singer-mistake-heard? (assoc-get data "singer-mistake-heard" #f))
          (set! singer-care-seen? (assoc-get data "singer-care-seen" #f))
          (if (and (>= 日常开始日 0) (member? singer-stage '(0 1 2 3)) (>= listen-count 0)
                   (member? singer-practice-seen? '(#t #f)) (member? singer-seat-seen? '(#t #f))) #t (error "夜莺生活：非法阶段"))))
        (else (error "夜莺生活：未知消息"))))))
