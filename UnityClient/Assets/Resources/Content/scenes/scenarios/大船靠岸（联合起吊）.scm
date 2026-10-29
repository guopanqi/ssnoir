;; 第九天大船靠岸的正式抢卸夜班；同时保留独立演示入口。
;; 起吊、状态触发摆动与落地走联合起吊舞台的重机动画；进港货船不变。
;; 普通卸货不计入六回合；转入准备、准备完成起吊都不刷新骰子，与剩余时间共用本班骰子。
;; 回合末：失衡结算 → 更新下一轮加快禁令 → 工作时间推进 → 超时结算。
;; 摆动满优先停吊；危机期间不重复结算失衡，时限仍走。
(define 阶段 '卸货)
(define 已结束? #f)
(define 普通货物 (make-clock "卸下普通货物" 4 'gauge "先卸完眼前这一批货；满格后继续查看舱内剩余货物。" :beneficial-direction 'increase))
(define 吊具 (make-clock "吊具检查" 3 'gauge "检查绳套、吊点与挂钩；完成后才能起吊。" :beneficial-direction 'increase))
(define 落点 (make-clock "整理落货区域" 3 'gauge "移走杂货，留出接货和垫木的位置；完成后才能起吊。" :beneficial-direction 'increase))
(define 防摆 (make-clock "辅助防摆准备" 2 'gauge "可选。完成后，两组工作允许相差两格；否则只允许一格。" :beneficial-direction 'increase))
(define 前端 (make-clock "前端吊运" 8 'gauge "前端班组照顾驱动总成这一端，与后端一起起落、移运。" :beneficial-direction 'increase))
(define 后端 (make-clock "后端吊运" 8 'gauge "后端班组照顾驱动总成另一端，与前端一起起落、移运。" :beneficial-direction 'increase))
(define 摆动 (make-clock "重物摆动" 3 'pressure "满格立即紧急停吊，先重新稳定吊运，不能继续推进。" :beneficial-direction 'decrease))
(define 危机 (make-clock "重新稳定吊运" 3 'gauge "重新检查受力、收紧导索；完成后摆动降至一格，恢复原进度。" :beneficial-direction 'increase))
(define 时间 (make-clock "剩余工作时间" 6 'countdown "准备、吊运和停吊共用六回合。回合末减一；归零未落地则延期卸载。" :beneficial-direction 'increase))
(define 工头 (speaker "联合起吊工头" :name "工头" :appearance '成年男性))
(define 弗兰克 (speaker "联合起吊弗兰克" :name "弗兰克" :appearance '成年男性))
(define (容许差) (if (防摆 'full?) 2 1))
(define (进度差) (abs (- (前端 'current) (后端 'current))))
(define (清理同伴!)
  (if (has-companion? '艾迪) (dismiss-companion! '艾迪) #f))
(define (结束! 成功?)
  (if 已结束? (error "联合起吊：重复结束") #t)
  (set! 已结束? #t)
  (if 成功?
    (begin
      (play-motion! "联合起吊/重机" "Landed" "联合起吊-落地")
      (play-dialogue!
        (line "世界" "（机器越过落货区，底座缓缓坐上垫木。艾迪没有立即放下手；等钢索松弛、机身彻底不动，他才点头。工人们一下喊出了声。）")
        (line 弗兰克 "落稳了！")
        (line "世界" "（弗兰克走过来，重重拍了拍尼尔的肩膀。工头绕着机器看了一圈，停在艾迪面前，朝他点点头。）")
        (line 工头 "这趟配合得好。")
        (line "世界" "（艾迪松开领口，看了看机器，又看了看围过来的工人，终于笑了。）")
        (line "艾迪" "是大家接得住。尼尔，手套摘了再揉眼睛。"))
      (spotlight! "安全落货" "巨型机器平稳落地。两组工人共同完成了这次吊运。"))
    (begin
      (play-dialogue!
        (line 工头 "到这儿吧。先把货稳妥安置，下一班再卸。")
        (line "世界" "（工人按指挥将机器安置稳固，撤下作业线。艾迪等最后一个人离开吊运区域，才摘下手套。）")
        (line "艾迪" "没卸下来，明天还有机会。人先平安收工。"))
      (spotlight! "延期卸载" "本班未能完成吊运，重型机器留待下一班处理。")))
  (清理同伴!)
  (end-scenario (if 成功? '安全落货 '延期卸载)))
(define (检查吊运!)
  (cond
    ((摆动 'full?)
      (set! 阶段 '停吊)
      (危机 'set! 0)
      (play-dialogue!
        (line "艾迪" "停！谁都别再拉，先把它稳住。")
        (line "世界" "（司机立即停止移动。工人们站稳，守住导索；艾迪的手仍举在半空。原来的推进工作暂停，必须重新稳定吊运。）"))
      (focus-node! "联合起吊"))
    ((and (前端 'full?) (后端 'full?)) (结束! #t))
    (else #f)))
(define (推进! 钟 数量)
  (if (eq? 阶段 '吊运) #t (error "联合起吊：当前不能推进"))
  (钟 'advance! 数量)
  (检查吊运!))
(define (准备! 钟 数量)
  (if (eq? 阶段 '准备) #t (error "联合起吊：当前不能准备"))
  (钟 'advance! 数量)
  (if (and (吊具 'full?) (落点 'full?))
    (begin
      (set! 阶段 '吊运)
      (play-dialogue!
        (line "世界" "（船上工人最后检查一遍绳套。岸上的杂货已经移开，垫木排好。艾迪站到两组人都能看见的位置，举起一只手；尼尔和弗兰克分别守住导索。）"))
      (play-motion! "联合起吊/重机" "Suspended" "联合起吊-起吊")
      (play-dialogue!
        (line "艾迪" "尼尔，盯住两端的绞车。哪一头没跟上，另一头就先等一等。"))
      (focus-node! "联合起吊")) #f))
(define (稳定危机! 数量)
  (危机 'advance! 数量)
  (if (危机 'full?)
    (begin
      (摆动 'set! 1)
      (set! 阶段 '吊运)
      (play-dialogue!
        (line "世界" "（导索重新收紧，机身的摆动逐渐停下。艾迪逐一确认两边的手势，工人回到原来的位置，等他重新发令。）")
        (line "艾迪" "接着来。还照刚才的分工。"))
      (focus-node! "联合起吊")) #f))
(define (进入准备!)
  (set! 阶段 '准备)
  (play-dialogue!
    (line "世界" "（最后一箱普通货物送进货棚。弗兰克拍掉手套上的灰，招呼尼尔往舱里看。给新货栈的输送线驱动总成还压在舱底：巨大的滚筒、齿轮箱和电机连在一副厚底座上，几乎占满了剩下的空地。）")
    (line 工头 "这东西过落货区太紧了。要不留到下一班？")
    (line "世界" "（艾迪退到能看清吊臂的位置，抬头看了片刻。他沿岸边走到狭窄的落货区，蹲下拨了拨垫木，又向船舱比划了一下。）")
    (line "艾迪" "两端分开挂索，两组绞车听我的口令。先查吊具，把岸边腾干净，导索的人站到两侧。")
    (line "艾迪" "还有工夫就多布一道防摆导索。尼尔，你跟我把准备做完。")
    (line "世界" "从现在起，还有六个工作回合。准备与吊运共用这段时间，停吊调整也不会延长时限。"))
  (focus-node! "联合起吊"))

(define-opponent-rule "两组工作失衡"
  (lambda () (and (not 已结束?) (eq? 阶段 '吊运) (> (进度差) (容许差))))
  (lambda ()
    (摆动 'advance! 1)
    (play-motion! "联合起吊/重机"
      (string-append (if (> (前端 'current) (后端 'current)) "FrontSway" "RearSway")
        (number->string (摆动 'current))) "联合起吊-摆动")
    (if (摆动 'full?) (检查吊运!)
      (play-dialogue! (line "艾迪" "慢一点！让另一边接上，别抢着拉。")))))
(define-opponent-rule "本班时间流逝"
  (lambda () (and (not 已结束?) (or (eq? 阶段 '准备) (eq? 阶段 '吊运) (eq? 阶段 '停吊))))
  (lambda () (时间 'advance! -1)))
(define-opponent-rule "本班收工"
  (lambda () (and (not 已结束?) (时间 'empty?)))
  (lambda () (结束! #f)))

(define (on-scenario-enter)
  (set! 阶段 '卸货) (set! 已结束? #f)
  (map (lambda (钟) (钟 'load! 0)) (list 普通货物 吊具 落点 防摆 前端 后端 摆动 危机))
  (时间 'load! 6)
  (recruit-companion! '艾迪 "艾迪" (同伴能力 '艾迪))
  (set-actor-die-profile! '艾迪 1 0 "")
  (play-dialogue!
    (line "世界" "（巨轮靠稳。舱口敞开，吊臂转向船舱。工人散开就位，艾迪领着尼尔走到货箱旁。）")
    (line "艾迪" "尼尔，先跟我们卸这批。搬货箱，或者过来帮着挂索。")
    (line 弗兰克 "货棚在那头，走这条通道。跟着我，别往吊钩底下钻。")))
(define (on-scenario-collapse)
  (清理同伴!)
  (collapse-result '延期卸载))

;; outcome 的顺序始终是失败、中立、成功。
(define (准备节点 id 钟 技能)
  (node id :risk '稳妥 :disabled (钟 'full?)
    :subtitle "坏无进展，中推进1，好推进2。"
    :progress-target (list 'clock (cadr (钟 'render-data)))
    :requires (list (req-die))
    :resolve (roll 技能
      (outcome (lambda () (准备! 钟 0)))
      (outcome (lambda () (准备! 钟 1)))
      (outcome (lambda () (准备! 钟 2))))))
(define (吊运节点 id 钟 技能 快? 锚点)
  (node id :anchor 锚点 :risk (if 快? '冒险 '稳妥)
    :disabled (钟 'full?)
    :subtitle (if 快? "坏行动者冷静−1，中进1，好进3。" "坏无进展，中进1，好进2。")
    :progress-target (list 'clock (cadr (钟 'render-data)))
    :requires (list (req-die))
    :resolve (roll 技能
      (outcome (lambda () (if 快? (spend-composure! 1) #f)))
      (outcome (lambda () (推进! 钟 1)))
      (outcome (lambda () (推进! 钟 (if 快? 3 2)))))))

(define (卸普通货! 数量)
  (if (eq? 阶段 '卸货) #t (error "联合起吊：普通卸货已经结束"))
  (普通货物 'advance! 数量)
  (if (普通货物 'full?) (进入准备!) #f))
(define (普通卸货行动 id 技能)
  (node id :risk '稳妥
    :subtitle "坏无进展，中卸1格，好卸2格。"
    :progress-target (list 'clock "卸下普通货物")
    :requires (list (req-die))
    :resolve (roll 技能
      (outcome (lambda () (卸普通货! 0)))
      (outcome (lambda () (卸普通货! 1)))
      (outcome (lambda () (卸普通货! 2))))))
(define (卸货节点)
  (append
    (clock-nodes (普通货物 'render-data))
    (list
      (note-node "普通卸货说明" "普通劳动" "两种行动都卸同一批货，满4格进入下一阶段。此阶段不计工作时限，回合结束仍支付通常冷静代价。")
      (普通卸货行动 "搬运货箱" 'violence)
      (普通卸货行动 "协助挂索" 'sharpness))))
(define (准备盘面)
  (append
    (clock-nodes (吊具 'render-data) (落点 'render-data) (防摆 'render-data))
    (list
      (note-node "准备规则" "起吊条件" "两项必要准备满格即起吊；可选防摆须在此之前完成，允许差由1格增至2格。")
      (准备节点 "协助检查吊具" 吊具 'sharpness)
      (准备节点 "整理落货区域" 落点 'violence)
      (准备节点 "布置辅助防摆" 防摆 'sharpness))))
(define (停吊盘面)
  (append
    (clock-nodes (前端 'render-data) (后端 'render-data) (摆动 'render-data) (危机 'render-data))
    (list
      (note-node "停吊规则" "紧急停吊" "原吊运行动暂停；稳定工作满3格后摆动降至1，恢复原进度。时限照常消耗。")
      (node "重新稳定吊运" :risk '稳妥 :subtitle "坏无进展，中推进1，好推进2。"
        :progress-target (list 'clock "重新稳定吊运") :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome (lambda () (稳定危机! 0)))
          (outcome (lambda () (稳定危机! 1)))
          (outcome (lambda () (稳定危机! 2))))))))
(define (吊运盘面)
  (append
    (clock-nodes (前端 'render-data) (后端 'render-data) (摆动 'render-data))
    (list
      (note-node "同步规则" "协调工作"
        (string-append "当前相差" (number->string (进度差)) "格，允许" (number->string (容许差))
          "格。回合末超差：摆动+1；仍可自行选择加快。两侧满格且摆动未满，立即落货。"))
      (吊运节点 "前端谨慎推进" 前端 'violence #f "联合起吊-前端")
      (吊运节点 "前端加快推进" 前端 'violence #t "联合起吊-前端")
      (吊运节点 "后端谨慎推进" 后端 'sharpness #f "联合起吊-后端")
      (吊运节点 "后端加快推进" 后端 'sharpness #t "联合起吊-后端"))))
(define (get-render-data)
  (container "联合起吊"
    (append
      (list (note-node "联合起吊目标" "目标"
        (cond
          ((eq? 阶段 '卸货) "跟着艾迪和工人们，卸完眼前这一批普通货物。")
          ((eq? 阶段 '准备) "检查吊具，清理落点；有余力可先布置防摆。")
          ((eq? 阶段 '停吊) "重新稳定吊运，再继续把机器安全卸下。")
          (else "协调前端与后端，在本班结束前安全落货。"))))
      (if (or (eq? 阶段 '吊运) (eq? 阶段 '停吊))
        (list (note-node "联合起吊悬吊状态" "重机" "机器悬在吊索下，两组人等着相互接应。")) '())
      (cond
        ((eq? 阶段 '卸货) (卸货节点))
        (else
          (append
            (clock-nodes (时间 'render-data))
            (list (note-node "起吊回合规则" "工作规则" "回合结束支付通常冷静代价，剩余时间减1。准备完成立即起吊，沿用本回合剩余骰子。"))
            (cond
              ((eq? 阶段 '准备) (准备盘面))
              ((eq? 阶段 '停吊) (停吊盘面))
              (else (吊运盘面)))))))))
