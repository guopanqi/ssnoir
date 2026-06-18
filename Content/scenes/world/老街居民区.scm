;; scenes/world/老街居民区.scm

(define residential
  (let ()
    (define helen-stage 1)
    ;; stage: 1=初见  2=索要酒  3=醉倒可搜查  4=拿到线索  5=下一小节占位

    (define (node-give-wine)
      (action "海伦"
        (list (req-item '酒 2))
        (instant (lambda ()
          (set! helen-stage 3)
          (notify! "酒劲很快翻上来。海伦伏在桌边，话没说完就睡了过去。")))))

    (define (node-search-room)
      (encounter-action "翻翻她的家，找一些东西"
        (lambda ()
          (start-encounter "搜查海伦的公寓"
            (lambda (result)
              (if (equal? result 'success)
                  (begin
                    (set! helen-stage 4)
                    (add-item! '海伦的线索 1)
                    (notify! "你从抽屉夹层里找到一张折起的便条。"))
                  (begin
                    (set! helen-stage 2)
                    (notify! "海伦猛地醒过来，盯着你和被翻乱的抽屉。"))))))))

    (define (node-finish-section)
      (instant-action "整理海伦的线索"
        (lambda ()
          (set! helen-stage 5)
          (advance-chapter!)
          (notify! "海伦的线索暂时够了。你把便条收好，准备追下一条线。"))))

    (define (helen-apartment-children)
      (cond
        ((= helen-stage 1)
         (list
           (instant-action "海伦"
             (lambda ()
               (set! helen-stage 2)
               (notify! "海伦：别问了，我真的不知道。可她的眼神总避开你，她知道些什么。")))
           (observe-action "几个空掉的酒瓶" "瓶口还湿着，廉价烈酒的味道压过了房间里的香水味。")))
        ((= helen-stage 2)
         (list
           (node-give-wine)
           (observe-action "几个空掉的酒瓶" "空瓶横七竖八地倒在桌边。")))
        ((= helen-stage 3)
         (list
           (observe-action "醉倒的海伦" "海伦伏在桌边，呼吸沉重，短时间内不会醒。")
           (node-search-room)))
        ((= helen-stage 4)
         (list
           (observe-action "醉倒的海伦" "海伦仍旧睡着。房间里只剩下她沉重的呼吸声。")
           (observe-action "海伦的线索" "那张便条上有几个被反复描重的名字。")
           (node-finish-section)))
        (#t
         (list
           (observe-action "翻过的公寓" "房间被你恢复成差不多原来的样子。海伦还没醒。")))))

    (define (helen-apartment-nodes)
      (if (get-global 'helen-apartment-open)
          (list
            (container "海伦的公寓"
              (helen-apartment-children)))
          '()))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list
             (container "老街居民区"
               (append
                 (list (observe-action "昏黄的灯光" "低矮的砖房挨着砖房，有人在里面说话，听不清楚。"))
                 (helen-apartment-nodes)))))

          ((equal? msg 'complete?)
           (>= helen-stage 5))

          ((equal? msg 'save)
           (list
             (list "helen-stage" helen-stage)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! helen-stage (assoc-get data "helen-stage" 1))))

          (#t #f))))))
