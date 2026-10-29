;; 独立流水线试验：船 → 吊机 → PlaceA → 转运 → 仓库。
(define 船上货物 (make-clock "船上货物" 8 'countdown
  "吊机搬走货物后减少。" :beneficial-direction 'decrease))
(define 仓库 (make-clock "仓库" 8 'gauge
  "填满八批货物即完成。" :beneficial-direction 'increase))
(define 已结束? #f)
(define (PlaceA) (- 8 (船上货物 'current) (仓库 'current)))
(define (收工!)
  (if 已结束? (error "协作卸货：重复收工") #t)
  (set! 已结束? #t)
  (play-dialogue!
    (line "世界" "最后一批货送进仓库。工头在货单上签了字，朝东侧的小组点了点头。")
    (line "艾迪" "干得不错。走，喝口热的。")
    (line "世界" "食堂里，弗兰克把一锅汤放到桌上，给尼尔留了张长凳。")
    (line "弗兰克" "坐这儿。今晚这碗不用你搬。"))
  (end-scenario '完工))
(define (检查完工!)
  (if (and (not 已结束?) (仓库 'full?)) (收工!) #f))
(define (吊机搬运! 数量)
  (if (and (> (船上货物 'current) 0) (not (船上货物 'empty?))) #t
      (error "协作卸货：船上已经没有货物"))
  (船上货物 'advance! (- (min 数量 (船上货物 'current))))
  (检查完工!))
(define (转运入库! 数量)
  (if (> (PlaceA) 0) #t (error "协作卸货：PlaceA 没有货物"))
  (仓库 'advance! (min 数量 (PlaceA) (- (仓库 'max) (仓库 'current))))
  (检查完工!))

(define (on-scenario-enter)
  (船上货物 'load! 8)
  (仓库 'load! 0)
  (set! 已结束? #f)
  (play-dialogue!
    (line "世界" "一艘大货轮靠上码头。吊机的钢索垂进船舱，PlaceA 的货车停在仓库门前。")
    (line "艾迪" "先把货从船上吊到 PlaceA，再送进仓库。仓库装满就收工。")))
(define (on-scenario-collapse)
  (collapse-result (list '未完工 (仓库 'current))))
(define (get-render-data)
  (container "协作卸货" (append
    (list
      (note-node "卸货目标" "目标" "把船上的八批货全部送进仓库。")
      (note-node "PlaceA货物" "PlaceA"
        (string-append "现有货物：" (number->string (PlaceA)))))
    (clock-nodes (船上货物 'render-data) (仓库 'render-data))
    (list
      (node "吊机搬运" :subtitle "搬运0／1／2批"
        :disabled (船上货物 'empty?)
        :requires (list (req-die))
        :resolve (roll 'violence
          (outcome (lambda () (吊机搬运! 0)))
          (outcome (lambda () (吊机搬运! 1)))
          (outcome (lambda () (吊机搬运! 2)))))
      (node "转运入库" :subtitle "转运0／1／2批"
        :disabled (= (PlaceA) 0)
        :progress-target (list 'clock "仓库")
        :requires (list (req-die))
        :resolve (roll 'sharpness
          (outcome (lambda () (转运入库! 0)))
          (outcome (lambda () (转运入库! 1)))
          (outcome (lambda () (转运入库! 2)))))))))
