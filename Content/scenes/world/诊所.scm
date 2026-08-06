;; scenes/world/诊所.scm - 诊所（官僚）
;; 服务点：买药品（带回家用）/ 正经治疗（占一颗骰，按伤势档位收费）。用药动作在家里。
;; 治疗是把伤势往下压的最快一条路，也是钱在这个游戏里最硬的去处：重伤那几天，
;; 每天固定一颗骰加一笔诊金，就是"欠账留到明天"的具体形状。

(define clinic
  (let ()

    ;; 官僚敌视时诊所涨价（约 +50%），但绝不拒诊——治病是唯一手段，没有替代路径，
    ;; 关系再差也不能把它彻底禁掉，最多让它变贵。
    (define (hostile-markup base)
      (if (equal? (relation-band "官僚") '敌视)
          (+ base (quotient base 2))
          base))

    (define (node-buy-medicine)
      (node "买药品"
        :subtitle (if (equal? (relation-band "官僚") '敌视)
                      "医生知道你现在不受待见，这份药比平时贵一截"
                      "")
        :requires (list (req-item "金钱" (hostile-markup 25)))
        :resolve (instant
          (outcome "抓了一份药"
            (lambda () (add-item! "药品" 1))))))

    ;; 正经治疗：一颗骰 + 诊金，压 2 点伤势。重伤诊金翻倍——伤越重，还得越贵。
    ;; 压 2 而不是 3：轻伤档上限就是 3 点，−3 意味着 15 金一次永远清空轻伤，
    ;; 伤势根本攒不起来；而且 −3/15 金对上药品的 −2/25 金，等于把药品彻底压死。
    ;; 现在两者效果相同、货币不同——「省一颗骰多花 10 金」，是一句读得懂的话。
    (define (treatment-fee)
      (hostile-markup (if (equal? (injury-band) '重伤) 30 15)))

    (define (node-treatment)
      (node "看医生"
        :subtitle (cond
                    ((equal? (injury-band) '完好) "身上没有需要处理的伤")
                    ((equal? (relation-band "官僚") '敌视)
                     "医生知道你现在不受待见，这份诊金比平时贵一截；压 2 点伤势")
                    (#t "投入一颗行动骰，压 2 点伤势"))
        :disabled (equal? (injury-band) '完好)
        :requires (list (req-die) (req-item "金钱" (treatment-fee)))
        :resolve (instant
          (outcome "医生给你处理了伤口"
            (lambda () (heal-injury! 2))))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (container "诊所"
                   (list (node-buy-medicine) (node-treatment)))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (#t #f))))))
