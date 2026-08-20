;; scenes/encounters/巷子里在打人.scm - 第一章·艾迪线的开场
;;
;; 一场很短的交锋，只有一个题：撵走三个人，和他还能不能站起来。
;; 两根钟互相抢你的手：每一手花在推开他们上，他就在地上多挨一轮；
;; 每一手花在把他拽出来上，那三个人就多站一轮。
;;
;; 「把他们撵开」满格 → 成功；「他还撑得住」归零 → 失败。
;; 失败不是白跑一趟，是这条线到此为止：他今晚之后就不是原来那个人了。
;;
;; 对外契约：回传 'success / 'fail。见 world/艾迪.scm 的 on-alley-result。

(define push-max 6)   ; 四颗骰、一回合打得干净就够；打得脏就得进第二轮，而他只撑得住三轮
(define hold-max 3)

(define push-clk
  (make-clock "把他们撵开" push-max 'segments
    "他们只是来传话的，不想真闹出人命。满格他们就走了。"))

(define hold-clk
  (make-clock "他还撑得住" hold-max 'countdown
    "地上那个还剩多少。每一回合他们都还在打，跟你做什么无关。"))

(define finished? #f)

(define (tick-n! clk n)
  (if (<= n 0) #f (begin (clk 'tick!) (tick-n! clk (- n 1)))))

(define (finish! result title text)
  (if finished? (error "巷子里在打人：交锋已经结算") #t)
  (set! finished? #t)
  (spotlight! title text)
  (end-encounter result))

(define (check!)
  (if finished?
      #f
      (cond
        ((push-clk 'full?)
         (finish! 'success "他们走了"
           "三个人往巷子那头退，走得不快，像是办完了事。靠墙那个撑着膝盖站起来，满脸是血。"))
        ((hold-clk 'empty?)
         (finish! 'fail "他们办完了"
           "等他们让开，地上那个已经不动了。有人从酒馆后门出来，把他架进去。你什么也没来得及做。"))
        (else #f))))

(define (on-encounter-enter)
  (hold-clk 'set! hold-max)
  (play-dialogue!
    (line "世界" "酒馆侧墙那条巷子。三个人围着一个，靠墙那个已经不还手了。")
    (line "打人的" "不是说好第四回合吗。")
    (line "打人的" "第四回合。你他妈数得清吗。")
    (line "世界" "他们不着急。这一片今晚不会有巡警过来。")))

;; ── 撵人 ────────────────────────────────────────
(define (node-charge)
  (node "冲进去"
    :tags (list "高风险")
    :subtitle "武力；三个打一个，你进去就是三个打两个"
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "你被顶回墙上"
        (lambda () (spend-composure! 1) (check!)))
      (outcome "他们分了一个人出来对付你"
        (lambda () (push-clk 'tick!) (check!)))
      (outcome "你把最近的那个推进了砖墙"
        (lambda () (tick-n! push-clk 2) (check!))))))

(define (node-shout)
  (node "喊一嗓子"
    :subtitle "交际；让他们以为你后面还有人。信了就走，不信就加快手脚"
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "他们知道这条巷子今晚是空的"
        (lambda () (hold-clk 'advance! -1) (check!)))
      (outcome "有一个回头看了看巷口"
        (lambda () (push-clk 'tick!) (check!)))
      (outcome "你报了个名字，这一片有的是"
        (lambda () (tick-n! push-clk 2) (check!))))))

;; ── 救人 ────────────────────────────────────────
;; 这一手不推目标钟。它买的是回合，代价是那三个人多站一轮。
(define (node-pull)
  (node "把他从中间拽出来"
    :subtitle "敏锐；不撵人，只是让他少挨几下"
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "有人把你推开了"
        (lambda () (check!)))
      (outcome "你把他拖开半步"
        (lambda () (hold-clk 'tick!) (check!)))
      (outcome "你把他整个拖到了墙根"
        (lambda () (hold-clk 'tick!) (push-clk 'tick!) (check!))))))

(define (node-leave)
  (instant-action "算了，走开"
    (lambda ()
      (finish! 'fail "你转身走了"
        "身后的声音又响了几下，然后停了。第二天没有人提起这件事。"))))

;; ── 回合末 ──────────────────────────────────────
;; 这根钟谁也拦不住：你在做什么，他们就在打什么。
(define-turn-rule "他们还在打"
  (lambda () (not finished?))
  (lambda ()
    (hold-clk 'advance! -1)
    (check!)))

(define (get-render-data)
  (container "巷子里在打人"
    (append
      (clock-nodes (push-clk 'render-data) (hold-clk 'render-data))
      (list (node-charge) (node-shout) (node-pull) (node-leave)))))
