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

        // ── 版面常量 ───────────────────────────────────────────────
        //
        // 这张面板的主语是「点数往哪儿花」，所以版面按**一个人一栏**组织，
        // 而栏宽是固定的：原来是「面板宽度 ÷ 人数」，队里只有一个人时那一栏就摊到 640，
        // 属性名贴在最左、加点按钮贴在最右，中间隔着大半个屏幕——它们明明是一件事的两半。
        // 现在栏宽由内容定，面板宽度反过来由栏数定，一个人时面板自己收窄。
        private const float ColW = 268f;
        private const float ColGap = 24f;
        private const float SidePad = 24f;
        private const float LevelRowH = 24f;
        private const float NameRowH = 30f;
        private const float PointsRowH = 26f;
        private const float NameToStats = 10f;
        private const float BottomPad = 24f;

        private static float StatRowH => UIScale.TouchHeight(36f);
        private static float ColumnH =>
            NameRowH + PointsRowH + NameToStats + StatRowH * StatKeys.Length;

        public static Rect GetPanelRect() => GetPanelRect(1);

        private static Rect GetPanelRect(int actorCount)
        {
            int columns = Mathf.Max(1, actorCount);
            float wanted = SidePad * 2f + ColW * columns + ColGap * (columns - 1);
            float wantedH = IMGUIStyles.ModalContentTop + LevelRowH + ColumnH + BottomPad;

            Rect safe = UIScale.SafeArea;
            float panelW = Mathf.Min(wanted, safe.width - 32f);
            float panelH = Mathf.Min(wantedH, safe.height - 24f);
            float panelX = safe.x + (safe.width - panelW) / 2f;
            float panelY = safe.y + (safe.height - panelH) / 2f;
            return new Rect(panelX, panelY, panelW, panelH);
        }

        public static GrowthPanelInteraction Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            var interaction = new GrowthPanelInteraction { ShouldClose = false };
            var snapshot = gameManager.DisplayedSnapshot;
            var actors = snapshot.Actors;

            var panelRect = GetPanelRect(actors.Count);

            // 外壳（压暗 / 纸底 / 标题 / X / 分隔线）和设置、卷宗共用一份。
            if (IMGUIStyles.DrawModalChrome(panelRect, "成 长", ui))
            {
                interaction.ShouldClose = true;
                return interaction;
            }

            // 等级那行是说明，不是标题：它解释的是下面每个人的「可用」从哪儿来。
            // 这条规则不写出来玩家猜不到——等级是队伍共有的，点数却是各人各花一份。
            var levelStyle = new GUIStyle(IMGUIStyles.SectionLabel)
            {
                fontSize = IMGUIStyles.FontSize(13),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = IMGUIStyles.PaperTextSecondary },
            };
            IMGUIStyles.DrawLabel(
                new Rect(panelRect.x + SidePad, panelRect.y + IMGUIStyles.ModalContentTop - 4f,
                         panelRect.width - SidePad * 2f, LevelRowH),
                $"成长等级 {snapshot.GrowthLevel}　每个人各自从这个等级里支取点数", levelStyle);

            float columnsW = ColW * actors.Count + ColGap * Mathf.Max(0, actors.Count - 1);
            float startX = panelRect.x + (panelRect.width - columnsW) / 2f;
            float columnY = panelRect.y + IMGUIStyles.ModalContentTop + LevelRowH;

            for (int i = 0; i < actors.Count; i++)
            {
                var column = new Rect(startX + i * (ColW + ColGap), columnY, ColW, ColumnH);
                if (i > 0)
                {
                    float lineX = column.x - ColGap * 0.5f;
                    IMGUIStyles.DrawLine(
                        new Vector2(lineX, column.y), new Vector2(lineX, column.yMax),
                        new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.25f), 1f);
                }
                DrawActorColumn(column, actors[i], snapshot.GrowthLevel, gameManager, ui);
            }

            return interaction;
        }

        private static void DrawActorColumn(
            Rect column, ActorSnapshot actor, int growthLevel,
            SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            bool away = actor.Status == "away";

            // 名字左对齐，不再居中：这一栏里所有东西都从同一条左边线起排，
            // 居中的名字会把这一栏读成两块互不相干的东西。
            var nameStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = away ? IMGUIStyles.PaperTextDisabled : IMGUIStyles.PaperInk },
            };
            IMGUIStyles.DrawLabel(new Rect(column.x, column.y, column.width - 60f, NameRowH), actor.Name, nameStyle);

            if (away)
            {
                var awayStyle = new GUIStyle(IMGUIStyles.SectionLabel)
                {
                    fontSize = IMGUIStyles.FontSize(12),
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = IMGUIStyles.SealRed },
                };
                IMGUIStyles.DrawLabel(new Rect(column.xMax - 60f, column.y, 60f, NameRowH), "暂离", awayStyle);
            }

            int availPoints = Mathf.Max(0, growthLevel - actor.SpentGrowthPoints);

            // 可用点数是这一栏的主语，做成实心墨牌——纸上唯一成片的深色就该给它和加点按钮。
            // 原来它是一行深金小字，和底下的属性名一个分量，看不出"这是你手里的东西"。
            var pointsRect = new Rect(column.x, column.y + NameRowH, 92f, PointsRowH - 4f);
            var pointsStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = IMGUIStyles.FontSize(13),
                alignment = TextAnchor.MiddleCenter,
            };
            IMGUIStyles.ApplyStrongFont(pointsStyle);
            if (availPoints > 0 && !away)
            {
                GUI.color = IMGUIStyles.PaperInk;
                GUI.DrawTexture(UIScale.PixelSnap(pointsRect), Texture2D.whiteTexture);
                GUI.color = Color.white;
                pointsStyle.normal.textColor = IMGUIStyles.Paper;
            }
            else
            {
                IMGUIStyles.DrawOutline(UIScale.PixelSnap(pointsRect), 1f,
                    new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.35f));
                pointsStyle.normal.textColor = IMGUIStyles.PaperTextSecondary;
            }
            IMGUIStyles.DrawLabel(pointsRect, $"可用 {availPoints}", pointsStyle);

            float rowY = column.y + NameRowH + PointsRowH + NameToStats;
            for (int s = 0; s < StatKeys.Length; s++)
                DrawStatRow(
                    new Rect(column.x, rowY + s * StatRowH, column.width, StatRowH),
                    actor, StatKeys[s], availPoints, away, gameManager, ui);
        }

        // 一行属性：名字 / 数值 / 加点按钮，三样紧挨着排在一条 200px 的带子里。
        // 原来按钮贴在整栏最右端，栏一宽它就和自己的属性名分了家——加点按钮属于那一行，
        // 不属于那一列的边。
        private static void DrawStatRow(
            Rect row, ActorSnapshot actor, string statKey, int availPoints, bool away,
            SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            if (!actor.Stats.TryGetValue(statKey, out int statVal))
                throw new InvalidOperationException($"Actor '{actor.Id}' is missing required stat '{statKey}'.");

            var labelStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = away ? IMGUIStyles.PaperTextDisabled : IMGUIStyles.PaperTextPrimary },
            };
            IMGUIStyles.ApplyStrongFont(labelStyle);
            // 能力名的中文只有一个来源：SkillInfo。面板里曾经自带一份同样的表，
            // 两份表迟早会各改各的。
            string statLabel = SkillInfo.DisplayName(statKey);
            IMGUIStyles.DrawLabel(new Rect(row.x, row.y, 64f, row.height), statLabel, labelStyle);

            var valueStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleRight };
            IMGUIStyles.DrawLabel(new Rect(row.x + 64f, row.y, 28f, row.height), statVal.ToString(), valueStyle);

            int upgradeCost = TeamState.GetStatUpgradeCost(statVal);
            bool maxed = upgradeCost <= 0;
            bool enabled = !away && !maxed && availPoints >= upgradeCost;

            float btnSize = Mathf.Min(UIScale.TouchHeight(24f), row.height - 6f);
            var btnRect = new Rect(row.x + 108f, row.y + (row.height - btnSize) * 0.5f, btnSize + 12f, btnSize);
            bool hover = enabled && ui.CanHover(btnRect);

            var btnStyle = new GUIStyle(IMGUIStyles.SlotLabel) { alignment = TextAnchor.MiddleCenter };
            if (enabled)
            {
                GUI.color = hover
                    ? new Color(IMGUIStyles.PaperInk.r * 1.8f, IMGUIStyles.PaperInk.g * 1.8f, IMGUIStyles.PaperInk.b * 1.8f, 1f)
                    : IMGUIStyles.PaperInk;
                GUI.DrawTexture(UIScale.PixelSnap(btnRect), Texture2D.whiteTexture);
                GUI.color = Color.white;
                btnStyle.normal.textColor = IMGUIStyles.Paper;
            }
            else
            {
                IMGUIStyles.DrawOutline(UIScale.PixelSnap(btnRect), 1f,
                    new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.35f));
                btnStyle.normal.textColor = IMGUIStyles.PaperTextDisabled;
            }
            IMGUIStyles.DrawLabel(btnRect, maxed ? "满" : $"+{upgradeCost}", btnStyle);

            if (enabled && ui.WasTapped(btnRect))
            {
                gameManager.UpgradeActorStat(actor.Id, statKey);
                Event.current.Use();
            }
        }
    }
}
