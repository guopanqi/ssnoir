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
        (spotlight! "海伦醒了" "你的动作惊醒了她，搜查被迫中断。")
        (end-encounter 'fail))
      #f))

(define (mark-clue! clue-id)
  (if (equal? clue-id target-clue)
      (begin
        (set! clue-found #t)
        (spotlight! "线索到手" "你意识到这才是真正有用的东西。现在该离开了。"))
      #f))

(define (node-leave)
  (instant-action "离开"
    (outcome "悄然离开" "你带着线索离开了海伦的公寓，门锁在身后轻轻合上。"
      (lambda ()
        (end-encounter 'success))
      'heavy)))

(define (helen-subtitle)
  (cond
    ((>= (wake-pressure 'current) 5) "危险——再有一点动静她就会醒来。")
    ((>= (wake-pressure 'current) 3) "她已经开始翻身，动作要快。")
    (#t                              "她伏在桌边沉睡。小心别弄出响动。")))

(define (node-helen)
  (clock-node "海伦"
    (helen-subtitle)
    (wake-pressure 'render-data)))

(define (node-search-room-a)
  (action "搜查卧室"
    (list (req-die))
    (roll 'sharpness
      (outcome "响动过大" "衣柜门轴发出一声尖响，海伦在外面动了一下。"
        (lambda ()
          (wake-tick! 2)))
      (outcome "搜查进展" "你翻过床头和衣柜，还需要继续找。"
        (lambda ()
          (room-a-search 'tick!)
          (wake-tick! 1)
          (if (room-a-search 'full?)
              (set! room-a-found #t)
              #f)))
      (outcome "柜底异物" "你很快发现柜底压着一个东西，但它被沉重的柜脚卡住了。"
        (lambda ()
          (tick-n! room-a-search 2)
          (if (room-a-search 'full?)
              (set! room-a-found #t)
              #f))))))

(define (node-lift-room-a)
  (action "搬开柜底"
    (list (req-die))
    (roll 'violence
      (outcome "柜子回落" "柜子只挪开一点，又重重撞回地板。"
        (lambda ()
          (wake-tick! 2)
          (stress-current-actor! 1)))
      (outcome "柜脚松动" "柜脚松动了一些，还差一点。"
        (lambda ()
          (room-a-lift 'tick!)
          (wake-tick! 1)
          (if (room-a-lift 'full?)
              (begin
                (set! room-a-checked #t)
                (mark-clue! 'A))
              #f)))
      (outcome "抽出纸包" "你稳稳托起柜角，抽出了下面的东西。"
        (lambda ()
          (tick-n! room-a-lift 2)
          (set! room-a-checked #t)
          (mark-clue! 'A))))))

(define (node-search-room-b)
  (action "搜查客厅"
    (list (req-die))
    (roll 'sharpness
      (outcome "险些失手" "玻璃杯滚到桌沿，差点摔碎。"
        (lambda ()
          (wake-tick! 2)))
      (outcome "继续翻找" "你检查了桌面和抽屉，还没找到关键物。"
        (lambda ()
          (room-b-search 'tick!)
          (wake-tick! 1)
          (if (room-b-search 'full?)
              (set! room-b-found #t)
              #f)))
      (outcome "折起的纸" "你在唱片机背后摸到一张折起来的纸。"
        (lambda ()
          (tick-n! room-b-search 2)
          (if (room-b-search 'full?)
              (set! room-b-found #t)
              #f))))))

(define (node-check-room-b)
  (instant-action "查看折纸"
    (outcome "纸上暗号" "纸面边角有一串仓促记下的暗号。"
      (lambda ()
        (set! room-b-checked #t)
        (mark-clue! 'B)))))

(define (node-search-room-c)
  (action "搜查杂物间"
    (list (req-die))
    (roll 'sharpness
      (outcome "纸箱塌落" "纸箱塌下来，灰尘呛得你差点咳出声。"
        (lambda ()
          (wake-tick! 1)))
      (outcome "揉皱留言" "你找到几张被揉皱的留言，还能再翻一翻。"
        (lambda ()
          (room-c-search 'tick!)))
      (outcome "整理留言" "你快速整理出一叠有用的留言。"
        (lambda ()
          (tick-n! room-c-search 2))))))

(define (node-take-room-c)
  (instant-action "收起留言"
    (outcome "收起留言" "这些留言不是目标线索，但能补上不少背景。"
      (lambda ()
        (set! room-c-looted #t)
        (add-item! '留言 1)
        (notify! "获得 留言 x1")))))

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

(define (room-a-clocks)
  (cond
    ((not room-a-found)
     (list (room-a-search 'render-data)))
    ((not room-a-checked)
     (list (room-a-lift 'render-data)))
    (#t '())))

(define (room-b-clocks)
  (if (not room-b-found)
      (list (room-b-search 'render-data))
      '()))

(define (room-c-clocks)
  (if (not (room-c-search 'full?))
      (list (room-c-search 'render-data))
      '()))

(define (get-render-data)
  (container "搜查海伦的公寓"
    (append
      (list
        (node-helen)
        (container-with-clocks "卧室" (room-a-children) (room-a-clocks))
        (container-with-clocks "客厅" (room-b-children) (room-b-clocks))
        (container-with-clocks "杂物间" (room-c-children) (room-c-clocks)))
      (if clue-found
          (list (node-leave))
          '()))))
