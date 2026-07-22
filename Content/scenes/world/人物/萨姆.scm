;; 萨姆（Samuel Rourke）——因拒绝压案而离职的前警探、现编外侦探。

(define sam
  (let ()
    (define stage 1) ; 1初谈(登门当天即达) / 2讲过亨利
    (define evidence "无") ; 无 / 老板 / 完整
    (define along? #f)
    (define met? #f)
    (define sighting-tavern? #f)
    (define sighting-dock? #f)
    (define favor-asked? #f)
    (define favor-done? #f)
    (define sighting-police? #f)
    (define identity "离开警队的邻城刑警，现为编外侦探")

    (define (bool-count flags)
      (if (null? flags)
          0
          (+ (if (car flags) 1 0) (bool-count (cdr flags)))))

    (define (sighting-count)
      (bool-count (list sighting-tavern? sighting-dock? sighting-police?)))

    (define (sighting-recap)
      (cond
        ((and sighting-tavern? sighting-dock?) "酒馆里请酒的是我。码头问潮水的也是我。我查的,一直是同一件事。")
        (sighting-tavern? "酒馆里请酒的是我。我查的,一直是同一件事。")
        (sighting-dock? "码头问潮水的是我。我查的,一直是同一件事。")
        (sighting-police? "警局柜台前争案卷的是我。我查的,一直是同一件事。")
        (else "这件事我查了七年,没查到尽头。")))

    (define (sighting-name anonymous-name)
      (if met? "萨姆" anonymous-name))

    (define (remembrance-ready?)
      (and (= stage 1) (>= (nightingale 'truth-progress) 2)))

    ;; 三层已揭后他已经登门自报家门(见 夜莺.scm 的「萨姆登门」必看场景)，
    ;; 这里只负责酒馆中的追加信息。
    (define (debut!)
      (if (= stage 1)
          (spotlight! "萨姆" "他手里有三条能追下去的线：码头老人、邻城案卷、货栈船期。")
          #f))

    ;; ── 登门之前的三次目击 ────────────────────────
    (define (node-tavern-sighting)
      (instant-action (sighting-name "角落里请酒的男人")
        (lambda ()
          (set! met? #t)
          (set! sighting-tavern? #t)
          (play-dialogue!
            (line "世界" "角落那桌,一个没见过的男人在给几个老码头工添酒。他自己那杯没动。")
            (line "世界" "他问的都是七年前的旧事:那年冬天谁在栈桥上值夜,哪班船停靠过。")
            (line "萨姆" "萨姆。酒我请，旧事你们要是想起来，就说。")
            (line "世界" "老工人们摇头。他也不追问,把酒钱压在杯底,走了。")))))

    (define (node-dock-sighting)
      (node (sighting-name "问潮水的男人")
        :subtitle (if met? "外地人；在查七年前的潮汐" "外地人；在问七年前的潮汐")
        :children
        (list
          (instant-action "替他带句话"
            (lambda ()
              (set! met? #t)
              (set! sighting-dock? #t)
              (set! favor-asked? #t)
              (set! favor-done? #t)
              (play-dialogue!
                (line "世界" "还是那个男人。他在问七年前十一月的潮汐,和一个具体的日子。")
                (line "萨姆" "劳驾。帮我问一句:登记房的老钟,那年是不是慢十分钟。就这一句。")
                (line "主角" "就一句?")
                (line "萨姆" "就一句。答案是或不是,都值一杯酒。"))
              (spotlight! "带一句话" "你替他问了。登记房的人说:是,慢十分钟,后来才校的。你把答案带给他,他点了点头,像是补上了什么。")))
          (instant-action "不掺和"
            (lambda ()
              (set! met? #t)
              (set! sighting-dock? #t)
              (set! favor-asked? #t)
              (play-dialogue!
                (line "世界" "还是那个男人。他在问七年前十一月的潮汐,和一个具体的日子。")
                (line "世界" "他朝你看了一眼,像是想开口,又算了。")))))))

    (define (node-police-sighting)
      (node (sighting-name "柜台前的争执")
        :subtitle (if met? "外地人；在争一份邻城旧案卷" "外地人；在警局柜台前争执")
        :resolve (instant
          (lambda ()
            (set! met? #t)
            (set! sighting-police? #t)
            (if (relation-at-least? "官僚" '相识)
                (play-dialogue!
                  (line "世界" "柜台前,那个男人在跟值班警员低声争一份邻城的旧案卷,被挡了回来。")
                  (line "世界" "他离开时,你听清了他念的名字:亨利·奎因。")
                  (line "世界" "值班的朝你摊手:'编外的。没有手续,谁也调不动邻城的卷。'"))
                (play-dialogue!
                  (line "世界" "柜台前,那个男人在跟值班警员低声争一份什么卷宗,被挡了回来。")
                  (line "萨姆" "萨姆。卷宗的名字不重要，重要的是有人不想让它翻出来。")
                  (line "世界" "他把帽檐往下按了按,走进雨里。")))))))

    (define (sighting-nodes-at location)
      (cond
        ((and (equal? location "酒馆") (= (nightingale 'story-stage) 1)
              (not (nightingale 'sam-intro?)) (not sighting-tavern?))
         (list (node-tavern-sighting)))
        ((and (equal? location "码头") (>= world-day 5) (<= world-day 10)
              (>= (nightingale 'story-stage) 2) (not (nightingale 'sam-intro?)) (not sighting-dock?))
         (list (node-dock-sighting)))
        ((and (equal? location "警局") (>= world-day 8)
              (>= (nightingale 'story-stage) 2) (not (nightingale 'sam-intro?)) (not sighting-police?))
         (list (node-police-sighting)))
        (else '())))

    ;; ── 正式登场后 ────────────────────────────────
    (define (node-chat)
      (observe-action "聊聊旧案"
        (cond
          ((= stage 1) "萨姆反复比对几张褪色的抄件。\"码头、案卷、船期，总有一处会把谎话撕开。\"")
          ((= stage 2) "萨姆把亨利的名字压在杯垫下面。\"真相不是替谁开脱；是别让活着的人只剩老板写的一种说法。\"")
          (else "萨姆收起案卷，没有再多说。"))))

    (define (sam-subtitle)
      (cond
        ((= stage 1) "编外侦探；等你带回能对上的线索")
        ((= stage 2) "编外侦探；等你决定如何交代真相")
        (else identity)))

    (define (node-along)
      (instant-action "跟他一起查"
        (lambda ()
          (set! along? #t)
          (play-dialogue!
            (line "萨姆" "行。丑话在前:我要的是那晚的全部,不是对她有利的那一半。")
            (line "主角" "查到哪算哪。")
            (line "萨姆" "三条线。码头的老人认得我这张脸就够了;案卷和货栈,得靠你的门路。"))
          (spotlight! "两个人查" "从今晚起,三条查访线上都有他。他不占你的骰子,他带来的是他自己。"))))

    (define (node-sam)
      (node "萨姆"
        :subtitle (sam-subtitle)
        :children (append
                    (list (node-chat))
                    (if (and (= stage 1) (not along?) (nightingale 'truth-pending?))
                        (list (node-along))
                        '())
                    (if (and (remembrance-ready?) (not (nightingale 'route-settled?)))
                        (list (node-remembrance))
                        '())
                    (if (and (= stage 2) (nightingale 'truth-known?) (equal? evidence "无")
                             (not (nightingale 'route-settled?)))
                        (list (node-evidence))
                        '()))))

    (define (present?)
      (and (nightingale 'stage3-open?) (nightingale 'sam-intro?) (not (nightingale 'route-settled?))))

    (define (node-remembrance)
      (node "听萨姆说起亨利"
        :subtitle identity
        :resolve (instant (lambda ()
          (set! stage 2)
          (play-dialogue!
            (line "萨姆" "亨利·奎因以前和我穿过同样的制服。后来他替歌厅老板做脏活。")
            (line "萨姆" "他不是好人，但他每个月给病中的妹妹寄钱。死人也不该只剩下一句‘活该’。")
            (line "萨姆" "那一晚的案子被压下去了。不是因为没人知道，是因为知道的人都有更安稳的写法。"))
          (spotlight! "亨利·奎因" "死者有了名字，也有了一个仍在等答案的亲属。")))))

    (define (node-evidence)
      (node "把证据交给萨姆"
        :subtitle identity
        :children (list
          (instant-action "只交出老板那一半"
            (lambda ()
              (set! evidence "老板")
              (play-dialogue!
                (line "萨姆" "我会追雇凶、拐卖和压案。夜莺在栈桥上的那一半，暂时留在纸外。"))
              (spotlight! "半份证据" "萨姆开始追查老板；正式立案仍需要阿瑟把材料送进程序。")))
          (instant-action "交出完整真相"
            (lambda ()
              (set! evidence "完整")
              (nightingale 'set-surrendered!)
              (play-dialogue!
                (line "萨姆" "我不会把她当货带回去。但她得作为当事人，把那晚的话说完。")
                (line "夜莺" "好。至少这一次，不让老板替所有人写结尾。"))
              (spotlight! "完整案卷" "夜莺将在第17天随案回去作证。"))))))

    (define (nodes-at location)
      (append
        (sighting-nodes-at location)
        (if (and (equal? location "酒馆") (present?)) (list (node-sam)) '())))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'stage) stage)
          ((equal? msg 'evidence) evidence)
          ((equal? msg 'along?) along?)
          ((equal? msg 'met?) met?)
          ((equal? msg 'sighting-count) (sighting-count))
          ((equal? msg 'sighting-recap) (sighting-recap))
          ((equal? msg 'favor-done?) favor-done?)
          ((equal? msg 'debut!) (debut!))
          ((equal? msg 'save)
           (list (list "stage" stage) (list "evidence" evidence) (list "along?" along?) (list "met?" met?)
                 (list "sighting-tavern?" sighting-tavern?) (list "sighting-dock?" sighting-dock?)
                 (list "favor-asked?" favor-asked?) (list "favor-done?" favor-done?)
                 (list "sighting-police?" sighting-police?)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! stage (assoc-get data "stage" 1))
             (set! evidence (assoc-get data "evidence" "无"))
             (set! along? (assoc-get data "along?" #f))
             (set! met? (assoc-get data "met?" #f))
             (set! sighting-tavern? (assoc-get data "sighting-tavern?" #f))
             (set! sighting-dock? (assoc-get data "sighting-dock?" #f))
             (set! favor-asked? (assoc-get data "favor-asked?" #f))
             (set! favor-done? (assoc-get data "favor-done?" #f))
             (set! sighting-police? (assoc-get data "sighting-police?" #f))
             (if (or (equal? evidence "无") (equal? evidence "老板") (equal? evidence "完整"))
                 #t (error "萨姆存档错误：证据状态非法"))))
          (else #f))))))
