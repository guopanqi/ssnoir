;; 移植立绘剧场四·独闯老街（老街 · 没有人愿意开口）。
;; 布景沿用 HTML SETS.alley：两侧高墙、电线、左右窗（右窗暖光）、左右门、横线；
;; whisp 街头浮字转成旁白自动字幕按顺序播；SVG 肢体/表情动画不移植。
;; alley 布景没有灯具，HTML 的 fl()/lamp() 在此无视觉目标，故略去，只保留描绘进场。
(define (独闯老街-演出)
  (define 图形 '())
  (define 描绘 '())
  (define (加入 object) (set! 图形 (append 图形 (list object))))
  (define (线 id parent color width points delay)
    (加入 (theatre-with (theatre-line id parent color width points) 'reveal 0))
    (set! 描绘 (append 描绘 (list
      (theatre-sequence
        (theatre-wait (+ .001 delay))
        (theatre-tween id 'reveal 1 2.2 'smooth))))))
  (define (框 id parent x y w h color width delay)
    (线 id parent color width
      (list (list x y) (list (+ x w) y) (list (+ x w) (+ y h))
        (list x (+ y h)) (list x y)) delay))
  (define (填 id parent x y w h color)
    (加入 (theatre-polygon id parent color
      (list (list x y) (list (+ x w) y) (list (+ x w) (+ y h)) (list x (+ y h))))))
  ;; 人物组落脚在地面（y=790）；走位改组 x，翻面改立绘 scale-x，显隐改立绘 opacity。
  (define (人物 id asset w h x flip)
    (加入 (theatre-group (string-append id "位置") "" x 790))
    (加入 (theatre-with
      (if flip
        (theatre-with
          (theatre-image id (string-append id "位置") asset 0 0 w h "巷灯") 'scale-x -1)
        (theatre-image id (string-append id "位置") asset 0 0 w h "巷灯"))
      'opacity 0)))
  (define (现身 id) (theatre-tween id 'opacity 1 .05))
  (define (走 group x ms pan tag)
    (theatre-parallel
      (theatre-tween group 'x x (/ ms 1000.0) 'smooth)
      (theatre-sound (string-append "脚步/" tag) "StageSounds/老街酒馆/脚步" #f .4 pan)
      (theatre-wait (+ (/ ms 1000.0) .2))))
  (define (说 who x text seconds)
    (theatre-during
      (theatre-caption-for who text seconds
        (cond ((equal? who "尼尔") "#F0CF8A")
              ((equal? who "夜莺") "#8FD9D0")
              ((equal? who "贝恩斯") "#93B4EA")
              ((equal? who "弗兰克") "#E39A88")
              (else "#C9CFE2")))
      (theatre-parallel
        (theatre-tween "焦点" 'x (+ 800 (* (- x 800) .5)) 1 'smooth)
        (theatre-tween "说话柔光" 'x x 1.2 'smooth)
        (theatre-tween "说话柔光" 'opacity .13 1.2 'smooth))))
  (define (旁白 text seconds)
    (theatre-caption-for "旁白" text seconds "#C9CFE2"))

  ;; 布景：HTML ALLEY（地面 790 为基准）。
  (加入 (theatre-with (theatre-group "布景" "" 0 0) 'opacity 0))
  (线 "地面" "布景" "#5A6F9C" 1.5 '((0 790) (1600 790)) 0)
  (线 "路沿" "布景" "#33466E" 1 '((0 806) (1600 806)) .4)
  (线 "左墙" "布景" "#CFE0FF" 2.5 '((340 60) (340 790)) .2)
  (线 "右墙" "布景" "#CFE0FF" 2.5 '((1260 60) (1260 790)) .3)
  (线 "电线左" "布景" "#9FB4E0" 1.5 '((340 150) (190 215)) .6)
  (线 "电线右" "布景" "#9FB4E0" 1.5 '((1260 150) (1410 215)) .6)
  (框 "左窗" "布景" 200 330 90 110 "#6F86B8" 1.5 1.2)
  (填 "右窗光" "布景" 1310 300 90 110 "#8A6A388C")
  (框 "右窗" "布景" 1310 300 90 110 "#6F86B8" 1.5 1.3)
  (框 "右门" "布景" 1330 520 100 270 "#9FB4E0" 2 1.5)
  (框 "左门" "布景" 180 540 110 250 "#7F95C4" 2 1.6)
  ;; HTML 的 Q 二次曲线换算成等价 cubic 控制点。
  (线 "横线" "布景" "#7F95C4" 1
    (theatre-cubic '(340 250) '(646.67 296.67) '(953.33 293.33) '(1260 240) 24) 1.8)
  (加入 (theatre-light "巷灯" "" "#F3D08A" 800 550 620 1))
  (加入 (theatre-focus "焦点" 800 576 .38 .66))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#9DB0E0" 800 560 440 560) 'opacity 0))
  ;; 立绘文件都是方形像素（1024，助手图 1254），显示框沿用雨夜来访/路灯下范本比例；
  ;; 街坊取小一号，小孩用黑影剪影小比例。
  (人物 "尼尔" "Portraits/Neon/尼尔_抱臂" 308 308 -150 #f)
  (人物 "街坊" "Portraits/Neon/路人男" 308 308 980 #t)
  (人物 "路人" "Portraits/Neon/路人男2" 308 308 1360 #t)
  (人物 "小孩" "Portraits/Neon/黑影" 147 147 1510 #f)

  (play-theatre! (theatre-scene 1600 900 "#080A0E" 图形)
    ;; 建楼收尾和尼尔进场重叠：大结构画出后他就开走，不等最后一笔。
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "布景" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait 2.5)
        (theatre-parallel
          (现身 "街坊")
          (现身 "路人")
          (现身 "尼尔")
          (走 "尼尔位置" 460 2600 -.2 "独闯1"))))
    (旁白 "（尼尔独自到了这里，有人看到尼尔是陌生人，行为举止都很陌生，就不再开口。）" 4.335)
    (走 "尼尔位置" 640 1500 -.1 "独闯2")
    (theatre-wait 1)
    (theatre-parallel
      (theatre-tween "街坊" 'scale-x 1 .3)
      (theatre-tween "路人" 'scale-x 1 .3)
      (theatre-wait 1.6))
    (theatre-parallel
      (theatre-tween "街坊位置" 'x 760 .9 'smooth)
      (theatre-sound "脚步/街坊" "StageSounds/老街酒馆/脚步" #f .25 .2)
      (theatre-wait 1.1))
    (theatre-wait 1)
    (旁白 "（有人故意指错路。）" 1.500)
    (走 "尼尔位置" 1050 2600 -.1 "独闯3")
    (theatre-wait .3)
    (theatre-tween "尼尔" 'scale-x -1 .3)
    (theatre-wait .7)
    (走 "尼尔位置" 700 2400 -.2 "独闯4")
    (theatre-wait .2)
    (现身 "小孩")
    (theatre-wait .7)
    (旁白 "（尼尔刚进巷子的时候，一个孩子看了他一眼，跑掉了。）" 3.180)
    (theatre-parallel
      (theatre-tween "小孩位置" 'x 1800 1.5 'smooth)
      (theatre-tween "小孩" 'scale-x 1 .3)
      (theatre-sound "脚步/小孩" "StageSounds/老街酒馆/脚步" #f .25 .3)
      (theatre-wait 1.7))
    ;; HTML 里五句 whisp 重叠飘 5 秒；Unity 没有文字图元，转成旁白按顺序播，
    ;; 配人群哗循环（主字幕结束时自动停）。
    (theatre-during
      (theatre-sequence
        (旁白 "有个穿长外套的侦探在问烟" 1.710)
        (旁白 "有个穿长外套的侦探在问烟" 1.710)
        (旁白 "在问烟……" 0.975)
        (旁白 "有个穿长外套的侦探在问烟" 1.710)
        (旁白 "侦探……" 0.870)
        (旁白 "（十分钟后，整个地方已经知道：“有个穿长外套的侦探在问烟。”）" 3.705))
      (theatre-loop
        (theatre-sequence
          (theatre-sound "街语" "StageSounds/老街酒馆/人群哗" #f .22 0)
          (theatre-wait 2.6))))
    ;; HTML lamp(.4)：巷子无灯具，转成布景与巷灯一起压暗。
    (theatre-parallel
      (theatre-tween "巷灯" 'brightness .5 .7)
      (theatre-tween "布景" 'opacity .6 .7)
      (theatre-wait .9))
    (旁白 "（独自探索填满之后，尼尔没有找到任何头绪。）" 2.760)
    (theatre-wait .8)))
