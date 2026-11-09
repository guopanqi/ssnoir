;; 居民区里的相识与接纳；不新增地图地点，也不接管弗兰克的后续生活事件。
(define 居民区相识
  (let ()
    (define invited? #f)
    (define visited? #f)
    (define 邻里 (make-clock "熟悉老街邻里" 8 'gauge "在居民区搭手帮忙，渐渐混熟这里的街坊。" :beneficial-direction 'increase))
    (define (invite!)
      (if invited? #f
        (begin (set! invited? #t)
          (if (大船靠岸 'completed?)
            (play-dialogue!
    (line "世界" "天快亮了，岸边的工人换了一班。艾迪揉着酸痛的肩膀，兜里揣着刚发的工钱，冲尼尔扬了扬下巴。")
    (line "艾迪" "这钱挣得骨头都快散架了。别回你那冷冰冰的旅馆了，我住老街居民区那边，弗兰克大叔这会儿肯定熬上热汤了。跟我过去坐坐，认认门，以后下工也有个歇脚的地方。")
    (line "尼尔" "好，听着就暖和。我歇口气过去找你。"))
            (play-dialogue!
    (line "世界" "码头边风很大，艾迪正把缆绳往桩子上绕，看见尼尔路过，停下手招呼了一声。")
    (line "艾迪" "这两天忙什么呢？大船那活儿没见你来，我还以为你发财去了。")
    (line "尼尔" "手头有别的事绊住了。")
    (line "艾迪" "没事，钱哪有挣完的时候。对了，我平时下工都在老街居民区那边混，弗兰克大叔那儿有热汤，晚上还有人打牌。你哪天得空直接过来，报我名字就行，别总一个人闷在旅馆里。")
    (line "尼尔" "行，这几天就过去看看。"))))))
    (define (meet!)
      (set! invited? #t)
      (set! visited? #t)
      (if (frank 'met?) #f
        (begin (frank 'meet!)
          (play-dialogue!
    (line "世界" "老街居民区的院子里拉着晾衣绳，煤炉上炖着大锅，热气往外冒。几个工友围在木桌边打牌，没人特意抬头看门口。艾迪从台阶上站起来，把尼尔领到炉子边一个头发花白的老工人面前。")
    (line "艾迪" "弗兰克大叔，这就是我跟你提过的尼尔，在二号桥跟我一块儿干活的。")
    (line "世界" "弗兰克正拿长柄勺撇锅里的浮沫，闻言抬起眼皮看了尼尔一眼，点点头。")
    (line "弗兰克" "艾迪的朋友啊。自己找空板凳坐，这儿没那么多讲究。")
    (line "世界" "旁边修鞋的工友头也不抬地插了一句：“新来的？搭把手，帮我把脚边那箱钉子递过来。”尼尔弯腰把木箱搬过去，顺势在旁边的矮凳上坐下。")
    (line "弗兰克" "手脚倒勤快。院子里平时有些劈柴、修棚子的零碎活儿，你要是闲着没事干，搭把手，少不了你的零花钱。")
    (line "尼尔" "好，有活您叫我。")))))
    (define (help! progress)
      (邻里 'advance! progress)
      (add-item! "金钱" 6)
      (if (邻里 'full?) (院里的炉火 'unlock!) #f)
      (if (邻里 'full?)
        (spotlight-page! ""
          (list
            (list "收好工具，牌桌边的工友挪出半截长凳，招呼你过来。" "EventSheets/城市生活/聚餐")
            (list "食堂的汤还热着。洗过手，盛一碗，再看看这回谁来发牌。" ""))
          (lambda () (result-supplement! "开放：热汤与牌局"))) #f))
    (define (meal!)
      (restore-actor-composure! 'player 5)
      (play-bubble! (line 旧街食堂老板 "趁热。喝完把碗搁木盆里。" :at "码头居民区-东侧门廊")))
    (define (cards! result)
      (cond ((eq? result '赢) (add-item! "金钱" 12))
        ((eq? result '平) (add-item! "金钱" 5))
        (else (spend-composure! 1))))
    (define (life-nodes)
      (list
        (node "旧街热饭" :name "喝碗热汤" :anchor "码头居民区-东侧门廊"
          :subtitle "23美元，喝碗热汤，恢复5心情。"
          :requires (list (req-item "金钱" 23)) :progress-target (list 'composure "player")
          :resolve (instant (outcome meal!)))
        (node "旧街牌桌" :name "街角牌桌" :anchor "码头居民区-东侧门廊"
          :subtitle "押5美元；赢发12，平退5，输损失1心情。" :risk '冒险
          :risk-target (list 'composure "player") :progress-target (list 'item "金钱")
          :requires (list (req-die) (req-item "金钱" 5))
          :resolve (roll 'sharpness
            (outcome (lambda () (cards! '输)))
            (outcome (lambda () (cards! '平)))
            (outcome (lambda () (cards! '赢)))))))
    (lambda (msg . args)
      (cond
        ((eq? msg 'known?) invited?)
        ((eq? msg 'familiar?) (邻里 'full?))
        ((eq? msg 'invite!) (invite!))
        ((eq? msg 'on-met!) (set! invited? #t) (set! visited? #t))
        ((eq? msg 'nodes-at)
          (if (or (not (equal? (car args) "码头居民区")) (not visited?) (第二章 'started?)) '()
            (if (邻里 'full?) (life-nodes)
            (list (工作 "居民区帮忙杂活" '稳妥 'violence
              (outcome (lambda () (help! 2)))
              (outcome (lambda () (help! 1)))
              (outcome (lambda () (help! 0)))
              "替街坊搬煤修补，每班6美元，慢慢混熟。"
              :anchor "码头居民区-东侧门廊" :clocks (list (邻里 'render-data))
              :progress-target (list 'clock "熟悉老街邻里"))))))
        ((eq? msg 'arrivals-at)
          (if (and (equal? (car args) "码头居民区") invited? (not visited?))
            (list (arrival "艾迪介绍老街的朋友" meet!)) '()))
        ((eq? msg 'dossier)
          (if (and invited? (not (第二章 'started?)))
            (list (dossier "老街的朋友" :kind '人物 :primary #f :where "码头居民区"
              :clocks (if (邻里 'full?) '() (list (邻里 'render-data)))
              :status (if (邻里 'full?) '了结 '进行中)
              :now (if visited? "替街坊干些杂活，挣点零钱。弗兰克和邻居们渐渐认得你了。" "艾迪邀你去老街居民区。工棚后面的食堂，是工友下工后常聚的地方。"))) '()))
        ((eq? msg 'save) (list (list "invited" invited?) (list "visited" visited?) (list "familiarity" (邻里 'save))))
        ((eq? msg 'load!) (let ((data (car args)))
          (set! invited? (assoc-get data "invited" #f)) (set! visited? (assoc-get data "visited" #f))
          (邻里 'load! (assoc-get data "familiarity" 0))
          (if (and (member? invited? '(#t #f)) (member? visited? '(#t #f)) (or (not visited?) invited?)) #t
            (error "居民区相识：非法相识状态"))))
        (else (error "居民区相识：未知消息"))))))
