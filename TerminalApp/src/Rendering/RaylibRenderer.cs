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
        private Action? _presentationDoneCallback;

        private const int WindowWidth = 800;
        private const int WindowHeight = 600;
        private static readonly bool FastPresentationMode =
            string.Equals(Environment.GetEnvironmentVariable("SSNOIR_FAST_PRESENTATION"), "1", StringComparison.Ordinal);

        public RaylibRenderer(SceneManager sceneManager, GameState gameState)
        {
            _sceneManager = sceneManager;
            _gameState = gameState;
            _state = new RendererState();

            SceneDropdownWidget.LoadAvailableScenes(_state);

            _sceneManager.OnSceneLoaded += () =>
            {
                ResetSceneUiState();
                _state.IsPresentingAction = false;
                _state.PendingReport = null;
                _state.ActiveRollResult = null;
                AdoptLatestSnapshot();
            };
        }

        private void ResetSceneUiState()
        {
            _state.NavigationStack.Clear();
            _state.ClearAllNodeSlots();
            _state.SelectedResource = null;
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
            AdoptLatestSnapshot();
            _state.PendingReport = null;
            _state.PendingActionName = string.Empty;
            _state.IsPresentingAction = false;
            _state.PresentationStepIndex = 0;
            _state.PresentationTimer = 0f;

            var callback = _presentationDoneCallback;
            _presentationDoneCallback = null;
            callback?.Invoke();
        }

        private void StartPresentation(ActionReport report, string actionName = "", Action? onDone = null)
        {
            _state.PendingActionName = actionName;
            _presentationDoneCallback = onDone;
            if (FastPresentationMode)
            {
                FinishPresentation();
                return;
            }

            _state.IsPresentingAction = true;
            _state.PendingReport = report;
            _state.PresentationStepIndex = 0;
            _state.PresentationTimer = 0f;
        }

        private void UpdatePresentation(float dt)
        {
            if (!_state.IsPresentingAction || _state.PendingReport == null)
            {
                return;
            }

            if (_state.ActiveRollResult != null)
            {
                return;
            }

            var hints = _state.PendingReport.PresentationHints;
            if (hints == null || hints.Count == 0 || _state.PresentationStepIndex >= hints.Count)
            {
                FinishPresentation();
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
                FinishPresentation();
            }
        }

        private void AdvancePresentationAfterRollConfirm()
        {
            _state.PresentationStepIndex++;
            _state.PresentationTimer = 0f;

            var hints = _state.PendingReport?.PresentationHints;
            if (hints == null || _state.PresentationStepIndex >= hints.Count)
            {
                FinishPresentation();
            }
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
            if (hint.Kind != PresentationHintKind.ExecuteProgress && hint.Kind != PresentationHintKind.PlayAnimation)
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
            var report = _sceneManager.ExecuteAction(node, slots);
            _state.NodeSlots.Remove(node.Name);
            _state.SelectedResource = null;
            if (!string.Equals(sceneBefore, _sceneManager.CurrentSceneName, StringComparison.OrdinalIgnoreCase))
            {
                ResetSceneUiState();
            }
            StartPresentation(report, node.Name);
        }

        private void SaveCurrentGame()
        {
            try
            {
                _sceneManager.SaveGame();
                _gameState.NotificationCenter.Push("游戏已存档。", NotificationKind.Success);
            }
            catch (Exception ex)
            {
                _gameState.NotificationCenter.Push($"存档失败: {ex.Message}", NotificationKind.Error);
            }
        }

        private void LoadSavedGame()
        {
            if (!System.IO.File.Exists(SaveManager.DefaultSavePath))
            {
                _gameState.NotificationCenter.Push("没有找到存档文件。", NotificationKind.Warning);
                return;
            }

            try
            {
                _sceneManager.LoadGame();
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
                _state.VisibleNodes = new List<GameNode>();
                return;
            }

            if (_state.NavigationStack.Count == 0)
            {
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
                ResolveNavigationStack();
            }
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

            // Update Notification Center
            _gameState.NotificationCenter.Update(Raylib.GetFrameTime());
            UpdatePresentation(Raylib.GetFrameTime());

            bool inputBlocked = _state.ActiveRollResult != null || _state.IsPresentingAction;
            var ui = new UiInteractionContext
            {
                Mouse = mousePos,
                IsLocked = inputBlocked
            };
            var bgUi = new UiInteractionContext
            {
                Mouse = mousePos,
                IsLocked = inputBlocked || _state.IsGrowthPanelOpen || _state.IsTurnPanelOpen
            };

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
            var navInteraction = NavigationWidget.Draw(_state, bgUi, WindowWidth);
            if (navInteraction.GoBackClicked)
            {
                GoBack();
            }

            // 2. Draw Node Clocks (if any)
            float cardsStartY = ClockWidget.Draw(_state, 90f, WindowWidth);

            // 3. Draw Node Cards
            DrawCards(bgUi, cardsStartY);

            // 4. Draw Hand Panel
            bool turnPanelWasOpen = _state.IsTurnPanelOpen;
            var handInteraction = HandPanelWidget.Draw(_state, bgUi, WindowWidth, WindowHeight, IsInEncounter);
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

            // 5. Draw Bottom Status Bar
            StatusBarWidget.Draw(_state.DisplayedSnapshot, WindowWidth, WindowHeight);

            if (_state.IsTurnPanelOpen && IsInEncounter)
            {
                bool justOpened = !turnPanelWasOpen;
                var turnPanelInteraction = TurnPanelWidget.Draw(ui, WindowWidth, WindowHeight, justOpened);
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

            // 6. Draw Faction Reputation Panel
            DrawReputationPanel();

            // 7. Draw Debug Menu (save/load + scene switch)
            DrawDebugMenu(bgUi, ui);

            // Draw Team / Growth Toggle Button
            float btnX = 520f;
            float btnY = 30f;
            float btnW = 80f;
            float btnH = 32f;
            var btnRect = new Rectangle(btnX, btnY, btnW, btnH);
            var toggleBtn = UiButton.Draw(btnRect, "成长/队伍", bgUi, true, 13, 
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
                DrawGrowthPanel(ui);
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
                    }
                }
            }

            DrawPresentationOverlay();

            // 8. Draw Overlays (Modals / Toasts)
            var overlayInteraction = OverlayWidget.Draw(_state, _gameState.NotificationCenter, ui, WindowWidth, WindowHeight);
            if (overlayInteraction.ConfirmClicked || (_state.ActiveRollResult != null && Raylib.IsKeyPressed(KeyboardKey.Escape)))
            {
                _state.ActiveRollResult = null;
                if (_state.IsPresentingAction)
                {
                    AdvancePresentationAfterRollConfirm();
                }
            }

            Raylib.EndDrawing();
        }

        private static readonly Color DbgBg     = new Color((byte)20,  (byte)20,  (byte)28,  (byte)255);
        private static readonly Color DbgBorder = new Color((byte)60,  (byte)60,  (byte)90,  (byte)255);
        private static readonly Color DbgBtn    = new Color((byte)25,  (byte)25,  (byte)38,  (byte)255);
        private static readonly Color DbgBtnHov = new Color((byte)40,  (byte)40,  (byte)60,  (byte)255);
        private static readonly Color DbgAccent = new Color((byte)110, (byte)110, (byte)200, (byte)255);
        private static readonly Color DbgText   = new Color((byte)200, (byte)200, (byte)220, (byte)255);
        private static readonly Color DbgMuted  = new Color((byte)90,  (byte)90,  (byte)115, (byte)255);

        private void DrawDebugMenu(UiInteractionContext bgUi, UiInteractionContext ui)
        {
            // ── Toggle button ─────────────────────────────────────────────
            float btnX = WindowWidth - 76f;
            float btnY = 30f;
            var toggleRect = new Rectangle(btnX, btnY, 66f, 32f);
            bool isOpen = _state.IsDebugMenuOpen;

            var togBg  = isOpen ? new Color((byte)45,(byte)45,(byte)80,(byte)255) : DbgBtn;
            var togBdr = isOpen ? DbgAccent : DbgBorder;
            bool hoverTog = bgUi.CanHover(toggleRect);
            Raylib.DrawRectangleRounded(toggleRect, 0.25f, 4, hoverTog ? DbgBtnHov : togBg);
            Raylib.DrawRectangleRoundedLinesEx(toggleRect, 0.25f, 4, 1.5f, togBdr);
            int lblW = FontManager.MeasureTextWidth("Debug v", 13);
            FontManager.DrawText("Debug v", btnX + (66 - lblW) / 2f, btnY + 9, 13, isOpen ? DbgAccent : DbgText);

            if (!bgUi.IsLocked && Raylib.IsMouseButtonPressed(MouseButton.Left) && hoverTog)
            {
                _state.IsDebugMenuOpen = !isOpen;
                if (_state.IsDebugMenuOpen)
                {
                    SceneDropdownWidget.LoadAvailableScenes(_state);
                }
            }

            if (!_state.IsDebugMenuOpen) return;

            // ── Panel ─────────────────────────────────────────────────────
            float pw = 170f;
            float px = btnX + 66f - pw;    // right-align with button
            float py = btnY + 32f + 4f;

            // Rows: save+load(36) + sep(14) + scene items
            var items = _state.DropdownItems;
            float itemH = 26f;
            float panelH = 36f + 14f + items.Count * itemH + 8f;
            var panelRect = new Rectangle(px, py, pw, panelH);

            Raylib.DrawRectangleRounded(panelRect, 0.15f, 4, DbgBg);
            Raylib.DrawRectangleRoundedLinesEx(panelRect, 0.15f, 4, 1.5f, DbgBorder);

            // Save / Load buttons
            float rowY = py + 8f;
            var saveRect = new Rectangle(px + 6f, rowY, 74f, 28f);
            var loadRect = new Rectangle(px + 86f, rowY, 74f, 28f);

            var saveBtn = UiButton.Draw(saveRect, "存档", bgUi, true, 13,
                DbgBtn, DbgBtnHov, null, DbgBorder, Color.White, null, Color.White, null);
            var loadBtn = UiButton.Draw(loadRect, "读档", bgUi, true, 13,
                DbgBtn, DbgBtnHov, null, DbgBorder, Color.White, null, Color.White, null);

            if (saveBtn.Clicked) { SaveCurrentGame(); _state.IsDebugMenuOpen = false; return; }
            if (loadBtn.Clicked) { LoadSavedGame();   _state.IsDebugMenuOpen = false; return; }

            // Separator + "切换场景" label
            float sepY = rowY + 28f + 6f;
            Raylib.DrawLineEx(new Vector2(px + 8, sepY), new Vector2(px + pw - 8, sepY), 1f, DbgBorder);
            FontManager.DrawText("切换场景", px + 8, sepY + 4, 11, DbgMuted);

            // Scene list
            float listY = sepY + 4f + itemH * 0.5f;
            bool mouseClick = !bgUi.IsLocked && Raylib.IsMouseButtonPressed(MouseButton.Left);
            bool clickHandled = false;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float iy = listY + i * itemH;
                var itemRect = new Rectangle(px + 4, iy, pw - 8, itemH - 2);
                bool hover = Raylib.CheckCollisionPointRec(bgUi.Mouse, itemRect) && !bgUi.IsLocked;

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
                && !Raylib.CheckCollisionPointRec(bgUi.Mouse, panelRect)
                && !Raylib.CheckCollisionPointRec(bgUi.Mouse, toggleRect))
            {
                _state.IsDebugMenuOpen = false;
            }
        }

        private void DrawCards(SSNoir.TerminalApp.Rendering.UiInteractionContext ui, float startY)
        {
            float startX = 40f;
            float cardWidth = 160f;
            float cardHeight = 110f;
            float spacing = 20f;
            int cardsPerRow = 4;

            for (int i = 0; i < _state.VisibleNodes.Count; i++)
            {
                var node = _state.VisibleNodes[i];
                int row = i / cardsPerRow;
                int col = i % cardsPerRow;

                float x = startX + col * (cardWidth + spacing);
                float y = startY + row * (cardHeight + spacing);

                var bounds = new Rectangle(x, y, cardWidth, cardHeight);
                bool isHovered = ui.CanHover(bounds);

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

                List<DifficultyModifierInfo>? modifiers = node.Resolve?.DifficultyModifiers.Count > 0 ? node.Resolve.DifficultyModifiers : null;
                var execution = GetCardExecutionState(node.Name);
                var interaction = CardWidget.DrawCard(
                    bounds,
                    node.Name,
                    typeLabel,
                    isHovered,
                    node.Clocks,
                    isFlipped,
                    backText,
                    requires,
                    slotted,
                    ui,
                    modifiers,
                    execution.IsExecuting,
                    execution.Progress,
                    execution.Text);

                if (interaction.CardClicked)
                {
                    if (node.IsContainer)
                    {
                        _state.ClearAllNodeSlots();
                        _state.NavigationStack.Add(node);
                        ResolveNavigationStack();
                    }
                    else if (node.Resolve != null)
                    {
                        if (node.Resolve.Type == ResolveType.Observe)
                        {
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
                    if (res != null)
                    {
                        slotted[j] = null;
                    }
                    else if (_state.SelectedResource != null)
                    {
                        var req = requires[j];
                        if (req.Type == "die" && _state.SelectedResource.Type == "die")
                        {
                            _state.ClearOtherNodeSlots(node.Name);
                            slotted[j] = new SlottedResource
                            {
                                Type = "die",
                                Value = _state.SelectedResource.Value,
                                SourceIndex = _state.SelectedResource.SourceIndex,
                                ActorId = _state.SelectedResource.ActorId,
                                DieIndex = _state.SelectedResource.DieIndex
                            };
                            _state.SelectedResource = null;
                        }
                        else if (req.Type == "item" && _state.SelectedResource.Type == "item")
                        {
                            if (req.ItemId.Equals(_state.SelectedResource.ItemName, StringComparison.OrdinalIgnoreCase))
                            {
                                _state.ClearOtherNodeSlots(node.Name);
                                int totalOwned = _state.DisplayedSnapshot.Inventory.TryGetValue(req.ItemId, out var ownedQty) ? ownedQty : 0;
                                int totalSlotted = 0;
                                foreach (var slotsList in _state.NodeSlots.Values)
                                {
                                    foreach (var s in slotsList)
                                    {
                                        if (s != null && s.Type == "item" && s.ItemId.Equals(req.ItemId, StringComparison.OrdinalIgnoreCase))
                                        {
                                            totalSlotted += s.Qty > 0 ? s.Qty : s.Value;
                                        }
                                    }
                                }
                                int available = totalOwned - totalSlotted;
                                if (available >= req.Qty)
                                {
                                    slotted[j] = new SlottedResource
                                    {
                                        Type = "item",
                                        ItemId = req.ItemId,
                                        Value = req.Qty,
                                        Qty = req.Qty
                                    };
                                    _state.SelectedResource = null;
                                }
                                else
                                {
                                    _gameState.NotificationCenter.Push($"缺少数量，需要 {req.Qty} 个 {_state.SelectedResource.ItemName}", NotificationKind.Warning);
                                }
                            }
                        }
                    }
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
        }

        private void ExecuteSlottedAction(GameNode node, List<SlottedResource?> slotted)
        {
            ExecuteNodeAction(node, slotted);
        }

        private void DrawReputationPanel()
        {
            var snapshot = _state.DisplayedSnapshot;
            int repMayor = snapshot.Reputation.TryGetValue("mayor", out var mayor) ? mayor : 0;
            int repWorkers = snapshot.Reputation.TryGetValue("workers", out var workers) ? workers : 0;
            int repElites = snapshot.Reputation.TryGetValue("elites", out var elites) ? elites : 0;

            float panelW = 210f;
            float panelH = 32f;
            float panelX = 300f;
            float panelY = 30f;

            var panelRect = new Rectangle(panelX, panelY, panelW, panelH);
            Color panelBg = new Color((byte)25, (byte)25, (byte)35, (byte)255);
            Color panelBorder = new Color((byte)50, (byte)50, (byte)70, (byte)255);

            Raylib.DrawRectangleRounded(panelRect, 0.2f, 4, panelBg);
            Raylib.DrawRectangleRoundedLinesEx(panelRect, 0.2f, 4, 1.5f, panelBorder);

            float cellW = panelW / 3f;
            string[] labels = { "市长", "工人", "权贵" };
            int[] values = { repMayor, repWorkers, repElites };

            for (int i = 0; i < 3; i++)
            {
                float cellX = panelX + i * cellW;

                if (i > 0)
                {
                    Raylib.DrawLineEx(new System.Numerics.Vector2(cellX, panelY + 6), new System.Numerics.Vector2(cellX, panelY + panelH - 6), 1f, new Color((byte)45, (byte)45, (byte)60, (byte)255));
                }

                int val = values[i];
                string sign = val > 0 ? "+" : "";
                string txt = $"{labels[i]} {sign}{val}";

                Color txtColor = new Color((byte)200, (byte)200, (byte)220, (byte)255);
                if (val >= 30)
                {
                    txtColor = new Color((byte)100, (byte)220, (byte)100, (byte)255);
                }
                else if (val <= -30)
                {
                    txtColor = new Color((byte)250, (byte)100, (byte)100, (byte)255);
                }

                int txtW = FontManager.MeasureTextWidth(txt, 13);
                FontManager.DrawText(txt, cellX + (cellW - txtW) / 2f, panelY + 9, 13, txtColor);
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
                (Key: "violence", Display: "violence"),
                (Key: "knowledge", Display: "knowledge"),
                (Key: "coding", Display: "coding"),
                (Key: "sharpness", Display: "sharpness")
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
