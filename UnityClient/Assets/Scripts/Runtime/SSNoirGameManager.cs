#nullable enable
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SSNoir.Core;
using SSNoir.IMGUI;

namespace SSNoir
{
    public class SelectedResource
    {
        public string Type { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int Value { get; set; }
        public int SourceIndex { get; set; } = -1;
        public string ActorId { get; set; } = string.Empty;
        public int DieIndex { get; set; } = -1;
    }

    internal enum ResourceDragOriginKind
    {
        None,
        Hand,
        Slot
    }

    public class SSNoirGameManager : MonoBehaviour
    {
        private const string ReduceMotionSettingKey = "reduceMotion";
        private const string MusicVolumeSettingKey = "musicVolume";
        private const string SfxVolumeSettingKey = "sfxVolume";
        private const string DialogueVolumeSettingKey = "dialogueVolume";
        private const string WorldRootNodeName = "世界";
        // Font files follow: <family>-Regular.ttf / <family>-SemiBold.ttf.
        private const string FontFamily = "SourceHanSerifCN";
        private const string RegularFontResourcePath = "Fonts/" + FontFamily + "-Regular";
        private const string SemiboldFontResourcePath = "Fonts/" + FontFamily + "-SemiBold";
#if UNITY_EDITOR
        private const string RegularFontAssetPath = "Assets/Resources/Fonts/" + FontFamily + "-Regular.ttf";
        private const string SemiboldFontAssetPath = "Assets/Resources/Fonts/" + FontFamily + "-SemiBold.ttf";
#endif

        [Header("标题灯牌配色")]
        [Tooltip("灯管中心颜色；标题页逐帧读取，可在 Play Mode 实时试色。")]
        [SerializeField] private Color titleSignCore = new Color(1f, 0.945f, 0.827f); // #FFF1D3
        [Tooltip("Belleville 主招牌的外部光晕颜色。")]
        [SerializeField] private Color titleSignGlow = new Color(0.788f, 0.608f, 0.408f); // #C99B68
        [Tooltip("THE BALLADS OF 小标题的光晕颜色。")]
        [SerializeField] private Color titleHeadingGlow = new Color(0.643f, 0.694f, 0.718f); // #A4B1B7
        [Tooltip("标题灯牌光晕强度：0 关闭光晕，1 为默认；不影响灯芯亮度。Play Mode 实时生效。")]
        [Range(0f, 2f)] [SerializeField] private float titleSignGlowStrength = 1f;
        public float TitleSignGlowStrength => titleSignGlowStrength;
        public Color TitleSignCore => titleSignCore;
        public Color TitleSignGlow => titleSignGlow;
        public Color TitleHeadingGlow => titleHeadingGlow;

        [Header("Camera Drag Settings")]
        [Tooltip("Pan 拖拽：1 = 拖一像素地面走一像素（按视线落点距离换算），大于 1 更快。")]
        [SerializeField] private float panSpeedMultiplier = 1f;

        private GameState _gameState = null!;
        private SceneManager _sceneManager = null!;
        private UnityScriptLoader _scriptLoader = null!;
        private SceneDirectory? _sceneDirectory;
        private IMGUIWorldRenderer _renderer = null!;
        private StageTransitionController _stageController = null!;
        private CutscenePlayer _cutscenePlayer = null!;
        private TitleScreen _titleScreen = null!;
        private CityOutlineState? _cityOutlines;
        private AmbientMusic _ambientMusic = null!;

        private SSNoirCameraManager _cameraManager = null!;
        private Font? _regularFont;
        private Font? _semiboldFont;

        // Core Gameplay / Interaction State
        private string _focusedNodeName = string.Empty;
        private readonly List<GameNode> _navigationStack = new List<GameNode>();
        private List<GameNode> _visibleNodes = new List<GameNode>();
        private SelectedResource? _selectedResource;
        // 休息键的两道闸：结算中（硬锁）与结算后的短冷却（软锁，见 CanRest）。
        private bool _isEndingTurn;
        private RoundTransitionPhase? _activeRoundTransitionPhase;
        private bool _stateTainted;
        private float _restCooldownUntil;
        private const float RestCooldownSeconds = 2f;

        private bool _resourceDragActive;
        private bool _resourceDropHandled;
        private ResourceDragOriginKind _resourceDragOrigin = ResourceDragOriginKind.None;
        private string _resourceOriginNodeName = string.Empty;
        private int _resourceOriginSlotIndex = -1;
        private SlottedResource? _resourceOriginSlotResource;

        private readonly Dictionary<string, List<SlottedResource?>> _nodeSlots = new Dictionary<string, List<SlottedResource?>>();
        private readonly HashSet<string> _flippedNodes = new HashSet<string>();
        private PresentationSnapshot _displayedSnapshot = new PresentationSnapshot();
        private const float ItemGainPulseDuration = 1.0f;
        private readonly Dictionary<string, float> _itemGainPulseUntil =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private bool _inventoryPulseBaselineReady;

        // 焦点上下文切换的演出窗口期：数据已切换，卡片还没换脸。见 BeginIncomingFocusContext。
        private bool _incomingFocusContextActive;
        private Cinemachine.CinemachineVirtualCamera? _incomingFocusContextCamera;
        private Cinemachine.CinemachineVirtualCamera? _outgoingFocusContextCamera;
        private int _cityOutlineClearRequest;
        private bool _incomingFocusCrossesStagePortal;
        // 回到世界视角时，建筑的 High 不能在相机离开近景前立刻撤掉；否则玩家会看到
        // High -> Low 的替换瞬间。每次新的焦点请求都会使旧的清理请求失效。


        // ── 冷静 / 伤势的变化脉冲 ────────────────────────────────────────
        //
        // 这两条轴讲的是同一件事的两段：冷静是缓冲，缓冲用完了才轮到身体。
        // 挨一下的时候，冷静条在**缩短**、伤势条在**变长**——两个相反的运动，
        // 但发生的是同一件事。统一它们的不是方向（方向的差别正是这套设计要讲的话），
        // 而是**颜色和节奏**：同一件坏事在两条轴上闪的是同一个白、同一条曲线。
        //
        // 节奏分三层，一层套一层：
        //
        //   1. 一格一格来。掉 3 点冷静就是 3、2、1 依次熄灭，不是三格一起闪。
        //      数量本身要能被**数出来**——同时闪只告诉玩家"少了一截"，
        //      挨个闪才告诉他"少了三点"。填格子的方向相反：从低位往高位亮上去。
        //   2. 还没轮到的那一格**保持旧样子**。冷静掉的时候它还亮着，伤势涨的时候
        //      它还空着，等轮到了才带着闪光换过来。否则格子先变、光后到，队列就散了。
        //   3. 击穿时两条轴接力：冷静那一串走完，隔 RelayDelay 才轮到伤势。
        //      于是读起来是"白光顺着冷静条退下去，跳到伤势条上"，一个连续动作。
        public enum VitalPulseTone { Loss, Gain }

        public readonly struct VitalPulse
        {
            public readonly float Strength;      // 1 → 0 的衰减强度
            public readonly VitalPulseTone Tone;
            public readonly bool Pending;        // 还没轮到它：这一格先按旧样子画

            public VitalPulse(float strength, VitalPulseTone tone, bool pending)
            {
                Strength = strength;
                Tone = tone;
                Pending = pending;
            }
        }

        private sealed class VitalPulseState
        {
            public int LoCell;                   // 变化覆盖的格子区间 [LoCell, HiCell)
            public int HiCell;
            public bool FillingUp;               // true＝格子在亮起来，false＝在熄灭
            public float StartTime;              // 队列里第一格的起闪时刻
            public float Duration;               // 单格闪多久
            public VitalPulseTone Tone;

            // 队列里最后一格是什么时候开始闪的。接力要等的是整串走完，不是第一格。
            public float LastCellStartTime => StartTime + (HiCell - LoCell - 1) * VitalPulseStepDelay;
        }

        // 变坏要硬（一下子过曝），变好要软（松一口气）。两者都得**看得清**：
        // 第一版 0.25/0.40 在实机上一闪就没，等于没做；第二版 0.45/0.60/0.14 能看见但
        // 还是赶——掉三点冷静半秒就走完，眼睛刚从卡上移过来就结束了。再放慢一档。
        private const float VitalPulseLossDuration = 0.60f;
        private const float VitalPulseGainDuration = 0.80f;
        private const float VitalPulseStepDelay = 0.20f;   // 一格接一格的间隔
        private const float VitalPulseRelayDelay = 0.30f;  // 冷静那串走完 → 伤势起闪

        private readonly Dictionary<string, VitalPulseState> _composurePulses =
            new Dictionary<string, VitalPulseState>(StringComparer.OrdinalIgnoreCase);
        private VitalPulseState? _injuryPulse;
        private readonly Dictionary<string, int> _lastComposure =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _latestVitalLossAt =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private int _lastInjurySeverity;
        private bool _vitalPulseBaselineReady;

        // 变化那一下整条读数的"弹"（0→1→0，给调用方缩放用）。眼睛先被条的动吸住，
        // 再去看格子——格子的脉冲接着说掉了几格。不写数字。
        private const float VitalPopDuration = 0.55f;

        public float ComposurePop(string actorId)
            => VitalPop(_composurePulses.TryGetValue(actorId, out var state) ? state : null);

        public float InjuryPop() => VitalPop(_injuryPulse);

        // 半身像只需要知道「刚才是否挨了一下」。具体怎样闪属于表现层，
        // 这里保留真实状态变化发生的时刻，避免 HUD 再维护一份快照差分。
        public float VitalLossAge(string actorId)
        {
            return _latestVitalLossAt.TryGetValue(actorId, out float startedAt)
                ? Time.unscaledTime - startedAt
                : float.PositiveInfinity;
        }

        private static float VitalPop(VitalPulseState? state)
        {
            if (state == null)
                return 0f;
            float t = Time.unscaledTime - state.StartTime;
            if (t < 0f || t >= VitalPopDuration)
                return 0f;
            const float attack = 0.10f;
            return t < attack ? t / attack : 1f - Mathf.Pow((t - attack) / (VitalPopDuration - attack), 0.7f);
        }

        /// <summary>某人冷静条上第 cellIndex 格此刻的脉冲。</summary>
        public VitalPulse GetComposureCellPulse(string actorId, int cellIndex)
            => EvaluateVitalPulse(
                _composurePulses.TryGetValue(actorId, out var state) ? state : null, cellIndex);

        /// <summary>伤势条上第 cellIndex 格此刻的脉冲。伤势是队伍级的，只有一条。</summary>
        public VitalPulse GetInjuryCellPulse(int cellIndex)
            => EvaluateVitalPulse(_injuryPulse, cellIndex);

        private static VitalPulse EvaluateVitalPulse(VitalPulseState? state, int cellIndex)
        {
            if (state == null || cellIndex < state.LoCell || cellIndex >= state.HiCell)
                return default;

            // 排队序号：亮起来的从低位数起，熄灭的从高位数起——两边都是"沿着变化的方向走"。
            int order = state.FillingUp
                ? cellIndex - state.LoCell
                : state.HiCell - 1 - cellIndex;
            float elapsed = Time.unscaledTime - (state.StartTime + order * VitalPulseStepDelay);

            if (elapsed < 0f)
                return new VitalPulse(0f, state.Tone, pending: true);
            if (elapsed >= state.Duration)
                return default;
            return new VitalPulse(1f - elapsed / state.Duration, state.Tone, pending: false);
        }

