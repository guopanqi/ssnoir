 ;; 第一幕的临时 Dialogue 表现；剧情事实由 world/第一幕.scm 持有。
(define (第一幕-清晨)
  (play-dialogue!
    (line "世界" "窗帘没拉严。尼尔数了数桌上的零钱，把房租单压回下面。")
    (line "尼尔" "这点钱……")
    (line "旅馆老板" "房钱别忘了。")
    (line "尼尔" "没忘，我过两天给你。")
    (line "尼尔" "还是先去酒吧打听打听吧。")))
(define (第一幕-酒馆)
  (play-dialogue!
    (line "世界" "台上有人伴着钢琴唱歌。尼尔走到吧台前。")
    (line "调酒师" "新面孔啊。找活儿干？")
    (line "尼尔" "你这儿缺人手？")
    (line "调酒师" "缺个看场子的，拳头得硬。码头、咖啡馆也都有些活儿。")
    (line "尼尔" "码头给得多？")
    (line "调酒师" "拿体力换的，能不多么。")
    (line "世界" "尼尔望向舞台。夜莺唱完最后几句。")
    (line "尼尔" "台上那个是谁？")
    (line "调酒师" "夜莺。")
    (line "世界" "掌声响起，夜莺欠身谢幕。")))

 ;; 尼尔不覆盖 :at，沿队员身份定位自己的气泡；夜莺使用实际人物锚点。
(define (第一幕-听歌初次)
  (play-bubble!
    (line "尼尔" "唱得好。")
    (line "夜莺" "谢谢大家，歇十分钟再唱。" :at "夜莺@酒馆")))

(define (第一幕-听歌再来)
  (play-bubble!
    (line "夜莺" "今晚又来了？" :at "夜莺@酒馆")
    (line "尼尔" "嗯，来听你唱歌。")
    (line "夜莺" "那我当你是特意来捧场的了。" :at "夜莺@酒馆")))

(define (第一幕-听歌回扣 heard?)
  (if heard?
    (play-bubble!
      (line "夜莺" "这回拍子没慢吧？" :at "夜莺@酒馆")
      (line "尼尔" "挑不出毛病。"))
    (play-bubble!
      (line "夜莺" "今天这首，比上回那首怎么样？" :at "夜莺@酒馆")
      (line "尼尔" "我更喜欢这首。")
      (line "夜莺" "有品位。" :at "夜莺@酒馆"))))

(define (第一幕-听歌熟客)
  (play-bubble!
    (line "夜莺" "刚才还在想，今晚会不会见着你。" :at "夜莺@酒馆")
    (line "尼尔" "这不来了么。")))

(define (第一幕-夜莺关怀)
  (play-bubble!
    (line "夜莺" "喝口水？" :at "夜莺@酒馆")
    (line "尼尔" "谢了。")))

(define (第一幕-夜莺台边)
  (play-dialogue!
    (line "世界" "夜莺提着裙角下台，在暗处踩空了一下。尼尔伸手扶住她的胳膊。")
    (line "夜莺" "吓死我了……谢了。这台阶早该换了，老板就是不舍得花这点钱。")
    (line "尼尔" "台下太黑了。")
    (line "夜莺" "可不是么。你是最近才来的？")
    (line "尼尔" "对，我叫尼尔。")))

(define (第一幕-夜莺收工 working?)
  (play-dialogue!
    (line "世界" "夜莺坐在小舞台边，晃着鞋跟，断断续续哼着同一小段调子。")
    (line "世界" (if working? "尼尔拎着扫帚走过台前。" "尼尔走过台前，停了下来。"))
    (line "尼尔" "第三拍慢了半拍。")
    (line "世界" "哼声停了。夜莺猛地抬起头。")
    (line "夜莺" "……你听出来了？")
    (line "夜莺" "我还以为这个地方没人听得出来呢。")
    (line "世界" "她清了清嗓子，把那句重新哼了一遍。")
    (line "夜莺" "这样呢？")
    (line "尼尔" "好多了。")
    (line "夜莺" "下次唱错，别当众说啊。")
    (line "世界" "尼尔笑了。")))

