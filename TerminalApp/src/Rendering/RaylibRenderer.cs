using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Raylib_cs;
using SSNoir.Core;
using SSNoir.TerminalApp.Rendering;

namespace SSNoir.Rendering
{
    public class RaylibRenderer
    {
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly RendererState _state;
        private readonly UiWindowStack _windowStack = new();
        private Action? _presentationDoneCallback;

        private const int WindowWidth = 900;
        private const int WindowHeight = 700;
        private static readonly bool FastPresentationMode =
            string.Equals(Environment.GetEnvironmentVariable("SSNOIR_FAST_PRESENTATION"), "1", StringComparison.Ordinal);

        public RaylibRenderer(SceneManager sceneManager, GameState gameState)
        {
            _sceneManager = sceneManager;
            _gameState = gameState;
            _state = new RendererState();

            SceneDropdownWidget.LoadAvailableScenes(_state);

            _gameState.NarrationCenter.OnNarrationRequested += ShowNarration;

            _sceneManager.OnSceneLoaded += () =>
            {
                ResetSceneUiState();
                _state.IsPresentingAction = false;
                _state.PendingReport = null;
                _state.ActiveRollResult = null;
                _state.ActiveOutcomeResult = null;
                _state.ActiveOutcomeActionName = string.Empty;
                AdoptLatestSnapshot();
            };
        }

        private void ResetSceneUiState()
        {
            _state.NavigationStack.Clear();
            _state.ClearAllNodeSlots();
            _state.SelectedResource = null;
            _state.CardsScrollOffset = 0f;
            _state.CardsScrollStack.Clear();
            _state.HandItemsScrollOffset = 0f;
            _state.CardResidues.Clear();
            _state.IsTurnPanelOpen = false;
            _state.IsGrowthPanelOpen = false;
            _state.IsDebugMenuOpen = false;
        }

        private void AdoptLatestSnapshot()
        {
            _state.DisplayedSnapshot = _sceneManager.LatestSnapshot;
            ResolveNavigationStack();
            SanitizeSlots();
        }

        private void FinishPresentation()
        {
            AddCardResidueIfNeeded();
            AdoptLatestSnapshot();

            var report = _state.PendingReport;
            _state.PendingReport = null;
            _state.PendingActionName = string.Empty;
            _state.IsPresentingAction = false;
            _state.PresentationStepIndex = 0;
            _state.PresentationTimer = 0f;
            _state.PendingActionSpotlights.Clear();
            _state.ActiveActionSpotlight = null;

            if (report != null)
            {
                ReleaseNarrations(report.NarrationIds);
                ReleaseBanter(report.Banter);
            }

            var callback = _presentationDoneCallback;
            _presentationDoneCallback = null;
            callback?.Invoke();
        }

        private void ReleaseNarrations(System.Collections.Generic.List<string> ids)
        {
            foreach (var id in ids)
                _gameState.NarrationCenter.Play(id);
        }

        // Terminal(兜底):banter 以非阻塞通知形式呈现。
        private void ReleaseBanter(System.Collections.Generic.List<DialogueSequence> sequences)
        {
            foreach (var seq in sequences)
                foreach (var line in seq.Lines)
                    _gameState.NotificationCenter.Push($"{line.Speaker}:{line.Text}", NotificationKind.Info);
        }

        private void ShowNarration(string id)
        {
            _state.ActiveNarrationId = id;
            _state.ActiveNarrationTime = 0f;
        }

        private void UpdateNarration(float dt)
        {
            if (string.IsNullOrEmpty(_state.ActiveNarrationId))
                return;

            _state.ActiveNarrationTime += dt;
            if (_state.ActiveNarrationTime >= _state.ActiveNarrationDuration)
            {
                _state.ActiveNarrationId = string.Empty;
                _state.ActiveNarrationTime = 0f;
            }
        }

        private void AdvanceToBlockingPresentationOrFinish()
        {
            if (_state.PendingActionSpotlights.Count > 0)
            {
                _state.ActiveActionSpotlight = _state.PendingActionSpotlights.Dequeue();
                return;
            }
            FinishPresentation();
        }

        private void StartPresentation(ActionReport report, string actionName = "", Action? onDone = null)
        {
            _state.PendingActionName = actionName;
            _state.PendingReport = report;
            _presentationDoneCallback = onDone;
            _state.PendingActionSpotlights.Clear();
            foreach (var step in report.BlockingStorySteps)
            {
                switch (step.Kind)
                {
                    case BlockingStoryStepKind.Spotlight when step.Spotlight != null:
                        _state.PendingActionSpotlights.Enqueue(step.Spotlight);
                        break;
                    case BlockingStoryStepKind.Dialogue when step.Dialogue != null:
                        // Terminal(兜底):阻塞对话逐句复用聚光弹窗呈现
                        foreach (var ln in step.Dialogue.Lines)
                            _state.PendingActionSpotlights.Enqueue(new SpotlightCard { Title = ln.Speaker, Subtitle = ln.Text });
                        break;
                    case BlockingStoryStepKind.Animation:
                        _state.PendingActionSpotlights.Enqueue(new SpotlightCard { Title = "[动画]", Subtitle = step.AnimationTag });
                        break;
                }
            }
            if (FastPresentationMode)
            {
                ShowHeavyOutcomeOrFinish();
                return;
            }

            _state.IsPresentingAction = true;
            _state.PresentationStepIndex = 0;
            _state.PresentationTimer = 0f;
        }

        private void UpdatePresentation(float dt)
        {
            if (!_state.IsPresentingAction || _state.PendingReport == null)
            {
                return;
            }

            if (_state.ActiveRollResult != null
                || _state.ActiveOutcomeResult != null
                || _state.ActiveActionSpotlight != null)
            {
                return;
            }

            var hints = _state.PendingReport.PresentationHints;
            if (hints == null || hints.Count == 0 || _state.PresentationStepIndex >= hints.Count)
            {
                ShowHeavyOutcomeOrFinish();
                return;
            }

            var hint = hints[_state.PresentationStepIndex];
            if (hint.Kind == PresentationHintKind.RollDice)
            {
                _state.ActiveRollResult = _state.PendingReport;
                _state.ActiveRollActionName = _state.PendingActionName;
                _state.ActiveRollTime = 0f;
                _state.ActiveRollPhase = 0;
                _state.ActiveRollDisplayDieValue = 1;
                _state.ActiveRollDisplayScale = 1f;
                return;
            }

            _state.PresentationTimer += dt;
            if (_state.PresentationTimer < hint.DurationSeconds)
            {
                return;
            }

            _state.PresentationTimer = 0f;
            _state.PresentationStepIndex++;

            if (_state.PresentationStepIndex >= hints.Count)
            {
                ShowHeavyOutcomeOrFinish();
            }
        }

        private void AdvancePresentationAfterRollConfirm()
        {
            _state.PresentationStepIndex++;
            _state.PresentationTimer = 0f;

            var hints = _state.PendingReport?.PresentationHints;
            if (hints == null || _state.PresentationStepIndex >= hints.Count)
            {
                ShowHeavyOutcomeOrFinish();
            }
        }

        private void ShowHeavyOutcomeOrFinish()
        {
            if (_state.PendingReport != null
                && OutcomePresentationPolicy.ShouldUseOutcomeModal(_state.PendingReport, _state.PendingActionName))
            {
                _state.ActiveOutcomeResult = _state.PendingReport;
                _state.ActiveOutcomeActionName = _state.PendingActionName;
                return;
            }

            AdvanceToBlockingPresentationOrFinish();
        }

