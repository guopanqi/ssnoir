;; 独立线绘舞台样板：移植 experiments/立绘剧场 · 线绘世界 · Claude Sonnet 5.5.html 的路灯片段。
;; 布景、表演和本场组合函数各有边界；不读写剧情状态，不替换正式剧情。
(define (路灯下-布景)
  ;; 地面高度是全场唯一的纵向基准：地面线、路沿、路面刻线、灯杆灯座底部、
  ;; 人物脚底、光池中心全部由此派生；焦点 Y 保持原型的 64% 高度不动。
  (define 地面 790)
  (define (描线 id color width points)
    (theatre-with (theatre-line id "线框" color width points) 'reveal 0))
  (define (路面刻线 i)
    (if (= i 8) '()
      (let ((x (+ 60 (* i 190))))
        (cons (描线 (string-append "路面/" (number->string i)) "#46608F" 3
                (list (list x (+ 地面 70)) (list (+ x 90) (+ 地面 70))))
              (路面刻线 (+ i 1))))))
  (theatre-scene 1600 900 "#08090F"
    (append
      (list
        (theatre-with (theatre-group "布景" "" 0 0) 'opacity 0)
        (theatre-group "线框" "布景" 0 0)
        (描线 "地面" "#5A6F9C" 1.5 (list (list 0 地面) (list 1600 地面)))
        (描线 "路沿" "#33466E" 1 (list (list 0 (+ 地面 16)) (list 1600 (+ 地面 16)))))
      (路面刻线 0)
      (list
        (描线 "灯杆左" "#CFE0FF" 2 (list (list 794 地面) (list 794 282)))
        (描线 "灯杆右" "#CFE0FF" 2 (list (list 806 地面) (list 806 282)))
        (描线 "灯座" "#CFE0FF" 2 (list (list 778 地面) (list 822 地面) (list 822 (- 地面 20)) (list 778 (- 地面 20)) (list 778 地面)))
        (描线 "灯罩" "#E6EEFF" 2.5 '((768 272) (832 272) (816 228) (784 228) (768 272)))
        (描线 "灯顶" "#CFE0FF" 2 '((800 228) (800 212)))
        ;; 一盏逻辑灯控制整套可见光，也控制绑定人物的照明。
        (theatre-light "路灯" "布景" "#F3D08A" 800 550 620 0)
        (theatre-polygon "光锥" "路灯" "#F3D08A18" '((-8 -280) (8 -280) (240 240) (-240 240)))
        (theatre-glow "地面光池" "路灯" "#F3D08A66" 0 240 620 76)
        (theatre-line "光池外圈" "路灯" "#D9B86A66" 1.5 (theatre-ellipse-points 0 240 250 24))
        (theatre-line "光池内圈" "路灯" "#D9B86A44" 1.5 (theatre-ellipse-points 0 240 165 15))
        (theatre-with (theatre-glow "灯泡光晕" "路灯" "#F3D08A" 0 -284 92 92) 'opacity .4)
        (theatre-glow "灯芯" "路灯" "#FFE6A8" 0 -286 30 30)
        (theatre-with (theatre-glow "路灯柔光" "路灯" "#F3D08A" 0 10 660 924) 'opacity .22)
        (theatre-focus "焦点" 800 576 .30 .60)
        (theatre-group "说话光" "" 800 560)
        (theatre-with (theatre-glow "说话柔光" "说话光" "#9DB0E0" 0 0 330 462) 'opacity 0)
        (theatre-with (theatre-image "尼尔" "" "Portraits/Neon/尼尔_抱臂" -140 地面 252 308 "路灯") 'opacity 0)
        (theatre-with
          (theatre-with (theatre-image "夜莺" "" "Portraits/Neon/夜莺_低头" 1740 地面 252 308 "路灯") 'scale-x -1)
          'opacity 0)))))

;; 原型 CSS easing 采样成归一化关键帧；演出运行时只有一个播放器时钟。
(define 路灯下-ease '((0.000000 0.000000) (0.041667 0.025556) (0.083333 0.070806) (0.125000 0.136888) (0.166667 0.220674) (0.208333 0.314292) (0.250000 0.408511) (0.291667 0.496716) (0.333333 0.575862) (0.375000 0.645321) (0.416667 0.705611) (0.458333 0.757648) (0.500000 0.802403) (0.541667 0.840773) (0.583333 0.873539) (0.625000 0.901368) (0.666667 0.924824) (0.708333 0.944386) (0.750000 0.960459) (0.791667 0.973389) (0.833333 0.983474) (0.875000 0.990969) (0.916667 0.996096) (0.958333 0.999050) (1.000000 1.000000)))
(define 路灯下-move '((0.000000 0.000000) (0.041667 0.003342) (0.083333 0.013605) (0.125000 0.031114) (0.166667 0.056131) (0.208333 0.088812) (0.250000 0.129162) (0.291667 0.176974) (0.333333 0.231775) (0.375000 0.292771) (0.416667 0.358826) (0.458333 0.428478) (0.500000 0.500000) (0.541667 0.571522) (0.583333 0.641174) (0.625000 0.707229) (0.666667 0.768225) (0.708333 0.823026) (0.750000 0.870838) (0.791667 0.911188) (0.833333 0.943869) (0.875000 0.968886) (0.916667 0.986395) (0.958333 0.996658) (1.000000 1.000000)))
(define (路灯下-变化 id property from to seconds delay curve)
  (theatre-animate id property
    (append (if (> delay 0) (list (list 0 from)) '())
      (map (lambda (pair) (list (+ delay (* seconds (car pair)))
        (+ from (* (- to from) (cadr pair))))) curve))))
(define (路灯下-说 who x text seconds)
  (theatre-parallel
    (theatre-caption-for who text seconds
      (if (equal? who "尼尔") "#F0CF8A" "#8FD9D0"))
    (theatre-tween "焦点" 'x (+ 800 (* (- x 800) .5)) 1)
    (theatre-tween "说话光" 'x x 1.2)
    (theatre-tween "说话柔光" 'opacity .13 1.2)))
(define (路灯下-闪烁)
  (theatre-parallel
    (theatre-animate "路灯" 'brightness
      '((0 1) (.001 .15) (.11 .15) (.111 1) (.22 1) (.221 .3)
        (.33 .3) (.331 .9) (.44 .9) (.441 .5) (.55 .5) (.551 1) (.66 1)))
    (theatre-sound "电流" "Theatre/路灯电流" #f .28 0)))
(define (路灯下-搭建)
  (define (刻线 i)
    (if (= i 8) '()
      (append
        (路灯下-变化 (string-append "路面/" (number->string i)) 'reveal 0 1 2.2
          (+ .6 (/ (abs (- (+ 60 (* i 190)) 800)) 700.0)) 路灯下-ease)
        (刻线 (+ i 1)))))
  (apply theatre-parallel
    (list
      (路灯下-变化 "布景" 'opacity 0 1 1.5 0 路灯下-ease)
      (路灯下-变化 "地面" 'reveal 0 1 2.2 0 路灯下-ease)
      (路灯下-变化 "路沿" 'reveal 0 1 2.2 .4 路灯下-ease)
      (路灯下-变化 "灯杆左" 'reveal 0 1 2.2 1 路灯下-ease)
      (路灯下-变化 "灯杆右" 'reveal 0 1 2.2 1 路灯下-ease)
      (路灯下-变化 "灯座" 'reveal 0 1 2.2 1.2 路灯下-ease)
      (路灯下-变化 "灯罩" 'reveal 0 1 2.2 1.6 路灯下-ease)
      (路灯下-变化 "灯顶" 'reveal 0 1 2.2 1.8 路灯下-ease)
      (刻线 0)
      (theatre-animate "路灯" 'brightness
        '((0 0) (3.3 0) (3.301 .15) (3.41 .15) (3.411 1) (3.52 1)
          (3.521 .3) (3.63 .3) (3.631 .9) (3.74 .9) (3.741 .5)
          (3.85 .5) (3.851 1) (4.86 1)))
      (theatre-sound-after "电流" "Theatre/路灯电流" #f .28 0 3.3)
      (theatre-wait 4.86))))
(define (路灯下-试演!)
  (play-theatre! (路灯下-布景)
    (路灯下-搭建)
    ;; 尼尔先走，1.4 秒后夜莺从另一侧进入；总等待与原型相同。
    (theatre-parallel
      (theatre-animate "尼尔" 'opacity '((0 1) (4.4 1)))
      (路灯下-变化 "尼尔" 'x -140 470 2.8 0 路灯下-move)
      (theatre-animate "夜莺" 'opacity '((0 0) (1.399 0) (1.4 1) (4.4 1)))
      (路灯下-变化 "夜莺" 'x 1740 1130 2.8 1.4 路灯下-move)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 -.3)
      (theatre-wait 4.4))
    (theatre-wait .8)
    (路灯下-说 "尼尔" 470 "现在可以说了？" 1.185)
    (路灯下-说 "夜莺" 1130 "莱恩是我……之前的男朋友。" 1.815)
    (路灯下-说 "尼尔" 470 "继续。" 0.765)
    (路灯下-说 "夜莺" 1130 "他不会做出这样的事情来，他，他怎么会这样？" 2.655)
    (theatre-parallel
      (theatre-image-to "尼尔" "Portraits/Neon/尼尔_逼近")
      (路灯下-变化 "尼尔" 'x 470 540 .7 0 路灯下-move)
      (路灯下-说 "尼尔" 540 "第一次的时候我就问过你，你为什么没有告诉我？" 2.760))
    (theatre-parallel
      (路灯下-变化 "夜莺" 'x 1130 1100 .7 0 路灯下-move)
      (路灯下-说 "夜莺" 1100 "我不能确定，而且我觉得你会不愿意管，一个没钱的女孩，一个以前的男人，如果我真的告诉你，你还会帮我吗？" 5.700))
    (theatre-clear-caption)
    (theatre-tween "线框" 'opacity .38 .7)
    (路灯下-闪烁)
    (theatre-parallel (theatre-tween "路灯" 'brightness .6 .2) (theatre-wait 1.9))
    (theatre-parallel
      (theatre-image-to "尼尔" "Portraits/Neon/尼尔_抱臂")
      (theatre-tween "线框" 'opacity 1 1)
      (theatre-tween "路灯" 'brightness 1 .2)
      (路灯下-说 "尼尔" 540 "也许会，也许不会，我不知道。" 1.920))
    (路灯下-说 "夜莺" 1100 "分手之后，他找过我，我总是甩掉他。后来我离开老街之后，他到剧院找我，但都被保安拦住了，我们没有再见过面了。" 6.015)
    (路灯下-说 "尼尔" 540 "你还有什么是瞒着我的？" 1.605)
    (theatre-parallel
      (路灯下-变化 "夜莺" 'x 1100 1070 .7 0 路灯下-move)
      (路灯下-说 "夜莺" 1070 "没有了，尼尔，我知道的就这些了，真的。" 2.445))
    (theatre-clear-caption)
    (theatre-parallel
      (theatre-tween "线框" 'opacity .38 1)
      (theatre-tween "路灯" 'brightness .7 .2)
      (theatre-wait 1.4))
    (路灯下-闪烁)
    (theatre-parallel (theatre-tween "路灯" 'brightness .5 .2) (theatre-wait 1.3))
    (theatre-parallel
      (theatre-image-to "夜莺" "Portraits/Neon/夜莺_仰头")
      (theatre-tween "线框" 'opacity 1 1)
      (theatre-tween "路灯" 'brightness 1 .2)
      (路灯下-说 "夜莺" 1070 "我知道他住的地方，我们去看看吧。" 2.130))
    (theatre-clear-caption)
    (theatre-parallel
      (theatre-animate "夜莺" 'scale-x '((0 -1) (.001 1) (10.5 1)))
      (路灯下-变化 "夜莺" 'x 1070 1850 5.6 0 路灯下-move)
      (路灯下-变化 "尼尔" 'x 540 1850 5.8 3.8 路灯下-move)
      (theatre-animate "灯泡光晕" 'opacity '((0 .4) (5.7 .4) (5.9 .95) (6.7 .95) (6.9 .4) (10.5 .4)))
      (theatre-animate "路灯柔光" 'opacity '((0 .22) (5.7 .22) (6.7 .34) (7.7 .22) (10.5 .22)))
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 .3)
      (theatre-wait 10.5))
    (路灯下-闪烁)
    (theatre-wait 2.2)
    (theatre-parallel
      (theatre-tween "路灯" 'brightness .6 .2)
      (theatre-tween "布景" 'opacity 0 .9)
      (theatre-tween "说话柔光" 'opacity 0 .9)
      (theatre-tween "焦点" 'opacity 0 .9))))
