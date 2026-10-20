#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 教程提示：什么时候弹、弹哪一条、画成什么样。
    ///
    /// 三条规矩，为的是以后能干净地换掉：
    ///   1. **文案不在这儿**（见 <see cref="TutorialLibrary"/>），帮助面板读的是同一份。
    ///   2. **触发全部只读快照**，不往引擎和内容层里塞钩子——所以关掉教程等于什么都没发生。
    ///   3. 一次只有一条在屏幕上。同一帧凑齐两个条件时排队，前一条关掉才轮到后一条。
    /// </summary>
    public static class TutorialDirector
    {
        private const float PanelW = 760f;
        private const float PadX = 24f;
        private const float ButtonH = 34f;
        private const float BottomPad = 20f;

        private static readonly List<string> _queue = new();

        public static bool IsOpen => _queue.Count > 0;

        /// <summary>把一条教程排进队列。已关教程、已看过、已在队里的，都当没发生。</summary>
        public static void Request(string id)
        {
            if (!TutorialState.Enabled) return;
            if (TutorialState.HasSeen(id)) return;
            if (_queue.Contains(id)) return;
            if (TutorialLibrary.Find(id) == null) return;
            _queue.Add(id);
        }

        /// <summary>回标题画面 / 重开一局时清空排队（看过的记录不清）。</summary>
        public static void Reset()
        {
            _queue.Clear();
            TutorialCardDrawer.StopVideo();
        }

        /// <summary>只在对应界面真正可见时提示；开关重新打开后仍能触发。</summary>
        public static void Observe(SSNoirGameManager gameManager, bool hasActionCards)
        {
            var snapshot = gameManager.DisplayedSnapshot;
            if (snapshot.IsInEncounter) return;

            // 冷静与伤势按实际状态触发教程；失败未扣冷静时不误触发。
            foreach (var actor in snapshot.Actors)
                if (actor.Role == "protagonist")
                {
                    if (TeamState.ComposureDicePenalty(actor.Composure) < 0)
                        Request(TutorialLibrary.Composure);
                    break;
                }
            if (snapshot.InjurySeverity > 0)
                Request(TutorialLibrary.Injury);

            foreach (var node in gameManager.VisibleNodes)
            {
                // 家的显示标题是「旅馆」，节点身份始终是「家」。
                if (gameManager.NavigationStack.Exists(node => node.Id == "家") && node.Id == "睡觉")
                    Request(TutorialLibrary.EndOfDay);
                if (gameManager.NavigationStack.Exists(node => node.Id == "老街酒馆") && hasActionCards
                    && node.Tags.Contains("工作"))
                    Request(TutorialLibrary.ActionDice);
            }
        }

        /// <summary>保留旧调用入口；实际教程触发以快照里的冷静状态为准。</summary>
        public static void RequestOnFailedRoll() => Request(TutorialLibrary.Composure);

        /// <summary>以目标卡为边界分配剩余空间，优先选能容下大教程的左右区域。</summary>
        public static Rect GetPanelRect(TutorialEntry entry, IReadOnlyList<Rect> highlights)
        {
            Rect safe = UIScale.SafeArea;
            safe = new Rect(safe.x + 16f, safe.y + 12f, safe.width - 32f, safe.height - 24f);
            Rect? target = null;
            // 动作卡排在高亮列表首位，骰池不决定左右布局。
            foreach (var rect in highlights)
                if (rect.width > 1f && rect.height > 1f) { target = rect; break; }
            if (!target.HasValue)
            {
                float width = Mathf.Min(PanelW, safe.width);
                float height = Mathf.Min(MeasureHeight(entry, width), safe.height);
                return UIScale.PixelSnap(new Rect(safe.center.x - width / 2f,
                    safe.center.y - height / 2f, width, height));
            }

            Rect card = target.Value;
            const float gap = 24f; // 金框及呼吸光也须留在教程之外。
            var regions = new[]
            {
                new Rect(safe.x, safe.y, Mathf.Max(0f, card.x - gap - safe.x), safe.height),
                new Rect(card.xMax + gap, safe.y, Mathf.Max(0f, safe.xMax - card.xMax - gap), safe.height),
                new Rect(safe.x, safe.y, safe.width, Mathf.Max(0f, card.y - gap - safe.y)),
                new Rect(safe.x, card.yMax + gap, safe.width, Mathf.Max(0f, safe.yMax - card.yMax - gap)),
            };
            Rect best = safe;
            float bestScore = float.MinValue;
            foreach (var region in regions)
            {
                float width = Mathf.Min(PanelW, region.width);
                if (width < Mathf.Min(400f, safe.width)) continue;
                float height = Mathf.Min(MeasureHeight(entry, width), region.height);
                // 文字、视频和页脚必须都能放下，不能只因宽度大就选一条扁带。
                if (height < Mathf.Min(MeasureHeight(entry, width), safe.height) * 0.78f) continue;
                var panel = new Rect(
                    Mathf.Clamp(card.center.x - width / 2f, region.x, region.xMax - width),
                    Mathf.Clamp(card.center.y - height / 2f, region.y, region.yMax - height), width, height);
                float score = width * height - TotalOverlap(panel, highlights) * 4f;
                if (score > bestScore) { bestScore = score; best = panel; }
            }
            // 极窄画布仍以露出目标为先，使用空间更宽的一侧。
            if (bestScore == float.MinValue)
            {
                Rect region = regions[0].width >= regions[1].width ? regions[0] : regions[1];
                float width = Mathf.Min(PanelW, region.width);
                float height = Mathf.Min(MeasureHeight(entry, width), region.height);
                best = new Rect(region.x, region.y, width, height);
            }
            return UIScale.PixelSnap(best);
        }

        /// <summary>窗口盖住这些框的总面积。圈出来的框按画上去的样子算（带那圈 6px 外扩）。</summary>
        private static float TotalOverlap(Rect panel, IReadOnlyList<Rect> highlights)
        {
            float total = 0f;
            foreach (var raw in highlights)
            {
                if (raw.width <= 1f || raw.height <= 1f) continue;
                var box = new Rect(raw.x - 6f, raw.y - 6f, raw.width + 12f, raw.height + 12f);
                float ox = Mathf.Min(panel.xMax, box.xMax) - Mathf.Max(panel.x, box.x);
                float oy = Mathf.Min(panel.yMax, box.yMax) - Mathf.Max(panel.y, box.y);
                if (ox > 0f && oy > 0f)
                    total += ox * oy;
            }
            return total;
        }

        /// <summary>本条教程要圈出来的框。摆窗口和画金圈读的是同一份，不会各算各的。</summary>
        private static List<Rect> CollectHighlights(
            SSNoirGameManager gameManager, TutorialHighlight highlight, Rect? firstCardRect)
        {
            var rects = new List<Rect>();
            switch (highlight)
            {
                case TutorialHighlight.ActionDiceAndCard:
                    if (firstCardRect.HasValue) rects.Add(firstCardRect.Value);
                    rects.Add(HandPanelDrawer.ActionDiceRect(gameManager));
                    break;
                case TutorialHighlight.RestCard:
                    if (firstCardRect.HasValue) rects.Add(firstCardRect.Value);
                    break;
                case TutorialHighlight.Vitals:
                    rects.Add(HandPanelDrawer.LeadVitalsRect(gameManager));
                    break;
                case TutorialHighlight.FunctionKey:
                    rects.Add(HandPanelDrawer.FunctionKeyRect(gameManager));
                    break;
            }
            return rects;
        }

        private static float MeasureHeight(TutorialEntry entry, float panelW)
        {
            return IMGUIStyles.ModalContentTop + TutorialCardDrawer.MeasureHeight(entry, panelW - PadX * 2f)
                + 8f + ButtonH + BottomPad;
        }

        public static TutorialEntry? Current =>
            _queue.Count > 0 ? TutorialLibrary.Find(_queue[0]) : null;

        /// <summary>画当前这一条。点了「继续」就记成看过并出队。</summary>
        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui, Rect? firstCardRect)
        {
            var entry = Current;
            if (entry == null) return;

            var highlights = CollectHighlights(gameManager, entry.Highlight, firstCardRect);
            var panel = GetPanelRect(entry, highlights);

            // 压暗 + 纸底 + 标题 + 关闭 X。关掉 X 和「继续」等价：都算读过了。
            DrawBackdrop(highlights);
            bool closed = IMGUIStyles.DrawModalChrome(panel, entry.Title, ui, dimBackground: false);

            // 高亮画在压暗之后：被指的那块地方要浮在暗幕上面，否则等于没指。
            foreach (var rect in highlights)
                DrawRing(rect);

            TutorialCardDrawer.Draw(entry, new Rect(panel.x + PadX,
                panel.y + IMGUIStyles.ModalContentTop, panel.width - PadX * 2f,
                panel.height - IMGUIStyles.ModalContentTop - BottomPad - ButtonH - 8f), ui);

            var buttonRect = UIScale.PixelSnap(new Rect(
                panel.xMax - PadX - 120f, panel.yMax - BottomPad - ButtonH, 120f, ButtonH));
            if (TutorialPaper.DrawPrimaryButton(buttonRect, "继 续", ui) || closed)
            {
                TutorialState.MarkSeen(entry.Id);
                _queue.RemoveAt(0);
                TutorialCardDrawer.StopVideo();
            }
        }

        // 从暗幕中挖出目标区域，让真实动作卡保持原亮度，交互仍由模态上下文锁定。
        private static void DrawBackdrop(IReadOnlyList<Rect> highlights)
        {
            var pieces = new List<Rect> { new Rect(0f, 0f, UIScale.VW, UIScale.VH) };
            foreach (var target in highlights)
            {
                var hole = new Rect(target.x - 8f, target.y - 8f, target.width + 16f, target.height + 16f);
                var next = new List<Rect>();
                foreach (var piece in pieces)
                {
                    if (!piece.Overlaps(hole)) { next.Add(piece); continue; }
                    float x0 = Mathf.Max(piece.x, hole.x), x1 = Mathf.Min(piece.xMax, hole.xMax);
                    float y0 = Mathf.Max(piece.y, hole.y), y1 = Mathf.Min(piece.yMax, hole.yMax);
                    next.Add(new Rect(piece.x, piece.y, piece.width, y0 - piece.y));
                    next.Add(new Rect(piece.x, y1, piece.width, piece.yMax - y1));
                    next.Add(new Rect(piece.x, y0, x0 - piece.x, y1 - y0));
                    next.Add(new Rect(x1, y0, piece.xMax - x1, y1 - y0));
                }
                pieces = next;
            }
            IMGUIStyles.SetColor(IMGUIStyles.Blocker);
            foreach (var piece in pieces)
                if (piece.width > 0f && piece.height > 0f) GUI.DrawTexture(piece, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
        }

        // 圈出来的框：金描边 + 金光呼吸，和「这张卡正在发生」用的是同一种金——
        // 界面里只有一种「看这里」的语言。
        private static void DrawRing(Rect rect)
        {
            if (rect.width <= 1f || rect.height <= 1f) return;
            var box = UIScale.PixelSnap(new Rect(rect.x - 6f, rect.y - 6f, rect.width + 12f, rect.height + 12f));
            IMGUIStyles.DrawOutline(box, 2f, IMGUIStyles.Gold);
            IMGUIStyles.DrawGoldPulse(box);
        }
    }

    /// <summary>教程窗口与帮助面板共用的纸上按钮（主 = 实心墨底 + 纸白字，见 DESIGN.md）。</summary>
    public static class TutorialPaper
    {
        public static bool DrawPrimaryButton(Rect rect, string label, IMGUIInteractionContext ui, bool enabled = true)
        {
            bool hover = enabled && ui.CanHover(rect);
            GUI.color = enabled
                ? (hover ? IMGUIStyles.PaperInk : new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.88f))
                : new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.25f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var style = new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = IMGUIStyles.FontSize(15),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = enabled ? IMGUIStyles.Paper : new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.55f) },
            };
            IMGUIStyles.ApplyStrongFont(style);
            IMGUIStyles.DrawLabel(rect, label, style);

            return enabled && ui.WasTapped(rect);
        }
    }
}
