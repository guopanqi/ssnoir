;; scenes/world/诊所.scm - 诊所
;; 服务点：买药品（带回家用）/ 正经治疗（占一颗骰，按伤势档位收费）。用药动作在家里。
;; 治疗是把伤势往下压的最快一条路，也是钱在这个游戏里最硬的去处：重伤那几天，
;; 每天固定一颗骰加一笔诊金，就是"欠账留到明天"的具体形状。

(define clinic
  (let ()
    ;; 开场地图上没有诊所。一个没受伤的人不会去记医院在哪儿——它挂在那儿只是
    ;; 又一个「以后大概有用」的图标。第一次带伤那一刻它才出现，出现的同时
    ;; 玩家正好需要它，于是它是答案，不是背景。
    ;; 一旦露过面就不再收回：那条街你已经走过了。
    (define discovered? #f)

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

    (define (node-buy-medicine)
      (node "买药品"
        :anchor "诊所-服务"
        :requires (list (req-item "金钱" 25))
        :resolve (instant
          (outcome (lambda () (add-item! "药品" 1))))))

    ;; 正经治疗：一颗骰 + 20 金，压 2 点伤势。重伤不加价。压 2 而不是 3，
    ;; 避免一次治疗永远清空轻伤。
    ;; 药品 25 金、零骰、只压 1、每天一份：纯花钱的那条故意最弱，
    ;; 伤势要真正下去必须交骰子（医生或养伤），钱只是让那颗骰更值钱。
    ;; 第三条路在住所：养伤 1 骰、0 金、−1（见 home.scm）。三条各自贵在不同的东西上。
    (define treatment-fee 20)

    (define (node-treatment)
      (node "看医生"
        :anchor "诊所-服务"
        :subtitle (if (equal? (injury-band) '完好)
                      "身上没有需要处理的伤"
                      "恢复 2 点伤势")
        :disabled (equal? (injury-band) '完好)
        :requires (list (req-die) (req-item "金钱" treatment-fee))
        :resolve (instant
          (outcome (lambda () (heal-injury! 2))))))

    ;; ── 标注（不可操作）─────────────────────────────────────────
    ;; 这两条是标注不是卡：碰不得、点不动，只告诉你这个地方是什么样子。
    ;; 没有锚点的那条升到画面上方，说的是整个诊所；带锚点的那条挂在诊台旁边，
    ;; 一根线指过去，说的是那一处（服务卡共用 Anchor_诊所-服务）。

    (define (note-waiting-room)
      (note-node "标注：候诊" ""
        "夜里只留一盏灯。挂号窗后面那位从不问伤是怎么来的。"))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "诊所"
                   :children (list (node-buy-medicine) (node-treatment)
                                   (note-waiting-room)))))
          ((equal? msg 'visible?) (visible?))
          ((equal? msg 'save) (list (list "discovered" (if discovered? 1 0))))
          ((equal? msg 'load!)
           (set! discovered? (= (assoc-get (cadr args) "discovered" 0) 1)))
          (#t #f))))))
