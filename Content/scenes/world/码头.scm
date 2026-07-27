;; 码头——普通生计由地点拥有；乔与弗兰克各自拥有个人生活。

(define dock
  (let ()
    (define (node-haul)
      (工作 "搬运" "劳工" '高 'violence
        (outcome "扛完一整班" "汗把衬衫贴在背上，工钱倒给得痛快。"
          (lambda () (add-item! "金钱" 15) (grant-work-relation! "劳工") (joe 'on-haul!)))
        (outcome "勉强做完" "工钱拿到了，腰背也像散了架。"
          (lambda () (add-item! "金钱" 8) (spend-composure! 1) (joe 'on-haul!) (joe 'on-haul-neutral!)))
        (outcome "货箱脱手" "货箱摔裂在跳板上，工头把损失和骂声全算在你头上。"
          (lambda () (spend-composure! 2) (joe 'on-haul-fail!)))))

    (define (node-sell-contraband-locally)
      (node "把私货散卖给水手"
        :subtitle "保底渠道，价钱很低"
        :requires (list (req-item "私货" 1))
        :resolve (instant
          (outcome "私货脱手" "水手把货塞进外套，留下十二块钱。"
            (lambda () (add-item! "金钱" 12))))))

    (define (children)
      (append
        (list (node-haul))
        (three-letters 'nodes-at "码头")
        (joe 'dock-nodes)
        (frank 'dock-nodes)
        (if (> (item-count "私货") 0) (list (node-sell-contraband-locally)) '())))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data) (list (container "码头" (children))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (else #f))))))
