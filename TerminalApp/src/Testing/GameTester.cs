using System;
using System.Collections.Generic;
using System.IO;
using Schemy;
using SSNoir.Core;
using SSNoir.Scripting;

namespace SSNoir.Testing
{
    public static class GameTester
    {
        public static void ValidateContent()
        {
            var scenesDir = Path.GetFullPath("scenes");
            if (!Directory.Exists(scenesDir))
            {
                scenesDir = Path.GetFullPath(Path.Combine("..", "Content", "scenes"));
            }
            if (!Directory.Exists(scenesDir))
            {
                scenesDir = Path.GetFullPath(Path.Combine("Content", "scenes"));
            }
            if (!Directory.Exists(scenesDir))
            {
                throw new DirectoryNotFoundException("Scenes directory not found under standard content paths.");
            }

            // Phase 1: paren balance check on every .scm file before loading anything
            Console.WriteLine("[validate] Phase 1: paren balance...");
            foreach (var scmFile in Directory.GetFiles(scenesDir, "*.scm", SearchOption.AllDirectories))
            {
                AssertParenBalance(scmFile);
            }
            Console.WriteLine("[validate] Phase 1: all files balanced.");

            // Phase 2: load and render-validate each scene
            foreach (var scenePath in Directory.GetFiles(scenesDir, "*.scm", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(scenesDir, scenePath);
                var sceneName = Path.Combine(Path.GetDirectoryName(relativePath) ?? "", Path.GetFileNameWithoutExtension(relativePath)).Replace('\\', '/');

                if (sceneName.StartsWith("world/") && sceneName != "world/world")
                {
                    continue;
                }

                var gameState = new GameState();
                var sceneManager = new SceneManager(gameState, new LocalScriptLoader());

                Console.WriteLine($"[validate] {sceneName}");
                try
                {
                    sceneManager.LoadScene(sceneName);
                }
                catch (Exception ex)
                {
                    string targetFile = scenePath;
                    var match = System.Text.RegularExpressions.Regex.Match(ex.Message, @"Error loading Scheme script '([^']+)'");
                    if (match.Success)
                    {
                        string relPath = match.Groups[1].Value;
                        string possiblePath = Path.Combine(Path.GetDirectoryName(scenesDir) ?? "", relPath);
                        if (File.Exists(possiblePath))
                        {
                            targetFile = possiblePath;
                        }
                        else
                        {
                            possiblePath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(scenesDir) ?? "") ?? "", relPath);
                            if (File.Exists(possiblePath))
                            {
                                targetFile = possiblePath;
                            }
                        }
                    }
                    CheckParenthesesDiagnostics(targetFile);
                    throw;
                }

                if (sceneManager.CurrentRootNode == null)
                {
                    throw new InvalidDataException($"Scene '{sceneName}' produced an empty world.");
                }

                Console.WriteLine($"Validated scene '{sceneName}' with root '{sceneManager.CurrentRootNode.Name}'.");
            }
        }

        public static void TestSaveLoad()
        {
            Console.WriteLine("=== Save/Load Test ===");
            var savePath = Path.Combine(Path.GetTempPath(), "ssnoir_test_save.json");

            try
            {
                // ── Phase 1: build non-default state, save ────────────────
                Console.WriteLine("[saveload] Phase 1: build state and save...");
                var gs1 = new GameState();
                var sm1 = new SceneManager(gs1, new LocalScriptLoader());
                sm1.LoadScene("world");

                gs1.Set("chapter", 1);
                gs1.Set("relation:劳工", 5);
                gs1.Team.ApplyStress("player", 2);
                gs1.Inventory.SetCount("金钱", 99);
                gs1.Inventory.SetCount("酒", 3);
                sm1.Refresh(); // rebuild tree after setting globals
                ExecuteNode(sm1, "搬运"); // 码头低门槛工作，掷骰强制为 6 → 成功

                sm1.SaveGame(savePath);
                Console.WriteLine($"[saveload] Saved to {savePath}");

                int savedChapter    = gs1.Get<int>("chapter");
                int savedRep        = gs1.Get<int>("relation:劳工");
                int savedStress     = gs1.Team.FindActor("player")!.Stress;
                int savedMoney      = gs1.Inventory.GetCount("金钱");
                int savedWine       = gs1.Inventory.GetCount("酒");
                int savedHealth     = gs1.Team.Health;
                int savedLocations  = sm1.CurrentRootNode?.Children.Count ?? 0;
                Console.WriteLine($"[saveload] chapter={savedChapter} rep={savedRep} stress={savedStress} 金钱={savedMoney} 酒={savedWine} health={savedHealth} locations={savedLocations}");

                // ── Phase 2: cold start, load save ───────────────────────
                Console.WriteLine("[saveload] Phase 2: cold start + LoadGame...");
                var gs2 = new GameState();
                var sm2 = new SceneManager(gs2, new LocalScriptLoader());
                sm2.LoadGame(savePath);   // creates world interpreter internally

                int loadedChapter   = gs2.Get<int>("chapter");
                int loadedRep       = gs2.Get<int>("relation:劳工");
                int loadedStress    = gs2.Team.FindActor("player")!.Stress;
                int loadedMoney     = gs2.Inventory.GetCount("金钱");
                int loadedWine      = gs2.Inventory.GetCount("酒");
                int loadedHealth    = gs2.Team.Health;
                int loadedLocations = sm2.CurrentRootNode?.Children.Count ?? 0;
                Console.WriteLine($"[saveload] chapter={loadedChapter} rep={loadedRep} stress={loadedStress} 金钱={loadedMoney} 酒={loadedWine} health={loadedHealth} locations={loadedLocations}");

                AssertEq("chapter",         savedChapter,  loadedChapter);
                AssertEq("relation:劳工",   savedRep,      loadedRep);
                AssertEq("player.stress",   savedStress,   loadedStress);
                AssertEq("金钱",             savedMoney,    loadedMoney);
                AssertEq("酒",               savedWine,     loadedWine);
                AssertEq("health",          savedHealth,   loadedHealth);
                AssertEq("locations",       savedLocations, loadedLocations);

                // ActionDice re-rolled: just verify non-empty
                var player2 = gs2.Team.FindActor("player")!;
                if (player2.ActionDice.Count == 0)
                    throw new Exception("[saveload] ActionDice should be re-rolled after load but is empty.");

                Console.WriteLine("[saveload] All assertions passed.");
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
            }
        }

