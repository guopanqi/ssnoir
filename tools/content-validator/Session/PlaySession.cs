#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SSNoir.Core;
using SSNoir.Playtest;
using SSNoir.Testing;

namespace SSNoir.Session;

/// <summary>玩家视角的单局会话。所有游戏状态与规则仍由 SceneManager 持有。</summary>
public sealed class PlaySession
{
    private readonly SceneManager _scenes;
    private readonly List<string> _navigation = new();
    private readonly Dictionary<string, Choice> _choices = new(StringComparer.Ordinal);
    private long _version;
    private bool _faulted;
    private SessionObservation _observation = null!;
    public IReadOnlyList<SessionEvent> OpeningEvents { get; private set; } = Array.Empty<SessionEvent>();

    private sealed record Choice(string Kind, GameNode? Node = null,
        List<SlottedResource?>? Slots = null);

    private PlaySession(SceneManager scenes) => _scenes = scenes;

    public static PlaySession Start(SessionSetup setup)
    {
        GameRandom.Reseed(setup.Seed);
        var state = new GameState();
        var scenes = new SceneManager(state, new LocalScriptLoader());
        var opening = new List<SessionEvent>();
        void OnDialogue(DialogueSequence sequence)
        {
            foreach (var line in sequence.Lines)
                opening.Add(new SessionEvent("入场对白", $"{line.Speaker}：{line.Text}"));
        }
        void OnBanter(DialogueSequence sequence)
        {
            foreach (var line in sequence.Lines)
                opening.Add(new SessionEvent("入场插话", $"{line.Speaker}：{line.Text}"));
        }
        void OnStage(StoryStageSequence sequence)
        {
            foreach (var beat in sequence.Beats)
                foreach (var command in beat.Commands)
                    if (command.Line != null)
                        opening.Add(new SessionEvent("入场演出", $"{command.Line.Speaker}：{command.Line.Text}"));
        }
        state.DialogueCenter.OnDialogueRequested += OnDialogue;
        state.DialogueCenter.OnBanterRequested += OnBanter;
        state.DialogueCenter.OnStageRequested += OnStage;
        var initial = new PlaytestSetup { Growth = setup.Growth,
            Items = setup.Items ?? new Dictionary<string, int>(), Seed = setup.Seed };
        if (setup.Entry == "world")
        {
            scenes.LoadScene("world");
            initial.ApplyTo(state);
            scenes.RebuildRenderTree();
        }
        else if (setup.Entry.TrimStart().StartsWith("(", StringComparison.Ordinal))
        {
            scenes.LoadScene("world");
            initial.ApplyTo(state);
            // 入场表达式由测试操作者提供，只在创建会话时执行；Agent 操作协议不暴露 Eval。
            scenes.ActiveInterpreter.Eval(setup.Entry);
        }
        else
        {
            scenes.LoadScene(setup.Entry);
            initial.ApplyTo(state);
            scenes.RebuildRenderTree();
        }
        state.DialogueCenter.OnDialogueRequested -= OnDialogue;
        state.DialogueCenter.OnBanterRequested -= OnBanter;
        state.DialogueCenter.OnStageRequested -= OnStage;
        if (state.SpotlightCenter.Current != null)
            opening.Add(new SessionEvent("入场聚光",
                $"【{state.SpotlightCenter.Current.Title}】{state.SpotlightCenter.Current.Subtitle}"));
        var session = new PlaySession(scenes) { OpeningEvents = opening.ToArray() };
        session.Refresh();
        return session;
    }

    public SessionObservation Observe() => _observation;

