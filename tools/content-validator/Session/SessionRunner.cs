#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SSNoir.Session;

/// <summary>一行一条 JSON 消息；stdin 命令，stdout 结果，stderr 诊断。</summary>
public static class SessionRunner
{
    private static SessionSetup ParseSetup(string[] args)
    {
        if (args.Length < 2)
            throw new ArgumentException("用法：--session <world|场景名|入场表达式> [--seed N] [--growth N] [--item 名称=数量]");
        var items = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] != "--item") continue;
            if (++i >= args.Length) throw new ArgumentException("--item 缺少 名称=数量");
            var parts = args[i].Split('=', 2);
            if (parts.Length != 2) throw new ArgumentException("--item 格式须为 名称=数量");
            items.Add(parts[0], int.Parse(parts[1]));
        }
        int seed = IntArg(args, "--seed", 7);
        return new SessionSetup(args[1], seed, IntArg(args, "--growth", 1), items);
    }

    public static void Run(string[] args)
    {
        var setup = ParseSetup(args);
        var session = PlaySession.Start(setup);
        var transcript = new SessionTranscript { Setup = setup, ContentRevision = Revision(),
            OpeningEvents = session.OpeningEvents.ToList() };
        Write(new { type = "observation", events = session.OpeningEvents,
            observation = session.Observe() });

        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            try
            {
                var command = JObject.Parse(line);
                switch ((string?)command["command"])
                {
                    case "observe":
                        Write(new { type = "observation", observation = session.Observe() });
                        break;
                    case "act":
                    {
                        long version = command.Value<long>("version");
                        string id = command.Value<string>("operationId")
                            ?? throw new ArgumentException("缺少 operationId");
                        var before = session.Observe();
                        var (after, events) = session.Act(version, id);
                        transcript.Steps.Add(MakeStep(version, id,
                            command.Value<string>("reason"), before, after, events));
                        Write(new { type = "result", events, observation = after });
                        break;
                    }
                    case "save":
                    {
                        string path = command.Value<string>("path")
                            ?? throw new ArgumentException("缺少 path");
                        File.WriteAllText(path, JsonConvert.SerializeObject(transcript, Formatting.Indented));
                        Write(new { type = "saved", path, steps = transcript.Steps.Count });
                        break;
                    }
                    case "quit":
                        Write(new { type = "bye" });
                        return;
                    default:
                        throw new ArgumentException("命令须为 observe、act、save 或 quit");
                }
            }
            catch (Exception ex)
            {
                Write(new { type = "error", message = ex.Message });
            }
        }
    }

    /// <summary>同一会话协议上的确定性参考策略；其选择质量不代表真人。</summary>
    public static void Baseline(string[] args)
    {
        var setup = ParseSetup(args);
        if (setup.Entry == "world")
            throw new ArgumentException("基线策略需要交锋入口。");
        int outputIndex = Array.IndexOf(args, "--output");
        if (outputIndex < 0 || outputIndex + 1 >= args.Length)
            throw new ArgumentException("基线策略需要 --output <记录.json>");
        string output = args[outputIndex + 1];
        var session = PlaySession.Start(setup);
        var transcript = new SessionTranscript { Setup = setup, ContentRevision = Revision(),
            OpeningEvents = session.OpeningEvents.ToList() };
        string initialScene = session.Observe().Scene;
        var explored = new HashSet<string>(StringComparer.Ordinal);
        int turns = 0;
        for (int i = 0; i < 300 && session.Observe().Scene == initialScene; i++)
        {
            var before = session.Observe();
            var player = before.Actors.FirstOrDefault(a => a.Id == "player");
            var actions = before.Operations.Where(o => o.Kind == "action"
                && (o.Card switch
                {
                    "抽烟" => player != null && player.MaxComposure - player.Composure >= 2,
                    "喝酒" => player != null && player.MaxComposure - player.Composure >= 3,
                    _ => true,
                })).ToList();
            SessionOperation? choice = actions.FirstOrDefault(o => o.DieValue == null)
                ?? actions.OrderByDescending(o => o.Prepared ?? o.DieValue ?? int.MinValue).FirstOrDefault();
            if (choice == null)
            {
                choice = before.Operations.FirstOrDefault(o => o.Kind == "navigate" && explored.Add(o.Label))
                    ?? before.Operations.FirstOrDefault(o => o.Kind == "back")
                    ?? before.Operations.FirstOrDefault(o => o.Kind == "end-turn");
            }
            if (choice == null) throw new InvalidOperationException("基线遇到没有合法操作的场面。");
            if (choice.Kind == "end-turn")
            {
                turns++;
                explored.Clear();
                if (turns > 20) break;
            }
            var (after, events) = session.Act(before.Version, choice.Id);
            transcript.Steps.Add(MakeStep(before.Version, choice.Id, "确定性基线", before, after, events));
        }
        if (session.Observe().Scene == initialScene && transcript.Steps.Count >= 300)
            throw new InvalidOperationException("基线达到 300 步仍未终局；疑似重复操作，拒绝生成有效记录。");
        transcript.StopReason = session.Observe().Scene == initialScene ? "回合上限" : "离开交锋";
        File.WriteAllText(output, JsonConvert.SerializeObject(transcript, Formatting.Indented));
        Console.WriteLine($"基线记录：{output}；{transcript.Steps.Count} 步，{turns} 次结束回合");
    }

    public static void Report(string[] paths)
    {
        if (paths.Length == 0) throw new ArgumentException("用法：--session-report <记录.json> [...]");
        foreach (string path in paths)
        {
            var transcript = JsonConvert.DeserializeObject<SessionTranscript>(File.ReadAllText(path))
                ?? throw new InvalidDataException($"空会话记录：{path}");
            var steps = transcript.Steps;
            var first = steps.FirstOrDefault()?.Before;
            var last = steps.LastOrDefault()?.After;
            Write(new
            {
                path, transcript.Setup.Entry, transcript.Setup.Seed, transcript.StopReason,
                openingEvents = transcript.OpeningEvents,
                steps = steps.Count,
                actions = steps.Count(s => s.Before.Operations.Any(o => o.Id == s.OperationId && o.Kind == "action")),
                turns = steps.Count(s => s.Before.Operations.Any(o => o.Id == s.OperationId && o.Kind == "end-turn")),
                startScene = first?.Scene, endScene = last?.Scene,
                result = last?.EncounterResult,
                startInventory = first?.Inventory, endInventory = last?.Inventory,
                decisions = steps.Where(s => s.Reason != null).Select(s => new
                {
                    s.Version, s.Reason,
                    choice = s.Before.Operations.First(o => o.Id == s.OperationId).Label,
                    feedback = s.Events.Select(e => e.Text)
                })
            });
        }
    }

    private static SessionStep MakeStep(long version, string id, string? reason,
        SessionObservation before, SessionObservation after, IReadOnlyList<SessionEvent> events)
        => new(version, id, reason, PlaySession.Hash(before), PlaySession.Hash(after),
            before, after, events);

    public static void Replay(string path)
    {
        var transcript = JsonConvert.DeserializeObject<SessionTranscript>(File.ReadAllText(path))
            ?? throw new InvalidDataException("空会话记录");
        string revision = Revision();
        if (revision != transcript.ContentRevision)
            throw new InvalidOperationException($"内容/代码版本不一致：记录 {transcript.ContentRevision}，当前 {revision}");
        var session = PlaySession.Start(transcript.Setup);
        if (JsonConvert.SerializeObject(session.OpeningEvents) !=
            JsonConvert.SerializeObject(transcript.OpeningEvents))
            throw new InvalidOperationException("入场反馈与记录不同");
        for (int i = 0; i < transcript.Steps.Count; i++)
        {
            var step = transcript.Steps[i];
            string before = PlaySession.Hash(session.Observe());
            if (before != step.BeforeHash || before != PlaySession.Hash(step.Before))
                throw new InvalidOperationException($"第 {i + 1} 步操作前快照不同");
            var (after, events) = session.Act(step.Version, step.OperationId);
            if (PlaySession.Hash(after) != step.AfterHash
                || PlaySession.Hash(step.After) != step.AfterHash
                || JsonConvert.SerializeObject(events) != JsonConvert.SerializeObject(step.Events))
                throw new InvalidOperationException($"第 {i + 1} 步结算或快照不同");
        }
        Console.WriteLine($"重放通过：{transcript.Steps.Count} 步");
    }

    private static void Write(object value)
    {
        Console.WriteLine(JsonConvert.SerializeObject(value));
        Console.Out.Flush();
    }

    private static int IntArg(string[] args, string key, int fallback)
    {
        int i = Array.IndexOf(args, key);
        if (i < 0) return fallback;
        if (i + 1 == args.Length) throw new ArgumentException($"{key} 缺少数值");
        return int.Parse(args[i + 1]);
    }

    private static string Revision()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        string root = ProjectPaths.ContentRoot;
        foreach (var path in Directory.GetFiles(root, "*.scm", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path)));
            hash.AppendData(File.ReadAllBytes(path));
        }
        // 记录协议与规则代码的当前版本；重放要求在同一构建上进行。
        hash.AppendData(File.ReadAllBytes(typeof(PlaySession).Assembly.Location));
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
