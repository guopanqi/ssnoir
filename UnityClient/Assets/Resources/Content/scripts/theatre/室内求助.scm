;; 移植立绘剧场 C2：室内求助（尼尔的房间 · 夜莺把一切摊在桌上）。
;; 布景 = 地面线 + 吊灯 HANG(800,610) + 桌子 TABLE(660,280)，坐标照 HTML 原样，
;; reveal 延迟用 HTML 的 --d。人物只用现成立绘，SVG 肢体/表情/帽子/眼泪不移植。
;; 对白逐字照抄 HTML run()，时长一律 0.45+字数×0.105 秒（字数 = Scheme 字符串长度）。
;; 与 HTML 的出入（含原因）见本仓库根目录下移植报告：WebAudio 合成音全部省略
;; （点烟 light/pad/tn 无采样，其中两次点烟用现成 尼尔_点烟 立绘表示）；“当”字
;; 票无文字绘制能力，只画票纸；fl() 灯闪用路灯电流采样 + 同款 brightness 关键帧。
(define (室内求助-演出)
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
  ;; 照片 #ph：桌上的相纸（HTML rotate(-6) 用分组旋转还原）。
  (加入 (theatre-with
    (theatre-with (theatre-group "照片" "房间" 798 608) 'opacity 0)
    'rotation -6))
  (加入 (theatre-polygon "相纸" "照片" "#D9D4C4"
    '((-26 -18) (26 -18) (26 18) (-26 18))))
  (加入 (theatre-polygon "相面" "照片" "#2A2D36"
    '((-19 -12) (19 -12) (19 12) (-19 12))))
  ;; 首饰当票 #jw：两枚戒指 + 链 + 表 + 当票（“当”字无文字绘制能力，只画票纸）。
  (加入 (theatre-with (theatre-group "首饰" "房间" 0 0) 'opacity 0))
  (加入 (theatre-line "戒指一" "首饰" "#E5C15A" 3
    (theatre-ellipse-points 720 602 7 7)))
  (加入 (theatre-line "戒指二" "首饰" "#E5C15A" 3
    (theatre-ellipse-points 742 606 5 5)))
  (加入 (theatre-line "链" "首饰" "#E5C15A" 2.5
    (theatre-cubic '(770 604) '(780.7 613.3) '(792 613.3) '(804 604) 12)))
  (加入 (theatre-line "表盘" "首饰" "#CFD6E6" 2.5
    (theatre-ellipse-points 850 600 11 11)))
  (加入 (theatre-line "表带上" "首饰" "#8A6A3A" 6 '((850 589) (850 579))))
  (加入 (theatre-line "表带下" "首饰" "#8A6A3A" 6 '((850 611) (850 621))))
  (加入 (theatre-polygon "当票" "首饰" "#D8CFAE"
    '((880 590) (914 590) (914 612) (880 612))))
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
    ;; 开场：布景按 --d 描绘，0.5 秒后灯闪、吊灯亮起（HTML w500/fl/lamp(1)/w600）。
    (theatre-parallel
      (apply theatre-parallel (cons (theatre-tween "房间" 'opacity 1 1.5) 描绘))
      (theatre-sequence
        (theatre-wait .5)
        (闪)
        (theatre-tween "吊灯光" 'brightness 1 .2)
        (theatre-wait .6)))
    ;; 夜莺进场（HTML go 560/2800 + w3000）。
    (theatre-parallel
      (theatre-tween "夜莺位置" 'opacity 1 .05)
      (theatre-tween "夜莺位置" 'x 560 2.8 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 -.2)
      (theatre-wait 3))
    ;; 夜莺把照片拍在桌上（HTML w700/op#ph/w600）。
    (theatre-wait .7)
    (theatre-parallel
      (theatre-tween "照片" 'opacity 1 .4)
      (theatre-wait .6))
    (说 "尼尔" 1040 "这张照片，嗯，是有些出格，但也没什么特别的，你为什么这么紧张？" 3.705)
    (说 "夜莺" 560 "他手上也许不止这一张，这种照片对普通人来说没什么，可我过些天有一场演出，这是我第一场大型的演出，这肯定会毁了我的" 6.330)
    ;; 尼尔点烟（HTML light()，点烟采样缺失，用点烟立绘表示）。
    (theatre-image-to "尼尔" "Portraits/Neon/尼尔_点烟")
    (说 "尼尔" 1040 "会不会是一个恶作剧，有些小混混会四处搜集照片，然后写一堆这样的信，如果恰巧有人有一些不愿意别人知道的故事的话" 6.120)
    (说 "夜莺" 560 "唉，但我这次真的不能冒险，万一他真的有些什么……" 2.970)
    (说 "尼尔" 1040 "你能想到，你身边有什么人会干这种事吗？" 2.445)
    (theatre-clear-caption)
    (theatre-wait 2.4)
    (说 "夜莺" 560 "……没有，我和他们很久都不联系了" 2.130)
    (说 "尼尔" 1040 "他们？" 0.765)
    (说 "夜莺" 560 "过去的邻居和朋友什么的，不会是他们" 2.235)
    (说 "尼尔" 1040 "报警呢？" 0.870)
    (说 "夜莺" 560 "不行，我经理会知道的，最好也别让他知道" 2.445)
    ;; 尼尔掐烟又点上一支（HTML cg:0/w300/light()，净效果仍是点烟，保持立绘）。
    (theatre-wait .3)
    (说 "尼尔" 1040 "还有一个方法，他要得不算多，不是什么大钱，把钱给他也许他就不会再纠缠了。" 4.230)
    (说 "夜莺" 560 "你不是一个侦探吗？" 1.395)
    (说 "尼尔" 1040 "我很少见到要这么少的勒索，让我来办也要付给我钱，恐怕未必划算" 3.600)
    (说 "尼尔" 1040 "但是话说回来，拿到钱他也未必肯停手，那我们就必须弄清楚他是谁了" 3.705)
    ;; 夜莺摊开首饰当票（HTML w700/op#jw/tn 点名音省略/w1200）。
    (theatre-clear-caption)
    (theatre-wait .7)
    (theatre-parallel
      (theatre-tween "首饰" 'opacity 1 .3)
      (theatre-wait 1.2))
    (说 "夜莺" 560 "说实话，我把首饰也当了，也凑不够他信里要的钱。我其实根本就没办法" 3.810)
    (说 "尼尔" 1040 "那我的报酬呢？你准备怎么付我的报酬？" 2.340)
    (theatre-wait 1.7)
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺_恳求")
    (说 "夜莺" 560 "演出之后可以吗？经理说过些天会有一场大型的演出，应该会有些钱。虽然之前很多次小一些的演出，他也没给过我什么钱" 6.120)
    ;; 夜莺转身背对（HTML f:1/back:1，翻面 0.7 秒与台词重叠）。
    (theatre-parallel
      (theatre-image-to "夜莺" "Portraits/Neon/夜莺_背身")
      (theatre-tween "夜莺位置" 'scale-x -1 .7)
      (说 "夜莺" 560 "当时他说会有些钱的，但是也没给" 2.025))
    (说 "夜莺" 560 "这次机会很重要，我不愿意错过" 1.920)
    (theatre-clear-caption)
    (theatre-wait 2.6)
    ;; 夜莺转回身（HTML f:0/back:0）。
    (theatre-parallel
      (theatre-image-to "夜莺" "Portraits/Neon/夜莺_低头")
      (theatre-tween "夜莺位置" 'scale-x 1 .7)
      (theatre-wait .9))
    (说 "夜莺" 560 "我明白，确实太麻烦你了" 1.605)
    ;; 收起首饰（HTML op#jw0/w700）。
    (theatre-parallel
      (theatre-tween "首饰" 'opacity 0 .3)
      (theatre-wait .7))
    (theatre-wait .9)
    (说 "夜莺" 560 "谢谢你，尼尔先生" 1.290)
    ;; 夜莺走向门口（HTML f:1/go330/2200/w2400）。
    (theatre-clear-caption)
    (theatre-parallel
      (theatre-image-to "夜莺" "Portraits/Neon/夜莺_背身")
      (theatre-tween "夜莺位置" 'scale-x -1 .7)
      (theatre-tween "夜莺位置" 'x 330 2.2 'smooth)
      (theatre-sound "脚步" "StageSounds/老街酒馆/脚步" #f .4 -.2)
      (theatre-wait 2.4))
    (说 "尼尔" 1040 "演出还有多久？" 1.185)
    (说 "夜莺" 330 "就这两周了。" 1.080)
    ;; 夜莺回头（HTML f:0/d:500）。
    (theatre-parallel
      (theatre-image-to "夜莺" "Portraits/Neon/夜莺_低头")
      (theatre-tween "夜莺位置" 'scale-x 1 .5)
      (theatre-wait .5))
    (说 "尼尔" 1040 "我们得先想办法凑够赎金，只要他来拿钱，我有办法弄明白他是谁" 3.495)
    (theatre-clear-caption)
    (theatre-wait .9)
    ;; 夜莺落泪（HTML tr:1/pad()/sp，说话柔光推到她身后；pad 无采样，等待保留）。
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺_恳求")
    (theatre-parallel
      (theatre-tween "说话柔光" 'x 330 1.2 'smooth)
      (theatre-tween "说话柔光" 'opacity .26 1.2 'smooth)
      (theatre-wait 3.8))
    ;; 灯调暗，尼尔独白收束（HTML lamp(.45)/mono/w900）。
    (theatre-tween "吊灯光" 'brightness .45 .2)
    (说 "尼尔（独白）" 1040 "我自己也没想明白，为什么我会接下这个赔钱的生意，但这是这几个月难得的委托。我打算从哪里凑些钱，不过，我是不会乖乖把钱送给这个杂种的。" 7.380)
    (theatre-wait .9)))
