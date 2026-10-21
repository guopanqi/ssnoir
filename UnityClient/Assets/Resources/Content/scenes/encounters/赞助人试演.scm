;; 第二幕 C04：这不是第一次正式彩排。
;; 赞助人复排时伴奏装置出故障。玩家可抢修、找人替代或争取清唱；
;; 机会耗尽后夜莺仍自己决定演下去，不成为等待主角修好的静态道具。
(define 修理进展 (make-clock "抢修进展" 3 'gauge "修好伴奏装置或组织临时伴奏。" :beneficial-direction 'increase))
(define 试演时间 (make-clock "试演剩余时间" 4 'countdown "导演与赞助人不会一直等。" :beneficial-direction 'increase))
(define 试演结束? #f)
(define (试演收场! outcome)
  (if 试演结束? (error "赞助人试演：交锋重复结算") #t)
  (set! 试演结束? #t)
  (cond
    ((eq? outcome '修好设备)
      (play-dialogue!
        (line "世界" "伴奏终于重新响起。夜莺在台上等了一个拍子，接上了自己的歌。")
        (line "夜莺" "我还以为你要把那台机器整个拆了。")
        (line "尼尔" "差一点。")))
    ((eq? outcome '替代伴奏)
      (play-dialogue!
        (line "世界" "酒馆的老乐手坐到钢琴前，试了两个音。")
        (line "夜莺" "这首歌我们以前在酒馆唱过。")
        (line "世界" "她跟上琴声，赞助人的目光终于从故障的设备上移开。")))
    ((eq? outcome '争取清唱)
      (play-dialogue!
        (line "导演" "没有伴奏？好。你只有一首歌的时间。")
        (line "世界" "夜莺站在空舞台上，等台下安静，独自开口。")
        (line "夜莺" "谢谢你给我这个机会。")))
    (else
      (play-dialogue!
        (line "导演" "时间到了。今天先这样吧。")
        (line "夜莺" "等等。让我把最后这一段唱完。")
        (line "世界" "没有伴奏。她依然唱完那段，才走下舞台。"))))
  (end-encounter outcome))
(define (试演尝试! progress cost route)
  (修理进展 'advance! progress)
  (试演时间 'advance! (- cost))
  (cond
    ((修理进展 'full?) (试演收场! route))
    ((试演时间 'empty?) (试演收场! '她自己唱完))
    (else #f)))
(define (试演清唱! success?)
  (试演时间 'advance! -1)
  (cond
    (success? (试演收场! '争取清唱))
    ((试演时间 'empty?) (试演收场! '她自己唱完))
    (else
      (play-dialogue!
        (line "导演" "先去想办法修伴奏。我还没有答应你。")))))
(define (on-encounter-enter)
  (修理进展 'load! 0)
  (试演时间 'load! 4)
  (set! 试演结束? #f)
  (play-dialogue!
    (line "世界" "大剧院。赞助人的试演即将开始，伴奏装置却忽然停了。")
    (line "工作人员" "重新开机至少还得等一会儿。")
    (line "导演" "我们的客人马上就要走了。")
    (line "夜莺" "我可以先唱。")
    (line "导演" "你先等着。")))
(define (on-encounter-collapse)
  (collapse-result '她自己唱完))
(define (get-render-data)
  (container "赞助人试演"
    (append
      (clock-nodes (试演时间 'render-data) (修理进展 'render-data))
      (list
        (note-node "夜莺的决定" "她会继续唱" "设备能否恢复会改变演出，但她不需要你替她决定是否站上舞台。")
        (node "检修伴奏" :subtitle "抢修装置；较容易推进，但可能浪费时间"
          :requires (list (req-die)) :resolve (roll 'sharpness
            (outcome (lambda () (spend-composure! 1) (试演尝试! 0 2 '修好设备)))
            (outcome (lambda () (试演尝试! 1 1 '修好设备)))
            (outcome (lambda () (试演尝试! 2 1 '修好设备)))))
        (node "找旧乐手救场" :subtitle "找能立刻给她伴奏的人；失败会耗掉时间"
          :requires (list (req-die)) :resolve (roll 'social
            (outcome (lambda () (spend-composure! 1) (试演尝试! 0 2 '替代伴奏)))
            (outcome (lambda () (试演尝试! 1 1 '替代伴奏)))
            (outcome (lambda () (试演尝试! 2 1 '替代伴奏)))))
        (node "争取清唱" :subtitle "直接说服导演让夜莺唱，成功便无需修设备"
          :requires (list (req-die)) :resolve (roll 'social
            (outcome (lambda () (spend-composure! 1) (试演清唱! #f)))
            (outcome (lambda () (试演清唱! #t)))
            (outcome (lambda () (试演清唱! #t)))))
        (node "交还给夜莺" :subtitle "退出救场；她仍会以自己的办法完成这段演出"
          :resolve (instant (outcome (lambda () (试演收场! '她自己唱完)))))))))
