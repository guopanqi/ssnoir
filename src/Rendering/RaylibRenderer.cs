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
        private GameNode? _activeActionChoiceNode = null;
        private RollResult? _activeRollResult = null;
        private string _uiNotification = "";
        private float _uiNotificationTimer = 0f;

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
            };

            _sceneManager.OnWorldRefreshed += ResolveNavigationStack;
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
            bool inputBlocked = _activeActionChoiceNode != null || _activeRollResult != null;
            var activeMousePos = inputBlocked ? new Vector2(-100f, -100f) : mousePos;

            // Handle ESC key to return to the parent node
            if (!inputBlocked && Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                if (_navigationStack.Count > 0)
                {
                    _navigationStack.RemoveAt(_navigationStack.Count - 1);
                    ResolveNavigationStack();
                }
            }

            if (!inputBlocked)
            {
                UpdateDropdown(mousePos);
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
            DrawStatusPanel();

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
                    _navigationStack.RemoveAt(_navigationStack.Count - 1);
                    ResolveNavigationStack();
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
            float cardHeight = 100f;
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

                bool clicked = CardWidget.DrawCard(bounds, node.Name, typeLabel, isHovered, node.Clocks, isFlipped, backText);

                if (clicked)
                {
                    if (node.HasChildren)
                    {
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
                        else
                        {
                            string missingReason;
                            if (!CheckRequirements(node, out missingReason))
                            {
                                TriggerNotification(missingReason);
                            }
                            else
                            {
                                bool requiresDie = false;
                                foreach (var cost in node.Requires)
                                {
                                    if (cost.Type == "die")
                                    {
                                        requiresDie = true;
                                        break;
                                    }
                                }

                                if (requiresDie)
                                {
                                    _activeActionChoiceNode = node;
                                }
                                else
                                {
                                    ExecuteActionWithoutDie(node);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void DrawStatusPanel()
        {
            float panelY = WindowHeight - 70;
            
            // Draw Panel background
            Raylib.DrawRectangle(0, (int)panelY, WindowWidth, 70, new Color(15, 15, 20, 255));
            Raylib.DrawLineEx(new Vector2(0, panelY), new Vector2(WindowWidth, panelY), 1.5f, new Color(40, 40, 50, 255));

            int money = _gameState.Get<int>("money");
            int health = _gameState.Get<int>("health");
            string location = _gameState.Get<string>("location");

            Color textColor = new Color(200, 200, 220, 255);
            Color valueColor = new Color(150, 150, 250, 255);

            // Column 1 (x=30): Money & Health on two rows
            FontManager.DrawText("钱金: ", 30, panelY + 15, 15, textColor);
            FontManager.DrawText($"${money}", 80, panelY + 15, 15, valueColor);

            FontManager.DrawText("健康: ", 30, panelY + 40, 15, textColor);
            FontManager.DrawText($"{health}%", 80, panelY + 40, 15, new Color(250, 100, 100, 255));

            // Column 2 (x=160): Location
            FontManager.DrawText("场景: ", 160, panelY + 25, 15, textColor);
            FontManager.DrawText(location.ToUpper(), 210, panelY + 25, 15, new Color(100, 220, 100, 255));

            // Column 3 (x=330): Action Dice
            FontManager.DrawText("行动力: ", 330, panelY + 25, 15, textColor);
            var dice = _gameState.Get<List<object>>("action-dice");
            if (dice != null)
            {
                for (int i = 0; i < dice.Count; i++)
                {
                    float dieX = 390 + i * 32;
                    float dieY = panelY + 21;

                    Raylib.DrawRectangleRounded(new Rectangle(dieX, dieY, 26, 26), 0.2f, 4, new Color(45, 45, 60, 255));
                    Raylib.DrawRectangleRoundedLinesEx(new Rectangle(dieX, dieY, 26, 26), 0.2f, 4, 1f, new Color(100, 100, 130, 255));

                    string numStr = dice[i]?.ToString() ?? "0";
                    int numW = FontManager.MeasureTextWidth(numStr, 14);
                    FontManager.DrawText(numStr, dieX + (26 - numW) / 2f, dieY + 5, 14, Color.White);
                }
            }

            // Column 4 (x=530): Inventory
            FontManager.DrawText("物品栏: ", 530, panelY + 15, 15, textColor);

            var items = new List<string>();
            foreach (var kvp in _gameState.GetAllStates())
            {
                if (kvp.Key.StartsWith("item:"))
                {
                    int qty = 0;
                    if (kvp.Value is double d) qty = (int)d;
                    else if (kvp.Value is long l) qty = (int)l;
                    else if (kvp.Value is int valInt) qty = valInt;

                    if (qty > 0)
                    {
                        items.Add($"{kvp.Key.Substring(5)} x{qty}");
                    }
                }
            }
            string inventoryStr = items.Count > 0 ? string.Join(", ", items) : "空";
            FontManager.DrawText(inventoryStr, 530, panelY + 40, 13, new Color(160, 160, 180, 255));
        }

        private bool CheckRequirements(GameNode node, out string missingReason)
        {
            missingReason = "";
            if (node.Requires == null || node.Requires.Count == 0)
                return true;

            foreach (var cost in node.Requires)
            {
                if (cost.Type == "item")
                {
                    int owned = 0;
                    if (cost.ItemName == "金钱")
                    {
                        owned = _gameState.Get<int>("money");
                    }
                    else
                    {
                        owned = _gameState.Get<int>("item:" + cost.ItemName, 0);
                    }

                    if (owned < cost.Qty)
                    {
                        missingReason = $"缺少物品: {cost.ItemName} (需要 {cost.Qty}, 拥有 {owned})";
                        return false;
                    }
                }
                else if (cost.Type == "die")
                {
                    var dice = _gameState.Get<List<object>>("action-dice");
                    if (dice == null || dice.Count < 1)
                    {
                        missingReason = "行动力骰子不足";
                        return false;
                    }
                }
            }
            return true;
        }

        private void ConsumeRequirements(GameNode node)
        {
            if (node.Requires == null) return;
            foreach (var cost in node.Requires)
            {
                if (cost.Type == "item")
                {
                    if (cost.ItemName == "金钱")
                    {
                        int owned = _gameState.Get<int>("money");
                        _gameState.Set("money", Math.Max(0, owned - cost.Qty));
                    }
                    else
                    {
                        int owned = _gameState.Get<int>("item:" + cost.ItemName, 0);
                        _gameState.Set("item:" + cost.ItemName, Math.Max(0, owned - cost.Qty));
                    }
                }
            }
        }

        private void ConsumeDie(int dieIndex)
        {
            var dice = _gameState.Get<List<object>>("action-dice");
            if (dice != null && dieIndex >= 0 && dieIndex < dice.Count)
            {
                dice.RemoveAt(dieIndex);
                _gameState.Set("action-dice", dice);
            }
        }

        private void ExecuteActionWithoutDie(GameNode node)
        {
            ConsumeRequirements(node);

            if (node.Resolve?.Type == ResolveType.Instant)
            {
                node.Resolve.Effect?.Invoke();
            }

            _sceneManager.OnActionExecuted();
        }

        private void ExecuteActionWithDie(GameNode node, int chosenDieVal, int dieIndex)
        {
            ConsumeDie(dieIndex);
            ConsumeRequirements(node);

            if (node.Resolve?.Type == ResolveType.Roll)
            {
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
            else if (node.Resolve?.Type == ResolveType.Instant)
            {
                node.Resolve.Effect?.Invoke();
                _sceneManager.OnActionExecuted();
            }
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

            // 2. Die Selection Modal
            if (_activeActionChoiceNode != null)
            {
                Raylib.DrawRectangle(0, 0, WindowWidth, WindowHeight, new Color(0, 0, 0, 180));

                float modalW = 340;
                float modalH = 200;
                float modalX = (WindowWidth - modalW) / 2f;
                float modalY = (WindowHeight - modalH) / 2f;
                var modalRect = new Rectangle(modalX, modalY, modalW, modalH);

                Raylib.DrawRectangleRounded(modalRect, 0.1f, 8, new Color(30, 30, 40, 255));
                Raylib.DrawRectangleRoundedLinesEx(modalRect, 0.1f, 8, 2f, new Color(80, 80, 100, 255));

                string title = "选择放入的行动力骰子";
                int titleW = FontManager.MeasureTextWidth(title, 18);
                FontManager.DrawText(title, modalX + (modalW - titleW) / 2f, modalY + 20, 18, Color.White);

                string sub = $"判定项目: {_activeActionChoiceNode.Resolve?.SkillName ?? "无"}";
                int subW = FontManager.MeasureTextWidth(sub, 12);
                FontManager.DrawText(sub, modalX + (modalW - subW) / 2f, modalY + 50, 12, new Color(150, 150, 170, 255));

                var dice = _gameState.Get<List<object>>("action-dice");
                if (dice != null && dice.Count > 0)
                {
                    float buttonY = modalY + 80;
                    float buttonW = 40;
                    float buttonH = 40;
                    float spacing = 15;
                    float startX = modalX + (modalW - (dice.Count * buttonW + (dice.Count - 1) * spacing)) / 2f;

                    for (int i = 0; i < dice.Count; i++)
                    {
                        float btnX = startX + i * (buttonW + spacing);
                        var btnRect = new Rectangle(btnX, buttonY, buttonW, buttonH);
                        bool hover = Raylib.CheckCollisionPointRec(mousePos, btnRect);

                        Color btnBg = hover ? new Color(70, 70, 100, 255) : new Color(45, 45, 60, 255);
                        Color btnBorder = hover ? new Color(150, 150, 250, 255) : new Color(80, 80, 110, 255);

                        Raylib.DrawRectangleRounded(btnRect, 0.2f, 4, btnBg);
                        Raylib.DrawRectangleRoundedLinesEx(btnRect, 0.2f, 4, 1.5f, btnBorder);

                        int val = 0;
                        if (dice[i] is double d) val = (int)d;
                        else if (dice[i] is long l) val = (int)l;
                        else if (dice[i] is int valInt) val = valInt;

                        string valStr = val.ToString();
                        int valW = FontManager.MeasureTextWidth(valStr, 18);
                        FontManager.DrawText(valStr, btnX + (buttonW - valW) / 2f, buttonY + 11, 18, Color.White);

                        if (hover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                        {
                            var selectedNode = _activeActionChoiceNode;
                            _activeActionChoiceNode = null;
                            ExecuteActionWithDie(selectedNode, val, i);
                            break;
                        }
                    }
                }
                else
                {
                    string emptyStr = "无可用行动力";
                    int emptyW = FontManager.MeasureTextWidth(emptyStr, 14);
                    FontManager.DrawText(emptyStr, modalX + (modalW - emptyW) / 2f, modalY + 90, 14, new Color(220, 100, 100, 255));
                }

                float cancelW = 80;
                float cancelH = 28;
                float cancelX = modalX + (modalW - cancelW) / 2f;
                float cancelY = modalY + modalH - 45;
                var cancelRect = new Rectangle(cancelX, cancelY, cancelW, cancelH);
                bool cancelHover = Raylib.CheckCollisionPointRec(mousePos, cancelRect);

                Color cBg = cancelHover ? new Color(70, 50, 55, 255) : new Color(50, 40, 42, 255);
                Color cBorder = cancelHover ? new Color(200, 100, 100, 255) : new Color(110, 80, 85, 255);

                Raylib.DrawRectangleRounded(cancelRect, 0.2f, 4, cBg);
                Raylib.DrawRectangleRoundedLinesEx(cancelRect, 0.2f, 4, 1.5f, cBorder);

                string cancelText = "取消";
                int cTextW = FontManager.MeasureTextWidth(cancelText, 14);
                FontManager.DrawText(cancelText, cancelX + (cancelW - cTextW) / 2f, cancelY + 6, 14, new Color(220, 180, 185, 255));

                if (cancelHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    _activeActionChoiceNode = null;
                }
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
            var scenesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scenes");
            if (!Directory.Exists(scenesDir))
            {
                scenesDir = Path.GetFullPath("scenes");
            }

            if (Directory.Exists(scenesDir))
            {
                var files = Directory.GetFiles(scenesDir, "*.scm");
                foreach (var file in files)
                {
                    _availableScenes.Add(Path.GetFileNameWithoutExtension(file));
                }
            }

            if (_availableScenes.Count == 0)
            {
                _availableScenes.Add("home");
                _availableScenes.Add("office");
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
