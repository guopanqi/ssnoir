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
        // Font files follow: <family>-Regular.ttf / <family>-SemiBold.ttf.
        private const string FontFamily = 
        // "SourceHanSerifCN";
        "MiSans";

        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly RendererState _state;
        private readonly UiWindowStack _windowStack = new();
        private Action? _presentationDoneCallback;
        private StartupScreen _startupScreen = StartupScreen.MainMenu;
        private string _startupError = string.Empty;
        private bool _exitRequested;

        private enum StartupScreen
        {
            MainMenu,
            LoadSlots,
            InGame,
        }

        private const int WindowWidth = 900;
        private const int WindowHeight = 800;
        private static readonly bool FastPresentationMode =
            string.Equals(Environment.GetEnvironmentVariable("SSNOIR_FAST_PRESENTATION"), "1", StringComparison.Ordinal);

        public RaylibRenderer(SceneManager sceneManager, GameState gameState)
        {
            _sceneManager = sceneManager;
            _gameState = gameState;
            _state = new RendererState();

            SceneDropdownWidget.LoadAvailableScenes(_state);

            _gameState.NarrationCenter.OnNarrationRequested += ShowNarration;
            _gameState.DialogueCenter.OnDialogueRequested += EnqueueImmediateDialogue;

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
            _state.IsRelationExpanded = false;
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

        // 动作外 dialogue 不会进入 ActionReport；Terminal 逐句复用 Spotlight 作为明确的阻塞兜底。
        private void EnqueueImmediateDialogue(DialogueSequence sequence)
        {
            foreach (var line in sequence.Lines)
            {
                _state.PendingImmediateDialogueSpotlights.Enqueue(new SpotlightCard
                {
                    Title = line.Speaker,
                    Subtitle = line.Text,
                });
            }
        }

        private void UpdateImmediateDialogue()
        {
            if (_state.ActiveImmediateDialogueSpotlight != null
                || _state.PendingImmediateDialogueSpotlights.Count == 0
                || _state.IsPresentingAction
                || _state.ActiveRollResult != null
                || _state.ActiveOutcomeResult != null
                || _state.ActiveActionSpotlight != null)
            {
                return;
            }

            _state.ActiveImmediateDialogueSpotlight = _state.PendingImmediateDialogueSpotlights.Dequeue();
        }

        private void ConfirmImmediateDialogue()
        {
            _state.ActiveImmediateDialogueSpotlight = null;
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
                FateDieValue = hasRollResult ? report!.FateDieValue : null,
                PreparedValue = hasRollResult ? report!.PreparedValue : 0,
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

        private bool LoadSavedGame(string? filePath = null)
        {
            var path = filePath ?? SaveManager.DefaultSavePath;
            if (!System.IO.File.Exists(path))
            {
                _gameState.NotificationCenter.Push("没有找到存档文件。", NotificationKind.Warning);
                return false;
            }

            try
            {
                _sceneManager.LoadGame(path);
                _state.DisplayedSnapshot = _sceneManager.LatestSnapshot;
                _state.SelectedResource = null;
                _gameState.NotificationCenter.Push("游戏已读档。", NotificationKind.Success);
                return true;
            }
            catch (Exception ex)
            {
                _gameState.NotificationCenter.Push($"读档失败: {ex.Message}", NotificationKind.Error);
                return false;
            }
        }

        private void StartNewGame()
        {
            _sceneManager.LoadScene("world");
            var opening = FindNodeByName(_state.DisplayedSnapshot.RootNode, "雨夜来客")
                ?? throw new InvalidOperationException("新游戏缺少自动开场动作“雨夜来客”。");

            _startupScreen = StartupScreen.InGame;
            ExecuteNodeAction(opening, new List<SlottedResource?>());
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

            FontManager.LoadFont($"assets/fonts/{FontFamily}-Regular.ttf", 48);

            while (!Raylib.WindowShouldClose() && !_exitRequested)
            {
                UpdateAndDraw();
            }

            FontManager.UnloadFont();
            Raylib.CloseWindow();
        }

        private void UpdateAndDraw()
        {
            if (_startupScreen != StartupScreen.InGame)
            {
                DrawStartupScreen();
                return;
            }

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
            UpdateImmediateDialogue();
            if (!_state.IsPresentingAction)
                _state.Spotlight = _gameState.SpotlightCenter.Current;
            UpdatePresentation(dt);

            bool inputBlocked = _state.ActiveRollResult != null || _state.IsPresentingAction
                || _state.ActiveActionSpotlight != null || _state.ActiveImmediateDialogueSpotlight != null || _state.Spotlight != null;

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
            // 两个常驻面板展开后，顶部开关与其内容处在同一交互层，仍可用于收起；
            // 其他全屏面板（例如回合面板）仍应屏蔽这些开关。
            var topControlsUi = _state.IsGrowthPanelOpen || _state.IsRelationExpanded
                ? panelUi
                : worldUi;
            if (_state.IsRelationExpanded)
                worldUi = lockedCtx;
            var ui = new UiInteractionContext { Mouse = mousePos, IsLocked = inputBlocked };

            // Handle Command+R to restart
            if (!inputBlocked && (Raylib.IsKeyDown(KeyboardKey.LeftSuper) || Raylib.IsKeyDown(KeyboardKey.RightSuper)) && Raylib.IsKeyPressed(KeyboardKey.R))
            {
                RestartApplication();
                return;
            }

            // ESC 统一关闭阻塞性结算与剧情层；轻型卡片附件仍按自身的短暂停留自动结束。
            if (Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                if (_state.ActiveOutcomeResult != null)
                {
                    ConfirmHeavyOutcome();
                }
                else if (_state.ActiveRollResult != null && ActiveRollUsesModal() && _state.ActiveRollPhase >= 2)
                {
                    _state.ActiveRollResult = null;
                    if (_state.IsPresentingAction)
                    {
                        AdvancePresentationAfterRollConfirm();
                    }
                }
                else if (_state.ActiveImmediateDialogueSpotlight != null)
                {
                    ConfirmImmediateDialogue();
                }
                else if (_state.ActiveActionSpotlight != null)
                {
                    ConfirmActionSpotlight();
                }
                else if (_state.Spotlight != null)
                {
                    _gameState.SpotlightCenter.Dismiss();
                    _state.Spotlight = null;
                }
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
                    else if (_state.IsRelationExpanded)
                    {
                        _state.IsRelationExpanded = false;
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
                // 返回会在同一帧重绘上一级的容器卡片。消费这次导航点击，
                // 避免它继续被新出现的卡片当作一次进入容器的点击。
                worldUi = new UiInteractionContext
                {
                    Mouse = mousePos,
                    IsSuppressed = true,
                };
            }

            // 2. Draw Node Clocks (if any)
            float cardsStartY = ClockWidget.Draw(_state, 66f, WindowWidth);

            // 3. Draw Node Cards
            DrawCards(worldUi, cardsStartY);

            // 4. Draw Hand Panel
            bool turnPanelWasOpen = _state.IsTurnPanelOpen;
            var handInteraction = HandPanelWidget.Draw(_state, worldUi, WindowWidth, WindowHeight, IsInEncounter);
            if (handInteraction.SmokeClicked)
            {
                _state.ClearAllNodeSlots();
                _state.SelectedResource = null;
                StartPresentation(_sceneManager.UseEncounterConsumable("香烟"), "抽烟");
            }
            else if (handInteraction.DrinkClicked)
            {
                _state.ClearAllNodeSlots();
                _state.SelectedResource = null;
                StartPresentation(_sceneManager.UseEncounterConsumable("酒"), "喝酒");
            }
            else if (handInteraction.TurnClicked)
            {
                if (IsInEncounter)
                {
                    _state.ClearAllNodeSlots();
                    _state.SelectedResource = null;
                    StartPresentation(_sceneManager.EndTurn(), "休息");
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

            if (_state.IsTurnPanelOpen && IsInEncounter)
            {
                bool justOpened = !turnPanelWasOpen;
                var turnPanelInteraction = TurnPanelWidget.Draw(panelUi, WindowWidth, WindowHeight, justOpened);
                if (turnPanelInteraction.RestClicked)
                {
                    _state.IsTurnPanelOpen = false;
                    _state.ClearAllNodeSlots();
                    _state.SelectedResource = null;
                    StartPresentation(_sceneManager.EndTurn(), "休息");
                }
                else if (turnPanelInteraction.ShouldClose)
                {
                    _state.IsTurnPanelOpen = false;
                }
            }

            // 6. Draw top-right controls: Relation, Growth/Team, Debug
            DrawTopRightControls(topControlsUi, panelUi);

            // 7. Draw Growth Panel if open
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
                    // 掷骰扫掠：命运高亮沿命运条减速滑动（easeOutCubic），精确落在最终骰面。
                    // 命运条本身就是动画——不再是一个方框里跳变的随机数字。
                    const float sweepDuration = 0.5f;
                    int finalFace = _state.ActiveRollResult.FateDieValue;
                    float t = Math.Clamp(elapsed / sweepDuration, 0f, 1f);
                    float eased = 1f - (float)Math.Pow(1f - t, 3f);
                    int totalSteps = 12 + (finalFace - 1); // 两圈扫掠后落格
                    int step = (int)Math.Round(eased * totalSteps);
                    _state.ActiveRollDisplayDieValue = step % 6 + 1;
                    _state.ActiveRollDisplayScale = 1f;

                    if (elapsed >= sweepDuration)
                    {
                        _state.ActiveRollPhase = 1;
                        _state.ActiveRollTime = 0f;
                        _state.ActiveRollDisplayDieValue = finalFace;
                        _state.ActiveRollDisplayScale = 1f;
                    }
                }
                else if (_state.ActiveRollPhase == 1)
                {
                    // 落格弹跳：命中格放大一下再收（sin 单峰）。
                    const float popDuration = 0.2f;
                    float t = Math.Clamp(elapsed / popDuration, 0f, 1f);
                    _state.ActiveRollDisplayScale = 1f + (float)Math.Sin(t * Math.PI) * 0.35f;

                    if (elapsed >= popDuration)
                    {
                        _state.ActiveRollPhase = 2;
                        _state.ActiveRollTime = 0f;
                        _state.ActiveRollDisplayScale = 1f;
                    }
                }
                else if (_state.ActiveRollPhase == 2 && !ActiveRollUsesModal())
                {
                    // 结果停留：轻结算让「好/中/坏」定格片刻再推进（重结算走模态、等点击）。
                    if (elapsed >= 0.45f)
                    {
                        _state.ActiveRollResult = null;
                        if (_state.IsPresentingAction)
                        {
                            AdvancePresentationAfterRollConfirm();
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
                if (_state.ActiveImmediateDialogueSpotlight != null)
                {
                    ConfirmImmediateDialogue();
                }
                else if (_state.ActiveActionSpotlight != null)
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

        private void DrawStartupScreen()
        {
            var mouse = Raylib.GetMousePosition();
            var ui = new UiInteractionContext { Mouse = mouse };
            float width = Raylib.GetScreenWidth();
            float height = Raylib.GetScreenHeight();
            float panelWidth = Math.Min(460f, width - 80f);
            float panelX = (width - panelWidth) / 2f;
            float panelY = Math.Max(72f, (height - 520f) / 2f);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(14, 14, 20, 255));

            FontManager.DrawText("SSNOIR", panelX, panelY, 44, TerminalPalette.AccentBright);
            FontManager.DrawText("雨夜，旧账与一座不肯睡去的城", panelX + 2f, panelY + 58f, 16, TerminalPalette.TextMuted);
            Raylib.DrawLineEx(
                new Vector2(panelX, panelY + 94f),
                new Vector2(panelX + panelWidth, panelY + 94f),
                1f,
                TerminalPalette.Border);

            if (_startupScreen == StartupScreen.MainMenu)
            {
                float buttonY = panelY + 132f;
                var newGame = UiButton.Draw(new Rectangle(panelX, buttonY, panelWidth, 54f), "新游戏", ui, true, 18,
                    TerminalPalette.AccentDark, new Color(58, 58, 104, 255), null,
                    TerminalPalette.Accent, TerminalPalette.AccentBright, null, TerminalPalette.Text, null);
                var loadGame = UiButton.Draw(new Rectangle(panelX, buttonY + 70f, panelWidth, 54f), "从存档启动", ui, true, 18);
                var exit = UiButton.Draw(new Rectangle(panelX, buttonY + 140f, panelWidth, 54f), "退出", ui, true, 18);

                if (newGame.Clicked)
                {
                    try
                    {
                        _startupError = string.Empty;
                        StartNewGame();
                    }
                    catch (Exception ex)
                    {
                        _startupError = $"新游戏启动失败：{ex.Message}";
                    }
                }
                else if (loadGame.Clicked)
                {
                    _startupError = string.Empty;
                    _startupScreen = StartupScreen.LoadSlots;
                }
                else if (exit.Clicked)
                {
                    _exitRequested = true;
                }
            }
            else
            {
                FontManager.DrawText("选择存档", panelX, panelY + 122f, 22, TerminalPalette.Text);
                float slotY = panelY + 164f;
                for (int slot = 1; slot <= SaveManager.SlotCount; slot++)
                {
                    string path = SaveManager.GetSlotFilePath(slot);
                    string saveTime = SaveManager.GetSaveTime(path);
                    bool hasSave = !string.IsNullOrEmpty(saveTime);
                    string label = hasSave ? $"槽位 {slot}    {saveTime}" : $"槽位 {slot}    空";
                    var slotButton = UiButton.Draw(
                        new Rectangle(panelX, slotY + (slot - 1) * 62f, panelWidth, 48f),
                        label, ui, hasSave, 15);
                    if (slotButton.Clicked)
                    {
                        _startupError = string.Empty;
                        if (LoadSavedGame(path))
                            _startupScreen = StartupScreen.InGame;
                        else
                            _startupError = $"槽位 {slot} 读取失败，请检查存档文件。";
                    }

                }

                float backY = slotY + SaveManager.SlotCount * 62f + 8f;
                var back = UiButton.Draw(new Rectangle(panelX, backY, panelWidth, 44f), "返回", ui, true, 15);
                if (back.Clicked)
                {
                    _startupError = string.Empty;
                    _startupScreen = StartupScreen.MainMenu;
                }
            }

            if (!string.IsNullOrEmpty(_startupError))
                FontManager.DrawText(_startupError, panelX, panelY + 548f, 13, new Color(230, 105, 110, 255));

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
            float y = WindowHeight - 165f;
            var rect = new Rectangle(x, y, boxW, boxH);

            Raylib.DrawRectangleRounded(rect, 0.08f, 8, new Color((byte)10, (byte)12, (byte)18, alpha));
            Raylib.DrawRectangleRoundedLinesEx(rect, 0.08f, 8, 1.2f, new Color((byte)100, (byte)120, (byte)170, alpha));
            FontManager.DrawText(text, x + 24f, y + 10f, fontSize, new Color((byte)220, (byte)226, (byte)245, alpha));
        }

        private (Rectangle ToggleRect, Rectangle PanelRect) GetDebugMenuRects()
        {
            const float rightMargin = 40f;
            const float btnW = 66f;
            const float btnH = 28f;
            float btnX = WindowWidth - rightMargin - btnW;
            float btnY = 14f;
            var toggleRect = new Rectangle(btnX, btnY, btnW, btnH);

            float pw = 360f;
            float px = btnX + btnW - pw;
            float py = btnY + btnH + 8f;
            float itemH = 26f;
            float slotsHeight = 20f + SaveManager.SlotCount * 28f + 14f;
            float panelH = 56f + slotsHeight + _state.DropdownItems.Count * itemH + 8f;
            var panelRect = new Rectangle(px, py, pw, panelH);

            return (toggleRect, panelRect);
        }

        private void DrawTopRightControls(UiInteractionContext worldUi, UiInteractionContext panelUi)
        {
            const float rightMargin = 40f;
            const float gap = 8f;
            const float topY = 14f;
            const float controlH = 28f;
            const float debugW = 66f;
            const float growthW = 80f;

            float debugX = WindowWidth - rightMargin - debugW;
            float growthX = debugX - gap - growthW;
            float relationRightEdge = growthX - gap;

            RelationWidget.Draw(_state, worldUi, relationRightEdge, topY);

            var growthRect = new Rectangle(growthX, topY, growthW, controlH);
            var growthBtn = UiButton.Draw(growthRect, "成长/队伍", worldUi, true, 13,
                _state.IsGrowthPanelOpen ? TerminalPalette.AccentDark : TerminalPalette.SurfaceRaised,
                new Color(52, 52, 82, 255), null,
                _state.IsGrowthPanelOpen ? TerminalPalette.Accent : TerminalPalette.Border,
                Color.White, null, Color.White, null);

            if (growthBtn.Clicked)
            {
                _state.IsGrowthPanelOpen = !_state.IsGrowthPanelOpen;
                if (_state.IsGrowthPanelOpen) _state.IsRelationExpanded = false;
            }

            DrawDebugMenu(panelUi);
        }

        private void DrawDebugMenu(UiInteractionContext ui)
        {
            // ── Toggle button ─────────────────────────────────────────────
            var (toggleRect, panelRect) = GetDebugMenuRects();
            bool isOpen = _state.IsDebugMenuOpen;

            var debugBtn = UiButton.Draw(toggleRect, "Debug", ui, true, 13,
                isOpen ? TerminalPalette.AccentDark : TerminalPalette.SurfaceRaised,
                new Color(52, 52, 82, 255), null,
                isOpen ? TerminalPalette.Accent : TerminalPalette.Border,
                Color.White, null, Color.White, null);

            if (debugBtn.Clicked)
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

            Raylib.DrawRectangleRounded(panelRect, 0.05f, 5, TerminalPalette.Surface);
            Raylib.DrawRectangleRoundedLinesEx(panelRect, 0.05f, 5, 1f, TerminalPalette.Border);

            FontManager.DrawText("调试", px + 18f, py + 14f, 17, TerminalPalette.AccentBright);
            FontManager.DrawText("存档管理与场景切换", px + 70f, py + 18f, 11, TerminalPalette.TextMuted);
            var closeRect = new Rectangle(px + pw - 66f, py + 10f, 50f, 24f);
            bool closeHover = ui.CanHover(closeRect);
            Raylib.DrawRectangleRounded(closeRect, 0.18f, 4,
                closeHover ? TerminalPalette.AccentDark : TerminalPalette.SurfaceRaised);
            FontManager.DrawText("收起", closeRect.X + 13f, closeRect.Y + 7f, 10, TerminalPalette.Text);
            if (ui.WasClicked(closeRect))
            {
                _state.IsDebugMenuOpen = false;
                return;
            }
            Raylib.DrawLineEx(new Vector2(px + 16f, py + 44f), new Vector2(px + pw - 16f, py + 44f),
                1f, new Color(55, 58, 70, 255));

            // Slots Section
            float curY = py + 56f;
            FontManager.DrawText("存档管理", px + 16f, curY + 2f, 11, TerminalPalette.TextMuted);
            curY += 20f;

            for (int slot = 1; slot <= SaveManager.SlotCount; slot++)
            {
                string slotPath = SaveManager.GetSlotFilePath(slot);
                string saveTime = SaveManager.GetSaveTime(slotPath);
                bool hasSave = !string.IsNullOrEmpty(saveTime);

                FontManager.DrawText($"槽位 {slot}", px + 16f, curY + 7f, 13, TerminalPalette.Text);

                string timeStr = hasSave ? saveTime : "（空）";
                Color timeColor = hasSave ? TerminalPalette.Text : TerminalPalette.TextMuted;
                FontManager.DrawText(timeStr, px + 64f, curY + 7f, 12, timeColor);

                var rectSave = new Rectangle(px + pw - 8f - 64f, curY + 3f, 30f, 22f);
                var rectLoad = new Rectangle(px + pw - 8f - 30f, curY + 3f, 30f, 22f);

                var saveBtn = UiButton.Draw(rectSave, "存", ui, true, 12,
                    TerminalPalette.AccentDark, new Color(52, 52, 82, 255), null,
                    TerminalPalette.Accent, Color.White, null, Color.White, null);
                var loadBtn = UiButton.Draw(rectLoad, "读", ui, hasSave, 12,
                    TerminalPalette.AccentDark, new Color(52, 52, 82, 255), null,
                    TerminalPalette.Accent, Color.White, null, Color.White, null);

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
            Raylib.DrawLineEx(new Vector2(px + 16f, sepY), new Vector2(px + pw - 16f, sepY), 1f, TerminalPalette.Border);
            FontManager.DrawText("切换场景", px + 16f, sepY + 4f, 11, TerminalPalette.TextMuted);

            // Scene list
            float listY = sepY + 4f + itemH * 0.5f;
            bool mouseClick = !ui.IsLocked && Raylib.IsMouseButtonPressed(MouseButton.Left);
            bool clickHandled = false;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float iy = listY + i * itemH;
                var itemRect = new Rectangle(px + 12f, iy, pw - 24f, itemH - 2);
                bool hover = Raylib.CheckCollisionPointRec(ui.Mouse, itemRect) && !ui.IsLocked;

                if (item.IsHeader)
                {
                    FontManager.DrawText(item.Name, px + 18f, iy + 5f, 11, TerminalPalette.TextMuted);
                    continue;
                }

                bool isCurrent = string.Equals(item.SceneName, _sceneManager.CurrentSceneName, StringComparison.OrdinalIgnoreCase);
                if (hover) Raylib.DrawRectangleRounded(itemRect, 0.15f, 4, TerminalPalette.SurfaceRaised);
                if (isCurrent) Raylib.DrawRectangle((int)px + 12, (int)iy + 2, 3, (int)itemH - 6, TerminalPalette.Accent);

                FontManager.DrawText(item.Name, px + 20f, iy + 5f, 13,
                    isCurrent ? TerminalPalette.AccentBright : (hover ? Color.White : TerminalPalette.Text));

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
            const float defaultCardHeight = 150f;
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
            float viewportBottom = WindowHeight - HandPanelWidget.PanelHeight - 8f;
            float viewportHeight = Math.Max(0f, viewportBottom - viewportTop);
            var viewport = new Rectangle(0, viewportTop, WindowWidth, viewportHeight);
            int rowCount = totalCards == 0
                ? 0
                : (totalCards + cardsPerRow - 1) / cardsPerRow;
            var rowHeights = Enumerable.Repeat(defaultCardHeight, rowCount).ToArray();
            for (int i = 0; i < visibleNodes.Count; i++)
            {
                var node = visibleNodes[i];
                float nodeCardHeight = CardWidget.GetMinimumHeight(
                    node.Subtitle,
                    node.Tags,
                    node.Requires,
                    node.Resolve?.Type == ResolveType.Roll ? node.Resolve.SkillName : null,
                    _state.DisplayedSnapshot.Actors);
                if (node.Resolve?.Type != ResolveType.Roll) continue;

                float attachment = 0f;
                bool activeLocalRoll = _state.ActiveRollResult != null
                    && !ActiveRollUsesModal()
                    && string.Equals(_state.ActiveRollActionName, node.Name, StringComparison.OrdinalIgnoreCase);
                if (activeLocalRoll) attachment = 68f;
                else if (_state.CardResidues.TryGetValue(node.Name, out var residue))
                    attachment = CardWidget.ResidueAttachmentHeight(residue) + 10f;
                else if (_state.NodeSlots.TryGetValue(node.Name, out var previewSlots)
                         && previewSlots.Any(slot => slot?.Type == "die")) attachment = 64f;

                int row = i / cardsPerRow;
                rowHeights[row] = Math.Max(rowHeights[row], nodeCardHeight + attachment);
            }

            var rowOffsets = new float[rowCount];
            float contentHeight = 0f;
            for (int row = 0; row < rowCount; row++)
            {
                rowOffsets[row] = contentHeight;
                contentHeight += rowHeights[row];
                if (row < rowCount - 1) contentHeight += spacing;
            }
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
                float y = startY + rowOffsets[row] - _state.CardsScrollOffset;

                if (y > viewportBottom || y + rowHeights[row] < viewportTop)
                {
                    continue;
                }

                float nodeCardHeight = defaultCardHeight;
                if (i < visibleNodes.Count)
                {
                    var cardNode = visibleNodes[i];
                    string? rollSkill = cardNode.Resolve is { Type: ResolveType.Roll } resolve
                        ? resolve.SkillName
                        : null;
                    nodeCardHeight = CardWidget.GetMinimumHeight(
                        cardNode.Subtitle,
                        cardNode.Tags,
                        cardNode.Requires,
                        rollSkill,
                        _state.DisplayedSnapshot.Actors);
                }
                var bounds = new Rectangle(x, y, cardWidth, nodeCardHeight);
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

        private void DrawGrowthPanel(SSNoir.TerminalApp.Rendering.UiInteractionContext ui)
        {
            // 与关系面板同族的右上展开面板，不再使用居中的遮罩弹窗。
            float panelW = Math.Min(620f, WindowWidth - 80f);
            float panelH = 360f;
            float panelX = WindowWidth - 40f - panelW;
            float panelY = 50f;
            var panelRect = new Rectangle(panelX, panelY, panelW, panelH);

            Raylib.DrawRectangleRounded(panelRect, 0.05f, 5, TerminalPalette.Surface);
            Raylib.DrawRectangleRoundedLinesEx(panelRect, 0.05f, 5, 1f, TerminalPalette.Border);

            FontManager.DrawText("成长 / 队伍", panelX + 18f, panelY + 14f, 17, TerminalPalette.AccentBright);
            FontManager.DrawText($"队伍成长等级 {_state.DisplayedSnapshot.GrowthLevel} · 成长点用于提升人物属性",
                panelX + 126f, panelY + 18f, 11, new Color(150, 154, 170, 255));

            float closeX = panelX + panelW - 66f;
            float closeY = panelY + 10f;
            var closeRect = new Rectangle(closeX, closeY, 50, 24);
            bool hoverClose = ui.CanHover(closeRect);
            Raylib.DrawRectangleRounded(closeRect, 0.18f, 4,
                hoverClose ? TerminalPalette.AccentDark : TerminalPalette.SurfaceRaised);
            FontManager.DrawText("收起", closeX + 13f, closeY + 7f, 10, new Color(205, 208, 220, 255));

            if (ui.WasClicked(closeRect))
            {
                _state.IsGrowthPanelOpen = false;
            }

            Raylib.DrawLineEx(new System.Numerics.Vector2(panelX + 16, panelY + 44),
                             new System.Numerics.Vector2(panelX + panelW - 16, panelY + 44),
                             1f, new Color(55, 58, 70, 255));

            var actors = _state.DisplayedSnapshot.Actors;
            float contentStartY = panelY + 56f;
            float colWidth = (panelW - 48f - Math.Max(0, actors.Count - 1) * 10f) / Math.Max(1, actors.Count);

            var statsToUpgrade = new[] {
                (Key: "violence", Display: "力量"),
                (Key: "knowledge", Display: "见识"),
                (Key: "sharpness", Display: "敏锐"),
                (Key: "social", Display: "交际")
            };

            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                float colX = panelX + 16f + i * (colWidth + 10f);
                var actorCard = new Rectangle(colX, contentStartY, colWidth, panelH - 72f);
                Raylib.DrawRectangleRounded(actorCard, 0.06f, 4, new Color(27, 29, 38, 245));
                Raylib.DrawRectangleRoundedLinesEx(actorCard, 0.06f, 4, 1f, new Color(58, 61, 75, 255));

                // Actor Name
                Color nameColor = actor.Status == "away" ? new Color((byte)130, (byte)130, (byte)130, (byte)255) : new Color(224, 224, 216, 255);
                FontManager.DrawText(actor.Name, colX + 15, contentStartY + 5, 15, nameColor);

                // Status label if away
                if (actor.Status == "away")
                {
                    FontManager.DrawText("[暂离]", colX + 15 + FontManager.MeasureTextWidth(actor.Name, 15) + 6, contentStartY + 7, 11, new Color((byte)230, (byte)80, (byte)80, (byte)255));
                }

                // Available points
                int availPoints = _state.GetAvailableGrowthPoints(actor);
                Color pointsColor = availPoints > 0 ? TerminalPalette.AccentBright : TerminalPalette.TextMuted;
                FontManager.DrawText($"可用成长点：{availPoints}", colX + 15, contentStartY + 28, 12, pointsColor);

                // Stats rows
                float rowStartY = contentStartY + 55f;
                float rowHeight = 32f;

                for (int s = 0; s < statsToUpgrade.Length; s++)
                {
                    var stat = statsToUpgrade[s];
                    float rowY = rowStartY + s * rowHeight;

                    // Get stat value
                    if (!actor.Stats.TryGetValue(stat.Key, out int statVal))
                        throw new InvalidOperationException($"Actor '{actor.Id}' is missing required stat '{stat.Key}'.");

                    // Stat text
                    FontManager.DrawText($"{stat.Display} {statVal}", colX + 15, rowY + 3, 14, new Color(210, 210, 204, 255));

                    // Upgrade Button [+]
                    float btnW = 26f;
                    float btnH = 20f;
                    float btnX = colX + colWidth - btnW - 20f;
                    var btnRect = new Rectangle(btnX, rowY, btnW, btnH);

                    bool isEnabled = actor.Status != "away" && availPoints > 0 && statVal < TeamState.MaxStatLevel;
                    
                    var upgradeBtn = UiButton.Draw(btnRect, "+", ui, isEnabled, 13,
                        TerminalPalette.AccentDark,
                        new Color(65, 65, 112, 255),
                        new Color((byte)30, (byte)30, (byte)35, (byte)255),
                        TerminalPalette.Accent,
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
