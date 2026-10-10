;; 移植立绘剧场“十·经理·电话”（尼尔回到公寓 · 经理和装电话的工人已经在等他）。
;; 保留布景坐标、延迟、顺序和对白原文；SVG 肢体/表情动画不移植，只换立绘与走位。
(define (经理电话-演出)
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
  (define (说 who x text seconds)
    (theatre-during
      (theatre-caption-for who text seconds
        (if (equal? who "尼尔") "#F0CF8A" "#F4907C"))
      (theatre-parallel
        (theatre-tween "焦点" 'x (+ 800 (* (- x 800) .5)) 1 'smooth)
        (theatre-tween "说话柔光" 'x x 1.2 'smooth)
        (theatre-tween "说话柔光" 'opacity .13 1.2 'smooth))))
  ;; 拍肩（HTML 的 pt 三连）：经理走近 + 尼尔脚底小顿 + 回位，配衣料声。
  (define (拍肩)
    (theatre-sequence
      (theatre-tween "经理" 'x 530 .25 'smooth)
      (theatre-sound "拍肩" "StageSounds/老街酒馆/轻推衣料" #f .5 0)
      (theatre-tween "尼尔" 'y 784 .12)
      (theatre-tween "尼尔" 'y 790 .18)
      (theatre-tween "经理" 'x 600 .3 'smooth)))

  (加入 (theatre-with (theatre-group "布景" "" 0 0) 'opacity 0))
  (线 "地面" "布景" "#5A6F9C" 1.5 '((0 790) (1600 790)) 0)
  (线 "地沿" "布景" "#33466E" 1 '((0 806) (1600 806)) .4)
  (线 "吊线" "布景" "#9FB4E0" 1.5 '((1160 0) (1160 150)) .3)
  (线 "灯罩" "布景" "#E6EEFF" 2.5
    '((1132 192) (1149 152) (1171 152) (1188 192) (1132 192)) .6)
  (加入 (theatre-light "吊灯" "" "#F3D08A" 1160 190 620 0))
  (加入 (theatre-polygon "光锥" "吊灯" "#F3D08A18"
    '((-24 4) (24 4) (190 420) (-190 420))))
  (加入 (theatre-glow "灯下光池" "吊灯" "#F3D08A66" 0 420 420 50))
  (加入 (theatre-with (theatre-glow "灯泡光晕" "吊灯" "#F3D08A" 0 0 92 92) 'opacity .4))
  (加入 (theatre-glow "灯芯" "吊灯" "#FFE6A8" 0 -2 30 30))
  (框 "桌板" "布景" 1060 610 200 14 "#F0F4FF" 3 1)
  (线 "桌腿左" "布景" "#9FB4E0" 2 '((1076 624) (1076 790)) 1.3)
  (线 "桌腿右" "布景" "#9FB4E0" 2 '((1244 624) (1244 790)) 1.3)
  (框 "电话座" "布景" 1100 578 120 32 "#8A95B8" 2 1.4)
  (框 "电话筒" "布景" 1106 556 108 22 "#8A95B8" 2 1.5)
  (线 "拨号盘" "布景" "#C9A45C" 1.5 (theatre-ellipse-points 1160 594 12 12) 1.6)
  ;; 电话线（HTML #cb）：M1220 596 C…S… 两段 cubic，在工人接好线时一次画出，不进入初始描绘。
  (加入 (theatre-with (theatre-line "电话线" "布景" "#E6B84A" 5
    (append (theatre-cubic '(1220 596) '(1300 650) '(1350 560) '(1440 690) 32)
      (cdr (theatre-cubic '(1440 690) '(1530 820) '(1590 800) '(1620 780) 24))))
    'reveal 0))
  (加入 (theatre-focus "焦点" 800 576 .20 .52))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#8FB4E8" 860 560 600 840) 'opacity 0))
  (加入 (theatre-with
    (theatre-image "尼尔" "" "Portraits/Neon/尼尔_抱臂" -150 790 308 308 "吊灯") 'opacity 0))
  (加入 (theatre-with
    (theatre-with (theatre-image "经理" "" "Portraits/Neon/经理" 900 790 308 308 "吊灯") 'scale-x -1)
    'opacity 0))
  (加入 (theatre-with
    (theatre-with (theatre-image "工人" "" "Portraits/Neon/送酒工" 1440 790 308 308 "吊灯") 'scale-x -1)
    'opacity 0))

  (play-theatre! (theatre-scene 1600 900 "#0B0907" 图形)
    ;; 建屋收尾和进场重叠：经理与工人先在场，尼尔随后进门。
    (theatre-parallel
      (apply theatre-parallel
        (cons (theatre-tween "布景" 'opacity 1 1.5)
          (append 描绘
            (list (theatre-animate "吊灯" 'brightness
              '((0 0) (2.4 0) (2.401 .15) (2.51 .15) (2.511 1) (2.62 1)
                (2.621 .3) (2.73 .3) (2.731 1) (3.8 1)))))))
      (theatre-sequence
        (theatre-wait 1)
        (theatre-parallel
          (theatre-tween "经理" 'opacity 1 .6)
          (theatre-tween "工人" 'opacity 1 .6)
          (theatre-wait 1))
        (theatre-parallel
          (theatre-tween "尼尔" 'opacity 1 .05)
          (theatre-tween "尼尔" 'x 430 2.4 'smooth)
          (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 -.3)
          (theatre-wait 2.8))))
    (theatre-wait .5)
    (theatre-parallel
      (theatre-tween "经理" 'x 600 .9 'smooth)
      (说 "经理" 600 "尼尔！" 0.765))
    (拍肩)
    (拍肩)
    (theatre-wait .5)
    (说 "尼尔" 430 "这是干嘛？" 0.975)
    (说 "经理" 600 "电话！以后出了事情，还让人通过门房留言吗？" 2.655)
    (说 "经理" 600 "大侦探得有个电话。" 1.395)
    (说 "尼尔" 430 "我还……" 0.870)
    (拍肩)
    (说 "经理" 600 "早晚的事。" 0.975)
    (theatre-wait .7)
    ;; 工人接好线：电话线一次画出，经理下令。
    (theatre-parallel
      (theatre-tween "电话线" 'reveal 1 3.5 'smooth)
      (theatre-sound "接线" "StageSounds/老街酒馆/拿布" #f .5 .2)
      (说 "经理" 600 "给他装好的！最好的线！" 1.605)
      (theatre-wait 3.6))))
