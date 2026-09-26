#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SSNoir.Scripting;
using Schemy;

namespace SSNoir.Core
{
    public class SceneManager
    {
        private readonly GameState _gameState;
        private readonly IScriptLoader _loader;

        private SchemeInterpreter? _worldInterpreter;
        private SchemeInterpreter? _encounterInterpreter;
        private string _encounterSceneName = string.Empty;
        private bool _turnEndedDuringAction;
        private bool _isExecutingAction;
        private bool _isEnteringPlace;
        private bool _isResolvingTurnEnd;
        private bool _hasPendingSceneDiceRoll;
        private readonly List<PendingAutoAction> _pendingAutoActions = new();
        private RoundTransitionState _roundTransitionState;
        private RoundTransitionPhase _roundTransitionPhase;
        private SchemeInterpreter? _roundTransitionInterpreter;
        private int _nextForcedAction;
        // 出发去交锋前扣下的世界骰池；回到世界时还回去。null = 没有可还的（新开局/读档/新一天）。
        private Dictionary<string, (List<int> Dice, List<int> SlotIds)>? _stashedWorldDice;
        private bool _pendingSceneIsEncounter;
        private Procedure? _encounterCallback;
        private bool _encounterEnded;
        private readonly HashSet<string> _seenWorldPlaces = new HashSet<string>(StringComparer.Ordinal);
        private bool _hasWorldPlaceBaseline;

        private enum RoundTransitionState { Idle, Resolving, Committed, Finishing }

        private sealed class PendingAutoAction
        {
            public string Name { get; init; } = string.Empty;
            public string Text { get; init; } = string.Empty;
            public string? AnchorName { get; init; }
            public List<(string ActorId, int Count)> Demands { get; init; } = new();
            public DialogueSequence? Prelude { get; init; }
            public ICallable Effect { get; init; } = null!;
            public string SceneName { get; init; } = string.Empty;
        }

        public event Action? OnSceneLoaded;
        public event Action? OnWorldRefreshed;
        public event Action<string>? OnWarning;

        /// <summary>随当前存档保存的客户端设置；值只允许使用 SaveManager 支持的基础类型。</summary>
        public Dictionary<string, object> Settings { get; } = new();

        public GameNode? CurrentRootNode { get; private set; }
        public List<GameClock> CurrentClocks { get; private set; } = new List<GameClock>();
        /// <summary>本帧的卷宗（世界场景才有，交锋里是空的）。</summary>
        public List<DossierEntry> CurrentDossier { get; private set; } = new List<DossierEntry>();
        public List<SupportEntry> CurrentSupports { get; private set; } = new List<SupportEntry>();

        /// <summary>玩家钉在地图上的那条线。是阅读偏好，不是故事状态，所以放在全局里跟着存档走。</summary>
        /// <summary>
        /// 卷宗里被玩家**取消钉住**的那些线的 id，按行分隔。钉住是默认：新接的线自动上地图，
        /// 玩家取消过的才记下来。存的是取消而不是钉住，新线才不需要任何登记就出现。
        /// </summary>
        public const string DossierUnpinnedKey = "dossier-unpinned";

        /// <summary>
        /// 不重建渲染树：这是阅读偏好，不改变世界，客户端下一帧直接读全局即可。
        /// </summary>
        public void SetDossierUnpinned(IEnumerable<string> ids)
            => _gameState.Set(DossierUnpinnedKey, string.Join("\n", ids));
        public PresentationSnapshot LatestSnapshot { get; private set; } = new PresentationSnapshot();

        public SchemeInterpreter ActiveInterpreter => _encounterInterpreter ?? _worldInterpreter ?? throw new InvalidOperationException("No active interpreter");
        public string CurrentSceneName => _encounterInterpreter != null ? _encounterSceneName : "world";
        public GameState GameState => _gameState;

        public SceneManager(GameState gameState, IScriptLoader loader)
        {
            _gameState = gameState;
            _loader = loader;
        }

        public void ResetForNewGame()
        {
            Settings.Clear();
            _worldInterpreter = null;
            _encounterInterpreter = null;
            _encounterSceneName = string.Empty;
            _encounterCallback = null;
            _encounterEnded = false;
            _turnEndedDuringAction = false;
            _hasPendingSceneDiceRoll = false;
            _pendingAutoActions.Clear();
            _isResolvingTurnEnd = false;
            _roundTransitionState = RoundTransitionState.Idle;
            _roundTransitionInterpreter = null;
            _nextForcedAction = 0;
            _pendingSceneIsEncounter = false;
            _stashedWorldDice = null;
            CurrentRootNode = null;
            CurrentClocks.Clear();
            LatestSnapshot = new PresentationSnapshot();
            ResetWorldPlaceBaseline();
            _gameState.ResetForNewGame();
            LoadScene("world");
        }

        // Explicit scene switch entry point for the UI. Scene transitions are an
        // explicit action — they are not triggered as a side effect of writing the
        // "location" global. No-op when already in the requested scene.
        public void GoToLocation(string sceneName)
        {
            if (sceneName != CurrentSceneName)
            {
                // 调试直切不属于原交锋的收场；不能把它的回调带进目标场景。
                _encounterCallback = null;
                LoadScene(sceneName);
            }
        }

        // The world is a single shared interpreter; everything else is an encounter.
        private static bool IsWorldScene(string sceneName)
        {
            return sceneName == "world" || sceneName == "world/world";
        }

        public void LoadScene(string sceneName)
        {
            // 世界骰池属于「今天」，不属于「这个场景」。去打一场交锋再回来，天没变，
            // 骰子就不该换一批——所以离开世界时把骰池扣下来，回来时原样还回去。
            if (IsWorldScene(CurrentSceneName) && !IsWorldScene(sceneName))
                _stashedWorldDice = _gameState.Team.CaptureActionDice();

            if (IsWorldScene(sceneName))
            {
                _encounterEnded = true;
                _encounterInterpreter = null;
                _encounterSceneName = string.Empty;
                
                if (_worldInterpreter == null)
                {
                    _worldInterpreter = new SchemeInterpreter(_gameState, _loader);
                    RegisterEncounterBridges(_worldInterpreter);
                    _worldInterpreter.LoadFile("scenes/world/world.scm");
                }
                
                if (_gameState.Get<string>("location") != "world")
                {
                    _gameState.Set("location", "world");
                }
            }
            else
            {
                // 正式入场和调试直载共用同一条生命周期边界。
                _encounterEnded = false;
                SupportUsedThisEncounter = false;
                string cleanName = sceneName;
                if (cleanName.StartsWith("encounters/"))
                {
                    cleanName = cleanName.Substring("encounters/".Length);
                }

                _encounterSceneName = cleanName;
                _encounterInterpreter = new SchemeInterpreter(_gameState, _loader);
                RegisterEncounterBridges(_encounterInterpreter);
                _encounterInterpreter.LoadFile($"scenes/encounters/{cleanName}.scm");
                
                if (_gameState.Get<string>("location") != cleanName)
                {
                    _gameState.Set("location", cleanName);
                }

                // 交锋自己的入场演出属于刚载入的交锋，而不是发起它的世界动作。
                // 若本次载入来自一个动作，这些步骤会写进该动作仍持有的 ActionReport；
                // Debug 直载时则由表现函数安全地忽略。
                _encounterInterpreter.Eval("(on-encounter-enter)");
            }

            RollSceneDice(!IsWorldScene(sceneName));

            RebuildRenderTree();
            NotifySceneLoaded();
        }

        /// <summary>交锋里每结束一个回合要付的冷静。见 EndTurn。
        /// 1 点＝满冷静能撑五个回合。曾经是 2：一场交锋三个回合就见底，
        /// 玩家还没来得及做完想做的事就已经在流血，代价来得太快，读不成"别磨"，
        /// 只读成"这场打不起"。压到 1，时间表拉长一倍，磨仍然要付账，但付得起。
        ///
        /// 注意这条本身**不构成**"别磨"的全部理由——1 点一回合足够便宜，
        /// 光靠它玩家可以一直等好骰子。真正让人不敢磨的东西得由交锋自己带
        /// （时钟走到底、对手的动作、机会窗口关掉），这条只负责让磨有个底价。</summary>
        private const int EncounterTurnComposureCost = 1;

        private void RollSceneDice(bool isInEncounter)
        {
            if (_isExecutingAction)
            {
                _hasPendingSceneDiceRoll = true;
                _pendingSceneIsEncounter = isInEncounter;
                return;
            }

            ApplySceneDice(isInEncounter);
        }

        private void ApplyPendingSceneDiceRoll()
        {
            if (!_hasPendingSceneDiceRoll)
            {
                return;
            }

            _hasPendingSceneDiceRoll = false;
            ApplySceneDice(_pendingSceneIsEncounter);
        }

