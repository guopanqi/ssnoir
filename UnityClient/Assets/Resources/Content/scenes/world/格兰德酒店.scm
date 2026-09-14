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
    (define (node-fix-problem)
      (关系工作 "替客人解围" "商业圈" '低 'social
        (outcome "没留下痕迹"
          (lambda () (add-item! "金钱" 45)))
        (outcome "事情办完了"
          (lambda () (add-item! "金钱" 28)))
        (outcome "话传了出去"
          (lambda () (spend-composure! 2)))
        "大厅里的委托：找东西、送口信、让人离开"
        :anchor "格兰德酒店-大厅"))

    ;; 留意谁在等谁，是这个地点自己的日常玩法；消费和恢复留给后续的专门地点。
    (define (node-watch-lobby)
      (at-anchor "格兰德酒店-大厅"
       (roll-action "留意大厅" (list (req-die)) 'knowledge
        (outcome "只看见人来人往"
          (lambda () (spend-actor-composure! 'player 1)))
        (outcome "记住了几张脸"
          (lambda () (result-note! "知道他们常坐哪儿")))
        (outcome "听懂了一段话"
          (lambda () (add-item! "情报" 1))))))

    (define (children)
      (append (list (node-fix-problem) (node-watch-lobby))
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
