;; scenes/combat.scm - Combat Scene

;; ── Enemy Constructor ─────────────────────────
(define (make-enemy type hp-max atk-max dmg)
  (let ((hp hp-max)
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
           (list 'node type
                 (list (list 'clock "HP" hp hp-max 'segments)
                       (atk-clock 'render-data))
                 (list (node "压制" :effect (lambda () (suppress!)))
                       (node "击倒" :effect (lambda () (eliminate!))))
                 #f))
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
(define-rule "敌人时钟与攻击"
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
                    (set-global! 'health (- (get-global 'health) 10))
                    (e 'reset-atk!))
                  #f))
            (process-enemy-atk (cdr list-enemies)))))
    (process-enemy-atk (live-enemies))))

(define-rule "增援机制"
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
    (set-global! 'location "home")
    (exit-clock 'reset!)))

;; ── Render Data Entrypoint ────────────────────
(define (get-render-data)
  (list
    (node "仓库"
      :clocks (list (exit-clock 'render-data)
                    (spawn-clock 'render-data))
      :children
      (append
        (map (lambda (e) (e 'render-data)) (live-enemies))
        (list
          (node "冲向出口"
            :effect (lambda ()
                      (exit-clock 'tick!))))))))
