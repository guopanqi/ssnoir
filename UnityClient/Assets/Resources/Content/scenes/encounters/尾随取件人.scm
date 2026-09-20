;; 《尾随取件人》——玩家只知道剧院的消息漏了，不知道取件人的姓名与身份。
;; 每幕结束时，局部 Clock 未填满便失去一格踪迹；踪迹归零或暴露填满都会失败。

(define 路段 0) ; 0 剧院后巷 / 1 商业街 / 2 电车 / 3 格兰德酒店 / 4 报社后门
(define 已结束? #f)
(define 本幕拿过情报? #f)

(define 踪迹
  (make-clock "踪迹" 3 'countdown
    "每幕没有咬住对方，就失去一格；归零时跟丢。"))

(define 暴露
  (make-clock "暴露" 3 'gauge
    "跟得越冒险，越可能被认出来；满格时尾随失败。"))

(define (路段名)
  (cond
    ((= 路段 0) "剧院后巷")
    ((= 路段 1) "商业街")
    ((= 路段 2) "电车")
    ((= 路段 3) "格兰德酒店")
    ((= 路段 4) "报社后门")
    (#t (error "尾随取件人：路段越界"))))

;; 五段各自的空间落点：CityBox 里 尾随取件人 那条街上的五个锚点，镜头随段沿街往前推
(define (路段锚点)
  (cond
    ((= 路段 0) "尾随取件人")
    ((= 路段 1) "尾随取件人-商业街")
    ((= 路段 2) "尾随取件人-电车")
    ((= 路段 3) "尾随取件人-酒店")
    ((= 路段 4) "尾随取件人-报社后门")
    (#t (error "尾随取件人：路段越界"))))

(define (本幕容量)
  (cond
    ((= 路段 0) 5)
    ((= 路段 1) 4)
    ((= 路段 2) 6)
    ((= 路段 3) 4)
    ((= 路段 4) 5)
    (#t (error "尾随取件人：路段越界"))))

;; 五段各自的空间落点：CityBox 里 尾随取件人 那条街上的五个锚点，镜头随段沿街往前推
(define (路段锚点)
  (cond
    ((= 路段 0) "尾随取件人")
    ((= 路段 1) "尾随取件人-商业街")
    ((= 路段 2) "尾随取件人-电车")
    ((= 路段 3) "尾随取件人-酒店")
    ((= 路段 4) "尾随取件人-报社后门")
    (#t (error "尾随取件人：路段越界"))))

(define (本幕说明)
  (cond
    ((= 路段 0) "他从后门出来，径直钻进散场的人群。")
    ((= 路段 1) "他不断换边过街，借下班的人潮遮住自己。")
    ((= 路段 2) "电车已经进站。他突然加快脚步。")
    ((= 路段 3) "他穿过酒店大厅，在一排信格前停了一下。")
    ((= 路段 4) "报社后门近在眼前，他开始频繁回头。")
    (#t (error "尾随取件人：路段越界"))))

;; 五段各自的空间落点：CityBox 里 尾随取件人 那条街上的五个锚点，镜头随段沿街往前推
(define (路段锚点)
  (cond
    ((= 路段 0) "尾随取件人")
    ((= 路段 1) "尾随取件人-商业街")
    ((= 路段 2) "尾随取件人-电车")
    ((= 路段 3) "尾随取件人-酒店")
    ((= 路段 4) "尾随取件人-报社后门")
    (#t (error "尾随取件人：路段越界"))))

(define 咬住踪迹
  (make-clock "咬住踪迹" 4 'gauge "本幕结束前填满，才不会失去踪迹。"))

(define (重置本幕!)
  (set! 本幕拿过情报? #f)
  (set! 咬住踪迹
    (make-clock "咬住踪迹" (本幕容量) 'gauge
      "本幕结束前填满，才不会失去踪迹。")))

(define (结束! result)
  (if 已结束? (error "尾随取件人：交锋已经结算") #t)
  (set! 已结束? #t)
  (end-encounter result))

(define (检查暴露!)
  (if (暴露 'full?)
      (begin
        (spotlight! "他看见你了"
          "取件人借橱窗看清了身后的人。他收起文件袋，转进另一条街。")
        (结束! '暴露))
      #f))

(define (跟紧节点)
  (roll-action "跟紧" (list (req-die)) 'sharpness
    (outcome (lambda ()
        (暴露 'advance! 1)
        (spend-actor-composure! 'player 1)
        (检查暴露!)))
    (outcome (lambda ()
        (咬住踪迹 'advance! 1)
        (spend-actor-composure! 'player 1)))
    (outcome (lambda ()
        (咬住踪迹 'advance! 2)))))

(define (低风险跟进节点)
  (roll-action "低风险跟进" (list (req-die)) 'sharpness
    (outcome (lambda ()
        (spend-actor-composure! 'player 1)))
    (outcome (lambda ()
        (咬住踪迹 'advance! 1)
        (spend-actor-composure! 'player 1)))
    (outcome (lambda ()
        (咬住踪迹 'advance! 1)))))

(define (获取情报节点)
  (roll-action "获取情报" (list (req-die)) 'sharpness
    (outcome (lambda ()
        (暴露 'advance! 1)
        (spend-actor-composure! 'player 1)
        (检查暴露!)))
    (outcome (lambda ()
        (咬住踪迹 'advance! 1)
        (spend-actor-composure! 'player 1)))
    (outcome (lambda ()
        (咬住踪迹 'advance! 2)
        (add-item! "情报" 1)
        (set! 本幕拿过情报? #t)))))

(define (低风险可用?)
  (or (= 路段 0) (= 路段 2) (= 路段 3)))

(define (情报可用?)
  (and (not (= 路段 0)) (not 本幕拿过情报?)))

(define (行动节点)
  (append
    (list (跟紧节点))
    (if (低风险可用?) (list (低风险跟进节点)) '())
    (if (情报可用?) (list (获取情报节点)) '())))

(define (进入下一幕!)
  (set! 路段 (+ 路段 1))
  (重置本幕!)
  (cond
    ((= 路段 1)
     (play-dialogue! (line "世界" "他穿出剧院区，沿商业街往电车站走。")))
    ((= 路段 2)
     (play-dialogue! (line "世界" "他挤上电车，把文件袋压在大衣里面。")))
    ((= 路段 3)
     (play-dialogue! (line "世界" "他在格兰德酒店下车，径直走进大厅。")))
    ((= 路段 4)
     (play-dialogue! (line "世界" "他从酒店另一侧出来，手里多了一个文件袋。")))
    (#t (error "尾随取件人：不能进入这个路段"))))

(define-opponent-rule "取件人继续赶路"
  (lambda () (not 已结束?))
  (lambda ()
    (if (not (咬住踪迹 'full?))
        (踪迹 'advance! -1)
        #f)
    (cond
      ((踪迹 'empty?)
       (spotlight! "跟丢了" "电车、人群和拐角把取件人吞了进去。那只文件袋也不见了。")
       (结束! '跟丢))
      ((= 路段 4)
       (结束! '跟到报社))
      (#t (进入下一幕!)))))

(define (on-encounter-enter)
  (set! 路段 0)
  (set! 已结束? #f)
  ;; 交锋初始盘面不是玩家造成的 Clock 变化，不写入场报告的效果条（同 失控的机械）。
  (踪迹 'load! 3)
  (暴露 'load! 0)
  (重置本幕!)
  (play-dialogue!
    (line "世界" "下午四点，剧院后巷。一个瘦男人从门房手里接过信封。")
    (line "世界" "信封上写着夜莺的名字。他夹进大衣，拿了东西就走。")))

(define (on-encounter-collapse)
  (collapse-result '跟丢))

(define (get-render-data)
  (node (路段名)
    :anchor (路段锚点)
    :children (append
      (clock-nodes
        (踪迹 'render-data)
        (暴露 'render-data)
        (咬住踪迹 'render-data))
      (list
        (note-node "标注：本幕局面" "眼前的情况" (本幕说明)))
      (行动节点))))
