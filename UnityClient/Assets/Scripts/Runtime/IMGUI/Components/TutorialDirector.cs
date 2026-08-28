#nullable enable
using System.Collections.Generic;
using UnityEngine;

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
        private const float PanelW = 440f;
        private const float PadX = 24f;
        private const float ParagraphGap = 12f;
        private const float ButtonH = 34f;
        private const float BottomPad = 20f;

        private static readonly List<string> _queue = new();

        // 触发条件里那几个「第一次」的记号。看过没有由 TutorialState 说了算，这里只防同一帧重复排队。
        private static bool _sawAnyCard;

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
            _sawAnyCard = false;
        }

        /// <summary>
        /// 每帧看一眼该不该弹。三条的触发点：
        ///   行动骰 —— 第一次看见动作卡（也就是第一次真的能做事）
        ///   冷静   —— 第一次坏结果（由 <see cref="RequestOnFailedRoll"/> 从结算那头喊）
        ///   结束一天 —— 骰子用空、人还在城里
        /// </summary>
        public static void Observe(SSNoirGameManager gameManager, bool hasActionCards)
        {
            var snapshot = gameManager.DisplayedSnapshot;

            if (hasActionCards && !_sawAnyCard)
            {
                _sawAnyCard = true;
                Request(TutorialLibrary.ActionDice);
            }

            // 骰子用空：城里才提示。交锋里没有「回家睡觉」这条路，说了是错的。
            if (!snapshot.IsInEncounter && _sawAnyCard)
            {
                bool anyOnStage = false, anyDieLeft = false;
                foreach (var actor in snapshot.Actors)
                {
                    if (!actor.OnStage) continue;
                    anyOnStage = true;
                    if (actor.ActionDice.Count > 0) anyDieLeft = true;
                }
                if (anyOnStage && !anyDieLeft)
                    Request(TutorialLibrary.EndOfDay);
            }
        }

        /// <summary>结算出坏结果时喊一声。冷静那条讲的是「不顺利会怎样」，得在人真的不顺利之后才成立。</summary>
        public static void RequestOnFailedRoll() => Request(TutorialLibrary.Composure);

        public static Rect GetPanelRect(TutorialEntry entry)
        {
            Rect safe = UIScale.SafeArea;
            float w = Mathf.Min(PanelW, safe.width - 32f);
            float h = Mathf.Min(MeasureHeight(entry, w), safe.height - 24f);
            // 竖直方向偏上：底下要露出行动骰和功能键——那正是这几条教程在指的东西。
            float y = safe.y + Mathf.Max(12f, (safe.height - h) * 0.32f);
            return UIScale.PixelSnap(new Rect(safe.x + (safe.width - w) / 2f, y, w, h));
        }

        private static float MeasureHeight(TutorialEntry entry, float panelW)
        {
            var body = BodyStyle();
            float textW = panelW - PadX * 2f;
            float h = IMGUIStyles.ModalContentTop;
            foreach (var paragraph in entry.Paragraphs)
                h += body.CalcHeight(new GUIContent(paragraph), textW) + ParagraphGap;
            return h + 8f + ButtonH + BottomPad;
        }

        private static GUIStyle BodyStyle()
        {
            return new GUIStyle(IMGUIStyles.ModalBody)
            {
                fontSize = IMGUIStyles.FontSize(15),
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
            };
        }

        public static TutorialEntry? Current =>
            _queue.Count > 0 ? TutorialLibrary.Find(_queue[0]) : null;

        /// <summary>画当前这一条。点了「继续」就记成看过并出队。</summary>
        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui, Rect? firstCardRect)
        {
            var entry = Current;
            if (entry == null) return;

            var panel = GetPanelRect(entry);

            // 压暗 + 纸底 + 标题 + 关闭 X。关掉 X 和「继续」等价：都算读过了。
            bool closed = IMGUIStyles.DrawModalChrome(panel, entry.Title, ui);

            // 高亮画在压暗之后：被指的那块地方要浮在暗幕上面，否则等于没指。
            DrawHighlights(gameManager, entry.Highlight, firstCardRect);

            var body = BodyStyle();
            float textW = panel.width - PadX * 2f;
            float y = panel.y + IMGUIStyles.ModalContentTop;
            foreach (var paragraph in entry.Paragraphs)
            {
                float h = body.CalcHeight(new GUIContent(paragraph), textW);
                IMGUIStyles.DrawLabel(new Rect(panel.x + PadX, y, textW, h), paragraph, body);
                y += h + ParagraphGap;
            }

            var buttonRect = UIScale.PixelSnap(new Rect(
                panel.xMax - PadX - 120f, panel.yMax - BottomPad - ButtonH, 120f, ButtonH));
            if (TutorialPaper.DrawPrimaryButton(buttonRect, "继 续", ui) || closed)
            {
                TutorialState.MarkSeen(entry.Id);
                _queue.RemoveAt(0);
            }
        }

        private static void DrawHighlights(
            SSNoirGameManager gameManager, TutorialHighlight highlight, Rect? firstCardRect)
        {
            switch (highlight)
            {
                case TutorialHighlight.ActionDiceAndCard:
                    DrawRing(HandPanelDrawer.ActionDiceRect(gameManager));
                    if (firstCardRect.HasValue) DrawRing(firstCardRect.Value);
                    break;
                case TutorialHighlight.Vitals:
                    DrawRing(HandPanelDrawer.LeadVitalsRect(gameManager));
                    break;
                case TutorialHighlight.FunctionKey:
                    DrawRing(HandPanelDrawer.FunctionKeyRect(gameManager));
                    break;
            }
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
