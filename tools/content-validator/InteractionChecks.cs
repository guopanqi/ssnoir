using SSNoir.Core;
namespace SSNoir.Testing;

public static class InteractionChecks
{
    static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException(name);
        Console.WriteLine("[interaction] OK: " + name);
    }
    static IEnumerable<GameNode> Nodes(GameNode n) => new[] { n }.Concat(n.Children.SelectMany(Nodes));
    public static void Run()
    {
        var s = new SceneManager(new GameState(), new LocalScriptLoader());
        s.LoadScene("world");
        object? E(string code) => s.ActiveInterpreter.Eval(code);
        E("(define test-count 0)");
        E("(define test-trigger (make-triggered-event \"测试\" \"行动结算\" \"测试活动\" (lambda (c) #t) (lambda (c) (set! test-count (+ test-count 1)) #t) 1 2 100))");
        E("(define test-ctx '((\"timing\" \"行动结算\") (\"activity\" \"测试活动\")))");
        Check((bool)E("(test-trigger 'try! test-ctx)")!, "触发条件命中");
        Check(!(bool)E("(test-trigger 'try! test-ctx)")!, "同日冷却不重复");
        E("(define test-save (test-trigger 'save))");
        E("(test-trigger 'load! '())"); E("(test-trigger 'load! test-save)");
        Check(!(bool)E("(test-trigger 'try! test-ctx)")!, "恢复记录不重发");
        E("(set-global! '世界日 (+ (get-global '世界日) 1))");
        Check((bool)E("(test-trigger 'try! test-ctx)")!, "冷却结束再次发生");
        E("(set-global! '世界日 (+ (get-global '世界日) 1))");
        Check(!(bool)E("(test-trigger 'try! test-ctx)")!, "次数上限生效");
        E("(define never (make-triggered-event \"概率零\" \"行动结算\" \"测试活动\" (lambda (c) #t) (lambda (c) (error \"不应发生\")) 0 0 0))");
        Check(!(bool)E("(never 'try! test-ctx)")!, "零概率不执行内容");
        E("(define first (make-triggered-event \"优先\" \"行动结算\" \"测试活动\" (lambda (c) #t) (lambda (c) #t) 0 0 100))");
        E("(define second (make-triggered-event \"后排\" \"行动结算\" \"测试活动\" (lambda (c) #t) (lambda (c) (error \"不能叠放\")) 0 0 100))");
        Check((bool)E("(try-interactions! (list first second) test-ctx)")!, "一处检查只发生一个互动");
        E("(set-global! '世界日 10)");
        E("(define gifts 0)"); E("(define next-gathering #f)");
        E("""
          (define (wine-event id payload)
            (make-triggered-event id "进入地点" ""
              (lambda (ctx) (>= (get-global '世界日) (assoc-get payload "from" 1)))
              (lambda (ctx) #f) 0 1 100
              (list (lambda () (or next-gathering (>= (get-global '世界日) (assoc-get payload "until" 14))))
                '("家" "码头")
                (lambda (place finish!)
                  (list (node (string-append id "@" place) :anchor "家"
                    :resolve (instant (outcome (lambda () (finish! (lambda () (set! gifts (+ gifts 1)))))))))))))
          (define pool (make-event-pool "生活后续" (list (list "留酒" wine-event))))
          (define payload '(("from" 11) ("until" 14)))
          """);
        Check((bool)E("(pool 'schedule! \"留酒\" \"第9场\" payload 100)")!, "聚餐结算时动态创建具体事件");
        Check(!(bool)E("(pool 'schedule! \"留酒\" \"第9场\" payload 100)")!, "同来源不重复创建");
        Check(Convert.ToInt32(E("(length (pool 'nodes-at \"家\"))")) == 0, "最早日期前不出现");
        E("(set-global! '世界日 11)");
        Check(Convert.ToInt32(E("(length (pool 'nodes-at \"家\"))")) == 1, "动态实例在有效地点提供入口");
        E("(define saved-pool (pool 'save))");
        E("(define pool (make-event-pool \"生活后续\" (list (list \"留酒\" wine-event))))");
        E("(pool 'load! saved-pool)");
        Check(Convert.ToInt32(E("(length (pool 'nodes-at \"码头\"))")) == 1, "读档按定义和数据重建实例");
        E("(define original-module-table 世界模块表)");
        E("(define (世界模块表) (append (original-module-table) (list (list \"pending-test\" (lambda (msg) (list pool)) '(互动)))))");
        E("(第一幕 'load! '((\"opened\" #t) (\"tavern\" #t) (\"act\" 1)))"); s.Refresh();
        Check(Nodes(s.CurrentRootNode!).Count(node => node.Id.StartsWith("留酒@第9场@")) == 2, "同一事件可在两个地点投射唯一入口");
        s.ExecuteAction(Nodes(s.CurrentRootNode!).Single(node => node.Id == "留酒@第9场@家"), new());
        Check(Convert.ToInt32(E("gifts")) == 1, "领取一次");
        Check(Convert.ToInt32(E("(length (assoc-get (pool 'save) \"instances\" '()))")) == 0, "完成实例从集合物理移除");
        Check(!(bool)E("(pool 'schedule! \"留酒\" \"第9场\" payload 100)")!, "移除后历史来源仍不重抽");
        Check(!(bool)E("(pool 'schedule! \"留酒\" \"第10场\" payload 0)")!, "未中也记录来源");
        Check(!(bool)E("(pool 'schedule! \"留酒\" \"第10场\" payload 100)")!, "进地点不能重抽失败来源");
        E("(pool 'schedule! \"留酒\" \"第11场\" payload 100)");
        E("(set! next-gathering #t)"); E("(pool 'refresh!)");
        Check(Convert.ToInt32(E("(length (assoc-get (pool 'save) \"instances\" '()))")) == 0, "下一场聚餐开始使未领取实例过期并移除");
        E("(set! next-gathering #f)"); E("(pool 'schedule! \"留酒\" \"第12场\" payload 100)"); E("(set-global! '世界日 14)");
        Check(Convert.ToInt32(E("(length (pool 'nodes-at \"家\"))")) == 0, "到期当天立即隐藏，不等待清理");
        // 使用真实 ExecuteAction 验证统一钩子，不在测试里手调人物消息。
        E("(define dispatch-count 0)");
        E("(define (dispatch-interactions! ctx) (if (equal? (assoc-get ctx \"timing\" \"\") \"行动结算\") (set! dispatch-count (+ dispatch-count 1)) #f))");
        E("(define (get-render-data) (node \"世界\" :children (list (place \"测试地点\" :children (list (node \"测试动作\" :interaction-activity \"测试活动\" :resolve (instant (outcome (lambda () #t)))) (node \"主线动作\" :interaction-activity \"测试活动\" :resolve (instant (outcome (lambda () (play-dialogue! (line \"世界\" \"主线\")))))))))))");
        s.Refresh();
        var action = Nodes(s.CurrentRootNode!).Single(n => n.Id == "测试动作");
        Check(action.InteractionActivity == "测试活动", "DSL活动字段解析");
        s.ExecuteAction(action, new());
        Check(Convert.ToInt32(E("dispatch-count")) == 1, "真实结算统一触发");
        s.ExecuteAction(Nodes(s.CurrentRootNode!).Single(n => n.Id == "主线动作"), new());
        Check(Convert.ToInt32(E("dispatch-count")) == 1, "主线对白占用时不叠互动");
        E("(define (dispatch-interactions! ctx) (if (equal? (assoc-get ctx \"timing\" \"\") \"进入地点\") (set! dispatch-count (+ dispatch-count 1)) #f))");
        s.EnterPlace("测试地点");
        Check(Convert.ToInt32(E("dispatch-count")) == 2, "无arrival的地点也经过统一入口");
        var n = new SceneManager(new GameState(), new LocalScriptLoader()); n.LoadScene("world");
        n.ActiveInterpreter.Eval("(第一幕 'load! '((\"opened\" #t) (\"tavern\" #t) (\"act\" 1)))");
        n.ActiveInterpreter.Eval("(夜莺生活 'load! '((\"daily-start\" 1) (\"social\" 2) (\"singer\" 1) (\"singer-day\" 0)))");
        n.Refresh();
        var listen = Nodes(n.CurrentRootNode!).Single(node => node.Id == "听夜莺唱歌");
        var actor = n.GameState.Team.FindActor("player")!;
        int die = actor.ActionDice.Count - 1;
        n.ExecuteAction(listen, new() { new SlottedResource { Type = "die", ActorId = actor.Id, DieIndex = actor.ActionDiceSlotIds[die], Value = actor.ActionDice[die] } });
        Check(Convert.ToInt32(n.ActiveInterpreter.Eval("(夜莺生活 'stage)")) == 2, "真实听歌经调度发生夜莺相处");
        Check(Convert.ToInt32(n.ActiveInterpreter.Eval("(assoc-get (assoc-get (assoc-get (夜莺生活 'save) \"interactions\" '()) \"夜莺酒馆相处\" '()) \"count\" 0)")) == 1, "夜莺发生记录归人物所有");
        n.ActiveInterpreter.Eval("(define seen-timings '())");
        n.ActiveInterpreter.Eval("(define (dispatch-interactions! ctx) (set! seen-timings (cons (assoc-get ctx \"timing\" \"\") seen-timings)))");
        n.ExecuteAction(Nodes(n.CurrentRootNode!).Single(node => node.Id == "睡觉"), new());
        Check((bool)n.ActiveInterpreter.Eval("(member? \"房间睡前\" seen-timings)")!, "真实房间睡前经过公共入口");
        Check((bool)n.ActiveInterpreter.Eval("(member? \"新日开始\" seen-timings)")!, "新日事实更新经过公共入口");
        bool eddieChecked = false;
        for (int seed = 1; seed <= 20 && !eddieChecked; seed++)
        {
            var d = new SceneManager(new GameState(), new LocalScriptLoader()); d.LoadScene("world");
            d.ActiveInterpreter.Eval("(第一幕 'load! '((\"opened\" #t) (\"tavern\" #t) (\"act\" 1)))");
            d.ActiveInterpreter.Eval("(码头工作 'load! '((\"work\" 4) (\"met-day\" 1) (\"ship-day\" 100)))");
            d.Refresh(); var a = d.GameState.Team.FindActor("player")!;
            int i = a.ActionDice.Count - 1; a.ActionDice[i] = 1;
            GameRandom.Reseed(seed);
            var report = d.ExecuteAction(Nodes(d.CurrentRootNode!).Single(node => node.Id == "搬运"),
                new() { new SlottedResource { Type = "die", ActorId = a.Id, Value = 1, DieIndex = a.ActionDiceSlotIds[i] } });
            if (report.Outcome != RollOutcome.Fail) continue;
            Check(report.Banter.Count > 0, "真实搬运失败由艾迪模块提供救场");
            Check(Convert.ToInt32(d.ActiveInterpreter.Eval("(assoc-get (assoc-get (assoc-get (艾迪生活 'save) \"interactions\" '()) \"艾迪搬运搭手\" '()) \"count\" 0)")) == 1, "真实艾迪救场记录已提交");
            i = a.ActionDice.Count - 1;
            var secondReport = d.ExecuteAction(Nodes(d.CurrentRootNode!).Single(node => node.Id == "搬运"),
                new() { new SlottedResource { Type = "die", ActorId = a.Id, Value = a.ActionDice[i], DieIndex = a.ActionDiceSlotIds[i] } });
            Check(secondReport.Banter.Count == 0, "同日再次搬运不重复救场");
            eddieChecked = true;
        }
        Check(eddieChecked, "覆盖一次真实搬运失败分支");
        // 冷加载真实人物存档；旧的码头help-day迁移到艾迪互动记录。
        var w = new SceneManager(new GameState(), new LocalScriptLoader()); w.LoadScene("world");
        w.ActiveInterpreter.Eval("(前期存档迁移! (list (list \"dock\" '((\"help-day\" 1))) (list \"eddie-life\" '())))");
        Check(Convert.ToInt32(w.ActiveInterpreter.Eval("(assoc-get (assoc-get (assoc-get (艾迪生活 'save) \"interactions\" '()) \"艾迪搬运搭手\" '()) \"last-day\" 0)")) == 1, "旧救场日期迁入人物记录");
        var path = Path.Combine(Path.GetTempPath(), "ssnoir-interaction-" + Guid.NewGuid() + ".json");
        try
        {
            w.SaveGame(path); var next = new SceneManager(new GameState(), new LocalScriptLoader()); next.LoadGame(path);
            Check(Convert.ToInt32(next.ActiveInterpreter.Eval("(assoc-get (assoc-get (assoc-get (艾迪生活 'save) \"interactions\" '()) \"艾迪搬运搭手\" '()) \"last-day\" 0)")) == 1, "人物互动记录真实存读档");
        }
        finally { File.Delete(path); }
    }
}
