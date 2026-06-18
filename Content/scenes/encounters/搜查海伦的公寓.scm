;; scenes/encounters/搜查海伦的公寓.scm
;; 三个房间的搜查：在海伦醒来前找到真正的线索并离开。

(define wake-pressure (make-clock "海伦醒来" 6 'segments))

(define room-a-search (make-clock "卧室搜查" 3 'segments))
(define room-a-lift   (make-clock "搬开柜底" 2 'segments))
(define room-b-search (make-clock "客厅搜查" 3 'segments))
(define room-c-search (make-clock "杂物间搜查" 2 'segments))

(define room-a-found #f)
(define room-a-checked #f)
(define room-b-found #f)
(define room-b-checked #f)
(define room-c-looted #f)
(define target-clue (random-choice '(A B)))
(define clue-found #f)

(define (tick-n! clock n)
  (if (> n 0)
      (begin (clock 'tick!) (tick-n! clock (- n 1)))
      #f))

(define (wake-tick! n)
  (tick-n! wake-pressure n)
  (if (wake-pressure 'full?)
      (begin
        (notify! "海伦被响动惊醒了。")
        (end-encounter 'fail))
      #f))

(define (mark-clue! clue-id)
  (if (equal? clue-id target-clue)
      (begin
        (set! clue-found #t)
        (notify! "你意识到这才是真正有用的东西。现在该离开了。"))
      (notify! "这东西可疑，但和眼下要找的线索对不上。")))

(define (node-leave)
  (instant-action "离开"
    (lambda ()
      (notify! "你带着线索悄悄离开了海伦的公寓。")
      (end-encounter 'success))))

(define (node-helen)
  (container-with-clocks "海伦"
    (list
      (observe-action "醉倒的海伦" "海伦伏在桌边。她睡得不安稳，任何响动都可能让她醒来。"))
    (list
      (wake-pressure 'render-data))))

(define (node-search-room-a)
  (action "搜查卧室"
    (list (req-die))
    (roll 'sharpness
      (lambda ()
        (wake-tick! 2)
        (notify! "衣柜门轴发出一声尖响，海伦在外面动了一下。"))
      (lambda ()
        (room-a-search 'tick!)
        (wake-tick! 1)
        (if (room-a-search 'full?)
            (begin
              (set! room-a-found #t)
              (notify! "你发现柜底压着一个东西，但它被沉重的柜脚卡住了。"))
            (notify! "你翻过床头和衣柜，还需要继续找。")))
      (lambda ()
        (tick-n! room-a-search 2)
        (if (room-a-search 'full?)
            (begin
              (set! room-a-found #t)
              (notify! "你很快发现柜底压着一个东西，但它被沉重的柜脚卡住了。"))
            (notify! "你排除了几个显眼的位置，离关键处更近了。"))))))

(define (node-lift-room-a)
  (action "搬开柜底"
    (list (req-die))
    (roll 'violence
      (lambda ()
        (wake-tick! 2)
        (stress-current-actor! 1)
        (notify! "柜子只挪开一点，又重重撞回地板。"))
      (lambda ()
        (room-a-lift 'tick!)
        (wake-tick! 1)
        (if (room-a-lift 'full?)
            (begin
              (set! room-a-checked #t)
              (mark-clue! 'A))
            (notify! "柜脚松动了一些，还差一点。")))
      (lambda ()
        (tick-n! room-a-lift 2)
        (set! room-a-checked #t)
        (notify! "你稳稳托起柜角，抽出了下面的东西。")
        (mark-clue! 'A)))))

(define (node-search-room-b)
  (action "搜查客厅"
    (list (req-die))
    (roll 'sharpness
      (lambda ()
        (wake-tick! 2)
        (notify! "玻璃杯滚到桌沿，差点摔碎。"))
      (lambda ()
        (room-b-search 'tick!)
        (wake-tick! 1)
        (if (room-b-search 'full?)
            (begin
              (set! room-b-found #t)
              (notify! "你在唱片机背后摸到一张折起来的纸。"))
            (notify! "你检查了桌面和抽屉，还没找到关键物。")))
      (lambda ()
        (tick-n! room-b-search 2)
        (if (room-b-search 'full?)
            (begin
              (set! room-b-found #t)
              (notify! "你在唱片机背后摸到一张折起来的纸。"))
            (notify! "客厅里的东西不多，你很快缩小了范围。"))))))

(define (node-check-room-b)
  (instant-action "查看折纸"
    (lambda ()
      (set! room-b-checked #t)
      (mark-clue! 'B))))

(define (node-search-room-c)
  (action "搜查杂物间"
    (list (req-die))
    (roll 'sharpness
      (lambda ()
        (wake-tick! 1)
        (notify! "纸箱塌下来，灰尘呛得你差点咳出声。"))
      (lambda ()
        (room-c-search 'tick!)
        (notify! "你找到几张被揉皱的留言，还能再翻一翻。"))
      (lambda ()
        (tick-n! room-c-search 2)
        (notify! "你快速整理出一叠有用的留言。")))))

(define (node-take-room-c)
  (instant-action "收起留言"
    (lambda ()
      (set! room-c-looted #t)
      (add-item! '留言 1)
      (notify! "获得留言。"))))

(define (room-a-children)
  (cond
    ((not room-a-found)
     (list (node-search-room-a)))
    ((not room-a-checked)
     (list
       (observe-action "柜底压着的东西" "一角纸边露在柜脚下面，必须先把柜子挪开。")
       (node-lift-room-a)))
    (#t
     (list
       (observe-action "翻过的卧室" "柜底已经被翻开，没有更多值得冒险的东西。")))))

(define (room-b-children)
  (cond
    ((not room-b-found)
     (list (node-search-room-b)))
    ((not room-b-checked)
     (list
       (observe-action "唱片机背后的折纸" "纸张被塞得很深，边缘磨得发白。")
       (node-check-room-b)))
    (#t
     (list
       (observe-action "翻过的客厅" "唱片机背后已经空了。")))))

(define (room-c-children)
  (cond
    ((not (room-c-search 'full?))
     (list (node-search-room-c)))
    ((not room-c-looted)
     (list
       (observe-action "散落的留言" "这些留言不像目标线索，但能补上不少背景。")
       (node-take-room-c)))
    (#t
     (list
       (observe-action "翻过的杂物间" "纸箱被重新推回角落。")))))

(define (get-render-data)
  (container "海伦的公寓"
    (append
      (list
        (node-helen)
        (container "卧室" (room-a-children))
        (container "客厅" (room-b-children))
        (container "杂物间" (room-c-children)))
      (if clue-found
          (list (node-leave))
          '()))))
