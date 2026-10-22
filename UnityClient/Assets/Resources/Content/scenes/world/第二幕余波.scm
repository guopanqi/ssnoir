;; 第二幕人物余波：不帮忙也不会让人物停在那里等玩家。
(define 二幕余波-艾迪已演? #f)
(define 二幕余波-露西已演? #f)
(define (二幕余波-艾迪已知?) 二幕余波-艾迪已演?)
(define (二幕余波-露西已知?) 二幕余波-露西已演?)
(define (二幕余波-艾迪近况)
  (cond
    ((equal? (第二幕 'wage-state) "拿回工钱") "工友拿回了10美元，艾迪仍在码头工作。")
    ((equal? (第二幕 'wage-state) "弗兰克调停") "弗兰克核清了账，艾迪仍在码头工作。")
    ((equal? (第二幕 'wage-state) "保住工作") "工钱上让了步，工友保住了工作。")
    ((equal? (第二幕 'wage-state) "撤出") "艾迪说争执还没完，他会继续想办法。")
    (else "艾迪和弗兰克后来自己去谈了，艾迪仍在码头工作。")))
(define (二幕余波-艾迪!)
  (set! 二幕余波-艾迪已演? #t)
  (play-dialogue!
    (line "世界" "年轻工人离开工棚。弗兰克把一件旧外套递给他。")
    (line "弗兰克" "明天要下雨。先把身体顾好，才有空继续讲理。")
    (line "艾迪"
      (cond
        ((equal? (第二幕 'wage-state) "拿回工钱")
          "十块钱已经要回来了。那小子总算敢抬头看工头。")
        ((equal? (第二幕 'wage-state) "弗兰克调停")
          "弗兰克核清了账，还顺手教训我一顿。")
        ((equal? (第二幕 'wage-state) "保住工作")
          "我们这次让了步。但人没丢饭碗，也算不坏。")
        ((equal? (第二幕 'wage-state) "撤出")
          "那事还没算完。你已经来过了，剩下的我去想办法。")
        (else
          "工资那事后来我和弗兰克自己去谈了。别件件事都替我操心。")))
    (line "尼尔" "你明天还去？")
    (line "艾迪" "去啊。总不能为了十块钱连饭都不吃了。")))
(define (二幕余波-露西!)
  (set! 二幕余波-露西已演? #t)
  (play-dialogue!
    (line "露西"
      (cond
        ((equal? (第二幕 'lucy-state) "顶班")
          "他们收下报名表了！你替我顶的那班，以后请你吃东西。")
        ((equal? (第二幕 'lucy-state) "代班")
          "报名表交上了。你出的代班钱我记着。")
        (else
          "我后来跟老板换了半天班，跑过去时鞋带都松了。好在报名表交上了。")))
    (line "尼尔" "第一堂课什么时候？")
    (line "露西" "下星期。希望我没忘记怎么写字。")))
(define (第二幕余波 . args)
  (cond
    ((eq? (car args) 'arrivals-at)
      (let ((loc (cadr args)))
        (cond
          ((and (equal? loc "码头") (>= world-day 12)
                (第一幕 'eddie-met?) (not 二幕余波-艾迪已演?))
            (list (arrival "艾迪：工钱之后" 二幕余波-艾迪!)))
          ((and (equal? loc "街角咖啡馆") (>= world-day 13)
                (第一幕 'lucy-met?) (not 二幕余波-露西已演?))
            (list (arrival "露西：报名之后" 二幕余波-露西!)))
          (else '()))))
    ((eq? (car args) 'save)
      (list (list "eddie-seen" 二幕余波-艾迪已演?)
            (list "lucy-seen" 二幕余波-露西已演?)))
    ((eq? (car args) 'load!)
      (let ((data (cadr args)))
        (set! 二幕余波-艾迪已演? (assoc-get data "eddie-seen" #f))
        (set! 二幕余波-露西已演? (assoc-get data "lucy-seen" #f))))
    (else (error "第二幕余波：未知消息"))))
