;; 移植立绘剧场十一：剧院 · 信（化妆室 · 第三封信）。
;; 立绘均为 1024x1024（PIL 实测），显示尺寸沿用路灯下范本的 252x308。
;; SVG 肢体/表情/流泪动画不移植，只换立绘 + 走位/翻面；信件用分组平移近似 mvl 三点移动。
;; 对白/旁白逐字照抄 HTML run() 原文，秒数一律 0.45+字数×0.105。
(define (剧院来信-演出)
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
  (define (说 who x text seconds color)
    (theatre-during
      (theatre-caption-for who text seconds color)
      (theatre-parallel
        (theatre-tween "焦点" 'x (+ 800 (* (- x 800) .5)) 1 'smooth)
        (theatre-tween "说话柔光" 'x x 1.2 'smooth)
        (theatre-tween "说话柔光" 'opacity .13 1.2 'smooth))))
  (define (灯泡 id x y)
    (加入 (theatre-glow id "化妆室" "#F3D08A" x y 14 14)))
  (define (上排 xs)
    (if (null? xs) '()
      (begin (灯泡 (string-append "灯泡/上/" (number->string (car xs))) (car xs) 210)
        (灯泡 (string-append "灯泡/下/" (number->string (car xs))) (car xs) 540)
        (上排 (cdr xs)))))
  (define (侧列 ys)
    (if (null? ys) '()
      (begin (灯泡 (string-append "灯泡/左/" (number->string (car ys))) 620 (car ys))
        (灯泡 (string-append "灯泡/右/" (number->string (car ys))) 980 (car ys))
        (侧列 (cdr ys)))))

  (加入 (theatre-with (theatre-group "化妆室" "" 0 0) 'opacity 0))
  (线 "地面" "化妆室" "#5A6F9C" 1.5 '((0 790) (1600 790)) 0)
  (线 "地面沿" "化妆室" "#33466E" 1 '((0 806) (1600 806)) .4)
  (框 "镜框" "化妆室" 620 210 360 330 "#E6EEFF" 3 .3)
  (框 "镜面" "化妆室" 640 230 320 290 "#7F95C4" 1.2 .8)
  (上排 '(620 665 710 755 800 845 890 935 980))
  (侧列 '(255 300 345 390 435 480))
  (框 "桌面" "化妆室" 1030 620 180 14 "#F0F4FF" 3 1)
  (线 "桌腿左" "化妆室" "#9FB4E0" 2 '((1046 634) (1046 790)) 1.3)
  (线 "桌腿右" "化妆室" "#9FB4E0" 2 '((1194 634) (1194 790)) 1.3)
  (框 "箱子" "化妆室" 1250 690 70 100 "#9FB4E0" 2 1.2)
  (加入 (theatre-with (theatre-glow "镜光" "化妆室" "#F3D08A" 800 380 520 520) 'opacity 0))
  (加入 (theatre-focus "焦点" 800 576 .34 .62))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#9DB0E0" 800 560 440 560) 'opacity 0))
  ;; 化妆室没有吊灯，人物不绑定灯光，沿用自身亮度。
  (加入 (theatre-with (theatre-group "夜莺位置" "" 700 790) 'opacity 0))
  (加入 (theatre-image "夜莺" "夜莺位置" "Portraits/Neon/夜莺_低头" 0 0 308 308 ""))
  (加入 (theatre-with
    (theatre-with (theatre-group "尼尔位置" "" 1760 790) 'scale-x -1) 'opacity 0))
  (加入 (theatre-image "尼尔" "尼尔位置" "Portraits/Neon/尼尔_抱臂" 0 0 308 308 ""))
  ;; 信画在人物之后：终点 (1300,640) 落在尼尔身前，看作被他拿在手中；
  ;; HTML 原型是窄 SVG 身形，宽立绘下并排会穿帮，故改前后关系而不改坐标。
  (加入 (theatre-with (theatre-group "信" "化妆室" 1120 598) 'opacity 0))
  (加入 (theatre-polygon "信纸" "信" "#E8E2D2"
    '((-28 -18) (28 -18) (28 20) (-28 20))))
  (加入 (theatre-line "信封" "信" "#8A8372" 2 '((-28 -18) (0 6) (28 -18))))

  (play-theatre! (theatre-scene 1600 900 "#0A0810" 图形)
    ;; 建镜收尾和人物进场重叠：镜体大结构画出后夜莺就已在场，不等最后一笔。
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "化妆室" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait .5)
        (theatre-parallel
          (theatre-tween "夜莺位置" 'opacity 1 .6)
          (theatre-tween "镜光" 'opacity .16 .4)
          (theatre-tween "信" 'opacity 1 .4)
          (theatre-wait 1))))
    ;; （在剧院里你见到了夜莺，她在房间里反复走来走去。）
    (theatre-caption-for "旁白" "（在剧院里你见到了夜莺，她在房间里反复走来走去。）" 3.075 "#C9CFE2")
    ;; 夜莺踱步：翻面 + 走位，脚步声只铺一次短音。
    (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 -.2)
    (theatre-tween "夜莺位置" 'scale-x -1 .1)
    (theatre-parallel
      (theatre-tween "夜莺位置" 'x 520 1.5 'smooth)
      (theatre-wait 1.7))
    (theatre-tween "夜莺位置" 'scale-x 1 .1)
    (theatre-parallel
      (theatre-tween "夜莺位置" 'x 900 1.7 'smooth)
      (theatre-wait 1.9))
    (theatre-tween "夜莺位置" 'scale-x -1 .1)
    (theatre-parallel
      (theatre-tween "夜莺位置" 'x 560 1.6 'smooth)
      (theatre-wait 1.8))
    ;; 尼尔从右侧进场；夜莺迎上去。
    (theatre-parallel
      (theatre-tween "尼尔位置" 'opacity 1 .05)
      (theatre-tween "尼尔位置" 'x 1300 2.4 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 .2)
      (theatre-wait 2))
    (theatre-parallel
      (theatre-tween "夜莺位置" 'scale-x 1 .1)
      (theatre-tween "夜莺位置" 'x 940 1 'smooth)
      (theatre-wait 1.1))
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺_恳求")
    (说 "夜莺" 940 "尼尔！" 0.765 "#8FD9D0")
    (说 "尼尔" 1300 "把那封信拿给我" 1.185 "#F0CF8A")
    ;; 信件传递：HTML 的 mvl 三点移动，先藏后现。
    (theatre-parallel
      (theatre-tween "信" 'opacity 0 .4)
      (theatre-wait .7))
    (theatre-parallel
      (theatre-tween "信" 'x 1200 .8 'smooth)
      (theatre-tween "信" 'y 560 .8 'smooth)
      (theatre-tween "信" 'opacity 1 .4)
      (theatre-wait .9))
    (theatre-parallel
      (theatre-tween "信" 'x 1300 .8 'smooth)
      (theatre-tween "信" 'y 640 .8 'smooth)
      (theatre-wait .9))
    (说 "尼尔" 1300 "冷静点，你是什么时候收到这封信的" 2.130 "#F0CF8A")
    (说 "夜莺" 940 "我在化妆室看到它的，我中午刚排练完。" 2.340 "#8FD9D0")
    (说 "尼尔" 1300 "还是莱恩？" 0.975 "#F0CF8A")
    (theatre-wait 1.4)
    (说 "夜莺" 940 "还能是别人吗？但我总觉得莱恩做不出这种事来的" 2.760 "#8FD9D0")
    (theatre-image-to "尼尔" "Portraits/Neon/尼尔_逼近")
    (说 "尼尔" 1300 "人是会变的。" 1.080 "#F0CF8A")
    (说 "夜莺" 940 "他就是要毁了我的一切…………" 1.920 "#8FD9D0")
    (说 "尼尔" 1300 "你还要上台吗？如果真的有危险的话" 2.130 "#F0CF8A")
    (说 "夜莺" 940 "……我不能放弃这次机会，这是我的梦想，我一直都想登上更大的舞台" 3.705 "#8FD9D0")
    ;; “但是……”说着又走开两步。
    (theatre-parallel
      (说 "夜莺" 640 "但是……" 0.870 "#8FD9D0")
      (theatre-tween "夜莺位置" 'x 640 1.4 'smooth)
      (theatre-wait 1.5))
    (theatre-parallel
      (theatre-tween "夜莺位置" 'x 900 1.4 'smooth)
      (theatre-wait 1.5))
    (说 "夜莺" 900 "尼尔，你会有办法的对吗？你总是有办法的" 2.445 "#8FD9D0")
    (theatre-image-to "尼尔" "Portraits/Neon/尼尔_抱臂")
    (说 "尼尔" 1300 "我会试试" 0.870 "#F0CF8A")
    (说 "夜莺" 900 "你真好，尼尔" 1.080 "#8FD9D0")
    (theatre-image-to "尼尔" "Portraits/Neon/尼尔_逼近")
    (说 "尼尔" 1300 "这是我的工作，处理麻烦是我的工作，这件事情是我没有处理好，我会去找到他的。" 4.335 "#F0CF8A")
    (theatre-clear-caption)
    (theatre-tween "镜光" 'opacity .15 .4)
    (theatre-caption-for "尼尔（独白）" "莱恩，我会找到你的。" 1.500 "#F0CF8A")
    (theatre-wait .9)))
