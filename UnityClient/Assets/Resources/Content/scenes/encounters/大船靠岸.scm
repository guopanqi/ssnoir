;; D9 临时货船：短型公共交锋，既有行动骰和回合响应。公共事件独立于玩家是否介入。
;; 成功与停卸只决定这一晚的报酬与记忆，不附加隐藏人物道德分支。
(define 货物 (make-clock "卸下货物" 6 'gauge "码头正在赶着卸完这一批急件。" :beneficial-direction 'increase))
(define 危险 (make-clock "吊索摆动" 5 'pressure "吊索失控时必须停止卸货。" :beneficial-direction 'decrease))
(define 收工钟 (make-clock "天亮之前" 4 'pressure "每回合结束推进1格，满格后结束夜班。" :beneficial-direction 'decrease))
(define 意外? #f)
(define 停止? #f)
(define (收工! kind)
  (if 停止? (error "大船：重复收工") #t)
  (set! 停止? #t)
  (play-dialogue!
    (line "世界" (if (eq? kind '满载) "最后一批货落在栈桥上，工友们放下肩上的绳索，望着彼此笑了。" "哨子响起，工头叫停了吊车。剩下的货只能等下一个班次。"))
    (line "艾迪" "歇口气吧。刚才都干得漂亮，先把手指头数齐了再去吃饭。")
    (line "世界" "食堂亮着灯，热豆汤在铁锅里冒着气。弗兰克把板凳向尼尔这边挪了挪。")
    (line "弗兰克" "坐。夜班的汤，凉了就不好喝了。"))
  (end-encounter (list kind (货物 'current))))
(define (检查!)
  (cond
    ((危险 'full?) (收工! '停卸))
    ((货物 'full?) (收工! '满载))
    ((收工钟 'full?) (收工! '停卸))
    ((and (not 意外?) (>= (货物 'current) 2))
      (set! 意外? #t)
      (危险 'advance! 1)
      (play-dialogue!
        (line "世界" "吊索猛地拧了一下，吊着的货箱开始摇晃。")
        (line "艾迪" "都往后！先把那小伙子叫出来，谁也别站在下面！")
        (line "弗兰克" "吊车停住！下面的人先撤。")))
    (else #f)))
(define (搬运! amount danger)
  (货物 'advance! amount)
  (危险 'advance! danger)
  (if (not 停止?) (检查!) #f))
(define (固定吊索!)
  (危险 'advance! -2)
  (play-bubble! (line "世界" "吊索逐渐稳住了。"))
  (检查!))
(define-opponent-rule "夜班逼近天亮"
  (lambda () (not 停止?))
  (lambda () (收工钟 'advance! 1) (检查!)))
(define (on-encounter-enter)
  (货物 'load! 0) (危险 'load! 0) (收工钟 'load! 0)
  (set! 意外? #f) (set! 停止? #f)
  (play-dialogue!
    (line "世界" "货轮靠上栈桥。成排的工人提着绳子赶过来，灯火将整个水面照得发白。")
    (line "艾迪" "先把自己的位置站稳，别抢在吊钩前头。这一晚工钱比平时好。")))
(define (on-encounter-collapse) (collapse-result (list '停卸 (货物 'current))))
(define (get-render-data)
  (container "大船靠岸" (append
    (clock-nodes (货物 'render-data) (危险 'render-data) (收工钟 'render-data))
    (list
      (node "齐心卸货" :subtitle "稳妥协作，失败冷静-1"
        :risk '稳妥 :progress-target (list 'clock "卸下货物")
        :risk-target (list 'composure "player") :requires (list (req-die))
        :resolve (roll 'violence
          (outcome (lambda () (spend-composure! 1) (搬运! 0 0)))
          (outcome (lambda () (搬运! 1 0)))
          (outcome (lambda () (搬运! 2 0)))))
      (node "抢卸高架货" :subtitle "推进更快，但可能加重吊索危险"
        :risk '危险 :progress-target (list 'clock "卸下货物")
        :risk-target (list 'clock "吊索摆动") :requires (list (req-die))
        :resolve (roll 'violence
          (outcome (lambda () (搬运! 0 2)))
          (outcome (lambda () (搬运! 2 1)))
          (outcome (lambda () (搬运! 3 0)))))
      (node "收紧吊索" :subtitle "用一次行动稳住吊车"
        :risk '稳妥 :progress-target (list 'clock "吊索摆动")
        :requires (list (req-die)) :resolve (instant (outcome 固定吊索!)))
      (instant-action "提前收工" (lambda () (收工! '停卸)))))))
