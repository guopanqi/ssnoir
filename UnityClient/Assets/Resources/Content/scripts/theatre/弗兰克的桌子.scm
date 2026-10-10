;; 移植立绘剧场 · 弗兰克的桌子（老街）：一拳之后醒来，对峙弗兰克。
;; 地面 + 吊灯 HANG(800,690) + 右侧门；前景桌板最后才显现；
;; “重击闪白 + 模糊苏醒”只用已有能力近似：闪白是全屏白块短暂显现，
;; 苏醒是布景与人物 brightness 先压暗再缓回，不碰 C#。
(define (弗兰克的桌子-演出)
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
        (if (equal? who "尼尔") "#F0CF8A"
          (if (equal? who "弗兰克") "#E39A88" "#C9CFE2")))
      (theatre-parallel
        (theatre-tween "焦点" 'x (+ 800 (* (- x 800) .5)) 1 'smooth)
        (theatre-tween "说话柔光" 'x x 1.2 'smooth)
        (theatre-tween "说话柔光" 'opacity .13 1.2 'smooth))))
  ;; 烟盒盒身：HTML 八边形是凹的，拆成两个凸四边形（同色共边，无缝）。
  (define (烟盒部件 前缀 盒组)
    (加入 (theatre-polygon (string-append 前缀 "/身下") 盒组 "#B5201E"
      '((-78 -34) (76 -30) (72 106) (-72 100))))
    (加入 (theatre-polygon (string-append 前缀 "/身上") 盒组 "#B5201E"
      '((-70 -105) (68 -98) (76 -30) (-78 -34))))
    (加入 (theatre-polygon (string-append 前缀 "/盒盖") 盒组 "#951918"
      '((-70 -105) (68 -98) (70 -60) (-74 -66))))
    (加入 (theatre-polygon (string-append 前缀 "/金标") 盒组 "#E5B84A"
      '((-63 -18) (61 -22) (63 40) (-60 44))))
    (加入 (theatre-line (string-append 前缀 "/描边") 盒组 "#7D1212" 3
      '((-70 -105) (68 -98) (76 -30) (64 36) (72 106) (-72 100) (-62 28) (-78 -34) (-70 -105))))
    (加入 (theatre-with (theatre-line (string-append 前缀 "/光一") 盒组 "#E8736A" 3 '((-40 60) (-12 92))) 'opacity .55))
    (加入 (theatre-with (theatre-line (string-append 前缀 "/光二") 盒组 "#E8736A" 3 '((30 -92) (22 -42))) 'opacity .55))
    (加入 (theatre-with (theatre-line (string-append 前缀 "/光三") 盒组 "#E8736A" 3 '((52 52) (12 100))) 'opacity .55))
    (加入 (theatre-with (theatre-line (string-append 前缀 "/光四") 盒组 "#E8736A" 3 '((-66 -10) (-40 -40))) 'opacity .55)))
  (define (盒组 id x y s r o)
    (加入 (theatre-with (theatre-with (theatre-with (theatre-with
      (theatre-group id "布景" x y) 'scale-x s) 'scale-y s) 'rotation r) 'opacity o)))
  ;; 人物脚底定位，y=790 为地面；遮挡由剧场随当前立绘自动绘制。
  (define (人形 组 像 x 肖像 朝左 灯 宽 高)
    (加入 (theatre-with (theatre-group 组 "" x 790) 'opacity 0))



    (if 朝左
      (加入 (theatre-with (theatre-image 像 组 肖像 0 0 宽 高 灯) 'scale-x -1))
      (加入 (theatre-image 像 组 肖像 0 0 宽 高 灯))))

  (加入 (theatre-with (theatre-group "布景" "" 0 0) 'opacity 0))
  (线 "地面" "布景" "#5A6F9C" 1.5 '((0 790) (1600 790)) 0)
  (线 "路沿" "布景" "#33466E" 1 '((0 806) (1600 806)) .4)
  ;; 吊灯 HANG(800,690)：竖线 + 灯罩 + 光锥到底 690（桌沿）。
  (线 "吊线" "布景" "#9FB4E0" 1.5 '((800 0) (800 150)) .3)
  (线 "灯罩" "布景" "#E6EEFF" 2.5 '((772 192) (789 152) (811 152) (828 192) (772 192)) .6)
  (加入 (theatre-light "吊灯" "布景" "#F3D08A" 800 440 500 0))
  (加入 (theatre-polygon "光锥" "吊灯" "#F3D08A18" '((-24 -246) (24 -246) (190 250) (-190 250))))
  (加入 (theatre-glow "桌光" "吊灯" "#F3D08A66" 0 250 500 60))
  (加入 (theatre-with (theatre-glow "灯泡光晕" "吊灯" "#F3D08A" 0 -250 92 92) 'opacity .4))
  (加入 (theatre-glow "灯芯" "吊灯" "#FFE6A8" 0 -252 30 30))
  ;; 右侧门。
  (框 "门框" "布景" 1380 470 130 320 "#E9EEF8" 2.5 1.4)
  (框 "门左" "布景" 1394 486 50 120 "#9FB4E0" 1.5 1.7)
  (框 "门右" "布景" 1450 486 44 120 "#9FB4E0" 1.5 1.7)
  (加入 (theatre-focus "焦点" 800 576 .20 .52))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#8FB4E8" 860 560 600 840) 'opacity 0))
  (人形 "尼尔位置" "尼尔" -150 "Portraits/Neon/尼尔_抱臂" #f "吊灯" 308 308)
  (人形 "弗兰克位置" "弗兰克" 800 "Portraits/Neon/弗兰克" #t "吊灯" 308 308)
  (人形 "工人位置" "工人" 1300 "Portraits/Neon/路人男" #t "吊灯" 308 308)
  ;; 前景桌板：重击之后才显现，直接加入（不进描绘），盖住弗兰克下半身。
  (加入 (theatre-with (theatre-group "前景桌" "" 0 0) 'opacity 0))
  (加入 (theatre-polygon "桌填" "前景桌" "#07080C" '((560 690) (1040 690) (1040 790) (560 790))))
  (加入 (theatre-line "桌框" "前景桌" "#9FB4E0" 2.5 '((560 690) (1040 690) (1040 790) (560 790) (560 690))))
  (加入 (theatre-line "桌沿" "前景桌" "#F0F4FF" 3.5 '((560 690) (1040 690))))
  ;; 烟盒：工人手边小尺寸待命；残影在弗兰克桌上。
  (盒组 "烟盒" 470 640 .22 0 0)
  (烟盒部件 "盒" "烟盒")
  (盒组 "烟盒残影" 1000 672 .3 8 0)
  (烟盒部件 "影" "烟盒残影")
  ;; 闪白块：最前，平时透明，重击时闪一下。
  (加入 (theatre-with (theatre-polygon "闪白" "" "#FFFFFF" '((0 0) (1600 0) (1600 900) (0 900))) 'opacity 0))

  (play-theatre! (theatre-scene 1600 900 "#0C0808" 图形)
    ;; 建场收尾和尼尔进场重叠。
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "布景" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait .4)
        (theatre-parallel
          (theatre-tween "尼尔位置" 'opacity 1 .05)
          (theatre-tween "尼尔位置" 'x 500 2.4 'smooth)
          (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 -.2)
          (theatre-wait 2.6))))
    ;; 重击：拳击 + 低频闷响 + 全场闪白，再黑回去。
    (theatre-parallel
      (theatre-sound "拳击" "StageSounds/老街酒馆/拳击" #f .8 0)
      (theatre-sound "闷响" "StageSounds/老街酒馆/闷踢" #f .5 0)
      (theatre-tween "闪白" 'opacity .85 .06)
      (theatre-wait .12))
    (theatre-tween "闪白" 'opacity 0 .1)
    (theatre-wait .3)
    ;; 倒地：尼尔栽到桌边，桌、弗兰克、工人、灯一起亮起。
    (theatre-tween "尼尔位置" 'x 440 .2)
    (theatre-tween "前景桌" 'opacity 1 .2)
    (theatre-tween "弗兰克位置" 'opacity 1 .05)
    (theatre-tween "工人位置" 'opacity 1 .05)
    (theatre-tween "吊灯" 'brightness 1 .5)
    (说 "旁白" 800 "（一个街角，突然你被一拳打晕了。……醒来的时候，你看到了弗兰克，和他的那张桌子。）" 4.755)
    ;; 苏醒：没有模糊能力，压暗再缓回近似。
    (theatre-parallel
      (theatre-tween "布景" 'brightness .35 .1)
      (theatre-tween "尼尔位置" 'brightness .35 .1)
      (theatre-tween "弗兰克位置" 'brightness .35 .1)
      (theatre-tween "工人位置" 'brightness .35 .1)
      (theatre-wait .1))
    (theatre-wait .06)
    (theatre-parallel
      (theatre-tween "布景" 'brightness 1 2.8)
      (theatre-tween "尼尔位置" 'brightness 1 2.8)
      (theatre-tween "弗兰克位置" 'brightness 1 2.8)
      (theatre-tween "工人位置" 'brightness 1 2.8)
      (theatre-wait 2.8))
    (说 "弗兰克" 800 "你在找什么？" 1.080)
    (说 "尼尔" 440 "找你" 0.660)
    ;; 敲桌两下：闷踢近似桌响。
    (theatre-sound "敲一" "StageSounds/老街酒馆/闷踢" #f .5 0)
    (theatre-wait .5)
    (theatre-sound "敲二" "StageSounds/老街酒馆/闷踢" #f .5 0)
    (theatre-wait .4)
    (说 "弗兰克" 800 "你想知道什么？" 1.185)
    (说 "尼尔" 440 "莱恩勒索一个女孩儿，她委托我来处理这件事" 2.550)
    ;; 工人跑腿：先凑近，再出场外。
    (theatre-parallel
      (theatre-tween "工人位置" 'scale-x -1 .05)
      (theatre-tween "工人位置" 'x 1150 .9 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 .3)
      (theatre-wait 1.1))
    (theatre-parallel
      (theatre-tween "工人位置" 'scale-x 1 .05)
      (theatre-tween "工人位置" 'x 1700 1.4 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 .3)
      (theatre-wait 1.5))
    (说 "弗兰克" 800 "有什么依据吗？" 1.185)
    (说 "尼尔" 440 "我拿到了他身上的烟，在我的口袋里" 2.130)
    ;; 工人取烟回来：从场外走到桌边，烟盒随之滑到桌上。
    (theatre-tween "工人位置" 'scale-x -1 .05)
    (theatre-parallel
      (theatre-tween "工人位置" 'x 500 2.6 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 .3)
      (theatre-wait 2.7))
    (theatre-tween "烟盒" 'opacity 1 .1)
    (theatre-wait .3)
    (theatre-parallel
      (theatre-tween "烟盒" 'x 540 .4)
      (theatre-tween "烟盒" 'y 610 .4)
      (theatre-wait .5))
    (theatre-parallel
      (theatre-tween "工人位置" 'x 1060 2.4 'smooth)
      (theatre-tween "工人位置" 'scale-x 1 .05)
      (theatre-tween "烟盒" 'x 940 2.4)
      (theatre-tween "烟盒" 'y 672 2.4)
      (theatre-tween "烟盒" 'scale-x .3 2.4)
      (theatre-tween "烟盒" 'scale-y .3 2.4)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 0)
      (theatre-wait 2.6))
    (说 "弗兰克" 800 "我也会抽这种烟" 1.185)
    (theatre-tween "烟盒残影" 'opacity .9 .5)
    (说 "尼尔" 440 "那么也可能是你" 1.185)
    ;; 弗兰克笑：单姿势无换图，停顿代替。
    (theatre-wait 1.8)
    (theatre-parallel
      (theatre-tween "工人位置" 'x 1240 1.2 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 .3)
      (theatre-wait 1.4))
    (theatre-wait 1.6)
    (说 "弗兰克" 800 "看来你说的有些道理，我们来谈谈这件事" 2.340)
    (theatre-clear-caption)
    (theatre-wait 1.6)
    (说 "旁白" 800 "（此后进入和莱恩的交锋：和弗兰克谈判 → 教训莱恩 · 见游戏）" 3.810)
    (theatre-clear-caption)))
