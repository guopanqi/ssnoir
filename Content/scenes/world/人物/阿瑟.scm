;; 阿瑟（Arthur Bell）——辖区警察局负责登记与档案的职员。

(define arthur
  (let ()
    ;; 0不认识 / 1登记时认识 / 2替他处理过麻烦
    (define stage 0)
    (define pass-cooldown 0)
    (define pass-cooldown-max 3)
    (define problem-invited? #f)
    (define identity "辖区警察局的登记与档案职员")

    (define (meet!)
      (if (= stage 0)
          (begin
            (set! stage 1)
            (notify! "登记职员阿瑟收下了威胁信。能不能派人，得看程序和他肯替你担多少责任。"))
          #f))

    (define (on-lesson-result result)
      (if (equal? result 'success)
          (begin
            (set! stage 2)
            (change-faction-relation! "官僚" 2)
            (complete-section!)
            (notify! "阿瑟欠你一次程序内的方便。"))
          #f))

    ;; 官僚·知识类生计：低风险的桌面文书活，是刷官僚关系的入门工，也给一份体面的日结。
    ;; 坏结果只是返工伤神（冷静 -1），不倒扣官僚关系——搞错日期是返工不是失职
    ;; （见城市生活设计「失败降低哪个势力由后果决定」）。情报不再从这里稳定产出。
    ;; 【待办】官僚线目前缺可重复的「保护/权限」产出，这份工暂以现金补足；
    ;; 长远应把它接到某条权限或结局路线上，见城市生活设计「已知问题」一节。
    (define (node-paperwork)
      (关系工作 "整理警察局文书" "官僚" '低 'knowledge
        (outcome "办得利落" (lambda () (add-item! "金钱" 10)))
        (outcome "按序归档" (lambda () (add-item! "金钱" 6)))
        (outcome "退回重填" (lambda () (spend-composure! 1)))))

    (define (node-problem)
      (node "替阿瑟处理一个程序管不了的人"
        :subtitle identity
        :tags (list "交锋")
        :resolve (instant (lambda () (start-encounter "教训" on-lesson-result)))))

    (define (police-hostile?) (equal? (relation-band "官僚") '敌视))

    (define (node-pass)
      (node "请阿瑟办理通行证"
        :subtitle (string-append identity "；" (cond
                    ((police-hostile?) "警察局现在不会给你签任何东西")
                    ((> (item-count "办案通行证") 0) "你已经持有一张")
                    ((> pass-cooldown 0) (string-append "还要等 " (number->string pass-cooldown) " 天"))
                    (else "一张一次性通行证，能在交锋中制造程序干预")))
        :disabled (or (police-hostile?) (> (item-count "办案通行证") 0) (> pass-cooldown 0))
        :requires (list (req-die))
        :resolve (instant
          (outcome "通行证签下来了"
            (lambda ()
              (add-item! "办案通行证" 1)
              (set! pass-cooldown (if (relation-at-least? "官僚" '信任) 1 pass-cooldown-max)))))))

    (define (nodes)
      (append
        (if (>= stage 1) (list (node-paperwork)) '())
        (if (>= stage 1)
            (list (node "阿瑟"
                    :subtitle identity
                    :resolve (observe
                      (cond
                        ((relation-at-least? "官僚" '信任)
                         "警察局记得你办过的事。真出了乱子，封锁线上的岗哨会认你的脸，该抬杆时抬杆——这点方便，够你在最紧要的一夜用上。")
                        ((= stage 1)
                         "阿瑟·贝尔相信每件事都该有一张表格；没有表格的事，只会在出后果时归警察局管。")
                        (else
                         "阿瑟不关心你是不是正确。他只承认你替他解决过一个后果。")))))
            '())
        (if (and (= stage 1) problem-invited?) (list (node-problem)) '())
        (if (= stage 2) (list (node-pass)) '())))

    (define-turn-rule "阿瑟通行证冷却"
      (lambda () (> pass-cooldown 0))
      (lambda () (set! pass-cooldown (- pass-cooldown 1))))

    (define-turn-rule "阿瑟提出私下委托"
      (lambda ()
        (and (= stage 1) (not problem-invited?) (relation-at-least? "官僚" '相识)))
      (lambda ()
        (set! problem-invited? #t)
        (notify! "阿瑟已经看过你做事。他有一件程序管不了的麻烦，想在警察局里和你谈。")))

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
