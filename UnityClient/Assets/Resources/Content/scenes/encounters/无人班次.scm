;; 无人班次——测试之夜。
;;
;; 三根钟，各归各的：
;;   夜班进度 —— 你推的。每清掉一个现场问题就跑完一批货。
;;   系统稳定 —— 你守的。归零就是事故。
;;   人工介入 —— 你欠的账。只升不降，全场可见。
;;
;; 题眼在第三根：出问题时叫工人来最安全，而每叫一次，你来这儿要证明的那件事
;; 就被划掉一格。林会说「等等，让它自己处理」——这句话有了那根钟才不是台词。
;;
;; 准备期不是入场券，是**这一夜的题面**：轨道校正过，轨道那一处就是小麻烦；
;; 没校正就是危机。控制器带着的那条具名隐患，会原样变成一个故障。
;;
;; 最后一批固定是《轨道上有人》：人工区有个工人在处理卡住的货，吊机正要进同一段。
;; 停机 / 人工介入 / 继续运行——林倾向最后一个。
;;
;; 城市输入：'准备-轨道 0..3、'控制器成色（上好/将就/凑合）、'控制器隐患（字符串）
;; 对外契约：回传 (list 收场 人工次数)
;;   收场：'完整自动通过 / '人工辅助完成 / '提前停机 / '出了事故

(define (global-or name fallback)
  (let ((v (get-global name))) (if v v fallback)))

