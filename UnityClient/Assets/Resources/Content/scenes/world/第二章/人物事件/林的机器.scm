;; 林·第二章：第一章的位置决定他站在项目核心还是外围；第二章的现场事实决定他看见什么。
(define 林的机器
  (let ()
    (define 身份已交代? #f)
    (define 试运行状态 "未开放") ; 未开放 / 待进行 / 成功 / 失败
    (define 试运行结束日 0)
    (define 林的认识 "尚未动摇") ; 尚未动摇 / 看见人的经验 / 意识到转型问题
    (define 培训状态 "未开放")   ; 未开放 / 进行中 / 通过 / 未完成
    (define 培训进度 (make-clock "基础操作" 8 'gauge "填满后，老乔取得基础操作合格。"))
    (define 培训说到作业单? #f)
    (define 培训说到模拟? #f)
    (define journal (make-journal))

    (define (核心?) (lin 'authority?))
    (define (开始了?) (第二章 'started?))
    (define (phase-b?) (equal? (第二章 'phase) "B"))
    (define (试运行结束?) (or (equal? 试运行状态 "成功") (equal? 试运行状态 "失败")))
    (define (培训结束?) (or (equal? 培训状态 "通过") (equal? 培训状态 "未完成")))
    ;; 培训在哪儿上，看林进没进核心：进了就在港务技术区，没进就在码头。
    (define (活动地点) (if (and (核心?) 身份已交代?) "港务技术区" "码头"))
    ;; 试运行**永远在码头**：机器是在三号货栈那条旧轨上试跑的，卷宗写的也是
    ;; "码头试运行"。曾经跟着 活动地点 走，核心线的玩家读着"去码头"却在码头找不到卡——
    ;; 卡搬去了港务技术区，而技术区暂时和工棚叠在地图上同一个点，根本看不出来。
    (define (试运行地点) "码头")

    ;; 活动在码头时落在三号货栈的院子；港务技术区还没有模型，那里的卡走网格。
    (define (落在 place node-data)
      (if (equal? place "码头")
          (at-anchor "码头-三号货栈" node-data)
          node-data))

    ;; 搬空次日才生效：入场节拍里就把脚下的地点撤掉，引擎会当场中断
    ;; （地点必须在自己的入场节拍之后仍然存在）。搬空当天工棚还在，
    ;; 只剩一条标注，次日再从地图上消失。
    ;; 旧档没有搬空生效日时当作 0：world-day 恒大于 0，已经交代过的照旧是空的。
    (define 搬空生效日 0)
    (define (vacated?) (and 身份已交代? (> world-day 搬空生效日)))

    (define (身份入场)
      (arrival "工棚正在搬空"
        (lambda ()
          (set! 身份已交代? #t)
          (set! 搬空生效日 world-day)
          (if (核心?)
              (begin
                (play-dialogue!
                  (line "世界" "设备贴上公司编号，图纸装进铁柜。有人拿着清单等林签字。")
                  (line "工作人员" "林先生，这批图纸送技术区还是旧档案室？")
                  (line "林" "技术区。原件跟我走。")
                  (line "世界" "技术区门口，警卫伸手拦住尼尔。")
                  (line "林" "他跟我一起。"))
                (journal 'add! "三号货栈的实验让林进入项目核心。他把尼尔带进了港务技术区。")
                (spotlight! "港务技术区" "林进入了项目核心。凭他的许可，你可以正常进入港务技术区。"))
              (begin
                (play-dialogue!
                  (line "世界" "公司的人把设备和原图装车，只留给林一套副本。")
                  (line "工作人员" "正式设备采用总部方案。你继续负责旧轨数据。")
                  (line "世界" "车开向港务技术区。林留在空下来的工棚门口。"))
                (journal 'add! "自动化项目采用了另一套方案。林仍在参与，但被留在项目外围。")
                (spotlight! "项目外围" "港务技术区已经投入使用。林没有权限带尼尔进去。"))))))

    (define (arrivals-at location)
      (if (and (equal? location "三号货栈工棚") (开始了?) (not 身份已交代?))
          (list (身份入场)) '()))

    (define (试运行结果! result)
      (cond
        ((and (list? result) (equal? (car result) '成功))
         (let ((参数胜 (cadr result)) (经验胜 (caddr result)))
           (set! 试运行状态 "成功")
           (set! 试运行结束日 world-day)
           (if (> 经验胜 参数胜)
               (begin
                 (set! 林的认识 "看见人的经验")
                 (lin 'on-saw-cost!)
                 (play-remote-dialogue!
                   (line "林" "刚才那几种情况，还有多少种？")
                   (line "老乔" "哪一种？")
                   (line "林" "……全部。")
                   (line "老乔" "那可多了。")))
               (play-remote-dialogue!
                 (line "林" "现场比试验场多了很多变量。")
                 (line "尼尔" "所以呢？")
                 (line "林" "所以把它们写进去。下次机器会认得。")))
           (journal 'add!
             (if (> 经验胜 参数胜)
                 "试运行成功。异常主要依靠老乔的经验解决，林第一次承认系统没有描述这些知识。"
                 "试运行成功。异常主要依靠参数修正解决，林更确信现场问题最终都能被建模。"))))
        ((equal? result '崩溃)
         (set! 试运行状态 "失败")
         (set! 试运行结束日 world-day)
         (journal 'add! "并发异常耗尽系统稳定，试运行以控制器崩溃告终。公司仍会继续部署。"))
        (#t (error "林的机器：《试运行》返回未知结果"))))

    (define (试运行节点)
      ;; 副标题写清是谁在哪：这张卡挂在三号货栈院子的锚点上，偏离码头默认视轴约 39°，
      ;; 不写清楚玩家转镜头都不知道该往哪转。
      (node "开始试运行"
        :subtitle "林和老乔在三号货栈的院子里"
        :tags (list "交锋")
        :resolve (instant (lambda () (start-encounter "码头试运行" 试运行结果!)))))

    (define (培训阶段)
      (cond ((< (培训进度 'current) 3) 0)
            ((< (培训进度 'current) 6) 1)
            (#t 2)))

    (define (培训推进! n)
      (培训进度 'advance! n)
      (if (and (>= (培训进度 'current) 3) (not 培训说到作业单?))
          (begin
            (set! 培训说到作业单? #t)
            (play-dialogue!
              (line "老乔" "我知道这批货怎么搬。可这张纸管它叫什么？")
              (line "林" "标准混装单元。")
              (line "老乔" "码头上没人这么叫。"))) #f)
      (if (and (>= (培训进度 'current) 6) (not 培训说到模拟?))
          (begin
            (set! 培训说到模拟? #t)
            (play-dialogue!
              (line "世界" "模拟器要求老乔重新输入一遍刚才亲手处理过的货物。")
              (line "老乔" "真货我会搬。这一箱假货倒把我难住了。"))) #f)
      (if (培训进度 'full?)
          (begin
            (set! 培训状态 "通过")
            (play-dialogue!
              (line "世界" "打印机吐出一张薄纸：基础操作，合格。")
              (line "林" "不对。")
              (line "尼尔" "什么不对？")
              (line "林" "我还不知道。"))
            (if (equal? 林的认识 "看见人的经验")
                (set! 林的认识 "意识到转型问题") #f)
            (journal 'add! "老乔完成了基础培训。那张合格证几乎没有描述他在码头上真正会做的事。")
            (complete-task! "林与新机器")) #f))

    (define (培训动作 name subtitle skill)
      (node name :subtitle subtitle :requires (list (req-die))
        :resolve (roll skill
          (outcome (lambda () (spend-actor-composure! 'player 1)))
          (outcome (lambda () (培训推进! 1)))
          (outcome (lambda () (培训推进! 2))))))

    (define (培训节点)
      (container "陪老乔培训"
        (append
          (clock-nodes (培训进度 'render-data))
          (cond
            ((= (培训阶段) 0)
             (list (培训动作 "逐项解释按钮" "把终端上的每一步拆开讲" 'knowledge)
                   (培训动作 "拿码头设备作比" "换成老乔熟悉的机械和动作" 'social)))
            ((= (培训阶段) 1)
             (list (培训动作 "拆开系统缩写" "把作业单翻回普通说法" 'knowledge)
                   (培训动作 "按真实货物还原" "从货物和绳结反推表格含义" 'sharpness)))
            (#t
             (list (培训动作 "陪他核对步骤" "逐行检查模拟操作" 'knowledge)
                   (培训动作 "让他按习惯做" "先做对，再找系统里的对应项" 'social)))))))

    (define (on-day-start!)
      (if (and (phase-b?) (equal? 试运行状态 "未开放"))
          (begin
            (set! 试运行状态 "待进行")
            (spotlight! "第一次试运行" "设备将在真实旧轨上试运行。林在机器旁边，老乔也被叫来盯现场。")) #f)
      (if (and (试运行结束?) (equal? 培训状态 "未开放")
               (>= world-day (+ 试运行结束日 1)))
          (begin
            (set! 培训状态 "进行中")
            (spotlight! "基础培训" "老乔收到转岗培训通知。课程不难——至少对设计课程的人来说不难。")) #f))

    (define (close!)
      (if (equal? 试运行状态 "待进行") (set! 试运行状态 "失败") #f)
      (if (equal? 培训状态 "进行中")
          (begin
            (set! 培训状态 "未完成")
            (if (equal? 林的认识 "看见人的经验")
                (set! 林的认识 "意识到转型问题") #f)
            (journal 'add! "第一批机器进场时，老乔仍没完成基础培训。这个阶段已经过去。")
            ;; 机器进场把这一节推到了头；陪过老乔才算经历完。
            (if (> (培训进度 'current) 0) (complete-task! "林与新机器") #f))
          (if (equal? 培训状态 "未开放") (set! 培训状态 "未完成") #f)))

    (define (nodes-at location)
      (cond
        ((and (equal? 试运行状态 "待进行") (equal? location (试运行地点))) (list (落在 (试运行地点) (试运行节点))))
        ((and (equal? 培训状态 "进行中") (equal? location (活动地点))) (list (落在 (活动地点) (培训节点))))
        ((and 身份已交代? (not (vacated?)) (equal? location "三号货栈工棚"))
         (list (note-node "标注：空下来的工棚" "搬空了"
                 (if (核心?)
                     "设备和图纸都搬去了港务技术区。门开着，里面只剩空架子和一地纸屑。"
                     "设备和原图都装车走了，只给林留了一套副本。他还在空下来的工棚门口。"))))
        ((and 身份已交代? (not (核心?)) (equal? location "码头"))
         (list (at-anchor "码头-三号货栈"
                 (note-node "标注：技术区门禁" "港务技术区" "林的证件只允许他去外围工位。"))))
        (#t '())))

    ;; 试运行结束、培训还没开的那两天，以及身份已交代、试运行还没安排时，
    ;; 玩家手里没有可做的事：nodes-at 回 '()。这时候状态必须是「等着别人」，
    ;; :now 也必须说出在等什么——挂着「进行中」配一句感慨，读起来就是一张空卡。
    (define (等待中?)
      (or (and 身份已交代? (equal? 试运行状态 "未开放"))
          (and (试运行结束?) (equal? 培训状态 "未开放"))))

    (define (steps)
      (list (step "去三号货栈工棚，看林的位置" 身份已交代?)
            (step "去码头参加第一次试运行" (试运行结束?))
            (step "陪老乔完成基础操作培训" (equal? 培训状态 "通过"))))

    (define (dossier-entry)
      (if (not (开始了?)) '()
          (list
            (dossier "林与新机器" :kind '人物
              :steps (steps)
              :status (cond ((and (试运行结束?) (培训结束?)) '了结)
                            ((等待中?) '等着别人)
                            (#t '进行中))
              :now (cond
                     ((not 身份已交代?) "去三号货栈工棚看看林的位置发生了什么")
                     ((equal? 试运行状态 "待进行") "参加第一次真实码头试运行")
                     ((equal? 培训状态 "进行中") "陪老乔完成基础操作培训")
                     ((equal? 试运行状态 "未开放") "第一次试运行还没安排，等林的消息")
                     ((equal? 培训状态 "未开放") "试运行结束了，等老乔的培训通知下来")
                     ((equal? 培训状态 "通过") "老乔通过了基础培训")
                     (#t "第一批机器进场，这一阶段结束了"))
              :where (cond
                       ((not 身份已交代?) "三号货栈工棚")
                       ((equal? 试运行状态 "待进行") (试运行地点))
                       ((equal? 培训状态 "进行中") (活动地点))
                       (#t ""))
              :clocks (cond
                        ((equal? 培训状态 "进行中") (list (培训进度 'render-data)))
                        ((and (试运行结束?) (equal? 培训状态 "未开放"))
                         (list (日期倒计时 "离培训通知" (+ 试运行结束日 2) 2
                                 "通知到了就去培训地点陪老乔（码头或港务技术区）")))
                        (#t '()))
              :log (journal 'render-data)))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) (arrivals-at (cadr args)))
          ((equal? msg 'dossier) (dossier-entry))
          ((equal? msg 'on-day-start!) (on-day-start!))
          ((equal? msg 'close!) (close!))
           ((equal? msg 'has-access?) (and 身份已交代? (核心?)))
           ((equal? msg 'workshop-vacated?) (vacated?))
          ((equal? msg 'insight) 林的认识)
           ((equal? msg 'save)
            (list (list "identity-shown" (if 身份已交代? 1 0))
                  (list "vacated-day" 搬空生效日)
                 (list "trial-state" 试运行状态) (list "trial-end-day" 试运行结束日)
                 (list "insight" 林的认识) (list "training-state" 培训状态)
                 (list "training-progress" (培训进度 'save))
                 (list "training-orders" (if 培训说到作业单? 1 0))
                 (list "training-simulation" (if 培训说到模拟? 1 0))
                 (list "journal" (journal 'save))))
          ((equal? msg 'load!)
            (let ((data (cadr args)))
              (set! 身份已交代? (= (assoc-get data "identity-shown" 0) 1))
              (set! 搬空生效日 (assoc-get data "vacated-day" 0))
             (set! 试运行状态 (assoc-get data "trial-state" "未开放"))
             (set! 试运行结束日 (assoc-get data "trial-end-day" 0))
             (set! 林的认识 (assoc-get data "insight" "尚未动摇"))
             (set! 培训状态 (assoc-get data "training-state" "未开放"))
             (培训进度 'load! (assoc-get data "training-progress" 0))
             (set! 培训说到作业单? (= (assoc-get data "training-orders" 0) 1))
             (set! 培训说到模拟? (= (assoc-get data "training-simulation" 0) 1))
             (journal 'load! (assoc-get data "journal" '()))))
          (else (error "林的机器：收到未知消息")))))))
