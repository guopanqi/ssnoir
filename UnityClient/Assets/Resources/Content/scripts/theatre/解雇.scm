;; 移植立绘剧场“十六·经理·解雇”（剧院 · 经理找尼尔谈调查进展）。
;; 保留布景坐标、延迟、顺序和对白原文；SVG 肢体/表情动画不移植，只换立绘与走位。
;; 砸门（HTML 的 fxf 闪白 + shk 抖动）用门扇缩放抖动 + 吊灯短暂闪光 + 门锁声近似。
(define (解雇-演出)
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
  (define (填 id parent x y w h color . opacity)
    (加入 (theatre-with (theatre-polygon id parent color
      (list (list x y) (list (+ x w) y) (list (+ x w) (+ y h)) (list x (+ y h))))
      'opacity (if (null? opacity) 1 (car opacity)))))
  (define (说 who x text seconds)
    (theatre-during
      (theatre-caption-for who text seconds
        (if (equal? who "尼尔") "#F0CF8A" "#F4907C"))
      (theatre-parallel
        (theatre-tween "焦点" 'x (+ 800 (* (- x 800) .5)) 1 'smooth)
        (theatre-tween "说话柔光" 'x x 1.2 'smooth)
        (theatre-tween "说话柔光" 'opacity .13 1.2 'smooth))))

  (加入 (theatre-with (theatre-group "布景" "" 0 0) 'opacity 0))
  (线 "地面" "布景" "#5A6F9C" 1.5 '((0 790) (1600 790)) 0)
  (线 "地沿" "布景" "#33466E" 1 '((0 806) (1600 806)) .4)
  (线 "吊线" "布景" "#9FB4E0" 1.5 '((620 0) (620 150)) .3)
  (线 "灯罩" "布景" "#E6EEFF" 2.5
    '((592 192) (609 152) (631 152) (648 192) (592 192)) .6)
  (加入 (theatre-light "吊灯" "" "#F3D08A" 620 190 620 0))
  (加入 (theatre-polygon "光锥" "吊灯" "#F3D08A18"
    '((-24 4) (24 4) (190 600) (-190 600))))
  (加入 (theatre-glow "灯下光池" "吊灯" "#F3D08A66" 0 600 420 50))
  (加入 (theatre-with (theatre-glow "灯泡光晕" "吊灯" "#F3D08A" 0 0 92 92) 'opacity .4))
  (加入 (theatre-glow "灯芯" "吊灯" "#FFE6A8" 0 -2 30 30))
  ;; 大门 DOOR2：外框 + 门内光 + 可缩放门扇（门轴在左）。
  (框 "门框" "布景" 1330 250 250 540 "#B8923F" 3 .6)
  (填 "门内光" "布景" 1344 264 222 512 "#CFD8EA" 0)
  (加入 (theatre-group "门扇" "布景" 1344 264))
  (填 "门板" "门扇" 0 0 222 512 "#04060C")
  (框 "门边" "门扇" 0 0 222 512 "#E9D9B0" 2.5 .9)
  (框 "门格上" "门扇" 26 30 170 190 "#B8923F" 1.5 1.2)
  (框 "门格下" "门扇" 26 256 170 220 "#B8923F" 1.5 1.3)
  (线 "门把手" "门扇" "#D8B252" 2.5 (theatre-ellipse-points 200 276 10 10) 1.3)
  (加入 (theatre-focus "焦点" 800 576 .20 .52))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#8FB4E8" 860 560 600 840) 'opacity 0))
  ;; 垫板 PADP：前景道具，保持 -8 度倾斜，随 mvp 移动与显隐。
  (加入 (theatre-with (theatre-group "垫板" "" 800 470) 'opacity 0))
  (加入 (theatre-with (theatre-group "垫板纸" "垫板" 0 0) 'rotation -8))
  (填 "垫板身" "垫板纸" -26 -34 52 68 "#D8CFB4")
  (线 "垫板线一" "垫板纸" "#6B6350" 3 '((-14 -16) (14 -16)) 0)
  (线 "垫板线二" "垫板纸" "#6B6350" 3 '((-14 -4) (14 -4)) 0)
  (线 "垫板线三" "垫板纸" "#6B6350" 3 '((-14 8) (6 8)) 0)
  (加入 (theatre-with
    (theatre-image "经理" "" "Portraits/Neon/经理" 540 790 308 308 "吊灯") 'opacity 0))
  (加入 (theatre-with
    (theatre-with
      (theatre-image "尼尔" "" "Portraits/Neon/尼尔_抱臂" 1050 790 308 308 "吊灯") 'scale-x -1)
    'opacity 0))

  (play-theatre! (theatre-scene 1600 900 "#1B1015" 图形)
    ;; 建屋收尾和进场重叠：两人开场即在场。
    (theatre-parallel
      (apply theatre-parallel
        (cons (theatre-tween "布景" 'opacity 1 1.5)
          (append 描绘
            (list (theatre-animate "吊灯" 'brightness
              '((0 0) (2.2 0) (2.201 .15) (2.31 .15) (2.311 1) (2.42 1)
                (2.421 .3) (2.53 .3) (2.531 1) (3.5 1)))))))
      (theatre-sequence
        (theatre-wait .5)
        (theatre-parallel
          (theatre-tween "经理" 'opacity 1 .6)
          (theatre-tween "尼尔" 'opacity 1 .6)
          (theatre-wait 1.2))))
    (theatre-parallel
      (theatre-tween "经理" 'x 620 .9 'smooth)
      (说 "经理" 620 "尼尔，我们必须得好好谈谈了。" 1.920))
    (theatre-clear-caption)
    (theatre-wait 1.8)
    (说 "经理" 620 "到目前为止已经有几天了，事情有什么重要的进展吗？" 2.970)
    (theatre-wait 2.3)
    (说 "经理" 620 "我只针对事情，不针对人。这是一份工作，我们每个人都在自己的岗位上，为这件事出一份力。" 4.860)
    (说 "经理" 620 "就拿我来说，我每天都在为赞助和演出奔走，如果我没能做得很好，我自己就不配呆在这个岗位上，我就要开除我自己。" 6.015)
    (theatre-clear-caption)
    (theatre-wait 1.5)
    (theatre-parallel
      (theatre-tween "经理" 'x 670 .7 'smooth)
      (说 "经理" 670 "如果我们不能为这件事情的解决作出贡献，那我们在这儿不是浪费时间吗？" 3.915))
    (theatre-clear-caption)
    (theatre-wait 2)
    (说 "经理" 670 "尼尔，我其实很不情愿，但是这件事情你可以先放一放了，我可能会找一个更老练的侦探。但也别那么绝情，别断了联系。" 6.120)
    (theatre-clear-caption)
    (theatre-wait 2.4)
    ;; 垫板传递（HTML 的 mvp）：尼尔上前接过，经理接回，垫板收走。
    (theatre-parallel
      (theatre-tween "尼尔" 'x 900 .9 'smooth)
      (theatre-tween "垫板" 'x 790 .7 'smooth)
      (theatre-tween "垫板" 'opacity 1 .4)
      (theatre-wait .9))
    (theatre-parallel
      (theatre-tween "经理" 'x 640 .7 'smooth)
      (theatre-wait .7))
    (theatre-parallel
      (theatre-tween "垫板" 'x 740 .6 'smooth)
      (theatre-tween "垫板" 'opacity 0 .4)
      (theatre-sound "递纸" "StageSounds/老街酒馆/拿布" #f .5 0)
      (theatre-wait .6))
    (theatre-wait .9)
    ;; 尼尔离场：换侧身姿势走向大门，门扇绕左轴收窄打开。
    (theatre-image-to "尼尔" "Portraits/Neon/尼尔_侧身退")
    (theatre-parallel
      (theatre-tween "尼尔" 'x 1500 3.6 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 0)
      (theatre-sequence
        (theatre-wait 1.2)
        (theatre-parallel
          (theatre-tween "门内光" 'opacity .9 .4)
          (theatre-tween "门扇" 'scale-x .1 .8 'smooth))
        (theatre-wait 2.4))
      (theatre-wait 3.6))
    (theatre-parallel
      (theatre-tween "尼尔" 'opacity 0 .5)
      (theatre-wait .9))
    ;; 砸门：门扇猛地合上 + 短暂闪光与抖动。
    (theatre-parallel
      (theatre-tween "门扇" 'scale-x 1 .15)
      (theatre-tween "门内光" 'opacity 0 .15)
      (theatre-animate "门扇" 'x '((0 1344) (.05 1350) (.1 1339) (.15 1347) (.25 1344)))
      (theatre-animate "吊灯" 'brightness '((0 1.3) (.05 1.3) (.25 1)))
      (theatre-sound "砸门" "StageSounds/雨夜求助/门锁" #f .8 0)
      (theatre-wait 1.4))
    (theatre-wait 1.3)
    (theatre-wait 2)))
