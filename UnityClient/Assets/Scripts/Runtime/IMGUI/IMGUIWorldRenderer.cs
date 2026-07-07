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
            _banterPlayer.Suspend();
            _conversationPlayer.Start(sequence, () => _banterPlayer.Resume());
        }

        public bool IsPresentationActive => _presentationPlayer.IsPlaying || _animator.IsPlaying || _activeHeavyOutcome != null || _activeActionSpotlight != null || _conversationPlayer.IsActive || _activeAnimationTag != null;

        public void PlayPresentation(ActionReport report, string actionName, Action onDone)
        {
            _pendingBlockingSteps.Clear();
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
            _completionReport = null;
            _completionActionName = string.Empty;
            _completionDone = null;
            DebugPanelDrawer.Reset();
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
            SyncNavigationScrollState();

            Vector2 mouse = Event.current.mousePosition;
            bool mouseDown = Event.current.type == EventType.MouseDown && Event.current.button == 0;
            _windowStack.BeginFrame(mouse, mouseDown);

            // Reset the pointer-over-UI accumulator; widgets set it via CanHover
            // during this pass, and we persist the result at the end of OnGUI.
            IMGUIInteractionContext.ResetPointerOverUi();

            if (DebugPanelDrawer.IsOpen && !_isGrowthPanelOpen)
            {
                var (_, debugPanelRect) = DebugPanelDrawer.GetRects();
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

            bool baseLocked = IsInputLocked || IsAnimationPlaying;
            var worldUi = _windowStack.MakeContext(IMGUIWindowLayer.World, baseLocked);
            var panelUi = _windowStack.MakeContext(IMGUIWindowLayer.Panel, baseLocked);

            // ── Navigation Bar ──
            NavigationDrawer.Draw(_gameManager, worldUi);

            // ── Node Clocks ──
            // 当前所在层的时钟：干净徽章，居中且与顶栏控件同一行高（分割线 y=88 以上）。
            var clocks = GetCurrentClocks();
            if (clocks.Count > 0)
            {
                ClockDrawer.DrawClocksBar(clocks, 28f);
            }

            // ── Node Cards (3D projected) ──
            DrawCards(worldUi);
            foreach (var kv in _cardCenters)
                _dialogueAnchors.RegisterNode(kv.Key, new Rect(kv.Value.x - 60f, kv.Value.y - 80f, 120f, 160f));

            // ── Bottom Panel ──
            HandPanelDrawer.Draw(_gameManager, worldUi, _dialogueAnchors);

            // ── Growth / Team Toggle Button ──
            DrawGrowthToggleButton(worldUi);

            // ── Debug Panel (Save/Load + Scene Switch) ──
            if (!_isGrowthPanelOpen)
            {
                DebugPanelDrawer.Draw(_gameManager, panelUi);
            }

            // ── Overlays ──
            OverlayDrawer.DrawNotifications(_gameManager.GameState.NotificationCenter);
            OverlayDrawer.DrawCursorFollower(_gameManager);
            DrawPresentationOverlay();
            DrawBanterOverlay();
            DrawHeavyOutcomeOverlay();
            DrawSpotlightOverlay();
            DrawConversationOverlay();
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

        private void DrawCards(IMGUIInteractionContext ui)
        {
            var cam = Camera.main;
            if (cam == null) return;

            var nodes = _gameManager.VisibleNodes;
            var focusedName = _gameManager.FocusedNodeName;

            // Split nodes into two groups: those with world anchors and those without
            var initialProjected = new List<(GameNode node, Vector3 screenPos, float distance)>();
            var gridNodes = new List<GameNode>();

            foreach (var node in nodes)
            {
                if (!string.IsNullOrEmpty(focusedName) && node.Name != focusedName)
                    continue;

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
                    // Nodes with anchors panned out of view are not drawn (neither projected nor in fallback grid)
                }
                else
                {
                    gridNodes.Add(node); // No anchor, draw in grid
                }
            }

            // Clean up old cached centers that are no longer visible to avoid memory leaks
            var visibleKeys = new HashSet<string>(initialProjected.Select(x => x.node.Name));
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
                float cardHeight = focused ? 320f : (isLocation ? 168f : 190f);

                // Default target center position (centered horizontally above 3D anchor point)
                Vector2 targetCenter = new Vector2(anchorX, anchorY - cardHeight / 2f - 40f);

                // Retrieve from cache or initialize
                if (!_cardCenters.TryGetValue(item.node.Name, out var currentCenter))
                {
                    currentCenter = targetCenter;
                    _cardCenters[item.node.Name] = currentCenter;
                }

                layouts.Add(new ProjectedCardLayout(item.node, new Vector2(anchorX, anchorY), item.distance, targetCenter, currentCenter, cardWidth, cardHeight));
            }

            // Calculate mutual repulsion forces for overlapping cards
            for (int i = 0; i < layouts.Count; i++)
            {
                for (int j = i + 1; j < layouts.Count; j++)
                {
                    var a = layouts[i];
                    var b = layouts[j];

                    if (a.Rect.Overlaps(b.Rect))
                    {
                        // Calculate overlap on Y axis
                        float overlapY = Mathf.Min(a.Rect.yMax, b.Rect.yMax) - Mathf.Max(a.Rect.yMin, b.Rect.yMin);
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
                Rect nextRect = new Rect(nextCenter.x - layout.Width / 2f, nextCenter.y - layout.Height / 2f, layout.Width, layout.Height);
                nextRect = ClampRect(nextRect, layout.Width, layout.Height);

                // Update current layout state and persistent cache
                layout.CurrentCenter = nextRect.center;
                _cardCenters[layout.Node.Name] = layout.CurrentCenter;
            }

            // Draw projected cards (sorted by distance, far to near)
            layouts.Sort((a, b) => b.Distance.CompareTo(a.Distance));
            foreach (var layout in layouts)
            {
                DrawNodeCard(layout.Node, layout.Rect, layout.AnchorPos, ui);
            }

            // Draw grid cards below
            if (gridNodes.Count > 0 || _cardResidues.Count > 0)
            {
                DrawCardsGrid(gridNodes, ui);
            }
        }

        private void DrawCardsGrid(List<GameNode> nodes, IMGUIInteractionContext ui)
        {
            var visibleNodes = nodes.ToList();
            float cardWidth = 340f;
            float cardHeight = 190f;
            float spacing = 20f;
            float startX = 40f;
            float startY = 140f;
            int cardsPerRow = Mathf.Max(1, (int)((UIScale.VW - startX * 2) / (cardWidth + spacing)));
            var visibleNames = new HashSet<string>(visibleNodes.Select(n => n.Name), StringComparer.OrdinalIgnoreCase);
            var orphanResidues = _cardResidues
                .Where(pair => !visibleNames.Contains(pair.Key))
                .Select(pair => pair.Value)
                .ToList();
            int totalCards = visibleNodes.Count + orphanResidues.Count;
            int rowCount = totalCards == 0 ? 0 : (totalCards + cardsPerRow - 1) / cardsPerRow;
            float contentHeight = rowCount == 0 ? 0f : rowCount * cardHeight + Mathf.Max(0, rowCount - 1) * spacing;
            float viewportBottom = UIScale.VH - 175f;
            var viewport = new Rect(0f, startY, UIScale.VW, Mathf.Max(0f, viewportBottom - startY));
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

            GUI.BeginGroup(viewport);
            var localUi = new IMGUIInteractionContext(ui.Mouse - new Vector2(viewport.x, viewport.y), ui.IsLocked);

            for (int i = 0; i < totalCards; i++)
            {
                int row = i / cardsPerRow;
                int col = i % cardsPerRow;
                float x = startX + col * (cardWidth + spacing);
                float y = row * (cardHeight + spacing) - _gridScrollOffset;
                if (y > viewport.height || y + cardHeight < 0f)
                {
                    continue;
                }
                var cardRect = new Rect(x, y, cardWidth, cardHeight);

                if (i >= visibleNodes.Count)
                {
                    CardDrawer.DrawResidueCard(cardRect, orphanResidues[i - visibleNodes.Count]);
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

                var interaction = CardDrawer.DrawCard(cardRect, node, CardDrawer.Classify(node, anchored: false), isHovered, isFlipped, focused,
                    slotted, node.Clocks, backText, localUi, _gameManager,
                    execution.IsExecuting, execution.Progress, execution.Text,
                    isLocalRoll ? _animator.CurrentReport : null,
                    _animator.Phase,
                    _animator.DisplayedDieValue,
                    _animator.DisplayScale,
                    residue);

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

            var interaction = CardDrawer.DrawCard(cardRect, node, CardDrawer.Classify(node, anchored: true), isHovered, isFlipped, focused,
                slotted, node.Clocks, backText, ui, _gameManager,
                execution.IsExecuting, execution.Progress, execution.Text,
                isLocalRoll ? _animator.CurrentReport : null,
                _animator.Phase,
                _animator.DisplayedDieValue,
                _animator.DisplayScale,
                residue);

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

        private void DrawGrowthToggleButton(IMGUIInteractionContext ui)
        {
            // 收进右上簇（关系条右侧），与「世界状态」归为一处，不再浮在半空。
            float btnW = 112f;
            float btnH = 34f;
            float btnX = UIScale.VW - btnW - 40f;
            float btnY = 26f;
            var btnRect = new Rect(btnX, btnY, btnW, btnH);

            bool btnHover = ui.CanHover(btnRect);
            Color hoverBg = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f);
            Color outlineColor = _isGrowthPanelOpen
                ? IMGUIStyles.Gold
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f);

            if (IMGUIButton.Draw(btnRect, "成长/队伍", ui, outlineColor, hoverBg, IMGUIStyles.ExecuteLabel))
            {
                _isGrowthPanelOpen = !_isGrowthPanelOpen;
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
                Subtitle = presentation.Subtitle,
                RollOutcome = report.Type == ActionType.Roll ? report.Outcome : null,
                Effects = new List<ActionEffectRecord>(report.Effects)
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
                fontSize = 18
            };
            GUI.Label(new Rect(modal.x + 24f, modal.y + 28f, modal.width - 48f, 28f), title, titleStyle);

            if (presentation != null && !string.IsNullOrWhiteSpace(presentation.Subtitle))
            {
                var subtitleStyle = new GUIStyle(IMGUIStyles.ModalBody)
                {
                    wordWrap = true,
                    alignment = TextAnchor.UpperCenter
                };
                GUI.Label(new Rect(modal.x + 36f, modal.y + 70f, modal.width - 72f, 70f), presentation.Subtitle, subtitleStyle);
            }

            var btnRect = new Rect(modal.x + (modal.width - 112f) / 2f, modal.yMax - 50f, 112f, 30f);
            var mouse = Event.current.mousePosition;
            bool hovered = btnRect.Contains(mouse);
            bool clicked = hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            if (IMGUIStyles.DrawTechnicalButton(btnRect, "确 定", hovered, clicked, IMGUIStyles.PaperInk, new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.08f), IMGUIStyles.ExecuteLabel))
            {
                _activeHeavyOutcome = null;
                _activeHeavyOutcomeActionName = string.Empty;
                _activeHeavyOutcomeDone = null;
                AdvanceToBlockingPresentationOrFinish();
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

            float modalW = 460f;
            float modalH = 220f;
            var modal = new Rect((UIScale.VW - modalW) / 2f, (UIScale.VH - modalH) / 2f, modalW, modalH);

            IMGUIStyles.DrawShadow(modal, new Vector2(5f, 6f), 0.50f);
            GUI.color = IMGUIStyles.ModalBg;
            GUI.DrawTexture(modal, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var titleStyle = new GUIStyle(IMGUIStyles.ModalTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20
            };
            GUI.Label(new Rect(modal.x + 28f, modal.y + 32f, modal.width - 56f, 30f), spotlight.Title, titleStyle);

            if (!string.IsNullOrWhiteSpace(spotlight.Subtitle))
            {
                var subtitleStyle = new GUIStyle(IMGUIStyles.ModalBody)
                {
                    wordWrap = true,
                    alignment = TextAnchor.UpperCenter
                };
                GUI.Label(new Rect(modal.x + 44f, modal.y + 78f, modal.width - 88f, 72f), spotlight.Subtitle, subtitleStyle);
            }

            var btnRect = new Rect(modal.x + (modal.width - 112f) / 2f, modal.yMax - 52f, 112f, 30f);
            var mouse = Event.current.mousePosition;
            bool hovered = btnRect.Contains(mouse);
            bool clicked = hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            if (IMGUIStyles.DrawTechnicalButton(btnRect, "确 定", hovered, clicked, IMGUIStyles.PaperInk, new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.08f), IMGUIStyles.ExecuteLabel))
            {
                if (_activeActionSpotlight != null)
                    ConfirmActionSpotlight();
                else
                    _gameManager.GameState.SpotlightCenter.Dismiss();
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
            DialogueBubbleDrawer.DrawBanter(_banterPlayer, _dialogueAnchors);
        }

        // 阻塞:画当前对话行;全屏接收点击以推进(表现上无遮罩)。
        private void DrawConversationOverlay()
        {
            var line = _conversationPlayer.CurrentLine;
            if (line == null)
                return;

            DialogueBubbleDrawer.DrawConversationLine(line.Speaker, line.Text, _dialogueAnchors);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                _conversationPlayer.Advance();
                Event.current.Use();
            }
            else
            {
                UsePointerEventForModal();
            }
        }

        // 命名动画 v1 占位:居中显示 [动画] tag。将来替换为真正的命名动画 / Timeline 播放。
        private void DrawAnimationOverlay()
        {
            if (_activeAnimationTag == null)
                return;
            var style = new GUIStyle(IMGUIStyles.ModalTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
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
                fontSize = 22,
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
            public GameNode Node { get; }
            public Vector2 AnchorPos { get; }
            public float Distance { get; }
            public Vector2 TargetCenter { get; }
            public Vector2 CurrentCenter { get; set; }
            public Vector2 RepulsionForce { get; set; }
            public float Width { get; }
            public float Height { get; }
            public Rect Rect => new Rect(CurrentCenter.x - Width / 2f, CurrentCenter.y - Height / 2f, Width, Height);

            public ProjectedCardLayout(GameNode node, Vector2 anchorPos, float distance, Vector2 targetCenter, Vector2 currentCenter, float width, float height)
            {
                Node = node;
                AnchorPos = anchorPos;
                Distance = distance;
                TargetCenter = targetCenter;
                CurrentCenter = currentCenter;
                Width = width;
                Height = height;
                RepulsionForce = Vector2.zero;
            }
        }
    }
}
