;; 走完四格路程，并应对一次短人影障碍；恐惧满格时她选择叫车。
(define 前进 (make-clock "回到住处" 4 'gauge "抵达住处。" :beneficial-direction 'increase))
(define 恐惧 (make-clock "夜莺的恐惧" 3 'pressure "满格后她去亮处叫车，结束同行。" :beneficial-direction 'decrease))
(define 人影在? #f)
(define 人影次数 0)
(define 人影上限 1)
(define 忽略请求? #f)
(define 照顾她? #f)
(define 已结束? #f)
(define (结果 reason) (list reason 忽略请求? 照顾她? (恐惧 'current)))
(define (结束! reason)
  (set! 已结束? #t)
  (if (eq? reason '抵达)
    (play-dialogue!
      (line "世界" "夜莺迈上住处的台阶。尼尔收起伞，她长舒了一口气。")
      (line "夜莺" (if 忽略请求?
        "到了。刚才那条巷子，我真的不想走。你回去也当心点。"
        "总算到家了。尼尔，今晚多亏有你，不然我都不敢走这条路。")))
    (play-dialogue!
      (line "世界" "夜莺走进路边亮灯的店，请店主借电话叫车。")
      (line "夜莺" (if 忽略请求?
        "我说了不想走暗巷。今晚就到这里吧，我在这儿等车。"
        "我不想再走了。谢谢你陪我，我在这儿等车。"))))
  (end-encounter (结果 reason)))
(define (检查!)
  (cond ((前进 'full?) (结束! '抵达))
        ((恐惧 'full?) (结束! '叫车离开)) (else #f)))
(define (恐惧增加! n)
  (let ((old (恐惧 'current)))
    (恐惧 'advance! n)
    (if (and (< old 2) (= (恐惧 'current) 2))
      (play-dialogue!
        (line "世界" "夜莺攥紧了尼尔的袖子，人几乎贴在他肩膀上。")
        (line "夜莺" "尼尔……我们能不能换条路？"))
      #f)))
(define (出现人影!)
  (set! 人影在? #t) (set! 人影次数 (+ 人影次数 1))
  (play-dialogue!
    (line "世界" "街角的水洼映着昏黄的灯光。墙根下站着一个穿雨衣的人，夜莺忽然抓住尼尔的袖子，停了下来。")
    (line "夜莺" "……尼尔，看路灯下。就是他。")))
(define (走! progress fear dark?)
  (if (and dark? (>= (恐惧 'current) 2)) (set! 忽略请求? #t) #f)
  (前进 'advance! progress) (恐惧增加! fear)
  ;; 先兑现抵达与恐惧，最后一格不会再凭空拦下已到家的人。
  (if (or (前进 'full?) (恐惧 'full?)) (检查!)
    (if (and (< 人影次数 人影上限)
             (>= (前进 'current) 2))
      (出现人影!) #f)))
(define (应对! success? shelter?)
  (if shelter? (set! 照顾她? #t) #f)
  (if success?
    (begin (set! 人影在? #f)
      (play-dialogue!
        (line "世界" "路口已经不见那个人了。夜莺悬着的一口气终于吐了出来。")))
    (恐惧增加! 1))
  (检查!))
(define (on-encounter-collapse) (collapse-result (结果 '倒下)))
(define (on-encounter-enter)
  (前进 'load! 0) (恐惧 'load! 0) (set! 人影在? #f) (set! 人影次数 0)
  (set! 人影上限 1)
  (set! 忽略请求? #f) (set! 照顾她? #f) (set! 已结束? #f)
  (play-dialogue! (line "世界" "风裹着雨吹过来，尼尔把伞往夜莺那边偏了偏。")
    (line "夜莺" "我们走那条主道吧，有灯，亮一点儿。")))
(define (get-render-data)
  (container "雨夜同行"
    (append (clock-nodes (前进 'render-data) (恐惧 'render-data))
      (list (note-node "夜莺" "夜莺"
        (cond ((>= (恐惧 'current) 2) "抓着你的袖子，不愿再走暗巷。")
          ((= (恐惧 'current) 1) "频频回头，脚步加快。")
          (else "与你交谈，主动说起附近的街道。"))))
      (if 人影在?
        (list (node "街灯下的人影" :anchor "雨夜同行-街灯下的人影" :children (list
          (node "人影：喝问" :name "出声喝问" :subtitle "失败恐惧+1、冷静-1；挡在她身前喝问"
            :anchor "雨夜同行-街灯下的人影"
            :risk '冒险 :risk-target (list 'clock "夜莺的恐惧")
            :requires (list (req-die)) :resolve (roll 'violence
              (outcome (lambda () (spend-composure! 1) (应对! #f #f)))
              (outcome (lambda () (应对! #t #f)))
              (outcome (lambda () (应对! #t #f)))))
          (node "人影：店铺" :name "绕进亮灯的店铺" :subtitle "失败恐惧+1、冷静-1；带她避开路口"
            :anchor "雨夜同行-亮灯的店铺"
            :risk '冒险 :risk-target (list 'clock "夜莺的恐惧")
            :requires (list (req-die)) :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1) (应对! #f #t)))
              (outcome (lambda () (应对! #t #t)))
              (outcome (lambda () (应对! #t #t)))))
          (node "人影：带路" :name "听夜莺指路" :subtitle "相信她熟悉的街道，一起避开路口"
            :anchor "雨夜同行-街灯下的人影"
            :risk '冒险 :risk-target (list 'clock "夜莺的恐惧")
            :requires (list (req-die)) :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1) (应对! #f #t)))
              (outcome (lambda () (应对! #t #t)))
              (outcome (lambda () (应对! #t #t))))))))
        (list
          (node "沿街灯走" :subtitle "失败冷静-1；沿亮处前进"
            :anchor "雨夜同行-街灯"
            :risk '稳妥 :risk-target (list 'composure "player")
            :progress-target (list 'clock "回到住处")
            :requires (list (req-die)) :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1) (走! 0 0 #f)))
              (outcome (lambda () (走! 1 0 #f)))
              (outcome (lambda () (走! 1 0 #f)))))
          (node "穿过暗巷" :subtitle "失败恐惧+1、冷静-1；抄暗巷近路"
            :anchor "雨夜同行-暗巷"
            :risk '冒险 :risk-target (list 'clock "夜莺的恐惧")
            :progress-target (list 'clock "回到住处")
            :requires (list (req-die)) :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1) (走! 0 1 #t)))
              (outcome (lambda () (走! 1 0 #t)))
              (outcome (lambda () (走! 2 0 #t)))))))
      )))
