;; scenes/club.scm - Elites Club Scene

;; ── Node Definitions ──────────────────────────
(define (node-go-home)
  (instant-action "回到街区"
    (lambda ()
      (set-global! 'location "home"))))

(define (node-club-desc)
  (observe-action "俱乐部大厅" "金碧辉煌的走廊铺着红色地毯，雪茄的烟雾在水晶吊灯下弥漫。只有真正的上流权贵才能涉足于此。"))

(define (node-mayor-vip)
  (observe-action "市长贵宾室" "市政厅的高官们正聚在里面轻声商讨着什么，门外的保镖警惕地看着你。"))

;; ── Render Data Entrypoint ────────────────────
(define (get-render-data)
  (list
    (container "权贵俱乐部"
      (list
        (node-club-desc)
        (node-mayor-vip)
        (node-go-home)))))