        private void UpdateVitalPulses(PresentationSnapshot incoming)
        {
            float now = Time.unscaledTime;
            // 击穿要等的是**主角**那串冷静走完。同伴掉冷静和伤势没有因果关系。
            float leadComposureQueueEnd = float.NegativeInfinity;

            if (_vitalPulseBaselineReady)
            {
                foreach (var actor in incoming.Actors)
                {
                    if (!_lastComposure.TryGetValue(actor.Id, out int before) || before == actor.Composure)
                        continue;
                    bool loss = actor.Composure < before;
                    var pulse = new VitalPulseState
                    {
                        LoCell = Mathf.Min(actor.Composure, before),
                        HiCell = Mathf.Max(actor.Composure, before),
                        FillingUp = !loss,
                        StartTime = now,
                        Duration = loss ? VitalPulseLossDuration : VitalPulseGainDuration,
                        Tone = loss ? VitalPulseTone.Loss : VitalPulseTone.Gain,
                    };
                    _composurePulses[actor.Id] = pulse;
                    // 固定的回合时间税不是人物挨打；数值条照常变化，但半身像不演受击。
                    if (loss && _activeRoundTransitionPhase != RoundTransitionPhase.TimeTax)
                        _latestVitalLossAt[actor.Id] = now;
                    if (loss && string.Equals(actor.Id, "player", StringComparison.OrdinalIgnoreCase))
                        leadComposureQueueEnd = pulse.LastCellStartTime;
                }

                if (incoming.InjurySeverity != _lastInjurySeverity)
                {
                    bool worse = incoming.InjurySeverity > _lastInjurySeverity;
                    if (worse)
                        _latestVitalLossAt["player"] = now;
                    // 只有「主角冷静掉了、同时伤势涨了」才是击穿，才排接力；
                    // 枪伤那种直接见血的不等，它本来就不是从缓冲溢出来的。
                    bool relay = worse && !float.IsNegativeInfinity(leadComposureQueueEnd);
                    _injuryPulse = new VitalPulseState
                    {
                        LoCell = Mathf.Min(incoming.InjurySeverity, _lastInjurySeverity),
                        HiCell = Mathf.Max(incoming.InjurySeverity, _lastInjurySeverity),
                        FillingUp = worse,
                        StartTime = relay ? leadComposureQueueEnd + VitalPulseRelayDelay : now,
                        Duration = worse ? VitalPulseLossDuration : VitalPulseGainDuration,
                        Tone = worse ? VitalPulseTone.Loss : VitalPulseTone.Gain,
                    };
                }
            }

            _lastComposure.Clear();
            foreach (var actor in incoming.Actors)
                _lastComposure[actor.Id] = actor.Composure;
            _lastInjurySeverity = incoming.InjurySeverity;
            _vitalPulseBaselineReady = true;
        }

        // Public properties
        public GameState GameState => _gameState;
        public SceneManager SceneManager => _sceneManager;
        public SceneDirectory? SceneDirectory => _sceneDirectory;
        public PresentationSnapshot DisplayedSnapshot => _displayedSnapshot;
        public string FocusedNodeName => _focusedNodeName;
        public List<GameNode> NavigationStack => _navigationStack;
        public List<GameNode> VisibleNodes => _visibleNodes;
        public SelectedResource? SelectedResource => _selectedResource;
        public Font? ChineseFont => _regularFont;
        public Font? SemiboldFont => _semiboldFont;
        public SSNoirCameraManager CameraManager => _cameraManager;
        public StageTransitionController StageController => _stageController;
        public CutscenePlayer Cutscene => _cutscenePlayer;
        public TitleScreen Title => _titleScreen;
        public bool IsInputLocked => _renderer != null && _renderer.IsInputLocked;
        public bool PointerOverUI => _renderer != null && _renderer.PointerOverUI;
        public Cinemachine.CinemachineVirtualCamera? CurrentFocusCamera => ResolveCurrentFocusCamera();
        // 过场需要下一场景的回程机位；旧快照的卡片仍须按离场前的镜头排布。
        public Cinemachine.CinemachineVirtualCamera? DisplayedFocusCamera =>
            _incomingFocusContextActive ? _outgoingFocusContextCamera : ResolveCurrentFocusCamera();
        public bool IncomingFocusCrossesStagePortal => _incomingFocusCrossesStagePortal;
        public bool IsStateTainted => _stateTainted;

        /// <summary>
        /// 最近一次落地的快照有没有换根节点（世界 ↔ 交锋）。落地时随 ReconcileContext 传给 Portal，
        /// 用来分辨"剧情带着镜头走"（进出交锋，不听减少动画）和"玩家自己翻页"（世界里点进一扇门，
        /// 不落快照、走轮询，听减少动画）。只在落地那一刻有意义，别在别处读它当"现在"。
        /// </summary>
        private bool LastSnapshotChangedRoot { get; set; }


        /// <summary>物品数量最近增加时返回 1→0 的亮起强度；读档和新游戏基线不触发。</summary>
        public float GetItemGainPulse(string itemName)
        {
            if (!_itemGainPulseUntil.TryGetValue(itemName, out float until))
                return 0f;
            float remaining = until - Time.unscaledTime;
            if (remaining <= 0f)
            {
                _itemGainPulseUntil.Remove(itemName);
                return 0f;
            }
            return Mathf.Clamp01(remaining / ItemGainPulseDuration);
        }

        public string? CurrentStageContextId
        {
            get
            {
                return ResolveCurrentStageContextId();
            }
        }

        /// <summary>某个动作此刻的「执行中」进度，供没有执行钮的控件自己画时间流逝。</summary>
        public (bool IsExecuting, float Progress, string Text) GetExecutionState(string actionName)
            => _renderer != null ? _renderer.GetExecutionState(actionName) : (false, 0f, string.Empty);

        public NodeAnchor? ResolveAnchor(GameNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            var anchor = _sceneDirectory?.GetAnchor(node.EffectiveAnchorName);
            if (node.HasExplicitAnchor && anchor == null)
            {
                string message =
                    $"[SSNoir] Node '{node.Name}' explicitly requires anchor " +
                    $"'{node.AnchorName}', but no such scene anchor exists.";
                Debug.LogError(message);
                UnityEngine.Assertions.Assert.IsTrue(false, message);
                throw new InvalidOperationException(message);
            }

            return anchor;
        }

        public NodeAnchor? ResolveAnchor(string nodeName)
        {
            var node = FindNodeByName(nodeName);
            return node != null
                ? ResolveAnchor(node)
                : _sceneDirectory?.GetAnchor(nodeName);
        }

        private void OnDestroy()
        {
            _cityOutlines?.Dispose();
        }

        private void Start()
        {
            if (GameLanguage.Warn == null)
                GameLanguage.Warn = msg => Debug.LogWarning(msg);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 开发测试统一按 60 Hz 观察镜头节奏。vSync 开着时 targetFrameRate 会被忽略，
            // 所以两项必须一起设；Release 构建继续服从平台自己的呈现策略。
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
#endif

            // 1. Initialize Game State & Script Loader
            LoadFonts();
            _gameState = new GameState();
            _scriptLoader = new UnityScriptLoader();
#if UNITY_EDITOR
            // 编辑器存档放在仓库缓存目录，便于检查且不污染 Unity 项目资源。
            var projectRoot = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(Application.dataPath));
            SaveManager.DefaultSavePath = System.IO.Path.Combine(projectRoot, ".cache", "saves", "save.json");
#else
            SaveManager.DefaultSavePath = System.IO.Path.Combine(Application.persistentDataPath, "save.json");
#endif

            // 2. Initialize Scene Manager
            _sceneManager = new SceneManager(_gameState, _scriptLoader);
            _sceneManager.OnWarning += message => Debug.LogWarning(message);

            // 3. Initialize camera interaction. Stable camera selection is resolved from the current node focus.
            _cameraManager = new SSNoirCameraManager(this, panSpeedMultiplier);
            _titleScreen = new TitleScreen(this);

            // 4. Find scene directory
            _sceneDirectory = FindObjectOfType<SceneDirectory>();
            if (_sceneDirectory == null)
            {
                var sdGo = new GameObject("SceneDirectory", typeof(SceneDirectory));
                _sceneDirectory = sdGo.GetComponent<SceneDirectory>();
            }
            _cityOutlines = CityOutlineState.TryCreateFromActiveScene();
            _ambientMusic = gameObject.AddComponent<AmbientMusic>();

            // 5. Get pre-placed StageTransitionController (must exist in scene with Inspector fields assigned),
            //    then spawn IMGUIWorldRenderer dynamically (no Inspector fields needed).
            _stageController = GetComponent<StageTransitionController>();
            if (_stageController == null)
                throw new InvalidOperationException("[SSNoir] StageTransitionController must be pre-placed on the SSNoirGameManager GameObject.");
            // 过场播放器要排在渲染器前面：渲染器每帧开头都要问它影幕开没开。
            _cutscenePlayer = gameObject.AddComponent<CutscenePlayer>();
            _cutscenePlayer.Initialize(this);
            _renderer = gameObject.AddComponent<IMGUIWorldRenderer>();
            _renderer.Initialize(this);
            _stageController.Initialize(this);

            // 6. 场景加载后重置界面状态并更新镜头。
            _sceneManager.OnSceneLoaded += () => {
                ResetSceneUiState();
                AdoptLatestSnapshot();
                UpdateCameraFocus();
            };

            string startingLocation = _gameState.Get<string>("location", "world");
            _sceneManager.LoadScene(startingLocation);

            // 7. 世界先加载、镜头先落到世界视角，然后才升起菜单——菜单底下压着的是活的世界，
            //    不是一张标题图。玩家在点"新游戏"之前就已经在看自己要进的那座城了。
            _titleScreen.Open();
        }

        private void LoadFonts()
        {
#if UNITY_EDITOR
            _regularFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(RegularFontAssetPath);
            _semiboldFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(SemiboldFontAssetPath);
#else
            _regularFont = Resources.Load<Font>(RegularFontResourcePath);
            _semiboldFont = Resources.Load<Font>(SemiboldFontResourcePath);
#endif
            if (_regularFont == null)
                Debug.LogError($"[SSNoir] Missing regular font: {RegularFontResourcePath}");
            if (_semiboldFont == null)
                Debug.LogError($"[SSNoir] Missing semibold font: {SemiboldFontResourcePath}");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !_titleScreen.IsActive)
            {
                if (_renderer != null && _renderer.TryAdvanceConversation())
                {
                    // 对白已消费 ESC；未打完时显示全文，已打完时推进下一句。
                }
                else if (_renderer != null && _renderer.TryAdvanceSpotlight())
                {
                    // Spotlight 已消费 ESC；动作内继续后续表现，全局 Spotlight 直接关闭。
                }
                else if (_renderer != null && _renderer.TrySkipOutcomeHold())
                {
                    // 结果停留已消费 ESC：看完了，采纳快照。
                }
                else if (_renderer != null && _renderer.IsAnimationPlaying)
                {
                    if (_renderer.IsAnimationReadyToAcknowledge)
                    {
                        _renderer.AcknowledgePresentationRoll();
                    }
                }
                else if (_renderer != null && _renderer.TryCloseUiPanel())
                {
                    // ESC 关闭最上层界面面板，不穿透触发返回导航。
                }
                else if (_renderer == null || !_renderer.IsInputLocked)
                {
                    if (_selectedResource != null)
                    {
                        ClearSelectedResource();
                    }
                    else
                    {
                        GoBackNavigation();
                    }
                }
            }

