;; 独立筹备交锋：回合压缩后台的排练空档，不推进城市日期。
;; 资源路径只决定试唱条件；D11由夜莺自己决定演唱版本。
(define 旧曲 (make-clock "排好旧曲" 5 'gauge "旧谱、伴奏与后台空档，要一件件凑齐。" :beneficial-direction 'increase))
(define 机会 (make-clock "完整试唱机会" 4 'gauge "为她留出完整唱完旧歌的舞台时间。" :beneficial-direction 'increase))
(define 压力 (make-clock "排练窗口缩紧" 6 'pressure "节目表就要敲定。排不齐，就得先按新编曲走。" :beneficial-direction 'decrease))
(define 敲定过? #f)
(define 结束? #f)
(define (收场! result)
  (if 结束? (error "旧歌交锋：重复结算") #t)
  (set! 结束? #t)
  (play-dialogue!
    (line "夜莺" (if (eq? result '保留旧歌)
      "旧曲能排完，舞台也肯留时间了。谢谢。唱哪版，我试过再定。"
      "先按新谱排吧。新的唱法我也喜欢，旧歌以后再找机会。")))
  (end-encounter result))
(define (检查局势!)
  (cond ((压力 'full?) (收场! '接受改编))
    ((and (旧曲 'full?) (机会 'full?)) (收场! '保留旧歌))
    ((and (not 敲定过?) (or (>= (压力 'current) 3) (>= (旧曲 'current) 3)))
      (set! 敲定过? #t)
      (play-dialogue!
        (line "经理" "节目表等着付印。能排齐就留这段，排不齐就按新谱走。")
        (line "夜莺" "把两份谱都留给我。我再试一次。")))
    (else #f)))
(define (投入! target amount heat)
  (if 结束? (error "旧歌交锋：已结束") #t)
  (target 'advance! amount) (压力 'advance! heat)
  (检查局势!))
(define (筹备行动 id name skill target risky?)
  (node id :name name
    :subtitle (if risky? "中结果也会挤压排练窗口" "稳步协调；坏结果挤压排练窗口")
    :risk (if risky? '危险 '冒险)
    :progress-target (list 'clock (cadr (target 'render-data)))
    :risk-target (list 'clock "排练窗口缩紧") :requires (list (req-die))
    :resolve (roll skill
      (outcome (lambda () (投入! target 0 1)))
      (outcome (lambda () (投入! target 1 (if risky? 1 0))))
      (outcome (lambda () (投入! target (if risky? 2 1) 0))))))
(define-opponent-rule "后台空档缩紧"
  (lambda () (not 结束?))
  (lambda () (压力 'advance! 1) (检查局势!)))
(define (on-encounter-enter)
  (旧曲 'load! 0) (机会 'load! 0) (压力 'load! 0)
  (set! 敲定过? #f) (set! 结束? #f)
  (play-dialogue!
    (line "世界" "后台正在换场。伴奏把两份谱摊开，舞台监督指了指还没定下的节目顺序。")))
(define (on-encounter-collapse) (collapse-result '接受改编))
(define (get-render-data)
  (container "赞助人试演" (append
    (list (note-node "旧歌：目标" "完整试唱" "排齐旧曲和舞台时间，让夜莺有机会试完两版。")
      (note-node "旧歌：回合回应" "排练空档" "每次结束回合，排练窗口缩紧增加1；满格后按新编曲排演。"))
    (clock-nodes (旧曲 'render-data) (机会 'render-data) (压力 'render-data))
    (if (旧曲 'full?) '() (list
      (筹备行动 "排旧曲" "和伴奏排旧曲" 'social 旧曲 #f)
      (筹备行动 "空档赶排" "利用空档赶排" 'sharpness 旧曲 #t)))
    (if (机会 'full?) '() (list
      (筹备行动 "谈后台时间" "和监督谈时间" 'social 机会 #f)
      (筹备行动 "协调后台" "借临工协调" 'social 机会 #t)))
    (list (instant-action "放下筹备" (lambda () (收场! '接受改编)))))))
