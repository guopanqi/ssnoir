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

        private static readonly string[] StatKeys = { "violence", "knowledge", "coding", "sharpness" };

        public static GrowthPanelInteraction Draw(SSNoirGameManager gameManager, Vector2 mousePos)
        {
            var interaction = new GrowthPanelInteraction { ShouldClose = false };
            var snapshot = gameManager.DisplayedSnapshot;

            // Layout coordinates identical to original
            float panelW = 640f;
            float panelH = 420f;
            float panelX = (Screen.width - panelW) / 2f;
            float panelY = (Screen.height - panelH) / 2f;
            var panelRect = new Rect(panelX, panelY, panelW, panelH);

            // 1. Dark tech blueprint background (85% opacity SurfaceColor)
            GUI.color = new Color(IMGUIStyles.SurfaceColor.r, IMGUIStyles.SurfaceColor.g, IMGUIStyles.SurfaceColor.b, 0.85f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            
            // Strictly rectangular 1px borders (no roundedness)
            IMGUIStyles.DrawOutline(panelRect, 1f, IMGUIStyles.PrimaryColor);

            // 2. L-shaped technical corners at the corners of the window
            DrawTechCorners(panelRect, IMGUIStyles.PrimaryColor, 15f, 1f);

            // 3. Grid Background lines (very subtle technical layout backdrop)
            DrawBackgroundGrid(panelRect);

            // 4. Tech Scan Line animation running over the panel
            IMGUIStyles.DrawScanLine(panelRect, new Color(IMGUIStyles.PrimaryColor.r, IMGUIStyles.PrimaryColor.g, IMGUIStyles.PrimaryColor.b, 0.05f), speed: 90f, thickness: 1f);

            // Main Title
            GUI.Label(new Rect(panelX + 24f, panelY + 20f, 200f, 28f), "成长 / 队伍", IMGUIStyles.ModalTitle);

            // Close [X] - Technical button style: rectangular, 1px border, 10% Primary/Error tint on hover
            float closeX = panelX + panelW - 44f;
            float closeY = panelY + 16f;
            var closeRect = new Rect(closeX, closeY, 28f, 28f);
            bool closeHover = closeRect.Contains(mousePos);

            Color closeBorder = closeHover ? Color.white : IMGUIStyles.PrimaryColor;
            Color closeBg = closeHover ? new Color(IMGUIStyles.ErrorColor.r, IMGUIStyles.ErrorColor.g, IMGUIStyles.ErrorColor.b, 0.10f) : Color.clear;
            Color closeTextColor = closeHover ? IMGUIStyles.ErrorColor : IMGUIStyles.OnSurfaceVariant;

            if (closeHover)
            {
                GUI.color = closeBg;
                GUI.DrawTexture(closeRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            IMGUIStyles.DrawOutline(closeRect, 1f, closeBorder);

            var closeStyle = new GUIStyle(IMGUIStyles.SlotLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            closeStyle.normal.textColor = closeTextColor;
            GUI.Label(closeRect, "X", closeStyle);

            if (closeHover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                interaction.ShouldClose = true;
                Event.current.Use();
            }

            // Divider - Technical double hairline
            IMGUIStyles.DrawLine(new Vector2(panelX + 24f, panelY + 58f), new Vector2(panelX + panelW - 24f, panelY + 58f), IMGUIStyles.OutlineVariantColor, 1f);
            IMGUIStyles.DrawLine(new Vector2(panelX + 24f, panelY + 60f), new Vector2(panelX + panelW - 24f, panelY + 60f), new Color(IMGUIStyles.OutlineVariantColor.r, IMGUIStyles.OutlineVariantColor.g, IMGUIStyles.OutlineVariantColor.b, 0.4f), 1f);

            // Team Growth Level
            GUI.Label(new Rect(panelX + 24f, panelY + 72f, 300f, 22f), $"队伍成长等级：{snapshot.GrowthLevel}", IMGUIStyles.SectionLabel);

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
                    IMGUIStyles.DrawLine(new Vector2(colX, contentStartY), new Vector2(colX, panelY + panelH - 24f), IMGUIStyles.OutlineVariantColor, 1f);
                }

                // Actor name
                Color nameColor = actor.Status == "away" ? new Color(0.5f, 0.5f, 0.5f, 1f) : Color.white;
                var nameStyle = new GUIStyle(IMGUIStyles.CardTitle);
                nameStyle.normal.textColor = nameColor;
                GUI.Label(new Rect(colX + 16f, contentStartY + 4f, colWidth - 32f, 24f), actor.Name, nameStyle);

                // Status label if away
                if (actor.Status == "away")
                {
                    var awayStyle = new GUIStyle(IMGUIStyles.SectionLabel);
                    awayStyle.normal.textColor = IMGUIStyles.ErrorColor;
                    GUI.Label(new Rect(colX + 16f, contentStartY + 28f, colWidth - 32f, 18f), "[暂离]", awayStyle);
                }

                // Available points
                int availPoints = snapshot.GrowthLevel - actor.SpentGrowthPoints;
                if (availPoints < 0) availPoints = 0;
                Color pointsColor = availPoints > 0 ? new Color(0.39f, 0.90f, 0.47f, 1f) : IMGUIStyles.OnSurfaceVariant;
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

                    // Display active system variables as English names (knowledge, coding, etc.)
                    GUI.Label(new Rect(colX + 16f, rowY + 4f, colWidth - 70f, 20f), $"{statKey.ToUpper()} {statVal}", IMGUIStyles.ModalBody);

                    // Upgrade [+] button - Technical Button style: rectangular, 1px border, 10% Primary tint on hover
                    float btnSize = 22f;
                    float btnX = colX + colWidth - btnSize - 20f;
                    var btnRect = new Rect(btnX, rowY + 2f, btnSize, btnSize);

                    bool isEnabled = actor.Status != "away" && availPoints > 0 && statVal < 6;
                    bool btnHover = isEnabled && btnRect.Contains(mousePos);

                    Color btnBg = isEnabled ? (btnHover ? new Color(IMGUIStyles.PrimaryColor.r, IMGUIStyles.PrimaryColor.g, IMGUIStyles.PrimaryColor.b, 0.10f) : Color.clear) : Color.clear;
                    Color btnBorder = isEnabled ? (btnHover ? Color.white : IMGUIStyles.PrimaryColor) : IMGUIStyles.OutlineVariantColor;

                    if (btnHover)
                    {
                        GUI.color = btnBg;
                        GUI.DrawTexture(btnRect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }
                    IMGUIStyles.DrawOutline(btnRect, 1f, btnBorder);

                    var btnStyle = new GUIStyle(IMGUIStyles.SlotLabel)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                    btnStyle.normal.textColor = isEnabled ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    GUI.Label(btnRect, "+", btnStyle);

                    if (isEnabled && btnHover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        gameManager.UpgradeActorStat(actor.Id, statKey);
                        Event.current.Use();
                    }
                }
            }

            return interaction;
        }

        private static void DrawTechCorners(Rect rect, Color color, float length, float thickness)
        {
            // Top-left
            IMGUIStyles.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.x + length, rect.y), color, thickness);
            IMGUIStyles.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.x, rect.y + length), color, thickness);
            // Top-right
            IMGUIStyles.DrawLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax - length, rect.y), color, thickness);
            IMGUIStyles.DrawLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax, rect.y + length), color, thickness);
            // Bottom-left
            IMGUIStyles.DrawLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.x + length, rect.yMax), color, thickness);
            IMGUIStyles.DrawLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.x, rect.yMax - length), color, thickness);
            // Bottom-right
            IMGUIStyles.DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax - length, rect.yMax), color, thickness);
            IMGUIStyles.DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMax - length), color, thickness);
        }

        private static void DrawBackgroundGrid(Rect rect)
        {
            Color gridColor = new Color(IMGUIStyles.PrimaryColor.r, IMGUIStyles.PrimaryColor.g, IMGUIStyles.PrimaryColor.b, 0.02f);
            
            // 4 vertical grid lines
            float vSpacing = rect.width / 5f;
            for (int i = 1; i <= 4; i++)
            {
                float x = rect.x + i * vSpacing;
                IMGUIStyles.DrawLine(new Vector2(x, rect.y + 2f), new Vector2(x, rect.yMax - 2f), gridColor, 1f);
            }

            // 3 horizontal grid lines
            float hSpacing = rect.height / 4f;
            for (int i = 1; i <= 3; i++)
            {
                float y = rect.y + i * hSpacing;
                IMGUIStyles.DrawLine(new Vector2(rect.x + 2f, y), new Vector2(rect.xMax - 2f, y), gridColor, 1f);
            }
        }
    }
}