            // Update camera panning & orbiting
            // [CAM] Pan 相机只允许在 XZ 上走。守卫放在这儿而不是 _cameraManager.Update() 里：
            // 那个方法在输入锁定和过场期间不跑，而"高度被改掉"最可能正是在那两段里发生的。
            _cameraManager.CheckPanCameraPose();
            if (_stageController != null && _stageController.IsTransitioning && !_stageController.RidesFocusArc)
            {
                // A stage transition drives the brain itself; the focus arc must not
                // be holding the brain's blend hostage while it does. The exception is the
                // portal's city half, which is itself a focus arc and needs the ticks.
                _cameraManager.FinishFocusTravel();
            }
            else
            {
                // The focus arc runs even while input is locked: a focus change during
                // a scripted beat still has to reach its shot.
                _cameraManager.TickFocusTravel();
                if (!IsInputLocked)
                    _cameraManager.Update();
            }
        }

        public bool IsNodeFlipped(string nodeName) => _flippedNodes.Contains(nodeName);

        public void ToggleNodeFlipped(string nodeName)
        {
            ClearTransientNodeUiState(clearSlots: false);
            if (_flippedNodes.Contains(nodeName))
                _flippedNodes.Remove(nodeName);
            else
                _flippedNodes.Add(nodeName);
        }

        public void OnNodeCardClicked(GameNode node)
        {
            ClearTransientNodeUiState(clearSlots: false);

            if (node.IsContainer)
            {
                _nodeSlots.Clear();
                _navigationStack.Add(node);
                ResolveNavigationStack();
                _selectedResource = null;
                SetFocusedNode(null, updateCamera: true);
                if (node.IsPlace)
                    PlayArrival(node.Name);
            }
            else if (node.Resolve?.Type == ResolveType.Note)
            {
                // 标注永远不可点：不能翻面，也不能成为执行 / 投骰目标。它压根不该收到
                // 这个回调（绘制层不给它任何点击目标），这里只是最后一道闸。
                return;
            }
            else if (node.Resolve != null && node.Resolve.Type == ResolveType.Observe && (node.Requires == null || node.Requires.Count == 0))
            {
                ToggleNodeFlipped(node.Name);
            }
            else if (node.Requires == null || node.Requires.Count == 0)
            {
                if (node.Resolve != null && node.Resolve.Type == ResolveType.Instant)
                {
                    SetFocusedNode(node.Name);
                }
                else
                {
                    ExecuteNodeAction(node);
                }
            }
            else
            {
                SetFocusedNode(node.Name);
            }
        }

        /// <summary>
        /// 设置卡片交互焦点。焦点用于槽位与高亮，不等同于镜头目标；只有导航路径变化时
        /// 才显式要求更新镜头，避免对白、Spotlight 等 UI 表现打断正在进行的运镜。
        /// </summary>
        public void SetFocusedNode(string? nodeName, bool updateCamera = false)
        {
            ReleaseRestCooldown();
            ClearTransientNodeUiState(clearSlots: false);
            _focusedNodeName = nodeName ?? string.Empty;

            if (!string.IsNullOrEmpty(_focusedNodeName))
                ClearOtherNodeSlots(_focusedNodeName);

            if (updateCamera)
                UpdateCameraFocus();
        }

        /// <summary>
        /// 重新对准当前导航路径的焦点镜头。
        ///
        /// <paramref name="storyDriven"/> 区分两种换镜：玩家自己翻页（点地点卡、返回、回家）
        /// 是城里最高频的动作，看多了会腻，「减少动画」就是给它准备的；而剧情把镜头带到
        /// 另一个地方（交锋里换场、倒下送医、交锋收场退回地点）一局里没几次，这段路本身
        /// 在交代"你从哪儿到了哪儿"，砍了空间关系就断了，所以不受该设置影响。Portal 和
        /// 过场出于同样理由也永远走全动画。
        /// </summary>
        private void UpdateCameraFocus(bool storyDriven = false)
        {
            if (ShouldDeferFocusToPendingPortal())
                return;

            var focusPath = GetCurrentFocusPathNames();
            var focusCamera = ResolveCurrentFocusCamera(focusPath, out var usedWorldFallback);
            if (usedWorldFallback)
            {
                Debug.LogWarning($"[SSNoir] No focus camera resolved for world node path '{string.Join(" > ", focusPath)}'. Falling back to '{WorldRootNodeName}' focus camera for testing.");
            }
            else if (focusCamera == null)
            {
                Debug.LogWarning($"[SSNoir] No focus camera resolved for current node path '{string.Join(" > ", focusPath)}'. Keeping the current camera priority state.");
            }

            if (focusCamera != null)
            {
                bool ownsCityOutline = _cityOutlines?.HasOwner(focusCamera) == true;
                if (ownsCityOutline)
                {
                    _cityOutlineClearRequest++;
                    PresentCamera(focusCamera);
                }

                // 探索只声明意图；路径由镜头管理器根据进出两端选择。
                // 剧情仍明确使用编排弧线，不受探索设置影响。
                bool travelStarted = false;
                if (_stageController == null || !_stageController.IsTransitioning)
                    travelStarted = _cameraManager.BeginFocusTravel(
                        focusCamera, respectReduceMotion: !storyDriven,
                        path: storyDriven ? FocusTravelPath.AuthoredArc : FocusTravelPath.Exploration);

                ResetFocusCameraPriorities();
                focusCamera.Priority = 20;

                if (!ownsCityOutline && _cityOutlines != null)
                {
                    if (travelStarted && _cameraManager.IsFocusTravelInFlight)
                        DeferCityOutlineClearUntilFocusTravelSettles();
                    else
                    {
                        _cityOutlineClearRequest++;
                        _cityOutlines.SetFocusedCamera(null);
                    }
                }
            }
        }

        private void DeferCityOutlineClearUntilFocusTravelSettles()
        {
            int request = ++_cityOutlineClearRequest;
            StartCoroutine(ClearCityOutlineWhenFocusTravelSettles(request));
        }

        private IEnumerator ClearCityOutlineWhenFocusTravelSettles(int request)
        {
            while (request == _cityOutlineClearRequest && _cameraManager.IsFocusTravelInFlight)
                yield return null;

            if (request == _cityOutlineClearRequest)
                _cityOutlines?.SetFocusedCamera(null);
        }

        private void ResetFocusCameraPriorities()
        {
            if (_sceneDirectory == null)
                return;

            foreach (var a in _sceneDirectory.AllAnchors)
            {
                if (a.FocusVirtualCamera != null)
                    a.FocusVirtualCamera.Priority = 5;
            }
        }

        private bool ShouldDeferFocusToPendingPortal()
        {
            if (_stageController == null)
                return false;

            var currentContextId = CurrentStageContextId;
            if (currentContextId == _stageController.CurrentContextId)
                return false;

            // 离开独立舞台时目的上下文就是 null，但这正是 Portal 的反向行程。
            // 不能把 null 当成“没有 Portal”：当前仍持有的 Portal 才是这次退场的所有者。
            if (_stageController.HasActivePortal)
                return true;

            // 从城市进入舞台时，目的 Anchor 上的配置声明这次换镜归 Portal。
            if (currentContextId == null)
                return false;

            return ResolveStageAnchor(currentContextId) != null;
        }

        private Cinemachine.CinemachineVirtualCamera? ResolveCurrentFocusCamera()
        {
            // 焦点上下文切换的演出期间由新根节点作数，哪怕它解不出镜头也不退回旧答案——
            // 那个答案一定是错的，宁可回 null（过场就不做回程运镜，交给回调重新聚焦）。
            if (_incomingFocusContextActive)
                return _incomingFocusContextCamera;

            return ResolveCurrentFocusCamera(GetCurrentFocusPathNames(), out _);
        }

        private Cinemachine.CinemachineVirtualCamera? ResolveCurrentFocusCamera(List<string> focusPath, out bool usedWorldFallback)
        {
            usedWorldFallback = false;
            foreach (var nodeName in focusPath.AsEnumerable().Reverse())
            {
                // Stage 身份是节点名（Portal 挂在 Anchor_<名>）。门卡也叫这个名，但
                // :anchor 指门外挂点（家的「门口」）——那种情况必须用 Stage 根机位，
                // 不能跟到门外。交锋根若额外声明了区内 :anchor（巷子 → 勒索信-巷口），
                // 且该锚点就在 Stage 空间里，则用分区机位，换段才能切走廊镜头。
                var stage = ResolveStageAnchor(nodeName);
                if (stage != null)
                {
                    var zoneCam = TryResolveInStageZoneCamera(nodeName, stage);
                    if (zoneCam != null)
                        return zoneCam;
                    if (stage.FocusVirtualCamera != null)
                        return stage.FocusVirtualCamera;
                }
                var anchor = ResolveAnchor(nodeName);
                if (anchor != null && anchor.FocusVirtualCamera != null)
                    return anchor.FocusVirtualCamera;
            }

            if (string.Equals(_sceneManager.CurrentSceneName, "world", StringComparison.OrdinalIgnoreCase))
            {
                var worldAnchor = ResolveAnchor(WorldRootNodeName);
                if (worldAnchor != null && worldAnchor.FocusVirtualCamera != null)
                {
                    usedWorldFallback = true;
                    return worldAnchor.FocusVirtualCamera;
                }
            }

            return null;
        }

        private List<string> GetCurrentFocusPathNames()
        {
            if (!string.IsNullOrEmpty(_focusedNodeName))
            {
                var focusedPath = FindPathToNode(_focusedNodeName);
                if (focusedPath.Count > 0)
                    return focusedPath;
            }

            return GetCurrentNavigationPathNames();
        }

        private List<string> GetCurrentNavigationPathNames()
        {
            var path = new List<string>();
            var root = GetRootNode();
            if (root == null)
                return path;

            path.Add(root.Name);
            foreach (var node in _navigationStack)
                path.Add(node.Name);
            return path;
        }

        private string? ResolveCurrentStageContextId()
        {
            foreach (var nodeName in GetCurrentNavigationPathNames().AsEnumerable().Reverse())
            {
                if (ResolveStageAnchor(nodeName) != null)
                    return nodeName;
            }

            return null;
        }

        /// <summary>
        /// 交锋根在 Stage 名上另声明了区内 :anchor 时，取该分区机位。
        /// 门卡那种 :anchor 在城市场地（与 Stage 锚点相距甚远）的，返回 null，继续用 Stage 根机位。
        /// </summary>
        private Cinemachine.CinemachineVirtualCamera? TryResolveInStageZoneCamera(string nodeName, NodeAnchor stage)
            => TryResolveInStageZoneCamera(FindNodeByName(nodeName), stage);

