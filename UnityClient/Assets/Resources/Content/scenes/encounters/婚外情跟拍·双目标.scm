;; 婚外情取证：寻找 → 照片/身份并行。前期暂不启用怀疑危机。
;; 回传成果档位与实际进度；城市负责胶卷、身份消息及报酬。无额外时间期限。
(define finished? #f)
(define meeting? #f)
;; 暂时移除额外危机；保留对象警觉及满格暴露，恢复时只改此内容开关。
(define attention-crisis-enabled? #f)
(define crisis-called? #f)
(define crisis? #f)
(define finding (make-clock "找到丈夫" 8 'gauge "满8发现会面；敏锐辨认或见识判断去向。" :beneficial-direction 'increase))
(define photos (make-clock "可靠照片" 8 'gauge "满8取得照片并领基础报酬；查清身份可提高报酬。" :beneficial-direction 'increase))
(define identity (make-clock "女子身份" 12 'gauge "4获姓氏、8获工作地点、12核实本人；两种办法用不同技能。" :beneficial-direction 'increase))
(define alertness (make-clock "对象警觉" 8 'pressure "满8暴露；拍摄和打听的失手仍会增加警觉。" :beneficial-direction 'decrease))
(define disguise (make-clock "摆脱注意" 4 'gauge "好2中1；满4危机解除，警觉减2。未解除每回合警觉加1。" :beneficial-direction 'increase))
(define (result-tier)
  (cond ((and (photos 'full?) (identity 'full?)) '照片与身份)
        ((photos 'full?) '照片) (else '无成果)))
(define (finish! reason)
  (if finished? (error "双目标跟拍：重复结算") #t)
  (set! finished? #t)
  (spotlight! "委托取证结束"
    (string-append (if (equal? reason '暴露) "他发现了你。" "你收起相机离开。")
      (cond ((equal? (result-tier) '照片与身份) "照片和身份均已核实，可结算最高档报酬。")
            ((equal? (result-tier) '照片) "可靠照片已取得，可结算基础报酬。")
            (else "没有完成取证，无法领取报酬。"))))
  (end-encounter (list reason (result-tier) (photos 'current) (identity 'current))))
(define (check!)
  ;; 警觉填满优先结算；既有成果保留。双目标均满则无需多一次离场点击。
  (cond ((alertness 'full?) (finish! '暴露))
        ((and (photos 'full?) (identity 'full?)) (finish! '完成)) (else #f)))
(define (identity-progress! n)
  (let ((before (identity 'current)))
    (identity 'advance! n)
    (if (and (< before 4) (>= (identity 'current) 4))
      (play-bubble!
        (line "服务员" "哈珀小姐，还是老样子？" :at "双目标跟拍-侍者")
        (line "女子" "嗯，谢谢。" :at "双目标跟拍-餐桌")) #f)
    (if (and (< before 8) (>= (identity 'current) 8))
      (play-bubble!
        (line "丈夫" "今天又这么晚？梅森百货还没下班？" :at "双目标跟拍-丈夫座位")
        (line "女子" "盘货，拖了一会儿。" :at "双目标跟拍-餐桌")) #f)
    (if (and (< before 12) (identity 'full?))
      (play-bubble!
        (line "停车侍者" "哈珀小姐的车到了。" :at "双目标跟拍-侍者")
        (line "另一名侍者" "伊芙琳·哈珀？梅森百货那位？" :at "双目标跟拍-侍者")
        (line "停车侍者" "对，她常来。" :at "双目标跟拍-侍者")) #f)))
(define (progress! goal n)
  (cond ((eq? goal '找人)
         (finding 'advance! n)
         (if (and (finding 'full?) (not meeting?))
           (begin (set! meeting? #t)
             (play-bubble!
               (line "丈夫" "路上耽搁了一会儿。" :at "双目标跟拍-丈夫座位")
               (line "女子" "没关系，我也才刚到一会儿。" "" 2.8 :at "双目标跟拍-餐桌")
               (line "尼尔" "看来就是他们了" :at "双目标跟拍-观察位"))
             (spotlight! "会面开始"
               "拍到可靠照片才能领取报酬；查清女子身份，还能拿到更多。")) #f))
        ((eq? goal '照片) (photos 'advance! n))
        ((eq? goal '身份) (identity-progress! n))
        ((eq? goal '掩饰)
         (disguise 'advance! n)
         (if (disguise 'full?)
           (begin (set! crisis? #f) (alertness 'advance! -2)
             (play-bubble!
               (line "丈夫" "看来是我想多了，不过刚才那个男的是有点奇怪。"
                 :at "双目标跟拍-丈夫座位"))) #f))
        (else (error "双目标跟拍：未知目标"))))
(define (resolve! goal risky? grade)
  (cond ((eq? grade '坏)
         (spend-composure! (if risky? 2 1))
         (alertness 'advance! (if risky? 2 1)))
        ((eq? grade '中)
         (progress! goal (if risky? 2 1))
         (if risky? (alertness 'advance! 1) #f))
        (else (progress! goal (if risky? 3 2))))
  (if (and crisis? (not (eq? goal '掩饰))) (alertness 'advance! 1) #f)
  (check!))
(define (action-anchor title goal risky?)
  (cond ((eq? goal '找人) "双目标跟拍-观察位")
        ((eq? goal '照片) (if risky? "双目标跟拍-窗外" "双目标跟拍-观察位"))
        ((eq? goal '身份) (if risky? "双目标跟拍-侍者" "双目标跟拍-餐桌"))
        ((eq? goal '掩饰) (if (equal? title "假装等人") "双目标跟拍-门房" "双目标跟拍-报摊"))
        (else (error "双目标跟拍：行动缺少空间落点"))))
(define (action-node title subtitle skill goal risky? anchored?)
  (let ((node-data
          (node title :subtitle subtitle :tags (list (if risky? "高风险" "低风险"))
            :requires (list (req-die))
            :resolve (roll skill
              (outcome (lambda () (resolve! goal risky? '坏)))
              (outcome (lambda () (resolve! goal risky? '中)))
              (outcome (lambda () (resolve! goal risky? '好)))))))
    (if anchored?
        (at-anchor (action-anchor title goal risky?) node-data)
        node-data)))
(define-opponent-rule "男人继续查看"
  (lambda () (and (not finished?) crisis?))
  (lambda () (alertness 'advance! 1) (check!)))
(define-opponent-rule "男人起疑"
  (lambda () (and attention-crisis-enabled? (not finished?) (not crisis-called?) (>= (alertness 'current) 4)))
  (lambda ()
    (set! crisis-called? #t) (set! crisis? #t)
    (play-dialogue!
      (line "世界" "男人离开座位，走到门口，朝你这边看。")
      (line "世界" "他还没确定。掩饰能让他回去，继续取证会增加警觉。"))))
(define (identity-nodes)
  (let ((stage (cond ((< (identity 'current) 4) 0) ((< (identity 'current) 8) 1) (else 2))))
    (list
      (action-node
        (cond ((= stage 0) "留意称呼") ((= stage 1) "留意随身物") (else "核对去向"))
        (cond ((= stage 0) "留意服务员怎样称呼她") ((= stage 1) "从她随身纸袋辨认工作地点")
              (else "依据姓氏和工作地点，核对接送与去向"))
        (if (= stage 0) 'sharpness 'knowledge) '身份 #f #t)
      (action-node
        (cond ((= stage 0) "打听熟客") ((= stage 1) "询问工作") (else "询问侍者"))
        "靠近向员工打听；进展快，但易引起注意"
        'social '身份 #t #t))))
(define (photo-action-nodes)
  (list
    (action-node "隔街拍摄" "隔街取景；好2、中1，坏扣冷静1和警觉1" 'sharpness '照片 #f #t)
    (action-node "靠窗取景" "穿过人流稳住相机；好3、中2并增加警觉1" 'violence '照片 #t #t)))
(define (photo-container)
  (node "照片取证"
    :anchor "双目标跟拍-观察位"
    :children (photo-action-nodes)))
(define (identity-container)
  (node "调查身份"
    :anchor "双目标跟拍-侍者"
    :children (identity-nodes)))
(define (on-encounter-enter)
  (play-dialogue!
    (line "尼尔" "就是这里。她说他晚上会来。")
    (line "尼尔" "我得先在人群里找到他。")
    (line "尼尔" "如果真是约会，他大概不会挑个难看的座位。")))
(define (on-encounter-collapse)
  (collapse-result (list '倒下 (result-tier) (photos 'current) (identity 'current))))
(define (get-render-data)
  (node "饭店秘闻"
    :anchor (if (and meeting? (not crisis?)) "双目标跟拍-餐桌" "双目标跟拍")
    :children (append
      (clock-nodes (alertness 'render-data))
      (list (at-anchor
        (cond (crisis? "双目标跟拍-丈夫门口") (meeting? "双目标跟拍-丈夫座位")
              (else "双目标跟拍-找人"))
        (note-node "标注：取证场面" "此刻的场面"
        (cond (crisis? "男人正在门口查看你；继续调查每次额外警觉1，回合末也加1。")
              (meeting? "两人在窗边用餐。可靠照片可领基础报酬；再核实身份能提高报酬。")
              (else "丈夫还在人群中。先辨认面孔或推断去向。")))))
      (if meeting?
        (append
          (list
            (at-anchor "双目标跟拍-观察位"
              (clock-node "标注：可靠照片" (photos 'render-data)))
            (at-anchor "双目标跟拍-侍者"
              (clock-node "标注：女子身份" (identity 'render-data))))
          (if (photos 'full?) '() (list (photo-container)))
          (if (identity 'full?) '() (list (identity-container)))
          (if (or (photos 'full?) (identity 'full?))
            (list (instant-action "收手离开" (lambda () (finish! '收手)))) '()))
        (append (clock-nodes (finding 'render-data))
          (list (action-node "人群辨认" "根据照片寻找面孔；好2、中1" 'sharpness '找人 #f #t)
                (action-node "判断去向" "提前靠近入口核对；好3、中2并增加警觉1" 'knowledge '找人 #t #t))))
      (if crisis?
        (append (clock-nodes (disguise 'render-data))
          (list (action-node "假装等人" "与门房攀谈；好2、中1，坏扣冷静1和警觉1" 'social '掩饰 #f #t)
                (action-node "借街景遮掩" "收起相机退到报摊；好2、中1" 'knowledge '掩饰 #f #t))) '()))))
