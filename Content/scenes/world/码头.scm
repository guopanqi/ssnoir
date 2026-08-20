;; 码头——普通生计由地点拥有；乔与弗兰克各自拥有个人生活。

(define dock
  (let ()
    ;; 码头内部没有独立空间语义的动作共用 City/Anchor_码头。它们不该落进移动端
    ;; 的 fallback grid；同一锚点上的投射卡由客户端稳定排布。人物等已声明专属
    ;; :anchor 的节点保留自己的落点，不在这里覆盖。
    (define dock-anchor "码头")

    (define (anchor-at-dock node-data)
      (if (member? :anchor node-data)
          node-data
          (append node-data (list :anchor dock-anchor))))

    (define (node-haul)
      (工作 "搬运" "劳工" '高 'violence
        (outcome "扛完一整班"
          (lambda () (add-item! "金钱" 15) (grant-work-relation! "劳工") (joe 'on-haul!)))
        (outcome "勉强做完"
          (lambda () (add-item! "金钱" 8) (spend-composure! 1) (joe 'on-haul!) (joe 'on-haul-neutral!)))
        (outcome "货箱脱手"
          (lambda () (spend-composure! 2) (joe 'on-haul-fail!)))))

    (define (node-sell-contraband-locally)
      (node "把私货散卖给水手"
        :subtitle "保底渠道，价钱很低"
        :requires (list (req-item "私货" 1))
        :resolve (instant
          (outcome "私货脱手"
            (lambda () (add-item! "金钱" 12))))))

    (define (children)
      (map anchor-at-dock
        (append
          (list (node-haul))
          (three-letters 'nodes-at "码头")
          (eddie 'nodes-at "码头")
          (joe 'dock-nodes)
          (frank 'dock-nodes)
          (dock-collapse 'dock-nodes)
          (if (> (item-count "私货") 0) (list (node-sell-contraband-locally)) '()))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data) (list (container "码头" (children))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (else #f))))))
