;; 阿瑟（Arthur Bell）——辖区警局负责登记与档案的职员。

(define arthur
  (let ()
    ;; 0不认识 / 1登记时认识 / 2替他处理过麻烦
    (define stage 0)
    (define pass-cooldown 0)
    (define pass-cooldown-max 3)
    (define problem-invited? #f)
    (define identity "辖区警局的登记与档案职员")

    (define (meet!)
      (if (= stage 0)
          (begin
            (set! stage 1)
            (notify! "登记职员阿瑟记下了你的名字。先在警局做些事，他才会把程序外的麻烦交给你。"))
          #f))

    (define (node-introduction)
      (node "见阿瑟"
        :subtitle identity
        :resolve (instant (lambda ()
          (if (not (= stage 0)) (error "见阿瑟：人物阶段错误") #t)
          (meet!)
          (play-dialogue!
            (line "世界" "阿瑟·贝尔坐在登记台后，桌上每一叠文件都用尺子压得笔直。")
            (line "阿瑟" "登记、调卷、移交，都归这张桌子。至于没有表格能管的事——出了后果才归警局。"))))))

    (define (on-lesson-result result)
      (if (equal? result 'success)
          (begin
            (set! stage 2)
            (change-faction-relation! "官僚" 2)
            (complete-section!)
            (notify! "阿瑟欠你一次程序内的方便。"))
          #f))

    (define (node-paperwork)
      (关系工作 "整理警局文书" "官僚" '低 'knowledge
        (outcome "补上缺页" "你找回一页险些被丢掉的记录。" (lambda () (add-item! "情报" 1)))
        (outcome "按序归档" "文件回到各自的抽屉，没人因此得到什么，也没人因此倒霉。" (lambda () #f))
        (outcome "退回重填" "一个日期写错，整叠表格都被退了回来。" (lambda () (spend-composure! 1)))))

    (define (node-problem)
      (node "替阿瑟处理一个程序管不了的人"
        :subtitle identity
        :tags (list "交锋")
        :resolve (instant (lambda () (start-encounter "教训" on-lesson-result)))))

    (define (police-hostile?) (equal? (relation-band "官僚") '敌视))

    (define (node-delay)
      (node "请阿瑟延期一天"
        :subtitle (string-append identity "；" (if (police-hostile?) "不会替一个被警局盯上的人改日期" "能让一份手续晚一天到桌上"))
        :disabled (police-hostile?)
        :requires (list (req-die))
        :resolve (instant
          (outcome "日期往后挪了一格" "阿瑟换了一张登记单。事情没有消失，只是晚一天发生。"
            (lambda () (delay-public-event-one-day!))))))

    (define (node-pass)
      (node "请阿瑟办理通行证"
        :subtitle (string-append identity "；" (cond
                    ((police-hostile?) "警局现在不会给你签任何东西")
                    ((> (item-count "办案通行证") 0) "你已经持有一张")
                    ((> pass-cooldown 0) (string-append "还要等 " (number->string pass-cooldown) " 天"))
                    (else "一张一次性通行证，能在交锋中制造程序干预")))
        :disabled (or (police-hostile?) (> (item-count "办案通行证") 0) (> pass-cooldown 0))
        :requires (list (req-die))
        :resolve (instant
          (outcome "通行证签下来了" "阿瑟在末页盖章，没有问你准备拿它做什么。"
            (lambda ()
              (add-item! "办案通行证" 1)
              (set! pass-cooldown (if (relation-at-least? "官僚" '信任) 1 pass-cooldown-max)))))))

    (define (nodes)
      (append
        (if (= stage 0) (list (node-introduction)) '())
        (list (node-paperwork))
        (if (>= stage 1)
            (list (node "阿瑟"
                    :subtitle identity
                    :resolve (observe (if (= stage 1)
                        "阿瑟·贝尔相信每件事都该有一张表格；没有表格的事，只会在出后果时归警局管。"
                        "阿瑟不关心你是不是正确。他只承认你替他解决过一个后果。"))))
            '())
        (if (and (= stage 1) problem-invited?) (list (node-problem)) '())
        (if (and (= stage 2) (public-event-can-delay?)) (list (node-delay)) '())
        (if (= stage 2) (list (node-pass)) '())))

    (define-turn-rule "阿瑟通行证冷却"
      (lambda () (> pass-cooldown 0))
      (lambda () (set! pass-cooldown (- pass-cooldown 1))))

    (define-turn-rule "阿瑟提出私下委托"
      (lambda ()
        (and (= stage 1) (not problem-invited?) (relation-at-least? "官僚" '相识)))
      (lambda ()
        (set! problem-invited? #t)
        (notify! "阿瑟已经看过你做事。他有一件程序管不了的麻烦，想在警局里和你谈。")))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'meet!) (meet!))
          ((equal? msg 'known?) (>= stage 1))
          ((equal? msg 'can-escalate?) (= stage 2))
          ((equal? msg 'nodes) (nodes))
          ((equal? msg 'save)
           (list (list "stage" stage)
                 (list "pass-cooldown" pass-cooldown)
                 (list "problem-invited?" problem-invited?)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! stage (assoc-get data "stage" 0))
             (set! pass-cooldown (assoc-get data "pass-cooldown" 0))
             (set! problem-invited? (assoc-get data "problem-invited?" #f))))
          (else #f))))))
