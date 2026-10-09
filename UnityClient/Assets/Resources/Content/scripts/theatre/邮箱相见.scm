;; 勒索日城市侧开场：两人相见 → 原筹款分支对白 → 夜莺投信离开 → 尼尔蹲守。
;; 本场只构造演出，不修改剧情状态；三封信在阻塞剧场结束后进入交锋。
(define (邮箱相见-演出 筹款)
  (define 地面 790)
  (define 图形 '())
  (define 描绘 '())
  (define (加入 object) (set! 图形 (append 图形 (list object))))
  (define (线 id color width points delay)
    (加入 (theatre-with (theatre-line id "布景" color width points) 'reveal 0))
    (set! 描绘 (append 描绘 (list
      (theatre-sequence (theatre-wait (+ .001 delay))
        (theatre-tween id 'reveal 1 1.3 'smooth))))))
  (define (框 id x y w h color delay)
    (线 id color 2
      (list (list x y) (list (+ x w) y) (list (+ x w) (+ y h))
        (list x (+ y h)) (list x y)) delay))
  (define (说 who text voice seconds)
    (define x (if (equal? who "尼尔") 540 1060))
    (theatre-during
      (theatre-caption-for who text seconds
        (if (equal? who "尼尔") "#F0CF8A" "#8FD9D0"))
      (theatre-sound "对白" (string-append "Voices/" voice) #f 1
        (if (equal? who "尼尔") -.2 .2))
      (theatre-parallel
        (theatre-tween "焦点" 'x (+ 800 (* (- x 800) .5)) .7 'smooth)
        (theatre-tween "说话柔光" 'x x .7 'smooth)
        (theatre-tween "说话柔光" 'opacity .13 .7 'smooth))))
  (define 对白
    (cond
      ((equal? 筹款 '足额)
       (theatre-sequence
        (说 "夜莺" "一百整。" "三封信/码头/投信/足额/01/夜莺" 1.15)
        (说 "尼尔" "你把它放到邮箱里就走，我会在这儿盯着。" "三封信/码头/投信/足额/01/尼尔" 4.03)))
      ((equal? 筹款 '她补)
       (theatre-sequence
        (说 "尼尔" "我已经尽力了，还是差一点。" "三封信/码头/投信/她补/01/尼尔" 2.91)
        (说 "夜莺" "我还有一点儿..." "三封信/码头/投信/她补/01/夜莺" 2.11)
        (说 "尼尔" "你上次不是说，那已经是全部了？" "三封信/码头/投信/她补/02/尼尔" 3.71)
        (说 "夜莺" "我...我总不能..." "三封信/码头/投信/她补/02/夜莺" 2.91)
        (说 "尼尔" "别说了，你去吧。放进邮箱就走，我会在这儿盯着。" "三封信/码头/投信/她补/03/尼尔" 4.83)))
      ((equal? 筹款 '塞报纸)
       (theatre-sequence
        (说 "尼尔" "我已经尽力了，还是差一点。就这样吧，我们用旧报纸塞进去。" "三封信/码头/投信/塞报纸/01/尼尔" 5.95)
        (说 "夜莺" "他回去会数的。" "三封信/码头/投信/塞报纸/01/夜莺" 1.79)
        (说 "尼尔" "那就让他数，别担心。你去吧，你把它放邮箱里就走，我会在这儿盯着。" "三封信/码头/投信/塞报纸/02/尼尔" 6.43)))
      (else (error "邮箱相见：未知筹款情况"))))

  (加入 (theatre-with (theatre-group "布景" "" 0 0) 'opacity 0))
  (线 "地面" "#5A6F9C" 1.5 (list (list 0 地面) (list 1600 地面)) 0)
  (线 "路沿" "#33466E" 1 (list (list 0 (+ 地面 16)) (list 1600 (+ 地面 16))) .1)
  ;; 邮箱与报摊分处两侧：投信人离开，观察者退回斜对面的报摊。
  (框 "邮箱箱体" 1210 555 96 138 "#8FAFC8" .3)
  (框 "投信口" 1224 580 68 8 "#DCE8EE" .6)
  (线 "邮箱左脚" "#8FAFC8" 2 '((1228 693) (1228 790)) .5)
  (线 "邮箱右脚" "#8FAFC8" 2 '((1288 693) (1288 790)) .5)
  (线 "摊棚" "#9FB4E0" 2 '((170 500) (340 440) (490 500)) .2)
  (框 "摊桌" 200 675 260 14 "#9FB4E0" .5)
  (线 "摊桌腿" "#7F95C4" 2 '((220 689) (220 790) (440 790) (440 689)) .7)
  (框 "摊上报纸" 275 650 95 24 "#C9CFE2" .8)
  (线 "灯杆" "#CFE0FF" 2 '((860 790) (860 290) (910 290)) .4)
  (线 "灯罩" "#E6EEFF" 2 '((890 290) (930 290) (920 272) (900 272) (890 290)) .6)
  (加入 (theatre-with (theatre-glow "灯光" "布景" "#F3D08A" 910 290 75 75) 'opacity 0))
  (加入 (theatre-with (theatre-glow "光池" "布景" "#F3D08A" 1050 地面 580 70) 'opacity .12))
  (加入 (theatre-focus "焦点" 800 576 .34 .62))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#9DB0E0" 800 560 440 560) 'opacity 0))
  (加入 (theatre-with (theatre-image "尼尔" "" "Portraits/Neon/尼尔_抱臂" -160 地面 308 308 "") 'opacity 0))
  (加入 (theatre-with
    (theatre-with (theatre-image "夜莺" "" "Portraits/Neon/夜莺_低头" 1760 地面 308 308 "") 'scale-x -1)
    'opacity 0))
  ;; 信封随夜莺移动，最后留在邮箱里；报纸只在蹲守时拿起。
  (加入 (theatre-with (theatre-group "信封" "" 1110 656) 'opacity 0))
  (加入 (theatre-polygon "信封纸" "信封" "#E8E2D2" '((-26 -16) (26 -16) (26 16) (-26 16))))
  (加入 (theatre-line "信封折线" "信封" "#8A8372" 1.5 '((-26 -16) (0 4) (26 -16))))
  (加入 (theatre-with (theatre-group "手中报纸" "" 360 635) 'opacity 0))
  (加入 (theatre-polygon "报纸纸面" "手中报纸" "#BBC5D6" '((-48 -32) (48 -32) (48 32) (-48 32))))
  (加入 (theatre-line "报纸中缝" "手中报纸" "#53627C" 1 '((0 -30) (0 30))))

  (play-theatre! (theatre-scene 1600 900 "#08090F" 图形)
    (apply theatre-parallel
      (append (list (theatre-tween "布景" 'opacity 1 .8)
                    (theatre-tween "灯光" 'opacity .4 1.4)) 描绘))
    (theatre-parallel
      (theatre-tween "尼尔" 'opacity 1 .1)
      (theatre-tween "尼尔" 'x 540 2 'smooth)
      (theatre-sequence (theatre-wait .5)
        (theatre-parallel (theatre-tween "夜莺" 'opacity 1 .1)
          (theatre-tween "夜莺" 'x 1060 2 'smooth)))
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 0))
    (theatre-tween "信封" 'opacity 1 .3)
    对白
    (theatre-clear-caption)
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺_背身")
    (theatre-parallel
      (theatre-tween "夜莺" 'scale-x 1 .1)
      (theatre-tween "夜莺" 'x 1170 .8 'smooth)
      (theatre-tween "信封" 'x 1230 .8 'smooth)
      (theatre-tween "信封" 'y 594 .8 'smooth)
      (theatre-tween "焦点" 'x 1100 .8 'smooth))
    (theatre-tween "信封" 'opacity 0 .35)
    (theatre-wait .4)
    (theatre-parallel
      (theatre-tween "夜莺" 'x 1760 2.1 'smooth)
      (theatre-tween "说话柔光" 'opacity 0 .7)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .35 .5))
    (theatre-image-to "尼尔" "Portraits/Neon/尼尔_靠墙")
    (theatre-parallel
      (theatre-tween "尼尔" 'x 360 1 'smooth)
      (theatre-tween "焦点" 'x 580 1 'smooth))
    (theatre-tween "手中报纸" 'opacity 1 .4)
    (theatre-wait 1)))
