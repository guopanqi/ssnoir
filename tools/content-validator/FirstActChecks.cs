using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SSNoir.Core;

namespace SSNoir.Testing;

/// <summary>第一幕的真实入口、资源成本、缺席推进与存档；不把基线打法当手感验收。</summary>
public static class FirstActChecks
{
    private static void Assert(bool ok, string message)
    {
        if (!ok) throw new Exception("[first-act] " + message);
        Console.WriteLine("[first-act] OK: " + message);
    }
    private static IEnumerable<GameNode> Nodes(GameNode root)
        => new[] { root }.Concat(root.Children.SelectMany(Nodes));
    private static GameNode Find(SceneManager scenes, string id)
        => Nodes(scenes.CurrentRootNode!).Single(n => n.Id == id);
    private static bool Has(SceneManager scenes, string id)
        => Nodes(scenes.CurrentRootNode!).Any(n => n.Id == id);
    private static ActionReport Act(SceneManager scenes, string id)
    {
        var node = Find(scenes, id);
        var slots = new List<SlottedResource?>();
        foreach (var cost in node.Requires)
        {
            if (cost.Type != "die") throw new Exception("本检查只投真实行动骰");
            var player = scenes.GameState.Team.FindActor("player")!;
            int index = player.ActionDice.IndexOf(player.ActionDice.Max());
            slots.Add(new SlottedResource { Type = "die", ActorId = "player",
                Value = player.ActionDice[index], DieIndex = player.ActionDiceSlotIds[index] });
        }
        return scenes.ExecuteAction(node, slots);
    }
    private static IEnumerable<string> Lines(ActionReport? report)
        => report == null ? Array.Empty<string>() : report.BlockingStorySteps
            .Concat(report.PostSceneBlockingSteps).Where(s => s.Dialogue != null)
            .SelectMany(s => s.Dialogue!.Lines.Select(l => l.Text));
    private static SceneManager Start()
    {
        GameRandom.Reseed(7);
        var scenes = new SceneManager(new GameState(), new LocalScriptLoader());
        scenes.LoadScene("world");
        Assert(Has(scenes, "旅馆清晨") && !Has(scenes, "老街酒馆"), "新档从旅馆清晨开始");
        int dice = scenes.GameState.Team.FindActor("player")!.ActionDice.Count;
        var report = Act(scenes, "旅馆清晨");
        Assert(scenes.GameState.Team.FindActor("player")!.ActionDice.Count == dice,
            "开场不消耗行动骰或偷换日期");
        Assert(report.BlockingStorySteps.All(s => s.Kind == BlockingStoryStepKind.Dialogue),
            "开场暂时只使用 Dialogue");
        Assert(Has(scenes, "老街酒馆") && !Has(scenes, "码头"), "酒馆亮相前不开放工作地图");
        Assert(scenes.GameState.RestBlockers.Count == 0,
            "首次酒馆入场对白不标成必须处理，不留下空的强制入口");
        Assert(Lines(scenes.EnterPlace("老街酒馆")).Any(), "首次进酒馆仍自动播放入场对白");
        Assert(scenes.EnterPlace("老街酒馆") == null, "酒馆入场对白播完不重复出现");
        Assert(Has(scenes, "搬运") && Has(scenes, "咖啡馆帮工") && Has(scenes, "看场子"),
            "固定亮相后直接开放三份工作");
        Assert(!Has(scenes, "探索码头") && !Has(scenes, "醉酒的尼尔"), "没有旧探索前置与旧开场");
        return scenes;
    }
    public static void CheckSecondActMatters()
    {
        var scenes = new SceneManager(new GameState(), new LocalScriptLoader());
        scenes.LoadScene("world");
        void Eval(string source) { scenes.ActiveInterpreter.Eval(source); scenes.Refresh(); }
        DossierEntry Entry(string id) => scenes.CurrentDossier.Single(e => e.Id == id);
        Eval("""(第一幕 'load! (list (list "opened" #t) (list "tavern" #t) (list "act" 2) (list "singer" 2) (list "lucy" 1) (list "eddie" 1))) (set! world-day 7)""");
        Assert(Entry("夜莺：大剧院").Now.Contains("明天"), "第二幕明天彩排独立入事项");
        Assert(!scenes.CurrentDossier.Any(e => e.Id.StartsWith("露西：") || e.Id.StartsWith("艾迪：")),
            "认识人物但未听请求，不提前透露私人事项");
        Eval("(set! world-day 8)");
        Assert(Entry("夜莺：大剧院").Now.Contains("今天"), "彩排当天自动更新相对日期");
        Eval("(set! world-day 9)");
        scenes.EnterPlace("街角咖啡馆");
        Assert(!scenes.CurrentDossier.Any(e => e.Id == "露西：夜校报名"), "仅进入咖啡馆不应揭示露西的私事");
        Act(scenes, "问露西报名");
        Assert(Entry("露西：夜校报名").Now.Contains("3天后"), "听到报名请求后显示真实截止日期");
        scenes.EnterPlace("码头");
        Act(scenes, "问艾迪工钱");
        Assert(Entry("艾迪：少了十块钱").Now.Contains("后天"), "工钱纠纷显示封账期限");
        scenes.EnterPlace("剧院");
        Act(scenes, "看看剧院招工告示");
        Assert(Entry("夜莺：大剧院").Now.Contains("明天起"), "听到试演安排后归入夜莺同一条线");
        Assert(!Entry("夜莺：大剧院").Now.Contains("故障"), "尚未遇见后台事件时不透露故障");
        Act(scenes, "听听老板怎么说");
        Assert(Entry("露西：夜校报名").Now.Contains("还需顶2班"),
            "听完老板的难处才开放两班或三十美元的真实选择");
        Act(scenes, "替露西顶班");
        Assert(Entry("露西：夜校报名").Status == "进行中"
            && Entry("露西：夜校报名").Now.Contains("还需顶1班"),
            "替班一回不会自动解决报名，还需腾出第二班或支付15美元");
        Act(scenes, "替露西顶班");
        Assert(Entry("露西：夜校报名").Status == "等着别人"
            && Entry("露西：夜校报名").Now.Contains("还没听到报名结果"),
            "真正投入两颗骰子完成顶班后才等待报名结果");
        Eval("(set! world-day 10)");
        scenes.EnterPlace("剧院");
        Assert(!Entry("夜莺：大剧院").Now.Contains("故障"), "只进入剧院不会自动触发试演故障");
        Act(scenes, "看看赞助人试演");
        Assert(Entry("夜莺：大剧院").Now.Contains("故障"), "亲眼遇到故障才更新当前行动");
        Assert(scenes.CurrentDossier.Count(e => e.Id.StartsWith("夜莺：")) == 1, "彩排和试演只占同一条故事线");
        Eval("(set! world-day 11)");
        Assert(Entry("艾迪：少了十块钱").Now.Contains("今天收工"), "封账当天提醒今天处理");
        Assert(Find(scenes, "少了十块钱").Clocks.Single().Current == 1, "封账当天行动期限剩一日");
        Eval("(set! world-day 12)");
        Assert(!Has(scenes, "少了十块钱"), "封账之后争执行动消失");
        scenes.EnterPlace("码头");
        Act(scenes, "问艾迪后来的事");
        Assert(Entry("艾迪：少了十块钱").Now.Contains("自己去谈"), "错过争执也会用亲耳听到的余波更新结果");
        Eval("(set! world-day 13)");
        scenes.EnterPlace("街角咖啡馆");
        Act(scenes, "问露西报名结果");
        Assert(Entry("露西：夜校报名").Status == "了结"
            && Entry("露西：夜校报名").Now.Contains("交上报名表"), "听到报名成功后同一事项了结");
        Eval("(set-global! '世界日 world-day)");
        string path = Path.Combine(Path.GetTempPath(), "ssnoir-second-act-matters.json");
        try
        {
            scenes.SaveGame(path);
            var restored = new SceneManager(new GameState(), new LocalScriptLoader());
            restored.LoadGame(path);
            Assert(restored.CurrentDossier.Single(e => e.Id == "露西：夜校报名").Now == Entry("露西：夜校报名").Now,
                "冷读档恢复报名结果，无需另存事项状态");
        }
        finally { if (File.Exists(path)) File.Delete(path); }

        // 计划性投入：与公开日程同一天看到准备机会，进入交锋后能兑现。
        var prep = new SceneManager(new GameState(), new LocalScriptLoader());
        prep.LoadScene("world");
        void PrepEval(string source) { prep.ActiveInterpreter.Eval(source); prep.Refresh(); }
        PrepEval("""(第一幕 'load! (list (list "opened" #t) (list "tavern" #t) (list "act" 2) (list "singer" 2) (list "eddie" 1))) (set! world-day 9)""");
        Act(prep, "看看剧院招工告示");
        Assert(Has(prep, "检查伴奏线路") && Has(prep, "预订旧乐手"),
            "D9 已知试演日程后，可选择用骰子查线路或花钱请乐手");
        Act(prep, "检查伴奏线路");
        Assert(!Has(prep, "检查伴奏线路"),
            "线路检查只可尝试一次，不许重复试出好结果");
        PrepEval("""(第二幕剧院 'load! (list (list "equipment-checked" #t) (list "equipment-ready" #t) (list "musician-booked" #t))) (set! world-day 10)""");
        Assert(!Has(prep, "预订旧乐手"), "已预约后不能再次付定金");
        Act(prep, "看看赞助人试演");
        Act(prep, "帮她想办法");
        Assert(Has(prep, "请已预约的乐手"),
            "已付订金的乐手在原生试演交锋中提供实际路线");

        var wage = new SceneManager(new GameState(), new LocalScriptLoader());
        wage.LoadScene("world");
        void WageEval(string source) { wage.ActiveInterpreter.Eval(source); wage.Refresh(); }
        WageEval("""(第一幕 'load! (list (list "opened" #t) (list "tavern" #t) (list "act" 2) (list "eddie" 1))) (set! world-day 9) (第二幕 'load! (list (list "wage-introduced" #t)))""");
        Assert(Has(wage, "查工时记录"), "已知工钱争议可在交锋前调查工时");
        Act(wage, "查工时记录");
        Assert(!Has(wage, "查工时记录"), "调查机会不能靠重复刷骰子规避风险");
        WageEval("""(第二幕 'load! (list (list "wage-introduced" #t) (list "wage-checked" #t) (list "wage-evidence" #t)))""");
        Act(wage, "少了十块钱");
        Assert(Has(wage, "出示工时记录"), "取得的实际记录在原生工钱交锋中兑现");
    }
    public static void Run()
    {
        var tavern = Start(); Act(tavern, "看场子");
        Assert(!Has(tavern, "醉客互相叫骂"), "第一次看场不提前触发两桌争吵");
        Act(tavern, "看场子");
        Assert(Has(tavern, "醉客互相叫骂"), "看场后进入两桌争吵的检查场景");
        Assert(Find(tavern, "看场子").Disabled && Find(tavern, "睡觉").Disabled,
            "争吵必须处理，不能继续看场或睡觉跳过");
        string troubleSave = Path.Combine(Path.GetTempPath(), "ssnoir-trouble-save.json");
        try
        {
            tavern.SaveGame(troubleSave);
            var restored = new SceneManager(new GameState(), new LocalScriptLoader()); restored.LoadGame(troubleSave);
            Assert(Find(restored, "睡觉").Disabled && Has(restored, "醉客互相叫骂"), "读档恢复必须处理事件与休息阻塞");
        }
        finally { if (File.Exists(troubleSave)) File.Delete(troubleSave); }
        Act(tavern, "醉客互相叫骂");
        Assert(!Has(tavern, "交给酒保"), "酒馆交锋没有主动失败按钮");
        var beforeRise = tavern.CurrentRootNode;
        var frame = tavern.BeginRoundTransition();
        bool sawRise = false;
        while (!frame.IsFinished)
        {
            if (frame.Report.BlockingStorySteps.Any(s => s.Kind == BlockingStoryStepKind.FocusNode))
            {
                sawRise = true;
                Assert(frame.Report.BlockingStorySteps.Select(s => s.Kind).SequenceEqual(
                    new[] { BlockingStoryStepKind.Dialogue, BlockingStoryStepKind.FocusNode }),
                    "醉客起身报告先说明，再声明导航落点");
                Assert(frame.Report.BlockingStorySteps.Last().TargetNodeId == "醉客互相叫骂",
                    "醉客起身明确回到交锋总览");
                Assert(!Nodes(beforeRise!).Any(n => n.Id == "破口大骂的人")
                    && Nodes(frame.Snapshot.RootNode!).Any(n => n.Id == "破口大骂的人"),
                    "新麻烦只存在于待提交快照，旧树不提前变化");
                Assert(frame.Report.Banter.Count == 1 && frame.Report.Banter[0].Lines.Count == 2,
                    "两句对骂保存在落地后的气泡序列");
            }
            frame = tavern.AdvanceRoundTransition();
        }
        Assert(sawRise, "真实回合转换触发醉客起身表现");
        bool rejectedFocus = false;
        try { tavern.ActiveInterpreter.Eval("(focus-node! \"醉客互相叫骂\")"); }
        catch { rejectedFocus = true; }
        Assert(rejectedFocus, "报告外不能偷偷提交或导航");
        tavern.ActiveInterpreter.Eval("(begin (东边 'load! 0) (set! 骂人位置 \"西边\"))");
        tavern.RebuildRenderTree();
        Assert(Find(tavern, "东边这桌：劝").Disabled && Find(tavern, "东边这桌：威胁").Disabled
            && !Find(tavern, "西边这桌：劝").Disabled, "冷静桌的动作禁用，另一桌不受影响");
        Assert(Find(tavern, "骂人者：回合规则").Resolve!.NoteText.Contains("东边这桌"),
            "骂人者常驻标注预告受影响的一桌与回合时机");
        var insults = new List<ActionReport>();
        frame = tavern.BeginRoundTransition();
        while (!frame.IsFinished)
        {
            if (frame.Report.Banter.Any(b => b.Lines.Any(l => l.Speaker == "破口大骂的人")))
                insults.Add(frame.Report);
            frame = tavern.AdvanceRoundTransition();
        }
        Assert(insults.Count == 1 && insults[0].Effects.Any(e => e.Kind == ActionEffectKind.Clock && e.Delta == 1),
            "每回合叫骂气泡和对桌愤怒加一属于同一报告");
        Assert(!Find(tavern, "东边这桌：劝").Disabled && !Find(tavern, "东边这桌：威胁").Disabled,
            "对桌骂人使愤怒回升后恢复动作");
        tavern.ActiveInterpreter.Eval("(begin (西边 'load! 0) (set! 骂人位置 \"东边\"))");
        tavern.RebuildRenderTree();
        Assert(Find(tavern, "西边这桌：劝").Disabled && Find(tavern, "西边这桌：威胁").Disabled,
            "西边归零同样禁用动作");
        tavern.EndTurn();
        Assert(!Find(tavern, "西边这桌：劝").Disabled && !Find(tavern, "西边这桌：威胁").Disabled,
            "西边重新被激怒后恢复动作");
        tavern.ActiveInterpreter.Eval("(begin (东边 'load! 0) (西边 'load! 0) (检查!))");
        tavern.RebuildRenderTree();
        Assert(tavern.CurrentSceneName == "world" && !Has(tavern, "醉客互相叫骂")
            && !Find(tavern, "看场子").Disabled && !Find(tavern, "睡觉").Disabled,
            "处理后清除现场，恢复看场和休息");
        Console.WriteLine("[first-act] 全部通过；回合手感与 Unity 视觉仍须实际试玩。");
    }
}