(define prep-rail (global-or '准备-轨道 0))
(define ctrl-grade (global-or '控制器成色 "凑合"))
(define ctrl-flaw (global-or '控制器隐患 "接线是临时搭的"))
(if (member? ctrl-grade (list "上好" "将就" "凑合"))
    #t
    (error "无人班次：控制器成色不合法"))

(define shift-max 4)
(define stability-max 4)
(define manual-max 4)

(define shift-clk
  (make-clock "夜班进度" shift-max 'gauge
    "四批货。全部由机器自己跑完，这一夜才算数。"))

(define stability-clk
  (make-clock "系统稳定" stability-max 'countdown
    "归零就是事故——不是测试失败，是有人要写伤情报告。"))

(define manual-clk
  (make-clock "人工介入" manual-max 'gauge
    (lambda (current max)
      (if (> current 0)
          "每一次都记在运行日志上。公司看的是这一夜要人扶了几回。"
          "到现在为止，没有人碰过它。"))))

;; 三个现场问题，按批次出。轨道那一处的轻重由准备期决定；
;; 过热那一处直接读控制器的成色，隐患的名字就是它的名字。
(define (fault-name n)
  (cond
    ((= n 1) "货物超重")
    ((= n 2) "轨道反馈异常")
    ((= n 3) ctrl-flaw)
    (else "")))

(define (fault-heavy? n)
  (cond
    ((= n 1) #f)
    ((= n 2) (< prep-rail 3))
    ((= n 3) (equal? ctrl-grade "凑合"))
    (else #f)))

(define (fault-skill n)
  (if (= n 1) 'sharpness 'knowledge))

(define batch 1)          ; 现在跑第几批
(define fault-open? #t)   ; 这一批的问题还没解决
(define manual-used 0)
(define finished? #f)

(define (finish! result title text)
  (if finished? (error "无人班次：交锋已经结算") #t)
  (set! finished? #t)
  (spotlight! title text)
  (end-encounter (list result manual-used)))

(define (on-encounter-collapse)
  (collapse-result (list '出了事故 manual-used)))

(define (accident!)
  (finish! '出了事故 "有人受伤了"
    "臂在半空停了半秒，然后偏出轨道。等所有人跑过去的时候，事情已经发生了。林站在控制台后面，没有动。"))

(define (check!)
  (if (or finished? (not (stability-clk 'empty?)))
      #f
      (accident!)))

(define (clear-fault!)
  (set! fault-open? #f)
  (shift-clk 'tick!)
  (set! batch (+ batch 1))
  (set! fault-open? #t)
  (if (> batch 3) (open-final!) #f))

;; ── 最后一批 ────────────────────────────────────
(define final-open? #f)

(define (open-final!)
  (set! final-open? #t)
  (play-dialogue!
    (line "世界" "第四批。吊机开始往人工区那一段走。")
    (line "世界" "那一段里有个人——货卡住了，他正弯腰把它拨正。")
    (line "尼尔" "轨道上有人。")
    (line "林" "它能处理。")
    (line "尼尔" "你确定？")
    (line "林" "……它能处理。")))

(define (node-stop)
  (instant-action "紧急停机"
    (lambda ()
      (finish! '提前停机 "你按了红的那个"
        "整条线在三秒内停住。人工区那个人抬起头，不知道刚才发生了什么。林盯着停下来的臂看了很久，一句话也没说。"))))

(define (node-manual-final)
  (instant-action "叫他先出来"
    (lambda ()
      (set! manual-used (+ manual-used 1))
      (manual-clk 'tick!)
      (shift-clk 'tick!)
      (finish! '人工辅助完成 "货送到了"
        "有人喊了一声，人工区那个人退出去，机器把最后一个柜子放稳。四批货全到了——只是这一夜不能再叫无人班次。"))))

(define (node-let-it)
  (node "让它自己处理"
    :tags (list "高风险")
    :subtitle "林说它能处理。你决定信不信"
    :requires (list (req-die))
    :resolve (roll 'knowledge
      (outcome (lambda () (stability-clk 'advance! -2) (accident!)))
      (outcome (lambda ()
          (shift-clk 'tick!)
          (if (= manual-used 0)
              (finish! '完整自动通过 "它自己跑完了"
                "臂在离他半米的地方停住，等他直起腰，然后绕过去把柜子放稳。整夜没有一个人碰过它。")
              (finish! '人工辅助完成 "它自己跑完了这一段"
                "臂在离他半米的地方停住，等他直起腰，然后绕过去。四批货全到了——只是前面那几次，是人扶过来的。"))))
      (outcome (lambda ()
          (shift-clk 'tick!)
          (if (= manual-used 0)
              (finish! '完整自动通过 "它自己跑完了"
                "臂停住，等那个人退出去，才继续。整夜没有一个人碰过它。")
              (finish! '人工辅助完成 "它自己跑完了这一段"
                "臂停住，等那个人退出去，才继续。四批货全到了——只是前面那几次，是人扶过来的。")))))))

;; ── 前三批 ──────────────────────────────────────
(define (node-handle)
  (node (string-append "处理：" (fault-name batch))
    :subtitle (if (fault-heavy? batch)
                  "这一处准备期没顾上，现在是危机"
                  "调参数、改配重，能在台上解决")
    :tags (if (fault-heavy? batch) (list "高风险") '())
    :requires (list (req-die))
    :resolve (roll (fault-skill batch)
      (outcome (lambda ()
          (stability-clk 'advance! (if (fault-heavy? batch) -2 -1))
          (check!)))
      (outcome (lambda ()
          (if (fault-heavy? batch)
              (begin (stability-clk 'advance! -1) (check!))
              (begin (stability-clk 'advance! -1) (clear-fault!) (check!)))))
      (outcome (lambda () (clear-fault!) (check!))))))

;; 全场最安全的一手，也是唯一会往第三根钟上记账的一手。
(define (node-manual)
  (instant-action "叫工人来"
    (lambda ()
      (set! manual-used (+ manual-used 1))
      (manual-clk 'tick!)
      (play-bubble! (line "林" "等等。让它自己处理。"))
      (clear-fault!)
      (check!))))

(define (on-encounter-enter)
  ;; 交锋初始盘面不是玩家造成的 Clock 变化，不写入场报告的效果条（同 失控的机械）。
  (stability-clk 'load! stability-max)
  (play-dialogue!
    (line "世界" "十一点。机械区只留了控制台那一盏灯。")
    (line "林" "四批货。今晚没有人上手。")
    (line "尼尔" "一次都不能？")
    (line "林" "一次都不能。有人碰过，它就不叫无人班次了。")
    (line "世界" "围栏外面站着几个下夜班没走的工人。没有人说话。")))

(define (get-render-data)
  (node "无人班次"
    :anchor "三号货栈工棚"
    :children (append
      (clock-nodes (shift-clk 'render-data)
                   (stability-clk 'render-data)
                   (manual-clk 'render-data))
      (if final-open?
          (list (node-let-it) (node-manual-final) (node-stop))
          (list (node-handle) (node-manual))))))
