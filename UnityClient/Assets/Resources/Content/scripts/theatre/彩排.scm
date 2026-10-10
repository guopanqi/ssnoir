;; 移植立绘剧场十七：彩排（空剧院 · 梦想最后一次纯粹的样子）。
;; 立绘 1024x1024（PIL 实测），显示尺寸沿用范本 252x308；十七 center 舞台用夜莺.png 基础姿势。
;; SVG 肢体/表情/流泪动画不移植；“♪♫”浮字省略（没有文字图元），用脚灯点亮 + 和弦表现演唱；
;; piano()/pad() 的 WebAudio 合成音改为离线生成的 StageSounds/剧院/钢琴.wav 与和弦.wav。
;; 对白逐字照抄 HTML run() 原文，秒数一律 0.45+字数×0.105。
(define (彩排-演出)
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
  ;; 脚灯排：x 300 起每 70 一盏，共 15 盏，初始微亮。
  (define (脚灯 x)
    (if (> x 1300) '()
      (begin
        (加入 (theatre-with
          (theatre-glow (string-append "脚灯/" (number->string x)) "舞台" "#F3D08A" x 796 14 14)
          'opacity .12))
        (脚灯 (+ x 70)))))
  ;; 脚灯逐个点亮：HTML 的 setTimeout(i*140) 排布。
  (define (点亮 x i)
    (if (> x 1300) '()
      (cons (theatre-sequence
              (theatre-wait (+ .001 (* i .14)))
              (theatre-tween (string-append "脚灯/" (number->string x)) 'opacity .95 .5))
        (点亮 (+ x 70) (+ i 1)))))

  (加入 (theatre-with (theatre-group "舞台" "" 0 0) 'opacity 0))
  (线 "地面" "舞台" "#5A6F9C" 1.5 '((0 790) (1600 790)) 0)
  (线 "幕左宽" "舞台" "#9FB4E0" 2 '((110 0) (110 790)) .3)
  (线 "幕左窄" "舞台" "#7F95C4" 1.2 '((140 0) (140 790)) .5)
  (线 "幕右宽" "舞台" "#9FB4E0" 2 '((1490 0) (1490 790)) .3)
  (线 "幕右窄" "舞台" "#7F95C4" 1.2 '((1460 0) (1460 790)) .5)
  ;; 钢琴：琴体、琴腿、琴盖。
  (框 "琴体" "舞台" 220 650 230 50 "#CFE0FF" 2.5 1)
  (线 "琴腿" "舞台" "#9FB4E0" 2 '((236 700) (236 790) (434 790) (434 700)) 1.3)
  (线 "琴盖" "舞台" "#CFE0FF" 2 '((220 650) (450 650) (410 600) (260 600) (220 650)) 1.2)
  ;; 一盏追光控制锥光与绑定人物的照明。
  (加入 (theatre-light "追光" "舞台" "#F3D08A" 800 400 620 0))
  (加入 (theatre-with (theatre-polygon "光锥" "追光" "#F3D08A22"
    '((-20 -400) (20 -400) (260 390) (-260 390))) 'opacity .5))
  (加入 (theatre-with (theatre-glow "追光晕" "追光" "#F3D08A" 0 -390 140 140) 'opacity .4))
  (脚灯 300)
  (加入 (theatre-focus "焦点" 800 576 .34 .62))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#9DB0E0" 800 560 440 560) 'opacity 0))
  (加入 (theatre-with (theatre-group "夜莺位置" "" 800 790) 'opacity 0))
  (加入 (theatre-image "夜莺" "夜莺位置" "Portraits/Neon/夜莺" 0 0 308 308 "追光"))
  (加入 (theatre-with
    (theatre-with (theatre-group "尼尔位置" "" 1800 790) 'scale-x -1) 'opacity 0))
  (加入 (theatre-image "尼尔" "尼尔位置" "Portraits/Neon/尼尔_抱臂" 0 0 308 308 "追光"))

  (play-theatre! (theatre-scene 1600 900 "#07060A" 图形)
    ;; 追光亮起，夜莺已在舞台中央。
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "舞台" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait .6)
        (theatre-parallel
          (theatre-tween "追光" 'brightness 1 1)
          (theatre-tween "说话柔光" 'x 800 .5)
          (theatre-tween "说话柔光" 'opacity .3 .5)
          (theatre-tween "夜莺位置" 'opacity 1 1.2)
          (theatre-wait 2))))
    (说 "夜莺" 800 "再来一次。" 0.975 "#8FD9D0")
    ;; 钢琴起，夜莺独唱：浮字省略，演唱只用立绘与停顿表现。
    (theatre-wait .5)
    (theatre-sound "钢琴" "StageSounds/剧院/钢琴" #f .5 0)
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺_恳求")
    (theatre-wait 3.8)
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺")
    (theatre-wait 1)
    ;; 尼尔从右侧进场，看她唱完。
    (theatre-parallel
      (theatre-tween "尼尔位置" 'opacity 1 .05)
      (theatre-tween "尼尔位置" 'x 1330 3 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 .3)
      (theatre-wait 3.2))
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺_低头")
    (theatre-wait .7)
    (说 "夜莺" 800 "你什么时候来的？" 1.290 "#8FD9D0")
    (说 "尼尔" 1330 "第二遍的时候" 1.080 "#F0CF8A")
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺")
    (说 "夜莺" 800 "我是不是唱得很差" 1.290 "#8FD9D0")
    (说 "尼尔" 1330 "有一点。" 0.870 "#F0CF8A")
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺_低头")
    (theatre-wait 1.4)
    (说 "尼尔" 1330 "但是比第二遍好多了。" 1.500 "#F0CF8A")
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺_恳求")
    (theatre-wait .9)
    (theatre-tween "夜莺位置" 'scale-x -1 .1)
    (theatre-wait .9)
    (说 "夜莺" 800 "但我喜欢这个地方，站在这里的时候，会觉得什么事情都可以发生。" 3.600 "#8FD9D0")
    (theatre-tween "夜莺位置" 'scale-x 1 .1)
    (说 "夜莺" 800 "我在上面的时候看起来怎么样" 1.815 "#8FD9D0")
    (theatre-wait 1.2)
    (说 "尼尔" 1330 "很漂亮" 0.765 "#F0CF8A")
    ;; 脚灯逐个点亮 + 和弦：她再唱一遍。
    (apply theatre-parallel
      (append (点亮 300 0)
        (list (theatre-sound "和弦" "StageSounds/剧院/和弦" #f .5 0)
          (theatre-tween "说话柔光" 'opacity .42 1)
          (theatre-wait 2.6))))
    (说 "夜莺" 800 "这是我一直以来的梦想，尼尔，谢谢你" 2.235 "#8FD9D0")
    (theatre-clear-caption)
    (theatre-wait 4.2)))