        // 进交锋：交锋有自己的一套骰池，重掷。
        // 回世界：有暂存就还回去（同一天不换骰子）；没有暂存才是真的新一天/新开局，重掷。
        private void ApplySceneDice(bool isInEncounter)
        {
            if (isInEncounter)
            {
                // 交锋使用自己的骰池，但不能动离开世界时扣下的那份。它要一直活到
                // EndEncounter 返回世界；此前这里无条件清 null，导致回来后重掷城市骰。
                _gameState.Team.RollActionDice(isInEncounter: true, consumeHangover: false);
                return;
            }

            if (_stashedWorldDice != null)
            {
                _gameState.Team.RestoreActionDice(_stashedWorldDice);
                _stashedWorldDice = null;
                return;
            }

            // 没有暂存才是真的新开局或新一天。
            _stashedWorldDice = null;
            _gameState.Team.RollActionDice(isInEncounter: false, consumeHangover: false);
        }

        private void NotifySceneLoaded()
        {
            // 动作与入场节拍都先把新快照准备好，再由各自的表现完成回调 adopt。
            // 在中途通知客户端会清掉正在播放的 ActionReport。
            if (!_isExecutingAction && !_isEnteringPlace)
            {
                OnSceneLoaded?.Invoke();
            }
        }

        public void StartEncounter(string name)
        {
            LoadScene(name);
        }

        /// <summary>带进这一场的关系支援用过了没有。每场一次，随交锋开始归零；
        /// 交锋中不能存档，所以它不进存档。</summary>
        public bool SupportUsedThisEncounter { get; private set; }

        /// <summary>最后一次交锋结算交回来的值。正式流程由回调消费，这里留一份供离线试跑读取。</summary>
        public object? LastEncounterResult { get; private set; }

        public void EndEncounter(object? result = null)
        {
            if (_encounterEnded) return;
            _encounterEnded = true;
            LastEncounterResult = result;

            var cb = _encounterCallback;
            _encounterCallback = null;
            var report = _gameState.CurrentActionReport;
            int oldSceneStepCount = report?.BlockingStorySteps.Count ?? 0;

            // 支援叫来的帮手只属于这一场：结算前先送走，回调和城市都不该看见他。
            _gameState.Team.DismissTemporaryCompanions();

            if (cb != null)
                cb.Call(new List<object> { result ?? Symbol.FromString("none") });

            if (report != null && report.BlockingStorySteps.Count > oldSceneStepCount)
            {
                int count = report.BlockingStorySteps.Count - oldSceneStepCount;
                report.PostSceneBlockingSteps.AddRange(
                    report.BlockingStorySteps.GetRange(oldSceneStepCount, count));
                report.BlockingStorySteps.RemoveRange(oldSceneStepCount, count);
            }

            LoadScene("world");
        }

        private void AbortEncounterForRetry()
        {
            if (_encounterEnded) return;
            _encounterEnded = true;
            LastEncounterResult = Symbol.FromString("倒下");
            _encounterCallback = null;
            _gameState.Team.DismissTemporaryCompanions();
            LoadScene("world");
        }

        /// <summary>
        /// 消费一次倒下：若仍在交锋，先按该交锋声明的现有失败结果或重试协议退出；
        /// 随后强制过完这一天，作废此前暂存的城市骰，并把表现位置落到诊所。
        ///
        /// **「你倒下了」必须排在退场回调讲的话前面。**退出交锋会当场跑世界那边的收场回调
        /// （勒索信 → 三封信的 on-delivery-result），它讲的是那一夜的结局；而倒下是那个结局
        /// 的**原因**。按追加顺序播，玩家先读到"你跟丢了他、钱一分没剩"——听起来像是他没追上，
        /// 然后才被告知自己其实是被撂倒了、已经躺在诊所。因果反过来，那条失败叙述就变成了假话。
        /// 因而把送医与倒下展示放在世界回调之前；旧交锋演出结束后先采纳世界快照，
        /// 再按“诊所醒来 → 你倒下了 → 那一夜的结局”播放。
        /// </summary>
        private void ResolvePendingHospitalization(
            ActionReport report,
            SchemeInterpreter collapseInterpreter,
            bool advanceWorldTurnRules)
        {
            if (!_gameState.HasPendingHospitalization)
                return;

            if (!CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase))
            {
                object rawPolicy = collapseInterpreter.Eval("(on-encounter-collapse)");
                if (rawPolicy is not List<object> policy || policy.Count == 0)
                    throw new InvalidOperationException(
                        $"交锋“{CurrentSceneName}”的 on-encounter-collapse 必须返回 collapse-result 或 collapse-retry。");

                string kind = SchemeValue.AsId(policy[0]);
                if (kind == "collapse-result" && policy.Count == 2)
                {
                    // 回传什么由交锋自己决定，引擎不管。曾经这里写着「只复用既有失败结果，
                    // 不增加倒下专属故事路线」——那条约定是错的，实测就露馅了：玩家在巷口被
                    // 撂倒，世界那边照着「跟丢」那条路讲了一遍"你没追上他"。他明明追上了，
                    // 是被打倒的。**倒下和普通失败是两件事，讲成一件就是在骗玩家。**
                    // 所以交锋在这里应当回传一个能被世界那边认出来的收场（例如 '倒下），
                    // 由拥有故事的模块决定它怎么讲——除非那一场的失败本来就是"你被打趴下"。
                    _stashedWorldDice = null;
                    EndEncounter(policy[1]);
                }
                else if (kind == "collapse-retry" && policy.Count == 1)
                {
                    _stashedWorldDice = null;
                    AbortEncounterForRetry();
                }
                else
                {
                    throw new InvalidOperationException(
                        $"交锋“{CurrentSceneName}”返回了非法倒下策略；应为 (collapse-result result) 或 (collapse-retry)。");
                }
            }

            if (!CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("倒下结算后必须回到世界场景。交锋回调不能在送医途中开启另一场交锋。");

            var hospitalization = _gameState.ConsumeHospitalization();

            // 先采纳世界快照并抵达诊所，再讲倒下与退场回调；旧交锋的演出在此前结束。
            report.PostSceneBlockingSteps.Insert(0,
                BlockingStoryStep.ForEnterPlace("诊所"));
            report.PostSceneBlockingSteps.Insert(1, BlockingStoryStep.ForSpotlight(
                new SpotlightCard { Title = "你倒下了", Subtitle = hospitalization.Text }));

            _stashedWorldDice = null;
            if (advanceWorldTurnRules)
            {
                var world = _worldInterpreter
                    ?? throw new InvalidOperationException("Hospitalization requires the world interpreter.");
                world.Eval("(on-turn-end)");
            }

            foreach (var name in _gameState.Team.BeginCityDay())
                report.AddSupplement($"{name}缓过来了，今天照旧跟着你。");

            // 这是新的一天，不是普通切场景：必须消费宿醉并重新发骰。EndEncounter 在动作中
            // 可能已经挂起过一次“回世界”的场景骰，那次只是过渡，明确取消，避免随后覆盖新日骰池。
            _hasPendingSceneDiceRoll = false;
            _gameState.Team.RollActionDice(isInEncounter: false);
        }

