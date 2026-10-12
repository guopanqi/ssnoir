;; 一次性的小侦探委托。酒馆接案、码头寻找、酒馆归还；状态纳入世界存档。
(define lost-property
  (let ()
    (define lunch-checked? #f)
    (define stage 0) ; 0 可接 / 1 已接 / 2 已找到 / 3 已归还
    (define rumours
      (make-clock "小道消息" 4 'gauge
        "喝酒 +3，做服务员 +1；填满后听到一件找东西的委托。"))
    (define (available?)
      (and (= stage 0) (= (three-letters 'story-stage) 1)))
    (define (hear-rumours! amount)
      (if (and (available?) (not (rumours 'full?)))
          (begin
            (rumours 'advance! amount)
            (if (rumours 'full?)
                (result-supplement! "听到了委托：寻找工具袋") #f))
          #f))
    (define (accept-node)
      (node "寻找工具袋"
        :anchor "老街酒馆"
        :subtitle "替修理工找回工具袋，报酬30"
        :resolve (instant
        (lambda ()
          (set! stage 1)
          (play-bubble!
            (line "修理工" "你那告示上说，替人找东西？" :at "老街酒馆")
            (line "尼尔" "什么东西？")
            (line "修理工" "工具袋。今天从码头回来就没了。" :at "老街酒馆")
            (line "修理工" "午饭在货堆旁吃的，收工在点工棚领钱。" :at "老街酒馆")
            (line "修理工" "棕皮的，铜扣少了一个。找回来给你三十。" :at "老街酒馆"))))))

    (define (search-node title description found?)
      (node title
        :subtitle description
        :requires (list (req-die))
        :resolve (instant
          (outcome (lambda ()
              (if found?
                  (begin
                    (set! stage 2)
                    (play-bubble!
                      (line "世界" "点工棚的长凳下，压着一个棕皮袋。")
                      (line "尼尔" "铜扣少一个。就是它。")))
                  (begin
                    (set! lunch-checked? #t)
                    (play-bubble!
                      (line "世界" "午饭留下的油纸还在，附近没有工具袋。")
                      (line "尼尔" "他收工还去了点工棚。")))))))))

    (define (return-node)
      (node "归还工具袋"
        :anchor "老街酒馆"
        :subtitle "把找到的工具袋交还修理工"
        :resolve (instant
        (lambda ()
          (set! stage 3)
          (add-item! "金钱" 30)
          (play-bubble!
            (line "修理工" "没错，里头那把扳手跟了我十年。" :at "老街酒馆")
            (line "尼尔" "落在点工棚的长凳下了。")
            (line "修理工" "三十块。你那张告示，我替你留意着。" :at "老街酒馆"))))))

    (define (nodes-at location)
      (if (and (= stage 0) (not (= (three-letters 'story-stage) 1))) '()
          (cond
            ((equal? location "老街酒馆")
             (cond ((= stage 0)
                    (if (rumours 'full?) (list (accept-node))
                        (list (clock-node "标注：酒馆小道消息" (rumours 'render-data)))))
                   ((= stage 2) (list (return-node)))
                   (else '())))
            ((and (equal? location "码头") (= stage 1))
             (append
               (if lunch-checked? '()
                   (list (search-node "查看午饭处" "他中午坐过货堆旁，尚未领工钱" #f)))
               (list (search-node "寻找长凳下" "点工棚：他收工领钱的最后一站" #t))))
            (else '()))))

    (lambda args
      (cond
        ((eq? (car args) 'nodes-at) (nodes-at (cadr args)))
        ((eq? (car args) 'hear-rumours!) (hear-rumours! (cadr args)))
        ((eq? (car args) 'save)
         (list (list "stage" stage) (list "lunch-checked?" lunch-checked?)
               (list "rumours" (rumours 'save))))
        ((eq? (car args) 'load!)
         (set! stage (assoc-get (cadr args) "stage" 0))
         (set! lunch-checked? (assoc-get (cadr args) "lunch-checked?" #f))
         (rumours 'load! (assoc-get (cadr args) "rumours" 0))
         (if (member? stage '(0 1 2 3)) #t (error "失物委托：非法存档阶段")))
        (else #f)))))