        private Cinemachine.CinemachineVirtualCamera? TryResolveInStageZoneCamera(GameNode? node, NodeAnchor stage)
        {
            if (node == null || !node.HasExplicitAnchor)
                return null;
            if (string.Equals(node.AnchorName, node.Name, StringComparison.Ordinal))
                return null;
            var zone = _sceneDirectory?.GetAnchor(node.AnchorName!);
            if (zone == null || zone == stage || zone.FocusVirtualCamera == null)
                return null;
            // Stage 停在郊野，区内锚点同 Prefab；门卡挂点在城里，通常隔数百米以上。
            float dist = Vector3.Distance(stage.transform.position, zone.transform.position);
            if (dist > 200f)
                return null;
            return zone.FocusVirtualCamera;
        }

        /// <summary>
        /// Stage 的身份是**节点名**：`Anchor_<节点名>` 挂着 <see cref="StagePortalConfig"/> 的节点就是一扇门，
        /// 点进去穿门。`:anchor` 只决定这张门卡挂在城市里的哪个位置（家的门口），与门通向哪儿无关——
        /// 门卡在门外看得见，门后的空间却在郊野的 Stage 里，两者不可能是同一个锚点。
        /// 交锋根容器名就是场景名，所以这条规则对交锋不是新东西。
        /// </summary>
        public NodeAnchor? ResolveStageAnchor(string nodeName)
        {
            var anchor = _sceneDirectory?.GetAnchor(nodeName);
            return anchor != null && anchor.GetComponent<StagePortalConfig>() != null ? anchor : null;
        }

        private GameNode? GetCurrentNavigationNode()
        {
            if (_navigationStack.Count > 0)
                return _navigationStack[_navigationStack.Count - 1];
            return GetRootNode();
        }

        private GameNode? GetRootNode()
        {
            return _displayedSnapshot.RootNode;
        }

        private List<string> FindPathToNode(string nodeName)
        {
            var result = new List<string>();
            var root = GetRootNode();
            if (root != null && FindPathToNodeRecursive(root, nodeName, result))
                return result;
            return new List<string>();
        }

        private bool FindPathToNodeRecursive(GameNode node, string nodeName, List<string> path)
        {
            path.Add(node.Name);
            if (node.Name == nodeName)
                return true;

            foreach (var child in node.Children)
            {
                if (FindPathToNodeRecursive(child, nodeName, path))
                    return true;
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        public List<SlottedResource?>? GetSlotsForNode(string nodeName)
        {
            if (!_nodeSlots.TryGetValue(nodeName, out var list))
            {
                var node = FindNodeByName(nodeName);
                if (node != null && node.Requires != null)
                {
                    list = new List<SlottedResource?>();
                    for (int i = 0; i < node.Requires.Count; i++)
                        list.Add(null);
                    _nodeSlots[nodeName] = list;
                }
            }
            return list;
        }

        public void OnSlotClicked(GameNode node, int slotIndex)
        {
            ClearTransientNodeUiState(clearSlots: false);
            BeginSlotResourceDrag(node, slotIndex);
        }

        private SelectedResource CreateSelectedResourceFromSlot(SlottedResource resource)
        {
            if (resource.Type == "die")
            {
                return new SelectedResource
                {
                    Type = "die",
                    Value = resource.Value,
                    SourceIndex = resource.SourceIndex,
                    ActorId = resource.ActorId,
                    DieIndex = resource.DieIndex
                };
            }

            return new SelectedResource
            {
                Type = "item",
                ItemName = resource.ItemId,
                Value = resource.Qty > 0 ? resource.Qty : resource.Value
            };
        }

        public bool CanMatchRequirement(ActionCost req)
        {
            if (_selectedResource == null)
            {
                return false;
            }

            if (req.Type == "die")
            {
                return _selectedResource.Type == "die";
            }

            if (req.Type == "item")
            {
                return _selectedResource.Type == "item"
                    && req.ItemId.Equals(_selectedResource.ItemName, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public bool CanPlaceSelectedResource(GameNode node, int slotIndex)
        {
            var slots = GetSlotsForNode(node.Name);
            // A filled slot is a valid target too — placing replaces it, and the displaced
            // die returns to hand automatically (slot state is derived, not consumed).
            if (_selectedResource == null
                || node.Requires == null
                || slots == null
                || slotIndex < 0
                || slotIndex >= node.Requires.Count
                || slotIndex >= slots.Count)
            {
                return false;
            }

            var req = node.Requires[slotIndex];
            if (!CanMatchRequirement(req))
            {
                return false;
            }

            if (req.Type == "die")
            {
                return true;
            }

            int totalOwned = _displayedSnapshot.Inventory.TryGetValue(req.ItemId, out var ownedQty) ? ownedQty : 0;
            int totalSlotted = GetTotalSlottedItemQty(req.ItemId, slots, slotIndex);
            return totalOwned - totalSlotted >= req.Qty;
        }

        public bool TryPlaceSelectedResource(GameNode node, int slotIndex)
        {
            var slots = GetSlotsForNode(node.Name);
            if (_selectedResource == null || slots == null || node.Requires == null)
            {
                return false;
            }

            var req = node.Requires[slotIndex];
            if (!CanPlaceSelectedResource(node, slotIndex))
            {
                ShowNotification(CanMatchRequirement(req)
                    ? $"缺少数量，需要 {req.Qty} 个 {req.ItemId}"
                    : $"槽位需要: {(req.Type == "die" ? "骰子" : req.ItemId)}");
                return false;
            }

            ClearOtherNodeSlots(node.Name);
            if (req.Type == "die" && _selectedResource.Type == "die")
            {
                ClearTransientNodeUiState(clearSlots: false);
                ClearDieFromAllSlots(_selectedResource.SourceIndex);
                slots[slotIndex] = new SlottedResource
                {
                    Type = "die",
                    Value = _selectedResource.Value,
                    SourceIndex = _selectedResource.SourceIndex,
                    ActorId = _selectedResource.ActorId,
                    DieIndex = _selectedResource.DieIndex
                };
                _selectedResource = null;
                return true;
            }

            if (req.Type == "item" && _selectedResource.Type == "item")
            {
                ClearTransientNodeUiState(clearSlots: false);
                slots[slotIndex] = new SlottedResource
                {
                    Type = "item",
                    ItemId = req.ItemId,
                    Value = req.Qty,
                    Qty = req.Qty
                };
                _selectedResource = null;
                return true;
            }

            return false;
        }

        private void ClearOtherNodeSlots(string activeNodeName)
        {
            foreach (var pair in _nodeSlots)
            {
                if (pair.Key != activeNodeName)
                {
                    var slots = pair.Value;
                    for (int i = 0; i < slots.Count; i++)
                        slots[i] = null;
                }
            }
        }

        private void ClearDieFromAllSlots(int dieIndex)
        {
            foreach (var list in _nodeSlots.Values)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var slot = list[i];
                    if (slot != null && slot.Type == "die" && slot.SourceIndex == dieIndex)
                        list[i] = null;
                }
            }
        }

        public int GetTotalSlottedItemQty(string itemName)
        {
            return GetTotalSlottedItemQty(itemName, null, -1);
        }

        private int GetTotalSlottedItemQty(string itemName, List<SlottedResource?>? targetSlots, int targetSlotIndex)
        {
            int sum = 0;
            foreach (var list in _nodeSlots.Values)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (ReferenceEquals(list, targetSlots) && i == targetSlotIndex)
                    {
                        continue;
                    }

                    var s = list[i];
                    if (s != null && s.Type == "item" && s.ItemId == itemName)
                        sum += s.Qty > 0 ? s.Qty : s.Value;
                }
            }
            return sum;
        }

        public bool IsDieSlotted(int dieIndex)
        {
            if (_selectedResource != null && _selectedResource.Type == "die" && _selectedResource.SourceIndex == dieIndex)
                return true;
            foreach (var slots in _nodeSlots.Values)
            {
                foreach (var slot in slots)
                {
                    if (slot != null && slot.Type == "die" && slot.SourceIndex == dieIndex)
                        return true;
                }
            }
            return false;
        }

        public int GetRemainingItemQty(string itemName)
        {
            int total = _displayedSnapshot.Inventory.TryGetValue(itemName, out var qty) ? qty : 0;

            foreach (var slots in _nodeSlots.Values)
            {
                foreach (var slot in slots)
                {
                    if (slot != null && slot.Type == "item" && slot.ItemId == itemName)
                        total -= slot.Qty > 0 ? slot.Qty : slot.Value;
                }
            }

            return Mathf.Max(0, total);
        }

        public void ClearSelectedResource()
        {
            if (_resourceDragActive)
            {
                RestoreResourceDragOrigin();
                _resourceDragActive = false;
                ClearResourceDragState();
                return;
            }

            _selectedResource = null;
        }

        // --- Execute Actions via Async Coroutine ---
        public void ExecuteNodeAction(GameNode node)
        {
            if (_stateTainted)
                return;
            StartCoroutine(ExecuteRoutine(node));
        }

        private IEnumerator ExecuteRoutine(GameNode node)
        {
            // 这里不再对骰子做第二道有效性检查。失效引用只在一个地方清：
            // 采纳新快照的那一刻（AdoptLatestSnapshot → ClearUnavailableDiceReferences）。
            // 玩家看见的卡槽和这里送出去的卡槽因此永远是同一份。
            var slots = GetSlotsForNode(node.Name) ?? new List<SlottedResource?>();

            _renderer.ClearCardResidues();
            _renderer.SetInputLocked(true);

            bool done = false;
            try
            {
                string sceneBefore = _sceneManager.CurrentSceneName;
                string rootBefore = _sceneManager.CurrentRootNode?.Name ?? string.Empty;
                var outgoingFocusCamera = CurrentFocusCamera;
                ActionReport report = _sceneManager.ExecuteAction(node, slots);
                var postTurnSteps = DetachPostTurnBlockingSteps(report);
                var postSceneSteps = report.PostSceneBlockingSteps;
                var turnStartReport = report.TurnStartReport;
                _nodeSlots.Remove(node.Name);
                _selectedResource = null;
                bool sceneChanged = !string.Equals(sceneBefore, _sceneManager.CurrentSceneName, System.StringComparison.OrdinalIgnoreCase);
                bool rootChanged = !string.Equals(rootBefore, _sceneManager.CurrentRootNode?.Name ?? string.Empty, System.StringComparison.Ordinal);
                bool focusContextChanged = sceneChanged || rootChanged;
                if (focusContextChanged)
                    BeginIncomingFocusContext(outgoingFocusCamera);

                // 家里的「睡觉」把 end-turn! 包在动作里，走的就是这条普通动作路径。
                // 黑幕跟着执行一起起跑，不等演出播完。
                bool dippedEarly = ShouldDipEarly(report);
                if (dippedEarly)
                    StartCoroutine(_stageController.FadeOutForTurn());

                // 换了场景也照传动作名：结果停留在采纳快照之前就结束，不会漏到新场景的同名卡上；
                // 宿主卡这时多半已经出镜，结果条会挂在屏幕中央的浮牌下。
                _renderer.PlayPresentation(report, node.Name, () =>
                {
                    // 对白、Spotlight、banter 等 UI 表现不会改相机；需要重新聚焦的只有两种：
                    // 场景／阶段根节点变了，或者你所在的那个容器执行完就从树上消失了。
                    Action land = () =>
                    {
                        // EnterPlace 阻塞步骤可能已经把快照采纳到最终落点；统一边界入口会
                        // 识别这个情况，不重复清 UI，也不再启动第二趟镜头。
                        CommitLatestSnapshotAtPresentationBoundary(focusContextChanged);
                    };
                    StartCoroutine(LandAfterTurnDip(report, dippedEarly, land, () =>
                    {
                        Action finishAfterScene = () =>
                        {
                            Action finish = () =>
                            {
                                if (postTurnSteps.Count > 0)
                                    StartCoroutine(PlayPostTurnStepsAfterDiceSettle(postTurnSteps, () => done = true));
                                else
                                    done = true;
                            };
                            // 睡醒后的消息排在新场景内容之后，均使用已采纳的快照。
                            if (turnStartReport != null)
                                _renderer.PlayPresentation(turnStartReport, string.Empty, finish);
                            else
                                finish();
                        };
                        if (postSceneSteps.Count > 0)
                            _renderer.PlayBlockingPresentation(postSceneSteps, finishAfterScene);
                        else
                            finishAfterScene();
                    }));
                }, holdBeforeBlocking: focusContextChanged && !dippedEarly);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ExecuteNodeAction] Exception during execution: {ex}");
                ShowNotification($"执行异常: {ex.Message}");
                // 异常是从引擎中途抛出来的：这一手可能已经改了一半状态（场景换了、骰池重掷了）。
                // UI 痕迹可以清，游戏状态却不能假装已经恢复；下面会把当前会话标成 tainted，
                // 只允许玩家从标题页重新开始或读档。
                _nodeSlots.Clear();
                ClearResourceDragState();
                // 早黑是在演出之前起的；演出这头炸了，收尾那一半就永远不会跑，
                // 不收黑幕玩家会留在一块黑屏里。
                _stageController.AbortTurnDip();
                // 演出没起来的话回调不会来，窗口期得在这里关掉，否则焦点相机会一直答着
                // 那个再也不会被采纳的新场景镜头。
                EndIncomingFocusContext();
                MarkStateTainted("动作结算失败", ex);
                done = true;
            }

            while (!done)
            {
                yield return null;
            }

            _renderer.SetInputLocked(false);
        }