        private void ConfirmHeavyOutcome()
        {
            _state.ActiveOutcomeResult = null;
            _state.ActiveOutcomeActionName = string.Empty;
            AdvanceToBlockingPresentationOrFinish();
        }

        private void ConfirmActionSpotlight()
        {
            _state.ActiveActionSpotlight = null;
            AdvanceToBlockingPresentationOrFinish();
        }

        private bool ActiveRollUsesModal()
        {
            return _state.ActiveRollResult != null
                && OutcomePresentationPolicy.ShouldUseRollModal(_state.ActiveRollResult, _state.ActiveRollActionName);
        }

        private void AddCardResidueIfNeeded()
        {
            var report = _state.PendingReport;
            if (string.IsNullOrEmpty(_state.PendingActionName))
            {
                return;
            }

            bool hasRollResult = report?.Type == ActionType.Roll;
            bool hasLightPresentation = report?.OutcomePresentation?.Mode == OutcomePresentationMode.Light
                && report.OutcomePresentation.HasText;
            if (!hasRollResult && !hasLightPresentation)
            {
                return;
            }

            _state.CardResidues[_state.PendingActionName] = new CardPresentationResidue
            {
                AnchorNodeName = _state.PendingActionName,
                Title = hasLightPresentation ? report!.OutcomePresentation!.Title : string.Empty,
                Subtitle = hasLightPresentation ? report!.OutcomePresentation!.Subtitle : string.Empty,
                RollOutcome = hasRollResult ? report!.Outcome : null,
                DieValue = hasRollResult ? report!.FinalRollValue : null,
                ModifiedRollValue = hasRollResult ? report!.ModifiedRollValue : null,
                Effects = new List<ActionEffectRecord>(report!.Effects)
            };
        }

        private void DrawPresentationOverlay()
        {
            // Execution progress is drawn inside the active card's execute button.
        }

        private (bool IsExecuting, float Progress, string Text) GetCardExecutionState(string nodeName)
        {
            if (!_state.IsPresentingAction || _state.PendingReport == null || _state.ActiveRollResult != null)
            {
                return (false, 0f, string.Empty);
            }
            if (!string.Equals(_state.PendingActionName, nodeName, StringComparison.OrdinalIgnoreCase))
            {
                return (false, 0f, string.Empty);
            }

            var hints = _state.PendingReport.PresentationHints;
            if (hints == null || _state.PresentationStepIndex >= hints.Count)
            {
                return (false, 0f, string.Empty);
            }

            var hint = hints[_state.PresentationStepIndex];
            if (hint.Kind != PresentationHintKind.ExecuteProgress)
            {
                return (false, 0f, string.Empty);
            }

            float progress = hint.DurationSeconds <= 0f
                ? 1f
                : Math.Clamp(_state.PresentationTimer / hint.DurationSeconds, 0f, 1f);
            return (true, progress, string.IsNullOrEmpty(hint.Text) ? "执行中" : hint.Text);
        }

        private static ActionReport CreateEndTurnReport()
        {
            return new ActionReport
            {
                Type = ActionType.Instant,
                PresentationHints = new List<PresentationHint>
                {
                    new PresentationHint
                    {
                        Kind = PresentationHintKind.ExecuteProgress,
                        Text = "回合结束",
                        DurationSeconds = 0.2f,
                    },
                },
            };
        }

        private void ExecuteNodeAction(GameNode node, List<SlottedResource?> slots)
        {
            string sceneBefore = _sceneManager.CurrentSceneName;
            _state.CardResidues.Clear();
            var report = _sceneManager.ExecuteAction(node, slots);
            _state.NodeSlots.Remove(node.Name);
            _state.SelectedResource = null;
            bool sceneChanged = !string.Equals(sceneBefore, _sceneManager.CurrentSceneName, StringComparison.OrdinalIgnoreCase);
            if (sceneChanged)
            {
                ResetSceneUiState();
            }
            StartPresentation(report, sceneChanged ? string.Empty : node.Name);
        }

        private void SaveCurrentGame(string? filePath = null)
        {
            try
            {
                var path = filePath ?? SaveManager.DefaultSavePath;
                _sceneManager.SaveGame(path);
                _gameState.NotificationCenter.Push("游戏已存档。", NotificationKind.Success);
            }
            catch (Exception ex)
            {
                _gameState.NotificationCenter.Push($"存档失败: {ex.Message}", NotificationKind.Error);
            }
        }

        private void LoadSavedGame(string? filePath = null)
        {
            var path = filePath ?? SaveManager.DefaultSavePath;
            if (!System.IO.File.Exists(path))
            {
                _gameState.NotificationCenter.Push("没有找到存档文件。", NotificationKind.Warning);
                return;
            }

            try
            {
                _sceneManager.LoadGame(path);
                _state.DisplayedSnapshot = _sceneManager.LatestSnapshot;
                _state.SelectedResource = null;
                _gameState.NotificationCenter.Push("游戏已读档。", NotificationKind.Success);
            }
            catch (Exception ex)
            {
                _gameState.NotificationCenter.Push($"读档失败: {ex.Message}", NotificationKind.Error);
            }
        }

        private void ResolveNavigationStack()
        {
            var root = _state.DisplayedSnapshot.RootNode;
            if (root == null)
            {
                _state.NavigationStack.Clear();
                _state.CardsScrollStack.Clear();
                _state.CardsScrollOffset = 0f;
                _state.VisibleNodes = new List<GameNode>();
                return;
            }

            if (_state.NavigationStack.Count == 0)
            {
                _state.CardsScrollStack.Clear();
                _state.VisibleNodes = root.Children.ToList();
                return;
            }

            var path = new List<string>();
            foreach (var node in _state.NavigationStack)
            {
                path.Add(node.Name);
            }

            _state.NavigationStack.Clear();
            var currentLevel = root.Children.ToList();

            foreach (var name in path)
            {
                var match = currentLevel.Find(n => n.Name == name);
                if (match != null && match.IsContainer)
                {
                    _state.NavigationStack.Add(match);
                    currentLevel = match.Children;
                }
                else
                {
                    _state.NavigationStack.Clear();
                    _state.CardsScrollStack.Clear();
                    _state.CardsScrollOffset = 0f;
                    _state.VisibleNodes = root.Children.ToList();
                    return;
                }
            }

            _state.VisibleNodes = currentLevel;
        }

        private void GoBack()
        {
            if (_state.NavigationStack.Count > 0)
            {
                _state.ClearAllNodeSlots();
                _state.NavigationStack.RemoveAt(_state.NavigationStack.Count - 1);
                _state.CardsScrollOffset = PopCardsScrollOffset();
                ResolveNavigationStack();
            }
        }

        private float PopCardsScrollOffset()
        {
            if (_state.CardsScrollStack.Count == 0)
            {
                return 0f;
            }

            int lastIndex = _state.CardsScrollStack.Count - 1;
            float offset = _state.CardsScrollStack[lastIndex];
            _state.CardsScrollStack.RemoveAt(lastIndex);
            return offset;
        }

        private void NavigateToHome()
        {
            var homeNode = FindNodeByName(_state.DisplayedSnapshot.RootNode, "家");
            if (homeNode == null)
            {
                throw new InvalidOperationException("Expected '家' node in world.");
            }

            bool alreadyAtHome = _state.NavigationStack.Count > 0
                && _state.NavigationStack[_state.NavigationStack.Count - 1].Name == "家";

            if (alreadyAtHome)
            {
                return;
            }

            _state.ClearAllNodeSlots();
            _state.SelectedResource = null;
            _state.CardsScrollOffset = 0f;
            _state.CardsScrollStack.Clear();
            _state.NavigationStack.Clear();
            _state.NavigationStack.Add(homeNode);
            ResolveNavigationStack();
        }

