;; 移植立绘剧场 · 烟盒（桥边）：赎金交付之后，桥上辨认烟盒。
;; 桥面线 + 立柱排；“金牌”二字省略（线画字糊），只保留红盒身 + 金标；
;; 烟盒小→中→大特写→收，照 HTML 的 pk() 参数用 group 缩放实现；
;; 夜莺身旁的残影盒照 gh() 做闪烁。
;; 保留现有独立试演入口，正式剧情由明确的线索状态选择演出。
(define (烟盒-演出) (老街启程-演出 #t))

(define (老街启程-演出 有烟?)
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
        (if (equal? who "尼尔") "#F0CF8A" "#8FD9D0"))
      (theatre-parallel
        (theatre-tween "焦点" 'x (+ 800 (* (- x 800) .5)) 1 'smooth)
        (theatre-tween "说话柔光" 'x x 1.2 'smooth)
        (theatre-tween "说话柔光" 'opacity .13 1.2 'smooth))))
  ;; 烟盒盒身：HTML 八边形是凹的，拆成两个凸四边形（同色共边，无缝）。
  ;; 盖、描边、高光照 #pk 矢量；金标是旋转 -2 度的矩形四角。
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
  (define (人形 组 像 x 肖像 朝左 宽 高)
    (加入 (theatre-with (theatre-group 组 "" x 790) 'opacity 0))



    (if 朝左
      (加入 (theatre-with (theatre-image 像 组 肖像 0 0 宽 高 "") 'scale-x -1))
      (加入 (theatre-image 像 组 肖像 0 0 宽 高 ""))))
  (define (柱 x)
    (if (> x 1400) #t
      (begin
        (线 (string-append "柱/" (number->string x)) "布景" "#9FB4E0" 2
          (list (list x 700) (list x 790)) (+ .8 (/ (abs (- x 800)) 900.0)))
        (柱 (+ x 80)))))

  (加入 (theatre-with (theatre-group "布景" "" 0 0) 'opacity 0))
  (线 "地面" "布景" "#5A6F9C" 1.5 '((0 790) (1600 790)) 0)
  (线 "桥面" "布景" "#CFE0FF" 3 '((180 700) (1420 700)) .4)
  (线 "桥下" "布景" "#7F95C4" 1.5 '((180 730) (1420 730)) .6)
  (柱 200)
  (加入 (theatre-focus "焦点" 800 576 .20 .52))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#8FB4E8" 860 560 600 840) 'opacity 0))
  (盒组 "烟盒" 588 612 .01 0 0)
  (烟盒部件 "盒" "烟盒")
  (盒组 "烟盒残影" 1070 470 .38 8 0)
  (烟盒部件 "影" "烟盒残影")
  (人形 "尼尔位置" "尼尔" -150 "Portraits/Neon/尼尔_抱臂" #f 308 308)
  (人形 "夜莺位置" "夜莺" 1130 "Portraits/Neon/夜莺_低头" #t 308 308)

  (play-theatre! (theatre-scene 1600 900 "#06080D" 图形)
    ;; 建桥收尾和人物进场重叠：大结构画出后尼尔就开走，不等最后一笔。
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "布景" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait .6)
        (theatre-parallel
          (theatre-tween "尼尔位置" 'opacity 1 .05)
          (theatre-tween "尼尔位置" 'x 470 1.8 'smooth)
          (theatre-tween "夜莺位置" 'opacity 1 .05)
          (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 -.2)
          (theatre-wait 2))))
    (if 有烟?
      (theatre-sequence
        (说 "尼尔" 470 "我没追到他，该死，在路口的时候突然窜出一辆摩托，我把它跟丢了" 3.600)
        (theatre-tween "夜莺位置" 'x 1090 .3 'smooth)
        (说 "夜莺" 1090 "你人没事儿就好" 1.185)
        ;; 亮盒：小尺寸在尼尔手边显现。
        (theatre-wait .4)
        (theatre-parallel
          (theatre-tween "烟盒" 'scale-x .22 .5)
          (theatre-tween "烟盒" 'scale-y .22 .5)
          (theatre-tween "烟盒" 'opacity 1 .5)
          (theatre-wait .5))
        (说 "尼尔" 470 "我拿到了这个" 1.080)
        ;; 大特写：盒移到画面中心放大，尼尔换逼近姿势。
        (theatre-parallel
          (theatre-tween "烟盒" 'x 800 1)
          (theatre-tween "烟盒" 'y 380 1)
          (theatre-tween "烟盒" 'scale-x 1.1 1)
          (theatre-tween "烟盒" 'scale-y 1.1 1)
          (theatre-tween "烟盒" 'rotation -5 1)
          (theatre-image-to "尼尔" "Portraits/Neon/尼尔_逼近")
          (theatre-wait 1.7))
        ;; 残影闪烁：夜莺认出盒子时的重影。
        (theatre-tween "烟盒残影" 'opacity .4 .5)
        (theatre-wait .5)
        (theatre-tween "烟盒残影" 'opacity 0 .3)
        (theatre-wait .3)
        (theatre-tween "烟盒残影" 'opacity .3 .4)
        (theatre-wait .4)
        (theatre-tween "烟盒残影" 'opacity 0 .3)
        (theatre-image-to "夜莺" "Portraits/Neon/夜莺_恳求")
        (说 "夜莺" 1090 "这是...他的？" 1.100)
        (说 "尼尔" 470 "追的时候从他身上掉下来的，“金牌”香烟" 2.445)
        (说 "夜莺" 1090 "...这个烟" 0.900)
        (说 "尼尔" 470 "你见过？" 0.800)
        ;; 推近一点，关键情报落在这句：贵，老街抽的人不多。
        (theatre-parallel
          (theatre-tween "烟盒" 'scale-x 1.25 .3)
          (theatre-tween "烟盒" 'scale-y 1.25 .3)
          (theatre-tween "烟盒" 'rotation -9 .3)
          (说 "夜莺" 1090 "见过，很贵，在老街抽的人不多" 2.000))
        (theatre-image-to "夜莺" "Portraits/Neon/夜莺_低头")
        ;; 收盒：缩回尼尔口袋，尼尔换回抱臂。
        (theatre-parallel
          (theatre-tween "烟盒" 'x 588 .7)
          (theatre-tween "烟盒" 'y 612 .7)
          (theatre-tween "烟盒" 'scale-x .01 .7)
          (theatre-tween "烟盒" 'scale-y .01 .7)
          (theatre-tween "烟盒" 'rotation 0 .7)
          (theatre-tween "烟盒" 'opacity 0 .7)
          (theatre-image-to "尼尔" "Portraits/Neon/尼尔_抱臂")
          (说 "尼尔" 470 "他从街道的桥上跳下去，应该对这一带很熟，我准备去问问。" 3.400))
        (theatre-image-to "夜莺" "Portraits/Neon/夜莺_恳求")
        (theatre-tween "夜莺位置" 'x 1150 .5 'smooth)
        (说 "夜莺" 1150 "你要去老街？" 1.000)
        (说 "尼尔" 470 "对" 0.555)
        (说 "夜莺" 1150 "我可不回去" 0.975)
        (theatre-tween "尼尔位置" 'x 520 .5 'smooth)
        (说 "尼尔" 520 "我没说要带上你......你不喜欢那里？" 2.400)
        (theatre-tween "夜莺位置" 'x 1170 .5 'smooth)
        (theatre-image-to "夜莺" "Portraits/Neon/夜莺_低头")
        (说 "夜莺" 1170 "我花了很久才从里面走出来，我不回去" 2.235)
        (theatre-wait 2.4))
      ;; 无烟版：没有烟盒情报，只剩方向。结构与有烟版同构，同样落在“她不回去”。
      (theatre-sequence
        (说 "夜莺" 1090 "你受伤了？" 1.300)
        (说 "尼尔" 470 "不要紧，就是让他给跑了。" 1.600)
        (说 "夜莺" 1090 "钱呢？也让他拿走了？" 1.400)
        (说 "尼尔" 470 "嗯。路口突然窜出一辆摩托，再看人就没了。" 2.600)
        (说 "夜莺" 1090 "那就一点办法也没了？" 1.400)
        (theatre-image-to "尼尔" "Portraits/Neon/尼尔_逼近")
        (说 "尼尔" 470 "他往码头居民区去了。那一片，我得去一趟。" 2.600)
        (theatre-image-to "夜莺" "Portraits/Neon/夜莺_恳求")
        (theatre-tween "夜莺位置" 'x 1150 .5 'smooth)
        (说 "夜莺" 1150 "你要去老街？" 1.000)
        (说 "尼尔" 470 "对" 0.555)
        (说 "夜莺" 1150 "我可不回去" 0.975)
        (theatre-tween "尼尔位置" 'x 520 .5 'smooth)
        (说 "尼尔" 520 "我没说要带上你......你不喜欢那里？" 2.400)
        (theatre-tween "夜莺位置" 'x 1170 .5 'smooth)
        (theatre-image-to "夜莺" "Portraits/Neon/夜莺_低头")
        (说 "夜莺" 1170 "我花了很久才从里面走出来，我不回去" 2.235)
        (theatre-wait 2.4)))
    (theatre-clear-caption)))
