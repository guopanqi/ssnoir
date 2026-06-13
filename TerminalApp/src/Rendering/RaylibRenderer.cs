using System;
using System.Collections.Generic;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public class RaylibRenderer
    {
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;
        private readonly RendererState _state;

        private const int WindowWidth = 800;
        private const int WindowHeight = 600;

        public RaylibRenderer(SceneManager sceneManager, GameState gameState)
        {
            _sceneManager = sceneManager;
            _gameState = gameState;
            _state = new RendererState();

            SceneDropdownWidget.LoadAvailableScenes(_state);

            _sceneManager.OnSceneLoaded += () =>
            {
                _state.NavigationStack.Clear();
                _state.VisibleNodes = _sceneManager.CurrentWorldNodes;
                _state.NodeSlots.Clear();
                _state.SelectedResource = null;
            };

            _sceneManager.OnWorldRefreshed += () =>
            {
                ResolveNavigationStack();
                SanitizeSlots();
            };
        }

        private void ResolveNavigationStack()
        {
            if (_state.NavigationStack.Count == 0)
            {
                _state.VisibleNodes = _sceneManager.CurrentWorldNodes;
                return;
            }

            var path = new List<string>();
            foreach (var node in _state.NavigationStack)
            {
                path.Add(node.Name);
            }

            _state.NavigationStack.Clear();
            var currentLevel = _sceneManager.CurrentWorldNodes;

            foreach (var name in path)
            {
                var match = currentLevel.Find(n => n.Name == name);
                if (match != null && match.HasChildren)
                {
                    _state.NavigationStack.Add(match);
                    currentLevel = match.Children;
                }
                else
                {
                    _state.NavigationStack.Clear();
                    _state.VisibleNodes = _sceneManager.CurrentWorldNodes;
                    return;
                }
            }

            _state.VisibleNodes = currentLevel;
        }

        private void GoBack()
        {
            if (_state.NavigationStack.Count > 0)
            {
                _state.NodeSlots.Clear();
                _state.NavigationStack.RemoveAt(_state.NavigationStack.Count - 1);
                ResolveNavigationStack();
            }
        }

        private void SanitizeSlots()
        {
            var keysToRemove = new List<string>();
            foreach (var key in _state.NodeSlots.Keys)
            {
                if (FindNodeByName(_sceneManager.CurrentWorldNodes, key) == null)
                {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in keysToRemove)
            {
                _state.NodeSlots.Remove(key);
            }
        }

        private GameNode? FindNodeByName(List<GameNode> nodes, string name)
        {
            foreach (var node in nodes)
            {
                if (node.Name == name) return node;
                if (node.Children != null)
                {
                    var found = FindNodeByName(node.Children, name);
                    if (found != null) return found;
                }
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
            if (_gameState.Team.Health <= 0)
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

            bool inputBlocked = _state.ActiveRollResult != null;
            var activeMousePos = (inputBlocked || _state.IsGrowthPanelOpen) ? new System.Numerics.Vector2(-100f, -100f) : mousePos;

            // Handle ESC key or right-click to clear selected card/resource first
            if (!inputBlocked)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsMouseButtonPressed(MouseButton.Right))
                {
                    if (_state.SelectedResource != null)
                    {
                        _state.SelectedResource = null;
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
            var navInteraction = NavigationWidget.Draw(_state, activeMousePos, WindowWidth);
            if (navInteraction.GoBackClicked)
            {
                GoBack();
            }

            // 2. Draw Node Clocks (if any)
            float cardsStartY = ClockWidget.Draw(_state, _sceneManager, 90f, WindowWidth);

            // 3. Draw Node Cards
            DrawCards(activeMousePos, cardsStartY);

            // 4. Draw Hand Panel
            var handInteraction = HandPanelWidget.Draw(_state, _gameState, activeMousePos, WindowWidth, WindowHeight);
            if (handInteraction.RestClicked)
            {
                _state.NodeSlots.Clear();
                _state.SelectedResource = null;
                _sceneManager.EndTurn();
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
            StatusBarWidget.Draw(_gameState, WindowWidth, WindowHeight);

            // 6. Draw Dropdown
            var dropdownInteraction = SceneDropdownWidget.Draw(_state, _sceneManager, activeMousePos, WindowWidth);
            if (!string.IsNullOrEmpty(dropdownInteraction.SelectedSceneName))
            {
                _sceneManager.LoadScene(dropdownInteraction.SelectedSceneName);
            }

            // 7. Draw Faction Reputation Panel
            DrawReputationPanel();

            // Draw Team / Growth Toggle Button
            float btnX = 520f;
            float btnY = 30f;
            float btnW = 80f;
            float btnH = 32f;
            var btnRect = new Rectangle(btnX, btnY, btnW, btnH);
            bool hoverBtn = Raylib.CheckCollisionPointRec(mousePos, btnRect);

            Color btnBg = _state.IsGrowthPanelOpen
                ? new Color((byte)50, (byte)50, (byte)90, (byte)255)
                : (hoverBtn ? new Color((byte)40, (byte)40, (byte)55, (byte)255) : new Color((byte)25, (byte)25, (byte)35, (byte)255));
            Color btnBorder = _state.IsGrowthPanelOpen ? new Color((byte)130, (byte)130, (byte)220, (byte)255) : new Color((byte)50, (byte)50, (byte)70, (byte)255);

            Raylib.DrawRectangleRounded(btnRect, 0.2f, 4, btnBg);
            Raylib.DrawRectangleRoundedLinesEx(btnRect, 0.2f, 4, 1.5f, btnBorder);

            string btnText = "成长/队伍";
            int btnTextW = FontManager.MeasureTextWidth(btnText, 13);
            FontManager.DrawText(btnText, btnX + (btnW - btnTextW) / 2f, btnY + 9, 13, Color.White);

            if (hoverBtn && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                _state.IsGrowthPanelOpen = !_state.IsGrowthPanelOpen;
            }

            // Draw Growth Panel if open
            if (_state.IsGrowthPanelOpen)
            {
                DrawGrowthPanel(mousePos);
            }

            // Update active roll animation timer
            if (_state.ActiveRollResult != null)
            {
                _state.ActiveRollTime += Raylib.GetFrameTime();
                float elapsed = _state.ActiveRollTime;

                if (_state.ActiveRollPhase == 0)
                {
                    // Rolling phase: 1.0s
                    float t = Math.Clamp(elapsed / 1.0f, 0f, 1f);
                    float interval = 0.05f + (0.22f - 0.05f) * t;

                    var rand = new Random();
                    _state.ActiveRollDisplayDieValue = rand.Next(1, 7);
                    _state.ActiveRollDisplayScale = 0.9f + (float)rand.NextDouble() * 0.25f;

                    if (elapsed >= 1.0f)
                    {
                        _state.ActiveRollPhase = 1;
                        _state.ActiveRollTime = 0f; // Reset phase time
                        _state.ActiveRollDisplayDieValue = _state.ActiveRollResult.FinalRollValue;
                        _state.ActiveRollDisplayScale = 1f;
                    }
                }
                else if (_state.ActiveRollPhase == 1)
                {
                    // Reveal pulse phase: 0.25s
                    float t = Math.Clamp(elapsed / 0.25f, 0f, 1f);
                    _state.ActiveRollDisplayScale = 1f + (float)Math.Sin(t * Math.PI) * 0.35f;

                    if (elapsed >= 0.25f)
                    {
                        _state.ActiveRollPhase = 2;
                        _state.ActiveRollTime = 0f;
                        _state.ActiveRollDisplayScale = 1f;
                    }
                }
            }

            // 8. Draw Overlays (Modals / Toasts)
            var overlayInteraction = OverlayWidget.Draw(_state, _gameState.NotificationCenter, mousePos, WindowWidth, WindowHeight);
            if (overlayInteraction.ConfirmClicked || (_state.ActiveRollResult != null && Raylib.IsKeyPressed(KeyboardKey.Escape)))
            {
                _state.ActiveRollResult = null;
            }

            Raylib.EndDrawing();
        }

        private void DrawCards(System.Numerics.Vector2 mousePos, float startY)
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
                bool isHovered = Raylib.CheckCollisionPointRec(mousePos, bounds);

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
                var interaction = CardWidget.DrawCard(bounds, node.Name, typeLabel, isHovered, node.Clocks, isFlipped, backText, requires, slotted, mousePos, modifiers);

                if (interaction.CardClicked)
                {
                    if (node.HasChildren)
                    {
                        _state.NodeSlots.Clear();
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
                                _sceneManager.ExecuteAction(node, new List<SlottedResource?>());
                            }
                        }
                        else if (requires == null)
                        {
                            _sceneManager.ExecuteAction(node, new List<SlottedResource?>());
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
                                int totalOwned = _gameState.Get<int>("item:" + req.ItemId, 0);
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

                if (interaction.ExecuteClicked && slotted != null)
                {
                    ExecuteSlottedAction(node, slotted);
                }
            }
        }

        private void ExecuteSlottedAction(GameNode node, List<SlottedResource?> slotted)
        {
            // 核心逻辑移交给 SceneManager，彻底解决重复与不一致问题
            ActionReport report = _sceneManager.ExecuteAction(node, slotted);

            // 清除卡槽
            _state.NodeSlots.Remove(node.Name);

            // 触发判定结果弹窗
            if (report.Type == ActionType.Roll)
            {
                _state.ActiveRollResult = report;
                _state.ActiveRollActionName = node.Name;
                _state.ActiveRollTime = 0f;
                _state.ActiveRollPhase = 0;
                _state.ActiveRollDisplayDieValue = 1;
                _state.ActiveRollDisplayScale = 1f;
            }
        }

        private void DrawReputationPanel()
        {
            int repMayor = _gameState.Get<int>("reputation:mayor");
            int repWorkers = _gameState.Get<int>("reputation:workers");
            int repElites = _gameState.Get<int>("reputation:elites");

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

        private void DrawGrowthPanel(System.Numerics.Vector2 mousePos)
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
            bool hoverClose = Raylib.CheckCollisionPointRec(mousePos, closeRect);
            Color closeColor = hoverClose ? Color.Red : new Color((byte)180, (byte)180, (byte)200, (byte)255);
            FontManager.DrawText("X", closeX + 6, closeY + 3, 16, closeColor);

            if (hoverClose && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                _state.IsGrowthPanelOpen = false;
            }

            // Divider line
            Raylib.DrawLineEx(new System.Numerics.Vector2(panelX + 20, panelY + 52),
                             new System.Numerics.Vector2(panelX + panelW - 20, panelY + 52),
                             1f, new Color((byte)55, (byte)55, (byte)70, (byte)255));

            // Team Growth Level
            FontManager.DrawText($"队伍成长等级：{_gameState.Team.GrowthLevel}", panelX + 25, panelY + 65, 14, new Color((byte)150, (byte)220, (byte)255, (byte)255));

            // Actors list
            var actors = _gameState.Team.Actors;
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
                int availPoints = _gameState.Team.GetAvailableGrowthPoints(actor);
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
                    bool hoverBtn = isEnabled && Raylib.CheckCollisionPointRec(mousePos, btnRect);

                    Color btnBgColor, btnBorderColor, btnTextColor;
                    if (isEnabled)
                    {
                        btnBgColor = hoverBtn ? new Color((byte)50, (byte)140, (byte)70, (byte)255) : new Color((byte)30, (byte)90, (byte)45, (byte)255);
                        btnBorderColor = hoverBtn ? Color.White : new Color((byte)100, (byte)210, (byte)120, (byte)255);
                        btnTextColor = Color.White;
                    }
                    else
                    {
                        btnBgColor = new Color((byte)30, (byte)30, (byte)35, (byte)255);
                        btnBorderColor = new Color((byte)50, (byte)50, (byte)55, (byte)255);
                        btnTextColor = new Color((byte)90, (byte)90, (byte)100, (byte)255);
                    }

                    Raylib.DrawRectangleRounded(btnRect, 0.25f, 4, btnBgColor);
                    Raylib.DrawRectangleRoundedLinesEx(btnRect, 0.25f, 4, 1.2f, btnBorderColor);
                    FontManager.DrawText("+", btnX + 8, rowY + 3, 13, btnTextColor);

                    if (isEnabled && hoverBtn && Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        // Upgrade the stat through the engine rule entry
                        _gameState.Team.UpgradeActorStat(actor.Id, stat.Key);

                        _gameState.NotificationCenter.Push($"{actor.Name} 升级了 {stat.Display} 属性！", NotificationKind.Success);
                    }
                }
            }
        }
    }
}
