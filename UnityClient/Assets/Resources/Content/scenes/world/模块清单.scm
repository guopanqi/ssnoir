;; scenes/world/模块清单.scm - 世界的装配表
;;
;; 一张表说清楚：谁有状态要存、谁往卷宗里写一条、谁往地点投卡片、谁有入场叙事。
;; **加一个模块只改这一张表**，不再是 world.scm 里三处名单加八个地点文件各点一次名。
;;
;; 表是显式的：不做自动扫描，不做事件总线，也没有插件。谁在往世界里放东西，
;; 读这一页就该看得全；少了一样东西，第一个该看的地方也是这里。
;;
;; 角色只有四个：
;;   存档  它有自己的状态，要进存档（键名就是第一列，改键名＝换一份存档）
;;   卷宗  它有一条故事线要摆进卷宗
;;   节点  它往地点投卡片，答得了 'nodes-at
;;   入场  它有走进某个地点时的一拍叙事，答得了 'arrivals-at
;; 没有的角色就不写。**不要求任何模块实现它用不上的空接口。**
;;
;; 顺序＝这张表的顺序＝卡片在地点里的先后。它是稳定的登记顺序，不按章节排：
;; 第二章想让自己的卡排在生活内容前面，改这张表的位置就行，不必跟第一章讲理。
;; 节点名全局唯一由引擎强制，所以顺序只影响呈现，不影响正确性。
;;
;; 写成函数而不是常量：表里的闭包在这个文件加载时还不存在，名字要到用的时候才解析。
;; 于是这一页可以第一个加载，地点文件也可以在任何位置引用下面的 纯投射地点。
(define (世界模块表)
  (list
    ;;    存档键                  模块                  角色
    (list "three-letters"        three-letters        '(存档 卷宗 节点 入场))
    (list "eddie"                eddie                '(存档 卷宗 节点 入场))
    (list "frank"                frank                '(存档 卷宗 节点 入场))
    (list "lin"                  lin                  '(存档 卷宗 节点 入场))
    (list "baines"               baines               '(存档 卷宗 节点 入场))
    (list "walter"               walter               '(存档 卷宗 节点 入场))
    (list "nightingale"          nightingale          '(存档))
    (list "chapter2"             第二章                '(存档 卷宗 节点 入场))
    (list "home"                 home                 '(存档))
    (list "dock"                 dock                 '(存档))
    (list "old-street-tavern"    old-street-tavern    '(存档))
    (list "clinic"               clinic               '(存档))
    (list "police-station"       police-station       '(存档))
    (list "freight-company"      freight-company      '(存档))
    (list "residential-district" residential-district '(存档))
    (list "theater"              theater              '(存档))
    (list "board"                board                '(存档))
    (list "grand-hotel"          grand-hotel          '(存档))
    (list "port-technical-zone"  port-technical-zone  '(存档))
    (list "newsroom"             newsroom             '(存档))))

(define (模块-键 row)  (car row))
(define (模块-本体 row) (cadr row))
(define (模块-有角色? row 角色) (member? 角色 (caddr row)))

(define (担任 角色)
  (map 模块-本体 (filter (lambda (row) (模块-有角色? row 角色)) (世界模块表))))

;; ── 地点内容 ─────────────────────────────────────
;; 地点只拥有自己的常驻生活内容和空间组织；故事的卡片从这里汇总后交给它。
;; **新增一件发生在剧院的事，改的是上面那张表，不是剧院。**
;;
;; 地点名就是 place 的正式名称（"家" "码头" "老街酒馆" "码头居民区" "三号货栈工棚"）。
;; 提供者、卷宗指路和地点节点共用同一个名字，不再维护另一套中文短名。
;; 提供者只回它真的有东西的那些地点，其余回 '()。
;;
;; 答不上这条消息时（多半是新登记的模块忘了实现）当场报清楚，
;; 别让它变成 append 收到 #f 之后那句看不懂的类型错误。
(define (收集内容 providers msg location)
  (if (null? providers)
      '()
      (let ((得到 ((car providers) msg location)))
        (if (list? 得到)
            (append 得到 (收集内容 (cdr providers) msg location))
            (error (string-append "模块清单：某个提供者答不了 " (symbol->string msg)
                                  " \"" location "\"。它要么实现这条消息（没有内容就回 '()），"
                                  "要么在表里去掉对应的角色。"))))))

(define (地点节点 location) (收集内容 (担任 '节点) 'nodes-at location))
(define (地点入场 location) (收集内容 (担任 '入场) 'arrivals-at location))

;; ── 只负责投射的地点 ─────────────────────────────
;; 剧院、警察局、居民区都是同一种东西：它们没有自己的生活内容，
;; 只是一个让故事落地的空间。这类地点写成一行，不必各自抄一遍锚点兜底和汇总。
;;
;; 锚点兜底的意思是：没有显式声明落点的卡收回地点主点，不掉进网格。
;; 有自己生活内容的地点（家、码头、酒馆、工棚）不用这个，它们本来就要自己组织空间。
;; 主锚点可以不给：第二章新开的地方在 Unity 里还没有模型锚点，
;; 硬塞一个不存在的锚点会让客户端当场中断。不给锚点的地点，卡片走网格布局——
;; 那是「这个动作没有空间落点」时的正常路径，等模型做出来再补上锚点。
(define (纯投射地点 正式名 . 锚点)
  (define 主锚点 (if (null? 锚点) "" (car 锚点)))
  (define (fallback-anchor node-data)
    (if (or (equal? 主锚点 "") (member? :anchor node-data))
        node-data
        (append node-data (list :anchor 主锚点))))
  (lambda args
    (let ((msg (car args)))
      (cond
        ((equal? msg 'render-data)
         (list (place 正式名
                 :anchor (if (equal? 主锚点 "") 正式名 主锚点)
                 :children (map fallback-anchor (地点节点 正式名))
                 :arrivals (地点入场 正式名))))
        ((equal? msg 'save) '())
        ((equal? msg 'load!) #t)
        (else #f)))))

;; ── 卷宗与存档 ───────────────────────────────────
;; 世界只收集与排序，不解释故事。顺序＝表的顺序，客户端在此之上把「了结」的沉到最后。
(define (模块卷宗)
  (收集内容-无参 (担任 '卷宗) 'dossier))

(define (收集内容-无参 providers msg)
  (if (null? providers)
      '()
      (append ((car providers) msg) (收集内容-无参 (cdr providers) msg))))

(define (存档模块) (filter (lambda (row) (模块-有角色? row '存档)) (世界模块表)))

(define (模块存档)
  (map (lambda (row) (list (模块-键 row) ((模块-本体 row) 'save))) (存档模块)))

(define (模块读档! data)
  (map (lambda (row)
         ((模块-本体 row) 'load! (assoc-get data (模块-键 row) '())))
       (存档模块))
  #t)
