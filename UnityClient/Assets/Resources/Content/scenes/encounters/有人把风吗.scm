;; 单场委派交锋：帮手只有岗位，没有独立骰池，也不自动搜出账簿。
;; 搜查每次推进现场一拍；结束回合算等待一拍，不重置现场时间。
;; 回传 (离开方式 账簿状态 搜查进展)，不擅自改正式案件或发放奖励。
(define 岗位 '未安排)
(define 已结束? #f)
(define 搜查 (make-clock "寻找账簿" 4 'gauge "满4找到账簿；拿到后仍须离开。"))
(define 时间 (make-clock "屋主归来" 3 'gauge "搜查或等待加1；满3屋主推门。"))

(define (有账簿?) (搜查 'full?))
(define (结果 reason)
  (list reason (if (有账簿?) '拿到账簿 '没有账簿) (搜查 'current)))
(define (结束! reason title text)
  (if 已结束? (error "有人把风吗：重复结算") #t)
  (set! 已结束? #t)
  (spotlight! title text)
  (end-encounter (结果 reason)))

(define (撞见!)
  (结束! '被撞见 "门开了"
    (if (有账簿?)
      "账簿已经在你手里，屋主却堵住了门。他看清了你们的脸。"
      "屋主堵住了门。抽屉还开着，你们没找到那本账簿。")))

(define (推进现场!)
  (时间 'tick!)
  (cond
    ((时间 'full?) (撞见!))
    ((and (eq? 岗位 '把风) (= (时间 'current) 2))
     (play-dialogue!
       (line "世界" "山姆在门框上轻敲两下。楼下传来脚步声。")
       (line "世界" "现在能走。再抢搜一次，得手后也得立刻撤。")))
    (else #f)))

(define (安排! job)
  (if (or 已结束? (not (eq? 岗位 '未安排)))
    (error "有人把风吗：只能在入场时委派一次") #t)
  (if (member? job (list '搜查 '把风)) #t
    (error "有人把风吗：未知岗位"))
  (set! 岗位 job)
  (result-supplement!
    (if (eq? job '搜查) "山姆留下帮你翻找" "山姆到门外把风")))

(define (搜查! grade)
  (if (or 已结束? (eq? 岗位 '未安排) (有账簿?))
    (error "有人把风吗：当前不能搜查") #t)
  (搜查 'advance!
    (+ (cond ((eq? grade '好) 2) ((eq? grade '中) 1) (else 0))
       (if (eq? 岗位 '搜查) 1 0)))
  (if (有账簿?)
    (play-dialogue! (line "世界" "假底下压着一本账簿。就是它。")) #f)
  ;; 把风后的最后一次行动是抢搜并撤离；只有好结果才能赶在推门前跑掉。
  (if (and (eq? 岗位 '把风) (= (时间 'current) 2))
    (begin
      (时间 'tick!)
      (if (eq? grade '好)
        (结束! '险中撤离 "赶在门开前"
          (if (有账簿?)
            "你夹起账簿，跟着山姆从后门钻出去。钥匙已经插进前门。"
            "你放下最后一叠纸，跟着山姆撤走。账簿还留在里面。"))
        (撞见!)))
    (推进现场!)))

(define (撤离!)
  (结束! '安全撤离 "离开办公室"
    (if (有账簿?)
      "你把账簿塞进大衣，和山姆离开办公室。门在身后轻轻合上。"
      "你合上抽屉，和山姆撤走。没拿到账簿，但也没人看见你们。")))

(define-opponent-rule "屋主越来越近"
  (lambda () (not 已结束?))
  (lambda () (推进现场!)))

(define (on-encounter-enter)
  (set! 岗位 '未安排)
  (set! 已结束? #f)
  (搜查 'load! 0)
  (时间 'load! 0)
  (play-dialogue!
    (line "世界" "办公室没人。你要的账簿就在这些抽屉和文件堆里。")
    (line "世界" "山姆等你安排：一起翻找，还是留在门外听动静。")
    (line "世界" "屋主快回来了。每搜一次，留给你们的时间就少一点。")))

(define (on-encounter-collapse) (collapse-result (结果 '倒下)))

(define (搜查节点)
  (let ((抢搜? (and (eq? 岗位 '把风) (= (时间 'current) 2))))
    (node (if 抢搜? "抢搜撤离" "翻找账簿")
      :subtitle (cond
        (抢搜? "好结果搜2并撤离；中搜1、坏搜0，都会被撞见")
        ((eq? 岗位 '搜查) "山姆帮忙：好3、中2、坏1；时间加1")
        (else "独自搜查：好2、中1、坏0；时间加1"))
      :requires (list (req-die))
      :resolve (roll 'sharpness
        (outcome (lambda () (搜查! '坏)))
        (outcome (lambda () (搜查! '中)))
        (outcome (lambda () (搜查! '好)))))))

(define (get-render-data)
  (node "有人把风吗" :anchor "货运公司-办公室"
    :children (append
      (clock-nodes (搜查 'render-data) (时间 'render-data))
      (list (note-node "标注：山姆的岗位" "山姆"
        (cond
          ((eq? 岗位 '未安排) "等你安排。一起搜查提高进展；门外把风提前警告。")
          ((eq? 岗位 '搜查) "帮你搜查，每次多找1格；没人留意门外。")
          ((= (时间 'current) 2) "在门外把风。屋主已到楼下；现在撤，或抢搜一次。")
          (else "在门外把风。屋主接近时，他会提前警告。"))))
      (if (eq? 岗位 '未安排)
        (list
          (instant-action "一起搜查" (lambda () (安排! '搜查)))
          (instant-action "门外把风" (lambda () (安排! '把风))))
        (if (有账簿?) '() (list (搜查节点))))
      (list (instant-action (if (有账簿?) "带账簿离开" "撤离办公室") 撤离!)))))
