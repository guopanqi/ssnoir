;; 艾迪自己攒钱；一次伤手最多损失两格，不帮助也会继续向前。
(define 艾迪的房间
  (let ()
    (define known? #f)
    (define checked-day 0)
    (define hurt-once? #f)
    (define hurt-left 0)
    (define help-day 0)
    (define moving-seen? #f)
    (define 储蓄 (make-clock "艾迪自己的房间" 12 'gauge
      "他每天攒一点钱，想租一间不必和别人轮床的房间。" :beneficial-direction 'neutral))
    (define (intro!)
      (set! known? #t) (set! checked-day world-day)
      (play-dialogue!
        (line "世界" "艾迪把工钱分成两叠，小的塞进衣袋，大的夹进一本旧拳赛杂志。")
        (line "艾迪" "楼上有间房，我去看过了。窗能开，有自己的钥匙。")
        (line "尼尔" "要搬？")
        (line "艾迪" "攒够押金就搬。不用等别人下床才能睡，手套也有地方晾。你来了，咱们还能坐下喝一杯。")))
    (define (new-day!)
      (if (and known? (> world-day checked-day))
        (begin
          (let loop ()
            (if (< checked-day world-day)
              (begin
                (set! checked-day (+ checked-day 1))
                (if (储蓄 'full?) #f
                  (cond
                    ((> hurt-left 0)
                      (if (= help-day (- checked-day 1)) #f (储蓄 'advance! -1))
                      (set! hurt-left (- hurt-left 1)))
                    ((and (>= (储蓄 'current) 6) (not hurt-once?))
                      (set! hurt-once? #t) (set! hurt-left 2))
                    (else (储蓄 'advance! 1))))
                (loop)) #f))) #f))
    (define-turn-start-rule "艾迪攒房钱" (lambda () #t) new-day!)
    (define (help! good?)
      (if (and (> hurt-left 0) (not (= help-day world-day))) #t (error "艾迪养手：今天不能再帮忙"))
      (set! help-day world-day)
      (if good? (储蓄 'advance! 1) #f)
      (play-bubble! (line "艾迪" "这几件事有人搭手，我就不用花钱叫人跑腿了。谢了。" :at "艾迪@码头")))
    (define (moved!)
      (set! moving-seen? #t)
      (spotlight-page! ""
        (list
          (list "艾迪把新钥匙放在手心，翻过来，又翻过去。" "EventSheets/城市生活/新房间")
          (list "床是旧的，窗能打开。拳击手套已经挂在窗边，不用再收进床底。" ""))
        (lambda () #f))
      (play-dialogue!
        (line "艾迪" "租下了！改天来坐坐。椅子只有一把，咱们搬个箱子就是。")
        (line "尼尔" "行。这回总算不用轮着睡了。")))
    (lambda (msg . args)
      (cond
        ((eq? msg 'resting?) (> hurt-left 0))
        ((eq? msg 'arrivals-at)
          (cond ((and (equal? (car args) "码头") (码头工作 'met?) (not known?)
                      (>= world-day (+ (码头工作 'met-day) 5)))
                  (list (arrival "艾迪的房钱" intro!)))
                ((and (equal? (car args) "码头") known? (储蓄 'full?) (not moving-seen?))
                  (list (arrival "艾迪租下房间" moved!))) (else '())))
        ((eq? msg 'nodes-at)
          (if (and known? (equal? (car args) "码头"))
            (append
              (list (at-anchor "艾迪@码头" (clock-node "艾迪攒房钱" (储蓄 'render-data))))
              (if (> hurt-left 0)
                (list
                  (at-anchor "艾迪@码头" (note-node "艾迪伤手近况" "艾迪的手"
                    "手掌被吊索磨破，歇两天。没人搭手，他也会自己好起来，只是房钱得缓缓。"))
                  (node "替艾迪跑腿" :name "照应艾迪" :anchor "艾迪@码头"
                    :subtitle "帮他处理零碎活，免去一天开销；顺利时还能省下一点。"
                    :disabled (= help-day world-day) :requires (list (req-die)) :risk '稳妥
                    :resolve (roll 'knowledge (outcome (lambda () (help! #f)))
                      (outcome (lambda () (help! #f))) (outcome (lambda () (help! #t)))))) '())) '()))
        ((eq? msg 'dossier)
          (if known?
            (list (dossier "艾迪自己的房间" :kind '人物 :where "码头"
              :status (if moving-seen? '了结 '进行中)
              :now (cond ((储蓄 'full?) "艾迪攒够了押金，租到了自己的房间。")
                ((> hurt-left 0) "艾迪伤了手，要歇两天。可以帮他处理零碎活，免去开销；不帮他也会慢慢攒够。")
                (else "艾迪每天自己攒些房钱，不需要你替他完成。碰面时可以看看他的近况。"))
              :clocks (list (储蓄 'render-data)))) '()))
        ((eq? msg 'save) (list (list "known" known?) (list "checked" checked-day)
          (list "hurt-once" hurt-once?) (list "hurt-left" hurt-left) (list "help-day" help-day)
          (list "moving-seen" moving-seen?) (list "savings" (储蓄 'save))))
        ((eq? msg 'load!) (let ((d (car args)))
          (set! known? (assoc-get d "known" #f)) (set! checked-day (assoc-get d "checked" 0))
          (set! hurt-once? (assoc-get d "hurt-once" #f)) (set! hurt-left (assoc-get d "hurt-left" 0))
          (set! help-day (assoc-get d "help-day" 0)) (set! moving-seen? (assoc-get d "moving-seen" #f))
          (储蓄 'load! (assoc-get d "savings" 0))
          (if (and (>= checked-day 0) (member? hurt-left '(0 1 2)) (>= help-day 0)) #t
            (error "艾迪房钱：非法存档"))))
        (else (error "艾迪房钱：未知消息"))))))
