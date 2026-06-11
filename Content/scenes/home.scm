;; scenes/home.scm - House Scene

;; ── Local State ────────────────────────────────
(define unlock? #f)
(define knock-count 0)
(define trash-count 0)
(define has-flower? #f)
(define has-gramophone? #f)
(define last-dialogue-index -1)
(define playing-song "")

(define merchant-dialogues
  '("黑市商人：“小本生意，谢绝赊账。买定离手，概不退换啊。”"
    "黑市商人：“嘘……小声点，巡逻队刚过去。你想找点什么？”"
    "黑市商人：“我这儿的货都是从配电房和控制室顺出来的，绝对好用。”"
    "黑市商人：“小伙子，看着面生啊，要不要来瓶上好的私酿酒？”"))

;; Helper to recursively pick a different random dialogue index
(define (get-random-dialogue-index)
  (let ((idx (random-choice '(0 1 2 3))))
    (if (= idx last-dialogue-index)
        (get-random-dialogue-index)
        (begin
          (set! last-dialogue-index idx)
          idx))))

;; ── Rules ──────────────────────────────────────
(define-rule "敲门三次开门"
  (lambda () (>= knock-count 3))
  (lambda () (set! unlock? #t)))

;; ── Node Definitions ──────────────────────────
(define (make-trash-nodes n)
  (if (<= n 0)
      '()
      (cons (instant-action "清理垃圾"
                            (lambda ()
                              (set! trash-count (- trash-count 1))))
            (make-trash-nodes (- n 1)))))

(define (node-kick-bin)
  (instant-action "踢垃圾桶"
    (lambda () (set! trash-count (+ trash-count 1)))))

(define (node-knock)
  (instant-action "敲门"
    (lambda () (set! knock-count (+ knock-count 1)))))

(define (node-enter)
  (instant-action "进门2"
    (lambda ()
      (set-global! 'location "office"))))

(define (node-buy-flower)
  (action "买一盆花"
          (list (req-item "金钱" 15))
          (instant (lambda ()
                     (set! has-flower? #t)))))

(define (node-flower)
  (observe-action "一盆花" "一盆散发着淡淡微香的白色雏菊，正静静地盛开着。"))

(define (node-buy-wine)
  (action "买一瓶酒"
          (list (req-item "金钱" 10))
          (instant (lambda ()
                     (set-global! (string-append "item:" "酒") (+ (get-item "酒") 1))))))

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

;; ── Merchant Dialogue Helper ──────────────────
(define (get-merchant-dialogue)
  (let ((m (get-item "金钱")))
    (cond
      ((< m 10) "黑市商人：“切，浑身抠不出十个子儿的穷鬼，别挡着我做生意！去去去！”")
      ((>= m 100) "黑市商人：“哎呀！大老板您来啦！今天带够了金条吧？我这儿可有刚出炉的顶级军火和神药，您随便挑！”")
      (else
       (let ((idx (get-random-dialogue-index)))
         (cond
           ((= idx 0) (car merchant-dialogues))
           ((= idx 1) (cadr merchant-dialogues))
           ((= idx 2) (caddr merchant-dialogues))
           ((= idx 3) (cadddr merchant-dialogues))
           (else "")))))))

(define (node-odd-job)
  (instant-action "打零工"
    (lambda ()
      (set-global! 'money (+ (get-item "金钱") 25)))))

(define (node-squander)
  (instant-action "花光所有钱"
    (lambda ()
      (set-global! 'money 0))))

(define (node-merchant-container)
  (container "黑市商人"
    (append
      (list
        (observe-action "商人" (get-merchant-dialogue))
        (action "买防弹衣" (list (req-item "金钱" 40))
                (instant (lambda () (set-global! (string-append "item:" "防弹衣") (+ (get-item "防弹衣") 1)))))
        (action "买急救包" (list (req-item "金钱" 20))
                (instant (lambda () (set-global! (string-append "item:" "急救包") (+ (get-item "急救包") 1)))))
        (action "出售私酿酒" (list (req-item "酒" 1))
                (instant (lambda ()
                           (consume-item! "酒" 1)
                           (set-global! 'money (+ (get-item "金钱") 15))))))
      (if (not has-gramophone?)
          (list (action "买唱片机" (list (req-item "金钱" 30))
                        (instant (lambda () (set! has-gramophone? #t)))))
          '()))))

;; ── Render Data Entrypoint ────────────────────
(define (get-render-data)
  (append
    (cons (node-kick-bin)
          (make-trash-nodes trash-count))
    (list
      (node-odd-job)
      (node-squander)
      (node-merchant-container)
      (container "家"
        (append
          (list (node-knock))
          (append
            (if unlock? (list (node-enter)) '())
            (append
              (list (node-buy-flower))
              (append
                (if has-flower? (list (node-flower)) '())
                (append
                  (if has-gramophone?
                      (list (node-gramophone))
                      '())
                  (list (node-buy-wine)
                        (node-drink-wine)))))))))))
