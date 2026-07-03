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

        // 端到端跑一遍成长弧线：花成长点 → 对账拿点 → 老周好感 → 排期交锋 → 打赢回城。
        // 全程强制骰=6（成功），验证动机/关系/交锋/复发/城市后果这条链不断。
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

            var initialDock = FindNode(sm.CurrentRootNode, "码头")!;
            Assert("交锋排期开局就在码头",
                initialDock.Clocks.Exists(clock => clock.Label == "找上门"
                    && clock.Current == 3 && clock.Note.Contains("再次找上码头")),
                "找上门 3/3，仅挂在码头");
            Assert("交锋排期不挂世界层",
                !sm.CurrentRootNode!.Clocks.Exists(clock => clock.Label == "找上门"),
                "世界根节点无找上门 Clock");

            // 1) 成长点花费入口：给 1 点 → 练交际 → social 1→2
            gs.Team.GrowthLevel = 1;
            sm.Refresh();
            int socialBefore = gs.Team.FindActor("player")!.Stats["social"];
            var growthReport = ExecuteNode(sm, "练交际");
            int socialAfter = gs.Team.FindActor("player")!.Stats["social"];
            Assert("练交际提升属性", socialAfter == socialBefore + 1, $"social {socialBefore}→{socialAfter}");
            Assert("属性成长进入效果条",
                growthReport.Effects.Exists(effect => effect.Label == "交际" && effect.Delta == 1),
                "交际 +1");

            // 2) 对账动机：脸熟 + 持线索 → 完成给 +1 成长点
            gs.Set("relation:劳工", 3);      // 脸熟
            gs.Set("货单对不上", true);      // 记账线索
            sm.Refresh();
            int growthBefore = gs.Team.GrowthLevel;
            ExecuteNode(sm, "帮老周对账");
            Assert("对账给成长点", gs.Team.GrowthLevel == growthBefore + 1, $"growth {growthBefore}→{gs.Team.GrowthLevel}");
            Assert("对账后老周常驻", FindNode(sm.CurrentRootNode, "老周") != null, "老周节点出现");

            // 3) 老周好感：搭把手 → 好感到熟络门槛，交锋可增援
            ExecuteNode(sm, "跟老周搭把手");  // 好感 2→3
            Assert("好感达增援门槛", gs.Get<int>("laozhou-favor") >= 3, $"favor={gs.Get<int>("laozhou-favor")}");

            // 4) 老周美差必须能随中途存档恢复，且做完一次立刻消失，不能同日反复刷。
            sm.ActiveInterpreter.Eval("(dock 'debug-cushy)");
            sm.Refresh();
            Assert("老周美差已刷新", FindNode(sm.CurrentRootNode, "帮老周带个话") != null, "美差节点出现");

            string arcSavePath = Path.Combine(Path.GetTempPath(), "ssnoir_growth_arc_save.json");
            try
            {
                sm.SaveGame(arcSavePath);
                var loadedGs = new GameState();
                var loadedSm = new SceneManager(loadedGs, new LocalScriptLoader());
                loadedSm.LoadGame(arcSavePath);
                gs = loadedGs;
                sm = loadedSm;
            }
            finally
            {
                if (File.Exists(arcSavePath)) File.Delete(arcSavePath);
            }
            Assert("中途读档保留老周美差", FindNode(sm.CurrentRootNode, "帮老周带个话") != null, "美差仍可用");
            var cushyNode = FindNode(sm.CurrentRootNode, "帮老周带个话")!;
            Assert("老周美差显示限时时钟",
                cushyNode.Clocks.Exists(clock => clock.Label == "转瞬即逝"
                    && clock.Current == 1 && clock.Max == 1 && clock.Style == ClockStyle.Countdown
                    && clock.Note.Contains("只在今天有效")),
                "转瞬即逝 1/1 countdown + 到期说明");
            ExecuteNode(sm, "帮老周带个话");
            Assert("老周美差只可做一次", FindNode(sm.CurrentRootNode, "帮老周带个话") == null, "结算后节点消失");

            // 5) 复发 3 场，逐场升级；每场：排期 → 进场 → 谈条件打满 → 回城
            int growthBeforeBouts = gs.Team.GrowthLevel;
            for (int bout = 1; bout <= 3; bout++)
            {
                bool pending = false;
                for (int day = 0; day < 6 && !pending; day++)
                {
                    sm.EndTurn();
                    sm.Refresh();
                    pending = FindNode(sm.CurrentRootNode, "有人来找你") != null;
                }
                Assert($"第{bout}场按时排到", pending, "出现交锋入口");
                var pendingDock = FindNode(sm.CurrentRootNode, "码头")!;
                Assert($"第{bout}场到期状态显示在码头",
                    pendingDock.Clocks.Exists(clock => clock.Label == "找上门"
                        && clock.Current == 0 && clock.Note.Contains("码头应付")),
                    "Clock 在码头持续显示直到处理");

                var blockedSleep = FindRestNode(sm.CurrentRootNode);
                Assert($"第{bout}场到期后禁止休息",
                    blockedSleep.Disabled && blockedSleep.Tags.Contains("不可休息")
                        && blockedSleep.Tags.Contains("码头：必须处理找上门的人"),
                    "睡觉 disabled，并显示阻塞原因");

                bool disabledRejected = false;
                try { sm.ExecuteAction(blockedSleep, new List<SlottedResource?>()); }
                catch (InvalidOperationException) { disabledRejected = true; }
                Assert($"第{bout}场 disabled 节点不可执行", disabledRejected, "ExecuteAction 拒绝睡觉");

                if (bout == 1)
                {
                    string pendingSavePath = Path.Combine(Path.GetTempPath(), "ssnoir_pending_bout_save.json");
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
                    Assert("待处理交锋读档后重新阻塞休息",
                        FindRestNode(sm.CurrentRootNode).Disabled,
                        "地点状态重新注册 blocker");

                    sm.ActiveInterpreter.Eval("(rest-block! \"测试/第二事件\" \"警局：必须接受问询\")");
                    sm.Refresh();
                    var multiplyBlockedSleep = FindRestNode(sm.CurrentRootNode);
                    Assert("多个 blocker 同时显示",
                        multiplyBlockedSleep.Tags.Contains("码头：必须处理找上门的人")
                            && multiplyBlockedSleep.Tags.Contains("警局：必须接受问询"),
                        "两个原因都在睡觉节点上");
                    sm.ActiveInterpreter.Eval("(rest-release! \"测试/第二事件\")");
                    sm.Refresh();
                    Assert("释放一个 blocker 后仍禁止休息",
                        FindRestNode(sm.CurrentRootNode).Disabled,
                        "码头 blocker 仍存在");

                    bool wrapperRejected = false;
                    try { sm.ActiveInterpreter.Eval("(end-turn!)"); }
                    catch (Exception) { wrapperRejected = true; }
                    Assert("Scheme end-turn 不能绕过 blocker", wrapperRejected, "end-turn! 拒绝推进");
                }

                int moneyBefore = gs.Inventory.GetCount("金钱");
                ExecuteNode(sm, "有人来找你");
                sm.Refresh();
                Assert($"第{bout}场进入交锋", sm.CurrentSceneName != "world", $"scene={sm.CurrentSceneName}");
                Assert($"第{bout}场老周增援", FindNode(sm.CurrentRootNode, "老周") != null, "好感门槛兑现为盟友单位");
                if (bout == 1)
                {
                    sm.EndTurn();
                    sm.Refresh();
                    Assert("世界 blocker 不阻止交锋回合", sm.CurrentSceneName != "world", "交锋可正常推进回合");
                }
                for (int i = 0; i < 8 && sm.CurrentSceneName != "world"; i++)
                {
                    var talk = FindNode(sm.CurrentRootNode, "谈条件");
                    if (talk == null) break;
                    ExecuteActionWithDefaults(sm, talk);
                    sm.Refresh();
                }
                Assert($"第{bout}场打赢回城", sm.CurrentSceneName == "world", $"scene={sm.CurrentSceneName}");
                Assert($"第{bout}场处理后恢复休息", !FindRestNode(sm.CurrentRootNode).Disabled, "blocker 已释放");
                Assert($"第{bout}场有收益", gs.Inventory.GetCount("金钱") > moneyBefore, $"金钱 {moneyBefore}→{gs.Inventory.GetCount("金钱")}");
            }

            // 6) 收尾：第 3 场全胜给里程碑成长点；之后不再排新场
            Assert("弧线收尾给成长点", gs.Team.GrowthLevel == growthBeforeBouts + 1, $"growth {growthBeforeBouts}→{gs.Team.GrowthLevel}");
            bool reArmed = false;
            for (int day = 0; day < 6 && !reArmed; day++)
            {
                sm.EndTurn();
                sm.Refresh();
                reArmed = FindNode(sm.CurrentRootNode, "有人来找你") != null;
            }
            Assert("收尾后不再复发", !reArmed, "无新交锋入口");

            // 7) 失败路径：必须打完整场交锋；失败回城、释放 blocker，并让走私暂闭。
            var failureGs = new GameState();
            var failureSm = new SceneManager(failureGs, new LocalScriptLoader());
            failureSm.LoadScene("world");
            // 从 7 开始，失败 -1 后仍保持“自己人”，从而单独验证三天风声冷却。
            failureGs.Set("relation:劳工", 7);
            failureSm.Refresh();
            for (int day = 0; day < 3; day++) failureSm.EndTurn();
            failureSm.Refresh();
            Assert("失败测试开始前休息被阻塞", FindRestNode(failureSm.CurrentRootNode).Disabled, "交锋已经到期");
            ExecuteNode(failureSm, "有人来找你");
            for (int turn = 0; turn < 8 && failureSm.CurrentSceneName != "world"; turn++)
                failureSm.EndTurn();
            Assert("交锋失败仍回到城市", failureSm.CurrentSceneName == "world", $"scene={failureSm.CurrentSceneName}");
            Assert("交锋失败后恢复休息", !FindRestNode(failureSm.CurrentRootNode).Disabled, "blocker 已释放");
            Assert("失败后走私暂闭", FindNode(failureSm.CurrentRootNode, "走私") == null, "走私节点隐藏");
            var retreatDock = FindNode(failureSm.CurrentRootNode, "码头")!;
            Assert("走私冷却对玩家可见",
                retreatDock.Clocks.Exists(clock => clock.Label == "码头风声"
                    && clock.Current == 3 && clock.Style == ClockStyle.Countdown
                    && clock.Note.Contains("走私工作恢复")),
                "码头风声 3/3 + 恢复说明");
            for (int day = 0; day < 3; day++) failureSm.EndTurn();
            failureSm.Refresh();
            Assert("风声三天后消退", FindNode(failureSm.CurrentRootNode, "走私") != null, "走私节点恢复");

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
