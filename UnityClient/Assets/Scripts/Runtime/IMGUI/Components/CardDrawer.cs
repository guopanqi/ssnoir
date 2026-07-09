#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // 节点卡入口与共享框架。
    //
    // DESIGN.md「节点规范（暗版，场景内）」通用规则：
    //   Ink 填充 + 1px 纸白描边 @ 70–75%（与建筑线同宽，偏纸白暖调）
    //   内侧 3px 加一根 20% 细线（双线，始终是「线」的语言，不是「框」）
    //   直角无圆角；硬投影偏移 (5,6) 纯黑 @ 45–50%，无模糊
    //
    // 交互状态（DESIGN.md）：默认白线 70% / 悬停白线 100%（变亮不变色）/ 选中金描边 /
    // 正在发生金光呼吸 / 禁用降到 35%。
    //
    // 本类只画共享框架与时钟徽章；动作 / 判定卡内容委托 ActionNodeDrawer，
    // 普通 / 地点 / 人物节点内容委托 ContainerNodeDrawer。
    public static class CardDrawer
    {
        public struct CardInteraction
        {
            public bool CardClicked;
            public int ClickedSlotIndex;
            public int DroppedSlotIndex;
            public bool ExecuteClicked;
        }

        // ── 入口 ───────────────────────────────────────────────────────

        // 节点视觉类别。分两大族：
        //   有框（Action / Character）——框起来的「器物」，你在上面操作。
        //   无边悬浮（Location / Common）——漂在图纸上的「一个地方 / 一件事」，你点进去。
        public enum CardKind { Action, Location, Character, Common }

        // 数据驱动分类。内容里并没有「地点 / 人物」标签，真正的「这是个地方」信号是它
        // 锚定在场景 3D 建筑上（projected）；网格里的容器则是地点内部的事件/分组（Common）。
        // 标签留作显式覆盖，未来内容想强制某类时可用。
        public static CardKind Classify(GameNode node, bool anchored)
        {
            if (node.HasResolve) return CardKind.Action;
            if (node.Tags.Contains("人物")) return CardKind.Character;
            if (anchored || node.Tags.Contains("地点")) return CardKind.Location;
            return CardKind.Common;
        }

        public static CardInteraction DrawCard(
            Rect rect, GameNode node, CardKind kind, bool isHovered, bool isFlipped, bool isFocused,
            List<SlottedResource?>? slotted, List<GameClock> clocks, string backText,
            IMGUIInteractionContext ui, SSNoirGameManager gameManager,
            bool isExecuting = false, float executeProgress = 0f, string executingText = "执行中",
            ActionReport? localRoll = null, int localRollPhase = 0, int localRollDisplayDieValue = 1, float localRollDisplayScale = 1f,
            CardPresentationResidue? residue = null)
        {
            var interaction = new CardInteraction { CardClicked = false, ClickedSlotIndex = -1, DroppedSlotIndex = -1, ExecuteClicked = false };
            bool disabled = node.Disabled;

            // 翻转卡（观察线索背面）：正交状态，独立绘制。
            if (isFlipped)
            {
                DrawFlippedCard(rect, node, backText, isHovered && !disabled, ui, ref interaction);
                if (disabled) interaction.CardClicked = false;
                return interaction;
            }

            // ── 无边悬浮族（地点 / 普通）：Ink + 硬投影，不走框架，整卡可点。
            if (kind == CardKind.Location || kind == CardKind.Common)
            {
                ContainerNodeDrawer.DrawFloating(rect, node, kind == CardKind.Location, isHovered && !disabled, disabled);
                DrawClockBadges(rect, clocks);
                if (!disabled && ui.WasClicked(rect))
                {
                    interaction.CardClicked = true;
                    Event.current.Use();
                }
                return interaction;
            }

            // ── 有框族（动作 / 人物相册页）：共享卡框架 + 内容。
            bool isCharacter = kind == CardKind.Character;
            Matrix4x4 oldMatrix = GUI.matrix;
            if (isCharacter && isFocused)
            {
                float rotationDeg = (node.Name.GetHashCode() % 2 == 0) ? -2f : 2f;
                GUIUtility.RotateAroundPivot(rotationDeg, rect.center);
            }

            DrawCardFrame(rect, isHovered, isFocused, disabled, isExecuting || localRoll != null);
            DrawClockBadges(rect, clocks);

            if (isCharacter)
            {
                ContainerNodeDrawer.DrawCharacter(rect, node, disabled);
                if (!disabled && ui.WasClicked(rect))
                {
                    interaction.CardClicked = true;
                    Event.current.Use();
                }
            }
            else
            {
                ActionNodeDrawer.DrawContent(
                    rect, node, slotted, ui, gameManager,
                    isExecuting, executeProgress, executingText,
                    localRoll, localRollPhase, localRollDisplayDieValue, localRollDisplayScale,
                    residue, ref interaction);
            }

            if (isCharacter && isFocused)
                GUI.matrix = oldMatrix;

            return interaction;
        }

        // ── 共享卡框架 ─────────────────────────────────────────────────

        // 无边悬浮：Ink 填充 + 硬投影，与地点/普通浮卡一致。去掉默认白边框与内双线，
        // 只保留「金框=选中/当前」「金光=正在发生」两个信号。存在感靠阴影，不靠线。
        private static void DrawCardFrame(Rect rect, bool isHovered, bool isFocused, bool disabled, bool isHappening)
        {
            IMGUIStyles.DrawShadow(rect, new Vector2(7f, 9f), 0.58f);

            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (isFocused && !disabled)
                IMGUIStyles.DrawOutline(rect, 2f, IMGUIStyles.Gold);

            if (isHappening && !disabled)
                IMGUIStyles.DrawGoldPulse(rect);
        }

        // ── 时钟徽章（卡右上角）──────────────────────────────────────

        private static void DrawClockBadges(Rect rect, List<GameClock>? clocks)
        {
            if (clocks == null || clocks.Count == 0) return;

            const float badgeH = 28f;
            const float gap = 8f;
            float maxRowW = rect.width - 24f;
            float perBadgeMaxW = (maxRowW - gap * (clocks.Count - 1)) / clocks.Count;
            perBadgeMaxW = Mathf.Clamp(perBadgeMaxW, 112f, 180f);

            float[] widths = new float[clocks.Count];
            float totalW = 0f;
            for (int i = 0; i < clocks.Count; i++)
            {
                widths[i] = Mathf.Min(MeasureNodeClockBadgeWidth(clocks[i]), perBadgeMaxW);
                totalW += widths[i];
            }
            totalW += gap * (clocks.Count - 1);

            float x = rect.x + (rect.width - totalW) * 0.5f;
            float y = rect.y + 8f;
            for (int i = 0; i < clocks.Count; i++)
            {
                DrawNodeClockBadge(new Rect(x, y, widths[i], badgeH), clocks[i]);
                x += widths[i] + gap;
            }
        }

        private static float MeasureNodeClockBadgeWidth(GameClock clock)
        {
            var labelStyle = new GUIStyle(IMGUIStyles.ClockLabel)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            IMGUIStyles.ApplyStrongFont(labelStyle);

            float labelW = labelStyle.CalcSize(new GUIContent(clock.Label)).x;
            float valueW = clock.Style switch
            {
                ClockStyle.Countdown => 44f,
                ClockStyle.Segments => Mathf.Max(0f, clock.Max * 10f + Mathf.Max(0, clock.Max - 1) * 5f),
                ClockStyle.Pie => 56f,
                _ => 44f
            };
            return 20f + labelW + 8f + valueW + 12f;
        }

        private static void DrawNodeClockBadge(Rect rect, GameClock clock)
        {
            Color activeColor = IMGUIStyles.Gold;
            Color inactiveColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f);
            Color outline = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.55f);

            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, 0.96f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, 1f, outline);

            var labelStyle = new GUIStyle(IMGUIStyles.ClockLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            IMGUIStyles.ApplyStrongFont(labelStyle);

            float valueW = clock.Style switch
            {
                ClockStyle.Countdown => 44f,
                ClockStyle.Segments => Mathf.Max(0f, clock.Max * 10f + Mathf.Max(0, clock.Max - 1) * 5f),
                ClockStyle.Pie => 56f,
                _ => 44f
            };
            float labelW = Mathf.Max(24f, rect.width - 20f - 8f - valueW);
            GUI.Label(new Rect(rect.x + 10f, rect.y, labelW, rect.height), clock.Label, labelStyle);

            float vx = rect.xMax - 10f - valueW;
            if (clock.Style == ClockStyle.Countdown)
            {
                var valueStyle = new GUIStyle(IMGUIStyles.ClockValue)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = IMGUIStyles.Gold }
                };
                GUI.Label(new Rect(vx, rect.y, valueW, rect.height), $"{clock.Current}/{clock.Max}", valueStyle);
            }
            else if (clock.Style == ClockStyle.Segments)
            {
                const float dot = 10f;
                const float spacing = 5f;
                float dotStartX = rect.xMax - 10f - valueW;
                float dotY = rect.y + (rect.height - dot) * 0.5f;
                for (int i = 0; i < clock.Max; i++)
                {
                    var dotRect = new Rect(dotStartX + i * (dot + spacing), dotY, dot, dot);
                    if (i < clock.Current)
                    {
                        GUI.color = activeColor;
                        GUI.DrawTexture(dotRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }
                    else
                    {
                        GUI.color = inactiveColor;
                        GUI.DrawTexture(dotRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        IMGUIStyles.DrawOutline(dotRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));
                    }
                    GUI.color = Color.white;
                }
            }
            else // Pie
            {
                float pieRadius = 9f;
                var pieRect = new Rect(vx, rect.center.y - pieRadius, pieRadius * 2f, pieRadius * 2f);
                float fillPct = clock.Max > 0 ? Mathf.Clamp01((float)clock.Current / clock.Max) : 0f;
                PieDrawer.DrawPieBadge(pieRect, fillPct, activeColor, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f));

                var fracStyle = new GUIStyle(IMGUIStyles.ClockValue)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = IMGUIStyles.Gold }
                };
                GUI.Label(new Rect(pieRect.xMax + 6f, rect.y, valueW - pieRect.width - 6f, rect.height), $"{clock.Current}/{clock.Max}", fracStyle);
            }
        }

        // ── 翻转卡（观察线索背面）──────────────────────────────────────

        // Ink 底 + 1px 纸白描边（70% 默认 / 100% 悬停）+ 标题 + 「已解读线索」+ 线索正文 + 返回提示。
        private static void DrawFlippedCard(Rect rect, GameNode node, string backText, bool isHovered, IMGUIInteractionContext ui,
            ref CardInteraction interaction)
        {
            Color outline = isHovered
                ? new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 1f)
                : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f);
            float thickness = isHovered ? 2f : 1f;

            IMGUIStyles.DrawShadow(rect, new Vector2(4f, 4f), 0.45f);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(rect, thickness, outline);

            GUI.Label(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, 20), node.Name, IMGUIStyles.FlippedTitle);
            GUI.Label(new Rect(rect.x + 8, rect.y + 28, rect.width - 16, 16), "— 已解读线索 —", IMGUIStyles.FlippedTip);

            var contentRect = new Rect(rect.x + 8, rect.y + 48, rect.width - 16, rect.height - 68);
            string clueText = node.Resolve != null ? node.Resolve.ObserveText : "";
            GUI.Label(contentRect, clueText, IMGUIStyles.FlippedContent);

            GUI.Label(new Rect(rect.x + 8, rect.y + rect.height - 18, rect.width - 16, 14), "点击返回", IMGUIStyles.FlippedTip);

            if (ui.WasClicked(rect))
            {
                interaction.CardClicked = true;
                Event.current.Use();
            }
        }

        // ── 残卡（行动结算后留在网格里的孤儿卡）────────────────────────

        // Ink 底 + 双线 + 结果盖印 + 结算标题 + 副文。卡锚点已不可见，仅展示历史结果。
        public static void DrawResidueCard(Rect rect, CardPresentationResidue residue)
        {
            IMGUIStyles.DrawShadow(rect, new Vector2(5f, 6f), 0.48f);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawDoubleOutline(rect, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.72f));

            // 结果盖印（右上角）：成=金、败=印章红。
            if (residue.RollOutcome == RollOutcome.Success || residue.RollOutcome == RollOutcome.Fail)
            {
                var sealRect = new Rect(rect.xMax - 60f, rect.y + 8f, 48f, 48f);
                Color sealColor = residue.RollOutcome == RollOutcome.Success ? IMGUIStyles.Gold : IMGUIStyles.SealRed;
                string sealText = residue.RollOutcome == RollOutcome.Success ? "成" : "败";
                IMGUIStyles.DrawStampSeal(sealRect, sealText, sealColor);
            }

            string label = residue.RollOutcome.HasValue ? FormatOutcomeLabel(residue.RollOutcome.Value) : "行动结果";
            var labelStyle = new GUIStyle(IMGUIStyles.CardSubtitle)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(rect.x + 12f, rect.y + 14f, rect.width - 24f, 22f), label, labelStyle);

            string title = residue.RollOutcome.HasValue
                ? $"{FormatOutcomeLabel(residue.RollOutcome.Value)}：{residue.Title}"
                : residue.Title;
            var titleStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 15,
                wordWrap = true,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = residue.RollOutcome.HasValue ? OutcomeColorFor(residue.RollOutcome.Value) : IMGUIStyles.TextPrimary }
            };
            GUI.Label(new Rect(rect.x + 18f, rect.y + 46f, rect.width - 36f, 42f), title, titleStyle);

            var subtitleStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = IMGUIStyles.TextSecondary }
            };
            GUI.Label(new Rect(rect.x + 18f, rect.y + 94f, rect.width - 36f, 34f), residue.Subtitle, subtitleStyle);

            DrawEffectRows(new Rect(rect.x + 18f, rect.y + 132f, rect.width - 36f, rect.yMax - rect.y - 144f), residue.Effects);
        }

        public static void DrawEffectRows(Rect area, IReadOnlyList<ActionEffectRecord> effects)
        {
            if (effects.Count == 0 || area.height <= 0f)
                return;

            const float rowHeight = 14f;
            const float rowGap = 16f;
            int maxRows = Mathf.Max(0, Mathf.FloorToInt(area.height / rowGap));
            if (maxRows == 0)
                return;

            int visibleCount = effects.Count <= maxRows ? effects.Count : maxRows - 1;
            for (int i = 0; i < visibleCount; i++)
                DrawSingleEffectRow(effects[i], new Rect(area.x, area.y + i * rowGap, area.width, rowHeight));

            if (effects.Count > maxRows)
            {
                var moreRect = new Rect(area.x, area.y + visibleCount * rowGap, area.width, rowHeight);
                Color accent = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.50f);
                DrawEffectRowBg(moreRect, accent);

                var style = new GUIStyle(IMGUIStyles.ModalBody)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = IMGUIStyles.TextSecondary }
                };
                GUI.Label(new Rect(moreRect.x + 8f, moreRect.y, moreRect.width - 16f, moreRect.height), $"+ 还有 {effects.Count - visibleCount} 项影响...", style);
            }
        }

        private static void DrawSingleEffectRow(ActionEffectRecord effect, Rect row)
        {
            Color accent = effect.Tone switch
            {
                ActionEffectTone.Positive => IMGUIStyles.OutcomeSuccess,
                ActionEffectTone.Negative => IMGUIStyles.OutcomeFail,
                _ => IMGUIStyles.TextSecondary
            };
            DrawEffectRowBg(row, accent);

            var labelStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = IMGUIStyles.TextPrimary }
            };

            if (effect.Kind == ActionEffectKind.Note)
            {
                GUI.Label(new Rect(row.x + 8f, row.y, row.width - 16f, row.height), effect.Text, labelStyle);
                return;
            }

            GUI.Label(new Rect(row.x + 8f, row.y, row.width - 56f, row.height), effect.Label, labelStyle);

            string value = effect.Delta.HasValue
                ? (effect.Delta.Value > 0 ? $"+{effect.Delta.Value}" : effect.Delta.Value.ToString())
                : string.Empty;
            var valueStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = accent }
            };
            IMGUIStyles.ApplyStrongFont(valueStyle);
            GUI.Label(new Rect(row.x + row.width - 52f, row.y, 44f, row.height), value, valueStyle);
        }

        private static void DrawEffectRowBg(Rect row, Color accent)
        {
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.16f);
            GUI.DrawTexture(row, Texture2D.whiteTexture);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(row.x, row.y, 3f, row.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static string FormatOutcomeLabel(RollOutcome outcome)
        {
            return outcome switch
            {
                RollOutcome.Success => "判定成功",
                RollOutcome.Neutral => "判定中性",
                RollOutcome.Fail => "判定失败",
                _ => "判定结果"
            };
        }

        private static Color OutcomeColorFor(RollOutcome outcome)
        {
            return outcome switch
            {
                RollOutcome.Success => IMGUIStyles.OutcomeSuccess,
                RollOutcome.Neutral => IMGUIStyles.OutcomeNeutral,
                RollOutcome.Fail => IMGUIStyles.OutcomeFail,
                _ => IMGUIStyles.TextPrimary
            };
        }
    }
}
