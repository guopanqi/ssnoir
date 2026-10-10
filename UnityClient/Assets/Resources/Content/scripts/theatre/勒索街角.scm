;; 第三、四场共用的邮箱街角：沿用参考的邮箱左、报摊右构图，不绘制“邮”字。
;; 线条按 reveal 逐条描出（HTML 原型的 --d 延迟）：布景调用时攒下描绘，
;; 调用方在开场用 (勒索街角-描绘) 与人物进场并行播放。
(define 街角描绘 '())
(define (勒索街角-描绘) (apply theatre-parallel 街角描绘))
(define (勒索街角-布景)
  (set! 街角描绘 '())
  (define 图形 (list (theatre-group "布景" "" 0 0)))
  (define (加入 o) (set! 图形 (append 图形 (list o))))
  (define (线 id color width points delay)
    (加入 (theatre-with (theatre-line id "布景" color width points) 'reveal 0))
    (set! 街角描绘 (append 街角描绘 (list
      (theatre-sequence
        (theatre-wait (+ .001 delay))
        (theatre-tween id 'reveal 1 2.2 'smooth))))))
  (define (框 id x y w h color delay)
    (线 id color 2 (list (list x y) (list (+ x w) y) (list (+ x w) (+ y h))
      (list x (+ y h)) (list x y)) delay))
  (线 "地面" "#5A6F9C" 1.5 '((0 790) (1600 790)) 0)
  (线 "路沿" "#33466E" 1 '((0 806) (1600 806)) .3)
  (框 "邮箱" 670 560 60 190 "#E6EEFF" .7)
  (线 "邮箱顶" "#E6EEFF" 2.5 '((670 560) (675 543) (687 534) (713 534) (725 543) (730 560)) .8)
  (框 "投信口" 680 592 40 8 "#9FB4E0" .9)
  (线 "邮箱脚" "#9FB4E0" 2 '((690 750) (690 790) (710 790) (710 750)) 1)
  (框 "报摊棚" 1110 430 320 36 "#E6EEFF" 1)
  (框 "报摊架" 1150 500 240 110 "#9FB4E0" 1.1)
  (框 "报摊柜" 1130 620 280 170 "#CFE0FF" 1.3)
  (线 "摊上报纸" "#6F86B8" 1.5 '((1170 520) (1250 520) (1250 540) (1170 540) (1170 560) (1250 560)) 1.5)
  (线 "摊上报纸右" "#6F86B8" 1.5 '((1290 520) (1370 520) (1370 540) (1290 540) (1290 560) (1370 560)) 1.7)
  (加入 (theatre-with (theatre-glow "摊灯光" "布景" "#F3D08A" 1270 494 140 140) 'opacity .15))
  (加入 (theatre-glow "摊灯芯" "布景" "#FFE6A8" 1270 494 14 14))
  (加入 (theatre-focus "焦点" 800 576 .34 .62))
  图形)

(define (勒索街角-报纸)
  (list
    (theatre-with (theatre-group "报纸" "" 1070 598) 'opacity 0)
    ;; 简单一张对折的报纸：纸面 + 一道中折。看人靠放下来看，不在纸上挖洞。
    (theatre-polygon "报纸面" "报纸" "#E8E2D2" '((-52 -58) (52 -58) (52 58) (-52 58)))
    (theatre-line "报纸中折" "报纸" "#A09A84" 1.5 '((0 -58) (0 58)))))

;; 原版缓慢通过街角的人群：固定错峰替代随机数，预览与游戏时序一致。
(define (勒索街角-人群 prefix count direction)
  (define (人物 i)
    (if (= i count) '()
      (cons
        (theatre-with
          (theatre-with
            (theatre-image (string-append prefix (number->string i)) "布景"
              "Portraits/Neon/黑影" (if (> direction 0) -160 1760) (+ 790 (* (modulo i 3) 5))
              (+ 240 (* (modulo i 3) 26)) (+ 240 (* (modulo i 3) 26)) "")
            'brightness .42)
          'opacity .7)
        (人物 (+ i 1)))))
  (人物 0))

(define (勒索街角-人群经过 prefix count direction)
  (define (人物 i)
    (if (= i count) '()
      (cons
        (theatre-sequence
          (theatre-wait (+ .001 (* i .65)))
          (theatre-tween (string-append prefix (number->string i)) 'x
            (if (> direction 0) 1760 -160) (+ 8 (* (modulo i 4) 1.1))))
        (人物 (+ i 1)))))
  (apply theatre-parallel (人物 0)))
