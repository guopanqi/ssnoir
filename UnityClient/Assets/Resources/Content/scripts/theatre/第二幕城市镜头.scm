;; 第二幕公共事件：入港复用场景动画，其余短镜头使用 theatre。
(define (第二幕-大船入港)
  (play-motion! "码头/货船" "Berthed" "码头-靠岸")
  (play-dialogue!
    (line "工人" "这么大一条？不是说去北港吗？")
    (line "弗兰克" "东边先接货！等吊索停稳再过去！")
    (line "艾迪" "先喝口水。船又不会自己跑掉。")
    (line "艾迪" "尼尔，跟不跟？今晚多给钱。")))
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
        (theatre-light "海报灯光" "" "#FFDDB2" 820 405 500 .24)
        (theatre-with (theatre-glow "海报追光" "" "#FFDDB2" 820 405 350 480) 'opacity .24)
        (theatre-image "夜莺海报" "剧院门口" "Portraits/Neon/夜莺" 810 495 420 500 "海报灯光")))
    (theatre-tween "海报灯光" 'brightness .48 2)
    (theatre-say "世界" "剧院门口的新海报写着：从旧港走向新城——夜莺首演。" 4 "#F0DFBC")
    (theatre-clear-caption)
    (theatre-wait 1.5)))
