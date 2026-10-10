;; 移植立绘剧场十二·贝恩斯（老街 · 莱恩消失之后）。
;; 布景沿用 HTML SETS.alley：两侧高墙、电线、左右窗（右窗暖光）、左右门、横线；
;; 揪衣领用走位靠近 + 短暂停顿表现（SVG 拽衣动画不移植），尼尔切逼近姿势后换回抱臂。
;; alley 布景没有灯具，HTML 的 fl()/lamp() 在此无视觉目标，故略去，只保留描绘进场。
(define (贝恩斯-演出)
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
  (define (填 id parent x y w h color)
    (加入 (theatre-polygon id parent color
      (list (list x y) (list (+ x w) y) (list (+ x w) (+ y h)) (list x (+ y h))))))
  ;; 人物组落脚在地面（y=790）；走位改组 x，翻面改立绘 scale-x，显隐改立绘 opacity。
  (define (人物 id asset w h x flip)
    (加入 (theatre-group (string-append id "位置") "" x 790))
    (加入 (theatre-with
      (if flip
        (theatre-with
          (theatre-image id (string-append id "位置") asset 0 0 w h "巷灯") 'scale-x -1)
        (theatre-image id (string-append id "位置") asset 0 0 w h "巷灯"))
      'opacity 0)))
  (define (现身 id) (theatre-tween id 'opacity 1 .05))
  (define (走 group x ms pan tag)
    (theatre-parallel
      (theatre-tween group 'x x (/ ms 1000.0) 'smooth)
      (theatre-sound (string-append "脚步/" tag) "StageSounds/老街酒馆/脚步" #f .4 pan)
      (theatre-wait (+ (/ ms 1000.0) .2))))
  (define (说 who x text seconds)
    (theatre-during
      (theatre-caption-for who text seconds
        (cond ((equal? who "尼尔") "#F0CF8A")
              ((equal? who "夜莺") "#8FD9D0")
              ((equal? who "贝恩斯") "#93B4EA")
              ((equal? who "弗兰克") "#E39A88")
              (else "#C9CFE2")))
      (theatre-parallel
        (theatre-tween "焦点" 'x (+ 800 (* (- x 800) .5)) 1 'smooth)
        (theatre-tween "说话柔光" 'x x 1.2 'smooth)
        (theatre-tween "说话柔光" 'opacity .13 1.2 'smooth))))
  (define (旁白 text seconds)
    (theatre-caption-for "旁白" text seconds "#C9CFE2"))

  ;; 布景：HTML ALLEY（地面 790 为基准）。
  (加入 (theatre-with (theatre-group "布景" "" 0 0) 'opacity 0))
  (线 "地面" "布景" "#5A6F9C" 1.5 '((0 790) (1600 790)) 0)
  (线 "路沿" "布景" "#33466E" 1 '((0 806) (1600 806)) .4)
  (线 "左墙" "布景" "#CFE0FF" 2.5 '((340 60) (340 790)) .2)
  (线 "右墙" "布景" "#CFE0FF" 2.5 '((1260 60) (1260 790)) .3)
  (线 "电线左" "布景" "#9FB4E0" 1.5 '((340 150) (190 215)) .6)
  (线 "电线右" "布景" "#9FB4E0" 1.5 '((1260 150) (1410 215)) .6)
  (框 "左窗" "布景" 200 330 90 110 "#6F86B8" 1.5 1.2)
  (填 "右窗光" "布景" 1310 300 90 110 "#8A6A388C")
  (框 "右窗" "布景" 1310 300 90 110 "#6F86B8" 1.5 1.3)
  (框 "右门" "布景" 1330 520 100 270 "#9FB4E0" 2 1.5)
  (框 "左门" "布景" 180 540 110 250 "#7F95C4" 2 1.6)
  ;; HTML 的 Q 二次曲线换算成等价 cubic 控制点。
  (线 "横线" "布景" "#7F95C4" 1
    (theatre-cubic '(340 250) '(646.67 296.67) '(953.33 293.33) '(1260 240) 24) 1.8)
  (加入 (theatre-light "巷灯" "" "#F3D08A" 800 550 620 1))
  (加入 (theatre-focus "焦点" 800 576 .34 .62))
  (加入 (theatre-with (theatre-glow "说话柔光" "" "#9DB0E0" 800 560 440 560) 'opacity 0))
  ;; 立绘文件都是方形像素（1024），显示框沿用雨夜来访/路灯下范本比例；
  ;; 四人同场，横向按 HTML 相对顺序拉开，避免立绘重叠。
  (人物 "贝恩斯" "Portraits/Neon/贝恩斯" 308 308 450 #f)
  (人物 "弗兰克" "Portraits/Neon/弗兰克" 308 308 850 #t)
  (人物 "尼尔" "Portraits/Neon/尼尔_抱臂" 308 308 1800 #t)
  (人物 "街坊" "Portraits/Neon/路人男" 308 308 1500 #t)

  (play-theatre! (theatre-scene 1600 900 "#080A0E" 图形)
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "布景" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait 1.5)
        (现身 "贝恩斯")
        (现身 "弗兰克")
        (theatre-wait 1.2)))
    (说 "贝恩斯" 450 "你就什么也不知道？" 1.395)
    (说 "弗兰克" 850 "我很少见他" 0.975)
    (说 "贝恩斯" 450 "这事儿跟你脱不了关系" 1.500)
    (说 "弗兰克" 850 "这就是你们站在老街的理由？" 1.815)
    (现身 "街坊")
    (现身 "尼尔")
    (走 "尼尔位置" 1250 2800 -.1 "贝恩斯1")
    (theatre-wait 1.3)
    (theatre-wait 1.7)
    (说 "弗兰克" 850 "这件事" 0.765)
    (theatre-clear-caption)
    (theatre-tween "弗兰克" 'scale-x 1 .3)
    (theatre-wait .7)
    ;; 揪衣领：尼尔切逼近姿势、走位靠近，配一拳击声与短暂停顿；弗兰克被拽得一晃。
    (theatre-image-to "尼尔" "Portraits/Neon/尼尔_逼近")
    (theatre-parallel
      (theatre-tween "尼尔位置" 'x 1100 .5 'smooth)
      (theatre-tween "弗兰克位置" 'x 820 .3 'smooth)
      (theatre-sound "揪衣" "StageSounds/老街酒馆/拳击" #f .5 0)
      (theatre-wait .7))
    (theatre-wait .5)
    (旁白 "（尼尔上来揪住衣领、逼近质问。弗兰克生气了。）" 2.865)
    ;; 贝恩斯上前隔开两人：尼尔松手后撤，弗兰克退回原位。
    (theatre-parallel
      (走 "贝恩斯位置" 980 900 0 "贝恩斯2")
      (theatre-sequence
        (theatre-tween "尼尔位置" 'x 1200 .4 'smooth)
        (theatre-tween "弗兰克位置" 'x 740 .4 'smooth)
        (theatre-wait 1.1)))
    (说 "尼尔" 1200 "莱恩呢？" 0.870)
    (说 "弗兰克" 740 "莱恩，莱恩，你们都要找这个莱恩" 2.025)
    (说 "贝恩斯" 980 "莱恩消失了，几天前，我已经派了警察在找他，没什么消息" 3.180)
    (说 "弗兰克" 740 "你觉得我在藏着他？我比你们还急，这个王八蛋" 2.655)
    (说 "贝恩斯" 980 "这场演出没这么简单，有谁？贝城的很多人都在看着这场演出。弗兰克，要是你的人，这里的人出了事，惹恼了那些人，你也不会好受" 6.645)
    (theatre-clear-caption)
    (theatre-tween "弗兰克" 'scale-x -1 .3)
    (走 "弗兰克位置" -200 3400 -.2 "贝恩斯3")
    (theatre-image-to "尼尔" "Portraits/Neon/尼尔_抱臂")
    (说 "尼尔" 1200 "你不觉得他藏着莱恩？" 1.500)
    (theatre-tween "贝恩斯" 'scale-x -1 .4)
    (说 "贝恩斯" 980 "我搜过了，没有" 1.185)
    (说 "贝恩斯" 980 "他一直不希望我们插手老街的事情，可是前提是他管好自己的人" 3.390)
    (说 "贝恩斯" 980 "你去看过他家里了吗？有些线索，我助手在那儿他会告诉你" 3.180)
    (说 "尼尔" 1200 "我去看看" 0.870)
    (theatre-clear-caption)
    (theatre-wait 1.5)))