        private void ResetSceneUiState()
        {
            _navigationStack.Clear();
            _nodeSlots.Clear();
            _flippedNodes.Clear();
            _selectedResource = null;
            _focusedNodeName = string.Empty;
            _renderer?.ResetUiState();
        }

        /// <summary>
        /// 场景或阶段根节点切换之后、演出回调之前，焦点相机改由新根节点回答。
        ///
        /// 这一段窗口期里 _displayedSnapshot 还是旧场景（卡片要等演出走完才换脸），照常
        /// 解析只会解出旧场景的镜头。谁在乎这个：过场起场时把当前焦点相机记成回程目标，
        /// 记错了片子放完就先飞回旧场景，再由回调推第二趟。
        ///
        /// 只登记，不动优先级——首个阻塞演出可能是几句入场对白，这时候抬优先级会让 brain
        /// 当场把画面blend到交锋镜头上，玩家在片子之前先看见一次多余的运镜。真正的接管点
        /// 在过场的回程（见 CutscenePlayer.BeginReturn）。
        /// </summary>
        private void BeginIncomingFocusContext(Cinemachine.CinemachineVirtualCamera? outgoingCamera)
        {
            _incomingFocusContextActive = true;
            _incomingFocusContextCamera = null;
            _outgoingFocusContextCamera = outgoingCamera;
            string? outgoingStage = ResolveCurrentStageContextId();
            string? incomingStage = null;

            var incomingRoot = _sceneManager.LatestSnapshot.RootNode;
            if (incomingRoot == null)
                return;

            var stage = ResolveStageAnchor(incomingRoot.Name);
            var anchor = stage ?? ResolveAnchor(incomingRoot);
            if (anchor != null)
            {
                // 用 incomingRoot 本身：此时 _displayedSnapshot 还是旧树，FindNodeByName 找不到新根。
                _incomingFocusContextCamera = stage != null
                    ? (TryResolveInStageZoneCamera(incomingRoot, stage) ?? stage.FocusVirtualCamera)
                    : anchor.FocusVirtualCamera;
                if (stage != null)
                    incomingStage = incomingRoot.Name;
            }
            _incomingFocusCrossesStagePortal = !string.Equals(outgoingStage, incomingStage, System.StringComparison.Ordinal);
        }

        private void EndIncomingFocusContext()
        {
            _incomingFocusContextActive = false;
            _incomingFocusContextCamera = null;
            _outgoingFocusContextCamera = null;
            _incomingFocusCrossesStagePortal = false;
        }

        /// <summary>
        /// 让这台相机成为场上唯一的高优先级焦点相机。运镜本身不改优先级，所以任何"要让
        /// brain 真的放这台"的地方都得配一次这个，否则会出现运镜在飞 A、画面在放 B。
        /// </summary>
        public void PromoteFocusCamera(Cinemachine.CinemachineVirtualCamera camera)
        {
            if (camera == null)
                return;

            PresentCamera(camera);
            ResetFocusCameraPriorities();
            camera.Priority = 20;
        }

        /// <summary>
        /// 统一登记屏幕上即将呈现的镜头。普通聚焦相机按自身所在的建筑解析；
        /// 独立放置的过场相机由 <paramref name="focusAnchor"/> 显式提供建筑语义。
        /// </summary>
        public void PresentCamera(
            Cinemachine.CinemachineVirtualCamera? camera,
            NodeAnchor? focusAnchor = null)
        {
            var outlineCamera = focusAnchor != null
                ? focusAnchor.FocusVirtualCamera
                : camera;
            if (focusAnchor != null && outlineCamera == null)
            {
                string message =
                    $"[SSNoir] Camera presentation anchor '{focusAnchor.ResolvedNodeName}' " +
                    "has no FocusVirtualCamera.";
                Debug.LogError(message);
                UnityEngine.Assertions.Assert.IsTrue(false, message);
                throw new InvalidOperationException(message);
            }

            _cityOutlines?.SetFocusedCamera(outlineCamera);
        }

