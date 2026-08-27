;; 第一章·弗兰克唯一交锋「船修好了却不开」。
;;
;; 城市输入：
;;   扣船-抢修结果  "按时修好" / "勉强修好" / "未介入"
;;
;; 对外契约：(list 'success/'fail 工钱格数)
;;   success ＝ 这条街当天拿到了东西（现金或抵债的货）；fail ＝ 船开走了，或者被清场。
;; 工钱格数原样写回城市，分钱那一场按它讲话；被清场不会抹掉此前已经逼出的付款。
;; 以货抵债另设一枚全局 flag：分钱那一场拆的是木箱，不是钱箱。

(define repair-result
  (let ((value (get-global '扣船-抢修结果)))
    (if value value "未介入")))
(if (member? repair-result (list "按时修好" "勉强修好" "未介入"))
    #t
    (error "扣船交锋：缺少有效的抢修结算"))

(define payment-clk
  (make-clock "工钱到账" 6 'gauge
    "已经逼到代理当场付出的份额。三格后可以接受部分付款；填满就是全额到账。"))

(define guard-clk
  (make-clock "警卫进场" 6 'gauge
    "货运公司的电话已经打出去。填满后警卫强制清场，但已经到账的钱不会消失。"))

(define finished? #f)
(define record-used? #f)
(define cargo-pointed? #f)
(define rounds 0)

(define (tick-n! clk n)
  (if (> n 0)
      (begin (clk 'tick!) (tick-n! clk (- n 1)))
      #f))

(define (payment+ n) (tick-n! payment-clk n))
(define (guards+ n) (tick-n! guard-clk n))

(define (finish! result title text)
  (if finished?
      #f
      (begin
        (set! finished? #t)
        (spotlight! title text)
        (end-encounter (list result (payment-clk 'current))))))

(define (on-encounter-collapse)
  (collapse-result (list 'fail (payment-clk 'current))))

(define (finish-full!)
  (play-dialogue!
    (line "货运代理" "钱会从办事处送来。今天。每一个在名单上的人。")
    (line "弗兰克" "名单上还有伤了的、临时顶班的。照我这本付。")
    (line "世界" "代理盯着那本油污账册，最后在付款单上补了两行。"))
  (finish! 'success "全额到账"
    "运钞员把钱箱送上码头。弗兰克让人把关键部件装回去，船在下午离港。"))

(define (finish-lost!)
  (play-dialogue!
    (line "世界" "公司的卡车横到跳板前。警卫从车斗下来，棍子夹在腋下。")
    (line "弗兰克" "拿到手的先送走。其余人退到仓门里面。")
    (line "货运代理" "现在把部件交出来。"))
  (finish! 'fail "警卫进场"
    (string-append
      "封锁被强行清开。此前已经到账的 "
      (number->string (payment-clk 'current))
      " 格付款仍由工人带走，其余欠款没有着落。")))

(define-rule "扣船全额到账"
  (lambda () (and (not finished?) (payment-clk 'full?) (not (guard-clk 'full?))))
  (lambda () (finish-full!)))

;; 后注册的规则先运行。同一手同时填满两根钟时，警卫已经进场，不能再算平稳全额。
(define-rule "扣船警卫进场"
  (lambda () (and (not finished?) (guard-clk 'full?)))
  (lambda () (finish-lost!)))

(define (on-encounter-enter)
  (play-dialogue!
    (line "世界" "旧货船吐着白汽停在泊位。跳板已经被工人占住，装货吊杆全停在半空。")
    (line "货运代理" "船主付过承包人。承包人跑了，不是我们的责任。")
    (line "弗兰克" "船修好了。可工钱到账以前，它不会开。")
    (line "世界" "弗兰克从大衣口袋里露出一截包着油布的关键部件，又收了回去。封锁早在你来以前就开始了。")
    (cond
      ((equal? repair-result "按时修好")
       (line "弗兰克" "你下过最后那班。抢修记录在工具棚，代理的签字也在。"))
      ((equal? repair-result "勉强修好")
       (line "弗兰克" "你下过舱。记录不全，但代理见过你在里面。"))
      (else
       (line "弗兰克" "你没下过舱。先看清他们欠的是谁的钱。")))))

(define (node-record)
  (node "摆出抢修记录"
    :subtitle (if (equal? repair-result "按时修好")
                  "见识；你记得每一班、每一次换人和代理催过的时刻。只用一次，不额外激化局面"
                  "见识；记录不完整，但足以证明代理一直知道谁在替这条船干活。只用一次")
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome "代理挑出一处空白"
        (lambda () (set! record-used? #t)))
      (outcome "班次和签收对上了"
        (lambda ()
          (set! record-used? #t)
          (payment+ (if (equal? repair-result "按时修好") 2 1))))
      (outcome "他的签字就在末页"
        (lambda ()
          (set! record-used? #t)
          (payment+ (if (equal? repair-result "按时修好") 3 2)))))))

(define (node-talk-duty)
  (node "谈清付款责任"
    :subtitle "不替公司制造清场的借口"
    :tags (list "低风险")
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome "代理又把承包人念了一遍" (lambda () #f))
      (outcome "他承认船方验收过抢修" (lambda () (payment+ 1)))
      (outcome "他肯把一部分写进付款单" (lambda () (payment+ 2))))))

(define (node-hold-gangway)
  (node "守住跳板"
    :subtitle "公开施压。装货的人会停手，警哨也会响"
    :tags (list "高风险")
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "装卸领班吹响了警哨" (lambda () (guards+ 2)))
      (outcome "装货的人全停了手" (lambda () (payment+ 1) (guards+ 1)))
      (outcome "连拖船水手也离开缆桩" (lambda () (payment+ 2) (guards+ 1))))))

(define (node-unregistered-cargo)
  (node "指出未登记货物"
    :subtitle "货舱里有一批没有出现在舱单上的木箱"
    :tags (list "高风险" "只用一次")
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome "箱号在阴影里没看全"
        (lambda () (set! cargo-pointed? #t) (guards+ 2)))
      (outcome "代理不敢让警卫先查货舱"
        (lambda () (set! cargo-pointed? #t) (payment+ 2) (guards+ 2)))
      (outcome "缺掉的舱单页就在他公文包里"
        (lambda () (set! cargo-pointed? #t) (payment+ 3) (guards+ 2))))))

(define (take-cargo! amount)
  (guards+ amount)
  (if (guard-clk 'full?)
      #f
      (begin
        (play-dialogue!
          (line "弗兰克" "二号舱那批箱子。按欠的数搬，不多拿。")
          (line "货运代理" "那是盗窃。")
          (line "弗兰克" "那就把欠款写成工资。你挑一个名字。"))
        (set-global! '扣船-以货抵债 #t)
        (finish! 'success "以货抵债"
          "工人把足够抵偿欠款的货搬进仓门。公司把它记作盗窃，船仍在当天离港。"))))

(define (node-take-cargo)
  (node "搬货抵债"
    :subtitle "成功便以货物结清这场封锁"
    :tags (list "高风险" "非法")
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome "货舱口被船员堵住" (lambda () (guards+ 2)))
      (outcome "按欠款搬够了" (lambda () (take-cargo! 2)))
      (outcome "箱子已经进了仓门" (lambda () (take-cargo! 1))))))

(define (node-accept-partial)
  (instant-action "接受部分付款"
    (lambda ()
      (play-dialogue!
        (line "尼尔" "先把已经答应的数当场付清。船今天走。")
        (line "弗兰克" "出过班的人能拿到。伤着没来的、临时顶过的，还是没有。")
        (line "弗兰克" "我会记得这是你选的数。"))
      (finish! 'success "部分到账"
        "代理支付了出勤名单上的一部分工资。弗兰克交回部件，船在警卫到场前离港。"))))

(define (node-return-part)
  (instant-action "交回关键部件"
    (lambda ()
      (play-dialogue!
        (line "尼尔" "把部件装回去。到这里。")
        (line "弗兰克" "船开了，他们就只剩一张找不到人的欠条。")
        (line "世界" "他看了你一会儿，还是把油布包交给机工。"))
      (finish! 'fail "放船离开"
        "关键部件装了回去。没有发生冲撞，工钱继续拖欠。"))))

(define (scene-nodes)
  (append
    (if (and (not record-used?) (not (equal? repair-result "未介入")))
        (list (node-record)) '())
    (list (node-talk-duty) (node-hold-gangway))
    (if cargo-pointed? '() (list (node-unregistered-cargo)))
    (list (node-take-cargo))
    (if (and (>= (payment-clk 'current) 3) (not (payment-clk 'full?)))
        (list (node-accept-partial)) '())
    (list (node-return-part))))

;; 弗兰克的人每回合都在收紧封锁；他们不是玩家手里的普通单位。
(define-turn-rule "弗兰克维持封锁"
  (lambda () (not finished?))
  (lambda ()
    (set! rounds (+ rounds 1))
    ;; 他能独自把代理逼到“先付一部分”，但全额必须靠玩家选手段继续推进。
    (if (< (payment-clk 'current) 3) (payment+ 1) #f)
    (if (= (modulo rounds 2) 0) (guards+ 1) #f)
    (cond
      ((guard-clk 'full?) (finish-lost!))
      ((payment-clk 'full?) (finish-full!))
      (else #f))))

(define (get-render-data)
  (container "船修好了却不开"
    (append (clock-nodes (payment-clk 'render-data) (guard-clk 'render-data))
      (scene-nodes))))