(define (第一幕-夜莺解围)
  (play-dialogue!
    (line "世界" "一个穿西装的客人把几枚硬币排在桌上，冲尼尔打了个响指。")
    (line "客人" "喂，大个子。把地上的烟头扫干净，钱归你。")
    (line "世界" "尼尔站着没动，手插在口袋里。")
    (line "尼尔" "……我不是做这个的，先生。")
    (line "客人" "在这烂地方要饭，还装什么硬骨头？嫌少？")
    (line "世界" "夜莺端着空酒杯走过来，把硬币推回客人面前，笑着站到两人之间。")
    (line "夜莺" "先生，您怎么还跟我们的看场人较上劲了？")
    (line "客人" "夜莺小姐！我不过叫他干点活儿。")
    (line "夜莺" "我下一场还指望他替我看着那些喝醉的先生呢。")
    (line "世界" "旁边有人笑了。客人有些尴尬，挥了挥手。")
    (line "客人" "行了，行了。")
    (line "世界" "夜莺跟着笑了笑。客人转回桌边，尼尔重新拿起自己的东西。")
    (line "夜莺" "刚才差点以为你要揍他了。")
    (line "尼尔" "揍了他，明天我就没活儿了。")
    (line "夜莺" "我明白。")))

(define (第一幕-露西初识)
  (play-dialogue!
    (line "世界" "尼尔端着托盘转身，两杯咖啡滑向边缘。露西伸手扶住杯子。")
    (line "露西" "小心！托盘底下垫块干抹布，不然走两步就打滑。")
    (line "尼尔" "谢谢。")
    (line "露西" "新来的吧？我叫露西。")
    (line "尼尔" "尼尔。")
    (line "露西" "拿好单子，那边的客人还等着要吐司呢。")))

(define (第一幕-露西打字)
  (play-dialogue!
    (line "世界" "趁着没人点单，露西拿着铅笔，在报纸上画圈。")
    (line "尼尔" "看什么呢？")
    (line "露西" "公司招打字员。坐办公室，整天敲打字机，不用站一天，也不用洗这些杯子。")
    (line "尼尔" "你会用打字机？")
    (line "露西" "还没碰过呢。附近的夜校有班，等攒够学费我就去报名。")
    (line "世界" "前台的铃响了。露西收起报纸，擦了擦手。")
    (line "露西" "来客人了，去拿咖啡壶。")))

(define (第一幕-艾迪教搬运)
  (play-dialogue!
    (line "世界" "工头指了指货堆。尼尔伸手去抬木箱，箱子忽然往下一沉。")
    (line "艾迪" "手缩回去！别往底下扣！")
    (line "世界" "艾迪把尼尔拉开，箱子落回地上。")
    (line "艾迪" "手别伸进箱底。先找稳当的地方，别光靠胳膊硬抬。")
    (line "尼尔" "谢了。")
    (line "艾迪" "第一天来？我叫艾迪，跟我搭一组。")
    (line "尼尔" "尼尔。")))

(define (第一幕-艾迪再见)
  (play-dialogue!
    (line "世界" "吊车的铁链响着。艾迪看见尼尔，把手套递了过来。")
    (line "艾迪" "骨架没散啊，小子，又来了？")
    (line "尼尔" "码头的钞票现结，不压账。")
    (line "艾迪" "那过来顶这头，别让货箱压着绳子。")))

(define (第一幕-艾迪邀请)
  (play-dialogue!
    (line "世界" "汽笛响了，工头喊着下工。艾迪拍了拍身上的灰。")
    (line "艾迪" "嗓子里都是灰。去老街喝一杯冲冲？")
    (line "尼尔" "你掏钱？")
    (line "艾迪" "第一轮啤酒算我的。")))

(define (第一幕-艾迪喝酒)
  (play-dialogue!
    (line "世界" "艾迪端着两杯啤酒坐下，泡沫溢到了桌上。")
    (line "尼尔" "你常来这儿？")
    (line "艾迪" "这一带混饭吃的谁不来。早些年打拳，赢了来喝，输了也来喝。")
    (line "尼尔" "现在不打了？")
    (line "艾迪" "打不动了。现在也就码头还用得上这身力气。你呢，以后什么打算？")
    (line "尼尔" "先把房租凑齐，免得房东把我的箱子扔出去。")
    (line "艾迪" "都一样。码头有活儿我叫你。")))