        private static void AssertEq<T>(string label, T expected, T actual)
        {
            if (!expected!.Equals(actual))
                throw new Exception($"[saveload] FAIL: {label} expected={expected} actual={actual}");
            Console.WriteLine($"[saveload] OK: {label} = {actual}");
        }

        // 判定概率是底层数学契约，算错会静默影响卡面预览与平衡感知，故固化。
        public static void TestRollOdds()
        {
            Console.WriteLine("=== Roll Odds Test ===");

            void Assert(string label, double expected, double actual)
            {
                if (Math.Abs(expected - actual) > 1e-9)
                    throw new Exception($"[odds] FAIL: {label} expected={expected:F6} actual={actual:F6}");
                Console.WriteLine($"[odds] OK: {label} = {actual:F4}");
            }

            void AssertSumsToOne(string label, RollOddsResult r)
            {
                double sum = r.Fail + r.Neutral + r.Success;
                if (Math.Abs(sum - 1.0) > 1e-9)
                    throw new Exception($"[odds] FAIL: {label} probabilities sum to {sum:F6}, not 1");
            }

            // 机制：final = d6 + (放入骰−4) + (技能−1) + 难度，按 ≤2 失败 / 3–4 中性 / ≥5 成功 分档。
            // 放入4、技能1、无修正是中枢：final = d6 → 三档各 2/6。
            var pivot = RollOdds.Compute(placedDie: 4, skillLevel: 1, modSum: 0);
            AssertSumsToOne("die4 lvl1", pivot);
            Assert("die4 lvl1 fail",    2.0 / 6.0, pivot.Fail);
            Assert("die4 lvl1 neutral", 2.0 / 6.0, pivot.Neutral);
            Assert("die4 lvl1 success", 2.0 / 6.0, pivot.Success);

            // 放入1、技能1：位移 −3，final = d6−3 ∈ −2..3 → 5/6 失败、1/6 中性、0 成功。
            // 这正是重设的目标：最差放置明显偏坏，而非 33/33/33。
            var worst = RollOdds.Compute(placedDie: 1, skillLevel: 1, modSum: 0);
            AssertSumsToOne("die1 lvl1", worst);
            Assert("die1 lvl1 fail",    5.0 / 6.0, worst.Fail);
            Assert("die1 lvl1 neutral", 1.0 / 6.0, worst.Neutral);
            Assert("die1 lvl1 success", 0.0,       worst.Success);

            // 放入6、技能1：位移 +2 → 0 失败、2/6 中性、4/6 成功。高骰偏好。
            var best = RollOdds.Compute(placedDie: 6, skillLevel: 1, modSum: 0);
            Assert("die6 lvl1 fail",    0.0,       best.Fail);
            Assert("die6 lvl1 success", 4.0 / 6.0, best.Success);

            // 放入骰是双向的：低骰失败率应高于高骰。
            if (!(worst.Fail > best.Fail))
                throw new Exception($"[odds] FAIL: expected die1 fail>die6 fail, got {worst.Fail:F4} vs {best.Fail:F4}");

            // 技能可救差骰：放入1，技能3(位移 −1) → 3/6 失败、2/6 中性、1/6 成功（技能1 时成功为 0）。
            var rescued = RollOdds.Compute(placedDie: 1, skillLevel: 3, modSum: 0);
            AssertSumsToOne("die1 lvl3", rescued);
            Assert("die1 lvl3 fail",    3.0 / 6.0, rescued.Fail);
            Assert("die1 lvl3 success", 1.0 / 6.0, rescued.Success);
            if (!(rescued.Success > worst.Success))
                throw new Exception($"[odds] FAIL: expected skill to lift die1 success, got {rescued.Success:F4} vs {worst.Success:F4}");

            // 难度修正整体平移：放入4、技能1、−2 → final=d6−2 → 4/6 失败、2/6 中性、0 成功。
            var hard = RollOdds.Compute(4, 1, -2);
            Assert("die4 lvl1 -2 fail",    4.0 / 6.0, hard.Fail);
            Assert("die4 lvl1 -2 success", 0.0,       hard.Success);

            Console.WriteLine("[odds] All assertions passed.");
        }

