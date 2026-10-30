;; 第一章第三幕《银色打火机》：城市事件与人物自主进展。
;; 独立 Scenario 尚待设计；当前用分段城市行动承载真实付出，不增加故事结局分支。
;; 所有城市行动使用现有骰子/金钱/冷静系统；不另建任务或日程系统。
;; 普通配角在所属事件模块就近声明；对白不依赖尚不存在的配音资产。
(define 第三幕-旅馆店员 (speaker "第三幕旅馆店员" :name "旅馆店员" :appearance '成年男性))
(define 第三幕-罗科 (speaker "第三幕罗科" :name "罗科" :appearance '成年男性))
(define 第三幕-码头搬运工 (speaker "第三幕码头搬运工" :name "搬运工" :appearance '成年男性))
(define 第三幕-工头 (speaker "第三幕工头" :name "工头" :appearance '成年男性))
(define 第三幕-收购代表 (speaker "第三幕收购代表" :name "收购代表" :appearance '成年男性))
(define 第三幕-汤米 (speaker "第三幕汤米" :name "汤米" :appearance '成年男性))
(define 第三幕
 (let ()
  (define started? #f)
  (define start-day 0)
  (define case-state "未开始")     ; 调查／结案
  (define rocco-hotel? #f)
  (define rocco-contract? #f)
  (define rocco-ledger? #f)
  (define rocco-threat? #f)
  (define rocco-warehouse? #f)
  (define watched? #f)
  (define watch-work (make-clock "摸清夜间行踪" 2 'gauge "先认清出入的人，再沿侧巷跟进。" :beneficial-direction 'increase))
  (define warehouse-work (make-clock "了结罗科纠缠" 3 'gauge "当面对账、商定结算，再留下停止接触的见证记录。" :beneficial-direction 'increase))
  (define eddie-work (make-clock "协助吊机检修" 3 'gauge "清出作业区、搬来备件，再协助艾迪试车。" :beneficial-direction 'increase))
  (define tommy-work (make-clock "照应贵客牌局" 3 'gauge "问清来意、安排侧间，再陪汤米处理争执。" :beneficial-direction 'increase))
  (define report? #f)
  (define nightingale-night "未邀请") ; 未邀请／待答复／已赴约／婉拒
  (define eddie-met? #f)
  (define eddie-help? #f)
  (define eddie-hired? #f)
  (define eddie-celebrated? #f)
  (define lucy-met? #f)
  (define lucy-read? #f)
  (define lucy-comment? #f)
  (define lucy-mailed? #f)
  (define lucy-after? #f)
  (define frank-met? #f)
  (define frank-debate? #f)
  (define frank-refused? #f)
  (define tommy-met? #f)
  (define tommy-intel? #f)
  (define tommy-hosted? #f)
  (define tommy-advanced? #f)
  (define tommy-after? #f)
  (define (active?) (and started? (not (第二章 'started?))))
  (define (elapsed) (- world-day start-day))
  (define (case-open?) (and (active?) (equal? case-state "调查")))
  (define (ensure-case!) (if (case-open?) #t (error "第三幕：案件当前不可调查")))
  (define (start!)
    (if (or started? (not (= (第一幕 'act) 2))
            (not (第二幕生活 'manager-met?)))
      (error "第三幕：不能重复或提前开始") #t)
    (set! started? #t)
    (set! start-day world-day)
    (set! case-state "调查")
    (第一幕 'begin-third-act!)
    (play-dialogue!
      (line "世界" "经理关上办公室的门，把一只信封推过桌面。里面有几张字迹粗重的索款纸条。")
      (line "经理" "尼尔，这不是找衣箱的活儿。有人在剧院侧门等她，甚至把信送进了化妆间。")
      (line "尼尔" "她认识这个人？")
      (line "经理" "她说是个叫罗科的旧代理人。她不肯一个人走后门。我想知道他到底想干什么。")
      (line "夜莺" "他以前替我安排过演出。现在他拿着一张旧合同，说我一辈子都欠他的。")
      (line "夜莺" "昨天他就在街那头站着。我不想再看见他了。")
      (line "经理" "我付你八十块。查清是谁、手里有什么、怎样才能让他别再来。")
      (line "尼尔" "我要看看合同和那些信。")
      (line "经理" "在化妆间。去吧，兄弟。这次我真指望你。")))
  (define (learn-hotel! grade)
    (ensure-case!)
    (if (eq? grade '坏)
      (begin (spend-composure! 1)
        (play-dialogue! (line 第三幕-旅馆店员 "这里每天进进出出几十个人。我可不替你记脸。")))
      (begin (set! rocco-hotel? #t)
        (play-dialogue!
          (line 第三幕-旅馆店员 "那只银打火机？见过。他租了后面的房间，常去货栈那条街。")
          (line "尼尔" "他通常什么时候回来？")
          (line 第三幕-旅馆店员 (if (eq? grade '好)
            "天黑前。最近还有个男人替他搬纸箱，车停在侧巷。"
            "说不准。他交钱的时候总是很准时。"))))))
  (define (learn-contract!)
    (ensure-case!)
    (set! rocco-contract? #t)
    (play-dialogue!
      (line "世界" "旧合同有夜莺的签名。罗科付过服装和路费，作为交换，要从演出报酬里抽取一份。期限已经届满。")
      (line "夜莺" "当时连回家的车票都是他垫的。我确实欠过他。")
      (line "尼尔" "但这张合同不能把你以后的每一次演出都算进去。")
      (line "夜莺" "你跟他说，他会拿出另一本账。那上面永远写着我还欠他多少。")))
  (define (learn-ledger! grade)
    (ensure-case!)
    (if (eq? grade '坏)
      (begin (spend-composure! 1)
        (play-dialogue! (line "调酒师" "去年的账没那么好翻。你别把我这儿翻成警察局。")))
      (begin (set! rocco-ledger? #t)
        (play-dialogue!
          (line "世界" "旧账簿上有一笔直接付给罗科的演出报酬，旁边留着他的收款签名。")
          (line "调酒师" "那个月夜莺唱了好几场。钱是罗科来领的，还非让我写清楚。")
          (line "尼尔" "他寄来的欠款清单里，没有这笔。")
          (line "调酒师" "那你最好把这一页抄准确。那家伙记账可不是为了少收钱。")))))
  (define (learn-threat! grade)
    (ensure-case!)
    (if (eq? grade '坏)
      (begin (spend-composure! 1)
        (play-dialogue! (line "尼尔" "人群散了，侧门什么都没留下。今晚白等。")))
      (begin (set! rocco-threat? #t)
        (play-dialogue!
          (line "世界" "散场后，一个穿黑大衣的男人从街角走出，朝夜莺的汽车走去。")
          (line "尼尔" "罗科。别靠近她。")
          (line 第三幕-罗科 "我没碰她。我只想要属于我的钱。")
          (line "世界" "罗科退回人群。尼尔记下了他出现的时间，也看见他刚才拦住司机的动作。")))))
  (define (learn-warehouse!)
    (ensure-case!)
    (set! rocco-warehouse? #t)
    (play-dialogue!
      (line 第三幕-码头搬运工 "罗科？那是以前替歌手运箱子的。旧货栈里还租着一间小库房。")
      (line "尼尔" "最近有货出门？")
      (line 第三幕-码头搬运工 "有几箱纸和衣服。白天有人来问过运费。那人想把东西带走。")))
  ;; C1 接口：将来可由 Scenario 用真实结果调用此消息。
  (define (night-watch-finish! result)
    (if (and (case-open?) (not watched?)
             (member? result '(见到联系人 守住旅馆 跟丢)))
      #t (error "第三幕旅馆夜守：结果或状态非法"))
    (set! watched? #t)
    (if (eq? result '见到联系人) (set! rocco-warehouse? #t) #f)
    (play-dialogue!
      (line "世界" (cond
        ((eq? result '见到联系人) "替罗科搬箱的人沿着电车线走到货栈，敲开一道侧门。")
        ((eq? result '守住旅馆) "罗科在旅馆门口停了片刻，确认身后没有人，才往码头方向走。")
        (else "两个人分头离开。尼尔跟丢了其中一人，但记住了他们分开的地点。")))
      (line "尼尔" "至少知道他不是随口说说。他已经在处理那些东西了。")))
  (define (watch-step!)
    (ensure-case!)
    (if watched? (error "旅馆夜守：已经完成") #t)
    (watch-work 'advance! 1)
    (if (watch-work 'full?)
      (night-watch-finish! '见到联系人)
      (play-dialogue!
        (line "世界" "尼尔记下旅馆正门与侧巷的出入。一个替罗科搬箱的男人走进了后门。")
        (line "尼尔" "认得他了。接下来得看他往哪里走。"))))
  (define (case-ready?)
    (and rocco-warehouse? rocco-contract? (or rocco-ledger? rocco-threat?)))
  (define (warehouse-step!)
    (ensure-case!)
    (if (case-ready?) #t (error "货栈交锋：尚未查清去处、旧约与付款或骚扰事实"))
    (warehouse-work 'advance! 1)
    (cond
      ((warehouse-work 'full?) (warehouse-finish! '完成结案))
      ((= (warehouse-work 'current) 1)
        (play-dialogue!
          (line "世界" "尼尔在货栈找到罗科，把旧合同摊在桌上，逐项指出已经届满的约定。")
          (line 第三幕-罗科 "合同是一回事，我替她垫的钱是另一回事。先把账说清楚。")))
      (else
        (play-dialogue!
          (line "世界" "尼尔把有争议的款项单列出来，与罗科逐项核对，并请剧院安排见证人。")
          (line "尼尔" "钱要有凭据。结算以后，怎样停止接触，也得写下来。")))))
  ;; C4 结果契约；当前由三段城市交锋调用，故事结果保持简单。
  (define (warehouse-finish! result)
    (if (and (case-open?) (member? result '(完成结案 较差结算)))
      #t (error "第三幕货栈：结果或状态非法"))
    (set! case-state "结案")
    (play-dialogue!
      (line "世界" "见证人来到货栈。尼尔把此前逐项核过的账单与约定放在灯下。")
      (line 第三幕-罗科 "我替她跑过多少地方？那些旅费和房钱是谁付的？她说不要我，就不要我了？")
      (line "尼尔" (if rocco-ledger?
        "你付过钱。她也还过。你交出的清单里少了一笔。"
        "你主张的旧账得逐项核对。守在侧门堵人，不能让账变得更有道理。"))
      (line "世界" (if rocco-ledger?
        "尼尔把旧酒馆的付款记录放在桌上。那一笔钱，罗科亲手签收。"
        "尼尔指出了需要重新核对的付款。经理安排的法律顾问会要求罗科逐项结算，不能继续照他这份清单索取。"))
      (line "尼尔" (if rocco-threat?
        "旧账可以算清。至于你守在侧门拦她的事，也有人看见。继续纠缠，她会正式追究。"
        "旧账可以算清。但你必须停止在剧院等她，之后的接触只通过见证人办理。"))
      (line 第三幕-罗科 "你替她把账算完，倒像你从前也跟着我们跑过戏班。")
      (line "世界" "双方随后在见证下核清真实余额，结束旧代理关系。罗科放弃不成立的索款，承诺停止接近夜莺，并离开本城。")
      (line "尼尔" "他走了。我会再查一次车站记录。")))
  (define (confirm-report!)
    (if (and (active?) (equal? case-state "结案") (not report?))
      #t (error "第三幕：结案报告状态非法"))
    (set! report? #t)
    (add-item! "金钱" 80)
    (play-dialogue!
      (line "世界" "罗科已经乘车离城。尼尔将他的结算记录、离城核实和调查摘要交给经理。")
      (line "经理" "干得漂亮，尼尔！我说过你靠得住。这是约定的报酬。")
      (line "尼尔" "以后如果还有类似的事，我可以帮忙。")
      (line "经理" "当然。这座城里，能把事情真正办完的人不多。")))
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
  ;; 艾迪：技术判断属于艾迪，尼尔负责帮工与争取停机条件。
  (define (eddie-meet!)
    (set! eddie-met? #t)
    (play-dialogue!
      (line "艾迪" (if eddie-hired?
        "工头已经把那组老吊机交给我了。以后哪台出事，就先叫我。"
        "工头想让我接一组老吊机。以后哪台出事，就先叫我。"))
      (line "尼尔" "正式岗位？")
      (line "艾迪" (if eddie-hired?
        "签过字了。我买了双工作靴，想着能穿久一点。"
        "要是这次检修做得好，就签字。我买了双工作靴，想着能穿久一点。"))))
  (define (eddie-help!)
    (if (and (active?) eddie-met? (not eddie-hired?) (not eddie-help?)) #t
      (error "艾迪检修：当前不能协助"))
    (eddie-work 'advance! 1)
    ;; 按实际劳动逐次付钱；独立受聘不会重复发放帮工报酬。
    (add-item! "金钱" 4)
    (cond
      ((= (eddie-work 'current) 1)
        (play-dialogue!
          (line "艾迪" "先别开机。尼尔，帮我把作业区清出来，让下面的人都离开。")
          (line "世界" "尼尔与工人清开货物。艾迪安排卸下吊重，检查制动装置。")))
      ((= (eddie-work 'current) 2)
        (play-dialogue!
          (line "艾迪" "制动索磨坏了。照这个规格去领新的，旧的也带过去。")
          (line "世界" "尼尔从库房搬回替换件，艾迪核过尺寸，开始更换。")))
      (else
        (set! eddie-help? #t)
        (play-dialogue!
          (line "世界" "尼尔守在试车区外，确认工人都已退开，再向艾迪打出手势。")
          (line "世界" "艾迪逐项试过制动与空载运转。工头看过记录，在检修单上签了字。")
          (line "艾迪" "这下才算能开。谢谢你搭手。")))))
  ;; C2 接口：未来可传 (安全修复／返工)；两种结果均保留艾迪自己的能力。
  (define (eddie-finish! result)
    (if (member? result '(安全修复 返工)) #t (error "第三幕艾迪：非法Scenario结果"))
    (set! eddie-help? #t))
  (define (eddie-celebrate!)
    (set! eddie-celebrated? #t)
    (play-dialogue!
      (line "艾迪" "工资还没到手，工头先把名字写上去了。往后那几台老机器就归我管。")
      (line "艾迪" "以前机器坏了都找我。现在总算得付我钱了！")
      (line "尼尔" "打算怎么花第一笔？")
      (line "艾迪" "先把靴子钱补上。剩下的存着。兴许哪天买辆自己的汽车。")))
  ;; 露西：交流本身不收费；稿件会自行寄出。
  (define (lucy-meet!)
    (set! lucy-met? #t)
    (play-dialogue!
      (line "露西" "瞧瞧那边三个人。窗边那个每次都等人，却从没见过有人来。")
      (line "尼尔" "也许他是在等老婆。")
      (line "露西" "也许是在等债主。你看他把杯垫翻过去写字，写完又全擦掉。")
      (line "尼尔" "你怎么什么都注意？")
      (line "露西" "我写他们呀。等有一天他们真的来找我，我就说一切都是编的。")))
  (define (lucy-read!)
    (set! lucy-read? #t)
    (play-dialogue!
      (line "世界" (if lucy-mailed?
        "露西从柜台下取出留存的底稿。稿纸上的侦探租着一间漏雨的办公室，靠替人打听婚外情付房租。"
        "稿纸上的侦探租着一间漏雨的办公室，靠替人打听婚外情付房租。"))
      (line "尼尔" "这侦探怎么跟我那么像？")
      (line "露西" "你哪有他那么潇洒。他至少有一张自己的办公桌。")
      (line "尼尔" "他下一页就要抓到坏人了吗？")
      (line "露西" "也许。也许他只是终于知道，自己一开始就找错了人。")
      (line "世界" (if lucy-mailed?
        "露西把最后一页翻了过去，示意尼尔从头读，别急着偷看结尾。"
        "她把稿纸抽回来，仍不肯让尼尔看最后一页。"))))
  (define (lucy-talk!)
    (set! lucy-comment? #t)
    (play-dialogue!
      (line "露西" (if lucy-mailed?
        "我原先写了两个结尾。寄出去的那个，他回到咖啡馆，还是只喝得起最便宜的咖啡。另一个倒是让他出了名。"
        "我写了两个结尾。一个让侦探有了名气；另一个，他回到咖啡馆，还是只喝得起最便宜的咖啡。"))
      (line "尼尔" "他总该赢点什么。")
      (line "露西" "他已经认识了那么多人。难道这还不算？")
      (line "世界" (if lucy-mailed?
        "露西笑着把底稿收好。故事已经寄出去了，这回她没再伸手改最后一页。"
        "露西笑着把稿纸收好。她并没有立刻决定要写哪一个。"))))
  (define (lucy-after!)
    (set! lucy-after? #t)
    (play-dialogue!
      (line "露西" "寄出去了。扔进邮筒以后，我才发现自己忘了把最后一页再改一遍。")
      (line "尼尔" "你不会想去把邮差追回来吧？")
      (line "露西" "我考虑过。后来想，下一篇还可以写得更好。")))
  ;; 弗兰克：微缩的旧港改造，收回泊位使用权，不收购黑鸥号本身。
  (define (frank-meet!)
    (set! frank-met? #t)
    (play-dialogue!
      (line "世界" "西装男人站在黑鸥号的舷梯边，把一份表格垫在公文包上。")
      (line 第三幕-收购代表 "我们买的是泊位使用权，不是您的船。价钱已经比隔壁几家厚道了。")
      (line "弗兰克" "我在这儿停了几十年，你们写几张纸，就说这里闲置？")
      (line 第三幕-收购代表 "将来泊位管理费可能提高。今天签，还有补偿。")
      (line "弗兰克" "船我不卖，地方也不让。拿着你的表格下去。")
      (line "世界" "隔壁船主却拿了协议。他说钱够他搬到儿子家附近，不想再守旧码头。")))
  (define (frank-debate!)
    (set! frank-debate? #t)
    (play-dialogue!
      (line "尼尔" "隔壁船主说，补偿够他安安稳稳过几年。")
      (line "弗兰克" "那就让他走。人有想去的地方，不该拦着。")
      (line "尼尔" "你没有？")
      (line "弗兰克" "有人在这儿借工具，有人在这儿等活。你数数每天登船的人，再说这泊位是不是空着。")
      (line "世界" "船舱里有人喝咖啡，有人修绳结。弗兰克回头又骂他们把工具丢得满地都是。")))
  ;; 汤米：第一次能自己处理大人物的麻烦，尼尔帮忙会得到真正的友情。
  (define (tommy-meet!)
    (set! tommy-met? #t)
    (play-dialogue!
      (line 第三幕-汤米 "瞧见我的鞋了吗？真皮的。店老板说穿上它，人家就会把我当回事。")
      (line "尼尔" "你把半个月工钱穿脚上了？")
      (line 第三幕-汤米 (if tommy-advanced?
        "那场贵客牌局办完了。老板现在肯让我坐进里间了。"
        "这几天我要接待几位贵客。要是办得漂亮，老板以后就让我坐进里间。"))
      (line 第三幕-汤米 (if tommy-advanced?
        "来里间喝一杯吧。让你看看我的新位子。"
        "牌局开始前，来俱乐部搭把手。我正缺个敢开口说话的朋友。"))))
  (define (tommy-intel!)
    (set! tommy-intel? #t)
    ;; 提前打听与现场问来意是同一份工作，不重复收费。
    (if (= (tommy-work 'current) 0) (tommy-work 'advance! 1) #f)
    (play-dialogue!
      (line "世界" "尼尔从跑腿人那里听说，有位外街债权人打算不请自来，在牌桌前让一名贵客难堪。")
      (line 第三幕-汤米 "他不是来打牌的，是来讨脸面的。让他当着所有人嚷出来，咱们这晚就毁了。")
      (line "尼尔" "那就别让他们在牌桌前谈。")
      (line 第三幕-汤米 "好。给他一张安静的桌子，再让该还钱的那位自己走进去。")))
  (define (tommy-step!)
    (if (and (active?) tommy-met? (not tommy-hosted?) (not tommy-advanced?)) #t
      (error "汤米牌局：当前不能协助"))
    (tommy-work 'advance! 1)
    (cond
      ((tommy-work 'full?) (tommy-finish! '保住牌局))
      ((= (tommy-work 'current) 1)
        (play-dialogue!
          (line "世界" "尼尔在门边问清来人的目的。对方要找一位欠钱的贵客，当众讨个说法。")
          (line "尼尔" "先让汤米过来。你要的是还钱，不是让所有人都听见。")))
      (else
        (play-dialogue!
          (line "世界" "尼尔腾出侧间，替汤米把要找的人请离牌桌。大厅里的人继续打牌。")
          (line 第三幕-汤米 "你陪我进去。两边坐下来，这事才有得谈。")))))
  ;; C3 结果契约；当前由城市接待的最后一段调用。
  (define (tommy-finish! result)
    (if (and (active?) (not tommy-hosted?) (member? result '(保住牌局 闹出麻烦)))
      #t (error "第三幕汤米：结果或状态非法"))
    (set! tommy-hosted? #t)
    (play-dialogue!
      (line "世界" "尼尔陪汤米走进侧间。来客与欠钱的贵客已经坐下，两个人都等着对方先开口。")
      (line 第三幕-汤米 "先生，您的事当然得有个说法。可您要让他还钱，总得先让他听得见您说话。")
      (line "世界" "尼尔帮忙核对双方的话，汤米定下下一次谈账的安排。大厅的牌桌继续开着，今晚至少没有砸场。")
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
        (tommy-hosted? "那晚要不是你在，我也不敢保证能把场子稳住。往后有事，你尽管开口。")
        ((> (tommy-work 'current) 0)
          (if tommy-intel?
            "你替我打听的那些话派上用场了。后面的事我自己接住了，多谢。"
            "那晚你替我搭的手，我记着呢。后面的事我自己接住了，多谢。"))
        (else "老板终于让我管几个人了。你看，下次咱们就能到更好的地方喝酒。")))))
  (define (on-day!)
    (if (active?)
      (begin
        (if (and (>= (elapsed) 4) (not eddie-hired?)) (set! eddie-hired? #t) #f)
        (if (and (>= (elapsed) 5) (not lucy-mailed?)) (set! lucy-mailed? #t) #f)
        (if (and (>= (elapsed) 3) (not frank-refused?)) (set! frank-refused? #t) #f)
        (if (and (>= (elapsed) 6) (not tommy-advanced?)) (set! tommy-advanced? #t) #f))
      #f))
  (define-turn-start-rule "第三幕人物生活"
    (lambda () #t) on-day!)
  (define (opportunity-clock label end-day)
    (clock-priority
      (list 'clock label (max 0 (- end-day (elapsed))) end-day 'countdown
        "换日后按剩余天数结算，期限届满由他们自行处理。")
      'secondary))
  (define (nodes-at loc)
    (if (not (active?)) '()
      (append
        ;; 进展跨日保留；可选工作到期自动关闭，完成部分不冒认整场参与。
        (if (and (equal? loc "旧旅馆") (case-open?) (not watched?)
                 (or rocco-hotel? (>= (elapsed) 2)))
          (list (clock-node "第三幕夜守进展" (watch-work 'render-data))) '())
        (if (and (equal? loc "码头") (case-open?) (case-ready?))
          (list (clock-node "第三幕结案进展" (warehouse-work 'render-data))) '())
        (if (and (equal? loc "码头") eddie-met? (not eddie-hired?) (not eddie-help?) (>= (elapsed) 2))
          (list (clock-node "第三幕检修进展" (eddie-work 'render-data))) '())
        (if (and (equal? loc "夜总会") tommy-met? (not tommy-hosted?) (not tommy-advanced?) (>= (elapsed) 3))
          (list (clock-node "第三幕接待进展" (tommy-work 'render-data))) '())
        ;; 罗科：调查与收网
        (if (and (equal? loc "旧旅馆") (case-open?) (not rocco-hotel?))
          (list (node "问旅馆店员" :subtitle "确认银打火机男人的出入"
            :risk '冒险 :risk-target (list 'composure "player")
            :requires (list (req-die)) :resolve
            (roll 'social
              (outcome (lambda () (learn-hotel! '坏)))
              (outcome (lambda () (learn-hotel! '中)))
              (outcome (lambda () (learn-hotel! '好)))))) '())
        (if (and (equal? loc "旧旅馆") (case-open?) (not watched?)
                 (or rocco-hotel? (>= (elapsed) 2)))
          (list (node "旅馆夜守" :name (if (= (watch-work 'current) 0) "认清夜间来客" "跟进搬箱人")
            :subtitle "每段用一枚骰，共两段；可分日继续"
            :requires (list (req-die)) :resolve (instant (outcome watch-step!)))) '())
        (if (and (equal? loc "剧院") (case-open?) (not rocco-contract?))
          (list (node "翻看旧合同" :subtitle "夜莺保存着签过字的旧约"
            :requires (list (req-die)) :resolve (instant (outcome learn-contract!)))) '())
        (if (and (equal? loc "剧院") (case-open?) (not rocco-threat?))
          (list (node "守候侧门" :subtitle "观察跟踪者是否再次出现"
            :risk '冒险 :risk-target (list 'composure "player")
            :requires (list (req-die)) :resolve
            (roll 'sharpness
              (outcome (lambda () (learn-threat! '坏)))
              (outcome (lambda () (learn-threat! '中)))
              (outcome (lambda () (learn-threat! '好)))))) '())
        (if (and (equal? loc "老街酒馆") (case-open?) (not rocco-ledger?))
          (list (node "核对旧演出账" :subtitle "旧酒馆留下过代付记录"
            :risk '冒险 :risk-target (list 'composure "player")
            :requires (list (req-die)) :resolve
            (roll 'sharpness
              (outcome (lambda () (learn-ledger! '坏)))
              (outcome (lambda () (learn-ledger! '中)))
              (outcome (lambda () (learn-ledger! '好)))))) '())
        (if (and (equal? loc "码头") (case-open?) (not rocco-warehouse?)
                 (or rocco-hotel? watched?))
          (list (node "打听货栈" :subtitle "罗科曾在这片货栈存物"
            :requires (list (req-die)) :resolve (instant (outcome learn-warehouse!)))) '())
        (if (and (equal? loc "码头") (case-open?) (case-ready?))
          (list (node "货栈交锋"
            :name (list-ref '("当面核对旧约" "逐项商定结算" "留下见证记录") (warehouse-work 'current))
            :subtitle "每段用一枚骰，共三段；可分日继续"
            :requires (list (req-die))
            :resolve (instant (outcome warehouse-step!)))) '())
        (if (and (equal? loc "剧院") (equal? case-state "结案") (not report?))
          (list (instant-action "提交调查报告" confirm-report!)) '())
        (if (and (equal? loc "剧院") report? (equal? nightingale-night "未邀请"))
          (list (instant-action "夜莺的邀请" invite-private-night!)) '())
        (if (and (equal? loc "剧院") report? (equal? nightingale-night "待答复"))
          (list (instant-action "和夜莺去河边" private-night!)
                (instant-action "婉拒河边邀约" decline-private-night!)) '())
        ;; 艾迪：本人在码头，尼尔可帮忙但不代替工程判断。
        (if (and (equal? loc "码头") (>= (elapsed) 1) (not eddie-met?))
          (list (instant-action "艾迪的新差事" eddie-meet!)) '())
        (if (and (equal? loc "码头") eddie-met? (not eddie-hired?) (not eddie-help?)
                 (>= (elapsed) 2))
          (list (node "帮艾迪停机"
            :name (list-ref '("清出作业区" "搬来替换件" "协助安全试车") (eddie-work 'current))
            :subtitle "每段一枚骰，挣4美元；共三段"
            :clocks (list (opportunity-clock "检修余日" 4))
            :requires (list (req-die)) :resolve (instant (outcome eddie-help!)))) '())
        (if (and (equal? loc "码头") eddie-hired? (not eddie-celebrated?))
          (list (instant-action "艾迪的新工作靴" eddie-celebrate!)) '())
        ;; 露西：可交流她的写作，不将文学阅读伪装成骰子判定。
        (if (and (and (equal? loc "街角咖啡馆") (第二幕 'lucy-at-cafe?)) (>= (elapsed) 1) (not lucy-met?))
          (list (instant-action "露西的三个客人" lucy-meet!)) '())
        (if (and (and (equal? loc "街角咖啡馆") (第二幕 'lucy-at-cafe?)) lucy-met? (not lucy-read?))
          (list (instant-action "读露西的小说" lucy-read!)) '())
        (if (and (and (equal? loc "街角咖啡馆") (第二幕 'lucy-at-cafe?)) lucy-read? (not lucy-comment?))
          (list (instant-action "聊聊小说结尾" lucy-talk!)) '())
        (if (and (and (equal? loc "街角咖啡馆") (第二幕 'lucy-at-cafe?)) lucy-mailed? (not lucy-after?))
          (list (instant-action "露西寄出的信" lucy-after!)) '())
        ;; 弗兰克：这回收的是泊位使用权，不是船。
        (if (and (equal? loc "码头居民区") (>= (elapsed) 1) (not frank-met?)
                 (not frank-refused?))
          (list (instant-action "泊位收购人" frank-meet!)) '())
        (if (and (equal? loc "码头居民区") frank-met? (not frank-debate?))
          (list (instant-action "再谈那份合同" frank-debate!)) '())
        (if (and (equal? loc "码头居民区") frank-refused? (not frank-met?))
          (list (instant-action "黑鸥号没有签" (lambda ()
            (set! frank-met? #t)
            (play-dialogue!
              (line "弗兰克" "他们来收我的泊位使用权。我没签。")
              (line "弗兰克" "船还在，人也还在。他们叫这里闲置，我倒想问他们看见过谁。"))))) '())
        ;; 汤米：牌局没有固定在收网那天，未参与也不会停住。
        (if (and (equal? loc "老街酒馆") (>= (elapsed) 1) (not tommy-met?))
          (list (instant-action "汤米的新皮鞋" tommy-meet!)) '())
        (if (and (equal? loc "老街酒馆") tommy-met? (not tommy-intel?) (= (tommy-work 'current) 0)
                 (not tommy-advanced?) (>= (elapsed) 2))
          (list (node "打听牌局来客" :subtitle "一枚骰问清来意，抵去现场第一段"
            :clocks (list (opportunity-clock "接待余日" 6))
            :requires (list (req-die)) :resolve (instant (outcome tommy-intel!)))) '())
        (if (and (equal? loc "夜总会") tommy-met? (not tommy-hosted?)
                 (not tommy-advanced?) (>= (elapsed) 3))
          (list (node "今晚的贵客"
            :name (list-ref '("问清来客诉求" "安排侧间会谈" "陪汤米去交涉") (tommy-work 'current))
            :subtitle "每段一枚骰；提前打听可抵第一段"
            :clocks (list (opportunity-clock "接待余日" 6))
            :requires (list (req-die))
            :resolve (instant (outcome tommy-step!)))) '())
        (if (and (equal? loc "夜总会") tommy-advanced? (not tommy-after?))
          (list (instant-action "汤米坐进包厢" tommy-after!)) '()))))
  (define (dossier-at)
    (if (not (active?)) '()
      (append
        (list (dossier "银色打火机" :kind '主线
          :primary (not report?) :status (if report? '了结 '进行中)
          :now (cond
            (report? "罗科已结清旧账、停止纠缠并离城。经理收到报告，夜莺终于能安心出门。")
            ((equal? case-state "结案") "罗科已办理旧账结算并离城。到剧院向经理交报告。")
            (else (string-append
              "查清货栈去处和旧约，再取得付款或骚扰事实之一，才能当面对质。交锋还需三次城市行动。"
              (if (or rocco-hotel? rocco-warehouse? watched?) "码头附近也有线索。"
                  "先到旧旅馆、剧院或旧酒馆查问。"))))
          :where (if (equal? case-state "结案") "剧院" "旧旅馆")))
        (if eddie-met? (list (dossier "艾迪的新岗位" :kind '人物 :primary #f
          :now (if eddie-hired?
            "艾迪成了旧港正式维修工，开始认真盘算自己的工资和将来。"
            "艾迪正在检修旧吊机。可用三次城市行动协助，每次挣4美元；到期后他会自行完成。") :where "码头")) '())
        (if lucy-met? (list (dossier "露西的小说" :kind '人物 :primary #f
          :now (if lucy-mailed? "露西已经寄出小说，仍在店里工作，开始等待编辑的回信。"
            "露西在咖啡馆观察客人，正写一篇有个落魄侦探的小说。")
          :where "街角咖啡馆")) '())
        (if frank-met? (list (dossier "黑鸥号的泊位" :kind '人物 :primary #f
          :now "改造公司想收回泊位使用权，弗兰克拒绝签字。收购尚未结束。"
          :where "码头居民区")) '())
        (if tommy-met? (list (dossier "汤米的新差事" :kind '人物 :primary #f
          :now (if tommy-advanced? "汤米终于获准坐进老板的私人包厢，开始得到重用。"
            "汤米负责接待贵客。协助共需三次城市行动，提前打听可抵第一段；到期由他自行处理。")
          :where "夜总会")) '()))))
  (lambda args
    (let ((msg (car args)))
      (cond
        ((eq? msg 'start!) (start!))
        ((eq? msg 'started?) started?)
        ((eq? msg 'on-day!) (on-day!))
        ((eq? msg 'finished?) (and report? (member? nightingale-night '("已赴约" "婉拒"))))
        ((eq? msg 'nodes-at) (nodes-at (cadr args)))
        ((eq? msg 'arrivals-at) '())
        ((eq? msg 'dossier) (dossier-at))
        ((eq? msg 'scenario-result!)
         (let ((which (cadr args)) (result (caddr args)))
           (cond
             ((eq? which '旅馆夜班) (night-watch-finish! result))
             ((eq? which '吊机停工) (eddie-finish! result))
             ((eq? which '今晚的贵客) (tommy-finish! result))
             ((eq? which '货栈之夜) (warehouse-finish! result))
             (else (error "第三幕：未知Scenario接口")))))
        ((eq? msg 'save)
         (list (list "version" 3)
           (list "watch-work" (watch-work 'current))
           (list "warehouse-work" (warehouse-work 'current))
           (list "eddie-work" (eddie-work 'current))
           (list "tommy-work" (tommy-work 'current))
           (list "started" started?) (list "start-day" start-day)
           (list "case" case-state) (list "hotel" rocco-hotel?)
           (list "contract" rocco-contract?) (list "ledger" rocco-ledger?)
           (list "threat" rocco-threat?) (list "warehouse" rocco-warehouse?)
           (list "watch" watched?) (list "report" report?)
           (list "night-state" nightingale-night)
           (list "eddie-met" eddie-met?) (list "eddie-help" eddie-help?)
           (list "eddie-hired" eddie-hired?) (list "eddie-celebrated" eddie-celebrated?)
           (list "lucy-met" lucy-met?) (list "lucy-read" lucy-read?)
           (list "lucy-comment" lucy-comment?) (list "lucy-mailed" lucy-mailed?)
           (list "lucy-after" lucy-after?)
           (list "frank-met" frank-met?) (list "frank-debate" frank-debate?)
           (list "frank-refused" frank-refused?)
           (list "tommy-met" tommy-met?) (list "tommy-intel" tommy-intel?)
           (list "tommy-hosted" tommy-hosted?) (list "tommy-advanced" tommy-advanced?)
           (list "tommy-after" tommy-after?)))
        ((eq? msg 'load!)
         (let ((d (cadr args)))
           (set! started? (assoc-get d "started" #f))
           (set! start-day (assoc-get d "start-day" 0))
           (set! case-state (assoc-get d "case" "未开始"))
           (set! rocco-hotel? (assoc-get d "hotel" #f))
           (set! rocco-contract? (assoc-get d "contract" #f))
           (set! rocco-ledger? (assoc-get d "ledger" #f))
           (set! rocco-threat? (assoc-get d "threat" #f))
           (set! rocco-warehouse? (assoc-get d "warehouse" #f))
           (set! watched? (assoc-get d "watch" #f))
           (set! report? (assoc-get d "report" #f))
           (set! nightingale-night
             (assoc-get d "night-state" (if (assoc-get d "night" #f) "已赴约" "未邀请")))
           (if (member? nightingale-night '("未邀请" "待答复" "已赴约" "婉拒")) #t
             (error "第三幕存档：私人邀约状态非法"))
           (set! eddie-met? (assoc-get d "eddie-met" #f))
           (set! eddie-help? (assoc-get d "eddie-help" #f))
           (set! eddie-hired? (assoc-get d "eddie-hired" #f))
           (set! eddie-celebrated? (assoc-get d "eddie-celebrated" #f))
           (set! lucy-met? (assoc-get d "lucy-met" #f))
           (set! lucy-read? (assoc-get d "lucy-read" #f))
           (set! lucy-comment? (assoc-get d "lucy-comment" #f))
           (set! lucy-mailed? (assoc-get d "lucy-mailed" #f))
           (set! lucy-after? (assoc-get d "lucy-after" #f))
           (set! frank-met? (assoc-get d "frank-met" #f))
           (set! frank-debate? (assoc-get d "frank-debate" #f))
           (set! frank-refused? (assoc-get d "frank-refused" #f))
           (set! tommy-met? (assoc-get d "tommy-met" #f))
           (set! tommy-intel? (assoc-get d "tommy-intel" #f))
           (set! tommy-hosted? (assoc-get d "tommy-hosted" #f))
           (set! tommy-advanced? (assoc-get d "tommy-advanced" #f))
           (set! tommy-after? (assoc-get d "tommy-after" #f))
           ;; 旧档已有事实仍有效，已完成的帮工不重新收费或发钱。
           (watch-work 'load! (assoc-get d "watch-work" (if watched? 2 0)))
           (warehouse-work 'load! (assoc-get d "warehouse-work" (if (equal? case-state "结案") 3 0)))
           (eddie-work 'load! (assoc-get d "eddie-work" (if eddie-help? 3 0)))
           (tommy-work 'load! (assoc-get d "tommy-work" (if tommy-hosted? 3 (if tommy-intel? 1 0))))
           (if (or (and started? (> start-day 0) (member? case-state '("调查" "结案")))
                   (and (not started?) (= start-day 0) (equal? case-state "未开始")))
             #t (error "第三幕存档：开案状态非法"))))
        (else (error "第三幕：未知消息")))))))
