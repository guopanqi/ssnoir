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
        private bool _hasPendingSceneDiceRoll;
        // 出发去交锋前扣下的世界骰池；回到世界时还回去。null = 没有可还的（新开局/读档/新一天）。
        private Dictionary<string, (List<int> Dice, List<int> SlotIds)>? _stashedWorldDice;
        private bool _pendingSceneIsEncounter;
        private Procedure? _encounterCallback;
        private bool _encounterEnded;
        private readonly HashSet<string> _seenWorldPlaces = new HashSet<string>(StringComparer.Ordinal);
        private bool _hasWorldPlaceBaseline;

        public event Action? OnSceneLoaded;
        public event Action? OnWorldRefreshed;

        public GameNode? CurrentRootNode { get; private set; }
        public List<GameClock> CurrentClocks { get; private set; } = new List<GameClock>();
        /// <summary>本帧的卷宗（世界场景才有，交锋里是空的）。</summary>
        public List<DossierEntry> CurrentDossier { get; private set; } = new List<DossierEntry>();

        /// <summary>玩家钉在地图上的那条线。是阅读偏好，不是故事状态，所以放在全局里跟着存档走。</summary>
        public const string DossierPinKey = "dossier-pin";

        /// <summary>
        /// 钉住一条线；传空字符串＝取消钉住，退回默认（第一条还没了结的委托）。
        /// 不重建渲染树：这是阅读偏好，不改变世界，客户端下一帧直接读全局即可。
        /// </summary>
        public void SetDossierPin(string id) => _gameState.Set(DossierPinKey, id ?? string.Empty);
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
            _worldInterpreter = null;
            _encounterInterpreter = null;
            _encounterSceneName = string.Empty;
            _encounterCallback = null;
            _encounterEnded = false;
            _turnEndedDuringAction = false;
            _hasPendingSceneDiceRoll = false;
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
                LoadScene(sceneName);
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
            _encounterEnded = false;
            LoadScene(name);
        }

        /// <summary>最后一次交锋结算交回来的值。正式流程由回调消费，这里留一份供离线试跑读取。</summary>
        public object? LastEncounterResult { get; private set; }

        public void EndEncounter(object? result = null)
        {
            if (_encounterEnded) return;
            _encounterEnded = true;
            LastEncounterResult = result;

            var cb = _encounterCallback;
            _encounterCallback = null;

            if (cb != null)
                cb.Call(new List<object> { result ?? Symbol.FromString("none") });

            LoadScene("world");
        }

        private void AbortEncounterForRetry()
        {
            if (_encounterEnded) return;
            _encounterEnded = true;
            LastEncounterResult = Symbol.FromString("倒下");
            _encounterCallback = null;
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
        /// 所以这里记下进入本方法时的步骤位置，把倒下与送医**插进**那个位置，
        /// 让顺序回到：你倒下了 → 诊所醒来 → 那一夜的结局。
        /// </summary>
        private void ResolvePendingHospitalization(
            ActionReport report,
            SchemeInterpreter collapseInterpreter,
            bool advanceWorldTurnRules)
        {
            if (!_gameState.HasPendingHospitalization)
                return;

            // 退场回调往这张报告里追加的每一步，都排在这个位置之后。
            int collapseStepIndex = report.BlockingStorySteps.Count;

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

            // 医院位置切换发生在原动作演出之后；后续日终来信/对白也都在诊所画面上播放。
            // 插在 collapseStepIndex：见方法注释——倒下是原因，退场回调讲的是结果。
            report.BlockingStorySteps.Insert(collapseStepIndex,
                BlockingStoryStep.ForEnterPlace("诊所"));
            report.BlockingStorySteps.Insert(collapseStepIndex + 1, BlockingStoryStep.ForSpotlight(
                new SpotlightCard { Title = "你倒下了", Subtitle = hospitalization.Text }));

            _stashedWorldDice = null;
            if (advanceWorldTurnRules)
            {
                var world = _worldInterpreter
                    ?? throw new InvalidOperationException("Hospitalization requires the world interpreter.");
                world.Eval("(on-turn-end)");
            }

            foreach (var name in _gameState.Team.BeginCityDay())
                report.AddNote($"{name}缓过来了，今天照旧跟着你。");

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

            CurrentCarryNodes = BuildCarryNodes(rootNode, active);

            CurrentDossier = IsWorldScene(CurrentSceneName)
                ? NodeConverter.ConvertDossier(active.Eval("(get-dossier)"))
                : new List<DossierEntry>();

            LatestSnapshot = BuildPresentationSnapshot(rootNode);
            
            OnWorldRefreshed?.Invoke();
        }

        /// <summary>
        /// 随身动作（烟、酒）：玩家自己带进交锋的东西。没有哪个交锋脚本声明它们——
        /// 那样每写一场就得重抄一遍——所以由引擎在这里统一取一份（内容见 engine.scm 的 carry-nodes），
        /// 只在手里真有那件东西时出现，城市里不给。
        ///
        /// 它们**不进渲染树**：进了树就会被排进场上的卡片区，读起来像是这一场的事。
        /// 客户端从 CarryNodes 单独取，画成从那件物品引出去的一张小卡。
        /// 但它们是**货真价实的动作节点**——同一个骰位、同一个 ExecuteAction，
        /// 放骰子的表现形式和别的卡一模一样，这正是它们不能是一个角落按钮的原因。
        /// </summary>
        public IReadOnlyList<GameNode> CurrentCarryNodes { get; private set; } = new List<GameNode>();

        private List<GameNode> BuildCarryNodes(GameNode rootNode, SchemeInterpreter active)
        {
            if (IsWorldScene(CurrentSceneName)) return new List<GameNode>();

            var carried = NodeConverter.ConvertList(active.Eval("(carry-nodes)"), active.RawInterpreter);
            var treeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectNodeNames(rootNode, treeNames);
            foreach (var node in carried)
            {
                if (string.IsNullOrEmpty(node.CarryItemId))
                    throw new InvalidOperationException(
                        $"随身动作 '{node.Name}' 没有声明 :carry-item——客户端不知道该把它从哪件物品上引出来。");
                // 槽位状态按节点名索引，重名会让随身卡和场上某张卡共用同一份槽位。
                if (treeNames.Contains(node.Name))
                    throw new InvalidOperationException(
                        $"随身动作 '{node.Name}' 和交锋 '{CurrentSceneName}' 里的一张卡重名。");
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

            var relations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string circle in GameState.Circles)
                relations[circle] = _gameState.Get<int>("relation:" + circle);
            var relationUnlocks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var relationBandNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string faction in relations.Keys)
            {
                foreach (string tier in RelationScale.PositiveTiers)
                {
                    relationUnlocks[$"{faction}:{tier}"] =
                        _gameState.Get<string>($"relation-goal:{faction}:{tier}", "当前无新增动作");
                    relationBandNames[$"{faction}:{tier}"] =
                        _gameState.Get<string>($"relation-band-name:{faction}:{tier}", tier);
                }
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
                Relations = relations,
                RelationUnlocks = relationUnlocks,
                RelationBandNames = relationBandNames,
                Dossier = CurrentDossier,
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
            if (_isExecutingAction)
                _turnEndedDuringAction = true;
            bool isInEncounter = !CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase);
            bool ownsReport = _gameState.CurrentActionReport == null;
            var report = _gameState.CurrentActionReport ?? new ActionReport { Type = ActionType.Instant };
            if (ownsReport)
                _gameState.CurrentActionReport = report;
            // 无论回合是玩家直接按休息结束的，还是脚本在动作里调 end-turn! 结束的，
            // 拿到这份报告的人都该知道时间翻页了。
            report.TurnEnded = true;
            try
            {
                int injuryBefore = _gameState.Team.Injury.Severity;
                int composureBefore = _gameState.Team.FindActor("player")?.Composure ?? 0;

                // 交锋里每结束一个回合扣一点冷静：时间本身就是代价。
                // 没有它，"这一回合手气不好，什么都不投，等下一轮重摇"是完全免费的，
                // 最优解就变成只投高点数——玩家不再需要在"现在动手"和"再等等"之间取舍。
                // 花超的部分由 SpendComposure 自动溢出成伤势，那正是"熬太久要还的"。
                if (isInEncounter)
                    _gameState.Team.SpendComposure("player", EncounterTurnComposureCost);
                int automaticComposureDelta = (_gameState.Team.FindActor("player")?.Composure ?? 0) - composureBefore;
                int automaticInjuryDelta = _gameState.HasPendingHospitalization
                    ? Injury.MaxSeverity - injuryBefore
                    : _gameState.Team.Injury.Severity - injuryBefore;

                var turnInterpreter = ActiveInterpreter;
                if (!_gameState.HasPendingHospitalization)
                    turnInterpreter.Eval("(on-turn-end)");

                if (_gameState.HasPendingHospitalization)
                {
                    // 城市 EndTurn 的世界日历规则固定最先执行；若后续日终规则意外打倒玩家，
                    // 日期已经推进，不能再跑一遍。交锋 EndTurn 则还需要补跑一次世界日终。
                    ResolvePendingHospitalization(report, turnInterpreter, advanceWorldTurnRules: isInEncounter);
                    report.AddEffect(
                        ActionEffectKind.Composure, "冷静", automaticComposureDelta,
                        automaticComposureDelta > 0 ? ActionEffectTone.Positive : ActionEffectTone.Negative);
                    report.AddEffect(
                        ActionEffectKind.Injury, "伤势", automaticInjuryDelta,
                        automaticInjuryDelta > 0 ? ActionEffectTone.Negative : ActionEffectTone.Positive);
                    if (automaticInjuryDelta > 0)
                        report.AddNote("冷静击穿：你的手在抖，身体先一步承受了代价。");

                    RebuildRenderTree();
                    if (ownsReport)
                        FillPresentationHints(report);
                    return report;
                }

                bool stillInSameMode = isInEncounter == !CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase);
                if (stillInSameMode)
                {
                    _gameState.Team.RollActionDice(isInEncounter);
                }

                // 城市里过完一天：同伴的冷静和行动骰一样按天重发（见 TeamState.BeginCityDay）。
                // 放在 on-turn-end 之后、和重新发骰同一处，因为它们是同一件事——新的一天的份额。
                if (!isInEncounter && stillInSameMode)
                {
                    foreach (var name in _gameState.Team.BeginCityDay())
                        report.AddNote($"{name}缓过来了，今天照旧跟着你。");
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
                    report.AddNote("冷静击穿：你的手在抖，身体先一步承受了代价。");
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

            var place = root.Children.Find(child => child.IsPlace && child.Name == placeName);
            if (place == null || place.Arrivals.Count == 0)
                return null;

            var report = new ActionReport { Type = ActionType.Instant };
            _gameState.CurrentActionReport = report;
            _isEnteringPlace = true;
            try
            {
                // 表现走报告而不是即时广播：动作外 __spotlight! 立刻显示而 __play-dialogue!
                // 进队列，混用则顺序不保；__play-animation! 在无报告时干脆静默丢弃。
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

            // Arrival 只负责叙事。写进 Effects 的东西（result-note!、加钟、资源增减）
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
                    ApplyOutcomePresentation(report, node.Resolve.Outcome);
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
                        ApplyOutcomePresentation(report, node.Resolve.FailOutcome);
                    }
                    else if (report.Outcome == RollOutcome.Neutral)
                    {
                        node.Resolve.NeutralOutcome?.Effect?.Invoke();
                        ApplyOutcomePresentation(report, node.Resolve.NeutralOutcome);
                    }
                    else
                    {
                        node.Resolve.SuccessOutcome?.Effect?.Invoke();
                        ApplyOutcomePresentation(report, node.Resolve.SuccessOutcome);
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

        private static void ApplyOutcomePresentation(ActionReport report, ActionOutcome? outcome)
        {
            if (outcome == null || !outcome.HasText)
            {
                return;
            }

            report.OutcomePresentation = outcome.Presentation;
        }
    }
}
