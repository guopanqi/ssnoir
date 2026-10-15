;; 同步 Claude 当前第二场：人物进退、对白停顿、灯光与镜头缓推。
;; 参考：experiments/portrait-stage/portrait-theatre-claude/src/js/chapters/02-indoor-plea.js。
;; 保留原有 24 句正式对白及停留秒；人物关节表演用既有立绘的关键姿势表达。
;; 剧场结束后的无配音内心旁白由 commission-dialogue! 播放，文字在 Narrations/序章接案后.json。
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

  (define (镜头 scale x y seconds)
    (theatre-parallel
      (theatre-tween "镜头" 'scale-x scale seconds 'smooth)
      (theatre-tween "镜头" 'scale-y scale seconds 'smooth)
      (theatre-tween "镜头" 'x (* (- 1 scale) x) seconds 'smooth)
      (theatre-tween "镜头" 'y (* (- 1 scale) y) seconds 'smooth)))
  (define (走 id x seconds hold)
    (theatre-parallel
      (theatre-tween id 'x x seconds 'smooth)
      (theatre-sound "走动脚步" "StageSounds/老街酒馆/脚步" #f .35 0)
      (theatre-wait hold)))
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

  ;; 小手包挂在架上，收拢的伞靠墙落地；都保持轻巧的线条轮廓。
  (线 "衣帽架杆" "房间" "#7F95C4" 5 '((470 790) (470 470)) .9)
  (线 "衣帽架底" "房间" "#7F95C4" 5 '((424 790) (516 790)) 1)
  (线 "衣帽架钩" "房间" "#7F95C4" 3.5
    '((436 546) (470 500) (504 546) (470 500) (470 560) (440 602) (470 560) (500 602)) 1.1)
  (加入 (theatre-group "架上手包" "房间" 440 585))
  (加入 (theatre-line "手包轮廓" "架上手包" "#7F95C4" 1.5
    '((-18 0) (18 0) (21 30) (-21 30) (-18 0))))
  (加入 (theatre-line "手包提手" "架上手包" "#7F95C4" 1.5
    '((-11 0) (-8 -14) (0 -20) (8 -14) (11 0))))
  (加入 (theatre-line "手包翻盖" "架上手包" "#7F95C4" 1
    '((-18 3) (0 13) (18 3))))
  (加入 (theatre-group "墙边雨伞" "房间" 520 790))
  (加入 (theatre-line "雨伞轮廓" "墙边雨伞" "#7F95C4" 1.5
    '((0 0) (4 -23) (-20 -152) (-26 -161) (-35 -161) (-39 -154)
      (-35 -146) (-29 -146) (-26 -151) (-20 -152) (-6 -25) (0 0))))
  (加入 (theatre-line "雨伞折线" "墙边雨伞" "#7F95C4" 1
    '((-20 -152) (0 0))))
  ;; 根分组就是镜头：道具、人物与灯一起推近，字幕保持原位置。
  (set! 图形 (cons (theatre-group "镜头" "" 0 0)
    (map (lambda (object)
      (if (and (equal? (cadr object) "") (not (eq? (list-ref object 2) 'focus)))
        (cons (car object) (cons "镜头" (cddr object))) object)) 图形)))
  (play-theatre! (theatre-scene 1600 900 "#0B0907" 图形)
    (theatre-during
      (theatre-sequence
        (theatre-wait .5) (闪)
        (theatre-during (theatre-wait .6) (theatre-tween "吊灯光" 'brightness 1 .2))
        (theatre-parallel
          (theatre-tween "夜莺位置" 'opacity 1 .001)
          (走 "夜莺位置" 400 2.2 2.4))
        ;; 向桌前推进的 26 秒镜头与第一段谈话同时进行。
        (theatre-during
          (theatre-sequence
            (走 "夜莺位置" 560 1.6 1.8)
            (theatre-wait .7)
            (theatre-during (theatre-wait .6) (theatre-tween "照片" 'opacity 1 .3))
            (说 "尼尔" 1040 "这张照片，嗯，是有些出格，但也没什么特别的，你为什么这么紧张？" 3.705)
            (theatre-during (说 "夜莺" 610 "他手上也许不止这一张，这种照片对普通人来说没什么，可我过些天有一场演出，这是我第一场大型的演出，这肯定会毁了我的" 6.330) (走 "夜莺位置" 610 .9 .9))
            (theatre-image-to "尼尔" "Portraits/Neon/尼尔_点烟")
            (theatre-during (说 "尼尔" 1040 "会不会是一个恶作剧，有些小混混会四处搜集照片，然后写一堆这样的信，如果恰巧有人有一些不愿意别人知道的故事的话" 6.120)
              (theatre-sound "点烟" "StageSounds/室内求助/点烟" #f 1 0)
              (theatre-sequence (theatre-wait 1.5)
                (theatre-tween "吊灯光" 'brightness .7 .2)
                (theatre-wait .5) (theatre-tween "吊灯光" 'brightness 1 .2)))
            (theatre-wait .5)
            (说 "夜莺" 610 "唉，但我这次真的不能冒险，万一他真的有些什么……" 2.970) (说 "尼尔" 1040 "你能想到，你身边有什么人会干这种事吗？" 2.445)
            (theatre-clear-caption)
            (theatre-image-to "夜莺" "Portraits/Neon/夜莺_背身")
            (theatre-during (走 "夜莺位置" 540 1.3 2.4)
              (theatre-sound "钟表" "StageSounds/室内求助/钟表" #f 1 0))
            (说 "夜莺" 540 "……没有，我和他们很久都不联系了" 2.130)
            (theatre-image-to "夜莺" "Portraits/Neon/夜莺_低头")
            (theatre-during (theatre-sequence (说 "尼尔" 1040 "他们？" 0.765) (说 "夜莺" 590 "过去的邻居和朋友什么的，不会是他们" 2.235))
              (走 "夜莺位置" 590 .9 .9))
            (说 "尼尔" 1040 "报警呢？" 0.870) (说 "夜莺" 590 "不行，我经理会知道的，最好也别让他知道" 2.445)
            (theatre-wait .3)
            (theatre-during (说 "尼尔" 990 "还有一个方法，他要得不算多，不是什么大钱，把钱给他也许他就不会再纠缠了。" 4.230)
              (theatre-sound "点烟" "StageSounds/室内求助/点烟" #f 1 0)
              (走 "尼尔位置" 990 1.2 1.2))
            (说 "夜莺" 590 "你不是一个侦探吗？" 1.395) (说 "尼尔" 990 "我很少见到要这么少的勒索，让我来办也要付给我钱，恐怕未必划算" 3.600) (说 "尼尔" 990 "但是话说回来，拿到钱他也未必肯停手，那我们就必须弄清楚他是谁了" 3.705)
            (theatre-clear-caption)
            (走 "夜莺位置" 640 .7 .7)
            (theatre-during (theatre-wait 1.2)
              (theatre-tween "首饰" 'opacity 1 .3)
              (theatre-sound "首饰" "StageSounds/室内求助/首饰" #f 1 0))
            (theatre-during (说 "夜莺" 640 "说实话，我把首饰也当了，也凑不够他信里要的钱。我其实根本就没办法" 3.810) (theatre-tween "吊灯光" 'brightness .8 .2))
            (说 "尼尔" 990 "那我的报酬呢？你准备怎么付我的报酬？" 2.340)
            (走 "夜莺位置" 570 .9 1.7)
            (theatre-image-to "夜莺" "Portraits/Neon/夜莺_恳求")
            (说 "夜莺" 570 "演出之后可以吗？经理说过些天会有一场大型的演出，应该会有些钱。虽然之前很多次小一些的演出，他也没给过我什么钱" 6.120))
          (镜头 1.06 928 576 26))
        (theatre-during
          (theatre-sequence
            (theatre-image-to "夜莺" "Portraits/Neon/夜莺_背身")
            (theatre-during (说 "夜莺" 570 "当时他说会有些钱的，但是也没给" 2.025) (theatre-tween "夜莺位置" 'scale-x -1 .7))
            (说 "夜莺" 570 "这次机会很重要，我不愿意错过" 1.920)
            (theatre-clear-caption) (theatre-wait 2.6)
            (theatre-image-to "夜莺" "Portraits/Neon/夜莺_掩面")
            (theatre-during (theatre-wait .9) (theatre-tween "夜莺位置" 'scale-x 1 .7))
            (theatre-image-to "夜莺" "Portraits/Neon/夜莺_低头")
            (说 "夜莺" 570 "我明白，确实太麻烦你了" 1.605))
          (镜头 1.1 832 576 6))
        (theatre-during
          (theatre-sequence
            (theatre-during (说 "夜莺" 570 "谢谢你，尼尔先生" 1.290)
              (theatre-tween "首饰" 'opacity 0 .3)
              (theatre-tween "吊灯光" 'brightness 1 .2))
            (theatre-clear-caption)
            (theatre-image-to "夜莺" "Portraits/Neon/夜莺_背身")
            (theatre-during (走 "夜莺位置" 420 1.5 1.7)
              (theatre-tween "夜莺位置" 'scale-x -1 .7))
            ;; 站在架前背身取包；轮廓离开挂钩，淡出代表拿到手里。
            (theatre-during (theatre-wait 1.1)
              (theatre-sequence
                (theatre-tween "夜莺位置" 'rotation 5 .3 'smooth)
                (theatre-wait .2)
                (theatre-tween "夜莺位置" 'rotation 0 .6 'smooth))
              (theatre-sequence
                (theatre-wait .35)
                (theatre-parallel
                  (theatre-tween "架上手包" 'x 424 .65 'smooth)
                  (theatre-tween "架上手包" 'y 602 .65 'smooth)
                  (theatre-tween "架上手包" 'opacity 0 .65))))
            (theatre-during (theatre-wait .7)
              (theatre-tween "夜莺位置" 'rotation 4 .3 'smooth)
              (theatre-tween "墙边雨伞" 'x 490 .7 'smooth)
              (theatre-tween "墙边雨伞" 'y 760 .7 'smooth)))
          (镜头 1.03 800 576 3))
        (theatre-during
          (theatre-sequence
            ;; 取伞尚未结束，尼尔叫住她；她收住动作，转身背对衣帽架回应。
            (theatre-during (说 "尼尔" 990 "演出还有多久？" 1.185)
              (theatre-sequence
                (theatre-parallel
                  (theatre-tween "墙边雨伞" 'opacity 0 .45)
                  (theatre-tween "墙边雨伞" 'x 470 .45 'smooth)
                  (theatre-tween "夜莺位置" 'rotation 0 .45 'smooth))
                (theatre-wait .15)
                (theatre-image-to "夜莺" "Portraits/Neon/夜莺_低头")
                (theatre-tween "夜莺位置" 'scale-x 1 .3 'smooth)))
            (说 "夜莺" 420 "就这两周了。" 1.080)
            (theatre-image-to "尼尔" "Portraits/Neon/尼尔_抱臂")
            (theatre-during (说 "尼尔" 960 "我们得先想办法凑够赎金，只要他来拿钱，我有办法弄明白他是谁" 3.495) (走 "尼尔位置" 960 1.4 1.4))
            (theatre-clear-caption) (theatre-wait .9)
            (theatre-image-to "夜莺" "Portraits/Neon/夜莺_恳求")
            (theatre-during (theatre-wait 4.2)
              (theatre-sound "收束低弦" "StageSounds/室内求助/收束低弦" #f 1 0)
              (theatre-tween "吊灯光" 'brightness .7 .2)
              (theatre-tween "说话柔光" 'x 410 1.2 'smooth)
              (theatre-tween "说话柔光" 'opacity .26 1.2 'smooth)))
          (镜头 1 800 558 3)))
      (apply theatre-parallel (cons (theatre-tween "房间" 'opacity 1 1.5) 描绘))
      (theatre-sound "窗外雨" "StageSounds/雨夜求助/雨" #t .1 0))))
