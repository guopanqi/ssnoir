;; scenes/world/merchant.scm - Black Market Merchant Sub-location

(define merchant
  (let ()
    ;; ── Local State ────────────────────────────────
    (define last-dialogue-index -1)

    (define merchant-dialogues
      '("黑市商人：小本生意，谢绝赊账。买定离手，概不退换啊。"
        "黑市商人：嘘……小声点，巡逻队刚过去。你想找点什么？"
        "黑市商人：我这儿的货都是从配电房 and 控制室顺出来的，绝对好用。"
        "黑市商人：小伙子，看着面生啊，要不要来瓶上好的私酿酒？"))

    ;; Helper to recursively pick a different random dialogue index
    (define (get-random-dialogue-index)
      (let ((idx (random-choice '(0 1 2 3))))
        (if (= idx last-dialogue-index)
            (get-random-dialogue-index)
            (begin
              (set! last-dialogue-index idx)
              idx))))

    ;; ── Merchant Dialogue Helper ──────────────────
    (define (get-merchant-dialogue)
      (let ((m (item-count '金钱)))
        (cond
          ((< m 10) "黑市商人：切，浑身抠不出十个子儿的穷鬼，别挡着我做生意！去去去！")
          ((>= m 100) "黑市商人：哎呀！大老板您来啦！今天带够了金条吧？我这儿可有刚出炉的顶级军火 and 神药，您随便挑！")
          (else
           (let ((idx (get-random-dialogue-index)))
             (cond
               ((= idx 0) (car merchant-dialogues))
               ((= idx 1) (cadr merchant-dialogues))
               ((= idx 2) (caddr merchant-dialogues))
               ((= idx 3) (cadddr merchant-dialogues))
               (else "")))))))

    (define (get-discounted-price base-price faction req-rep discount-rate)
      (if (>= (get-reputation faction) req-rep)
          (inexact->exact (round (* base-price discount-rate)))
          base-price))

    (define (get-discounted-desc name base-price discount-price)
      (if (< discount-price base-price)
          (string-append name " (特惠省 " (number->string (- base-price discount-price)) " 金钱)")
          name))

    (define (node-merchant-container)
      (let ((armor-price (get-discounted-price 40 "mayor" 30 0.7))
            (med-price (get-discounted-price 20 "mayor" 30 0.7)))
        (let ((armor-desc (get-discounted-desc "买防弹衣" 40 armor-price))
              (med-desc (get-discounted-desc "买急救包" 20 med-price)))
          (list
            (container "黑市商人"
              (append
                (list
                  (observe-action "商人" (get-merchant-dialogue))
                  (action armor-desc (list (req-item '金钱 armor-price))
                          (instant (lambda () (add-item! '防弹衣 1))))
                  (action med-desc (list (req-item '金钱 med-price))
                          (instant (lambda () (add-item! '急救包 1))))
                  (action "去饭店吃饭" (list (req-item '金钱 12))
                          (instant (lambda ()
                                     (add-supplies! 3)
                                     (notify! "吃了顿饱饭，饱腹恢复了。"))))
                  (action "购买食物" (list (req-item '金钱 25))
                          (instant (lambda ()
                                     (add-item! '食物 1)
                                     (notify! "买了些食物。"))))
                  (action "出售私酿酒" (list (req-item '酒 1))
                          (instant (lambda ()
                                     (add-item! '金钱 15))))
                  (action "买一瓶酒" (list (req-item '金钱 10))
                          (instant (lambda () (add-item! '酒 1)))))
                (if (not (home 'has-flower?))
                    (list (action "买一盆花" (list (req-item '金钱 15))
                                  (instant (lambda () (home 'buy-flower!)))))
                    '())
                (if (not (home 'has-gramophone?))
                    (list (action "买唱片机" (list (req-item '金钱 30))
                                  (instant (lambda () (home 'buy-gramophone!)))))
                    '())))))))

    ;; ── Message Passing Interface ─────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (node-merchant-container))

          ((equal? msg 'save)
           (list
             (list "last-dialogue-index" last-dialogue-index)))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! last-dialogue-index (assoc-get data "last-dialogue-index" -1))))

          (#t #f))))))
