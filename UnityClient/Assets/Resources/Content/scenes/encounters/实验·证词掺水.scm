;; 增长伴随污染；去污染会撤销已取得的部分进展。
(define 完了? #f)
(define 口供 (make-clock "口供" 8 'gauge "至少4格才够指认；不自动提交，可继续核实。"))
(define 假话 (make-clock "假话" 4 'readout "逼问每次加1；未核实就交稿，会误指一个无辜司机。"))
(define (交稿!)
  (if 完了? (error "证词掺水：重复结束") #t)
  (set! 完了? #t)
  (let ((结果 (cond ((< (口供 'current) 4) '证词不足)
                    ((假话 'empty?) '可信证词)
                    (else '误指司机))))
    (spotlight! "清晨的证词"
      (cond ((equal? 结果 '证词不足) "警车到了。你只有零碎的几句，无法指出货主。")
            ((equal? 结果 '可信证词) "货主的名字对得上。那个无辜司机没有被写进指认。")
            (else "货主的名字写下了，但证人顺着你的话，牵连了一个无辜司机。")))
    (end-encounter (list 结果 (口供 'current) (假话 'current)))))
(define-opponent-rule "警车来到门口"
  (lambda () (not 完了?))
  (lambda () (交稿!)))
(define (on-encounter-enter)
  (play-dialogue!
    (line "世界" "你要问出货主的名字，交给警方。证词得够完整，也不能牵连无辜司机。")
    (line "世界" "证人坐在厨房里。警车下一回合就到，你只有这一手骰子。")
    (line "世界" "逼问能保证两格口供，却带进一句假话。核实会删掉假话，也删一格口供。")))
(define (on-encounter-collapse)
  (collapse-result (list '倒下 (口供 'current) (假话 'current))))
(define (get-render-data)
  (container "证词掺水"
    (append
      (list (note-node "标注：目标" "目标：可信证词"
        (if (and (>= (口供 'current) 4) (假话 'empty?)) "证词已足够且没有假话。交出笔记，就能正确指认货主。" "问出能指认货主的证词：至少四格、假话为零，再交出笔记。假话会误指无辜司机。")))
      (clock-nodes (口供 'render-data) (假话 'render-data))
      (list (note-node "标注：来不及重问" "警车在路上" "本回合结束立即交稿；至少4格，且假话为0才可信。")
        (node "听他讲完" :subtitle "坏：无进展、花1冷静；中：口供1；好：口供2。"
          :requires (list (req-die)) :resolve (roll 'social
            (outcome (lambda () (spend-actor-composure! 'player 1)))
            (outcome (lambda () (口供 'advance! 1)))
            (outcome (lambda () (口供 'advance! 2)))))
        (node "逼他说名字" :subtitle "一骰：保证口供2，但混入1句假话。"
          :requires (list (req-die)) :resolve (instant (lambda ()
            (口供 'advance! 2) (假话 'advance! 1)))))
      (if (假话 'empty?) '()
        (list (node "核对车牌" :subtitle "一骰：删1句假话，同时删1格口供。"
          :requires (list (req-die)) :resolve (instant (lambda ()
            (假话 'advance! -1) (口供 'advance! -1))))))
      (list (instant-action "交出笔记" (lambda () (交稿!)))))))
