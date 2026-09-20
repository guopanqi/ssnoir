;; 《别给他们想要的》——首演之后，警察来老街带一个年轻码头工人回去问话。
;;
;; 街上两堆人已经绷到了头：弗兰克的人、年轻人的家里人、越来越多的警察，
;; 路边还站着记者和摄影师。谁先动一下手，第二天报纸上就是「老街暴徒袭击警方」。
;; 弗兰克知道这一点，所以他把自己的人按在原地——但他只按得住这一边。
;; 能同时跟警察说话的，只有你。这一场考的是**分诊**：四颗骰给哪一边、先灭哪一处火。
;;
;; 结构：两根目标钟（老街这边 / 警察那边）都填满就算调停成功；
;; 一根「冲突升级」满格就算失败。升级钟不自己走——推它的是**没被处理的麻烦**：
;; 到了固定回合，人群里会有人喊起来、警察会拔警棍；麻烦没压下去，每回合末升级一格。
;; 坏结果也推一格：你说错一句话，两边都听得见。
;;
;; 没有低风险 / 高风险两档。骰面是明牌，两档只会变成「大骰投高、小骰投低」的排序，
;; 不是决定。真正的决定是这颗骰给谁。
;;
;; 对外契约：回传 'success / 'fail。见 world/第二章/人物事件/别给他们想要的.scm。

(define 老街-max 8)
(define 警方-max 8)
(define 升级-max 5)
(define 麻烦-max 3)

(define 老街-clk
  (make-clock "老街这边" 老街-max 'gauge
    "弗兰克的人和年轻人的家里人。填满＝他们肯退回门里。"))

(define 警方-clk
  (make-clock "警察那边" 警方-max 'gauge
    "带队的警官。填满＝他同意年轻人做完笔录当天回来。"))

(define 升级-clk
  (make-clock "冲突升级" 升级-max 'gauge
    (lambda (current max)
      (cond
        ((>= current 3) "有人已经弯腰捡东西了。摄影师把镜头举了起来。")
        ((> current 0) "两边的距离在缩短。")
        (else "现在还只是对峙。谁也没先动。")))))

;; 两件麻烦按回合出现。每件是一个具体的人，压下去要三格。
(define 喊人-clk
  (make-clock "喊起来的人" 麻烦-max 'gauge
    "他在前排冲警察喊。没人拉住他，每回合末冲突升级一格。"))
(define 警棍-clk
  (make-clock "拔警棍的警察" 麻烦-max 'gauge
    "年轻的那个警察已经把手放在警棍上。每回合末冲突升级一格。"))

(define 喊人-出现回合 2)
(define 警棍-出现回合 3)

