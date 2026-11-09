;; 仅 world-load! 调用；旧章节字段只在此读取，不保留运行时转发接口或第二份状态。
(define (前期存档迁移! data)
  (let* ((eddie-data (assoc-get data "eddie-life" '()))
         (old-help (assoc-get (assoc-get data "dock" '()) "help-day" 0)))
    (if (or (assoc-get eddie-data "interactions" #f) (= old-help 0)) #t
      (艾迪生活 'load! (cons (list "interactions"
        (list (list "艾迪搬运搭手" (list (list "last-day" old-help) (list "count" 1))))) eddie-data))))
  (let ((first (assoc-get data "first-act" '()))
        (rhythm (assoc-get data "act-two-rhythm" '()))
        (tavern (assoc-get data "old-street-tavern" '()))
        (city (assoc-get data "act-two-city" '()))
        (boat (assoc-get data "black-gull" '())))
    (if (assoc-get data "lucy-start" #f) #t
      (露西起步 'load! (list (list "stage" (assoc-get first "lucy" 0))
        (list "last-day" (assoc-get first "lucy-day" 0)) (list "work" (min 3 (assoc-get first "lucy-count" 0))))))
    (if (assoc-get data "nightingale-life" #f) #t (夜莺生活 'load! first))
    (if (assoc-get data "dock-career" #f) #t
      (let ((count (min 4 (max (if (> (assoc-get first "eddie" 0) 0) 1 0) (assoc-get first "eddie-count" 0)))))
        (码头工作 'load! (list (list "work" count)
          (list "ship-day" (if (= count 4) (if (assoc-get rhythm "ship" #f) 9 (+ world-day 2)) 0))))))
    (if (assoc-get data "ship-work" #f) #t (大船靠岸 'load! rhythm))
    (if (assoc-get data "tavern-start" #f) #t
      (let ((pending (not (equal? (assoc-get tavern "watch-trouble" "") "")))
            (done (not (assoc-get tavern "watch-first?" #t))))
        (酒馆起步 'load! (list
          (list "work" (if (or pending done) 4 (min 3 (assoc-get tavern "watch-count" 0))))
          (list "stage" (cond (pending "待处理") (done "已收场") (else "消息中")))))))
    (if (assoc-get data "detective-career" #f) #t
      (调查职业 'load! (list (list "referral-day" (if (酒馆起步 'settled?) (+ world-day 2) 0)))))
    (if (assoc-get data "neighborhood-life" #f) #t
      (居民区相识 'load! (list
        (list "invited" (or (assoc-get boat "known" #f) (大船靠岸 'completed?)))
        (list "visited" (assoc-get boat "boarded" #f)))))
    (if (and (= (第一幕 'act) 1) (= (第一幕 'farewell-day) 0) (夜莺生活 'daily-open?))
      (第一幕 'schedule-farewell! (+ world-day 3)) #f)
    (if (or (assoc-get city "frank" #f) (assoc-get boat "boarded" #f)) (frank 'meet!) #f)))
