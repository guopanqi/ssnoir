#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 成长：尼尔手里一切「不是运气」的东西，摊在一张纸上，横屏切成左右两半。
    ///
    /// 左半是**这个人**：一块相纸黑上的霓虹半身像、展签式的一行小字，底下四个墨方块——
    /// 四项能力画成四个骰面（大字数值在上、名字在下），加点按钮贴在各自底下。
    /// 这就是手牌方块那一族，玩家在交锋里天天看的东西，不是一张属性表。
    ///
    /// 右半是**他认识的人**：每个给过支援的人一张名片——左边一小块相纸上是他的半身像，
    /// 右边人名、那条办法叫什么、干什么用。办法来自和这个人的关系，所以人像必须在卡上：
    /// 玩家看见的是「弗兰克给的」，不是一条无主的技能。
    /// 带着的那张金描边（金描边＝当前，全屏只此一处），点别的名片就是换人；交锋里只读。
    /// 一个人以后可能给不止一条办法，所以名片按人分，办法列在名片里。
    ///
    /// 没有「圈子声誉」那一段：你和谁的关系就是你和那一片的关系，这一页右半已经说完了。
    /// </summary>
    public static class GrowthPanelDrawer
    {
        public struct GrowthPanelInteraction
        {
            public bool ShouldClose;
        }

        private static readonly string[] StatKeys = { "violence", "knowledge", "sharpness", "social" };

        // ── 版面 ───────────────────────────────────────────────────
        private const float PanelW = 780f;
        private const float PanelH = 450f;
        private const float SidePad = 26f;
        private const float ColGap = 30f;
        private const float LeftW = 300f;

        private const float PhotoH = 150f;
        private const float CaptionH = 22f;
        private const float DieSize = 58f;
        private const float DieGap = 14f;
        private const float DieLabelH = 20f;
        private const float DieBtnH = 24f;
        private const float CompanionRowH = 30f;

        private static Vector2 _scroll;

        public static Rect GetPanelRect()
        {
            Rect safe = UIScale.SafeArea;
            float w = Mathf.Min(PanelW, safe.width - 32f);
            float h = Mathf.Min(PanelH, safe.height - 24f);
            return new Rect(safe.x + (safe.width - w) / 2f, safe.y + (safe.height - h) / 2f, w, h);
        }

        public static GrowthPanelInteraction Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            var interaction = new GrowthPanelInteraction { ShouldClose = false };
            var snapshot = gameManager.DisplayedSnapshot;
            var panel = GetPanelRect();

            if (IMGUIStyles.DrawModalChrome(panel, "成 长", ui))
            {
                interaction.ShouldClose = true;
                return interaction;
            }

            float top = panel.y + IMGUIStyles.ModalContentTop;
            float bottom = panel.yMax - 22f;
            var left = new Rect(panel.x + SidePad, top, LeftW, bottom - top);
            float dividerX = left.xMax + ColGap * 0.5f;
            var right = new Rect(left.xMax + ColGap, top, panel.xMax - SidePad - (left.xMax + ColGap), bottom - top);

            IMGUIStyles.DrawLine(new Vector2(dividerX, top), new Vector2(dividerX, bottom),
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.25f), 1f);

            DrawPerson(left, snapshot, gameManager, ui);
            DrawPeople(right, snapshot, gameManager, ui);
            return interaction;
        }

        // ── 左：这个人 ──────────────────────────────────────────────

        private static void DrawPerson(Rect col, PresentationSnapshot snapshot,
            SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            if (snapshot.Actors.Count == 0) return;
            var lead = snapshot.Actors[0];
            int avail = Mathf.Max(0, snapshot.GrowthLevel - lead.SpentGrowthPoints);

            // 相纸块：纸上唯一一块成片的黑，人物从里头亮出来。
            var photo = UIScale.PixelSnap(new Rect(col.x, col.y, col.width, PhotoH));
            GUI.color = IMGUIStyles.PhotoBlack;
            GUI.DrawTexture(photo, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var neon = NeonPortraitLibrary.Load(lead.Name);
            if (neon != null)
            {
                var uv = NeonPortraitLibrary.BustCrop;
                float bustH = PhotoH * 1.12f;
                float bustW = bustH * (uv.width / uv.height);
                GUI.BeginGroup(photo);
                HandPanelDrawer.DrawNeonBust(
                    new Rect((photo.width - bustW) / 2f, photo.height - bustH + 10f, bustW, bustH), neon, uv);
                GUI.EndGroup();
            }
            else
            {
                var big = new GUIStyle(IMGUIStyles.CardTitle)
                {
                    fontSize = IMGUIStyles.FontSize(28),
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = IMGUIStyles.Paper },
                };
                IMGUIStyles.DrawLabel(photo, lead.Name, big);
            }

            // 展签：名字在左，成长读数在右。可用点数是墨牌——纸上唯一成片的深色给它和加点钮。
            float y = photo.yMax + 8f;
            var nameStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                fontSize = IMGUIStyles.FontSize(18),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = IMGUIStyles.PaperInk },
            };
            IMGUIStyles.DrawLabel(new Rect(col.x, y, 120f, CaptionH), lead.Name, nameStyle);

            var levelStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = IMGUIStyles.FontSize(12),
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = IMGUIStyles.PaperTextSecondary },
            };
            var pointsRect = UIScale.PixelSnap(new Rect(col.xMax - 66f, y + 1f, 66f, CaptionH - 2f));
            IMGUIStyles.DrawLabel(new Rect(col.x + 120f, y, pointsRect.x - 8f - (col.x + 120f), CaptionH),
                $"成长 {snapshot.GrowthLevel}", levelStyle);
            var pointsStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = IMGUIStyles.FontSize(12),
                alignment = TextAnchor.MiddleCenter,
            };
            IMGUIStyles.ApplyStrongFont(pointsStyle);
            if (avail > 0)
            {
                GUI.color = IMGUIStyles.PaperInk;
                GUI.DrawTexture(pointsRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                pointsStyle.normal.textColor = IMGUIStyles.Paper;
            }
            else
            {
                IMGUIStyles.DrawOutline(pointsRect, 1f, InkAlpha(0.35f));
                pointsStyle.normal.textColor = IMGUIStyles.PaperTextSecondary;
            }
            IMGUIStyles.DrawLabel(pointsRect, $"可用 {avail}", pointsStyle);

            // 四个骰面。
            y += CaptionH + 14f;
            float rowW = DieSize * 4f + DieGap * 3f;
            float x0 = col.x + (col.width - rowW) / 2f;
            for (int i = 0; i < StatKeys.Length; i++)
                DrawDieFace(new Rect(x0 + i * (DieSize + DieGap), y, DieSize, 0f), lead, StatKeys[i], avail,
                    away: lead.Status == "away", gameManager, ui);
            y += DieSize + 6f + DieLabelH + 4f + DieBtnH;

            // 同伴：一人一行小字，只展示四个数值。加点只负责尼尔一个人，
            // 这里没有加点钮。不给他们相纸。
            for (int a = 1; a < snapshot.Actors.Count; a++)
            {
                y += 12f;
                DrawCompanionRow(new Rect(col.x, y, col.width, CompanionRowH), snapshot.Actors[a]);
                y += CompanionRowH;
            }
        }

        // 一个骰面：墨方块里一个大数，底下能力名，再底下加点钮。
        private static void DrawDieFace(Rect slot, ActorSnapshot actor, string statKey, int avail, bool away,
            SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            if (!actor.Stats.TryGetValue(statKey, out int val))
                throw new InvalidOperationException($"Actor '{actor.Id}' is missing required stat '{statKey}'.");

            var face = UIScale.PixelSnap(new Rect(slot.x, slot.y, DieSize, DieSize));
            // 硬投影 + 墨面：和手牌方块同一族。
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(face.x + 3f, face.y + 3f, face.width, face.height), Texture2D.whiteTexture);
            GUI.color = away ? IMGUIStyles.PaperTextDisabled : IMGUIStyles.PaperInk;
            GUI.DrawTexture(face, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var valueStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                fontSize = IMGUIStyles.FontSize(28),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = IMGUIStyles.Paper },
            };
            IMGUIStyles.DrawLabel(face, val.ToString(), valueStyle);

            var labelStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = IMGUIStyles.FontSize(13),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = away ? IMGUIStyles.PaperTextDisabled : IMGUIStyles.PaperTextPrimary },
            };
            IMGUIStyles.ApplyStrongFont(labelStyle);
            IMGUIStyles.DrawLabel(new Rect(face.x - 6f, face.yMax + 6f, face.width + 12f, DieLabelH),
                SkillInfo.DisplayName(statKey), labelStyle);

            DrawUpgradeButton(new Rect(face.x + 6f, face.yMax + 6f + DieLabelH + 4f, face.width - 12f, DieBtnH),
                actor, statKey, val, avail, away, gameManager, ui);
        }

        // 同伴行纯展示：名字加四个数值，没有加点钮、不吃 ui 和 gameManager。
        private static void DrawCompanionRow(Rect row, ActorSnapshot actor)
        {
            bool away = actor.Status == "away";
            var nameStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = IMGUIStyles.FontSize(13),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = away ? IMGUIStyles.PaperTextDisabled : IMGUIStyles.PaperInk },
            };
            IMGUIStyles.ApplyStrongFont(nameStyle);
            IMGUIStyles.DrawLabel(new Rect(row.x, row.y, 56f, row.height), away ? actor.Name + "·暂离" : actor.Name, nameStyle);

            float cellW = (row.width - 60f) / StatKeys.Length;
            for (int i = 0; i < StatKeys.Length; i++)
            {
                if (!actor.Stats.TryGetValue(StatKeys[i], out int val))
                    throw new InvalidOperationException($"Actor '{actor.Id}' is missing required stat '{StatKeys[i]}'.");
                float cx = row.x + 60f + i * cellW;
                var v = new GUIStyle(IMGUIStyles.ModalBody)
                {
                    fontSize = IMGUIStyles.FontSize(13),
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = away ? IMGUIStyles.PaperTextDisabled : IMGUIStyles.PaperTextPrimary },
                };
                IMGUIStyles.DrawLabel(new Rect(cx, row.y, cellW, row.height),
                    $"{SkillInfo.DisplayName(StatKeys[i]).Substring(0, 1)}{val}", v);
            }
        }

        // 加点钮：够点是实心墨牌（主行动），不够是淡描边，满了写「满」。
        private static void DrawUpgradeButton(Rect btn, ActorSnapshot actor, string statKey, int val, int avail,
            bool away, SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            int cost = TeamState.GetStatUpgradeCost(val);
            bool maxed = cost <= 0;
            bool enabled = !away && !maxed && avail >= cost;
            btn = UIScale.PixelSnap(btn);
            var style = new GUIStyle(IMGUIStyles.SlotLabel) { alignment = TextAnchor.MiddleCenter };
            if (enabled)
            {
                bool hover = ui.CanHover(btn);
                GUI.color = hover ? Color.Lerp(IMGUIStyles.PaperInk, IMGUIStyles.Paper, 0.25f) : IMGUIStyles.PaperInk;
                GUI.DrawTexture(btn, Texture2D.whiteTexture);
                GUI.color = Color.white;
                style.normal.textColor = IMGUIStyles.Paper;
            }
            else
            {
                IMGUIStyles.DrawOutline(btn, 1f, InkAlpha(0.3f));
                style.normal.textColor = IMGUIStyles.PaperTextDisabled;
            }
            IMGUIStyles.DrawLabel(btn, maxed ? "满" : $"+{cost}", style);
            if (enabled && ui.WasTapped(btn))
            {
                gameManager.UpgradeActorStat(actor.Id, statKey);
                Event.current.Use();
            }
        }

        // ── 右：他认识的人 ──────────────────────────────────────────

        private const float CardPad = 12f;
        private const float CardGap = 10f;
        private const float CardPhoto = 72f;   // 名片左侧那块相纸的边长
        private const float CardPhotoGap = 12f;

        private static GUIStyle PersonStyle() => new GUIStyle(IMGUIStyles.CardTitle)
        {
            fontSize = IMGUIStyles.FontSize(16),
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = IMGUIStyles.PaperInk },
        };

        private static GUIStyle AbilityStyle() => new GUIStyle(IMGUIStyles.ModalBody)
        {
            fontSize = IMGUIStyles.FontSize(14),
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = IMGUIStyles.PaperInk },
        };

        private static GUIStyle DescStyle() => new GUIStyle(IMGUIStyles.ModalBody)
        {
            fontSize = IMGUIStyles.FontSize(12),
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = IMGUIStyles.PaperTextSecondary },
        };

        private static float CardTextWidth(float cardW) => cardW - CardPad * 2f - CardPhoto - CardPhotoGap;

        private static float CardHeight(SupportEntry e, float width)
        {
            float textH = 22f + 24f + DescStyle().CalcHeight(new GUIContent(e.Description), CardTextWidth(width));
            return CardPad + Mathf.Max(CardPhoto, textH) + CardPad;
        }

        // 名片上的人像：一小块相纸黑，人物从里头亮出来。没有立绘就写名字的第一个字。
        private static void DrawCardPhoto(Rect photo, string personName)
        {
            photo = UIScale.PixelSnap(photo);
            GUI.color = IMGUIStyles.PhotoBlack;
            GUI.DrawTexture(photo, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var neon = NeonPortraitLibrary.Load(personName);
            if (neon != null)
            {
                var uv = NeonPortraitLibrary.BustCrop;
                float bustH = photo.height * 1.18f;
                float bustW = bustH * (uv.width / uv.height);
                GUI.BeginGroup(photo);
                HandPanelDrawer.DrawNeonBust(
                    new Rect((photo.width - bustW) / 2f, photo.height - bustH + 6f, bustW, bustH), neon, uv);
                GUI.EndGroup();
            }
            else
            {
                var s = new GUIStyle(IMGUIStyles.CardTitle)
                {
                    fontSize = IMGUIStyles.FontSize(24),
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = IMGUIStyles.Paper },
                };
                IMGUIStyles.DrawLabel(photo, personName.Length > 0 ? personName.Substring(0, 1) : "?", s);
            }
        }

        private static void DrawPeople(Rect col, PresentationSnapshot snapshot,
            SSNoirGameManager gameManager, IMGUIInteractionContext ui)
        {
            // 段头：标题 + 一句规则。
            var head = new GUIStyle(IMGUIStyles.CardTitle)
            {
                fontSize = IMGUIStyles.FontSize(15),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = IMGUIStyles.PaperInk },
            };
            var note = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = IMGUIStyles.FontSize(12),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = IMGUIStyles.PaperTextSecondary },
            };
            IMGUIStyles.DrawLabel(new Rect(col.x, col.y, 70f, 22f), "支 援", head);
            IMGUIStyles.DrawLabel(new Rect(col.x + 70f, col.y, col.width - 70f, 22f),
                snapshot.IsInEncounter ? "这一场带着谁，进场时就定了" : "一场只带一个，出门前选", note);

            var viewport = new Rect(col.x, col.y + 30f, col.width, col.height - 30f);
            if (snapshot.Supports.Count == 0)
            {
                // 空位画成虚描边的一张空名片：告诉玩家这里以后会有东西。
                var empty = UIScale.PixelSnap(new Rect(viewport.x, viewport.y, viewport.width, 72f));
                DrawDashedOutline(empty, InkAlpha(0.35f));
                var es = new GUIStyle(DescStyle())
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = IMGUIStyles.PaperTextDisabled },
                };
                IMGUIStyles.DrawLabel(empty, "还没有人把自己的办法交给你。\n人物的事办到某一步，他们的名片会放在这儿。", es);
                return;
            }

            float innerW = viewport.width - 10f;
            float contentH = 0f;
            foreach (var e in snapshot.Supports) contentH += CardHeight(e, innerW) + CardGap;
            bool scrolls = contentH > viewport.height;
            float cardW = scrolls ? innerW : viewport.width;

            _scroll = GUI.BeginScrollView(viewport, _scroll, new Rect(0f, 0f, cardW, contentH));
            float y = 0f;
            string picked = string.Empty;
            foreach (var e in snapshot.Supports)
            {
                float h = CardHeight(e, cardW);
                var card = UIScale.PixelSnap(new Rect(0f, y, cardW, h));
                var screen = new Rect(card.x + viewport.x - _scroll.x, card.y + viewport.y - _scroll.y, card.width, card.height);
                bool canPick = !snapshot.IsInEncounter && !e.Carried;
                bool hover = canPick && ui.CanHover(screen);

                if (e.Carried)
                {
                    IMGUIStyles.DrawOutline(card, 2f, IMGUIStyles.Gold);
                    // 右上角金签「带着」。
                    var tag = new Rect(card.xMax - 44f, card.y, 44f, 18f);
                    GUI.color = IMGUIStyles.Gold;
                    GUI.DrawTexture(tag, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    var ts = new GUIStyle(IMGUIStyles.SlotLabel)
                    {
                        fontSize = IMGUIStyles.FontSize(11),
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = IMGUIStyles.GoldOnDark },
                    };
                    IMGUIStyles.DrawLabel(tag, "带着", ts);
                }
                else
                {
                    if (hover)
                    {
                        GUI.color = InkAlpha(0.06f);
                        GUI.DrawTexture(card, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }
                    IMGUIStyles.DrawOutline(card, 1f, InkAlpha(canPick ? 0.8f : 0.3f));
                }

                DrawCardPhoto(new Rect(card.x + CardPad, card.y + CardPad, CardPhoto, CardPhoto), e.Id);

                float tx = card.x + CardPad + CardPhoto + CardPhotoGap;
                float tw = CardTextWidth(card.width);
                float ty = card.y + CardPad;
                IMGUIStyles.DrawLabel(new Rect(tx, ty, tw - 50f, 20f), e.Id, PersonStyle());
                ty += 22f;
                IMGUIStyles.DrawLabel(new Rect(tx, ty, tw, 20f), $"《{e.Title}》", AbilityStyle());
                ty += 24f;
                IMGUIStyles.DrawLabel(new Rect(tx, ty, tw, card.yMax - CardPad - ty), e.Description, DescStyle());

                if (canPick && ui.WasTapped(screen))
                {
                    picked = e.Id;
                    Event.current.Use();
                }
                y += h + CardGap;
            }
            GUI.EndScrollView();

            if (picked.Length > 0)
                gameManager.SetCarriedSupport(picked);
        }

        private static Color InkAlpha(float a) =>
            new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, a);

        // 虚线框：按段画短线。只给空名片用，别处的描边都是实线。
        private static void DrawDashedOutline(Rect r, Color color)
        {
            const float dash = 6f, gap = 4f;
            for (float x = r.x; x < r.xMax; x += dash + gap)
            {
                float w = Mathf.Min(dash, r.xMax - x);
                IMGUIStyles.DrawLine(new Vector2(x, r.y), new Vector2(x + w, r.y), color, 1f);
                IMGUIStyles.DrawLine(new Vector2(x, r.yMax), new Vector2(x + w, r.yMax), color, 1f);
            }
            for (float y = r.y; y < r.yMax; y += dash + gap)
            {
                float h = Mathf.Min(dash, r.yMax - y);
                IMGUIStyles.DrawLine(new Vector2(r.x, y), new Vector2(r.x, y + h), color, 1f);
                IMGUIStyles.DrawLine(new Vector2(r.xMax, y), new Vector2(r.xMax, y + h), color, 1f);
            }
        }
    }
}
