;; 移植立绘剧场十四：剧院的安全（封堵漏洞，筹备首演）。
;; 立绘 1024x1024（PIL 实测），显示尺寸沿用范本 252x308。
;; SVG 肢体动画不移植；钉木板 #plk 用三线条分段显现；门锁变红用绿/红双圆
;; opacity 切换近似（theatre 颜色不能 tween）；透光缝 #gap 用 polygon 显隐；
;; EXIT 铜字没有文字图元，用线条描出 E-X-I-T；bs()/tn() 合成音无对应采样，
;; 钉木板就近取用门锁短音，sawtooth 警示音省略。
;; 对白/旁白逐字照抄 HTML run() 原文，秒数一律 0.45+字数×0.105。
(define (剧院安全-演出)
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
  (define (闪烁)
    (theatre-parallel
      (theatre-animate "吊灯" 'brightness
        '((0 1) (.001 .15) (.11 .15) (.111 1) (.22 1) (.221 .3)
          (.33 .3) (.331 .9) (.44 .9) (.441 .5) (.55 .5) (.551 1) (.66 1)))
      (theatre-sound "电流" "Theatre/路灯电流" #f .28 0)))

  (加入 (theatre-with (theatre-group "后台" "" 0 0) 'opacity 0))
  (线 "地面" "后台" "#5A6F9C" 1.5 '((0 790) (1600 790)) 0)
  (线 "地面沿" "后台" "#33466E" 1 '((0 806) (1600 806)) .4)
  (加入 (theatre-light "吊灯" "后台" "#F3D08A" 1000 550 620 0))
  (线 "灯绳" "吊灯" "#9FB4E0" 1.5 '((0 -550) (0 -400)) .3)
  (线 "灯罩" "吊灯" "#E6EEFF" 2.5 '((-28 -358) (-11 -398) (11 -398) (28 -358) (-28 -358)) .6)
  (加入 (theatre-polygon "光锥" "吊灯" "#F3D08A22"
    '((-24 -356) (24 -356) (190 240) (-190 240))))
  (加入 (theatre-with (theatre-glow "灯泡光晕" "吊灯" "#F3D08A" 0 -360 92 92) 'opacity .4))
  (加入 (theatre-glow "灯芯" "吊灯" "#FFE6A8" 0 -362 30 30))
  ;; 安全门与 EXIT 牌：铜字用线条描出。
  (框 "安全门" "后台" 260 470 150 320 "#E9EEF8" 2.5 1)
  (框 "出口牌" "后台" 290 420 90 36 "#7AD4A0" 2 1.3)
  (线 "字E竖" "后台" "#7AD4A0" 2 '((300 424) (300 452)) 1.3)
  (线 "字E横" "后台" "#7AD4A0" 2 '((300 424) (318 424) (300 424) (300 438) (316 438) (300 438) (300 452) (318 452)) 1.3)
  (线 "字X" "后台" "#7AD4A0" 2 '((324 424) (340 452) (340 424) (324 452)) 1.3)
  (线 "字I" "后台" "#7AD4A0" 2 '((340 424) (352 424) (346 424) (346 452) (340 452) (352 452)) 1.3)
  (线 "字T" "后台" "#7AD4A0" 2 '((358 424) (374 424) (366 424) (366 452)) 1.3)
  ;; 门锁 #lk：绿/红双圆，opacity 切换近似换色。
  (加入 (theatre-glow "锁绿" "后台" "#6AD59A" 396 610 16 16))
  (加入 (theatre-with (theatre-glow "锁红" "后台" "#E0584A" 396 610 16 16) 'opacity 0))
  ;; 透光缝 #gap：门缝暖光。
  (加入 (theatre-with (theatre-polygon "透光缝" "后台" "#F2C36E"
    '((404 476) (410 476) (410 784) (404 784))) 'opacity 0))
  ;; 木板 #plk：三线条分段显现。
  (加入 (theatre-with (theatre-group "木板" "后台" 0 0) 'opacity 0))
  (加入 (theatre-with (theatre-line "木条/1" "木板" "#CFD6E6" 10 '((270 500) (400 560))) 'reveal 0))
  (加入 (theatre-with (theatre-line "木条/2" "木板" "#CFD6E6" 10 '((270 600) (400 660))) 'reveal 0))
  (加入 (theatre-with (theatre-line "木条/3" "木板" "#CFD6E6" 10 '((270 560) (400 500))) 'reveal 0))
  (加入 (theatre-focus "焦点" 800 576 .34 .62))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#9DB0E0" 800 560 440 560) 'opacity 0))
  (加入 (theatre-with (theatre-group "尼尔位置" "" -150 790) 'opacity 0))
  (加入 (theatre-image "尼尔" "尼尔位置" "Portraits/Neon/尼尔_逼近" 0 0 308 308 "吊灯"))

  (play-theatre! (theatre-scene 1600 900 "#08090D" 图形)
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "后台" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait .5)
        (theatre-parallel
          (theatre-sequence
            (闪烁)
            (theatre-tween "尼尔位置" 'opacity 1 .05))
          (theatre-sequence
            (theatre-tween "尼尔位置" 'x 900 2.6 'smooth)
            (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 -.2)
            (theatre-wait 2.8)))))
    ;; （调查剧院的安全。封堵漏洞，筹备。）
    (theatre-caption-for "旁白" "（调查剧院的安全。封堵漏洞，筹备。）" 2.340 "#C9CFE2")
    ;; 尼尔走到安全门前，钉上木板：三线条分段显现。
    (theatre-tween "尼尔位置" 'scale-x -1 .1)
    (theatre-parallel
      (theatre-tween "尼尔位置" 'x 540 2 'smooth)
      (theatre-wait 2.2))
    (theatre-wait .9)
    (theatre-parallel
      (theatre-tween "木条/1" 'reveal 1 .45)
      (theatre-tween "木板" 'opacity .35 .45)
      (theatre-sound "钉木1" "StageSounds/雨夜求助/门锁" #f .6 -.3)
      (theatre-wait .45))
    (theatre-parallel
      (theatre-tween "木条/2" 'reveal 1 .45)
      (theatre-tween "木板" 'opacity .65 .45)
      (theatre-sound "钉木2" "StageSounds/雨夜求助/门锁" #f .6 -.3)
      (theatre-wait .45))
    (theatre-parallel
      (theatre-tween "木条/3" 'reveal 1 .45)
      (theatre-tween "木板" 'opacity 1 .45)
      (theatre-sound "钉木3" "StageSounds/雨夜求助/门锁" #f .6 -.3)
      (theatre-wait .45))
    (theatre-image-to "尼尔" "Portraits/Neon/尼尔_抱臂")
    (theatre-wait .9)
    (theatre-caption-for "尼尔（独白）" "莱恩这种头脑的人，根本就不会在这里下功夫。" 2.655 "#F0CF8A")
    ;; 尼尔离场；后来却发现还是出了问题：木板被挪开、门锁变红、门缝透光。
    (theatre-clear-caption)
    (theatre-parallel
      (theatre-tween "尼尔位置" 'scale-x 1 .1)
      (theatre-tween "尼尔位置" 'x 1500 3.2 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 .3)
      (theatre-wait 3))
    (theatre-parallel
      (theatre-tween "木板" 'opacity .2 .4)
      (theatre-tween "锁红" 'opacity 1 .3)
      (theatre-tween "锁绿" 'opacity 0 .3)
      (theatre-tween "透光缝" 'opacity .9 .6)
      (theatre-wait .5))
    (闪烁)
    (theatre-tween "吊灯" 'brightness .7 .2)
    (theatre-wait .9)
    ;; （但是后来你发现，还是出了问题。）
    (theatre-caption-for "旁白" "（但是后来你发现，还是出了问题。）" 2.235 "#C9CFE2")
    (theatre-wait .8)))
