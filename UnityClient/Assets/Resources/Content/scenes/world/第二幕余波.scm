;; 亲近只记录是否呈现；不再按五种交锋路径分出五份余波。
(define 第二幕余波
  (let ((gloves-seen? #f))
    (define (gloves!)
      (if (or gloves-seen? (not (第一幕 'eddie-met?))) #f
        (begin
          (set! gloves-seen? #t)
          (play-dialogue!
            (line "世界" "艾迪瞥了一眼尼尔磨破的手套，从工具袋里抽出一副厚帆布的，递了过来。")
            (line "艾迪" "你那破布条早该扔了。戴上这个。")
            (line "尼尔" "你自己呢？")
            (line "艾迪" "箱子里还有旧的。顾好你自己的手。")))))
    (lambda args
      (cond
        ((eq? (car args) 'gloves!) (gloves!))
        ((eq? (car args) 'gloves-seen?) gloves-seen?)
        ((eq? (car args) 'mark-gloves-seen!) (set! gloves-seen? #t))
        ((eq? (car args) 'nodes-at) '())
        ((eq? (car args) 'arrivals-at)
          (if (and (= (第一幕 'act) 2) (第一幕 'eddie-met?) (not gloves-seen?)
                   (member? (第二幕 'wage-state) '("上工" "停班"))
                   (or (and (equal? (cadr args) "码头") (第二幕 'eddie-on-dock?))
                       (and (equal? (cadr args) "码头居民区") (not (第二幕 'eddie-on-dock?)))))
            (list (arrival "艾迪的手套" gloves!)) '()))
        ((eq? (car args) 'save) (list (list "gloves" gloves-seen?)))
        ((eq? (car args) 'load!) (set! gloves-seen? (assoc-get (cadr args) "gloves" #f)))
        (else (error "第二幕余波：未知消息"))))))
