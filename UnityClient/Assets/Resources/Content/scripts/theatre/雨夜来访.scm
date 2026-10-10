;; 移植立绘剧场 C1：雨夜求助（公寓楼下）。
;; 保留构图、顺序和对白；雨只使用已有环境音，不加入屏幕雨粒子。
(define (雨夜来访-演出)
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
  (define (填 id parent x y w h color . opacity)
    (加入 (theatre-with (theatre-polygon id parent color
      (list (list x y) (list (+ x w) y) (list (+ x w) (+ y h)) (list x (+ y h))))
      'opacity (if (null? opacity) 1 (car opacity)))))
  (define (说 who text 电铃 seconds)
    (if 电铃
      (theatre-during
        (theatre-caption-for "尼尔（电铃）" text seconds "#F0CF8A")
        (theatre-sequence
          (theatre-tween "楼上灯窗" 'opacity .92 .2)
          (theatre-tween "楼上信号" 'opacity .7 .2)
          (theatre-loop
            (theatre-sequence
              (theatre-parallel
                (theatre-tween "电铃信号" 'opacity .3 .16)
                (theatre-tween "楼上信号" 'opacity .3 .16))
              (theatre-parallel
                (theatre-tween "电铃信号" 'opacity .7 .16)
                (theatre-tween "楼上信号" 'opacity .7 .16))))))
      (theatre-during
        (theatre-caption-for who text seconds "#8FD9D0")
        (theatre-parallel
          (theatre-tween "焦点" 'x 805 1 'smooth)
          (theatre-tween "说话柔光" 'x 810 1.2 'smooth)
          (theatre-tween "说话柔光" 'opacity .13 1.2 'smooth)))))
  (define (窗 r c)
    (if (< r 4)
      (if (< c 5)
        (let ((x (+ 500 (* c 118))) (y (+ 60 (* r 110)))
              (id (string-append "窗/" (number->string r) "/" (number->string c))))
          (if (and (= r 2) (= c 3))
            (填 "楼上灯窗" "立面" x y 56 70 "#F2C36E" 0)
            (if (= (modulo (+ (* c 5) (* r 3)) 7) 1)
              (填 (string-append id "/灯") "立面" x y 56 70 "#8A6A388C")))
          (框 id "立面" x y 56 70 "#6F86B8" 1.5 (+ 1 (* (+ r c) .08)))
          (窗 r (+ c 1)))
        (窗 (+ r 1) 0))))
  (define (逃生梯 y)
    (线 (string-append "梯横/" (number->string y)) "立面" "#7F95C4" 1.5
      (list (list 1150 y) (list 1270 y) (list 1270 (+ y 8))) (+ 2 (/ y 400)))
    (线 (string-append "梯斜/" (number->string y)) "立面" "#5F759F" 1.2
      (list (list 1270 (+ y 8)) (list 1150 (+ y 110))) (+ 2.2 (/ y 400))))

  (加入 (theatre-with (theatre-group "立面" "" 0 0) 'opacity 0))
  (线 "地面" "立面" "#2A3A60" 1 '((0 790) (1600 790)) 0)
  (框 "外墙" "立面" 460 -30 680 820 "#9FB4E0" 2 .2)
  (线 "侧线左" "立面" "#6F86B8" 1.5 '((450 -30) (450 790)) .3)
  (线 "侧线右" "立面" "#6F86B8" 1.5 '((1150 -30) (1150 790)) .3)
  (窗 0 0)
  (框 "下窗左" "立面" 500 560 110 130 "#6F86B8" 1.5 1.6)
  (线 "下窗左竖" "立面" "#4F6490" 1 '((555 560) (555 690)) 1.8)
  (线 "下窗左横" "立面" "#4F6490" 1 '((500 625) (610 625)) 1.8)
  (框 "下窗右" "立面" 990 560 110 130 "#6F86B8" 1.5 1.7)
  (线 "下窗右竖" "立面" "#4F6490" 1 '((1045 560) (1045 690)) 1.9)
  (线 "下窗右横" "立面" "#4F6490" 1 '((990 625) (1100 625)) 1.9)
  (逃生梯 150) (逃生梯 260) (逃生梯 370) (逃生梯 480)
  (线 "雨棚" "立面" "#E6EEFF" 2.5 '((690 482) (910 482) (930 450) (670 450) (690 482)) 1.5)
  (框 "台阶" "立面" 710 774 180 16 "#9FB4E0" 2 1.6)
  ;; 门牌 17：线绘数字，落在雨棚上方、窗之间，不用图片字体。
  (线 "门牌一" "立面" "#E0BD70" 1.2 '((788 408) (794 408) (794 432)) 2.4)
  (线 "门牌七" "立面" "#E0BD70" 1.2 '((802 408) (822 408) (812 432)) 2.4)
  (加入 (theatre-with (theatre-glow "楼上信号" "立面" "#FFBF55" 882 315 64 64) 'opacity 0))
  (加入 (theatre-light "门光" "" "#F4CF80" 800 640 620 0))
  (填 "门内光" "门光" -65 -150 130 284 "#F2C36EF2")
  (框 "门框" "立面" 729 484 142 290 "#7F95C4" 1.5 1.3)
  (加入 (theatre-group "门扇" "立面" 735 490))
  (填 "门板" "门扇" 0 0 130 284 "#04060C")
  (框 "门边" "门扇" 0 0 130 284 "#E9EEF8" 2.5 1.4)
  (框 "门格左上" "门扇" 13 15 48 110 "#CFE0FF" 1.5 1.7)
  (框 "门格右上" "门扇" 69 15 48 110 "#CFE0FF" 1.5 1.7)
  (框 "门格左下" "门扇" 13 140 48 128 "#CFE0FF" 1.5 1.9)
  (框 "门格右下" "门扇" 69 140 48 128 "#CFE0FF" 1.5 1.9)
  (框 "电铃盒" "立面" 900 548 44 92 "#9FB0D8" 2 2)
  (线 "电铃格栅" "立面" "#9FB0D8" 1.5 '((908 556) (936 556) (936 563) (908 563) (908 570) (936 570)) 2.2)
  (线 "电铃按钮" "立面" "#9FB0D8" 1.5 (theatre-ellipse-points 924 590 6 6) 2.3)
  (加入 (theatre-with (theatre-glow "按钮光" "立面" "#FFBF55" 924 590 24 24) 'opacity 0))
  (加入 (theatre-with (theatre-glow "电铃信号" "立面" "#FFBF55" 922 563 64 64) 'opacity 0))
  (加入 (theatre-focus "焦点" 800 576 .20 .52))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#8FB4E8" 860 560 600 840) 'opacity 0))
  (加入 (theatre-with (theatre-glow "门前柔光" "门光" "#F4CF80" 0 -80 600 840) 'opacity .26))
  (加入 (theatre-glow "门前光池" "门光" "#F4CF8055" 0 150 400 50))
  (加入 (theatre-with (theatre-group "夜莺位置" "" -200 790) 'opacity 0))
  ;; 人物轮廓遮挡由剧场随当前立绘自动绘制。
  (加入 (theatre-with (theatre-image "夜莺" "夜莺位置" "Portraits/Neon/夜莺_低头" 0 0 297 297 "门光") 'brightness 1.5))
  (加入 (theatre-polygon "伞底" "夜莺位置" "#070A12CC"
    '((-100 -362) (-80 -393) (-48 -414) (-8 -427) (36 -425) (72 -406) (100 -362))))
  (加入 (theatre-line "伞弧" "夜莺位置" "#9FB4E0" 1.8
    (theatre-cubic '(-100 -362) '(-55 -425) '(60 -425) '(100 -362) 24)))
  (加入 (theatre-line "伞沿" "夜莺位置" "#9FB4E0" 1.5 '((-100 -362) (100 -362))))

  (play-theatre! (theatre-scene 1600 900 "#070A12" 图形)
    (theatre-sound "雨声" "StageSounds/雨夜求助/雨" #t .55 0)
    ;; 建楼收尾和夜莺进场重叠：楼体大结构画出后她就开走，不等最后一笔。
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "立面" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait 3)
        (theatre-parallel
          (theatre-tween "夜莺位置" 'opacity 1 .05)
          (theatre-tween "夜莺位置" 'x 810 3.4 'smooth)
          (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 -.2)
          (theatre-wait 3.6))))
    (theatre-wait .5)
    (theatre-parallel
      (theatre-sound "电铃" "StageSounds/雨夜求助/电铃" #f .6 .15)
      (theatre-tween "按钮光" 'opacity 1 .2)
      (theatre-wait 1.3))
    (theatre-tween "按钮光" 'opacity 0 .2)
    (theatre-wait .5)
    (说 "夜莺" "尼尔先生？" #f 0.975)
    (theatre-wait .4)
    (说 "尼尔" "是，你是？" #t 0.975)
    (theatre-tween "电铃信号" 'opacity 0 .2)
    (theatre-tween "楼上信号" 'opacity 0 .2)
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺_恳求")
    (说 "夜莺" "我听朋友说起过你，我遇到了一些麻烦，我想请你帮忙。" #f 3.075)
    (说 "尼尔" "现在很晚了，而且……" #t 1.500)
    (theatre-tween "电铃信号" 'opacity 0 .2)
    (theatre-tween "楼上信号" 'opacity 0 .2)
    (说 "夜莺" "就几分钟，先生，而且这事有点急，我怕明天就太晚了。" #f 3.075)
    (theatre-clear-caption)
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺_低头")
    (theatre-during (theatre-wait 3)
      (theatre-parallel
        (theatre-volume "雨声" .8 .7)
        (theatre-tween "立面" 'opacity .38 .7)
        (theatre-tween "楼上灯窗" 'opacity 0 .3)))
    ;; “可以吗”是她在压暗的楼前独自问的；楼上灯只在尼尔真正回应（门锁/开门）时才回亮，
    ;; 灯亮 = 尼尔的动作，不和她的台词撞在一起。
    (说 "夜莺" "可以吗？" #f 0.870)
    (theatre-clear-caption)
    (theatre-wait .9)
    (theatre-sound "门锁" "StageSounds/雨夜求助/门锁" #f .6 0)
    (theatre-wait .9)
    (theatre-parallel
      (theatre-tween "立面" 'opacity 1 .8)
      (theatre-tween "楼上灯窗" 'opacity .92 .5)
      (theatre-tween "门光" 'brightness 1 .8)
      (theatre-tween "门扇" 'scale-x .12 .9 'smooth)
      (theatre-wait 1.4))
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺_背身")
    (theatre-wait .7)
    (theatre-parallel
      (theatre-tween "夜莺位置" 'x 790 2.6 'smooth)
      (theatre-tween "夜莺位置" 'y 786 2.6 'smooth)
      (theatre-tween "夜莺位置" 'scale-x .85 2.6 'smooth)
      (theatre-tween "夜莺位置" 'scale-y .85 2.6 'smooth)
      (theatre-tween "夜莺位置" 'opacity 0 2.6 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 0)
      (theatre-wait 2.8))
    (theatre-parallel
      (theatre-tween "门扇" 'scale-x 1 .9 'smooth)
      (theatre-tween "门光" 'brightness 0 .8)
      (theatre-tween "说话柔光" 'opacity 0 1.2)
      (theatre-volume "雨声" .2 1.6)
      (theatre-wait 1.6))
    (theatre-wait .8)
    (theatre-stop-sound "雨声")))
