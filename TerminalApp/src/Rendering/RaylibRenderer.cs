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
            var mousePos = Raylib.GetMousePosition();

            // Update Notification Timer
            if (_state.UiNotificationTimer > 0)
            {
                _state.UiNotificationTimer -= Raylib.GetFrameTime();
            }

            bool inputBlocked = _state.ActiveRollResult != null;
            var activeMousePos = inputBlocked ? new System.Numerics.Vector2(-100f, -100f) : mousePos;

            // Handle ESC key to return to the parent node
            if (!inputBlocked && Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                GoBack();
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

            // 8. Draw Overlays (Modals / Toasts)
            var overlayInteraction = OverlayWidget.Draw(_state, mousePos, WindowWidth, WindowHeight);
            if (overlayInteraction.ConfirmClicked)
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
                                SourceIndex = _state.SelectedResource.SourceIndex
                            };
                            _state.SelectedResource = null;
                        }
                        else if (req.Type == "item" && _state.SelectedResource.Type == "item" && req.ItemName == _state.SelectedResource.ItemName)
                        {
                            _state.ClearOtherNodeSlots(node.Name);
                            int totalOwned = (req.ItemName == "金钱") ? _gameState.Get<int>("money") : _gameState.Get<int>("item:" + req.ItemName, 0);
                            int totalSlotted = 0;
                            foreach (var slots in _state.NodeSlots.Values)
                            {
                                foreach (var s in slots)
                                {
                                    if (s != null && s.Type == "item" && s.ItemName == req.ItemName)
                                    {
                                        totalSlotted += s.Value;
                                    }
                                }
                            }
                            int available = totalOwned - totalSlotted;
                            if (available >= req.Qty)
                            {
                                slotted[j] = new SlottedResource
                                {
                                    Type = "item",
                                    ItemName = req.ItemName,
                                    Value = req.Qty
                                };
                                _state.SelectedResource = null;
                            }
                            else
                            {
                                _state.TriggerNotification($"缺少数量，需要 {req.Qty} 个 {req.ItemName}");
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
            }
        }

        private void DrawReputationPanel()
        {
            int repMayor = _gameState.Get<int>("reputation:mayor");
            int repWorkers = _gameState.Get<int>("reputation:workers");
            int repElites = _gameState.Get<int>("reputation:elites");

            float panelW = 240f;
            float panelH = 32f;
            float panelX = 350f;
            float panelY = 30f;

            var panelRect = new Rectangle(panelX, panelY, panelW, panelH);
            Color panelBg = new Color(25, 25, 35, 255);
            Color panelBorder = new Color(50, 50, 70, 255);

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
                    Raylib.DrawLineEx(new System.Numerics.Vector2(cellX, panelY + 6), new System.Numerics.Vector2(cellX, panelY + panelH - 6), 1f, new Color(45, 45, 60, 255));
                }

                int val = values[i];
                string sign = val > 0 ? "+" : "";
                string txt = $"{labels[i]} {sign}{val}";

                Color txtColor = new Color(200, 200, 220, 255);
                if (val >= 30)
                {
                    txtColor = new Color(100, 220, 100, 255);
                }
                else if (val <= -30)
                {
                    txtColor = new Color(250, 100, 100, 255);
                }

                int txtW = FontManager.MeasureTextWidth(txt, 13);
                FontManager.DrawText(txt, cellX + (cellW - txtW) / 2f, panelY + 9, 13, txtColor);
            }
        }
    }
}
