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
  (cond
    ((eq? reason '抵达)
      (play-dialogue! (line "世界" "夜莺停在住处的台阶前，收起了伞。")
        (line "夜莺" (cond ((恐惧 'full?) "到了……让我缓一会儿。")
          (忽略请求? "到了。刚才我真的不想再走暗巷。")
          (照顾她? "你一直陪着我。谢谢你，尼尔。")
          (else "谢谢。今晚一个人走，我还真有点害怕。")))))
    (else
      (play-dialogue! (line "世界" "两人来到亮灯的店门口。夜莺请店主帮忙叫车。")
        (line "夜莺" (if 忽略请求? "我说过不想走暗巷。今晚就到这里吧。"
          "我不想再走了。谢谢你陪我，我在这里等车。")))))
  (end-encounter (结果 reason)))
(define (检查!)
  (cond ((前进 'full?) (结束! '抵达))
        ((恐惧 'full?) (结束! '叫车离开)) (else #f)))
(define (恐惧增加! n)
  (let ((old (恐惧 'current)))
    (恐惧 'advance! n)
    (if (> (恐惧 'current) old)
      (cond ((恐惧 'full?) #f)
        ((= (恐惧 'current) 2)
          (play-dialogue! (line "世界" "夜莺抓住尼尔的袖子。")
            (line "夜莺" "我们走有灯的那边，好吗？")))
        (else (play-dialogue! (line "世界" "夜莺频频回头，话少了，脚步也快起来。")))) #f)))
(define (出现人影!)
  (set! 人影在? #t) (set! 人影次数 (+ 人影次数 1))
  (play-dialogue!
    (line "世界" "雨水顺着屋檐滴落。前面的街灯下，一个戴帽子的人影停在路口。")
    (line "世界" "夜莺忽然抓住尼尔的袖子。")
    (line "夜莺" "是他……我好像又看见他了。")))
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
      (play-dialogue! (line "世界" (if shelter?
        "两人在店里等了一阵。再出来时，路口已经没有人。"
        "尼尔挡在她身前出声喝问。人影匆匆走开，没有回答。"))))
    (恐惧增加! 1))
  (检查!))
(define (on-encounter-collapse) (collapse-result (结果 '倒下)))
(define (on-encounter-enter)
  (前进 'load! 0) (恐惧 'load! 0) (set! 人影在? #f) (set! 人影次数 0)
  (set! 人影上限 1)
  (set! 忽略请求? #f) (set! 照顾她? #f) (set! 已结束? #f)
  (play-dialogue! (line "世界" "夜莺撑开旧伞，往尼尔那边挪了一点。")
    (line "夜莺" "走有灯的那边，好吗？我们慢慢来。")))
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
            :tags (list "高风险")
            :requires (list (req-die)) :resolve (roll 'violence
              (outcome (lambda () (spend-composure! 1) (应对! #f #f)))
              (outcome (lambda () (应对! #t #f)))
              (outcome (lambda () (应对! #t #f)))))
          (node "人影：店铺" :name "绕进亮灯的店铺" :subtitle "失败恐惧+1、冷静-1；带她避开路口"
            :anchor "雨夜同行-亮灯的店铺"
            :tags (list "高风险")
            :requires (list (req-die)) :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1) (应对! #f #t)))
              (outcome (lambda () (应对! #t #t)))
              (outcome (lambda () (应对! #t #t)))))
          (node "人影：带路" :name "听夜莺指路" :subtitle "相信她熟悉的街道，一起避开路口"
            :anchor "雨夜同行-街灯下的人影"
            :requires (list (req-die)) :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1) (应对! #f #t)))
              (outcome (lambda () (应对! #t #t)))
              (outcome (lambda () (应对! #t #t))))))))
        (list
          (node "沿街灯走" :subtitle "失败冷静-1；沿亮处前进"
            :anchor "雨夜同行-街灯"
            :requires (list (req-die)) :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1) (走! 0 0 #f)))
              (outcome (lambda () (走! 1 0 #f)))
              (outcome (lambda () (走! 1 0 #f)))))
          (node "穿过暗巷" :subtitle "失败恐惧+1、冷静-1；抄暗巷近路"
            :anchor "雨夜同行-暗巷"
            :tags (list "高风险")
            :requires (list (req-die)) :resolve (roll 'sharpness
              (outcome (lambda () (spend-composure! 1) (走! 0 1 #t)))
              (outcome (lambda () (走! 1 0 #t)))
              (outcome (lambda () (走! 2 0 #t)))))))
      )))
