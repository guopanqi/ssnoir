;; scenes/world/码头.scm

(define dock
  (let ()
    ;; ── Local State ───────────────────────────────────
    (define dock-clock (make-clock "码头探索" 6 'segments))

    ;; Two pools — A and B names never overlap, so sibling nodes always have distinct names.
    (define npc-a-index 0)
    (define npc-b-index 0)

    (define (npc-a-label)
      (let ((idx (modulo npc-a-index 4)))
        (cond ((= idx 0) "码头工人")
              ((= idx 1) "渔夫")
              ((= idx 2) "卸货工")
              (else       "巡逻员"))))

    (define (npc-b-label)
      (let ((idx (modulo npc-b-index 4)))
        (cond ((= idx 0) "老水手")
              ((= idx 1) "闲汉")
              ((= idx 2) "收货员")
              (else       "破烂王"))))

    ;; ── Sub-location Unlocks ──────────────────────────
    (define (check-unlocks!)
      (if (and (>= (dock-clock 'current) 2)
               (not (get-global 'dock-market-open)))
          (begin
            (set-global! 'dock-market-open #t)
            (notify! "老街市集的方向传来叫卖声——那边有人在做生意。"))
          #f)
      (if (and (>= (dock-clock 'current) 4)
               (not (get-global 'dock-residential-open)))
          (begin
            (set-global! 'dock-residential-open #t)
            (notify! "你在码头深处发现了一片昏黄的灯火——有人住在那儿。"))
          #f)
      (if (and (>= (dock-clock 'current) 6)
               (not (get-global 'dock-bar-open)))
          (begin
            (set-global! 'dock-bar-open #t)
            (notify! "探索到了尽头，你注意到角落里有家破旧的酒吧，门缝里透出昏黄的光。"))
          #f))

    ;; ── NPC Nodes ─────────────────────────────────────
    (define (node-npc-a)
      (roll-action (npc-a-label) (list (req-die)) 'violence
        (lambda ()
          (stress-current-actor! 1)
          (set! npc-a-index (+ npc-a-index 1))
          (notify! "对方打量了你一眼，转身走开了，什么都没说。"))
        (lambda ()
          (dock-clock 'tick!)
          (set! npc-a-index (+ npc-a-index 1))
          (check-unlocks!)
          (notify! "话不多，但你拼凑出了一两句有用的消息。"))
        (lambda ()
          (dock-clock 'tick!)
          (dock-clock 'tick!)
          (set! npc-a-index (+ npc-a-index 1))
          (check-unlocks!)
          (notify! "对方话匣子打开了，你得到了不少线索。"))))

    (define (node-npc-b)
      (roll-action (npc-b-label) (list (req-die)) 'violence
        (lambda ()
          (stress-current-actor! 1)
          (set! npc-b-index (+ npc-b-index 1))
          (notify! "对方不耐烦地挥了挥手，不肯多说。"))
        (lambda ()
          (dock-clock 'tick!)
          (set! npc-b-index (+ npc-b-index 1))
          (check-unlocks!)
          (notify! "获得了一点零碎的信息，还不够。"))
        (lambda ()
          (dock-clock 'tick!)
          (dock-clock 'tick!)
          (set! npc-b-index (+ npc-b-index 1))
          (check-unlocks!)
          (notify! "你摸清了对方的口风，获得了关键信息。"))))

    ;; ── Container ────────────────────────────────────
    (define (node-dock-container)
      (if (dock-clock 'full?)
          (container "码头"
            (list (observe-action "码头全貌" "你已经摸透了这片码头，可以自由穿行。")))
          (container-with-clocks "码头"
            (list (node-npc-a) (node-npc-b))
            (list (dock-clock 'render-data)))))

    ;; ── Message Passing Interface ─────────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (list (node-dock-container)))

          ((equal? msg 'complete?)
           (dock-clock 'full?))

          ((equal? msg 'save)
           (list
             (list "dock-progress"  (dock-clock 'current))
             (list "npc-a-index"    npc-a-index)
             (list "npc-b-index"    npc-b-index)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (dock-clock 'set!  (assoc-get data "dock-progress"  0))
             (set! npc-a-index  (assoc-get data "npc-a-index"    0))
             (set! npc-b-index  (assoc-get data "npc-b-index"    0))))

          (#t #f))))))