        private bool IsInEncounter =>
            !_sceneManager.CurrentSceneName.Equals("world", StringComparison.OrdinalIgnoreCase);

        private void SanitizeSlots()
        {
            var keysToRemove = new List<string>();
            foreach (var key in _state.NodeSlots.Keys)
            {
                if (FindNodeByName(_state.DisplayedSnapshot.RootNode, key) == null)
                {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in keysToRemove)
            {
                _state.NodeSlots.Remove(key);
            }
        }

        private GameNode? FindNodeByName(GameNode? node, string name)
        {
            if (node == null)
                return null;

            if (node.Name == name)
                return node;

            foreach (var child in node.Children)
            {
                var found = FindNodeByName(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        public void Run()
        {
            Raylib.SetConfigFlags(ConfigFlags.HighDpiWindow | ConfigFlags.Msaa4xHint);
            Raylib.InitWindow(WindowWidth, WindowHeight, "SSNoir Prototype");
            Raylib.SetExitKey(KeyboardKey.Null); // Disable ESC key exiting the game
            Raylib.SetTargetFPS(60);

            FontManager.LoadFont("assets/fonts/MiSans-Normal.ttf", 48);

            _sceneManager.LoadScene(_gameState.Get<string>("location"));

            while (!Raylib.WindowShouldClose())
            {
                UpdateAndDraw();
            }

            FontManager.UnloadFont();
            Raylib.CloseWindow();
        }

        private void UpdateAndDraw()
        {
            if (_state.DisplayedSnapshot.Health <= 0)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape))
                {
                    Raylib.CloseWindow();
                    Environment.Exit(0);
                }

                Raylib.BeginDrawing();
                Raylib.ClearBackground(new Color(15, 15, 20, 255));

                int screenWidth = Raylib.GetScreenWidth();
                int screenHeight = Raylib.GetScreenHeight();

                string title = "GAME OVER";
                string sub = "主角生命值归零，游戏结束。";
                string tip = "按 ESC 或关闭窗口退出程序。";

                int titleSize = 48;
                int titleWidth = FontManager.MeasureTextWidth(title, titleSize);
                FontManager.DrawText(title, (screenWidth - titleWidth) / 2f, screenHeight / 2f - 60f, titleSize, Color.Red);

                int subSize = 24;
                int subWidth = FontManager.MeasureTextWidth(sub, subSize);
                FontManager.DrawText(sub, (screenWidth - subWidth) / 2f, screenHeight / 2f + 10f, subSize, new Color(200, 200, 220, 255));

                int tipSize = 16;
                int tipWidth = FontManager.MeasureTextWidth(tip, tipSize);
                FontManager.DrawText(tip, (screenWidth - tipWidth) / 2f, screenHeight / 2f + 60f, tipSize, new Color(120, 120, 140, 255));

                Raylib.EndDrawing();
                return;
            }

            var mousePos = Raylib.GetMousePosition();

            float dt = Raylib.GetFrameTime();

            // Update runtime presentation centers.
            _gameState.NotificationCenter.Update(dt);
            UpdateNarration(dt);
            if (!_state.IsPresentingAction)
                _state.Spotlight = _gameState.SpotlightCenter.Current;
            UpdatePresentation(dt);

            bool inputBlocked = _state.ActiveRollResult != null || _state.IsPresentingAction
                || _state.ActiveActionSpotlight != null || _state.Spotlight != null;

            _windowStack.BeginFrame(mousePos, Raylib.IsMouseButtonPressed(MouseButton.Left));

            if (_state.IsDebugMenuOpen)
            {
                var (_, debugPanelRect) = GetDebugMenuRects();
                _windowStack.Register(new UiWindowBlocker
                {
                    Id = UiWindowId.DebugMenu,
                    Bounds = debugPanelRect,
                    Layer = UiLayer.Panel,
                    BlockMode = UiBlockMode.Bounds,
                    CloseOnClickedOutside = false,
                });
            }
            if (_state.IsGrowthPanelOpen)
            {
                _windowStack.Register(new UiWindowBlocker
                {
                    Id = UiWindowId.GrowthPanel,
                    Layer = UiLayer.Panel,
                    BlockMode = UiBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }
            if (_state.IsTurnPanelOpen)
            {
                _windowStack.Register(new UiWindowBlocker
                {
                    Id = UiWindowId.TurnPanel,
                    Layer = UiLayer.Panel,
                    BlockMode = UiBlockMode.Fullscreen,
                    CloseOnClickedOutside = false,
                });
            }

            _windowStack.Update();

            var lockedCtx = new UiInteractionContext { Mouse = mousePos, IsLocked = true };
            var worldUi = inputBlocked ? lockedCtx : _windowStack.MakeContext(UiLayer.World);
            var panelUi = inputBlocked ? lockedCtx : _windowStack.MakeContext(UiLayer.Panel);
            var ui = new UiInteractionContext { Mouse = mousePos, IsLocked = inputBlocked };

            // Handle Command+R to restart
            if (!inputBlocked && (Raylib.IsKeyDown(KeyboardKey.LeftSuper) || Raylib.IsKeyDown(KeyboardKey.RightSuper)) && Raylib.IsKeyPressed(KeyboardKey.R))
            {
                RestartApplication();
                return;
            }

            // Handle ESC key or right-click to clear selected card/resource first
            if (!inputBlocked)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsMouseButtonPressed(MouseButton.Right))
                {
                    if (_state.SelectedResource != null)
                    {
                        _state.SelectedResource = null;
                    }
                    else if (_state.IsTurnPanelOpen)
                    {
                        _state.IsTurnPanelOpen = false;
                    }
                    else if (_state.IsGrowthPanelOpen)
                    {
                        _state.IsGrowthPanelOpen = false;
                    }
                    else if (Raylib.IsKeyPressed(KeyboardKey.Escape))
                    {
                        GoBack();
                    }
                }
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 20, 25, 255));

            // 1. Draw Navigation / Breadcrumbs
            var navInteraction = NavigationWidget.Draw(_state, worldUi, WindowWidth);
            if (navInteraction.GoBackClicked)
            {
                GoBack();
            }

            // 2. Draw Node Clocks (if any)
            float cardsStartY = ClockWidget.Draw(_state, 90f, WindowWidth);

            // 3. Draw Node Cards
            DrawCards(worldUi, cardsStartY);

            // 4. Draw Hand Panel
            bool turnPanelWasOpen = _state.IsTurnPanelOpen;
            var handInteraction = HandPanelWidget.Draw(_state, worldUi, WindowWidth, WindowHeight, IsInEncounter);
            if (handInteraction.TurnClicked)
            {
                if (IsInEncounter)
                {
                    _state.ClearAllNodeSlots();
                    _state.SelectedResource = null;
                    _sceneManager.EndTurn();
                    StartPresentation(CreateEndTurnReport(), "休息");
                }
                else
                {
                    NavigateToHome();
                }
            }
            else if (handInteraction.ShouldClearSelection)
            {
                _state.SelectedResource = null;
            }
            else if (handInteraction.SelectedResourceToSet != null)
            {
                _state.SelectedResource = handInteraction.SelectedResourceToSet;
            }

            if (!inputBlocked && Raylib.IsMouseButtonReleased(MouseButton.Left) && _state.SelectedResource != null)
            {
                _state.SelectedResource = null;
            }

            // 5. Draw Bottom Status Bar
            StatusBarWidget.Draw(_state.DisplayedSnapshot, WindowWidth, WindowHeight);

            if (_state.IsTurnPanelOpen && IsInEncounter)
            {
                bool justOpened = !turnPanelWasOpen;
                var turnPanelInteraction = TurnPanelWidget.Draw(panelUi, WindowWidth, WindowHeight, justOpened);
                if (turnPanelInteraction.RestClicked)
                {
                    _state.IsTurnPanelOpen = false;
                    _state.ClearAllNodeSlots();
                    _state.SelectedResource = null;
                    _sceneManager.EndTurn();
                    StartPresentation(CreateEndTurnReport(), "休息");
                }
                else if (turnPanelInteraction.ShouldClose)
                {
                    _state.IsTurnPanelOpen = false;
                }
            }

            // 6. Draw Faction Relation Panel
            DrawRelationPanel(panelUi);

            // 7. Draw Debug Menu (save/load + scene switch)
            DrawDebugMenu(panelUi);

            // Draw Team / Growth Toggle Button
            float btnX = 520f;
            float btnY = 30f;
            float btnW = 80f;
            float btnH = 32f;
            var btnRect = new Rectangle(btnX, btnY, btnW, btnH);
            var toggleBtn = UiButton.Draw(btnRect, "成长/队伍", worldUi, true, 13,
                _state.IsGrowthPanelOpen ? new Color((byte)50, (byte)50, (byte)90, (byte)255) : new Color((byte)25, (byte)25, (byte)35, (byte)255),
                new Color((byte)40, (byte)40, (byte)55, (byte)255), null,
                _state.IsGrowthPanelOpen ? new Color((byte)130, (byte)130, (byte)220, (byte)255) : new Color((byte)50, (byte)50, (byte)70, (byte)255),
                Color.White, null, Color.White, null);

            if (toggleBtn.Clicked)
            {
                _state.IsGrowthPanelOpen = !_state.IsGrowthPanelOpen;
            }

            // Draw Growth Panel if open
            if (_state.IsGrowthPanelOpen)
            {
                DrawGrowthPanel(panelUi);
            }

            // Update active roll animation timer
            if (_state.ActiveRollResult != null)
            {
                _state.ActiveRollTime += Raylib.GetFrameTime();
                float elapsed = _state.ActiveRollTime;

                if (_state.ActiveRollPhase == 0)
                {
                    // Rolling phase: 0.3s (was 1.0s)
                    float t = Math.Clamp(elapsed / 0.3f, 0f, 1f);
                    float interval = 0.05f + (0.22f - 0.05f) * t;

                    var rand = new Random();
                    _state.ActiveRollDisplayDieValue = rand.Next(1, 7);
                    _state.ActiveRollDisplayScale = 0.9f + (float)rand.NextDouble() * 0.25f;

                    if (elapsed >= 0.3f)
                    {
                        _state.ActiveRollPhase = 1;
                        _state.ActiveRollTime = 0f; // Reset phase time
                        _state.ActiveRollDisplayDieValue = _state.ActiveRollResult.FinalRollValue;
                        _state.ActiveRollDisplayScale = 1f;
                    }
                }
                else if (_state.ActiveRollPhase == 1)
                {
                    // Reveal pulse phase: 0.15s (was 0.25s)
                    float t = Math.Clamp(elapsed / 0.15f, 0f, 1f);
                    _state.ActiveRollDisplayScale = 1f + (float)Math.Sin(t * Math.PI) * 0.35f;

                    if (elapsed >= 0.15f)
                    {
                        _state.ActiveRollPhase = 2;
                        _state.ActiveRollTime = 0f;
                        _state.ActiveRollDisplayScale = 1f;

                        if (!ActiveRollUsesModal())
                        {
                            _state.ActiveRollResult = null;
                            if (_state.IsPresentingAction)
                            {
                                AdvancePresentationAfterRollConfirm();
                            }
                        }
                    }
                }
            }

            DrawPresentationOverlay();

            // 8. Draw Overlays (Modals / Toasts)
            var overlayInteraction = OverlayWidget.Draw(_state, _gameState.NotificationCenter, ui, WindowWidth, WindowHeight);
            if (overlayInteraction.ConfirmClicked)
            {
                if (_state.ActiveOutcomeResult != null)
                {
                    ConfirmHeavyOutcome();
                }
                else if (_state.ActiveRollResult != null)
                {
                    _state.ActiveRollResult = null;
                    if (_state.IsPresentingAction)
                    {
                        AdvancePresentationAfterRollConfirm();
                    }
                }
            }
            if (overlayInteraction.SpotlightDismissClicked)
            {
                if (_state.ActiveActionSpotlight != null)
                    ConfirmActionSpotlight();
                else
                {
                    _gameState.SpotlightCenter.Dismiss();
                    _state.Spotlight = null;
                }
            }

            DrawNarrationOverlay();

            Raylib.EndDrawing();
        }

