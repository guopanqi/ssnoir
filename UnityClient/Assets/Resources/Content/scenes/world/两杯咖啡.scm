;; 被露西写作片段引用的一段城市经历。这里只拥有老人故事的进展，次数由引擎的 daily-once 管理；
;; 不拥有露西的创作、寄稿或事项；四段先用 Dialogue，之后可原位替换表现。
(define 两杯咖啡-老人 (speaker "两杯咖啡的老人" :name "老人" :appearance '成年男性))
(define 两杯咖啡
  (let ()
    (define story-clock
      (make-clock "两杯咖啡" 4 'gauge
        (lambda (current max)
          (list-ref '("靠窗的老人正在等人点单。"
                      "他面前有两杯咖啡，对面的椅子空着。"
                      "他总想坐在靠窗的位子。"
                      "父子争吵以后，就没再坐下来喝过咖啡。"
                      "两个人喝完了咖啡，一起走了出去。") current))))
    (define (active?)
      (and (>= (第一幕 'act) 2) (not (第二章 'started?))
           (第三幕 'lucy-writing-known?)))
    (define (advance!)
      (if (and (active?) (not (story-clock 'full?)))
        #t (error "两杯咖啡：故事未开放、已经完成"))
      (story-clock 'advance! 1)
      (cond
        ((= (story-clock 'current) 1)
          (play-dialogue!
            (line "世界" "一个老人坐在靠窗的位置。他把帽子放在膝上，向尼尔抬了抬手。")
            (line 两杯咖啡-老人 "两杯咖啡。")
            (line "尼尔" "两杯？")
            (line 两杯咖啡-老人 "对。")
            (line "尼尔" "另一杯要带走吗？")
            (line 两杯咖啡-老人 "不，就在这里喝。")
            (line "世界" "老人喝完自己面前的咖啡。对面的杯子还满着，椅子上没有人。")))
        ((= (story-clock 'current) 2)
          (play-dialogue!
            (line "世界" "老人又来了。靠窗的桌子都坐了人，他攥着帽檐，在一张桌旁停下。")
            (line 两杯咖啡-老人 "对不起。能不能跟我换个位子？里面那张也挺宽敞。")
            (line "世界" "客人端起杯子让了座。老人连声道谢，坐到窗边，又点了两杯咖啡。")
            (line "尼尔" "还是都在这里喝？")
            (line 两杯咖啡-老人 "是。谢谢。")
            (line "世界" "他只动了自己那一杯，不时朝窗外望。另一杯始终摆在空椅子前面。")))
        ((= (story-clock 'current) 3)
          (play-dialogue!
            (line "尼尔" "您等的人，知道您在这儿吗？")
            (line 两杯咖啡-老人 "以前知道。那时候，我们常坐这张桌子。")
            (line "尼尔" "家里人？")
            (line 两杯咖啡-老人 "我儿子。我替他问了份工作。他没去。")
            (line "世界" "老人用拇指摩挲着杯把，过了一会儿才接着说。")
            (line 两杯咖啡-老人 "他说，我连他明天穿哪件衬衫都想管。我说了些不好听的话。他也是。")
            (line "尼尔" "后来呢？")
            (line 两杯咖啡-老人 "后来就没一起喝过咖啡了。")
            (line "世界" "老人转头望向街上。尼尔没有撤走对面的杯子。")))
        ((= (story-clock 'current) 4)
          (play-dialogue!
            (line "世界" "尼尔过来时，老人已经坐在窗边，面前摆着两杯咖啡。")
            (line "世界" "门响了一声。一个穿工装的男人走进来，在门边站了片刻，然后走到窗边。")
            (line "世界" "老人抬起头。男人拉开对面的椅子坐下，把那杯咖啡端到自己面前。")
            (line "世界" "两个人什么也没说，慢慢喝完了咖啡。")
            (line "世界" "老人戴好帽子。男人替他拉开门，两人一起走了出去。尼尔收起桌上的两只空杯。")))))
    (define (nodes-at loc)
      (if (and (equal? loc "街角咖啡馆") (active?) (not (story-clock 'full?)))
        (list
          (node "咖啡馆的老人" :name "一个老人" :anchor "咖啡馆帮工"
            :subtitle (list-ref '("一个靠窗的老人，正等着点咖啡。"
                                 "他又来了，对面还是那只空杯子。"
                                 "他一直望着窗外。问问他在等谁。"
                                 "窗边的老人又点了两杯咖啡。") (story-clock 'current))
            :use-limit 'daily-once
            :button-text "查看"
            :clocks (list (story-clock 'render-data))
            :requires (list (req-die))
            :progress-target (list 'clock "两杯咖啡")
            :resolve (instant (outcome advance!)))) '()))
    (lambda args
      (let ((msg (car args)))
        (cond
          ((eq? msg 'nodes-at) (nodes-at (cadr args)))
          ((eq? msg 'completed?) (story-clock 'full?))
          ((eq? msg 'progress) (story-clock 'current))
          ((eq? msg 'clock-data) (story-clock 'render-data))
          ((eq? msg 'save)
            (list (list "progress" (story-clock 'save))))
          ((eq? msg 'load!)
            (let ((data (cadr args)))
              (story-clock 'load! (assoc-get data "progress" 0))
              #t))
          (else (error "两杯咖啡：未知消息")))))))