    public (SessionObservation Observation, IReadOnlyList<SessionEvent> Events) Act(
        long version, string operationId)
    {
        if (_faulted)
            throw new InvalidOperationException("本局曾在结算中失败，状态不可信；请重新开始会话。");
        if (version != _version)
            throw new InvalidOperationException($"过期观察：提交 {version}，当前 {_version}。");
        if (!_choices.TryGetValue(operationId, out var choice))
            throw new InvalidOperationException($"当前观察中没有操作：{operationId}");

        string oldScene = _scenes.CurrentSceneName;
        var events = new List<SessionEvent>();
        try
        {
            switch (choice.Kind)
            {
                case "navigate":
                    _navigation.Add(choice.Node!.Name);
                    break;
                case "back":
                    _navigation.RemoveAt(_navigation.Count - 1);
                    break;
                case "enter-place":
                    _navigation.Add(choice.Node!.Name);
                    AddReport(events, "入场", _scenes.EnterPlace(choice.Node.Name));
                    break;
                case "action":
                    AddReport(events, "行动", _scenes.ExecuteAction(choice.Node!, choice.Slots!));
                    break;
                case "end-turn":
                    if (_scenes.LatestSnapshot.IsInEncounter)
                    {
                        var frame = _scenes.BeginRoundTransition();
                        while (!frame.IsFinished)
                        {
                            events.Add(new SessionEvent("回合阶段", frame.Phase.ToString(),
                                FrameState(frame.Snapshot)));
                            AddReport(events, frame.Phase.ToString(), frame.Report);
                            frame = _scenes.AdvanceRoundTransition();
                        }
                    }
                    else
                        AddReport(events, "结束回合", _scenes.EndTurn());
                    break;
                default:
                    throw new InvalidOperationException($"未知操作：{choice.Kind}");
            }
            if (!string.Equals(oldScene, _scenes.CurrentSceneName, StringComparison.Ordinal))
            {
                events.Add(new SessionEvent("场景切换", $"{oldScene} → {_scenes.CurrentSceneName}"));
                _navigation.Clear();
            }
            Refresh();
        }
        catch
        {
            _faulted = true;
            throw;
        }
        return (_observation, events);
    }

    private void Refresh()
    {
        _version++;
        _choices.Clear();
        var snapshot = _scenes.LatestSnapshot;
        var root = snapshot.RootNode ?? throw new InvalidOperationException("场景没有玩家可见根节点。");
        var focus = root;
        int validDepth = 0;
        foreach (string name in _navigation)
        {
            var next = focus.Children.FirstOrDefault(n => n.Name == name);
            if (next == null) break;
            focus = next;
            validDepth++;
        }
        if (validDepth < _navigation.Count)
            _navigation.RemoveRange(validDepth, _navigation.Count - validDepth);

        var operations = new List<SessionOperation>();
        void Add(string kind, string label, GameNode? node = null, List<SlottedResource?>? slots = null)
        {
            string id = $"o{operations.Count + 1}";
            var die = slots?.FirstOrDefault(s => s?.Type == "die");
            int? prepared = null;
            string? odds = null;
            if (die != null && node?.Resolve?.Type == ResolveType.Roll)
            {
                var actor = snapshot.Actors.First(a => a.Id == die.ActorId);
                string skill = node.Resolve.SkillName;
                int modifier = node.Resolve.DifficultyModifiers.Sum(m => m.Value);
                if (actor.Role == "protagonist")
                {
                    if (string.Equals(snapshot.InjurySkillKey, skill, StringComparison.OrdinalIgnoreCase))
                        modifier += snapshot.InjurySkillPenalty;
                    if (snapshot.ScarModifiers.TryGetValue(skill, out var scar))
                        modifier += scar.Value;
                }
                prepared = FateStrip.PreparedValue(die.Value, actor.Stats[skill], modifier);
                odds = FateStrip.Describe(FateStrip.StripForPrepared(prepared.Value));
            }
            operations.Add(new SessionOperation(id, kind, label, node?.Name,
                die?.Value, prepared, odds));
            _choices.Add(id, new Choice(kind, node, slots));
        }

        foreach (var node in focus.Children)
        {
            if (node.IsPlace && focus == root && !snapshot.IsInEncounter)
                Add("enter-place", $"进入 {node.Name}", node);
            else if (node.IsContainer)
                Add("navigate", $"查看 {node.Name}", node);
            else if (node.Resolve?.Type is ResolveType.Instant or ResolveType.Roll && !node.Disabled)
                AddActionChoices(node, snapshot, Add);
        }
        // CarryNodes 在客户端是独立的物品/支援区，任何交锋导航层都可访问。
        foreach (var node in snapshot.CarryNodes)
            if (!node.Disabled && node.Resolve?.Type is ResolveType.Instant or ResolveType.Roll)
                AddActionChoices(node, snapshot, Add);
        if (_navigation.Count > 0) Add("back", "返回上层");
        // 玩家随时可主动结束回合。RestBlockers 在 UI 中提示原因，而非隐藏按钮。
        Add("end-turn", "结束回合");

        _observation = new SessionObservation(_version, _scenes.CurrentSceneName,
            snapshot.IsInEncounter, snapshot.WorldDay, focus.Name,
            $"{snapshot.InjuryPart} {snapshot.InjuryBandName} {snapshot.InjurySeverity}/{snapshot.InjuryMaxSeverity}",
            snapshot.ScarSummary,
            focus.Children.Select(Card).ToArray(), snapshot.CarryNodes.Select(Card).ToArray(),
            snapshot.Actors.Where(a => a.OnStage).Select(a => new SessionActor(a.Id, a.Name,
                a.Composure, a.MaxComposure, a.Stats,
                a.ActionDice.Select((value, index) => new SessionDie(a.ActionDiceSlotIds[index], value)).ToArray())).ToArray(),
            snapshot.Inventory,
            snapshot.RestBlockers.Select(b => b.Reason).ToArray(),
            snapshot.Dossier.Select(d => $"{d.Kind}：{d.Now}（{d.Status}）").ToArray(),
            FormatResult(_scenes.LastEncounterResult),
            operations, AllClocks(snapshot));
    }

