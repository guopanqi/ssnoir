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

        private const int WindowWidth = 800;
        private const int WindowHeight = 600;

        public RaylibRenderer(SceneManager sceneManager, GameState gameState)
        {
            _sceneManager = sceneManager;
            _gameState = gameState;

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
            Raylib.InitWindow(WindowWidth, WindowHeight, "SSNoir Prototype");
            Raylib.SetTargetFPS(60);

            _sceneManager.LoadScene(_gameState.Get<string>("location"));

            while (!Raylib.WindowShouldClose())
            {
                UpdateAndDraw();
            }

            Raylib.CloseWindow();
        }

        private void UpdateAndDraw()
        {
            var mousePos = Raylib.GetMousePosition();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(20, 20, 25, 255));

            // ── Draw Navigation / Breadcrumbs ──
            DrawNavigation(mousePos);

            // ── Draw Node Cards ──
            DrawCards(mousePos);

            // ── Draw Bottom Status Panel ──
            DrawStatusPanel();

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
                Raylib.DrawText("< 返回", (int)(startX + 18), (int)(startY + 8), 16, textColor);

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
            Raylib.DrawText(breadcrumbText, (int)startX, (int)(startY + 8), 16, new Color(180, 180, 200, 255));

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
            Raylib.DrawText("钱金: ", startX, (int)(panelY + 25), 18, textColor);
            Raylib.DrawText($"${money}", startX + 50, (int)(panelY + 25), 18, valueColor);

            // Draw Health
            Raylib.DrawText("健康: ", startX + spacing, (int)(panelY + 25), 18, textColor);
            Raylib.DrawText($"{health}%", startX + spacing + 50, (int)(panelY + 25), 18, new Color(250, 100, 100, 255));

            // Draw Location
            Raylib.DrawText("场景: ", startX + spacing * 2, (int)(panelY + 25), 18, textColor);
            Raylib.DrawText(location.ToUpper(), startX + spacing * 2 + 50, (int)(panelY + 25), 18, new Color(100, 220, 100, 255));
        }
    }
}
