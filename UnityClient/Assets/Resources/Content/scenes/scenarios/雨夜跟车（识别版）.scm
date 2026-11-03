;; 第三幕与独立试玩共用，复用雨夜追车情景；由城市回调接经理核查。
;; 沿用雨夜追信的推进／降压／冒险加速结构。警觉先于成功结算。
(define 已结束? #f)
(define 已过回合 0)
(define 回合上限 4)
(define 识别进度
  (make-clock "识别进度" 10 'gauge
    "记全车牌、车型和驾驶人的外貌，供经理核查身份。"
    :beneficial-direction 'increase))
(define 对方警觉
  (make-clock "对方警觉" 6 'pressure
    "对方逐渐注意到后面的车；满格立即暴露。回合结束增加1格。"
    :beneficial-direction 'decrease))

(define (结束! 结果)
  (if 已结束? (error "雨夜跟车识别版：重复结算") #t)
  (set! 已结束? #t)
  (cond
    ((eq? 结果 '取得识别线索)
      (spotlight! "记全了" "车牌、车型和驾驶人的侧脸都记下来了。这些线索可以交给经理核查，尚不能据此断定他是谁。"))
    ((eq? 结果 '跟踪暴露)
      (spotlight! "被发现了" "前车突然转进旁街。驾驶人已经发现有人跟着，记录还不足以供经理核查。"))
    (else
      (spotlight! "线索不足" "轿车驶入车流。你记下的零散特征，还不足以让经理确认身份。")))
  (end-scenario 结果))

(define (检查结果!)
  (cond
    ((对方警觉 'full?) (结束! '跟踪暴露))
    ((识别进度 'full?) (结束! '取得识别线索))
    ((>= 已过回合 回合上限) (结束! '线索不足))
    (else #f)))

(define (观察! 进度 危险)
  (识别进度 'advance! 进度)
  (对方警觉 'advance! 危险)
  (检查结果!))

(define-opponent-rule "前车逐渐驶离"
  (lambda () (not 已结束?))
  (lambda ()
    (set! 已过回合 (+ 已过回合 1))
    (对方警觉 'advance! 1)
    (play-bubble! (line "世界" "前车驶过下一个路口。驾驶人又朝后视镜看了一眼。"))
    (检查结果!)))

(define (on-scenario-enter)
  (set! 已结束? #f)
  (set! 已过回合 0)
  (识别进度 'load! 0)
  (对方警觉 'load! 0))

(define (on-scenario-collapse)
  (collapse-result '线索不足))

(define (get-render-data)
  (node "雨夜追车"
    :anchor "雨夜追车"
    :children (append
      (list
        (note-node "识别跟车：目标" "目标" "四回合内记全识别线索，交给经理核查身份；警觉满格立即暴露。")
        (note-node "识别跟车：回合规则" "观察窗口"
          (string-append "剩余" (number->string (- 回合上限 已过回合))
            "回合。回合结束警觉+1；第四回合结束仍未记全则失败。警觉与识别同时满，先判暴露。")))
      (clock-nodes (识别进度 'render-data) (对方警觉 'render-data))
      (list
        (node "辨认车辆特征"
          :anchor "雨夜追车-方向盘"
          :subtitle "隔着雨幕辨认：坏无进展且警觉+1，中识别+1，好+2。"
          :risk '冒险
          :risk-target (list 'clock "对方警觉")
          :progress-target (list 'clock "识别进度")
          :requires (list (req-die))
          :resolve (roll 'sharpness
            (outcome (lambda () (观察! 0 1)))
            (outcome (lambda () (观察! 1 0)))
            (outcome (lambda () (观察! 2 0)))))
        (node "拉开距离"
          :anchor "雨夜追车-前车"
          :subtitle "放慢融入车流：坏心情−1，中警觉−1，好−2；不增加识别进度。"
          :risk '稳妥
          :progress-target (list 'clock "对方警觉")
          :requires (list (req-die))
          :resolve (roll 'sharpness
            (outcome (lambda () (spend-actor-composure! 'player 1)))
            (outcome (lambda () (观察! 0 -1)))
            (outcome (lambda () (观察! 0 -2)))))
        (node "靠近识别"
          :anchor "雨夜追车-旁街"
          :subtitle "靠近看车牌与侧脸：警觉必增2；坏识别+0，中+2，好+3。"
          :risk '危险
          :risk-target (list 'clock "对方警觉")
          :progress-target (list 'clock "识别进度")
          :requires (list (req-die))
          :resolve (roll 'sharpness
            (outcome (lambda () (观察! 0 2)))
            (outcome (lambda () (观察! 2 2)))
            (outcome (lambda () (观察! 3 2)))))))))