    private static string? FormatResult(object? result)
    {
        if (result == null) return null;
        if (result is IEnumerable<object> values)
            return "(" + string.Join(" ", values.Select(FormatResult)) + ")";
        return result.ToString();
    }

    private SessionFrameState FrameState(PresentationSnapshot snapshot)
    {
        var root = snapshot.RootNode ?? throw new InvalidOperationException("回合阶段没有场景根节点。");
        var focus = root;
        foreach (string name in _navigation)
        {
            var next = focus.Children.FirstOrDefault(n => n.Name == name);
            if (next == null) break;
            focus = next;
        }
        return new SessionFrameState(_scenes.CurrentSceneName, snapshot.WorldDay, focus.Name,
            focus.Children.Select(Card).ToArray(),
            snapshot.Actors.Where(a => a.OnStage).Select(a => new SessionActor(a.Id, a.Name,
                a.Composure, a.MaxComposure, a.Stats,
                a.ActionDice.Select((value, index) => new SessionDie(a.ActionDiceSlotIds[index], value)).ToArray())).ToArray(),
            snapshot.Inventory, snapshot.InjurySeverity, AllClocks(snapshot));
    }

    private static IReadOnlyList<SessionClock> AllClocks(PresentationSnapshot snapshot)
    {
        var root = snapshot.RootNode ?? throw new InvalidOperationException("场景没有玩家可见根节点。");
        var clocks = new Dictionary<string, SessionClock>(StringComparer.Ordinal);
        void Visit(GameNode node)
        {
            foreach (var clock in Card(node).Clocks)
            {
                if (clocks.TryGetValue(clock.Label, out var previous) && previous != clock)
                    throw new InvalidOperationException($"同名时钟状态不一致：{clock.Label}");
                clocks[clock.Label] = clock;
            }
            foreach (var child in node.Children) Visit(child);
        }
        Visit(root);
        return clocks.Values.ToArray();
    }

    private static SessionCard Card(GameNode node)
    {
        var resolve = node.Resolve;
        var clocks = node.Clocks.Concat(resolve?.Clock == null ? Array.Empty<GameClock>() : new[] { resolve.Clock })
            .Select(c => new SessionClock(c.Label, c.Note, c.Current, c.Max, c.Style.ToString())).ToArray();
        string text = resolve?.Type == ResolveType.Note
            ? $"{resolve.NoteTitle} {resolve.NoteText}".Trim()
            : resolve?.ObserveText ?? "";
        return new SessionCard(node.Name, node.Subtitle, resolve?.Type.ToString() ?? "Container",
            node.Disabled, text, resolve?.SkillName ?? "", node.Tags.ToArray(), clocks);
    }

