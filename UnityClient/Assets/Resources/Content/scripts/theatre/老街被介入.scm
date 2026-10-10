;; 移植立绘剧场十五·老街被介入（老街 · 警察在各处搜查）。
;; 布景沿用 HTML SETS.sign（alley + 路牌杆与三块路牌）；
;; note 标签（搜查/盘问/审查/不处置）转成旁白自动字幕按顺序播；
;; HTML 没有警灯元件，按任务要求用红/蓝两个 glow 交替明灭近似警灯。
;; alley 布景没有灯具，HTML 的 fl()/lamp() 在此无视觉目标，故略去，只保留描绘进场。
(define (老街被介入-演出)
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

  ;; 布景：HTML SIGN = ALLEY + 路牌杆与三块路牌（地面 790 为基准）。
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
  (线 "牌杆" "布景" "#E6EEFF" 3 '((800 790) (800 360)) 1)
  (线 "牌左上" "布景" "#CFE0FF" 2
    '((800 372) (650 372) (622 394) (650 416) (800 416) (800 372)) 1.3)
  (线 "牌右" "布景" "#CFE0FF" 2
    '((800 430) (950 430) (978 452) (950 474) (800 474) (800 430)) 1.5)
  (线 "牌左下" "布景" "#9FB4E0" 2
    '((800 490) (680 490) (656 510) (680 530) (800 530) (800 490)) 1.7)
  (加入 (theatre-light "巷灯" "" "#F3D08A" 800 550 620 1))
  (加入 (theatre-focus "焦点" 800 576 .36 .64))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#9DB0E0" 800 560 440 560) 'opacity 0))
  ;; 警灯：红/蓝两个柔光交替明灭（HTML 无此元件，为近似效果）。
  (加入 (theatre-with (theatre-glow "警灯红" "" "#E0393E" 800 150 520 200) 'opacity 0))
  (加入 (theatre-with (theatre-glow "警灯蓝" "" "#3B7DE0" 800 150 520 200) 'opacity 0))
  ;; 立绘文件都是方形像素（1024，助手图 1254），显示框沿用雨夜来访/路灯下范本比例；
  ;; 六人同场，横向按 HTML 相对顺序拉开；小孩用黑影剪影小比例。
  (人物 "街坊" "Portraits/Neon/路人男" 308 308 950 #t)
  (人物 "路人" "Portraits/Neon/路人女" 308 308 1150 #f)
  (人物 "小孩" "Portraits/Neon/黑影" 147 147 730 #f)
  (人物 "贝恩斯" "Portraits/Neon/贝恩斯" 308 308 1800 #t)
  (人物 "助手" "Portraits/Neon/贝恩斯_立定" 308 308 -200 #f)
  (人物 "尼尔" "Portraits/Neon/尼尔_抱臂" 308 308 -150 #f)

  (play-theatre! (theatre-scene 1600 900 "#080A0E" 图形)
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "布景" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait 1.5)
        (现身 "街坊")
        (现身 "路人")
        (现身 "小孩")
        (theatre-wait 1.5)))
    (现身 "尼尔")
    (走 "尼尔位置" 520 2400 -.2 "介入1")
    (theatre-wait .2)
    (旁白 "（你进入老街，就看到一些警察在各处搜查，盘问，审查。）" 3.285)
    ;; 警察在场期间警灯循环交替；主段落结束时自动停下并熄灭。
    (theatre-during
      (theatre-sequence
        (theatre-parallel
          (theatre-sequence (现身 "贝恩斯") (走 "贝恩斯位置" 1420 2600 .2 "介入2"))
          (theatre-sequence (现身 "助手") (走 "助手位置" 180 2600 -.2 "介入3")))
        (theatre-tween "路人" 'scale-x -1 .3)
        ;; HTML 里三块 note 标签按延迟重叠出现；Unity 没有文字图元，转成旁白按顺序播。
        (旁白 "搜查" 0.660)
        (旁白 "盘问" 0.660)
        (旁白 "审查" 0.660)
        (theatre-wait 1.6)
        (theatre-wait 1.6)
        (theatre-wait 2)
        (旁白 "不处置，转而打电话" 1.395)
        (theatre-wait 3.6)
        (theatre-wait .8))
      (theatre-loop
        (theatre-sequence
          (theatre-tween "警灯红" 'opacity .45 .4)
          (theatre-tween "警灯红" 'opacity 0 .4)
          (theatre-tween "警灯蓝" 'opacity .45 .4)
          (theatre-tween "警灯蓝" 'opacity 0 .4))))
    (theatre-parallel
      (theatre-tween "警灯红" 'opacity 0 .3)
      (theatre-tween "警灯蓝" 'opacity 0 .3)
      (theatre-wait .4))))
