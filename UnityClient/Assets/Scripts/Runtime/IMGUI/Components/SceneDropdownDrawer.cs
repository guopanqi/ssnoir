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
                LoadScenes(gameManager);
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
            Color boxBg = hoverBox ? IMGUIStyles.DropdownHover : IMGUIStyles.DropdownBg;
            Color boxBorder = _isOpen ? IMGUIStyles.PrimaryColor : IMGUIStyles.OutlineColor;

            GUI.color = boxBg;
            GUI.DrawTexture(boxRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(boxRect, 1f, boxBorder);

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

                    Color optBg = hoverOpt ? IMGUIStyles.DropdownHover : IMGUIStyles.SlotEmpty;
                    Color optText = hoverOpt ? Color.white : IMGUIStyles.OnSurfaceVariant;

                    GUI.color = optBg;
                    GUI.DrawTexture(optRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    if (_availableScenes[i] == currentScene)
                    {
                        GUI.color = IMGUIStyles.PrimaryColor;
                        GUI.DrawTexture(new Rect(optRect.x, optRect.y, 4, optRect.height), Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }

                    var itemStyle = new GUIStyle(IMGUIStyles.DropdownItem);
                    itemStyle.normal.textColor = optText;
                    GUI.Label(new Rect(optRect.x + 12, optRect.y + 6, optRect.width - 16, 20), _availableScenes[i], itemStyle);

                    if (i < _availableScenes.Count - 1)
                    {
                        IMGUIStyles.DrawLine(new Vector2(optRect.x, optRect.y + optRect.height), new Vector2(optRect.x + optRect.width, optRect.y + optRect.height), IMGUIStyles.OutlineVariantColor, 1f);
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
                IMGUIStyles.DrawOutline(listRect, 1f, boxBorder);
            }
        }

        private static void LoadScenes(SSNoirGameManager gameManager)
        {
            _availableScenes.Clear();
            _availableScenes.AddRange(gameManager.LoadAvailableSceneNames());
        }
    }
}
