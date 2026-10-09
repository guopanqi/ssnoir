;; 第一幕辨认成功 → 第二幕巷口。无字幕、无点击等待，动作净时长约 8 秒，含舞台淡入淡出约 9 秒。
;; 人偶疾跑用前倾、短促起伏与间距变化；街面和远窗分速后退。
(define (勒索信追逐-演出)
  (define 图形 '())
  (define (加入 o) (set! 图形 (append 图形 (list o))))
  (define (线 id parent color width points)
    (加入 (theatre-line id parent color width points)))
  (define (框 id parent x y w h)
    (线 id parent "#536780" 2
      (list (list x y) (list (+ x w) y) (list (+ x w) (+ y h))
            (list x (+ y h)) (list x y))))
  (define (人物 id asset parent x y opacity)
    (加入 (theatre-group id parent x y))
    (加入 (theatre-with
      (theatre-image (string-append id "像") id asset 0 0 308 308 "")
      'opacity opacity)))
  (define (跑 id 秒)
    (theatre-during (theatre-wait 秒)
      (theatre-loop (theatre-sequence
        (theatre-tween id 'y 780 .10)
        (theatre-tween id 'y 790 .10)))
      (theatre-loop (theatre-sequence
        (theatre-tween id 'rotation 7 .10)
        (theatre-tween id 'rotation 11 .10)))))
  (define (拐入 id)
    (theatre-parallel
      (theatre-tween id 'x 1135 .38)
      (theatre-tween id 'y 728 .38)
      (theatre-tween id 'scale-x .38 .38)
      (theatre-tween id 'scale-y .72 .38)
      (theatre-tween id 'opacity 0 .38)))

  ;; 长街一次滚过，不循环传送建筑；巷口跟着街面抵达人物前方。
  (加入 (theatre-group "远街" "" 0 0))
  (define (远窗 n)
    (if (< n 12)
      (begin
        (框 (string-append "窗" (number->string n)) "远街" (+ 120 (* n 330)) 330 75 115)
        (远窗 (+ n 1)))))
  (远窗 0)
  (加入 (theatre-group "街面" "" 0 0))
  (线 "路沿" "街面" "#677D96" 2 '((-1500 800) (5500 800)))
  (define (街段 n)
    (if (< n 8)
      (let ((x (* n 520)) (id (number->string n)))
        (线 (string-append "墙缝" id) "街面" "#64758C" 2
          (list (list x 160) (list x 790)))
        (框 (string-append "门" id) "街面" (+ x 70) 555 105 235)
        (线 (string-append "路痕" id) "街面" "#34475B" 2
          (list (list (+ x 30) 843) (list (+ x 290) 843)))
        (街段 (+ n 1)))))
  (街段 0)
  (加入 (theatre-group "巷口" "街面" 3410 0))
  (加入 (theatre-polygon "巷内" "巷口" "#020307"
    '((-95 170) (115 170) (115 790) (-95 790))))
  (线 "巷左壁" "巷口" "#8093AA" 3 '((-95 170) (-95 790) (-40 735)))
  (线 "巷右壁" "巷口" "#8093AA" 3 '((115 170) (115 790) (40 735)))
  (线 "巷深" "巷口" "#263448" 1.5 '((-40 400) (-40 735) (40 735) (40 400)))
  (加入 (theatre-group "人群" "" 0 0))
  (人物 "后影" "Portraits/Neon/黑影" "人群" 740 775 .28)
  (人物 "后影二" "Portraits/Neon/路人男2" "人群" 440 775 .25)
  (人物 "逃者" "Portraits/Neon/黑影" "" 760 790 .85)
  (人物 "尼尔" "Portraits/Neon/尼尔_逼近" "" 460 790 1)
  (人物 "前影" "Portraits/Neon/路人男" "人群" 850 805 .48)
  (人物 "前影二" "Portraits/Neon/路人女" "人群" 580 815 .40)

  (play-theatre! (theatre-scene 1600 900 "#080A0E" 图形)
    ;; 开场已经挤在人群里，0.6 秒内见到推搡，随后立即冲出。
    (theatre-sound "人群声" "StageSounds/老街酒馆/人群哗" #f .38 0)
    (theatre-parallel
      (theatre-tween "逃者" 'x 850 .28)
      (theatre-tween "逃者" 'rotation 13 .28)
      (theatre-sequence (theatre-wait .18)
        (theatre-sound "推搡" "StageSounds/老街酒馆/轻推衣料" #f .7 .2)
        (theatre-parallel
          (theatre-tween "前影" 'x 930 .25)
          (theatre-tween "前影" 'rotation 15 .25)))
      (theatre-sequence (theatre-wait .32)
        (theatre-parallel
          (theatre-tween "尼尔" 'x 610 .30)
          (theatre-tween "前影二" 'x 510 .30)
          (theatre-tween "前影二" 'rotation -12 .30))))
    (theatre-sound "疾步" "StageSounds/老街酒馆/脚步" #t .65 0)
    ;; 5.4 秒疾追：景物向左掠过，两个人的距离先缩后拉，再逼近。
    (theatre-parallel
      (theatre-tween "街面" 'x -2280 5.4)
      (theatre-tween "远街" 'x -760 5.4)
      (theatre-tween "人群" 'x -1800 2.1)
      (theatre-animate "逃者" 'x '((0 850) (1.8 980) (3.2 900) (5.4 1050)))
      (theatre-animate "尼尔" 'x '((0 610) (1.8 820) (3.2 650) (5.4 890)))
      (跑 "逃者" 5.4)
      (跑 "尼尔" 5.4))
    ;; 先跑的人不犹豫；尼尔刹住，看一眼才拐，追逐没有结束。
    (拐入 "逃者")
    (theatre-parallel
      (theatre-tween "尼尔" 'x 1020 .36 'smooth)
      (theatre-tween "尼尔" 'y 790 .12)
      (theatre-tween "尼尔" 'rotation -7 .18))
    (theatre-stop-sound "疾步")
    (theatre-tween "尼尔" 'rotation 0 .16)
    (theatre-image-to "尼尔像" "Portraits/Neon/尼尔")
    (theatre-wait .32)
    (theatre-sound "追入" "StageSounds/老街酒馆/脚步" #f .5 .35)
    (theatre-parallel
      (theatre-tween "尼尔" 'x 1080 .24)
      (theatre-tween "尼尔" 'rotation 9 .24))
    (拐入 "尼尔")
    (theatre-wait .12)))
