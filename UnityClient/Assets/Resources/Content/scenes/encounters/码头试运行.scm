;; 《试运行》——三个现场异常同时出现。未处理的异常每回合一起侵蚀系统稳定；
;; 玩家必须决定先救哪一处，也必须决定主要依靠参数还是老乔的现场经验。

(define 系统稳定
  (make-clock "系统稳定" 8 'countdown
    "每回合结束，每个未处理异常都会消耗一格；归零时系统崩溃。"))
(define 偏载异常 (make-clock "偏载货箱" 5 'gauge "填满后，重心异常解除。"))
(define 钢索异常 (make-clock "钢索异响" 5 'gauge "填满后，吊索恢复稳定。"))
(define 混装异常 (make-clock "混装货物" 5 'gauge "填满后，系统能够识别这批货。"))

(define 偏载参数 0)
(define 偏载经验 0)
(define 钢索参数 0)
(define 钢索经验 0)
(define 混装参数 0)
(define 混装经验 0)
(define 已结束? #f)

(define (未解决数)
  (+ (if (偏载异常 'full?) 0 1)
     (if (钢索异常 'full?) 0 1)
     (if (混装异常 'full?) 0 1)))

(define (实际推进! clk n)
  (let ((before (clk 'current)))
    (clk 'advance! n)
    (- (clk 'current) before)))

(define (记贡献! issue route n)
  (cond
    ((and (equal? issue '偏载) (equal? route '参数)) (set! 偏载参数 (+ 偏载参数 n)))
    ((and (equal? issue '偏载) (equal? route '经验)) (set! 偏载经验 (+ 偏载经验 n)))
    ((and (equal? issue '钢索) (equal? route '参数)) (set! 钢索参数 (+ 钢索参数 n)))
    ((and (equal? issue '钢索) (equal? route '经验)) (set! 钢索经验 (+ 钢索经验 n)))
    ((and (equal? issue '混装) (equal? route '参数)) (set! 混装参数 (+ 混装参数 n)))
    ((and (equal? issue '混装) (equal? route '经验)) (set! 混装经验 (+ 混装经验 n)))
    (#t (error "码头试运行：未知贡献来源"))))

(define (参数胜数)
  (+ (if (> 偏载参数 偏载经验) 1 0)
     (if (> 钢索参数 钢索经验) 1 0)
     (if (> 混装参数 混装经验) 1 0)))

(define (经验胜数)
  (+ (if (> 偏载经验 偏载参数) 1 0)
     (if (> 钢索经验 钢索参数) 1 0)
     (if (> 混装经验 混装参数) 1 0)))

(define (全部解决?) (= (未解决数) 0))

(define (成功!)
  (if 已结束? (error "码头试运行：交锋已经结算") #t)
  (set! 已结束? #t)
  (play-dialogue!
    (line "世界" "最后一盏警示灯熄了。吊臂重新抬起货箱，稳稳送过旧轨。")
    (line "林" "它能跑。不是在图纸上——在这里也能跑。")
    (line "世界" "老乔看着货箱落地，只把手套重新塞回腰带。"))
  (end-encounter (list '成功 (参数胜数) (经验胜数))))

(define (检查成功!)
  (if (and (not 已结束?) (全部解决?)) (成功!) #f))

(define (处理结果! clk issue route amount stability-loss note)
  (let ((gained (实际推进! clk amount)))
    (记贡献! issue route gained)
    (if (> stability-loss 0) (系统稳定 'advance! (- 0 stability-loss)) #f)
    (result-note! note)
    (检查成功!)))

(define (异常动作 name subtitle skill clk issue route)
  (node name
    :subtitle subtitle
    :requires (list (req-die))
    :resolve (roll skill
      (outcome "引起连锁震动"
        (lambda () (处理结果! clk issue route 1 1 "推进 1 格；稳定 -1")))
      (outcome "暂时压住"
        (lambda () (处理结果! clk issue route 1 0 "异常推进 1 格")))
      (outcome "找准症结"
        (lambda () (处理结果! clk issue route 2 0 "异常推进 2 格"))))))

(define (已解决节点 name text route-text)
  (note-node (string-append "标注：已解决-" name) name
    (string-append text "；" route-text)))

(define (偏载节点)
  (if (偏载异常 'full?)
      (已解决节点 "偏载货箱" "重心读数已经稳定"
        (if (> 偏载经验 偏载参数) "主要靠现场经验" "主要靠参数校准"))
      (node "处理偏载货箱"
        :subtitle "重量未超标，重心读数却不断漂移"
        :children (list
          (异常动作 "重算货箱配重" "根据传感器重建重心模型" 'knowledge 偏载异常 '偏载 '参数)
          (异常动作 "看轮印和绑绳" "老乔说重量方向写在轮印和绳结上" 'sharpness 偏载异常 '偏载 '经验)))))

(define (钢索节点)
  (if (钢索异常 'full?)
      (已解决节点 "钢索异响" "吊索不再往滑轮里吃"
        (if (> 钢索经验 钢索参数) "主要靠现场经验" "主要靠参数校准"))
      (node "处理钢索异响"
        :subtitle "仪表仍在安全线内，钢索却发出闷响"
        :children (list
          (异常动作 "检查张力曲线" "从仪表变化寻找受力异常" 'knowledge 钢索异常 '钢索 '参数)
          (异常动作 "听钢索的声音" "老乔说它快要吃进滑轮了" 'sharpness 钢索异常 '钢索 '经验)))))

(define (混装节点)
  (if (混装异常 'full?)
      (已解决节点 "混装货物" "机器接受了这批非标准货"
        (if (> 混装经验 混装参数) "主要靠现场经验" "主要靠参数校准"))
      (node "处理混装货物"
        :subtitle "同一托盘混着三类系统未定义的货物"
        :children (list
          (异常动作 "重做货物分类" "补齐系统没有见过的组合" 'knowledge 混装异常 '混装 '参数)
          (异常动作 "问过去怎么堆" "让老乔按旧码头规矩拆开这批货" 'social 混装异常 '混装 '经验)))))

(define (崩溃!)
  (if 已结束? (error "码头试运行：交锋已经结算") #t)
  (set! 已结束? #t)
  (spotlight! "系统崩溃"
    "异常互相拖累，控制器切断了整条线。机器停在旧轨中央，这次试运行失败了。")
  (end-encounter '崩溃))

(define-turn-rule "并发异常侵蚀系统"
  (lambda () (not 已结束?))
  (lambda ()
    (let ((pressure (未解决数)))
      (if (> pressure 0) (系统稳定 'advance! (- 0 pressure)) #f)
      (if (系统稳定 'empty?) (崩溃!) #f))))

(define (on-encounter-enter)
  (set! 已结束? #f)
  (set! 偏载参数 0) (set! 偏载经验 0)
  (set! 钢索参数 0) (set! 钢索经验 0)
  (set! 混装参数 0) (set! 混装经验 0)
  (系统稳定 'set! 8)
  (偏载异常 'set! 0)
  (钢索异常 'set! 0)
  (混装异常 'set! 0)
  (play-dialogue!
    (line "世界" "前三只标准货箱缓慢越过旧轨。机器停顿、校正，却没有叫人冒险上手。")
    (line "林" "慢了四分钟。可它自己做完了。")
    (line "世界" "第四只货箱刚抬起，三盏警示灯几乎同时亮了。")))

(define (on-encounter-collapse)
  (collapse-result '崩溃))

(define (get-render-data)
  (node "码头试运行"
    :anchor "三号货栈工棚"
    :children (append
      (clock-nodes
        (系统稳定 'render-data)
        (偏载异常 'render-data)
        (钢索异常 'render-data)
        (混装异常 'render-data))
      (list (偏载节点) (钢索节点) (混装节点)))))
