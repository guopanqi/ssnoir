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
        private static readonly List<CutsceneSequence> _sequences = new List<CutsceneSequence>();

        public static bool IsOpen => _isOpen;

        public static (Rect ToggleRect, Rect PanelRect) GetRects(TopHudLayout topHud)
        {
            var toggleRect = topHud.DebugToggle;

            float itemH = 26f;
            float panelW = 260f;
            float panelX = toggleRect.xMax - panelW;
            float panelY = toggleRect.yMax + 4f;
            float slotsHeight = 20f + SaveManager.SlotCount * 28f + 14f;
            float cameraSectionHeight = 78f;
            // 没有过场时也留一行，用来显示"场景里没有"，免得面板看起来像坏了。
            float cutsceneSectionHeight = 26f + Mathf.Max(_sequences.Count, 1) * itemH;
            float panelH = 8f + slotsHeight + cameraSectionHeight + cutsceneSectionHeight
                + _scenes.Count * itemH + 8f;
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
                }

                if (IMGUIButton.Draw(rectLoad, "读", ui,
                        new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f),
                        new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f), labelStyle, hasSave))
                {
                    gameManager.LoadGame(slotPath);
                    _isOpen = false;
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
            }

            // 过场首帧截图：渲的是纯世界，这个面板开着也不会进画面，所以就放在这儿点。
            float captureRowY = cameraRowY + itemH;
            GUI.Label(new Rect(panelX + 8f, captureRowY + 2f, 120f, 22f),
                "过场截图", cameraLabelStyle);

            var captureStyle = new GUIStyle(labelStyle)
            {
                normal = { textColor = CinematicCapture.IsCapturing ? IMGUIStyles.TextDisabled : IMGUIStyles.TextSecondary }
            };
            var captureBorder = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f);
            var captureFill = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f);

            if (IMGUIButton.Draw(new Rect(panelX + panelW - 8f - 132f, captureRowY + 2f, 64f, 22f),
                    "1080p", ui, captureBorder, captureFill, captureStyle, !CinematicCapture.IsCapturing))
            {
                CinematicCapture.Capture(gameManager, 1080);
            }

            if (IMGUIButton.Draw(new Rect(panelX + panelW - 8f - 64f, captureRowY + 2f, 64f, 22f),
                    "4K", ui, captureBorder, captureFill, captureStyle, !CinematicCapture.IsCapturing))
            {
                CinematicCapture.Capture(gameManager, 2160);
            }

            // 过场测试：列出场景里所有 CutsceneSequence，点一个就走完整套流程
            // （推第一镜 → 压黑边 → 逐镜放片子 → 收黑边 → 镜头回来）。
            float cutsceneSepY = captureRowY + itemH + 6f;
            IMGUIStyles.DrawLine(new Vector2(panelX + 8, cutsceneSepY), new Vector2(panelX + panelW - 8, cutsceneSepY),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
            GUI.Label(new Rect(panelX + 8, cutsceneSepY + 2f, panelW, 18f), "过场测试", mutedStyle);

            float cutsceneListY = cutsceneSepY + 22f;

            if (_sequences.Count == 0)
            {
                GUI.Label(new Rect(panelX + 12f, cutsceneListY + 2f, panelW - 16f, itemH),
                    "（场景里没有 CutsceneSequence）", mutedStyle);
                cutsceneListY += itemH;
            }
            else
            {
                for (int i = 0; i < _sequences.Count; i++)
                {
                    var sequence = _sequences[i];
                    var rowRect = new Rect(panelX + 4f, cutsceneListY + i * itemH, panelW - 8f, itemH - 2f);

                    // 场景切换后列表里的引用会失效，但面板可能还开着。
                    if (sequence == null)
                        continue;

                    bool hovered = ui.CanHover(rowRect);
                    if (hovered)
                    {
                        GUI.color = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f);
                        GUI.DrawTexture(rowRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }

                    var rowStyle = new GUIStyle(labelStyle)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = hovered ? IMGUIStyles.TextPrimary : IMGUIStyles.TextSecondary }
                    };
                    GUI.Label(new Rect(rowRect.x + 10, rowRect.y + 4, rowRect.width - 46f, rowRect.height),
                        sequence.DisplayName, rowStyle);

                    // 标镜头数：一眼看出这场是单镜还是多镜，也能立刻发现"列表忘了填"。
                    int shotCount = 0;
                    foreach (var _ in sequence.ValidShots())
                        shotCount++;
                    GUI.Label(new Rect(rowRect.xMax - 52f, rowRect.y + 4, 48f, rowRect.height),
                        shotCount == 0 ? "空" : $"{shotCount} 镜", mutedStyle);

                    if (ui.WasClicked(rowRect))
                    {
                        gameManager.Cutscene.Play(sequence);
                        _isOpen = false;
                        Event.current.Use();
                        return;
                    }
                }

                cutsceneListY += _sequences.Count * itemH;
            }

            // Scene switch section
            sepY = cutsceneListY + 6f;
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

        public static void Close() { _isOpen = false; }

        public static void Reset() { Close(); }

        private static void LoadScenes(SSNoirGameManager gameManager)
        {
            // 过场清单每次开面板重扫：配好一场戏，关开一次面板就能试，不用重进 Play 模式。
            // 这一步是要反复跑的，能省一次重进就省一次。
            _sequences.Clear();
            _sequences.AddRange(Object.FindObjectsOfType<CutsceneSequence>(true));

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