        private void DrawNarrationOverlay()
        {
            if (string.IsNullOrEmpty(_state.ActiveNarrationId))
                return;

            float fadeIn = Math.Clamp(_state.ActiveNarrationTime / 0.2f, 0f, 1f);
            float remaining = _state.ActiveNarrationDuration - _state.ActiveNarrationTime;
            float fadeOut = remaining < 0.5f ? Math.Clamp(remaining / 0.5f, 0f, 1f) : 1f;
            byte alpha = (byte)(220 * Math.Min(fadeIn, fadeOut));

            string text = $"[旁白] {_state.ActiveNarrationId}";
            int fontSize = 18;
            float maxW = WindowWidth - 120f;
            float textW = Math.Min(maxW - 48f, FontManager.MeasureTextWidth(text, fontSize));
            float boxW = Math.Max(260f, textW + 48f);
            float boxH = 40f;
            float x = (WindowWidth - boxW) / 2f;
            float y = WindowHeight - 154f;
            var rect = new Rectangle(x, y, boxW, boxH);

            Raylib.DrawRectangleRounded(rect, 0.08f, 8, new Color((byte)10, (byte)12, (byte)18, alpha));
            Raylib.DrawRectangleRoundedLinesEx(rect, 0.08f, 8, 1.2f, new Color((byte)100, (byte)120, (byte)170, alpha));
            FontManager.DrawText(text, x + 24f, y + 10f, fontSize, new Color((byte)220, (byte)226, (byte)245, alpha));
        }

        private static readonly Color DbgBg     = new Color((byte)20,  (byte)20,  (byte)28,  (byte)255);
        private static readonly Color DbgBorder = new Color((byte)60,  (byte)60,  (byte)90,  (byte)255);
        private static readonly Color DbgBtn    = new Color((byte)25,  (byte)25,  (byte)38,  (byte)255);
        private static readonly Color DbgBtnHov = new Color((byte)40,  (byte)40,  (byte)60,  (byte)255);
        private static readonly Color DbgAccent = new Color((byte)110, (byte)110, (byte)200, (byte)255);
        private static readonly Color DbgText   = new Color((byte)200, (byte)200, (byte)220, (byte)255);
        private static readonly Color DbgMuted  = new Color((byte)90,  (byte)90,  (byte)115, (byte)255);

