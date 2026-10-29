;; 两桌愤怒归零完成；只支付引擎已有回合冷静消耗，不另设失败钟。
(define 东边 (make-clock "东桌愤怒值" 5 'countdown "归零后仍可能被另一桌激怒。" :beneficial-direction 'decrease))
(define 西边 (make-clock "西桌愤怒值" 5 'countdown "两桌同时冷静才结束。" :beneficial-direction 'decrease))
(define 骂人者 (make-clock "叫骂者愤怒值" 2 'countdown "每回合让对方愤怒值加1。" :beneficial-direction 'decrease))
(define 骂人位置 "")
(define 出现次数 0)
(define 回合 1)
(define 已结束? #f)
;; 视觉状态直接取自两桌Clock和当前叫骂者，不额外记录一份动画状态。
(define (位置的钟 位置)
  (cond ((equal? 位置 "东边") 东边) ((equal? 位置 "西边") 西边)
        (else (error "醉客互相叫骂：未知桌边位置"))))
(define (醉客动作! 位置 状态)
  (let ((边 (cond ((equal? 位置 "东边") "东") ((equal? 位置 "西边") "西")
                   (else (error "醉客互相叫骂：未知动作位置")))))
    (play-motion! (string-append "醉客互相叫骂/" 边 "桌醉客") 状态
      (string-append "醉客互相叫骂-" 边 "桌"))))
(define (坐回去! 位置)
  (醉客动作! 位置 (if ((位置的钟 位置) 'empty?) "平静" "吵嚷")))
(define (结束! result)
  (set! 已结束? #t)
  (if (and (eq? result 'success) (not (equal? 骂人位置 "")))
      (醉客动作! 骂人位置 "平静") #f)
  (play-dialogue! (line "世界" (if (eq? result 'success)
    "两桌人重新坐下，各自喝酒，不再隔桌叫骂。" "酒保走过来接手，两桌人仍互相瞪着。")))
  (end-scenario result))
(define (检查!)
  (if (and (东边 'empty?) (西边 'empty?)) (结束! 'success) #f))
(define (on-scenario-collapse) (collapse-result 'fail))
(define (on-scenario-enter)
  (东边 'load! 5) (西边 'load! 5) (骂人者 'load! 2)
  (set! 骂人位置 "") (set! 出现次数 0) (set! 回合 1) (set! 已结束? #f)
  ;; 每次正常进入都从当前局面恢复两桌姿态，包括同一舞台再次进入。
  (醉客动作! "东边" "吵嚷")
  (醉客动作! "西边" "吵嚷")
  (play-dialogue!
    (line "世界" "东边和西边两桌醉客隔桌叫骂，坐着也不肯收下指人的手。")
    (line "尼尔" "都先坐下。有话一个一个说。")))
(define (劝! clock n 位置)
  (clock 'advance! (- 0 n))
  (if (and (clock 'empty?) (not (equal? 骂人位置 位置)))
      (醉客动作! 位置 "平静") #f)
  (检查!))
(define (桌子 id clock anchor 位置)
  (node id :anchor anchor :clocks (list (clock 'render-data))
    :children (list
      (clock-node (string-append id "：愤怒") (clock 'render-data))
      (node (string-append id "：劝") :name "劝他们" :anchor anchor :requires (list (req-die))
        :risk '稳妥 :progress-target (list 'clock (if (equal? id "东边这桌") "东桌愤怒值" "西桌愤怒值"))
        :disabled (clock 'empty?)
        :subtitle (if (clock 'empty?) "已经冷静下来" "好降2，中降1；坏无进展")
        :resolve (roll 'social
          (outcome (lambda () #f))
          (outcome (lambda () (劝! clock 1 位置)))
          (outcome (lambda () (劝! clock 2 位置)))))
      (node (string-append id "：威胁") :name "喝令散开" :anchor anchor :requires (list (req-die))
        :risk '冒险 :risk-target (list 'composure "player")
        :progress-target (list 'clock (if (equal? id "东边这桌") "东桌愤怒值" "西桌愤怒值"))
        :disabled (clock 'empty?)
        :subtitle (if (clock 'empty?) "已经冷静下来" "好降3，中降1；坏冷静-2")
        :resolve (roll 'violence
          (outcome (lambda () (spend-composure! 2)))
          (outcome (lambda () (劝! clock 1 位置)))
          (outcome (lambda () (劝! clock 3 位置))))))))
(define (压住! n)
  (骂人者 'advance! (- 0 n))
  (if (骂人者 'empty?)
    (begin (坐回去! 骂人位置)
      (play-dialogue! (line "世界" "他收下指人的手，坐回椅子里，不再扯着嗓子叫骂。"))
      (set! 骂人位置 "")) #f))
(define (麻烦节点)
  (let ((anchor (if (equal? 骂人位置 "东边") "醉客互相叫骂-东桌" "醉客互相叫骂-西桌")))
  (node "破口大骂的人" :anchor anchor
    :subtitle (string-append 骂人位置 "桌：已经站起身，抬手指向对桌")
    :clocks (list (骂人者 'render-data))
    :children (list
      (clock-node "骂人者：愤怒" (骂人者 'render-data))
      (at-anchor anchor
        (note-node "骂人者：回合规则" "回合结束"
          (string-append "骂向" (if (equal? 骂人位置 "东边") "西边" "东边") "这桌，令其愤怒+1")))
      (node "羞辱他" :anchor anchor :requires (list (req-die))
        :risk '冒险 :risk-target (list 'composure "player")
        :progress-target (list 'clock "叫骂者愤怒值")
        :subtitle "好降2，中降1；坏冷静-1"
        :resolve (roll 'social
          (outcome (lambda () (spend-composure! 1)))
          (outcome (lambda () (压住! 1))) (outcome (lambda () (压住! 2)))))
      (node "拦住他" :anchor anchor :requires (list (req-die))
        :risk '危险 :risk-target (list 'composure "player")
        :progress-target (list 'clock "叫骂者愤怒值")
        :subtitle "好降2，中降1且冷静-1；坏冷静-2"
        :resolve (roll 'violence
          (outcome (lambda () (spend-composure! 2)))
          (outcome (lambda () (spend-composure! 1) (压住! 1)))
          (outcome (lambda () (压住! 2)))))))))
(define (选择起身位置)
  (cond
    ((and (not (东边 'empty?)) (not (西边 'empty?)))
      (random-choice (list "东边" "西边")))
    ((not (东边 'empty?)) "东边")
    ((not (西边 'empty?)) "西边")
    (else "")))
(define-opponent-rule "隔桌叫骂"
  (lambda () (and (not 已结束?) (not (equal? 骂人位置 ""))))
  (lambda ()
    ;; 平静的一桌被重新激怒时补上姿态变化，再接叫骂气泡。
    (let* ((对桌位置 (if (equal? 骂人位置 "东边") "西边" "东边"))
           (clock (位置的钟 对桌位置))
           (原本平静? (clock 'empty?)))
      (clock 'tick!)
      (if (and 原本平静? (not (clock 'empty?)))
          (醉客动作! 对桌位置 "吵嚷") #f))
    (play-bubble!
      (line "破口大骂的人"
        (random-choice (list "你再说一遍试试！" "别装没听见，我说的就是你！" "有胆子就站起来说！"))
        :at (if (equal? 骂人位置 "东边") "醉客互相叫骂-东桌" "醉客互相叫骂-西桌")))))
;; 先处理旧麻烦，再生成新麻烦，保证新出现者有完整一回合的处理窗口。
;; 第一位在第二回合必定起身；压住后，第二位每个空档回合有 1/3 概率起身。
(define-opponent-rule "醉客起身"
  (lambda () (not 已结束?))
  (lambda ()
    (set! 回合 (+ 回合 1))
    (if (and (equal? 骂人位置 "") (< 出现次数 2)
             (or (= 回合 2) (random-choice (list #t #f #f))))
      (let ((位置 (选择起身位置)))
        (if (equal? 位置 "") #f
          (begin
            (set! 骂人位置 位置)
            (set! 出现次数 (+ 出现次数 1)) (骂人者 'load! 2)
            (醉客动作! 骂人位置 "起身")
            (play-dialogue! (line "世界" (string-append 骂人位置 "这桌一个醉客站起来，朝对桌挥着手叫骂。")))
            (focus-node! "醉客互相叫骂")
            (let ((speaker (if (equal? 骂人位置 "东边") "东桌醉客" "西桌醉客"))
                  (other (if (equal? 骂人位置 "东边") "西桌醉客" "东桌醉客"))
                  (anchor (if (equal? 骂人位置 "东边") "醉客互相叫骂-东桌" "醉客互相叫骂-西桌"))
                  (other-anchor (if (equal? 骂人位置 "东边") "醉客互相叫骂-西桌" "醉客互相叫骂-东桌")))
              (play-bubble!
                (line speaker "你刚才骂谁呢？" :at anchor)
                (line other "说的就是你，怎么着？" :at other-anchor))))) #f))))
(define (get-render-data)
  ;; 根名 = Camera_醉客互相叫骂 / Anchor_醉客互相叫骂：独立 Stage 经 老街酒馆 的门进来。
  (node "醉客互相叫骂" :anchor "醉客互相叫骂"
    :children (append (list (note-node "对骂：Scenario目标" "目标" "让两桌人都冷静下来")
                  (桌子 "东边这桌" 东边 "醉客互相叫骂-东桌" "东边")
                  (桌子 "西边这桌" 西边 "醉客互相叫骂-西桌" "西边"))
      (if (equal? 骂人位置 "") '() (list (麻烦节点))))))
