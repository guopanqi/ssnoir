;; 七回合夜班。吊车正常时可反复起吊，玩家行动不占用工友的劳动机会。
;; 第二回合末固定事故，不归咎于玩家。工资由城市模块唯一结算。
;; 搬货立即推进；协助检修放弃眼前产出，换取玩家与艾迪更早恢复起吊。
(define 货物 (make-clock "卸下货物" 36 'gauge "东边这批急件归本组负责，离港前必须卸齐。" :beneficial-direction 'increase))
(define 收工钟 (make-clock "离港之前" 7 'pressure "回合结束：冷静-1、离港推进1格；满格未卸齐只领辛苦钱。" :beneficial-direction 'decrease))
(define 意外? #f)
(define 停止? #f)
(define 检修 (make-clock "恢复吊车" 6 'gauge "退出卡索、重新挂钩并试吊；完成后恢复起货。" :beneficial-direction 'increase))
(define (吊车可用?) (检修 'full?))
(define (收工! kind)
  (if 停止? (error "大船：重复收工") #t)
  (set! 停止? #t)
  (play-dialogue!
    (line "世界" (if (eq? kind '满载)
      "东边最后一箱落地。工头在货单上签了字，把完整加班费和奖金一起装进工资袋。"
      "离港的汽笛响了，东边还有货没卸完。工头叫停吊车，只给每人发了六块辛苦钱。"))
    (line "弗兰克" "锅还热着。都来，今晚算我的。")
    (line "世界" "工人们挤进食堂，把湿手套摊在长凳上。弗兰克端来一锅热汤，又把面包掰开放在桌中央。")
    (line "年轻工人" (if 意外? "刚才那箱子晃过来，我腿都不会动了。" "我还以为自己能一直搬到早上。"))
    (line "艾迪" (if 意外? "下次离吊钩远点。汤碗底下倒是可以站。" "先把汤喝了。明天还有得搬。"))
    (line "世界" "年轻人低头笑了。尼尔把工资袋放进内袋，接过弗兰克递来的碗。")
    (line "弗兰克" "往里坐，门口漏风。"))
  (end-encounter (list kind (货物 'current))))
(define (检查!)
  (if (and (not 停止?) (货物 'full?)) (收工! '满载) #f))
(define (搬运! amount)
  (货物 'advance! amount)
  (检查!))
(define (起吊! amount cost)
  (if (吊车可用?) #t (error "大船：吊车当前不能起吊"))
  (spend-composure! cost)
  (搬运! amount))
(define (检修! amount)
  (if (and 意外? (not (吊车可用?))) #t (error "大船：吊车无需检修"))
  (检修 'advance! amount)
  (if (吊车可用?)
    (play-dialogue!
      (line "世界" "钢索退出滑轮，吊钩重新吃住绳套。空钩试转了一圈，艾迪向司机抬起手。")
      (line "艾迪" "稳了，接着卸。")) #f))
;; 回应依次呈现：工友劳动 → 离港时间 → 事故 → 收工。
(define-opponent-rule "工友接着卸货"
  (lambda () (not 停止?))
  (lambda ()
    (if 意外?
      (if (吊车可用?)
        (begin (货物 'advance! 3)
          (play-bubble! (line "世界" "艾迪套好绳索，招呼司机把重箱送上栈桥。")))
        (begin (检修! 2)
          (if (not (吊车可用?))
            (play-bubble! (line "世界" "艾迪蹲在滑轮旁，慢慢把绞住的钢索退出来。")) #f)))
      (begin (货物 'advance! 1)
        (play-bubble! (line "世界" "年轻工人抱着货箱从尼尔身边跑过，袖口已经湿透了。"))))
    (检查!)))
(define-opponent-rule "夜班逼近离港"
  (lambda () (not 停止?))
  (lambda () (收工钟 'advance! 1)))
(define-opponent-rule "吊索卡住艾迪救人"
  (lambda () (and (not 停止?) (not 意外?) (= (收工钟 'current) 2)))
  (lambda ()
    (set! 意外? #t) (检修 'set! 0)
    (play-dialogue!
      (line "世界" "滑轮里响起一声尖啸。吊着的木箱横摆过来，年轻工人还站在下面。艾迪扑过去，一把将他拖出吊钩底下。")
      (line "艾迪" "别动。先喘口气。")
      (line "弗兰克" "停机！把卡索退出来，重新挂钩再起吊。")
      (line "世界" "弗兰克领着年轻人坐到货棚下面。艾迪捡起地上的手套，站到尼尔旁边。")
      (line "艾迪" "我来修。你先搬散货；想早点起吊，就过来搭把手。"))
    (focus-node! "大船靠岸")))
(define-opponent-rule "夜班收尾"
  (lambda () (not 停止?))
  (lambda ()
    (if (收工钟 'full?) (收工! '停卸) #f)))
(define (on-encounter-enter)
  (货物 'load! 0) (收工钟 'load! 0)
  (set! 意外? #f) (set! 停止? #f) (检修 'load! (检修 'max))
  (play-dialogue!
    (line "世界" "陌生货轮遮住了对岸的灯。工人从货棚里跑出来，吊臂在汽笛声中转向船舱。")
    (line "工头" "下个潮水就走！东边这批归你们组。卸齐，每人三十六块加班费，另加十二块奖金；没卸齐，只给六块辛苦钱。")
    (line "艾迪" "尼尔，东边缺人。你跟这小伙子一组，我去接那边的货。")
    (line "年轻工人" "散货从这儿搬。重箱得挂索，等司机起吊。")))
(define (on-encounter-collapse) (collapse-result (list '停卸 (货物 'current))))
(define (get-render-data)
  (container "大船靠岸" (append
    (list (note-node "大船目标" "目标" "赶在离港前，卸完本组负责的东侧货物。"))
    (clock-nodes (货物 'render-data) (收工钟 'render-data))
    (if (and 意外? (not (吊车可用?))) (clock-nodes (检修 'render-data)) '())
    (list
      (note-node "夜班计酬" "夜班工钱" "整批卸完：加班费36块、奖金12块；未完成：辛苦钱6块。")
      (note-node "夜班工友" (if 意外? "艾迪" "年轻工人")
        (if 意外? "年轻工人在货棚下歇着；艾迪留下接替他的活。" "年轻工人抱着货箱，来回穿过栈桥。"))
      (note-node "工友劳动规则" "回合结束"
        (if 意外? "艾迪：吊车可用时卸货+3，否则检修+2；修好当轮不卸货。" "年轻工人搬货+1。"))
      (note-node "吊车状态" "吊车"
        (if (吊车可用?) "吊车可用，每次起吊消耗一颗骰；不限制每回合次数。"
          "吊车暂停。可继续搬货，或协助艾迪检修；进度完成后恢复起吊。"))
      (node "搬运散货" :subtitle "卸货0／1／2；失手不损失冷静"
        :risk '稳妥 :progress-target (list 'clock "卸下货物")
        :requires (list (req-die))
        :resolve (roll 'violence
          (outcome (lambda () (搬运! 0)))
          (outcome (lambda () (搬运! 1)))
          (outcome (lambda () (搬运! 2)))))
      (node "跟吊起货" :subtitle "卸货0／2／3；冷静损失2／1／0"
        :disabled (not (吊车可用?))
        :risk '危险 :progress-target (list 'clock "卸下货物")
        :risk-target (list 'composure "player") :requires (list (req-die))
        :resolve (roll 'violence
          (outcome (lambda () (起吊! 0 2)))
          (outcome (lambda () (起吊! 2 1)))
          (outcome (lambda () (起吊! 3 0))))))
    (if (and 意外? (not (吊车可用?)))
      (list (node "恢复吊索" :name "协助检修"
        :subtitle "检修0／1／2；不直接推进卸货"
        :risk '稳妥 :progress-target (list 'clock "恢复吊车")
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome (lambda () (检修! 0)))
          (outcome (lambda () (检修! 1)))
          (outcome (lambda () (检修! 2)))))) '()))))
