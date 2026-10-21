;; 两桌愤怒归零完成；只支付引擎已有回合冷静消耗，不另设失败钟。
(define 东边 (make-clock "东桌愤怒值" 5 'countdown "归零后仍可能被另一桌激怒。" :beneficial-direction 'decrease))
(define 西边 (make-clock "西桌愤怒值" 5 'countdown "两桌同时冷静才结束。" :beneficial-direction 'decrease))
(define 骂人者 (make-clock "叫骂者愤怒值" 2 'countdown "每回合让对方愤怒值加1。" :beneficial-direction 'decrease))
(define 骂人位置 "")
(define 出现次数 0)
(define 回合 1)
(define 已结束? #f)
(define (结束! result)
  (set! 已结束? #t)
  (play-dialogue! (line "世界" (if (eq? result 'success)
    "两桌人重新坐下，各自喝酒，不再隔桌叫骂。" "酒保走过来接手，两桌人仍互相瞪着。")))
  (end-encounter result))
(define (检查!)
  (if (and (东边 'empty?) (西边 'empty?)) (结束! 'success) #f))
(define (on-encounter-collapse) (collapse-result 'fail))
(define (on-encounter-enter)
  (东边 'load! 5) (西边 'load! 5) (骂人者 'load! 2)
  (set! 骂人位置 "") (set! 出现次数 0) (set! 回合 1) (set! 已结束? #f)
  (play-dialogue!
    (line "世界" "东边和西边两桌醉客隔桌叫骂，椅子被踢得直响。")
    (line "尼尔" "都先坐下。有话一个一个说。")))
(define (劝! clock n) (clock 'advance! (- 0 n)) (检查!))
(define (桌子 id clock anchor)
  (node id :anchor anchor :clocks (list (clock 'render-data))
    :children (list
      (clock-node (string-append id "：愤怒") (clock 'render-data))
      (node (string-append id "：劝") :name "劝他们" :anchor anchor :requires (list (req-die))
        :disabled (clock 'empty?)
        :subtitle (if (clock 'empty?) "已经冷静下来" "好降2，中降1；坏冷静-1")
        :resolve (roll 'social
          (outcome (lambda () (spend-composure! 1)))
          (outcome (lambda () (劝! clock 1)))
          (outcome (lambda () (劝! clock 2)))))
      (node (string-append id "：威胁") :name "威胁他们" :anchor anchor :requires (list (req-die))
        :disabled (clock 'empty?)
        :subtitle (if (clock 'empty?) "已经冷静下来" "好降2，中降1；坏冷静-1")
        :resolve (roll 'violence
          (outcome (lambda () (spend-composure! 1)))
          (outcome (lambda () (劝! clock 1)))
          (outcome (lambda () (劝! clock 2))))))))
(define (压住! n)
  (骂人者 'advance! (- 0 n))
  (if (骂人者 'empty?)
    (begin (play-dialogue! (line "世界" "同桌的人拉他坐下。他终于不再隔桌骂人。"))
      (set! 骂人位置 "")) #f))
(define (麻烦节点)
  (let ((anchor (if (equal? 骂人位置 "东边") "老街酒馆-工作" "老街酒馆")))
  (node "破口大骂的人" :anchor anchor
    :subtitle (string-append 骂人位置 "桌：醉得脸红，攥着酒杯指向对桌")
    :clocks (list (骂人者 'render-data))
    :children (list
      (clock-node "骂人者：愤怒" (骂人者 'render-data))
      (at-anchor anchor
        (note-node "骂人者：回合规则" "回合结束"
          (string-append "骂向" (if (equal? 骂人位置 "东边") "西边" "东边") "这桌，令其愤怒+1")))
      (node "羞辱他" :anchor anchor :requires (list (req-die)) :subtitle "好降2，中降1；坏冷静-1"
        :resolve (roll 'social
          (outcome (lambda () (spend-composure! 1)))
          (outcome (lambda () (压住! 1))) (outcome (lambda () (压住! 2)))))
      (node "拦住他" :anchor anchor :requires (list (req-die)) :subtitle "好降2，中降1；坏冷静-1"
        :resolve (roll 'violence
          (outcome (lambda () (spend-composure! 1)))
          (outcome (lambda () (压住! 1))) (outcome (lambda () (压住! 2)))))))))
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
    ;; 一次叫骂及其后果属于同一因果批，客户端落地后一起呈现。
    ((if (equal? 骂人位置 "东边") 西边 东边) 'tick!)
    (play-bubble!
      (line "破口大骂的人"
        (random-choice (list "你再说一遍试试！" "别装没听见，我说的就是你！" "有胆子就站起来说！"))
        :at (if (equal? 骂人位置 "东边") "老街酒馆-工作" "老街酒馆")))))
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
            (play-dialogue! (line "世界" (string-append 骂人位置 "这桌一个醉客踢开椅子，脸涨得通红，指着对桌叫骂。")))
            (focus-node! "醉客互相叫骂")
            (let ((speaker (if (equal? 骂人位置 "东边") "东桌醉客" "西桌醉客"))
                  (other (if (equal? 骂人位置 "东边") "西桌醉客" "东桌醉客"))
                  (anchor (if (equal? 骂人位置 "东边") "老街酒馆-工作" "老街酒馆"))
                  (other-anchor (if (equal? 骂人位置 "东边") "老街酒馆" "老街酒馆-工作")))
              (play-bubble!
                (line speaker "你刚才骂谁呢？" :at anchor)
                (line other "说的就是你，怎么着？" :at other-anchor))))) #f))))
(define (get-render-data)
  (container "醉客互相叫骂"
    (append (list (note-node "对骂：交锋目标" "目标" "让两桌人都冷静下来")
                  (桌子 "东边这桌" 东边 "老街酒馆-工作")
                  (桌子 "西边这桌" 西边 "老街酒馆"))
      (if (equal? 骂人位置 "") '() (list (麻烦节点))))))
