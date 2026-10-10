;; 移植立绘剧场六·巷口（老街 · 听到“莱恩”这个名字）。
;; 布景沿用 HTML SETS.sign（alley + 路牌杆与三块路牌）；
;; whisp 街头浮字转成旁白自动字幕按顺序播；SVG 肢体/表情动画不移植。
;; alley 布景没有灯具，HTML 的 fl()/lamp() 在此无视觉目标，故略去，只保留描绘进场。
(define (巷口-演出)
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
  (加入 (theatre-focus "焦点" 800 576 .34 .62))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#9DB0E0" 800 560 440 560) 'opacity 0))
  ;; 立绘文件都是方形像素（1024），显示框沿用雨夜来访/路灯下范本比例。
  (人物 "尼尔" "Portraits/Neon/尼尔_抱臂" 308 308 -150 #f)
  (人物 "夜莺" "Portraits/Neon/夜莺_低头" 297 297 1760 #t)
  (人物 "街坊" "Portraits/Neon/路人男" 308 308 1430 #t)

  (play-theatre! (theatre-scene 1600 900 "#080A0E" 图形)
    ;; 建牌收尾和两人进场重叠：大结构画出后他们就开走，不等最后一笔。
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "布景" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait 2)
        (现身 "尼尔")
        (现身 "夜莺")
        (现身 "街坊")
        (theatre-parallel
          (走 "尼尔位置" 520 2600 -.2 "巷口1")
          (走 "夜莺位置" 1060 2800 .2 "巷口2"))))
    (旁白 "（夜莺换了一身衣服，回到老街。）" 2.130)
    ;; HTML 里两句 whisp 重叠飘；Unity 没有文字图元，转成旁白按顺序播，配一声人群哗。
    (theatre-sound "街语" "StageSounds/老街酒馆/人群哗" #f .22 0)
    (旁白 "……莱恩？" 0.975)
    (旁白 "莱恩那小子……" 1.185)
    (theatre-wait 2.4)
    (theatre-tween "街坊" 'scale-x 1 .3)
    (theatre-wait 1.4)
    (说 "尼尔" 520 "莱恩？你们认识？" 1.290)
    (theatre-wait 1)
    (说 "夜莺" 1060 "……我们回去再说" 1.290)
    (说 "尼尔" 520 "他和这件事情有关？" 1.395)
    (说 "夜莺" 1060 "我不想在这儿说" 1.185)
    (说 "尼尔" 520 "那就这样吧" 0.975)
    (说 "夜莺" 1060 "尼尔……" 0.870)
    (theatre-clear-caption)
    ;; 两人先后离场：尼尔先走，2.4 秒后夜莺跟上；收尾压暗（HTML lamp(.5) 无灯具可调）。
    (theatre-tween "尼尔" 'scale-x -1 .3)
    (theatre-parallel
      (走 "尼尔位置" -200 4200 -.2 "巷口3")
      (theatre-sequence
        (theatre-wait 2.4)
        (走 "夜莺位置" -200 4600 -.1 "巷口4")))
    (theatre-parallel
      (theatre-tween "巷灯" 'brightness .5 .5)
      (theatre-tween "布景" 'opacity .6 .5)
      (theatre-wait 1.2))))
