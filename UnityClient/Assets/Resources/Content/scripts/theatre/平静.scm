;; 移植立绘剧场 C9：平静（尼尔的房间 · 事情过去后第二天晚上）。
;; 布景同一间房，另加纸箱 #bx（桌上两只蒸汽纸箱，CSS 烟用两团小 glow 反相循环代替）。
;; 尼尔沿用 尼尔_抱臂（曾试 尼尔_靠墙：镜像后倚靠方向朝外且嘴里多一支正文没有的烟，故弃用）；
;; 夜莺离场/返场沿用背身切换。
;; 对白逐字照抄（含括号旁白，旁白走 speaker “旁白”），时长 0.45+字数×0.105 秒。
;; 两处与 HTML 秒数不同的地方：开场旁白 HTML 只给 2600ms，这里按公式给 3.285 秒；
;; “……” HTML 是直接设字幕等 2800ms，这里字幕 0.66 秒 + 补等 2.14 秒，总拍子不变。
(define (平静-演出)
  (define 图形 '())
  (define 描绘 '())
  (define (加入 object) (set! 图形 (append 图形 (list object))))
  (define (线 id parent color width points delay)
    (加入 (theatre-with (theatre-line id parent color width points) 'reveal 0))
    (set! 描绘 (append 描绘 (list
      (theatre-sequence
        (theatre-wait (+ .001 delay))
        (theatre-tween id 'reveal 1 2.2 'smooth))))))
  (define (说 who x text seconds . color)
    (theatre-during
      (theatre-caption-for who text seconds
        (if (null? color)
          (if (equal? who "夜莺") "#8FD9D0" "#F0CF8A")
          (car color)))
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
  ;; 纸箱 #bx：桌上两只（HTML translate(730 610)/(870 610)，箱体 52×34）。
  (加入 (theatre-with (theatre-group "纸箱" "房间" 0 0) 'opacity 0))
  (加入 (theatre-polygon "箱一" "纸箱" "#CDBF9E"
    '((704 610) (710 576) (750 576) (756 610))))
  (加入 (theatre-polygon "箱二" "纸箱" "#CDBF9E"
    '((844 610) (850 576) (890 576) (896 610))))
  ;; 箱上蒸汽：CSS 烟无对应物，用两团小 glow 反相呼吸循环代替（箱子出现后才起）。
  (加入 (theatre-with (theatre-glow "蒸汽一" "房间" "#D8CFB4" 724 548 30 70) 'opacity 0))
  (加入 (theatre-with (theatre-glow "蒸汽二" "房间" "#D8CFB4" 864 548 30 70) 'opacity 0))
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
    ;; 开场：布景描绘中灯闪到 0.8（HTML w400/fl/lamp(.8)），旁白定场。
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "房间" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait .4)
        (闪)
        (theatre-tween "吊灯光" 'brightness .8 .2)))
    (theatre-caption-for "旁白" "（一切都很平静，尼尔又回到日常的生活中，第二天晚上。）" 3.285 "#C9CFE2")
    ;; 夜莺进场（HTML go 560/2600 + w2900）。
    (theatre-parallel
      (theatre-tween "夜莺位置" 'opacity 1 .05)
      (theatre-tween "夜莺位置" 'x 560 2.6 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 -.2)
      (theatre-wait 2.9))
    (说 "夜莺" 560 "我想请你吃顿饭。" 1.290)
    (说 "尼尔" 1040 "今晚……可能不行，搬了一下午的箱子。" 2.340)
    (theatre-clear-caption)
    (theatre-wait 2.2)
    (说 "夜莺" 560 "那你家里有什么？" 1.290)
    ;; 尼尔无言（HTML 直接设“……”字幕等 2800ms；字幕按公式 0.66 秒，余下补等）。
    (theatre-caption-for "尼尔" "……" 0.660 "#F0CF8A")
    (theatre-wait 2.14)
    (说 "夜莺" 560 "你对这顿饭最好期待低一点。" 1.815)
    ;; 夜莺离场（HTML f:1/go-160/2200/w2400/show:0/w1600）。
    (theatre-clear-caption)
    (theatre-parallel
      (theatre-image-to "夜莺" "Portraits/Neon/夜莺_背身")
      (theatre-tween "夜莺位置" 'scale-x -1 .7)
      (theatre-tween "夜莺位置" 'x -160 2.2 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 -.2)
      (theatre-wait 2.4))
    (theatre-parallel
      (theatre-tween "夜莺位置" 'opacity 0 .3)
      (theatre-wait 1.6))
    ;; 夜莺带着纸箱回来（HTML f:0/show:1/go560/2400/op#bx/lamp(1)/w3000）；
    ;; 此后直到结束，箱上蒸汽一直在后台呼吸。
    (theatre-during
      (theatre-sequence
        (theatre-image-to "夜莺" "Portraits/Neon/夜莺_低头")
        (theatre-parallel
          (theatre-tween "夜莺位置" 'scale-x 1 .01)
          (theatre-tween "夜莺位置" 'opacity 1 .05)
          (theatre-tween "夜莺位置" 'x 560 2.4 'smooth)
          (theatre-tween "纸箱" 'opacity 1 .8)
          (theatre-tween "吊灯光" 'brightness 1 .2)
          (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 .2)
          (theatre-wait 3))
        (theatre-wait 1.4)
        (说 "夜莺" 560 "你为什么想当侦探？" 1.395)
        (说 "尼尔" 1040 "小时候书看多了。" 1.290)
        (说 "夜莺" 560 "我是认真的。" 1.080)
        (theatre-clear-caption)
        (theatre-wait 4.2)
        (theatre-tween "吊灯光" 'brightness .7 .2)
        (theatre-wait 1.2))
      (theatre-loop
        (theatre-sequence
          (theatre-tween "蒸汽一" 'opacity .25 1)
          (theatre-tween "蒸汽一" 'opacity 0 1)))
      (theatre-loop
        (theatre-sequence
          (theatre-tween "蒸汽二" 'opacity 0 1)
          (theatre-tween "蒸汽二" 'opacity .25 1))))))
