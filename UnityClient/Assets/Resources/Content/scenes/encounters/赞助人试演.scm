;; 赞助人试演：短型舞台时间争取，不替夜莺选择唱法。
;; 仅留“完整试唱/压缩试唱”两种外部条件，她都能独立完成演出。
(define 时间 (make-clock "完整演出时间" 4 'gauge "争取让夜莺把整首歌唱完。" :beneficial-direction 'increase))
(define 节目表 (make-clock "节目表敲定" 5 'pressure "满格后只剩缩短的试演时间。" :beneficial-direction 'decrease))
(define 改期? #f)
(define 结束? #f)
(define (收场! result)
  (if 结束? (error "试演：已经结算") #t)
  (set! 结束? #t)
  (play-dialogue!
    (line "经理" (if (eq? result '完整试唱) "行！给她留整首歌。我相信她能抓住机会。" "赞助人已经等着。只能给她缩短的时间。"))
    (line "夜莺" "谢谢。剩下的交给我，我知道自己该怎样唱。"))
  (end-encounter result))
(define (检查!)
  (cond
    ((节目表 'full?) (收场! '压缩试唱))
    ((时间 'full?) (收场! '完整试唱))
    ((and (not 改期?) (>= (时间 'current) 2))
      (set! 改期? #t)
      (节目表 'advance! 1)
      (play-dialogue!
        (line "世界" "赞助人提前落座。舞台监督把节目表上的试演时长又圈短了一截。")
        (line "夜莺" "我可以少说几句，但这首歌让我自己唱完。")
        (line "经理" "时间不等人，兄弟。去和监督把剩下的空档谈下来。")))
    (else #f)))
(define (投入! amount stress)
  (时间 'advance! amount) (节目表 'advance! stress) (检查!))
(define-opponent-rule "节目表临时调整"
  (lambda () (not 结束?))
  (lambda () (节目表 'advance! 1) (检查!)))
(define (on-encounter-enter)
  (时间 'load! 0) (节目表 'load! 0)
  (set! 改期? #f) (set! 结束? #f)
  (play-dialogue!
    (line "世界" "剧院后台人来人往。经理翻着节目单，夜莺在台边轻声试唱。")
    (line "夜莺" "尼尔，替我争取一点时间就好。歌要怎么唱，我自己来。")))
(define (on-encounter-collapse) (collapse-result '压缩试唱))
(define (get-render-data)
  (container "赞助人试演" (append
    (clock-nodes (时间 'render-data) (节目表 'render-data))
    (list
      (node "协调演出时间" :subtitle "与舞台监督协调；失败无进展"
        :risk '稳妥 :progress-target (list 'clock "完整演出时间")
        :requires (list (req-die)) :resolve (roll 'social
          (outcome (lambda () (投入! 0 0)))
          (outcome (lambda () (投入! 1 0)))
          (outcome (lambda () (投入! 2 0)))))
      (node "抢留舞台空档" :subtitle "推进较快，但可能挤压排程"
        :risk '危险 :progress-target (list 'clock "完整演出时间")
        :risk-target (list 'clock "节目表敲定") :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome (lambda () (投入! 0 2)))
          (outcome (lambda () (投入! 2 1)))
          (outcome (lambda () (投入! 3 0)))))
      (instant-action "交给夜莺" (lambda () (收场! '压缩试唱)))))))
