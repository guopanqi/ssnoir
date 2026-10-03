;; 重量直接改变同一移动判定的收益；扔钱不可撤回，是否扔取决于当前骰与剩余路程。
(define 完了? #f)
(define 走过? #f)
(define 钱袋 (make-clock "钱袋" 3 'readout "每袋5金；好结果前进5减钱袋数，中结果再少1。"))
(define 脱身 (make-clock "脱身" 6 'gauge "满6穿过铁路；每回合只能移动一次。"))
(define 追兵 (make-clock "追兵" 3 'gauge "每回合加1，满3被追上；可以先完成脱身。"))
(define (结束! result text)
  (if 完了? (error "赃款会拖脚：重复结束") #t)
  (set! 完了? #t)
  (spotlight! "铁路线尽头" text)
  (end-encounter (list result (if (equal? result '脱身) (钱袋 'current) 0))))
(define (走! n)
  (set! 走过? #t)
  (脱身 'advance! n)
  (if (脱身 'full?)
      (结束! '脱身 "你翻过铁路边的矮墙。留下的钱袋，已经拿不回来了。") #f))
(define-opponent-rule "追兵越过围栏"
  (lambda () (not 完了?))
  (lambda ()
    (set! 走过? #f)
    (追兵 'advance! 1)
    (if (追兵 'full?) (结束! '被追上 "鞋跟声从身后追上来。你抱着的钱袋全被夺走了。") #f)))
(define (on-encounter-enter)
  (钱袋 'load! 3)
  (play-dialogue!
    (line "世界" "你要甩开追兵，尽量保住赃款。抱得越多，跑得越慢。")
    (line "世界" "你抱着三袋赃款冲向铁路线。枪手从车里追了出来。")
    (line "世界" "扔一袋，就多跑一步；钱还能留多少，取决于你现在的脚力。")))
(define (on-encounter-collapse) (collapse-result (list '倒下 0)))
(define (get-render-data)
  (container "赃款会拖脚"
    (append
      (list (note-node "标注：目标" "目标：甩开追兵"
        "追兵追上前跑完六格路；尽量留下钱袋。可以扔钱换脚力，逃不掉会全被夺走。"))
      (clock-nodes (钱袋 'render-data) (脱身 'render-data) (追兵 'render-data))
      (list (note-node "标注：脚力" "这一趟能跑"
        (string-append "好：" (number->string (- 5 (钱袋 'current))) "步；中："
          (number->string (- 4 (钱袋 'current))) "步；坏：停步、花1冷静。")))
      (if (钱袋 'empty?) '()
          (list (instant-action "扔下一袋" (lambda () (钱袋 'advance! -1)))))
      (if 走过? '()
          (list (node "翻过围墙" :subtitle "每回合一次，按当前重量结算；扔钱后再走。"
            :requires (list (req-die)) :resolve (roll 'sharpness
              (outcome (lambda () (spend-actor-composure! 'player 1) (走! 0)))
              (outcome (lambda () (走! (- 4 (钱袋 'current)))))
              (outcome (lambda () (走! (- 5 (钱袋 'current)))))))))
      (list (instant-action "举起双手" (lambda () (结束! '投降 "你把钱袋放在脚边。追兵取走了全部赃款。")))))))