        private void RegisterEncounterBridges(SchemeInterpreter interpreter)
        {
            interpreter.RawInterpreter.DefineGlobal(
                Symbol.FromString("start-encounter"),
                new NativeProcedure(args =>
                {
                    if (args.Count < 1)
                        throw new ArgumentException("start-encounter requires 1 argument (encounter name)");
                    string name = SchemeValue.AsId(args[0]);
                    if (_isEnteringPlace && _gameState.CurrentActionReport?.Effects.Count > 0)
                        throw new InvalidOperationException(
                            $"入场节拍在 start-encounter（\"{name}\"）之前产生了动作结算效果。"
                            + "Arrival 只能先播叙事，再把 start-encounter 放在最后。");
                    _encounterCallback = args.Count > 1 ? args[1] as Procedure : null;
                    StartEncounter(name);
                    return new None();
                }, "start-encounter")
            );

            // 关系支援每场一次：次数记在这里，随 StartEncounter 归零；消费在 ExecuteAction 里由引擎做，
            // 内容只读它来把卡变灰。
            interpreter.RawInterpreter.DefineGlobal(
                Symbol.FromString("__support-used?"),
                new NativeProcedure(args => SupportUsedThisEncounter, "__support-used?"));

            // 支援叫来的临时帮手：入队并当场发骰；回合末 / 交锋结束由引擎自动清走。只能在交锋里叫。
            interpreter.RawInterpreter.DefineGlobal(
                Symbol.FromString("__summon-helper!"),
                new NativeProcedure(args =>
                {
                    if (args.Count < 3)
                        throw new ArgumentException("__summon-helper! requires id, name, and stats alist");
                    if (IsWorldScene(CurrentSceneName))
                        throw new InvalidOperationException("summon-helper!: 帮手只能在交锋里叫，城市里没有他的骰位。");
                    string actorId = SchemeValue.AsId(args[0]);
                    string name = args[1] as string
                        ?? throw new ArgumentException("helper name must be a string");
                    var stats = NativeFunctions.ParseCompanionStats(args[2]);
                    _gameState.Team.SummonHelper(actorId, name, stats);
                    _gameState.NotificationCenter.Push($"{name}来了", NotificationKind.Info);
                    return new None();
                }, "__summon-helper!"));

            interpreter.RawInterpreter.DefineGlobal(
                Symbol.FromString("end-encounter"),
                new NativeProcedure(args =>
                {
                    // 造成倒下的脚本效果必须把退场交给 ResolvePendingHospitalization。
                    // 若此处先正常退场，动作后处理只能看见 world，交锋自己的
                    // on-encounter-collapse 将永远没有机会决定倒下结果。
                    if (_gameState.HasPendingHospitalization)
                        throw new InvalidOperationException(
                            "玩家已经倒下，脚本不能再调用 end-encounter。"
                            + "请停止当前结算，由 on-encounter-collapse 声明退场结果。");
                    var result = args.Count > 0 ? args[0] : null;
                    EndEncounter(result);
                    return new None();
                }, "end-encounter")
            );

            interpreter.RawInterpreter.DefineGlobal(
                Symbol.FromString("__end-turn!"),
                new NativeProcedure(args =>
                {
                    if (_isEnteringPlace)
                        throw new InvalidOperationException(
                            "入场节拍里不能 end-turn!。走进一个地点不推进时间。");
                    EndTurn();
                    return new None();
                }, "__end-turn!")
            );

            interpreter.RawInterpreter.DefineGlobal(
                Symbol.FromString("__refresh-encounter-dice!"),
                new NativeProcedure(args =>
                {
                    if (args.Count != 0)
                        throw new ArgumentException("refresh-encounter-dice!: expected no arguments");
                    if (CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("refresh-encounter-dice!: 只能在交锋中使用。");

                    // 动作内调用时延迟到资源扣除之后，避免新手里的同一槽位被旧动作误扣。
                    RollSceneDice(isInEncounter: true);
                    return new None();
                }, "__refresh-encounter-dice!")
            );

            interpreter.RawInterpreter.DefineGlobal(
                Symbol.FromString("__auto-action!"),
                new NativeProcedure(args =>
                {
                    if (args.Count != 6)
                        throw new ArgumentException("auto-action!: expected name, subtitle, anchor, actor/count demands, prelude, and effect");
                    if (CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("auto-action!: 只能在交锋中使用。");
                    if (!_isResolvingTurnEnd)
                        throw new InvalidOperationException("auto-action!: 只能由交锋的回合结算规则调用。");
                    _ = _gameState.CurrentActionReport
                        ?? throw new InvalidOperationException("auto-action!: 必须在回合结算中调用。");
                    string name = args[0] as string
                        ?? throw new ArgumentException("auto-action!: name must be a string");
                    string text = args[1] as string
                        ?? throw new ArgumentException("auto-action!: progress text must be a string");
                    string? anchorName = args[2] is bool noAnchor && !noAnchor
                        ? null
                        : args[2] as string ?? throw new ArgumentException("auto-action!: anchor must be #f or a string");
                    if (args[3] is not List<object> demands || demands.Count == 0)
                        throw new ArgumentException("auto-action!: demands must be a non-empty list");
                    if (args[4] is not List<object> rawPrelude)
                        throw new ArgumentException("auto-action!: prelude must be (auto-dialogue ...) or (no-dialogue)");
                    DialogueSequence? prelude = rawPrelude.Count == 0
                        ? null
                        : NativeFunctions.ParseDialogueSequence(
                            new List<object> { rawPrelude }, "auto-action! prelude");
                    if (args[5] is not ICallable effect)
                        throw new ArgumentException("auto-action!: effect must be a procedure");

                    // 回合规则发生在新一手骰子发出之前；这里只登记，EndTurn 发骰后统一校验、扣除。
                    var parsed = new List<(string ActorId, int Count)>();
                    foreach (object raw in demands)
                    {
                        if (raw is not List<object> entry || entry.Count != 2)
                            throw new ArgumentException("auto-action!: each demand must be (actor-id count)");
                        string actorId = SchemeValue.AsId(entry[0]);
                        int count = SchemeValue.ToInt(entry[1]);
                        if (count <= 0)
                            throw new ArgumentOutOfRangeException("auto-action!: demand count must be positive");
                        parsed.Add((actorId, count));
                    }
                    _pendingAutoActions.Add(new PendingAutoAction
                    {
                        Name = name,
                        Text = text,
                        AnchorName = anchorName,
                        Demands = parsed,
                        Prelude = prelude,
                        Effect = effect,
                        SceneName = CurrentSceneName,
                    });
                    return new None();
                }, "__auto-action!")
            );

        }

        private List<SlottedResource> ConsumeAutoActionDice(PendingAutoAction pending)
        {
            var result = new List<SlottedResource>();
            foreach (var demand in pending.Demands)
            {
                var actor = _gameState.Team.FindActor(demand.ActorId)!;
                if (actor == null)
                    throw new InvalidOperationException($"auto-action!: actor '{demand.ActorId}' not found");
                if (demand.Count > actor.ActionSlotCount)
                    throw new InvalidOperationException(
                        $"auto-action!: actor '{demand.ActorId}' has {actor.ActionSlotCount} action slots, " +
                        $"but action '{pending.Name}' demands {demand.Count}");
                int available = actor.Status == "active" ? actor.ActionDice.Count : 0;
                int acquired = Math.Min(demand.Count, available);
                if (acquired > 0)
                    result.AddRange(_gameState.Team.ConsumeAvailableActionDice(demand.ActorId, acquired));

                if (acquired < demand.Count)
                {
                    string reason = actor.Status != "active"
                        ? $"status is '{actor.Status}'"
                        : $"only {available} action dice are available";
                    OnWarning?.Invoke(
                        $"[SSNoir] auto-action degraded: scene '{CurrentSceneName}', action '{pending.Name}', " +
                        $"actor '{demand.ActorId}' requested {demand.Count} dice but acquired {acquired}; {reason}.");
                }
            }
            return result;
        }

        public void SaveGame() => SaveGame(SaveManager.DefaultSavePath);
        public void LoadGame() => LoadGame(SaveManager.DefaultSavePath);

        public void SaveGame(string filePath)
        {
            if (_encounterInterpreter != null)
                throw new InvalidOperationException("Cannot save during an encounter. End the encounter first.");

            var globals = _gameState.GetPureGlobals();
            globals.Remove("location"); // reconstructed on load

            var data = new SaveData
            {
                Settings  = new Dictionary<string, object>(Settings),
                Globals   = globals,
                Team      = _gameState.Team.Serialize(),
                Inventory = new Dictionary<string, int>(_gameState.Inventory.Items),
                WorldData = _worldInterpreter?.Eval("(world-save)"),
                SaveTime  = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            SaveManager.Write(filePath, data);
        }

        public void LoadGame(string filePath)
        {
            var data = SaveManager.Read(filePath);

            Settings.Clear();
            foreach (var setting in data.Settings)
                Settings.Add(setting.Key, setting.Value);

            // 1. Force exit any active encounter
            _encounterInterpreter = null;
            _encounterSceneName   = string.Empty;
            _encounterCallback    = null;

            // 2. Ensure world interpreter exists (creates it if not yet initialized)
            if (_worldInterpreter == null)
            {
                _worldInterpreter = new SchemeInterpreter(_gameState, _loader);
                RegisterEncounterBridges(_worldInterpreter);
                _worldInterpreter.LoadFile("scenes/world/world.scm");
            }

            // 3. Restore Team (health, 骰池状态, actor composure/stats)
            _gameState.PrepareForLoad();
            _gameState.Team.ApplySaveData(data.Team);

            // 4. Restore Inventory (replaces entirely — no stale items left over)
            _gameState.Inventory.ApplySaveData(data.Inventory);

            // 5. Replace pure globals (chapter, reputation, etc.).
            //    ReplacePureGlobals clears _states and resets location=world; the
            //    render tree is rebuilt explicitly at the end of this method.
            _gameState.ReplacePureGlobals(data.Globals);

            // 6. Restore Scheme world state (must run after C# state is fully set)
            if (data.WorldData != null)
            {
                _worldInterpreter.RawInterpreter.DefineGlobal(
                    Symbol.FromString("__world-load-data"), data.WorldData);
                _worldInterpreter.Eval("(world-load! __world-load-data)");
            }

            // 7. 读档后骰池以存档为准，旧的暂存一律作废。
            _stashedWorldDice = null;
            // 新格式原样恢复当天剩余骰池。旧存档没有骰池字段，只在迁移时补掷一次；
            // 空骰池在新格式中是有效状态，绝不能误判成“需要重新掷骰”。
            if (!data.Team.HasSavedActionDice)
                _gameState.Team.RollActionDice(isInEncounter: false, consumeHangover: false);

            // 8. 读档后的第一棵世界树只建立地点基线。存档里已经开放的地点
            // 不应被误报为本次新开放。
            ResetWorldPlaceBaseline();
            RebuildRenderTree();
            OnSceneLoaded?.Invoke();
        }

        public void Refresh() => RebuildRenderTree();

        // 支援的文案归内容：每条 id 去问一次 (support-info id)，回 (标题 说明)。
        // 没登记的 id 由 Scheme 侧报错——发了支援却没写它是什么，是内容的问题。
        private List<SupportEntry> BuildSupportEntries(SchemeInterpreter active)
        {
            var result = new List<SupportEntry>();
            foreach (string id in _gameState.Team.Supports)
            {
                var info = active.Eval($"(support-info \"{id}\")");
                if (!(info is List<object> pair) || pair.Count != 2
                    || !(pair[0] is string title) || !(pair[1] is string desc))
                    throw new InvalidOperationException($"(support-info \"{id}\") 必须返回 (标题 说明) 两个字符串。");
                result.Add(new SupportEntry
                {
                    Id = id, Title = title, Description = desc,
                    Carried = string.Equals(id, _gameState.Team.CarriedSupport, StringComparison.Ordinal),
                });
            }
            return result;
        }

        /// <summary>出门前选带哪一条支援。交锋里不许换——那一场带谁进场时就定了。</summary>
        public void SetCarriedSupport(string id)
        {
            if (!IsWorldScene(CurrentSceneName))
                throw new InvalidOperationException("交锋里不能换支援。");
            _gameState.Team.SetCarriedSupport(id);
            RebuildRenderTree();
        }

        public void RebuildRenderTree()
        {
            var active = ActiveInterpreter;
            var rawData = active.Eval("(get-render-data)");
            var rootNode = NodeConverter.ConvertSingle(rawData, active.RawInterpreter);

            if (rootNode.Clocks.Count > 0)
                throw new InvalidOperationException(
                    "根容器不能挂 :clocks：根节点不会被渲染为卡。请改用 clock-node 作为第一个子节点。");

            AssertUniqueNodeNames(rootNode);
            AssertPlacesWellFormed(rootNode, IsWorldScene(CurrentSceneName));
            TrackNewWorldPlaces(rootNode);
            CurrentRootNode = rootNode;

            var flatClocks = new List<GameClock>();
            CollectClocksRecursive(rootNode, flatClocks);
            CurrentClocks = flatClocks;

            CurrentCarryNodes = BuildEncounterActionNodes(rootNode, active);

            CurrentDossier = IsWorldScene(CurrentSceneName)
                ? NodeConverter.ConvertDossier(active.Eval("(get-dossier)"))
                : new List<DossierEntry>();
            CurrentSupports = BuildSupportEntries(active);

            LatestSnapshot = BuildPresentationSnapshot(rootNode);
            
            OnWorldRefreshed?.Invoke();
        }

        /// <summary>
        /// 非场景交锋动作：随身消耗品与人物支援都不由具体交锋声明，否则每写一场都要重抄。
        /// 引擎通过 engine.scm 的 encounter-action-nodes 统一取一份，城市里不给。
        ///
        /// 它们**不进渲染树**：进了树就会被排进场上的卡片区，读起来像是这一场的事。
        /// 客户端从 CarryNodes 单独取，但一律使用普通动作卡和同一个 ExecuteAction；差别只在
        /// 出现条件与需求槽，不在 UI 类型。
        /// </summary>
        public IReadOnlyList<GameNode> CurrentCarryNodes { get; private set; } = new List<GameNode>();

        private List<GameNode> BuildEncounterActionNodes(GameNode rootNode, SchemeInterpreter active)
        {
            if (IsWorldScene(CurrentSceneName)) return new List<GameNode>();

            var carried = NodeConverter.ConvertList(active.Eval("(encounter-action-nodes)"), active.RawInterpreter);
            foreach (var node in carried)
            {
                if (!string.IsNullOrEmpty(node.SupportId) && node.Requires.Count > 0)
                    throw new InvalidOperationException(
                        $"支援动作 '{node.Name}' 不能有需求槽：这项人物能力本身不消耗资源。");
            }
            var treeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectNodeNames(rootNode, treeNames);
            foreach (var node in carried)
            {
                if (string.IsNullOrEmpty(node.CarryItemId) && string.IsNullOrEmpty(node.SupportId))
                    throw new InvalidOperationException(
                        $"非场景交锋动作 '{node.Name}' 必须声明 :carry-item 或 :support。");
                // 槽位状态按节点名索引，重名会让随身卡和场上某张卡共用同一份槽位。
                if (treeNames.Contains(node.Name))
                    throw new InvalidOperationException(
                        $"非场景交锋动作 '{node.Name}' 和交锋 '{CurrentSceneName}' 里的一张卡重名。");
            }
            return carried;
        }

        private static void CollectNodeNames(GameNode node, HashSet<string> into)
        {
            into.Add(node.Name);
            foreach (var child in node.Children)
                CollectNodeNames(child, into);
        }

        private void ResetWorldPlaceBaseline()
        {
            _seenWorldPlaces.Clear();
            _hasWorldPlaceBaseline = false;
        }

        /// <summary>
        /// 地点开放通知来自世界根的结构变化，不由内容模块逐条手写。
        /// 首次建树（新游戏、读档）只建立基线；此后同一会话里第一次出现的 Place
        /// 才通知。地点暂时隐藏后重新出现也不会重复通知。
        /// </summary>
        private void TrackNewWorldPlaces(GameNode rootNode)
        {
            if (!IsWorldScene(CurrentSceneName))
                return;

            if (!_hasWorldPlaceBaseline)
            {
                foreach (var child in rootNode.Children)
                {
                    if (child.IsPlace)
                        _seenWorldPlaces.Add(child.Name);
                }
                _hasWorldPlaceBaseline = true;
                return;
            }

            foreach (var child in rootNode.Children)
            {
                if (child.IsPlace && _seenWorldPlaces.Add(child.Name))
                {
                    _gameState.NotificationCenter.Push(
                        $"新地点开放：{child.Name}", NotificationKind.Info);
                }
            }
        }

        // 带上限的物品（现在只有烟）。表在 engine.scm，取一次记下来：它是内容层的常量，
        // 不随存档变，客户端每帧要读。
        private IReadOnlyDictionary<string, int> ItemCapacities =>
            _itemCapacities ??= NodeConverter.ConvertItemCapacities(
                ActiveInterpreter.Eval("(item-capacity-table)"));
        private IReadOnlyDictionary<string, int>? _itemCapacities;

        private PresentationSnapshot BuildPresentationSnapshot(GameNode rootNode)
        {
            var inventory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _gameState.Inventory.Items)
            {
                inventory[item.Key] = item.Value;
            }

            bool isInEncounter = !CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase);
            var actors = new List<ActorSnapshot>();
            foreach (var actor in _gameState.Team.Actors)
            {
                actors.Add(new ActorSnapshot
                {
                    Id = actor.Id,
                    Name = actor.Name,
                    Role = actor.Role,
                    Status = actor.Status,
                    OnStage = TeamState.IsOnStage(actor, isInEncounter),
                    Composure = actor.Composure,
                    MaxComposure = actor.MaxComposure,
                    SpentGrowthPoints = actor.SpentGrowthPoints,
                    Stats = new Dictionary<string, int>(actor.Stats),
                    ActionSlotCount = actor.ActionSlotCount,
                    ActionDice = actor.ActionDice.ToArray(),
                    ActionDiceSlotIds = actor.ActionDiceSlotIds.ToArray(),
                    ActiveActionSlotStatuses = _gameState.Team.GetActiveActionSlotStatuses(actor),
                    PendingActionSlotStatuses = _gameState.Team.GetPendingActionSlotStatuses(actor),
                });
            }

            return new PresentationSnapshot
            {
                RootNode = rootNode,
                CarryNodes = CurrentCarryNodes,
                InjurySeverity = _gameState.Team.Injury.Severity,
                InjuryPart = _gameState.Team.Injury.Part,
                InjuryBandName = Injury.BandName(_gameState.Team.Injury.Band),
                InjurySkillName = _gameState.Team.Injury.SkillName,
                InjurySkillKey = _gameState.Team.Injury.Skill,
                InjurySkillPenalty = _gameState.Team.Injury.SkillPenalty,
                InjuryCostsActionDie = _gameState.Team.Injury.CostsActionDie,
                ScarModifiers = _gameState.Team.Scars.ModifiersBySkill(),
                ScarSummary = _gameState.Team.Scars.Describe(),
                Failure = _gameState.Failure,
                RestBlockers = _gameState.RestBlockers,
                GrowthLevel = _gameState.Team.GrowthLevel,
                WorldDay = _gameState.Get<int>("世界日", 1),
                Location = _gameState.Get<string>("location"),
                Inventory = inventory,
                ItemCapacities = ItemCapacities,
                Dossier = CurrentDossier,
                Supports = CurrentSupports,
                Actors = actors,
                IsInEncounter = isInEncounter,
            };
        }

        private void CollectClocksRecursive(GameNode node, List<GameClock> result)
        {
            result.AddRange(node.Clocks);
            foreach (var child in node.Children)
                CollectClocksRecursive(child, result);
        }

        private static void AssertUniqueNodeNames(GameNode rootNode)
        {
            var firstPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            AssertUniqueNodeNamesRecursive(rootNode, "", firstPaths);
        }

        private static void AssertUniqueNodeNamesRecursive(
            GameNode node,
            string parentPath,
            Dictionary<string, string> firstPaths)
        {
            string path = string.IsNullOrEmpty(parentPath)
                ? node.Name
                : $"{parentPath} > {node.Name}";
            if (firstPaths.TryGetValue(node.Name, out string? firstPath))
            {
                throw new InvalidOperationException(
                    $"重复节点名称: \"{node.Name}\"。节点名称在整棵渲染树中必须唯一。"
                    + $"\n首次出现: {firstPath}"
                    + $"\n再次出现: {path}");
            }
            firstPaths[node.Name] = path;
            foreach (var child in node.Children)
            {
                AssertUniqueNodeNamesRecursive(child, path, firstPaths);
            }
        }

        /// <summary>
        /// Place 只能是世界根的直接子节点。地点里的地点会让「走进去」变成一个有层级的
        /// 概念（进了酒馆再进后厨算不算又到了一个地方？），而导航栈里那一串到底哪一层
        /// 是「玩家在哪」就说不清了。交锋树里则根本没有地点这回事。
        /// </summary>
        private static void AssertPlacesWellFormed(GameNode rootNode, bool isWorldScene)
        {
            foreach (var child in rootNode.Children)
            {
                AssertNoPlaceBelow(child, child.IsPlace ? child.Name : rootNode.Name);
            }

            if (isWorldScene)
                return;

            foreach (var child in rootNode.Children)
            {
                if (child.IsPlace)
                    throw new InvalidOperationException(
                        $"交锋树里不能出现地点：\"{child.Name}\"。地点只属于世界。");
            }
        }

        private static void AssertNoPlaceBelow(GameNode node, string containerName)
        {
            foreach (var child in node.Children)
            {
                if (child.IsPlace)
                    throw new InvalidOperationException(
                        $"\"{child.Name}\" 是地点，却嵌在 \"{containerName}\" 里。"
                        + "地点只能是世界根的直接子节点；里面的分区请用普通 container。");
                AssertNoPlaceBelow(child, containerName);
            }
        }

        private static void RunOnActionRules(SchemeInterpreter actionInterpreter)
        {
            actionInterpreter.Eval("(on-action)");
        }

        public ActionReport EndTurn()
        {
            if (!IsWorldScene(CurrentSceneName))
            {
                var aggregate = new ActionReport { Type = ActionType.Instant, TurnEnded = true };
                var frame = BeginRoundTransition();
                while (true)
                {
                    MergeReport(aggregate, frame.Report);
                    if (frame.IsFinished)
                        return aggregate;
                    frame = AdvanceRoundTransition();
                }
            }

            if (_isExecutingAction)
                _turnEndedDuringAction = true;
            bool ownsReport = _gameState.CurrentActionReport == null;
            var report = _gameState.CurrentActionReport ?? new ActionReport { Type = ActionType.Instant };
            if (ownsReport)
                _gameState.CurrentActionReport = report;
            // 无论回合是玩家直接按休息结束的，还是脚本在动作里调 end-turn! 结束的，
            // 拿到这份报告的人都该知道时间翻页了。
            report.TurnEnded = true;
            try
            {
                // 交锋里每结束一个回合扣一点冷静：时间本身就是代价。
                // 没有它，"这一回合手气不好，什么都不投，等下一轮重摇"是完全免费的，
                // 最优解就变成只投高点数——玩家不再需要在"现在动手"和"再等等"之间取舍。
                // 花超的部分由 SpendComposure 自动溢出成伤势，那正是"熬太久要还的"。
                // 动作里已经倒下（end-turn! 包在动作里）则照旧跳过：那一手的规则归动作。
                // 规则先跑，费用后收：这一回合把交锋结算了（散场/失败回到世界），
                // 时间税就不再收——成功之后不再调用其他。
                var turnInterpreter = ActiveInterpreter;
                if (!_gameState.HasPendingHospitalization)
                {
                    _isResolvingTurnEnd = true;
                    try
                    {
                        turnInterpreter.Eval("(on-turn-end)");
                    }
                    finally
                    {
                        _isResolvingTurnEnd = false;
                    }
                }

                int injuryBefore = _gameState.Team.Injury.Severity;
                int composureBefore = _gameState.Team.FindActor("player")?.Composure ?? 0;
                int automaticComposureDelta = (_gameState.Team.FindActor("player")?.Composure ?? 0) - composureBefore;
                int automaticInjuryDelta = _gameState.HasPendingHospitalization
                    ? Injury.MaxSeverity - injuryBefore
                    : _gameState.Team.Injury.Severity - injuryBefore;

                if (_gameState.HasPendingHospitalization)
                {
                    _gameState.Team.DismissTemporaryCompanions();
                    // 城市 EndTurn 的世界日历规则固定最先执行；若后续日终规则意外打倒玩家，
                    // 日期已经推进，不能再跑一遍。交锋 EndTurn 则还需要补跑一次世界日终。
                    ResolvePendingHospitalization(report, turnInterpreter, advanceWorldTurnRules: false);
                    report.AddEffect(
                        ActionEffectKind.Composure, "冷静", automaticComposureDelta,
                        automaticComposureDelta > 0 ? ActionEffectTone.Positive : ActionEffectTone.Negative);
                    report.AddEffect(
                        ActionEffectKind.Injury, "伤势", automaticInjuryDelta,
                        automaticInjuryDelta > 0 ? ActionEffectTone.Negative : ActionEffectTone.Positive);
                    if (automaticInjuryDelta > 0)
                        report.AddSupplement("冷静击穿：你的手在抖，身体先一步承受了代价。");

                    RebuildRenderTree();
                    if (ownsReport)
                        FillPresentationHints(report);
                    return report;
                }

                // 支援叫来的帮手只待这一回合：回合规则跑完就走，新一手骰子里没有他。
                foreach (var name in _gameState.Team.DismissTemporaryCompanions())
                    report.AddSupplement($"{name}走了。");

                bool stillInSameMode = IsWorldScene(CurrentSceneName);
                if (stillInSameMode)
                    _gameState.Team.RollActionDice(isInEncounter: false);

                // 城市里过完一天：同伴的冷静和行动骰一样按天重发（见 TeamState.BeginCityDay）。
                // 放在 on-turn-end 之后、和重新发骰同一处，因为它们是同一件事——新的一天的份额。
                if (stillInSameMode)
                {
                    foreach (var name in _gameState.Team.BeginCityDay())
                        report.AddSupplement($"{name}缓过来了，今天照旧跟着你。");

                    // 新日事件必须和旧日动作的表现分开。否则 on-turn-start 里的早报、电话
                    // 会先作为 BlockingStoryStep 播完，客户端才有机会落下睡眠黑幕。
                    var turnStartReport = new ActionReport { Type = ActionType.Instant };
                    var previousReport = _gameState.CurrentActionReport;
                    _gameState.CurrentActionReport = turnStartReport;
                    try
                    {
                        turnInterpreter.Eval("(on-turn-start)");
                    }
                    finally
                    {
                        _gameState.CurrentActionReport = previousReport;
                    }
                    if (HasVisibleResult(turnStartReport))
                        report.TurnStartReport = turnStartReport;
                }

                report.AddEffect(
                    ActionEffectKind.Composure, "冷静", automaticComposureDelta,
                    automaticComposureDelta > 0 ? ActionEffectTone.Positive : ActionEffectTone.Negative);

                // 伤势的正负与其他资源相反：数值涨上去是坏事。
                report.AddEffect(
                    ActionEffectKind.Injury, "伤势", automaticInjuryDelta,
                    automaticInjuryDelta > 0 ? ActionEffectTone.Negative : ActionEffectTone.Positive);
                if (automaticInjuryDelta > 0)
                {
                    report.AddSupplement("冷静击穿：你的手在抖，身体先一步承受了代价。");
                }

                RebuildRenderTree();
                if (ownsReport)
                {
                    FillPresentationHints(report);
                }
                return report;
            }
            finally
            {
                if (ownsReport)
                    _gameState.CurrentActionReport = null;
            }
        }

        public RoundTransitionFrame BeginRoundTransition()
        {
            if (IsWorldScene(CurrentSceneName))
                throw new InvalidOperationException("交锋回合转换只能在交锋中开始。");
            if (_roundTransitionState != RoundTransitionState.Idle)
                throw new InvalidOperationException($"回合转换已经处于 {_roundTransitionState}。");
            if (_gameState.CurrentActionReport != null)
                throw new InvalidOperationException("动作结算中不能开始交锋回合转换。");

            _roundTransitionState = RoundTransitionState.Resolving;
            _roundTransitionPhase = RoundTransitionPhase.TimeTax;
            _roundTransitionInterpreter = ActiveInterpreter;
            _pendingAutoActions.Clear();
            _nextForcedAction = 0;
            _roundTransitionInterpreter.Eval("(__begin-opponent-rules!)");
            return ResolveNextRoundTransitionFrame();
        }

        public RoundTransitionFrame AdvanceRoundTransition()
        {
            if (_roundTransitionState != RoundTransitionState.Committed)
                throw new InvalidOperationException(
                    $"只有已提交的回合批可以继续；当前状态是 {_roundTransitionState}。");
            _roundTransitionState = RoundTransitionState.Resolving;
            return ResolveNextRoundTransitionFrame();
        }

        private RoundTransitionFrame ResolveNextRoundTransitionFrame()
        {
            while (true)
            {
                switch (_roundTransitionPhase)
                {
                    case RoundTransitionPhase.TimeTax:
                    {
                        // 交锋里每结束一个回合扣一点冷静：时间本身就是代价。它是"结束回合"这个
                        // 动作自己的账，和按键同一拍结清，然后才轮到场上的人回应。
                        // 没有它，"这一回合手气不好，什么都不投，等下一轮重摇"是完全免费的。
                        // 花超的部分由 SpendComposure 自动溢出成伤势；击穿在这儿就倒下的，
                        // 对方规则一条都不跑（OpponentRules 顶部的 pending 检查）。
                        var report = ResolveWithReport(() =>
                        {
                            if (_gameState.HasPendingHospitalization)
                                return;
                            int injuryBefore = _gameState.Team.Injury.Severity;
                            int composureBefore = _gameState.Team.FindActor("player")?.Composure ?? 0;
                            _gameState.Team.SpendComposure("player", EncounterTurnComposureCost);
                            int composureDelta = (_gameState.Team.FindActor("player")?.Composure ?? 0) - composureBefore;
                            int injuryDelta = _gameState.HasPendingHospitalization
                                ? Injury.MaxSeverity - injuryBefore
                                : _gameState.Team.Injury.Severity - injuryBefore;
                            _gameState.CurrentActionReport!.AddEffect(ActionEffectKind.Composure, "冷静", composureDelta,
                                composureDelta > 0 ? ActionEffectTone.Positive : ActionEffectTone.Negative);
                            _gameState.CurrentActionReport!.AddEffect(ActionEffectKind.Injury, "伤势", injuryDelta,
                                injuryDelta > 0 ? ActionEffectTone.Negative : ActionEffectTone.Positive);
                            if (injuryDelta > 0)
                                _gameState.CurrentActionReport!.AddSupplement(
                                    "冷静击穿：你的手在抖，身体先一步承受了代价。");
                        });
                        RebuildRenderTree();
                        _roundTransitionPhase = RoundTransitionPhase.OpponentRules;
                        if (HasVisibleResult(report))
                            return CommitRoundFrame(RoundTransitionPhase.TimeTax, report);
                        continue;
                    }

                    case RoundTransitionPhase.OpponentRules:
                    {
                        if (RoundTransitionLeftEncounter())
                            return FinishRoundTransition();
                        if (_gameState.HasPendingHospitalization)
                        {
                            _roundTransitionPhase = RoundTransitionPhase.RoundEndMaintenance;
                            continue;
                        }
                        bool pending = Utils.IsTruthy(
                            _roundTransitionInterpreter!.Eval("(__opponent-rules-pending?)"));
                        if (!pending)
                        {
                            _roundTransitionPhase = RoundTransitionPhase.RoundEndMaintenance;
                            continue;
                        }

                        var report = ResolveWithReport(() =>
                        {
                            _isResolvingTurnEnd = true;
                            try { _roundTransitionInterpreter.Eval("(__run-next-opponent-rule!)"); }
                            finally { _isResolvingTurnEnd = false; }
                        });
                        RebuildRenderTree();
                        if (HasVisibleResult(report))
                            return CommitRoundFrame(RoundTransitionPhase.OpponentRules, report);
                        // 空规则也可能结束交锋或打倒玩家；回到循环顶部先检查终止。
                        continue;
                    }

                    case RoundTransitionPhase.RoundEndMaintenance:
                    {
                        if (RoundTransitionLeftEncounter())
                            return FinishRoundTransition();
                        // 回合末的收尾：帮手只待这一回合；击穿/倒下在这儿送医。
                        var report = ResolveWithReport(() =>
                        {
                            foreach (var name in _gameState.Team.DismissTemporaryCompanions())
                                _gameState.CurrentActionReport!.AddSupplement($"{name}走了。");
                            if (_gameState.HasPendingHospitalization)
                                ResolvePendingHospitalization(
                                    _gameState.CurrentActionReport!, _roundTransitionInterpreter!,
                                    advanceWorldTurnRules: true);
                        });
                        RebuildRenderTree();
                        _roundTransitionPhase = RoundTransitionLeftEncounter()
                            ? RoundTransitionPhase.Finished
                            : RoundTransitionPhase.NewDice;
                        if (HasVisibleResult(report))
                            return CommitRoundFrame(RoundTransitionPhase.RoundEndMaintenance, report);
                        continue;
                    }

                    case RoundTransitionPhase.NewDice:
                    {
                        if (RoundTransitionLeftEncounter())
                            return FinishRoundTransition();
                        _gameState.Team.RollActionDice(isInEncounter: true);
                        RebuildRenderTree();
                        _roundTransitionPhase = RoundTransitionPhase.ForcedAction;
                        return CommitRoundFrame(RoundTransitionPhase.NewDice,
                            new ActionReport { Type = ActionType.Instant });
                    }

                    case RoundTransitionPhase.ForcedAction:
                    {
                        if (RoundTransitionLeftEncounter())
                            return FinishRoundTransition();
                        if (_nextForcedAction >= _pendingAutoActions.Count)
                            return FinishRoundTransition();
                        var pending = _pendingAutoActions[_nextForcedAction++];
                        if (!CurrentSceneName.Equals(pending.SceneName, StringComparison.OrdinalIgnoreCase))
                            return FinishRoundTransition();
                        var slots = ConsumeAutoActionDice(pending);
                        var autoReport = ResolveWithReport(() => pending.Effect.Call(new List<object>()));
                        if (autoReport.BlockingStorySteps.Count > 0)
                            throw new InvalidOperationException(
                                $"auto-action!: '{pending.Name}' 的效果里不能排阻塞剧情步骤；要说话用 play-banter!。");
                        var frameReport = new ActionReport { Type = ActionType.Instant };
                        frameReport.BlockingStorySteps.Add(BlockingStoryStep.ForResolvedAutoAction(
                            pending.Name, pending.Text, pending.AnchorName, pending.Prelude, slots, autoReport));
                        RebuildRenderTree();
                        return CommitRoundFrame(RoundTransitionPhase.ForcedAction, frameReport);
                    }

                    case RoundTransitionPhase.Finished:
                        return FinishRoundTransition();
                    default:
                        throw new InvalidOperationException($"未知回合阶段：{_roundTransitionPhase}");
                }
            }
        }

        private ActionReport ResolveWithReport(Action action)
        {
            if (_gameState.CurrentActionReport != null)
                throw new InvalidOperationException("回合批开始时已有未关闭的 ActionReport。");
            var report = new ActionReport { Type = ActionType.Instant };
            _gameState.CurrentActionReport = report;
            try { action(); }
            finally { _gameState.CurrentActionReport = null; }
            FillPresentationHints(report);
            return report;
        }

        private RoundTransitionFrame CommitRoundFrame(RoundTransitionPhase phase, ActionReport report)
        {
            _roundTransitionState = RoundTransitionState.Committed;
            return new RoundTransitionFrame { Phase = phase, Report = report, Snapshot = LatestSnapshot };
        }

        private RoundTransitionFrame FinishRoundTransition()
        {
            _roundTransitionState = RoundTransitionState.Finishing;
            _pendingAutoActions.Clear();
            _nextForcedAction = 0;
            _roundTransitionInterpreter = null;
            _roundTransitionPhase = RoundTransitionPhase.Finished;
            _roundTransitionState = RoundTransitionState.Idle;
            return new RoundTransitionFrame
            {
                Phase = RoundTransitionPhase.Finished,
                Report = new ActionReport { Type = ActionType.Instant },
                Snapshot = LatestSnapshot,
            };
        }

        private bool RoundTransitionLeftEncounter()
            => _roundTransitionInterpreter == null
                || !ReferenceEquals(_roundTransitionInterpreter, _encounterInterpreter)
                || IsWorldScene(CurrentSceneName);

        private static bool HasVisibleResult(ActionReport report)
            => report.Effects.Count > 0 || report.BlockingStorySteps.Count > 0
                || report.PostSceneBlockingSteps.Count > 0
                || report.Banter.Count > 0 || report.NarrationIds.Count > 0;

        private static void MergeReport(ActionReport target, ActionReport source)
        {
            target.Effects.AddRange(source.Effects);
            target.BlockingStorySteps.AddRange(source.BlockingStorySteps);
            target.PostSceneBlockingSteps.AddRange(source.PostSceneBlockingSteps);
            target.Banter.AddRange(source.Banter);
            target.NarrationIds.AddRange(source.NarrationIds);
            foreach (var step in source.BlockingStorySteps)
                if (step.ResolvedReport != null)
                    MergeReport(target, step.ResolvedReport);
        }

        /// <summary>
        /// 玩家真的走进一个地点。只在两条真实移动路径上调用：从世界层点开地点卡，
        /// 以及主动「回家」。返回上一层、恢复导航栈、读档重建、快照刷新都不算到达，
        /// 睡醒仍在家里也不算——那些路径不要调这个方法。
        ///
        /// 按名字取节点而不是收客户端手里的对象：客户端持有的是它自己那份快照里的
        /// 节点，未必是引擎当前树里的那一个。节点名全局唯一（AssertUniqueNodeNames），
        /// 按名字查是准的。
        ///
        /// 没有入场节拍时返回 null，客户端照常进入即可。
        /// </summary>
        public ActionReport? EnterPlace(string placeName)
        {
            if (!IsWorldScene(CurrentSceneName))
                return null;
            if (_isExecutingAction)
                throw new InvalidOperationException(
                    $"动作结算过程中不能进入地点(\"{placeName}\")。");
            if (_isEnteringPlace)
                throw new InvalidOperationException(
                    $"入场节拍里不能再进入地点(\"{placeName}\")。");

            var root = CurrentRootNode;
            if (root == null)
                return null;

            // 退回世界层也算一次到达：世界根自己不是子地点，但「你离开过这里」
            // 这类规则（家里的睡觉锁）要听见它。根没有入场节拍，只跑钩子并重建。
            if (root.Name == placeName)
            {
                ActiveInterpreter.Eval($"(on-enter-place \"{placeName}\")");
                RebuildRenderTree();
                return null;
            }

            var place = root.Children.Find(child => child.IsPlace && child.Name == placeName);
            if (place == null)
                return null;

            // 「你走进了这里」这件事本身先告诉内容（早于入场节拍），树随之重建：
            // 有些卡的可用性只看你去过哪儿（家里的睡觉锁）。没有节拍也要重建，
            // 客户端拿到 null 后自己采纳最新快照。
            ActiveInterpreter.Eval($"(on-enter-place \"{placeName}\")");
            if (place.Arrivals.Count == 0)
            {
                RebuildRenderTree();
                return null;
            }

            var report = new ActionReport { Type = ActionType.Instant };
            _gameState.CurrentActionReport = report;
            _isEnteringPlace = true;
            try
            {
                // 表现走报告而不是即时广播：动作外 __spotlight! 立刻显示而 __play-dialogue!
                // 进队列，混用则顺序不保；__play-video! 在无报告时干脆静默丢弃。
                // 报告同时给出原子性——中途抛错就不交出报告，一句也不播。
                foreach (var beat in place.Arrivals)
                {
                    beat.Effect();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"地点 \"{placeName}\" 的入场节拍执行失败：{ex.Message}", ex);
            }
            finally
            {
                _isEnteringPlace = false;
                _gameState.CurrentActionReport = null;
            }

            // Arrival 只负责叙事。写进 Effects 的东西（result-supplement!、加钟、资源增减）
            // 会让入场长得像一次动作结算——「这件事存在」必须在玩家走进来之前
            // 就由日终规则或某个动作建立好。
            if (report.Effects.Count > 0)
                throw new InvalidOperationException(
                    $"地点 \"{placeName}\" 的入场节拍产生了动作结算效果（钟／标注／资源变化）。"
                    + "Arrival 只能播叙事：把状态变更移回日终规则或动作里。");

            // start-encounter 会在入场报告仍被持有时准备好交锋快照；客户端继续显示
            // 刚进入的地点，直到报告中的 dialogue / animation 播完才 adopt 新快照。
            // 没有切场景的普通 arrival 则照常重建世界树并校验地点仍存在。
            if (!IsWorldScene(CurrentSceneName))
                return report;

            RebuildRenderTree();

            var stillThere = CurrentRootNode?.Children.Find(child => child.IsPlace && child.Name == placeName);
            if (stillThere == null)
                throw new InvalidOperationException(
                    $"地点 \"{placeName}\" 的入场节拍把玩家刚走进来的地点从世界里移掉了。");

            // 故意不填 PresentationHints：那是动作的「执行中…」进度条与投骰动画，
            // 走进一个地点没有这两样。空 hints 让播放器直接进阻塞剧情步骤。
            return report;
        }

        public ActionReport ExecuteAction(GameNode node, List<SlottedResource?> slots)
        {
            if (node.Disabled)
                throw new InvalidOperationException($"Node '{node.Name}' is disabled and cannot be executed.");
            Debug.Assert(node.Resolve != null, "Cannot execute action on a node that has no resolve");
            if (node.Resolve.Type == ResolveType.Observe)
            {
                throw new InvalidOperationException("Observe actions must not be executed via ExecuteAction.");
            }

            // 关系支援卡：每场一次由引擎在这里消费，内容脚本不参与——漏写就能无限用的契约不该交给作者。
            if (!string.IsNullOrEmpty(node.SupportId))
            {
                if (IsWorldScene(CurrentSceneName))
                    throw new InvalidOperationException($"支援卡 '{node.Name}' 只能在交锋里用。");
                if (!string.Equals(node.SupportId, _gameState.Team.CarriedSupport, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"支援卡 '{node.Name}' 属于 '{node.SupportId}'，但这一场带的是 '{_gameState.Team.CarriedSupport}'。");
                if (SupportUsedThisEncounter)
                    throw new InvalidOperationException($"支援卡 '{node.Name}'：这一场的支援已经用过了。");
                SupportUsedThisEncounter = true;
            }

            // 一个 action 的后处理规则属于发起该 action 的场景。action 本身可以
            // start/end encounter 并切换 ActiveInterpreter，但不能因此改写规则归属。
            var actionInterpreter = ActiveInterpreter;
            var report = new ActionReport();

            slots = slots ?? new List<SlottedResource?>();
            int reqCount = node.Requires?.Count ?? 0;
            int slotCount = slots.Count;
            if (reqCount != slotCount)
            {
                throw new InvalidOperationException($"Required resource count ({reqCount}) does not match slotted resource count ({slotCount}).");
            }

            if (reqCount > 0)
            {
                var usedDicePerActor = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reqCount; i++)
                {
                    var req = node.Requires![i];
                    var slot = slots[i];
                    if (slot == null)
                    {
                        throw new InvalidOperationException($"Slot {i} for requirement of type '{req.Type}' is null.");
                    }

                    if (req.Type == "item")
                    {
                        if (slot.Type != "item")
                        {
                            throw new InvalidOperationException($"Requirement {i} requires an item, but slot contains '{slot.Type}'.");
                        }
                        if (!string.Equals(slot.ItemId, req.ItemId, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException($"Requirement {i} requires item '{req.ItemId}', but slot contains '{slot.ItemId}'.");
                        }
                        int reqQty = req.Qty;
                        int slotQty = slot.Qty > 0 ? slot.Qty : slot.Value;
                        if (slotQty != reqQty)
                        {
                            throw new InvalidOperationException($"Requirement {i} requires quantity {reqQty}, but slot specifies {slotQty}.");
                        }
                        int owned = _gameState.Inventory.GetCount(req.ItemId);
                        if (owned < reqQty)
                        {
                            throw new InvalidOperationException($"Insufficient inventory: required {reqQty} of '{req.ItemId}', but only have {owned}.");
                        }
                    }
                    else if (req.Type == "die")
                    {
                        if (slot.Type != "die")
                        {
                            throw new InvalidOperationException($"Requirement {i} requires a die, but slot contains '{slot.Type}'.");
                        }
                        
                        string actorId = string.IsNullOrEmpty(slot.ActorId) ? "player" : slot.ActorId;
                        var a = _gameState.Team.FindActor(actorId);
                        if (a == null)
                        {
                            throw new InvalidOperationException($"Actor '{actorId}' for slotted die not found.");
                        }
                        if (a.Status != "active")
                        {
                            throw new InvalidOperationException($"Actor '{actorId}' is not active (status: {a.Status}).");
                        }

                        int slotId = slot.DieIndex >= 0 ? slot.DieIndex : slot.SourceIndex;
                        int idx = a.ActionDiceSlotIds.IndexOf(slotId);
                        if (idx < 0)
                        {
                            throw new InvalidOperationException($"Action slot {slotId} has no available die for actor '{actorId}'.");
                        }

                        if (a.ActionDice[idx] != slot.Value)
                        {
                            throw new InvalidOperationException($"Die value mismatch: slotted die has value {slot.Value}, but actor's slot {slotId} has value {a.ActionDice[idx]}.");
                        }

                        if (!usedDicePerActor.ContainsKey(actorId))
                        {
                            usedDicePerActor[actorId] = new HashSet<int>();
                        }
                        if (usedDicePerActor[actorId].Contains(slotId))
                        {
                            throw new InvalidOperationException($"Die in slot {slotId} for actor '{actorId}' is slotted more than once.");
                        }
                        usedDicePerActor[actorId].Add(slotId);
                    }
                }
            }

            string activeActorId = "player";
            var actorDieSlot = slots.Find(s => s != null && s.Type == "die");
            if (actorDieSlot != null)
            {
                activeActorId = actorDieSlot.ActorId;
                if (string.IsNullOrEmpty(activeActorId))
                {
                    activeActorId = "player";
                }
            }

            var actor = _gameState.Team.FindActor(activeActorId);
            if (actor == null)
            {
                throw new InvalidOperationException($"Actor '{activeActorId}' not found.");
            }
            if (actor.Status != "active")
            {
                throw new InvalidOperationException($"Actor '{activeActorId}' is not active (status: {actor.Status}).");
            }

            // 谁能出手由「他在不在队里」决定（TeamState.IsOnStage），不在这里按场景类型另判一次。
            string mode = CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase) ? "world" : "encounter";

            var context = new ActionExecutionContext
            {
                ActorId = activeActorId,
                Mode = mode,
                SlottedResources = slots
            };
            _gameState.CurrentContext = context;
            _gameState.CurrentActionReport = report;
            _turnEndedDuringAction = false;
            bool wasExecutingAction = _isExecutingAction;
            _isExecutingAction = true;

            try
            {
                Action consumeResources = () => {
                    var diceSlotsByActor = new Dictionary<string, List<SlottedResource>>();
                    foreach (var s in slots)
                    {
                        if (s != null && s.Type == "die")
                        {
                            string aid = string.IsNullOrEmpty(s.ActorId) ? "player" : s.ActorId;
                            if (!diceSlotsByActor.ContainsKey(aid))
                            {
                                diceSlotsByActor[aid] = new List<SlottedResource>();
                            }
                            diceSlotsByActor[aid].Add(s);
                        }
                    }

                    foreach (var kvp in diceSlotsByActor)
                    {
                        var a = _gameState.Team.FindActor(kvp.Key);
                        if (a != null)
                        {
                            var sorted = kvp.Value;
                            foreach (var s in sorted)
                            {
                                int slotId = s.DieIndex >= 0 ? s.DieIndex : s.SourceIndex;
                                int idx = a.ActionDiceSlotIds.IndexOf(slotId);
                                if (idx >= 0)
                                {
                                    a.ActionDice.RemoveAt(idx);
                                    a.ActionDiceSlotIds.RemoveAt(idx);
                                }
                            }
                        }
                    }

                    foreach (var s in slots)
                    {
                        if (s != null && s.Type == "item")
                        {
                            int cur = _gameState.Inventory.GetCount(s.ItemId);
                            _gameState.Inventory.SetCount(s.ItemId, Math.Max(0, cur - s.Qty));
                        }
                    }
                };

                if (node.Resolve.Type == ResolveType.Instant)
                {
                    report.Type = ActionType.Instant;
                    node.Resolve.Outcome?.Effect?.Invoke();
                    consumeResources();
                }
                else if (node.Resolve.Type == ResolveType.Roll)
                {
                    report.Type = ActionType.Roll;
                    int chosenDieVal = actorDieSlot != null ? actorDieSlot.Value : 1;
                    report.ChosenDieValue = chosenDieVal;

                    string skillName = node.Resolve.SkillName;
                    int skillLevel;
                    if (actor.Stats.TryGetValue(skillName, out var sVal))
                    {
                        skillLevel = sVal;
                    }
                    else
                    {
                        throw new InvalidOperationException($"未知技能/属性: {skillName} 对于角色 {actor.Id}");
                    }

                    var rand = GameRandom.Instance;

                    var modifiers = new List<DifficultyModifierInfo>(node.Resolve.DifficultyModifiers);
                    // 伤势只压主角被打中的那一项能力，并且走和「势力敌视 −1」「非法 −2」
                    // 同一条可见修正——玩家在投骰前就看得见它，不做暗扣。
                    var injuryMod = _gameState.Team.Injury.ModifierFor(actor.Role, skillName);
                    if (injuryMod != null) modifiers.Add(injuryMod);
                    // 旧伤同理：永久，治不掉，同样摆在明面上。
                    var scarMod = _gameState.Team.Scars.ModifierFor(actor.Role, skillName);
                    if (scarMod != null) modifiers.Add(scarMod);
                    int modifierSum = 0;
                    foreach (var mod in modifiers)
                    {
                        modifierSum += mod.Value;
                    }
                    report.DifficultyModifiers = modifiers;
                    report.SkillLevel = skillLevel;
                    report.ModifierTotal = modifierSum;

                    int fateDie = rand.Next(1, 7);
                    report.PreparedValue = FateStrip.PreparedValue(chosenDieVal, skillLevel, modifierSum);
                    report.FateDieValue = fateDie;
                    report.Outcome = FateStrip.Resolve(chosenDieVal, skillLevel, modifierSum, fateDie);

                    if (report.Outcome == RollOutcome.Fail)
                    {
                        node.Resolve.FailOutcome?.Effect?.Invoke();
                    }
                    else if (report.Outcome == RollOutcome.Neutral)
                    {
                        node.Resolve.NeutralOutcome?.Effect?.Invoke();
                    }
                    else
                    {
                        node.Resolve.SuccessOutcome?.Effect?.Invoke();
                    }

                    consumeResources();
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported resolve type: {node.Resolve.Type}");
                }

                if (!_turnEndedDuringAction)
                {
                    if (_gameState.HasPendingHospitalization)
                    {
                        ResolvePendingHospitalization(report, actionInterpreter, advanceWorldTurnRules: true);
                    }
                    else
                    {
                        RunOnActionRules(actionInterpreter);
                        if (_gameState.HasPendingHospitalization)
                            ResolvePendingHospitalization(report, actionInterpreter, advanceWorldTurnRules: true);
                    }
                }
                else
                {
                    _turnEndedDuringAction = false;
                }
            }
            finally
            {
                _gameState.CurrentContext = null;
                _gameState.CurrentActionReport = null;
                _isExecutingAction = wasExecutingAction;
            }

            ApplyPendingSceneDiceRoll();
            RebuildRenderTree();
            FillPresentationHints(report);
            return report;
        }

        // 「执行中」进度条表达的是**这件事花了你一段时间**。判定和直接执行需要的时长不一样：
        //
        //   判定：0.3 秒。它后面还接着骰子扫掠和结果，是一个序列的开头——
        //         这一段只要够让玩家看见"开始了"，长了反而拖慢每一次投骰。
        //   直接执行：0.4 秒。它后面什么都没有。用 0.3 的话，进度条从出现到消失
        //         比一次眨眼还短，玩家读到的是"点了一下，数字变了"，
        //         时间流逝这件事根本没被演出来。再长就黏手了——睡觉、看花、用药
        //         这些是一天里要点好几次的高频动作。
        private const float ExecuteProgressRollSeconds = 0.3f;
        private const float ExecuteProgressInstantSeconds = 0.4f;

        private static void FillPresentationHints(ActionReport report)
        {
            bool isRoll = report.Type == ActionType.Roll;
            var hints = new List<PresentationHint>
            {
                new PresentationHint
                {
                    Kind = PresentationHintKind.ExecuteProgress,
                    Text = "执行中...",
                    DurationSeconds = isRoll
                        ? ExecuteProgressRollSeconds
                        : ExecuteProgressInstantSeconds,
                },
            };

            if (isRoll)
            {
                hints.Add(new PresentationHint
                {
                    Kind = PresentationHintKind.RollDice,
                    DurationSeconds = 0f,
                });
            }

            report.PresentationHints = hints;
        }
    }
}