(define turn 1)
(define 喊人在? #f)
(define 警棍在? #f)
(define finished? #f)

(define (finish! result title text)
  (if finished? (error "别给他们想要的：交锋已经结算") #t)
  (set! finished? #t)
  (spotlight! title text)
  (end-encounter result))

;; 你倒下了，中间那块地就空了。按失败走：这一场没有第二次。
(define (on-encounter-collapse)
  (collapse-result 'fail))

(define (check!)
  (if finished?
      #f
      (cond
        ((升级-clk 'full?)
         (finish! 'fail "有人先动了手"
           "一个瓶子从人堆后面飞出来。警察推人，有人倒在路边。年轻人被按进车里，摄影师拍到了他要的那张。"))
        ((and (老街-clk 'full?) (警方-clk 'full?))
         (finish! 'success "没人先动手"
           "年轻人跟警察走了，你陪着去做了笔录。天黑前他自己走回老街。摄影师什么也没拍到。"))
        (else #f))))

(define (escalate!)
  (升级-clk 'tick!)
  (check!))

(define (on-encounter-enter)
  (set! turn 1)
  (set! 喊人在? #f)
  (set! 警棍在? #f)
  (set! finished? #f)
  (play-dialogue!
    (line "世界" "两个警察架着一个年轻人往车那边走。他没反抗，脸一直朝着人堆。")
    (line "世界" "弗兰克的人从酒馆出来了。年轻人的母亲从对面出来了。街口又停下一辆警车。")
    (line "世界" "路边有人在装胶卷。")
    (line "弗兰克" "他们就等这个。")
    (line "弗兰克" "我压得住我的人。那边的，我一句话都说不上。")
    (line "尼尔" "那边我来。")))

;; ── 两边 ─────────────────────────────────────────
(define (node-street)
  (node "安抚老街的人"
    :subtitle "站到他们前面，把话说给他们听"
    :clocks (list (老街-clk 'render-data))
    :requires (list (req-die))
    :resolve (roll 'social
      (outcome (lambda () (escalate!)))
      (outcome (lambda () (老街-clk 'tick!) (check!)))
      (outcome (lambda ()
          (老街-clk 'advance! 2)
          (if (老街-clk 'full?)
              (play-banter! (line "世界" "弗兰克抬了抬手。前排的人往门里退了。"))
              #f)
          (check!))))))

(define (node-police)
  (node "跟警官谈"
    :subtitle "笔录的程序，你比他们两边都清楚"
    :clocks (list (警方-clk 'render-data))
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome (lambda () (escalate!)))
      (outcome (lambda () (警方-clk 'tick!) (check!)))
      (outcome (lambda ()
          (警方-clk 'advance! 2)
          (if (警方-clk 'full?)
              (play-banter! (line "世界" "警官朝车那边点了点头。架着人的手松了。"))
              #f)
          (check!))))))

;; ── 麻烦 ─────────────────────────────────────────
(define (node-shouter)
  (node "把喊的人拉回去"
    :subtitle "他嗓门最大，警察的眼睛全在他身上"
    :clocks (list (喊人-clk 'render-data))
    :requires (list (req-die))
    :resolve (roll 'violence
      (outcome (lambda () (escalate!)))
      (outcome (lambda () (喊人-clk 'tick!) (settle-shouter!)))
      (outcome (lambda () (喊人-clk 'advance! 2) (settle-shouter!))))))

(define (settle-shouter!)
  (if (喊人-clk 'full?)
      (begin
        (set! 喊人在? #f)
        (play-banter! (line "世界" "他被人从后面拖进酒馆。门在他身后关上了。")))
      #f)
  (check!))

(define (node-baton)
  (node "挡在警棍前面"
    :subtitle "站到他和人堆中间，让他看着你"
    :clocks (list (警棍-clk 'render-data))
    :requires (list (req-die))
    :resolve (roll 'sharpness
      (outcome (lambda () (spend-composure! 1) (escalate!)))
      (outcome (lambda () (警棍-clk 'tick!) (settle-baton!)))
      (outcome (lambda () (警棍-clk 'advance! 2) (settle-baton!))))))

(define (settle-baton!)
  (if (警棍-clk 'full?)
      (begin
        (set! 警棍在? #f)
        (play-banter! (line "世界" "警官叫了他一声。他退回车边，手插进了口袋。")))
      #f)
  (check!))

;; ── 回合末 ──────────────────────────────────────
;; 麻烦按回合出现，不看你做得好不好——街上的人不是等你准备好才喊的。
;; 没压下去的麻烦每回合推升级一格；两件都在，一回合就是两格。
(define (spawn-shouter!)
  (set! 喊人在? #t)
  (喊人-clk 'reset!)
  (play-banter!
    (line "世界" "前排有人喊起来了。喊的是那个警察的名字，和他家住哪条街。")))

(define (spawn-baton!)
  (set! 警棍在? #t)
  (警棍-clk 'reset!)
  (play-banter!
    (line "世界" "年轻的那个警察把手放到了警棍上。他的眼睛在找人堆里最近的一个。")))

(define-opponent-rule "街上越站越紧"
  (lambda () (not finished?))
  (lambda ()
    (if 喊人在? (升级-clk 'tick!) #f)
    (if 警棍在? (升级-clk 'tick!) #f)
    (check!)
    (set! turn (+ turn 1))
    (if (and (not finished?) (= turn 喊人-出现回合)) (spawn-shouter!) #f)
    (if (and (not finished?) (= turn 警棍-出现回合)) (spawn-baton!) #f)))

(define (get-render-data)
  (container "别给他们想要的"
    (append
      (clock-nodes (升级-clk 'render-data))
      (if 喊人在? (list (node-shouter)) '())
      (if 警棍在? (list (node-baton)) '())
      (if (老街-clk 'full?) '() (list (node-street)))
      (if (警方-clk 'full?) '() (list (node-police))))))