        private (Rectangle ToggleRect, Rectangle PanelRect) GetDebugMenuRects()
        {
            float btnX = WindowWidth - 76f;
            float btnY = 30f;
            var toggleRect = new Rectangle(btnX, btnY, 66f, 32f);

            float pw = 260f;
            float px = btnX + 66f - pw;
            float py = btnY + 32f + 4f;
            float itemH = 26f;
            float slotsHeight = 20f + 3 * 28f + 14f;
            float panelH = 8f + slotsHeight + _state.DropdownItems.Count * itemH + 8f;
            var panelRect = new Rectangle(px, py, pw, panelH);

            return (toggleRect, panelRect);
        }

        private void DrawDebugMenu(UiInteractionContext ui)
        {
            // ── Toggle button ─────────────────────────────────────────────
            var (toggleRect, panelRect) = GetDebugMenuRects();
            float btnX = toggleRect.X;
            float btnY = toggleRect.Y;
            bool isOpen = _state.IsDebugMenuOpen;

            var togBg  = isOpen ? new Color((byte)45,(byte)45,(byte)80,(byte)255) : DbgBtn;
            var togBdr = isOpen ? DbgAccent : DbgBorder;
            bool hoverTog = ui.CanHover(toggleRect);
            Raylib.DrawRectangleRounded(toggleRect, 0.25f, 4, hoverTog ? DbgBtnHov : togBg);
            Raylib.DrawRectangleRoundedLinesEx(toggleRect, 0.25f, 4, 1.5f, togBdr);
            int lblW = FontManager.MeasureTextWidth("Debug v", 13);
            FontManager.DrawText("Debug v", btnX + (66 - lblW) / 2f, btnY + 9, 13, isOpen ? DbgAccent : DbgText);

            if (!ui.IsLocked && Raylib.IsMouseButtonPressed(MouseButton.Left) && hoverTog)
            {
                _state.IsDebugMenuOpen = !isOpen;
                if (_state.IsDebugMenuOpen)
                {
                    SceneDropdownWidget.LoadAvailableScenes(_state);
                }
            }

            if (!_state.IsDebugMenuOpen) return;

            // ── Panel ─────────────────────────────────────────────────────
            float pw = panelRect.Width;
            float px = panelRect.X;
            float py = panelRect.Y;
            var items = _state.DropdownItems;
            float itemH = 26f;

            Raylib.DrawRectangleRounded(panelRect, 0.15f, 4, DbgBg);
            Raylib.DrawRectangleRoundedLinesEx(panelRect, 0.15f, 4, 1.5f, DbgBorder);

            // Slots Section
            float curY = py + 8f;
            FontManager.DrawText("存档管理", px + 8, curY + 2f, 11, DbgMuted);
            curY += 20f;

            for (int slot = 1; slot <= 3; slot++)
            {
                string slotPath = SaveManager.GetSlotFilePath(slot);
                string saveTime = SaveManager.GetSaveTime(slotPath);
                bool hasSave = !string.IsNullOrEmpty(saveTime);

                FontManager.DrawText($"槽位 {slot}", px + 8, curY + 7f, 13, DbgText);

                string timeStr = hasSave ? saveTime : "（空）";
                Color timeColor = hasSave ? DbgText : DbgMuted;
                FontManager.DrawText(timeStr, px + 52f, curY + 7f, 12, timeColor);

                var rectSave = new Rectangle(px + pw - 8f - 64f, curY + 3f, 30f, 22f);
                var rectLoad = new Rectangle(px + pw - 8f - 30f, curY + 3f, 30f, 22f);

                var saveBtn = UiButton.Draw(rectSave, "存", ui, true, 12,
                    DbgBtn, DbgBtnHov, null, DbgBorder, Color.White, null, Color.White, null);
                var loadBtn = UiButton.Draw(rectLoad, "读", ui, hasSave, 12,
                    DbgBtn, DbgBtnHov, null, DbgBorder, Color.White, null, Color.White, null);

                if (saveBtn.Clicked)
                {
                    SaveCurrentGame(slotPath);
                }
                if (loadBtn.Clicked)
                {
                    LoadSavedGame(slotPath);
                    _state.IsDebugMenuOpen = false;
                    return;
                }

                curY += 28f;
            }

            // Separator + "切换场景" label
            float sepY = curY + 6f;
            Raylib.DrawLineEx(new Vector2(px + 8, sepY), new Vector2(px + pw - 8, sepY), 1f, DbgBorder);
            FontManager.DrawText("切换场景", px + 8, sepY + 4, 11, DbgMuted);

            // Scene list
            float listY = sepY + 4f + itemH * 0.5f;
            bool mouseClick = !ui.IsLocked && Raylib.IsMouseButtonPressed(MouseButton.Left);
            bool clickHandled = false;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float iy = listY + i * itemH;
                var itemRect = new Rectangle(px + 4, iy, pw - 8, itemH - 2);
                bool hover = Raylib.CheckCollisionPointRec(ui.Mouse, itemRect) && !ui.IsLocked;

                if (item.IsHeader)
                {
                    FontManager.DrawText(item.Name, px + 10, iy + 5, 11, DbgMuted);
                    continue;
                }

                bool isCurrent = string.Equals(item.SceneName, _sceneManager.CurrentSceneName, StringComparison.OrdinalIgnoreCase);
                if (hover)  Raylib.DrawRectangleRounded(itemRect, 0.15f, 4, DbgBtnHov);
                if (isCurrent) Raylib.DrawRectangle((int)px + 4, (int)iy + 2, 3, (int)itemH - 6, DbgAccent);

                FontManager.DrawText(item.Name, px + 12, iy + 5, 13,
                    isCurrent ? DbgAccent : (hover ? Color.White : DbgText));

                if (mouseClick && hover && !clickHandled)
                {
                    clickHandled = true;
                    _state.IsDebugMenuOpen = false;
                    _sceneManager.LoadScene(item.SceneName);
                    return;
                }
            }

            // Close when clicking outside
            if (mouseClick && !clickHandled
                && !Raylib.CheckCollisionPointRec(ui.Mouse, panelRect)
                && !Raylib.CheckCollisionPointRec(ui.Mouse, toggleRect))
            {
                _state.IsDebugMenuOpen = false;
            }
        }

