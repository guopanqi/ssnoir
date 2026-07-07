#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public static class GrowthPanelDrawer
    {
        public struct GrowthPanelInteraction
        {
            public bool ShouldClose;
        }

        private static readonly string[] StatKeys = { "violence", "knowledge", "sharpness", "social" };

        private static readonly Dictionary<string, string> StatLabels = new Dictionary<string, string>
        {
            { "violence", "力量" },
            { "knowledge", "见识" },
            { "sharpness", "敏锐" },
            { "social", "交际" },
        };

        public static Rect GetPanelRect()
        {
            float panelW = 640f;
            float panelH = 420f;
            float panelX = (UIScale.VW - panelW) / 2f;
            float panelY = (UIScale.VH - panelH) / 2f;
            return new Rect(panelX, panelY, panelW, panelH);
        }

        public static GrowthPanelInteraction Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            var interaction = new GrowthPanelInteraction { ShouldClose = false };
            var snapshot = gameManager.DisplayedSnapshot;

            var panelRect = GetPanelRect();
            float panelW = panelRect.width;
            float panelH = panelRect.height;
            float panelX = panelRect.x;
            float panelY = panelRect.y;

            // 纸物件（功能性窗口，保持水平）：Paper 底 @96% + 硬投影 (5,6) 黑 @50%
            IMGUIStyles.DrawShadow(panelRect, new Vector2(5f, 6f), 0.50f);
            GUI.color = IMGUIStyles.ModalBg;
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Main Title（纸上墨字，拉字距）
            GUI.Label(new Rect(panelX + 24f, panelY + 20f, 220f, 28f), "成 长 / 队 伍", IMGUIStyles.ModalTitle);

            // Close [X]：纸上次级按钮 = 1px 黑描边透明底
            float closeX = panelX + panelW - 44f;
            float closeY = panelY + 16f;
            var closeRect = new Rect(closeX, closeY, 28f, 28f);
            bool closeHover = ui.CanHover(closeRect);

            if (closeHover)
            {
                GUI.color = new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.08f);
                GUI.DrawTexture(closeRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            IMGUIStyles.DrawOutline(closeRect, 1f, closeHover
                ? IMGUIStyles.PaperInk
                : new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.55f));

            var closeStyle = new GUIStyle(IMGUIStyles.StatusLabel);
            closeStyle.alignment = TextAnchor.MiddleCenter;
            closeStyle.normal.textColor = closeHover ? IMGUIStyles.PaperInk : IMGUIStyles.PaperTextSecondary;
            GUI.Label(closeRect, "X", closeStyle);

            if (ui.WasClicked(closeRect))
            {
                interaction.ShouldClose = true;
                Event.current.Use();
            }

            // Divider（纸上单发丝线）
            IMGUIStyles.DrawLine(new Vector2(panelX + 24f, panelY + 58f), new Vector2(panelX + panelW - 24f, panelY + 58f),
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.35f), 1f);

            // Team Growth Level（纸上次级字）
            var levelStyle = new GUIStyle(IMGUIStyles.SectionLabel);
            levelStyle.normal.textColor = IMGUIStyles.PaperTextSecondary;
            GUI.Label(new Rect(panelX + 24f, panelY + 72f, 300f, 22f), $"队伍成长等级：{snapshot.GrowthLevel}", levelStyle);

            var actors = snapshot.Actors;
            float contentStartY = panelY + 110f;
            float colWidth = (panelW - 48f) / Mathf.Max(1, actors.Count);

            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                float colX = panelX + 24f + i * colWidth;

                // Vertical separator
                if (i > 0)
                {
                    IMGUIStyles.DrawLine(new Vector2(colX, contentStartY), new Vector2(colX, panelY + panelH - 24f),
                        new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.25f), 1f);
                }

                // Actor name（纸上墨字；暂离降为弱化字）
                Color nameColor = actor.Status == "away" ? IMGUIStyles.PaperTextDisabled : IMGUIStyles.PaperInk;
                var nameStyle = new GUIStyle(IMGUIStyles.CardTitle);
                nameStyle.normal.textColor = nameColor;
                GUI.Label(new Rect(colX + 16f, contentStartY + 4f, colWidth - 32f, 24f), actor.Name, nameStyle);

                // Status label if away（印章红只做高危/失败标记）
                if (actor.Status == "away")
                {
                    var awayStyle = new GUIStyle(IMGUIStyles.SectionLabel);
                    awayStyle.normal.textColor = IMGUIStyles.SealRed;
                    GUI.Label(new Rect(colX + 16f, contentStartY + 28f, colWidth - 32f, 18f), "[暂离]", awayStyle);
                }

                // Available points（有可用点 = 收益语义 → 金文字；金在纸上用深金字保证对比）
                int availPoints = snapshot.GrowthLevel - actor.SpentGrowthPoints;
                if (availPoints < 0) availPoints = 0;
                Color pointsColor = availPoints > 0 ? new Color(0.62f, 0.47f, 0.10f, 1f) : IMGUIStyles.PaperTextSecondary;
                var pointsStyle = new GUIStyle(IMGUIStyles.SectionLabel);
                pointsStyle.normal.textColor = pointsColor;
                GUI.Label(new Rect(colX + 16f, contentStartY + 50f, colWidth - 32f, 18f), $"可用：{availPoints}", pointsStyle);

                // Stats rows
                float rowStartY = contentStartY + 82f;
                float rowHeight = 38f;

                for (int s = 0; s < StatKeys.Length; s++)
                {
                    string statKey = StatKeys[s];
                    float rowY = rowStartY + s * rowHeight;

                    int statVal = actor.Stats.TryGetValue(statKey, out var val) ? val : 1;

                    // 中文属性名（力量/见识/敏锐/交际）。
                    string statLabel = StatLabels.TryGetValue(statKey, out var lbl) ? lbl : statKey;
                    GUI.Label(new Rect(colX + 16f, rowY + 4f, colWidth - 70f, 20f), $"{statLabel} {statVal}", IMGUIStyles.ModalBody);

                    // Upgrade [+] button：纸上主选项 = 黑底白字实心块；禁用 = 35% 黑描边
                    float btnSize = 22f;
                    float btnX = colX + colWidth - btnSize - 20f;
                    var btnRect = new Rect(btnX, rowY + 2f, btnSize, btnSize);

                    bool isEnabled = actor.Status != "away" && availPoints > 0 && statVal < 6;
                    bool btnHover = isEnabled && ui.CanHover(btnRect);

                    var btnStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                    if (isEnabled)
                    {
                        GUI.color = btnHover
                            ? new Color(IMGUIStyles.PaperInk.r * 1.8f, IMGUIStyles.PaperInk.g * 1.8f, IMGUIStyles.PaperInk.b * 1.8f, 1f)
                            : IMGUIStyles.PaperInk;
                        GUI.DrawTexture(btnRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                        btnStyle.normal.textColor = IMGUIStyles.Paper;
                    }
                    else
                    {
                        IMGUIStyles.DrawOutline(btnRect, 1f, new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.35f));
                        btnStyle.normal.textColor = IMGUIStyles.PaperTextDisabled;
                    }
                    GUI.Label(btnRect, "+", btnStyle);

                    if (isEnabled && ui.WasClicked(btnRect))
                    {
                        gameManager.UpgradeActorStat(actor.Id, statKey);
                        Event.current.Use();
                    }
                }
            }

            return interaction;
        }

    }
}
