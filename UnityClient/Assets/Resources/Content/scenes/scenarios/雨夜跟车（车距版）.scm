;; 三次驾驶决策；每次行动后结算路况并自动进入下一段。仅测试入口。
(define 路段 0)
(define 车距 2)
(define 结束? #f)
(define 记住车牌? #f)
(define (路名) (list-ref '("剧院街区" "红灯路口" "旧港公路") 路段))
(define (路况变化) (list-ref '(0 -1 1) 路段))
(define (方向线索)
  (cond ((= 路段 0) "轿车从剧院驶向商业街。")
    ((= 路段 1) "轿车穿过商业街，向旧港开去。")
    (else "轿车驶过高架桥，进入旧港货运街区。")))
(define (结算! kind text)
  (set! 结束? #t)
  (play-dialogue! (line "世界" (string-append text
    (if 记住车牌? "尼尔已记下车牌，这条线索还在。" ""))))
  (end-scenario (list kind 记住车牌? (方向线索))))
(define (行驶! move grade)
  (if 结束? (error "跟车已经结束") #t)
  (let* ((road (路况变化))
         (delta (cond
           ((eq? grade '坏) (cond ((<= 车距 1) -1) ((>= 车距 3) 1) ((< road 0) -1) (else 1)))
           ((eq? move '维持) (if (eq? grade '好) (- road) 0))
           ((eq? move '加速) -1)
           ((eq? move '放慢) 1)
           (else (error "未知驾驶行动")))))
    ;; 行动与当前路况作为同一次机动结算，玩家事前可读净变化。
    (set! 车距 (+ 车距 delta road))
    (cond
      ((<= 车距 0) (结算! "暴露" "红色尾灯在雨里停住。司机盯着后视镜，随后突然靠边，让尼尔先过去。跟踪已经暴露。"))
      ((>= 车距 4) (结算! "跟丢" "车灯被雨幕和转弯吞没，尼尔再也没能辨认出那辆黑色轿车。"))
      (else
        (if (and (= 路段 0) (= 车距 1))
          (begin (set! 记住车牌? #t)
            (play-dialogue! (line "世界" "借着出租车的灯光，尼尔看清并记住了前车的车牌。"))) #f)
        (if (= 路段 2)
          (cond
            ((= 车距 1) (结算! "地址与人物" "黑色轿车驶入货运大楼。尼尔看清门牌，也看见一个穿黑大衣、右颊有疤的男人下车走入后门。"))
            ((= 车距 2) (结算! "确认地址" "轿车转入货运大楼后院。尼尔记下门牌，但没看清下车的人。"))
            (else (结算! "街区线索" "尼尔只看清轿车转进旧港货运街的一排院落，还需要从城市里打听具体地址。")))
          (begin
            (set! 路段 (+ 路段 1))
            (play-dialogue! (line "世界" (if (= 路段 1)
              "前车驶向十字路口，信号灯变红，车流开始减速。"
              "过了高架桥，出租车渐渐散去。前车在空旷的旧港公路上加速。")))))))))
(define (预告 move grade)
  (let* ((road (路况变化))
         (delta (cond ((eq? grade '坏) (cond ((<= 车距 1) -1) ((>= 车距 3) 1) ((< road 0) -1) (else 1)))
           ((eq? move '维持) (if (eq? grade '好) (- road) 0))
           ((eq? move '加速) -1) (else 1)))
         (n (+ 车距 delta road)))
    (cond ((<= n 0) "暴露") ((>= n 4) "跟丢") (else (number->string n)))))
(define (驾驶卡 id move)
  (node id :subtitle (string-append "好：" (预告 move '好) "；中：" (预告 move '中) "；坏：" (预告 move '坏) "（已计路况）")
    :requires (list (req-die)) :risk '冒险
    :resolve (roll 'sharpness
      (outcome (lambda () (行驶! move '坏)))
      (outcome (lambda () (行驶! move '中)))
      (outcome (lambda () (行驶! move '好))))))
(define (on-scenario-enter)
  (play-dialogue!
    (line "世界" "彩排结束，一辆黑色轿车驶离剧院。尼尔开着经理借来的车，隔着两辆出租车跟上。")
    (line "世界" "每段路只作一次驾驶决定，行动后立即结算路况、驶入下一段。车距零是暴露，四是跟丢。靠近能看清更多。")))
(define (on-scenario-collapse) (collapse-result (list "调查中断" 记住车牌? (方向线索))))
(define (get-render-data)
  (container "雨夜跟车"
    (list
      (note-node "车距版：目标" "目标" "跟完三个路段，尽可能看清目标去向。每次驾驶都会进入下一段。")
      (note-node "车距版：距离" "尾随距离" (string-append
        (number->string 车距) "：" (list-ref '("暴露" "太近" "合适" "太远" "跟丢") (max 0 (min 4 车距)))
        "。最后距离近可看清人物，适中可确认地址，远则只知街区。"))
      (note-node "车距版：道路" (路名) (list-ref
        '("出租车与电车遮掩行踪；行动后路况不改车距。贴近可记住车牌。"
          "前车遇红灯减速；行动后车距减少1。"
          "前车在空旷道路加速；行动后车距增加1。") 路段))
      (驾驶卡 "加速跟近" '加速)
      (驾驶卡 "放慢隐蔽" '放慢)
      (驾驶卡 "维持跟踪" '维持))))
