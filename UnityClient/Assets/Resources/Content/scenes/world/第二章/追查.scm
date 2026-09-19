;; 《谁把她卖给了报纸》：在两条独立渠道放消息，等报纸暴露其中一条，
;; 再从正确地点尾随取件人。尾随失败便永久终止，不开启更深的调查线。
(define 追查
  (let ()
    ;; 未立卷 / 放消息 / 等报纸 / 待看报 / 待尾随 / 已解决 / 失败
    (define 状态 "未立卷")
    (define 已关闭? #f)
    (define 比信更早-立卷? #f)
    (define 记者已查登记? #f)
    (define 上午材料已证实? #f)
    ;; 两根都要填：报纸选中哪个版本才证明泄密来自哪条渠道，缺一根就无从对照。
    ;; 各 6 格（骨架原定）：好结果 +3、中 +2，一根约两到三颗骰，两根共四到六颗。
    (define 剧院消息
      (make-clock "剧院的假消息" 6 'gauge
        "填满后，剧院只会听到她周一去巴尔的摩。"))
    (define 酒店消息
      (make-clock "酒店的假消息" 6 'gauge
        "填满后，酒店只会听到她周二去芝加哥。"))
    (define 等早报
      (make-clock "等早报" 2 'countdown
        "每天减少一格；归零后，门缝下会出现采用其中一个版本的早报。"))
    (define journal (make-journal))

    (define (已立卷?) (not (equal? 状态 "未立卷")))
    (define (已解决?) (equal? 状态 "已解决"))
    (define (失败?) (equal? 状态 "失败"))
    (define (终止?) (or (已解决?) (失败?)))
    (define (还开着?) (and (已立卷?) (not 已关闭?) (not (终止?))))

    (define (开卷!)
      (if (已立卷?)
          #f
          (begin
            (set! 状态 "放消息")
            (journal 'add! "旧照片来自老街，后台细节来自剧院；夜莺要知道是谁把这些东西交给了报纸。"))))

    (define (封卷!)
      (if (or 已关闭? (not (已立卷?)))
          #f
          (begin
            (set! 已关闭? #t)
            (cond
              ((失败?) #f)
              ((and (已解决?) 比信更早-立卷?)
               (journal 'add! "机器进了老街，上午十一点那份材料没能继续追下去。"))
              ((not (已解决?))
               (journal 'add! "机器进了老街，这条没有追完的泄密渠道也就此断了。"))
              (#t #f)))))

    (define (开始等报!)
      (if (and (剧院消息 'full?) (酒店消息 'full?) (equal? 状态 "放消息"))
          (begin
            (set! 状态 "等报纸")
            (等早报 'set! 2)
            (journal 'add! "剧院听到的是巴尔的摩，格兰德酒店听到的是芝加哥。现在只等报纸开口。")
            (spotlight! "诱饵放出去了"
              "两个地方拿到了两个不同的行程。接下来两天不用再做什么；报纸会替泄密的人选出答案。"))
          #f))

    (define (推进消息! clk n)
      (clk 'advance! n)
      (开始等报!))

    (define (消息节点 name subtitle clk skill)
      (node name
        :subtitle subtitle
        :clocks (list (clk 'render-data))
        :requires (list (req-die))
        :resolve (roll skill
          (outcome (lambda ()
              (spend-actor-composure! 'player 1)))
          (outcome (lambda () (推进消息! clk 2)))
          (outcome (lambda () (推进消息! clk 3))))))

    (define (剧院节点)
      (at-anchor "剧院-后台"
        (消息节点 "放给剧院" "让后台只听到巴尔的摩版本" 剧院消息 'social)))

    (define (酒店节点)
      (at-anchor "格兰德酒店-礼宾台"
        (消息节点 "留给酒店" "让礼宾台只听到芝加哥版本" 酒店消息 'sharpness)))

    ;; 等待只由世界日推动。两天倒数归零后仍不自动揭晓，玩家要亲手打开早报。
    (define (on-day-end!)
      (if (equal? 状态 "等报纸")
          (begin
            (等早报 'advance! -1)
            (if (等早报 'empty?)
                (begin
                  (set! 状态 "待看报")
                  (journal 'add! "两天后的早报从门缝下塞了进来。两个版本中有一个已经登了出来。")
                  (spotlight! "等的早报到了"
                    "两天后的早报塞在门缝下面。你还没拆开；回家看看它采用了哪个版本。"))
                #f))
          #f))

    ;; 早报塞在门缝下面（见 on-day-end! 的 journal 与 spotlight），所以落在家里的门口锚点。
    ;; 同链其他卡都有锚（剧院-后台 / 礼宾台 / 后巷 / 编辑部 / 报社），这张漏了就会掉进网格。
    (define (看报节点)
      (anchored-instant-action "看早报" "门口"
        (lambda ()
          (play-dialogue!
            (line "世界" "社会版写着：夜莺将在周一启程前往巴尔的摩。")
            (line "夜莺" "巴尔的摩。这个版本只放给了剧院。")
            (line "尼尔" "消息从剧院出去。我们去看谁来拿。"))
          (set! 状态 "待尾随")
          (journal 'add! "早报采用了巴尔的摩版本。泄密来自剧院；去后巷等取件的人。")
          (spotlight! "剧院后巷"
            "报纸采用了只放给剧院的行程。现在还不知道是谁送的，只知道该去哪里等。"))))

    (define (尾随结果! result)
      (cond
        ((equal? result '跟到报社)
         (play-remote-dialogue!
           (line "世界" "取件人把文件袋放在编辑桌上。编辑顺手翻开递送簿。")
           (line "编辑" "科尔，今天就这些？")
           (line "世界" "旧页一闪而过：首演日，十一点零七分，夜莺。")
           (line "尼尔" "科尔。名字有了。那一页还得回来查。"))
         (set! 状态 "已解决")
         (set! 比信更早-立卷? #t)
         (journal 'add! "取件人名叫科尔。他替剧院、酒店和报社传递材料；递送簿上还有首演日上午 11:07 的一笔夜莺来件。")
         (complete-task! "谁把她卖给了报纸")
         (spotlight! "比信更早"
           "泄密渠道已经查清。递送簿上还有一笔首演日上午的夜莺来件，需要回报社核对。"))
        ((or (equal? result '跟丢) (equal? result '暴露))
         (set! 状态 "失败")
         (journal 'add!
           (if (equal? result '跟丢)
               "你跟丢了剧院的取件人。这条泄密渠道没有留下第二次机会。"
               "剧院的取件人发现了尾随。这条泄密渠道就此封死。"))
         ;; 尾随失败也是经历完；机器进场把没查完的线封掉（封卷!）不发。
         (complete-task! "谁把她卖给了报纸")
         (spotlight! "线断了"
           (if (equal? result '跟丢)
               "取件人消失在人群里。假消息已经用过，他不会再沿同一条路线出现。"
               "取件人认出了尾随。剧院和报社换了传递方式，这条线到这里结束。")))
        (#t (error "追查：尾随交锋返回了未知结果"))))

    (define (尾随节点)
      (at-anchor "剧院-后巷"
       (encounter-action "等取件人"
        (lambda ()
          (start-encounter "尾随取件人" 尾随结果!)))))

    (define (废稿间结果! result)
      (cond
        ((equal? result '证实提前供稿)
         (set! 上午材料已证实? #t)
         (journal 'add! "11:07 的收件封套与 11:35 的第一版校样互相对应；第三封信直到下午 1:20 才在剧院出现。有人提前准备了报道。")
         (complete-task! "比信更早的人")
         (spotlight! "材料早于信"
           "报社上午已经拿到死亡威胁与内部演出安排。下一步是查清谁准备了稿号四一七。"))
        ((equal? result '未完成) #f)
        (#t (error "追查：废稿间交锋返回了未知结果"))))

    (define (核对登记节点)
      (anchored-instant-action "问十一点零七分" "报社-编辑部"
        (lambda ()
          (play-dialogue!
            (line "尼尔" "首演那天十一点零七分送来的东西还在吗？")
            (line "记者" "你连分秒都记住了？让我看看内部登记。")
            (line "世界" "记者沿着旧账页找到那一行。")
            (line "记者" "夜莺，首演之夜。厚文件袋，送到娱乐版四号桌。")
            (line "尼尔" "袋子呢？")
            (line "记者" "报社不是档案馆。刊完的旧料全进地下废稿间。")
            (line "记者" "要是还没清走，就在下面。只是没人替你分好类。"))
          (set! 记者已查登记? #t)
          (journal 'add! "记者核对了内部登记：11:07 的来件是一只厚文件袋，被送往娱乐版四号桌。旧材料已经进入地下废稿间。")
          (spotlight! "地下废稿间"
            "登记只能证明文件袋来过。要知道里面写了什么，只能去地下废稿间找。"))))

    (define (废稿间节点)
      (at-anchor "报社"
       (encounter-action "下废稿间"
        (lambda ()
          (start-encounter "上午十一点零七分" 废稿间结果!)))))

    (define (节点 location)
      ;; 《谁把她卖给了报纸》结案后，《比信更早的人》仍要继续产出节点。
      ;; 只有章节事件真正关闭时才把整条调查从城市里撤下。
      (if 已关闭?
          '()
          (cond
            ((and (equal? 状态 "放消息") (equal? location "剧院")
                  (not (剧院消息 'full?)))
             (list (剧院节点)))
            ((and (equal? 状态 "放消息") (equal? location "格兰德酒店")
                  (not (酒店消息 'full?)))
             (list (酒店节点)))
            ((and (equal? 状态 "待看报") (equal? location "家"))
             (list (看报节点)))
            ((and (equal? 状态 "待尾随") (equal? location "剧院"))
             (list (尾随节点)))
            ((and 比信更早-立卷? (not 上午材料已证实?)
                  (equal? location "报社"))
             (list
               (if 记者已查登记?
                   (废稿间节点)
                   (核对登记节点))))
            (#t '()))))

    (define (当前目标)
      (cond
        ((equal? 状态 "放消息")
         (cond
           ((and (not (剧院消息 'full?)) (not (酒店消息 'full?)))
            "去剧院和格兰德酒店分别放出假消息")
           ((not (剧院消息 'full?)) "去剧院放出巴尔的摩版本")
           (#t "去格兰德酒店留下芝加哥版本")))
        ((equal? 状态 "等报纸") "等两天，看报纸采用哪一个版本")
        ((equal? 状态 "待看报") "回家打开两天后送来的早报")
        ((equal? 状态 "待尾随") "去剧院后巷，等取走夜莺材料的人")
        ((已解决?) "科尔把剧院的材料送进了报社")
        ((失败?) "尾随失败，泄密渠道已经封死")
        (#t "")))

    (define (当前地点)
      (cond
        ((equal? 状态 "放消息")
         (if (剧院消息 'full?) "格兰德酒店" "剧院"))
        ((equal? 状态 "待看报") "家")
        ((equal? 状态 "待尾随") "剧院")
        (#t "")))

    (define (当前时钟)
      (cond
        ((equal? 状态 "放消息")
         (list (剧院消息 'render-data) (酒店消息 'render-data)))
        ((equal? 状态 "等报纸") (list (等早报 'render-data)))
        (#t '())))

    (define (dossier-entries)
      (append
        (if (已立卷?)
            (list (dossier "谁把她卖给了报纸"
                    :kind '委托
                    :status (if (or 已关闭? (终止?)) '了结 '进行中)
                    :steps (list (step "在剧院放一个行程" (剧院消息 'full?))
                                 (step "在格兰德酒店放另一个行程" (酒店消息 'full?))
                                 (step "等早报开口" (member? 状态 (list "待尾随" "已解决" "失败")))
                                 (step "尾随取件人" (终止?)))
                    :now (cond
                           ((已解决?) "科尔把剧院的材料送进报社；泄密渠道已经查清。")
                           ((失败?) "尾随失败，泄密渠道已经封死。")
                           (已关闭? "机器进了老街，这条没有查完的线已经断了。")
                           (#t (当前目标)))
                    :where (if (or 已关闭? (终止?)) "" (当前地点))
                    :clocks (当前时钟)
                    :log (journal 'render-data)))
            '())
        (if 比信更早-立卷?
            (list (dossier "比信更早的人"
                    :kind '委托
                    :status (if (or 已关闭? 上午材料已证实?) '了结 '进行中)
                    :steps (list (step "去报社核对十一点零七分的登记" 记者已查登记?)
                                 (step "在废稿间找到那份材料" 上午材料已证实?))
                    :now (cond
                           (已关闭? "首演日上午那份材料没能继续追下去。")
                           (上午材料已证实? "两件材料证明报道早于第三封信；供稿人仍然不明。")
                           (记者已查登记? "去报社地下废稿间寻找十一点零七分的材料")
                           (#t "去报社核对首演日上午十一点零七分的登记"))
                    :where (if (or 已关闭? 上午材料已证实?) "" "报社")
                    :log '()))
            '())))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (节点 (cadr args)))
          ((equal? msg 'arrivals-at) '())
          ((equal? msg 'dossier) (dossier-entries))
          ((equal? msg 'on-day-end!) (on-day-end!))
          ((equal? msg 'open!) (开卷!))
          ((equal? msg 'close!) (封卷!))
          ((equal? msg 'pressure) 0)
          ((equal? msg 'has-schedule?) #f)
          ((equal? msg 'knows-truth?) 比信更早-立卷?)
          ((equal? msg 'mystery-open?) 比信更早-立卷?)
          ((equal? msg 'save)
           (list (list "state" 状态)
                 (list "theater-bait" (剧院消息 'save))
                 (list "hotel-bait" (酒店消息 'save))
                 (list "newspaper-wait" (等早报 'save))
                 (list "mystery-open" (if 比信更早-立卷? 1 0))
                 (list "reporter-checked-ledger" (if 记者已查登记? 1 0))
                 (list "morning-material-proved" (if 上午材料已证实? 1 0))
                 (list "closed" (if 已关闭? 1 0))
                 (list "journal" (journal 'save))))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! 状态 (assoc-get data "state" "未立卷"))
             (剧院消息 'load! (assoc-get data "theater-bait" 0))
             (酒店消息 'load! (assoc-get data "hotel-bait" 0))
             (等早报 'load! (assoc-get data "newspaper-wait" 0))
             (set! 比信更早-立卷? (= (assoc-get data "mystery-open" 0) 1))
             (set! 记者已查登记? (= (assoc-get data "reporter-checked-ledger" 0) 1))
             (set! 上午材料已证实? (= (assoc-get data "morning-material-proved" 0) 1))
             (set! 已关闭? (= (assoc-get data "closed" 0) 1))
             (journal 'load! (assoc-get data "journal" '()))))
          (else (error "追查：收到未知消息")))))))
