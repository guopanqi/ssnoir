;; scenes/combat.scm - Combat Scene

;; ── Enemy Constructor ─────────────────────────
(define (make-enemy type hp-max atk-max dmg)
  (let ((hp hp-max)
        (atk-clock (make-clock type atk-max)))
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
           (let ((clock-data (atk-clock 'render-data)))
             (let ((current (caddr clock-data)))
               (let ((rem-turns (- atk-max current)))
                 (list
                   clock-data
                   (list 'node (string-append type " (HP: " (number->string hp) "/" (number->string hp-max) " | 距攻击: " (number->string rem-turns) "轮)")
                         (list
                           (list 'node "压制" '() (lambda () (suppress!)))
                           (list 'node "击倒" '() (lambda () (eliminate!))))
                         #f))))))
          (else #f))))))

;; ── Local State ────────────────────────────────
(define exit-clock (make-clock "逃生通道" 5))
(define spawn-clock (make-clock "敌人增援" 2))

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
  (append
    ;; Clocks: Exit clock + Spawn clock + enemy attack clocks
    (cons (exit-clock 'render-data)
          (cons (spawn-clock 'render-data)
                (map (lambda (e) (car (e 'render-data))) (live-enemies))))
    
    ;; Nodes: Enemy nodes + Escape action node
    (list
      (node "战场"
        :children
        (append
          ;; Enemy node lists
          (map (lambda (e) (cadr (e 'render-data))) (live-enemies))
          ;; Escape option
          (list
            (node "冲向出口"
              :effect (lambda ()
                        (exit-clock 'tick!)))))))))