        // 端到端验证三条生活路线、世界公共事件、可选货单和小节成长。
        // 全程强制骰=6（成功）；数值平衡另做报数，不在这里硬断言。
        public static void TestGrowthArc()
        {
            Console.WriteLine("=== Growth Arc Test ===");
            var gs = new GameState();
            var sm = new SceneManager(gs, new LocalScriptLoader());
            sm.LoadScene("world");

            void Assert(string label, bool ok, string detail)
            {
                if (!ok) throw new Exception($"[arc] FAIL: {label} — {detail}");
                Console.WriteLine($"[arc] OK: {label} ({detail})");
            }

            Assert("公共事件时钟位于世界层",
                sm.CurrentRootNode!.Clocks.Exists(clock => clock.Label == "码头公共事件"),
                "世界根节点持有倒计时");
            Assert("码头不再拥有公共事件时钟",
                !FindNode(sm.CurrentRootNode, "码头")!.Clocks.Exists(clock => clock.Label == "码头公共事件"),
                "地点与主线调度边界分离");

            // 1) 劳工：工资和老周关系拆开；完成老周小节才获得成长与援助。
            gs.Set("relation:劳工", 3);
            sm.Refresh();
            int favorBefore = gs.Get<int>("laozhou-favor");
            ExecuteNode(sm, "替工头记账");
            Assert("工头记账不涨老周好感", gs.Get<int>("laozhou-favor") == favorBefore,
                $"favor={gs.Get<int>("laozhou-favor")}");
            ExecuteNode(sm, "帮老周查账");
            ExecuteNode(sm, "帮老周查账");
            int growthBeforeZhou = gs.Team.GrowthLevel;
            ExecuteNode(sm, "替老周跑一趟");
            Assert("老周小节奖励成长", gs.Team.GrowthLevel == growthBeforeZhou + 1,
                $"growth={gs.Team.GrowthLevel}");
            Assert("老周支线解锁援助", gs.Get<bool>("laozhou-can-help"), "laozhou-can-help=true");

            // 2) 官僚：完成探长小节；延期真实回退一天；办理有冷却的一次性通行证。
            gs.Set("relation:官僚", 3);
            sm.Refresh();
            int growthBeforeDetective = gs.Team.GrowthLevel;
            ExecuteNode(sm, "陪探长走访");
            ExecuteNode(sm, "替探长整理口供");
            Assert("探长小节奖励成长", gs.Team.GrowthLevel == growthBeforeDetective + 1,
                $"growth={gs.Team.GrowthLevel}");
            sm.EndTurn();
            sm.Refresh();
            int beforeDelay = sm.CurrentRootNode!.Clocks.Find(c => c.Label == "码头公共事件")!.Current;
            ExecuteNode(sm, "请探长延期一天");
            int afterDelay = sm.CurrentRootNode!.Clocks.Find(c => c.Label == "码头公共事件")!.Current;
            Assert("官僚延期回退倒计时", afterDelay == beforeDelay + 1,
                $"remaining {beforeDelay}→{afterDelay}");
            ExecuteNode(sm, "办理办案通行证");
            var passNode = FindNode(sm.CurrentRootNode, "办理办案通行证")!;
            Assert("一次性办案通行证已准备",
                gs.Inventory.GetCount("办案通行证") == 1 && passNode.Disabled,
                "持有一张时不能继续办理");
            Assert("办案通行证显示再次签发时钟",
                passNode.Clocks.Exists(c => c.Label == "再次签发" && c.Current == 3),
                "再次签发=3天");

            // 3) 富商：应酬完成小节；投资需要考察、谈判、本金与等待。
            gs.Set("relation:富商", 3);
            gs.Inventory.SetCount("金钱", 200);
            sm.Refresh();
            int growthBeforeAgent = gs.Team.GrowthLevel;
            ExecuteNode(sm, "陪货运代理应酬");
            ExecuteNode(sm, "核对代理人的条件");
            Assert("代理人小节尚未完成", gs.Team.GrowthLevel == growthBeforeAgent,
                "必须等第一笔投资结算");
            ExecuteNode(sm, "考察货运项目");
            ExecuteNode(sm, "谈投资条件");
            ExecuteNode(sm, "投入货运项目");
            Assert("投资进入延迟结算", FindNode(sm.CurrentRootNode, "货运公司")!.Clocks.Exists(c => c.Label == "投资结算"),
                "投资结算时钟可见");
            for (int day = 0; day < 3; day++) sm.EndTurn();
            sm.Refresh();
            Assert("首笔投资结算后奖励成长", gs.Team.GrowthLevel == growthBeforeAgent + 1,
                $"growth={gs.Team.GrowthLevel}");

            // 4) 强制公共事件：世界入口、休息阻塞、存读档恢复。
            sm.ActiveInterpreter.Eval("(debug-trigger-public-event!)");
            sm.Refresh();
            Assert("世界出现公共事件入口", FindNode(sm.CurrentRootNode, "处理码头公共事件") != null,
                "公共事件为世界节点");
            Assert("公共事件阻塞休息", FindRestNode(sm.CurrentRootNode).Disabled,
                "睡觉 disabled");

            string pendingSavePath = Path.Combine(Path.GetTempPath(), "ssnoir_public_event_save.json");
            try
            {
                sm.SaveGame(pendingSavePath);
                var loadedGs = new GameState();
                var loadedSm = new SceneManager(loadedGs, new LocalScriptLoader());
                loadedSm.LoadGame(pendingSavePath);
                gs = loadedGs;
                sm = loadedSm;
            }
            finally
            {
                if (File.Exists(pendingSavePath)) File.Delete(pendingSavePath);
            }
            Assert("待处理公共事件读档后仍阻塞", FindRestNode(sm.CurrentRootNode).Disabled,
                "world blocker restored");

            // 5) 交锋中路线兑现：老周免费援助、办案通行证、异常货单次要目标。
            int growthBeforeEvent = gs.Team.GrowthLevel;
            ExecuteNode(sm, "处理码头公共事件");
            Assert("进入公共交锋", sm.CurrentSceneName != "world", $"scene={sm.CurrentSceneName}");
            Assert("老周作为盟友出现", FindNode(sm.CurrentRootNode, "请老周出面") != null,
                "人物支线兑现");
            Assert("办案通行证成为额外行动", FindNode(sm.CurrentRootNode, "出示办案通行证") != null,
                "一次性物品解锁程序手段");
            ExecuteNode(sm, "请老周出面");
            ExecuteNode(sm, "出示办案通行证");
            Assert("办案通行证使用后消耗", gs.Inventory.GetCount("办案通行证") == 0,
                "通行证=0");
            ExecuteNode(sm, "抢下异常货单");
            ExecuteNode(sm, "抢下异常货单");
            Assert("可选目标完成后持有货单", gs.Get<string>("异常货单状态") == "持有",
                $"state={gs.Get<string>("异常货单状态")}");
            for (int i = 0; i < 6 && sm.CurrentSceneName != "world"; i++)
            {
                var talk = FindNode(sm.CurrentRootNode, "谈条件");
                if (talk == null) break;
                ExecuteActionWithDefaults(sm, talk);
                sm.Refresh();
            }
            Assert("公共交锋结束回城", sm.CurrentSceneName == "world", $"scene={sm.CurrentSceneName}");
            Assert("公共小节奖励成长", gs.Team.GrowthLevel == growthBeforeEvent + 1,
                $"growth={gs.Team.GrowthLevel}");
            Assert("主目标结束后货单仍保留", gs.Get<string>("异常货单状态") == "持有",
                "异常货单状态=持有");
            Assert("处理后恢复休息", !FindRestNode(sm.CurrentRootNode).Disabled,
                "world blocker released");

            // 6) 货单是持有状态，不是立即四选一；交付后其他去向消失。
            ExecuteNode(sm, "把异常货单交给探长");
            Assert("货单进入唯一终态", gs.Get<string>("异常货单状态") == "交给探长",
                $"state={gs.Get<string>("异常货单状态")}");
            Assert("其他货单去向消失",
                FindNode(sm.CurrentRootNode, "把异常货单交给老周") == null
                    && FindNode(sm.CurrentRootNode, "把异常货单卖给货运代理") == null,
                "单一状态保证互斥");

            // 7) 已取得的货单在主目标失败后保留；富商保险只赔付，不改变战局。
            var failureGs = new GameState();
            var failureSm = new SceneManager(failureGs, new LocalScriptLoader());
            failureSm.LoadScene("world");
            failureGs.Set("relation:富商", 3);
            failureGs.Inventory.SetCount("金钱", 100);
            failureSm.Refresh();
            ExecuteNode(failureSm, "陪货运代理应酬");
            ExecuteNode(failureSm, "核对代理人的条件");
            ExecuteNode(failureSm, "考察货运项目");
            ExecuteNode(failureSm, "投入货运项目");
            ExecuteNode(failureSm, "购买意外保险");

            string companySavePath = Path.Combine(Path.GetTempPath(), "ssnoir_company_state_save.json");
            try
            {
                failureSm.SaveGame(companySavePath);
                var loadedGs = new GameState();
                var loadedSm = new SceneManager(loadedGs, new LocalScriptLoader());
                loadedSm.LoadGame(companySavePath);
                failureGs = loadedGs;
                failureSm = loadedSm;
            }
            finally
            {
                if (File.Exists(companySavePath)) File.Delete(companySavePath);
            }
            Assert("投资与保险可存读档",
                FindNode(failureSm.CurrentRootNode, "货运公司")!.Clocks.Exists(c => c.Label == "投资结算")
                    && FindNode(failureSm.CurrentRootNode, "购买意外保险") == null
                    && FindNode(failureSm.CurrentRootNode, "保单生效中") != null,
                "投资进行中且保单有效");
            failureSm.ActiveInterpreter.Eval("(debug-trigger-public-event!)");
            failureSm.Refresh();
            ExecuteNode(failureSm, "处理码头公共事件");
            ExecuteNode(failureSm, "抢下异常货单");
            ExecuteNode(failureSm, "抢下异常货单");
            int moneyBeforeFailure = failureGs.Inventory.GetCount("金钱");
            for (int turn = 0; turn < 8 && failureSm.CurrentSceneName != "world"; turn++)
                failureSm.EndTurn();
            Assert("交锋失败仍回城", failureSm.CurrentSceneName == "world",
                $"scene={failureSm.CurrentSceneName}");
            Assert("失败后仍保留已取得货单", failureGs.Get<string>("异常货单状态") == "持有",
                $"state={failureGs.Get<string>("异常货单状态")}");
            Assert("保险只在失败后赔付", failureGs.Inventory.GetCount("金钱") == moneyBeforeFailure + 20,
                $"金钱 {moneyBeforeFailure}→{failureGs.Inventory.GetCount("金钱")}");

            Console.WriteLine("[arc] All assertions passed.");
        }
        private static ActionReport ExecuteActionWithDefaults(SceneManager sceneManager, GameNode node)
        {
            var slots = new List<SlottedResource?>();
            if (node.Requires != null)
            {
                var actor = sceneManager.GameState.Team.FindActor("player");
                if (actor != null)
                {
                    // Force dice to 6 to make simulator deterministic
                    actor.ActionDice.Clear();
                    actor.ActionDice.Add(6);
                    actor.ActionDice.Add(6);
                }

                int dieCount = 0;
                foreach (var req in node.Requires)
                {
                    if (req.Type == "die")
                    {
                        int val = 6;
                        if (actor != null && dieCount < actor.ActionDice.Count)
                        {
                            val = actor.ActionDice[dieCount];
                        }
                        slots.Add(new SlottedResource { Type = "die", ActorId = "player", DieIndex = dieCount, Value = val });
                        dieCount++;
                    }
                    else if (req.Type == "item")
                    {
                        slots.Add(new SlottedResource { Type = "item", ItemId = req.ItemId, Qty = req.Qty, Value = req.Qty });
                    }
                }
            }
            return sceneManager.ExecuteAction(node, slots);
        }

