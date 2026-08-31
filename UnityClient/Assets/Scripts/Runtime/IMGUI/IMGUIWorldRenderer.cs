#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public class IMGUIWorldRenderer : MonoBehaviour
    {
        private SSNoirGameManager _gameManager = null!;
        private IMGUIAnimationPlayer _animator = null!;
        private PresentationPlayer _presentationPlayer = null!;
        private UnityNarrationPlayer _narrationPlayer = null!;
        private DialogueVoicePlayer _voicePlayer = null!;
        private BanterPlayer _banterPlayer = null!;
        private ConversationPlayer _conversationPlayer = null!;
        private readonly DialogueAnchors _dialogueAnchors = new DialogueAnchors();
        private bool _warnedAboutCurrentDialogueRemoteFallback;
        private string? _activeAnimationTag;
        private float _animationTimer;

        // 这一步的收尾归过场播放器管，Update 里的占位倒计时要让开——否则两边都会去推进
        // 下一个剧情步骤，同一步走两次。
        private bool _animationOwnedByCutscene;

        private bool _isGrowthPanelOpen = false;
        private readonly IMGUIWindowStack _windowStack = new();

        private readonly Dictionary<string, Vector2> _cardCenters = new Dictionary<string, Vector2>();
        // 上一次解算给出的「相对锚点的偏移」，以及解算时各卡的占位尺寸。
        // 镜头在动的时候整套排布就靠这份偏移跟着锚点走，不重算——见 DrawCards 里的说明。
        private readonly Dictionary<string, Vector2> _stackOffsets = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, Vector2> _stackFootprints = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, Vector2> _lastAnchors = new Dictionary<string, Vector2>();
        // 起手就当作「已经停稳」，否则第一帧的卡片要先挤在锚点上等够静止时间才摊开。
        private float _anchorsStillFor = AnchorSettleTime;
        private bool _stackDirty = true;
        private readonly Dictionary<string, CardPresentationResidue> _cardResidues = new Dictionary<string, CardPresentationResidue>();
        // 关系浮层上一帧是否展开：只在「合→开」那一帧算作打开面板，不是每帧都算。
        private bool _relationWasExpanded = false;
        // 判定条、结果条等不参与卡片布局；它们在所有卡本体之后统一绘制，才不会被近景卡遮住。
        private readonly List<CardAttachmentOverlay> _cardAttachmentOverlays = new List<CardAttachmentOverlay>();
        // 本帧世界投射层的浓度，由 DrawCards 写、附件那一段读——附件画在卡片之后、
        // 物品栏之上，隔着几个绘制阶段，只能这样把浓度带过去。
        private float _cardLayerReveal = 1f;
        // 本帧的引线。与 attachment 相反，它们在所有卡本体**之前**统一绘制。
        private readonly List<CardLeaderLineDrawer.Tether> _tethers = new List<CardLeaderLineDrawer.Tether>();
        private readonly List<float> _gridScrollStack = new();
        private float _gridScrollOffset = 0f;
        // 本帧的顶栏布局。顶栏是「世界内容从哪开始」的唯一依据，网格视口和边缘信标都问它。
        private TopHudLayout _topHud;
        // 本帧钉住条实际占的位置。卡片要绕开它，和顶栏按钮一样。
        private Rect _pinStripRect;
        // 本帧场景标注带占了多大。卡片排布与网格视口都要按它往下让，所以它必须在
        // 任何卡片定位之前量好；没有场景标注时是一个零高度的空矩形。
        private Rect _sceneBandRect;
        // 触控拖拽滚动的状态：手机没有滚轮，卡片网格只能靠手指推。
        private bool _gridDragActive;
        private float _gridDragLastY;
        private float _gridDragTravel;
        private int _lastNavigationDepth = 0;
        // 教程要圈的那张卡：本帧画出来的第一张「能投骰子的动作卡」，屏幕坐标。
        // 网格与空间投射两条路都往这里报，教程那边不必知道当前是哪一种版面。
        private Rect? _firstActionCardRect;
        private ActionReport? _activeHeavyOutcome;
        private string _activeHeavyOutcomeActionName = string.Empty;
        private Action? _activeHeavyOutcomeDone;
        private readonly Queue<BlockingStoryStep> _pendingBlockingSteps = new Queue<BlockingStoryStep>();
        private readonly Queue<DialogueSequence> _pendingImmediateDialogues = new Queue<DialogueSequence>();
        private SpotlightCard? _activeActionSpotlight;
        private ActionReport? _completionReport;
        private string _completionActionName = string.Empty;
        private Action? _completionDone;

        public void Initialize(SSNoirGameManager gameManager)
        {
            _gameManager = gameManager;
            TutorialState.Bind(gameManager.GameState);
            _animator = gameObject.AddComponent<IMGUIAnimationPlayer>();
            _presentationPlayer = new PresentationPlayer(_animator);
            _narrationPlayer = gameObject.AddComponent<UnityNarrationPlayer>();
            _voicePlayer = gameObject.AddComponent<DialogueVoicePlayer>();
            _banterPlayer = new BanterPlayer(_voicePlayer);
            _conversationPlayer = new ConversationPlayer(_voicePlayer);
            _animator.OnAcknowledged = () => _presentationPlayer.OnRollAcknowledged();
            _gameManager.GameState.NarrationCenter.OnNarrationRequested += ShowNarration;
            _gameManager.GameState.DialogueCenter.OnBanterRequested += _banterPlayer.Enqueue;
            _gameManager.GameState.DialogueCenter.OnDialogueRequested += StartImmediateDialogue;
        }

        private void OnDestroy()
        {
            if (_gameManager != null)
            {
                _gameManager.GameState.NarrationCenter.OnNarrationRequested -= ShowNarration;
                _gameManager.GameState.DialogueCenter.OnBanterRequested -= _banterPlayer.Enqueue;
                _gameManager.GameState.DialogueCenter.OnDialogueRequested -= StartImmediateDialogue;
            }
        }

        // 动作外即时触发的阻塞对话:暂停 banter,演完恢复(不接入动作表现流程)。
        private void StartImmediateDialogue(SSNoir.Core.DialogueSequence sequence)
        {
            if (_conversationPlayer.IsActive)
            {
                _pendingImmediateDialogues.Enqueue(sequence);
                return;
            }

            _banterPlayer.Suspend();
            StartNextImmediateDialogue(sequence);
        }

        private void StartNextImmediateDialogue(SSNoir.Core.DialogueSequence sequence)
        {
            // 动作外即时对话仍以"此刻是否在场"校验说话人；普通 dialogue 不在场时会报警。
            DialogueStageDrawer.BeginConversation();
            _conversationPlayer.Start(sequence, () =>
            {
                if (_pendingImmediateDialogues.Count > 0)
                {
                    StartNextImmediateDialogue(_pendingImmediateDialogues.Dequeue());
                    return;
                }

                _banterPlayer.Resume();
            });
        }

        public bool IsPresentationActive => _presentationPlayer.IsPlaying || _animator.IsPlaying || _activeHeavyOutcome != null || _activeActionSpotlight != null || _conversationPlayer.IsActive || _activeAnimationTag != null;

        public void PlayPresentation(ActionReport report, string actionName, Action onDone)
        {
            _pendingBlockingSteps.Clear();
            _pendingImmediateDialogues.Clear();
            foreach (var step in report.BlockingStorySteps)
                _pendingBlockingSteps.Enqueue(step);
            _completionReport = report;
            _completionActionName = actionName;
            _completionDone = onDone;

            // 「冷静与伤势」讲的是不顺利会怎样，所以挂在第一次坏结果上，不挂在开局。
            if (report.Type == ActionType.Roll && report.Outcome == RollOutcome.Fail)
                TutorialDirector.RequestOnFailedRoll();

            _presentationPlayer.Play(report, actionName, () =>
            {
                if (OutcomePresentationPolicy.ShouldUseOutcomeModal(report, actionName))
                {
                    _activeHeavyOutcome = report;
                    _activeHeavyOutcomeActionName = actionName;
                    _activeHeavyOutcomeDone = onDone;
                    return;
                }

                AdvanceToBlockingPresentationOrFinish();
            });
        }

        // 按 Scheme 调用顺序逐个播放阻塞剧情步骤;每个步骤完成后回调本方法推进下一个。
        private void AdvanceToBlockingPresentationOrFinish()
        {
            if (_pendingBlockingSteps.Count > 0)
            {
                var step = _pendingBlockingSteps.Dequeue();
                switch (step.Kind)
                {
                    case BlockingStoryStepKind.Spotlight:
                        _activeActionSpotlight = step.Spotlight;
                        return;
                    case BlockingStoryStepKind.Dialogue:
                        _banterPlayer.Suspend();   // 对话聚焦,杂音让位
                        // 动作内先尝试锚定动作前画面；普通对话失败时由绘制层报警并降级为场外卡，
                        // 显式 remote 则直接允许场外说话人且不报警。
                        DialogueStageDrawer.BeginConversation();
                        _conversationPlayer.Start(step.Dialogue!, () =>
                        {
                            _banterPlayer.Resume();
                            AdvanceToBlockingPresentationOrFinish();
                        });
                        return;
                    case BlockingStoryStepKind.Animation:
                        PlayAnimationStep(step.AnimationTag);
                        return;
                    case BlockingStoryStepKind.EnterPlace:
                        _gameManager.EnterForcedPlace(step.PlaceName);
                        // 位置切换本身没有确认按钮；下一张倒下卡立刻在诊所画面上接管输入。
                        AdvanceToBlockingPresentationOrFinish();
                        return;
                }
            }

            var report = _completionReport;
            var actionName = _completionActionName;
            var done = _completionDone;
            _completionReport = null;
            _completionActionName = string.Empty;
            _completionDone = null;

            if (report != null)
                AddLightResidueIfNeeded(report, actionName);
            done?.Invoke();   // adopt 最新快照
            if (report != null)
            {
                ReleaseNarrations(report.NarrationIds);
                // banter 在 adopt 之后释放,锚定动作后的新快照(避免提前剧透)
                foreach (var sequence in report.Banter)
                    _banterPlayer.Enqueue(sequence);
            }
        }

        /// <summary>
        /// 剧本里的 (play-animation! "tag") 落到这里：tag 认场景里同名的 CutsceneSequence，
        /// 找到就播那场过场，播完（或被 ESC 跳过）再推进下一个阻塞步骤。
        ///
        /// 找不到不算错，退回原来的定时占位就行——剧本先行、镜头后补是常态，不该因为镜头还
        /// 没配就把剧情卡死。但要出声，否则配错名字会表现成"过场莫名其妙没播"。
        /// </summary>
        private void PlayAnimationStep(string? tag)
        {
            var sequence = CutsceneSequence.Find(tag ?? string.Empty);
            if (sequence != null)
            {
                _activeAnimationTag = tag;
                _animationOwnedByCutscene = true;
                _gameManager.Cutscene.Play(sequence, () =>
                {
                    _activeAnimationTag = null;
                    _animationOwnedByCutscene = false;
                    AdvanceToBlockingPresentationOrFinish();
                });
                return;
            }

            Debug.LogWarning(
                $"[SSNoir] play-animation! 的 tag '{tag}' 在场景里找不到对应的 CutsceneSequence，"
                + "这一步按占位时长跳过。");
            _activeAnimationTag = tag;
            _animationTimer = AnimationPlaceholderSeconds;
        }

        private const float AnimationPlaceholderSeconds = 0.8f;

        private void ReleaseNarrations(System.Collections.Generic.List<string> ids)
        {
            foreach (var id in ids)
                _gameManager.GameState.NarrationCenter.Play(id);
        }

        private void ShowNarration(string id)
        {
            _narrationPlayer.Play(id);
        }

        private void ConfirmActionSpotlight()
        {
            _activeActionSpotlight = null;
            AdvanceToBlockingPresentationOrFinish();
        }

        public void AcknowledgePresentationRoll()
        {
            _presentationPlayer.OnRollAcknowledged();
        }

        public void ShowNotification(string message)
        {
            _gameManager.GameState.NotificationCenter.Push(message, NotificationKind.Info);
        }

        public bool IsAnimationPlaying => _animator.IsPlaying || _presentationPlayer.IsPlaying || _activeHeavyOutcome != null || _activeActionSpotlight != null || _conversationPlayer.IsActive || _activeAnimationTag != null;
        public bool IsAnimationReadyToAcknowledge => _animator != null && _animator.IsReadyToAcknowledge();
        public bool IsInputLocked => _inputLocked || _activeHeavyOutcome != null || _activeActionSpotlight != null || _gameManager.GameState.SpotlightCenter.HasSpotlight || _conversationPlayer.IsActive || _activeAnimationTag != null || _gameManager.Cutscene.IsActive || _gameManager.Title.IsActive;

        // 与对白舞台上的左键点击共用同一套推进语义：打字中先显示全文，否则进入下一句。
        // 由 SSNoirGameManager 的全局 ESC 输入调用，避免 ESC 在对白期间落入返回导航逻辑。
        public bool TryAdvanceConversation()
        {
            if (!_conversationPlayer.IsActive)
                return false;

            if (DialogueStageDrawer.IsCurrentLineFullyRevealed)
            {
                _conversationPlayer.Advance();
                _warnedAboutCurrentDialogueRemoteFallback = false;
            }
            else
            {
                DialogueStageDrawer.CompleteCurrentLine();
            }

            return true;
        }

        // 动作内 Spotlight 与全局 Spotlight 都是“确认后继续”的阻塞层。
        public bool TryAdvanceSpotlight()
        {
            if (_activeActionSpotlight != null)
            {
                ConfirmActionSpotlight();
                return true;
            }

            if (!_gameManager.GameState.SpotlightCenter.HasSpotlight)
                return false;

            _gameManager.GameState.SpotlightCenter.Dismiss();
            return true;
        }

        // 重结算结果弹窗的按钮与 ESC 都表示确认并进入后续表现。
        public bool TryConfirmHeavyOutcome()
        {
            if (_activeHeavyOutcome == null)
                return false;

            _activeHeavyOutcome = null;
            _activeHeavyOutcomeActionName = string.Empty;
            _activeHeavyOutcomeDone = null;
            AdvanceToBlockingPresentationOrFinish();
            return true;
        }

        // 普通界面面板不参与剧情推进；ESC 只关闭当前最上层面板。
        public bool TryCloseUiPanel()
        {
            if (_isGrowthPanelOpen)
            {
                _isGrowthPanelOpen = false;
                return true;
            }

            if (NavigationDrawer.IsRelationExpanded)
            {
                NavigationDrawer.CollapseRelation();
                return true;
            }

            if (DebugPanelDrawer.IsOpen)
            {
                DebugPanelDrawer.Close();
                return true;
            }

            if (DossierPanelDrawer.IsOpen)
            {
                DossierPanelDrawer.Close();
                return true;
            }

            if (SettingsPanelDrawer.IsOpen)
            {
                SettingsPanelDrawer.Close();
                return true;
            }

            return false;
        }

        // Whether the pointer was over any interactive UI in the last OnGUI pass.
        public bool PointerOverUI { get; private set; }

        private bool _inputLocked = false;
        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;
        }

        // Called on scene load/reset to close all overlay panels.
        public void ResetUiState()
        {
            _isGrowthPanelOpen = false;
            _inputLocked = false;
            _gridScrollOffset = 0f;
            _gridScrollStack.Clear();
            _lastNavigationDepth = 0;
            _relationWasExpanded = false;
            _cardResidues.Clear();
            _activeHeavyOutcome = null;
            _activeHeavyOutcomeActionName = string.Empty;
            _activeHeavyOutcomeDone = null;
            _pendingBlockingSteps.Clear();
            _activeActionSpotlight = null;
            _activeAnimationTag = null;
            _animationTimer = 0f;
            _animationOwnedByCutscene = false;
            _banterPlayer.Reset();
            _conversationPlayer.Reset();
            DialogueStageDrawer.BeginConversation();
            _warnedAboutCurrentDialogueRemoteFallback = false;
            _completionReport = null;
            _completionActionName = string.Empty;
            _completionDone = null;
            DebugPanelDrawer.Reset();
            SettingsPanelDrawer.Reset();
            HelpPanelDrawer.Reset();
            TutorialDirector.Reset();
        }

        public void ClearCardResidues()
        {
            _cardResidues.Clear();
            _completionReport = null;
            _completionActionName = string.Empty;
            _completionDone = null;
        }

        private void Update()
        {
            _gameManager.GameState.NotificationCenter.Update(Time.deltaTime);
            _presentationPlayer.Update(Time.deltaTime);
            _banterPlayer.Update(Time.deltaTime);

            // 命名动画占位:到点后推进下一个阻塞剧情步骤。
            // 归过场管的那种不在这里收尾，它自己播完会回调。
            if (_activeAnimationTag != null && !_animationOwnedByCutscene)
            {
                _animationTimer -= Time.deltaTime;
                if (_animationTimer <= 0f)
                {
                    _activeAnimationTag = null;
                    AdvanceToBlockingPresentationOrFinish();
                }
            }

            _animator.Update();

            // 过场首帧截图。截的是纯世界，界面开着照样干净，所以随时按都行，也不挑时机。
            if (Input.GetKeyDown(KeyCode.F9))
            {
                CinematicCapture.Capture(this, 1080);
            }
            else if (Input.GetKeyDown(KeyCode.F10))
            {
                CinematicCapture.Capture(this, 2160);
            }

            // Right-click to cancel selection
            if (Input.GetMouseButtonDown(1)
                && !IsAnimationPlaying
                && !_isGrowthPanelOpen
                && !DebugPanelDrawer.IsOpen
                && !_inputLocked
                // 影幕期间没有任何 UI 反馈，右键把选中的资源清掉，玩家出来才发现，等于静默丢状态。
                && !_gameManager.Cutscene.IsActive)
            {
                if (_gameManager.SelectedResource != null)
                {
                    _gameManager.ClearSelectedResource();
                }
            }
        }

        private void OnGUI()
        {
            // MouseDrag 必须放行：触控上的「滑动 vs 轻点」判定和卡片网格的拖拽滚动都靠它。
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown
                && Event.current.type != EventType.MouseUp && Event.current.type != EventType.MouseDrag
                && Event.current.type != EventType.ScrollWheel
                && Event.current.type != EventType.Layout)
                return;

            // 图层透明度每帧归位。它是全局状态，Begin / End 中间断一次就会一直半透明下去。
            IMGUIStyles.ResetLayer();

            // 把整个 IMGUI 放进按设备物理尺寸推导出来的虚拟画布里。
            UIScale.Apply();

            // 指针状态要在 GUI.matrix 生效之后再读——mousePosition 是按当前矩阵换算的，
            // 在 Apply 之前取到的是物理坐标，和后面所有比较的坐标系对不上。
            IMGUIInteractionContext.NotePointerEvent(Event.current);

            // 触控设备上「悬停」只在手指按住时存在；手指抬起后 mousePosition 停在最后触点，
            // 不把这件事说清楚的话，最后碰过的控件会一直亮着。
            IMGUIInteractionContext.SetHoverAvailable(
                UIScale.HasHoverPointer || Input.GetMouseButton(0) || Input.GetMouseButton(1));

            // Initialize styles if needed
            IMGUIStyles.Init(_gameManager.ChineseFont, _gameManager.SemiboldFont);

            // ── 标题菜单 ──
            // 和影幕同一个位置、同一个道理：菜单在的时候世界照常渲染（那就是背景），但一个
            // 游戏控件都不出现。PointerOverUI 置真，镜头也就跟着不接受拖拽了。
            if (_gameManager.Title.IsActive)
            {
                _gameManager.Title.Draw(Event.current.mousePosition);
                PointerOverUI = true;
                IMGUIInteractionContext.FinishPointerEvent(Event.current);
                return;
            }

            // ── 影幕 ──
            // 排在最前面，画完就走：过场期间一个游戏控件都不该出现，玩家看到的只有实时世界
            // （或盖在上面的视频）加黑边。世界本身照常渲染，所以黑边压下来的过程中背景是活的，
            // 视频首帧接上去才不会有缝。
            if (_gameManager.Cutscene.IsActive)
            {
                IMGUIInteractionContext.ConsumeCurrentPress();
                _gameManager.Cutscene.Draw();
                PointerOverUI = true;
                IMGUIInteractionContext.FinishPointerEvent(Event.current);
                return;
            }

            if (_gameManager.DisplayedSnapshot.Failure.IsFailed)
            {
                DrawFailureOverlay(Event.current.mousePosition);
                PointerOverUI = true;
                IMGUIInteractionContext.FinishPointerEvent(Event.current);
                return;
            }
            SyncNavigationScrollState();

            Vector2 mouse = Event.current.mousePosition;
            bool mouseDown = Event.current.type == EventType.MouseDown && Event.current.button == 0;
            _windowStack.BeginFrame(mouse, mouseDown);

            // Reset the pointer-over-UI accumulator; widgets set it via CanHover
            // during this pass, and we persist the result at the end of OnGUI.
            IMGUIInteractionContext.ResetPointerOverUi();
            _firstActionCardRect = null;
            TopHudLayout topHud = TopHudLayout.Create();
            _topHud = topHud;

            if (DossierPanelDrawer.IsOpen && !_isGrowthPanelOpen)
            {
                var (_, dossierPanelRect) = DossierPanelDrawer.GetRects(topHud);
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.DossierPanel,
                    Bounds = dossierPanelRect,
                    Layer = IMGUIWindowLayer.Panel,
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            if (SettingsPanelDrawer.IsOpen && !_isGrowthPanelOpen)
            {
                var (_, settingsPanelRect) = SettingsPanelDrawer.GetRects(topHud);
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.SettingsPanel,
                    Bounds = settingsPanelRect,
                    Layer = IMGUIWindowLayer.Panel,
                    // 现在是跟成长面板同一套居中纸卡模态，也要跟成长面板一样挡住整屏——
                    // 否则背后世界还能被点到（相机拖拽/卡片点击穿透）。
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            if (HelpPanelDrawer.IsOpen && !_isGrowthPanelOpen)
            {
                var (_, helpPanelRect) = HelpPanelDrawer.GetRects(topHud);
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.HelpPanel,
                    Bounds = helpPanelRect,
                    Layer = IMGUIWindowLayer.Panel,
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            // 教程是模态：它一出现，底下的世界和面板都不该被点到——正在教你怎么点，
            // 你却先点到了别处，是最坏的一种混乱。
            if (TutorialDirector.IsOpen)
            {
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.Tutorial,
                    Bounds = new Rect(0, 0, UIScale.VW, UIScale.VH),
                    Layer = IMGUIWindowLayer.Modal,
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            if (DebugPanelDrawer.IsOpen && !_isGrowthPanelOpen)
            {
                var (_, debugPanelRect) = DebugPanelDrawer.GetRects(topHud);
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.DebugPanel,
                    Bounds = debugPanelRect,
                    Layer = IMGUIWindowLayer.Panel,
                    BlockMode = IMGUIBlockMode.Bounds,
                    CloseOnClickedOutside = false,
                });
            }

            if (_isGrowthPanelOpen)
            {
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.GrowthPanel,
                    Bounds = GrowthPanelDrawer.GetPanelRect(),
                    Layer = IMGUIWindowLayer.Panel,
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            if (_activeHeavyOutcome != null)
            {
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.HeavyOutcome,
                    Bounds = new Rect(0, 0, UIScale.VW, UIScale.VH),
                    Layer = IMGUIWindowLayer.Modal,
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            if (_activeActionSpotlight != null || _gameManager.GameState.SpotlightCenter.HasSpotlight)
            {
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.Spotlight,
                    Bounds = new Rect(0, 0, UIScale.VW, UIScale.VH),
                    Layer = IMGUIWindowLayer.Modal,
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            // 阻塞对话:全屏 blocker 锁住下层(表现上不画遮罩),点击由顶层 overlay 消费来推进。
            if (_conversationPlayer.IsActive)
            {
                _windowStack.Register(new IMGUIWindowBlocker
                {
                    Id = IMGUIWindowId.Conversation,
                    Bounds = new Rect(0, 0, UIScale.VW, UIScale.VH),
                    Layer = IMGUIWindowLayer.Modal,
                    BlockMode = IMGUIBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            bool baseLocked = IsInputLocked || IsAnimationPlaying;
            if (baseLocked)
                IMGUIInteractionContext.ConsumeCurrentPress();
            _windowStack.Update();
            _dialogueAnchors.Clear();

            // 你正身处其中的那些容器（导航栈）也是在场的人/地点，只是没有卡——进入「夜莺」
            // 之后她本人依然能开口。锚在面包屑上（气泡会翻到它下方），先登记，这样同名的
            // 真实卡片随后覆盖它，卡片优先。
            foreach (var container in _gameManager.NavigationStack)
                _dialogueAnchors.RegisterNode(container.Name, topHud.Breadcrumb);

            var worldUi = _windowStack.MakeContext(IMGUIWindowLayer.World, baseLocked);
            var panelUi = _windowStack.MakeContext(IMGUIWindowLayer.Panel, baseLocked);

            // ── Navigation Bar ──
            NavigationDrawer.Draw(_gameManager, worldUi, topHud);
            // 展开的关系进展图是显式的 HUD 浮层；锁住其后的世界控件，避免点击穿透。
            if (NavigationDrawer.IsRelationExpanded)
            {
                if (!_relationWasExpanded)
                    ClearCardResidues();
                worldUi = _windowStack.MakeContext(IMGUIWindowLayer.World, true);
            }
            _relationWasExpanded = NavigationDrawer.IsRelationExpanded;

            // 半身像只负责把人物钉在场景里；可读、可点的卡片与附件必须永远压在它上面。
            HandPanelDrawer.DrawPortraits(_gameManager);

            // ── Node Cards (3D projected) ──
            DrawCards(worldUi);
            foreach (var kv in _cardCenters)
                _dialogueAnchors.RegisterNode(kv.Key, new Rect(kv.Value.x - 60f, kv.Value.y - 80f, 120f, 160f));

            // ── Bottom Panel ──
            // 结果 attachment 的视觉层在物品栏之后；点击消费提前做，避免结果盖住物品栏后
            // 视觉层在前、交互层却误点到底下的物品。
            // 影子状态的卡片不接点击，它的附件同理。
            if (_cardLayerReveal >= 0.999f)
                HandleCardAttachmentTaps(worldUi);
            HandPanelDrawer.Draw(_gameManager, worldUi, _dialogueAnchors);
            float attachmentRestore = IMGUIStyles.BeginLayer(_cardLayerReveal);
            DrawCardAttachmentOverlays(worldUi);
            IMGUIStyles.EndLayer(attachmentRestore);

            // ── Growth / Team Toggle Button ──
            DrawGrowthToggleButton(worldUi, topHud.GrowthToggle);

            // ── Settings / Debug 顶部按钮 ──
            // 三个面板（成长/设置/Debug）都不能整段跳过绘制——之前那样做会让按钮凭空消失，
            // 很突兀。改成始终画出来，被更高优先级面板占屏时只是传一个强制锁定的 ui 上下文，
            // 按钮可见但点不动。优先级：成长 > 设置 > Debug；打开谁就顺手关掉下面优先级的
            // 面板，避免两个居中纸卡模态叠在一起抢点击。
            var lockedPanelUi = _windowStack.MakeContext(IMGUIWindowLayer.Panel, true);

            // 卷宗：城里的事，交锋里不给入口。
            if (!_gameManager.DisplayedSnapshot.IsInEncounter)
            {
                bool dossierWasOpen = DossierPanelDrawer.IsOpen;
                DossierPanelDrawer.Draw(_gameManager, _isGrowthPanelOpen ? lockedPanelUi : panelUi, topHud);
                if (!dossierWasOpen && DossierPanelDrawer.IsOpen)
                {
                    ClearCardResidues();
                    SettingsPanelDrawer.Close();
                    DebugPanelDrawer.Close();
                }
            }
            else
            {
                // 交锋开场时把它收起来：否则打完回到城里，面板会自己弹回来。
                DossierPanelDrawer.Close();
            }

            var settingsUi = (_isGrowthPanelOpen || DossierPanelDrawer.IsOpen) ? lockedPanelUi : panelUi;
            bool settingsWasOpen = SettingsPanelDrawer.IsOpen;
            SettingsPanelDrawer.Draw(settingsUi, topHud);
            if (!settingsWasOpen && SettingsPanelDrawer.IsOpen)
            {
                ClearCardResidues();
                DebugPanelDrawer.Close();
            }

            var helpUi = (_isGrowthPanelOpen || DossierPanelDrawer.IsOpen || SettingsPanelDrawer.IsOpen)
                ? lockedPanelUi : panelUi;
            bool helpWasOpen = HelpPanelDrawer.IsOpen;
            HelpPanelDrawer.Draw(helpUi, topHud);
            if (!helpWasOpen && HelpPanelDrawer.IsOpen)
            {
                ClearCardResidues();
                DebugPanelDrawer.Close();
            }

            var debugUi = (_isGrowthPanelOpen || SettingsPanelDrawer.IsOpen || DossierPanelDrawer.IsOpen
                    || HelpPanelDrawer.IsOpen)
                ? lockedPanelUi : panelUi;
            DebugPanelDrawer.Draw(_gameManager, debugUi, topHud);

            // 大型关系进展图最后绘制在世界控件之上。
            NavigationDrawer.DrawRelationOverlay(_gameManager.DisplayedSnapshot, topHud);

            // ── Overlays ──
            OverlayDrawer.DrawNotifications(_gameManager.GameState.NotificationCenter);
            OverlayDrawer.DrawCursorFollower(_gameManager);
            DrawPresentationOverlay();
            DrawBanterOverlay();
            DrawHeavyOutcomeOverlay();
            DrawSpotlightOverlay();
            DrawAnimationOverlay();
            DrawNarrationOverlay();

            // ── Growth Panel ──
            if (_isGrowthPanelOpen)
            {
                var growthInteraction = GrowthPanelDrawer.Draw(_gameManager, panelUi);
                if (growthInteraction.ShouldClose)
                {
                    _isGrowthPanelOpen = false;
                }
            }

            // ── 教程提示 ──
            // 观察放在所有卡片画完之后：这一帧到底有没有动作卡、第一张在哪儿，这时才知道。
            // 演出/对白/其它模态在跑的时候不弹——教程要指的东西这会儿正被盖着。
            bool tutorialQuiet = !IsPresentationActive && !_isGrowthPanelOpen
                && !DossierPanelDrawer.IsOpen && !SettingsPanelDrawer.IsOpen && !HelpPanelDrawer.IsOpen;
            if (tutorialQuiet)
                TutorialDirector.Observe(_gameManager, _firstActionCardRect.HasValue);
            if (TutorialDirector.IsOpen && tutorialQuiet)
                TutorialDirector.Draw(
                    _gameManager,
                    _windowStack.MakeContext(IMGUIWindowLayer.Modal),
                    _firstActionCardRect);

            // ── Animation Modal ──
            _animator.DrawModal();

            // 阻塞对白必须盖住成长面板和其余可交互 UI；底层同时由 IsInputLocked 显式禁用。
            DrawConversationOverlay();

            // ── Stage Transition Flash ──
            var stageCtrl = _gameManager.StageController;
            if (stageCtrl.FadeAlpha > 0f)
            {
                var oldColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, stageCtrl.FadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, UIScale.VW, UIScale.VH), Texture2D.whiteTexture);
                GUI.color = oldColor;
            }

            // Persist whether the pointer is over any UI this pass, so the camera
            // manager (which polls raw Input in Update, one step ahead of OnGUI)
            // can skip starting a drag that begins on the UI.
            PointerOverUI = baseLocked
                || IMGUIInteractionContext.PointerOverUi
                || _windowStack.IsPointerOverBlocker();

            // Resolve a resource drag on release. rawType (not type) so this still
            // fires when a slot already consumed the MouseUp to place the token.
            if (Event.current.rawType == EventType.MouseUp && _gameManager.IsDraggingResource)
            {
                _gameManager.EndResourceDrag(Event.current.mousePosition);
            }

            IMGUIInteractionContext.FinishPointerEvent(Event.current);
        }

        // 失败卡。这一屏是一章的句号，也是玩家在这一局里读到的最后一段字——
        // 它的排版不能比路边一张动作卡还随便。
        //
        // 上一版三个具体的毛病，都不是"审美问题"，是排版规则用错了：
        //   · 正文居中。中文段落居中排，最后一行常常只剩一个句号孤零零挂在中间。
        //     叙述性段落一律左对齐——居中只留给标题这种一两行的东西。
        //   · 正文框写死 56px 高，而三行 18px 的字要 80 上下。文字从框里溢出去，
        //     和下面的分隔线挤在一起。现在高度由 CalcHeight 量出来，卡的高度跟着走，
        //     以后写多长的失败文案都不会再撞。
        //   · 「退出游戏」直接借了 StatusLabel，那是个 MiddleLeft、给暗底用的样式，
        //     落在纸白卡上就是一行左对齐的浅灰字。按钮的样式在这儿现配，不借共享静态的。
        private void DrawFailureOverlay(Vector2 mouse)
        {
            var failure = _gameManager.DisplayedSnapshot.Failure;
            var screen = new Rect(0f, 0f, UIScale.VW, UIScale.VH);
            GUI.DrawTexture(screen, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                new Color(0.02f, 0.03f, 0.05f, 0.92f), 0f, 0f);

            const float cardWidth = 560f;
            const float padX = 52f;
            const float padTop = 46f;
            const float padBottom = 40f;
            const float ruleWidth = 44f;     // 标题上方那一道红杠：印章的语气，不是装饰线
            const float ruleHeight = 3f;
            const float gapRuleToTitle = 18f;
            const float gapTitleToBody = 20f;
            const float gapBodyToDivider = 28f;
            const float gapDividerToButtons = 24f;
            const float gapBetweenButtons = 10f;
            const float primaryH = 44f;
            const float secondaryH = 38f;
            float contentW = cardWidth - padX * 2f;

            var titleStyle = new GUIStyle(IMGUIStyles.ModalTitle)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                fontSize = IMGUIStyles.FontSize(32),
                normal = { textColor = IMGUIStyles.SealRed },
            };
            // 正文自带一份样式，不从 StatusLabel 派生：那一份是 MiddleLeft + 暗底配色，
            // 每次都要改两个字段才能用在纸上，改漏一个就是上一版那行浅灰字。
            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(17),
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                richText = false,
                normal = { textColor = new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.88f) },
            };

            float titleH = Mathf.Max(
                titleStyle.CalcHeight(new GUIContent(failure.Title), contentW),
                IMGUIStyles.FontSize(32) * 1.3f);
            float bodyH = string.IsNullOrEmpty(failure.Description)
                ? 0f
                : bodyStyle.CalcHeight(new GUIContent(failure.Description), contentW);

            // 卡有多高由内容说了算。写死高度是上一版文字溢出来的根源。
            float cardHeight = padTop + ruleHeight + gapRuleToTitle + titleH
                             + (bodyH > 0f ? gapTitleToBody + bodyH : 0f)
                             + gapBodyToDivider + 1f + gapDividerToButtons
                             + primaryH + gapBetweenButtons + secondaryH + padBottom;

            var card = UIScale.PixelSnap(UIScale.CenteredModal(cardWidth, cardHeight));
            IMGUIStyles.DrawShadow(card, new Vector2(6f, 7f), 0.55f);
            GUI.DrawTexture(card, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                IMGUIStyles.Paper, 0f, 0f);
            IMGUIStyles.DrawOutline(card, 1f, IMGUIStyles.PaperInk);

            float x = card.x + padX;
            float y = card.y + padTop;

            GUI.color = IMGUIStyles.SealRed;
            GUI.DrawTexture(new Rect(x, y, ruleWidth, ruleHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            y += ruleHeight + gapRuleToTitle;

            IMGUIStyles.DrawLabel(new Rect(x, y, contentW, titleH), failure.Title, titleStyle);
            y += titleH;

            if (bodyH > 0f)
            {
                y += gapTitleToBody;
                IMGUIStyles.DrawLabel(new Rect(x, y, contentW, bodyH), failure.Description, bodyStyle);
                y += bodyH;
            }

            y += gapBodyToDivider;
            IMGUIStyles.DrawLine(new Vector2(x, y), new Vector2(card.xMax - padX, y),
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.30f), 1f);
            y += 1f + gapDividerToButtons;

            // 两个按钮的字都在这儿现配成居中的深色：DrawTechnicalButton 会拿描边色去写字，
            // 所以主次之分靠 alpha（0.85 / 0.55），不靠换一个给暗底用的样式。
            var buttonLabel = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(16),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
            };
            IMGUIStyles.ApplyStrongFont(buttonLabel);

            var ui = new IMGUIInteractionContext(mouse, isLocked: false);
            var restart = new Rect(x, y, contentW, primaryH);
            var quit = new Rect(x, y + primaryH + gapBetweenButtons, contentW, secondaryH);

            // 两个各自判断。上一版写成 if / else if——重新开始被点中的那一帧，
            // 退出按钮整个不画，屏幕上会缺一块。
            bool restartClicked = IMGUIButton.Draw(restart, "重新开始", ui,
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.85f),
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.10f),
                buttonLabel);
            bool quitClicked = IMGUIButton.Draw(quit, "退出游戏", ui,
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.55f),
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.06f),
                buttonLabel);

            if (restartClicked)
                _gameManager.RestartGame();
            else if (quitClicked)
                Application.Quit();
        }

        private void DrawCards(IMGUIInteractionContext ui)
        {
            var cam = Camera.main;
            if (cam == null) return;
            _cardAttachmentOverlays.Clear();

            var nodes = _gameManager.VisibleNodes;
            var focusedName = _gameManager.FocusedNodeName;
            // 建筑镜头下卡片退到左右两条栏里，把中间让给建筑；城市总览保持贴着锚点上浮。
            bool inGutters = CardGutterLayout.IsActive(_gameManager.CurrentFocusCamera);
            // 世界投射层跟着换镜一起显形：新那一镜露出多少，这一层就画多浓（见下面
            // BeginLayer 那一段）。低动画换镜之外恒为 1，什么都不变。
            float viewReveal = _gameManager.CameraManager.ReducedViewReveal;
            bool swappingView = viewReveal < 1f;
            // 附件（判定条、结果条）画在别处、隔着几个绘制阶段，浓度只能这样带过去。
            _cardLayerReveal = viewReveal;

            // Split nodes into two groups: those with world anchors and those without
            var initialProjected = new List<(GameNode node, string anchorKey, Vector3 screenPos, float distance, int order)>();
            var projectedResidues = new List<(CardPresentationResidue residue, Vector3 screenPos, float distance)>();
            var gridNodes = new List<GameNode>();
            var gridResidues = new List<CardPresentationResidue>();
            var sceneNotes = new List<GameNode>();
            var importantBeacons = new List<ImportantNodeBeacon>();
            var currentNodeNames = new HashSet<string>(nodes.Select(node => node.Name), StringComparer.OrdinalIgnoreCase);
            var restBlockers = _gameManager.DisplayedSnapshot.RestBlockers;

            for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                var node = nodes[nodeIndex];
                if (!string.IsNullOrEmpty(focusedName) && node.Name != focusedName)
                    continue;

                var containedBlocker = RestBlockerPresentation.FindContained(node, restBlockers);
                var anchor = _gameManager.ResolveAnchor(node);
                if (anchor != null)
                {
                    // Check if the anchor point is inside the camera's viewport frustum
                    // We add a small padding (e.g. 0.05) so the card doesn't pop out abruptly when its anchor crosses the screen edge.
                    var viewPos = cam.WorldToViewportPoint(anchor.transform.position);
                    float padding = 0.05f;
                    bool inCameraSight = viewPos.z >= 0 
                                      && viewPos.x >= -padding && viewPos.x <= (1f + padding)
                                      && viewPos.y >= -padding && viewPos.y <= (1f + padding);

                    if (inCameraSight)
                    {
                        var screenPos = cam.WorldToScreenPoint(anchor.transform.position);
                        // screenPos is actual screen pixels; order 是内容里的声明序号，
                        // 只在锚点投影几乎重合时用来定先后（见 SolveProjectedStacks）。
                        initialProjected.Add((node, anchor.ResolvedNodeName, screenPos, screenPos.z, nodeIndex));
                    }
                    else if (containedBlocker != null)
                    {
                        importantBeacons.Add(new ImportantNodeBeacon(
                            node.Name,
                            containedBlocker.Reason,
                            ViewportDirection(viewPos)));
                    }
                    // Nodes with anchors panned out of view are not drawn (neither projected nor in fallback grid)
                }
                else if (AnnotationDrawer.IsAnnotation(node))
                {
                    // 标注没有锚点就升到画面上方的场景标注带：它不指某一处，指的是整个画面。
                    // 它永远不进网格——网格是"还没给锚点的卡"的临时住处，而一条标注宁可
                    // 去说这一整场在发生什么，也不该排在待办里假装自己可以点。
                    sceneNotes.Add(node);
                }
                else
                {
                    gridNodes.Add(node); // No anchor, draw in grid
                }
            }

            // ── 钉住条 ──
            // 地图上常驻的唯一一条故事信息：玩家钉的那条线的读数和那一句「现在」。
            //
            // 它紧贴顶栏，位置**恒定**：玩家每天要看的就是这两个数，眼睛该知道往哪儿落，
            // 不能今天这个地点有一条标注、它就往下挪一截。所以让位的是标注带——
            // 标注是"这一场是什么样子"，一天里换好几副面孔，本来就该跟着环境走。
            // 但"让位"是让开这 300 宽的一块，不是让开整整一行：标注带绕着它排。
            // 交锋里不画：那时候没有别的线可想。
            _pinStripRect = _gameManager.DisplayedSnapshot.IsInEncounter
                ? new Rect(0f, 0f, 0f, 0f)
                : DossierPanelDrawer.DrawPinStrip(_gameManager, _topHud.ContentTop);

            // 带子先量后画：卡片的可用区间（含网格视口）要按它实际占了多高往下让。
            // 标注是最底下的一层——它是印在图纸上的字，一切卡片都压在它上面。
            //
            // 带子和钉住条同高起排，绕着钉住条走（见 LayoutSceneBand）。所以卡片让位的
            // 底边要取两者的较大值：绕排时带子可能收在钉住条上方结束，那时该让的是钉住条。
            float bandTop = _topHud.ContentTop;
            _sceneBandRect = sceneNotes.Count == 0
                ? new Rect(0f, _pinStripRect.yMax, 0f, 0f)
                : new Rect(
                    UIScale.SafeArea.xMin, bandTop, UIScale.SafeArea.width,
                    Mathf.Max(
                        AnnotationDrawer.MeasureSceneBandHeight(sceneNotes, bandTop, _pinStripRect),
                        _pinStripRect.yMax - bandTop));
            if (sceneNotes.Count > 0)
                AnnotationDrawer.DrawSceneBand(sceneNotes, bandTop, _pinStripRect);

            // residue 只有三个互斥归属：
            // 1. 宿主节点仍在当前快照：由节点卡自己读取并绘制 residue；
            // 2. 宿主已消失但仍有世界锚点：保留在世界投射布局，出镜时随世界卡一起隐藏；
            // 3. 宿主已消失且没有世界锚点：才进入网格兜底。
            // 不能用“本帧有没有画出来”判断归属，否则现存投射卡会多出网格副本，
            // 有锚点的结果卡也会在相机转开时突然跳进网格。
            foreach (var pair in _cardResidues)
            {
                if (currentNodeNames.Contains(pair.Key))
                    continue;

                var anchor = _gameManager.SceneDirectory?.GetAnchor(pair.Value.SpatialAnchorName);
                if (anchor == null)
                {
                    gridResidues.Add(pair.Value);
                    continue;
                }

                var viewPos = cam.WorldToViewportPoint(anchor.transform.position);
                const float padding = 0.05f;
                bool inCameraSight = viewPos.z >= 0
                    && viewPos.x >= -padding && viewPos.x <= 1f + padding
                    && viewPos.y >= -padding && viewPos.y <= 1f + padding;
                if (!inCameraSight)
                    continue;

                var screenPos = cam.WorldToScreenPoint(anchor.transform.position);
                projectedResidues.Add((pair.Value, screenPos, screenPos.z));
            }

            // Clean up old cached centers that are no longer visible to avoid memory leaks
            var visibleKeys = new HashSet<string>(initialProjected.Select(x => x.node.Name));
            foreach (var item in projectedResidues)
                visibleKeys.Add(item.residue.HostNodeName);
            var keysToRemove = _cardCenters.Keys.Where(k => !visibleKeys.Contains(k)).ToList();
            foreach (var key in keysToRemove)
            {
                _cardCenters.Remove(key);
            }

            // Create layout records for projected cards
            var layouts = new List<ProjectedCardLayout>();
            foreach (var item in initialProjected)
            {
                // Convert from actual screen pixels (Y-up) to virtual GUI coords (Y-down).
                var virtualAnchor = UIScale.WorldPointToVirtual(item.screenPos);
                float anchorX = virtualAnchor.x;
                float anchorY = virtualAnchor.y;

                bool isAnnotation = AnnotationDrawer.IsAnnotation(item.node);
                bool isLocation = !isAnnotation && item.node.IsContainer;
                bool focused = !isAnnotation && isFocused(item.node.Name);

                // 地点卡按地名实际需要多宽收敛（见 PreferredLocationWidth）：它现在是一行
                // 「小符号 + 地名」的牌子，不需要偏方的体量。
                // 标注与动作卡同宽，高度完全由内容定——它没有卡框，也就没有「最小卡高」
                // 这回事：内容只有一行就只占一行。
                float cardWidth = isAnnotation
                    ? AnnotationDrawer.AnchoredWidth
                    : (focused
                        ? FocusedCardWidth
                        : (isLocation ? ContainerNodeDrawer.PreferredLocationWidth(item.node) : ActionCardDesignWidth));
                float contentHeight = isAnnotation
                    ? AnnotationDrawer.MeasureHeight(item.node, cardWidth)
                    : CardDrawer.MeasureCardHeight(
                        item.node,
                        CardDrawer.Classify(item.node, anchored: true),
                        cardWidth,
                        _gameManager.DisplayedSnapshot.Actors);
                // 聚焦卡放大是「凑近看」，只抬下限，不再把内容压回一个固定高度。
                // 地点牌不参与：它整张就是一行字，撑到 240 只会得到一个空盒子。
                // 聚焦表示“凑近看”，不是额外塞一段留白。需求展签移除后，200 已足够让
                // 短行动卡维持明确的可操作体量；实际内容更高时仍以 contentHeight 为准。
                const float focusedMinHeight = 200f;
                float cardHeight = focused && !isLocation ? Mathf.Max(focusedMinHeight, contentHeight) : contentHeight;
                Vector2 targetCenter = inGutters
                    ? CardGutterLayout.TargetCenter(item.anchorKey, new Vector2(anchorX, anchorY), cardWidth, ActionCardDesignWidth)
                    // 城市总览：卡片浮在锚点正上方。
                    : new Vector2(anchorX, anchorY - cardHeight / 2f - 40f);

                // Retrieve from cache or initialize
                if (!_cardCenters.TryGetValue(item.node.Name, out var currentCenter))
                {
                    currentCenter = targetCenter;
                    _cardCenters[item.node.Name] = currentCenter;
                }

                layouts.Add(new ProjectedCardLayout(
                    item.node,
                    new Vector2(anchorX, anchorY),
                    item.distance,
                    item.order,
                    targetCenter,
                    currentCenter,
                    cardWidth,
                    cardHeight,
                    isAnnotation));
            }

            foreach (var item in projectedResidues)
            {
                var virtualAnchor = UIScale.WorldPointToVirtual(item.screenPos);
                float cardWidth = ActionCardDesignWidth;
                float cardHeight = GridResidueCardHeight;
                Vector2 targetCenter = inGutters
                    ? CardGutterLayout.TargetCenter(item.residue.SpatialAnchorName, virtualAnchor, cardWidth, ActionCardDesignWidth)
                    : new Vector2(virtualAnchor.x, virtualAnchor.y - cardHeight / 2f - 40f);
                if (!_cardCenters.TryGetValue(item.residue.HostNodeName, out var currentCenter))
                {
                    currentCenter = targetCenter;
                    _cardCenters[item.residue.HostNodeName] = currentCenter;
                }

                // 宿主已经不在快照里，没有声明序号可用；排在同高度节点之后，再由名字定序。
                layouts.Add(new ProjectedCardLayout(
                    item.residue,
                    new Vector2(virtualAnchor.x, virtualAnchor.y),
                    item.distance,
                    int.MaxValue,
                    targetCenter,
                    currentCenter,
                    cardWidth,
                    cardHeight));
            }

            // 谁在上谁在下由 SolveProjectedStacks 一次算死，弹簧只负责把卡片平滑送过去。
            //
            // 但**排布不是每帧都重算的**。它的输入是锚点的屏幕投影，镜头一动这些输入就一直在变：
            // 绕着一栋楼转半圈，两个锚点的上下关系会来回交换，卡片跟着换位、分摞方式跟着变，
            // 于是一次推进加环绕看下来，卡片一路在打架——逻辑上每一帧都对，看着却糟透了。
            //
            // 所以：**镜头在动的时候排布冻结**，上次解算的结果作为「相对锚点的偏移」原样跟着
            // 锚点平移，卡片像钉在世界上一样跟着镜头走；等镜头停稳再重算一次，卡片顺着弹簧
            // 摊开。停机位（orbit focus 慢慢转）本来就够慢、锚点关系变化也小，看到的仍然是
            // 卡片跟着景物缓缓移动。
            var keepOut = BuildCardKeepOut();

            // 排布与位置都只在重绘时推进：OnGUI 一帧会被调用多次（Layout / 输入 / Repaint），
            // 每次都积分会让速度随事件数漂移，也会让命中判定用上一个还没画出来的位置。
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                NoteAnchorMotion(layouts);
                if (StackContentChanged(layouts) || (_stackDirty && _anchorsStillFor >= AnchorSettleTime))
                {
                    SolveProjectedStacks(layouts, keepOut);
                    RememberStacks(layouts);
                }
                else
                {
                    ApplyRememberedStacks(layouts);
                }

                // 低动画换镜期间不走弹簧：这一层此刻正从无到有地浮出来，位置必须一开始
                // 就是最终位置——淡入的同时还在滑，恰恰是这个模式要消掉的那种运动。
                float t = swappingView
                    ? 1f
                    : 1f - Mathf.Exp(-CardSettleSpeed * Time.deltaTime);
                foreach (var layout in layouts)
                {
                    // 先按锚点的位移刚性搬一次，再走弹簧。少了这一步，弹簧就得去追一个
                    // 正在快速移动的目标，卡片会拖在景物后面晃——那正是「抖」的来源。
                    // 搬过之后弹簧只剩下排布本身的变化要消化，重排依然是平滑摊开的。
                    layout.CurrentCenter += layout.AnchorDelta;
                    layout.CurrentCenter = Vector2.Lerp(layout.CurrentCenter, layout.SolvedCenter, t);
                    // 解算位已经在边界之内，这里夹的是路上的中间位置（以及镜头动着时
                    // 被搬到屏幕外的那些）。
                    layout.CurrentCenter = ClampCardCenter(layout, keepOut);
                    _cardCenters[layout.Key] = layout.CurrentCenter;
                }
            }

            // Draw projected cards (sorted by distance, far to near)。List.Sort 不稳定，
            // 同深度时再落回排序键，免得压叠关系和命中归属逐帧抖。
            layouts.Sort((a, b) =>
            {
                int byDistance = b.Distance.CompareTo(a.Distance);
                return byDistance != 0 ? byDistance : CompareStackRank(a, b);
            });

            // ── 世界投射层：跟着换镜一起显形 ──
            //
            // 这一层是钉在世界上的东西（卡片、引线、标注、边缘信标），它属于**镜头看到的
            // 那一镜**。低动画换镜时相机先一步落到新机位，屏幕上却还盖着旧画面的冻帧：
            // 照常画，卡片就会扎在一张旧画面上先跳一下，再滑向新排布。所以这一层的浓度
            // 直接跟着"新那一镜露出了多少"走——世界怎么显形，它就怎么显形。位置在上面
            // 已经直接落到解算位，全程不动，只是从无到有地浮出来。
            //
            // 淡入途中不接点击：那时它还是个影子，点一张看不清的卡不该算数。
            bool ghostLayer = viewReveal < 0.999f;
            float cardLayerRestore = IMGUIStyles.BeginLayer(viewReveal);

            foreach (var layout in layouts)
            {
                if (layout.IsAnnotation)
                    continue;   // 标注没有判定条也没有结果条，它不会长出附件
                if (layout.Residue != null)
                    _cardAttachmentOverlays.Add(CardAttachmentOverlay.ForResidue(layout.Rect, layout.Residue, spacious: true));
                else
                    AddAttachmentForNode(layout.Node!, layout.Rect, spacious: true);
            }

            // 命中归属：卡片是画家算法——投射卡按远→近画，网格卡再盖在最上面。而 IMGUI
            // 的点击是「先处理者 Event.Use() 吃掉」，顺序正好相反：不先解析一遍，重叠区域
            // 就会被画在最底下的那张卡抢走点击。附件不再是卡的碰撞体：它可以压在邻卡上，
            // 但只有没有任何卡本体占住时，结果残影才可接收一次"点此收起"。
            ProjectedCardLayout? hitOwner = null;
            bool attachmentConsumesPointer = AttachmentConsumesPointer(ui.Mouse);
            if (!ghostLayer && !attachmentConsumesPointer
                && !MouseOverGridCard(gridNodes, gridResidues, ui.Mouse))
            {
                for (int i = 0; i < layouts.Count; i++)
                {
                    // 标注永远不可点，也就永远不该抢走指针归属。
                    if (layouts[i].IsAnnotation)
                        continue;
                    if (layouts[i].Rect.Contains(ui.Mouse))
                        hitOwner = layouts[i];
                }
            }

            // 标注在最底下：它是印在图纸上的字，卡片压在它上面——卡才是可操作层。
            // 它自己画那一笔引线（线走到头长出字，见 AnnotationDrawer），所以不进下面
            // 那一层；卡片引线接的是矩形边中点，两种线的语言本来就不同。
            foreach (var layout in layouts)
            {
                if (layout.IsAnnotation)
                    AnnotationDrawer.DrawAnchored(layout.Rect, layout.Node!, layout.AnchorPos);
            }

            // 引线自成一层，在所有卡片之前一次画完。让每张卡各画各的线，后画的卡就会把
            // 先画的线拦腰切断——理由与实现都在 CardLeaderLineDrawer。
            _tethers.Clear();
            foreach (var layout in layouts)
            {
                if (layout.IsAnnotation)
                    continue;
                _tethers.Add(new CardLeaderLineDrawer.Tether(
                    layout.AnchorPos, layout.Rect, TetherWeight(layout, hitOwner)));
            }
            CardLeaderLineDrawer.Draw(_tethers);

            foreach (var layout in layouts)
            {
                if (layout.IsAnnotation)
                    continue;

                var cardUi = !ghostLayer && ReferenceEquals(layout, hitOwner) ? ui : ui.Occluded();
                if (layout.Residue != null)
                {
                    DrawProjectedResidueCard(layout.Residue, layout.Rect, cardUi);
                }
                else
                {
                    DrawNodeCard(layout.Node!, layout.Rect, cardUi);
                }
            }

            IMGUIStyles.EndLayer(cardLayerRestore);

            // 网格卡最后绘制，因此视觉上压在世界投射卡之上。它钉在屏幕上、不在那一镜里，
            // 所以不跟着换镜显形——上面那层已经收掉了。
            if (gridNodes.Count > 0 || gridResidues.Count > 0)
            {
                DrawCardsGrid(gridNodes, gridResidues, ui);
            }

            // 信标说的是"这一镜外头还有东西"，所以它和投射层同进同退。
            cardLayerRestore = IMGUIStyles.BeginLayer(viewReveal);
            string? beaconTarget = ImportantNodeBeaconDrawer.Draw(importantBeacons, ui, _topHud.ContentTop);
            IMGUIStyles.EndLayer(cardLayerRestore);
            if (beaconTarget != null && !ghostLayer)
                _gameManager.CameraManager.NavigateToNode(beaconTarget);
        }

        /// <summary>
        /// 这条引线该画多重。指针停在哪张卡上、或哪张卡是当前焦点，就只让那一条亮起来，
        /// 其余压暗——比把所有线都加粗有效得多，平时也不多一分噪音。
        /// 已结算的残影没有"指着谁"可言，一律压暗。
        ///
        /// 手指没有悬停：`hitOwner` 在触控上是"上一次点在哪"，拿它做常驻高亮会让某张卡
        /// 在松手之后一直亮着。所以联动高亮只在有悬停指针时生效——这是输入能力的差别，
        /// 不改任何元素画在哪、画多大。
        /// </summary>
        private CardLeaderLineDrawer.Emphasis TetherWeight(
            ProjectedCardLayout layout, ProjectedCardLayout? hitOwner)
        {
            var pointed = UIScale.HasHoverPointer ? hitOwner : null;
            bool highlighted = ReferenceEquals(layout, pointed)
                || (layout.Node != null && isFocused(layout.Node.Name));
            if (highlighted)
                return CardLeaderLineDrawer.Emphasis.Highlighted;
            if (layout.Residue != null || pointed != null)
                return CardLeaderLineDrawer.Emphasis.Muted;
            return CardLeaderLineDrawer.Emphasis.Normal;
        }

        private void AddAttachmentForNode(GameNode node, Rect cardRect, bool spacious)
        {
            bool isLocalRoll = _animator.IsPlaying
                && !_animator.UsesModal
                && string.Equals(_animator.ActionName, node.Name, StringComparison.OrdinalIgnoreCase);
            if (isLocalRoll)
            {
                _cardAttachmentOverlays.Add(CardAttachmentOverlay.ForLocalRoll(
                    cardRect, node.Name, _animator.CurrentReport!, _animator.Phase,
                    _animator.DisplayedDieValue, _animator.DisplayScale, spacious));
            }
            else if (_cardResidues.TryGetValue(node.Name, out var residue))
            {
                _cardAttachmentOverlays.Add(CardAttachmentOverlay.ForResidue(cardRect, residue, spacious));
            }
        }

        private void DrawCardAttachmentOverlays(IMGUIInteractionContext ui)
        {
            for (int i = 0; i < _cardAttachmentOverlays.Count; i++)
            {
                var attachment = _cardAttachmentOverlays[i];
                if (attachment.Residue != null
                    && !_cardResidues.ContainsKey(attachment.Residue.HostNodeName))
                    continue;
                if (ActionNodeDrawer.DrawAttachmentOverlay(
                        attachment.CardRect, attachment.LocalRoll, attachment.LocalRollPhase,
                        attachment.LocalRollDieValue, attachment.LocalRollScale, attachment.Residue,
                        ui, attachment.Spacious)
                    && attachment.Residue != null)
                {
                    _cardResidues.Remove(attachment.Residue.HostNodeName);
                }
            }
        }

        private void HandleCardAttachmentTaps(IMGUIInteractionContext ui)
        {
            for (int i = 0; i < _cardAttachmentOverlays.Count; i++)
            {
                var attachment = _cardAttachmentOverlays[i];
                if (attachment.Residue == null)
                    continue;

                var residueRect = ActionNodeDrawer.ResidueAttachmentRect(
                    attachment.CardRect, attachment.Residue, attachment.Spacious);
                ui.CanHover(residueRect);
                if (!ui.WasTapped(residueRect))
                    continue;

                Event.current.Use();
                _cardResidues.Remove(attachment.Residue.HostNodeName);
            }
        }

        private bool AttachmentConsumesPointer(Vector2 pointer)
        {
            foreach (var attachment in _cardAttachmentOverlays)
            {
                if (attachment.Residue != null
                    && ActionNodeDrawer.ResidueAttachmentRect(attachment.CardRect, attachment.Residue, attachment.Spacious).Contains(pointer))
                    return true;
            }
            return false;
        }

        // 卡片吸向解算位的速度（每秒 e 折次数）。只影响动画手感，不参与决定排布。
        private const float CardSettleSpeed = 9f;
        // 锚点一帧挪过这么多（虚拟像素）就算镜头在动。取得小一点：Cinemachine 的阻尼是
        // 渐进收敛的，宁可多等几帧再重排，也不要在还在缓停的时候就把卡片重新摊一遍。
        private const float AnchorMoveEpsilon = 0.2f;
        // 锚点连续静止这么久（秒）才认为镜头停稳。够长，盖得住阻尼收尾的抖动；
        // 又短到玩家感觉不出「停下之后才摊开」有延迟。
        private const float AnchorSettleTime = 0.1f;

        // 量出每张卡的锚点这一帧移动了多少（记在 AnchorDelta 上，供卡片刚性跟随），
        // 并维护「锚点静止了多久」。镜头在动就把排布标脏，等停稳再重排。
        private void NoteAnchorMotion(List<ProjectedCardLayout> layouts)
        {
            float moved = 0f;
            foreach (var layout in layouts)
            {
                if (_lastAnchors.TryGetValue(layout.Key, out var previous))
                {
                    layout.AnchorDelta = layout.AnchorPos - previous;
                    moved = Mathf.Max(moved, layout.AnchorDelta.sqrMagnitude);
                }
                _lastAnchors[layout.Key] = layout.AnchorPos;
            }
            if (_lastAnchors.Count > layouts.Count)
            {
                var live = new HashSet<string>(layouts.Select(l => l.Key));
                foreach (var key in _lastAnchors.Keys.Where(k => !live.Contains(k)).ToList())
                    _lastAnchors.Remove(key);
            }

            if (moved > AnchorMoveEpsilon * AnchorMoveEpsilon)
            {
                _anchorsStillFor = 0f;
                _stackDirty = true;
            }
            else
            {
                _anchorsStillFor += Time.deltaTime;
            }
        }

        // 「摆的东西本身变了」——多了一张卡、少了一张卡、某张卡因时钟徽章换行而长高或变宽。
        // 结算附件不在此列：它是覆盖在卡片下方的暂态残影，不应让整摞节点跟着跳位。
        // 这类变化是离散的、一次性的，冻结期间也必须重排：让新卡压在邻居
        // 身上一直到镜头停下，比重排一次更难看。镜头动个不停带来的连续变化不走这条路。
        private bool StackContentChanged(List<ProjectedCardLayout> layouts)
        {
            if (layouts.Count != _stackFootprints.Count)
                return true;
            foreach (var layout in layouts)
            {
                if (!_stackFootprints.TryGetValue(layout.Key, out var footprint)
                    || !Mathf.Approximately(footprint.x, layout.Width)
                    || !Mathf.Approximately(footprint.y, layout.Height))
                    return true;
            }
            return false;
        }

        private void RememberStacks(List<ProjectedCardLayout> layouts)
        {
            _stackOffsets.Clear();
            _stackFootprints.Clear();
            foreach (var layout in layouts)
            {
                _stackOffsets[layout.Key] = layout.SolvedCenter - layout.TargetCenter;
                _stackFootprints[layout.Key] = new Vector2(
                    layout.Width, layout.Height);
            }
            _stackDirty = false;
        }

        // 冻结期间：解算位 = 这一帧的锚点位置 + 上次解算时的偏移。整套排布刚性地跟着镜头走。
        // 上次解算之后才出现的卡还没有偏移，就先贴着自己的锚点（偏移 0），等停稳那一次重排
        // 给它安排位置——为了一张新卡把所有卡重新摊一遍，代价比它暂时压着邻居大得多。
        private void ApplyRememberedStacks(List<ProjectedCardLayout> layouts)
        {
            foreach (var layout in layouts)
            {
                layout.SolvedCenter = _stackOffsets.TryGetValue(layout.Key, out var offset)
                    ? layout.TargetCenter + offset
                    : layout.TargetCenter;
            }
        }
        // 同一列里两张卡之间留出的呼吸缝。
        private const float CardStackGap = 12f;
        // 一列放不下、并排分成几摞时，两摞之间的横向缝。
        private const float CardLaneGap = 14f;
        // 两个锚点的投影差在这个范围内视作一样高/一样偏，先后交给下一级键。
        private const float AnchorTieBand = 6f;

        // 世界投射卡的排布：先定出唯一的上下顺序，再在每一列里做一次一维消重叠，
        // 得到唯一的 SolvedCenter。
        //
        // 不能像原来那样「看谁此刻偏上就把谁往上推」——那让顺序取决于卡片这一瞬间飘到
        // 了哪里。同一栋楼的几个锚点投影得很近，卡片开局几乎重合，谁上谁下就由浮点噪声
        // 决定：同样的镜头推进，这次埃迪在上，下次它在下。
        //
        // 排序键是锚点自己的投影位置：屏幕上锚点在上的，卡也在上。这既是玩家眼里唯一
        // 说得通的顺序（楼上的房间卡如果排到楼下那张的下面，引线就是拧着的），也已经
        // 足够稳定——它只跟镜头有关，同一个机位每次都算出同一份排布。锚点投影几乎重合
        // 时才落到声明顺序和名字，避免在毫厘之差上分先后。
        //
        // 注：曾经试过用锚点的世界高度当主键，图上更「客观」，但读起来是反的——家在
        // 世界里比码头高，屏幕上却是码头的锚点更靠上，卡片于是和它指的东西上下颠倒。
        private static void SolveProjectedStacks(List<ProjectedCardLayout> layouts, in CardKeepOut keepOut)
        {
            if (layouts.Count == 0)
                return;

            var ranked = new List<ProjectedCardLayout>(layouts);
            ranked.Sort(CompareStackRank);

            // 只有横向真压在一起的卡才需要争上下；彼此错开的卡各排各的，
            // 否则屏幕两头毫不相干的两张卡会被硬拉进同一列。
            foreach (var column in GroupIntoColumns(ranked))
                SolveColumn(column, keepOut);
        }

        private static int CompareStackRank(ProjectedCardLayout a, ProjectedCardLayout b)
        {
            // 卡片先占位，标注填剩下的。卡是要点的，位置稳到能形成肌肉记忆比"离锚点最近"
            // 重要；标注被挤下去一点不影响读。同一条边距里两者争位置时，这一行就是裁决。
            if (a.IsAnnotation != b.IsAnnotation)
                return a.IsAnnotation ? 1 : -1;

            // GUI 坐标 Y 向下：锚点投影得越靠上，排得越靠前。分档而不是直接比大小——
            // 「差不多齐平」要判成平级，而分档是可传递的，直接比差值会得到 a<b、b<c 却
            // a==c 的比较器，List.Sort 会当场抛。
            int ya = Mathf.RoundToInt(a.AnchorPos.y / AnchorTieBand);
            int yb = Mathf.RoundToInt(b.AnchorPos.y / AnchorTieBand);
            if (ya != yb)
                return ya.CompareTo(yb);
            int xa = Mathf.RoundToInt(a.AnchorPos.x / AnchorTieBand);
            int xb = Mathf.RoundToInt(b.AnchorPos.x / AnchorTieBand);
            if (xa != xb)
                return xa.CompareTo(xb);
            if (a.DeclarationOrder != b.DeclarationOrder)
                return a.DeclarationOrder.CompareTo(b.DeclarationOrder);
            return string.CompareOrdinal(a.Key, b.Key);
        }

        // 按目标横向跨度扫一遍，把彼此搭上的卡归进同一列；列内保持稳定的上下顺序。
        private static List<List<ProjectedCardLayout>> GroupIntoColumns(List<ProjectedCardLayout> ranked)
        {
            var order = new List<int>();
            for (int i = 0; i < ranked.Count; i++)
                order.Add(i);
            order.Sort((x, y) =>
            {
                float lx = ranked[x].TargetCenter.x - ranked[x].FootprintWidth / 2f;
                float ly = ranked[y].TargetCenter.x - ranked[y].FootprintWidth / 2f;
                return !Mathf.Approximately(lx, ly) ? lx.CompareTo(ly) : x.CompareTo(y);
            });

            var columns = new List<List<ProjectedCardLayout>>();
            var current = new List<int>();
            float reach = float.NegativeInfinity;
            foreach (int i in order)
            {
                float left = ranked[i].TargetCenter.x - ranked[i].FootprintWidth / 2f;
                float right = ranked[i].TargetCenter.x + ranked[i].FootprintWidth / 2f;
                if (current.Count > 0 && left >= reach)
                {
                    columns.Add(TakeColumn(ranked, current));
                    current = new List<int>();
                    reach = float.NegativeInfinity;
                }
                current.Add(i);
                reach = Mathf.Max(reach, right);
            }
            if (current.Count > 0)
                columns.Add(TakeColumn(ranked, current));
            return columns;
        }

        private static List<ProjectedCardLayout> TakeColumn(List<ProjectedCardLayout> ranked, List<int> indices)
        {
            indices.Sort();   // 回到 rank 顺序
            var column = new List<ProjectedCardLayout>(indices.Count);
            foreach (int i in indices)
                column.Add(ranked[i]);
            return column;
        }

        // 一列卡的排布。
        //
        // 竖着放得下就竖着放（一维消重叠）；一列真的塞不下，就**并排分成几摞**，
        // 而不是硬挤成一叠。以前是后者：解算完再一张张按边界夹回来，超出去的部分被压平，
        // 于是一间挂了四五个动作的酒馆总是叠成一摞——这正是「怎么还是重叠」的根因，
        // 光把上下边界放宽是治不了的。
        //
        // 边界按整列的横向跨度算一次，全列共用：每张卡各按自己的跨度算的话，
        // 同一列的卡会得到不一样的上下限，解算出来的间距又会在夹取时被破坏。
        private static void SolveColumn(List<ProjectedCardLayout> column, in CardKeepOut keepOut)
        {
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            foreach (var card in column)
            {
                left = Mathf.Min(left, card.TargetCenter.x - card.Width / 2f);
                right = Mathf.Max(right, card.TargetCenter.x + card.Width / 2f);
            }

            var band = keepOut.BandFor(left, right);
            float required = StackHeight(column);

            if (required > band.Bottom - band.Top)
            {
                FanOutColumn(column, keepOut, band.Bottom - band.Top);
                return;
            }

            StackInPlace(column);
            FitStackIntoBand(column, band, required);
        }

        // 这一摞卡竖着摆需要多高（各自的上下伸展 + 卡间缝）。
        private static float StackHeight(List<ProjectedCardLayout> stack)
        {
            float total = CardStackGap * (stack.Count - 1);
            foreach (var card in stack)
                total += card.TopExtent + card.BottomExtent;
            return total;
        }

        // 解算完的一摞整体平移进可用区间。分块解算给出的间距可能大于最小值（几块之间本来
        // 就该有空隙），整摞比区间还长时就退回「贴紧排一遍」——宁可失去那点呼吸感，
        // 也不让卡片被逐张夹回来后又叠在一起。
        private static void FitStackIntoBand(List<ProjectedCardLayout> stack, (float Top, float Bottom) band, float required)
        {
            float spanTop = float.PositiveInfinity, spanBottom = float.NegativeInfinity;
            foreach (var card in stack)
            {
                spanTop = Mathf.Min(spanTop, card.SolvedCenter.y - card.TopExtent);
                spanBottom = Mathf.Max(spanBottom, card.SolvedCenter.y + card.BottomExtent);
            }

            if (spanBottom - spanTop > band.Bottom - band.Top)
            {
                StackTight(stack, Mathf.Clamp(MeanTargetY(stack) - required / 2f, band.Top, band.Bottom - required));
                return;
            }

            float shift = Mathf.Max(0f, band.Top - spanTop) - Mathf.Max(0f, spanBottom - band.Bottom);
            if (Mathf.Approximately(shift, 0f))
                return;
            foreach (var card in stack)
                card.SolvedCenter = new Vector2(card.SolvedCenter.x, card.SolvedCenter.y + shift);
        }

        // 从 top 起贴着最小间距排一遍。横向位置保持不变（由调用方先定好）。
        private static void StackTight(List<ProjectedCardLayout> stack, float top)
        {
            float y = top;
            foreach (var card in stack)
            {
                card.SolvedCenter = new Vector2(card.SolvedCenter.x, y + card.TopExtent);
                y += card.TopExtent + card.BottomExtent + CardStackGap;
            }
        }

        private static float MeanTargetY(List<ProjectedCardLayout> stack)
        {
            float sum = 0f;
            foreach (var card in stack)
                sum += card.TargetCenter.y;
            return sum / stack.Count;
        }

        // 一列放不下：拆成并排的几摞，整体居中在原来那一列的位置上。
        //
        // 顺序仍然是「先上后下、再左到右」——第一摞装最靠上的几张，装满换下一摞。引线会
        // 说明每张卡指的是哪儿，所以横向散开不会让人跟丢；叠在一起才会。
        // 散开后的总宽可能压到旁边一列的卡上（那一列本来横向错开、没被并进来）；
        // 同一栋楼的卡挤在一处时这种情况很少见，让它按画家算法压过去即可。
        private static void FanOutColumn(List<ProjectedCardLayout> column, in CardKeepOut keepOut, float available)
        {
            var lanes = SplitIntoLanes(column, Mathf.Clamp(
                Mathf.CeilToInt(StackHeight(column) / Mathf.Max(1f, available)), 2, column.Count));

            float totalWidth = -CardLaneGap;
            foreach (var lane in lanes)
                totalWidth += LaneWidth(lane) + CardLaneGap;

            float center = 0f;
            foreach (var card in column)
                center += card.TargetCenter.x;
            center /= column.Count;

            Rect safe = UIScale.SafeArea;
            float minX = safe.x + 12f;
            float x = Mathf.Clamp(center - totalWidth / 2f, minX, Mathf.Max(minX, safe.xMax - 12f - totalWidth));

            foreach (var lane in lanes)
            {
                float width = LaneWidth(lane);
                float laneCenter = x + width / 2f;
                foreach (var card in lane)
                    card.SolvedCenter = new Vector2(laneCenter, card.SolvedCenter.y);

                // 每摞按自己所在的横向位置重新问一次边界：靠边那摞可能正压在按钮上下。
                var band = keepOut.BandFor(x, x + width);
                float required = StackHeight(lane);
                StackTight(lane, Mathf.Clamp(
                    MeanTargetY(lane) - required / 2f,
                    band.Top,
                    Mathf.Max(band.Top, band.Bottom - required)));

                x += width + CardLaneGap;
            }
        }

        // 按累计高度切分，保持原有的上下顺序。切到只剩一摞时全部塞进去——
        // 摞数是个上限，不是必须凑够的数。
        private static List<List<ProjectedCardLayout>> SplitIntoLanes(List<ProjectedCardLayout> column, int laneCount)
        {
            float perLane = StackHeight(column) / laneCount;
            var lanes = new List<List<ProjectedCardLayout>>();
            var current = new List<ProjectedCardLayout>();
            float filled = 0f;

            foreach (var card in column)
            {
                float need = card.TopExtent + card.BottomExtent + CardStackGap;
                // 过半才换摞：一张卡跨在分界上时，按它落在哪一边多一点来归属。
                if (current.Count > 0 && lanes.Count < laneCount - 1 && filled + need * 0.5f > perLane)
                {
                    lanes.Add(current);
                    current = new List<ProjectedCardLayout>();
                    filled = 0f;
                }
                current.Add(card);
                filled += need;
            }
            lanes.Add(current);
            return lanes;
        }

        private static float LaneWidth(List<ProjectedCardLayout> lane)
        {
            float width = 0f;
            foreach (var card in lane)
                width = Mathf.Max(width, card.Width);
            return width;
        }

        // 一维消重叠：顺序已经定死，只需让挤在一起的卡各自让开。互相挤住的连成一「块」，
        // 整块停在成员们期望位置的平均处——所以卡群仍然贴着锚点，不会整体往下漂。
        private static void StackInPlace(List<ProjectedCardLayout> column)
        {
            int n = column.Count;

            // cum[i]：第 i 张卡的中心相对本块块首至少要偏开多远（相邻最小间距的前缀和）。
            var cum = new float[n];
            for (int i = 1; i < n; i++)
                cum[i] = cum[i - 1] + column[i - 1].BottomExtent + column[i].TopExtent + CardStackGap;

            var blockHead = new List<int>();
            var blockCount = new List<int>();
            var blockSum = new List<float>();   // Σ(期望中心 − 相对块首的固定偏移)
            for (int i = 0; i < n; i++)
            {
                blockHead.Add(i);
                blockCount.Add(1);
                blockSum.Add(column[i].TargetCenter.y);

                while (blockHead.Count >= 2)
                {
                    int last = blockHead.Count - 1;
                    int prev = last - 1;
                    float span = cum[blockHead[last]] - cum[blockHead[prev]];
                    if (blockSum[last] / blockCount[last] >= blockSum[prev] / blockCount[prev] + span)
                        break;

                    blockSum[prev] += blockSum[last] - blockCount[last] * span;
                    blockCount[prev] += blockCount[last];
                    blockHead.RemoveAt(last);
                    blockCount.RemoveAt(last);
                    blockSum.RemoveAt(last);
                }
            }

            for (int b = 0; b < blockHead.Count; b++)
            {
                int head = blockHead[b];
                int end = b + 1 < blockHead.Count ? blockHead[b + 1] : n;
                float headY = blockSum[b] / blockCount[b];
                for (int i = head; i < end; i++)
                {
                    column[i].SolvedCenter = new Vector2(
                        column[i].TargetCenter.x,
                        headY + cum[i] - cum[head]);
                }
            }
        }

        private static Vector2 ClampCardCenter(ProjectedCardLayout layout, in CardKeepOut keepOut)
        {
            var footprint = new Rect(
                layout.CurrentCenter.x - layout.Width / 2f,
                layout.CurrentCenter.y - layout.Height / 2f,
                layout.Width,
                layout.Height);
            footprint = ClampRect(footprint, layout.Width, layout.Height, keepOut);
            return new Vector2(footprint.center.x, footprint.y + layout.Height / 2f);
        }

        private static Vector2 ViewportDirection(Vector3 viewportPosition)
        {
            var direction = new Vector2(viewportPosition.x - 0.5f, 0.5f - viewportPosition.y);
            if (viewportPosition.z < 0f)
                direction = -direction;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        }

        // 网格卡的版面：位置只由序号、屏幕尺寸和滚动量决定，所以命中解析和实际绘制
        // 共用这几个函数，不各算一遍。返回的是视口内的局部坐标。
        // ── 卡片体量 ─────────────────────────────────────────────────────
        //
        // 手机上所有卡都要按更小的基准画，这不是"缩水"，是横屏手机的形状使然：
        // 虚拟画布只有 826×465 上下，一张按 340 宽画的卡就占掉近半个屏幕宽，一屏放不下
        // 两列还看得清的东西。字号不受影响（字号由画布定，不由卡宽定），少掉的是留白。
        //
        // 250 是按内容反推的：副标题一行 14 字、标题 10 字、三个骰位横排 196px 都放得下，
        // 而 826 宽的画布正好排得下三列。
        // 地点牌的宽度不在这儿：它由地名实际多长决定，见 ContainerNodeDrawer.PreferredLocationWidth。
        private const float ActionCardDesignWidth = 250f;
        private const float FocusedCardWidth = 330f;

        // 网格卡的基准宽度。实际宽度由 GridCardWidth 按当前列数摊开：列数定死之后，
        // 剩下的宽度摊回给每张卡，右边不留一条没用的空地。
        private const float GridCardDesignWidth = ActionCardDesignWidth;
        // 残留（节点已消失、只剩结算结果的宿主卡）没有内容可量，保留一个固定的小盒子。
        private const float GridResidueCardHeight = 150f;
        private const float GridSpacing = 20f;
        // 金色脉冲描边会向卡片外扩最多 7.5px；网格卡不能贴着 GUI.Group 顶部，
        // 否则第一行卡片的上边会被父 Group 裁掉。
        private const float GridContentTopPadding = 10f;

        // 左右留白随设备走：桌面 40px 的边距搬到手机的虚拟画布上会吃掉整整一列卡。
        private const float GridSideMargin = 16f;
        private static float GridStartX => UIScale.SafeArea.x + GridSideMargin;
        private static float GridEndX => UIScale.SafeArea.xMax - GridSideMargin;

        private static int GridCardsPerRow()
        {
            return Mathf.Max(1, (int)((GridEndX - GridStartX + GridSpacing) / (GridCardDesignWidth + GridSpacing)));
        }

        // 列数定下来之后，把剩下的宽度摊回给每张卡（最多放宽到基准的 1.4 倍，再宽就不像卡了）。
        private static float GridCardWidth
        {
            get
            {
                int columns = GridCardsPerRow();
                float available = GridEndX - GridStartX - GridSpacing * (columns - 1);
                return Mathf.Clamp(available / columns, GridCardDesignWidth, GridCardDesignWidth * 1.4f);
            }
        }

        // 网格视口夹在顶栏下沿和底部手牌簇之间；两头都由各自的所有者报高度，这里不写死。
        // 场景标注带和钉住条也算这一头：它们占了画面上方多少，网格就从多少往下开始。
        private Rect GridViewport()
        {
            // 网格是固定在屏幕上的 UI，不会像空间投射卡那样随视角移动；它的最低上界
            // 永远是顶栏内容区。交锋里没有钉住条，且根节点没有场景标注时，下面两个
            // Rect 都是零矩形；只取它们的 yMax 会让网格从 y=0 开始，把顶栏整块盖住。
            float top = Mathf.Max(_topHud.ContentTop,
                Mathf.Max(_sceneBandRect.yMax, _pinStripRect.yMax));
            float bottom = UIScale.SafeArea.yMax - HandPanelDrawer.ReservedHeight;
            return new Rect(0f, top, UIScale.VW, Mathf.Max(0f, bottom - top));
        }

        private List<GridCardLayout> BuildGridCardLayouts(
            IReadOnlyList<GameNode> nodes,
            IReadOnlyList<CardPresentationResidue> residues,
            float scrollOffset,
            out float contentHeight)
        {
            int columnCount = GridCardsPerRow();
            int totalCards = nodes.Count + residues.Count;
            var columnHeights = new float[columnCount];
            for (int i = 0; i < columnHeights.Length; i++)
                columnHeights[i] = GridContentTopPadding;
            var layouts = new List<GridCardLayout>(totalCards);

            for (int i = 0; i < totalCards; i++)
            {
                int column = i % columnCount;
                float markerSpace = i < nodes.Count
                    && RestBlockerPresentation.ContainsTarget(nodes[i], _gameManager.DisplayedSnapshot.RestBlockers)
                    ? CardDrawer.ExternalRestBlockerMarkerSpace
                    : 0f;
                // 每张网格卡按自身内容定高（瀑布流本来就允许列内高度不齐）。以前这里是固定
                // 190：副标题长一点、时钟徽章多一个，内容就只能在同一个盒子里互相挤。
                float cardHeight = i < nodes.Count
                    ? CardDrawer.MeasureCardHeight(
                        nodes[i],
                        CardDrawer.Classify(nodes[i], anchored: false),
                        GridCardWidth,
                        _gameManager.DisplayedSnapshot.Actors)
                    : GridResidueCardHeight;
                var cardRect = new Rect(
                    GridStartX + column * (GridCardWidth + GridSpacing),
                    columnHeights[column] + markerSpace - scrollOffset,
                    GridCardWidth,
                    cardHeight);
                layouts.Add(new GridCardLayout(cardRect));
                columnHeights[column] += markerSpace + cardHeight + GridSpacing;
            }

            contentHeight = totalCards == 0
                ? 0f
                : Mathf.Max(0f, columnHeights.Max() - GridSpacing);
            return layouts;
        }

        // 鼠标是否落在某张可见网格卡上（视口裁剪之外的部分不算）。
        private bool MouseOverGridCard(
            IReadOnlyList<GameNode> nodes,
            IReadOnlyList<CardPresentationResidue> residues,
            Vector2 mouse)
        {
            var viewport = GridViewport();
            if (nodes.Count + residues.Count <= 0 || !viewport.Contains(mouse))
                return false;

            var local = mouse - new Vector2(viewport.x, viewport.y);
            var layouts = BuildGridCardLayouts(nodes, residues, _gridScrollOffset, out _);
            foreach (var layout in layouts)
            {
                if (layout.FootprintRect.Contains(local))
                    return true;
            }
            return false;
        }

        // 手指推着卡片网格滚动。触控设备上没有滚轮，这是唯一能翻到下面那几张卡的办法。
        //
        // 只在触控上启用：桌面用滚轮，鼠标按住拖动是「拖镜头」，不该变成滚列表。
        // 卡片的激活走 WasTapped（抬手才算），所以从卡片上开始滑不会顺手把它点掉。
        private void UpdateGridDragScroll(
            IMGUIInteractionContext ui, Rect viewport, bool pointerInViewport, float maxScroll)
        {
            if (UIScale.HasHoverPointer) return;

            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && pointerInViewport && maxScroll > 0f:
                    _gridDragActive = true;
                    _gridDragLastY = ui.Mouse.y;
                    _gridDragTravel = 0f;
                    break;

                case EventType.MouseDrag when _gridDragActive:
                {
                    float dy = ui.Mouse.y - _gridDragLastY;
                    _gridDragLastY = ui.Mouse.y;
                    _gridDragTravel += Mathf.Abs(dy);
                    _gridScrollOffset -= dy;
                    // 推过阈值之后这次按压归滚动所有，别让它同时传给镜头。
                    if (_gridDragTravel >= IMGUIInteractionContext.TapSlop)
                        e.Use();
                    break;
                }

                case EventType.MouseUp:
                    _gridDragActive = false;
                    _gridDragTravel = 0f;
                    break;
            }
        }

        private void DrawCardsGrid(List<GameNode> nodes, List<CardPresentationResidue> gridResidues, IMGUIInteractionContext ui)
        {
            var visibleNodes = nodes;
            int totalCards = visibleNodes.Count + gridResidues.Count;
            BuildGridCardLayouts(visibleNodes, gridResidues, 0f, out float contentHeight);
            var viewport = GridViewport();
            float maxScroll = Mathf.Max(0f, contentHeight - viewport.height);

            // ContainsMouse (not CanHover): this viewport spans the whole play area
            // and is only used to catch scroll-wheel events — it must not mark the
            // area as UI, or camera dragging through empty grid space would break.
            bool mouseInViewport = ui.ContainsMouse(viewport);
            if (mouseInViewport && Event.current.type == EventType.ScrollWheel && maxScroll > 0f)
            {
                _gridScrollOffset += Event.current.delta.y * 18f;
                Event.current.Use();
            }
            UpdateGridDragScroll(ui, viewport, mouseInViewport, maxScroll);
            _gridScrollOffset = Mathf.Clamp(_gridScrollOffset, 0f, maxScroll);
            var layouts = BuildGridCardLayouts(visibleNodes, gridResidues, _gridScrollOffset, out _);

            // 网格卡同样先登记附件、后画本体；后面的统一 overlay 才能盖过所有卡片。
            for (int i = 0; i < totalCards; i++)
            {
                Rect screenCardRect = layouts[i].CardRect;
                screenCardRect.position += viewport.position;
                if (i >= visibleNodes.Count)
                    _cardAttachmentOverlays.Add(CardAttachmentOverlay.ForResidue(
                        screenCardRect, gridResidues[i - visibleNodes.Count], spacious: false));
                else
                {
                    NoteActionCardRect(visibleNodes[i], screenCardRect);
                    AddAttachmentForNode(visibleNodes[i], screenCardRect, spacious: false);
                }
            }
            bool attachmentConsumesPointer = AttachmentConsumesPointer(ui.Mouse);

            GUI.BeginGroup(viewport);
            var localUi = attachmentConsumesPointer
                ? ui.Occluded().Translated(new Vector2(viewport.x, viewport.y))
                : ui.Translated(new Vector2(viewport.x, viewport.y));

            // 卡片的边缘外挂（便签 / 能力片）攒到最后一起画：网格列间距只有 GridSpacing，
            // 外挂一定伸进邻居的地盘，一遍画下来右缘的能力片会被下一张卡的卡身盖掉。
            // 详见 ActionNodeDrawer.BeginDeferredEdgeAttachments。
            ActionNodeDrawer.BeginDeferredEdgeAttachments();

            for (int i = 0; i < totalCards; i++)
            {
                var layout = layouts[i];
                var cardRect = layout.CardRect;
                if (layout.FootprintRect.y > viewport.height || layout.FootprintRect.yMax < 0f)
                {
                    continue;
                }

                if (i >= visibleNodes.Count)
                {
                    DrawGridResidueCard(cardRect, gridResidues[i - visibleNodes.Count], localUi);
                    continue;
                }

                var node = visibleNodes[i];
                _dialogueAnchors.RegisterNode(node.Name, new Rect(
                    cardRect.x + viewport.x,
                    cardRect.y + viewport.y,
                    cardRect.width,
                    cardRect.height));

                bool isHovered = localUi.CanHover(cardRect);
                bool isFlipped = _gameManager.IsNodeFlipped(node.Name);
                bool focused = isFocused(node.Name);

                List<SlottedResource?>? slotted = null;
                if (node.Requires != null && node.Requires.Count > 0)
                {
                    slotted = _gameManager.GetSlotsForNode(node.Name);
                }

                string backText = (node.Resolve?.Type == ResolveType.Observe) ? (node.Resolve?.ObserveText ?? "") : "";
                var execution = GetExecutionState(node.Name);
                bool isLocalRoll = _animator.IsPlaying
                    && !_animator.UsesModal
                    && string.Equals(_animator.ActionName, node.Name, StringComparison.OrdinalIgnoreCase);
                _cardResidues.TryGetValue(node.Name, out var residue);
                bool isRestBlockerTarget = RestBlockerPresentation.IsTarget(node, _gameManager.DisplayedSnapshot.RestBlockers);
                bool containsRestBlockerTarget = RestBlockerPresentation.ContainsTarget(node, _gameManager.DisplayedSnapshot.RestBlockers);

                var interaction = CardDrawer.DrawCard(cardRect, node, CardDrawer.Classify(node, anchored: false), isHovered, isFlipped, focused,
                    slotted, node.Clocks, backText, localUi, _gameManager,
                    execution.IsExecuting, execution.Progress, execution.Text,
                    isLocalRoll ? _animator.CurrentReport : null,
                    _animator.Phase,
                    _animator.DisplayedDieValue,
                    _animator.DisplayScale,
                    residue,
                    isRestBlockerTarget,
                    containsRestBlockerTarget);

                if (interaction.CardClicked)
                {
                    if (node.IsContainer)
                    {
                        _gridScrollStack.Add(_gridScrollOffset);
                        _gridScrollOffset = 0f;
                    }
                    _gameManager.OnNodeCardClicked(node);
                }
                if (interaction.ClickedSlotIndex != -1 && slotted != null && node.Requires != null)
                {
                    _gameManager.OnSlotClicked(node, interaction.ClickedSlotIndex);
                }
                if (interaction.DroppedSlotIndex != -1 && slotted != null && node.Requires != null)
                {
                    if (_gameManager.TryPlaceSelectedResource(node, interaction.DroppedSlotIndex))
                        _gameManager.MarkResourceDropHandled();
                }
                if (interaction.ExecuteClicked)
                {
                    _gameManager.ExecuteNodeAction(node);
                }
            }

            // 必须在 EndGroup 之前：外挂的坐标是这个分组里的局部坐标。
            // 这一句不能被上面任何一条 continue 或异常绕过，否则标志位会卡在"还在攒"，
            // 世界投射的卡也跟着不画外挂了。
            ActionNodeDrawer.FlushDeferredEdgeAttachments();

            GUI.EndGroup();
            DrawGridScrollbar(viewport, contentHeight, _gridScrollOffset, maxScroll);
        }

        private readonly struct GridCardLayout
        {
            public Rect CardRect { get; }
            public Rect FootprintRect => CardRect;

            public GridCardLayout(Rect cardRect)
            {
                CardRect = cardRect;
            }
        }

        private readonly struct CardAttachmentOverlay
        {
            public Rect CardRect { get; }
            public string HostNodeName { get; }
            public ActionReport? LocalRoll { get; }
            public int LocalRollPhase { get; }
            public int LocalRollDieValue { get; }
            public float LocalRollScale { get; }
            public CardPresentationResidue? Residue { get; }
            public bool Spacious { get; }

            private CardAttachmentOverlay(
                Rect cardRect, string hostNodeName, ActionReport? localRoll, int localRollPhase,
                int localRollDieValue, float localRollScale, CardPresentationResidue? residue, bool spacious)
            {
                CardRect = cardRect;
                HostNodeName = hostNodeName;
                LocalRoll = localRoll;
                LocalRollPhase = localRollPhase;
                LocalRollDieValue = localRollDieValue;
                LocalRollScale = localRollScale;
                Residue = residue;
                Spacious = spacious;
            }

            public static CardAttachmentOverlay ForLocalRoll(
                Rect cardRect, string hostNodeName, ActionReport report, int phase, int dieValue, float scale, bool spacious)
                => new CardAttachmentOverlay(cardRect, hostNodeName, report, phase, dieValue, scale, null, spacious);

            public static CardAttachmentOverlay ForResidue(Rect cardRect, CardPresentationResidue residue, bool spacious)
                => new CardAttachmentOverlay(cardRect, residue.HostNodeName, null, 0, 0, 1f, residue, spacious);
        }

        // 引线不在这里画：它是所有卡片之前的一整层，见 CardLeaderLineDrawer。
        private void NoteActionCardRect(GameNode node, Rect screenRect)
        {
            if (_firstActionCardRect.HasValue) return;
            if (node.Resolve == null || node.IsContainer) return;
            _firstActionCardRect = screenRect;
        }

        private void DrawNodeCard(GameNode node, Rect cardRect, IMGUIInteractionContext ui)
        {
            NoteActionCardRect(node, cardRect);
            bool isHovered = ui.CanHover(cardRect);
            bool isFlipped = _gameManager.IsNodeFlipped(node.Name);
            bool focused = isFocused(node.Name);

            List<SlottedResource?>? slotted = null;
            if (node.Requires != null && node.Requires.Count > 0)
            {
                slotted = _gameManager.GetSlotsForNode(node.Name);
            }

            string backText = (node.Resolve?.Type == ResolveType.Observe) ? (node.Resolve?.ObserveText ?? "") : "";
            var execution = GetExecutionState(node.Name);
            bool isLocalRoll = _animator.IsPlaying
                && !_animator.UsesModal
                && string.Equals(_animator.ActionName, node.Name, StringComparison.OrdinalIgnoreCase);
            _cardResidues.TryGetValue(node.Name, out var residue);
            bool isRestBlockerTarget = RestBlockerPresentation.IsTarget(node, _gameManager.DisplayedSnapshot.RestBlockers);
            bool containsRestBlockerTarget = RestBlockerPresentation.ContainsTarget(node, _gameManager.DisplayedSnapshot.RestBlockers);

            var interaction = CardDrawer.DrawCard(cardRect, node, CardDrawer.Classify(node, anchored: true), isHovered, isFlipped, focused,
                slotted, node.Clocks, backText, ui, _gameManager,
                execution.IsExecuting, execution.Progress, execution.Text,
                isLocalRoll ? _animator.CurrentReport : null,
                _animator.Phase,
                _animator.DisplayedDieValue,
                _animator.DisplayScale,
                residue,
                isRestBlockerTarget,
                containsRestBlockerTarget,
                spaciousAttachments: true);

            if (interaction.CardClicked)
            {
                _gameManager.OnNodeCardClicked(node);
            }

            if (interaction.ClickedSlotIndex != -1 && slotted != null && node.Requires != null)
            {
                _gameManager.OnSlotClicked(node, interaction.ClickedSlotIndex);
            }

            if (interaction.DroppedSlotIndex != -1 && slotted != null && node.Requires != null)
            {
                if (_gameManager.TryPlaceSelectedResource(node, interaction.DroppedSlotIndex))
                    _gameManager.MarkResourceDropHandled();
            }

            if (interaction.ExecuteClicked)
            {
                _gameManager.ExecuteNodeAction(node);
            }
        }

        private void DrawProjectedResidueCard(CardPresentationResidue residue, Rect cardRect, IMGUIInteractionContext ui)
        {
            var source = residue.SourceNode ?? new GameNode
            {
                Name = residue.HostNodeName,
                Resolve = new GameResolve { Type = ResolveType.Instant },
            };
            var host = CreateDisabledResidueHost(source);
            var interaction = CardDrawer.DrawCard(cardRect, host, CardDrawer.CardKind.Action,
                isHovered: false, isFlipped: false, isFocused: false,
                slotted: null, clocks: host.Clocks, backText: string.Empty,
                ui: ui, gameManager: _gameManager, residue: residue,
                spaciousAttachments: true);
        }

        private void DrawGridResidueCard(Rect cardRect, CardPresentationResidue residue, IMGUIInteractionContext ui)
        {
            var source = residue.SourceNode ?? new GameNode
            {
                Name = residue.HostNodeName,
                Resolve = new GameResolve { Type = ResolveType.Instant },
            };
            var host = CreateDisabledResidueHost(source);
            var interaction = CardDrawer.DrawCard(cardRect, host, CardDrawer.CardKind.Action,
                isHovered: false, isFlipped: false, isFocused: false,
                slotted: null, clocks: host.Clocks, backText: string.Empty,
                ui: ui, gameManager: _gameManager, residue: residue);
        }

        private static GameNode CreateDisabledResidueHost(GameNode source)
        {
            var host = new GameNode
            {
                Name = source.Name,
                Subtitle = source.Subtitle,
                Disabled = true,
                Tags = new List<string>(source.Tags),
                Requires = new List<ActionCost>(source.Requires),
                Resolve = source.Resolve,
            };
            host.Clocks.AddRange(source.Clocks);
            return host;
        }

        private void SyncNavigationScrollState()
        {
            int depth = _gameManager.NavigationStack.Count;
            if (depth < _lastNavigationDepth)
            {
                while (_gridScrollStack.Count > depth)
                {
                    _gridScrollStack.RemoveAt(_gridScrollStack.Count - 1);
                }
                _gridScrollOffset = PopGridScrollOffset();
            }
            else if (depth == 0 && _lastNavigationDepth != 0)
            {
                _gridScrollStack.Clear();
                _gridScrollOffset = 0f;
            }
            else if (depth > _lastNavigationDepth + 1)
            {
                _gridScrollStack.Clear();
                _gridScrollOffset = 0f;
            }

            _lastNavigationDepth = depth;
        }

        private float PopGridScrollOffset()
        {
            if (_gridScrollStack.Count == 0)
            {
                return 0f;
            }

            int last = _gridScrollStack.Count - 1;
            float offset = _gridScrollStack[last];
            _gridScrollStack.RemoveAt(last);
            return offset;
        }

        private static void DrawGridScrollbar(Rect viewport, float contentHeight, float scrollOffset, float maxScroll)
        {
            if (contentHeight <= viewport.height || viewport.height <= 0f)
            {
                return;
            }

            float trackW = 6f;
            var track = new Rect(viewport.xMax - trackW - 10f, viewport.y + 4f, trackW, viewport.height - 8f);
            float thumbH = Mathf.Max(34f, track.height * (viewport.height / contentHeight));
            float travel = Mathf.Max(0f, track.height - thumbH);
            float thumbY = track.y + (maxScroll <= 0f ? 0f : travel * (scrollOffset / maxScroll));
            var thumb = new Rect(track.x, thumbY, track.width, thumbH);

            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, 0.75f);
            GUI.DrawTexture(track, Texture2D.whiteTexture);
            GUI.color = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.55f);
            GUI.DrawTexture(thumb, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private bool isFocused(string nodeName)
        {
            return _gameManager.FocusedNodeName == nodeName;
        }

        /// <summary>
        /// 某个动作此刻的「执行中」进度。原来这只服务画成卡的节点（进度条画在执行钮里），
        /// 于是没有执行钮的两处——右下角的随身挂件（抽烟）和交锋里的休息键——
        /// 明明也在播同一段演出，却什么都不显示。改成按名字问，谁都能来取。
        /// </summary>
        public (bool IsExecuting, float Progress, string Text) GetExecutionState(string nodeName)
        {
            if (!_presentationPlayer.IsPlaying || _animator.IsPlaying)
            {
                return (false, 0f, string.Empty);
            }
            if (!string.Equals(_presentationPlayer.ActionName, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                return (false, 0f, string.Empty);
            }
            if (string.IsNullOrEmpty(_presentationPlayer.ProgressText))
            {
                return (false, 0f, string.Empty);
            }
            return (true, _presentationPlayer.Progress01, _presentationPlayer.ProgressText);
        }

        private void DrawGrowthToggleButton(IMGUIInteractionContext ui, Rect btnRect)
        {
            // 和声誉 / 卷宗 / 设置共用同一份顶栏开关实现。原来这里是另写的一份，
            // 用了 ExecuteLabel、也没铺 HUD 底，排在其余三个旁边一眼就看得出不是一伙的。
            if (IMGUIButton.DrawHudToggle(btnRect, "成 长", _isGrowthPanelOpen, ui))
            {
                _isGrowthPanelOpen = !_isGrowthPanelOpen;
                if (_isGrowthPanelOpen)
                {
                    // 打开面板＝明确切换上下文，上一次的结算已经读完了。
                    ClearCardResidues();
                    // 成长面板打开时不留一个悬在背后的下拉——避免两个 Panel 层弹窗抢点击。
                    SettingsPanelDrawer.Close();
                    DebugPanelDrawer.Close();
                }
            }
        }

        private void DrawPresentationOverlay()
        {
            // Execution progress is drawn inside the active card's execute button.
        }

        private void AddLightResidueIfNeeded(ActionReport report, string actionName)
        {
            // 结果残影是城市世界卡的短暂附着物。交锋里的行动/时钟只在交锋场景
            // 有意义，不能在交锋结束后随旧 actionName 一起泄漏到世界；没有锚定动作名
            // 的入场/离场演出也没有宿主卡，不能创建残影。
            if (string.IsNullOrWhiteSpace(actionName)
                || !string.Equals(_gameManager.SceneManager.CurrentSceneName, "world",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var presentation = report.OutcomePresentation;
            if (presentation == null || !presentation.HasText || presentation.Mode != OutcomePresentationMode.Light)
            {
                return;
            }

            var sourceNode = _gameManager.VisibleNodes.FirstOrDefault(node =>
                string.Equals(node.Name, actionName, StringComparison.OrdinalIgnoreCase));
            _cardResidues[actionName] = new CardPresentationResidue
            {
                HostNodeName = actionName,
                SpatialAnchorName = sourceNode?.EffectiveAnchorName ?? actionName,
                Title = presentation.Title,
                RollOutcome = report.Type == ActionType.Roll ? report.Outcome : null,
                FateDieValue = report.Type == ActionType.Roll ? report.FateDieValue : null,
                PreparedValue = report.Type == ActionType.Roll ? report.PreparedValue : 0,
                Effects = new List<ActionEffectRecord>(report.Effects),
                SourceNode = sourceNode
            };
        }

        private void DrawHeavyOutcomeOverlay()
        {
            if (_activeHeavyOutcome == null)
            {
                return;
            }

            GUI.color = IMGUIStyles.Blocker;
            GUI.DrawTexture(new Rect(0, 0, UIScale.VW, UIScale.VH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float modalW = 420f;
            float modalH = 210f;
            var modal = UIScale.CenteredModal(modalW, modalH);

            IMGUIStyles.DrawShadow(modal, new Vector2(5f, 6f), 0.50f);
            GUI.color = IMGUIStyles.ModalBg;
            GUI.DrawTexture(modal, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var presentation = _activeHeavyOutcome.OutcomePresentation;
            string title = presentation?.Title ?? _activeHeavyOutcomeActionName;
            var titleStyle = new GUIStyle(IMGUIStyles.ModalTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(18)
            };
            IMGUIStyles.DrawLabel(new Rect(modal.x + 24f, modal.y + 28f, modal.width - 48f, 28f), title, titleStyle);

            var btnRect = new Rect(modal.x + (modal.width - 112f) / 2f, modal.yMax - 50f, 112f, 30f);
            var mouse = Event.current.mousePosition;
            bool hovered = btnRect.Contains(mouse);
            bool clicked = hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            if (IMGUIStyles.DrawTechnicalButton(btnRect, "确 定", hovered, clicked, IMGUIStyles.PaperInk, new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.08f), IMGUIStyles.ExecuteLabel))
            {
                TryConfirmHeavyOutcome();
                Event.current.Use();
            }
            else
            {
                UsePointerEventForModal();
            }
        }

        private void DrawSpotlightOverlay()
        {
            var spotlight = _activeActionSpotlight ?? _gameManager.GameState.SpotlightCenter.Current;
            if (spotlight == null)
            {
                return;
            }

            GUI.color = IMGUIStyles.Blocker;
            GUI.DrawTexture(new Rect(0, 0, UIScale.VW, UIScale.VH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 聚光弹窗是「一封信 / 一条通告」，正文长度完全由内容决定：先量出正文换行后要多高，
            // 弹窗再照这个高度长。以前是固定 220 高 + 固定 72 高的正文框，长信要么被挤到按钮上，
            // 要么直接被裁掉。
            const float modalW = 600f;
            const float titleTop = 42f;
            const float titleH = 42f;
            const float bodyTop = 14f;      // 标题与正文之间
            const float bodyToButton = 22f; // 正文与按钮之间
            const float buttonH = 34f;
            const float bottomPad = 26f;
            float bodyWidth = modalW - 104f;

            var subtitleStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                wordWrap = true,
                alignment = TextAnchor.UpperCenter,
                fontSize = IMGUIStyles.FontSize(20)
            };
            bool hasBody = !string.IsNullOrWhiteSpace(spotlight.Subtitle);
            float bodyH = hasBody
                ? subtitleStyle.CalcHeight(new GUIContent(spotlight.Subtitle), bodyWidth)
                : 0f;

            float modalH = titleTop + titleH
                + (hasBody ? bodyTop + bodyH : 0f)
                + bodyToButton + buttonH + bottomPad;
            var modal = UIScale.CenteredModal(modalW, modalH);

            IMGUIStyles.DrawShadow(modal, new Vector2(5f, 6f), 0.50f);
            GUI.color = IMGUIStyles.ModalBg;
            GUI.DrawTexture(modal, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(IMGUIStyles.ModalTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(28)
            };
            IMGUIStyles.DrawLabel(new Rect(modal.x + 28f, modal.y + titleTop, modal.width - 56f, titleH), spotlight.Title, titleStyle);

            if (hasBody)
            {
                IMGUIStyles.DrawLabel(new Rect(modal.x + 44f, modal.y + titleTop + titleH + bodyTop, bodyWidth, bodyH),
                    spotlight.Subtitle, subtitleStyle);
            }

            var btnRect = new Rect(modal.x + (modal.width - 112f) / 2f, modal.yMax - bottomPad - buttonH, 112f, buttonH);
            var mouse = Event.current.mousePosition;
            bool hovered = btnRect.Contains(mouse);
            bool clicked = hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            if (IMGUIStyles.DrawTechnicalButton(btnRect, "确 定", hovered, clicked, IMGUIStyles.PaperInk, new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.08f), IMGUIStyles.ExecuteLabel))
            {
                TryAdvanceSpotlight();
                Event.current.Use();
            }
            else
            {
                UsePointerEventForModal();
            }
        }

        // 非阻塞:把当前可见的 banter 气泡画在各说话人锚点上方(不消费点击,游戏照常)。
        private void DrawBanterOverlay()
        {
            if (_banterPlayer.Visible.Count == 0)
                return;
            DialogueBubbleDrawer.DrawBanter(
                _banterPlayer,
                _dialogueAnchors,
                _gameManager,
                speaker => ReportRemoteFallback("play-banter!", speaker));
        }

        // 阻塞：立绘舞台覆盖世界；全屏任意左键推进，后方控件由 IsInputLocked 显式禁用。
        private void DrawConversationOverlay()
        {
            var line = _conversationPlayer.CurrentLine;
            if (line == null)
                return;

            bool usedRemoteFallback = DialogueStageDrawer.DrawConversationLine(
                line.Speaker,
                line.Text,
                _conversationPlayer.CurrentLineIndex,
                _dialogueAnchors,
                _conversationPlayer.AllowsRemoteParticipants);

            if (usedRemoteFallback && !_warnedAboutCurrentDialogueRemoteFallback)
            {
                ReportRemoteFallback("play-dialogue!", line.Speaker);
                _warnedAboutCurrentDialogueRemoteFallback = true;
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                TryAdvanceConversation();
                Event.current.Use();
            }
            else
            {
                UsePointerEventForModal();
            }
        }

        private void ReportRemoteFallback(string command, string speaker)
        {
            bool isBanter = command == "play-banter!";
            Debug.LogWarning(
                $"{command} 说话人 '{speaker}' 无法解析到当前屏幕锚点，"
                + (isBanter ? "已自动改用场外临时卡。" : "立绘舞台仍会显示，但本行被视为场外对白。")
                + "若此人确实不在场，请改用对应的 remote 调用；"
                + "否则请检查说话人拼写和动作前的可见节点。");
        }

        // 命名动画 v1 占位:居中显示 [动画] tag。将来替换为真正的命名动画 / Timeline 播放。
        private void DrawAnimationOverlay()
        {
            if (_activeAnimationTag == null)
                return;
            var style = new GUIStyle(IMGUIStyles.ModalTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(22),
            };
            IMGUIStyles.DrawLabel(new Rect(0f, UIScale.VH * 0.4f, UIScale.VW, 48f), $"[动画] {_activeAnimationTag}", style);
        }

        private static void UsePointerEventForModal()
        {
            if (Event.current.type == EventType.MouseDown
                || Event.current.type == EventType.MouseUp
                || Event.current.type == EventType.ScrollWheel)
            {
                Event.current.Use();
            }
        }

        private void DrawNarrationOverlay()
        {
            string text = _narrationPlayer.CurrentSubtitle;
            if (string.IsNullOrWhiteSpace(text))
                return;

            float bandH = 96f;
            var rect = new Rect(0f, UIScale.VH - bandH, UIScale.VW, bandH);

            var oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.76f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;

            var style = new GUIStyle(IMGUIStyles.ModalBody)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(22),
                wordWrap = true
            };
            style.normal.textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.65f);
            IMGUIStyles.DrawLabel(new Rect(160f, rect.y + 18f, UIScale.VW - 320f, bandH - 36f), text, style);
        }

        // 世界投射卡的活动范围。
        //
        // 以前是上下各缩进一条的横带：上边界是整条顶栏的底边，下边界给骰子那一排让路。
        // 两条线都横贯全屏，可它们挡住的东西并不横贯全屏——顶栏中段只有「你在哪、第几天」
        // 这类读的字，底栏中段干脆是空的。卡片被两条并不存在的墙夹在中间，四百来像素里
        // 放不下两张带骰位的动作卡，一间挂了四五个动作的酒馆必然叠成一摞。
        //
        // 现在按「这块地有没有人在用」算：安全区整块都能飘，只避开真的要用手指点的控件
        // （返回键、右上那组按钮、宿主胶囊、骰子与物品方块）。而且避让是**按横向跨度**算的
        // ——飘在屏幕中段的卡可以一直探到安全区的上下边缘，只有正压在某个按钮上下的卡才被
        // 推开。名字、面包屑、冷静条这些是读的不是点的，卡片压上去无妨。
        //
        // 这不只是好看：卡片能不能各自让开、不叠成一摞，差的就是这上下多出来的一百多像素。
        private readonly struct CardKeepOut
        {
            // 卡片离安全区边缘、离控件各留一点缝，别贴着画。
            private const float EdgeInset = 6f;
            private const float BlockerGap = 8f;

            private readonly Rect _safe;
            private readonly List<Rect> _blockers;

            public CardKeepOut(Rect safe, List<Rect> blockers)
            {
                _safe = safe;
                _blockers = blockers;
            }

            /// <summary>给定卡片占用的横向跨度，返回它可以待的纵向区间。</summary>
            public (float Top, float Bottom) BandFor(float left, float right)
            {
                float top = _safe.y + EdgeInset;
                float bottom = _safe.yMax - EdgeInset;
                float middle = _safe.center.y;

                foreach (var blocker in _blockers)
                {
                    if (blocker.width <= 0f || blocker.height <= 0f)
                        continue;
                    if (blocker.xMax <= left || blocker.x >= right)
                        continue;   // 横向不相干，这张卡从它旁边过得去

                    if (blocker.center.y < middle)
                        top = Mathf.Max(top, blocker.yMax + BlockerGap);
                    else
                        bottom = Mathf.Min(bottom, blocker.y - BlockerGap);
                }
                return (top, Mathf.Max(top, bottom));
            }
        }

        // 本帧要避开的控件。顶栏按钮**恒定预留**（返回键有时不画，但位置照让）：
        // 让位只在某些情况下发生的话，排布就会跟着状态跳来跳去。
        private CardKeepOut BuildCardKeepOut()
        {
            var blockers = new List<Rect>
            {
                _topHud.Back,
                _topHud.RelationToggle,
                _topHud.GrowthToggle,
                _topHud.DebugToggle,
                _topHud.DossierToggle,
                _topHud.SettingsToggle,
                _pinStripRect,
                _sceneBandRect,
            };
            HandPanelDrawer.CollectTouchBlockers(_gameManager, blockers);
            return new CardKeepOut(UIScale.SafeArea, blockers);
        }

        private static Rect ClampRect(Rect r, float cardWidth, float cardHeight, in CardKeepOut keepOut)
        {
            Rect safe = UIScale.SafeArea;
            float minX = safe.x + 12f;
            float maxX = Mathf.Max(minX, safe.xMax - cardWidth - 12f);
            float x = Mathf.Clamp(r.x, minX, maxX);

            var band = keepOut.BandFor(x, x + cardWidth);
            float maxY = Mathf.Max(band.Top, band.Bottom - cardHeight);
            return new Rect(x, Mathf.Clamp(r.y, band.Top, maxY), cardWidth, cardHeight);
        }

        private class ProjectedCardLayout
        {
            public GameNode? Node { get; }
            public CardPresentationResidue? Residue { get; }
            // 标注和卡片共用这套排布（同一片边距、同一个消重叠解算），但它不是卡：
            // 不参与命中、不挂附件、不走卡片引线层，排位也让卡片先挑。
            public bool IsAnnotation { get; }
            public string Key => Node?.Name ?? Residue!.HostNodeName;
            public Vector2 AnchorPos { get; }
            // 锚点相对上一帧移动了多少。卡片先按它刚性跟随，弹簧只消化排布的变化。
            public Vector2 AnchorDelta { get; set; }
            public float Distance { get; }
            // 锚点投影几乎重合时的兜底排序键，见 SolveProjectedStacks。
            public int DeclarationOrder { get; }
            public Vector2 TargetCenter { get; }
            public Vector2 SolvedCenter { get; set; }
            public Vector2 CurrentCenter { get; set; }
            public float Width { get; }
            public float Height { get; }
            // 布局只认卡本体。判定条、结果条与标签等 card attachments 是视觉延伸，
            // 可以覆盖邻卡，却绝不能因出现或消失带动整摞卡重排。
            public float FootprintWidth => Width;
            public float TopExtent => Height / 2f;
            public float BottomExtent => Height / 2f;
            public Rect Rect => new Rect(CurrentCenter.x - Width / 2f, CurrentCenter.y - Height / 2f, Width, Height);

            public ProjectedCardLayout(
                GameNode node,
                Vector2 anchorPos,
                float distance,
                int declarationOrder,
                Vector2 targetCenter,
                Vector2 currentCenter,
                float width,
                float height,
                bool isAnnotation = false)
            {
                Node = node;
                IsAnnotation = isAnnotation;
                AnchorPos = anchorPos;
                Distance = distance;
                DeclarationOrder = declarationOrder;
                TargetCenter = targetCenter;
                SolvedCenter = targetCenter;
                CurrentCenter = currentCenter;
                Width = width;
                Height = height;
            }

            public ProjectedCardLayout(
                CardPresentationResidue residue,
                Vector2 anchorPos,
                float distance,
                int declarationOrder,
                Vector2 targetCenter,
                Vector2 currentCenter,
                float width,
                float height)
            {
                Residue = residue;
                AnchorPos = anchorPos;
                Distance = distance;
                DeclarationOrder = declarationOrder;
                TargetCenter = targetCenter;
                SolvedCenter = targetCenter;
                CurrentCenter = currentCenter;
                Width = width;
                Height = height;
            }
        }
    }
}