    private static void AddActionChoices(GameNode node, PresentationSnapshot snapshot,
        Action<string, string, GameNode?, List<SlottedResource?>?> add)
    {
        var requirements = node.Requires;
        var slots = new List<SlottedResource?>();
        var selected = new HashSet<string>(StringComparer.Ordinal);
        var itemDemand = new Dictionary<string, int>(StringComparer.Ordinal);
        void Expand(int index, string description)
        {
            if (index == requirements.Count)
            {
                add("action", description.Length == 0 ? node.Name : $"{node.Name}：{description}",
                    node, new List<SlottedResource?>(slots));
                return;
            }
            var req = requirements[index];
            if (req.Type == "item")
            {
                int needed = itemDemand.GetValueOrDefault(req.ItemId) + req.Qty;
                if (!snapshot.Inventory.TryGetValue(req.ItemId, out int owned) || owned < needed)
                    return;
                itemDemand[req.ItemId] = needed;
                slots.Add(new SlottedResource { Type = "item", ItemId = req.ItemId,
                    Qty = req.Qty, Value = req.Qty });
                Expand(index + 1, description + $" {req.ItemId}×{req.Qty}");
                slots.RemoveAt(slots.Count - 1);
                if (needed == req.Qty) itemDemand.Remove(req.ItemId);
                else itemDemand[req.ItemId] = needed - req.Qty;
            }
            else if (req.Type == "die")
            {
                foreach (var actor in snapshot.Actors.Where(a => a.OnStage && a.Status == "active"))
                for (int i = 0; i < actor.ActionDice.Count; i++)
                {
                    string key = $"{actor.Id}:{actor.ActionDiceSlotIds[i]}";
                    if (!selected.Add(key)) continue;
                    int slotId = actor.ActionDiceSlotIds[i];
                    int value = actor.ActionDice[i];
                    slots.Add(new SlottedResource { Type = "die", ActorId = actor.Id,
                        DieIndex = slotId, SourceIndex = slotId, Value = value });
                    Expand(index + 1, description + $" {actor.Name}[{slotId}]={value}");
                    slots.RemoveAt(slots.Count - 1);
                    selected.Remove(key);
                }
            }
            else
                throw new InvalidOperationException($"会话不支持资源要求：{req.Type}（{node.Name}）。");
        }
        Expand(0, "");
    }

    private static void AddReport(List<SessionEvent> events, string phase, ActionReport? report)
    {
        if (report == null) return;
        if (report.Type == ActionType.Roll)
            events.Add(new SessionEvent(phase, $"判定：{report.Outcome}，准备值 {report.PreparedValue}，命运骰 {report.FateDieValue}"));
        foreach (var effect in report.Effects)
            events.Add(new SessionEvent(phase, $"{effect.Label ?? effect.Text} {effect.Delta}"));
        foreach (var step in report.BlockingStorySteps.Concat(report.PostSceneBlockingSteps))
        {
            if (step.Spotlight != null)
                events.Add(new SessionEvent(phase, $"【{step.Spotlight.Title}】{step.Spotlight.Subtitle}"));
            if (step.Dialogue != null)
                foreach (var line in step.Dialogue.Lines)
                    events.Add(new SessionEvent(phase, $"{line.Speaker}：{line.Text}"));
            if (step.Stage != null)
                foreach (var beat in step.Stage.Beats)
                    foreach (var command in beat.Commands)
                        if (command.Line != null)
                            events.Add(new SessionEvent(phase, $"{command.Line.Speaker}：{command.Line.Text}"));
            if (step.Kind == BlockingStoryStepKind.Video)
                events.Add(new SessionEvent(phase, $"视频：{step.VideoTag}"));
            if (step.Kind == BlockingStoryStepKind.Motion)
                events.Add(new SessionEvent(phase, $"场景演出：{step.MotionProp}/{step.MotionState}"));
            if (step.Kind == BlockingStoryStepKind.EnterPlace)
                events.Add(new SessionEvent(phase, $"进入地点：{step.PlaceName}"));
            if (step.Kind == BlockingStoryStepKind.AutoAction)
            {
                events.Add(new SessionEvent(phase,
                    $"强制行动：{step.AutoActionNode?.Name} {step.AutoActionNode?.Subtitle}"));
                if (step.AutoActionPrelude != null)
                    foreach (var line in step.AutoActionPrelude.Lines)
                        events.Add(new SessionEvent(phase, $"{line.Speaker}：{line.Text}"));
                AddReport(events, phase, step.ResolvedReport);
            }
        }
        foreach (var banter in report.Banter)
            foreach (var line in banter.Lines)
                events.Add(new SessionEvent(phase, $"{line.Speaker}：{line.Text}"));
        if (report.TurnStartReport != null) AddReport(events, "新回合", report.TurnStartReport);
    }

    public static string Hash(SessionObservation observation)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(observation)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
