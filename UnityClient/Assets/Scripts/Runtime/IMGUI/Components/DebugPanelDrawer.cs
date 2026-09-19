#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // Unified debug panel: runtime test controls + Save/Load + Scene switch.
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
        private static float _scrollOffset;
        private static bool _scrollDragActive;
        private static float _scrollDragLastY;
        private static float _scrollDragTravel;

        public static bool IsOpen => _isOpen;

        // 列表行高。触控上抬一档才点得准；这是个开发面板，行数多，抬到完整的 44 会让面板
        // 直接长出屏幕，所以取中间值而不是 UIScale.MinTouchSize。
        private const float ItemH = 34f;

        // 章节跳转：内容侧留了 debug-* 消息，这里只负责把它们摆成按钮。
        // 原来这套开关是世界里一个叫「调试台」的地点（world/test.scm），它的门槛读的是
        // 一个永远为假的全局，等于谁也打不开；调试入口只该有一处，就是这个面板。
        private struct ChapterJump
        {
            public string Label;
            public string Code;
        }

        private static readonly ChapterJump[] ChapterJumps =
        {
            new ChapterJump
            {
                Label = "第二章第 1 天（林：外围）",
                Code = "(debug-enter-chapter2!)"
            },
            new ChapterJump
            {
                Label = "第二章第 1 天（林：核心）",
                Code = "(debug-enter-chapter2-lin-core!)"
            },
            new ChapterJump
            {
                Label = "Phase B（林：外围）",
                Code = "(debug-enter-chapter2-phase-b!)"
            },
            new ChapterJump
            {
                Label = "Phase B（林：核心）",
                Code = "(debug-enter-chapter2-phase-b-lin-core!)"
            },
            // 不是跳章：只把一段带舞台指示的对白演一遍，不改任何故事状态。
            new ChapterJump
            {
                Label = "舞台试演：桥廊之后",
                Code = "(three-letters 'debug-stage-bridge!)"
            },
            // 下面三个是审核期临时入口，审完删。
            new ChapterJump { Label = "舞台试演：开场委托", Code = "(three-letters 'debug-stage-commission!)" },
            new ChapterJump { Label = "舞台试演：她不取消", Code = "(three-letters 'debug-stage-refusal!)" },
            new ChapterJump { Label = "舞台试演：尾声", Code = "(three-letters 'debug-stage-closing!)" },
        };

        private static void RunChapterJump(SSNoirGameManager gameManager, string code)
        {
            var sceneManager = gameManager.SceneManager;
            // 只在世界里跳章节：交锋中途改世界状态没有意义，还会把那一场的快照搅乱。
            if (sceneManager.CurrentSceneName != "world")
            {
                gameManager.ShowNotification("先回到世界地图再跳章节。");
                return;
            }

            try
            {
                sceneManager.ActiveInterpreter.Eval(code);
                sceneManager.Refresh();
            }
            catch (System.Exception e)
            {
                gameManager.ShowNotification("章节跳转失败：" + e.Message);
            }
        }

        private static bool DrawTapRow(IMGUIInteractionContext ui, Rect rowRect, string label, GUIStyle labelStyle)
        {
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
            IMGUIStyles.DrawLabel(new Rect(rowRect.x + 10, rowRect.y + 4, rowRect.width - 12f, rowRect.height),
                label, rowStyle);
            return ui.WasTapped(rowRect);
        }

        private static float ContentHeight()
        {
            float y = 8f + 20f + SaveManager.SlotCount * 28f;
            y += 6f + 20f; // 镜头测试标题与第一行
            y += ItemH; // 过场截图行
            y += 6f + 22f; // 过场测试标题及列表起点
            y += Mathf.Max(_sequences.Count, 1) * ItemH;
            y += 6f + 22f + ChapterJumps.Length * ItemH; // 章节跳转
            y += 6f + 22f + ItemH; // 资源调试
            y += 6f + 4f + ItemH * 0.5f; // 场景标题及列表起点
            y += _scenes.Count * ItemH;
            return y + 8f;
        }

        public static (Rect ToggleRect, Rect PanelRect) GetRects(TopHudLayout topHud)
        {
            var toggleRect = topHud.DebugToggle;

            float panelW = 260f;
            float panelX = toggleRect.xMax - panelW;
            float panelY = toggleRect.yMax + 4f;
            Rect safe = UIScale.SafeArea;
            // 手机上内容高度远大于屏幕。面板只占 Debug 按钮下方的安全区，
            // 内容由 Draw 里的触摸滚动负责；不再把面板顶到屏幕外。
            float availableHeight = Mathf.Max(ItemH, safe.yMax - panelY);
            float panelH = Mathf.Min(ContentHeight(), availableHeight);
            panelX = Mathf.Clamp(panelX, safe.x, Mathf.Max(safe.x, safe.xMax - panelW));
            return (toggleRect, new Rect(panelX, panelY, panelW, panelH));
        }

        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui, TopHudLayout topHud)
        {
            var (toggleRect, panelRect) = GetRects(topHud);

            // Toggle button：和卷宗/成长/设置/帮助共用同一份顶栏开关样式。
            if (IMGUIButton.DrawHudToggle(toggleRect, "Debug ▾", _isOpen, ui))
            {
                _isOpen = !_isOpen;
                if (_isOpen)
                {
                    LoadScenes(gameManager);
                    _scrollOffset = 0f;
                }
            }

            if (!_isOpen) return;

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(13),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = IMGUIStyles.TextPrimary }
            };

            // Panel
            float itemH = ItemH;
            float panelW = panelRect.width;
            float contentHeight = ContentHeight();
            float maxScroll = Mathf.Max(0f, contentHeight - panelRect.height);

            GUI.color = new Color(IMGUIStyles.HudBg.r, IMGUIStyles.HudBg.g, IMGUIStyles.HudBg.b, IMGUIStyles.ModalOpacity);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panelRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));

            UpdateScroll(ui, panelRect, maxScroll);
            _scrollOffset = Mathf.Clamp(_scrollOffset, 0f, maxScroll);

            GUI.BeginGroup(panelRect);
            try
            {
            float panelX = 0f;
            float panelY = -_scrollOffset;
            var translatedUi = ui.Translated(new Vector2(panelRect.x, panelRect.y));
            // BeginGroup 只负责裁剪，命中判定还要显式拒绝面板外的指针。
            var contentUi = panelRect.Contains(ui.Mouse) ? translatedUi : translatedUi.Occluded();

            // Slots Section
            float curY = panelY + 8f;
            var mutedStyle = new GUIStyle(labelStyle) { fontSize = IMGUIStyles.FontSize(11),
                normal = { textColor = IMGUIStyles.TextDisabled } };
            IMGUIStyles.DrawLabel(new Rect(panelX + 8, curY + 2f, panelW, 18f), "存档管理", mutedStyle);
            curY += 20f;

            for (int slot = 1; slot <= SaveManager.SlotCount; slot++)
            {
                string slotPath = SaveManager.GetSlotFilePath(slot);
                string saveTime = SaveManager.GetSaveTime(slotPath);
                bool hasSave = !string.IsNullOrEmpty(saveTime);

                IMGUIStyles.DrawLabel(new Rect(panelX + 8f, curY + 4f, 50f, 20f), $"槽位 {slot}", labelStyle);

                string timeStr = hasSave ? saveTime : "（空）";
                var timeStyle = new GUIStyle(labelStyle) {
                    normal = { textColor = hasSave ? IMGUIStyles.TextPrimary : IMGUIStyles.TextDisabled }
                };
                IMGUIStyles.DrawLabel(new Rect(panelX + 52f, curY + 4f, 130f, 20f), timeStr, timeStyle);

                var rectSave = new Rect(panelX + panelW - 8f - 64f, curY + 2f, 30f, 22f);
                var rectLoad = new Rect(panelX + panelW - 8f - 30f, curY + 2f, 30f, 22f);

                if (IMGUIButton.Draw(rectSave, "存", contentUi,
                        new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f),
                        new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f), labelStyle))
                {
                    gameManager.SaveGame(slotPath);
                }

                if (IMGUIButton.Draw(rectLoad, "读", contentUi,
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
            IMGUIStyles.DrawLabel(new Rect(panelX + 8, sepY + 2f, panelW, 18f), "镜头测试", mutedStyle);

            float cameraRowY = sepY + 20f;
            var cameraLabelStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = IMGUIStyles.TextPrimary }
            };

            // 过场首帧截图：渲的是纯世界，这个面板开着也不会进画面，所以就放在这儿点。
            float captureRowY = cameraRowY;
            IMGUIStyles.DrawLabel(new Rect(panelX + 8f, captureRowY + 2f, 120f, 22f),
                "过场截图", cameraLabelStyle);

            var captureStyle = new GUIStyle(labelStyle)
            {
                normal = { textColor = CinematicCapture.IsCapturing ? IMGUIStyles.TextDisabled : IMGUIStyles.TextSecondary }
            };
            var captureBorder = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f);
            var captureFill = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f);

            if (IMGUIButton.Draw(new Rect(panelX + panelW - 8f - 132f, captureRowY + 2f, 64f, 22f),
                    "1080p", contentUi, captureBorder, captureFill, captureStyle, !CinematicCapture.IsCapturing))
            {
                CinematicCapture.Capture(gameManager, 1080);
            }

            if (IMGUIButton.Draw(new Rect(panelX + panelW - 8f - 64f, captureRowY + 2f, 64f, 22f),
                    "4K", contentUi, captureBorder, captureFill, captureStyle, !CinematicCapture.IsCapturing))
            {
                CinematicCapture.Capture(gameManager, 2160);
            }

            // 过场测试：列出场景里所有 CutsceneSequence，点一个就走完整套流程
            // （推第一镜 → 压黑边 → 逐镜放片子 → 收黑边 → 镜头回来）。
            float cutsceneSepY = captureRowY + itemH + 6f;
            IMGUIStyles.DrawLine(new Vector2(panelX + 8, cutsceneSepY), new Vector2(panelX + panelW - 8, cutsceneSepY),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
            IMGUIStyles.DrawLabel(new Rect(panelX + 8, cutsceneSepY + 2f, panelW, 18f), "过场测试（只播放，不进入交锋）", mutedStyle);

            float cutsceneListY = cutsceneSepY + 22f;

            if (_sequences.Count == 0)
            {
                IMGUIStyles.DrawLabel(new Rect(panelX + 12f, cutsceneListY + 2f, panelW - 16f, itemH),
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

                    bool hovered = contentUi.CanHover(rowRect);
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
                    IMGUIStyles.DrawLabel(new Rect(rowRect.x + 10, rowRect.y + 4, rowRect.width - 46f, rowRect.height),
                        sequence.DisplayName, rowStyle);

                    // 标镜头数：一眼看出这场是单镜还是多镜，也能立刻发现"列表忘了填"。
                    int shotCount = 0;
                    foreach (var _ in sequence.ValidShots())
                        shotCount++;
                    IMGUIStyles.DrawLabel(new Rect(rowRect.xMax - 52f, rowRect.y + 4, 48f, rowRect.height),
                        shotCount == 0 ? "空" : $"{shotCount} 镜", mutedStyle);

                    if (contentUi.WasTapped(rowRect))
                    {
                        gameManager.Cutscene.Play(sequence);
                        _isOpen = false;
                        Event.current.Use();
                        return;
                    }
                }

                cutsceneListY += _sequences.Count * itemH;
            }

            // 章节跳转
            float chapterSepY = cutsceneListY + 6f;
            IMGUIStyles.DrawLine(new Vector2(panelX + 8, chapterSepY), new Vector2(panelX + panelW - 8, chapterSepY),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
            IMGUIStyles.DrawLabel(new Rect(panelX + 8, chapterSepY + 2f, panelW, 18f), "章节跳转（调试）", mutedStyle);

            float chapterY = chapterSepY + 22f;
            for (int i = 0; i < ChapterJumps.Length; i++)
            {
                var jumpRect = new Rect(panelX + 4f, chapterY + i * itemH, panelW - 8f, itemH - 2f);
                if (DrawTapRow(contentUi, jumpRect, ChapterJumps[i].Label, labelStyle))
                {
                    RunChapterJump(gameManager, ChapterJumps[i].Code);
                    _isOpen = false;
                    Event.current.Use();
                    return;
                }
            }
            cutsceneListY = chapterY + ChapterJumps.Length * itemH;

            // 资源调试
            float resourceSepY = cutsceneListY + 6f;
            IMGUIStyles.DrawLine(new Vector2(panelX + 8, resourceSepY), new Vector2(panelX + panelW - 8, resourceSepY),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
            IMGUIStyles.DrawLabel(new Rect(panelX + 8, resourceSepY + 2f, panelW, 18f), "资源调试", mutedStyle);

            float resourceRowY = resourceSepY + 22f;
            IMGUIStyles.DrawLabel(new Rect(panelX + 8f, resourceRowY + 2f, 120f, 22f),
                "金钱", cameraLabelStyle);
            var resourceBorder = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f);
            var resourceFill = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f);
            if (IMGUIButton.Draw(new Rect(panelX + panelW - 8f - 76f, resourceRowY + 2f, 76f, 22f),
                    "+100", contentUi, resourceBorder, resourceFill, labelStyle))
            {
                gameManager.AddDebugMoney(100);
            }

            cutsceneListY = resourceRowY + itemH;

            // Scene switch section
            sepY = cutsceneListY + 6f;
            IMGUIStyles.DrawLine(new Vector2(panelX + 8, sepY), new Vector2(panelX + panelW - 8, sepY),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
            IMGUIStyles.DrawLabel(new Rect(panelX + 8, sepY + 2f, panelW, 18f), "交锋测试（直接进入）", mutedStyle);

            // Scene list
            float listY = sepY + 4f + itemH * 0.5f;
            string currentScene = gameManager.SceneManager.CurrentSceneName;

            for (int i = 0; i < _scenes.Count; i++)
            {
                var item = _scenes[i];
                var itemRect = new Rect(panelX + 4f, listY + i * itemH, panelW - 8f, itemH - 2f);

                if (item.IsHeader)
                {
                    IMGUIStyles.DrawLabel(new Rect(itemRect.x + 6, itemRect.y + 4, itemRect.width, itemRect.height),
                        item.Name, mutedStyle);
                    continue;
                }

                bool isHovered = contentUi.CanHover(itemRect);
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
                IMGUIStyles.DrawLabel(new Rect(itemRect.x + 10, itemRect.y + 4, itemRect.width, itemRect.height), item.Name, itemStyle);

                if (contentUi.WasTapped(itemRect))
                {
                    gameManager.OnSceneButtonClicked(item.SceneName);
                    _isOpen = false;
                    Event.current.Use();
                    return;
                }
            }

            }
            finally
            {
                GUI.EndGroup();
            }

            DrawScrollbar(panelRect, contentHeight, maxScroll);

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

        public static void Close()
        {
            _isOpen = false;
            _scrollDragActive = false;
        }

        public static void Reset() { Close(); }

        private static void UpdateScroll(IMGUIInteractionContext ui, Rect panelRect, float maxScroll)
        {
            bool pointerInPanel = panelRect.Contains(ui.Mouse);
            var e = Event.current;

            if (pointerInPanel && e.type == EventType.ScrollWheel && maxScroll > 0f)
            {
                _scrollOffset += e.delta.y * 18f;
                e.Use();
                return;
            }

            // 桌面用滚轮；手机用手指拖动。面板内的按钮都在 MouseUp 且未滑动时响应，
            // 因此从按钮上开始滑动也不会误触。
            if (UIScale.HasHoverPointer)
                return;

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && pointerInPanel && maxScroll > 0f:
                    _scrollDragActive = true;
                    _scrollDragLastY = ui.Mouse.y;
                    _scrollDragTravel = 0f;
                    break;

                case EventType.MouseDrag when _scrollDragActive:
                {
                    float dy = ui.Mouse.y - _scrollDragLastY;
                    _scrollDragLastY = ui.Mouse.y;
                    _scrollDragTravel += Mathf.Abs(dy);
                    _scrollOffset -= dy;
                    if (_scrollDragTravel >= IMGUIInteractionContext.TapSlop)
                        e.Use();
                    break;
                }

                case EventType.MouseUp:
                    _scrollDragActive = false;
                    _scrollDragTravel = 0f;
                    break;
            }
        }

        private static void DrawScrollbar(Rect panelRect, float contentHeight, float maxScroll)
        {
            if (maxScroll <= 0f)
                return;

            const float trackW = 5f;
            var track = new Rect(panelRect.xMax - trackW - 3f, panelRect.y + 4f,
                trackW, panelRect.height - 8f);
            float thumbH = Mathf.Max(ItemH, track.height * (panelRect.height / contentHeight));
            float travel = Mathf.Max(0f, track.height - thumbH);
            var thumb = new Rect(track.x, track.y + travel * (_scrollOffset / maxScroll), track.width, thumbH);

            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, 0.65f);
            GUI.DrawTexture(track, Texture2D.whiteTexture);
            GUI.color = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.55f);
            GUI.DrawTexture(thumb, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

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

            foreach (var asset in Resources.LoadAll<TextAsset>("Content/scenes/encounters"))
            {
                string name = asset.name;
                _scenes.Add(new SceneItem { Name = name, SceneName = name });
            }
        }
    }
}
