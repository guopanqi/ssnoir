using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public class RaylibRenderer
    {
        private readonly SceneManager _sceneManager;
        private readonly GameState _gameState;

        private readonly List<GameNode> _navigationStack = new List<GameNode>();
        private List<GameNode> _visibleNodes = new List<GameNode>();

        private bool _isDropdownOpen = false;
        private readonly List<string> _availableScenes = new List<string>();

        private readonly HashSet<string> _flippedNodes = new HashSet<string>();
        private readonly Dictionary<string, List<SlottedResource?>> _nodeSlots = new Dictionary<string, List<SlottedResource?>>();
        private SelectedResource? _selectedResource = null;
        private RollResult? _activeRollResult = null;
        private string _uiNotification = "";
        private float _uiNotificationTimer = 0f;

        private class SelectedResource
        {
            public string Type { get; set; } = string.Empty; // "die" or "item"
            public string ItemName { get; set; } = string.Empty;
            public int Value { get; set; }
            public int SourceIndex { get; set; } = -1;
        }

        private const int WindowWidth = 800;
        private const int WindowHeight = 600;

        public RaylibRenderer(SceneManager sceneManager, GameState gameState)
        {
            _sceneManager = sceneManager;
            _gameState = gameState;

            LoadAvailableScenes();

            _sceneManager.OnSceneLoaded += () =>
            {
                _navigationStack.Clear();
                _visibleNodes = _sceneManager.CurrentWorldNodes;
                _nodeSlots.Clear();
                _selectedResource = null;
            };

            _sceneManager.OnWorldRefreshed += () =>
            {
                ResolveNavigationStack();
                SanitizeSlots();
            };
        }

        private void ResolveNavigationStack()
        {
            if (_navigationStack.Count == 0)
            {
                _visibleNodes = _sceneManager.CurrentWorldNodes;
                return;
            }

            var path = new List<string>();
            foreach (var node in _navigationStack)
            {
                path.Add(node.Name);
            }

            _navigationStack.Clear();
            var currentLevel = _sceneManager.CurrentWorldNodes;

            foreach (var name in path)
            {
                var match = currentLevel.Find(n => n.Name == name);
                if (match != null && match.HasChildren)
                {
                    _navigationStack.Add(match);
                    currentLevel = match.Children;
                }
                else
                {
                    _navigationStack.Clear();
                    _visibleNodes = _sceneManager.CurrentWorldNodes;
                    return;
                }
            }

            _visibleNodes = currentLevel;
        }

        private void GoBack()
        {
            if (_navigationStack.Count > 0)
            {
                _nodeSlots.Clear();
                _navigationStack.RemoveAt(_navigationStack.Count - 1);
                ResolveNavigationStack();
            }
        }

        private void SanitizeSlots()
        {
            var keysToRemove = new List<string>();
            foreach (var key in _nodeSlots.Keys)
            {
                if (FindNodeByName(_sceneManager.CurrentWorldNodes, key) == null)
                {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in keysToRemove)
            {
                _nodeSlots.Remove(key);
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

            // Load font with Chinese characters support
            FontManager.LoadFont("assets/fonts/ArialUnicode.ttf", 48);

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
            if (_uiNotificationTimer > 0)
            {
                _uiNotificationTimer -= Raylib.GetFrameTime();
            }

            // Block input to underlying layers if a modal overlay is active
            bool inputBlocked = _activeRollResult != null;
            var activeMousePos = inputBlocked ? new Vector2(-100f, -100f) : mousePos;

            // Handle ESC key to return to the parent node
            if (!inputBlocked && Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                GoBack();
            }

            if (!inputBlocked)
            {
                UpdateDropdown(mousePos);
                if (Raylib.IsMouseButtonPressed(MouseButton.Right))
                {
                    _selectedResource = null;
                }
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 20, 25, 255));

            // ── Draw Navigation / Breadcrumbs ──
            DrawNavigation(activeMousePos);

            // ── Draw Node Clocks (if any) ──
            float cardsStartY = DrawNodeClocks(90f);

            // ── Draw Node Cards ──
            DrawCards(activeMousePos, cardsStartY);

            // ── Draw Bottom Status Panel ──
            DrawStatusPanel(activeMousePos);

            // ── Draw Dropdown ──
            DrawDropdown(activeMousePos);

            // ── Draw Overlays (Modals / Toasts) ──
            DrawOverlays(mousePos);

            Raylib.EndDrawing();
        }

        private void DrawNavigation(Vector2 mousePos)
        {
            float startX = 40f;
            float startY = 30f;

            // Return Button if we are deep in the stack
            if (_navigationStack.Count > 0)
            {
                var returnRect = new Rectangle(startX, startY, 90, 32);
                bool isHovered = Raylib.CheckCollisionPointRec(mousePos, returnRect);
                
                Color btnColor = isHovered ? new Color(60, 60, 80, 255) : new Color(40, 40, 50, 255);
                Color textColor = isHovered ? Color.White : new Color(180, 180, 200, 255);
                
                Raylib.DrawRectangleRounded(returnRect, 0.2f, 4, btnColor);
                Raylib.DrawRectangleRoundedLinesEx(returnRect, 0.2f, 4, 1.5f, new Color(80, 80, 100, 255));
                FontManager.DrawText("< 返回", startX + 18, startY + 8, 16, textColor);

                if (isHovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    GoBack();
                }

                startX += 110f;
            }

            // Draw Breadcrumbs
            string breadcrumbText = "当前位置: ";
            if (_navigationStack.Count == 0)
            {
                breadcrumbText += "根目录";
            }
            else
            {
                breadcrumbText += string.Join(" > ", _navigationStack.ConvertAll(n => n.Name));
            }
            FontManager.DrawText(breadcrumbText, startX, startY + 8, 16, new Color(180, 180, 200, 255));

            // Draw horizontal divider line
            Raylib.DrawLineEx(new Vector2(40, 80), new Vector2(WindowWidth - 40, 80), 1.5f, new Color(50, 50, 60, 255));
        }

        private void DrawCards(Vector2 mousePos, float startY)
        {
            float startX = 40f;
            float cardWidth = 160f;
            float cardHeight = 110f;
            float spacing = 20f;
            int cardsPerRow = 4;

            for (int i = 0; i < _visibleNodes.Count; i++)
            {
                var node = _visibleNodes[i];
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

                bool isFlipped = _flippedNodes.Contains(node.Name);
                string backText = (node.Resolve?.Type == ResolveType.Observe) ? node.Resolve.ObserveText : "";

                List<ActionCost>? requires = null;
                List<SlottedResource?>? slotted = null;

                if (node.Requires != null && node.Requires.Count > 0)
                {
                    requires = node.Requires;
                    if (!_nodeSlots.TryGetValue(node.Name, out slotted))
                    {
                        slotted = new List<SlottedResource?>();
                        for (int j = 0; j < node.Requires.Count; j++)
                        {
                            slotted.Add(null);
                        }
                        _nodeSlots[node.Name] = slotted;
                    }
                }

                var interaction = CardWidget.DrawCard(bounds, node.Name, typeLabel, isHovered, node.Clocks, isFlipped, backText, requires, slotted, mousePos);

                if (interaction.CardClicked)
                {
                    if (node.HasChildren)
                    {
                        _nodeSlots.Clear();
                        _navigationStack.Add(node);
                        ResolveNavigationStack();
                    }
                    else if (node.Resolve != null)
                    {
                        if (node.Resolve.Type == ResolveType.Observe)
                        {
                            if (isFlipped)
                            {
                                _flippedNodes.Remove(node.Name);
                            }
                            else
                            {
                                _flippedNodes.Add(node.Name);
                            }
                        }
                        else if (requires == null)
                        {
                            ExecuteActionWithoutDie(node);
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
                    else if (_selectedResource != null)
                    {
                        var req = requires[j];
                        if (req.Type == "die" && _selectedResource.Type == "die")
                        {
                            ClearOtherNodeSlots(node.Name);
                            slotted[j] = new SlottedResource
                            {
                                Type = "die",
                                Value = _selectedResource.Value,
                                SourceIndex = _selectedResource.SourceIndex
                            };
                            _selectedResource = null;
                        }
                        else if (req.Type == "item" && _selectedResource.Type == "item" && req.ItemName == _selectedResource.ItemName)
                        {
                            ClearOtherNodeSlots(node.Name);
                            int totalOwned = (req.ItemName == "金钱") ? _gameState.Get<int>("money") : _gameState.Get<int>("item:" + req.ItemName, 0);
                            int totalSlotted = 0;
                            foreach (var slots in _nodeSlots.Values)
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
                                _selectedResource = null;
                            }
                            else
                            {
                                TriggerNotification($"缺少数量，需要 {req.Qty} 个 {req.ItemName}");
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
            // 1. Permanently consume resources from GameState
            var diceToConsume = new List<int>();
            foreach (var s in slotted)
            {
                if (s != null && s.Type == "die")
                {
                    diceToConsume.Add(s.SourceIndex);
                }
            }
            diceToConsume.Sort((a, b) => b.CompareTo(a));
            var diceList = _gameState.Get<List<object>>("action-dice");
            if (diceList != null)
            {
                foreach (var idx in diceToConsume)
                {
                    if (idx >= 0 && idx < diceList.Count)
                    {
                        diceList.RemoveAt(idx);
                    }
                }
                _gameState.Set("action-dice", diceList);
            }

            foreach (var s in slotted)
            {
                if (s != null && s.Type == "item")
                {
                    if (s.ItemName == "金钱")
                    {
                        int owned = _gameState.Get<int>("money");
                        _gameState.Set("money", Math.Max(0, owned - s.Value));
                    }
                    else
                    {
                        int owned = _gameState.Get<int>("item:" + s.ItemName, 0);
                        _gameState.Set("item:" + s.ItemName, Math.Max(0, owned - s.Value));
                    }
                }
            }

            // 2. Clear slots
            _nodeSlots.Remove(node.Name);

            // 3. Resolve
            if (node.Resolve != null)
            {
                if (node.Resolve.Type == ResolveType.Instant)
                {
                    node.Resolve.Effect?.Invoke();
                    _sceneManager.OnActionExecuted();
                }
                else if (node.Resolve.Type == ResolveType.Roll)
                {
                    var dieSlot = slotted.Find(s => s != null && s.Type == "die");
                    int chosenDieVal = dieSlot != null ? dieSlot.Value : 1;

                    int skillLevel = _gameState.Get<int>("skill:" + node.Resolve.SkillName, 1);
                    var rand = new Random();
                    var randomDice = new List<int>();
                    int finalValue = chosenDieVal;

                    for (int i = 0; i < skillLevel - 1; i++)
                    {
                        int r = rand.Next(1, 7);
                        randomDice.Add(r);
                        if (r > finalValue)
                        {
                            finalValue = r;
                        }
                    }

                    string outcome = "";
                    if (finalValue <= 2)
                    {
                        outcome = "失败";
                        node.Resolve.OnFail?.Invoke();
                    }
                    else if (finalValue <= 4)
                    {
                        outcome = "中性";
                        node.Resolve.OnNeutral?.Invoke();
                    }
                    else
                    {
                        outcome = "成功";
                        node.Resolve.OnSuccess?.Invoke();
                    }

                    _activeRollResult = new RollResult
                    {
                        ActionName = node.Name,
                        ChosenDie = chosenDieVal,
                        RandomDice = randomDice,
                        FinalValue = finalValue,
                        Outcome = outcome
                    };
                }
            }
        }

        private void ClearOtherNodeSlots(string activeNodeName)
        {
            foreach (var pair in _nodeSlots)
            {
                if (pair.Key != activeNodeName)
                {
                    var slots = pair.Value;
                    for (int i = 0; i < slots.Count; i++)
                    {
                        slots[i] = null;
                    }
                }
            }
        }

        private bool IsDieSlotted(int dieIndex)
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

        private int GetRemainingItemQty(string itemName)
        {
            int total = 0;
            if (itemName == "金钱")
            {
                total = _gameState.Get<int>("money");
            }
            else
            {
                total = _gameState.Get<int>("item:" + itemName, 0);
            }

            foreach (var slots in _nodeSlots.Values)
            {
                foreach (var slot in slots)
                {
                    if (slot != null && slot.Type == "item" && slot.ItemName == itemName)
                    {
                        total -= slot.Value;
                    }
                }
            }

            if (_selectedResource != null && _selectedResource.Type == "item" && _selectedResource.ItemName == itemName)
            {
                if (itemName != "金钱")
                {
                    total -= 1;
                }
            }

            return Math.Max(0, total);
        }

        private void DrawStatusPanel(Vector2 mousePos)
        {
            float handY = WindowHeight - 100;
            float statusY = WindowHeight - 25;

            // 1. Draw Hand Panel (y=500, height 75)
            Raylib.DrawRectangle(0, (int)handY, WindowWidth, 75, new Color(18, 18, 24, 255));
            Raylib.DrawLineEx(new Vector2(0, handY), new Vector2(WindowWidth, handY), 1.5f, new Color(40, 40, 50, 255));

            Color labelColor = new Color(150, 150, 170, 255);

            // Draw Action Dice in Hand
            FontManager.DrawText("手牌骰子: ", 30, handY + 28, 14, labelColor);
            var dice = _gameState.Get<List<object>>("action-dice");
            if (dice != null)
            {
                for (int i = 0; i < dice.Count; i++)
                {
                    float dieX = 110 + i * 42;
                    float dieY = handY + 18;
                    var dieRect = new Rectangle(dieX, dieY, 32, 32);

                    bool isSlotted = IsDieSlotted(i);
                    bool hover = !isSlotted && Raylib.CheckCollisionPointRec(mousePos, dieRect);

                    if (isSlotted)
                    {
                        Raylib.DrawRectangleRounded(dieRect, 0.2f, 4, new Color(30, 30, 35, 120));
                        Raylib.DrawRectangleRoundedLinesEx(dieRect, 0.2f, 4, 1f, new Color(40, 40, 45, 120));

                        string numStr = dice[i]?.ToString() ?? "0";
                        int numW = FontManager.MeasureTextWidth(numStr, 14);
                        FontManager.DrawText(numStr, dieX + (32 - numW) / 2f, dieY + 8, 14, new Color(80, 80, 90, 120));
                    }
                    else
                    {
                        Color bg = hover ? new Color(70, 70, 100, 255) : new Color(45, 45, 60, 255);
                        Color border = hover ? new Color(150, 150, 250, 255) : new Color(90, 90, 110, 255);

                        Raylib.DrawRectangleRounded(dieRect, 0.2f, 4, bg);
                        Raylib.DrawRectangleRoundedLinesEx(dieRect, 0.2f, 4, 1.5f, border);

                        string numStr = dice[i]?.ToString() ?? "0";
                        int numW = FontManager.MeasureTextWidth(numStr, 14);
                        FontManager.DrawText(numStr, dieX + (32 - numW) / 2f, dieY + 8, 14, Color.White);

                        if (hover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                        {
                            int val = 0;
                            if (dice[i] is double d) val = (int)d;
                            else if (dice[i] is long l) val = (int)l;
                            else if (dice[i] is int valInt) val = valInt;

                            _selectedResource = new SelectedResource
                            {
                                Type = "die",
                                Value = val,
                                SourceIndex = i
                            };
                        }
                    }
                }
            }

            // Draw Items in Hand (plus money)
            float itemsStartX = 360f;
            FontManager.DrawText("手牌物品: ", itemsStartX, handY + 28, 14, labelColor);

            var items = new List<(string Name, int Qty)>();
            int money = _gameState.Get<int>("money");
            if (money > 0)
            {
                items.Add(("金钱", money));
            }

            foreach (var kvp in _gameState.GetAllStates())
            {
                if (kvp.Key.StartsWith("item:"))
                {
                    string itemName = kvp.Key.Substring(5);
                    int qty = 0;
                    if (kvp.Value is double d) qty = (int)d;
                    else if (kvp.Value is long l) qty = (int)l;
                    else if (kvp.Value is int valInt) qty = valInt;

                    if (qty > 0)
                    {
                        items.Add((itemName, qty));
                    }
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float itemX = itemsStartX + 75 + i * 75;
                float itemY = handY + 18;
                var itemRect = new Rectangle(itemX, itemY, 68, 32);

                int remaining = GetRemainingItemQty(item.Name);
                bool hover = (remaining > 0) && Raylib.CheckCollisionPointRec(mousePos, itemRect);

                if (remaining <= 0)
                {
                    Raylib.DrawRectangleRounded(itemRect, 0.2f, 4, new Color(30, 30, 35, 120));
                    Raylib.DrawRectangleRoundedLinesEx(itemRect, 0.2f, 4, 1f, new Color(40, 40, 45, 120));

                    string label = item.Name == "金钱" ? "$0" : $"{item.Name} x0";
                    int lblW = FontManager.MeasureTextWidth(label, 12);
                    FontManager.DrawText(label, itemX + (68 - lblW) / 2f, itemY + 8, 12, new Color(80, 80, 90, 120));
                }
                else
                {
                    Color bg = hover ? new Color(70, 70, 100, 255) : new Color(45, 45, 60, 255);
                    Color border = hover ? new Color(150, 150, 250, 255) : new Color(90, 90, 110, 255);

                    Raylib.DrawRectangleRounded(itemRect, 0.2f, 4, bg);
                    Raylib.DrawRectangleRoundedLinesEx(itemRect, 0.2f, 4, 1.5f, border);

                    string label = item.Name == "金钱" ? $"${remaining}" : $"{item.Name} x{remaining}";
                    int lblW = FontManager.MeasureTextWidth(label, 12);
                    FontManager.DrawText(label, itemX + (68 - lblW) / 2f, itemY + 8, 12, Color.White);

                    if (hover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        _selectedResource = new SelectedResource
                        {
                            Type = "item",
                            ItemName = item.Name
                        };
                    }
                }
            }

            // 3. Draw Rest / End Turn Button
            float restX = WindowWidth - 110;
            float restY = handY + 18;
            var restRect = new Rectangle(restX, restY, 80, 32);
            bool restHover = Raylib.CheckCollisionPointRec(mousePos, restRect);

            Color restBg = restHover ? new Color(120, 50, 50, 255) : new Color(85, 30, 30, 255);
            Color restBorder = restHover ? new Color(220, 100, 100, 255) : new Color(140, 60, 60, 255);

            Raylib.DrawRectangleRounded(restRect, 0.2f, 4, restBg);
            Raylib.DrawRectangleRoundedLinesEx(restRect, 0.2f, 4, 1.5f, restBorder);

            string restText = "休息";
            int restW = FontManager.MeasureTextWidth(restText, 14);
            FontManager.DrawText(restText, restX + (80 - restW) / 2f, restY + 9, 14, Color.White);

            if (restHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                EndTurn();
            }

            // 2. Draw Status Bar (y=575, height 25)
            Raylib.DrawRectangle(0, (int)statusY, WindowWidth, 25, new Color(10, 10, 15, 255));
            Raylib.DrawLineEx(new Vector2(0, statusY), new Vector2(WindowWidth, statusY), 1.5f, new Color(30, 30, 40, 255));

            int health = _gameState.Get<int>("health");
            string location = _gameState.Get<string>("location");

            FontManager.DrawText("健康: ", 30, statusY + 5, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText($"{health}%", 70, statusY + 5, 13, new Color(250, 100, 100, 255));

            FontManager.DrawText("场景: ", 160, statusY + 5, 13, new Color(200, 200, 220, 255));
            FontManager.DrawText(location.ToUpper(), 200, statusY + 5, 13, new Color(100, 220, 100, 255));

            // Help tip
            string tip = "提示: 点击手牌选择，点击卡槽放入，右键取消选择。";
            FontManager.DrawText(tip, 320, statusY + 5, 12, new Color(140, 140, 160, 255));
        }

        private void ExecuteActionWithoutDie(GameNode node)
        {
            if (node.Resolve?.Type == ResolveType.Instant)
            {
                node.Resolve.Effect?.Invoke();
            }
            _sceneManager.OnActionExecuted();
        }

        private void EndTurn()
        {
            _nodeSlots.Clear();
            _selectedResource = null;
            _sceneManager.EndTurn();
        }

        private void TriggerNotification(string message)
        {
            _uiNotification = message;
            _uiNotificationTimer = 2.5f;
        }

        private void DrawOverlays(Vector2 mousePos)
        {
            // 1. Toast Notification
            if (_uiNotificationTimer > 0 && !string.IsNullOrEmpty(_uiNotification))
            {
                int toastW = FontManager.MeasureTextWidth(_uiNotification, 14) + 40;
                float toastX = (WindowWidth - toastW) / 2f;
                float toastY = 15f;
                var toastRect = new Rectangle(toastX, toastY, toastW, 30);
                
                Raylib.DrawRectangleRounded(toastRect, 0.4f, 4, new Color(120, 20, 30, 230));
                Raylib.DrawRectangleRoundedLinesEx(toastRect, 0.4f, 4, 1.5f, new Color(180, 40, 50, 255));
                FontManager.DrawText(_uiNotification, toastX + 20, toastY + 7, 14, Color.White);
            }

            // 2. Trailing Selected Resource
            if (_selectedResource != null)
            {
                var mPos = Raylib.GetMousePosition();
                float overlayW = _selectedResource.Type == "die" ? 32f : 68f;
                float overlayH = 32f;
                var rect = new Rectangle(mPos.X + 12, mPos.Y + 12, overlayW, overlayH);

                Raylib.DrawRectangleRounded(rect, 0.2f, 4, new Color(50, 50, 90, 200));
                Raylib.DrawRectangleRoundedLinesEx(rect, 0.2f, 4, 1.5f, new Color(150, 150, 250, 255));

                string text = _selectedResource.Type == "die" 
                    ? _selectedResource.Value.ToString() 
                    : _selectedResource.ItemName;
                int textW = FontManager.MeasureTextWidth(text, 12);
                FontManager.DrawText(text, rect.X + (overlayW - textW) / 2f, rect.Y + 8, 12, Color.White);
            }

            // 3. Roll Result Modal
            if (_activeRollResult != null)
            {
                Raylib.DrawRectangle(0, 0, WindowWidth, WindowHeight, new Color(0, 0, 0, 180));

                float modalW = 380;
                float modalH = 240;
                float modalX = (WindowWidth - modalW) / 2f;
                float modalY = (WindowHeight - modalH) / 2f;
                var modalRect = new Rectangle(modalX, modalY, modalW, modalH);

                Raylib.DrawRectangleRounded(modalRect, 0.1f, 8, new Color(30, 30, 42, 255));
                Raylib.DrawRectangleRoundedLinesEx(modalRect, 0.1f, 8, 2f, new Color(100, 100, 130, 255));

                string title = $"判定结果: {_activeRollResult.ActionName}";
                int titleW = FontManager.MeasureTextWidth(title, 18);
                FontManager.DrawText(title, modalX + (modalW - titleW) / 2f, modalY + 20, 18, Color.White);

                float lineY = modalY + 60;
                
                string line1 = $"投入行动力骰子值: {_activeRollResult.ChosenDie}";
                FontManager.DrawText(line1, modalX + 40, lineY, 14, new Color(200, 200, 220, 255));
                lineY += 20;

                string line2 = _activeRollResult.RandomDice.Count > 0 
                    ? $"额外掷骰结果: {string.Join(", ", _activeRollResult.RandomDice)}"
                    : "无额外随机掷骰";
                FontManager.DrawText(line2, modalX + 40, lineY, 14, new Color(200, 200, 220, 255));
                lineY += 20;

                string line3 = $"最终判定最大值: {_activeRollResult.FinalValue}";
                FontManager.DrawText(line3, modalX + 40, lineY, 14, new Color(220, 220, 250, 255));
                lineY += 25;

                Color outcomeColor = _activeRollResult.Outcome == "失败" ? new Color(250, 80, 80, 255)
                                    : _activeRollResult.Outcome == "中性" ? new Color(250, 220, 100, 255)
                                    : new Color(80, 250, 80, 255);

                string outcomeStr = $"判定结论: {_activeRollResult.Outcome}";
                int outW = FontManager.MeasureTextWidth(outcomeStr, 16);
                FontManager.DrawText(outcomeStr, modalX + (modalW - outW) / 2f, lineY, 16, outcomeColor);

                float btnW = 90;
                float btnH = 32;
                float btnX = modalX + (modalW - btnW) / 2f;
                float btnY = modalY + modalH - 50;
                var btnRect = new Rectangle(btnX, btnY, btnW, btnH);
                bool btnHover = Raylib.CheckCollisionPointRec(mousePos, btnRect);

                Color bBg = btnHover ? new Color(80, 80, 110, 255) : new Color(50, 50, 70, 255);
                Color bBorder = btnHover ? new Color(180, 180, 250, 255) : new Color(90, 90, 120, 255);

                Raylib.DrawRectangleRounded(btnRect, 0.2f, 4, bBg);
                Raylib.DrawRectangleRoundedLinesEx(btnRect, 0.2f, 4, 1.5f, bBorder);

                string btnText = "确定";
                int btnTextW = FontManager.MeasureTextWidth(btnText, 14);
                FontManager.DrawText(btnText, btnX + (btnW - btnTextW) / 2f, btnY + 8, 14, Color.White);

                if (btnHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    _activeRollResult = null;
                    _sceneManager.OnActionExecuted();
                }
            }
        }

        private void DrawDetailedClock(ref float x, float y, GameClock clock)
        {
            Color textColor = new Color(200, 200, 220, 255);
            Color activeColor = new Color(130, 130, 250, 255);
            Color inactiveColor = new Color(45, 45, 55, 255);
            Color outlineColor = new Color(70, 70, 90, 255);

            FontManager.DrawText(clock.Label, x, y, 16, textColor);
            int labelWidth = FontManager.MeasureTextWidth(clock.Label, 16);
            float contentX = x + labelWidth + 8;

            if (clock.Style == ClockStyle.Pie)
            {
                float radius = 10f;
                var center = new Vector2(contentX + radius, y + 8);
                
                Raylib.DrawCircleLines((int)center.X, (int)center.Y, radius, outlineColor);
                if (clock.Max > 0 && clock.Current > 0)
                {
                    float percent = (float)clock.Current / clock.Max;
                    float startAngle = -90f;
                    float endAngle = -90f + 360f * percent;
                    Raylib.DrawCircleSector(center, radius, startAngle, endAngle, 36, activeColor);
                }

                string frac = $"{clock.Current}/{clock.Max}";
                FontManager.DrawText(frac, contentX + radius * 2 + 6, y, 14, textColor);
                int fracW = FontManager.MeasureTextWidth(frac, 14);

                x += labelWidth + 8 + radius * 2 + 6 + fracW + 20;
            }
            else if (clock.Style == ClockStyle.Countdown)
            {
                float boxW = 22;
                float boxH = 18;
                var boxRect = new Rectangle(contentX, y, boxW, boxH);
                
                Raylib.DrawRectangleRounded(boxRect, 0.2f, 4, inactiveColor);
                Raylib.DrawRectangleRoundedLinesEx(boxRect, 0.2f, 4, 1f, outlineColor);

                string numStr = clock.Current.ToString();
                int numW = FontManager.MeasureTextWidth(numStr, 14);
                FontManager.DrawText(numStr, contentX + (boxW - numW) / 2f, y + 2, 14, activeColor);

                string maxStr = $"/{clock.Max}";
                FontManager.DrawText(maxStr, contentX + boxW + 4, y + 2, 14, new Color(120, 120, 140, 255));
                int maxW = FontManager.MeasureTextWidth(maxStr, 14);

                x += labelWidth + 8 + boxW + 4 + maxW + 20;
            }
            else
            {
                for (int i = 0; i < clock.Max; i++)
                {
                    var segRect = new Rectangle(contentX + i * 14, y + 2, 10, 10);
                    if (i < clock.Current)
                    {
                        Raylib.DrawRectangleRounded(segRect, 0.3f, 4, activeColor);
                    }
                    else
                    {
                        Raylib.DrawRectangleRounded(segRect, 0.3f, 4, inactiveColor);
                        Raylib.DrawRectangleRoundedLinesEx(segRect, 0.3f, 4, 1f, outlineColor);
                    }
                }

                x += labelWidth + 8 + clock.Max * 14 + 20;
            }
        }

        private float DrawNodeClocks(float y)
        {
            var clocksToShow = new List<GameClock>();
            if (_navigationStack.Count == 0)
            {
                foreach (var node in _sceneManager.CurrentWorldNodes)
                {
                    clocksToShow.AddRange(node.Clocks);
                }
            }
            else
            {
                var currentNode = _navigationStack[_navigationStack.Count - 1];
                if (currentNode.Clocks != null)
                {
                    clocksToShow.AddRange(currentNode.Clocks);
                }
            }

            if (clocksToShow.Count == 0)
            {
                return 110f;
            }

            float x = 40f;
            FontManager.DrawText("当前节点状态: ", x, y, 14, new Color(150, 150, 170, 255));
            x += 105;

            foreach (var clock in clocksToShow)
            {
                DrawDetailedClock(ref x, y, clock);
            }

            Raylib.DrawLineEx(new Vector2(40, y + 25), new Vector2(WindowWidth - 40, y + 25), 1.0f, new Color(50, 50, 60, 255));

            return y + 40f;
        }

        private void LoadAvailableScenes()
        {
            _availableScenes.Clear();
            
            // Try AppDomain.CurrentDomain.BaseDirectory + "Content/scenes" (production / compiled layout)
            var scenesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "scenes");
            
            // Fallback to "../Content/scenes" (development layout under TerminalApp)
            if (!Directory.Exists(scenesDir))
            {
                scenesDir = Path.GetFullPath(Path.Combine("..", "Content", "scenes"));
            }

            if (!Directory.Exists(scenesDir))
            {
                throw new DirectoryNotFoundException($"Scenes directory not found at '{scenesDir}'. Please check your Monorepo Content setup.");
            }

            var files = Directory.GetFiles(scenesDir, "*.scm");
            foreach (var file in files)
            {
                _availableScenes.Add(Path.GetFileNameWithoutExtension(file));
            }
        }

        private void UpdateDropdown(Vector2 mousePos)
        {
            var boxRect = new Rectangle(WindowWidth - 190, 30, 150, 32);
            bool hoverBox = Raylib.CheckCollisionPointRec(mousePos, boxRect);
            bool leftClick = Raylib.IsMouseButtonPressed(MouseButton.Left);

            if (leftClick)
            {
                if (hoverBox)
                {
                    _isDropdownOpen = !_isDropdownOpen;
                }
                else if (_isDropdownOpen)
                {
                    for (int i = 0; i < _availableScenes.Count; i++)
                    {
                        var optRect = new Rectangle(boxRect.X, boxRect.Y + boxRect.Height + i * 32, boxRect.Width, 32);
                        if (Raylib.CheckCollisionPointRec(mousePos, optRect))
                        {
                            var selectedScene = _availableScenes[i];
                            _gameState.Set("location", selectedScene);
                            break;
                        }
                    }
                    _isDropdownOpen = false;
                }
            }
        }

        private void DrawDropdown(Vector2 mousePos)
        {
            var boxRect = new Rectangle(WindowWidth - 190, 30, 150, 32);
            bool hoverBox = Raylib.CheckCollisionPointRec(mousePos, boxRect);

            Color boxBgColor = hoverBox ? new Color(50, 50, 70, 255) : new Color(30, 30, 40, 255);
            Color boxOutlineColor = _isDropdownOpen ? new Color(130, 130, 220, 255) : new Color(70, 70, 90, 255);
            
            Raylib.DrawRectangleRounded(boxRect, 0.2f, 4, boxBgColor);
            Raylib.DrawRectangleRoundedLinesEx(boxRect, 0.2f, 4, 1.5f, boxOutlineColor);

            string currentScene = _sceneManager.CurrentSceneName;
            FontManager.DrawText(currentScene, boxRect.X + 12, boxRect.Y + 6, 15, Color.White);
            FontManager.DrawText("v", boxRect.X + boxRect.Width - 22, boxRect.Y + 6, 14, new Color(150, 150, 170, 255));

            if (_isDropdownOpen)
            {
                for (int i = 0; i < _availableScenes.Count; i++)
                {
                    var optRect = new Rectangle(boxRect.X, boxRect.Y + boxRect.Height + i * 32, boxRect.Width, 32);
                    bool hoverOpt = Raylib.CheckCollisionPointRec(mousePos, optRect);

                    Color optBgColor = hoverOpt ? new Color(70, 70, 95, 255) : new Color(25, 25, 35, 255);
                    Color optTextColor = hoverOpt ? Color.White : new Color(180, 180, 200, 255);

                    Raylib.DrawRectangleRec(optRect, optBgColor);
                    if (_availableScenes[i] == currentScene)
                    {
                        Raylib.DrawRectangle((int)optRect.X, (int)optRect.Y, 4, (int)optRect.Height, new Color(100, 100, 250, 255));
                    }
                    
                    FontManager.DrawText(_availableScenes[i], optRect.X + 12, optRect.Y + 6, 15, optTextColor);

                    if (i < _availableScenes.Count - 1)
                    {
                        Raylib.DrawLineEx(new Vector2(optRect.X, optRect.Y + optRect.Height), 
                                         new Vector2(optRect.X + optRect.Width, optRect.Y + optRect.Height), 
                                         1f, new Color(45, 45, 55, 255));
                    }
                }

                var listRect = new Rectangle(boxRect.X, boxRect.Y + boxRect.Height, boxRect.Width, _availableScenes.Count * 32);
                Raylib.DrawRectangleLinesEx(listRect, 1.5f, boxOutlineColor);
            }
        }
    }

    public class RollResult
    {
        public string ActionName { get; set; } = string.Empty;
        public int ChosenDie { get; set; }
        public List<int> RandomDice { get; set; } = new List<int>();
        public int FinalValue { get; set; }
        public string Outcome { get; set; } = string.Empty;
    }
}
