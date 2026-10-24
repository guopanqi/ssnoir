;; 第二幕公共事件的三个短镜头：复用现有 theatre DSL、立绘及音效。
(define (第二幕-大船入港)
  (play-theatre!
    (theatre-scene 1600 900 "#080B10"
      (list
        (theatre-group "轮船" "" -130 0)
        (theatre-polygon "船体" "轮船" "#171C25"
          '((90 425) (1480 425) (1390 710) (260 740)))
        (theatre-line "船舷" "轮船" "#A2B1B5" 3 '((90 425) (1480 425)))
        (theatre-polygon "船楼" "轮船" "#343A45"
          '((960 215) (1200 215) (1200 425) (960 425)))
        (theatre-line "吊车" "" "#C0BDAA" 5 '((260 760) (375 140) (900 320)))
        (theatre-line "吊绳" "" "#D7CEB9" 2 '((900 320) (900 535)))
        (theatre-polygon "货箱" "" "#534B42"
          '((830 535) (970 535) (970 650) (830 650)))
        (theatre-with (theatre-glow "甲板灯" "" "#EACB8D" 1180 405 175 100) 'opacity .34)
        (theatre-with (theatre-glow "码头灯" "" "#F0CD97" 250 720 140 125) 'opacity .4)
        (theatre-line "水面" "" "#56636A" 2
          '((0 780) (310 795) (680 785) (940 808) (1600 780)))
        (theatre-polygon "前景工人左" "" "#08080C"
          '((40 860) (60 710) (108 665) (155 730) (180 900)))
        (theatre-polygon "前景工人右" "" "#09090D"
          '((1340 900) (1380 730) (1435 685) (1490 750) (1560 900)))))
    (theatre-sound "急活" "StageSounds/码头/急活" #f .6 0)
    (theatre-parallel
      (theatre-tween "轮船" 'x 0 3 'smooth)
      (theatre-tween "甲板灯" 'opacity .65 3 'smooth))
    (theatre-say "工人" "这么大一条？不是说去北港吗？" 2.5 "#DEDEDC")
    (theatre-say "弗兰克" "东边先接货！等吊索停稳再过去！" 2.7 "#C9D5CC")
    (theatre-say "艾迪" "先喝口水。船又不会自己跑掉。" 2.4 "#E8CE9F")
    (theatre-say "艾迪" "尼尔，跟不跟？今晚多给钱。" 2.3 "#E8CE9F")
    (theatre-clear-caption)
    (theatre-wait 1)))
(define (第二幕-货栈白线)
  (play-theatre!
    (theatre-scene 1600 900 "#0C0C0F"
      (list
        (theatre-group "仓库" "" 0 0)
        (theatre-polygon "砖墙" "仓库" "#252429"
          '((100 90) (1500 90) (1500 815) (100 815)))
        (theatre-line "墙缝一" "仓库" "#5A5454" 1 '((100 420) (1500 420)))
        (theatre-line "墙缝二" "仓库" "#4A4549" 1 '((100 650) (1500 650)))
        (theatre-line "测绘白线" "" "#E0D5BD" 7
          '((910 130) (890 250) (930 430) (900 600) (910 800)))
        (theatre-polygon "公告纸" "" "#BBB4A1"
          '((1005 350) (1320 350) (1320 620) (1005 620)))
        (theatre-line "木桩" "" "#CFC0A6" 5 '((550 690) (550 835)))))
    (theatre-say "世界" "昨天还在卸货的仓库，今天出现了一道测绘白线。" 3 "#E0D9CA")
    (theatre-say "工人" "我们明天还在这儿干活吗？" 2.8 "#D5D5D2")
    (theatre-clear-caption)
    (theatre-wait 1.3)))
(define (第二幕-新城海报)
  (play-theatre!
    (theatre-scene 1600 900 "#101015"
      (list
        (theatre-group "剧院门口" "" 0 0)
        (theatre-polygon "海报背景" "剧院门口" "#514D53"
          '((420 75) (1200 75) (1200 835) (420 835)))
        (theatre-line "海报金框" "剧院门口" "#C2A97B" 7
          '((420 75) (1200 75) (1200 835) (420 835) (420 75)))
        (theatre-with (theatre-glow "海报追光" "" "#FFDDB2" 820 405 350 480) 'opacity .24)
        (theatre-image "夜莺海报" "剧院门口" "Portraits/Neon/夜莺" 810 495 420 500 "海报追光")))
    (theatre-tween "海报追光" 'opacity .48 2)
    (theatre-say "世界" "剧院门口的新海报写着：从旧港走向新城——夜莺首演。" 4 "#F0DFBC")
    (theatre-clear-caption)
    (theatre-wait 1.5)))
