;; 第一章唯一的公共事故：码头坍塌。
;;
;; 本模块只拥有事故排期、响应状态与公共救援结果。乔拥有关系、伤势和照料；
;; 交锋只拥有现场目标与自身回合。任何人物线都只能接入这里，不能另造一次事故。
;;
;; 乔不在救援现场——玩家赶到以前他已经被救出。本事件只负责触发他的事故后人物线；
;; 现场伤亡和玩家是否到场，都不改变乔的伤势与后续内容。

(define dock-collapse
  (let ()
    ;; 未浮现 / 可发生 / 待响应 / 救援中 / 已结算 / 缺席结算
    (define event-state "未浮现")
    ;; 未结算 / 小型 / 大型。零人死亡也属于小型；玩家缺席不是另一种灾难规模。
    (define rescue-result "未结算")
    ;; 交锋直接返回灾难规模；世界不保存或重新解释现场人数。
    (define scheduled-day 0)
    (define response-day 0)

    ;; 老街阶段浮现后隔两日尝试安排；若当天有主线强制事件，就顺延到下一个清静早晨。
    (define schedule-delay 2)
    ;; 两日城市响应窗口走到缺席结算后，还要让乔度过送回当天与四个完整照料日终；
    ;; 首演规则先于人物规则执行，因此待响应日距首演至少要有八天。
    (define minimum-response-window 8)

    (define (required-field data key)
      (let ((value (assoc-get data key 'missing)))
        (if (equal? value 'missing)
            (error (string-append "码头坍塌存档错误：缺少 " key))
            value)))

    (define (valid-state? value)
      (member? value (list "未浮现" "可发生" "待响应" "救援中" "已结算" "缺席结算")))

    (define (valid-result? value)
      (member? value (list "未结算" "小型" "大型")))

    (define (validate-state!)
      (if (valid-state? event-state) #t (error "码头坍塌存档错误：公共状态非法"))
      (if (valid-result? rescue-result) #t (error "码头坍塌存档错误：救援结果非法"))
      (if (and (number? scheduled-day) (>= scheduled-day 0))
          #t (error "码头坍塌存档错误：排期日非法"))
      (if (and (number? response-day) (>= response-day 0))
          #t (error "码头坍塌存档错误：响应日非法"))
      (cond
        ((equal? event-state "未浮现")
         (if (and (equal? rescue-result "未结算") (= scheduled-day 0) (= response-day 0))
             #t (error "码头坍塌存档错误：未浮现状态残留排期或结果")))
        ((equal? event-state "可发生")
         (if (and (equal? rescue-result "未结算") (> scheduled-day 0) (= response-day 0))
             #t (error "码头坍塌存档错误：可发生状态缺少唯一排期")))
        ((or (equal? event-state "待响应") (equal? event-state "救援中"))
         (if (and (equal? rescue-result "未结算") (> scheduled-day 0) (> response-day 0))
             #t (error "码头坍塌存档错误：进行中状态缺少响应日或提前有结果")))
        ((equal? event-state "已结算")
         (if (member? rescue-result (list "小型" "大型"))
             #t (error "码头坍塌存档错误：现场救援没有登记有效结果")))
        ((equal? event-state "缺席结算")
         (if (member? rescue-result (list "小型" "大型"))
             #t (error "码头坍塌存档错误：缺席状态的结果不一致")))
        (else (error "码头坍塌存档错误：无法校验公共状态"))))

    (define (validate-with-joe!)
      (validate-state!)
      (let ((joe-state (joe 'final-state))
            (relationship? (joe 'relationship-established?)))
        (cond
          ((member? event-state (list "未浮现" "可发生" "待响应" "救援中"))
           (if (equal? joe-state "尚未结算")
               #t (error "码头坍塌存档错误：事故未结算但乔已有事故后命运")))
          ((member? event-state (list "已结算" "缺席结算"))
           (if (equal? joe-state "尚未结算")
               (error "码头坍塌存档错误：事故已结算但乔的命运尚未写回") #t)
           (if relationship?
               (if (equal? joe-state "未介入残疾")
                   (error "码头坍塌存档错误：已完成接送却被写成未介入残疾") #t)
               (if (equal? joe-state "未介入残疾")
                   #t (error "码头坍塌存档错误：关系不足时乔必须结算为未介入残疾"))))
          (else (error "码头坍塌存档错误：无法交叉校验乔的命运")))))

    (define (validate-chapter-end!)
      (validate-with-joe!)
      (if (member? event-state (list "已结算" "缺席结算"))
          #t
          (error "第一章结算错误：码头坍塌尚未完成或仍在等待响应"))
      (let ((joe-state (joe 'final-state)))
        (if (or (equal? joe-state "尚未结算") (equal? joe-state "养伤中"))
            (error "第一章结算错误：乔的四日照料尚未得到最终结果")
            #t)))

    (define (days-to-premiere) (three-letters 'days-to-premiere))

    (define (aftermath-text)
      (cond
        ((equal? rescue-result "小型")
         "旧栈桥已经封死。报纸把事故放在城内版，封锁线外只剩零散的记者。")
        ((equal? rescue-result "大型")
         "旧栈桥已经封死。死者名单登上头版，公司和市政厅都开始争夺事故的说法。")
        (else
         "旧栈桥已经封死。弗兰克、林和码头上的人挖了两天，你不在场。")))

    (define (settle! result final-state)
      (if (or (equal? event-state "救援中") (equal? event-state "待响应"))
          #t (error "码头坍塌：结算发生在不可能的公共状态"))
      (if (valid-result? result) #t (error "码头坍塌：交锋返回了未登记的公共结果"))
      (set! rescue-result result)
      (set! event-state final-state)
      ;; TODO：救援归功暂不实现。后续确有消费点时，再独立设计工人 / 机器的归功规则。
      (set-global! (quote 坍塌-灾难规模) rescue-result)
      ;; 只发送“事故已经发生”这一事实，不把任何现场结果传进乔的人物模块。
      (joe 'on-dock-collapse!)
      ;; 林线只读取救援结果并开放事故后内容；不参与本事件的排期或结算条件。
      (lin 'on-dock-collapse! rescue-result (equal? final-state "已结算"))
      (spotlight! (if (equal? final-state "缺席结算") "码头传来的消息" "旧码头封了")
                  (aftermath-text)))

    (define (on-rescue-result result)
      (if (member? result (list "小型" "大型"))
          (settle! result "已结算")
          (error "码头坍塌：交锋必须返回小型或大型")))

    (define (start-rescue!)
      (if (equal? event-state "待响应") #t (error "码头坍塌：今天已经不能再进入救援"))
      (if (and (>= world-day response-day) (< (- world-day response-day) 2))
          #t
          (error "码头坍塌：已经越过两日响应窗口"))
      ;; 机器建设不是入场门槛；此处只在救援开始前收口验收结果，供林的人物线读取。
      (lin 'prepare-rescue!)
      (set! event-state "救援中")
      (play-remote-dialogue!
        (line "码头工人" "三号栈桥下去了！货垛、吊杆、分拣棚压在一块！")
        (line "码头工人" "先前抬走了几个。班表上还有四个没上来，谁也说不清在哪一片底下。")
        (line "尼尔" "能走的都拉到封锁线外面。剩下的，一片一片挖。"))
      (start-encounter "码头坍塌" on-rescue-result))

    ;; 你没去，现场也不会停下来：弗兰克带人挖了两天，林把机器开了过去。
    ;; 数字定死在这里，因为它不是玩家的成绩，而是这座城市在没有你的时候的样子。
    (define (settle-absence!)
      (if (equal? event-state "待响应") #t (error "码头坍塌：缺席结算时事件并非待响应"))
      (settle! "小型" "缺席结算"))

    (define (dock-nodes)
      (cond
        ((equal? event-state "待响应")
         (list
           (node "赶去救援"
             :subtitle (string-append
                         "事故响应还剩 "
                         (number->string (max 0 (- 2 (- world-day response-day))))
                         " 天；赶到以后要留在现场，直到救援结算")
             :tags (list "交锋")
             :clocks (list
               (list 'clock "赶到现场" (max 0 (- 2 (- world-day response-day))) 2 'countdown
                     "今天和明天都能赶去；第二天日终仍未进入，就按缺席结算。"))
             :resolve (instant (lambda () (start-rescue!))))))
        ((member? event-state (list "已结算" "缺席结算"))
         (list (observe-action "坍塌后的旧栈桥" (aftermath-text))))
        (else '())))

    (define-turn-rule "码头坍塌取得排期"
      (lambda () (and (equal? event-state "未浮现") (>= (three-letters 'story-stage) 2)))
      (lambda ()
        (set! event-state "可发生")
        (set! scheduled-day (+ world-day schedule-delay))
        (if (>= (days-to-premiere) (+ schedule-delay minimum-response-window))
            #t
            (error "码头坍塌排期错误：老街开放得太晚，无法留下两日响应与四日照料"))))

    (define-turn-rule "码头坍塌等待清静早晨"
      (lambda () (equal? event-state "可发生"))
      (lambda ()
        (if (< (days-to-premiere) minimum-response-window)
            (error "码头坍塌排期错误：强制主线事件挤占了最后可用救援窗口")
            #t)
        (if (and (>= world-day scheduled-day) (not (rest-blocked?)))
            (begin
              (set! event-state "待响应")
              (set! response-day world-day)
              (spotlight! "旧码头安全事故"
                "今晨，旧码头三号栈桥连同货棚发生坍塌。救护车已赶往海边，仍有工人被困。现场将在今天和明天继续搜救。"))
            #f)))

    ;; 待响应状态是在事故早晨写入；完整开放当天与次日，第二天日终后才按缺席结算。
    (define-turn-rule "码头坍塌响应期限"
      (lambda () (and (equal? event-state "待响应") (>= (- world-day response-day) 2)))
      (lambda () (settle-absence!)))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'state) event-state)
          ((equal? msg 'result) rescue-result)
          ((equal? msg 'dock-nodes) (dock-nodes))
          ((equal? msg 'validate!) (validate-state!))
          ((equal? msg 'validate-with-joe!) (validate-with-joe!))
          ((equal? msg 'validate-chapter-end!) (validate-chapter-end!))
          ((equal? msg 'save)
           (list (list "state" event-state)
                 (list "rescue-result" rescue-result)
                 (list "scheduled-day" scheduled-day)
                 (list "response-day" response-day)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! event-state (required-field data "state"))
             (set! rescue-result (required-field data "rescue-result"))
             (set! scheduled-day (required-field data "scheduled-day"))
             (set! response-day (required-field data "response-day"))
             (if (equal? event-state "救援中")
                 (error "码头坍塌存档错误：普通交锋不能保存，城市存档不应停在救援中")
                 #t)
             (validate-state!)))
          ((equal? msg 'debug-make-pending!)
           (if (member? event-state (list "未浮现" "可发生"))
               (begin
                 (set! event-state "待响应")
                 (set! rescue-result "未结算")
                 (set! scheduled-day world-day)
                 (set! response-day world-day))
               (error "码头坍塌调试：事件已经发生")))
          ((equal? msg 'debug-enter!)
           ;; 直接站到封锁线里面。救援现场只能从事故当天进入，调试时不必先走完主线。
           (if (member? event-state (list "未浮现" "可发生" "待响应"))
               (begin
                 (if (or (= scheduled-day 0) (= response-day 0))
                     (begin (set! scheduled-day world-day) (set! response-day world-day)) #f)
                 (set! event-state "待响应")
                 (set! rescue-result "未结算")
                 (start-rescue!))
               (error "码头坍塌调试：事件已经结算")))
          ((equal? msg 'debug-resolve!)
           (if (member? event-state (list "未浮现" "可发生" "待响应"))
               (begin
                 (if (or (= scheduled-day 0) (= response-day 0))
                     (begin (set! scheduled-day world-day) (set! response-day world-day)) #f)
                 (set! event-state "救援中")
                 (on-rescue-result (cadr args)))
               (error "码头坍塌调试：事件已经结算")))
          (else (error "码头坍塌：收到未知消息")))))))
