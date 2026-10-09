;; 改编网页第三场“邮箱”：同场相见、投信、退到报摊。保留正式筹款台词与配音。
(load-file "scripts/theatre/勒索街角.scm")
(define (邮箱相见-演出 筹款)
  (define (说 who text voice seconds)
    (define x (if (equal? who "尼尔") 840 560))
    (theatre-during
      (theatre-caption-for who text seconds
        (if (equal? who "尼尔") "#F0CF8A" "#8FD9D0"))
      (theatre-sound "对白" (string-append "Voices/" voice) #f 1 0)
      (theatre-tween "焦点" 'x (+ 800 (* (- x 800) .5)) .4 'smooth)))
  (define 对白
    (cond
      ((equal? 筹款 '足额)
       (theatre-sequence
        (说 "夜莺" "一百整。" "三封信/码头/投信/足额/01/夜莺" 1.15)
        (说 "尼尔" "你把它放到邮箱里就走，我会在这儿盯着。" "三封信/码头/投信/足额/01/尼尔" 4.03)))
      ((equal? 筹款 '她补)
       (theatre-sequence
        (说 "尼尔" "我已经尽力了，还是差一点。" "三封信/码头/投信/她补/01/尼尔" 2.91)
        (说 "夜莺" "我还有一点儿..." "三封信/码头/投信/她补/01/夜莺" 2.11)
        (说 "尼尔" "你上次不是说，那已经是全部了？" "三封信/码头/投信/她补/02/尼尔" 3.71)
        (说 "夜莺" "我...我总不能..." "三封信/码头/投信/她补/02/夜莺" 2.91)
        (说 "尼尔" "别说了，你去吧。放进邮箱就走，我会在这儿盯着。" "三封信/码头/投信/她补/03/尼尔" 4.83)))
      ((equal? 筹款 '塞报纸)
       (theatre-sequence
        (说 "尼尔" "我已经尽力了，还是差一点。就这样吧，我们用旧报纸塞进去。" "三封信/码头/投信/塞报纸/01/尼尔" 5.95)
        (说 "夜莺" "他回去会数的。" "三封信/码头/投信/塞报纸/01/夜莺" 1.79)
        (说 "尼尔" "那就让他数，别担心。你去吧，你把它放邮箱里就走，我会在这儿盯着。" "三封信/码头/投信/塞报纸/02/尼尔" 6.43)))
      (else (error "邮箱相见：未知筹款情况"))))

  (play-theatre!
    (theatre-scene 1600 900 "#07090E"
      (append (勒索街角-布景)
        (list
          (theatre-with (theatre-image "夜莺" "" "Portraits/Neon/夜莺_低头" -160 790 308 308 "") 'opacity 0)
          (theatre-with
            (theatre-with (theatre-image "尼尔" "" "Portraits/Neon/尼尔_抱臂" 1760 790 308 308 "") 'scale-x -1)
            'opacity 0)
          (theatre-with (theatre-group "信封" "" 610 640) 'opacity 0)
          (theatre-polygon "信封纸" "信封" "#E8E2D2" '((-28 -18) (28 -18) (28 18) (-28 18)))
          (theatre-line "信封折线" "信封" "#8A8372" 1.5 '((-28 -18) (0 6) (28 -18))))
        (勒索街角-报纸)))
    ;; 两人进场同时完成，不等画完布景再走。
    (theatre-parallel
      (theatre-tween "夜莺" 'opacity 1 .1)
      (theatre-tween "尼尔" 'opacity 1 .1)
      (theatre-tween "夜莺" 'x 560 1.25 'smooth)
      (theatre-tween "尼尔" 'x 840 1.25 'smooth)
      (theatre-sound "相见脚步" "StageSounds/老街酒馆/脚步" #f .35 0))
    (theatre-tween "信封" 'opacity 1 .1)
    对白
    (theatre-clear-caption)
    (theatre-image-to "夜莺" "Portraits/Neon/夜莺_背身")
    (theatre-parallel
      (theatre-tween "夜莺" 'x 615 .35)
      (theatre-tween "信封" 'x 700 .35)
      (theatre-tween "信封" 'y 596 .35)
      (theatre-tween "焦点" 'x 700 .35))
    (theatre-parallel
      (theatre-tween "信封" 'opacity 0 .18)
      (theatre-sound "投信轻响" "StageSounds/勒索信/投信" #f .7 -.2))
    ;; 她离开与尼尔退回报摊重叠；不追加旁白或空等。
    (theatre-parallel
      (theatre-sequence
        (theatre-tween "夜莺" 'scale-x -1 .05)
        (theatre-tween "夜莺" 'x -160 1.25 'smooth))
      (theatre-sequence
        (theatre-image-to "尼尔" "Portraits/Neon/尼尔_靠墙")
        (theatre-tween "尼尔" 'scale-x 1 .05)
        (theatre-tween "尼尔" 'x 1070 .7 'smooth)
        (theatre-parallel
          (theatre-tween "报纸" 'opacity 1 .2)
          (theatre-sound "报纸展开" "StageSounds/勒索信/纸响" #f .65 .35)))
      (theatre-tween "焦点" 'x 1000 .8 'smooth)
      (theatre-sound "离场脚步" "StageSounds/老街酒馆/脚步" #f .3 -.3))))
