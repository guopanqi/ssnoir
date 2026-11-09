using SSNoir.Core;
namespace SSNoir.Testing;
public static class LifeEventChecks
{
    static void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); Console.WriteLine("[life] OK: " + label); }
    static IEnumerable<GameNode> Nodes(GameNode n) => new[] { n }.Concat(n.Children.SelectMany(Nodes));
    public static void Run()
    {
        var s = new SceneManager(new GameState(), new LocalScriptLoader()); s.LoadScene("world");
        object? E(string code) => s.ActiveInterpreter.Eval(code);
        int N(string code) => Convert.ToInt32(E(code));
        void Day(int day)
        {
            while (N("world-day") < day) { E("(on-turn-end)"); E("(on-turn-start)"); }
            s.Refresh();
        }
        E("(第一幕 'load! '((\"opened\" #t) (\"tavern\" #t) (\"act\" 1)))");
        E("(居民区相识 'load! '((\"invited\" #t) (\"visited\" #t) (\"familiarity\" 8)))");
        E("(院里的炉火 'unlock!)"); s.Refresh();
        Check(N("(assoc-get (院里的炉火 'save) \"next\" 0)") == N("world-day") + 3 &&
              Nodes(s.CurrentRootNode!).Any(n => n.Id == "院里聚餐等待") &&
              !Nodes(s.CurrentRootNode!).Any(n => n.Id == "院里聚餐近况"),
            "解锁当天立刻安排三天 Clock，不再出现准备中的 note");
        E("(院里的炉火 'load! '((\"next\" 12)))");
        Day(10); Check(N("(assoc-get (院里的炉火 'save) \"next\" 0)") == 12, "熟悉邻里后安排第一场聚餐");
        Day(12); s.GameState.Team.FindActor("player")!.Composure = 1;
        var gather = Nodes(s.CurrentRootNode!).Single(n => n.Id == "院里聚餐");
        var gatheringReport = s.ExecuteAction(gather, new());
        Check(gatheringReport.BlockingStorySteps.Single().Spotlight?.Effects.Single().Delta == 2,
            "聚餐迁为单页并显示真实恢复效果");
        Check(s.GameState.Team.FindActor("player")!.Composure == 3 && Nodes(s.CurrentRootNode!).Single(n => n.Id == "院里聚餐").Disabled, "聚餐免费恢复二点且本场只能一次");
        Day(13); Check(N("(assoc-get (院里的炉火 'save) \"next\" 0)") == 16, "散场后第二天显示三天等待");
        // 固定随机种子只用于覆盖留酒分支，不评估实际概率手感。
        Day(16); GameRandom.Reseed(1); Day(17);
        bool gotGift = (bool)E("(assoc-get (院里的炉火 'save) \"gift-arranged\" #f)")!;
        for (int day = 21; day <= 49 && !gotGift; day += 4)
        { Day(day); gotGift = (bool)E("(assoc-get (院里的炉火 'save) \"gift-arranged\" #f)")!; }
        Check(gotGift, "缺席场次结算产生留酒实例");
        var entries = Nodes(s.CurrentRootNode!).Where(n => n.Id.StartsWith("邻里留酒@") && n.Resolve != null).ToList();
        Check(entries.Count >= 1, "留酒只显示交谈入口");
        var path = Path.Combine(Path.GetTempPath(), "ssnoir-life-" + Guid.NewGuid() + ".json");
        try
        {
            s.SaveGame(path);
            var expired = new SceneManager(new GameState(), new LocalScriptLoader()); expired.LoadGame(path);
            var deadline = Convert.ToInt32(expired.ActiveInterpreter.Eval("(assoc-get (院里的炉火 'save) \"next\" 0)"));
            expired.ActiveInterpreter.Eval($"(set! world-day {deadline})");
            expired.ActiveInterpreter.Eval($"(set-global! '世界日 {deadline})");
            expired.ActiveInterpreter.Eval("(on-turn-start)"); expired.Refresh();
            Check(!Nodes(expired.CurrentRootNode!).Any(n => n.Id.StartsWith("邻里留酒@")), "下一场聚餐开始即收回未领取的酒");
            var restored = new SceneManager(new GameState(), new LocalScriptLoader()); restored.LoadGame(path); s = restored;
        }
        finally { File.Delete(path); }
        var gift = Nodes(s.CurrentRootNode!).First(n => n.Id.StartsWith("邻里留酒@") && n.Resolve != null);
        var giftReport = s.ExecuteAction(gift, new());
        Check(giftReport.BlockingStorySteps.Single().Spotlight?.Effects.Any(effect => effect.Label == "邻里留酒" && effect.Delta == 1) == true,
            "留酒迁为单页并显示实际物品奖励");
        Check(s.GameState.Inventory.GetCount("邻里留酒") == 1 && !Nodes(s.CurrentRootNode!).Any(n => n.Id.StartsWith("邻里留酒@")), "读档后只领一次，所有地点入口同步结束");
        var drink = Nodes(s.CurrentRootNode!).First(n => n.Id.StartsWith("喝邻里留酒@"));
        s.GameState.Team.FindActor("player")!.Composure = 1;
        s.ExecuteAction(drink, new() { new SlottedResource { Type = "item", ItemId = "邻里留酒", Qty = 1 } });
        Check(s.GameState.Team.FindActor("player")!.Composure == 2 && s.GameState.Inventory.GetCount("邻里留酒") == 0, "赠酒消费一瓶只恢复一点");
        // 艾迪不获帮助也一定完成；一次伤手只倒退两格。
        E("(艾迪的房间 'load! '((\"known\" #t) (\"checked\" 40) (\"savings\" 6)))");
        Day(41); Check(N("(assoc-get (艾迪的房间 'save) \"hurt-left\" 0)") == 2, "储蓄中段只出现一次伤手");
        Day(43); Check(N("(assoc-get (艾迪的房间 'save) \"savings\" 0)") == 4, "不帮忙最多损失两格");
        Day(51); Check(N("(assoc-get (艾迪的房间 'save) \"savings\" 0)") == 12, "不介入也自行攒够房钱");
        var movingReport = s.EnterPlace("码头")!;
        Check(movingReport.BlockingStorySteps.First().Kind == BlockingStoryStepKind.Spotlight &&
              movingReport.BlockingStorySteps.Last().Kind == BlockingStoryStepKind.Dialogue,
            "艾迪搬家以单页开场，再保留本人分享喜悦的对白");
        Day(60); Check(N("(assoc-get (艾迪的房间 'save) \"savings\" 0)") == 12, "完成后不会反复受伤倒退");
        E("(艾迪的房间 'load! '((\"known\" #t) (\"checked\" 60) (\"savings\" 6) (\"hurt-once\" #t) (\"hurt-left\" 2) (\"help-day\" 60)))");
        Day(61); Check(N("(assoc-get (艾迪的房间 'save) \"savings\" 0)") == 6, "当天照顾保护下一次日结开销");
        // 亲近期与伤势条件由真实触发器检查，命中通过种子覆盖。
        E("(第一幕 'load! '((\"opened\" #t) (\"tavern\" #t) (\"act\" 2)))");
        E("(第二幕生活 'remember-manager!)"); E("(第三幕 'start!)");
        E("(夜莺生活 'load! '())");
        E("(injure-badly!)");
        bool healed = false;
        ActionReport? careReport = null;
        for (int seed = 1; seed <= 50 && !healed; seed++)
        {
            GameRandom.Reseed(seed);
            var report = new ActionReport(); s.GameState.CurrentActionReport = report;
            try { E("(dispatch-interactions! '((\"timing\" \"房间睡前\") (\"place\" \"家\")))"); healed = s.GameState.Team.Injury.Severity == 2; if (healed)
                {
                    careReport = report;
                    Check(!(bool)E("(dispatch-interactions! '((\"timing\" \"房间睡前\") (\"place\" \"家\")))")!,
                        "同一睡前报告已有包扎，不再派发宵夜");
                } }
            finally { s.GameState.CurrentActionReport = null; }
        }
        Check(healed && s.GameState.Team.Injury.Severity == 2, "亲近期睡前照顾只恢复一格伤势");
        var sheet = careReport!.BlockingStorySteps.Single(step => step.Kind == BlockingStoryStepKind.Spotlight).Spotlight!;
        Check(sheet.Paragraphs.Count == 3 && sheet.Paragraphs.All(p => p.Image.Length > 0), "包扎单页有三段配图");
        Check(sheet.Effects.Single().Delta == -1 && careReport.Effects.Count == 0, "真实治疗效果归属单页，不重复显示");
        Check(careReport.BlockingStorySteps.Last().Kind == BlockingStoryStepKind.Dialogue, "首次包扎单页后接对白");
        E("(try-interactions! (夜莺生活 'interactions) '((\"timing\" \"房间睡前\")))");
        Check(s.GameState.Team.Injury.Severity == 2, "冷却期间其他事件可发生，但不会反复治疗");
        Day(65);
        bool secondCare = false;
        ActionReport? secondReport = null;
        for (int seed = 1; seed <= 50 && !secondCare; seed++)
        { GameRandom.Reseed(seed); secondReport = new ActionReport(); s.GameState.CurrentActionReport = secondReport;
          try { secondCare = (bool)E("(try-interactions! (夜莺生活 'interactions) '((\"timing\" \"房间睡前\")))")!; } finally { s.GameState.CurrentActionReport = null; } }
        Check(secondCare && s.GameState.Team.Injury.Severity == 1, "四天后最多再照顾一次");
        Check(secondReport!.BlockingStorySteps.Count == 1 && secondReport.BlockingStorySteps[0].Kind == BlockingStoryStepKind.Spotlight, "再次包扎只有单页，没有重复对白");
        var scoped = new ActionReport();
        scoped.AddClockEffect("测试钟", "测试钟", 1, BeneficialDirection.Increase);
        int effectStart = scoped.BeginEffectCapture();
        scoped.AddClockEffect("测试钟", "测试钟", 2, BeneficialDirection.Increase);
        Check(scoped.EndEffectCapture(effectStart).Single().Delta == 2 && scoped.Effects.Single().Delta == 1,
            "单页 Clock 效果不会吞并外层同名 Clock");
        var noticeReport = new ActionReport(); s.GameState.CurrentActionReport = noticeReport;
        try
        {
            E("(spotlight! \"告示\" \"一封新的来信。\")");
            Check(noticeReport.BlockingStorySteps.Single().Spotlight!.Body == "一封新的来信。", "旧标题正文入口迁入统一单页");
            s.GameState.Team.FindActor("player")!.Composure = 1;
            E("(spotlight-page! \"\" '((\"一句文字，两种效果。\" \"\")) (lambda () (heal-injury! 1) (restore-actor-composure! \"player\" 1)))");
            Check(noticeReport.BlockingStorySteps.Last().Spotlight!.Effects.Count == 2 && noticeReport.Effects.Count == 0,
                "单页显示多个真实效果，外层不重复报账");
        }
        finally { s.GameState.CurrentActionReport = null; }
        foreach (bool paid in new[] { true, false })
        {
            var rent = new SceneManager(new GameState(), new LocalScriptLoader()); rent.LoadScene("world");
            rent.ActiveInterpreter.Eval("(第一幕 'load! '((\"opened\" #t) (\"tavern\" #t) (\"act\" 1)))");
            rent.ActiveInterpreter.Eval("(home 'load! (map (lambda (entry) (cond ((equal? (car entry) \"rent-left\") (list \"rent-left\" 0)) ((equal? (car entry) \"boss-visited?\") (list \"boss-visited?\" #t)) (else entry))) (home 'save)))");
            rent.GameState.Inventory.SetCount("金钱", paid ? 60 : 10); rent.Refresh();
            var rentReport = rent.EnterPlace("家")!;
            Check(rent.GameState.Inventory.GetCount("金钱") == (paid ? 20 : 10), "再次收租只在付得起时扣四十美元");
            Check(paid ? rentReport.BlockingStorySteps.Single().Spotlight?.Effects.Any(e => e.Delta == -40) == true
                       : rentReport.BlockingStorySteps.Single().Kind == BlockingStoryStepKind.Dialogue,
                paid ? "再次付清房租使用单页与真实扣款标签" : "交不起房租仍使用对白");
        }
        var tavern = new SceneManager(new GameState(), new LocalScriptLoader()); tavern.LoadScene("world");
        tavern.ActiveInterpreter.Eval("(第一幕 'load! '((\"opened\" #t) (\"tavern\" #t) (\"act\" 1)))");
        tavern.ActiveInterpreter.Eval("(酒馆起步 'load! '((\"stage\" \"待处理\")))"); tavern.Refresh();
        tavern.ExecuteAction(Nodes(tavern.CurrentRootNode!).Single(n => n.Id == "醉客互相叫骂"), new());
        tavern.GameState.Team.Injure(Injury.MaxSeverity - 1);
        tavern.ActiveInterpreter.Eval("(define (get-render-data) (container \"倒下测试\" (list (instant-action \"受伤检查\" (lambda () (injure!))))))"); tavern.Refresh();
        var collapseReport = tavern.ExecuteAction(Nodes(tavern.CurrentRootNode!).Single(n => n.Id == "受伤检查"), new());
        Check(collapseReport.WakePlaceName == "诊所" && !collapseReport.PostSceneBlockingSteps.Any(step => step.Kind == BlockingStoryStepKind.Dialogue),
            "酒馆倒下送医不会把酒保对白排到医院");
        var returnReport = tavern.EnterPlace("老街酒馆");
        Check(returnReport != null && returnReport.BlockingStorySteps.Any(step => step.Kind == BlockingStoryStepKind.Dialogue),
            "送医后回酒馆才当面交代收场与推荐");
        var secondVisit = tavern.EnterPlace("老街酒馆");
        Check(secondVisit == null || !secondVisit.BlockingStorySteps.Any(step => step.Dialogue != null &&
            step.Dialogue.Lines.Any(line => line.Text.Contains("先帮我把场子看稳") || line.Text.Contains("那天后来有人叫了巡警"))),
            "回酒馆的收场对白只播放一次");
        Day(69); E("(injure!)");
        Check(!(bool)E("(try-interactions! (夜莺生活 'interactions) '((\"timing\" \"房间睡前\")))")!, "次数耗尽后不会替代医生持续治疗");
    }
}
