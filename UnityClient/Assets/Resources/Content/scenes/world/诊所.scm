;; scenes/world/诊所.scm - 诊所
;; 看医生：20美元和一枚骰子，稳定恢复2点 Health；药品更贵但不花骰子。

(define clinic
  (let ()
    ;; 开场地图上没有诊所。一个没受伤的人不会去记医院在哪儿——它挂在那儿只是
    ;; 又一个「以后大概有用」的图标。第一次带伤那一刻它才出现，出现的同时
    ;; 玩家正好需要它，于是它是答案，不是背景。
    ;; 一旦露过面就不再收回：那条街你已经走过了。
    (define discovered? #f)
    (define introduction-seen? #f)

    (define (visible?)
      (if discovered?
          #t
          ;; 倒下送医会先把伤势清零，再留下永久疤痕并强制切到诊所。
          ;; 只查 injury-band 会使新快照重新把诊所藏掉，令强制落点无路可进。
          (if (and (equal? (injury-band) '完好) (= (scar-count) 0))
              #f
              (begin (set! discovered? #t) #t))))

    ;; 诊所对谁都是一个价。治病是唯一手段、没有替代路径，价格再挂上任何一条声誉，
    ;; 都等于把已经删掉的那条全城关系换个名字装回来。

    (define treatment-fee 20)

    (define (node-treatment)
      (node "看医生"
        :anchor "诊所-服务"
        :subtitle (if (equal? (injury-band) '完好)
                      "身上没有需要处理的伤"
                      "支付20美元及一枚骰，恢复2点 健康")
        :disabled (equal? (injury-band) '完好)
        :requires (list (req-item "金钱" treatment-fee) (req-die))
        :resolve (instant
          (outcome (lambda () (heal-injury! 2))))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "诊所"
                   :children (list (node-treatment)))))
          ((equal? msg 'arrivals-at)
           (if (and (visible?) (not introduction-seen?) (member? (cadr args) '("世界" "诊所")))
             (list (arrival "第一次受伤后得知诊所"
               (lambda () (set! introduction-seen? #t)
                 (play-dialogue! (line "世界" "尼尔按住伤处，停下来缓了口气。路人朝街角的门牌指了指。")
                   (line "世界" "那边有家诊所。看医生要二十美元，还得留出一点时间。"))))) '()))
          ((equal? msg 'visible?) (visible?))
          ((equal? msg 'save) (list (list "discovered" (if discovered? 1 0)) (list "introduction-seen" introduction-seen?)))
          ((equal? msg 'load!)
           (set! discovered? (= (assoc-get (cadr args) "discovered" 0) 1))
           (set! introduction-seen? (assoc-get (cadr args) "introduction-seen" discovered?)))
          (#t #f))))))
