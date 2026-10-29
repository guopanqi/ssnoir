;; 独立演示：人物机会限时，调查本身不限回合；始终可艰难地自行查记录。
;; 直接询问无修正；场务帮忙3格、仅第一回合：查记录修正−3→0。
;; 独立观察修正+1，但坏结果冷静−1。帮忙不直接给线索，不锁其他路线。
(define 时间 (make-clock "距离交班结束" 3 'countdown "每回合末减1、冷静−1；第1回合末关闭交班帮忙，第2回合末门房与夜莺离开，第3回合末场务下班，仍可自行查簿。" :beneficial-direction 'increase))
(define 线索 (make-clock "跟踪线索" 8 'gauge "汇集外貌、出没时间与去向；填满取得可继续追查的地点，不确认身份。" :beneficial-direction 'increase))
(define 交班工作 (make-clock "整理交班" 3 'gauge "仅第一回合可帮忙；满格后场务陪查到第3回合末，期间查记录修正由−3变为0。" :beneficial-direction 'increase))
(define 已结束? #f)
(define 场务离开? #f)
(define (场务协助?) (and (not 场务离开?) (交班工作 'full?)))
(define 门房 (speaker "剧院侧门门房" :name "门房" :appearance '成年男性))
(define 场务 (speaker "侧门调查场务" :name "场务" :appearance '成年男性))
(define (结束! 来源)
  (if 已结束? (error "侧门调查：重复结算") #t)
  (set! 已结束? #t)
  (play-dialogue!
        (line "世界" (cond
          ((eq? 来源 '门房) "门房终于想起车门上的字。那个灰帽子男子几次坐旧旅馆的接送车来，问过夜莺的散场时间，又坐车回去。")
          ((eq? 来源 '记录) "交接单上同一辆旧旅馆接送车的号码出现了几次，车停靠的时间和那名男子来打听人的时间吻合。")
          (else "灰帽子男子问过散场时间，登上一辆车门印着旧旅馆名字的接送车。尼尔记下车号和他袖口的破口。")))
        (line "尼尔" "先去旧旅馆查车和司机。还不能说他就是雨夜那个人。"))
      (spotlight! "找到追查方向" "陌生男子与旧旅馆的接送车有关。可以继续查车和司机，姓名与来意尚不清楚。")
  (end-scenario '取得可靠线索))
(define (调查! n 来源)
  (线索 'advance! n)
  (if (线索 'full?) (结束! 来源) #f))
(define (帮忙! 钟 n 人 话)
  (钟 'advance! n)
  (if (钟 'full?) (play-dialogue! (line 人 话)) #f))
(define-opponent-rule "交班时间推进"
  (lambda () (and (not 已结束?) (not (时间 'empty?))))
  (lambda () (时间 'advance! -1)))
(define-opponent-rule "场务收起交接单"
  (lambda () (and (not 已结束?) (= (时间 'current) 2)))
  (lambda ()
    (play-dialogue! (line 场务 (if (交班工作 'full?)
      "交接单理清了，接下来我陪你查。"
      "我得把交接单收走了。值班簿还在桌上，要查你得自己找。")))))
(define-opponent-rule "门房下班与夜莺离开"
  (lambda () (and (not 已结束?) (= (时间 'current) 1)))
  (lambda ()
    (play-dialogue!
      (line "世界" "夜莺随演员们离开剧院，侧门外等人的人也散去。门房穿上外套下班，场务还在后台收拾交接单。")
      (line "尼尔" "门口问不着了。还可以去核对后台的记录。"))))
(define-opponent-rule "场务下班"
  (lambda () (and (not 已结束?) (not 场务离开?) (时间 'empty?)))
  (lambda ()
    (set! 场务离开? #t)
    (play-dialogue! (line 场务 "我下班了。值班簿留在桌上，你还想查就慢慢找，走时把门带上。"))))
(define (on-scenario-enter)
  (时间 'load! 3) (线索 'load! 0) (交班工作 'load! 0)
  (set! 已结束? #f) (set! 场务离开? #f)
  (play-dialogue!
    (line "世界" "一名工人告诉尼尔，最近有陌生男子在侧门打听夜莺什么时候离开。门房正在接待来客。")
    (line 门房 "来来往往这么多人，别让我凭一句猜测议论客人。我得先照看进出的人。")
    (line 场务 "值班簿可以看，但我现在没工夫陪你一页页找。这些钥匙和交接单还没理完。")
    (line "尼尔" "也可以自己去侧门守着，只是未必能看出什么。")
    (line "世界" "一回合后场务收走交接单；两回合后门房下班、夜莺离开。第三回合末场务下班，值班簿会留下，你仍能自己查。")))
(define (on-scenario-collapse) (collapse-result '线索不足))
;; 期限只投影主时间钟；提前关闭的动作扣除相应回合，不另存倒计时。
(define (窗口标签 名 提前关闭)
  (clock-priority
    (list 'clock 名 (max 0 (- (时间 'current) 提前关闭))
      (- (时间 'max) 提前关闭) 'countdown "归零后该行动关闭。")
    'secondary))
(define (帮助节点 id 钟 技能 人 话 期限)
  (node id :subtitle "可选帮忙，坏0／中1／好2；满后改善配合"
    :clocks (list 期限)
    :risk '稳妥 :progress-target (list 'clock (cadr (钟 'render-data)))
    :requires (list (req-die))
    :resolve (roll 技能
      (outcome (lambda () #f))
      (outcome (lambda () (帮忙! 钟 1 人 话)))
      (outcome (lambda () (帮忙! 钟 2 人 话))))))
(define (get-render-data)
  (node "首演之夜" :name "侧门的陌生人"
    :children (append
      (list
        (note-node "侧门目标" "目标" "调查陌生人的外貌、时间与去向，取得一处可以继续追查的地点。")
        (note-node "侧门窗口" "调查窗口" "第1回合末交班帮忙关闭；第2回合末询问与观察关闭；第3回合末场务下班，仍可自行查簿。每回合末冷静−1。"))
      (clock-nodes (线索 'render-data))
      (if (时间 'empty?) '() (clock-nodes (时间 'render-data)))
      (if (> (时间 'current) 1)
          (list
            (node "询问门房" :clocks (list (窗口标签 "询问剩余" 1))
              :subtitle "直接询问，无修正；坏0，中1，好2"
              :risk '稳妥 :progress-target (list 'clock "跟踪线索")
              :requires (list (req-die))
              :resolve (roll 'social
                (outcome (lambda () #f))
                (outcome (lambda () (调查! 1 '门房)))
                (outcome (lambda () (调查! 2 '门房)))))
            (node "侧门外观察" :clocks (list (窗口标签 "观察剩余" 1))
              :subtitle "观察修正+1；坏冷静−1，中1，好2"
              :risk '冒险 :risk-target (list 'composure "player")
              :progress-target (list 'clock "跟踪线索") :requires (list (req-die))
              :resolve (roll 'sharpness (lambda () (list (modifier 1 "直接目击")))
                (outcome (lambda () (spend-composure! 1)))
                (outcome (lambda () (调查! 1 '观察)))
                (outcome (lambda () (调查! 2 '观察)))))) '())
      (if 场务离开?
        (list (note-node "场务已离开" "值班簿留在桌上" "可不限回合自行查阅，判定−3；每回合结束仍消耗1冷静。"))
        (if (交班工作 'full?)
        (list (note-node "场务配合" "场务陪你查阅" "查记录修正为0，协助持续到第3回合末；之后仍可自行查簿。"))
        (if (= (时间 'current) 3)
          (append (clock-nodes (交班工作 'render-data))
            (list (帮助节点 "帮场务整理" 交班工作 'sharpness 场务 "交接单理齐了。把你听到的说说，咱们一页页对。" (窗口标签 "交班剩余" 2))))
          (list (note-node "交班帮忙关闭" "交接单已收走" "未取得场务配合；仍可自行查记录，判定−3。")))))
      (list
        (node "核对值班簿"
          :subtitle (if (场务协助?) "有人配合核对：坏0，中1，好2" "无人协助，判定−3；坏0，中1，好2")
          :risk '稳妥 :progress-target (list 'clock "跟踪线索")
          :requires (list (req-die))
          :resolve (roll 'knowledge
            (lambda () (if (场务协助?) '() (list (modifier -3 "无人协助"))))
            (outcome (lambda () #f))
            (outcome (lambda () (调查! 1 '记录)))
            (outcome (lambda () (调查! 2 '记录)))))))))
