;; scenes/combat.scm - Combat Scene

(define enemy-id-seq 0)
(define (next-enemy-id)
  (set! enemy-id-seq (+ enemy-id-seq 1))
  enemy-id-seq)

;; ── Enemy Constructor ─────────────────────────
(define (make-enemy type hp-max atk-max dmg)
  (let ((id (next-enemy-id))
        (hp hp-max)
        (atk-clock (make-clock "A" atk-max 'countdown)))
    (let ((suppress! (lambda ()
                       (set! hp (- hp 1))
                       (atk-clock 'reset!)))
          (eliminate! (lambda ()
                        (set! hp (- hp 2)))))
      (lambda (msg)
        (cond
          ((equal? msg 'type)      type)
          ((equal? msg 'hp)        hp)
          ((equal? msg 'dead?)     (<= hp 0))
          ((equal? msg 'tick-atk!) (atk-clock 'tick!))
          ((equal? msg 'atk-full?) (atk-clock 'full?))
          ((equal? msg 'reset-atk!)(atk-clock 'reset!))
          ((equal? msg 'render-data)
           (container-with-clocks
             (string-append type " " (number->string id))
             (list
               (action (string-append "压制 " type " " (number->string id))
                       (list (req-die))
                       (roll 'violence
                             (lambda () #f)                    ; 失败: 无效果
                             (lambda () (atk-clock 'reset!))   ; 中性: 仅重置敌人攻击
                             (lambda () (suppress!))))         ; 成功: 伤害加重置
               (action (string-append "击倒 " type " " (number->string id))
                       (list (req-die))
                       (roll 'violence
                             (lambda () #f)                    ; 失败: 无效果
                             (lambda () (set! hp (- hp 1)))    ; 中性: 造成1点小伤害
                             (lambda () (eliminate!)))))       ; 成功: 造成2点大伤害
             (list (list 'clock "HP" hp hp-max 'segments)
                   (atk-clock 'render-data))))
          (else #f))))))

;; ── Local State ────────────────────────────────
(define exit-clock (make-clock "逃脱" 12 'pie))
(define spawn-clock (make-clock "增援" 3 'segments))

(define enemies
  (list (make-enemy "持刀者" 3 3 10)
        (make-enemy "持枪手" 2 4 15)))

(define (live-enemies)
  (filter (lambda (e) (not (e 'dead?))) enemies))

(define (make-random-enemy)
  (let ((type (random-choice '("持刀者" "持枪手"))))
    (if (equal? type "持刀者")
        (make-enemy "持刀者" 3 3 10)
        (make-enemy "持枪手" 2 4 15))))

;; ── Rules ──────────────────────────────────────
(define-turn-rule "敌人时钟与攻击"
  (lambda () #t)
  (lambda ()
    ;; 1. Tick attack clocks
    (define (tick-enemies list-enemies)
      (if (null? list-enemies)
          #t
          (begin
            ((car list-enemies) 'tick-atk!)
            (tick-enemies (cdr list-enemies)))))
    (tick-enemies (live-enemies))
    
    ;; 2. Process attacks
    (define (process-enemy-atk list-enemies)
      (if (null? list-enemies)
          #t
          (begin
            (let ((e (car list-enemies)))
              (if (e 'atk-full?)
                  (begin
                    (damage-party! 1)
                    (e 'reset-atk!))
                  #f))
            (process-enemy-atk (cdr list-enemies)))))
    (process-enemy-atk (live-enemies))))

(define-turn-rule "增援机制"
  (lambda () #t)
  (lambda ()
    (spawn-clock 'tick!)
    (if (spawn-clock 'full?)
        (begin
          (set! enemies (cons (make-random-enemy) enemies))
          (spawn-clock 'reset!))
        #f)))

(define-rule "成功逃脱"
  (lambda () (exit-clock 'full?))
  (lambda ()
    (end-encounter)
    (exit-clock 'reset!)))

;; ── Render Data Entrypoint ────────────────────
(define (get-render-data)
  (list
    (container-with-clocks "仓库"
      (append
        (map (lambda (e) (e 'render-data)) (live-enemies))
        (list
          (action "冲向出口"
                  (list (req-die))
                  (instant (lambda ()
                             (exit-clock 'tick!))))))
      (list (exit-clock 'render-data)
            (spawn-clock 'render-data)))))
