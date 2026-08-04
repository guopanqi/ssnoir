#nullable enable
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // Unified debug panel: runtime test controls + Save/Load + Scene switch.
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

        public static bool IsOpen => _isOpen;

        public static (Rect ToggleRect, Rect PanelRect) GetRects(TopHudLayout topHud)
        {
            var toggleRect = topHud.DebugToggle;

            float itemH = 26f;
            float panelW = 260f;
            float panelX = toggleRect.xMax - panelW;
            float panelY = toggleRect.yMax + 4f;
            float slotsHeight = 20f + SaveManager.SlotCount * 28f + 14f;
            float cameraSectionHeight = 52f;
            float panelH = 8f + slotsHeight + cameraSectionHeight + _scenes.Count * itemH + 8f;
            return (toggleRect, new Rect(panelX, panelY, panelW, panelH));
        }

        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui, TopHudLayout topHud)
        {
            var (toggleRect, panelRect) = GetRects(topHud);

            // Toggle button
            Color toggleBg = _isOpen
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f)
                : IMGUIStyles.HudBg;
            Color toggleBorder = _isOpen
                ? IMGUIStyles.Gold
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f);
            GUI.color = toggleBg;
            GUI.DrawTexture(toggleRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(toggleRect, 1f, toggleBorder);

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(13),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = _isOpen ? IMGUIStyles.Gold : IMGUIStyles.TextPrimary }
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
            float panelW = panelRect.width;
            float panelX = panelRect.x;
            float panelY = panelRect.y;

            GUI.color = new Color(IMGUIStyles.HudBg.r, IMGUIStyles.HudBg.g, IMGUIStyles.HudBg.b, IMGUIStyles.ModalOpacity);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panelRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));

            // Slots Section
            float curY = panelY + 8f;
            var mutedStyle = new GUIStyle(labelStyle) { fontSize = IMGUIStyles.FontSize(11),
                normal = { textColor = IMGUIStyles.TextDisabled } };
            GUI.Label(new Rect(panelX + 8, curY + 2f, panelW, 18f), "存档管理", mutedStyle);
            curY += 20f;

            for (int slot = 1; slot <= SaveManager.SlotCount; slot++)
            {
                string slotPath = SaveManager.GetSlotFilePath(slot);
                string saveTime = SaveManager.GetSaveTime(slotPath);
                bool hasSave = !string.IsNullOrEmpty(saveTime);

                GUI.Label(new Rect(panelX + 8f, curY + 4f, 50f, 20f), $"槽位 {slot}", labelStyle);

                string timeStr = hasSave ? saveTime : "（空）";
                var timeStyle = new GUIStyle(labelStyle) {
                    normal = { textColor = hasSave ? IMGUIStyles.TextPrimary : IMGUIStyles.TextDisabled }
                };
                GUI.Label(new Rect(panelX + 52f, curY + 4f, 130f, 20f), timeStr, timeStyle);

                var rectSave = new Rect(panelX + panelW - 8f - 64f, curY + 2f, 30f, 22f);
                var rectLoad = new Rect(panelX + panelW - 8f - 30f, curY + 2f, 30f, 22f);

                if (IMGUIButton.Draw(rectSave, "存", ui,
                        new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f),
                        new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f), labelStyle))
                {
                    gameManager.SaveGame(slotPath);
                    Event.current.Use();
                }

                if (IMGUIButton.Draw(rectLoad, "读", ui,
                        new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f),
                        new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f), labelStyle, hasSave))
                {
                    gameManager.LoadGame(slotPath);
                    _isOpen = false;
                    Event.current.Use();
                    return;
                }

                curY += 28f;
            }

            // Camera animation test mode
            float sepY = curY + 6f;
            IMGUIStyles.DrawLine(new Vector2(panelX + 8, sepY), new Vector2(panelX + panelW - 8, sepY),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
            GUI.Label(new Rect(panelX + 8, sepY + 2f, panelW, 18f), "镜头测试", mutedStyle);

            float cameraRowY = sepY + 20f;
            bool instantCuts = MotionSettings.DebugInstantCameraCuts;
            var cameraLabelStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = IMGUIStyles.TextPrimary }
            };
            GUI.Label(new Rect(panelX + 8f, cameraRowY + 2f, 120f, 22f),
                "自动运镜", cameraLabelStyle);

            var cameraModeStyle = new GUIStyle(labelStyle)
            {
                normal = { textColor = instantCuts ? IMGUIStyles.Gold : IMGUIStyles.TextSecondary }
            };
            var cameraModeRect = new Rect(panelX + panelW - 8f - 64f, cameraRowY + 2f, 64f, 22f);
            if (IMGUIButton.Draw(cameraModeRect, instantCuts ? "0 秒" : "正常", ui,
                    instantCuts ? IMGUIStyles.Gold : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f),
                    new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f), cameraModeStyle))
            {
                MotionSettings.DebugInstantCameraCuts = !instantCuts;
                Event.current.Use();
            }

            // Scene switch section
            sepY = cameraRowY + itemH + 6f;
            IMGUIStyles.DrawLine(new Vector2(panelX + 8, sepY), new Vector2(panelX + panelW - 8, sepY),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
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
                    GUI.color = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f);
                    GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                if (isCurrent)
                {
                    GUI.color = IMGUIStyles.Gold;
                    GUI.DrawTexture(new Rect(panelX + 4, itemRect.y + 2, 3, itemRect.height - 4), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                var itemStyle = new GUIStyle(labelStyle)
                {
                    normal = { textColor = isCurrent ? IMGUIStyles.Gold : (isHovered ? IMGUIStyles.TextPrimary : IMGUIStyles.TextSecondary) }
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
