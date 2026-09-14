;; scenes/world/第二章/谁把她卖给了报纸.scm - 锚点二《封面上的夜莺》
;;
;; 晚宴之后几天。夜莺本来约好要接受一次正式专访，结果专访还没做，
;; 一家报纸抢先登出一篇非常漂亮的文章：《逃出老街的夜莺》。
;;
;; 她生气不是因为内容假，**恰恰因为里面很多东西是真的**：她以前的名字、
;; 她和莱恩的关系、老街的一张旧照片、首演夜后台的一些细节。
;; 这些她一样都没告诉过记者。所以她只问一句：「谁给他们的？」
;;
;; 这一拍是主轴上的必经事件：到日子就发生，堵住休息直到玩家到场看完这一幕。
;; 它把疑点和第一条线索直接放进卷宗，不要求玩家先选择接受或拒绝。
;; 要不要去挖、挖到哪一步，体现在玩家之后是否投入行动（见 world/第二章/追查.scm）。
;;
;; 主轴的规矩照旧：必经拍不花骰。花骰的是后面那条支线上的每一次追查。
(define 封面上的夜莺
  (let ()
    (define 开门-第几天 6)

    ;; 未开始 / 待去 / 已发生
    (define 状态 "未开始")

    (define (待去?) (equal? 状态 "待去"))
    (define (开门日) (第二章 'day-of 开门-第几天))

    ;; ── 必经拍：到日子就堵着 ─────────────────────────
    (define (sync-blockers!)
      (rest-release! "第二章/报纸")
      (if (待去?)
          (rest-block! "第二章/报纸" "夜莺让人来叫你" "剧院" "见夜莺")
          #f))

    (define (on-day-end!)
      (if (and (equal? 状态 "未开始") (第二章 'started?)
               (>= world-day (开门日)))
          (begin
            (set! 状态 "待去")
            (sync-blockers!)
            ((第二章 'journal) 'add! "报纸登了一篇《逃出老街的夜莺》。里面的东西她一样也没说过。")
            (spotlight! "《逃出老街的夜莺》"
              "今早的报纸抢在专访前登了她的故事。写得很漂亮，也写了太多不该有人知道的事。"))
          #f))

    ;; ── 那一场 ──────────────────────────────────────
    (define (node-meet)
      (anchored-instant-action "见夜莺" "夜莺@剧院"
        (lambda ()
          (play-dialogue!
            (line "世界" "化妆间的镜子前摊着那张报纸，边角被捏皱了。")
            (line "夜莺" "我以前的名字在上面。谁会知道我以前的名字？")
            (line "尼尔" "老街上很多人知道。")
            (line "夜莺" "老街上没人知道我第三首歌之后从左边下台。")
            (line "夜莺" "那是后台的事。那天在后台的人，一只手数得完。")
            (line "世界" "她把报纸推到你面前，手指压在那张老照片上。")
            (line "夜莺" "谁给他们的？"))
          (set! 状态 "已发生")
          (sync-blockers!)
          (追查 'open!)
          (spotlight! "谁给他们的"
            "旧照片来自老街，后台细节来自剧院。有人把两边的东西送进了同一篇文章。"))))

    (define (nodes-at location)
      (if (equal? location "剧院")
          (if (待去?) (list (node-meet)) '())
          '()))

    (lambda args
      (let ((msg (car args)))
        (cond
          ((equal? msg 'nodes-at) (nodes-at (cadr args)))
          ((equal? msg 'arrivals-at) '())
          ((equal? msg 'dossier) '())
          ((equal? msg 'on-day-end!) (on-day-end!))
          ((equal? msg 'sync-blockers!) (sync-blockers!))
          ((equal? msg 'state) 状态)
          ;; 文章已经出街——追查那条线从这一刻起才有东西可查。
          ((equal? msg 'published?) (not (equal? 状态 "未开始")))
          ((equal? msg 'now)
           (cond ((待去?) "去剧院见夜莺。今早的报纸出事了")
                 ((equal? 状态 "已发生") "那篇文章留下了一条可以追查的线索")
                 (#t "夜莺那边这几天没有事")))
          ((equal? msg 'where) (if (待去?) "剧院" ""))
          ((equal? msg 'clocks) '())
          ((equal? msg 'save) (list (list "state" 状态)))
          ((equal? msg 'load!)
           (let ((data (cadr args)))
             (set! 状态 (assoc-get data "state" "未开始"))))
          ;; 调试用：当作那一场已经过去，疑点已经进入卷宗。
          ((equal? msg 'debug-settle!)
           (set! 状态 "已发生")
           (sync-blockers!)
           (追查 'open!))
          (else (error "封面上的夜莺：收到未知消息")))))))
