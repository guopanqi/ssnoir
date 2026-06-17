;; scenes/world/剧院.scm - Theater location

(define theater
  (let ()
    ;; ── Local State ────────────────────────────────
    (define gate-entered #f)
    (define nightingale-talked #f)
    (define mission-stage 1)
    ;; 阶段: 1=调查中 2=追击成功(被打晕) 3=追击失败(夜莺安慰) 4=结束

    (define (node-enter-gate-action)
      (action "进入大门"
              (list (req-die))
              (roll 'sharpness
                    (lambda ()
                      (set! gate-entered #t)
                      (stress-current-actor! 1)
                      (notify! "保安态度极其恶劣，你强忍怒火走了进去。承受了1点压力。"))
                    (lambda ()
                      (set! gate-entered #t)
                      (notify! "保安有些敷衍地摆摆手，你推门走了进去。"))
                    (lambda ()
                      (set! gate-entered #t)
                      (notify! "保安非常客气地侧身开门，你体面地走了进去。")))))

    (define (node-gate-container)
      (container "大门"
        (if (not nightingale-talked)
            (list
              (instant-action "和夜莺谈话"
                (lambda ()
                  (set! nightingale-talked #t)
                  (notify! "夜莺：你终于来了，我一直在等你。"))))
            (list
              (observe-action "夜莺" "夜莺：快去追吧，别让他跑了。")))))

    (define (node-chase-man)
      (encounter-action "追上黑衣人"
        (lambda ()
          (start-encounter "追击黑衣人"
            (lambda (result)
              (damage-party! 1)
              (add-actor-stress! 'player 1)
              (if (equal? result 'success)
                  (set! mission-stage 2)
                  (set! mission-stage 3)))))))

    (define (stage-one-nodes)
      (cond
        ((not gate-entered)
         (list (node-enter-gate-action)))

        ((not nightingale-talked)
         (list
           (node-gate-container)
           (observe-action "黑衣人" "一个黑衣人正匆忙地走出大门。")))

        (#t
         (list
           (node-gate-container)
           (observe-action "黑衣人留下的踪迹" "地上残留着潮湿的泥土，以及一串延伸向阴暗巷弄的脚印。")
           (node-chase-man)))))

    (define (node-theater-container)
      (container "剧院"
        (cond
          ;; 阶段1: 调查大门，追上黑衣人
          ((= mission-stage 1)
           (stage-one-nodes))

          ;; 阶段2: 追击成功，但被打晕
          ((= mission-stage 2)
           (list
             (observe-action "眩晕中" "你追上了黑衣人，正要开口，背后一记重击将你打晕。等你醒来，四周空无一人。")
             (instant-action "撑起身子"
                             (lambda ()
                               (set! mission-stage 4)
                                (advance-chapter!)
                               (notify! "头还在嗡嗡作响，但人已经不见了。")))))

          ;; 阶段3: 追击失败，夜莺赶来
          ((= mission-stage 3)
           (list
             (observe-action "夜莺赶来" "夜莺从后面追上来，轻声说：别自责，你已经尽力了。你心急失蹄，膝盖还在疼。")
             (instant-action "接受安慰"
                             (lambda ()
                               (set! mission-stage 4)
                                (advance-chapter!)
                               (notify! "这次没追上，但还有机会。")))))

          ;; 阶段4: 结束
          (#t
           (list
             (observe-action "剧院门口" "夜色渐深，剧院大门紧闭。"))))))

    ;; ── Message Passing Interface ─────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node-theater-container)))

          ((equal? msg 'save)
           (list
             (list "gate-entered"       gate-entered)
             (list "nightingale-talked" nightingale-talked)
             (list "mission-stage"      mission-stage)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! gate-entered       (assoc-get data "gate-entered"       #f))
             (set! nightingale-talked (assoc-get data "nightingale-talked" #f))
             (set! mission-stage      (assoc-get data "mission-stage"      1))))

          (#t #f))))))
