;; scenes/world/home.scm - House Scene

(define home
  (let ()
    ;; ── Local State ────────────────────────────────
    (define has-flower? #f)
    (define has-gramophone? #f)
    (define playing-song "")
    (define workers-protesting? #f)
    (define protest-just-triggered #f)

    (define protest-clock (make-clock "工人抗议期限" 3 'countdown))

    ;; ── Rules ──────────────────────────────────────
    (define-turn-rule "触发工人抗议"
      (lambda () (and (not workers-protesting?) (< (get-reputation "workers") -30)))
      (lambda ()
        (set! workers-protesting? #t)
        (set! protest-just-triggered #t)
        (protest-clock 'reset!)
        (set-global! 'notification "警报：工人们发起了抗议！")))

    (define-turn-rule "工人抗议倒计时"
      (lambda () workers-protesting?)
      (lambda ()
        (if protest-just-triggered
            (set! protest-just-triggered #f)
            (begin
              (protest-clock 'tick!)
              (if (protest-clock 'full?)
                  (begin
                    (set-global! 'health (max 0 (- (get-global 'health) 30)))
                    (protest-clock 'reset!)
                    (set-global! 'notification "工人抗议期满！你遭到了示威工人的袭击。(-30生命值)"))
                  #f)))))

    ;; ── Node Definitions ──────────────────────────
    (define (node-flower)
      (observe-action "一盆花" "一盆散发着淡淡微香的白色雏菊，正静静地盛开着。"))

    (define (node-drink-wine)
      (action "喝酒"
              (list (req-item "酒" 1))
              (instant (lambda ()
                         (set-global! 'health (min 100 (+ (get-global 'health) 10)))))))

    (define (format-song-name name)
      (if (equal? playing-song name)
          (string-append "-> " name)
          name))

    (define (node-gramophone)
      (container "唱片机"
        (list
          (instant-action (format-song-name "《甜蜜蜜》")
                          (lambda () (set! playing-song "《甜蜜蜜》")))
          (instant-action (format-song-name "《怒放的生命》")
                          (lambda () (set! playing-song "《怒放的生命》")))
          (instant-action (format-song-name "《爵士舞曲》")
                          (lambda () (set! playing-song "《爵士舞曲》")))
          (instant-action "停止播放"
                          (lambda () (set! playing-song ""))))))

    (define (node-workers-protest)
      (action-with-clocks "谈判妥协"
                          (list (req-item "金钱" 30))
                          (instant (lambda ()
                                     (set! workers-protesting? #f)
                                     (protest-clock 'reset!)
                                     (change-reputation! "workers" 15)))
                          (list (protest-clock 'render-data))))

    ;; ── Message Passing Interface ─────────────────
    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'render-data)
           (append
             (if has-flower? (list (node-flower)) '())
             (append
               (if has-gramophone? (list (node-gramophone)) '())
               (append
                 (list (node-drink-wine))
                 (if workers-protesting? (list (node-workers-protest)) '())))))

          ((equal? msg 'has-gramophone?) has-gramophone?)
          ((equal? msg 'buy-gramophone!) (set! has-gramophone? #t))
          
          ((equal? msg 'has-flower?) has-flower?)
          ((equal? msg 'buy-flower!) (set! has-flower? #t))

          ((equal? msg 'save)
           (list
             (list "has-flower?" has-flower?)
             (list "has-gramophone?" has-gramophone?)
             (list "playing-song" playing-song)
             (list "workers-protesting?" workers-protesting?)
             (list "protest-just-triggered" protest-just-triggered)
             (list "protest-clock" (protest-clock 'current))))

          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! has-flower? (assoc-get data "has-flower?" #f))
             (set! has-gramophone? (assoc-get data "has-gramophone?" #f))
             (set! playing-song (assoc-get data "playing-song" ""))
             (set! workers-protesting? (assoc-get data "workers-protesting?" #f))
             (set! protest-just-triggered (assoc-get data "protest-just-triggered" #f))
             (protest-clock 'set! (assoc-get data "protest-clock" 0))))

          (#t #f))))))
