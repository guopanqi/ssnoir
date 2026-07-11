using System;
using System.Collections.Generic;
using System.IO;
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
            var scenesDir = Path.GetFullPath("scenes");
            if (!Directory.Exists(scenesDir))
                scenesDir = Path.GetFullPath(Path.Combine("..", "Content", "scenes"));
            if (!Directory.Exists(scenesDir))
                scenesDir = Path.GetFullPath(Path.Combine("Content", "scenes"));
            if (!Directory.Exists(scenesDir))
                throw new DirectoryNotFoundException("Scenes directory not found under standard content paths.");

            Console.WriteLine("[validate] Phase 1: paren balance...");
            foreach (var scmFile in Directory.GetFiles(scenesDir, "*.scm", SearchOption.AllDirectories))
                AssertParenBalance(scmFile);
            Console.WriteLine("[validate] Phase 1: all files balanced.");

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

                Console.WriteLine($"Validated scene '{sceneName}' with root '{sceneManager.CurrentRootNode.Name}'.");
            }
        }

        // 只验证稳定的存档契约：纯全局值、类型化资源、主角状态、动态同伴及读档后重掷骰。
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
                source.Team.Health = 4;
                source.Team.Satiety = 2;
                source.Team.GrowthLevel = 3;
                source.Team.ApplyStress("player", 2);
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
                source.Team.ApplyStress(companion.Id, 2);
                sourceManager.SaveGame(savePath);

                var loaded = new GameState();
                var loadedManager = new SceneManager(loaded, new LocalScriptLoader());
                loadedManager.LoadGame(savePath);

                AssertEq("pure global", "persisted", loaded.Get<string>("test-global"));
                AssertEq("inventory", 7, loaded.Inventory.GetCount("测试物品"));
                AssertEq("health", 4, loaded.Team.Health);
                AssertEq("satiety", 2, loaded.Team.Satiety);
                AssertEq("growth", 3, loaded.Team.GrowthLevel);
                AssertEq("player stress", 2, loaded.Team.FindActor("player")!.Stress);

                var loadedCompanion = loaded.Team.FindActor("test-companion")
                    ?? throw new Exception("[saveload] companion was not recreated during cold load");
                AssertEq("companion role", "companion", loadedCompanion.Role);
                AssertEq("companion name", "测试同伴", loadedCompanion.Name);
                AssertEq("companion stress", 2, loadedCompanion.Stress);
                AssertEq("companion knowledge", 2, loadedCompanion.Stats["knowledge"]);
                AssertEq("player dice", 3, loaded.Team.FindActor("player")!.ActionDice.Count);
                AssertEq("companion dice", 1, loadedCompanion.ActionDice.Count);

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
                if (prepared <= -1) return (6, 0, 0);
                if (prepared <= 1) return (5, 1, 0);
                return prepared switch
                {
                    2 => (4, 1, 1),
                    3 => (3, 2, 1),
                    4 => (2, 2, 2),
                    5 => (1, 2, 3),
                    6 => (1, 1, 4),
                    7 or 8 => (0, 1, 5),
                    _ => (0, 0, 6),
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

            AssertEq("natural 1 modifier", -1, FateStrip.NaturalModifier(1));
            AssertEq("natural 6 modifier", 1, FateStrip.NaturalModifier(6));
            AssertEq("stress 0 modifier", 0, TeamState.GetStressRollModifier(0));
            AssertEq("stress 1 modifier", 0, TeamState.GetStressRollModifier(1));
            AssertEq("stress 2 modifier", -1, TeamState.GetStressRollModifier(2));
            AssertEq("stress 4 modifier", -1, TeamState.GetStressRollModifier(4));
            var stressedState = new GameState();
            stressedState.Team.ApplyStress("player", 5);
            AssertEq("stress cap", 4, stressedState.Team.FindActor("player")!.Stress);
            AssertEq("stress overflow health damage", 4, stressedState.Team.Health);
            AssertEq("B=1 summary", "1–5 坏 · 6 中", FateStrip.Describe(FateStrip.Compute(1, 0, 0)));
            AssertEq("B=4 summary", "1–2 坏 · 3–4 中 · 5–6 好", FateStrip.Describe(FateStrip.Compute(4, 0, 0)));
            AssertEq("B=7 summary", "1 中 · 2–6 好", FateStrip.Describe(FateStrip.Compute(6, 1, 0)));

            AssertThrows(() => FateStrip.Compute(0, 0, 0), "invalid placed die");
            AssertThrows(() => FateStrip.Compute(1, -1, 0), "negative skill");
            AssertThrows(() => FateStrip.Resolve(1, 0, 0, 7), "invalid fate die");
            AssertThrows(() => TeamState.GetStressRollModifier(5), "invalid stress");

            Console.WriteLine("[fate-strip] All contract assertions passed.");
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