        private void DrawCards(SSNoir.TerminalApp.Rendering.UiInteractionContext ui, float startY)
        {
            float startX = 40f;
            float cardWidth = 240f;
            float cardHeight = 150f;
            float spacing = 20f;
            int cardsPerRow = Math.Max(1, (int)((WindowWidth - startX * 2 + spacing) / (cardWidth + spacing)));
            var visibleNodes = _state.VisibleNodes.ToList();
            var visibleNames = new HashSet<string>(visibleNodes.Select(n => n.Name), StringComparer.OrdinalIgnoreCase);
            var orphanResidues = _state.CardResidues
                .Where(pair => !visibleNames.Contains(pair.Key))
                .Select(pair => pair.Value)
                .ToList();
            int totalCards = visibleNodes.Count + orphanResidues.Count;
            float viewportTop = startY;
            float viewportBottom = WindowHeight - 108f;
            float viewportHeight = Math.Max(0f, viewportBottom - viewportTop);
            var viewport = new Rectangle(0, viewportTop, WindowWidth, viewportHeight);
            int rowCount = totalCards == 0
                ? 0
                : (totalCards + cardsPerRow - 1) / cardsPerRow;
            float contentHeight = rowCount == 0 ? 0f : rowCount * cardHeight + Math.Max(0, rowCount - 1) * spacing;
            float maxScroll = Math.Max(0f, contentHeight - viewportHeight);

            bool mouseInViewport = ui.CanHover(viewport);
            float wheel = Raylib.GetMouseWheelMove();
            if (mouseInViewport && Math.Abs(wheel) > 0.001f && maxScroll > 0f)
            {
                _state.CardsScrollOffset -= wheel * 48f;
            }
            _state.CardsScrollOffset = Math.Clamp(_state.CardsScrollOffset, 0f, maxScroll);

            Raylib.BeginScissorMode(
                (int)viewport.X,
                (int)viewport.Y,
                (int)viewport.Width,
                (int)viewport.Height);

            for (int i = 0; i < totalCards; i++)
            {
                int row = i / cardsPerRow;
                int col = i % cardsPerRow;

                float x = startX + col * (cardWidth + spacing);
                float y = startY + row * (cardHeight + spacing) - _state.CardsScrollOffset;

                if (y > viewportBottom || y + cardHeight < viewportTop)
                {
                    continue;
                }

                var bounds = new Rectangle(x, y, cardWidth, cardHeight);
                bool isHovered = ui.CanHover(bounds);

                if (i >= visibleNodes.Count)
                {
                    CardWidget.DrawResidueCard(bounds, orphanResidues[i - visibleNodes.Count]);
                    continue;
                }

                var node = visibleNodes[i];

                if (node.Resolve?.Type == ResolveType.Clock)
                {
                    CardWidget.DrawClockCard(bounds, node.Name, node.Subtitle, node.Resolve.Clock);
                    continue;
                }

                string typeLabel = "容器";
                if (node.Resolve != null)
                {
                    if (node.Resolve.Type == ResolveType.Instant) typeLabel = "行动";
                    else if (node.Resolve.Type == ResolveType.Roll) typeLabel = "判定";
                    else if (node.Resolve.Type == ResolveType.Observe) typeLabel = "观察";
                }

                bool isFlipped = _state.FlippedNodes.Contains(node.Name);
                string backText = (node.Resolve?.Type == ResolveType.Observe) ? node.Resolve.ObserveText : "";

                List<ActionCost>? requires = null;
                List<SlottedResource?>? slotted = null;

                if (node.Requires != null && node.Requires.Count > 0)
                {
                    requires = node.Requires;
                    if (!_state.NodeSlots.TryGetValue(node.Name, out slotted))
                    {
                        slotted = new List<SlottedResource?>();
                        for (int j = 0; j < node.Requires.Count; j++)
                        {
                            slotted.Add(null);
                        }
                        _state.NodeSlots[node.Name] = slotted;
                    }
                }
                var canDropHeldResource = BuildDropStates(requires, slotted);

                List<DifficultyModifierInfo>? modifiers = node.Resolve?.DifficultyModifiers.Count > 0 ? node.Resolve.DifficultyModifiers : null;
                var execution = GetCardExecutionState(node.Name);
                bool isActiveRollCard = _state.ActiveRollResult != null
                    && !ActiveRollUsesModal()
                    && string.Equals(_state.ActiveRollActionName, node.Name, StringComparison.OrdinalIgnoreCase);
                _state.CardResidues.TryGetValue(node.Name, out var residue);
                var interaction = CardWidget.DrawCard(
                    bounds,
                    node.Name,
                    node.Subtitle,
                    typeLabel,
                    isHovered,
                    node.Clocks,
                    isFlipped,
                    backText,
                    node.Tags,
                    requires,
                    slotted,
                    ui,
                    _state.SelectedResource,
                    canDropHeldResource,
                    modifiers,
                    execution.IsExecuting,
                    execution.Progress,
                    execution.Text,
                    isActiveRollCard ? _state.ActiveRollResult : null,
                    _state.ActiveRollPhase,
                    _state.ActiveRollDisplayDieValue,
                    _state.ActiveRollDisplayScale,
                    residue,
                    node.Disabled,
                    node.Resolve?.Type == ResolveType.Roll ? node.Resolve.SkillName : null,
                    _state.DisplayedSnapshot.Actors);

                if (interaction.CardClicked)
                {
                    if (node.IsContainer)
                    {
                        _state.ClearAllNodeSlots();
                        _state.CardsScrollStack.Add(_state.CardsScrollOffset);
                        _state.CardsScrollOffset = 0f;
                        _state.NavigationStack.Add(node);
                        ResolveNavigationStack();
                    }
                    else if (node.Resolve != null)
                    {
                        if (node.Resolve.Type == ResolveType.Observe)
                        {
                            _state.CardResidues.Clear();
                            if (isFlipped)
                            {
                                _state.FlippedNodes.Remove(node.Name);
                            }
                            else
                            {
                                _state.FlippedNodes.Add(node.Name);
                            }
                        }
                        else if (requires == null)
                        {
                            ExecuteNodeAction(node, new List<SlottedResource?>());
                        }
                    }
                }

                if (interaction.ClickedSlotIndex != -1 && slotted != null && requires != null)
                {
                    int j = interaction.ClickedSlotIndex;
                    var res = slotted[j];

                    if (_state.SelectedResource == null && res != null)
                    {
                        _state.CardResidues.Clear();
                        _state.SelectedResource = CreateSelectedResourceFromSlot(res);
                        slotted[j] = null;
                    }
                    else if (_state.SelectedResource != null)
                    {
                        // Places into an empty slot, or replaces a filled one.
                        TryPlaceSelectedResource(node, requires, slotted, j);
                    }
                }

                if (interaction.DroppedSlotIndex != -1 && slotted != null && requires != null)
                {
                    TryPlaceSelectedResource(node, requires, slotted, interaction.DroppedSlotIndex);
                }

                if (interaction.ExecuteClicked)
                {
                    if (slotted != null)
                    {
                        ExecuteSlottedAction(node, slotted);
                    }
                    else
                    {
                        ExecuteNodeAction(node, new List<SlottedResource?>());
                    }
                }
            }

            Raylib.EndScissorMode();

            DrawCardsScrollbar(viewport, contentHeight, _state.CardsScrollOffset, maxScroll);
        }

        private static void DrawCardsScrollbar(Rectangle viewport, float contentHeight, float scrollOffset, float maxScroll)
        {
            if (contentHeight <= viewport.Height || viewport.Height <= 0f)
            {
                return;
            }

            float trackW = 5f;
            float trackX = viewport.X + viewport.Width - trackW - 8f;
            var track = new Rectangle(trackX, viewport.Y + 4f, trackW, viewport.Height - 8f);
            float thumbH = Math.Max(28f, track.Height * (viewport.Height / contentHeight));
            float travel = Math.Max(0f, track.Height - thumbH);
            float thumbY = track.Y + (maxScroll <= 0f ? 0f : travel * (scrollOffset / maxScroll));
            var thumb = new Rectangle(track.X, thumbY, track.Width, thumbH);

            Raylib.DrawRectangleRounded(track, 0.6f, 4, new Color(30, 30, 42, 180));
            Raylib.DrawRectangleRounded(thumb, 0.6f, 4, new Color(105, 115, 155, 210));
        }

