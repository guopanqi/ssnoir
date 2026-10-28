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
        private static bool _theatreExpanded = false;
        private static bool _researchExpanded = false;
        private static readonly List<SceneItem> _scenes = new List<SceneItem>();
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
            new ChapterJump { Label = "剧场试演：序幕（新版）", Code = "(begin (load-file \"scripts/theatre/第一节开场新版.scm\") (开场新版-序幕))" },
            new ChapterJump { Label = "剧场试演：清晨（新版）", Code = "(begin (load-file \"scripts/theatre/第一节开场新版.scm\") (开场新版-清晨))" },
            new ChapterJump { Label = "剧场试演：酒馆（新版）", Code = "(begin (load-file \"scripts/theatre/第一节开场新版.scm\") (开场新版-酒馆初访))" },
            new ChapterJump { Label = "剧场试演：再访（新版）", Code = "(begin (load-file \"scripts/theatre/第一节开场新版.scm\") (开场新版-酒馆再访))" },
            new ChapterJump { Label = "剧场试演：醉归（旧版）", Code = "(begin (load-file \"scripts/theatre/第一节生活.scm\") (第一节-醉归))" },
            new ChapterJump { Label = "剧场试演：清晨敲门（旧版）", Code = "(begin (load-file \"scripts/theatre/第一节生活.scm\") (第一节-清晨敲门))" },
            new ChapterJump { Label = "剧场试演：酒馆初访（旧版）", Code = "(begin (load-file \"scripts/theatre/第一节生活.scm\") (第一节-酒馆初访))" },
            new ChapterJump { Label = "剧场试演：酒馆接案后（旧版）", Code = "(begin (load-file \"scripts/theatre/第一节生活.scm\") (第一节-酒馆接案后))" },
            new ChapterJump { Label = "剧场试演：艾迪相识", Code = "(begin (load-file \"scripts/theatre/第一节生活.scm\") (第一节-艾迪相识))" },

            new ChapterJump { Label = "剧场试演：雨夜来访", Code = "(begin (load-file \"scripts/theatre/雨夜来访.scm\") (雨夜来访-演出))" },
            new ChapterJump { Label = "剧场试演：路灯下", Code = "(begin (load-file \"scripts/theatre/路灯下.scm\") (路灯下-试演!))" },
            new ChapterJump { Label = "剧场试演：室内求助", Code = "(begin (load-file \"scripts/theatre/室内求助.scm\") (室内求助-演出))" },
            new ChapterJump { Label = "剧场试演：第二天", Code = "(begin (load-file \"scripts/theatre/第二天.scm\") (第二天-演出))" },
            new ChapterJump { Label = "剧场试演：邮箱相见：足额", Code = "(begin (load-file \"scripts/theatre/邮箱相见.scm\") (邮箱相见-演出 '足额))" },
            new ChapterJump { Label = "剧场试演：邮箱相见：她补", Code = "(begin (load-file \"scripts/theatre/邮箱相见.scm\") (邮箱相见-演出 '她补))" },
            new ChapterJump { Label = "剧场试演：邮箱相见：塞报纸", Code = "(begin (load-file \"scripts/theatre/邮箱相见.scm\") (邮箱相见-演出 '塞报纸))" },
            new ChapterJump { Label = "剧场试演：勒索追逐", Code = "(begin (load-file \"scripts/theatre/勒索追逐.scm\") (勒索追逐-演出))" },
            new ChapterJump { Label = "剧场试演：平静", Code = "(begin (load-file \"scripts/theatre/平静.scm\") (平静-演出))" },
            new ChapterJump { Label = "剧场试演：烟盒", Code = "(begin (load-file \"scripts/theatre/烟盒.scm\") (烟盒-演出))" },
            new ChapterJump { Label = "剧场试演：弗兰克的桌子", Code = "(begin (load-file \"scripts/theatre/弗兰克的桌子.scm\") (弗兰克的桌子-演出))" },
            new ChapterJump { Label = "剧场试演：独闯老街", Code = "(begin (load-file \"scripts/theatre/独闯老街.scm\") (独闯老街-演出))" },
            new ChapterJump { Label = "剧场试演：巷口", Code = "(begin (load-file \"scripts/theatre/巷口.scm\") (巷口-演出))" },
            new ChapterJump { Label = "剧场试演：贝恩斯", Code = "(begin (load-file \"scripts/theatre/贝恩斯.scm\") (贝恩斯-演出))" },
            new ChapterJump { Label = "剧场试演：老街被介入", Code = "(begin (load-file \"scripts/theatre/老街被介入.scm\") (老街被介入-演出))" },
            new ChapterJump { Label = "剧场试演：经理电话", Code = "(begin (load-file \"scripts/theatre/经理电话.scm\") (经理电话-演出))" },
            new ChapterJump { Label = "剧场试演：解雇", Code = "(begin (load-file \"scripts/theatre/解雇.scm\") (解雇-演出))" },
            new ChapterJump { Label = "剧场试演：剧院来信", Code = "(begin (load-file \"scripts/theatre/剧院来信.scm\") (剧院来信-演出))" },
            new ChapterJump { Label = "剧场试演：莱恩的家", Code = "(begin (load-file \"scripts/theatre/莱恩的家.scm\") (莱恩的家-演出))" },
            new ChapterJump { Label = "剧场试演：剧院安全", Code = "(begin (load-file \"scripts/theatre/剧院安全.scm\") (剧院安全-演出))" },
            new ChapterJump { Label = "剧场试演：彩排", Code = "(begin (load-file \"scripts/theatre/彩排.scm\") (彩排-演出))" },
            new ChapterJump { Label = "剧场试演：照相馆冲印（两卷）", Code = "(begin (load-file \"scripts/theatre/照相馆.scm\") (照相馆冲印-演出 #t))" },
            new ChapterJump { Label = "剧场试演：照相馆冲印（一卷）", Code = "(begin (load-file \"scripts/theatre/照相馆.scm\") (照相馆冲印-演出 #f))" },
            new ChapterJump { Label = "剧场试演：照相馆查来源", Code = "(begin (load-file \"scripts/theatre/照相馆.scm\") (照相馆查来源-演出))" },
            new ChapterJump
            {
                Label = "第一章：大船靠岸（再进码头）",
                Code = "(debug-prepare-ship-repair!)"
            },
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
            new ChapterJump { Label = "准备：酒馆门前（再进酒馆）", Code = "(baines 'debug-arm-street!)" },
            new ChapterJump { Label = "舞台试演：酒馆门前", Code = "(baines 'debug-play-street!)" },
        };

        private static void RunChapterJump(SSNoirGameManager gameManager, string code)
        {
            var sceneManager = gameManager.SceneManager;
            // 只在世界里跳章节：Scenario中途改世界状态没有意义，还会把那一场的快照搅乱。
            if (sceneManager.CurrentSceneName != "world")
            {
                gameManager.ShowNotification("先回到世界地图再跳章节。");
                return;
            }

            try
            {
                sceneManager.ActiveInterpreter.Eval(code);
                sceneManager.Refresh();
                if (code == "(debug-prepare-ship-repair!)")
                    gameManager.ShowNotification("货船已排在当前日。请重新进入码头看完整入场演出。");
                if (code == "(baines 'debug-arm-street!)")
                    gameManager.ShowNotification("酒馆门前已挂上。请重新进入老街酒馆。");
            }
            catch (System.Exception e)
            {
                gameManager.ShowNotification("章节跳转失败：" + e.Message);
            }
        }

        private static bool DrawTapRow(IMGUIInteractionContext ui, Rect rowRect, string label, GUIStyle labelStyle, bool selected = false)
        {
            bool hovered = ui.CanHover(rowRect);
            if (selected)
            {
                GUI.color = new Color(IMGUIStyles.Gold.r, IMGUIStyles.Gold.g, IMGUIStyles.Gold.b, 0.13f);
                GUI.DrawTexture(rowRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawLine(new Vector2(rowRect.x + 3f, rowRect.y + 5f),
                    new Vector2(rowRect.x + 3f, rowRect.yMax - 5f), IMGUIStyles.Gold, 2f);
            }
            else if (hovered)
            {
                GUI.color = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f);
                GUI.DrawTexture(rowRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            var rowStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = selected ? IMGUIStyles.Gold : (hovered ? IMGUIStyles.TextPrimary : IMGUIStyles.TextSecondary) }
            };
            IMGUIStyles.DrawLabel(new Rect(rowRect.x + 10, rowRect.y + 4, rowRect.width - 12f, rowRect.height),
                label, rowStyle);
            return ui.WasTapped(rowRect);
        }

        private static float ContentHeight()
        {
            float y = 8f + 20f + SaveManager.SlotCount * 28f;
            y += 6f + 20f + ItemH; // 雨雪预览标题与天气按钮
            y += 6f + 22f + ChapterRowCount() * ItemH; // 章节跳转（剧场试演折叠）
            y += 6f + 22f + ItemH; // 资源调试
            y += 6f + 4f + ItemH * 0.5f; // 场景标题及列表起点
            y += SceneRowCount() * ItemH;
            return y + 8f;
        }

        // 剧场与对白试演统一收进折叠菜单。
        private static bool IsTheatreJump(string label) =>
            label.StartsWith("剧场试演") || label.StartsWith("舞台试演") || label.StartsWith("对白试演");

        private static int TheatreJumpCount()
        {
            int n = 0;
            foreach (var jump in ChapterJumps)
                if (IsTheatreJump(jump.Label)) n++;
            return n;
        }

        private static int ChapterRowCount()
        {
            int normal = ChapterJumps.Length - TheatreJumpCount();
            return normal + 1 + (_theatreExpanded ? TheatreJumpCount() : 0);
        }

        // Scenario列表里“研究”开头的 scenario 收进二级菜单。
        private static bool IsResearchScene(SceneItem item) => !item.IsHeader && item.SceneName.StartsWith("研究");

        private static int ResearchSceneCount()
        {
            int n = 0;
            foreach (var item in _scenes)
                if (IsResearchScene(item)) n++;
            return n;
        }

        private static int SceneRowCount()
        {
            int research = ResearchSceneCount();
            if (research == 0) return _scenes.Count;
            return _scenes.Count - research + 1 + (_researchExpanded ? research : 0);
        }

        public static Rect GetPanelRect(TopHudLayout topHud)
        {
            // 面板挂在调试开关下面，和卷宗 / 帮助各自挂自己开关是同一套规矩。
            var anchor = topHud.DebugToggle;

            float panelW = 260f;
            float panelX = anchor.xMax - panelW;
            float panelY = anchor.yMax + 4f;
            Rect safe = UIScale.SafeArea;
            // 手机上内容高度远大于屏幕。面板只占设置开关下方的安全区，
            // 内容由 Draw 里的触摸滚动负责；不再把面板顶到屏幕外。
            float availableHeight = Mathf.Max(ItemH, safe.yMax - panelY);
            float panelH = Mathf.Min(ContentHeight(), availableHeight);
            panelX = Mathf.Clamp(panelX, safe.x, Mathf.Max(safe.x, safe.xMax - panelW));
            return new Rect(panelX, panelY, panelW, panelH);
        }

        public static void Open(SSNoirGameManager gameManager)
        {
            if (!_isOpen)
            {
                _isOpen = true;
                LoadScenes(gameManager);
                _scrollOffset = 0f;
                _theatreExpanded = false;
                _researchExpanded = false;
            }
        }

        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui, TopHudLayout topHud)
        {
            // 开关和卷宗 / 帮助共用同一份实现，五个长得一模一样。
            if (IMGUIButton.DrawTopTextToggle(topHud.DebugToggle, UiText.Get("调试"), _isOpen, ui))
            {
                if (_isOpen) Close();
                else Open(gameManager);
            }

            if (!_isOpen) return;
            Rect panelRect = GetPanelRect(topHud);

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
            IMGUIStyles.DrawLabel(new Rect(panelX + 8, curY + 2f, panelW, 18f), "雨雪预览", mutedStyle);
            curY += 20f;
            string? preview = gameManager.Weather.DebugWeatherPreview;
            string[] weatherLabels = { "跟随", "晴", "雨", "雪" };
            string?[] weatherValues = { null, "晴", "雨", "雪" };
            float weatherGap = 4f;
            float weatherButtonW = (panelW - 16f - weatherGap * (weatherLabels.Length - 1)) / weatherLabels.Length;
            for (int i = 0; i < weatherLabels.Length; i++)
            {
                var weatherRect = new Rect(panelX + 8f + i * (weatherButtonW + weatherGap), curY + 1f,
                    weatherButtonW, ItemH - 2f);
                bool selected = preview == weatherValues[i];
                if (DrawTapRow(contentUi, weatherRect, weatherLabels[i], labelStyle, selected))
                    gameManager.Weather.SetDebugWeatherPreview(weatherValues[i]);
            }
            curY += ItemH + 6f;
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

            var leftLabelStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleLeft };
            float sectionEndY = curY;
            float sepY;

            // 章节跳转
            float chapterSepY = sectionEndY + 6f;
            IMGUIStyles.DrawLine(new Vector2(panelX + 8, chapterSepY), new Vector2(panelX + panelW - 8, chapterSepY),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
            IMGUIStyles.DrawLabel(new Rect(panelX + 8, chapterSepY + 2f, panelW, 18f), "剧情测试", mutedStyle);

            float chapterY = chapterSepY + 22f;
            foreach (var jump in ChapterJumps)
            {
                if (IsTheatreJump(jump.Label)) continue;
                var jumpRect = new Rect(panelX + 4f, chapterY, panelW - 8f, itemH - 2f);
                if (DrawTapRow(contentUi, jumpRect, jump.Label, labelStyle))
                {
                    RunChapterJump(gameManager, jump.Code);
                    _isOpen = false;
                    Event.current.Use();
                    return;
                }
                chapterY += itemH;
            }
            {
                // 剧场试演二级菜单：点一下展开/收起，子项缩进。
                int theatreCount = TheatreJumpCount();
                var groupRect = new Rect(panelX + 4f, chapterY, panelW - 8f, itemH - 2f);
                if (DrawTapRow(contentUi, groupRect,
                        $"剧场试演（{theatreCount}）" + (_theatreExpanded ? " ▾" : " ▸"), labelStyle))
                {
                    _theatreExpanded = !_theatreExpanded;
                    Event.current.Use();
                }
                chapterY += itemH;
                if (_theatreExpanded)
                {
                    foreach (var jump in ChapterJumps)
                    {
                        if (!IsTheatreJump(jump.Label)) continue;
                        var subRect = new Rect(panelX + 16f, chapterY, panelW - 20f, itemH - 2f);
                        if (DrawTapRow(contentUi, subRect, jump.Label, labelStyle))
                        {
                            RunChapterJump(gameManager, jump.Code);
                            _isOpen = false;
                            Event.current.Use();
                            return;
                        }
                        chapterY += itemH;
                    }
                }
            }
            sectionEndY = chapterY;

            // 资源调试
            float resourceSepY = sectionEndY + 6f;
            IMGUIStyles.DrawLine(new Vector2(panelX + 8, resourceSepY), new Vector2(panelX + panelW - 8, resourceSepY),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
            IMGUIStyles.DrawLabel(new Rect(panelX + 8, resourceSepY + 2f, panelW, 18f), "资源调试", mutedStyle);

            float resourceRowY = resourceSepY + 22f;
            IMGUIStyles.DrawLabel(new Rect(panelX + 8f, resourceRowY + 2f, 120f, 22f),
                "金钱", leftLabelStyle);
            var resourceBorder = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f);
            var resourceFill = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.08f);
            if (IMGUIButton.Draw(new Rect(panelX + panelW - 8f - 76f, resourceRowY + 2f, 76f, 22f),
                    "+100", contentUi, resourceBorder, resourceFill, labelStyle))
            {
                gameManager.AddDebugMoney(100);
            }

            sectionEndY = resourceRowY + itemH;

            // Scene switch section
            sepY = sectionEndY + 6f;
            IMGUIStyles.DrawLine(new Vector2(panelX + 8, sepY), new Vector2(panelX + panelW - 8, sepY),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f), 1f);
            IMGUIStyles.DrawLabel(new Rect(panelX + 8, sepY + 2f, panelW, 18f), "Scenario测试（直接进入）", mutedStyle);

            // Scene list
            float listY = sepY + 4f + itemH * 0.5f;
            string currentScene = gameManager.SceneManager.CurrentSceneName;

            // 研究类 scenario 收进二级菜单：先画组行，展开才画子项（缩进）。
            int researchTotal = ResearchSceneCount();
            bool researchGroupDrawn = false;
            foreach (var item in _scenes)
            {
                if (IsResearchScene(item))
                {
                    if (!researchGroupDrawn)
                    {
                        researchGroupDrawn = true;
                        var groupRect = new Rect(panelX + 4f, listY, panelW - 8f, itemH - 2f);
                        if (DrawTapRow(contentUi, groupRect,
                                $"研究（{researchTotal}）" + (_researchExpanded ? " ▾" : " ▸"), labelStyle))
                        {
                            _researchExpanded = !_researchExpanded;
                            Event.current.Use();
                        }
                        listY += itemH;
                    }
                    if (!_researchExpanded) continue;
                    DrawSceneRow(item, panelX + 16f, panelW - 20f);
                    listY += itemH;
                    if (!_isOpen) return;
                    continue;
                }
                DrawSceneRow(item, panelX + 4f, panelW - 8f);
                listY += itemH;
                if (!_isOpen) return;
            }

            void DrawSceneRow(SceneItem row, float rowX, float rowW)
            {
                var itemRect = new Rect(rowX, listY, rowW, itemH - 2f);

                if (row.IsHeader)
                {
                    IMGUIStyles.DrawLabel(new Rect(itemRect.x + 6, itemRect.y + 4, itemRect.width, itemRect.height),
                        row.Name, mutedStyle);
                    return;
                }

                bool isHovered = contentUi.CanHover(itemRect);
                bool isCurrent = row.SceneName == currentScene;

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
                IMGUIStyles.DrawLabel(new Rect(itemRect.x + 10, itemRect.y + 4, itemRect.width, itemRect.height), row.Name, itemStyle);

                if (contentUi.WasTapped(itemRect))
                {
                    gameManager.OnSceneButtonClicked(row.SceneName);
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

            // 点面板外面关掉；开关自己不算"外面"，不然开着时点开关会先关再开，永远关不上。
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && !ui.IsLocked)
            {
                if (!panelRect.Contains(ui.Mouse) && !topHud.DebugToggle.Contains(ui.Mouse))
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
            _scenes.Clear();
            _scenes.Add(new SceneItem { Name = "--- 世界 ---", IsHeader = true });
            _scenes.Add(new SceneItem { Name = "world", SceneName = "world" });

            // 剧情Scenario全部列出；研究与实验不混入剧情列表。
            var allowed = DebugScenarioCatalog.Load();
            bool storyHeader = false;
            bool researchHeader = false;
            foreach (var scene in allowed)
            {
                if (scene.Research && !researchHeader)
                {
                    _scenes.Add(new SceneItem { Name = "--- 精选机制试玩 ---", IsHeader = true });
                    researchHeader = true;
                }
                else if (!scene.Research && !storyHeader)
                {
                    _scenes.Add(new SceneItem { Name = "--- 剧情Scenario ---", IsHeader = true });
                    storyHeader = true;
                }
                _scenes.Add(new SceneItem { Name = scene.Label, SceneName = scene.Scene });
            }
        }
    }
}
