;; 移植立绘剧场十三：莱恩的家（莱恩的住处 · 很多碎片）。
;; 立绘：尼尔 1024x1024、贝恩斯_立定 1254x1254（PIL 实测），显示尺寸沿用范本 252x308。
;; SVG 肢体/弯腰/表情动画不移植；拾物光点 #ob 用 glow 显隐；tn() 合成提示音无对应采样，省略。
;; 对白/旁白逐字照抄 HTML run() 原文，秒数一律 0.45+字数×0.105。
(define (莱恩的家-演出)
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
  ;; 吊灯闪烁：原型 fl() 的灯光抖动 + 电流声，沿用路灯下样板。
  (define (闪烁)
    (theatre-parallel
      (theatre-animate "吊灯" 'brightness
        '((0 1) (.001 .15) (.11 .15) (.111 1) (.22 1) (.221 .3)
          (.33 .3) (.331 .9) (.44 .9) (.441 .5) (.55 .5) (.551 1) (.66 1)))
      (theatre-sound "电流" "Theatre/路灯电流" #f .28 0)))

  (加入 (theatre-with (theatre-group "住处" "" 0 0) 'opacity 0))
  (线 "地面" "住处" "#5A6F9C" 1.5 '((0 790) (1600 790)) 0)
  (线 "地面沿" "住处" "#33466E" 1 '((0 806) (1600 806)) .4)
  ;; 一盏逻辑灯控制整套可见光，也控制绑定人物的照明。
  (加入 (theatre-light "吊灯" "住处" "#F3D08A" 800 550 620 0))
  (线 "灯绳" "吊灯" "#9FB4E0" 1.5 '((0 -550) (0 -400)) .3)
  (线 "灯罩" "吊灯" "#E6EEFF" 2.5 '((-28 -358) (-11 -398) (11 -398) (28 -358) (-28 -358)) .6)
  (加入 (theatre-polygon "光锥" "吊灯" "#F3D08A22"
    '((-24 -356) (24 -356) (190 240) (-190 240))))
  (加入 (theatre-with (theatre-glow "灯泡光晕" "吊灯" "#F3D08A" 0 -360 92 92) 'opacity .4))
  (加入 (theatre-glow "灯芯" "吊灯" "#FFE6A8" 0 -362 30 30))
  ;; 地面碎片：HTML 的六个多边形描线，延迟 1.4 起每片 +.12。
  (线 "碎片/1" "住处" "#7F95C4" 1.5 '((420 790) (460 760) (490 784) (420 790)) 1.4)
  (线 "碎片/2" "住处" "#7F95C4" 1.5 '((540 788) (596 752) (632 770) (572 800) (540 788)) 1.52)
  (线 "碎片/3" "住处" "#7F95C4" 1.5 '((650 790) (690 790) (690 780) (650 780) (650 790)) 1.64)
  (线 "碎片/4" "住处" "#7F95C4" 1.5 '((940 790) (970 756) (1004 776) (940 790)) 1.76)
  (线 "碎片/5" "住处" "#7F95C4" 1.5 '((1060 788) (1110 768) (1134 796) (1060 788)) 1.88)
  (线 "碎片/6" "住处" "#7F95C4" 1.5 '((1180 790) (1250 766) (1260 788) (1180 790)) 2)
  (框 "房门" "住处" 1390 480 120 310 "#E9EEF8" 2.5 1.4)
  ;; 拾物光点 #ob：HTML 在 (1004,772)，但宽立绘下尼尔（x900，半宽 126）会盖住它；
  ;; 移到身侧碎片上 (1080,772)，仍在柔光范围内，glow 显隐近似。
  (加入 (theatre-with (theatre-glow "拾物光点" "住处" "#FFD98A" 1080 772 18 18) 'opacity 0))
  (加入 (theatre-focus "焦点" 800 576 .34 .62))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#9DB0E0" 800 560 440 560) 'opacity 0))
  (加入 (theatre-with (theatre-group "尼尔位置" "" -150 790) 'opacity 0))
  (加入 (theatre-image "尼尔" "尼尔位置" "Portraits/Neon/尼尔_逼近" 0 0 308 308 "吊灯"))
  (加入 (theatre-with
    (theatre-with (theatre-group "助手位置" "" 1320 790) 'scale-x -1) 'opacity 0))
  (加入 (theatre-image "助手" "助手位置" "Portraits/Neon/贝恩斯_立定" 0 0 308 308 "吊灯"))

  (play-theatre! (theatre-scene 1600 900 "#090A0D" 图形)
    ;; 建屋收尾和两人进场重叠：大结构画出后尼尔就开走，不等最后一笔。
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "住处" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait .5)
        (theatre-parallel
          (theatre-sequence
            (闪烁)
            (theatre-tween "助手位置" 'opacity 1 .7)
            (theatre-tween "尼尔位置" 'opacity 1 .05))
          (theatre-sequence
            (theatre-tween "尼尔位置" 'x 420 2.6 'smooth)
            (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 -.2)
            (theatre-wait 2.8)))))
    ;; （你去到莱恩的家，这里很多碎片。）
    (theatre-caption-for "旁白" "（你去到莱恩的家，这里很多碎片。）" 2.235 "#C9CFE2")
    (theatre-wait .9)
    ;; 尼尔逐处查看碎片：走位 + 停顿，合成提示音省略。
    (theatre-tween "尼尔位置" 'scale-x 1 .1)
    (theatre-parallel
      (theatre-tween "尼尔位置" 'x 560 1.2 'smooth)
      (theatre-wait 1.3))
    (theatre-wait 1.5)
    (theatre-parallel
      (theatre-tween "尼尔位置" 'x 700 1.2 'smooth)
      (theatre-wait 1.3))
    (theatre-wait 1.3)
    ;; （你会进行探索，分析。）
    (theatre-caption-for "旁白" "（你会进行探索，分析。）" 1.710 "#C9CFE2")
    (theatre-parallel
      (theatre-tween "尼尔位置" 'x 900 1.8 'smooth)
      (theatre-wait 1.9))
    (theatre-image-to "尼尔" "Portraits/Neon/尼尔_抱臂")
    (theatre-wait .9)
    ;; 其中一件东西发亮，尼尔拿了起来：光点显隐 + 柔光聚拢。
    (theatre-parallel
      (theatre-tween "拾物光点" 'opacity 1 .4)
      (theatre-wait 1.3))
    (theatre-wait .9)
    (theatre-parallel
      (theatre-tween "拾物光点" 'opacity 0 .4)
      (theatre-tween "说话柔光" 'x 900 1 'smooth)
      (theatre-tween "说话柔光" 'opacity .3 1)
      (theatre-wait 1.2))
    ;; （其中一个东西，你拿了起来。）
    (theatre-caption-for "旁白" "（其中一个东西，你拿了起来。）" 2.025 "#C9CFE2")
    (theatre-tween "吊灯" 'brightness .5 .4)
    (theatre-wait 1)))
