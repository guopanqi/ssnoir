;; scenes/encounters/追击黑衣人.scm - Chase encounter

(define (get-render-data)
  (list
    (container "追击途中"
      (list
        (observe-action "阴暗小巷" "黑衣人的身影在前方一闪而过，地上留有未干的雨水痕迹。")
        (instant-action "成功追击"
                        (lambda ()
                          ;; 1. 修改世界全局状态：标记任务成功，并将任务阶段推进到 2
                          (set-global! 'chase-success #t)
                          (set-global! 'theater-mission-stage 2)
                          (notify! "你成功截住了黑衣人，获取了重要线索！")
                          
                          ;; 2. 结束交锋，返回大地图世界
                          (end-encounter)))))))
