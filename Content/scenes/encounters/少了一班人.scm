;; 坍塌结算后的一次集中人物交锋。三个选择都当场结算，不派生培训或调查项目。

(define finished? #f)

(define (finish! result)
  (if finished? (error "少了一班人：交锋已经结算") #t)
  (set! finished? #t)
  (end-encounter result))

(define (on-encounter-enter)
  (play-dialogue!
    (line "工人" "事故一停工，公司就把我们那一班从表上抹了。你那台机器却还在里头。")
    (line "林" "新港本来就在减临时班。我可以争取操作和维护的名额。")
    (line "工人" "一班人，你给几张凳子？")
    (line "世界" "围栏外还在来人。公司警卫的车已经停在路口。")))

(define (node-escort)
  (instant-action "护送林离开"
    (lambda ()
      (play-dialogue!
        (line "尼尔" "后面有一道送油管的小门。你跟我走。")
        (line "林" "我走了，他们也不会回到班表上。")
        (line "尼尔" "今天他们要的不是班表。先出去。")
        (line "世界" "你从货栈后方把林带出去。门前的人还在，机器也还在。"))
      (finish! 'escort))))

(define (node-frank)
  (instant-action "请弗兰克调停"
    (lambda ()
      (play-dialogue!
        (line "世界" "弗兰克从码头另一头赶来，站到围栏和人群中间。吵声先低了一层。")
        (line "林" "我能留下两个操作和维护的名额。先让旧班上的人来学。")
        (line "工人" "两个人进工棚，剩下的呢。")
        (line "林" "我只能先拿到两个。但这两个不是空话。")
        (line "世界" "弗兰克把名字和条件当场写下。人群散了，一整班人仍然没有工作。"))
      (finish! 'frank))))

(define (node-guards)
  (instant-action "叫警卫清场"
    (lambda ()
      (play-dialogue!
        (line "尼尔" "路口那辆车可以进来了。")
        (line "林" "等等。他们只是丢了工作。")
        (line "尼尔" "围栏要是倒了，下一步就不只是说话。")
        (line "世界" "警卫用棍子和肩膀把人群推到街对面。机器没有损伤，地上留了几顶帽子。"))
      (finish! 'guards))))

(define (get-render-data)
  (container "少了一班人"
    (list
      (observe-action "林" "他站在工棚门内，手里还拿着一张机器读数。")
      (observe-action "工人" "被停掉的临时班堵在围栏外，要林出来解释。")
      (node-escort)
      (node-frank)
      (node-guards))))
