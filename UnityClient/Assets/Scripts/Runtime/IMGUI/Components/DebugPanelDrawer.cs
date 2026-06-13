#nullable enable
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // Unified debug panel: Save/Load + Scene switch.
    // Mirrors TerminalApp's DrawDebugMenu in RaylibRenderer.
    public static class DebugPanelDrawer
    {
        private struct SceneItem
        {
            public string Name;
            public bool IsHeader;
            public string SceneName;
        }

        private static bool _isOpen = false;
        private static readonly List<SceneItem> _scenes = new List<SceneItem>();

        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            float btnW = 70f;
            float btnH = 32f;
            float btnX = Screen.width - btnW - 10f;
            float btnY = 30f;
            var toggleRect = new Rect(btnX, btnY, btnW, btnH);

            // Toggle button
            Color toggleBg = _isOpen ? new Color(0.18f, 0.18f, 0.35f, 1f) : IMGUIStyles.DropdownBg;
            Color toggleBorder = _isOpen ? IMGUIStyles.PrimaryColor : IMGUIStyles.OutlineColor;
            GUI.color = toggleBg;
            GUI.DrawTexture(toggleRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(toggleRect, 1f, toggleBorder);

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _isOpen ? IMGUIStyles.PrimaryColor : IMGUIStyles.OnSurface }
            };
            GUI.Label(toggleRect, "Debug ▾", labelStyle);

            if (ui.WasClicked(toggleRect))
            {
                _isOpen = !_isOpen;
                if (_isOpen)
                {
                    LoadScenes(gameManager);
                }
                Event.current.Use();
            }

            if (!_isOpen) return;

            // Panel
            float itemH = 26f;
            float panelW = 170f;
            float panelX = btnX + btnW - panelW;
            float panelY = btnY + btnH + 4f;
            float panelH = 44f + 14f + _scenes.Count * itemH + 8f;
            var panelRect = new Rect(panelX, panelY, panelW, panelH);

            GUI.color = new Color(0.078f, 0.086f, 0.11f, 1f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panelRect, 1.5f, IMGUIStyles.OutlineColor);

            // Save / Load buttons
            float rowY = panelY + 8f;
            var saveRect = new Rect(panelX + 6f, rowY, 74f, 28f);
            var loadRect = new Rect(panelX + 86f, rowY, 74f, 28f);

            if (IMGUIButton.Draw(saveRect, "存档", ui, IMGUIStyles.OutlineColor,
                    new Color(0.2f, 0.2f, 0.35f, 1f), labelStyle))
            {
                gameManager.SaveGame();
                _isOpen = false;
                Event.current.Use();
                return;
            }

            if (IMGUIButton.Draw(loadRect, "读档", ui, IMGUIStyles.OutlineColor,
                    new Color(0.2f, 0.2f, 0.35f, 1f), labelStyle))
            {
                gameManager.LoadGame();
                _isOpen = false;
                Event.current.Use();
                return;
            }

            // Separator + label
            float sepY = rowY + 28f + 6f;
            IMGUIStyles.DrawLine(new Vector2(panelX + 8, sepY), new Vector2(panelX + panelW - 8, sepY),
                IMGUIStyles.OutlineVariantColor, 1f);
            var mutedStyle = new GUIStyle(labelStyle) { fontSize = 11,
                normal = { textColor = new Color(0.35f, 0.35f, 0.45f, 1f) } };
            GUI.Label(new Rect(panelX + 8, sepY + 2f, panelW, 18f), "切换场景", mutedStyle);

            // Scene list
            float listY = sepY + 4f + itemH * 0.5f;
            string currentScene = gameManager.SceneManager.CurrentSceneName;

            for (int i = 0; i < _scenes.Count; i++)
            {
                var item = _scenes[i];
                var itemRect = new Rect(panelX + 4f, listY + i * itemH, panelW - 8f, itemH - 2f);

                if (item.IsHeader)
                {
                    GUI.Label(new Rect(itemRect.x + 6, itemRect.y + 4, itemRect.width, itemRect.height),
                        item.Name, mutedStyle);
                    continue;
                }

                bool isHovered = ui.CanHover(itemRect);
                bool isCurrent = item.SceneName == currentScene;

                if (isHovered)
                {
                    GUI.color = new Color(0.16f, 0.16f, 0.24f, 1f);
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                if (isCurrent)
                {
                    GUI.color = IMGUIStyles.PrimaryColor;
                    GUI.DrawTexture(new Rect(panelX + 4, itemRect.y + 2, 3, itemRect.height - 4), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                var itemStyle = new GUIStyle(labelStyle)
                {
                    normal = { textColor = isCurrent ? IMGUIStyles.PrimaryColor : (isHovered ? Color.white : IMGUIStyles.OnSurfaceVariant) }
                };
                GUI.Label(new Rect(itemRect.x + 10, itemRect.y + 4, itemRect.width, itemRect.height), item.Name, itemStyle);

                if (ui.WasClicked(itemRect))
                {
                    gameManager.OnSceneButtonClicked(item.SceneName);
                    _isOpen = false;
                    Event.current.Use();
                    return;
                }
            }

            // Close when clicking outside
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && !ui.IsLocked)
            {
                if (!panelRect.Contains(ui.Mouse) && !toggleRect.Contains(ui.Mouse))
                {
                    _isOpen = false;
                    Event.current.Use();
                }
            }
        }

        public static void Reset() { _isOpen = false; }

        private static void LoadScenes(SSNoirGameManager gameManager)
        {
            _scenes.Clear();
            _scenes.Add(new SceneItem { Name = "--- 世界 ---", IsHeader = true });
            _scenes.Add(new SceneItem { Name = "world", SceneName = "world" });
            _scenes.Add(new SceneItem { Name = "--- 交锋 ---", IsHeader = true });

            string streamingDir = Path.Combine(Application.streamingAssetsPath, "Content", "scenes", "encounters");
            if (Directory.Exists(streamingDir))
            {
                foreach (string file in Directory.GetFiles(streamingDir, "*.scm"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    _scenes.Add(new SceneItem { Name = name, SceneName = name });
                }
            }
            else
            {
                foreach (var asset in Resources.LoadAll<TextAsset>("Content/scenes/encounters"))
                {
                    string name = Path.GetFileNameWithoutExtension(asset.name.Replace(".scm", ""));
                    _scenes.Add(new SceneItem { Name = name, SceneName = name });
                }
            }
        }
    }
}
