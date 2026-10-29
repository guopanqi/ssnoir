;; A：人物协作。独立试玩，保留原版；同轮正常推进→指挥→逐人出手→救援期限。

(define coat-life-max 6)
(define hat-life-max 3)
(define sleeves-life-max 3)

(define eddie-rounds 4)
(define eddie-clk
  (make-clock "艾迪还撑得住" eddie-rounds 'countdown
    "每回合末走一格。归零时你还没清场，他就撑不住了。" :beneficial-direction 'increase))

(define coat-life
  (make-clock "生命" coat-life-max 'gauge "他的生命值。个子最大，撑得最久。" :beneficial-direction 'decrease))
(define coat-attack
  (make-clock "皮夹克·出手" 3 'countdown "归零就出手：这一下最重。" :beneficial-direction 'increase))

(define hat-life
  (make-clock "生命" hat-life-max 'gauge "他的生命值。归零他就站不住。" :beneficial-direction 'decrease))
(define hat-attack
  (make-clock "毡帽·出手" 2 'countdown "归零就出手：快，但不重。" :beneficial-direction 'increase))

(define sleeves-life
  (make-clock "生命" sleeves-life-max 'gauge "他的生命值。最不经打的一个。" :beneficial-direction 'decrease))
(define sleeves-attack
  (make-clock "卷袖子·出手" 3 'countdown "归零就出手：快，但不重。" :beneficial-direction 'increase))

(define finished? #f)
(define round-count 0)

(define (finish! result title text)
  (if finished? (error "巷子里在打人（新实验）：Scenario已经结算") #t)
  (set! finished? #t)
  (spotlight! title text)
  (end-scenario result))

(define (on-scenario-collapse)
  (collapse-result '倒下))

(define (all-down?)
  (and (coat-life 'empty?) (hat-life 'empty?) (sleeves-life 'empty?)))

(define (check-victory!)
  (if (and (not finished?) (all-down?))
      (finish! 'success "三个人都倒了"
        "三个打手躺在巷子里。靠墙那个抹掉嘴角的血，自己站了起来。")
      #f))

(define (hit-enemy! name life amount)
  (if (life 'empty?)
      (error "巷子里在打人（新实验）：试图攻击已经退场的人")
      #t)
  (life 'advance! (- amount))
  (if (life 'empty?)
      (play-motion! (string-append "巷子里在打人/击倒" name) "倒地"
        (string-append "巷战-" name))
      #f)
  (check-victory!))

(define (enemy-node title subtitle life attack)
  (node title
    :anchor title
    :subtitle subtitle
    :clocks (list (life 'render-data) (attack 'render-data))
    :risk '冒险
    :risk-target (list 'composure "player")
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome (lambda ()
          (spend-actor-composure! 'player 1)))
      (outcome (lambda () (hit-enemy! title life 1)))
      (outcome (lambda () (hit-enemy! title life 2))))))

(define (node-coat)
  (enemy-node "皮夹克" "出手冷静−3；站着时会指挥小弟"
    coat-life coat-attack))

(define (node-hat)
  (enemy-node "毡帽" "出手冷静−1；坏冷静−1，中伤1，好伤2"
    hat-life hat-attack))

(define (node-sleeves)
  (enemy-node "卷袖子" "出手冷静−1；坏冷静−1，中伤1，好伤2"
    sleeves-life sleeves-attack))

(define (standing? life) (not (life 'empty?)))

(define (approach! life attack)
  (if (and (standing? life) (not (attack 'empty?)))
      (attack 'advance! -1)
      #f))

(define (define-strike! name life attack damage say)
  (define-opponent-rule (string-append name "出手")
    (lambda () (and (not finished?) (standing? life) (attack 'empty?)))
    (lambda ()
      (play-bubble! (line name say :at name))
      (attack 'set! (attack 'max))
      (play-motion! (string-append "巷子里在打人/击倒" name) "出拳"
        (string-append "巷战还击-" name))
      (spend-actor-composure! 'player damage))))

(define (on-scenario-enter)
  (set! finished? #f)
  (set! round-count 0)
  (eddie-clk 'load! eddie-rounds)
  (coat-life 'load! coat-life-max)
  (coat-attack 'load! 3)
  (hat-life 'load! hat-life-max)
  (hat-attack 'load! 2)
  (sleeves-life 'load! sleeves-life-max)
  (sleeves-attack 'load! 3)
  (play-dialogue!
    (line "世界" "酒馆侧巷，三个人把艾迪堵在墙边。毡帽又朝他逼近了一步。"))
  (play-motion! "巷子里在打人/开场" "围殴" "巷战-开场")
  (play-dialogue!
    (line "尼尔" "放开他。")
    (line "世界" "你站在巷口这一侧。皮夹克看见你，抬了抬下巴。")
    (line "皮夹克" "你们俩，给我把他赶走。")))


(define-opponent-rule "他们在逼近"
  (lambda () (not finished?))
  (lambda ()
    (approach! hat-life hat-attack)
    (approach! sleeves-life sleeves-attack)
    (approach! coat-life coat-attack)))

(define (command-target)
  (let ((hat? (and (standing? hat-life) (not (hat-attack 'empty?))))
        (sleeves? (and (standing? sleeves-life) (not (sleeves-attack 'empty?)))))
    (cond
      ((and hat? sleeves?)
       (if (<= (hat-attack 'current) (sleeves-attack 'current)) 'hat 'sleeves))
      (hat? 'hat)
      (sleeves? 'sleeves)
      (else #f))))

(define-opponent-rule "皮夹克指挥"
  (lambda () (and (not finished?) (standing? coat-life) (not (not (command-target)))))
  (lambda ()
    (if (eq? (command-target) 'hat)
        (begin
          (hat-attack 'advance! -1)
          (play-bubble! (line "皮夹克" "毡帽，别让他喘气。" :at "皮夹克")))
        (begin
          (sleeves-attack 'advance! -1)
          (play-bubble! (line "皮夹克" "你，卷袖子的，上啊。" :at "皮夹克"))))))

(define-strike! "毡帽" hat-life hat-attack 1
  "看哪儿呢。")

(define-strike! "卷袖子" sleeves-life sleeves-attack 1
  "转过来。")

(define-strike! "皮夹克" coat-life coat-attack 3
  "站好了。")

(define-opponent-rule "艾迪撑不住了"
  (lambda () (not finished?))
  (lambda ()
    (set! round-count (+ round-count 1))
    (eddie-clk 'advance! -1)
    (play-bubble! (line "世界" "靠墙的人又矮下去一点。"))
    (if (and (not finished?) (eddie-clk 'empty?))
        (finish! 'fail "艾迪撑不住了"
          "你还在和面前的人纠缠。后面传来一声闷响，艾迪顺着墙倒下去，没有再动。")
        #f)))

(define (get-render-data)
  (node "巷子里在打人"
    :anchor "巷子里在打人"
    :children
      (append
        (list
          (note-node "领头：目标" "目标" "四轮内打退三名打手，救下艾迪。")
          (note-node "领头：出手规则" "回合结束"
            "支付通常的冷静代价；存活敌人的出手倒计时−1，归零出手后重置。攻击坏冷静−1、中伤1、好伤2。"))
        (if (and (standing? coat-life) (or (standing? hat-life) (standing? sleeves-life)))
            (list (note-node "领头：指挥规则" "皮夹克指挥"
              "正常倒计时后，尚未归零的小弟中，最接近出手的一人额外−1；平手选毡帽。随后各出手一次。"))
            '())
        (map (lambda (n) (at-anchor "艾迪还撑得住" n))
             (clock-nodes (eddie-clk 'render-data)))
        (if (coat-life 'empty?) '() (list (node-coat)))
        (if (hat-life 'empty?) '() (list (node-hat)))
        (if (sleeves-life 'empty?) '() (list (node-sleeves))))))
