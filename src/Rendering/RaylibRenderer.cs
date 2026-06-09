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

            UpdateDropdown(mousePos);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 20, 25, 255));

            // ── Draw Navigation / Breadcrumbs ──
            DrawNavigation(mousePos);

            // ── Draw Node Cards ──
            DrawCards(mousePos);

            // ── Draw Bottom Status Panel ──
            DrawStatusPanel();

            // ── Draw Dropdown ──
            DrawDropdown(mousePos);

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

        private void DrawCards(Vector2 mousePos)
        {
            float startX = 40f;
            float startY = 110f;
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

                string typeLabel = node.HasChildren ? "场所" : "行动";
                bool clicked = CardWidget.DrawCard(bounds, node.Name, typeLabel, isHovered);

                if (clicked)
                {
                    if (node.HasChildren)
                    {
                        _navigationStack.Add(node);
                        ResolveNavigationStack();
                    }
                    else if (node.HasEffect)
                    {
                        _sceneManager.ExecuteEffect(node);
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

            int startX = 50;
            int spacing = 220;

            // Draw Money
            FontManager.DrawText("钱金: ", startX, panelY + 25, 18, textColor);
            FontManager.DrawText($"${money}", startX + 50, panelY + 25, 18, valueColor);

            // Draw Health
            FontManager.DrawText("健康: ", startX + spacing, panelY + 25, 18, textColor);
            FontManager.DrawText($"{health}%", startX + spacing + 50, panelY + 25, 18, new Color(250, 100, 100, 255));

            // Draw Location
            FontManager.DrawText("场景: ", startX + spacing * 2, panelY + 25, 18, textColor);
            FontManager.DrawText(location.ToUpper(), startX + spacing * 2 + 50, panelY + 25, 18, new Color(100, 220, 100, 255));

            // Draw Clocks
            DrawClocks(615, panelY);
        }

        private void DrawClocks(float startX, float panelY)
        {
            var clocks = _sceneManager.CurrentClocks;
            if (clocks == null || clocks.Count == 0) return;

            float x = startX;
            float y = panelY + 25;

            Color textColor = new Color(200, 200, 220, 255);
            Color activeColor = new Color(130, 130, 250, 255);
            Color inactiveColor = new Color(45, 45, 55, 255);
            Color outlineColor = new Color(70, 70, 90, 255);

            foreach (var clock in clocks)
            {
                // Draw Label
                FontManager.DrawText(clock.Label, x, y, 16, textColor);
                
                int labelWidth = FontManager.MeasureTextWidth(clock.Label, 16);
                float segmentsX = x + labelWidth + 8;

                // Draw segments
                for (int i = 0; i < clock.Max; i++)
                {
                    var segRect = new Rectangle(segmentsX + i * 14, y + 2, 10, 10);
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

                x += labelWidth + 8 + clock.Max * 14 + 16;
            }
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
}
