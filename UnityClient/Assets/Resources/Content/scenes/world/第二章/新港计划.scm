;; scenes/world/第二章/新港计划.scm - Phase B 的公共环境变化
;;
;; 这里只让城市显出「正在准备」：看得见，但不要求玩家调查，也不制造选择卡。
;; 真正可介入的内容由林、弗兰克和艾迪各自的模块拥有。
(define 新港计划
  (let ()
    (define (准备中?) (equal? (机器进入老街 'state) "已公布"))

    (define (nodes-at location)
      (if (not (准备中?))
          '()
          (cond
            ((equal? location "码头")
             (list (at-anchor "码头-三号货栈"
                     (note-node "标注：码头施工" "泊位封闭"
                       "三号泊位拉起施工线。仓门上贴着调岗名单，空地堆满设备箱。"))))
            ((equal? location "码头居民区")
             (list (note-node "标注：老街测量" "公司测量员"
                     "穿风衣的人沿仓库记录尺寸，被粉笔标过的门牌越来越多。")))
            ((equal? location "港务技术区")
             (list (note-node "标注：设备装车" "准备出发"
                     "轨道和传动箱正在装车。技术人员围着运行表核对编号。")))
            ((equal? location "老街酒馆")
             (list (note-node "标注：酒馆公告" "培训名单"
                     "墙边多了一张从码头撕下来的培训公告。有人写上名字，又划掉。")))
            (#t '()))))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) '())
          ((equal? msg 'dossier) '())
          ((equal? msg 'on-day-end!) #f)
          ((equal? msg 'save) '())
          ((equal? msg 'load!) #t)
          (else (error "新港计划：收到未知消息")))))))
