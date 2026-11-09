;; 第一章第三幕《银色打火机》：城市事件与人物自主进展。
;; 城市循线后：货运大楼潜入或支付150美元，各自结案；暂不启用城市期限。
;; 所有城市行动使用现有骰子/金钱/冷静系统；不另建任务或日程系统。
;; 普通配角在所属事件模块就近声明；对白不依赖尚不存在的配音资产。
(define 第三幕-罗科 (speaker "第三幕罗科" :name "罗科" :appearance '成年男性))
(define 第三幕-来客 (speaker "第三幕来客" :name "来客" :appearance '成年男性))
(define 第三幕-贵客 (speaker "第三幕贵客" :name "贵客" :appearance '成年男性))
(define 第三幕-收购代表 (speaker "第三幕收购代表" :name "收购代表" :appearance '成年男性))
(define 第三幕-汤米 (speaker "第三幕汤米" :name "汤米" :appearance '成年男性))
(define 第三幕
 (let ()
  (define started? #f)
  (define start-day 0)
  (define case-state "未开始")     ; 调查／补救／结案
  (define inquiry-stage "守候") ; 守候／跟车／记录／核查／旧事／公司／已查清
  (define inquiry-day 0)          ; 下次可守候或领取核查结果的世界日
  (define rocco-contract? #f)
  (define rocco-ledger? #f)
  (define rocco-threat? #f)
  (define rocco-warehouse? #f)
  (define papers-leaked? #f)
  (define nightingale-protected? #f)
  (define settlement-fund 0)
  (define manager-advance 0)
  (define case-expense 0)
  (define warehouse-stopped? #f)
  (define warehouse-private-exposed? #f)
  (define warehouse-ledger? #f)
  (define warehouse-threat? #f)
  (define warehouse-material? #f)
  (define 金钱协议价 150)
  (define tommy-work (make-clock "照应贵客牌局" 3 'gauge "问清来意，选择怎样处理讨债，再陪汤米交涉。" :beneficial-direction 'increase))
  (define report? #f)
  (define nightingale-night "未邀请") ; 未邀请／待答复／已赴约／婉拒
  (define lucy-met? #f)
  (define lucy-material "未谈") ; 未谈／入稿／寄后
  (define lucy-mailed? #f)
  (define lucy-after? #f)

  (define tommy-met? #f)
  (define tommy-met-day 0)
  (define tommy-invited? #f)
  (define tommy-intel? #f)
  (define tommy-hosted? #f)
  (define tommy-approach "未定") ; 未定／私下／当众
  (define tommy-advanced? #f)
  (define tommy-after? #f)
  (define (active?) (and started? (not (第二章 'started?))))
  (define (city-active?) (and (>= (第一幕 'act) 2) (not (第二章 'started?))))
  ;; 城市事件各有自己的排布，暂用现有试唱、码头测绘日程附近的值；不跟接案日一起重置。

  (define 贵客准备日 17)
  (define 贵客散场日 21)
  (define (case-open?) (and (active?) (member? case-state '("调查" "补救"))))
  (define (ensure-case!) (if (case-open?) #t (error "第三幕：案件当前不可调查")))
  (define (start!)
    (if (or started? (not (= (第一幕 'act) 2))
            (not (第二幕生活 'manager-met?)))
      (error "第三幕：不能重复或提前开始") #t)
    (set! started? #t)
    (set! start-day world-day)
    (set! case-state "调查")
    (set! inquiry-stage "守候")
    (set! inquiry-day world-day)
    (第一幕 'begin-third-act!)
    (begin))
  (define (offer!)
    (play-dialogue!
    (line "世界" "街角的报亭旁，剧院经理的黑色轿车缓缓停在路边。车窗摇下一半，经理朝你招了招手。")
    (line "经理" "来得正好，尼尔。关于跟着夜莺小姐的那个人，我托人摸了一点边。")
    (line "尼尔" "查出是谁了吗？")
    (line "经理" "还没摸清真名实姓，但看路数不像是普通小流氓，也不像碰巧路过。剧院最近在筹备新戏，绝不能闹出丑闻。")
    (line "尼尔" "你想让我怎么查？")
    (line "经理" "这人很滑头，这两天常在剧院后街和老旅馆一带晃悠。你去蹲守几天，先把人和他坐的车记下来，顺藤摸瓜摸清他的底细，把麻烦彻底按下去。")
    (line "经理" "这单活我出八十美元。要是需要跑远路，剧院后院有辆备用轿车，你可以借去开。怎么样？")
    (line "尼尔" "好，我先记清人和车。车钥匙在哪儿拿？")))
  (define (preparing?) (and (case-open?) rocco-warehouse?))
  (define (ensure-preparing!)
    (if (preparing?) #t (error "罗科：尚未查清公司内的材料与进入机会")))
  (define (ensure-inquiry! stage)
    (ensure-case!)
    (if (equal? inquiry-stage stage) #t (error "第三幕：调查阶段不符")))
  (define (tail-result! result)
    (ensure-inquiry! "跟车")
    (if (member? result '(取得识别线索 跟踪暴露 线索不足)) #t
      (error "第三幕：未知跟车结果"))
    (if (eq? result '取得识别线索)
      (begin
        (set! inquiry-stage "记录")
        (play-dialogue! (line "尼尔" "车牌和人的样子都记下了。回去让经理查一查。")))
      (begin
        (set! inquiry-stage "守候")
        (set! inquiry-day (+ world-day 1))
        (play-dialogue! (line "世界" "这次没记全。尼尔把零散记录收好；今晚继续追只会更显眼，只能改天再等他出现。")))))
  (define (wait-for-car!)
    (ensure-inquiry! "守候")
    (if (>= world-day inquiry-day) #t (error "第三幕：今天已错过跟车机会"))
    (set! inquiry-stage "跟车")
    (play-dialogue!
      (line "世界" "尼尔把借来的车停在剧院附近。雨下起来时，黑大衣从侧门对面的屋檐下走出，坐进一辆轿车。")
      (line "尼尔" "就是他。"))
    (start-scenario "雨夜跟车（识别版）" tail-result!))
  (define (give-car-record!)
    (ensure-inquiry! "记录")
    (set! inquiry-stage "核查")
    (set! inquiry-day (+ world-day 1))
    (play-dialogue!
      (line "世界" "尼尔把车牌、车型和男子的外貌记录交给经理。")
      (line "经理" "我去问车行和做货运的熟人。明天回来找我。")))
  (define (manager-findings!)
    (ensure-inquiry! "核查")
    (if (>= world-day inquiry-day) #t (error "第三幕：核查结果尚未回来"))
    (set! inquiry-stage "旧事")
    (play-dialogue!
      (line "经理" "车行核过了，车已经卖给一个叫罗科的人。人也对得上。他在货运联合公司租了办公室，收信、寄东西都走那里。")
      (line "尼尔" "罗科。这个名字，夜莺认识吗？")
      (line "经理" "去问问她。我把地址给你。")))
  (define (nightingale-history!)
    (ensure-inquiry! "旧事")
    (set! inquiry-stage "公司")
    (set! rocco-contract? #t)
    (play-dialogue!
      (line "尼尔" "那辆车的主人叫罗科。在货运联合公司租着办公室。")
      (line "夜莺" "罗科？是我从前的经纪人。我没看清街对面的人，没想到他已经找到这里。")
      (line "夜莺" "他替我找过演出，垫过服装和旅费。我签过合同，也付过钱。后来只要我接到工作，他就说还有一笔账没结。")
      (line "夜莺" "合同早到期了。他还留着我的信和旧演出的材料，说不按他的数付钱，就拿去给报社。")
      (line "尼尔" "你还留着合同吗？")
      (line "世界" "夜莺拿出自己的那份。代理期限已经届满，垫款和已付报酬却需要另行核对。")
      (line "尼尔" "我去公司查查。他租了什么地方，东西放在哪里，又准备怎么寄出去。")))
  (define (investigate-company!)
    (ensure-inquiry! "公司")
    (set! inquiry-stage "已查清")
    (set! rocco-warehouse? #t)
    (play-dialogue!
      (line "世界" "尼尔以询问寄存和临时装卸为由，查了楼里的租户名录与收发安排。罗科租用三楼一间办公室，也租着旁边档案室的柜位。")
      (line "世界" "收发处有他预约的外寄包裹：旧账、信件和准备交给小报的指控稿，封好后会由公司的货运或邮寄渠道送出。")
      (line "尼尔" "他不是这里的老板。装卸场还在招人，搬运工核完货，要把运单送上三楼。")
      (line "尼尔" "可以借这个机会进去找材料，再和他谈。也可以筹一百五十美元，请经理到货运公司见证签约，把钱和停止纠缠的条件一起结清。")))
  (define (clear-case-items!)
    (if (> settlement-fund 0) (begin
      (remove-item! "罗科结算款" 1)
      (add-item! "金钱" settlement-fund)
      (set! settlement-fund 0)) #f)
    (if (> (item-count "罗科付款凭据") 0) (remove-item! "罗科付款凭据" 1) #f)
    (if (> (item-count "罗科恐吓证词") 0) (remove-item! "罗科恐吓证词" 1) #f))
  (define (resolve-case!)
    (clear-case-items!)
    (set! warehouse-stopped? #t)
    (set! case-state "结案"))
  (define (money-agreement!)
    (ensure-preparing!)
    ;; 150美元全部由资源槽支付；不在本路线组织预支或其他筹款方式。
    (play-dialogue!
      (line "世界" "经理来到货运公司，在罗科租用的办公室里见证双方签约。")
      (line "经理" "一百五十美元，一次结清，解除代理关系，以后不许再纠缠。条款都写在这里。")
      (line 第三幕-罗科 "钱到手，我签字。")
      (line "尼尔" "收款、解除关系、停止接触，不再把私人材料交给报社。都签清楚。")
      (line "世界" "经理在场见证。罗科签下协议，清点一百五十美元，把手里的私人材料交还，收据也签了字。"))
    (resolve-case!))
  (define (infiltration-result! result)
    (ensure-preparing!)
    (cond
      ((equal? result "问题被解决") (resolve-case!))
      ((equal? result "失败")
        (play-dialogue! (line "世界" "潜入没能办成，罗科的纠缠还没有结束。尼尔仍可以另找机会进入货运大楼，或筹钱来货运公司达成协议。")))
      (else (error "货运大楼潜入：未知结果"))))
  (define (enter-infiltration!)
    (ensure-preparing!)
    (start-scenario "货运大楼潜入" infiltration-result!))
  (define (confirm-report!)
    (if (and (active?) (equal? case-state "结案") (not report?))
      #t (error "第三幕：结案报告状态非法"))
    (set! report? #t)
    (add-item! "金钱" (- 80 manager-advance case-expense))
    (begin
(play-dialogue!
    (line "世界" "大剧院办公室里，你把整理好的调查记录推到经理面前。")
    (line "尼尔" "查清楚了。跟着夜莺的是她早年在外地签过的旧经纪人，叫罗科。他手里攥着当年的苛刻旧合同和一些私人材料，想趁夜莺在大剧院出名狠敲一笔。现在人已经让他滚出城了，离城记录也核实了。"))
(cond
  (warehouse-private-exposed? (play-dialogue!
    (line "经理" "（合上报告，神色有些凝重）人走了就好。只是那几封私人信件被报社的记者瞄到了几眼，这两天夜莺小姐少不得要受点流言蜚语的委屈。我还得去跟报社谈谈，你也别太自责，至少他没法再拿着原件长期勒索。")
    (line "尼尔" "……我会再去看看夜莺的情况。")))
  (papers-leaked? (play-dialogue!
    (line "经理" "（看着报告，指尖在桌面上轻轻叩击，叹了口气）罗科虽然走了，但还是慢了半步……有些旧合同的风声已经传进小报耳朵里了。不过这不怪你，面对那种无赖，能把人逼走已经是万幸。剩下的口水官司，剧院会出面摆平。")
    (line "尼尔" "抱歉，让他钻了空子。")
    (line "经理" "街面上的事哪有十全十美的？你已经把最棘手的钉子拔掉了。")))
  (else (play-dialogue!
    (line "经理" "（翻看完手里的材料，长舒了一口气，把它们锁进抽屉）干得非常漂亮，尼尔。东西全截下来了，私人材料没有再流到报社。你不仅帮了夜莺小姐，也保住了大剧院的招牌。")
    (line "尼尔" "她不用再躲着他散场了。"))))
(play-dialogue!
    (line "经理" "（拉开抽屉，取出信封推过来）按之前说好的八十美元总账，扣除之前的预支和代付结算款，这是剩下的尾款，你点点。")
    (line "尼尔" "（收起信封）谢了，经理。"))))
  (define (invite-private-night!)
    (if (and report? (equal? nightingale-night "未邀请")) #t
      (error "第三幕：当前不能发出私人邀约"))
    (set! nightingale-night "待答复")
    (play-dialogue!
      (line "夜莺" "尼尔，陪我去河边走走，好吗？不用替我看着街角，也不用谈罗科。")
      (line "夜莺" "我带了点吃的。你要是愿意，我们就走。")))
  (define (decline-private-night!)
    (if (and report? (equal? nightingale-night "待答复")) #t
      (error "第三幕：没有待答复的私人邀约"))
    (set! nightingale-night "婉拒")
    (play-dialogue!
      (line "尼尔" "谢谢你。今天我想一个人待一会儿。")
      (line "夜莺" "好。那我就不拉着你了。回去好好歇歇。")))
  (define (private-night!)
    (if (and report? (equal? nightingale-night "待答复")) #t (error "第三幕：私人夜晚状态非法"))
    ;; 接受后立即赴约，不另留跨日承诺或额外阻塞。
    (set! nightingale-night "已赴约")
    (play-dialogue!
      (line "世界" "夜莺带尼尔走到河边。她没有穿演出服，只在外套里塞了一包面包和两只纸杯。")
      (line "夜莺" "今天没有人跟着我们。你看，我连后面都没回头。")
      (line "尼尔" "你可以一直这样走。")
      (line "夜莺" "他们把我放进节目单里的一个小角落。我还不服气。你知道吗？我想站在正中间。")
      (line "尼尔" "我知道。")
      (line "夜莺" "你说，我会不会有一天走到哪儿，都有人认得我？")
      (line "世界" "河对岸的剧院亮着灯。她把纸杯递给尼尔，手指在他的手背上停了片刻。")))
  ;; 写作拥有者只引用老人经历是否完成，不接管其四段进度。
  (define (lucy-mail-day) (+ (第二幕 'lucy-writing-day) 6))
  (define (lucy-material-pending?)
    (and (两杯咖啡 'completed?) (equal? lucy-material "未谈")))
  (define (lucy-meet!)
    (set! lucy-met? #t)
    (play-dialogue!
      (line "世界" "露西把柜台上的纸翻过来。背面是一段打过又改了的故事，已经写了好几页。")
      (line "尼尔" (if (第二幕 'lucy-class-talk?)
        "这回不是给我的事务所打招牌了？" "这也是打字练习？"))
      (line "露西" (if (> (第二幕 'lucy-enrolled-day) 0)
        "练习归练习。这几页是我自己写的故事。"
        "课本总不能读一辈子。我自己试着写点东西，打不顺的地方就用手写。"))
      (line "露西" (if lucy-mailed?
        "这一篇已经寄了。下一篇写什么，还没想好。"
        "里面有个侦探。不过写着写着，就不知道该让他遇上什么人了。"))
      (line "露西" "你在店里要是碰上什么怪事，记得讲给我听。天天端咖啡，总该有点用处。")
      (line "尼尔" "行。我替你留意。")))
  (define (lucy-share!)
    (if (and (city-active?) lucy-met? (not lucy-after?)
             (第二幕 'lucy-at-cafe?) (lucy-material-pending?))
      #t (error "露西素材：当前不能讲述老人经历"))
    (set! lucy-material (if lucy-mailed? "寄后" "入稿"))
    (play-dialogue!
      (line "尼尔" "你说要找些事情写。我见着一件。窗边那个老人，每次都要两杯咖啡。")
      (line "世界" "尼尔说起老人让座、窗外的街道，还有那场没说清的父子争吵。")
      (line "尼尔" "今天他儿子来了。他们什么也没说，喝完就一起走了。")
      (line "露西" "两杯都喝完了？")
      (line "尼尔" "都喝完了。")
      (line "露西" (if lucy-mailed?
        "这一篇已经寄出去了。这件事我留着，下回写。"
        "等等，我找支铅笔。侦探在窗边看到这一幕，比我原来写的那段好。"))
      (line "世界" "她抽过一张收据，在背面记下了几行字。")))
  (define (lucy-finish!)
    (if (and (city-active?) lucy-met? lucy-mailed? (not lucy-after?)
             (第二幕 'lucy-at-cafe?) (not (lucy-material-pending?)))
      #t (error "露西小说：尚未寄出、已经收尾、她不在店里或还有经历未讲述"))
    (set! lucy-after? #t)
    (play-dialogue!
      (line "露西" "寄出去了。这里留了一份，你想读就拿过去。")
      (line "世界" (if (equal? lucy-material "入稿")
        "侦探在咖啡馆坐下。窗边的老人点了两杯咖啡，每次只喝一杯。尼尔认出了那张桌子。"
        "稿纸上的侦探租着一间漏雨的办公室，靠替人打听婚外情付房租。"))
      (line "世界" (if (equal? lucy-material "入稿")
        "最后几页，一个穿工装的男人在老人对面坐下。两只杯子空了，他们一起走出门。侦探没有再跟过去。"
        "侦探终于办完了一件案子。最后，他回到咖啡馆，还是只喝得起最便宜的咖啡。"))
      (line "尼尔" (if (equal? lucy-material "入稿") "你把那两只空杯子也写进去了。" "这侦探怎么跟我那么像？"))
      (line "露西" (if (equal? lucy-material "入稿") "当然。你特意讲给我听的。" "你哪有他那么潇洒。他至少有一张自己的办公桌。"))
      (line "露西" "扔进邮筒以后，我才发现自己忘了把最后一页再改一遍。")
      (line "尼尔" "你不会想去把邮差追回来吧？")
      (line "露西" "我考虑过。后来想，下一篇还可以写得更好。")))

  ;; 汤米：第一次能自己处理大人物的麻烦，尼尔帮忙会得到真正的友情。
  (define (tommy-return-day) (max 贵客准备日 (+ tommy-met-day 1)))
  (define (tommy-meet!)
    (set! tommy-met? #t)
    (set! tommy-met-day world-day)
    (play-dialogue!
      (line "世界" "一个年轻人把俱乐部的酒水单递给酒保，先核清箱数，又问少送的两瓶算在谁的账上。")
      (line "调酒师" "汤米，老板让你来取货，没让你替我管账。")
      (line 第三幕-汤米 "少两瓶，回去挨骂的是我。")
      (line "尼尔" "你替那家俱乐部办事？")
      (line 第三幕-汤米 "现在替他们跑腿。我叫汤米。你呢？")
      (line "尼尔" "尼尔。")
      (line 第三幕-汤米 (if tommy-advanced?
        "来俱乐部找我吧，我给你讲讲那边的事。"
        (string-append "我" (第二幕-日期 (tommy-return-day)) "还得回这儿交单。到时候碰见，给你讲讲那边的事。")))
      (line 第三幕-汤米 (if tommy-advanced?
        "记住了。现在老板肯让我管几个人了。到俱乐部来，我请你喝一杯。"
        "记住了。总有一天，他们会让我办点比搬酒更重要的事。"))))
  (define (tommy-invite!)
    (set! tommy-invited? #t)
    (play-dialogue!
      (line "世界" "汤米把签过的酒水单交回吧台，脚上换了一双新皮鞋。")
      (line 第三幕-汤米 "尼尔，上回那批货一瓶没少。这回老板让我接待贵客了。")
      (line 第三幕-汤米 "牌局这几天要开。你要是愿意，来俱乐部搭把手，问问门边来客是什么事。")
      (line 第三幕-汤米 "不来我也得自己接住。可我想让老板知道，我不只会搬酒。")))
  (define (tommy-open?)
    (and (人物线开放? "汤米") (city-active?) tommy-invited? (not tommy-hosted?) (not tommy-advanced?)
         (>= world-day 贵客准备日) (< world-day 贵客散场日)))
  (define (tommy-intel!)
    (if (and (tommy-open?) (= (tommy-work 'current) 0) (not tommy-intel?)) #t
      (error "汤米：当前不能提前打听"))
    (set! tommy-intel? #t)
    ;; 提前打听与现场问来意是同一份工作，不重复收费。
    (if (= (tommy-work 'current) 0) (tommy-work 'advance! 1) #f)
    (play-dialogue!
      (line "世界" "尼尔从跑腿人那里听说，有位外街债权人打算不请自来，在牌桌前让一名贵客难堪。")
      (line 第三幕-汤米 "他不是来打牌的，是来讨脸面的。让他当着所有人嚷出来，咱们这晚就毁了。")
      (line "尼尔" "那就别让他们在牌桌前谈。")
      (line 第三幕-汤米 "可以试着请他们私下谈。但贵客未必肯挪步，你到时替我看看。")))
  (define (tommy-step!)
    (if (and (tommy-open?) (or (= (tommy-work 'current) 0) (= (tommy-work 'current) 2))) #t
      (error "汤米牌局：须先决定怎样处理来客"))
    (tommy-work 'advance! 1)
    (if (tommy-work 'full?) (tommy-finish! '保住牌局)
      (play-dialogue!
        (line "世界" "尼尔问清来人的目的：对方要找欠钱的贵客，当着牌桌讨个说法。贵客却不肯起身。")
        (line 第三幕-汤米 "先请他到侧间，也许不惊动别人。可让他一直躲着，那位也不会走。你看怎么办？"))))
  (define (tommy-choose! approach grade)
    (if (and (tommy-open?) (= (tommy-work 'current) 1) (equal? tommy-approach "未定")) #t
      (error "汤米牌局：当前不可选择处理方式"))
    (if (and (equal? approach "私下") (eq? grade '坏))
      (begin (spend-composure! 1)
        (play-dialogue! (line 第三幕-贵客 "我这手还没打完。你们想叫谁，就自己叫去。")))
      (begin
        (if (equal? approach "当众") (spend-composure! 1) #f)
        (set! tommy-approach approach)
        (tommy-work 'advance! 1)
        (play-dialogue!
          (line "世界" (if (equal? approach "私下")
            "尼尔低声告诉贵客，来人已经等了很久，再不出去只会闹得更难看。他终于放下牌，跟着汤米走进侧间。"
            "尼尔先拦住了来客的骂声，让他只说清欠款和来意。牌桌安静下来，贵客终于当着众人的面承认确有这笔账。"))
          (line 第三幕-汤米 (if (equal? approach "私下")
            "谢谢，给两边都留了台阶。你陪我进去，把还款的时间说清楚。"
            "这下他不能装没听见了。不过得罪了这张桌子，后面我来接。咱们去把时间说清楚。"))))))
  ;; C3 结果契约；当前由城市接待的最后一段调用。
  (define (tommy-finish! result)
    (if (and (tommy-open?) (tommy-work 'full?) (member? result '(保住牌局 闹出麻烦)))
      #t (error "第三幕汤米：结果或状态非法"))
    (set! tommy-hosted? #t)
    (play-dialogue!
      (line "世界" "尼尔陪汤米走进侧间。来客与欠钱的贵客已经坐下，两个人都等着对方先开口。")
      (line 第三幕-汤米 "先生，您的事当然得有个说法。可您要让他还钱，总得先让他听得见您说话。")
      (line "世界" (if (equal? tommy-approach "当众")
        "贵客不能再否认欠款，汤米让双方写下谈账的日期。牌局重新开了起来，贵客却不愿再与他多说话。"
        "双方私下核清来意，汤米定下下一次谈账的安排。大厅继续打牌，贵客离开时还朝他点了点头。"))
      (line "尼尔" "你刚才不怕他动手？")
      (line 第三幕-汤米 "怕。可我要是一辈子站在门口，比挨一拳还难受。")))
  (define (tommy-after!)
    (set! tommy-after? #t)
    (play-dialogue!
      (line "世界" "里间的门开了。汤米坐在以前只敢站着看的那张桌旁，领带打得格外整齐。")
      (line 第三幕-汤米 (if tommy-hosted?
        "兄弟！过来坐。这回酒算我的。"
        "尼尔！过来坐。这回酒算我的。"))
      (line 第三幕-汤米 (cond
        (tommy-hosted? (if (equal? tommy-approach "当众")
          "那晚他当众认了账，后来一阵子不肯理我。可门口的人知道，我不会只让欠钱的躲着。多谢你陪我接住。"
          "那晚给两边留了台阶，老板也没丢脸。以后这种场面还得会办，多谢你陪我。"))
        ((> (tommy-work 'current) 0)
          (if tommy-intel?
            "你替我打听的那些话派上用场了。后面的事我自己接住了，多谢。"
            "那晚你替我搭的手，我记着呢。后面的事我自己接住了，多谢。"))
        (else "老板终于让我管几个人了。你看，下次咱们就能到更好的地方喝酒。")))))
  (define (on-day!)
    (if (city-active?)
      (begin
        (if (>= world-day (lucy-mail-day)) (set! lucy-mailed? #t) #f)

        (if (and (人物线开放? "汤米") (>= world-day 贵客散场日)) (set! tommy-advanced? #t) #f)) #f))
  (define-turn-start-rule "第三幕人物生活"
    (lambda () #t) on-day!)
  (define (opportunity-clock label first-day end-day)
    (clock-priority
      (list 'clock label (max 0 (- end-day world-day)) (- end-day first-day) 'countdown
        "期限届满由他们自行处理。") 'secondary))

  (define (arrivals-at loc)
    (if (not (city-active?)) '()
      (cond
        ((and (equal? loc "街角咖啡馆") (第二幕 'lucy-at-cafe?)
              (not (第二幕 'lucy-arrival-pending?))
              (>= world-day (第二幕 'lucy-writing-day)) (not lucy-met?))
          (list (arrival "露西自己的稿件" lucy-meet!)))

        ((and (人物线开放? "汤米") (equal? loc "老街酒馆") (>= world-day 12) (not tommy-met?))
          (list (arrival "替俱乐部取酒的年轻人" tommy-meet!)))
        ((and (人物线开放? "汤米") (equal? loc "老街酒馆") tommy-met? (> world-day tommy-met-day)
              (>= world-day 贵客准备日) (not tommy-invited?) (not tommy-advanced?))
          (list (arrival "汤米接下贵客差事" tommy-invite!)))
        ((and (equal? loc "剧院") report? (equal? nightingale-night "未邀请"))
          (list (arrival "夜莺提出河边邀约" invite-private-night!)))
        (else '()))))
  (define (nodes-at loc)
    (if (not (city-active?)) '()
      (append
        (if (and (equal? loc "夜总会") (tommy-open?))
          (list (clock-node "第三幕接待进展" (tommy-work 'render-data))) '())
        ;; 调查阶段只开放当前可知的行动；地点ID与共享模型保持稳定。
        (if (and (equal? loc "剧院") (case-open?))
          (cond
            ((equal? inquiry-stage "守候")
              (if (>= world-day inquiry-day)
                (list (node "等待黑衣人" :subtitle "守候下一次出现，借车跟上"
                  :resolve (instant (outcome wait-for-car!))))
                (list (note-node "跟车等待" "另找机会" "今晚已错过，明天再来剧院守候。"))))
            ((equal? inquiry-stage "记录") (list (instant-action "交回跟车记录" give-car-record!)))
            ((equal? inquiry-stage "核查")
              (if (>= world-day inquiry-day)
                (list (instant-action "询问核查结果" manager-findings!))
                (list (note-node "经理核查中" "等待回音" "经理正在联系车行和货运熟人，明天回来找他。"))))
            ((equal? inquiry-stage "旧事") (list (instant-action "问夜莺旧事" nightingale-history!)))
            (else '())) '())
        (if (and (equal? loc "货运公司") (case-open?) (equal? inquiry-stage "公司"))
          (list (node "调查罗科租处" :subtitle "一枚骰，核对寄存与收发安排"
            :requires (list (req-die))
            :resolve (instant (outcome investigate-company!)))) '())
        (if (and (equal? loc "货运公司") (preparing?))
          (list (node "付钱达成协议" :subtitle "支付150美元，签约结束纠缠"
            :requires (list (req-item "金钱" 金钱协议价))
            :resolve (instant (outcome money-agreement!)))) '())
        (if (and (equal? loc "货运公司") (preparing?))
          (list (note-node "罗科档案所在" "三楼档案室" "罗科把旧账和私人信件放在这里。可以潜入取回材料，也可以付150美元签约。")
            (node "潜入货运大楼" :subtitle "进入后选择装卸或潜入，再搜查档案"
              :resolve (instant (outcome enter-infiltration!)))) '())
        (if (and (equal? loc "剧院") (equal? case-state "结案") (not report?))
          (list (at-anchor "剧院-后台" (instant-action "提交调查报告" confirm-report!))) '())
        (if (and (equal? loc "剧院") report? (equal? nightingale-night "待答复"))
          (list (instant-action "和夜莺去河边" private-night!)
                (instant-action "婉拒河边邀约" decline-private-night!)) '())
        ;; 老人经历由独立模块投卡；这里只提供回到写作的出口与合并后的收尾。
        (if (and (equal? loc "街角咖啡馆") (第二幕 'lucy-at-cafe?)
                 lucy-met? (not lucy-after?) (lucy-material-pending?))
          (list (node "把故事讲给露西" :anchor "露西@咖啡馆"
            :subtitle (if lucy-mailed? "她的稿已寄出，这段经历留给下一篇。"
                         "把窗边老人和儿子的事讲给她。")
            :resolve (instant (outcome lucy-share!)))) '())
        (if (and (equal? loc "街角咖啡馆") (第二幕 'lucy-at-cafe?)
                 lucy-met? lucy-mailed? (not lucy-after?) (not (lucy-material-pending?)))
          (list (node "读露西的小说" :anchor "露西@咖啡馆"
            :subtitle "看看她寄出的小说，听听投稿后的心情。"
            :resolve (instant (outcome lucy-finish!)))) '())

        ;; 汤米：牌局没有固定在收网那天，未参与也不会停住。
        (if (and (equal? loc "老街酒馆") (tommy-open?) (not tommy-intel?) (= (tommy-work 'current) 0))
          (list (node "打听牌局来客" :subtitle "一枚骰问清来意，抵去现场第一段"
            :clocks (list (opportunity-clock "接待余日" 贵客准备日 贵客散场日))
            :requires (list (req-die)) :resolve (instant (outcome tommy-intel!)))) '())
        (if (and (equal? loc "夜总会") (tommy-open?))
          (if (= (tommy-work 'current) 1)
            (list
              (node "请到侧间" :subtitle "交际判定；私下谈账，失利耗1心情"
                :risk '冒险 :risk-target (list 'composure "player")
                :requires (list (req-die)) :resolve
                (roll 'social (outcome (lambda () (tommy-choose! "私下" '坏)))
                  (outcome (lambda () (tommy-choose! "私下" '中)))
                  (outcome (lambda () (tommy-choose! "私下" '好)))))
              (node "让来客说清" :subtitle "一枚骰、1心情；当众认账，会得罪贵客"
                :risk-target (list 'composure "player")
                :requires (list (req-die)) :resolve (instant (outcome (lambda () (tommy-choose! "当众" '好))))))
            (list (node "今晚的贵客"
              :name (if (= (tommy-work 'current) 0) "问清来客诉求" "陪汤米去交涉")
              :subtitle "一枚骰；先问来意，再选处理方式，最后交涉"
              :clocks (list (opportunity-clock "接待余日" 贵客准备日 贵客散场日))
              :requires (list (req-die)) :resolve (instant (outcome tommy-step!))))) '())
        (if (and (人物线开放? "汤米") (equal? loc "夜总会") tommy-met? tommy-advanced? (not tommy-after?))
          (list (instant-action "汤米坐进包厢" tommy-after!)) '()))))
  ;; 一个人的一段事保留为同一小节；子项按经历推进，不把每张行动卡变成一条任务。
  (define (dossier-at)
    (if (not (city-active?)) '()
      (append
        (if started? (list (dossier "银色打火机" :kind '主线
          :primary (not report?) :status (if report? '了结 '进行中)
          :now (cond
            (report? "已经向经理报告，罗科的纠缠结束。")
            ((equal? case-state "结案") "结算已经落实，去剧院向经理交报告。")
            ((equal? inquiry-stage "守候") (if (>= world-day inquiry-day)
              "去剧院等待黑衣人再次出现，跟踪他的轿车，记下识别信息。"
              "这次跟车没能记全信息。明天再去剧院守候。"))
            ((equal? inquiry-stage "跟车") "正在跟踪轿车，记录车辆与驾驶人的特征。")
            ((equal? inquiry-stage "记录") "已记下车辆与驾驶人特征，回剧院交给经理核查。")
            ((equal? inquiry-stage "核查") (if (>= world-day inquiry-day)
              "经理的核查已有回音，回剧院问他结果。" "经理正在核查车主与落脚地点，明天回剧院。"))
            ((equal? inquiry-stage "旧事") "车主是罗科，在货运联合公司租着办公室。去问夜莺是否认识他。")
            ((equal? inquiry-stage "公司") "夜莺说罗科是她的旧经纪人。去货运联合公司查明材料与收发安排。")
            (else "她确实受过帮助，但旧账有争议。可以潜入货运大楼取得材料，也可以支付150美元达成协议。"))
          :steps (cond
            (report? (list (step "真实结案，已经向经理交报告。" #t)))
            ((equal? case-state "结案") (list (step "到剧院提交调查报告。" #f)))
            ((not rocco-warehouse?) (list (step
              (cond
                ((equal? inquiry-stage "守候") "到剧院等待黑衣人，再跟踪轿车记录信息。")
                ((equal? inquiry-stage "跟车") "在暴露前记全识别信息。")
                ((equal? inquiry-stage "记录") "将跟车记录交给经理。")
                ((equal? inquiry-stage "核查") "次日回剧院询问经理的核查结果。")
                ((equal? inquiry-stage "旧事") "在剧院问夜莺与罗科的旧关系。")
                (else "到货运联合公司调查罗科租用的办公室和寄存服务。")) #f)))
            (else (list
              (step "已找到罗科，知道真实合同与货运大楼的档案。" #t)
              (step "在货运公司选择一种办法：\n潜入取得材料，再与罗科谈判。\n支付150美元，直接签约结束纠缠。" #f))))
          :where (cond ((equal? case-state "结案") "剧院") (rocco-warehouse? "货运公司") ((equal? inquiry-stage "公司") "货运公司") (else "剧院")))) '())
        (if report? (list (dossier "案后与夜莺" :kind '人物
          :status (cond
            ((member? nightingale-night '("已赴约" "婉拒")) '了结)
            (else '进行中))
          :now (cond
            ((equal? nightingale-night "已赴约") "和夜莺在河边度过了一个没有追踪者的夜晚。")
            ((equal? nightingale-night "婉拒") "你婉拒了夜莺的邀请，想自己歇一会儿。")
            ((equal? nightingale-night "待答复") "夜莺想去河边走走，等你决定是否同行。")
            (else "案子已经结束。可以回剧院见见夜莺，听听她的近况。"))
          :steps (if (member? nightingale-night '("已赴约" "婉拒")) '()
            (list (step (if (equal? nightingale-night "待答复")
                          "到剧院答复邀约：可以同行，也可以婉拒。"
                          "下次到剧院，与夜莺见面。") #f)))
          :where "剧院")) '())
        (if lucy-met? (list (dossier "露西的小说" :kind '人物
          :status (cond (lucy-after? '了结)
                        ((and (not (equal? lucy-material "未谈")) (not lucy-mailed?)) '等着别人)
                        (else '进行中))
          :now (cond
            (lucy-after? (if (equal? lucy-material "入稿")
              "窗边父子的故事出现在她寄出的小说里。她已经开始想着下一篇。"
              "读过她寄出的小说。她愿意继续写下去。"))
            ((equal? lucy-material "入稿") "她把窗边父子的事写进了小说，正准备定稿。")
            ((equal? lucy-material "寄后") "小说已经寄出。她记下窗边父子的故事，留作以后的素材。")
            ((两杯咖啡 'completed?) "窗边的父子喝完了两杯咖啡，一起离开。可以把这件事讲给露西。")
            (else "露西想找些事情写。咖啡馆的老人值得留意；她也会按自己的安排定稿、寄出。"))
          :steps (append
            (if (or lucy-after? (not (equal? lucy-material "未谈"))) '()
              (list (step (if (两杯咖啡 'completed?)
                  "露西当班时，把窗边老人的经历讲给她。"
                  "可在咖啡馆留意老人：每天一次，每次一枚骰，无需判定；共四段经历。") #f)))
            (list (step (if lucy-mailed?
                (if (lucy-material-pending?) "先把窗边老人的故事讲给露西，再读她寄出的小说。"
                    "露西当班时回来，读她寄出的小说。")
                "她会自行定稿寄出，不必等老人经历完成；之后再来读稿。") lucy-after?)))
          :clocks (if (and (not lucy-after?) (not (两杯咖啡 'completed?)))
            (list (两杯咖啡 'clock-data)) '())
          :where "街角咖啡馆")) '())

        (if (and (人物线开放? "汤米") tommy-met?) (list (dossier "汤米的新差事" :kind '人物
          :status (cond (tommy-after? '了结)
                        ((and (not tommy-invited?) (not tommy-advanced?) (< world-day (tommy-return-day))) '等着别人)
                        ((and tommy-hosted? (not tommy-advanced?)) '等着别人)
                        (else '进行中))
          :now "认识了替俱乐部跑腿的汤米。他想接下比搬酒更重要的差事。"
          :steps (list
            (step (if tommy-advanced? "贵客牌局已经过去，可以听汤米说后来怎样。"
                    (string-append
                      (if (< world-day (tommy-return-day)) (第二幕-日期 (tommy-return-day)) "今天")
                      "到老街酒馆，汤米要回来交单，可以听听他的后续。"))
              (or tommy-invited? tommy-advanced?))
            (step (cond
              (tommy-after? "已经在俱乐部见过汤米的新处境。")
              (tommy-advanced? "去夜总会找汤米，看看那场牌局之后他过得怎样。")
              (tommy-hosted? "接待已经帮完。等这轮差事结束，再到夜总会见汤米。")
              (else "可在酒馆打听或直接去夜总会；问清来意后，选私下劝谈或当众认账，再一起交涉。也可不参加。"))
              tommy-after?))
          :clocks (if (and tommy-invited? (not tommy-advanced?) (not tommy-hosted?))
            (list (tommy-work 'render-data) (opportunity-clock "接待余日" 贵客准备日 贵客散场日)) '())
          :where (if (or tommy-invited? tommy-advanced?) "夜总会" "老街酒馆"))) '()))))
  (lambda args
    (let ((msg (car args)))
      (cond
        ((eq? msg 'start!) (start!))
        ((eq? msg 'reported?) report?)
        ((eq? msg 'investigating?) (and started? (not report?) (not (equal? case-state "结案"))))
        ((eq? msg 'privacy-exposed?) (or papers-leaked? warehouse-private-exposed?))
        ((eq? msg 'started?) started?)
        ((eq? msg 'rocco-building-open?) (and (city-active?) (member? inquiry-stage '("旧事" "公司" "已查清")) started?))
        ((eq? msg 'lucy-writing-known?) lucy-met?)
        ((eq? msg 'club-known?) (and (人物线开放? "汤米") (or tommy-invited? (and tommy-met? tommy-advanced?))))
        ((eq? msg 'on-day!) (on-day!))
        ((eq? msg 'finished?) (and report? (member? nightingale-night '("已赴约" "婉拒"))))
        ((eq? msg 'nodes-at) (nodes-at (cadr args)))
        ((eq? msg 'arrivals-at) (arrivals-at (cadr args)))
        ((eq? msg 'offer!) (offer!))
        ((eq? msg 'dossier) (dossier-at))
        ((eq? msg 'scenario-result!)
         (let ((which (cadr args)) (result (caddr args)))
           (cond
             ((eq? which '今晚的贵客) (tommy-finish! result))
             (else (error "第三幕：未知Scenario接口")))))
        ((eq? msg 'save)
         (list (list "version" 11)
           (list "tommy-work" (tommy-work 'current))
           (list "started" started?) (list "start-day" start-day)
           (list "case" case-state)
           (list "inquiry-stage" inquiry-stage) (list "inquiry-day" inquiry-day)
           (list "contract" rocco-contract?) (list "ledger" rocco-ledger?)
           (list "threat" rocco-threat?) (list "warehouse" rocco-warehouse?)
           (list "papers-leaked" papers-leaked?) (list "protected" nightingale-protected?)
           (list "settlement-fund" settlement-fund) (list "manager-advance" manager-advance) (list "case-expense" case-expense)
           (list "warehouse-stopped" warehouse-stopped?) (list "warehouse-private-exposed" warehouse-private-exposed?)
           (list "warehouse-ledger" warehouse-ledger?) (list "warehouse-threat" warehouse-threat?)
           (list "warehouse-material" warehouse-material?) (list "report" report?)
           (list "night-state" nightingale-night)
           (list "lucy-met" lucy-met?) (list "lucy-material" lucy-material)
           (list "lucy-mailed" lucy-mailed?)
           (list "lucy-after" lucy-after?)

           (list "tommy-met" tommy-met?) (list "tommy-met-day" tommy-met-day)
           (list "tommy-invited" tommy-invited?) (list "tommy-intel" tommy-intel?)
           (list "tommy-hosted" tommy-hosted?) (list "tommy-advanced" tommy-advanced?)
           (list "tommy-after" tommy-after?) (list "tommy-approach" tommy-approach)))
        ((eq? msg 'load!)
         (let ((d (cadr args)))
           (set! started? (assoc-get d "started" #f))
           (set! start-day (assoc-get d "start-day" 0))
           (set! case-state (assoc-get d "case" "未开始"))
           (set! inquiry-stage (assoc-get d "inquiry-stage"
             (if (assoc-get d "warehouse" #f) "已查清" "守候")))
           (set! inquiry-day (assoc-get d "inquiry-day" 0))
           (if (and (member? inquiry-stage '("守候" "跟车" "记录" "核查" "旧事" "公司" "已查清"))
                    (number? inquiry-day) (>= inquiry-day 0)) #t
             (error "第三幕存档：调查阶段非法"))
           (set! rocco-contract? (assoc-get d "contract" #f))
           (set! rocco-ledger? (assoc-get d "ledger" #f))
           (set! rocco-threat? (assoc-get d "threat" #f))
           (set! rocco-warehouse? (assoc-get d "warehouse" #f))
           (if (equal? rocco-warehouse? (equal? inquiry-stage "已查清")) #t
             (error "第三幕存档：公司发现与调查阶段不符"))
           (set! papers-leaked? (assoc-get d "papers-leaked" #f))
           (set! nightingale-protected? (assoc-get d "protected" #f))
           (set! settlement-fund (assoc-get d "settlement-fund" 0))
           (set! manager-advance (assoc-get d "manager-advance" 0))
           (set! case-expense (assoc-get d "case-expense" 0))
           (if (and (member? settlement-fund '(0 40)) (member? manager-advance '(0 20))
                    (member? case-expense '(0 20 40 60 80))
                    (<= (+ manager-advance case-expense) 80)
                    (or (= manager-advance 0) (= settlement-fund 40) (equal? case-state "结案")))
             #t (error "第三幕存档：罗科既有款项非法"))
           ;; 第7版将已经取得的筹码实体化；只升级旧档，不在每次渲染补物品。
           (if (and (< (assoc-get d "version" 0) 7) (equal? case-state "调查"))
             (begin
               (if rocco-ledger? (add-item! "罗科付款凭据" 1) #f)
               (if rocco-threat? (add-item! "罗科恐吓证词" 1) #f)
               (if (> settlement-fund 0) (add-item! "罗科结算款" 1) #f)) #f)
           (set! warehouse-stopped? (assoc-get d "warehouse-stopped" #f))
           (set! warehouse-private-exposed? (assoc-get d "warehouse-private-exposed" #f))
           (set! warehouse-ledger? (assoc-get d "warehouse-ledger" #f))
           (set! warehouse-threat? (assoc-get d "warehouse-threat" #f))
           (set! warehouse-material? (assoc-get d "warehouse-material" #f))
           (if (and (member? warehouse-stopped? '(#t #f))
                    (member? warehouse-private-exposed? '(#t #f))
                    (member? warehouse-ledger? '(#t #f))
                    (member? warehouse-threat? '(#t #f))
                    (member? warehouse-material? '(#t #f))
                    (or (not warehouse-private-exposed?) warehouse-stopped?)) #t
             (error "货栈存档：报道与隐私事实非法"))
           (set! report? (assoc-get d "report" #f))
           (set! nightingale-night
             (assoc-get d "night-state" (if (assoc-get d "night" #f) "已赴约" "未邀请")))
           (if (member? nightingale-night '("未邀请" "待答复" "已赴约" "婉拒")) #t
             (error "第三幕存档：私人邀约状态非法"))
           (set! lucy-met? (assoc-get d "lucy-met" #f))
           (set! lucy-material (assoc-get d "lucy-material" "未谈"))
           (if (member? lucy-material '("未谈" "入稿" "寄后")) #t
             (error "第三幕存档：露西素材状态非法"))
           (set! lucy-mailed? (assoc-get d "lucy-mailed" #f))
           (set! lucy-after? (assoc-get d "lucy-after" #f))

           (set! tommy-met? (assoc-get d "tommy-met" #f))
           (set! tommy-met-day (assoc-get d "tommy-met-day" (if tommy-met? start-day 0)))
           (set! tommy-invited? (assoc-get d "tommy-invited" tommy-met?))
           (set! tommy-intel? (assoc-get d "tommy-intel" #f))
           (set! tommy-hosted? (assoc-get d "tommy-hosted" #f))
           (set! tommy-advanced? (assoc-get d "tommy-advanced" #f))
           (set! tommy-after? (assoc-get d "tommy-after" #f))
           ;; 旧档已有事实仍有效，已完成的帮工不重新收费或发钱。
           (tommy-work 'load! (assoc-get d "tommy-work" (if tommy-hosted? 3 (if tommy-intel? 1 0))))
           ;; 旧版第二段明确安排过侧间，对已有事实按私下处理接续。
           (set! tommy-approach (assoc-get d "tommy-approach" (if (>= (tommy-work 'current) 2) "私下" "未定")))
           (if (and (member? tommy-approach '("未定" "私下" "当众"))
                    (if (>= (tommy-work 'current) 2) (not (equal? tommy-approach "未定")) (equal? tommy-approach "未定")))
             #t (error "汤米存档：处理方式与接待进度不一致"))
           (if (or (and started? (> start-day 0) (member? case-state '("调查" "补救" "结案")))
                   (and (not started?) (= start-day 0) (equal? case-state "未开始")))
             #t (error "第三幕存档：开案状态非法"))))
        (else (error "第三幕：未知消息")))))))
