;; 赞助人试唱结束后进入；保留独立演示入口。第一阶段无截止；第二阶段三回合。
;; 两条预写路线每次选一条为真；错误路线满格给正确路线2格，不重复发放。
(define 公共踪迹 (make-clock "公共走廊踪迹" 4 'gauge "询问或搜寻；两格获得局部发现，满格确认路线。" :beneficial-direction 'increase))
(define 后台踪迹 (make-clock "工作通道踪迹" 4 'gauge "询问或搜寻；两格获得局部发现，满格确认路线。" :beneficial-direction 'increase))
(define 离场 (make-clock "离场去向" 6 'gauge "满格立刻追出侧门，结束搜索，取得车辆线索。" :beneficial-direction 'increase))
(define 消息 (make-clock "后台消息" 4 'gauge "每满四格取得一份情报；追出侧门后不能再打听。" :beneficial-direction 'increase))
(define 收工 (make-clock "剧院收工" 3 'countdown "第二阶段每回合结束减一；归零时车辆离开，失去车牌线索。" :beneficial-direction 'increase))
(define 真路线 '后台)
(define 阶段 1)
(define 公共发现? #f)
(define 后台发现? #f)
(define 出口发现? #f)
(define 情报数 0)
(define 结束? #f)
(define 引座员 (speaker "末排引座员" :name "引座员" :appearance '成年男性))
(define 搬运工 (speaker "末排搬运工" :name "搬运工" :appearance '成年男性))
(define 门卫 (speaker "末排侧门门卫" :name "门卫" :appearance '成年男性))
(define 场工 (speaker "末排老场工" :name "老场工" :appearance '成年男性))

(define (路线钟 route) (if (eq? route '公共) 公共踪迹 后台踪迹))
(define (局部发现! route)
  (if (eq? route '公共)
    (begin
      (set! 公共发现? #t)
      (if (eq? 真路线 '公共)
        (play-dialogue!
          (line 引座员 "有人从包厢那边走了。穿黑大衣，没等我问就拐进去了。")
          (line "尼尔" "包厢那头通哪里？")
          (line 引座员 "有条窄楼梯，平常是我们走。"))
        (play-dialogue!
          (line 引座员 "我一直在擦扶手。除了搬椅子的伙计，没人从这里过去。")
          (line "尼尔" "我再往里看看。"))))
    (begin
      (set! 后台发现? #t)
      (if (eq? 真路线 '后台)
        (play-dialogue!
          (line 搬运工 "刚才确实有一个。黑大衣，走得挺快。我还以为是经理的客人。")
          (line "尼尔" "往哪边？")
          (line 搬运工 "升降平台那头。小心脚下。"))
        (play-dialogue!
          (line 搬运工 "我这车堵在门口半天了，没见你说的人。")
          (line "尼尔" "旁边还有路吗？")
          (line 搬运工 "包厢那边有道楼梯。"))))))

(define (进入后台!)
  (set! 阶段 2)
  (refresh-scenario-dice!)
  (收工 'load! 3)
  (play-dialogue!
    (line "世界" "（上层平台。楼梯尽头，黑大衣刚推开一道窄门。）")
    (line "尼尔" "喂！等一下！")
    (line "世界" "（男人回头一瞥，随即下楼。岔口两边没有标牌，他径直推开藏在布景背后的门。）")
    (line "尼尔" "他怎么知道那后面还有路……")
    (line "世界" "（尼尔绕过布景，来到下层后台。试唱已经结束，场务推着道具往侧门走。）")
    (line 门卫 "借过，先生。我们收工了，侧门一会儿就锁。")
    (line "尼尔" "刚才有个穿黑大衣的人从这里经过？")
    (line 门卫 "问问外头装车的伙计。我得先把这些东西放出去。")))

(define (搜寻! route amount)
  (let ((clock (路线钟 route)))
    (clock 'advance! amount)
    (if (and (>= (clock 'current) 2)
             (not (if (eq? route '公共) 公共发现? 后台发现?)))
      (局部发现! route) #f)
    (if (clock 'full?)
      (if (eq? route 真路线)
        (进入后台!)
        (begin
          ((路线钟 真路线) 'advance! 2)
          (play-dialogue!
            (line "世界" "（走廊尽头的出口锁着，门前积灰完整。那个人没有走这里。）")
            (line "尼尔" "走错了。")
            (line "世界" "（工作人员打开旁边的连接门，指向另一侧的上层平台。）"))
          (if ((路线钟 真路线) 'full?) (进入后台!) #f))) #f)))

(define (结算结果 success?) (list (if success? '车辆线索 '错失车辆) 情报数))
(define (收场! success?)
  (if 结束? (error "最后一排：重复结算") #t)
  (set! 结束? #t)
  (if success?
    (play-dialogue!
      (line "世界" "（侧门外，雨刚停。黑大衣弯腰坐进一辆灰色轿车，手里的银色打火机闪了一下。）")
      (line "尼尔" "等一等！")
      (line "世界" "（车门关上。尼尔追到路灯下，看清车门上的联城车行标志，以及车牌末尾的四七二。）")
      (line "尼尔" "联城车行，四七二……这回有东西可以查了。"))
    (play-dialogue!
      (line "世界" "（侧门外只剩下装完道具的推车。街道尽头，两点车灯正在远去。）")
      (line "尼尔" "刚才有辆车在这里等人？")
      (line 门卫 "黑大衣那个？刚上车。你来晚了。")
      (line "尼尔" "车牌呢？")
      (line 门卫 "没留意。")))
  (play-dialogue!
    (line "世界" "（后台。夜莺已换下演出服，坐在椅子上揉脚。）")
    (line "夜莺" "你去哪儿了？我还以为你走了。")
    (line "尼尔" "后排坐着一个穿黑大衣的人。我看见了他手里的银色打火机。")
    (line "夜莺" "你认识他？")
    (line "尼尔" "不认识。我跟了上去，没看清脸。他倒很熟悉这里，知道布景后面那道门。")
    (line "夜莺" "那里连我都没走过。"))
  (if success?
    (play-dialogue!
      (line "尼尔" "他从侧门上了一辆灰色轿车。车门写着联城车行，车牌最后三位是四七二。")
      (line "夜莺" "你记住了？")
      (line "尼尔" "记住了。可以从车行问起。"))
    (play-dialogue!
      (line "尼尔" "他从侧门坐车走了。我晚了一步，没记下车牌。")
      (line "夜莺" "会不会只是来听试唱的人？")
      (line "尼尔" "有可能。可他看见我过去，就起身走了。")))
  (play-dialogue!
    (line "世界" "（夜莺停下手里的动作，望向门外。）")
    (line "夜莺" "今天唱的时候，我一直觉得有人在看我。")
    (line "尼尔" "你还要在这里待多久？")
    (line "夜莺" "马上就好。这双鞋，我实在不想再穿了。")
    (line "尼尔" "我等你。出去吃点东西吧。"))
  (end-scenario (结算结果 success?)))

(define (查去向! amount)
  (离场 'advance! amount)
  (if (离场 'full?) (收场! #t)
    (if (and (>= (离场 'current) 3) (not 出口发现?))
      (begin
        (set! 出口发现? #t)
        (play-dialogue!
          (line 门卫 "有人替他打电话叫了车。是哪一家我没听清，装车的伙计在门外见过他。")
          (line "尼尔" "那我得赶紧问问。"))) #f)))
(define (听消息! amount)
  (let ((total (+ (消息 'current) amount)))
    (消息 'advance! amount)
    (if (>= total 4)
      (begin
        (set! 情报数 (+ 情报数 1))
        (add-item! "情报" 1)
        (消息 'set! (- total 4))
        (if (= 情报数 1)
          (play-dialogue!
            (line 场工 "换景机又响了。上星期报上去，到现在也没人换那只磨坏的轮子。")
            (line "尼尔" "不是检查过了？")
            (line 场工 "检查过。修没修好是另一回事。"))
          (play-dialogue!
            (line 场工 "报社的人来过两趟了，还没开演就要我们讲后台的事。")
            (line "尼尔" "你告诉他们了？")
            (line 场工 "我让他们先问问，这月加班费什么时候发。")))) #f)))

(define-opponent-rule "剧院逐渐收工"
  (lambda () (and (= 阶段 2) (not 结束?)))
  (lambda ()
    (收工 'advance! -1)
    (if (收工 'empty?) (收场! #f) #f)))
(define (on-scenario-collapse) (collapse-result (list '倒下 情报数)))
(define (on-scenario-enter)
  (公共踪迹 'load! 0) (后台踪迹 'load! 0)
  (离场 'load! 0) (消息 'load! 0) (收工 'load! 3)
  (set! 真路线 (random-choice '(公共 后台)))
  (set! 阶段 1) (set! 公共发现? #f) (set! 后台发现? #f)
  (set! 出口发现? #f) (set! 情报数 0) (set! 结束? #f)
  (play-dialogue!
    (line "世界" "（大剧院。赞助人试唱结束，夜莺走下舞台，尼尔回到观众席取自己的外套。）")
    (line "夜莺" "等我换了鞋，一起走。")
    (line "世界" "（琴师合上谱子。最后几排的暗处，一个黑大衣男人合上银色打火机。）")
    (line "尼尔" "那只打火机……")
    (line "世界" "（尼尔起身走向后排。男人站起来，消失在侧面的通道里。座椅旁还有烟味。）")
    (line "尼尔" "刚才坐在这里的人呢？黑大衣，戴帽子的。")
    (line 引座员 "没注意。您到走廊看看？")
    (line "世界" (if (eq? 真路线 '公共)
      "（公共走廊的地毯上有新踩出的湿脚印，包厢门还虚掩着。工作通道里，木箱堆在窄门前。）"
      "（工作通道的窄门正在回摆，门内有人喊借过。公共走廊的引座员仍在擦那段扶手。）"))))

(define (路线节点 route title)
  (let ((clock (路线钟 route)))
    (if (clock 'full?)
      (note-node (string-append title "结论") title "这里没有通向他的踪迹；连接口已找到。")
      (container title (list
        (clock-node (string-append title "进度") (clock 'render-data))
        (node (string-append title "询问") :name (if (eq? route '公共) "询问引座员" "询问搬运工")
          :subtitle "失败无进展；中1格，好2格"
          :risk '稳妥 :progress-target (list 'clock (if (eq? route '公共) "公共走廊踪迹" "工作通道踪迹"))
          :requires (list (req-die)) :resolve (roll 'social
            (outcome (lambda () (搜寻! route 0)))
            (outcome (lambda () (搜寻! route 1)))
            (outcome (lambda () (搜寻! route 2)))))
        (node (string-append title "搜寻") :name (if (eq? route '公共) "沿走廊寻找" "穿过工作通道")
          :subtitle "失败冷静−1；中2格，好3格"
          :risk '冒险 :risk-target (list 'composure "player")
          :progress-target (list 'clock (if (eq? route '公共) "公共走廊踪迹" "工作通道踪迹"))
          :requires (list (req-die)) :resolve (roll 'sharpness
            (outcome (lambda () (spend-composure! 1)))
            (outcome (lambda () (搜寻! route 2)))
            (outcome (lambda () (搜寻! route 3))))))))))
(define (get-render-data)
  (container "最后一排的观众"
    (if (= 阶段 1)
      (list
        (note-node "末排目标" "目标" "查清黑大衣走了哪条路。两路各四格，两格获得局部发现；本阶段没有截止时间。")
        (路线节点 '公共 "公共走廊")
        (路线节点 '后台 "工作通道"))
      (append
        (list (note-node "末排目标" "目标" "查清黑大衣的离场去向；满格立即追出侧门，关闭情报搜集。")
          (clock-node "收工期限" (收工 'render-data))
          (clock-node "离场调查进度" (离场 'render-data))
          (clock-node "后台消息进度" (消息 'render-data))
          (node "打听离场去向" :subtitle "失败冷静−1；中1格，好2格"
            :risk '冒险 :risk-target (list 'composure "player")
            :progress-target (list 'clock "离场去向")
            :requires (list (req-die)) :resolve (roll 'social
              (outcome (lambda () (spend-composure! 1)))
              (outcome (lambda () (查去向! 1)))
              (outcome (lambda () (查去向! 2)))))
          (node "打听后台消息" :subtitle "失败冷静−1；中1格，好2格；每满4格情报+1"
            :risk '冒险 :risk-target (list 'composure "player")
            :progress-target (list 'clock "后台消息")
            :requires (list (req-die)) :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1)))
              (outcome (lambda () (听消息! 1)))
              (outcome (lambda () (听消息! 2))))))))))