;; D8 大剧院首次彩排：已有舞台 DSL 与音乐，区别于需要玩家判定的交锋。
;; 第一遍只是试音；第二遍真正给观众留一段听歌的时间，不用对白复述美感。
(define (第一幕-彩排)
  (define 景
    (list
      (theatre-group "剧院布景" "" 0 0)
      (theatre-polygon "舞台地面" "剧院布景" "#18171B"
        '((120 535) (1480 535) (1580 795) (20 795)))
      (theatre-polygon "天幕" "剧院布景" "#090A11"
        '((100 90) (1500 90) (1490 535) (110 535)))
      (theatre-line "台口" "剧院布景" "#B7A37C" 2
        '((100 537) (1500 537)))
      (theatre-line "前排座椅" "剧院布景" "#3D3740" 3
        '((50 810) (260 775) (480 800) (730 776) (1010 800) (1300 775) (1550 810)))
      (theatre-light "彩排追光" "" "#F2D3A5" 800 425 510 .16)
      (theatre-with (theatre-glow "舞台光池" "" "#F4D5AB" 800 630 340 85) 'opacity .1)
      (theatre-with (theatre-group "歌者位置" "" 800 650) 'opacity 1)
      (theatre-image "夜莺" "歌者位置" "Portraits/Neon/夜莺" 0 0 310 310 "彩排追光")
      (theatre-line "钢琴" "剧院布景" "#A1A4A7" 3
        '((1060 610) (1200 610) (1200 680) (1080 680)))
      (theatre-line "琴凳" "剧院布景" "#77777D" 2
        '((1085 700) (1160 700)))))
  (play-theatre! (theatre-scene 1600 900 "#07080B" 景)
    (theatre-say "舞台监督" "第一遍，先试试声音。" 2.3 "#C5C3BB")
    (theatre-sound "试音" "StageSounds/酒馆告别/最后一首" #t .24 0)
    (theatre-during
      (theatre-tween "彩排追光" 'brightness .42 4 'smooth)
      (theatre-loop (theatre-animate "歌者位置" 'rotation '((0 0) (1 .7) (2 0)))))
    (theatre-stop-sound "试音")
    (theatre-say "舞台监督" "再来一遍。这一次，不用收着。" 2.6 "#C5C3BB")
    (theatre-parallel
      (theatre-tween "彩排追光" 'brightness 1.4 2 'smooth)
      (theatre-tween "舞台光池" 'opacity .34 2 'smooth))
    (theatre-sound "第二遍" "StageSounds/酒馆告别/最后一首" #t .8 0)
    (theatre-during
      (theatre-sequence
        (theatre-wait 6)
        (theatre-image-to "夜莺" "Portraits/Neon/夜莺_仰头")
        (theatre-wait 8)
        (theatre-image-to "夜莺" "Portraits/Neon/夜莺"))
      (theatre-loop (theatre-animate "歌者位置" 'rotation
        '((0 0) (2 .8) (4 0) (6 -.8) (8 0)))))
    (theatre-stop-sound "第二遍")
    (theatre-tween "彩排追光" 'brightness .55 1.5 'smooth)
    (theatre-say "夜莺" "你什么时候来的？" 1.8 "#8FD9D0")
    (theatre-say "尼尔" "第二遍的时候。" 1.8 "#F0CF8A")
    (theatre-say "夜莺" "那你只听见好的一遍。" 2.2 "#8FD9D0")
    (theatre-say "夜莺" "我在上面的时候，看起来怎么样？" 2.7 "#8FD9D0")
    (theatre-say "尼尔" "很漂亮。" 2 "#F0CF8A")
    (theatre-parallel
      (theatre-tween "彩排追光" 'brightness 1.1 1.2)
      (theatre-tween "舞台光池" 'opacity .2 1.2))
    (theatre-say "夜莺" "我喜欢这里。" 2.5 "#8FD9D0")
    (theatre-clear-caption)
    (theatre-wait 2)))