        private static ActionReport ExecuteNode(SceneManager sceneManager, string name)
        {
            var node = FindNode(sceneManager.CurrentRootNode, name);
            if (node == null)
            {
                throw new InvalidOperationException($"Node not found: {name}");
            }

            return ExecuteActionWithDefaults(sceneManager, node);
        }

        private static GameNode? FindNode(GameNode? node, string name)
        {
            if (node == null)
                return null;

            if (node.Name.StartsWith(name))
            {
                return node;
            }

            foreach (var childNode in node.Children)
            {
                var child = FindNode(childNode, name);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private static GameNode FindRestNode(GameNode? root)
        {
            return FindNode(root, "睡觉")
                ?? FindNode(root, "蜷缩在门口")
                ?? throw new InvalidOperationException("Rest node not found.");
        }

        public static void TestCapabilities()
        {
            Console.WriteLine("=== Schemy Capability Test ===");

            var interpreter = new Interpreter(
                new Interpreter.CreateSymbolTableDelegate[] { Builtins.CreateBuiltins },
                new ReadOnlyFileSystemAccessor()
            );

            // Register native function test-native-fn
            interpreter.DefineGlobal(Symbol.FromString("test-native-fn"), new NativeProcedure(args =>
            {
                var arg = args[0];
                Console.WriteLine($"[C# Native] Received argument. Type: {arg?.GetType().FullName}, Value: {arg}");
                return null;
            }, "test-native-fn"));

            interpreter.DefineGlobal(Symbol.FromString("string-append"), new NativeProcedure(args =>
            {
                return string.Concat(args);
            }, "string-append"));

            interpreter.DefineGlobal(Symbol.FromString("number->string"), new NativeProcedure(args =>
            {
                if (args.Count < 1)
                    throw new ArgumentException("number->string requires 1 argument");
                return args[0]?.ToString() ?? "";
            }, "number->string"));

            var rand = new Random();
            interpreter.DefineGlobal(Symbol.FromString("random-choice"), new NativeProcedure(args =>
            {
                if (args.Count < 1)
                    throw new ArgumentException("random-choice requires 1 argument: a list of options");

                if (args[0] is List<object> list)
                {
                    if (list.Count == 0)
                        return null;
                    return list[rand.Next(list.Count)];
                }

                throw new ArgumentException("random-choice argument must be a list");
            }, "random-choice"));

            // 1. Can lambda be saved as a variable?
            Console.WriteLine("\nTest 1: Can lambda be saved as a variable?");
            var r1 = interpreter.Evaluate(new StringReader(@"
                (define my-lambda (lambda () 42))
                my-lambda
            "));
            if (r1.Error != null)
            {
                Console.WriteLine($"Test 1 Failed: {r1.Error}");
            }
            else
            {
                Console.WriteLine($"Test 1 Success. Result type: {r1.Result?.GetType().FullName}");
            }


            // 2. Can lambda be put into a cons?
            Console.WriteLine("\nTest 2: Can lambda be put into a cons?");
            var r2 = interpreter.Evaluate(new StringReader(@"
                (cons (lambda () 42) '())
            "));
            if (r2.Error != null)
            {
                Console.WriteLine($"Test 2 Failed: {r2.Error}");
            }
            else
            {
                Console.WriteLine($"Test 2 Success. Result type: {r2.Result?.GetType().FullName}");
                if (r2.Result is List<object> list)
                {
                    Console.WriteLine($"List length: {list.Count}, Element 0 type: {list[0]?.GetType().FullName}");
                }
            }

            // 3. Can lambda be put into a quote structure?
            Console.WriteLine("\nTest 3: Can lambda be put into a quote structure?");
            var r3 = interpreter.Evaluate(new StringReader(@"
                '(lambda () 42)
            "));
            if (r3.Error != null)
            {
                Console.WriteLine($"Test 3 Failed: {r3.Error}");
            }
            else
            {
                Console.WriteLine($"Test 3 Success. Result type: {r3.Result?.GetType().FullName}");
                if (r3.Result is List<object> list)
                {
                    Console.WriteLine($"Quote list length: {list.Count}");
                    foreach (var item in list)
                    {
                        Console.WriteLine($"  Element: {item} (Type: {item?.GetType().FullName})");
                    }
                }
            }

            // 3b. Can lambda evaluated be put in a list using (list ...)?
            Console.WriteLine("\nTest 3b: Can lambda be put into a list using (list ...)?");
            var r3b = interpreter.Evaluate(new StringReader(@"
                (list (lambda () 42))
            "));
            if (r3b.Error != null)
            {
                Console.WriteLine($"Test 3b Failed: {r3b.Error}");
            }
            else
            {
                Console.WriteLine($"Test 3b Success. Result type: {r3b.Result?.GetType().FullName}");
                if (r3b.Result is List<object> list)
                {
                    Console.WriteLine($"List length: {list.Count}, Element 0 type: {list[0]?.GetType().FullName}");
                }
            }

            // 4. Can lambda be passed as native function parameter?
            Console.WriteLine("\nTest 4: Can lambda be passed as native function parameter?");
            var r4 = interpreter.Evaluate(new StringReader(@"
                (test-native-fn (lambda () 42))
            "));
            if (r4.Error != null)
            {
                Console.WriteLine($"Test 4 Failed: {r4.Error}");
            }
            else
            {
                Console.WriteLine($"Test 4 Success.");
            }

            // 5. Test varargs define syntax
            Console.WriteLine("\nTest 5: Test varargs define syntax");
            var r5 = interpreter.Evaluate(new StringReader(@"
                (define (test-varargs first . rest)
                  rest)
                (test-varargs 1 2 3)
            "));
            if (r5.Error != null)
            {
                Console.WriteLine($"Test 5 Failed: {r5.Error}");
            }
            else
            {
                Console.WriteLine($"Test 5 Success. Result type: {r5.Result?.GetType().FullName}, value: {r5.Result}");
            }

            // Test 5b: lambda with symbol list parameter
            Console.WriteLine("\nTest 5b: lambda with symbol list parameter");
            var r5b = interpreter.Evaluate(new StringReader(@"
                (define test-varargs-b (lambda args args))
                (test-varargs-b 1 2 3)
            "));
            if (r5b.Error != null)
            {
                Console.WriteLine($"Test 5b Failed: {r5b.Error}");
            }
            else
            {
                Console.WriteLine($"Test 5b Success. Result type: {r5b.Result?.GetType().FullName}, value: {r5b.Result}");
                if (r5b.Result is List<object> list5b)
                {
                    Console.WriteLine($"  Length: {list5b.Count}");
                    foreach (var x in list5b)
                    {
                        Console.WriteLine($"    Item: {x} (Type: {x?.GetType().FullName})");
                    }
                }
            }

            // Test 5c: define with single symbol parameter list
            Console.WriteLine("\nTest 5c: define with single symbol parameter list");
            var r5c = interpreter.Evaluate(new StringReader(@"
                (define (test-varargs-c args) args)
                (test-varargs-c 1 2 3)
            "));
            if (r5c.Error != null)
            {
                Console.WriteLine($"Test 5c Failed (as expected if it expects 1 arg): {r5c.Error}");
            }
            else
            {
                Console.WriteLine($"Test 5c Success (unexpected if it expects 1 arg). Result type: {r5c.Result?.GetType().FullName}, value: {r5c.Result}");
            }

            // Test 5d: lambda with dot-varargs parameter list
            Console.WriteLine("\nTest 5d: lambda with dot-varargs parameter list");
            var r5d = interpreter.Evaluate(new StringReader(@"
                (define test-varargs-d (lambda (first . rest) rest))
                (test-varargs-d 1 2 3)
            "));
            if (r5d.Error != null)
            {
                Console.WriteLine($"Test 5d Failed: {r5d.Error}");
            }
            else
            {
                Console.WriteLine($"Test 5d Success. Result type: {r5d.Result?.GetType().FullName}, value: {r5d.Result}");
            }

            // Test 5e: test node constructor implementation
            Console.WriteLine("\nTest 5e: test node constructor implementation");
            var r5e = interpreter.Evaluate(new StringReader(@"
                (define (cadr xs) (car (cdr xs)))
                (define (get-kwarg kwargs key default)
                  (if (null? kwargs)
                      default
                      (if (null? (cdr kwargs))
                          default
                          (if (equal? (car kwargs) key)
                              (cadr kwargs)
                              (get-kwarg (cdr (cdr kwargs)) key default)))))
                (define node
                  (lambda args
                    (let ((name (car args))
                          (kwargs (cdr args)))
                      (list 'node
                            name
                            (get-kwarg kwargs ':children '())
                            (get-kwarg kwargs ':effect #f)))))
                (node ""写代码"" ':effect (lambda () 42))
            "));
            if (r5e.Error != null)
            {
                Console.WriteLine($"Test 5e Failed: {r5e.Error}");
            }
            else
            {
                Console.WriteLine($"Test 5e Success. Result type: {r5e.Result?.GetType().FullName}, value: {r5e.Result}");
                if (r5e.Result is List<object> list5e)
                {
                    Console.WriteLine($"  Length: {list5e.Count}");
                    for (int i = 0; i < list5e.Count; i++)
                    {
                        Console.WriteLine($"    [{i}]: {list5e[i]} (Type: {list5e[i]?.GetType().FullName})");
                    }
                }
            }

            // Test 6: Test map, apply append, and make-enemy closure
            Console.WriteLine("\nTest 6: Test map, apply append, and make-enemy closure");
            var r6 = interpreter.Evaluate(new StringReader(@"
                (define (make-clock label max)
                  (let ((current 0))
                    (lambda (msg)
                      (cond
                        ((equal? msg 'tick!)       (set! current (+ current 1)))
                        ((equal? msg 'reset!)      (set! current 0))
                        ((equal? msg 'full?)       (>= current max))
                        ((equal? msg 'render-data) (list 'clock label current max))
                        (else #f)))))

                (define (make-enemy type hp-max atk-max dmg)
                  (let ((hp hp-max)
                        (atk-clock (make-clock type atk-max)))
                    (let ((suppress! (lambda ()
                                       (set! hp (- hp 1))
                                       (atk-clock 'reset!)))
                          (eliminate! (lambda ()
                                        (set! hp (- hp 2)))))
                      (lambda (msg)
                        (cond
                          ((equal? msg 'type)      type)
                          ((equal? msg 'hp)        hp)
                          ((equal? msg 'dead?)     (<= hp 0))
                          ((equal? msg 'tick-atk!) (atk-clock 'tick!))
                          ((equal? msg 'atk-full?) (atk-clock 'full?))
                          ((equal? msg 'reset-atk!)(atk-clock 'reset!))
                          ((equal? msg 'render-data)
                           (list
                             (atk-clock 'render-data)
                             (list 'node type
                                   (list
                                     (list 'node ""压制"" '() (lambda () (suppress!)))
                                     (list 'node ""击倒"" '() (lambda () (eliminate!))))
                                   #f)))
                          (else #f))))))

                (define enemy (make-enemy ""持刀者"" 2 2 1))
                (enemy 'render-data)
            "));
            if (r6.Error != null)
            {
                Console.WriteLine($"Test 6 Failed: {r6.Error}");
            }
            else
            {
                Console.WriteLine($"Test 6 Success. Result type: {r6.Result?.GetType().FullName}, value: {r6.Result}");
                if (r6.Result is List<object> list6)
                {
                    Console.WriteLine($"  Length: {list6.Count}");
                    for (int i = 0; i < list6.Count; i++)
                    {
                        Console.WriteLine($"    [{i}]: {list6[i]} (Type: {list6[i]?.GetType().FullName})");
                    }
                }
            }

            // Test 7: Test map and apply builtins
            Console.WriteLine("\nTest 7: Test map and apply builtins");
            var r7 = interpreter.Evaluate(new StringReader(@"
                (list (map (lambda (x) (+ x 1)) '(1 2 3))
                      (apply + '(1 2 3))
                      (apply append '((1 2) (3 4))))
            "));
            if (r7.Error != null)
            {
                Console.WriteLine($"Test 7 Failed: {r7.Error}");
            }
            else
            {
                Console.WriteLine($"Test 7 Success. Result: {r7.Result}");
                if (r7.Result is List<object> list7)
                {
                    Console.WriteLine($"  Length: {list7.Count}");
                    for (int i = 0; i < list7.Count; i++)
                    {
                        Console.WriteLine($"    [{i}]: {list7[i]} (Type: {list7[i]?.GetType().FullName})");
                    }
                }
            }

            // Test 8: Test string-append and number->string
            Console.WriteLine("\nTest 8: Test string-append and number->string");
            var r8 = interpreter.Evaluate(new StringReader(@"
                (string-append ""HP: "" (number->string 3))
            "));
            if (r8.Error != null)
            {
                Console.WriteLine($"Test 8 Failed: {r8.Error}");
            }
            else
            {
                Console.WriteLine($"Test 8 Success. Result: {r8.Result}");
            }

            // Test 9: Check whether raw Schemy and project stdlib expose `and` and `or`
            Console.WriteLine("\nTest 9: Test `and` and `or` availability");
            
            // Raw Schemy test for 'and'
            var r9RawAnd = interpreter.Evaluate(new StringReader("(and #t #t)"));
            if (r9RawAnd.Error != null)
            {
                Console.WriteLine($"Test 9 Raw Schemy 'and' Failed (expected): {r9RawAnd.Error.GetType().FullName}: {r9RawAnd.Error.Message}");
            }
            else
            {
                Console.WriteLine($"Test 9 Raw Schemy 'and' Success. Result: {r9RawAnd.Result}");
            }

            // Raw Schemy test for 'or'
            var r9RawOr = interpreter.Evaluate(new StringReader("(or #f #t)"));
            if (r9RawOr.Error != null)
            {
                Console.WriteLine($"Test 9 Raw Schemy 'or' Failed (expected): {r9RawOr.Error.GetType().FullName}: {r9RawOr.Error.Message}");
            }
            else
            {
                Console.WriteLine($"Test 9 Raw Schemy 'or' Success. Result: {r9RawOr.Result}");
            }

            var projectInterpreter = new SchemeInterpreter(new GameState(), new LocalScriptLoader());
            
            // Define a helper to run project test case and assert/print result
            Action<string, object> runProjectTest = (expression, expected) =>
            {
                try
                {
                    var result = projectInterpreter.Eval(expression);
                    bool match = Equals(result, expected);
                    Console.WriteLine($"  {expression} => {result} (Expected: {expected}) - {(match ? "PASS" : "FAIL")}");
                    if (!match)
                    {
                        throw new InvalidOperationException($"Test failed for: {expression}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  {expression} => ERROR: {ex.GetType().FullName}: {ex.Message} - FAIL");
                    throw;
                }
            };

            Console.WriteLine("Testing 'and' and 'or' under project stdlib:");
            runProjectTest("(and)", true);
            runProjectTest("(and #t)", true);
            runProjectTest("(and #f)", false);
            runProjectTest("(and #t #t)", true);
            runProjectTest("(and #t #f)", false);
            runProjectTest("(and #t #t #f)", false);
            runProjectTest("(and #t #t #t)", true);

            runProjectTest("(or)", false);
            runProjectTest("(or #f)", false);
            runProjectTest("(or #t)", true);
            runProjectTest("(or #f #f)", false);
            runProjectTest("(or #f #t)", true);
            runProjectTest("(or #f #f #t)", true);
            runProjectTest("(or #f #f #f)", false);

            runProjectTest("(and (or #f #t) #t)", true);
            runProjectTest("(and (or #f #f) #t)", false);
            runProjectTest("(or (and #t #f) #t)", true);
            runProjectTest("(or (and #t #f) (and #f #t))", false);
            Console.WriteLine("Test 9 Project stdlib 'and'/'or' Completed Successfully.");

            // Test 10: Check availability of common Scheme symbols and keywords
            Console.WriteLine("\nTest 10: Unified Scheme Feature / Symbol Matrix");

            var symbolsToTest = new string[]
            {
                "+", "-", "*", "/", "=", "<", ">", "<=", ">=", "abs", "modulo", "remainder", "quotient", "even?", "odd?", "zero?",
                "eq?", "eqv?", "equal?",
                "null?", "pair?", "list?", "number?", "string?", "symbol?", "procedure?", "boolean?",
                "cons", "car", "cdr", "cadr", "caddr", "cadddr", "list", "length", "append", "reverse", "member", "assoc",
                "map", "filter", "apply",
                "string-append", "number->string", "display", "newline", "error", "not", "and", "or"
            };

            var expressionsToTest = new string[]
            {
                "(let ((x 1)) x)",
                "(let* ((x 1) (y (+ x 1))) y)",
                "(begin 1 2)",
                "(if #t 1 2)",
                "(cond (#f 1) (#t 2) (else 3))",
                "(case 2 ((1) 'one) ((2) 'two) (else 'other))",
                "((lambda () 42))",
                "(begin (define test-val 99) test-val)"
            };

            Console.WriteLine(string.Format("{0,-18} | {1,-15} | {2,-30}", "Symbol/Expr", "Raw Schemy", "Project Interpreter (with stdlib)"));
            Console.WriteLine(new string('-', 75));

            foreach (var sym in symbolsToTest)
            {
                // Test raw
                var rawResult = interpreter.Evaluate(new StringReader(sym));
                string rawStatus = rawResult.Error != null ? "NO" : "YES";

                // Test project
                string projStatus;
                try
                {
                    var result = projectInterpreter.Eval(sym);
                    projStatus = "YES";
                }
                catch
                {
                    projStatus = "NO";
                }

                Console.WriteLine(string.Format("{0,-18} | {1,-15} | {2,-30}", sym, rawStatus, projStatus));
            }

            Console.WriteLine("\nSyntax & Special Forms Expression Check:");
            Console.WriteLine(new string('-', 75));

            foreach (var expr in expressionsToTest)
            {
                // Test raw
                var rawResult = interpreter.Evaluate(new StringReader(expr));
                string rawStatus = rawResult.Error != null ? "FAIL" : "PASS";

                // Test project
                string projStatus;
                try
                {
                    projectInterpreter.Eval(expr);
                    projStatus = "PASS";
                }
                catch
                {
                    projStatus = "FAIL";
                }

                Console.WriteLine(string.Format("{0,-45} | {1,-10} | {2,-10}", expr, rawStatus, projStatus));
            }
        }

        private static void AssertParenBalance(string filePath)
        {
            string content = File.ReadAllText(filePath);
            string fileName = Path.GetFileName(filePath);
            string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var stack = new Stack<(int line, int col)>();
            bool inString = false;
            bool escape = false;

            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li];
                escape = false;
                for (int ci = 0; ci < line.Length; ci++)
                {
                    char c = line[ci];
                    if (inString)
                    {
                        if (escape) { escape = false; continue; }
                        if (c == '\\') { escape = true; continue; }
                        if (c == '"') inString = false;
                        continue;
                    }
                    if (c == ';') break;
                    if (c == '"') { inString = true; continue; }
                    if (c == '(')
                    {
                        stack.Push((li + 1, ci + 1));
                    }
                    else if (c == ')')
                    {
                        if (stack.Count == 0)
                            throw new InvalidDataException(
                                $"括号不平衡 [{fileName}] 第 {li + 1} 行第 {ci + 1} 列：多余的 ')'\n  {line.Trim()}");
                        stack.Pop();
                    }
                }
            }

            if (stack.Count > 0)
            {
                var (ul, uc) = stack.Pop();
                throw new InvalidDataException(
                    $"括号不平衡 [{fileName}] 第 {ul} 行第 {uc} 列：'(' 未关闭\n  {lines[ul - 1].Trim()}");
            }
        }

        private static void CheckParenthesesDiagnostics(string filePath)
        {
            if (!File.Exists(filePath)) return;
            try
            {
                string code = File.ReadAllText(filePath);
                var stack = new Stack<(int line, int col)>();
                string[] lines = code.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
                
                bool inString = false;
                bool escape = false;
                
                for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
                {
                    string line = lines[lineIdx];
                    escape = false;
                    for (int colIdx = 0; colIdx < line.Length; colIdx++)
                    {
                        char c = line[colIdx];
                        if (inString)
                        {
                            if (escape)
                            {
                                escape = false;
                            }
                            else if (c == '\\')
                            {
                                escape = true;
                            }
                            else if (c == '"')
                            {
                                inString = false;
                            }
                            continue;
                        }
                        
                        if (c == ';')
                        {
                            break;
                        }
                        
                        if (c == '"')
                        {
                            inString = true;
                            escape = false;
                            continue;
                        }
                        
                        if (c == '(')
                        {
                            stack.Push((lineIdx + 1, colIdx + 1));
                        }
                        else if (c == ')')
                        {
                            if (stack.Count == 0)
                            {
                                Console.Error.WriteLine($"\n[DIAGNOSTICS] Parenthesis mismatch in {filePath}:");
                                Console.Error.WriteLine($"  Extra closing parenthesis ')' found at Line {lineIdx + 1}, Col {colIdx + 1}.");
                                Console.Error.WriteLine($"  Line content: {line.Trim()}");
                                return;
                            }
                            stack.Pop();
                        }
                    }
                }
                
                if (stack.Count > 0)
                {
                    var unmatched = stack.Pop();
                    Console.Error.WriteLine($"\n[DIAGNOSTICS] Parenthesis mismatch in {filePath}:");
                    Console.Error.WriteLine($"  Unclosed opening parenthesis '(' started at Line {unmatched.line}, Col {unmatched.col}.");
                    Console.Error.WriteLine($"  Line content: {lines[unmatched.line - 1].Trim()}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DIAGNOSTICS] Failed to perform parenthesis checks: {ex.Message}");
            }
        }
    }
}
