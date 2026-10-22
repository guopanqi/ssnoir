;; 码头——第一章新版城市生计。旧《三封信》的邮箱与诱捕内容已经退役。
;; 艾迪每天首次失手后恢复1冷静；不修改判定概率。
(define dock
  (let ()
    ;; 码头只拥有工资与每日救场；人物剧情阶段由第一幕拥有。
    (define help-day 0)

    (define (haul-subtitle)
      (cond
        ((not (第二幕 'eddie-on-dock?))
          "艾迪被停了班；失手会完整损失2点冷静")
        ((= help-day world-day)
          "艾迪今天已安排帮忙；失手损失2冷静")
        (else "失手损失2冷静，艾迪每天可帮忙一次")))

    (define (on-haul! grade)
      (cond
        ((eq? grade '好) (add-item! "金钱" 20))
        ((eq? grade '中) (add-item! "金钱" 10) (spend-composure! 1))
        (else (spend-composure! 2)))
      ;; 人物剧情先处理；第二幕生活也记录当天码头工作。
      (let ((story-played? (第一幕 'on-work! "码头" grade)))
        (第二幕生活 'on-dock-work!)
        (第二幕 'on-dock-work!)
        ;; 没有第一幕人物对白占用这次失败演出时，艾迪每天可救场一次。
        (if (and (eq? grade '坏) (not story-played?) (not (= help-day world-day))
                 (第二幕 'eddie-on-dock?))
          (begin
            (set! help-day world-day)
            (play-bubble!
              (line "尼尔" "啊！我的手！" :at "码头-货堆")
              (bubble-restore-composure
                (line "艾迪" "我来！你松手！" :at "码头-货堆")
                'player 1 "艾迪帮了你一把，冷静恢复1点。")
              (line "艾迪" "哈哈哈，熟能生巧，下次就好了。" :at "码头-货堆")))
          #f)))

    (define (haul-node)
      (工作 "搬运" '冒险 'violence
        (outcome (lambda () (on-haul! '好)))
        (outcome (lambda () (on-haul! '中)))
        (outcome (lambda () (on-haul! '坏)))
        (haul-subtitle)
        :anchor "码头-货堆"
        :risk-target (list 'composure "player")
        :progress-target (list 'item "金钱")))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((eq? msg 'render-data)
           (list (place "码头"
             :children (append
               (if (not (第二幕 'eddie-on-dock?))
                 (list (note-node "艾迪没来" "工棚" "工头暂停了艾迪的班，码头今天少了一个能帮你的人。"))
                 '())
               (list (haul-node))
               (map (lambda (n)
                      (if (member? :anchor n) n
                        (append n (list :anchor "码头"))))
                 (地点节点 "码头")))
             :arrivals (地点入场 "码头"))))
          ((eq? msg 'eddie-met?) (第一幕 'eddie-met?))
          ((eq? msg 'save) (list (list "help-day" help-day)))
          ((eq? msg 'load!)
           (set! help-day (assoc-get (cadr args) "help-day" 0)))
          (else (error "码头：未知消息")))))))
