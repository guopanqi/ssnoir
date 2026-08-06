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

        private bool _isGrowthPanelOpen = false;
        private readonly IMGUIWindowStack _windowStack = new();

        private readonly Dictionary<string, Vector2> _cardCenters = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, CardPresentationResidue> _cardResidues = new Dictionary<string, CardPresentationResidue>();
        private readonly List<float> _gridScrollStack = new();
        private float _gridScrollOffset = 0f;
        private int _lastNavigationDepth = 0;
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
                        _activeAnimationTag = step.AnimationTag;
                        _animationTimer = AnimationPlaceholderSeconds;
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
        public bool IsInputLocked => _inputLocked || _activeHeavyOutcome != null || _activeActionSpotlight != null || _gameManager.GameState.SpotlightCenter.HasSpotlight || _conversationPlayer.IsActive || _activeAnimationTag != null;

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
            _cardResidues.Clear();
            _activeHeavyOutcome = null;
            _activeHeavyOutcomeActionName = string.Empty;
            _activeHeavyOutcomeDone = null;
            _pendingBlockingSteps.Clear();
            _activeActionSpotlight = null;
            _activeAnimationTag = null;
            _animationTimer = 0f;
            _banterPlayer.Reset();
            _conversationPlayer.Reset();
            DialogueStageDrawer.BeginConversation();
            _warnedAboutCurrentDialogueRemoteFallback = false;
            _completionReport = null;
            _completionActionName = string.Empty;
            _completionDone = null;
            DebugPanelDrawer.Reset();
            SettingsPanelDrawer.Reset();
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

            // 命名动画占位:到点后推进下一个阻塞剧情步骤
            if (_activeAnimationTag != null)
            {
                _animationTimer -= Time.deltaTime;
                if (_animationTimer <= 0f)
                {
                    _activeAnimationTag = null;
                    AdvanceToBlockingPresentationOrFinish();
                }
            }

            _animator.Update();

            // Right-click to cancel selection
            if (Input.GetMouseButtonDown(1)
                && !IsAnimationPlaying
                && !_isGrowthPanelOpen
                && !DebugPanelDrawer.IsOpen
                && !_inputLocked)
            {
                if (_gameManager.SelectedResource != null)
                {
                    _gameManager.ClearSelectedResource();
                }
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown
                && Event.current.type != EventType.MouseUp && Event.current.type != EventType.ScrollWheel
                && Event.current.type != EventType.Layout)
                return;

            // Scale the entire GUI to the reference resolution (1920×1080).
            UIScale.Apply();

            // Initialize styles if needed
            IMGUIStyles.Init(_gameManager.ChineseFont, _gameManager.SemiboldFont);

            // ── Camera Crossfade (reduce-motion focus change) ──
            // Drawn before anything else, so the frozen outgoing shot covers the live 3D
            // and every live panel sits on top of it. Only the world dissolves; the UI is
            // never in the frozen frame to begin with.
            var crossfade = _gameManager.CameraManager.Crossfade;
            var frozenView = crossfade.FrozenView;
            if (frozenView != null)
            {
                var previousColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, crossfade.Alpha);
                GUI.DrawTexture(new Rect(0, 0, UIScale.VW, UIScale.VH), frozenView);
                GUI.color = previousColor;
            }

            if (_gameManager.DisplayedSnapshot.Failure.IsFailed)
            {
                DrawFailureOverlay(Event.current.mousePosition);
                PointerOverUI = true;
                return;
            }
            SyncNavigationScrollState();

            Vector2 mouse = Event.current.mousePosition;
            bool mouseDown = Event.current.type == EventType.MouseDown && Event.current.button == 0;
            _windowStack.BeginFrame(mouse, mouseDown);

            // Reset the pointer-over-UI accumulator; widgets set it via CanHover
            // during this pass, and we persist the result at the end of OnGUI.
            IMGUIInteractionContext.ResetPointerOverUi();
            TopHudLayout topHud = TopHudLayout.Create();

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

            _windowStack.Update();
            _dialogueAnchors.Clear();

            // 你正身处其中的那些容器（导航栈）也是在场的人/地点，只是没有卡——进入「夜莺」
            // 之后她本人依然能开口。锚在面包屑上（气泡会翻到它下方），先登记，这样同名的
            // 真实卡片随后覆盖它，卡片优先。
            foreach (var container in _gameManager.NavigationStack)
                _dialogueAnchors.RegisterNode(container.Name, new Rect(40f, 30f, 360f, 44f));

            bool baseLocked = IsInputLocked || IsAnimationPlaying;
            var worldUi = _windowStack.MakeContext(IMGUIWindowLayer.World, baseLocked);
            var panelUi = _windowStack.MakeContext(IMGUIWindowLayer.Panel, baseLocked);

            // ── Navigation Bar ──
            NavigationDrawer.Draw(_gameManager, worldUi, topHud);
            // 展开的关系进展图是显式的 HUD 浮层；锁住其后的世界控件，避免点击穿透。
            if (NavigationDrawer.IsRelationExpanded)
                worldUi = _windowStack.MakeContext(IMGUIWindowLayer.World, true);

            // ── Node Cards (3D projected) ──
            DrawCards(worldUi);
            foreach (var kv in _cardCenters)
                _dialogueAnchors.RegisterNode(kv.Key, new Rect(kv.Value.x - 60f, kv.Value.y - 80f, 120f, 160f));

            // ── Bottom Panel ──
            HandPanelDrawer.Draw(_gameManager, worldUi, _dialogueAnchors);

            // ── Growth / Team Toggle Button ──
            DrawGrowthToggleButton(worldUi, topHud.GrowthToggle);

            // ── Settings / Debug 顶部按钮 ──
            // 三个面板（成长/设置/Debug）都不能整段跳过绘制——之前那样做会让按钮凭空消失，
            // 很突兀。改成始终画出来，被更高优先级面板占屏时只是传一个强制锁定的 ui 上下文，
            // 按钮可见但点不动。优先级：成长 > 设置 > Debug；打开谁就顺手关掉下面优先级的
            // 面板，避免两个居中纸卡模态叠在一起抢点击。
            var lockedPanelUi = _windowStack.MakeContext(IMGUIWindowLayer.Panel, true);
            var settingsUi = _isGrowthPanelOpen ? lockedPanelUi : panelUi;
            bool settingsWasOpen = SettingsPanelDrawer.IsOpen;
            SettingsPanelDrawer.Draw(settingsUi, topHud);
            if (!settingsWasOpen && SettingsPanelDrawer.IsOpen)
            {
                DebugPanelDrawer.Close();
            }

            var debugUi = (_isGrowthPanelOpen || SettingsPanelDrawer.IsOpen) ? lockedPanelUi : panelUi;
            DebugPanelDrawer.Draw(_gameManager, debugUi, topHud);

            // 大型关系进展图最后绘制在世界控件之上。
            NavigationDrawer.DrawRelationOverlay(_gameManager.DisplayedSnapshot);

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
            PointerOverUI = IMGUIInteractionContext.PointerOverUi || _windowStack.IsPointerOverBlocker();

            // Resolve a resource drag on release. rawType (not type) so this still
            // fires when a slot already consumed the MouseUp to place the token.
            if (Event.current.rawType == EventType.MouseUp && _gameManager.IsDraggingResource)
            {
                _gameManager.EndResourceDrag(Event.current.mousePosition);
            }
        }

        private void DrawFailureOverlay(Vector2 mouse)
        {
            var failure = _gameManager.DisplayedSnapshot.Failure;
            var screen = new Rect(0f, 0f, UIScale.VW, UIScale.VH);
            GUI.DrawTexture(screen, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                new Color(0.02f, 0.03f, 0.05f, 0.92f), 0f, 0f);

            const float cardWidth = 560f;
            const float cardHeight = 360f;
            var card = new Rect((UIScale.VW - cardWidth) * 0.5f, (UIScale.VH - cardHeight) * 0.5f,
                cardWidth, cardHeight);
            IMGUIStyles.DrawShadow(card, new Vector2(6f, 7f), 0.55f);
            GUI.DrawTexture(card, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                IMGUIStyles.Paper, 0f, 0f);
            IMGUIStyles.DrawOutline(card, 1f, IMGUIStyles.PaperInk);

            var titleStyle = new GUIStyle(IMGUIStyles.ModalTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(34),
                normal = { textColor = IMGUIStyles.SealRed }
            };
            var descriptionStyle = new GUIStyle(IMGUIStyles.StatusLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = IMGUIStyles.FontSize(18),
                normal = { textColor = IMGUIStyles.PaperInk }
            };
            GUI.Label(new Rect(card.x + 44f, card.y + 60f, card.width - 88f, 52f), failure.Title, titleStyle);
            GUI.Label(new Rect(card.x + 64f, card.y + 132f, card.width - 128f, 56f), failure.Description, descriptionStyle);
            IMGUIStyles.DrawLine(new Vector2(card.x + 64f, card.y + 212f), new Vector2(card.xMax - 64f, card.y + 212f),
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.35f), 1f);

            var ui = new IMGUIInteractionContext(mouse, isLocked: false);
            var restart = new Rect(card.x + 64f, card.y + 244f, card.width - 128f, 42f);
            var quit = new Rect(card.x + 64f, card.y + 296f, card.width - 128f, 34f);
            if (IMGUIButton.Draw(restart, "重新开始", ui,
                    IMGUIStyles.PaperInk, new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.10f),
                    IMGUIStyles.ExecuteLabel))
            {
                _gameManager.RestartGame();
                Event.current.Use();
            }
            else if (IMGUIButton.Draw(quit, "退出游戏", ui,
                         new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.50f),
                         new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.06f),
                         IMGUIStyles.StatusLabel))
            {
                Application.Quit();
                Event.current.Use();
            }
        }

        private void DrawCards(IMGUIInteractionContext ui)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var nodes = _gameManager.VisibleNodes;
            var focusedName = _gameManager.FocusedNodeName;

            // Split nodes into two groups: those with world anchors and those without
            var initialProjected = new List<(GameNode node, Vector3 screenPos, float distance)>();
            var projectedResidues = new List<(CardPresentationResidue residue, Vector3 screenPos, float distance)>();
            var gridNodes = new List<GameNode>();
            var gridResidues = new List<CardPresentationResidue>();
            var importantBeacons = new List<ImportantNodeBeacon>();
            var currentNodeNames = new HashSet<string>(nodes.Select(node => node.Name), StringComparer.OrdinalIgnoreCase);
            var restBlockers = _gameManager.DisplayedSnapshot.RestBlockers;

            foreach (var node in nodes)
            {
                if (!string.IsNullOrEmpty(focusedName) && node.Name != focusedName)
                    continue;

                var containedBlocker = RestBlockerPresentation.FindContained(node, restBlockers);
                var anchor = _gameManager.SceneDirectory?.GetAnchor(node.Name);
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
                        initialProjected.Add((node, screenPos, screenPos.z)); // screenPos is actual screen pixels
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
                else
                {
                    gridNodes.Add(node); // No anchor, draw in grid
                }
            }

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

                var anchor = _gameManager.SceneDirectory?.GetAnchor(pair.Key);
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
                visibleKeys.Add(item.residue.AnchorNodeName);
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

                bool isLocation = item.node.IsContainer;
                bool focused = isFocused(item.node.Name);

                // 地点卡即使信息少也保持偏方的体量（更有存在感、不发「融」），且高度足以容下悬浮建筑线稿。
                float cardWidth = focused ? 430f : (isLocation ? 180f : 340f);
                float contentHeight = CardDrawer.MeasureCardHeight(
                    item.node,
                    CardDrawer.Classify(item.node, anchored: true),
                    cardWidth,
                    _gameManager.DisplayedSnapshot.Actors);
                // 聚焦卡放大是「凑近看」，只抬下限，不再把内容压回一个固定高度。
                float cardHeight = focused ? Mathf.Max(320f, contentHeight) : contentHeight;
                float attachmentHeight = AttachmentHeightForNode(item.node, spacious: true);
                float attachmentWidth = attachmentHeight > 0f
                    ? ActionNodeDrawer.AttachmentWidth(cardWidth, spacious: true)
                    : cardWidth;

                // Default target center position (centered horizontally above 3D anchor point)
                Vector2 targetCenter = new Vector2(anchorX, anchorY - cardHeight / 2f - 40f);

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
                    targetCenter,
                    currentCenter,
                    cardWidth,
                    cardHeight,
                    attachmentWidth,
                    attachmentHeight));
            }

            foreach (var item in projectedResidues)
            {
                var virtualAnchor = UIScale.WorldPointToVirtual(item.screenPos);
                const float cardWidth = 340f;
                const float cardHeight = 190f;
                float attachmentWidth = ActionNodeDrawer.AttachmentWidth(cardWidth, spacious: true);
                float attachmentHeight = ActionNodeDrawer.ResidueAttachmentHeight(item.residue, spacious: true);
                Vector2 targetCenter = new Vector2(virtualAnchor.x, virtualAnchor.y - cardHeight / 2f - 40f);
                if (!_cardCenters.TryGetValue(item.residue.AnchorNodeName, out var currentCenter))
                {
                    currentCenter = targetCenter;
                    _cardCenters[item.residue.AnchorNodeName] = currentCenter;
                }

                layouts.Add(new ProjectedCardLayout(
                    item.residue,
                    new Vector2(virtualAnchor.x, virtualAnchor.y),
                    item.distance,
                    targetCenter,
                    currentCenter,
                    cardWidth,
                    cardHeight,
                    attachmentWidth,
                    attachmentHeight));
            }

            // Calculate mutual repulsion forces for overlapping cards
            for (int i = 0; i < layouts.Count; i++)
            {
                for (int j = i + 1; j < layouts.Count; j++)
                {
                    var a = layouts[i];
                    var b = layouts[j];

                    if (a.FootprintRect.Overlaps(b.FootprintRect))
                    {
                        // Calculate overlap on Y axis
                        float overlapY = Mathf.Min(a.FootprintRect.yMax, b.FootprintRect.yMax)
                            - Mathf.Max(a.FootprintRect.yMin, b.FootprintRect.yMin);
                        if (overlapY > 0)
                        {
                            // A continuous push force proportional to overlap to eliminate jitter/oscillations
                            float pushForce = overlapY * 0.4f;

                            if (a.CurrentCenter.y < b.CurrentCenter.y)
                            {
                                a.RepulsionForce += new Vector2(0f, -pushForce);
                                b.RepulsionForce += new Vector2(0f, pushForce);
                            }
                            else
                            {
                                a.RepulsionForce += new Vector2(0f, pushForce);
                                b.RepulsionForce += new Vector2(0f, -pushForce);
                            }
                        }
                    }
                }
            }

            // Integrate forces: update positions smoothly
            float attractionStrength = 0.08f; // Softer attraction strength to allow repulsion to dominate
            foreach (var layout in layouts)
            {
                Vector2 attraction = (layout.TargetCenter - layout.CurrentCenter) * attractionStrength;
                Vector2 nextCenter = layout.CurrentCenter + attraction + layout.RepulsionForce;

                // Create tentative rect and clamp to safe boundaries
                Rect nextFootprint = new Rect(
                    nextCenter.x - layout.FootprintWidth / 2f,
                    nextCenter.y - layout.Height / 2f,
                    layout.FootprintWidth,
                    layout.Height + layout.AttachmentHeight);
                nextFootprint = ClampRect(nextFootprint, layout.FootprintWidth, layout.Height + layout.AttachmentHeight);

                // Update current layout state and persistent cache
                layout.CurrentCenter = new Vector2(nextFootprint.center.x, nextFootprint.y + layout.Height / 2f);
                _cardCenters[layout.Key] = layout.CurrentCenter;
            }

            // Draw projected cards (sorted by distance, far to near)
            layouts.Sort((a, b) => b.Distance.CompareTo(a.Distance));

            // 命中归属：卡片是画家算法——投射卡按远→近画，网格卡再盖在最上面。而 IMGUI
            // 的点击是「先处理者 Event.Use() 吃掉」，顺序正好相反：不先解析一遍，重叠区域
            // 就会被画在最底下的那张卡抢走点击。这里先按绘制顺序挑出最上层那张，其余一律
            // 标记为遮挡——遮挡只吞命中与悬停，不改变卡片的可用/禁用外观。
            ProjectedCardLayout? hitOwner = null;
            if (!MouseOverGridCard(gridNodes, gridResidues, ui.Mouse))
            {
                for (int i = 0; i < layouts.Count; i++)
                {
                    if (layouts[i].FootprintRect.Contains(ui.Mouse))
                        hitOwner = layouts[i];
                }
            }

            foreach (var layout in layouts)
            {
                var cardUi = ReferenceEquals(layout, hitOwner) ? ui : ui.Occluded();
                if (layout.Residue != null)
                    DrawProjectedResidueCard(layout.Residue, layout.Rect, layout.AnchorPos, cardUi);
                else
                    DrawNodeCard(layout.Node!, layout.Rect, layout.AnchorPos, cardUi);
            }

            // 网格卡最后绘制，因此视觉上压在世界投射卡之上。
            if (gridNodes.Count > 0 || gridResidues.Count > 0)
            {
                DrawCardsGrid(gridNodes, gridResidues, ui);
            }

            string? beaconTarget = ImportantNodeBeaconDrawer.Draw(importantBeacons, ui);
            if (beaconTarget != null)
                _gameManager.CameraManager.NavigateToNode(beaconTarget);
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
        private const float GridCardWidth = 340f;
        // 残留（节点已消失、只剩结算结果的宿主卡）没有内容可量，保留一个固定的小盒子。
        private const float GridResidueCardHeight = 150f;
        private const float GridSpacing = 20f;
        private const float GridStartX = 40f;
        private const float GridStartY = 140f;
        // 金色脉冲描边会向卡片外扩最多 7.5px；网格卡不能贴着 GUI.Group 顶部，
        // 否则第一行卡片的上边会被父 Group 裁掉。
        private const float GridContentTopPadding = 10f;

        private static int GridCardsPerRow()
        {
            return Mathf.Max(1, (int)((UIScale.VW - GridStartX * 2) / (GridCardWidth + GridSpacing)));
        }

        private static Rect GridViewport()
        {
            return new Rect(0f, GridStartY, UIScale.VW, Mathf.Max(0f, (UIScale.VH - 175f) - GridStartY));
        }

        private float AttachmentHeightForNode(GameNode node, bool spacious)
        {
            bool isLocalRoll = _animator.IsPlaying
                && !_animator.UsesModal
                && string.Equals(_animator.ActionName, node.Name, StringComparison.OrdinalIgnoreCase);
            if (isLocalRoll)
                return ActionNodeDrawer.LocalRollAttachmentHeight(spacious);
            if (_cardResidues.TryGetValue(node.Name, out var residue))
                return ActionNodeDrawer.ResidueAttachmentHeight(residue, spacious);
            return 0f;
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
                float attachmentHeight = i < nodes.Count
                    ? AttachmentHeightForNode(nodes[i], spacious: false)
                    : ActionNodeDrawer.ResidueAttachmentHeight(residues[i - nodes.Count], spacious: false);
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
                layouts.Add(new GridCardLayout(cardRect, attachmentHeight));
                columnHeights[column] += markerSpace + cardHeight + attachmentHeight + GridSpacing;
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
            _gridScrollOffset = Mathf.Clamp(_gridScrollOffset, 0f, maxScroll);
            var layouts = BuildGridCardLayouts(visibleNodes, gridResidues, _gridScrollOffset, out _);

            GUI.BeginGroup(viewport);
            var localUi = new IMGUIInteractionContext(ui.Mouse - new Vector2(viewport.x, viewport.y), ui.IsLocked);

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
                var execution = GetCardExecutionState(node.Name);
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

            GUI.EndGroup();
            DrawGridScrollbar(viewport, contentHeight, _gridScrollOffset, maxScroll);
        }

        private readonly struct GridCardLayout
        {
            public Rect CardRect { get; }
            public float AttachmentHeight { get; }
            public Rect FootprintRect => new Rect(
                CardRect.x,
                CardRect.y,
                CardRect.width,
                CardRect.height + AttachmentHeight);

            public GridCardLayout(Rect cardRect, float attachmentHeight)
            {
                CardRect = cardRect;
                AttachmentHeight = attachmentHeight;
            }
        }

        private void DrawNodeCard(GameNode node, Rect cardRect, Vector2 anchorPos, IMGUIInteractionContext ui)
        {
            float anchorX = anchorPos.x;
            float anchorY = anchorPos.y;

            // Determine target Y on card edge (bottom center if card is above anchor, top center if card is below)
            float targetY = (anchorY > cardRect.yMax) ? cardRect.yMax : (anchorY < cardRect.yMin ? cardRect.yMin : anchorY);
            float targetX = cardRect.center.x;

            // Draw elbow polyline: (anchorX, anchorY) -> (targetX, anchorY) -> (targetX, targetY)
            Vector2 pStart = new Vector2(anchorX, anchorY);
            Vector2 pElbow = new Vector2(targetX, anchorY);
            Vector2 pEnd = new Vector2(targetX, targetY);

            Color lineColor = isFocused(node.Name)
                ? IMGUIStyles.Gold
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.55f);
            float lineThickness = isFocused(node.Name) ? 2f : 1.5f;

            // Draw the leader line segments behind the card
            IMGUIStyles.DrawLine(pStart, pElbow, lineColor, lineThickness);
            IMGUIStyles.DrawLine(pElbow, pEnd, lineColor, lineThickness);

            bool isHovered = ui.CanHover(cardRect);
            bool isFlipped = _gameManager.IsNodeFlipped(node.Name);
            bool focused = isFocused(node.Name);

            List<SlottedResource?>? slotted = null;
            if (node.Requires != null && node.Requires.Count > 0)
            {
                slotted = _gameManager.GetSlotsForNode(node.Name);
            }

            string backText = (node.Resolve?.Type == ResolveType.Observe) ? (node.Resolve?.ObserveText ?? "") : "";
            var execution = GetCardExecutionState(node.Name);
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

        private void DrawProjectedResidueCard(CardPresentationResidue residue, Rect cardRect, Vector2 anchorPos, IMGUIInteractionContext ui)
        {
            DrawProjectedLeaderLine(cardRect, anchorPos, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.32f), 1f);

            var source = residue.SourceNode ?? new GameNode
            {
                Name = residue.AnchorNodeName,
                Resolve = new GameResolve { Type = ResolveType.Instant },
            };
            var host = CreateDisabledResidueHost(source);
            var lockedUi = new IMGUIInteractionContext(ui.Mouse, isLocked: true);
            CardDrawer.DrawCard(cardRect, host, CardDrawer.CardKind.Action,
                isHovered: false, isFlipped: false, isFocused: false,
                slotted: null, clocks: host.Clocks, backText: string.Empty,
                ui: lockedUi, gameManager: _gameManager, residue: residue,
                spaciousAttachments: true);
        }

        private void DrawGridResidueCard(Rect cardRect, CardPresentationResidue residue, IMGUIInteractionContext ui)
        {
            var source = residue.SourceNode ?? new GameNode
            {
                Name = residue.AnchorNodeName,
                Resolve = new GameResolve { Type = ResolveType.Instant },
            };
            var host = CreateDisabledResidueHost(source);
            var lockedUi = new IMGUIInteractionContext(ui.Mouse, isLocked: true);
            CardDrawer.DrawCard(cardRect, host, CardDrawer.CardKind.Action,
                isHovered: false, isFlipped: false, isFocused: false,
                slotted: null, clocks: host.Clocks, backText: string.Empty,
                ui: lockedUi, gameManager: _gameManager, residue: residue);
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

        private static void DrawProjectedLeaderLine(Rect cardRect, Vector2 anchorPos, Color color, float thickness)
        {
            float targetY = anchorPos.y > cardRect.yMax ? cardRect.yMax
                : anchorPos.y < cardRect.yMin ? cardRect.yMin : anchorPos.y;
            float targetX = cardRect.center.x;
            var elbow = new Vector2(targetX, anchorPos.y);
            IMGUIStyles.DrawLine(anchorPos, elbow, color, thickness);
            IMGUIStyles.DrawLine(elbow, new Vector2(targetX, targetY), color, thickness);
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

        private (bool IsExecuting, float Progress, string Text) GetCardExecutionState(string nodeName)
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

        private List<GameClock> GetCurrentClocks()
        {
            var clocks = new List<GameClock>();
            if (_gameManager.NavigationStack.Count > 0)
            {
                var currentNode = _gameManager.NavigationStack[_gameManager.NavigationStack.Count - 1];
                clocks.AddRange(currentNode.Clocks);
            }
            else
            {
                var root = _gameManager.DisplayedSnapshot.RootNode;
                if (root != null)
                {
                    clocks.AddRange(root.Clocks);
                }
            }
            return clocks;
        }

        private void DrawGrowthToggleButton(IMGUIInteractionContext ui, Rect btnRect)
        {
            bool btnHover = ui.CanHover(btnRect);
            Color hoverBg = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f);
            Color outlineColor = _isGrowthPanelOpen
                ? IMGUIStyles.Gold
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f);

            if (IMGUIButton.Draw(btnRect, "成长/队伍", ui, outlineColor, hoverBg, IMGUIStyles.ExecuteLabel))
            {
                _isGrowthPanelOpen = !_isGrowthPanelOpen;
                if (_isGrowthPanelOpen)
                {
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
            var presentation = report.OutcomePresentation;
            if (presentation == null || !presentation.HasText || presentation.Mode != OutcomePresentationMode.Light)
            {
                return;
            }

            _cardResidues[actionName] = new CardPresentationResidue
            {
                AnchorNodeName = actionName,
                Title = presentation.Title,
                RollOutcome = report.Type == ActionType.Roll ? report.Outcome : null,
                FateDieValue = report.Type == ActionType.Roll ? report.FateDieValue : null,
                PreparedValue = report.Type == ActionType.Roll ? report.PreparedValue : 0,
                Effects = new List<ActionEffectRecord>(report.Effects),
                SourceNode = _gameManager.VisibleNodes.FirstOrDefault(node =>
                    string.Equals(node.Name, actionName, StringComparison.OrdinalIgnoreCase))
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
            var modal = new Rect((UIScale.VW - modalW) / 2f, (UIScale.VH - modalH) / 2f, modalW, modalH);

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
            GUI.Label(new Rect(modal.x + 24f, modal.y + 28f, modal.width - 48f, 28f), title, titleStyle);

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
            const float modalW = 460f;
            const float titleTop = 32f;
            const float titleH = 30f;
            const float bodyTop = 14f;      // 标题与正文之间
            const float bodyToButton = 22f; // 正文与按钮之间
            const float buttonH = 30f;
            const float bottomPad = 22f;
            float bodyWidth = modalW - 88f;

            var subtitleStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                wordWrap = true,
                alignment = TextAnchor.UpperCenter
            };
            bool hasBody = !string.IsNullOrWhiteSpace(spotlight.Subtitle);
            float bodyH = hasBody
                ? subtitleStyle.CalcHeight(new GUIContent(spotlight.Subtitle), bodyWidth)
                : 0f;

            float modalH = titleTop + titleH
                + (hasBody ? bodyTop + bodyH : 0f)
                + bodyToButton + buttonH + bottomPad;
            var modal = new Rect((UIScale.VW - modalW) / 2f, (UIScale.VH - modalH) / 2f, modalW, modalH);

            IMGUIStyles.DrawShadow(modal, new Vector2(5f, 6f), 0.50f);
            GUI.color = IMGUIStyles.ModalBg;
            GUI.DrawTexture(modal, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(IMGUIStyles.ModalTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(20)
            };
            GUI.Label(new Rect(modal.x + 28f, modal.y + titleTop, modal.width - 56f, titleH), spotlight.Title, titleStyle);

            if (hasBody)
            {
                GUI.Label(new Rect(modal.x + 44f, modal.y + titleTop + titleH + bodyTop, bodyWidth, bodyH),
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
            _gameManager.GameState.NotificationCenter.Push(
                isBanter
                    ? $"警告：「{speaker}」未在场，已显示场外卡。"
                    : $"警告：「{speaker}」未在场，已按场外对白呈现。",
                NotificationKind.Warning);
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
            GUI.Label(new Rect(0f, UIScale.VH * 0.4f, UIScale.VW, 48f), $"[动画] {_activeAnimationTag}", style);
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
            GUI.Label(new Rect(160f, rect.y + 18f, UIScale.VW - 320f, bandH - 36f), text, style);
        }

        private Rect ClampRect(Rect r, float cardWidth, float cardHeight)
        {
            float minX = 20f;
            float maxX = UIScale.VW - cardWidth - 20f;
            float minY = 90f;
            float maxY = UIScale.VH - 180f - cardHeight;
            return new Rect(Mathf.Clamp(r.x, minX, maxX), Mathf.Clamp(r.y, minY, maxY), cardWidth, cardHeight);
        }

        private class ProjectedCardLayout
        {
            public GameNode? Node { get; }
            public CardPresentationResidue? Residue { get; }
            public string Key => Node?.Name ?? Residue!.AnchorNodeName;
            public Vector2 AnchorPos { get; }
            public float Distance { get; }
            public Vector2 TargetCenter { get; }
            public Vector2 CurrentCenter { get; set; }
            public Vector2 RepulsionForce { get; set; }
            public float Width { get; }
            public float Height { get; }
            public float AttachmentWidth { get; }
            public float AttachmentHeight { get; }
            public float FootprintWidth => Mathf.Max(Width, AttachmentWidth);
            public Rect Rect => new Rect(CurrentCenter.x - Width / 2f, CurrentCenter.y - Height / 2f, Width, Height);
            public Rect FootprintRect => new Rect(
                CurrentCenter.x - FootprintWidth / 2f,
                Rect.y,
                FootprintWidth,
                Height + AttachmentHeight);

            public ProjectedCardLayout(
                GameNode node,
                Vector2 anchorPos,
                float distance,
                Vector2 targetCenter,
                Vector2 currentCenter,
                float width,
                float height,
                float attachmentWidth,
                float attachmentHeight)
            {
                Node = node;
                AnchorPos = anchorPos;
                Distance = distance;
                TargetCenter = targetCenter;
                CurrentCenter = currentCenter;
                Width = width;
                Height = height;
                AttachmentWidth = attachmentWidth;
                AttachmentHeight = attachmentHeight;
                RepulsionForce = Vector2.zero;
            }

            public ProjectedCardLayout(
                CardPresentationResidue residue,
                Vector2 anchorPos,
                float distance,
                Vector2 targetCenter,
                Vector2 currentCenter,
                float width,
                float height,
                float attachmentWidth,
                float attachmentHeight)
            {
                Residue = residue;
                AnchorPos = anchorPos;
                Distance = distance;
                TargetCenter = targetCenter;
                CurrentCenter = currentCenter;
                Width = width;
                Height = height;
                AttachmentWidth = attachmentWidth;
                AttachmentHeight = attachmentHeight;
                RepulsionForce = Vector2.zero;
            }
        }
    }
}
