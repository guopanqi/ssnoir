
;; 经理的下一份调查邀请与罗科案后的章节衔接。
(define 第二幕节奏
 (let ()

  (define invitation? #f)
  (define case-offered? #f)
  (define (active?) (and (= (第一幕 'act) 2) (not (第二章 'started?))))

  (define (offer-case!)
    (if case-offered? #f (第三幕 'offer!))
    (set! case-offered? #t)
    (next-job!))
  (define (next-job!)
    (set! invitation? #t)
    (第三幕 'start!))
  (define (arrivals-at loc)
    (if (and (equal? loc "世界") (第三幕 'reported?) (not (第二章 'started?)))
      (list (arrival "罗科案后的晚宴邀请" (lambda () (第二章 'start-after-case!))))
    (if (not (active?)) '()
      (append

        (if (and (equal? loc "世界") (第二幕生活 'manager-met?)
                 (第二幕剧院 'case-followup-ready?) (not invitation?)
                 #t)
          (list (arrival "经理的新委托" offer-case!)) '())))))
  (define (nodes-at loc) '())
  (define (dossier-at)
    (append
      (if (and (active?) case-offered? (not invitation?))
        (list (dossier "经理的正式委托" :kind '委托
          :now "经理愿付八十美元，请你查清剧院外黑衣人的身份与活动地点。"
          :steps (list (step "到剧院找经理，决定是否接下黑衣人调查。" #f))
          :where "剧院")) '())))
  (lambda args
    (let ((msg (car args)))
      (cond
        ((eq? msg 'nodes-at) (nodes-at (cadr args)))
        ((eq? msg 'arrivals-at) (arrivals-at (cadr args)))
        ((eq? msg 'dossier) (dossier-at))
        ((eq? msg 'case-offered?) case-offered?)
        ((eq? msg 'save) (list

           (list "invitation" invitation?) (list "case-offered" case-offered?)))
        ((eq? msg 'load!)
          (let ((data (cadr args)))

            (set! invitation? (assoc-get data "invitation" #f))
            (set! case-offered? (assoc-get data "case-offered" invitation?))
            #t))
        (else (error "第二幕节奏：未知消息")))))))
