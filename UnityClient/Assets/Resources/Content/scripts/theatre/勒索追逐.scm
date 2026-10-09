;; 改编网页第四场“追逐”：取信、报纸落下、向左追赶、先后入巷。
;; 空间与声音沿用参考；删掉漫长等待和浮字，跑步为两张立绘交替。
(load-file "scripts/theatre/勒索街角.scm")
(define (勒索追逐-演出)
  (define 图形 (勒索街角-布景))
  (define (加入 o) (set! 图形 (append 图形 (list o))))
  (define (线 id parent color width points) (加入 (theatre-line id parent color width points)))
  (define (框 id parent x y w h color)
    (线 id parent color 2 (list (list x y) (list (+ x w) y) (list (+ x w) (+ y h))
      (list x (+ y h)) (list x y))))
  (define (剪影 id parent x y)
    (加入 (theatre-group id parent x y))
    ;; 分成凸体块填黑；近景擦身时真正遮住人物，不只是叠一圈轮廓线。
    (加入 (theatre-polygon (string-append id "/头") id "#05070D"
      '((-12 -210) (12 -210) (20 -195) (17 -175) (-17 -175) (-20 -195))))
    (加入 (theatre-polygon (string-append id "/身") id "#05070D"
      '((-17 -175) (17 -175) (26 -80) (-26 -80))))
    (加入 (theatre-polygon (string-append id "/左臂") id "#05070D"
      '((-17 -163) (-28 -158) (-34 -85) (-24 -80))))
    (加入 (theatre-polygon (string-append id "/右臂") id "#05070D"
      '((17 -163) (28 -158) (34 -85) (24 -80))))
    (加入 (theatre-polygon (string-append id "/左腿") id "#05070D"
      '((-24 -80) (-3 -80) (-8 0) (-20 0))))
    (加入 (theatre-polygon (string-append id "/右腿") id "#05070D"
      '((24 -80) (3 -80) (8 0) (20 0))))
    (线 (string-append id "/轮廓") id "#4A5F8C" 1.5
      '((-12 -210) (12 -210) (20 -195) (17 -175) (30 -155) (34 -85)
        (24 -80) (20 0) (8 0) (0 -60) (-8 0) (-20 0) (-24 -80)
        (-34 -85) (-30 -155) (-17 -175) (-20 -195) (-12 -210))))
  (define (街景 i)
    (if (< i 24)
      (let ((x (- (* i 160) 1600)) (tag (number->string i)))
        (框 (string-append "远楼/" tag) "远街" x (+ 350 (* (modulo i 3) 70)) 120 (- 440 (* (modulo i 3) 70)) "#2A3A60")
        (线 (string-append "路刻/" tag) "路面" "#46608F" 3 (list (list x 850) (list (+ x 75) 850)))
        (if (= (modulo i 4) 0)
          (begin
            (线 (string-append "街灯/" tag) "中街" "#3B4A70" 4 (list (list x 790) (list x 300)))
            (加入 (theatre-glow (string-append "灯芯/" tag) "中街" "#FFE6A8" x 300 18 18))))
        (剪影 (string-append "路人/" tag) "中街" (+ x 50) 790)
        (街景 (+ i 1)))))
  (define (卷层 id seconds)
    (theatre-loop (theatre-sequence
      (theatre-tween id 'x 1600 seconds)
      (theatre-tween id 'x 0 .001))))
  (define (跑 id actor)
    (theatre-loop (theatre-sequence
      (theatre-image-to id (string-append "Portraits/Chase/" actor "_跑步甲"))
      (theatre-sound (string-append id "/脚步") "StageSounds/勒索信/脚步" #f .75 0)
      (theatre-wait .12)
      (theatre-image-to id (string-append "Portraits/Chase/" actor "_跑步乙"))
      (theatre-wait .12))))
  (define (擦身 id from to seconds)
    (theatre-sequence
      (theatre-tween id 'x from .001)
      (theatre-parallel
        (theatre-tween id 'x to seconds)
        (theatre-sound (string-append id "/风声") "StageSounds/勒索信/擦身" #f .6 0))))

  (加入 (theatre-with (theatre-group "卷轴" "" 0 0) 'opacity 0))
  (加入 (theatre-group "远街" "卷轴" 0 0))
  (加入 (theatre-group "中街" "卷轴" 0 0))
  (加入 (theatre-group "路面" "卷轴" 0 0))
  (街景 0)
  (线 "追逐地面" "卷轴" "#5A6F9C" 1.5 '((-300 790) (1900 790)))
  (加入 (theatre-with (theatre-group "巷口" "" -1800 0) 'opacity 0))
  (框 "巷墙" "巷口" -100 190 980 600 "#7F95C4")
  (加入 (theatre-polygon "巷门暗处" "巷口" "#000000" '((330 320) (470 320) (470 790) (330 790))))
  (框 "巷门" "巷口" 330 320 140 470 "#9FB4E0")
  (框 "巷窗左" "巷口" 170 260 70 90 "#6F86B8")
  (框 "巷窗右" "巷口" 560 260 70 90 "#6F86B8")
  (加入 (theatre-with (theatre-group "取信人位置" "" 640 790) 'brightness 2.4))
  (加入 (theatre-image "取信人" "取信人位置" "Portraits/Neon/黑影" 0 0 308 308 ""))
  (加入 (theatre-with (theatre-group "尼尔位置" "" 1070 790) 'brightness 1.3))
  (加入 (theatre-with (theatre-image "尼尔" "尼尔位置" "Portraits/Neon/尼尔_靠墙" 0 0 308 308 "") 'scale-x -1))
  (set! 图形 (append 图形 (勒索街角-报纸)))
  (加入 (theatre-group "信封" "" 700 596))
  (加入 (theatre-polygon "信封纸" "信封" "#E8E2D2" '((-15 -10) (15 -10) (15 10) (-15 10))))
  ;; 两个近景黑影在主角之后画，短暂遮挡形成擦肩和推挤，避免持续盖住尼尔。
  (剪影 "近影甲" "" -400 830)
  (剪影 "近影乙" "" 1900 830)

  (play-theatre! (theatre-scene 1600 900 "#07090E" 图形)
    (theatre-tween "报纸" 'opacity 1 .001)
    (theatre-parallel
      (theatre-tween "信封" 'x 660 .24)
      (theatre-tween "信封" 'y 650 .24)
      (theatre-sound "取信" "StageSounds/勒索信/纸响" #f .7 -.15))
    (theatre-parallel
      (theatre-tween "报纸" 'y 830 .18)
      (theatre-tween "报纸" 'opacity 0 .18)
      (theatre-tween "信封" 'opacity 0 .18)
      (theatre-sound "起身" "StageSounds/勒索信/起身" #f .6 .4))
    (theatre-image-to "尼尔" "Portraits/Chase/尼尔_跑步甲")
    (theatre-image-to "取信人" "Portraits/Chase/取信人_跑步甲")
    ;; 快速进入卷轴：尼尔从第一拍就存在，跑步不靠倾斜站姿冒充。
    (theatre-during
      (theatre-parallel
        (theatre-tween "布景" 'x 1900 .55 'smooth)
        (theatre-tween "卷轴" 'opacity 1 .3)
        (theatre-tween "取信人位置" 'x 520 .6)
        (theatre-tween "尼尔位置" 'x 900 .6))
      (跑 "尼尔" "尼尔") (跑 "取信人" "取信人"))
    (theatre-during
      (theatre-parallel
        ;; 对手略拉开，尼尔一度被擦身的人影逼退再追回，距离变化就是这一段的戏。
        (theatre-tween "取信人位置" 'x 490 4.2)
        (theatre-animate "尼尔位置" 'x '((0 900) (1.2 800) (1.48 875) (2.2 790) (4.2 740)))
        (theatre-sequence
          (擦身 "近影甲" -300 1900 .5)
          (theatre-wait .6)
          (擦身 "近影乙" 1900 -300 .45)
          (theatre-wait .75)
          (擦身 "近影甲" -300 1900 .4)))
      (卷层 "远街" 6.5) (卷层 "中街" 2.8) (卷层 "路面" 1.1)
      (跑 "尼尔" "尼尔")
      (theatre-sequence (theatre-wait .06) (跑 "取信人" "取信人")))
    ;; 巷口迅速滑入；双方仍跑着，不先停一大段再让人入巷。
    (theatre-during
      (theatre-parallel
        (theatre-tween "巷口" 'opacity 1 .1)
        (theatre-tween "巷口" 'x 0 .65 'smooth)
        (theatre-tween "卷轴" 'opacity .3 .65)
        (theatre-tween "取信人位置" 'x 400 .65)
        (theatre-tween "尼尔位置" 'x 620 .65))
      (跑 "尼尔" "尼尔") (跑 "取信人" "取信人"))
    (theatre-parallel
      (theatre-tween "取信人位置" 'y 748 .32)
      (theatre-tween "取信人位置" 'scale-x .56 .32)
      (theatre-tween "取信人位置" 'scale-y .56 .32)
      (theatre-tween "取信人位置" 'opacity 0 .32)
      (theatre-tween "尼尔位置" 'x 590 .4 'smooth))
    (theatre-image-to "尼尔" "Portraits/Neon/尼尔_抱臂")
    (theatre-wait .3)
    (theatre-image-to "尼尔" "Portraits/Neon/尼尔_背身")
    (theatre-parallel
      (theatre-tween "尼尔位置" 'x 400 .45 'smooth)
      (theatre-sound "跟入脚步" "StageSounds/勒索信/脚步" #f .65 -.3))
    (theatre-parallel
      (theatre-tween "尼尔位置" 'y 748 .3)
      (theatre-tween "尼尔位置" 'scale-x .56 .3)
      (theatre-tween "尼尔位置" 'scale-y .56 .3)
      (theatre-tween "尼尔位置" 'opacity 0 .3))))
