;; 《上午十一点零七分》——在报社地下废稿间重建首演日上午的材料流转。
;; 没有倒计时和失败线。玩家付出的代价是回合：方向不对，调查就会拖得更久。
;; 结构不是并列搜四堆，而是把一个模糊概念逐层弄清：1 堆 → 2 类 → 4 堆。

(define 阶段 0) ; 0 混在一起 / 1 来件与成稿 / 2 四个具体纸堆
(define 已结束? #f)
(define 找到封套? #f)
(define 找到校样? #f)
(define 拿过八卦情报? #f)
(define 拿过退稿情报? #f)

(define 混杂废稿
  (make-clock "理清这批废稿" 8 'gauge
    "填满后，先分清哪些是送进来的，哪些是报社写出的。"))
(define 外来材料
  (make-clock "分清外来材料" 6 'gauge
    "按收件章和递送渠道，把外面送来的东西归到一起。"))
(define 报社稿件
  (make-clock "分清报社稿件" 6 'gauge
    "按稿号和版次，把编辑写出的东西归到一起。"))
(define 剧院酒店来件
  (make-clock "剧院酒店来件" 6 'gauge
    "这里最可能留下科尔送来那只文件袋。"))
(define 名流社交来件
  (make-clock "名流社交来件" 6 'gauge
    "与首演关系不大，但匿名爆料也能换钱。"))
(define 四号桌校样
  (make-clock "四号桌校样" 6 'gauge
    "十一点零七分的材料正是送到这张桌上。"))
(define 未刊稿与退稿
  (make-clock "未刊稿与退稿" 6 'gauge
    "市政、警局和港口不愿见报的消息堆在这里。"))

(define (推进! clk n)
  ;; 钟的落行由引擎自动写（"纸山 +1"这类），这里不再另写一行复述。
  (clk 'advance! n))

(define (整理动作 name anchor subtitle skill clk)
  (node name
    :anchor anchor
    :subtitle subtitle
    :requires (list (req-die))
    :resolve (roll skill
      (outcome (lambda ()
          (spend-actor-composure! 'player 1)))
      (outcome (lambda () (推进! clk 1)))
      (outcome (lambda () (推进! clk 2))))))

(define (同步阶段!)
  (cond
    ((and (= 阶段 0) (混杂废稿 'full?))
     (set! 阶段 1)
     (play-dialogue!
       (line "尼尔" "先别找名字。先分清什么是送来的，什么是他们自己写的。")
       (line "世界" "那座纸山终于裂成两半：外来材料，以及报社稿件。")))
    ((and (= 阶段 1) (外来材料 'full?) (报社稿件 'full?))
     (set! 阶段 2)
     (play-dialogue!
       (line "世界" "收件章露出了来源，编辑记号也露出了桌号。")
       (line "尼尔" "现在不是找一张纸。是选四堆里该翻哪一堆。")))
    (#t #f)))

(define-opponent-rule "废稿逐渐分门别类"
  (lambda () (not 已结束?))
  (lambda () (同步阶段!)))

(define (取得封套!)
  (if (and (剧院酒店来件 'full?) (not 找到封套?))
      (begin
        (set! 找到封套? #t)
        (play-dialogue!
          (line "世界" "一只压扁的牛皮封套夹在剧院照片底下。")
          (line "尼尔" "十一点零七分。夜莺，首演。经格兰德酒店信格，转四号桌。")
          (line "世界" "封套右下角还有同一个稿号：四一七。"))
        (result-supplement! "证据：11:07 收件封套"))
      #f))

(define (取得校样!)
  (if (and (四号桌校样 'full?) (not 找到校样?))
      (begin
        (set! 找到校样? #t)
        (play-dialogue!
          (line "世界" "四号桌的废稿里压着稿号四一七的第一版校样。")
          (line "尼尔" "十一点三十五分。标题已经写着死亡威胁。")
          (line "尼尔" "上台时间、换装顺序——和下午那封信里一样。"))
        (result-supplement! "证据：11:35 第一版校样"))
      #f))

(define (取得八卦情报!)
  (if (and (名流社交来件 'full?) (not 拿过八卦情报?))
      (begin
        (set! 拿过八卦情报? #t)
        (add-item! "情报" 1)
        (result-supplement! "找到一份可卖的名流消息"))
      #f))

(define (取得退稿情报!)
  (if (and (未刊稿与退稿 'full?) (not 拿过退稿情报?))
      (begin
        (set! 拿过退稿情报? #t)
        (add-item! "情报" 1)
        (result-supplement! "找到一份被撤下的消息"))
      #f))

(define-opponent-rule "废稿里露出内容"
  (lambda () (and (= 阶段 2) (not 已结束?)))
  (lambda ()
    (取得封套!)
    (取得校样!)
    (取得八卦情报!)
    (取得退稿情报!)))

(define (第一层)
  (node "首演周废稿"
    :anchor "上午十一点零七分-纸山"
    :children
    (append
      (list
        (note-node "标注：无从下手" "眼前的纸堆"
          "信封、照片、通讯稿和校样全压在一起。"))
      (clock-nodes (混杂废稿 'render-data))
      (list
        (整理动作 "按日期整理" "上午十一点零七分-纸山" "先找出同一天留下的纸" 'knowledge 混杂废稿)
        (整理动作 "辨认收件记号" "上午十一点零七分-纸山" "从邮戳、铅笔字和桌号下手" 'sharpness 混杂废稿)))))

(define (外来材料节点)
  (node "送进来的材料"
    :anchor "上午十一点零七分-外来材料"
    :children
    (append
      (list
        (note-node "标注：外来材料" "信封、照片和通讯稿"
          "收件处按来路登记，再送往不同编辑桌。"))
      (clock-nodes (外来材料 'render-data))
      (if (外来材料 'full?)
          (list (note-node "标注：外来材料已分清" "已经归类"
                  "收件章和递送渠道已经排好。"))
          (list
            (整理动作 "核对收件章" "上午十一点零七分-外来材料" "顺着日期和时刻重新排" 'sharpness 外来材料)
            (整理动作 "还原递送渠道" "上午十一点零七分-外来材料" "从信格、门房和跑腿规矩判断来路" 'social 外来材料))))))

(define (报社稿件节点)
  (node "报社写出的稿件"
    :anchor "上午十一点零七分-报社稿件"
    :children
    (append
      (list
        (note-node "标注：报社稿件" "废稿、校样和修改记录"
          "同一篇报道留下了几个不同版本。"))
      (clock-nodes (报社稿件 'render-data))
      (if (报社稿件 'full?)
          (list (note-node "标注：报社稿件已分清" "已经归类"
                  "稿号和不同版次已经排好。"))
          (list
            (整理动作 "按稿号归档" "上午十一点零七分-报社稿件" "把散开的同一篇稿重新并起来" 'knowledge 报社稿件)
            (整理动作 "追踪编辑批注" "上午十一点零七分-报社稿件" "沿铅笔字找出稿件流向" 'sharpness 报社稿件))))))

(define (第二层)
  (node "分出两类"
    :anchor "上午十一点零七分"
    :children
    (list (外来材料节点) (报社稿件节点))))

(define (具体纸堆 name anchor subtitle clk action-a subtitle-a skill-a action-b subtitle-b skill-b)
  (node name
    :anchor anchor
    :subtitle subtitle
    :children
      (append
        (clock-nodes (clk 'render-data))
        (if (clk 'full?)
            (list (note-node (string-append "标注：" name) "已经翻清"
                    "这堆材料已经没有遗漏。"))
            (list
              (整理动作 action-a anchor subtitle-a skill-a clk)
              (整理动作 action-b anchor subtitle-b skill-b clk))))))

(define (结算节点)
  (anchored-instant-action "排列两件证据" "上午十一点零七分-纸山"
    (lambda ()
      (play-dialogue!
        (line "世界" "尼尔把牛皮封套放在左边，把第一版校样压在中间。")
        (line "尼尔" "十一点零七分，东西进了报社。十一点三十五分，稿已经写出来了。")
        (line "世界" "他在右侧空白处写下：下午一点二十分，第三封信出现在剧院。")
        (line "尼尔" "他们不是收到信以后才知道。他们在等那封信出现。"))
      (spotlight! "早于第三封信"
        "上午的材料已经写出死亡威胁，也掌握剧院内部的上台与换装安排。")
      (set! 已结束? #t)
      (end-encounter '证实提前供稿))))

(define (第三层)
  (node "分成四堆"
    :anchor "上午十一点零七分"
    :children
    (append
      (list
        (具体纸堆 "剧院酒店来件"
          "上午十一点零七分-剧院酒店来件"
          "科尔往来的两处；最可能有那只文件袋"
          剧院酒店来件
          "查首演日收件章" "寻找上午十一点前后的来件" 'sharpness
          "还原科尔路线" "沿剧院、信格和报社的交接找" 'social)
        (具体纸堆 "名流社交来件"
          "上午十一点零七分-名流社交来件"
          "匿名爆料和照片说明；未必有关，但值钱"
          名流社交来件
          "筛选可信爆料" "找出能经得住追问的消息" 'social
          "核对照片日期" "排除旧照片和重复来件" 'sharpness)
        (具体纸堆 "四号桌校样"
          "上午十一点零七分-四号桌校样"
          "十一点零七分的材料被送到这里"
          四号桌校样
          "寻找稿号四一七" "按稿号找回首演稿的第一版" 'knowledge
          "辨认覆盖文字" "从铅笔和擦痕里辨认旧版" 'sharpness)
        (具体纸堆 "未刊稿与退稿"
          "上午十一点零七分-未刊稿与退稿"
          "市政、警局和港口不愿见报的消息"
          未刊稿与退稿
          "寻找撤稿原因" "从编辑批注判断是谁叫停" 'knowledge
          "辨认被藏人名" "从称呼和关系找出当事人" 'social))
      (if (and 找到封套? 找到校样?)
          (list (结算节点))
          '()))))

(define (on-encounter-enter)
  (set! 阶段 0)
  (set! 已结束? #f)
  (set! 找到封套? #f)
  (set! 找到校样? #f)
  (set! 拿过八卦情报? #f)
  (set! 拿过退稿情报? #f)
  (混杂废稿 'set! 0)
  (外来材料 'set! 0)
  (报社稿件 'set! 0)
  (剧院酒店来件 'set! 0)
  (名流社交来件 'set! 0)
  (四号桌校样 'set! 0)
  (未刊稿与退稿 'set! 0)
  (play-dialogue!
    (line "世界" "地下没有档案柜，只有捆绳、旧墨水和一座纸山。")
    (line "尼尔" "四号桌。十一点零七分。先让这堆东西分出个顺序。")))

(define (on-encounter-collapse)
  (collapse-result '未完成))

(define (get-render-data)
  ;; 根节点名就是地点名，对应 Anchor_上午十一点零七分；分阶段是内部状态，不进名字。
  (container "上午十一点零七分"
    (list
      (cond
        ((= 阶段 0) (第一层))
        ((= 阶段 1) (第二层))
        ((= 阶段 2) (第三层))
        (#t (error "上午十一点零七分：未知探索阶段"))))))
