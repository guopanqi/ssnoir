(load-file "scripts/theatre/第二幕城市镜头.scm")
;; 第二幕公共生活：大船、码头、经理的下一份调查邀请。彩排与亲密由剧院模块拥有。
(define 第二幕节奏
 (let ()
  (define tavern? #f)
  (define ship? #f)
  (define ship-news? #f)
  (define ship-work "未参加") ; 未参加／已结算
  (define dock-mark? #f)
  (define poster? #f)
  (define invitation? #f)
  (define case-offered? #f)
  (define (active?) (and (= (第一幕 'act) 2) (not (第二章 'started?))))
  (define (empty-tavern!)
    (set! tavern? #t)
    (play-dialogue!
      (line "世界" "小舞台还在，灯却没亮。酒保擦着那块从前放歌谱的吧台。")
      (line "调酒师" "她走了以后，这儿倒清静。")
      (line "尼尔" "你不是总嫌她唱得太晚？")
      (line "调酒师" "嫌归嫌。晚上少点动静，酒都卖得慢了。")
      (line "调酒师" "剧院在招搬布景的。你不是总嫌这儿没出息？")))
  ;; 与弗兰克抢修船分开拥有在泊事实；表现层汇总两条剧情的泊位占用。
  ;; 抵港演出先播放，随后采用在泊快照；不能在当天开始时提前摆成靠岸终态。
  (define (sync-ship!)
    (set-global! '抢卸货船在泊 (and (= world-day 9) ship?))
    (set-global! '抢卸夜班亲历 (equal? ship-work "已结算")))
  (define (ship-news!)
    (set! ship-news? #t)
    (play-dialogue!
      (line "世界" (if (第一幕 'eddie-met?)
        "艾迪托人带来消息：一艘大船今天临时靠港，码头要加人手。"
        "旅馆门口有人传招工消息：一艘大船今天临时靠港，码头要加人手。"))
      (line "世界" "今天去码头可参加抢卸夜班。安全落货有四十八美元，延期也有六美元辛苦钱。")))
  ;; 招工在当天走出住处或诊所、回到城市时获知，不在醒来时广播。
  (define-turn-start-rule "抢卸货船泊位"
    (lambda () #t) sync-ship!)
  (define (ship-arrival!)
    (set! ship? #t)
    (sync-ship!)
    (第二幕-大船入港)
    (play-dialogue!
      (line "世界" "临时靠港的大船已经系稳，工头在货堆前招呼愿意接夜班的人。")
      (line "世界" "这班有额外报酬。尼尔可以参加，也可以照自己的计划离开。")))
  (define (ship-end! result)
    (if (member? result '(安全落货 延期卸载)) #t
      (error "大船：非法Scenario结果"))
    (if (equal? ship-work "已结算") (error "大船重复结算") #t)
    (set! ship-work "已结算")
    (sync-ship!)
    ;; 安全落货发完整加班费与奖金；未完成（包括倒下）只发固定辛苦钱。
    (add-item! "金钱" (if (eq? result '安全落货) (+ 36 12) 6)))
  (define (join-ship!)
    (if (and (= world-day 9) (equal? ship-work "未参加")) #t
      (error "大船：不在夜班窗口"))
    (第一幕 'meet-eddie!)
    (start-scenario "大船靠岸（联合起吊）" ship-end!))
  (define (dock-mark!)
    (set! dock-mark? #t)
    (第二幕-货栈白线))
  (define (poster!)
    (set! poster? #t)
    (第二幕-新城海报))
  (define (offer-case!)
    (set! case-offered? #t)
    (第三幕 'offer!))
  (define (next-job!)
    (set! invitation? #t)
    (第三幕 'start!))
  (define (arrivals-at loc)
    (if (not (active?)) '()
      (append
        (if (and (= world-day 9) (not ship-news?))
          (list (arrival "临时大船的招工消息" ship-news!)) '())
        (if (and (= world-day 7) (equal? loc "老街酒馆") (not tavern?))
          (list (arrival "酒馆里少了一个人" empty-tavern!)) '())
        (if (and (= world-day 9) (equal? loc "码头") (not ship?))
          (list (arrival "大船靠岸" ship-arrival!)) '())
        (if (and (>= world-day 15) (equal? loc "码头") (not dock-mark?))
          (list (arrival "旧港测绘标记" dock-mark!)) '())
        (if (and (>= world-day 15) (equal? loc "剧院") (not poster?))
          (list (arrival "新城海报" poster!)) '())
        (if (and (equal? loc "剧院") (第二幕生活 'manager-met?)
                 (第二幕剧院 'case-followup-ready?) (not case-offered?) (not invitation?)
                 ;; 海报已出现才接下一场，避免一次进门连续交付两个新事件。
                 (or (< world-day 15) poster?))
          (list (arrival "经理的新委托" offer-case!)) '()))))
  (define (nodes-at loc)
    (if (not (active?)) '()
      (append
        (if (and (equal? loc "码头") (= world-day 9)
                 (equal? ship-work "未参加"))
          (list (node "参加大船夜班" :name "参加抢卸夜班"
            :subtitle "安全落货领48块，延期卸载领6块"
            :resolve (instant (outcome join-ship!)))) '())
        (if (and (equal? loc "剧院") case-offered? (not invitation?))
          (list (instant-action "接下黑衣人调查" next-job!)) '()))))
  (define (dossier-at)
    (append
      (if (and (active?) (= world-day 9))
      (list (dossier "临时货船" :kind '城市 :primary #f
        :now (if (equal? ship-work "已结算")
          "码头抢卸夜班已经收工，工人回了旧街食堂。"
          "大船今天临时靠港。可去码头参加夜班，安全落货48美元，延期6美元；也可不参加。")
        :where "码头"))  '())
      (if (and (active?) case-offered? (not invitation?))
        (list (dossier "经理的正式委托" :kind '委托
          :now "经理愿付八十美元，请你查清剧院外黑衣人的身份与活动地点。"
          :steps (list (step "到剧院找经理，决定是否接下黑衣人调查。" #f))
          :where "剧院")) '())))
  (set-global! '抢卸货船在泊 #f)
  (set-global! '抢卸夜班亲历 #f)
  (lambda args
    (let ((msg (car args)))
      (cond
        ((eq? msg 'nodes-at) (nodes-at (cadr args)))
        ((eq? msg 'arrivals-at) (arrivals-at (cadr args)))
        ((eq? msg 'dossier) (dossier-at))
        ((eq? msg 'ship-work) ship-work)
        ((eq? msg 'case-offered?) case-offered?)
        ((eq? msg 'save) (list (list "tavern" tavern?) (list "ship" ship?) (list "ship-news" ship-news?) (list "ship-work" ship-work)
          (list "dock-mark" dock-mark?)
          (list "poster" poster?) (list "invitation" invitation?) (list "case-offered" case-offered?)))
        ((eq? msg 'load!)
          (let ((data (cadr args)))
            (set! tavern? (assoc-get data "tavern" #f))
            (set! ship? (assoc-get data "ship" #f))
            (set! ship-news? (assoc-get data "ship-news" #f))
            (set! ship-work (assoc-get data "ship-work" "未参加"))
            (set! dock-mark? (assoc-get data "dock-mark" #f))
            (set! poster? (assoc-get data "poster" #f))
            (set! invitation? (assoc-get data "invitation" #f))
            (set! case-offered? (assoc-get data "case-offered" invitation?))
            (sync-ship!)))
        (else (error "第二幕节奏：未知消息")))))))
