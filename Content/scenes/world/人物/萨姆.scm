;; 萨姆（Samuel Rourke）——因拒绝压案而离职的前警探、现编外侦探。

(define sam
  (let ()
    (define stage 1) ; 1初谈(登门当天即达) / 2讲过亨利
    (define evidence "无") ; 无 / 老板 / 完整
    (define identity "离开警队的邻城刑警，现为编外侦探")

    (define (remembrance-ready?)
      (and (= stage 1) (>= (nightingale 'truth-progress) 2)))

    ;; 三层已揭后他已经登门自报家门(见 夜莺.scm 的「萨姆登门」必看场景)，
    ;; 这里只负责把内部 stage 从初始值推进、补一句酒馆场景才有的追加信息。
    (define (debut!)
      (if (= stage 1)
          (spotlight! "萨姆" "他手里有三条能追下去的线：码头老人、邻城案卷、货栈船期。")
          #f))

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

    (define (node-sam)
      (node "萨姆"
        :subtitle (sam-subtitle)
        :children (append
                    (list (node-chat))
                    (if (and (remembrance-ready?) (not (nightingale 'route-settled?)))
                        (list (node-remembrance))
                        '())
                    (if (and (= stage 2) (nightingale 'truth-known?) (equal? evidence "无")
                             (not (nightingale 'route-settled?)))
                        (list (node-evidence))
                        '()))))

    (define (present?)
      (and (nightingale 'stage3-open?) (nightingale 'sam-intro?) (not (nightingale 'route-settled?))))

    (define (nodes)
      (if (present?) (list (node-sam)) '()))

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

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes) (nodes))
          ((equal? msg 'stage) stage)
          ((equal? msg 'evidence) evidence)
          ((equal? msg 'debut!) (debut!))
          ((equal? msg 'save) (list (list "stage" stage) (list "evidence" evidence)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! stage (assoc-get data "stage" 1))
             (set! evidence (assoc-get data "evidence" "无"))
             (if (or (equal? evidence "无") (equal? evidence "老板") (equal? evidence "完整"))
                 #t (error "萨姆存档错误：证据状态非法"))))
          (else #f))))))
