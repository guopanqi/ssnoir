using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SSNoir.Core;

namespace SSNoir.Testing;

/// <summary>前期内容跨 owner 的真实触发、资源交付、等待与冷读档；不把脚本加载当作交互证据。</summary>
public static class PreludeContentChecks
{
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("[prelude] " + message);
        Console.WriteLine("[prelude] OK: " + message);
    }
    private static IEnumerable<GameNode> Nodes(GameNode n) => new[] { n }.Concat(n.Children.SelectMany(Nodes));
    private static bool Has(SceneManager s, string id) => Nodes(s.CurrentRootNode!).Any(n => n.Id == id);
    private static GameNode Find(SceneManager s, string id) => Nodes(s.CurrentRootNode!).Single(n => n.Id == id);
    private static void Eval(SceneManager s, string code) { s.ActiveInterpreter.Eval(code); s.Refresh(); }
    private static SceneManager Start()
    {
        GameRandom.Reseed(7);
        var s = new SceneManager(new GameState(), new LocalScriptLoader());
        s.LoadScene("world");
        Act(s, "旅馆清晨"); s.EnterPlace("老街酒馆");
        s.GameState.Inventory.SetCount("金钱", 400);
        return s;
    }
    private static ActionReport Act(SceneManager s, string id)
    {
        var node = Find(s, id);
        var slots = new List<SlottedResource?>();
        foreach (var r in node.Requires)
        {
            if (r.Type == "item") slots.Add(new() { Type = "item", ItemId = r.ItemId, Qty = r.Qty });
            else
            {
                var a = s.GameState.Team.FindActor("player")!;
                int i = a.ActionDice.Count - 1;
                slots.Add(new() { Type = "die", ActorId = a.Id, Value = a.ActionDice[i], DieIndex = a.ActionDiceSlotIds[i] });
            }
        }
        return s.ExecuteAction(node, slots);
    }
    private static SceneManager Reload(SceneManager s)
    {
        string path = Path.Combine(Path.GetTempPath(), "ssnoir-prelude-" + Guid.NewGuid() + ".json");
        try { s.SaveGame(path); var next = new SceneManager(new GameState(), new LocalScriptLoader()); next.LoadGame(path); return next; }
        finally { File.Delete(path); }
    }
    private static void Good(SceneManager s, string id)
    {
        // 固定档位的分支夹具仍建立正常的效果报告上下文；资源交付另走 ExecuteAction。
        ResolveFixture(s, () => Find(s, id).Resolve!.SuccessOutcome!.Effect!());
    }
    private static string Text(ActionReport? r) => r == null ? "" : string.Join("\n", r.BlockingStorySteps.Concat(r.PostSceneBlockingSteps)
        .SelectMany(x => x.Dialogue?.Lines.Select(l => l.Text) ?? Array.Empty<string>()));

    private static void Advance(SceneManager s, int days)
    {
        for (int i = 0; i < days; i++) { s.EnterPlace("世界"); s.EndTurn(); }
        s.Refresh();
    }
    private static void Bad(SceneManager s, string id)
    {
        ResolveFixture(s, () => Find(s, id).Resolve!.FailOutcome!.Effect!());
    }
    private static void ResolveFixture(SceneManager s, Action effect)
    {
        var previous = s.GameState.CurrentActionReport;
        s.GameState.CurrentActionReport = new ActionReport { Type = ActionType.Roll };
        try { effect(); }
        finally { s.GameState.CurrentActionReport = previous; }
        s.Refresh();
    }
    public static void Run()
    {
        var s = Start();
        Act(s, "点一杯酒"); Act(s, "点一杯酒");
        Act(s, "醉客互相叫骂"); s.EndScenario(s.ActiveInterpreter.Eval("'success")); s.Refresh();
        Check(Has(s, "看场子"), "酒馆收场仍开放谋生工作");
        Check(Text(s.EnterPlace("老街酒馆")).Contains("四十美元"), "经理在酒馆注意到尼尔并说明差事");
        Act(s, "接受经理首单"); s.EnterPlace("世界");
        Check(Has(s, "安抚演员亨利") && Has(s, "清点演员赔偿"), "接取当天餐厅出现两个共享进展的城市行动");
        var failed = Reload(s);
        for (int i = 0; i < 4; i++) Bad(failed, "安抚演员亨利");
        Check(!Has(failed, "安抚演员亨利") && Has(failed, "经理首单交差"), "危险满格停止处理，失败也保留交差");
        failed.EnterPlace("老街酒馆"); int cash = failed.GameState.Inventory.GetCount("金钱");
        Check(Text(Act(failed, "经理首单交差")).Contains("这事不怪你"), "失败交差经理替尼尔解围");
        Check(failed.GameState.Inventory.GetCount("金钱") == cash + 10 && (string)failed.ActiveInterpreter.Eval("(get-global '身份)")! == "侦探", "失败报酬十美元且仍然入行");
        failed = Reload(failed); Check(!Has(failed, "经理首单交差"), "已交差读档不重复发钱");
        for (int i = 0; i < 3; i++) Good(s, "清点演员赔偿");
        s = Reload(s); s.EnterPlace("老街酒馆"); cash = s.GameState.Inventory.GetCount("金钱");
        Act(s, "经理首单交差");
        Check(s.GameState.Inventory.GetCount("金钱") == cash + 40, "成功交差四十美元");
        Advance(s, 1); s.EnterPlace("家");
        Check(!Has(s, "城市事件-海伦"), "职业首单入行后尚无陌生客户，等待罗科案口碑");

        var n = Start(); Act(n, "点一杯酒"); Act(n, "点一杯酒"); Act(n, "醉客互相叫骂");
        n.EndScenario(n.ActiveInterpreter.Eval("'success")); n.Refresh();
        for (int i = 0; i < 4; i++) { if (n.GameState.Team.FindActor("player")!.ActionDice.Count == 0) Advance(n, 1); Act(n, "看场子"); }
        Check(!Has(n, "雨夜同行"), "先有扶住夜莺的相遇，不同一瞬间就提出求助");
        Advance(n, 2); n.EnterPlace("老街酒馆");
        Check(Has(n, "雨夜同行"), "相遇几天后求助开放原有同行Encounter");
        Act(n, "雨夜同行"); n.EndScenario(n.ActiveInterpreter.Eval("'(抵达 #f #t 0)")); n.Refresh();
        Check(Has(n, "听夜莺唱歌"), "抵达家门后开放日常听歌");
        var music = Reload(n);
        Advance(music, 1); music.EnterPlace("老街酒馆");
        Check(Text(Act(music, "听夜莺唱歌")).Contains("气口"), "日常听歌能触发半拍交流，而非仅增长进度");
        music = Reload(music); Advance(music, 1); music.EnterPlace("老街酒馆");
        string seatText = Text(Act(music, "听夜莺唱歌"));
        Check(seatText.Contains("苏打水") && seatText.Contains("今天第二首可没慢"), "面试前能出现留座及已听过半拍的回扣");
        music = Reload(music);
        Check((bool)music.ActiveInterpreter.Eval("(assoc-get (夜莺生活 'save) \"singer-seat-seen\" #f)")!, "留座片段的已读剧情随存档保留");
        var practice = Reload(n); Advance(practice, 1); practice.EnterPlace("老街酒馆");
        Check(Text(Act(practice, "看场子")).Contains("老吉姆"), "看场可独立触发上台前误认，而不必先听半拍");
        practice = Reload(practice);
        Check((bool)practice.ActiveInterpreter.Eval("(assoc-get (夜莺生活 'save) \"singer-practice-seen\" #f)")!, "上台前片段随存档保留且不反复初遇");
        Advance(n, 3); n.EnterPlace("世界");
        Check(Has(n, "夜莺最后一场演出") && !(bool)n.ActiveInterpreter.Eval("(第一幕 'theater-open?)")!,
            "面试报喜只发出告别演出约定，夜莺尚未转去剧院");
        n.EnterPlace("老街酒馆");
        Check(Has(n, "夜莺驻唱") && !Has(n, "听夜莺唱歌")
            && !string.IsNullOrEmpty(Find(n, "夜莺最后一场演出").RequiredBeforeRest),
            "待赴约时夜莺仍在酒馆，普通听歌由必须参加的最后演出替代");
        n = Reload(n);
        Check((bool)n.ActiveInterpreter.Eval("(第一幕 'farewell-pending?)")!, "告别演出的待赴约状态冷读档保留");
        n.EnterPlace("家"); int pendingDay = (int)n.ActiveInterpreter.Eval("world-day")!;
        bool restRejected = false;
        try { n.EndTurn(); } catch (InvalidOperationException) { restRejected = true; }
        Check(restRejected && (int)n.ActiveInterpreter.Eval("world-day")! == pendingDay,
            "在旅馆也不能睡过已答应的告别演出");
        var broke = Reload(n); broke.EnterPlace("老街酒馆");
        broke.GameState.Inventory.SetCount("金钱", 0);
        broke.GameState.Team.FindActor("player")!.ActionDice.Clear();
        broke.GameState.Team.FindActor("player")!.ActionDiceSlotIds.Clear();
        Check(Find(broke, "夜莺最后一场演出").Requires.Count == 0, "赴约不额外收费或消耗骰子，避免资源耗尽阻塞");
        var show = Act(broke, "夜莺最后一场演出");
        Check(show.BlockingStorySteps.Concat(show.PostSceneBlockingSteps)
            .Any(step => step.Kind == BlockingStoryStepKind.Theatre), "赴约实际排入原有酒馆演出");
        Check((bool)broke.ActiveInterpreter.Eval("(第一幕 'theater-open?)")!
            && !(bool)broke.ActiveInterpreter.Eval("(第一幕 'farewell-pending?)")!, "演出后才转入剧院阶段并解除赴约阻塞");
        broke = Reload(broke); broke.EnterPlace("老街酒馆");
        Check(!Has(broke, "夜莺最后一场演出") && Has(broke, "夜莺·去向"), "演出完成读档不重播，酒馆留下去向");
        n.EnterPlace("老街酒馆"); Act(n, "夜莺最后一场演出"); n.EnterPlace("剧院");
        Check(Has(n, "夜莺排练"), "告别演出后能在剧院找到夜莺");
        Check(Has(n, "彩排帮工") && Has(n, "和经理谈黑影") && Find(n, "彩排帮工").Resolve!.SuccessOutcome != null, "彩排是可选判定工作，经理谈话独立出现");
        Check(string.IsNullOrEmpty(Find(n, "彩排帮工").RequiredBeforeRest), "彩排没有休息阻塞");
        Act(n, "和经理谈黑影"); Advance(n, 2); n.EnterPlace("世界");
        Check(!(bool)n.ActiveInterpreter.Eval("(第三幕 'started?)")!, "提前谈话与等待不能越过彩排启动罗科案");
        n.EnterPlace("剧院"); Act(n, "赞助人彩排");
        n.EndScenario(n.ActiveInterpreter.Eval("'两项交付")); n.Refresh();
        n.EnterPlace("剧院");
        Check(n.ActiveInterpreter != null, "彩排结束接黑衣人追查");
        n.EndScenario(n.ActiveInterpreter.Eval("'(车辆线索 1)")); n.Refresh();
        Advance(n, 2); n.EnterPlace("世界");
        Check((bool)n.ActiveInterpreter.Eval("(第三幕 'started?)")!, "追查后两天经理提出罗科案");
        n.GameState.Team.FindActor("player")!.Composure = 3;
        Check(Text(n.EnterPlace("家")).Contains("火腿三明治") && n.GameState.Team.FindActor("player")!.Composure == 5, "调查期间首返旅馆自动宵夜并恢复二点心情");
        n = Reload(n); int mood = n.GameState.Team.FindActor("player")!.Composure;
        n.EnterPlace("家"); Check(n.GameState.Team.FindActor("player")!.Composure == mood, "宵夜读档不重播");

        // 只准备旧调查的合法结案状态，验证本轮新加的报告→晚宴衔接。
        Eval(n, "(第三幕 'load! (cons (list \"case\" \"结案\") (filter (lambda (row) (not (equal? (car row) \"case\"))) (第三幕 'save))))");
        var leaked = Reload(n);
        Eval(leaked, "(第三幕 'load! (cons (list \"papers-leaked\" #t) (filter (lambda (row) (not (equal? (car row) \"papers-leaked\"))) (第三幕 'save))))");
        leaked.EnterPlace("剧院"); string leakedReport = Text(Act(leaked, "提交调查报告"));
        Check(leakedReport.Contains("小报") && !leakedReport.Contains("东西全截下来了"), "材料泄露的结案报告不写成隐私全部保住");
        var leakedArrival = leaked.EnterPlace("世界");
        Check(leakedArrival!.BlockingStorySteps.Concat(leakedArrival.PostSceneBlockingSteps)
            .Any(step => step.Spotlight?.Body.Contains("流言") == true), "有泄露遗憾的晚宴邀请仍明确提及流言");
        n.EnterPlace("剧院"); Act(n, "提交调查报告");
        Check(!(bool)n.ActiveInterpreter.Eval("(第二章 'started?)")!, "报告前没有提前举办晚宴");
        n.EnterPlace("世界");
        Check((bool)n.ActiveInterpreter.Eval("(第二章 'started?)")! && Has(n, "格兰德酒店"), "罗科案报告成功后开放晚宴所在的酒店");

        Advance(n, 1); n.EnterPlace("家");
        Check(Has(n, "城市事件-海伦"), "罗科案交差后下一晨开放陌生客户海伦");

        // 同一张门口照可在深查前或深查后交付；后者不能谎称尚未查过旅馆。
        var h = Start(); Eval(h, "(海伦的委托 'activate!)"); h.EnterPlace("家");
        Act(h, "询问海伦委托"); Act(h, "接下海伦委托"); h.EnterPlace("码头");
        Good(h, "跟踪海伦丈夫"); Good(h, "跟踪海伦丈夫");
        var shallow = Reload(h); shallow.EnterPlace("家");
        cash = shallow.GameState.Inventory.GetCount("金钱");
        string shallowText = Text(Act(shallow, "海伦交付"));
        Check(shallowText.Contains("还没查清里面的事") && shallow.GameState.Inventory.GetCount("金钱") == cash + 25,
            "未深查交门口照，说明未知范围且仅付二十五美元");
        for (int i = 0; i < 3; i++) Good(h, "打听马丁");
        h = Reload(h); h.EnterPlace("家"); var full = Reload(h);
        cash = h.GameState.Inventory.GetCount("金钱");
        string withheldText = Text(Act(h, "海伦交付"));
        Check(!withheldText.Contains("没有进屋查实") && withheldText.Contains("不能证明他有外遇")
            && h.GameState.Inventory.GetCount("金钱") == cash + 25,
            "已深查但仅交门口照，不假称自己没有调查");
        Check(!Has(h, "城市事件-海伦") && !Has(Reload(h), "城市事件-海伦"), "部分交付结束委托，冷读档不恢复另一份领奖入口");
        full.EnterPlace("家"); cash = full.GameState.Inventory.GetCount("金钱");
        string fullText = Text(Act(full, "海伦交付完整照片"));
        Check(fullText.Contains("后厨") && full.GameState.Inventory.GetCount("金钱") == cash + 25
            && !Has(full, "城市事件-海伦"), "完整交付说明后厨实情，报酬仍为二十五美元");

        var e = Start(); Act(e, "搬运"); Advance(e, 2); e.EnterPlace("码头");
        Check(Has(e, "陪艾迪练拳"), "相识之后自然开放练拳，不要求建满居民区");
        int beforeMood = e.GameState.Team.FindActor("player")!.Composure;
        for (int i = 0; i < 4; i++) Bad(e, "陪艾迪练拳");
        Check(!Has(e, "陪艾迪练拳") && Has(e, "艾迪·在场") && e.GameState.Team.FindActor("player")!.Composure == beforeMood,
            "输四次也计满参与，行动结束但艾迪仍在场，且没有受伤惩罚");
        Check(!Has(e, "学习码头技术") && !Has(e, "绞盘班次") && !Has(e, "陪艾迪去舞厅"), "绞盘与艾迪后段暂不开放");

        var t = Start(); Act(t, "点一杯酒"); Act(t, "点一杯酒"); Act(t, "醉客互相叫骂");
        t.EndScenario(t.ActiveInterpreter.Eval("'success")); t.Refresh(); Act(t, "看场子");
        t.EnterPlace("老街酒馆"); Act(t, "请汤米吃土豆"); cash = t.GameState.Inventory.GetCount("金钱");
        Advance(t, 4); t.EnterPlace("老街酒馆");
        Check(t.GameState.Inventory.GetCount("金钱") == cash + 30, "Tommy四天后加倍还清十五美元垫账");
        Act(t, "接下汤米收账"); t.EnterPlace("码头"); Act(t, "第一次找莫里斯");
        Check(t.GameState.Inventory.GetCount("莫里斯债据") == 1 && Has(t, "莫里斯收账"), "碰壁后保留债据和等待把柄的交付槽");
        Advance(t, 1); t.EnterPlace("老街酒馆"); Check(!Has(t, "打听快脚雷"), "一天后不会提前给线索");
        Advance(t, 1); t.EnterPlace("老街酒馆"); t.EnterPlace("码头"); Act(t, "打听快脚雷");
        var faces = Enumerable.Range(1, 4).Select(i => Find(t, "旧马票箱锁扣" + i).Requires.Single().DieValue).ToArray();
        Check(faces.All(x => x == 1 || x == 2), "四锁扣显示随机低点数需求");
        t = Reload(t);
        Check(faces.SequenceEqual(Enumerable.Range(1, 4).Select(i => Find(t, "旧马票箱锁扣" + i).Requires.Single().DieValue)), "读档不重抽锁面");
        var actor = t.GameState.Team.FindActor("player")!;
        if (actor.ActionDice.Count == 0) Advance(t, 1);
        actor = t.GameState.Team.FindActor("player")!;
        int idx = actor.ActionDice.Count - 1; actor.ActionDice[idx] = 6;
        var badSlot = new SlottedResource { Type = "die", ActorId = "player", DieIndex = actor.ActionDiceSlotIds[idx], Value = 6 };
        int dieCount = actor.ActionDice.Count;
        bool rejected = false;
        try { t.ExecuteAction(Find(t, "旧马票箱锁扣1"), new List<SlottedResource?> { badSlot }); }
        catch (InvalidOperationException ex) { rejected = ex.Message.Contains("needs die face"); }
        Check(rejected && actor.ActionDice.Count == dieCount, "错误点数执行端拒绝且不吞骰");
        for (int i = 1; i <= 4; i++)
        {
            if (t.GameState.Team.FindActor("player")!.ActionDice.Count == 0) Advance(t, 1);
            actor = t.GameState.Team.FindActor("player")!;
            actor.ActionDice[actor.ActionDice.Count - 1] = Find(t, "旧马票箱锁扣" + i).Requires.Single().DieValue!.Value;
            dieCount = actor.ActionDice.Count;
            Act(t, "旧马票箱锁扣" + i);
            Check(t.GameState.Team.FindActor("player")!.ActionDice.Count == dieCount - 1, "每个锁扣真实消耗一枚匹配骰子");
            if (i == 1) { t = Reload(t); Check(!Has(t, "旧马票箱锁扣1"), "分次开启状态冷读档保留"); }
        }
        Check(t.GameState.Inventory.GetCount("快脚雷的旧账") == 1 && !Has(t, "旧马票箱"), "全开直接接故事和把柄，箱子不重复领奖");
        cash = t.GameState.Inventory.GetCount("金钱"); Act(t, "莫里斯收账");
        Check(t.GameState.Inventory.GetCount("快脚雷的旧账") == 0 && t.GameState.Inventory.GetCount("金钱") == cash + 20 && !Has(t, "莫里斯收账"), "把柄投入真实交付，完成收账且只发一次报酬");
        t = Reload(t); Check(!Has(t, "莫里斯收账"), "收账完成状态冷读档保留");
        Console.WriteLine("[prelude] All revised story checks passed. Unity visual interaction not exercised.");
    }
}
