#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 卷宗：城里所有故事线各自的「现在」。
    ///
    /// 它要回答的只有一句话——隔了三天再打开游戏，我现在在办什么。所以每条线在这里
    /// 只占三行：名字、那一句「现在」、读数。要看以前发生过什么才展开履历。
    ///
    /// 内容由故事模块自己回答（见 engine.scm 的 dossier），这里一句话也不自己拼：
    /// 拼不出来说明内容没写，那是内容的问题，不该由界面遮过去。
    ///
    /// 地图上常驻的只剩「钉住条」（<see cref="DrawPinStrip"/>）：玩家钉的那条线的
    /// 读数和那一句。其余全部收进这块面板。
    /// </summary>
    public static class DossierPanelDrawer
    {
        private const float PanelW = 520f;
        private const float PanelH = 460f;

        private static bool _isOpen;
        private static string _expandedId = string.Empty;
        private static Vector2 _scroll;

        public static bool IsOpen => _isOpen;

        public static void Close() => _isOpen = false;

        public static (Rect ToggleRect, Rect PanelRect) GetRects(TopHudLayout topHud)
        {
            Rect safe = UIScale.SafeArea;
            float panelW = Mathf.Min(PanelW, safe.width - 32f);
            float panelH = Mathf.Min(PanelH, safe.height - 24f);
            float panelX = safe.x + (safe.width - panelW) / 2f;
            float panelY = safe.y + (safe.height - panelH) / 2f;
            return (topHud.DossierToggle, new Rect(panelX, panelY, panelW, panelH));
        }

        // ── 钉住条 ───────────────────────────────────────────────────────
        //
        // 压力必须留在地图上。玩家每天那个「这颗骰子拿去挣钱还是拿去踩点」的决定，
        // 是照着交割日和交割款做的——它们藏进面板里就没用了。
        // 但也只留这一条：两行，一行读数一行「现在」。塞不下第三行，说明那条线的
        // 「现在」写长了，该改文案，不是加行。

        private const float StripW = 300f;

        /// <summary>topY 由调用方给。钉住条先摆，标注带再绕着它排（见 AnnotationDrawer.LayoutSceneBand）。</summary>
        public static Rect StripRect(float topY)
        {
            Rect safe = UIScale.SafeArea;
            float w = Mathf.Min(StripW, safe.width - 32f);
            return new Rect(safe.x + 16f, topY, w, 0f);
        }

        public static Rect DrawPinStrip(SSNoirGameManager gameManager, float topY)
        {
            var entry = Pinned(gameManager);
            if (entry == null) return new Rect(0f, 0f, 0f, 0f);

            Rect frame = StripRect(topY);
            float padX = 12f;
            float innerW = frame.width - padX * 2f;

            var titleStyle = new GUIStyle(IMGUIStyles.StatusLabel)
            {
                fontSize = IMGUIStyles.FontSize(14),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = IMGUIStyles.Gold }
            };
            var nowStyle = new GUIStyle(IMGUIStyles.StatusLabel)
            {
                fontSize = IMGUIStyles.FontSize(13),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                normal = { textColor = IMGUIStyles.TextPrimary }
            };

            float nowH = string.IsNullOrEmpty(entry.Now)
                ? 0f
                : nowStyle.CalcHeight(new GUIContent(entry.Now), innerW);
            float clockH = entry.Clocks.Count > 0 ? ClocksHeight(entry.Clocks, innerW) + 6f : 0f;
            float h = 10f + 20f + nowH + clockH + 10f;
            frame = UIScale.PixelSnap(new Rect(frame.x, frame.y, frame.width, h));

            GUI.color = IMGUIStyles.HudBg;
            GUI.DrawTexture(frame, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(frame, 1f,
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.30f));

            float y = frame.y + 8f;
            IMGUIStyles.DrawLabel(new Rect(frame.x + padX, y, innerW, 20f), entry.Id, titleStyle);
            y += 20f;

            if (nowH > 0f)
            {
                IMGUIStyles.DrawLabel(new Rect(frame.x + padX, y, innerW, nowH), entry.Now, nowStyle);
                y += nowH;
            }

            if (clockH > 0f)
            {
                y += 6f;
                DrawClocks(new Rect(frame.x + padX, y, innerW, clockH - 6f), entry.Clocks);
            }

            return frame;
        }

        // 钉住状态有三种，不是两种：
        //   ""   还没表过态 —— 自动钉第一条还没了结的委托。新玩家不必先发现"钉住"这个
        //        功能才看得见交割日；地图上那两个数是他每天要照着做决定的东西。
        //   "无" 他主动取消了 —— 就真的什么也不显示，不许自动弹回来。
        //   别的  他自己选的那条。
        // 面板里的钉子画的是**实际生效**的那条（自动选中的也画成实心），否则就会出现
        // 你看到的那种自相矛盾：条目上是空心的，地图上却挂着它。
        private const string PinNone = "无";

        public static string EffectivePinId(SSNoirGameManager gameManager)
        {
            var entry = Pinned(gameManager);
            return entry == null ? string.Empty : entry.Id;
        }

        /// <summary>钉住的那条线。没表过态就取第一条还没了结的委托；主动取消过就没有。</summary>
        public static DossierEntry? Pinned(SSNoirGameManager gameManager)
        {
            var dossier = gameManager.DisplayedSnapshot.Dossier;
            if (dossier.Count == 0) return null;

            string pin = gameManager.GameState.Get<string>(SceneManager.DossierPinKey, string.Empty);
            if (pin == PinNone) return null;

            if (!string.IsNullOrEmpty(pin))
            {
                foreach (var e in dossier)
                    if (e.Id == pin) return e;
                // 钉着的那条已经不在卷宗里了（比如一条临时线收走了），退回自动。
            }

            foreach (var e in dossier)
                if (!e.IsClosed && e.Kind == "委托") return e;
            foreach (var e in dossier)
                if (!e.IsClosed) return e;
            return null;
        }

        // ── 面板 ─────────────────────────────────────────────────────────

        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui, TopHudLayout topHud)
        {
            var (toggleRect, panelRect) = GetRects(topHud);
            var dossier = gameManager.DisplayedSnapshot.Dossier;

            if (IMGUIButton.DrawHudToggle(toggleRect, "卷 宗", _isOpen, ui))
                _isOpen = !_isOpen;

            if (!_isOpen) return;

            if (IMGUIStyles.DrawModalChrome(panelRect, "卷 宗", ui))
            {
                _isOpen = false;
                return;
            }

            var viewport = new Rect(panelRect.x + 24f, panelRect.y + IMGUIStyles.ModalContentTop,
                panelRect.width - 48f, panelRect.height - IMGUIStyles.ModalContentTop - 20f);

            if (dossier.Count == 0)
            {
                var emptyStyle = new GUIStyle(IMGUIStyles.ModalBody)
                {
                    normal = { textColor = IMGUIStyles.PaperTextSecondary }
                };
                IMGUIStyles.DrawLabel(new Rect(viewport.x, viewport.y + 8f, viewport.width, 24f),
                    "还没有在办的事。", emptyStyle);
                return;
            }

            var ordered = Ordered(dossier);
            float contentH = 0f;
            foreach (var e in ordered)
                contentH += MeasureEntry(e, viewport.width - 16f) + 10f;

            _scroll = GUI.BeginScrollView(viewport, _scroll,
                new Rect(0f, 0f, viewport.width - 16f, contentH));
            float y = 0f;
            string toggledPin = string.Empty;
            string toggledExpand = string.Empty;
            foreach (var e in ordered)
            {
                float h = MeasureEntry(e, viewport.width - 16f);
                var row = new Rect(0f, y, viewport.width - 16f, h);
                DrawEntry(row, e, gameManager, ui, viewport, ref toggledPin, ref toggledExpand);
                y += h + 10f;
            }
            GUI.EndScrollView();

            if (!string.IsNullOrEmpty(toggledPin))
            {
                // 点实际生效的那条＝取消钉住（记成「无」，不让它自动弹回来）；否则改钉这条。
                bool wasEffective = EffectivePinId(gameManager) == toggledPin;
                gameManager.SceneManager.SetDossierPin(wasEffective ? PinNone : toggledPin);
            }
            if (!string.IsNullOrEmpty(toggledExpand))
                _expandedId = _expandedId == toggledExpand ? string.Empty : toggledExpand;
        }

        // 委托在前，人物/城市其次，了结的沉到最后。玩家不必自己在一串里翻找还在办的事。
        private static List<DossierEntry> Ordered(IReadOnlyList<DossierEntry> dossier)
        {
            var open = new List<DossierEntry>();
            var others = new List<DossierEntry>();
            var closed = new List<DossierEntry>();
            foreach (var e in dossier)
            {
                if (e.IsClosed) closed.Add(e);
                else if (e.Kind == "委托") open.Add(e);
                else others.Add(e);
            }
            open.AddRange(others);
            open.AddRange(closed);
            return open;
        }

        private static GUIStyle NameStyle(bool closed) => new GUIStyle(IMGUIStyles.ModalBody)
        {
            fontSize = IMGUIStyles.FontSize(16),
            fontStyle = FontStyle.Bold,
            normal = { textColor = closed ? IMGUIStyles.PaperTextSecondary : IMGUIStyles.PaperInk }
        };

        private static GUIStyle NowStyle(bool closed) => new GUIStyle(IMGUIStyles.ModalBody)
        {
            fontSize = IMGUIStyles.FontSize(14),
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = closed ? IMGUIStyles.PaperTextSecondary : IMGUIStyles.PaperTextPrimary }
        };

        private static GUIStyle MetaStyle() => new GUIStyle(IMGUIStyles.ModalBody)
        {
            fontSize = IMGUIStyles.FontSize(12),
            normal = { textColor = IMGUIStyles.PaperTextSecondary }
        };

        private const float PinW = 26f;
        private const float RowPad = 10f;

        private static float MeasureEntry(DossierEntry e, float width)
        {
            float textW = width - PinW - RowPad * 2f;
            float h = RowPad + 22f;                                   // 名字那一行
            if (!string.IsNullOrEmpty(e.Now))
                h += NowStyle(e.IsClosed).CalcHeight(new GUIContent(e.Now), textW) + 4f;
            if (e.Clocks.Count > 0)
                h += ClocksHeight(e.Clocks, textW) + 6f;
            if (!string.IsNullOrEmpty(e.Where) || e.Log.Count > 0)
                h += 18f;
            if (_expandedId == e.Id && e.Log.Count > 0)
            {
                var logStyle = MetaStyle();
                foreach (var line in e.Log)
                    h += LogRowHeight(logStyle, line, textW - 12f) + 3f;
                h += 6f;
            }
            return h + RowPad;
        }

        // 日期是标签，正文是段落——两者不能拼成一个字符串交给自动换行。
        // 拼起来的话「第 4 天」里那个空格就成了换行点，于是出现"第 4"独占一行、
        // 「天」跟着正文跑到下一行。日期占自己一栏、不换行，这种断法就不可能发生。
        private const float LogDayW = 46f;
        private const float LogDayGap = 6f;
        private const float LogMinRowH = 16f;

        private static string LogDay(DossierLogEntry line) => $"第{line.Day}天";

        // 正文可用的宽度。measure 和 draw 都从这儿取，免得两边各算一遍、改一处忘一处。
        private static float LogBodyWidth(float logWidth) => logWidth - LogDayW - LogDayGap;

        private static float LogRowHeight(GUIStyle bodyStyle, DossierLogEntry line, float logWidth)
            => Mathf.Max(LogMinRowH,
                         bodyStyle.CalcHeight(new GUIContent(line.Text), LogBodyWidth(logWidth)));

        private static GUIStyle LogDayStyle()
        {
            var style = new GUIStyle(MetaStyle())
            {
                alignment = TextAnchor.UpperLeft,
                // 日期一栏绝不换行：它是刻度，不是文字。
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            style.normal.textColor = IMGUIStyles.PaperTextDisabled;
            return style;
        }

        private static void DrawEntry(Rect row, DossierEntry e, SSNoirGameManager gameManager,
            IMGUIInteractionContext ui, Rect viewport, ref string toggledPin, ref string toggledExpand)
        {
            IMGUIStyles.DrawLine(new Vector2(row.x, row.yMax), new Vector2(row.xMax, row.yMax),
                new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.16f), 1f);

            bool isPinned = EffectivePinId(gameManager) == e.Id;

            float textX = row.x + PinW;
            float textW = row.width - PinW - RowPad;
            float y = row.y + RowPad;

            // 钉子。滚动视图里的坐标是本地的，命中测试要换算回屏幕坐标再问 ui。
            var pinRect = new Rect(row.x, y, PinW - 4f, 22f);
            var pinScreen = new Rect(pinRect.x + viewport.x - _scroll.x, pinRect.y + viewport.y - _scroll.y,
                pinRect.width, pinRect.height);
            var pinStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = isPinned ? IMGUIStyles.SealRed : IMGUIStyles.PaperTextSecondary }
            };
            IMGUIStyles.DrawLabel(pinRect, isPinned ? "◆" : "◇", pinStyle);
            if (ui.WasTapped(pinScreen))
            {
                toggledPin = e.Id;   // 点已经钉住的那条＝取消钉住，见调用处
                Event.current.Use();
            }

            IMGUIStyles.DrawLabel(new Rect(textX, y, textW - 90f, 22f), e.Id, NameStyle(e.IsClosed));
            IMGUIStyles.DrawLabel(new Rect(row.xMax - 88f, y, 88f, 22f), e.Status,
                new GUIStyle(MetaStyle()) { alignment = TextAnchor.MiddleRight });
            y += 22f;

            if (!string.IsNullOrEmpty(e.Now))
            {
                var nowStyle = NowStyle(e.IsClosed);
                float h = nowStyle.CalcHeight(new GUIContent(e.Now), textW);
                IMGUIStyles.DrawLabel(new Rect(textX, y, textW, h), e.Now, nowStyle);
                y += h + 4f;
            }

            if (e.Clocks.Count > 0)
            {
                float clocksH = ClocksHeight(e.Clocks, textW);
                DrawClocks(new Rect(textX, y, textW, clocksH), e.Clocks);
                y += clocksH + 6f;
            }

            if (!string.IsNullOrEmpty(e.Where) || e.Log.Count > 0)
            {
                string meta = string.IsNullOrEmpty(e.Where) ? string.Empty : e.Where;
                IMGUIStyles.DrawLabel(new Rect(textX, y, textW - 120f, 18f), meta, MetaStyle());

                if (e.Log.Count > 0)
                {
                    var moreRect = new Rect(row.xMax - 118f, y, 118f, 18f);
                    var moreScreen = new Rect(moreRect.x + viewport.x - _scroll.x,
                        moreRect.y + viewport.y - _scroll.y, moreRect.width, moreRect.height);
                    bool expanded = _expandedId == e.Id;
                    IMGUIStyles.DrawLabel(moreRect,
                        expanded ? "收起履历 ▴" : $"履历 {e.Log.Count} 条 ▾",
                        new GUIStyle(MetaStyle()) { alignment = TextAnchor.MiddleRight });
                    if (ui.WasTapped(moreScreen))
                    {
                        toggledExpand = e.Id;
                        Event.current.Use();
                    }
                }
                y += 18f;
            }

            if (_expandedId == e.Id && e.Log.Count > 0)
            {
                y += 6f;
                var logStyle = new GUIStyle(MetaStyle()) { wordWrap = true, alignment = TextAnchor.UpperLeft };
                IMGUIStyles.DrawLine(new Vector2(textX + 2f, y), new Vector2(textX + 2f, row.yMax - RowPad),
                    new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.30f), 1f);
                var dayStyle = LogDayStyle();
                float logX = textX + 12f;
                float logW = textW - 12f;
                foreach (var line in e.Log)
                {
                    float h = LogRowHeight(logStyle, line, logW);
                    IMGUIStyles.DrawLabel(new Rect(logX, y, LogDayW, LogMinRowH), LogDay(line), dayStyle);
                    IMGUIStyles.DrawLabel(
                        new Rect(logX + LogDayW + LogDayGap, y, LogBodyWidth(logW), h), line.Text, logStyle);
                    y += h + 3f;
                }
            }
        }

        // 一排放不下就换行，绝不把徽章压窄——压窄的代价是标签被裁，读出来是「踩 i▯▯▯」
        // 这种半个词。宽度是版面的约束，字数不是；放不下就往下长。
        private const float ClockGap = 8f;
        private const float ClockRowGap = 6f;

        private static float ClocksHeight(IReadOnlyList<GameClock> clocks, float width) =>
            LayoutClocks(clocks, new Rect(0f, 0f, width, 0f), draw: false);

        private static void DrawClocks(Rect rect, IReadOnlyList<GameClock> clocks) =>
            LayoutClocks(clocks, rect, draw: true);

        /// <summary>摆一组时钟徽章，返回它们实际占的高度。draw=false 时只量不画。</summary>
        private static float LayoutClocks(IReadOnlyList<GameClock> clocks, Rect rect, bool draw)
        {
            if (clocks.Count == 0) return 0f;

            float rowH = CardDrawer.ClockBadgeHeightCompact;
            float x = rect.x;
            float y = rect.y;
            bool rowEmpty = true;

            foreach (var clock in clocks)
            {
                // 徽章本身可以比整行还宽（长标签 + 多段进度）；那时它独占一行并收到行宽，
                // 由徽章自己去收标签，这是它已经会做的事。
                float w = Mathf.Min(CardDrawer.MeasureClockBadge(clock), rect.width);
                if (!rowEmpty && x + w > rect.xMax)
                {
                    x = rect.x;
                    y += rowH + ClockRowGap;
                    rowEmpty = true;
                }
                if (draw)
                    CardDrawer.DrawClockBadge(new Rect(x, y, w, rowH), clock);
                x += w + ClockGap;
                rowEmpty = false;
            }
            return y - rect.y + rowH;
        }

    }
}
