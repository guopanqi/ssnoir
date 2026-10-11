;; 独立原型：距离不是推进条；线索窗口、回头与面孔记忆共同改变它的价值。
;; 路线固定，下一拍明牌。移动吃一颗任意骰，偷听吃一次判定；每段只能偷听一次。
(define 段 0)
(define 完了? #f)
(define 听过? #f)
(define 线索 '())
(define 距离 (make-clock "距离" 5 'readout "0贴身；1能听清；2至4可跟；5跟丢。"))
(define 疑心 (make-clock "疑心" 4 'gauge "满格暴露；拉开到3以上，回合末消退1。"))
(define 认脸 (make-clock "认脸" 3 'gauge "每段贴近偷听加1；记忆不消退，回头时加重疑心。"))
(define 路程 (make-clock "路程" 6 'gauge "六段后交割；跟到终点才知道接头地点。"))
(define (线索文字 xs)
  (if (null? (cdr xs)) (car xs)
      (string-append (car xs) "、" (线索文字 (cdr xs)))))
(define (窗口?) (member? 段 '(0 2 4)))
(define (回头?) (member? 段 '(1 4 5)))
(define (加速?) (member? 段 '(2 3 5)))
(define (地名) (list-ref '("电话亭" "橱窗前" "穿过车流" "电车站" "酒店门口" "后门交割") 段))
(define (局面)
  (list-ref '("他在电话里报一个名字。走开就听不到了。"
              "他停在橱窗前，借玻璃看身后。"
              "他边赶路边向门房交代时间。"
              "他向酒店侍者招手。想听交代，现在得跟近。"
              "他向侍者说出交货暗号，同时观察街面。"
              "他快步走向后门，最后确认有没有尾巴。") 段))
(define (预告)
  (string-append (if (回头?) "回头：距离≤2时，疑心增加1＋认脸。" "不回头。")
                 (if (加速?) "随后距离+1。" "距离不变。")))
(define (结束! result title text)
  (if 完了? (error "橱窗里的脸：重复结算") #t)
  (set! 完了? #t)
  (spotlight! title text)
  (end-encounter (list result (reverse 线索))))
(define (检查!)
  (cond ((疑心 'full?) (结束! '暴露 "空手的接头人" "他撕掉纸条，空着手走进后门。你已经惊动了他们。"))
        ((距离 'full?) (结束! '跟丢 "车流合上了" "你记下了几句话，却再也找不到那件灰大衣。"))))
(define (移动 name delta subtitle)
  (node name :subtitle subtitle :requires (list (req-die))
    :resolve (instant (lambda () (距离 'advance! delta) (检查!)))))
(define (听! 成功?)
  (set! 听过? #t)
  (认脸 'advance! 1)
  (if 成功?
      (let ((词 (list-ref '("莫里斯" "" "午夜" "" "蓝火柴" "") 段)))
        (set! 线索 (cons 词 线索))
        (play-bubble! (line "世界" (string-append "你听清了：" 词 "。"))))
      (begin (疑心 'advance! 1) (result-supplement! "他压低了声音，你没有听清。")))
  (检查!))
(define-opponent-rule "他检查尾巴"
  (lambda () (and (not 完了?) (回头?) (<= (距离 'current) 2)))
  (lambda ()
    (play-bubble! (line "世界" "他的目光在街面上扫了一遍。"))
    (疑心 'advance! (+ 1 (认脸 'current)))
    (检查!)))
(define-opponent-rule "藏进街面"
  (lambda () (and (not 完了?) (>= (距离 'current) 3)))
  (lambda () (疑心 'advance! -1)))
(define-opponent-rule "灰大衣赶路"
  (lambda () (not 完了?))
  (lambda ()
    (if (加速?) (距离 'advance! 1) #f)
    (检查!)
    (if (not 完了?)
        (begin
          (路程 'advance! 1)
          (if (= 段 5)
              (结束! '跟到交割 "交割地点" "后门通向报社的印刷间。你知道了他们在哪里碰头。")
              (begin (set! 段 (+ 段 1)) (set! 听过? #f)))) #f)))
(define (on-encounter-enter)
  (距离 'load! 3)
  (play-dialogue!
    (line "世界" "你要查出这封信交给谁。跟到接头地点，沿途尽量听清线索。")
    (line "世界" "灰大衣接过信封，走向街角的电话亭。")
    (line "世界" "跟到交割地点。名字、时间和暗号，能听到多少算多少。")
    (line "世界" "他会记住反复靠近的脸。结束回合前，看清他下一步要做什么。")))
(define (on-encounter-collapse) (collapse-result (list '倒下 (reverse 线索))))
(define (get-render-data)
  (container "橱窗里的脸"
    (append
      (list (note-node "标注：目标" "目标：跟到接头处"
        "跟到接头地点；沿途可偷听名字、时间和暗号。跟丢或暴露就失败。"))
      (clock-nodes (距离 'render-data) (疑心 'render-data) (认脸 'render-data) (路程 'render-data))
      (list (note-node "标注：街面" (地名) (局面))
            (note-node "标注：下一拍" "结束回合后" (预告))
            (note-node "标注：听到的话" "记下的话" (if (null? 线索) "还没有听清任何线索。" (线索文字 (reverse 线索)))))
      (if (> (距离 'current) 0) (list (移动 "跟近一步" -1 "任意骰：距离减1。")) '())
      (if (< (距离 'current) 4) (list (移动 "拉开一步" 1 "任意骰：距离加1。")) '())
      (if (and (窗口?) (not 听过?) (<= (距离 'current) 1))
          (list (node "贴近偷听" :subtitle "认脸加1；中：听清但起疑；坏：没听清。"
                 :requires (list (req-die))
                 :resolve (roll 'sharpness
                   (outcome (lambda () (听! #f)))
                   (outcome (lambda () (疑心 'advance! 1) (听! #t)))
                   (outcome (lambda () (听! #t)))))) '())
      (list (instant-action "放弃尾随"
        (lambda () (结束! '撤离 "收起笔记" "你让灰大衣走远了。手里的话还需要别的证据。")))))))
