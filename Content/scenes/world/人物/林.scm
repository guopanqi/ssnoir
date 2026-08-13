;; 林与试验装卸机。
;;
;; 本模块只拥有林的关系、机器验收、救援回收、工棚冲突与剧院回收。
;; 码头坍塌仍由 dock-collapse 唯一排期；这里不参与它的触发判定。

(define lin
  (let ()
    (define installation-max 5)
    (define acceptance-delay 2)

    (define arrival-day 0)
    (define acceptance-day 0)
    ;; 陌生 / 认识 / 信任
    (define relationship "陌生")
    (define installation-participated? #f)
    ;; 未验收 / 保守试运 / 精密调试
    (define machine-state "未验收")
    ;; 未发生 / 基础机器 / 精密机器
    (define rescue-state "未发生")
    ;; 未发生 / 护送离开 / 弗兰克调停 / 警卫清场 / 玩家缺席
    (define conflict-result "未发生")
    (define conflict-day 0)
    ;; 未发生 / 陌生相见 / 普通帮助 / 可靠帮助
    (define theater-result "未发生")

    (define installation-clk
      (make-clock "机器安装与调试" installation-max 'segments
        "搬固底座、铺设线路、装配重并完成负载试验；验收日只按这一根进度结算。"))

    (define (required-field data key)
      (let ((value (assoc-get data key 'missing)))
        (if (equal? value 'missing)
            (error (string-append "林存档错误：缺少 " key))
            value)))

    (define (boolean-value? value)
      (or (equal? value #t) (equal? value #f)))

    (define (valid-relationship? value)
      (member? value (list "陌生" "认识" "信任")))

    (define (valid-machine-state? value)
      (member? value (list "未验收" "保守试运" "精密调试")))

    (define (valid-rescue-state? value)
      (member? value (list "未发生" "基础机器" "精密机器")))

    (define (valid-conflict-result? value)
      (member? value (list "未发生" "护送离开" "弗兰克调停" "警卫清场" "玩家缺席")))

    (define (valid-theater-result? value)
      (member? value (list "未发生" "陌生相见" "普通帮助" "可靠帮助")))

    (define (workshop-open?) (> arrival-day 0))
    (define (machine-accepted?) (not (equal? machine-state "未验收")))
    (define (known?) (not (equal? relationship "陌生")))
    (define (trusted?) (equal? relationship "信任"))
    (define (precise?) (equal? machine-state "精密调试"))
    (define (conflict-pending?)
      (and (> conflict-day 0) (equal? conflict-result "未发生")))
    (define (at-theater?) (= (three-letters 'story-stage) 4))

    (define (sync-globals!)
      (set-global! '林-关系 relationship)
      (set-global! '林-机器状态 machine-state)
      (set-global! '林-救援参与 rescue-state)
      (set-global! '林-工棚冲突 conflict-result)
      (set-global! '林-剧院回收 theater-result))

    (define (meet!)
      (if (equal? relationship "陌生") (set! relationship "认识") #f)
      (sync-globals!))

    (define (trust!)
      (set! relationship "信任")
      (sync-globals!))

    (define (announce-running-dialogue!)
      (play-remote-dialogue!
        (line "世界" "三号货栈里，试验机第一次把一整排钢箱举离地面。")
        (line "林" "以前得让一班人拉索子，还得有人站在回弹的钢缆旁边。")
        (line "林" "现在这里不该再站人了。危险又枯燥的活，本来就该交给机器。")))

    (define (settle-machine! announce?)
      (if (equal? machine-state "未验收")
          #t
          (error "林：机器试图重复验收"))
      (if (installation-clk 'full?)
          (begin
            (set! machine-state "精密调试")
            (if (known?) (set! relationship "信任") #f))
          (set! machine-state "保守试运"))
      (sync-globals!)
      (if announce?
          (spotlight! "试验机验收"
            (if (precise?)
                "负载表的指针在窄区间里停住了。林签下精密调试记录，机器可以靠近人员作业。"
                "公司的装配队把基本部件收了尾。机器通过保守试运，只允许在外围做粗重作业。"))
          #f)
      (announce-running-dialogue!))

    ;; 坍塌可以在玩家从未去过工棚时照常发生。救援入场只收口当前验收结果，
    ;; 绝不把安装进度变成公共事件的门槛。
    (define (prepare-rescue!)
      (if (= arrival-day 0)
          (begin
            (set! arrival-day world-day)
            (set! acceptance-day world-day))
          #f)
      (if (not (machine-accepted?)) (settle-machine! #f) #f)
      (set-global! '坍塌-机器状态 machine-state)
      machine-state)

    (define (rescue-dialogue! result)
      (play-remote-dialogue!
        (line "林"
          (if (equal? result "大型")
              "再多一台机器，再早一小时调度，名单上就不会留那么多空格。"
              "人手抬不动的梁，它抬起来了。再多放几台在旧港，下一次就不用等到人被压在下面。"))
        (line "尼尔" "它会抬梁。先抬哪一根，还得有人开口。")
        (line "林" "是。所以你来定，我让它做到。")))

    (define (on-dock-collapse! result attended?)
      (if (equal? rescue-state "未发生")
          #t
          (error "林：码头坍塌试图重复写回救援参与"))
      (prepare-rescue!)
      (set! rescue-state (if (precise?) "精密机器" "基础机器"))
      (set! conflict-day world-day)
      ;; 玩家在场时，林已经在现场跟他说过话了；这段是补给缺席的人的事后通报。
      (if attended? #f (rescue-dialogue! result))
      (sync-globals!))

    (define (advance-installation! amount note)
      (set! installation-participated? #t)
      (meet!)
      (installation-clk 'advance! amount)
      (result-note! note)
      (if (installation-clk 'full?)
          (result-note! "安装调试已完成，等待验收日试车")
          #f))

    (define (node-installation)
      (node "协助安装调试"
        :subtitle "底座、电缆、油管、配重与负载试验共用同一个持续目标"
        :tags (list "持续目标" "低风险")
        :disabled (installation-clk 'full?)
        :clocks (list (installation-clk 'render-data))
        :requires (list (req-die))
        :resolve (roll 'knowledge
          (outcome "读数又漂了"
            (lambda ()
              (set! installation-participated? #t)
              (meet!)
              (spend-composure! 1)
              (result-note! "林记下了错误读数，这次没有推进验收准备")))
          (outcome "收好一组管线"
            (lambda () (advance-installation! 1 "油管和电缆已经固定")))
          (outcome "把负载调稳"
            (lambda () (advance-installation! 2 "配重与负载表在满载下保持稳定"))))))

    (define (node-machine-work)
      (工作 "协助机器试验" "富商" '低 'knowledge
        (outcome "试验记录齐了" (lambda () (add-item! "金钱" 12)))
        (outcome "跑完一组读数" (lambda () (add-item! "金钱" 8)))
        (outcome "压力表得重来" (lambda () (spend-composure! 1)))
        "按试验表跑负载、记油压；稳定的见识工作"))

    (define (machine-status-text)
      (cond
        ((equal? machine-state "未验收")
         (string-append
           "试验机还在安装。距验收还有 "
           (number->string (max 0 (- acceptance-day world-day)))
           " 天；未完成时由公司人手收尾，按保守试运结算。"))
        ((equal? machine-state "保守试运")
         "机器已通过保守试运，只允许支撑、外围清障与粗重抬升。")
        ((equal? machine-state "精密调试")
         "机器已完成精密调试，除了粗重作业，还能在伤者身边稳定抬升。")
        (else (error "林：无法显示未登记的机器状态"))))

    (define (relationship-text)
      (cond
        ((equal? relationship "陌生") "负责控制器、仪表和液压系统的工程师。")
        ((equal? relationship "认识") "林记得你在底座、管线和负载试验上帮过忙。")
        ((equal? relationship "信任") "林信任你对载荷、现场和风险的判断。")
        (else (error "林：无法显示未登记的关系"))))

    (define (on-conflict-result result)
      (if (conflict-pending?) #t (error "林：工棚冲突已经结算"))
      (cond
        ((equal? result 'escort)
         (set! conflict-result "护送离开")
         (trust!))
        ((equal? result 'frank)
         (set! conflict-result "弗兰克调停")
         (meet!))
        ((equal? result 'guards)
         (set! conflict-result "警卫清场")
         (meet!)
         (change-faction-relation! "劳工" -2))
        (else (error "林：少了一班人返回了未登记结果")))
      (sync-globals!))

    (define (node-conflict)
      (encounter-action "少了一班人"
        (lambda ()
          (start-encounter "少了一班人" on-conflict-result))))

    (define (workshop-nodes)
      (if (not (workshop-open?))
          '()
          (append
            (list (observe-action "试验装卸机" (machine-status-text)))
            (if (and (not (machine-accepted?)) (not (installation-clk 'full?)))
                (list (node-installation))
                '())
            (if (and installation-participated? (machine-accepted?)) (list (node-machine-work)) '())
            (if (at-theater?) '() (list (observe-action "林" (relationship-text))))
            (if (conflict-pending?) (list (node-conflict)) '()))))

    (define (theater-help!)
      (if (equal? theater-result "未发生") #t (error "林：剧院回收已经结算"))
      (if (machine-accepted?) #t (error "林：剧院回收时机器仍未验收"))
      (let ((was-known? (known?)))
        (cond
          ((not was-known?)
           (play-dialogue!
             (line "薇拉" "这是我弟弟林。公司资助他在码头的试验，我请他顺道看看这里的机械。")
             (line "林" "中央台的升降机很旧，不过今晚之前没有理由让它停。")
             (line "尼尔" "码头那台试验机也是你的。")
             (line "林" "是。同一家公司，同一笔钱，只是这回把机器用在舞台下面。"))
           (set! theater-result "陌生相见")
           (meet!))
          ((precise?)
           (play-dialogue!
             (line "林" "三号货栈的负载表还没说完，我们又在一台升降机旁边见面了。")
             (line "薇拉" "原来你们认识。林的试验是公司出的钱；我请他来，是因为今晚不容出错。")
             (line "林" "你在码头帮我把窄区间调出来了。这一台也一样：行程、制动和通风，我替你一项项过。"))
           (set! theater-result "可靠帮助")
           (three-letters 'apply-lin-technical-help! 2))
          (else
           (play-dialogue!
             (line "林" "没想到会在中央台边上见到你。")
             (line "薇拉" "林是我弟弟。公司资助他的试验，剧院这边也由他来看技术上的事。")
             (line "林" "码头那台机器只通过了保守试运。不过这台升降机的制动，我能先替你收紧一段。"))
           (set! theater-result "普通帮助")
           (three-letters 'apply-lin-technical-help! 1)))
        (three-letters 'mark-vera-met!)
        (sync-globals!)))

    (define (theater-nodes)
      (if (not (at-theater?))
          '()
          (list
            (container "中央台边"
              (if (equal? theater-result "未发生")
                  (list
                    (node "林"
                      :subtitle (relationship-text)
                      :children (list (instant-action "请林看看升降台" (lambda () (theater-help!)))))
                    (observe-action "薇拉" "赞助公司的代表，正站在中央台边等林说完。"))
                  (list (observe-action "林" "林留在中央台边，把升降机的行程和制动又核对了一遍。")))))))

    (define (validate-state!)
      (if (and (number? arrival-day) (>= arrival-day 0)) #t (error "林存档错误：机器运抵日非法"))
      (if (and (number? acceptance-day) (>= acceptance-day 0)) #t (error "林存档错误：验收日非法"))
      (if (valid-relationship? relationship) #t (error "林存档错误：关系状态非法"))
      (if (boolean-value? installation-participated?) #t (error "林存档错误：安装参与标记非法"))
      (if (valid-machine-state? machine-state) #t (error "林存档错误：机器状态非法"))
      (if (valid-rescue-state? rescue-state) #t (error "林存档错误：救援参与非法"))
      (if (valid-conflict-result? conflict-result) #t (error "林存档错误：工棚冲突结果非法"))
      (if (valid-theater-result? theater-result) #t (error "林存档错误：剧院回收结果非法"))
      (if (and (number? conflict-day) (>= conflict-day 0)) #t (error "林存档错误：工棚冲突日非法"))
      (if (= arrival-day 0)
          (if (and (= acceptance-day 0) (equal? machine-state "未验收")
                   (installation-clk 'empty?) (not installation-participated?))
              #t
              (error "林存档错误：机器未运抵却残留安装或验收状态"))
          (if (>= acceptance-day arrival-day) #t (error "林存档错误：验收早于机器运抵")))
      (if (and (> (installation-clk 'current) 0) (not installation-participated?))
          (error "林存档错误：安装有进度却没有参与记录")
          #t)
      (if (and (equal? machine-state "精密调试") (not (installation-clk 'full?)))
          (error "林存档错误：未完成项目却写成精密调试")
          #t)
      (if (and installation-participated? (equal? relationship "陌生"))
          (error "林存档错误：参与过安装却仍与林陌生")
          #t)
      (if (and (equal? machine-state "精密调试") (not (equal? relationship "信任")))
          (error "林存档错误：精密调试完成却未建立技术信任")
          #t)
      (cond
        ((equal? rescue-state "未发生") #t)
        ((equal? rescue-state "基础机器")
         (if (equal? machine-state "保守试运") #t
             (error "林存档错误：基础机器救援与验收状态不一致")))
        ((equal? rescue-state "精密机器")
         (if (equal? machine-state "精密调试") #t
             (error "林存档错误：精密机器救援与验收状态不一致")))
        (else (error "林存档错误：无法交叉校验救援参与")))
      (if (and (member? conflict-result (list "护送离开" "弗兰克调停" "警卫清场"))
               (equal? relationship "陌生"))
          (error "林存档错误：当面处理过工棚冲突却仍与林陌生")
          #t)
      (if (and (equal? conflict-result "护送离开") (not (equal? relationship "信任")))
          (error "林存档错误：护送林离开却未写回信任")
          #t)
      (if (and (equal? theater-result "可靠帮助") (not (precise?)))
          (error "林存档错误：未完成精密调试却有可靠剧院帮助")
          #t)
      (if (equal? conflict-result "未发生")
          #t
          (if (> conflict-day 0) #t (error "林存档错误：工棚冲突已结算却没有发生日"))))

    (define (validate-with-collapse!)
      (validate-state!)
      (let ((collapse-state (dock-collapse 'state)))
        (if (member? collapse-state (list "已结算" "缺席结算"))
            (begin
              (if (equal? rescue-state "未发生")
                  (error "林存档错误：坍塌已结算却没有写回机器参与")
                  #t)
              (if (> conflict-day 0) #t (error "林存档错误：坍塌已结算却未开放工棚冲突")))
            (if (and (equal? rescue-state "未发生") (= conflict-day 0)
                     (equal? conflict-result "未发生"))
                #t
                (error "林存档错误：坍塌未结算却已有事故后状态")))))

    (define (validate-chapter-end!)
      (validate-with-collapse!)
      (if (machine-accepted?) #t (error "第一章结算错误：试验机仍未验收"))
      (if (equal? rescue-state "未发生")
          (error "第一章结算错误：林与机器尚未写回坍塌救援")
          #t)
      (if (equal? conflict-result "未发生")
          (error "第一章结算错误：少了一班人尚未结算")
          #t))

    (define-turn-rule "试验装卸机运抵"
      (lambda () (and (= arrival-day 0) (>= (three-letters 'story-stage) 2)))
      (lambda ()
        (set! arrival-day world-day)
        (set! acceptance-day (+ world-day acceptance-delay))
        (sync-globals!)
        (spotlight! "三号货栈的试验工棚"
          "试验装卸机已经运到旧码头。公司在三号货栈后面围出一间工棚，林正缺人处理底座、管线、配重和负载试验。")))

    (define-turn-rule "试验装卸机验收"
      (lambda ()
        (and (> arrival-day 0) (not (machine-accepted?)) (>= world-day acceptance-day)))
      (lambda () (settle-machine! #t)))

    (define-turn-rule "少了一班人缺席结算"
      (lambda () (and (conflict-pending?) (> world-day conflict-day)))
      (lambda ()
        (set! conflict-result "玩家缺席")
        (sync-globals!)
        (spotlight! "工棚门前清场"
          "你没有去三号货栈。公司警卫在傍晚把人群推出围栏，机器留在原地，那班人的名字没有回到班表上。")))

    (sync-globals!)
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'workshop-open?) (workshop-open?))
          ((equal? msg 'workshop-nodes) (workshop-nodes))
          ((equal? msg 'theater-nodes) (theater-nodes))
          ((equal? msg 'relationship) relationship)
          ((equal? msg 'known?) (known?))
          ((equal? msg 'trusted?) (trusted?))
          ((equal? msg 'machine-state) machine-state)
          ((equal? msg 'precise?) (precise?))
          ((equal? msg 'rescue-state) rescue-state)
          ((equal? msg 'conflict-result) conflict-result)
          ((equal? msg 'theater-result) theater-result)
          ((equal? msg 'prepare-rescue!) (prepare-rescue!))
          ((equal? msg 'on-dock-collapse!) (on-dock-collapse! (cadr args) (caddr args)))
          ((equal? msg 'validate!) (validate-state!))
          ((equal? msg 'validate-with-collapse!) (validate-with-collapse!))
          ((equal? msg 'validate-chapter-end!) (validate-chapter-end!))
          ((equal? msg 'sync-globals!) (sync-globals!))
          ((equal? msg 'save)
           (list
             (list "arrival-day" arrival-day)
             (list "acceptance-day" acceptance-day)
             (list "relationship" relationship)
             (list "installation-participated?" installation-participated?)
             (list "machine-state" machine-state)
             (list "installation-progress" (installation-clk 'save))
             (list "rescue-state" rescue-state)
             (list "conflict-result" conflict-result)
             (list "conflict-day" conflict-day)
             (list "theater-result" theater-result)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! arrival-day (required-field data "arrival-day"))
             (set! acceptance-day (required-field data "acceptance-day"))
             (set! relationship (required-field data "relationship"))
             (set! installation-participated? (required-field data "installation-participated?"))
             (set! machine-state (required-field data "machine-state"))
             (installation-clk 'load! (required-field data "installation-progress"))
             (set! rescue-state (required-field data "rescue-state"))
             (set! conflict-result (required-field data "conflict-result"))
             (set! conflict-day (required-field data "conflict-day"))
             (set! theater-result (required-field data "theater-result"))
             (validate-state!)
             (sync-globals!)))
          (else (error "林：收到未知消息")))))))
