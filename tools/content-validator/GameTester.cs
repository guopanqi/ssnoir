using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SSNoir.Core;
using SSNoir.Scripting;

namespace SSNoir.Testing
{
    public static class GameTester
    {
        // 内容校验只检查所有 Scheme 文件的基础语法，并确保每个顶层场景能加载和渲染。
        // 不固化剧情节点名称、数值平衡或完整游玩流程。
        public static void ValidateContent()
        {
            var scenesDir = Path.Combine(ProjectPaths.ContentRoot, "scenes");
            if (!Directory.Exists(scenesDir))
                throw new DirectoryNotFoundException($"找不到场景目录：{scenesDir}");

            Console.WriteLine("[validate] Phase 1: paren balance...");
            foreach (var scmFile in Directory.GetFiles(scenesDir, "*.scm", SearchOption.AllDirectories))
                AssertParenBalance(scmFile);
            Console.WriteLine("[validate] Phase 1: all files balanced.");

            Console.WriteLine("[validate] Phase 1b: state variables called as procedures...");
            foreach (var scmFile in Directory.GetFiles(scenesDir, "*.scm", SearchOption.AllDirectories))
                AssertNoStateVariableCalls(scmFile);
            foreach (var scmFile in Directory.GetFiles(
                         Path.Combine(Path.GetDirectoryName(scenesDir) ?? "", "scripts"), "*.scm", SearchOption.AllDirectories))
                AssertNoStateVariableCalls(scmFile);
            Console.WriteLine("[validate] Phase 1b: none.");

            foreach (var scenePath in Directory.GetFiles(scenesDir, "*.scm", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(scenesDir, scenePath);
                var sceneName = Path.Combine(
                    Path.GetDirectoryName(relativePath) ?? "",
                    Path.GetFileNameWithoutExtension(relativePath)).Replace('\\', '/');

                if (sceneName.StartsWith("world/") && sceneName != "world/world")
                    continue;

                var sceneManager = new SceneManager(new GameState(), new LocalScriptLoader());
                Console.WriteLine($"[validate] {sceneName}");
                try
                {
                    sceneManager.LoadScene(sceneName);
                }
                catch (Exception ex)
                {
                    string targetFile = scenePath;
                    var match = System.Text.RegularExpressions.Regex.Match(
                        ex.Message, @"Error loading Scheme script '([^']+)'");
                    if (match.Success)
                    {
                        string relPath = match.Groups[1].Value;
                        string possiblePath = Path.Combine(Path.GetDirectoryName(scenesDir) ?? "", relPath);
                        if (File.Exists(possiblePath))
                            targetFile = possiblePath;
                    }
                    CheckParenthesesDiagnostics(targetFile);
                    throw;
                }

                if (sceneManager.CurrentRootNode == null)
                    throw new InvalidDataException($"Scene '{sceneName}' produced an empty world.");

                if (sceneName.StartsWith("encounters/", StringComparison.Ordinal))
                    AssertEncounterCollapsePolicy(sceneManager, sceneName);

                if (sceneName == "world/world")
                {
                    AssertNodeAnchorDsl(sceneManager);
                    AssertArrivalDsl(sceneManager);
                    AssertPlaceUnlockNotifications(sceneManager);
                }

                Console.WriteLine($"Validated scene '{sceneName}' with root '{sceneManager.CurrentRootNode.Name}'.");
            }
        }

        /// <summary>
        /// 把状态变量当函数调用：`(define 查到了? #f)` 写成 `(if (查到了?) …)`。
        /// Schemy 直到那一行真的被求值才会喊 "Object is not callable: False"，
        /// 而世界渲染只重建**当前可见**的地点——被剧情门槛挡住的分支，validate 走不到，
        /// 一路静默到玩家在那个阶段推开那扇门为止。
        ///
        /// 谓词函数和状态变量在这套内容里都以 ? 结尾，肉眼分不出来，所以只能机器查：
        /// 同一个文件里定义成值的名字，如果又以 `(名字)` 的零参形式被调用，就是这个错。
        /// 纯文本检查，不需要求值，因此不受剧情阶段影响。
        /// </summary>
        private static void AssertNoStateVariableCalls(string filePath)
        {
            string text = File.ReadAllText(filePath);

            var functions = new HashSet<string>(StringComparer.Ordinal);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\(define\s*\(\s*([^\s()]+)"))
                functions.Add(m.Groups[1].Value);

            var values = new HashSet<string>(StringComparer.Ordinal);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\(define\s+([^\s()]+)\s+(\S+)"))
            {
                // (define f (lambda …)) 仍然是函数；(define n 5) / (define b #f) / (define xs '()) 才是值。
                if (m.Groups[2].Value.StartsWith("(lambda", StringComparison.Ordinal)) continue;
                values.Add(m.Groups[1].Value);
            }

            foreach (string name in values)
            {
                if (functions.Contains(name)) continue;
                var call = System.Text.RegularExpressions.Regex.Match(text, @"\(" + System.Text.RegularExpressions.Regex.Escape(name) + @"\)");
                if (!call.Success) continue;
                int line = text.Take(call.Index).Count(c => c == '\n') + 1;
                throw new InvalidDataException(
                    $"{Path.GetFileName(filePath)}:{line} 把状态变量 '{name}' 当函数调用了："
                    + $"写的是 ({name})，应该直接写 {name}。"
                    + "（它是 (define " + name + " …) 定义的值，不是过程。）");
            }
        }

        private static void AssertEncounterCollapsePolicy(SceneManager sceneManager, string sceneName)
        {
            object raw = sceneManager.ActiveInterpreter.Eval("(on-encounter-collapse)");
            if (raw is not List<object> policy || policy.Count == 0)
                throw new InvalidDataException(
                    $"Encounter '{sceneName}' must return collapse-result or collapse-retry from on-encounter-collapse.");

            string kind = SchemeValue.AsId(policy[0]);
            bool valid = (kind == "collapse-result" && policy.Count == 2)
                || (kind == "collapse-retry" && policy.Count == 1);
            if (!valid)
                throw new InvalidDataException(
                    $"Encounter '{sceneName}' returned an invalid on-encounter-collapse policy.");
        }

        private static void AssertNodeAnchorDsl(SceneManager sceneManager)
        {
            var explicitExpr = sceneManager.ActiveInterpreter.Eval(
                "(node \"锚点DSL校验\" :anchor \"锚点DSL校验@测试\")");
            var explicitNode = NodeConverter.ConvertSingle(
                explicitExpr, sceneManager.ActiveInterpreter.RawInterpreter);
            if (explicitNode.AnchorName != "锚点DSL校验@测试"
                || explicitNode.EffectiveAnchorName != "锚点DSL校验@测试"
                || !explicitNode.HasExplicitAnchor)
            {
                throw new InvalidDataException("node :anchor did not survive the Scheme-to-GameNode conversion.");
            }

            var defaultExpr = sceneManager.ActiveInterpreter.Eval("(node \"默认锚点DSL校验\")");
            var defaultNode = NodeConverter.ConvertSingle(
                defaultExpr, sceneManager.ActiveInterpreter.RawInterpreter);
            if (defaultNode.AnchorName != null
                || defaultNode.EffectiveAnchorName != defaultNode.Name
                || defaultNode.HasExplicitAnchor)
            {
                throw new InvalidDataException("node without :anchor did not preserve name-based fallback.");
            }

            Console.WriteLine("[validate] node :anchor DSL contract passed.");
        }

        // place / arrival 的 DSL 契约：作者写错的地方基本都在这一层，而写错的后果
        // （节拍永远不触发）是静默的，必须在加载期就响。
        private static void AssertArrivalDsl(SceneManager sceneManager)
        {
            var interpreter = sceneManager.ActiveInterpreter;
            var raw = interpreter.RawInterpreter;

            GameNode ConvertScheme(string expr) =>
                NodeConverter.ConvertSingle(interpreter.Eval(expr), raw);

            var plain = ConvertScheme("(container \"入场DSL校验-普通容器\" '())");
            if (plain.IsPlace || plain.Arrivals.Count != 0)
                throw new InvalidDataException("container 不该是 place，也不该带入场节拍。");

            var bare = ConvertScheme("(place \"入场DSL校验-空地点\" :children '())");
            if (!bare.IsPlace || bare.Arrivals.Count != 0)
                throw new InvalidDataException("place 没有作为地点存活到 GameNode。");

            // place 转发 node 的全部 kwargs——「家」要靠 :subtitle 显示住所等级。
            var withSubtitle = ConvertScheme(
                "(place \"入场DSL校验-带副标题\" :subtitle \"廉价旅馆\" :children '())");
            if (withSubtitle.Subtitle != "廉价旅馆")
                throw new InvalidDataException("place 没有转发 :subtitle。");

            var withBeats = ConvertScheme(
                "(place \"入场DSL校验-两拍\" :children '()"
                + " :arrivals (list (arrival \"甲\" (lambda () #t))"
                + "                 (arrival \"乙\" (lambda () #t))))");
            if (withBeats.Arrivals.Count != 2
                || withBeats.Arrivals[0].Id != "甲" || withBeats.Arrivals[1].Id != "乙")
                throw new InvalidDataException("入场节拍没有按声明顺序保留。");

            AssertThrowsAny(() => ConvertScheme(
                "(place \"入场DSL校验-带结算\" :resolve (instant (lambda () #t)))"),
                "place with :resolve");

            AssertThrowsAny(() => ConvertScheme(
                "(node \"入场DSL校验-节拍挂错\" :children '()"
                + " :arrivals (list (arrival \"甲\" (lambda () #t))))"),
                "arrivals on a non-place");

            AssertThrowsAny(() => ConvertScheme(
                "(place \"入场DSL校验-重复标识\" :children '()"
                + " :arrivals (list (arrival \"甲\" (lambda () #t))"
                + "                 (arrival \"甲\" (lambda () #t))))"),
                "duplicate arrival id");

            AssertThrowsAny(() => ConvertScheme(
                "(place \"入场DSL校验-空标识\" :children '()"
                + " :arrivals (list (arrival \"\" (lambda () #t))))"),
                "empty arrival id");

            AssertThrowsAny(() => ConvertScheme(
                "(place \"入场DSL校验-非过程\" :children '()"
                + " :arrivals (list (arrival \"甲\" \"不是过程\")))"),
                "arrival effect that is not a procedure");

            // 强制遭遇允许把 start-encounter 放在 arrival 末尾。引擎先准备交锋快照，
            // 但不能发普通场景加载事件打断客户端仍在播放的入场报告。
            var forcedEncounter = ConvertScheme(
                "(place \"入场DSL校验-强制遭遇\" :children '()"
                + " :arrivals (list (arrival \"机器响了\""
                + "   (lambda ()"
                + "     (play-dialogue! (line \"世界\" \"里面有东西在响。\"))"
                + "     (start-encounter \"失控的机械\")))))");
            sceneManager.CurrentRootNode!.Children.Add(forcedEncounter);
            int sceneLoadedEvents = 0;
            sceneManager.OnSceneLoaded += () => sceneLoadedEvents++;
            var arrivalReport = sceneManager.EnterPlace("入场DSL校验-强制遭遇");
            if (arrivalReport == null || sceneManager.CurrentSceneName != "失控的机械")
                throw new InvalidDataException("arrival 没有准备好强制遭遇及其入场报告。");
            if (sceneLoadedEvents != 0)
                throw new InvalidDataException("arrival 强制遭遇在报告播完前发送了场景加载事件。");
            sceneManager.LoadScene("world");

            Console.WriteLine("[validate] place / arrival DSL contract passed.");
        }

        // 地点开放通知是世界根 Place 集合的结构契约：首次建树只建基线，
        // 新地点第一次出现时通知，纯重建不重复。诊所正好提供一个稳定的条件切换。
        private static void AssertPlaceUnlockNotifications(SceneManager sceneManager)
        {
            var notifications = sceneManager.GameState.NotificationCenter;
            if (notifications.GetVisible().Count != 0)
                throw new InvalidDataException("世界首次建树不应把初始地点报告为新开放。");

            sceneManager.GameState.Team.Injure(1);
            sceneManager.RebuildRenderTree();

            var firstRefresh = notifications.GetVisible();
            if (firstRefresh.Count != 1 || firstRefresh[0].Text != "新地点开放：诊所")
                throw new InvalidDataException("诊所首次出现时没有产生唯一的地点开放通知。");

            sceneManager.RebuildRenderTree();
            if (notifications.GetVisible().Count != 1)
                throw new InvalidDataException("纯重建重复发送了地点开放通知。");

            notifications.Clear();
            Console.WriteLine("[validate] place unlock notification contract passed.");
        }

        // 只验证稳定的存档契约：纯全局值、类型化资源、主角状态、动态同伴及当天剩余骰池。
        public static void TestSaveLoad()
        {
            Console.WriteLine("=== Save/Load Contract Test ===");
            var savePath = Path.Combine(Path.GetTempPath(), "ssnoir_test_save.json");

            try
            {
                var source = new GameState();
                var sourceManager = new SceneManager(source, new LocalScriptLoader());
                sourceManager.LoadScene("world");

                source.Set("test-global", "persisted");
                source.Inventory.SetCount("测试物品", 7);
                source.Team.Injure(2);
                source.Team.GrowthLevel = 3;
                source.Team.ApplyHangover();
                source.Team.SpendComposure("player", 1);
                var companion = source.Team.RecruitCompanion(
                    "test-companion",
                    "测试同伴",
                    new Dictionary<string, int>
                    {
                        ["violence"] = 1,
                        ["knowledge"] = 2,
                        ["sharpness"] = 0,
                        ["social"] = 1,
                    });
                // 只花 1 点：花到 0 会让同伴离场，那是另一条契约，别和存档往返混在一起。
                source.Team.SpendComposure(companion.Id, 1);
                companion.PermanentDiePenaltyLabel = "残疾";
                companion.PermanentDiePenalty = -1;
                var player = source.Team.FindActor("player")!;
                player.ActionDice.Clear();
                player.ActionDiceSlotIds.Clear();
                player.ActionDice.AddRange(new[] { 6, 2 });
                player.ActionDiceSlotIds.AddRange(new[] { 0, 3 });
                // 空表是重要状态：同伴的骰子已经用完，读档不能凭空补一颗。
                companion.ActionDice.Clear();
                companion.ActionDiceSlotIds.Clear();
                sourceManager.SaveGame(savePath);

                var loaded = new GameState();
                var loadedManager = new SceneManager(loaded, new LocalScriptLoader());
                loadedManager.LoadGame(savePath);

                AssertEq("pure global", "persisted", loaded.Get<string>("test-global"));
                AssertEq("inventory", 7, loaded.Inventory.GetCount("测试物品"));
                AssertEq("injury severity", 2, loaded.Team.Injury.Severity);
                AssertEq("injury part", source.Team.Injury.Part, loaded.Team.Injury.Part);
                AssertEq("injury skill", source.Team.Injury.Skill, loaded.Team.Injury.Skill);
                AssertEq("growth", 3, loaded.Team.GrowthLevel);
                AssertEq<int?>("hangover slot", 0, loaded.Team.FindActor("player")!.HangoverSlotId);
                AssertEq("player composure", TeamState.MaxComposure - 1, loaded.Team.FindActor("player")!.Composure);

                var loadedCompanion = loaded.Team.FindActor("test-companion")
                    ?? throw new Exception("[saveload] companion was not recreated during cold load");
                AssertEq("companion role", "companion", loadedCompanion.Role);
                AssertEq("companion name", "测试同伴", loadedCompanion.Name);
                AssertEq("companion max composure", 2, loadedCompanion.MaxComposure);
                AssertEq("companion composure", 1, loadedCompanion.Composure);
                AssertEq("companion knowledge", 2, loadedCompanion.Stats["knowledge"]);
                AssertEq("companion permanent penalty label", "残疾", loadedCompanion.PermanentDiePenaltyLabel);
                AssertEq("companion permanent penalty", -1, loadedCompanion.PermanentDiePenalty);
                AssertEq("companion permanent status count", 1, loaded.Team.GetActiveActionSlotStatuses(loadedCompanion).Count);
                var loadedPlayer = loaded.Team.FindActor("player")!;
                AssertEq("player dice", "6,2", string.Join(",", loadedPlayer.ActionDice));
                AssertEq("player die slots", "0,3", string.Join(",", loadedPlayer.ActionDiceSlotIds));
                AssertEq("spent companion dice", 0, loadedCompanion.ActionDice.Count);
                AssertEq("spent companion die slots", 0, loadedCompanion.ActionDiceSlotIds.Count);

                // 交锋有自己的骰池；返回城市时，当天尚未使用的骰值、骰位和顺序必须原样回来。
                loadedPlayer.ActionDice.Clear();
                loadedPlayer.ActionDiceSlotIds.Clear();
                loadedPlayer.ActionDice.AddRange(new[] { 3, 6, 3, 6 });
                loadedPlayer.ActionDiceSlotIds.AddRange(new[] { 0, 1, 2, 3 });
                loadedManager.StartEncounter("失控的机械");
                loadedManager.EndEncounter();
                AssertEq("world dice after encounter", "3,6,3,6",
                    string.Join(",", loadedPlayer.ActionDice));
                AssertEq("world die slots after encounter", "0,1,2,3",
                    string.Join(",", loadedPlayer.ActionDiceSlotIds));

                var growthState = new GameState();
                growthState.Team.GrowthLevel = 3;
                var growthPlayer = growthState.Team.FindActor("player")!;
                AssertEq("growth cost level 0", 1, TeamState.GetStatUpgradeCost(0));
                AssertEq("growth cost level 1", 2, TeamState.GetStatUpgradeCost(1));
                AssertEq("growth cost level 2", 3, TeamState.GetStatUpgradeCost(2));
                AssertEq("growth cost level 3", 4, TeamState.GetStatUpgradeCost(3));
                AssertEq("growth cost at maximum", 0, TeamState.GetStatUpgradeCost(4));
                growthState.Team.UpgradeActorStat("player", "knowledge");
                AssertEq("growth upgraded level", 2, growthPlayer.Stats["knowledge"]);
                AssertEq("growth spent escalating cost", 2, growthPlayer.SpentGrowthPoints);
                AssertEq("growth remaining points", 1, growthState.Team.GetAvailableGrowthPoints(growthPlayer));

                Console.WriteLine("[saveload] All contract assertions passed.");
            }
            finally
            {
                if (File.Exists(savePath)) File.Delete(savePath);
            }
        }

        // 判定概率是稳定的底层数学契约，算错会同时破坏结算和 UI 概率预览。
        public static void TestFateStrip()
        {
            Console.WriteLine("=== Fate Strip Contract Test ===");

            static (int fail, int neutral, int success) ExpectedCounts(int prepared)
            {
                if (prepared <= 1) return (3, 3, 0);
                return prepared switch
                {
                    2 => (2, 3, 1),
                    3 => (1, 3, 2),
                    4 => (1, 2, 3),
                    5 => (0, 2, 4),
                    6 => (0, 1, 5),
                    _ => (0, 0, 6), // ≥7
                };
            }

            for (int prepared = -2; prepared <= 10; prepared++)
            {
                int mod = prepared - 4; // placed die 4 + skill 0 + mod = prepared
                var strip = FateStrip.Compute(placedDie: 4, skill: 0, modSum: mod);
                int fail = 0, neutral = 0, success = 0;
                for (int fate = 1; fate <= 6; fate++)
                {
                    AssertEq($"B={prepared} face={fate} resolve", strip[fate - 1],
                        FateStrip.Resolve(4, 0, mod, fate));
                    if (strip[fate - 1] == RollOutcome.Fail) fail++;
                    else if (strip[fate - 1] == RollOutcome.Neutral) neutral++;
                    else success++;
                }
                AssertEq($"B={prepared} counts", ExpectedCounts(prepared), (fail, neutral, success));
            }

            // 冷静击穿溢出是主角唯一的第二受伤口来源，容易在重构中静默改坏，值得覆盖。
            // 一比一：垫子用完，代价照原价打在身上。
            var overflowState = new GameState();
            overflowState.Team.SpendComposure("player", TeamState.MaxComposure + 4);
            AssertEq("composure floor", 0, overflowState.Team.FindActor("player")!.Composure);
            AssertEq("composure overflow injury", 4, overflowState.Team.Injury.Severity);

            // 内容脚本必须走同一条击穿路径，不能在 0 点被 wrapper 静默截断。
            var scriptedOverflowState = new GameState();
            var scriptedOverflowManager = new SceneManager(scriptedOverflowState, new LocalScriptLoader());
            scriptedOverflowManager.LoadScene("world");
            // 先精确花到 0（不溢出），再花 1 点——这一点必须变成伤势，不能被 wrapper 静默吞掉。
            scriptedOverflowManager.ActiveInterpreter.Eval(
                $"(spend-actor-composure! 'player {TeamState.MaxComposure})");
            scriptedOverflowManager.ActiveInterpreter.Eval("(spend-actor-composure! 'player 1)");
            AssertEq("scripted composure floor", 0, scriptedOverflowState.Team.FindActor("player")!.Composure);
            AssertEq("scripted composure overflow injury", 1, scriptedOverflowState.Team.Injury.Severity);

            // 轻伤和重伤是两种不叠加的代价：轻伤压对应能力，重伤改为封一颗骰。
            var injuryBandState = new GameState();
            injuryBandState.Team.Injure(1);
            AssertEq("light injury skill penalty", -1, injuryBandState.Team.Injury.SkillPenalty);
            AssertEq("light injury keeps action dice", false, injuryBandState.Team.Injury.CostsActionDie);
            injuryBandState.Team.Injure(Injury.SevereThreshold - 1);
            AssertEq("severe injury removes skill penalty", 0, injuryBandState.Team.Injury.SkillPenalty);
            AssertEq("severe injury costs action die", true, injuryBandState.Team.Injury.CostsActionDie);

            // 谷底仍须保留三颗骰：重伤减一颗，冷静见底本身不再征用或降质任何骰子。
            var bottomState = new GameState();
            bottomState.Team.Injure(Injury.SevereThreshold);
            bottomState.Team.SpendComposure("player", TeamState.MaxComposure);
            bottomState.Team.RollActionDice(isInEncounter: false);
            AssertEq("bottom-state dice", 3, bottomState.Team.FindActor("player")!.ActionDice.Count);

            // 伤势刻度的终点是立即结算线，不是“再挨一次才倒下”的预告线。
            var collapseState = new GameState();
            collapseState.Inventory.SetCount("金钱", GameState.CollapseTreatmentFee);
            collapseState.Team.Injure(Injury.MaxSeverity);
            AssertEq("injury collapse at threshold", Injury.PostCollapseSeverity, collapseState.Team.Injury.Severity);
            AssertEq("collapse resets composure", 0, collapseState.Team.FindActor("player")!.Composure);
            AssertEq("collapse leaves permanent scar", 1, collapseState.Team.Scars.Count);
            AssertEq("injury collapse treatment fee", 0, collapseState.Inventory.GetCount("金钱"));
            AssertEq("collapse waits for scene boundary", true, collapseState.HasPendingHospitalization);
            AssertEq("collapse has no premature global spotlight", false, collapseState.SpotlightCenter.HasSpotlight);
            AssertEq("collapse notification removed", 0, collapseState.NotificationCenter.GetVisible().Count);

            // 动作中倒下也只留下待送医记录。交锋结果、日终和诊所位置必须由 SceneManager
            // 在动作边界一次性完成，GameState 不能抢先把 Spotlight 插进队列。
            var actionCollapseState = new GameState();
            actionCollapseState.Inventory.SetCount("金钱", GameState.CollapseTreatmentFee);
            actionCollapseState.CurrentActionReport = new ActionReport();
            actionCollapseState.Team.Injure(Injury.MaxSeverity);
            AssertEq("collapse action story steps deferred", 0,
                actionCollapseState.CurrentActionReport.BlockingStorySteps.Count);
            AssertEq("collapse action hospitalization pending", true,
                actionCollapseState.HasPendingHospitalization);
            AssertEq("collapse action global spotlight", false, actionCollapseState.SpotlightCenter.HasSpotlight);

            // 脚本不能在已经造成倒下之后抢先正常结束交锋。否则场景先切回 world，
            // 动作边界再处理住院时已经找不到交锋的 on-encounter-collapse，收场结果会串线。
            var prematureExitState = new GameState();
            prematureExitState.Inventory.SetCount("金钱", GameState.CollapseTreatmentFee);
            var prematureExitManager = new SceneManager(prematureExitState, new LocalScriptLoader());
            prematureExitManager.LoadScene("核赔");
            prematureExitState.Team.Injure(Injury.MaxSeverity);
            AssertThrowsAny(
                () => prematureExitManager.ActiveInterpreter.Eval("(end-encounter 'confirmed)"),
                "end encounter while hospitalization is pending");
            AssertEq("premature end keeps encounter active", "核赔", prematureExitManager.CurrentSceneName);

            // 交锋中倒下：核赔声明为可重试，所以不调用城市回调；但仍结束当天、发放新日骰池，
            // 并按 EnterPlace → Spotlight 的顺序要求客户端在诊所醒来。
            var encounterCollapseState = new GameState();
            encounterCollapseState.Inventory.SetCount("金钱", GameState.CollapseTreatmentFee);
            var encounterCollapseManager = new SceneManager(encounterCollapseState, new LocalScriptLoader());
            encounterCollapseManager.LoadScene("world");
            encounterCollapseManager.LoadScene("核赔");
            encounterCollapseState.Team.Injure(Injury.MaxSeverity - 1);
            encounterCollapseState.Team.SpendComposure("player", TeamState.MaxComposure);
            var hospitalizationReport = encounterCollapseManager.EndTurn();
            AssertEq("collapse exits encounter", "world", encounterCollapseManager.CurrentSceneName);
            AssertEq("collapse retry result marker", "倒下",
                SchemeValue.AsId(encounterCollapseManager.LastEncounterResult));
            AssertEq("collapse advances world day", 2, encounterCollapseState.Get<int>("世界日"));
            AssertEq("collapse pending consumed", false, encounterCollapseState.HasPendingHospitalization);
            AssertEq("collapse wakes with city dice", true,
                encounterCollapseState.Team.FindActor("player")!.ActionDice.Count > 0);
            int enterPlaceIndex = hospitalizationReport.BlockingStorySteps.FindIndex(
                step => step.Kind == BlockingStoryStepKind.EnterPlace && step.PlaceName == "诊所");
            int collapseSpotlightIndex = hospitalizationReport.BlockingStorySteps.FindIndex(
                step => step.Kind == BlockingStoryStepKind.Spotlight && step.Spotlight?.Title == "你倒下了");
            AssertEq("collapse report enters clinic", true, enterPlaceIndex >= 0);
            AssertEq("collapse spotlight follows clinic", true, collapseSpotlightIndex > enterPlaceIndex);

            var hangoverState = new GameState();
            hangoverState.Team.ApplyHangover();
            hangoverState.Team.RollActionDice(isInEncounter: false, consumeHangover: false);
            AssertEq<int?>("hangover survives scene roll", 0, hangoverState.Team.FindActor("player")!.HangoverSlotId);
            hangoverState.Team.RollActionDice(isInEncounter: false);
            AssertEq<int?>("hangover consumed on day end", null, hangoverState.Team.FindActor("player")!.HangoverSlotId);

            // 随身动作（烟、酒）不由交锋脚本声明，是 SceneManager 补进每一场交锋的树的。
            // 它们走的是和别的卡完全同一条路径：骰位 + 物品位 + ExecuteAction。
            // 这里守三件事——这一场真的有它、它认得自己属于哪件物品、手里没那件东西时它不出现。
            var consumableState = new GameState();
            consumableState.Inventory.SetCount("香烟", 1);
            consumableState.Inventory.SetCount("酒", 0);
            consumableState.Team.SpendComposure("player", TeamState.MaxComposure);
            var consumableManager = new SceneManager(consumableState, new LocalScriptLoader());
            // 任意一场活的交锋都行，这里只需要一个已载入的交锋上下文。
            consumableManager.LoadScene("encounters/巷子里在打人");
            var smokeNode = consumableManager.CurrentCarryNodes.FirstOrDefault(n => n.Name == "抽烟")
                ?? throw new Exception("[saveload] 这一场没有随身动作「抽烟」");
            AssertEq("carry node names its item", "香烟", smokeNode.CarryItemId);
            // 随身卡不进渲染树：进了树就会被排进场上的卡片区，读起来像是这一场的事。
            AssertEq("carry node stays out of the scene tree", true,
                consumableManager.CurrentRootNode!.Children.TrueForAll(n => n.Name != "抽烟"));
            AssertEq("carry node hidden without item", true,
                consumableManager.CurrentCarryNodes.All(n => n.Name != "喝酒"));
            var consumablePlayer = consumableState.Team.FindActor("player")!;
            int diceBeforeSmoke = consumablePlayer.ActionDice.Count;
            consumableManager.ExecuteAction(smokeNode, new List<SlottedResource?>
            {
                new SlottedResource
                {
                    Type = "die", Value = consumablePlayer.ActionDice[0],
                    ActorId = "player", DieIndex = consumablePlayer.ActionDiceSlotIds[0]
                },
            });
            AssertEq("smoke consumed", 0, consumableState.Inventory.GetCount("香烟"));
            // 交锋每回合流失 2，一根烟正好换回一个回合。
            AssertEq("smoke composure restore", 2, consumableState.Team.FindActor("player")!.Composure);
            // 它要投一颗行动骰：角落里那种一按就生效的按钮已经不存在了。
            AssertEq("smoke spends an action die", diceBeforeSmoke - 1, consumablePlayer.ActionDice.Count);
            AssertEq("B=1 summary", "1–3 坏 · 4–6 中", FateStrip.Describe(FateStrip.Compute(1, 0, 0)));
            AssertEq("B=4 summary", "1 坏 · 2–3 中 · 4–6 好", FateStrip.Describe(FateStrip.Compute(4, 0, 0)));
            AssertEq("B=7 summary", "1–6 好", FateStrip.Describe(FateStrip.Compute(6, 1, 0)));

            AssertThrows(() => FateStrip.Compute(0, 0, 0), "invalid placed die");
            AssertThrows(() => FateStrip.Compute(1, -2, 0), "skill below minimum");
            AssertThrows(() => FateStrip.Resolve(1, 0, 0, 7), "invalid fate die");

            Console.WriteLine("[fate-strip] All contract assertions passed.");
        }

        // 只要求「拦下来了」：具体异常类型会被 Scheme 求值层包一层，钉死类型只会让
        // 测试跟着实现细节走。
        private static void AssertThrowsAny(Action action, string label)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                return;
            }
            throw new Exception($"[assert] FAIL: {label} did not throw");
        }

        private static void AssertThrows(Action action, string label)
        {
            try
            {
                action();
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }
            throw new Exception($"[assert] FAIL: {label} did not throw ArgumentOutOfRangeException");
        }

        private static void AssertEq<T>(string label, T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"[saveload] FAIL: {label} expected={expected} actual={actual}");
            Console.WriteLine($"[saveload] OK: {label} = {actual}");
        }

        private static void AssertParenBalance(string filePath)
        {
            string content = File.ReadAllText(filePath);
            string fileName = Path.GetFileName(filePath);
            string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var stack = new Stack<(int line, int col)>();
            bool inString = false;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                bool escape = false;
                for (int columnIndex = 0; columnIndex < line.Length; columnIndex++)
                {
                    char c = line[columnIndex];
                    if (inString)
                    {
                        if (escape) { escape = false; continue; }
                        if (c == '\\') { escape = true; continue; }
                        if (c == '"') inString = false;
                        continue;
                    }
                    if (c == ';') break;
                    if (c == '"') { inString = true; continue; }
                    if (c == '(') stack.Push((lineIndex + 1, columnIndex + 1));
                    else if (c == ')')
                    {
                        if (stack.Count == 0)
                            throw new InvalidDataException(
                                $"括号不平衡 [{fileName}] 第 {lineIndex + 1} 行第 {columnIndex + 1} 列：多余的 ')'\n  {line.Trim()}");
                        stack.Pop();
                    }
                }
            }

            if (stack.Count > 0)
            {
                var (line, column) = stack.Pop();
                throw new InvalidDataException(
                    $"括号不平衡 [{fileName}] 第 {line} 行第 {column} 列：'(' 未关闭\n  {lines[line - 1].Trim()}");
            }
        }

        private static void CheckParenthesesDiagnostics(string filePath)
        {
            if (!File.Exists(filePath)) return;
            try
            {
                AssertParenBalance(filePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DIAGNOSTICS] {ex.Message}");
            }
        }
    }
}
