(load-file "scripts/theatre/第二幕城市镜头.scm")
(set-global! '抢卸货船在泊 #f)
(set-global! '抢卸夜班亲历 #f)
;; 一次重要工作。开放日期由码头成长唯一决定，不再由章节日历硬编码。
(define 大船靠岸
 (let ()
  (define ship? #f)
  (define ship-news? #f)
  (define ship-work "未参加")
  (define (open?) (and (not (第二章 'started?)) (> (码头工作 'ship-day) 0)
    (>= world-day (码头工作 'ship-day)) (< world-day (+ (码头工作 'ship-day) 4))))
  ;; 与弗兰克抢修船分开拥有在泊事实；表现层汇总两条剧情的泊位占用。
  ;; 抵港演出先播放，随后采用在泊快照；不能在当天开始时提前摆成靠岸终态。
  (define (sync-ship!)
    (set-global! '抢卸货船在泊 (and (open?) ship?))
    (set-global! '抢卸夜班亲历 (equal? ship-work "已结算")))
  (define (ship-news!)
    (set! ship-news? #t)
    (spotlight! "大船靠岸" "码头正在招抢卸夜班的人。靠岸起四天内可参加一次，安全落货48美元，延期6美元。"))
  ;; 招工在当天走出住处或诊所、回到城市时获知，不在醒来时广播。
  (define (ship-arrival!)
    (set! ship? #t)
    (sync-ship!)
    (第二幕-大船入港)
    (spotlight-page! ""
      (list
        (list "黑色船体靠上码头，甲板上的探照灯照亮了吊网。" "EventSheets/城市生活/靠岸")
        (list "艾迪站在吊网旁朝你招手。这船只停四天，抢卸夜班正在招人。" ""))
      (lambda () #f)))
  (define (ship-end! result)
    (if (member? result '(安全落货 延期卸载)) #t
      (error "大船：非法Scenario结果"))
    (if (equal? ship-work "已结算") (error "大船重复结算") #t)
    (set! ship-work "已结算")
    (sync-ship!)
    ;; 安全落货发完整加班费与奖金；未完成（包括倒下）只发固定辛苦钱。
    (add-item! "金钱" (if (eq? result '安全落货) (+ 36 12) 6))
    (居民区相识 'invite!))
  (define (followup-ready?)
    (or (equal? ship-work "已结算")
      (and (> (码头工作 'ship-day) 0) (>= world-day (+ (码头工作 'ship-day) 5)))))
  (define (join-ship!)
    (if (and (open?) (equal? ship-work "未参加")) #t
      (error "大船：不在夜班窗口"))
    (start-scenario "大船靠岸（联合起吊）" ship-end!))

  (define-turn-start-rule "抢卸货船泊位"
    (lambda () #t) sync-ship!)
  (lambda (msg . args)
    (cond
      ((eq? msg 'arrived?) ship?)
      ((eq? msg 'followup-ready?) (followup-ready?))
      ((eq? msg 'completed?) (equal? ship-work "已结算"))
      ((eq? msg 'result) ship-work)
      ((eq? msg 'nodes-at)
        (if (and (equal? (car args) "码头") (open?) (equal? ship-work "未参加"))
          (list (node "参加大船夜班" :name "抢卸夜班" :anchor "码头-岸口"
            :subtitle "靠岸四天内参加；安全落货48美元，延期6美元。"
            :clocks (list (clock-priority (list 'clock "抢卸夜班" (max 0 (- (+ (码头工作 'ship-day) 4) world-day)) 4 'countdown "四天窗口结束后不再招工。") 'secondary))
            :resolve (instant (outcome join-ship!)))) '()))
      ((eq? msg 'arrivals-at)
        (append
          (if (and (followup-ready?) (not (居民区相识 'known?))
                   (member? (car args) '("世界" "码头")))
            (list (arrival "错过夜班后艾迪的邀请" (lambda () (居民区相识 'invite!)))) '())
          (if (and (open?) (equal? (car args) "世界") (not ship-news?))
            (list (arrival "临时大船的招工消息" ship-news!)) '())
          (if (and (open?) (equal? (car args) "码头") (not ship?))
            (list (arrival "大船靠岸" ship-arrival!)) '())))
      ((eq? msg 'dossier)
        (if (or (equal? ship-work "已结算") ship-news? ship?)
          (list (dossier "临时货船" :kind '城市 :primary #f
            :status (if (or (equal? ship-work "已结算") (not (open?))) '了结 '进行中)
            :now (cond ((equal? ship-work "已结算") "抢卸夜班收工了。艾迪邀你去老街居民区走走，认认下工后的熟人。")
              ((not (open?)) "大船已离港，你没赶上这班夜工。码头平时的搬运活仍在继续。")
              (else "大船已经靠岸。靠岸起四天内可接一次抢卸夜班，安全落货48美元，延期6美元。")) :where (if (open?) "码头" ""))) '()))
      ((eq? msg 'save) (list (list "ship" ship?) (list "ship-news" ship-news?) (list "ship-work" ship-work)))
      ((eq? msg 'load!) (let ((data (car args)))
        (set! ship? (assoc-get data "ship" #f)) (set! ship-news? (assoc-get data "ship-news" #f))
        (set! ship-work (assoc-get data "ship-work" "未参加"))
        (if (member? ship-work '("未参加" "已结算")) #t (error "大船靠岸：非法工作状态"))
        (sync-ship!)))
      (else (error "大船靠岸：未知消息"))))))
