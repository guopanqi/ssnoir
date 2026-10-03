;; 兑现当前机会会永久关闭未来机会；等待使已准备的优势衰减。
(define 完了? #f)
(define 本轮准备 0)
(define 伏击 (make-clock "伏击准备" 6 'gauge "每回合最多准备两次；好2、中1、坏花1冷静。回合末丢2。"))
(define 来客 (make-clock "来客" 3 'readout "0无人；1送货人需准备4；2买主需准备6；3全部离开。"))
(define (结束! result text)
  (if 完了? (error "等买主现身：重复结束") #t)
  (set! 完了? #t) (spotlight! "桥下交易" text)
  (end-encounter result))
(define (准备! n)
  (set! 本轮准备 (+ 本轮准备 1))
  (伏击 'advance! n))
(define-opponent-rule "交易的下一辆车"
  (lambda () (not 完了?))
  (lambda ()
    (伏击 'advance! -2) (来客 'advance! 1) (set! 本轮准备 0)
    (cond ((= (来客 'current) 1)
      (play-dialogue! (line "世界" "送货人到了。现在抓他能查到仓库，但买主就不会出现。")))
      ((= (来客 'current) 2)
        (play-dialogue! (line "世界" "买主下了车。账本在他身上，抓住他需要六格准备。")))
      (else (结束! '全部离开 "两辆车消失在桥下，你错过了这一场交易。")))))
(define (on-encounter-enter)
  (play-dialogue!
    (line "世界" "你要查出货主的交易线索。抓送货人拿地址，或等买主拿账本。")
    (line "世界" "你躲在桥下，先来的是送货人，下一回合才是买主。")
    (line "世界" "每轮只能准备两次，等一轮丢两格。抓送货人要四格，抓买主要六格。")))
(define (on-encounter-collapse) (collapse-result '倒下))
(define (get-render-data)
  (container "等买主现身"
    (append
      (list (note-node "标注：目标" "目标：交易线索"
        (cond ((= (来客 'current) 0) "先准备伏击。抓送货人能拿仓库地址；放他过去才有机会抓买主、拿账本。") ((= (来客 'current) 1) "送货人在场。现在抓他拿地址，或放他过去等买主；动手后买主就不会出现。") (else "买主已到。准备满六格，抓他拿账本；下一回合交易结束，送货人也无法补抓。"))))
      (clock-nodes (伏击 'render-data) (来客 'render-data))
      (list (note-node "标注：眼前机会" "桥下的车"
        (cond ((= (来客 'current) 0) "尚未到场。先准备，下一回合送货人出现。")
          ((= (来客 'current) 1) "抓送货人：仓库地址；等买主：货主账本，但准备会丢2。")
          (else "买主在场，准备6可抓；下一回合全部离开。"))))
      (if (< 本轮准备 2)
        (list (node "布置伏击" :subtitle "本轮最多两次：好2、中1；坏花1冷静。"
          :requires (list (req-die)) :resolve (roll 'sharpness
            (outcome (lambda () (spend-actor-composure! 'player 1) (准备! 0)))
            (outcome (lambda () (准备! 1)))
            (outcome (lambda () (准备! 2)))))) '())
      (if (and (= (来客 'current) 1) (>= (伏击 'current) 4))
        (list (instant-action "抓住送货人"
          (lambda () (结束! '仓库地址 "你扣住送货人，问出了仓库地址。买主的车掉头走了。")))) '())
      (if (and (= (来客 'current) 2) (伏击 'full?))
        (list (instant-action "抓住买主"
          (lambda () (结束! '货主账本 "你将买主按在车门上，从他的口袋里取出了账本。")))) '())
      (list (instant-action "收手离开"
        (lambda () (结束! '撤离 "你从桥下退开，留下两辆车继续交易。")))))))
