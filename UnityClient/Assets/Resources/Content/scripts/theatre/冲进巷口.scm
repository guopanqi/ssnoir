;; 勒索信第一幕 → 第二幕：穿过人群、横向追赶、先后拐入巷口。
;; 不等对白、不逐笔搭景；横向移动背景，两人的短促起伏与前倾保持追赶节奏。
(define (冲进巷口-演出)
  (define 地面 790)
  (define 图形 '())
  (define (加入 object) (set! 图形 (append 图形 (list object))))
  (define (线 id parent color width points)
    (加入 (theatre-line id parent color width points)))
  (define (框 id parent x y w h color)
    (线 id parent color 2
      (list (list x y) (list (+ x w) y) (list (+ x w) (+ y h))
        (list x (+ y h)) (list x y))))
  (define (街屋 i)
    (if (< i 8)
      (let ((x (* i 480)) (id (number->string i)))
        (框 (string-append "楼/" id) "远街" x 250 410 540 "#33466E")
        (框 (string-append "窗/" id) "远街" (+ x 50) 360 100 130 "#536A91")
        (线 (string-append "电线/" id) "远街" "#3F5272" 1
          (list (list x 230) (list (+ x 240) 290) (list (+ x 480) 230)))
        (框 (string-append "门/" id) "街道" (+ x 160) 565 110 225 "#7F95B4")
        (线 (string-append "路面/" id) "街道" "#526A91" 2
          (list (list x 840) (list (+ x 260) 840)))
        (街屋 (+ i 1)))))
  (define (人 id asset x brightness)
    (加入 (theatre-with (theatre-group id "" x 地面) 'brightness brightness))
    (加入 (theatre-image (string-append id "立绘") id asset 0 0 308 308 "")))
  (define (跑 id)
    (theatre-loop
      (theatre-sequence
        (theatre-tween id 'y (- 地面 7) .12)
        (theatre-tween id 'y 地面 .12))))

  (加入 (theatre-group "远街" "" 0 0))
  (加入 (theatre-group "街道" "" 0 0))
  (街屋 0)
  (线 "路沿" "街道" "#5A6F9C" 1.5 '((0 790) (4400 790)))
  ;; 巷口随街道从右侧进入，最后停在 x=1250。转角墙在人物之后画，遮住拐入者。
  (加入 (theatre-group "巷口" "街道" 3450 0))
  (加入 (theatre-polygon "巷内暗处" "巷口" "#020409"
    '((0 440) (220 410) (220 790) (0 790))))
  (线 "巷内路沿" "巷口" "#293A56" 1 '((20 790) (160 705) (220 705)))
  (线 "左转角" "巷口" "#9FB4E0" 2.5 '((0 300) (0 790)))
  (加入 (theatre-focus "焦点" 830 590 .38 .68))
  ;; 黑影有前后层次；接触后退开，稍晚的尼尔再挤过同一个缺口。
  (人 "人群后左" "Portraits/Neon/路人男" 700 .28)
  (人 "人群后右" "Portraits/Neon/路人女2" 1020 .24)
  (人 "取信人" "Portraits/Neon/黑影" 850 .75)
  (人 "尼尔" "Portraits/Neon/尼尔_伸手" 480 1)
  (人 "人群前" "Portraits/Neon/路人男2" 1090 .2)
  (加入 (theatre-group "转角遮挡" "街道" 3490 0))
  (加入 (theatre-polygon "墙面" "转角遮挡" "#080A10"
    '((0 340) (390 275) (390 790) (0 790))))
  (线 "墙边" "转角遮挡" "#9FB4E0" 2.5 '((0 340) (0 790)))
  (线 "墙砖" "转角遮挡" "#33466E" 1 '((20 570) (330 525)))

  (play-theatre! (theatre-scene 1600 900 "#080A10" 图形)
    ;; 短促的接触 → 黑影让开 → 尼尔穿过；不把出手和受推压成同一拍。
    (theatre-sound "人群" "StageSounds/老街酒馆/人群哗" #f .3 .2)
    (theatre-parallel
      (theatre-tween "取信人" 'x 1010 .28)
      (theatre-tween "取信人" 'rotation 9 .18))
    (theatre-parallel
      (theatre-tween "人群后右" 'x 1140 .2)
      (theatre-tween "人群后右" 'rotation -8 .2)
      (theatre-tween "人群前" 'x 1240 .25)
      (theatre-tween "尼尔" 'x 720 .3)
      (theatre-sound "推搡" "StageSounds/老街酒馆/轻推衣料" #f .55 .3))
    (theatre-parallel
      (theatre-tween "人群后左" 'x 580 .22)
      (theatre-tween "人群后左" 'rotation -7 .22)
      (theatre-tween "尼尔" 'rotation 8 .2)
      (theatre-tween "取信人" 'x 1090 .25))
    (theatre-image-to "尼尔立绘" "Portraits/Neon/尼尔_逼近")
    ;; 背景后退快于远处楼房：保持一段看得清的距离，取信人仍在拉开。
    (theatre-during
      (theatre-parallel
        (theatre-tween "街道" 'x -2200 4.4)
        (theatre-tween "远街" 'x -900 4.4)
        (theatre-tween "尼尔" 'x 740 4.4)
        (theatre-tween "取信人" 'x 1160 4.4)
        (theatre-tween "人群后左" 'x -350 1.3)
        (theatre-tween "人群后右" 'x -200 1.5)
        (theatre-tween "人群前" 'x -320 1.1))
      (跑 "尼尔")
      (跑 "取信人")
      (theatre-sound "追赶脚步" "StageSounds/老街酒馆/脚步" #t .55 0))
    ;; 背景停下；取信人向纵深拐，跨过墙边后被实景遮挡。
    (theatre-parallel
      (theatre-tween "取信人" 'x 1320 .4)
      (theatre-tween "取信人" 'y 720 .4)
      (theatre-tween "取信人" 'scale-x .65 .4)
      (theatre-tween "取信人" 'scale-y .85 .4)
      (theatre-tween "尼尔" 'x 1150 .55 'smooth)
      (theatre-tween "尼尔" 'y 地面 .2)
      (theatre-tween "尼尔" 'rotation 0 .5)
      (theatre-sound "停步" "StageSounds/老街酒馆/脚步" #f .4 .4))
    (theatre-tween "取信人" 'opacity 0 .01)
    ;; 只留三分之一秒读清“站住、认准巷口”，不把追赶拖成等待。
    (theatre-image-to "尼尔立绘" "Portraits/Neon/尼尔_抱臂")
    (theatre-wait .33)
    (theatre-image-to "尼尔立绘" "Portraits/Neon/尼尔_背身")
    (theatre-parallel
      (theatre-tween "尼尔" 'x 1350 .55 'smooth)
      (theatre-tween "尼尔" 'y 715 .55 'smooth)
      (theatre-tween "尼尔" 'scale-x .65 .55)
      (theatre-tween "尼尔" 'scale-y .85 .55)
      (theatre-sound "入巷脚步" "StageSounds/老街酒馆/脚步" #f .5 .6))
    (theatre-tween "尼尔" 'opacity 0 .01)))
