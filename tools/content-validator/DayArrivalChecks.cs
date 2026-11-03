using SSNoir.Core;

namespace SSNoir.Testing;

public static class DayArrivalChecks
{
    static void Check(bool ok, string name)
    {
        if (!ok) throw new InvalidOperationException(name);
        Console.WriteLine("OK: " + name);
    }
    static SceneManager World()
    {
        var s = new SceneManager(new GameState(), new LocalScriptLoader());
        s.LoadScene("world");
        s.ActiveInterpreter.Eval("(第一幕 'load! '((\"opened\" #t) (\"tavern\" #t) (\"act\" 2) (\"rehearsal-seen\" #t)))");
        s.Refresh();
        return s;
    }
    static string Text(ActionReport? report) => report == null ? "" : string.Join("\n",
        report.BlockingStorySteps.Concat(report.PostSceneBlockingSteps)
            .SelectMany(step => step.Dialogue?.Lines.Select(line => line.Text) ?? Array.Empty<string>()));
    static SceneManager Reload(SceneManager s)
    {
        string path = Path.Combine(Path.GetTempPath(), "ssnoir-day-" + Guid.NewGuid() + ".json");
        try { s.SaveGame(path); var next = new SceneManager(new GameState(), new LocalScriptLoader()); next.LoadGame(path); return next; }
        finally { File.Delete(path); }
    }
    public static void Run()
    {
        foreach (bool paid in new[] { true, false })
        {
            var s = World();
            s.GameState.Inventory.SetCount("金钱", 80);
            s.ActiveInterpreter.Eval("(home 'load! (map (lambda (entry) (if (equal? (car entry) \"rent-left\") (list \"rent-left\" 1) entry)) (home 'save)))");
            s.Refresh();
            var end = s.EndTurn();
            Check(s.GameState.Get<int>("世界日") == 2, "正常换日推进一次");
            Check(s.GameState.Inventory.GetCount("金钱") == 80, "房费到期不提前扣钱");
            Check(!Text(end).Contains("房费") && !Text(end).Contains("房钱"), "日终不播放收租通知");
            Check(s.EnterPlace("诊所") == null && s.EnterPlace("世界") == null, "医院和世界地图不提醒也不收租");
            // 到期时付得起，回来时可能已在医生或酒馆花掉：收租读取碰面当下余额。
            s.GameState.Inventory.SetCount("金钱", paid ? 60 : 10);
            s.EndTurn();
            Check((int)s.ActiveInterpreter.Eval("(assoc-get (home 'save) \"rent-left\" -1)")! == 0,
                "未回旅馆期间到期停在零，不重复计租");
            s = Reload(s);
            Check(s.GameState.Inventory.GetCount("金钱") == (paid ? 60 : 10), "到期待收存档不触发扣款");
            var meeting = s.EnterPlace("家");
            Check(Text(meeting).Contains(paid ? "点清四十美元" : "房间先锁着"), "回旅馆同一事件交代付款或锁门");
            Check(s.GameState.Inventory.GetCount("金钱") == (paid ? 20 : 10), "按回来时余额结算，付不起不扣款");
            Check(s.CurrentRootNode!.Children.Single(node => node.Id == "家").Children.Any(node => node.Id == "租屋") == paid,
                "房间入口和付款结果在同次事件中更新");
            Check(meeting!.Effects.Count > 0 == paid, "成功收租在同一入场报告中记录结算");
            Check((bool)s.ActiveInterpreter.Eval("(assoc-get (home 'save) \"evicted?\" #f)")! == !paid, "锁门状态和收租结果一致");
            Check(s.EnterPlace("世界") == null && s.EnterPlace("家") == null, "出门不补通知，再次回家不重复收租或催缴");
            if (!paid)
            {
                s.GameState.Inventory.SetCount("金钱", 40);
                s.Refresh();
                var retry = s.EnterPlace("家");
                Check(Text(retry).Contains("点清四十美元") && s.GameState.Inventory.GetCount("金钱") == 0,
                    "凑齐钱回旅馆，在同次收租事件中付款开门");
            }
            s = Reload(s);
            Check(s.EnterPlace("家") == null, "付款后冷读档不重收");
        }

        var collapse = World();
        collapse.ActiveInterpreter.Eval("(define-turn-start-rule \"送医新日检查\" (lambda () #t) (lambda () (set-global! '送医新日已执行 #t)))");
        collapse.LoadScene("核赔");
        collapse.GameState.Inventory.SetCount("金钱", 80);
        collapse.GameState.Team.Injure(Injury.MaxSeverity - 1);
        collapse.GameState.Team.SpendComposure("player", TeamState.MaxComposure);
        var report = collapse.EndTurn();
        Check(report.TurnEnded && report.WakePlaceName == "诊所", "送医声明换日黑场与诊所落点");
        Check(report.BlockingStorySteps.Any(step => step.Spotlight?.Title == "你倒下了"), "倒下提示在旧场景步骤中");
        Check(!report.BlockingStorySteps.Any(step => step.Kind == BlockingStoryStepKind.EnterPlace), "现场提示前不进入诊所");
        Check(report.PostSceneBlockingSteps[0].Spotlight?.Title == "在诊所醒来", "落地后先交代诊所醒来");
        Check(collapse.GameState.Get<int>("世界日") == 2 && collapse.LatestSnapshot.Actors.First(a => a.Id == "player").ActionDice.Count > 0,
            "醒来快照已经是次日的新骰");
        Check(collapse.GameState.Get<bool>("送医新日已执行"), "送医同样执行新日状态规则");

        var actionCollapse = World();
        actionCollapse.LoadScene("核赔");
        actionCollapse.GameState.Inventory.SetCount("金钱", 80);
        actionCollapse.GameState.Team.Injure(Injury.MaxSeverity - 1);
        actionCollapse.ActiveInterpreter.Eval("(define (get-render-data) (container \"核赔\" (list (instant-action \"倒下检查\" (lambda () (injure!))))))");
        actionCollapse.Refresh();
        var actionReport = actionCollapse.ExecuteAction(actionCollapse.CurrentRootNode!.Children.Single(), new List<SlottedResource?>());
        Check(actionReport.TurnEnded && actionReport.WakePlaceName == "诊所"
            && actionCollapse.GameState.Get<int>("世界日") == 2, "动作中倒下同样只换日一次并在黑场送医");

        var ship = World();
        ship.ActiveInterpreter.Eval("(set! world-day 8) (set-global! '世界日 8)"); ship.Refresh();
        var morning = ship.EndTurn();
        Check(!Text(morning.TurnStartReport).Contains("大船"), "招工不在醒来时广播");
        Check(Text(ship.EnterPlace("世界")).Contains("大船今天"), "当天出门才收到大船招工消息");
        Check(ship.EnterPlace("世界") == null, "招工消息只播一次");
        var chapter = World();
        chapter.ActiveInterpreter.Eval("(第二章 'debug-start!) (set! world-day 9) (set-global! '世界日 9)");
        var dayState = new ActionReport { Type = ActionType.Instant };
        chapter.GameState.CurrentActionReport = dayState;
        try { chapter.ActiveInterpreter.Eval("(on-turn-start)"); }
        finally { chapter.GameState.CurrentActionReport = null; }
        chapter.Refresh();
        Check(dayState.BlockingStorySteps.Count == 0, "第二章新日推进状态，计划、报纸和培训不在醒来时广播");
        var cityNews = chapter.EnterPlace("世界");
        Check(Text(cityNews).Contains("新港计划通过"), "新港计划在世界入口告知");
        Check(cityNews!.BlockingStorySteps.Any(step => step.Spotlight?.Title == "《逃出老街的夜莺》"), "报道在世界入口告知");
        chapter = Reload(chapter);
        Check(chapter.EnterPlace("世界") == null, "第二章已读消息冷读档后不重播");

        Console.WriteLine("送医与每日入场检查通过");
    }
}