        /// <summary>
        /// 采纳最新快照并重建导航栈。
        /// </summary>
        /// <returns>
        /// true 表示导航栈在这次采纳里被改写了——你原本所在的容器从新树上消失，
        /// ResolveNavigationStack 把栈截断（多半是一路退回世界根）。
        /// 这是一次真实的空间上下文变化，调用方必须据此重新聚焦相机：
        /// 否则人已经回到世界根，镜头还留在那个已经不存在的容器上。
        /// </returns>
        public bool AdoptLatestSnapshot()
        {
            var previousInventory = _displayedSnapshot.Inventory;
            var incomingInventory = _sceneManager.LatestSnapshot.Inventory;
            if (_inventoryPulseBaselineReady)
            {
                foreach (var item in incomingInventory)
                {
                    int before = previousInventory.TryGetValue(item.Key, out int count) ? count : 0;
                    if (item.Value > before)
                        _itemGainPulseUntil[item.Key] = Time.unscaledTime + ItemGainPulseDuration;
                }
            }
            else
            {
                _inventoryPulseBaselineReady = true;
            }

            UpdateVitalPulses(_sceneManager.LatestSnapshot);

            string? previousRoot = _displayedSnapshot.RootNode?.Name;
            _displayedSnapshot = _sceneManager.LatestSnapshot;
            LastSnapshotChangedRoot = !string.Equals(previousRoot, _displayedSnapshot.RootNode?.Name, StringComparison.Ordinal);
            ClearUnavailableDiceReferences(_displayedSnapshot);
            if (_displayedSnapshot.RootNode != null)
                ValidateExplicitAnchors(_displayedSnapshot.RootNode);
            if (_cityOutlines != null)
            {
                var referenced = new HashSet<string>(StringComparer.Ordinal);
                if (_displayedSnapshot.RootNode != null)
                    CollectEffectiveAnchors(_displayedSnapshot.RootNode, referenced);
                _cityOutlines.SetReferencedAnchors(referenced);
            }
            _ambientMusic.Apply(_gameState.Get<object>("音乐") as string);
            PropMotion.SyncAll(_gameState);
            var pathBefore = new List<string>();
            foreach (var node in _navigationStack)
                pathBefore.Add(node.Name);
            ResolveNavigationStack();
            CleanupNodeSlots();

            // 快照是“玩家现在在哪”的唯一事实。Stage Portal 在这里同步接管，Debug 直载、
            // 正式交锋和返回世界因而共用同一条边界，不再等下一帧 Update 猜执行顺序。
            _stageController?.ReconcileContext(storyDriven: LastSnapshotChangedRoot);

            if (pathBefore.Count != _navigationStack.Count)
                return true;
            for (int i = 0; i < pathBefore.Count; i++)
            {
                if (!string.Equals(pathBefore[i], _navigationStack[i].Name, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void CollectEffectiveAnchors(GameNode node, HashSet<string> into)

        {

            into.Add(node.EffectiveAnchorName);

            foreach (var child in node.Children)

                CollectEffectiveAnchors(child, into);

        }


        private void ValidateExplicitAnchors(GameNode node)
        {
            if (node.HasExplicitAnchor)
                ResolveAnchor(node);

            foreach (var child in node.Children)
                ValidateExplicitAnchors(child);
        }

        public void SetCarriedSupport(string supportId)
        {
            if (_stateTainted)
                return;
            try
            {
                _sceneManager.SetCarriedSupport(supportId);
                AdoptLatestSnapshot();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SetCarriedSupport] Exception: {ex}");
                MarkStateTainted("更换支援失败", ex);
            }
        }

        public void UpgradeActorStat(string actorId, string statKey)
        {
            if (_stateTainted)
                return;
            try
            {
                _gameState.Team.UpgradeActorStat(actorId, statKey);
                _sceneManager.RebuildRenderTree();
                AdoptLatestSnapshot();

                var actor = _gameState.Team.FindActor(actorId);
                string actorName = actor?.Name ?? actorId;
                _gameState.NotificationCenter.Push(
                    UiText.GrowthUpgrade(actorName, statKey, actor?.Stats.GetValueOrDefault(statKey) ?? 0),
                    NotificationKind.Success);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UpgradeActorStat] Exception: {ex}");
                MarkStateTainted("能力升级失败", ex);
            }
        }

        public void EnterDemoEncounter(string scene, string entryExpression)
        {
            if (_stateTainted) return;
            if (string.IsNullOrWhiteSpace(entryExpression))
            {
                OnSceneButtonClicked(scene);
                return;
            }

            // 演示演出节点由城市内容提供，走普通动作报告与表现流程。
            OnSceneButtonClicked("world");
            SetFocusedNode(null, updateCamera: true);
            var interpreter = _sceneManager.ActiveInterpreter;
            var node = SSNoir.Scripting.NodeConverter.ConvertSingle(
                interpreter.Eval(entryExpression), interpreter.RawInterpreter);
            ExecuteNodeAction(node);
        }

        public void UnlockDemoLocations()
        {
            if (_stateTainted) return;
            if (_sceneManager.CurrentSceneName != "world")
                throw new InvalidOperationException("演示地点解锁必须在世界中执行。");
            _gameState.Set("演示地点全开", true);
            _sceneManager.Refresh();
            CommitLatestSnapshotAtPresentationBoundary(resetUiOnContextChange: false);
            ShowNotification("演示：全部地点已开放。");
        }

        public void AddDebugMoney(int amount)
        {
            if (_stateTainted)
                return;
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Debug money amount must be positive.");

            try
            {
                int current = _gameState.Inventory.GetCount("金钱");
                _gameState.Inventory.SetCount("金钱", checked(current + amount));
                _sceneManager.RebuildRenderTree();
                AdoptLatestSnapshot();
                ShowNotification($"调试：金钱 +{amount}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddDebugMoney] Exception: {ex}");
                MarkStateTainted("调试资源修改失败", ex);
            }
        }

        public void GoBackNavigation()
        {
            ClearTransientNodeUiState(clearSlots: false);

            if (!string.IsNullOrEmpty(_focusedNodeName))
            {
                // 从聚焦状态返回：取消聚焦
                SetFocusedNode(null, updateCamera: true);
            }
            else if (_navigationStack.Count > 0)
            {
                // 从导航栈返回：弹出栈
                _nodeSlots.Clear();
                _navigationStack.RemoveAt(_navigationStack.Count - 1);
                ResolveNavigationStack();

                // 退回世界层算一次到达（睡觉锁这类"你离开过"规则要听见它）；
                // 中间层之间的返回不算，不要在这里调其他名字。
                if (_navigationStack.Count == 0)
                {
                    var root = GetRootNode();
                    if (root != null)
                        PlayArrival(root.Name);
                }

                // Update camera focus priority for navigation parent or global view
                UpdateCameraFocus();
            }
        }

        private void ResolveNavigationStack()
        {
            var root = GetRootNode();
            if (root == null)
            {
                _navigationStack.Clear();
                _visibleNodes = new List<GameNode>();
                return;
            }

            if (_navigationStack.Count == 0)
            {
                _visibleNodes = root.Children.ToList();
                return;
            }

            var path = new List<string>();
            foreach (var node in _navigationStack)
                path.Add(node.Name);

            _navigationStack.Clear();
            var currentLevel = root.Children.ToList();

            foreach (var name in path)
            {
                var match = currentLevel.Find(n => n.Name == name);
                if (match != null && match.IsContainer)
                {
                    _navigationStack.Add(match);
                    currentLevel = match.Children;
                }
                else
                {
                    // 当前容器可能被刚执行的动作移除。保留此前仍有效的祖先路径，
                    // 让玩家退到最近一级，而不是因为最深一层消失就直接回世界根。
                    break;
                }
            }

            _visibleNodes = currentLevel;
        }

        private void CleanupNodeSlots()
        {
            var currentNames = new HashSet<string>();
            var root = GetRootNode();
            if (root != null)
                CollectAllNodeNamesRecursive(root, currentNames);

            var keysToRemove = new List<string>();
            foreach (var name in _nodeSlots.Keys)
            {
                if (!currentNames.Contains(name))
                    keysToRemove.Add(name);
            }
            foreach (var key in keysToRemove)
                _nodeSlots.Remove(key);
        }

        // 骰子按角色与固定骰池槽位识别；重掷、受伤或阶段换手都会让旧引用失效。
        //
        // 唯一的清理时机是**采纳新快照的那一刻**——玩家眼前的卡槽和送进引擎的卡槽
        // 由此永远是同一份。别再在执行前补第二道检查：那道检查读的是还没显示出来的
        // 新快照，等于用另一把尺子量同一件东西，反而制造出"看得见却用不了"的骰子。
        private bool IsDieAvailable(PresentationSnapshot snapshot, string actorId, int dieIndex, int value)
        {
            foreach (var actor in snapshot.Actors)
            {
                if (!string.Equals(actor.Id, actorId, StringComparison.OrdinalIgnoreCase))
                    continue;

                for (int i = 0; i < actor.ActionDice.Count; i++)
                {
                    if (actor.ActionDiceSlotIds[i] == dieIndex && actor.ActionDice[i] == value)
                        return true;
                }
                return false;
            }
            return false;
        }

        private bool IsAvailableDieReference(PresentationSnapshot snapshot, SlottedResource resource)
        {
            if (resource.Type != "die")
                return true;

            string actorId = string.IsNullOrEmpty(resource.ActorId) ? "player" : resource.ActorId;
            int dieIndex = resource.DieIndex >= 0 ? resource.DieIndex : resource.SourceIndex;
            return IsDieAvailable(snapshot, actorId, dieIndex, resource.Value);
        }

        private void ClearUnavailableDiceReferences(PresentationSnapshot snapshot)
        {
            foreach (var slots in _nodeSlots.Values)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null && !IsAvailableDieReference(snapshot, slots[i]!))
                        slots[i] = null;
                }
            }

