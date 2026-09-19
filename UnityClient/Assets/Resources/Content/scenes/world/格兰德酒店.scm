;; scenes/world/格兰德酒店.scm - 第二章开放的上城常驻枢纽
;;
;; 它不是另一间更贵的酒馆。大厅是上城的公共客厅：外地商人、经纪人、
;; 记者和公司职员在这里等人、介绍工作、说那些不会留在办公室纸面上的话。
;; 宴会厅、客房与服务层是事件空间，不作为常驻生活卡摆出来。
;; 落点（city-box/prefabs/src/格兰德酒店.py）：格兰德酒店＝正门雨棚，赴宴从这儿进；
;; -大厅＝门厅后面的公共客厅；-礼宾台＝门厅一侧，留口信的地方。
(define grand-hotel
  (let ()
    ;; 这里的工作不是搬运，而是替客人把尴尬的事情不动声色地处理掉。
    ;; 曾经给 45/28/扣冷静：一班抵一期房租，严格压过城里所有别的工，玩家从此只来这儿。
    ;; 上城的钱不是白拿的：办砸了客人不只不付，还要你赔——挑剔的人翻脸就是账单。
    ;; 好结果比码头夜班高一点、不伤身；坏结果是全城唯一会倒扣钱的工。
    (define 赔付 15)
    (define (node-fix-problem)
      (工作 "替客人解围" '低 'social
        (outcome (lambda () (add-item! "金钱" 30)))
        (outcome (lambda () (add-item! "金钱" 18)))
        (outcome (lambda ()
            (remove-item! "金钱" (min 赔付 (item-count "金钱")))
            (spend-composure! 2)))
        "大厅里的委托：找东西、送口信、让人离开；办砸了要赔"
        :anchor "格兰德酒店-大厅"))

    ;; 大堂不产情报：情报只在交锋里拿得到（见 报社.scm 卖消息那一段），
    ;; 城里能刷的话它就不稀缺了。消费和恢复留给后续的专门地点。
    (define (children)
      (append (list (node-fix-problem))
              (地点节点 "格兰德酒店")))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (place "格兰德酒店"
                   :children (children)
                   :arrivals (地点入场 "格兰德酒店"))))
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (else #f))))))
