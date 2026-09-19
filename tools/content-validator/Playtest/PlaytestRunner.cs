#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SSNoir.Core;

namespace SSNoir.Playtest
{
    /// <summary>
    /// 命令行试跑：把一场交锋跑到结算，打出逐回合流水与最终结果。
    ///
    ///   ./run --playtest "(dock-collapse 'debug-enter!)" --runs 20 --growth 1 --seed 7
    ///   ./run --playtest "(dock-collapse 'debug-enter!)" --verbose        # 逐回合看牌面
    ///
    /// 这里的默认打法是**基线不是玩家**：每颗骰投给准备值最高的那张卡，从不浪费、
    /// 也从不做错误的分诊。它衡量的是这一场的天花板，用来看"改了数值以后上限动了多少"，
    /// 不用来回答"这场难不难"——那个问题只有真人坐下来玩才算数。
    /// </summary>
    public static class PlaytestRunner
    {
        public static void Run(string[] args)
        {
            string entry = args.Length > 1 && !args[1].StartsWith("--")
                ? args[1]
                : throw new ArgumentException("用法：--playtest <入场表达式或场景名> [--runs N] [--growth G] [--seed S] [--verbose]");

            int runs = IntArg(args, "--runs", 1);
            int growth = IntArg(args, "--growth", 1);
            int? seed = HasArg(args, "--seed") ? IntArg(args, "--seed", 0) : null;
            bool verbose = HasArg(args, "--verbose");
            var items = new Dictionary<string, int> { { "香烟", IntArg(args, "--cigarettes", 3) } };

            for (int run = 0; run < runs; run++)
            {
                var setup = new PlaytestSetup
                {
                    Growth = growth,
                    Items = items,
                    Seed = seed == null ? null : seed.Value + run
                };
                PlayOnce(entry, setup, verbose, run);
            }
        }

        private static void PlayOnce(string entry, PlaytestSetup setup, bool verbose, int run)
        {
            var driver = EncounterDriver.Enter(entry, setup);
            var log = new StringBuilder();

            while (!driver.Finished && driver.Turn <= 20)
            {
                if (verbose)
                {
                    Console.WriteLine($"───────── 第 {driver.Turn} 回合 ─────────");
                    Console.WriteLine(driver.Observe());
                }

                // 抽烟不再有专门的入口：它是交锋树上的普通动作卡（engine.scm 的 carry-nodes），
                // 会跟着别的卡一起出现在 LegalMoves 里，驱动照常投骰执行。
                // 它吃骰，所以基线打法按准备值排序时会自动把它排在后面——那和真人的分诊一致。

                while (!driver.Finished)
                {
                    var moves = driver.LegalMoves();
                    if (moves.Count == 0) break;
                    // 不吃骰的卡先用掉，其余按准备值取最高。
                    var move = moves.Where(m => !m.NeedsDie).FirstOrDefault()
                               ?? moves.OrderByDescending(m => m.Prepared).First();
                    string label = move.Describe(driver.State);
                    Report(log, label, driver.Play(move), verbose);
                }

                if (driver.Finished) break;
                Report(log, $"结束第 {driver.Turn} 回合", driver.EndTurn(), verbose);
            }

            if (!verbose) Console.Write(log);
            Console.WriteLine($"[试跑 {run + 1}] 成长{setup.Growth} 种子{(setup.Seed?.ToString() ?? "随机")}"
                + $" → 结果 {Format(driver.Result)}"
                + $"；打了 {driver.Turn - 1} 个回合"
                + $"；主角冷静 {driver.State.Team.FindActor("player")!.Composure}"
                + $"，伤势 {driver.State.Team.Injury.Severity}"
                + $"，队伍 {string.Join("/", driver.State.Team.Actors.Select(a => a.Name))}");
            // 交锋临时请来的人必须已经离队，否则这里会当场抛错。
            driver.State.Team.Serialize();
        }

        /// <summary>把一次行动的效果条、结果标题、对白与告示卡打成一行流水。</summary>
        private static void Report(StringBuilder log, string label, ActionReport report, bool verbose)
        {
            var parts = new List<string>();
            if (report.Type == ActionType.Roll)
                parts.Add(report.Outcome switch
                {
                    RollOutcome.Success => "好",
                    RollOutcome.Neutral => "中",
                    _ => "坏"
                });
            foreach (var effect in report.Effects)
                parts.Add(effect.Kind == ActionEffectKind.Supplement
                    ? effect.Text
                    : $"{effect.Label ?? effect.Text} {effect.Delta:+#;-#;0}");
            foreach (var step in report.BlockingStorySteps)
            {
                if (step.Spotlight != null) parts.Add($"【{step.Spotlight.Title}】");
                if (step.Dialogue != null)
                    parts.Add(string.Join(" / ", step.Dialogue.Lines.Select(l => $"{l.Speaker}：{l.Text}")));
            }
            foreach (var banter in report.Banter)
                parts.Add(string.Join(" / ", banter.Lines.Select(l => $"{l.Speaker}：{l.Text}")));

            string line = $"  {label}" + (parts.Count > 0 ? "　→　" + string.Join("；", parts) : "");
            if (verbose) Console.WriteLine(line); else log.AppendLine(line);
        }

        private static string Format(object? result)
        {
            if (result == null) return "（无）";
            if (result is List<object> list) return "(" + string.Join(" ", list.Select(Format)) + ")";
            return result.ToString() ?? "";
        }

        private static bool HasArg(string[] args, string name) => Array.IndexOf(args, name) >= 0;

        private static int IntArg(string[] args, string name, int fallback)
        {
            int i = Array.IndexOf(args, name);
            if (i < 0 || i + 1 >= args.Length) return fallback;
            return int.Parse(args[i + 1]);
        }
    }
}
