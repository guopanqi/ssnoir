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

            float badgeX = rect.x + rect.width - 6;
            float badgeY = rect.y + 4;
            foreach (var clock in clocks)
            {
                DrawClockBadge(ref badgeX, badgeY, clock);
            }
        }

        private static void DrawClockBadge(ref float rightX, float topY, GameClock clock)
        {
            Color activeColor = IMGUIStyles.Gold;
            Color inactiveColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f);

            if (clock.Style == ClockStyle.Countdown)
            {
                string text = $"{clock.Label} {clock.Current}/{clock.Max}";
                float textWidth = 80;
                float badgeW = textWidth + 8;
                float badgeH = 14;
                float badgeX = rightX - badgeW;
                float badgeY = topY;

                GUI.color = IMGUIStyles.Ink;
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));

                var style = new GUIStyle(IMGUIStyles.ClockLabel)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleCenter
                };
                GUI.Label(new Rect(badgeX, badgeY, badgeW, badgeH), text, style);

                rightX -= (badgeW + 4);
            }
            else if (clock.Style == ClockStyle.Segments)
            {
                string labelText = clock.Label;
                float labelWidth = 40;
                int dotSize = 5;
                int spacing = 2;
                float dotsW = clock.Max * (dotSize + spacing) - spacing;
                float badgeW = labelWidth + 6 + dotsW + 6;
                float badgeH = 14;
                float badgeX = rightX - badgeW;
                float badgeY = topY;

                GUI.color = IMGUIStyles.Ink;
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));

                var style = new GUIStyle(IMGUIStyles.ClockLabel) { fontSize = 10 };
                GUI.Label(new Rect(badgeX + 4, badgeY, labelWidth, badgeH), labelText, style);

                float dotStartX = badgeX + 4 + labelWidth + 4;
                for (int i = 0; i < clock.Max; i++)
                {
                    var dotRect = new Rect(dotStartX + i * (dotSize + spacing), badgeY + (badgeH - dotSize) / 2f, dotSize, dotSize);
                    if (i < clock.Current)
                    {
                        GUI.color = activeColor;
                        GUI.DrawTexture(dotRect, Texture2D.whiteTexture);
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

                rightX -= (badgeW + 4);
            }
            else // Pie
            {
                string labelText = clock.Label;
                float labelWidth = 40;
                float pieRadius = 10f;
                float badgeW = labelWidth + 6 + pieRadius * 2 + 6;
                float badgeH = 14;
                float badgeX = rightX - badgeW;
                float badgeY = topY;

                GUI.color = IMGUIStyles.Ink;
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeW, badgeH), Texture2D.whiteTexture);
                GUI.color = Color.white;
                IMGUIStyles.DrawOutline(new Rect(badgeX, badgeY, badgeW, badgeH), 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.40f));

                var style = new GUIStyle(IMGUIStyles.ClockLabel) { fontSize = 10 };
                GUI.Label(new Rect(badgeX + 4, badgeY, labelWidth, badgeH), labelText, style);

                float pieX = badgeX + 4 + labelWidth + 4;
                float pieY = badgeY + (badgeH - pieRadius * 2) / 2f;
                var pieRect = new Rect(pieX, pieY, pieRadius * 2, pieRadius * 2);
                float fillPct = clock.Max > 0 ? Mathf.Clamp01((float)clock.Current / clock.Max) : 0f;
                PieDrawer.DrawPieBadge(pieRect, fillPct, activeColor, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.70f));

                rightX -= (badgeW + 4);
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
            GUI.Label(new Rect(rect.x + 18f, rect.y + 94f, rect.width - 36f, rect.height - 106f), residue.Subtitle, subtitleStyle);
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
