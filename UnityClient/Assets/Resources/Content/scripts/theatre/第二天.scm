;; 移植立绘剧场 C5：第二天（尼尔的房间 · 独自探索失败后）。
;; 布景与室内求助同一间房：地面线 + 吊灯 HANG(800,610) + 桌子 TABLE(660,280)。
;; 本场无道具。HTML 全场只有走位与 SVG 表情变化，无翻面/转身，故两张立绘
;; 一次到位、中途不换。对白逐字照抄，时长 0.45+字数×0.105 秒。
(define (第二天-演出)
  (define 图形 '())
  (define 描绘 '())
  (define (加入 object) (set! 图形 (append 图形 (list object))))
  (define (线 id parent color width points delay)
    (加入 (theatre-with (theatre-line id parent color width points) 'reveal 0))
    (set! 描绘 (append 描绘 (list
      (theatre-sequence
        (theatre-wait (+ .001 delay))
        (theatre-tween id 'reveal 1 2.2 'smooth))))))
  (define (说 who x text seconds)
    (theatre-during
      (theatre-caption-for who text seconds
        (if (equal? who "夜莺") "#8FD9D0" "#F0CF8A"))
      (theatre-parallel
        (theatre-tween "焦点" 'x (+ 800 (* (- x 800) .5)) 1 'smooth)
        (theatre-tween "说话柔光" 'x x 1.2 'smooth)
        (theatre-tween "说话柔光" 'opacity .13 1.2 'smooth))))
  (define (闪)
    (theatre-parallel
      (theatre-animate "吊灯光" 'brightness
        '((0 1) (.001 .15) (.11 .15) (.111 1) (.22 1) (.221 .3)
          (.33 .3) (.331 .9) (.44 .9) (.441 .5) (.55 .5) (.551 1) (.66 1)))
      (theatre-sound "电流" "Theatre/路灯电流" #f .28 0)))

  (加入 (theatre-with (theatre-group "房间" "" 0 0) 'opacity 0))
  (线 "地面" "房间" "#5A6F9C" 1.5 '((0 790) (1600 790)) 0)
  (线 "地面沿" "房间" "#33466E" 1 '((0 806) (1600 806)) .4)
  (线 "吊线" "房间" "#9FB4E0" 1.5 '((800 0) (800 150)) .3)
  (线 "灯罩" "房间" "#E6EEFF" 2.5
    '((772 192) (789 152) (811 152) (828 192) (772 192)) .6)
  (加入 (theatre-light "吊灯光" "" "#F3D08A" 800 190 750 0))
  (加入 (theatre-polygon "光锥" "吊灯光" "#F3D08A18"
    '((-24 4) (24 4) (190 420) (-190 420))))
  (加入 (theatre-with (theatre-glow "灯晕" "吊灯光" "#F3D08A" 0 0 92 92) 'opacity .4))
  (加入 (theatre-glow "灯芯" "吊灯光" "#FFE6A8" 0 -2 30 30))
  (加入 (theatre-glow "桌光池" "吊灯光" "#F3D08A55" 0 420 360 40))
  (加入 (theatre-line "桌光环" "吊灯光" "#D9B86A66" 1.5
    (theatre-ellipse-points 0 420 150 14)))
  (线 "桌面" "房间" "#F0F4FF" 3
    '((660 610) (940 610) (940 624) (660 624) (660 610)) 1)
  (线 "桌腿左" "房间" "#9FB4E0" 2 '((676 624) (676 790)) 1.3)
  (线 "桌腿右" "房间" "#9FB4E0" 2 '((924 624) (924 790)) 1.3)
  (加入 (theatre-focus "焦点" 800 576 .20 .52))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#8FB4E8" 860 560 600 840) 'opacity 0))
  (加入 (theatre-with (theatre-group "尼尔位置" "" 1040 790) 'opacity 1))
  (加入 (theatre-with
    (theatre-with
      (theatre-image "尼尔" "尼尔位置" "Portraits/Neon/尼尔_抱臂" 0 0 308 308 "吊灯光")
      'scale-x -1)
    'brightness 1.5))
  (加入 (theatre-with (theatre-group "夜莺位置" "" -160 790) 'opacity 0))
  (加入 (theatre-with
    (theatre-image "夜莺" "夜莺位置" "Portraits/Neon/夜莺_低头" 0 0 308 308 "吊灯光")
    'brightness 1.5))

  (play-theatre! (theatre-scene 1600 900 "#0B0907" 图形)
    ;; 开场：布景按 --d 描绘，0.5 秒后灯闪、吊灯亮起（HTML w500/fl/lamp(1)/w500）。
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "房间" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait .5)
        (闪)
        (theatre-tween "吊灯光" 'brightness 1 .2)
        (theatre-wait .5)))
    ;; 夜莺进场（HTML go 560/2600 + w2900）。
    (theatre-parallel
      (theatre-tween "夜莺位置" 'opacity 1 .05)
      (theatre-tween "夜莺位置" 'x 560 2.6 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 -.2)
      (theatre-wait 2.9))
    (说 "夜莺" 560 "他们没告诉你怎么走？" 1.500)
    (说 "尼尔" 1040 "差不多" 0.765)
    (说 "夜莺" 560 "打听到什么消息了？" 1.395)
    (说 "尼尔" 1040 "毫无进展，他们好像早就知道我要问什么一样" 2.550)
    (说 "夜莺" 560 "你穿成这样，一眼就看出你是外面的，没把你裤子扒了已经算客气的了。" 3.810)
    (theatre-wait 1.8)
    (说 "夜莺" 560 "明天我带你进去" 1.185)
    (说 "尼尔" 1040 "你不是不想回去吗" 1.290)
    ;; 夜莺神色黯下（HTML face sad/w2600，SVG 表情不移植，只留停顿）。
    (theatre-clear-caption)
    (theatre-wait 2.6)
    (说 "夜莺" 560 "我不想回去，特别不想。可是，什么时候也不能都只让你去。" 3.285)
    (theatre-wait 2.2)))
