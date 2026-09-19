;; scenes/world/报社.scm - 报社（Phase B 开放）
;;
;; 「故事是怎么被写出来的」这件事的现场。第二章的调查从这里往下走。
;; 落点（city-box/prefabs/src/报社.py）：报社＝厂房装卸口，下地下室从这儿走；
;; -编辑部＝方楼中层，记者和登记簿；-旧报库＝厂房里印过的报纸堆着的那头。

(define newsroom
  (let ()
    ;; ── 情报的出口 ──────────────────────────────────
    ;; 情报只在交锋里拿得到（尾随取件人、废稿间），城里不生产——能刷就不稀缺。
    ;; 这里是它唯一的去处：卖给编辑部，一份换一笔钱。不占骰：交易不是劳作。
    ;; 以后它可能不只换钱——攒成一根钟，换一份活或一种过法；先让它有地方花。
    (define 消息价 25)

    (define (node-sell-tip)
      (node "卖消息"
        :anchor "报社-编辑部"
        :subtitle (string-append "一份情报换 " (number->string 消息价) " 金钱。编辑部不问来处")
        :requires (list (req-item "情报" 1))
        :resolve (instant
          (outcome (lambda () (add-item! "金钱" 消息价))))))

    (define (children)
      (append (list (node-sell-tip)) (地点节点 "报社")))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "报社" :children (children) :arrivals (地点入场 "报社"))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (else #f))))))
