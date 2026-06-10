#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    public static class SceneDropdownDrawer
    {
        private static bool _isOpen = false;
        private static readonly List<string> _availableScenes = new List<string>();
        private static bool _initialized = false;

        public static void Draw(SSNoirGameManager gameManager, Vector2 mousePos)
        {
            if (!_initialized)
            {
                LoadScenes();
                _initialized = true;
            }

            float boxW = 150;
            float boxH = 32;
            float boxX = Screen.width - 200;
            float boxY = 30;
            var boxRect = new Rect(boxX, boxY, boxW, boxH);

            bool hoverBox = boxRect.Contains(mousePos);

            // Toggle on click
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (hoverBox)
                {
                    _isOpen = !_isOpen;
                    Event.current.Use();
                }
                else if (_isOpen)
                {
                    // Check if clicked outside dropdown
                    float listH = _availableScenes.Count * 32;
                    var listRect = new Rect(boxX, boxY + boxH, boxW, listH);
                    if (!listRect.Contains(mousePos))
                    {
                        _isOpen = false;
                    }
                }
            }

            // Draw box
            Color boxBg = hoverBox ? new Color(0.2f, 0.2f, 0.3f, 1f) : IMGUIStyles.DropdownBg;
            Color boxBorder = _isOpen ? new Color(0.5f, 0.5f, 0.7f, 1f) : new Color(0.3f, 0.3f, 0.45f, 1f);

            GUI.color = boxBg;
            GUI.DrawTexture(boxRect, Texture2D.whiteTexture);
            GUI.color = boxBorder;
            DrawOutline(boxRect, 1);
            GUI.color = Color.white;

            string currentScene = gameManager.SceneManager.CurrentSceneName;
            GUI.Label(new Rect(boxX + 12, boxY + 6, boxW - 30, 20), currentScene, IMGUIStyles.DropdownCurrent);
            GUI.Label(new Rect(boxX + boxW - 22, boxY + 6, 20, 20), "v", IMGUIStyles.DropdownCurrent);

            // Draw dropdown list
            if (_isOpen)
            {
                for (int i = 0; i < _availableScenes.Count; i++)
                {
                    var optRect = new Rect(boxX, boxY + boxH + i * 32, boxW, 32);
                    bool hoverOpt = optRect.Contains(mousePos);

                    Color optBg = hoverOpt ? IMGUIStyles.DropdownHover : new Color(0.08f, 0.08f, 0.12f, 1f);
                    Color optText = hoverOpt ? Color.white : new Color(0.7f, 0.7f, 0.8f, 1f);

                    GUI.color = optBg;
                    GUI.DrawTexture(optRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    if (_availableScenes[i] == currentScene)
                    {
                        GUI.color = new Color(0.4f, 0.4f, 0.8f, 1f);
                        GUI.DrawTexture(new Rect(optRect.x, optRect.y, 4, optRect.height), Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }

                    var itemStyle = new GUIStyle(IMGUIStyles.DropdownItem);
                    itemStyle.normal.textColor = optText;
                    GUI.Label(new Rect(optRect.x + 12, optRect.y + 6, optRect.width - 16, 20), _availableScenes[i], itemStyle);

                    if (i < _availableScenes.Count - 1)
                    {
                        GUI.color = new Color(0.15f, 0.15f, 0.2f, 1f);
                        GUI.DrawTexture(new Rect(optRect.x, optRect.y + optRect.height, optRect.width, 1), Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }

                    if (hoverOpt && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        gameManager.OnSceneButtonClicked(_availableScenes[i]);
                        _isOpen = false;
                        Event.current.Use();
                    }
                }

                // List outline
                float listH = _availableScenes.Count * 32;
                var listRect = new Rect(boxX, boxY + boxH, boxW, listH);
                GUI.color = boxBorder;
                DrawOutline(listRect, 1);
                GUI.color = Color.white;
            }
        }

        private static void LoadScenes()
        {
            _availableScenes.Clear();
            var scenesDir = System.IO.Path.Combine(Application.streamingAssetsPath, "Content", "scenes");
            if (System.IO.Directory.Exists(scenesDir))
            {
                var files = System.IO.Directory.GetFiles(scenesDir, "*.scm");
                foreach (var file in files)
                {
                    _availableScenes.Add(System.IO.Path.GetFileNameWithoutExtension(file));
                }
            }
        }

        private static void DrawOutline(Rect rect, int thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }
    }
}
