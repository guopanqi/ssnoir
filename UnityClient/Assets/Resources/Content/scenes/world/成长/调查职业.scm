;; 职业只拥有资格与口碑等待，不拥有客户各自的调查过程。
(define 调查职业
  (let ()
    (define 推荐日 0)
    (define (认可!)
      (if (equal? (get-global '身份) "侦探") #f
        (begin (set-global! '身份 "侦探") (set-global! '侦探入行日 world-day))))
    (define (传开!)
      (if (> 推荐日 0) #f
        (begin (set! 推荐日 (+ world-day 2))
          (play-dialogue!
    (line "调酒师" "你不是想替人办事吗？有人问起，我会提你的名字。")
    (line "尼尔" "谢了。")
    (line "调酒师" "先帮我把场子看稳。")))))
    (define (on-day!)
      (if (and (第三幕 'reported?) (> 推荐日 0) (>= world-day 推荐日) (乔的手提箱 'eligible?))
        (乔的手提箱 'activate!) #f))
    (lambda (msg . args)
      (cond
        ((eq? msg 'on-trouble-settled!) (传开!))
        ((eq? msg 'recognize!) (认可!))
        ((eq? msg 'on-day-start!) (on-day!))
        
        ((eq? msg 'arrivals-at) '())
        ((eq? msg 'nodes-at) '())
        ((eq? msg 'dossier)
          (if (or (not (第三幕 'reported?)) (equal? (get-global '身份) "侦探")
                  (member? (assoc-get (经理的首单 'save) "stage" "未见") '("待答复" "处理中" "成功待交差" "失败待交差"))) '()
            (list (dossier "寻找第一个客户" :kind '打算 :primary #f
              :now (cond ((= 推荐日 0) "先去老街酒馆混个脸熟。有人遇到麻烦时，让他们知道你肯帮忙。")
                ((> 推荐日 world-day) "酒保答应推荐你。先在城里干活糊口，留心旅馆里来访的主顾。")
                (else "乔丢了谋生用的样品箱。替他找回来，赚下工钱和一份口碑。"))
              :where (if (= 推荐日 0) "老街酒馆" "家")))))
        ((eq? msg 'save) (list (list "referral-day" 推荐日)))
        ((eq? msg 'load!) (let ((data (car args)))
          (set! 推荐日 (assoc-get data "referral-day" 0))
          (if (and (number? 推荐日) (>= 推荐日 0)) #t (error "调查职业：非法推荐日"))))
        (else (error "调查职业：未知消息"))))))
