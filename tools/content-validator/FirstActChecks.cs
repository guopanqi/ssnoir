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
    private static void Sleep(SceneManager scenes)
    {
        scenes.EnterPlace("老街酒馆"); // 真正出门，解除防误触连睡锁。
        scenes.EnterPlace("家");
        Act(scenes, Has(scenes, "睡觉") ? "睡觉" : "蜷缩在门口");
    }
    private static void SchemeAssert(SceneManager scenes, string condition, string message)
    {
        object result = scenes.ActiveInterpreter.Eval(condition);
        Assert(result is bool b && b, message);
    }
    public static void Run()
    {
        var cafe = Start();
        var firstCoffee = Act(cafe, "咖啡馆帮工");
        Assert(Lines(firstCoffee).Any(t => t.Contains("不像常端盘子"))
            && Lines(firstCoffee).All(t => !t.Contains("煤灰")), "未去码头的露西初识不伪造搬运经历");
        cafe.EnterPlace("世界");
        Assert(cafe.EnterPlace("街角咖啡馆") == null, "退出再进咖啡馆不会触发后续剧情");
        for (int work = 0; work < 2; work++)
        {
            var ordinary = Act(cafe, "咖啡馆帮工");
            Assert(!Lines(ordinary).Any(), "普通劳动不连续插入人物剧情");
            Assert(!ordinary.Effects.Any(e => e.Label.Contains("日常间隔")), "隐藏 Clock 不泄露到效果条");
        }
        string lifeSave = Path.Combine(Path.GetTempPath(), "ssnoir-life-clock-save.json");
        try
        {
            cafe.SaveGame(lifeSave);
            var restored = new SceneManager(new GameState(), new LocalScriptLoader()); restored.LoadGame(lifeSave);
            var story = Act(restored, "咖啡馆帮工");
            Assert(Lines(story).Any(t => t.Contains("夜校")), "隐藏进度冷读档后保留，第三班触发打字片段");
            Sleep(restored);
            Assert(!Lines(Act(restored, "咖啡馆帮工")).Any(), "打字片段完成后不再重复");
        }
        finally { if (File.Exists(lifeSave)) File.Delete(lifeSave); }

        var dock = Start();
        Act(dock, "搬运");
        for (int work = 0; work < 2; work++)
            Assert(!Lines(Act(dock, "搬运")).Any(), "艾迪初识后先过普通劳动，不逐次演出");
        Assert(Lines(Act(dock, "搬运")).Any(t => t.Contains("又来了")), "三班间隔后艾迪再次招呼");
        Sleep(dock);
        for (int work = 0; work < 2; work++)
            Assert(!Lines(Act(dock, "搬运")).Any(), "招呼之后重新积累日常，不立刻邀约");
        Act(dock, "搬运");
        Assert(Has(dock, "去坐一会儿"), "又三班劳动后才出现艾迪邀约");
        Act(dock, "去坐一会儿"); dock.EnterPlace("老街酒馆");
        int before = dock.GameState.Team.FindActor("player")!.ActionDice.Count;
        int composure = dock.GameState.Team.FindActor("player")!.Composure;
        int money = dock.GameState.Inventory.GetCount("金钱");
        Act(dock, "与艾迪喝酒"); Act(dock, "先交房租");
        Assert(dock.GameState.Team.FindActor("player")!.ActionDice.Count == before - 1
            && dock.GameState.Team.FindActor("player")!.Composure == composure
            && dock.GameState.Inventory.GetCount("金钱") == money, "艾迪赴约只花一枚骰子，不冒充恢复服务");
        SchemeAssert(dock, "(assoc-get (第一幕 'save) \"drank\" #f)", "记住真实赴约");

        var absent = Start();
        Sleep(absent); absent.EnterPlace("老街酒馆");
        Assert(!Has(absent, "夜莺的请求") && !Has(absent, "陪夜莺回家"), "第二天尚未出现请求");
        Sleep(absent);
        Assert(Find(absent, "睡觉").Disabled && Has(absent, "夜莺的请求"), "第三天请求出现，未去酒馆也不能睡过事件");
        var request = absent.EnterPlace("老街酒馆");
        Assert(Lines(request).Contains("我陪你。") && Has(absent, "陪夜莺回家")
            && !Has(absent, "我陪你") && !Has(absent, "今晚不方便"), "对白中直接答应，没有接受拒绝选项");
        string requestSave = Path.Combine(Path.GetTempPath(), "ssnoir-mandatory-walk-save.json");
        try
        {
            absent.SaveGame(requestSave);
            var restored = new SceneManager(new GameState(), new LocalScriptLoader()); restored.LoadGame(requestSave);
            Assert(Find(restored, "睡觉").Disabled && Has(restored, "陪夜莺回家"), "请求读档后仍必须处理");
        }
        finally { if (File.Exists(requestSave)) File.Delete(requestSave); }
        Act(absent, "陪夜莺回家");
        absent.ActiveInterpreter.Eval("(begin (恐惧 'load! 3) (检查!))"); absent.RebuildRenderTree();
        Assert(!Find(absent, "睡觉").Disabled, "交锋结算后解除同行阻塞");
        Sleep(absent);
        Assert(Find(absent, "睡觉").Disabled && Has(absent, "观看正式彩排"), "第四天彩排节点声明必须处理");
        Act(absent, "观看正式彩排");
        Assert(!Has(absent, "她在舞台上"), "雨夜失利不自动获得亲近对白");
        Sleep(absent);
        SchemeAssert(absent, "(and (= world-day 5) (= (第一幕 'act) 2) (not (第二章 'started?)))",
            "未认识艾迪露西、雨夜失利，处理彩排后仍正常进入第二幕");
        SchemeAssert(absent, "(and (= (assoc-get (第一幕 'save) \"eddie\" -1) 0) (= (assoc-get (第一幕 'save) \"lucy\" -1) 0))",
            "跳过地点不自动认识艾迪和露西");
        Assert(!Has(absent, "观看正式彩排") && !Has(absent, "陪夜莺回家"), "过期内容从行动树消失");
        SchemeAssert(absent, "(and (assoc-get (第一幕 'save) \"done\" #f) (assoc-get (第一幕 'save) \"seen\" #f))",
            "观看彩排的真实经历保留");

        var rain = Start(); Sleep(rain); Sleep(rain); rain.EnterPlace("老街酒馆");
        Act(rain, "陪夜莺回家");
        Assert(rain.CurrentSceneName == "雨夜同行" && !Has(rain, "陪她去叫车"), "正式入口进入雨夜，没有主动退出按钮");
        rain.ActiveInterpreter.Eval("(begin (前进 'load! 7) (恐惧 'load! 2) (走! 1 1 #t))");
        rain.RebuildRenderTree();
        SchemeAssert(rain, "(and (equal? (assoc-get (first-act-walk 'save) \"result\" \"\") \"抵达\") (= (assoc-get (first-act-walk 'save) \"final-fear\" 0) 3))",
            "同时满格回写为惊魂未定的抵达");
        string path = Path.Combine(Path.GetTempPath(), "ssnoir-first-act-save.json");
        try
        {
            rain.SaveGame(path);
            var restored = new SceneManager(new GameState(), new LocalScriptLoader()); restored.LoadGame(path);
            SchemeAssert(restored, "(and (第一幕 'work-open?) (first-act-walk 'walked?) (not (first-act-walk 'close?)))",
                "冷读档保留经历与亲密程度，不重复开场");
        }
        finally { if (File.Exists(path)) File.Delete(path); }

        var close = Start(); Sleep(close); Sleep(close); close.EnterPlace("老街酒馆");
        Act(close, "陪夜莺回家");
        close.ActiveInterpreter.Eval("(begin (前进 'load! 7) (走! 1 0 #f))"); close.RebuildRenderTree();
        Sleep(close);
        Act(close, "观看正式彩排");
        Assert(Has(close, "她在舞台上"), "可靠同行后彩排有私人交流");
        Act(close, "像是属于这里");
        Assert(!Has(close, "她在舞台上"), "彩排回答只发生一次");

        var tavern = Start(); Act(tavern, "看场子");
        Assert(!Has(tavern, "醉客互相叫骂"), "第一次看场不提前触发两桌争吵");
        Act(tavern, "看场子");
        Assert(!Has(tavern, "醉客互相叫骂"), "第二次看场仍不提前触发两桌争吵");
        Act(tavern, "看场子");
        Assert(Has(tavern, "醉客互相叫骂"), "第三次看场固定触发两桌争吵");
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
