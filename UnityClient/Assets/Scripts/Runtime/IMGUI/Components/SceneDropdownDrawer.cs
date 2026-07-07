#nullable enable
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SSNoir.IMGUI
{
    public static class SceneDropdownDrawer
    {
        private struct DropdownItem
        {
            public string Name;
            public bool IsHeader;
            public string SceneName;
        }

        private static bool _isOpen = false;
        private static readonly List<DropdownItem> _dropdownItems = new List<DropdownItem>();
        private static bool _initialized = false;

        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            if (!_initialized)
            {
                LoadScenes(gameManager);
                _initialized = true;
            }

            float boxW = 150;
            float boxH = 32;
            float boxX = UIScale.VW - 200;
            float boxY = 30;
            var boxRect = new Rect(boxX, boxY, boxW, boxH);

            bool hoverBox = ui.CanHover(boxRect);

            // Toggle on click
            if (ui.WasClicked(boxRect))
            {
                _isOpen = !_isOpen;
                Event.current.Use();
            }
            else if (_isOpen && Event.current.type == EventType.MouseDown && Event.current.button == 0 && !ui.IsLocked)
            {
                // Check if clicked outside dropdown
                float listH = _dropdownItems.Count * 32;
                var listRect = new Rect(boxX, boxY + boxH, boxW, listH);
                if (!listRect.Contains(ui.Mouse))
                {
                    _isOpen = false;
                }
            }

            // Draw box：黑底 HUD；展开=金描边（选中态），默认=Paper 40%，悬停提亮
            Color boxBg = hoverBox
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f)
                : IMGUIStyles.HudBg;
            Color boxBorder = _isOpen
                ? IMGUIStyles.Gold
                : hoverBox
                    ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 1f)
                    : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f);

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
                for (int i = 0; i < _dropdownItems.Count; i++)
                {
                    var optRect = new Rect(boxX, boxY + boxH + i * 32, boxW, 32);
                    var item = _dropdownItems[i];

                    if (item.IsHeader)
                    {
                        GUI.color = IMGUIStyles.HudBg;
                        GUI.DrawTexture(optRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;

                        var headerStyle = new GUIStyle(IMGUIStyles.HelpTip);
                        headerStyle.alignment = TextAnchor.MiddleCenter;
                        headerStyle.normal.textColor = IMGUIStyles.TextDisabled;
                        headerStyle.fontSize = 11;
                        GUI.Label(optRect, item.Name, headerStyle);
                    }
                    else
                    {
                        bool hoverOpt = ui.CanHover(optRect);
                        Color optBg = hoverOpt
                            ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f)
                            : IMGUIStyles.HudBg;
                        Color optText = hoverOpt ? IMGUIStyles.TextPrimary : IMGUIStyles.TextSecondary;

                        GUI.color = optBg;
                        GUI.DrawTexture(optRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;

                        if (item.SceneName == currentScene)
                        {
                            // 当前所在场景 = 金（选中语义）
                            GUI.color = IMGUIStyles.Gold;
                            GUI.DrawTexture(new Rect(optRect.x, optRect.y, 4, optRect.height), Texture2D.whiteTexture);
                            GUI.color = Color.white;
                        }

                        var itemStyle = new GUIStyle(IMGUIStyles.DropdownItem);
                        itemStyle.normal.textColor = optText;
                        GUI.Label(new Rect(optRect.x + 12, optRect.y + 6, optRect.width - 16, 20), item.Name, itemStyle);

                        if (ui.WasClicked(optRect))
                        {
                            gameManager.OnSceneButtonClicked(item.SceneName);
                            _isOpen = false;
                            Event.current.Use();
                        }
                    }

                    if (i < _dropdownItems.Count - 1)
                    {
                        IMGUIStyles.DrawLine(new Vector2(optRect.x, optRect.y + optRect.height), new Vector2(optRect.x + optRect.width, optRect.y + optRect.height),
                            new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.20f), 1f);
                    }
                }

                // List outline
                float listH = _dropdownItems.Count * 32;
                var listRect = new Rect(boxX, boxY + boxH, boxW, listH);
                IMGUIStyles.DrawOutline(listRect, 1f, boxBorder);
            }
        }

        private static void LoadScenes(SSNoirGameManager gameManager)
        {
            _dropdownItems.Clear();
            _dropdownItems.Add(new DropdownItem { Name = "--- WORLD ---", IsHeader = true });
            _dropdownItems.Add(new DropdownItem { Name = "world", SceneName = "world" });
            
            _dropdownItems.Add(new DropdownItem { Name = "--- OTHERS ---", IsHeader = true });

            string searchDir = Path.Combine(Application.streamingAssetsPath, "Content", "scenes", "encounters");
            if (Directory.Exists(searchDir))
            {
                foreach (string file in Directory.GetFiles(searchDir, "*.scm"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    _dropdownItems.Add(new DropdownItem { Name = name, SceneName = name });
                }
            }
            else
            {
                foreach (var asset in Resources.LoadAll<TextAsset>("Content/scenes/encounters"))
                {
                    string name = Path.GetFileNameWithoutExtension(asset.name);
                    _dropdownItems.Add(new DropdownItem { Name = name, SceneName = name });
                }
            }
        }
    }
}
