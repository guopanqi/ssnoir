using SSNoir;

using SSNoir.Core;

using System.Linq;

using System.Collections.Generic;

namespace SSNoir.Testing;

// 精确结果用于边界；真实骰子动作覆盖资源扣除。不是交锋手感的试玩证据。
internal static class SecondActV02Checks
{
    public static void Run()
    {
        static IEnumerable<GameNode> Nodes(GameNode n) => new[] { n }.Concat(n.Children.SelectMany(Nodes));

        static bool Has(SceneManager s, string id) => Nodes(s.CurrentRootNode!).Any(n => n.Id == id);

        static void Check(bool x, string m)
        {
            if (!x) throw new Exception(m);
            Console.WriteLine("OK: " + m);
        }
        static void Eval(SceneManager s, string e)
        {
            s.ActiveInterpreter.Eval(e);
            s.Refresh();
        }
        static SceneManager Start(int day)
        {
            GameRandom.Reseed(7);
            var s = new SceneManager(new GameState(), new LocalScriptLoader());
            s.LoadScene("world");
            Eval(s, "(第一幕 'load! (list (list \"opened\" #t) (list \"tavern\" #t) (list \"act\" 2) (list \"singer\" 2) (list \"lucy\" 1) (list \"eddie\" 2))) (set! world-day " + day + ") (set-global! '世界日 world-day) (add-item! \"金钱\" 200)");
            return s;
        }
        static ActionReport Act(SceneManager s, string id)
        {
            var n = Nodes(s.CurrentRootNode!).Single(n => n.Id == id);
            var slots = new List<SlottedResource?>();
            int offset = 0;
            foreach (var cost in n.Requires)
            {
                if (cost.Type == "die")
                {
                    var p = s.GameState.Team.FindActor("player")!;
                    int i = p.ActionDice.Count - 1 - offset++;
                    if (i < 0) throw new Exception("no dice");
                    slots.Add(new SlottedResource { Type = "die", ActorId = "player", Value = p.ActionDice[i], DieIndex = p.ActionDiceSlotIds[i] });
                }
                else if (cost.Type == "item")
                {
                    slots.Add(new SlottedResource { Type = "item", ItemId = cost.ItemId, Qty = cost.Qty });
                }
                else throw new Exception("cost: " + cost.Type);
            }
            return s.ExecuteAction(n, slots);
        }
        static SceneManager Reload(SceneManager s, string tag)
        {
            string p = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssnoir-v02-" + Guid.NewGuid() + ".json");
            try
            {
                s.SaveGame(p);
                var t = new SceneManager(new GameState(), new LocalScriptLoader());
                t.LoadGame(p);
                return t;
            }
            finally
            {
                System.IO.File.Delete(p);
            }
        }

        static GameNode Find(SceneManager s, string id) => Nodes(s.CurrentRootNode!).Single(n => n.Id == id);

        static IEnumerable<string> Lines(ActionReport? r) => r == null ? Array.Empty<string>() : r.BlockingStorySteps.Concat(r.PostSceneBlockingSteps).Where(b => b.Dialogue != null).SelectMany(b => b.Dialogue!.Lines.Select(l => l.Text));




        static object? Get(SceneManager s, string e) => s.ActiveInterpreter.Eval(e);

        static void Good(SceneManager s, string id)
        {
            Find(s, id).Resolve!.SuccessOutcome!.Effect!();
            s.Refresh();
        }
        static void Bad(SceneManager s, string id)
        {
            Find(s, id).Resolve!.FailOutcome!.Effect!();
            s.Refresh();
        }
        foreach (int familiar in new[] { 0, 1 })
        {
            var opening = Start(6);
            Eval(opening, "(第一幕 'load! '((\"opened\" #t) (\"tavern\" #t) (\"act\" 1) (\"singer\" " + familiar + ")))" );
            Act(opening, "夜莺最后一场演出");
            Check((int)Get(opening, "(第一幕 'act)")! == 2 && (int)Get(opening, "(第一幕 'singer-stage)")! >= 1, "D6不论此前相识与否都建立夜莺关系");
            Eval(opening, "(end-turn!) (end-turn!)");
            Act(opening, "观看正式彩排");
            Check(Has(opening, "接下演出服调查"), "D6到D8保留固定赴约并自然出现经理机会");
        }
        var s = Start(8);
        Act(s, "观看正式彩排");
        Check(Has(s, "接下演出服调查") && !Has(s, "谈谈自己的打算"), "彩排直接介绍调查愿望和机会，只需决定接受");
        Act(s, "接下演出服调查");
        Check(Has(s, "旧库房找衣箱") && Has(s, "问运货人衣箱"), "经理两种调查途径同时存在");
        Good(s, "旧库房找衣箱");
        Check(Find(s, "请人找回衣箱").Requires.Single().Qty == 15, "经理两格进度现金折价至15");
        int cash = s.GameState.Inventory.GetCount("金钱");
        Act(s, "请人找回衣箱");
        Check(s.GameState.Inventory.GetCount("演出服") == 1 && s.GameState.Inventory.GetCount("金钱") == cash - 15, "代找补齐进度，只发一次衣箱");
        s = Reload(s, "costume");
        Check(!Has(s, "请人找回衣箱") && s.GameState.Inventory.GetCount("演出服") == 1, "持有衣箱冷读档不复制");
        Act(s, "交付演出服");
        Check(s.CurrentDossier.Single(e => e.Id.StartsWith("经理：")).IsClosed, "交付完成直接归档");

        static void Round(SceneManager scene)
        {
            var frame = scene.BeginRoundTransition();
            while (!frame.IsFinished) frame = scene.AdvanceRoundTransition();
        }
        var e = Start(9);
        var entry = e.EnterPlace("码头");
        Check(e.CurrentSceneName == "world" && Has(e, "介入工钱争执") && !Has(e, "核对工时簿"), "城市只留交锋入口，不投射调查行动");
        Check(!Lines(entry).Any(t => t.Contains("血") || t.Contains("衣领")), "C1开场尚未失控");
        Act(e, "介入工钱争执");
        Check(e.CurrentSceneName == "少了十块钱" && Has(e,"核对工时簿"), "C1进入独立交锋场景");
        Good(e,"追问账房"); Good(e,"追问账房"); Good(e,"当场作证"); Good(e,"当场作证");
        Check(e.CurrentSceneName == "少了十块钱" && Has(e,"请弗兰克协助"), "四次最好结果未结束，危机支持已开放");
        Round(e);
        Check(Convert.ToInt32(e.GameState.Get<int>("世界日",0)) == 9, "交锋结束回合不推进世界日期");
        Check(Nodes(e.CurrentRootNode!).First(x=>x.Resolve?.Clock?.Label=="核清工时").Resolve!.Clock!.Current == 4 && Nodes(e.CurrentRootNode!).First(x=>x.Resolve?.Clock?.Label=="争执升级").Resolve!.Clock!.Current == 1, "回合保留进度并推进争执压力");
        Good(e,"追问账房");
        Check(e.CurrentSceneName == "world" && (string)Get(e,"(第二幕 'wage-state)")! == "上工", "独立交锋上工结果回写城市");
        Check(Lines(e.EnterPlace("码头")).Any(t=>t.Contains("手套")), "相熟者正常重逢仍自然获得手套");
        e=Reload(e,"wage-result");
        Check(!Has(e,"介入工钱争执") && (string)Get(e,"(第二幕 'wage-state)")! == "上工", "读档保留结局，不重复开启交锋");

        var f=Start(9); f.EnterPlace("码头"); Act(f,"介入工钱争执");
        Bad(f,"追问账房"); Bad(f,"追问账房"); Bad(f,"追问账房");
        Act(f,"请弗兰克协助"); Check(!Has(f,"请弗兰克协助"),"弗兰克协助一次，不能直接通关");
        Bad(f,"追问账房"); Bad(f,"追问账房"); Bad(f,"追问账房"); Bad(f,"追问账房");
        Check(f.CurrentSceneName == "world" && !((bool)Get(f,"(第二幕 'eddie-on-dock?)")!),"压力满回到城市，艾迪停班");
        Check(Lines(f.EnterPlace("码头居民区")).Any(t=>t.Contains("空饭盒")),"停班探望依然自然触发");
        f.EnterPlace("码头居民区"); Check((bool)Get(f,"(第二幕生活 'met-frank?)")!,"食堂弗兰克不依赖交锋路径");

        var n=Start(8); Act(n,"观看正式彩排"); Eval(n,"(end-turn!)");
        Check(Lines(n.EnterPlace("剧院")).Any(t=>t.Contains("分你一半")), "免费私人相处保留");
        Check(Has(n,"帮她准备旧歌")&&!Has(n,"排旧曲"),"城市只保留旧歌交锋入口");
        Act(n,"帮她准备旧歌");
        Check(n.CurrentSceneName=="赞助人试演", "C2进入独立筹备交锋");
        Good(n,"空档赶排"); Good(n,"空档赶排"); Good(n,"协调后台"); Good(n,"协调后台");
        Check(n.CurrentSceneName=="赞助人试演", "四次最好结果不能一回合做完双目标");
        Round(n); Check(n.GameState.Get<int>("世界日",0)==9,"C2交锋回合不跨城市日");
        Good(n,"空档赶排"); Check(n.CurrentSceneName=="world"&&!Has(n,"帮她准备旧歌"),"筹备结算返回城市且不重复进入");
        Eval(n,"(end-turn!) (end-turn!)");
        Check((string)Get(n,"(第二幕剧院 'sponsor-result)")! == "保留旧歌", "D11依交锋结果确定旧歌条件");
        Check(Lines(n.EnterPlace("剧院")).Any(t=>t.Contains("今天唱旧")),"D11仍由夜莺亲口选择版本");
        Eval(n,"(end-turn!) (end-turn!)"); Check(Find(n,"首演节目单").Resolve!.NoteText.Contains("旧港"),"D13节目单仍兑现旧歌结果");

        var stop=Start(8); Act(stop,"观看正式彩排"); Eval(stop,"(end-turn!)"); stop.EnterPlace("剧院"); Act(stop,"帮她准备旧歌"); Act(stop,"放下筹备");
        Check(stop.CurrentSceneName=="world"&&!Has(stop,"帮她准备旧歌"),"交锋中放下准备返回城市，不能重开刷结果");
        var l = Start(8);
        Eval(l, "(第二幕 'on-cafe-work!)");
        Check(Has(l, "付代班费"), "露西玩法仍可达");
        Check(Find(l, "付代班费").Risk == null
              && Find(l, "付代班费").ProgressTarget?.Id == "去学校的时间",
              "付代班费是确定性付款，并关联露西进度Clock");
        Act(l, "付代班费");
        Check(!l.CurrentDossier.Single(x => x.Id.StartsWith("露西：")).IsClosed, "入学后事项保留到打字纸");
        Eval(l, "(set! world-day 11) (set-global! '世界日 11)");
        Check(Lines(l.EnterPlace("街角咖啡馆")).Any(t => t.Contains("Neil")), "露西记住愿望并给出打字纸");

        var missed = Start(9);
        Eval(missed, "(end-turn!) (end-turn!) (end-turn!)");
        Check((string)Get(missed, "(第二幕 'wage-state)")! == "停班" && !missed.CurrentDossier.Any(x => x.Id.StartsWith("艾迪：")), "未参与者按期停班，未知事件不泄露");
        Eval(missed, "(end-turn!) (end-turn!) (end-turn!)");
        Check((bool)Get(missed, "(第二幕 'eddie-on-dock?)")!, "统一停班到第四天自动返岗");

        var nohelp = Start(8);
        Act(nohelp, "观看正式彩排");
        Eval(nohelp, "(end-turn!) (end-turn!) (end-turn!)");
        nohelp.EnterPlace("剧院");
        Check((string)Get(nohelp, "(第二幕剧院 'sponsor-result)")! == "接受改编", "未筹备也按D11演出，夜莺接受改编");
        Eval(nohelp, "(end-turn!) (end-turn!)");
        Check(Find(nohelp, "首演节目单").Resolve!.NoteText.Contains("新编曲"), "改编结果的D13节目单有不同署名");

        var legacy = Start(10);
        Eval(legacy, "(第二幕 'load! '((\"wage\" \"凭证核清\") (\"wage-end-day\" 9)))");
        Check((string)Get(legacy, "(第二幕 'wage-state)")! == "上工", "旧凭证结局归一上工");
        Eval(legacy, "(第二幕 'load! '((\"wage\" \"撤出\") (\"wage-end-day\" 9)))");
        Check((string)Get(legacy, "(第二幕 'wage-state)")! == "停班", "旧撤出结局归一停班");

        Eval(legacy, "(add-item! \"演出服\" 1) (第二幕生活 'load! '((\"manager\" #t) (\"contract\" \"已接下\") (\"warehouse-known\" #t)))");
        Check(legacy.GameState.Inventory.GetCount("演出服") == 1 && !Has(legacy, "请人找回衣箱"), "旧库位档持有衣箱不重复生成");
        Eval(legacy, "(第二幕生活 'load! '((\"manager\" #t) (\"contract\" \"完成\")))");
        Check(!Has(legacy, "交付演出服"), "旧完成委托不可再次领钱");

        Eval(legacy, "(第二幕剧院 'load! '((\"sponsor-result\" \"修好设备\") (\"sponsor-aftermath\" #t)))");
        Check((string)Get(legacy, "(第二幕剧院 'sponsor-result)")! == "接受改编", "旧技术结局统一映射改编，未伪称曾准备旧曲");

        var strangers = Start(8);
        Eval(strangers, "(第一幕 'load! '((\"opened\" #t) (\"tavern\" #t) (\"act\" 2) (\"singer\" 1) (\"lucy\" 0) (\"eddie\" 0)))");
        Check(!Has(strangers, "付代班费"), "未认识露西时不开放私人帮助");
        Act(strangers, "咖啡馆帮工");
        Check(!Has(strangers, "付代班费"), "第一次工作先认识露西，没有同次跳到私密愿望");
        Act(strangers, "观看正式彩排");
        Eval(strangers, "(end-turn!)");
        Act(strangers, "咖啡馆帮工");
        Check(Has(strangers, "付代班费"), "第二幕仍能自然认识后进入露西故事");

        var poor = Start(9);
        Eval(poor, "(remove-item! \"金钱\" (item-count \"金钱\")) (injure!) (set-actor-composure! 'player 1)");
        poor.EnterPlace("码头");
        Eval(poor, "(end-turn!) (end-turn!) (end-turn!) (end-turn!)");
        Check(Has(poor, "补交住宿费") && !Has(poor, "租屋"), "低冷静受伤且无房费仍能跨日，欠租锁门不阻断世界");

        Console.WriteLine("V0.2定向检查通过");

    }
}
