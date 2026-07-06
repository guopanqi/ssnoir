;; scenes/world/警局.scm - 官僚路线
;; 用时间处理文书、陪探长办案，换取具体而有限的程序手段。

(define police-station
  (let ()
    (define detective-stage 0) ; 0=初识，1=走访后待整理，2=人物小节完成
    (define pass-cooldown 0)
    (define pass-cooldown-max 3)

    (define (has-investigation-pass?)
      (> (item-count "办案通行证") 0))

    (define (issue-investigation-pass!)
      (if (has-investigation-pass?)
          (error "办理办案通行证：已持有一张")
          #t)
      (if (> pass-cooldown 0)
          (error "办理办案通行证：仍在冷却中")
          #t)
      (add-item! "办案通行证" 1)
      (set! pass-cooldown pass-cooldown-max))

    (define (node-paperwork)
      (工作 "整理警局文书" "官僚" '低 'knowledge
        (outcome "条目清楚" "案卷归了类，值班警员少见地夸了一句。"
          (lambda () (add-item! "金钱" 6)))
        (outcome "按时交差" "一下午都耗在纸堆里，拿到一点报酬。"
          (lambda () (add-item! "金钱" 3)))
        (outcome "抄错编号" "编号抄岔了，只能从头返工。"
          (lambda () (stress-current-actor! 1)))))

    (define (finish-detective-section!)
      (if (not (= detective-stage 1))
          (error "探长支线：非法重复完成")
          #t)
      (set! detective-stage 2)
      (set-global! '探长愿意担保 #t)
      (complete-section!)
      (notify! "探长记住了你。遇到程序上的麻烦，他愿意替你担保一次。"))

    (define (finish-detective-visit!)
      (if (not (= detective-stage 0))
          (error "陪探长走访：支线阶段错误")
          #t)
      (set! detective-stage 1)
      (notify! "证人的话还没有整理成正式口供，探长请你再帮一次。"))

    (define (node-accompany-detective)
      (action "陪探长走访"
        (list (req-die))
        (roll 'social
          (outcome "碰了一鼻子灰" "问了一圈，没人愿意当着警察的面说实话。"
            (lambda () (stress-current-actor! 1)))
          (outcome "找到证人" "你替探长缓和了气氛，终于有人愿意开口。"
            (lambda () (finish-detective-visit!)))
          (outcome "问到关键处" "你找准了说话方式，探长顺势拿到了口供。"
            (lambda () (finish-detective-visit!))))))

    (define (node-file-statement)
      (action "替探长整理口供"
        (list (req-die))
        (roll 'knowledge
          (outcome "材料退回" "几处说法对不上，材料被退了回来。"
            (lambda () (stress-current-actor! 1)))
          (outcome "口供入档" "你把散乱的话整理成了能进入正式记录的口供。"
            (lambda () (finish-detective-section!)))
          (outcome "留下余地" "口供写得清楚，也替证人避开了不必要的麻烦。"
            (lambda () (finish-detective-section!))))))

    (define (node-delay-event)
      (action "请探长延期一天"
        (list (req-die))
        (instant
          (outcome "压下一天" "探长打了几个电话，把码头的手续拖后了一天。"
            (lambda () (delay-public-event-one-day!))))))

    (define (pass-status-text)
      (cond
        ((has-investigation-pass?) "你已经持有一张；用掉以前不能再办。")
        ((> pass-cooldown 0)
         (string-append "警局还要等 " (number->string pass-cooldown) " 天才能再签发。"))
        (else "消耗一次行动，取得一张可在不同场合使用的一次性通行证。")))

    (define (node-investigation-pass)
      (node "办理办案通行证"
        :subtitle (pass-status-text)
        :requires (list (req-die))
        :clocks (if (> pass-cooldown 0)
                    (list (list 'clock "再次签发" pass-cooldown pass-cooldown-max 'countdown
                                "归零后可以再次办理；最多持有一张。"))
                    '())
        :disabled (or (has-investigation-pass?) (> pass-cooldown 0))
        :resolve (instant
          (outcome "通行证办妥" "凭这张证件，你可以在需要时以协助办案的名义要求通行或调查。"
            (lambda () (issue-investigation-pass!))))))

    (define (node-give-invoice)
      (action "把异常货单交给探长"
        (list (req-die))
        (instant
          (outcome "正式立案" "探长把货单压进案卷：这次终于有东西能写进正式记录。"
            (lambda ()
              (deliver-abnormal-invoice! "探长")
              (change-faction-relation! "官僚" 1)
              (if (and (not (has-investigation-pass?)) (= pass-cooldown 0))
                  (begin
                    (issue-investigation-pass!)
                    (notify! "探长收下货单，并替你签发了一张办案通行证。"))
                  #f))))))

    (define (detective-description)
      (cond
        ((= detective-stage 0) "探长忙着翻案卷。混个脸熟以后，也许能陪他出去走一趟。")
        ((= detective-stage 1) "走访拿到了证词，但还需要有人把它整理成正式口供。")
        ((equal? (abnormal-invoice-state) "交给探长")
         "异常货单已经进了案卷，码头接下来会多一些正式检查。")
        (else "探长已经认得你。程序上有余地时，他愿意替你说句话。")))

    (define (police-children)
      (append
        (list (node-paperwork)
              (observe-action "探长" (detective-description)))
        (if (and (= detective-stage 0) (relation-at-least? "官僚" '脸熟))
            (list (node-accompany-detective))
            '())
        (if (= detective-stage 1) (list (node-file-statement)) '())
        (if (and (>= detective-stage 2) (public-event-can-delay?))
            (list (node-delay-event))
            '())
        (if (>= detective-stage 2)
            (list (node-investigation-pass))
            '())
        (if (abnormal-invoice-held?) (list (node-give-invoice)) '())))

    (define-turn-rule "办案通行证再次签发"
      (lambda () (> pass-cooldown 0))
      (lambda () (set! pass-cooldown (- pass-cooldown 1))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (container "警局" (police-children))))
          ((equal? msg 'save)
           (list (list "detective-stage" detective-stage)
                 (list "pass-cooldown" pass-cooldown)))
          ((equal? msg 'load!)
           (begin
             (set! detective-stage
                   (assoc-get (cadr args) "detective-stage" 0))
             (set! pass-cooldown
                   (assoc-get (cadr args) "pass-cooldown" 0))))
          ((equal? msg 'debug-finish-section)
           (begin
             (if (= detective-stage 0) (set! detective-stage 1) #f)
             (if (= detective-stage 1) (finish-detective-section!) #f)))
          (#t #f))))))