        private List<bool>? BuildDropStates(List<ActionCost>? requires, List<SlottedResource?>? slotted)
        {
            if (_state.SelectedResource == null || requires == null || slotted == null)
            {
                return null;
            }

            var result = new List<bool>(requires.Count);
            for (int i = 0; i < requires.Count; i++)
            {
                // A filled slot is a valid drop target too — dropping replaces it, and the
                // displaced die returns to hand automatically (slot state is derived).
                result.Add(ResourceSlotRules.CanPlaceSelectedResource(_state, requires[i], slotted, i));
            }
            return result;
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
                Qty = resource.Qty > 0 ? resource.Qty : resource.Value
            };
        }

        private bool TryPlaceSelectedResource(GameNode node, List<ActionCost> requires, List<SlottedResource?> slotted, int slotIndex)
        {
            if (_state.SelectedResource == null)
                return false;

            var req = requires[slotIndex];

            if (!ResourceSlotRules.CanPlaceSelectedResource(_state, req, slotted, slotIndex))
            {
                if (ResourceSlotRules.CanMatchRequirement(req, _state.SelectedResource))
                {
                    _gameState.NotificationCenter.Push($"缺少数量，需要 {req.Qty} 个 {_state.SelectedResource.ItemName}", NotificationKind.Warning);
                }
                return false;
            }

            if (req.Type == "die" && _state.SelectedResource.Type == "die")
            {
                _state.CardResidues.Clear();
                _state.ClearOtherNodeSlots(node.Name);
                slotted[slotIndex] = new SlottedResource
                {
                    Type = "die",
                    Value = _state.SelectedResource.Value,
                    SourceIndex = _state.SelectedResource.SourceIndex,
                    ActorId = _state.SelectedResource.ActorId,
                    DieIndex = _state.SelectedResource.DieIndex
                };
                _state.SelectedResource = null;
                return true;
            }

            if (req.Type == "item" && _state.SelectedResource.Type == "item")
            {
                if (req.ItemId.Equals(_state.SelectedResource.ItemName, StringComparison.OrdinalIgnoreCase))
                {
                    _state.CardResidues.Clear();
                    _state.ClearOtherNodeSlots(node.Name);
                    slotted[slotIndex] = new SlottedResource
                    {
                        Type = "item",
                        ItemId = req.ItemId,
                        Value = req.Qty,
                        Qty = req.Qty
                    };
                    _state.SelectedResource = null;
                    return true;
                }
            }

            return false;
        }

        private void ExecuteSlottedAction(GameNode node, List<SlottedResource?> slotted)
        {
            ExecuteNodeAction(node, slotted);
        }

        // 关系档位配色（序号 0..4 对应 RelationScale.BandNames：敌视/冷淡/中立/脸熟/自己人）。
        private static readonly Color[] RelationBandColors =
        {
            new Color((byte)190, (byte)70,  (byte)70,  (byte)255), // 敌视
            new Color((byte)200, (byte)140, (byte)60,  (byte)255), // 冷淡
            new Color((byte)110, (byte)112, (byte)130, (byte)255), // 中立
            new Color((byte)70,  (byte)150, (byte)165, (byte)255), // 脸熟
            new Color((byte)80,  (byte)185, (byte)115, (byte)255), // 自己人
        };

        private bool _relationExpanded;

        // 默认只显示三派关系数值；点击后展开成完整进度条。
        private void DrawRelationPanel(UiInteractionContext ui)
        {
            var snapshot = _state.DisplayedSnapshot;
            string[] factions = { "官僚", "劳工", "富商" };

            float panelX = 275f, panelY = 26f;
            float pad = 6f;

            if (_relationExpanded)
            {
                float rowH = 18f, labelW = 32f, valueW = 20f, gap = 6f;
                float panelW = 225f;
                float panelH = 3 * rowH + pad * 2;

                var panelRect = new Rectangle(panelX, panelY, panelW, panelH);
                bool hovered = ui.CanHover(panelRect);

                Raylib.DrawRectangleRounded(panelRect, 0.15f, 4, new Color((byte)20, (byte)22, (byte)30, (byte)220));
                var borderColor = hovered
                    ? new Color((byte)90, (byte)120, (byte)180, (byte)255)
                    : new Color((byte)55, (byte)60, (byte)80, (byte)255);
                Raylib.DrawRectangleRoundedLinesEx(panelRect, 0.15f, 4, 1.0f, borderColor);

                if (ui.WasClicked(panelRect))
                {
                    _relationExpanded = false;
                }

                float barX = panelX + pad + labelW + gap;
                float barW = panelW - pad * 2 - labelW - valueW - gap * 2;

                var b = RelationScale.Boundaries;
                int[] edges = new int[b.Length + 2];
                edges[0] = RelationScale.Min;
                for (int k = 0; k < b.Length; k++) edges[k + 1] = b[k];
                edges[edges.Length - 1] = RelationScale.Max;

                for (int i = 0; i < factions.Length; i++)
                {
                    int value = snapshot.Relations.TryGetValue(factions[i], out var v) ? v : 0;
                    int bi = RelationScale.BandIndex(value);
                    float rowY = panelY + pad + i * rowH;
                    float textY = rowY + 2f;
                    float barY = rowY + 6f;
                    float barH = 5f;

                    FontManager.DrawText(factions[i], panelX + pad, textY, 12, new Color((byte)170, (byte)175, (byte)195, (byte)255));

                    var outlineRect = new Rectangle(barX, barY, barW, barH);
                    Raylib.DrawRectangleRoundedLinesEx(outlineRect, 0.5f, 4, 1.0f, new Color((byte)50, (byte)53, (byte)70, (byte)255));

                    float mx = barX + RelationScale.Fraction(value) * barW;

                    for (int s = 0; s < edges.Length - 1; s++)
                    {
                        float x0 = barX + RelationScale.Fraction(edges[s]) * barW;
                        float x1 = barX + RelationScale.Fraction(edges[s + 1]) * barW;
                        var c = RelationBandColors[s];

                        if (s < bi)
                        {
                            var col = new Color(c.R, c.G, c.B, (byte)35);
                            Raylib.DrawRectangle((int)x0, (int)barY, (int)Math.Max(1f, x1 - x0), (int)barH, col);
                        }
                        else if (s == bi)
                        {
                            if (mx > x0)
                            {
                                var activeCol = new Color(c.R, c.G, c.B, (byte)75);
                                Raylib.DrawRectangle((int)x0, (int)barY, (int)Math.Max(1f, mx - x0), (int)barH, activeCol);
                            }
                            if (x1 > mx)
                            {
                                var inactiveCol = new Color(c.R, c.G, c.B, (byte)12);
                                Raylib.DrawRectangle((int)mx, (int)barY, (int)Math.Max(1f, x1 - mx), (int)barH, inactiveCol);
                            }
                        }
                        else
                        {
                            var col = new Color(c.R, c.G, c.B, (byte)12);
                            Raylib.DrawRectangle((int)x0, (int)barY, (int)Math.Max(1f, x1 - x0), (int)barH, col);
                        }
                    }

                    for (int s = 1; s < edges.Length - 1; s++)
                    {
                        float segX = barX + RelationScale.Fraction(edges[s]) * barW;
                        Raylib.DrawLineEx(
                            new System.Numerics.Vector2(segX, barY - 1f),
                            new System.Numerics.Vector2(segX, barY + barH + 1f),
                            1.0f,
                            new Color((byte)55, (byte)58, (byte)75, (byte)255)
                        );
                    }

                    var activeColor = RelationBandColors[bi];
                    Raylib.DrawCircle((int)mx, (int)(barY + barH / 2f), 3.5f, activeColor);
                    Raylib.DrawCircleLines((int)mx, (int)(barY + barH / 2f), 4.5f, new Color(255, 255, 255, 180));

                    string vs = value.ToString();
                    int vw = FontManager.MeasureTextWidth(vs, 12);
                    FontManager.DrawText(vs, barX + barW + gap + (valueW - vw) / 2f, textY, 12, RelationBandColors[bi]);
                }
            }
            else
            {
                float panelW = 180f;
                float panelH = 28f;

                var panelRect = new Rectangle(panelX, panelY, panelW, panelH);
                bool hovered = ui.CanHover(panelRect);

                Raylib.DrawRectangleRounded(panelRect, 0.15f, 4, new Color((byte)20, (byte)22, (byte)30, (byte)220));
                var borderColor = hovered
                    ? new Color((byte)90, (byte)120, (byte)180, (byte)255)
                    : new Color((byte)55, (byte)60, (byte)80, (byte)255);
                Raylib.DrawRectangleRoundedLinesEx(panelRect, 0.15f, 4, 1.0f, borderColor);

                if (ui.WasClicked(panelRect))
                {
                    _relationExpanded = true;
                }

                float usableW = panelW - pad * 2;
                float itemW = usableW / factions.Length;
                float textY = panelY + 7f;

                for (int i = 0; i < factions.Length; i++)
                {
                    int value = snapshot.Relations.TryGetValue(factions[i], out var v) ? v : 0;
                    int bi = RelationScale.BandIndex(value);
                    float itemX = panelX + pad + i * itemW;

                    FontManager.DrawText(factions[i], itemX, textY, 12, new Color((byte)170, (byte)175, (byte)195, (byte)255));

                    string vs = value.ToString();
                    int vw = FontManager.MeasureTextWidth(vs, 12);
                    FontManager.DrawText(vs, itemX + itemW - vw, textY, 12, RelationBandColors[bi]);
                }
            }
        }

        private void DrawGrowthPanel(SSNoir.TerminalApp.Rendering.UiInteractionContext ui)
        {
            // Dim background (modal overlay overlaying cards/hand)
            Raylib.DrawRectangle(0, 0, WindowWidth, WindowHeight, new Color((byte)10, (byte)10, (byte)15, (byte)180));

            float panelW = 540f;
            float panelH = 350f;
            float panelX = (WindowWidth - panelW) / 2f;
            float panelY = (WindowHeight - panelH) / 2f - 20f;
            var panelRect = new Rectangle(panelX, panelY, panelW, panelH);

            // Frame
            Raylib.DrawRectangleRounded(panelRect, 0.15f, 4, new Color((byte)20, (byte)20, (byte)28, (byte)255));
            Raylib.DrawRectangleRoundedLinesEx(panelRect, 0.15f, 4, 2f, new Color((byte)70, (byte)70, (byte)95, (byte)255));

            // Title
            FontManager.DrawText("成长 / 队伍", panelX + 25, panelY + 20, 18, Color.White);

            // Close Button [X]
            float closeX = panelX + panelW - 40f;
            float closeY = panelY + 18f;
            var closeRect = new Rectangle(closeX, closeY, 24, 24);
            bool hoverClose = ui.CanHover(closeRect);
            Color closeColor = hoverClose ? Color.Red : new Color((byte)180, (byte)180, (byte)200, (byte)255);
            FontManager.DrawText("X", closeX + 6, closeY + 3, 16, closeColor);

            if (ui.WasClicked(closeRect))
            {
                _state.IsGrowthPanelOpen = false;
            }

            // Divider line
            Raylib.DrawLineEx(new System.Numerics.Vector2(panelX + 20, panelY + 52),
                             new System.Numerics.Vector2(panelX + panelW - 20, panelY + 52),
                             1f, new Color((byte)55, (byte)55, (byte)70, (byte)255));

            // Team Growth Level
            FontManager.DrawText($"队伍成长等级：{_state.DisplayedSnapshot.GrowthLevel}", panelX + 25, panelY + 65, 14, new Color((byte)150, (byte)220, (byte)255, (byte)255));

            var actors = _state.DisplayedSnapshot.Actors;
            float contentStartY = panelY + 95f;
            float colWidth = (panelW - 40f) / Math.Max(1, actors.Count);

            var statsToUpgrade = new[] {
                (Key: "violence", Display: "力量"),
                (Key: "knowledge", Display: "见识"),
                (Key: "sharpness", Display: "敏锐"),
                (Key: "social", Display: "交际")
            };

            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                float colX = panelX + 20f + i * colWidth;

                // Draw vertical separator between columns (except first)
                if (i > 0)
                {
                    Raylib.DrawLineEx(new System.Numerics.Vector2(colX, contentStartY),
                                     new System.Numerics.Vector2(colX, panelY + panelH - 25f),
                                     1f, new Color((byte)45, (byte)45, (byte)60, (byte)255));
                }

                // Actor Name
                Color nameColor = actor.Status == "away" ? new Color((byte)130, (byte)130, (byte)130, (byte)255) : Color.White;
                FontManager.DrawText(actor.Name, colX + 15, contentStartY + 5, 15, nameColor);

                // Status label if away
                if (actor.Status == "away")
                {
                    FontManager.DrawText("[暂离]", colX + 15 + FontManager.MeasureTextWidth(actor.Name, 15) + 6, contentStartY + 7, 11, new Color((byte)230, (byte)80, (byte)80, (byte)255));
                }

                // Available points
                int availPoints = _state.GetAvailableGrowthPoints(actor);
                Color pointsColor = availPoints > 0 ? new Color((byte)100, (byte)230, (byte)120, (byte)255) : new Color((byte)170, (byte)170, (byte)180, (byte)255);
                FontManager.DrawText($"可用成长点：{availPoints}", colX + 15, contentStartY + 28, 12, pointsColor);

                // Stats rows
                float rowStartY = contentStartY + 55f;
                float rowHeight = 32f;

                for (int s = 0; s < statsToUpgrade.Length; s++)
                {
                    var stat = statsToUpgrade[s];
                    float rowY = rowStartY + s * rowHeight;

                    // Get stat value
                    int statVal = actor.Stats.TryGetValue(stat.Key, out var val) ? val : 1;

                    // Stat text
                    FontManager.DrawText($"{stat.Display} {statVal}", colX + 15, rowY + 3, 14, new Color((byte)210, (byte)210, (byte)225, (byte)255));

                    // Upgrade Button [+]
                    float btnW = 26f;
                    float btnH = 20f;
                    float btnX = colX + colWidth - btnW - 20f;
                    var btnRect = new Rectangle(btnX, rowY, btnW, btnH);

                    bool isEnabled = actor.Status != "away" && availPoints > 0 && statVal < 6;
                    
                    var upgradeBtn = UiButton.Draw(btnRect, "+", ui, isEnabled, 13,
                        new Color((byte)30, (byte)90, (byte)45, (byte)255),
                        new Color((byte)50, (byte)140, (byte)70, (byte)255),
                        new Color((byte)30, (byte)30, (byte)35, (byte)255),
                        new Color((byte)100, (byte)210, (byte)120, (byte)255),
                        Color.White,
                        new Color((byte)50, (byte)50, (byte)55, (byte)255),
                        Color.White,
                        new Color((byte)90, (byte)90, (byte)100, (byte)255));

                    if (upgradeBtn.Clicked)
                    {
                        try
                        {
                            _gameState.Team.UpgradeActorStat(actor.Id, stat.Key);
                            _sceneManager.RebuildRenderTree();
                            AdoptLatestSnapshot();
                            _gameState.NotificationCenter.Push($"{actor.Name} 升级了 {stat.Display} 属性！", NotificationKind.Success);
                        }
                        catch (System.Exception ex)
                        {
                            _gameState.NotificationCenter.Push($"升级失败: {ex.Message}", NotificationKind.Error);
                        }
                    }
                }
            }
        }

        private void RestartApplication()
        {
            try
            {
                Raylib.CloseWindow();

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/zsh",
                    Arguments = "-c \"./run\"",
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Restart failed: {ex.Message}");
            }
            finally
            {
                Environment.Exit(0);
            }
        }
    }
}