            if (_selectedResource != null && _selectedResource.Type == "die")
            {
                string actorId = string.IsNullOrEmpty(_selectedResource.ActorId) ? "player" : _selectedResource.ActorId;
                int dieIndex = _selectedResource.DieIndex >= 0 ? _selectedResource.DieIndex : _selectedResource.SourceIndex;
                if (!IsDieAvailable(snapshot, actorId, dieIndex, _selectedResource.Value))
                    ClearResourceDragState();
            }
        }

        private void CollectAllNodeNamesRecursive(GameNode node, HashSet<string> result)
        {
            result.Add(node.Name);
            foreach (var child in node.Children)
                CollectAllNodeNamesRecursive(child, result);
        }

        private GameNode? FindNodeByName(string name)
        {
            var root = GetRootNode();
            var found = root == null ? null : FindNodeRecursive(root, name);
            if (found != null) return found;

            // 随身动作（烟、酒）不在渲染树上——它们不属于任何一场交锋。但它们是货真价实的
            // 动作节点，一样要有骰位、一样能放骰子，所以按名字找节点时必须也找得到它们，
            // 否则 GetSlotsForNode 拿不到槽，槽就一直是"放不进去"的红。
            foreach (var carry in _displayedSnapshot.CarryNodes)
                if (string.Equals(carry.Name, name, StringComparison.Ordinal))
                    return carry;
            return null;
        }

        private GameNode? FindNodeRecursive(GameNode node, string name)
        {
            if (node.Name == name)
                return node;

            foreach (var childNode in node.Children)
            {
                var child = FindNodeRecursive(childNode, name);
                if (child != null) return child;
            }
            return null;
        }

        public void ShowNotification(string message)
        {
            _gameState.NotificationCenter.Push(message, NotificationKind.Info);
        }

        public void OnSceneButtonClicked(string sc)
        {
            if (_stateTainted)
                return;
            _selectedResource = null;
            _navigationStack.Clear();
            _sceneManager.GoToLocation(sc);
        }

        public void SaveGame(string? filePath = null)
        {
            if (_stateTainted)
            {
                Debug.LogError("[SSNoir] Refusing to save because the current game state is tainted.");
                return;
            }
            try
            {
                var path = filePath ?? SaveManager.DefaultSavePath;
                _sceneManager.Settings.Remove("language");
                _sceneManager.Settings[ReduceMotionSettingKey] = MotionSettings.ReduceMotion;
                // Settings 只认 string/int/long/double/bool（见 SaveManager.WritePrimitive），
                // 音量存 double，读出来也一定是 double。
                _sceneManager.Settings[MusicVolumeSettingKey] = (double)AudioVolumes.Music;
                _sceneManager.Settings[SfxVolumeSettingKey] = (double)AudioVolumes.Sfx;
                _sceneManager.Settings[DialogueVolumeSettingKey] = (double)AudioVolumes.Dialogue;
                _sceneManager.SaveGame(path);
                _gameState.NotificationCenter.Push(UiText.Get("游戏已存档。"), NotificationKind.Success);
            }
            catch (System.Exception ex)
            {
                _gameState.NotificationCenter.Push($"{UiText.Get("存档失败")}: {ex.Message}", NotificationKind.Error);
                Debug.LogError($"[SSNoir] SaveGame failed: {ex}");
            }
        }

        public void LoadGame(string? filePath = null)
        {
            var path = filePath ?? SaveManager.DefaultSavePath;
            if (!System.IO.File.Exists(path))
            {
                _gameState.NotificationCenter.Push(UiText.Get("没有找到存档文件。"), NotificationKind.Warning);
                Debug.LogWarning($"[SSNoir] No save file found at {path}");
                return;
            }
            try
            {
                ResetInventoryGainPulseBaseline();
                _sceneManager.LoadGame(path);
                if (_sceneManager.Settings.TryGetValue(ReduceMotionSettingKey, out object savedReduceMotion))
                {
                    if (savedReduceMotion is not bool reduceMotion)
                        throw new System.IO.InvalidDataException(
                            $"Save setting '{ReduceMotionSettingKey}' must be a boolean.");
                    MotionSettings.ReduceMotion = reduceMotion;
                }
                else
                {
                    MotionSettings.ReduceMotion = false;
                }
                AudioVolumes.Music = ReadVolumeSetting(
                    MusicVolumeSettingKey, AudioVolumes.MusicDefault);
                AudioVolumes.Sfx = ReadVolumeSetting(
                    SfxVolumeSettingKey, AudioVolumes.SfxDefault);
                AudioVolumes.Dialogue = ReadVolumeSetting(
                    DialogueVolumeSettingKey, AudioVolumes.DialogueDefault);
                _stateTainted = false;
                // OnSceneLoaded fires inside LoadGame → ResetSceneUiState → ResetUiState
                _gameState.NotificationCenter.Push(UiText.Get("游戏已读档。"), NotificationKind.Success);
            }
            catch (System.Exception ex)
            {
                _gameState.NotificationCenter.Push($"{UiText.Get("读档失败")}: {ex.Message}", NotificationKind.Error);
                Debug.LogError($"[SSNoir] LoadGame failed: {ex}");
                MarkStateTainted("读档失败", ex);
            }
        }

        public void RestartGame()
        {
            ResetInventoryGainPulseBaseline();
            _sceneManager.ResetForNewGame();
            MotionSettings.ReduceMotion = false;
            AudioVolumes.ResetToDefaults();
            _stateTainted = false;
        }

        private float ReadVolumeSetting(string key, float fallback)
        {
            if (!_sceneManager.Settings.TryGetValue(key, out object saved))
                return fallback;
            if (saved is not double volume)
                throw new System.IO.InvalidDataException(
                    $"Save setting '{key}' must be a number.");
            return UnityEngine.Mathf.Clamp01((float)volume);
        }

        private void MarkStateTainted(string operation, Exception exception)
        {
            _stateTainted = true;
            Debug.LogError(
                $"[SSNoir] {operation}; current state is no longer safe to continue or save. " +
                $"Start a new game or load a save.\n{exception}");
            _titleScreen.Open();
        }

        private void ResetInventoryGainPulseBaseline()
        {
            _inventoryPulseBaselineReady = false;
            _itemGainPulseUntil.Clear();
            // 读档和新游戏不是"发生了什么"，是换了一个世界：基线一起清掉，
            // 否则开局第一帧整条冷静会莫名其妙闪一下。
            _vitalPulseBaselineReady = false;
            _composurePulses.Clear();
            _latestVitalLossAt.Clear();
            _injuryPulse = null;
            _lastComposure.Clear();
            _lastInjurySeverity = 0;
        }

        /// <summary>
        /// 跑新游戏的开场动作。名字写在剧本的 '开场动作 全局里，客户端只负责按名字找节点、
        /// 执行它——开场里放什么片子、说什么话、给什么 spotlight，全在 .scm 那一边。
        /// 换章节、换开场都不该动这个文件。
        ///
        /// 剧本没定开场就什么都不做：直接落进世界，这是合法的开局方式。
        /// </summary>
        public void RunOpeningAction()
        {
            string actionName = _gameState.Get<string>("开场动作", string.Empty);
            if (string.IsNullOrWhiteSpace(actionName))
                return;

            var node = FindNodeByName(actionName);
            if (node == null)
            {
                // 不抛：开场缺一节戏也还是能玩，硬停在标题界面才是真的没救。
                Debug.LogWarning(
                    $"[SSNoir] 开场动作 '{actionName}' 在当前世界里找不到同名节点，直接进游戏。");
                return;
            }

            StartCoroutine(PlayOpeningAtPlace(node));
        }

        private IEnumerator PlayOpeningAtPlace(GameNode node)
        {
            // 开场先到动作所属地点；地点由内容树决定，不写死章节或住所名。
            var path = FindPathToNode(node.Name);
            if (path.Count >= 3)
            {
                EnterForcedPlace(path[path.Count - 2]);
                while (_cameraManager.IsFocusTravelInFlight || _stageController.IsTransitioning)
                    yield return null;
            }
            ExecuteNodeAction(node);
        }

        public bool IsDraggingResource => _resourceDragActive;

        public void BeginDieDrag(int dieIndex, int val, Vector2 mouse)
        {
            ResolveDieOwner(dieIndex, out string actorId, out int innerDieIndex);
            BeginResourceDrag(new SelectedResource
            {
                Type = "die",
                Value = val,
                SourceIndex = dieIndex,
                ActorId = actorId,
                DieIndex = innerDieIndex
            }, ResourceDragOriginKind.Hand);
        }

        public void BeginItemDrag(string itemName, int qty, Vector2 mouse)
        {
            BeginResourceDrag(new SelectedResource
            {
                Type = "item",
                ItemName = itemName,
                Value = qty
            }, ResourceDragOriginKind.Hand);
        }

        public void BeginSlotResourceDrag(GameNode node, int slotIndex)
        {
            var slots = GetSlotsForNode(node.Name);
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Count) return;

            var existing = slots[slotIndex];
            if (existing == null) return;

            ClearTransientNodeUiState(clearSlots: false);
            slots[slotIndex] = null;
            _resourceOriginNodeName = node.Name;
            _resourceOriginSlotIndex = slotIndex;
            _resourceOriginSlotResource = CloneSlottedResource(existing);
            BeginResourceDrag(CreateSelectedResourceFromSlot(existing), ResourceDragOriginKind.Slot);
        }

        public void MarkResourceDropHandled() => _resourceDropHandled = true;

        public void EndResourceDrag(Vector2 mouse)
        {
            if (!_resourceDragActive) return;
            _resourceDragActive = false;

            if (!_resourceDropHandled)
                RestoreResourceDragOrigin();

            ClearResourceDragState();
        }

        private void BeginResourceDrag(SelectedResource resource, ResourceDragOriginKind origin)
        {
            ReleaseRestCooldown();
            _selectedResource = resource;
            _resourceDragActive = true;
            _resourceDropHandled = false;
            _resourceDragOrigin = origin;
            if (origin != ResourceDragOriginKind.Slot)
            {
                _resourceOriginNodeName = string.Empty;
                _resourceOriginSlotIndex = -1;
                _resourceOriginSlotResource = null;
            }
        }

        private void RestoreResourceDragOrigin()
        {
            if (_resourceDragOrigin == ResourceDragOriginKind.Slot)
            {
                RestoreDraggedSlotResource();
            }

            _selectedResource = null;
        }

        private void RestoreDraggedSlotResource()
        {
            if (string.IsNullOrEmpty(_resourceOriginNodeName)
                || _resourceOriginSlotIndex < 0
                || _resourceOriginSlotResource == null)
            {
                Debug.LogError("[SSNoir] Resource drag lost its slot origin.");
                return;
            }

            var slots = GetSlotsForNode(_resourceOriginNodeName);
            if (slots == null || _resourceOriginSlotIndex >= slots.Count)
            {
                Debug.LogError($"[SSNoir] Resource drag origin slot is invalid: {_resourceOriginNodeName}[{_resourceOriginSlotIndex}].");
                return;
            }

            if (slots[_resourceOriginSlotIndex] != null)
            {
                Debug.LogError($"[SSNoir] Resource drag origin slot was unexpectedly occupied: {_resourceOriginNodeName}[{_resourceOriginSlotIndex}].");
                return;
            }

            slots[_resourceOriginSlotIndex] = CloneSlottedResource(_resourceOriginSlotResource);
        }

        private void ClearResourceDragState()
        {
            _selectedResource = null;
            _resourceDropHandled = false;
            _resourceDragOrigin = ResourceDragOriginKind.None;
            _resourceOriginNodeName = string.Empty;
            _resourceOriginSlotIndex = -1;
            _resourceOriginSlotResource = null;
        }

        private static SlottedResource CloneSlottedResource(SlottedResource resource)
        {
            return new SlottedResource
            {
                Type = resource.Type,
                ItemId = resource.ItemId,
                Value = resource.Value,
                SourceIndex = resource.SourceIndex,
                ActorId = resource.ActorId,
                DieIndex = resource.DieIndex,
                Qty = resource.Qty
            };
        }

        // Maps a flat action-die index (as enumerated by the hand panel:
        // Team.Actors in order, each actor's ActionDice in order) back to the
        // owning actor and that actor's local die index.
        /// <summary>
        /// 手牌里那个「第几颗骰」是一个**位置**，位置只有放在它被画出来的那份名单里才有意义。
        /// 所以这里数的是 <see cref="_displayedSnapshot"/>——HandPanelDrawer 数的就是它。
        ///
        /// 曾经这里数的是 _gameState.Team 的实时骰池。两份名单只在"没有任何事情正在发生"时
        /// 才一样：演出播放期间快照故意落后一拍，执行中途抛异常时引擎那半边已经变了。
        /// 一旦长度对不上，第 N 颗就指到别人头上，送进引擎就是
        /// "Action slot N has no available die"——错的不是那颗骰，是数它的那把尺子。
        /// </summary>
        private void ResolveDieOwner(int flatIndex, out string actorId, out int innerIndex)
        {
            int running = 0;
            foreach (var actor in _displayedSnapshot.Actors)
            {
                for (int i = 0; i < actor.ActionDice.Count; i++)
                {
                    if (running == flatIndex)
                    {
                        actorId = actor.Id;
                        innerIndex = actor.ActionDiceSlotIds[i];
                        return;
                    }
                    running++;
                }
            }
            actorId = string.Empty;
            innerIndex = -1;
        }

        /// <summary>
        /// 结束回合键此刻能不能按。整个回合转换（对方回应、新骰、强制行动）
        /// 期间一律不能；结算完还留一小段冷却，防止手抖连点把第二天也睡过去——冷却在
        /// 玩家去碰别的东西（聚焦卡片、拿起骰子/物品）时立刻解除，不让人干等。
        /// </summary>
        public bool CanRest => !_isEndingTurn && !IsInputLocked && Time.unscaledTime >= _restCooldownUntil;

        /// <summary>
        /// 当前正在演出的回合转换阶段。只给客户端标明“现在是谁在行动”，不参与规则结算。
        /// </summary>
        public RoundTransitionPhase? ActiveRoundTransitionPhase => _activeRoundTransitionPhase;

        /// <summary>
        /// 主动权不在玩家手里的那一段：对方回应 + 回合末的时间税。手牌区在这期间变暗——
        /// 骰子还在，但不是你的时候。新骰落定之后主动权就回来了，即使还有强制行动要演。
        /// </summary>
        public bool IsOpponentActing => _activeRoundTransitionPhase == RoundTransitionPhase.TimeTax
            || _activeRoundTransitionPhase == RoundTransitionPhase.OpponentRules
            || _activeRoundTransitionPhase == RoundTransitionPhase.RoundEndMaintenance;

        public void OnEndTurnClicked()
        {
            if (_stateTainted || !CanRest)
                return;
            StartCoroutine(EndTurnRoutine());
        }

        private IEnumerator EndTurnRoutine()
        {
            _isEndingTurn = true;
            _selectedResource = null;
            // 一整天都翻篇了，上一次结算的残影没有理由跨过日界线。
            // 这里不写 ?.：本方法下面就无条件用 _renderer 播休息演出，
            // 加问号只会让可空分析认为它可能为 null，反而给那一行凭空造一条警告。
            _renderer.ClearCardResidues();
            // 和 ExecuteRoutine 一样锁到整段演出结束。以前这里不锁：黑幕落地之后、骰子
            // 还在滚、auto-action 还没接管输入的那一两秒里，休息键是活的，再按一下就会在
            // 上一回合的 auto-action 还挂着的时候再结一次回合，引擎状态从此对不上。
            _renderer.SetInputLocked(true);

            RoundTransitionFrame? frame = null;
            Exception? failure = null;
            try
            {
                frame = _sceneManager.BeginRoundTransition();
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            while (failure == null && frame != null && !frame.IsFinished)
            {
                _activeRoundTransitionPhase = frame.Phase;
                if (frame.Phase == RoundTransitionPhase.TimeTax)
                {
                    // 时间税和按下结束回合是同一拍：冷静条当场弹一下、掉一格，画面同时开始变冷。
                    // 不等、不说话——这是你自己交出去的一手，不是别人做的事。
                    AdoptRoundTransitionFrame();
                    float until = Time.unscaledTime + 0.6f;
                    while (Time.unscaledTime < until)
                        yield return null;
                }
                else if (frame.Phase == RoundTransitionPhase.NewDice)
                {
                    AdoptRoundTransitionFrame();
                    yield return null;
                    while (HandPanelDrawer.HasUnsettledDice(_displayedSnapshot))
                        yield return null;
                }
                else
                {
                    bool batchDone = false;
                    bool landed = false;
                    try
                    {
                        if (frame.Phase == RoundTransitionPhase.ForcedAction)
                        {
                            // 自动行动卡自己有出场、吸骰、结算的节奏；播完再落快照。
                            _renderer.PlayBlockingPresentation(frame.Report.BlockingStorySteps, () => batchDone = true);
                        }
                        else
                        {
                            // 换场批先播完旧场景的步骤，再采纳新场景；同场景仍在拍的中段落地。
                            bool changesContext = _displayedSnapshot.IsInEncounter != frame.Snapshot.IsInEncounter
                                || !string.Equals(_displayedSnapshot.RootNode?.Name,
                                    frame.Snapshot.RootNode?.Name, StringComparison.Ordinal);
                            _renderer.PlayRoundBatch(frame.Report, frame.Snapshot,
                                land: () =>
                                {
                                    if (!changesContext)
                                    {
                                        AdoptRoundTransitionFrame();
                                        landed = true;
                                    }
                                },
                                done: () => batchDone = true);
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                        break;
                    }
                    while (!batchDone)
                        yield return null;
                    if (!landed)
                        AdoptRoundTransitionFrame();
                    if (frame.Report.PostSceneBlockingSteps.Count > 0)
                    {
                        bool postSceneDone = false;
                        _renderer.PlayBlockingPresentation(frame.Report.PostSceneBlockingSteps,
                            () => postSceneDone = true);
                        while (!postSceneDone)
                            yield return null;
                    }
                }

                try
                {
                    frame = _sceneManager.AdvanceRoundTransition();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            }

            _activeRoundTransitionPhase = null;
            _renderer.EndRoundTransitionPresentation();

            if (failure != null)
            {
                // 同 ExecuteRoutine 的善后：引擎半途抛出来时演出回调永远不会来，
                // 黑幕、焦点窗口、锁都得在这里亲手收掉，否则玩家留在一块黑屏里。
                Debug.LogError($"[EndTurn] Exception during turn end: {failure}");
                ShowNotification($"结束回合异常: {failure.Message}");
                _nodeSlots.Clear();
                ClearResourceDragState();
                _stageController.AbortTurnDip();
                EndIncomingFocusContext();
                MarkStateTainted("回合结算失败", failure);
            }

            _renderer.SetInputLocked(false);
            // 冷却只给城市：睡一觉是一下黑屏就过去的，手抖连点会把第二天也睡掉。
            // 交锋的回合转换本身要走好几秒、画面一路在变，新骰落定后键就该立刻是你的。
            if (!_displayedSnapshot.IsInEncounter)
                _restCooldownUntil = Time.unscaledTime + RestCooldownSeconds;
            _isEndingTurn = false;
        }

        private void AdoptRoundTransitionFrame()
        {
            CommitLatestSnapshotAtPresentationBoundary(resetUiOnContextChange: true);
        }

        /// <summary>
        /// 所有“表现已经走到落点”的路径都从这里采纳引擎快照。
        ///
        /// 采纳快照不是普通刷新：它可能重建导航栈、清掉旧卡片，并触发一次镜头上下文变更。
        /// 之前玩家动作和回合转换各自复制了一份这段逻辑，导致 EnterPlace 已经落地后，外层
        /// 回调又把旧 UI 清掉、再启动一次焦点运镜。现在边界只有一个入口，并且把“已经采纳”
        /// 作为正常的幂等结果处理。
        /// </summary>
        private bool CommitLatestSnapshotAtPresentationBoundary(bool resetUiOnContextChange)
        {
            if (ReferenceEquals(_displayedSnapshot, _sceneManager.LatestSnapshot))
            {
                EndIncomingFocusContext();
                return false;
            }

            string oldRoot = _displayedSnapshot.RootNode?.Name ?? string.Empty;
            string newRoot = _sceneManager.LatestSnapshot.RootNode?.Name ?? string.Empty;
            bool contextChanged = _displayedSnapshot.IsInEncounter != _sceneManager.LatestSnapshot.IsInEncounter
                || !string.Equals(oldRoot, newRoot, StringComparison.Ordinal);

            if (resetUiOnContextChange && contextChanged)
            {
                ResetSceneUiState();
                _renderer.SetInputLocked(true);
            }

            // 采纳前记下当前焦点机位：同根换区内 :anchor（Act2 巷口→货栈）不算
            // contextChanged，但分区机位必须跟着切，否则卡钉在新锚点上而镜头留在上一段。
            var focusCamBefore = ResolveCurrentFocusCamera(GetCurrentFocusPathNames(), out _);
            bool navigationCollapsed = AdoptLatestSnapshot();
            EndIncomingFocusContext();
            var focusCamAfter = ResolveCurrentFocusCamera(GetCurrentFocusPathNames(), out _);
            bool zoneCamChanged = focusCamAfter != null && focusCamAfter != focusCamBefore;
            if (contextChanged || navigationCollapsed || zoneCamChanged)
                UpdateCameraFocus(storyDriven: true);
            return true;
        }

        /// <summary>玩家碰了别的东西：休息键的冷却没有继续存在的理由。</summary>
        private void ReleaseRestCooldown() => _restCooldownUntil = 0f;

        // 这里曾有 OnUseEncounterConsumable / HasSelectedDie：烟和酒走引擎旁路的那条。
        // 它们现在是交锋树上的普通动作卡，走 ExecuteNodeAction，不需要专门的入口。

        /// <summary>
        /// 这一手是不是该在**按下去的那一刻**就开始黑。
        ///
        /// 翻页与否报告里已经写着了（<see cref="ActionReport.TurnEnded"/>），所以不必等演出播完
        /// 才知道——睡觉这件事该在手离开按钮时就开始，那根"执行中"的进度条本来演的就是
        /// 这一夜过去。唯一的例外是演出里还压着阻塞剧情：对白、告示卡、动画都是要人看的，
        /// 一开场就全黑等于把它们扔了。那种情况仍旧等演完再黑。
        /// </summary>
        public static bool ShouldDipEarly(ActionReport report)
            => report.TurnEnded
                && report.Type == ActionType.Instant
                && report.BlockingStorySteps.Count == 0;

        private static List<BlockingStoryStep> DetachPostTurnBlockingSteps(ActionReport report)
        {
            var result = new List<BlockingStoryStep>();
            if (!report.TurnEnded)
                return result;
            int firstAuto = report.BlockingStorySteps.FindIndex(
                step => step.Kind == BlockingStoryStepKind.AutoAction);
            if (firstAuto < 0)
                return result;
            for (int i = firstAuto; i < report.BlockingStorySteps.Count; i++)
                result.Add(report.BlockingStorySteps[i]);
            report.BlockingStorySteps.RemoveRange(
                firstAuto, report.BlockingStorySteps.Count - firstAuto);
            return result;
        }

        /// <summary>
        /// 世界真的换成下一拍的样子是在 <c>AdoptLatestSnapshot</c> 那一下；这个换页永远
        /// 发生在全黑里。早黑的那半程在演出开始时就起跑了，这里只负责收尾；没翻页就原样落地。
        /// </summary>
        private IEnumerator LandAfterTurnDip(ActionReport report, bool dippedEarly, Action land, Action onDone)
        {
            if (dippedEarly)
            {
                yield return _stageController.FinishTurnDip(land);
                onDone();
                yield break;
            }

            if (!report.TurnEnded)
            {
                land();
                onDone();
                yield break;
            }

            yield return _stageController.PlayTurnDip(land);
            onDone();
        }

        private IEnumerator PlayPostTurnStepsAfterDiceSettle(
            IReadOnlyList<BlockingStoryStep> steps, Action onDone)
        {
            // AdoptLatestSnapshot 在黑幕中换上新骰池；至少让手牌绘制一帧，建立滚骰状态，
            // 再等待这一把骰子真正落定，随后才让 auto-action 出牌并捕获它们。
            yield return null;
            while (HandPanelDrawer.HasUnsettledDice(_displayedSnapshot))
                yield return null;
            _renderer.PlayBlockingPresentation(steps, onDone);
        }

        private IEnumerator WaitForPresentation(Func<bool> isDone)
        {
            while (!isDone())
            {
                yield return null;
            }
        }

        public void NavigateToHome()
        {
            var homePath = FindPathToNode("家");
            if (homePath.Count < 2)
            {
                Debug.LogWarning("[SSNoir] Expected '家' node in world.");
                return;
            }

            bool alreadyAtHome = _navigationStack.Count > 0
                && _navigationStack[_navigationStack.Count - 1].Name == "家";

            ClearTransientNodeUiState(clearSlots: true);
            _selectedResource = null;

            if (alreadyAtHome)
            {
                return;
            }

            _navigationStack.Clear();
            foreach (var nodeName in homePath.GetRange(1, homePath.Count - 1))
            {
                var node = FindNodeByName(nodeName);
                Debug.Assert(node != null, $"[SSNoir] Failed to resolve path node '{nodeName}' while navigating home.");
                if (node != null)
                    _navigationStack.Add(node);
            }
            ResolveNavigationStack();
            UpdateCameraFocus();
            PlayArrival("家");
        }

        /// <summary>
        /// 动作演出中的强制落点（目前只用于倒下送医）。采纳引擎已经准备好的世界快照，
        /// 直接把导航栈落到地点；不触发普通 arrival，也不重置正在播放的阻塞演出队列。
        /// </summary>
        public void EnterForcedPlace(string placeName)
        {
            AdoptLatestSnapshot();
            var path = FindPathToNode(placeName);
            if (path.Count < 2)
                throw new InvalidOperationException($"强制进入地点失败：世界中找不到“{placeName}”。");

            _nodeSlots.Clear();
            _flippedNodes.Clear();
            _selectedResource = null;
            _focusedNodeName = string.Empty;
            _navigationStack.Clear();
            foreach (var nodeName in path.GetRange(1, path.Count - 1))
            {
                var node = FindNodeByName(nodeName)
                    ?? throw new InvalidOperationException($"强制进入地点失败：路径节点“{nodeName}”不存在。");
                _navigationStack.Add(node);
            }
            ResolveNavigationStack();
            UpdateCameraFocus(storyDriven: true);
        }

        /// <summary>
        /// 玩家真的走进了一个地点。只有三条路径会走到这里：从世界层点开地点卡，
        /// 主动回家，以及退回世界层。中间层之间的返回、读档恢复、快照刷新都不算到达，
        /// 不要在那些地方调。没有入场节拍时引擎返回 null，什么都不发生。
        /// </summary>
        private void PlayArrival(string placeName)
        {
            if (_stateTainted)
                return;
            ActionReport? report;
            try
            {
                report = _sceneManager.EnterPlace(placeName);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EnterPlace] Exception entering '{placeName}': {ex}");
                MarkStateTainted($"进入地点“{placeName}”失败", ex);
                return;
            }

            if (report == null)
            {
                // 没有入场节拍也可能改了树（on-enter-place 规则），把最新快照拿过来。
                AdoptLatestSnapshot();
                return;
            }

            StartCoroutine(ArrivalRoutine(report));
        }

        private IEnumerator ArrivalRoutine(ActionReport report)
        {
            _renderer.ClearCardResidues();
            _renderer.SetInputLocked(true);

            bool done = false;
            // 动作名留空：这不是一次动作，没有卡片可以锚定进度与投骰演出。
            _renderer.PlayPresentation(report, string.Empty, () =>
            {
                bool navigationCollapsed = AdoptLatestSnapshot();
                if (navigationCollapsed)
                    UpdateCameraFocus(storyDriven: true);
                done = true;
            });

            while (!done)
            {
                yield return null;
            }

            _renderer.SetInputLocked(false);
        }

        private void ClearTransientNodeUiState(bool clearSlots)
        {
            _renderer?.ClearCardResidues();
            if (clearSlots)
                _nodeSlots.Clear();
        }
    }
}
