;; 区间不是单纯最小风险：压力3让堵截更快，压力2给失败留余量。
(define 完了? #f)
(define 出门? #f)
(define 压力 (make-clock "压力" 4 'gauge "2或3时，回合末他会出门；4时他报警。"))
(define 堵截 (make-clock "堵截" 3 'gauge "满格拿回信封。压力3时好结果推进3，否则推进2。"))
(define 警笛 (make-clock "警笛" 4 'gauge "每回合加1；满格警车封街。"))
(define (结束! result text)
  (if 完了? (error "逼他出门：重复结束") #t)
  (set! 完了? #t)
  (spotlight! "后门的信封" text)
  (end-encounter result))
(define (检查!)
  (cond ((压力 'full?) (结束! '报警 "柜台后的男人抓起电话。你把他逼向了警察。"))
        ((堵截 'full?) (结束! '截获 "他在后门撞上你。那只信封终于回到你手里。"))
        ((警笛 'full?) (结束! '封街 "警车横在街口。他把信封交给了巡警。"))))
(define (加压! n)
  (压力 'advance! n)
  (检查!))
(define-opponent-rule "柜台后的男人回应"
  (lambda () (not 完了?))
  (lambda ()
    (if 出门?
        (begin
          (set! 出门? #f)
          (堵截 'advance! -1)
          (压力 'advance! -1)
          (play-bubble! (line "世界" "他退回柜台，重新锁上后门。堵截机会过去了。")))
        (if (and (>= (压力 'current) 2) (< (压力 'current) 4))
            (begin
              (set! 出门? #t)
              (play-dialogue! (line "世界" "他把信封塞进大衣，打开后门。现在能截住他。"))) #f))))
(define-opponent-rule "巡警走近"
  (lambda () (not 完了?))
  (lambda () (警笛 'advance! 1) (检查!)))
(define (on-encounter-enter)
  (play-dialogue!
    (line "世界" "你要拿回他手里的信封。先逼他出门，再截住他。")
    (line "世界" "典当铺的柜台后，男人捏着你要找的信封。")
    (line "世界" "让他觉得待不住，又不能把他逼到报警。出门后只有一回合。")))
(define (on-encounter-collapse) (collapse-result '倒下))
(define (get-render-data)
  (container "逼他出门"
    (append
      (list (note-node "标注：目标" "目标：拿回信封"
        (if 出门? "现在截住他，堵截满格拿回信封；下回合他会退回店内。" "让他带着信封出门，再截住他。逼他报警或警车封街都会失败。")))
      (clock-nodes (压力 'render-data) (堵截 'render-data) (警笛 'render-data))
      (list (note-node "标注：男人" "后门动静"
              (if 出门? "他已出门。回合末会退回，堵截减1、压力减1。" "他躲在柜台后。结束回合才回应压力。"))
            (node "敲侧门" :subtitle "压力加1。" :requires (list (req-die))
              :resolve (instant (lambda () (加压! 1))))
            (node "砸店招" :subtitle "压力加2，花1冷静。" :requires (list (req-die))
              :resolve (instant (lambda () (spend-actor-composure! 'player 1) (加压! 2))))
            (node "退到街角" :subtitle "压力减1；出门的机会不会立刻消失。" :requires (list (req-die))
              :resolve (instant (lambda () (压力 'advance! -1)))))
      (if 出门?
          (list (node "堵住后门" :subtitle "坏：压力加1；中：堵截加1；好：加2或3。"
            :requires (list (req-die)) :resolve (roll 'violence
              (outcome (lambda () (加压! 1)))
              (outcome (lambda () (堵截 'advance! 1) (检查!)))
              (outcome (lambda () (堵截 'advance! (if (= (压力 'current) 3) 3 2)) (检查!)))))) '())
      (list (instant-action "离开典当铺" (lambda () (结束! '撤离 "你把手从门上拿开。信封还在他的大衣里。")))))))
