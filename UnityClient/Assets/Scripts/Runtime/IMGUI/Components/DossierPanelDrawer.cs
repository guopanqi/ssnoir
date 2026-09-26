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
    /// 只占几行：名字、那一句「现在」、子项清单、读数。要看以前发生过什么才展开记录。
    ///
    /// 一张卡是一个小节。那一句「现在」说这一节整体要干什么（说不清就空着）；
    /// 子项清单说走到了哪——做过的划掉，正在做的照常写，后面的不露：卷宗不知道未来。
    /// 奖励在最后一项划掉时由内容发（complete-task!），卡上不预告。
    ///
    /// 内容由故事模块自己回答（见 engine.scm 的 dossier），这里一句话也不自己拼：
    /// 拼不出来说明内容没写，那是内容的问题，不该由界面遮过去。
    ///
    /// 地图上常驻的只剩「钉住条」（<see cref="DrawPinStrip"/>）：钉住的每条线一行，
    /// 线名接着当前那一项，按卷宗顺序叠在左上角。新接的线默认钉上，玩家取消的才收进面板。
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
        // 但地图上只放「现在做什么」这一件事：一条线一小叠，线名在上、当前那一项在下，
        // 做完的、还没到的都不上地图（那是面板的事）。有钟再往下一行摆钟。
        // 不画卡：跟右上角功能槽同一格微透底（无框、无投影），够读就行；
        // 线名退到弱化灰（13 常态），当前项留在次级灰（14 半粗）——标题和内容靠亮度分层，
        // 不靠字号硬撑。金只做三件事，线名不配用金。钉住的线按卷宗顺序往下叠，取消过的不画。
        // 条子挂在右上角功能槽下面：左上角还给场景和标注带，眼睛只往右上角落一次。

        private const float StripMaxW = 300f;
        private const float StripGap = 4f;
        private const float StripPadX = 8f;
        private const float StripPadY = 6f;
        private const float StripTitleGap = 4f;

        /// <summary>topY 由调用方给（功能槽底 + 间隙）。钉住条先摆，标注带再绕着它排（见 AnnotationDrawer.LayoutSceneBand）。</summary>
        public static Rect StripRect(float topY)
        {
            Rect safe = UIScale.SafeArea;
            float w = Mathf.Min(StripMaxW, safe.width - 32f);
            // 右缘和功能槽对齐：同一列东西，右边缘是一条线。
            return new Rect(safe.xMax - 16f - w, topY, w, 0f);
        }

        /// <summary>画所有钉住的线，返回它们叠起来占的那一块（没有就是空矩形）。</summary>
        public static Rect DrawPinStrip(SSNoirGameManager gameManager, float topY)
            => LayoutPinStrip(gameManager, topY, draw: true);

        public static Rect MeasurePinStrip(SSNoirGameManager gameManager, float topY)
            => LayoutPinStrip(gameManager, topY, draw: false);

        private static Rect LayoutPinStrip(SSNoirGameManager gameManager, float topY, bool draw)
        {
            var pinned = PinnedEntries(gameManager);
            if (pinned.Count == 0) return new Rect(0f, 0f, 0f, 0f);

            Rect union = new Rect(0f, 0f, 0f, 0f);
            float y = topY;
            foreach (var entry in pinned)
            {
                var frame = LayoutOneStrip(entry, y, draw);
                union = union.width <= 0f ? frame : Rect.MinMaxRect(
                    Mathf.Min(union.xMin, frame.xMin), Mathf.Min(union.yMin, frame.yMin),
                    Mathf.Max(union.xMax, frame.xMax), Mathf.Max(union.yMax, frame.yMax));
                y = frame.yMax + StripGap;
            }
            return union;
        }

        private static GUIStyle StripTitleStyle() => new GUIStyle(IMGUIStyles.StatusLabel)
        {
            fontSize = IMGUIStyles.FontSize(13),
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.UpperRight,
            wordWrap = false,
            normal = { textColor = IMGUIStyles.TextDisabled }
        };

        private static GUIStyle StripNowStyle() => new GUIStyle(IMGUIStyles.StatusLabel)
        {
            fontSize = IMGUIStyles.FontSize(14),
            alignment = TextAnchor.UpperRight,
            wordWrap = true,
            normal = { textColor = IMGUIStyles.TextSecondary }
        };

        /// <summary>地图上那一句：有清单就是当前那一项，没有就是那句「现在」。</summary>
        private static string StripLine(DossierEntry entry)
        {
            foreach (var step in entry.Steps)
                if (!step.Done) return step.Text;
            return entry.Now;
        }

        private static Rect LayoutOneStrip(DossierEntry entry, float topY, bool draw)
        {
            Rect bounds = StripRect(topY);
            var titleStyle = StripTitleStyle();
            var nowStyle = StripNowStyle();
            string line = StripLine(entry);

            // 纵向一小叠：线名在上、当前项在下、钟再下；右对齐收进功能槽同一右缘。
            float innerW = bounds.width - StripPadX * 2f;
            float titleW = Mathf.Min(titleStyle.CalcSize(new GUIContent(entry.Id)).x, innerW);
            float titleH = titleStyle.lineHeight;
            float nowNatural = string.IsNullOrEmpty(line)
                ? 0f : nowStyle.CalcSize(new GUIContent(line)).x;
            float textW = Mathf.Max(titleW, Mathf.Min(nowNatural, innerW));

            float clockH = entry.Clocks.Count > 0 ? ClocksHeight(entry.Clocks, innerW) : 0f;
            float clockW = 0f;
            if (clockH > 0f)
            {
                foreach (var c in entry.Clocks)
                    clockW += Mathf.Min(CardDrawer.MeasureClockBadge(c), innerW) + ClockGap;
                clockW = Mathf.Min(clockW - ClockGap, innerW);
            }

            float w = Mathf.Max(textW, clockW) + StripPadX * 2f;
            float textBoxW = w - StripPadX * 2f;
            float nowH = string.IsNullOrEmpty(line)
                ? 0f : nowStyle.CalcHeight(new GUIContent(line), textBoxW);
            float h = StripPadY * 2f + titleH + (nowH > 0f ? StripTitleGap + nowH : 0f)
                + (clockH > 0f ? 4f + clockH : 0f);
            Rect frame = UIScale.PixelSnap(new Rect(bounds.xMax - w, bounds.y, w, h));

            if (!draw) return frame;

            IMGUIStyles.SetColor(IMGUIStyles.FunctionSlotBg);
            GUI.DrawTexture(frame, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();

            float y = frame.y + StripPadY;
            float right = frame.xMax - StripPadX;
            IMGUIStyles.DrawLabel(new Rect(right - textBoxW, y, textBoxW, titleH), entry.Id, titleStyle);
            y += titleH;
            if (nowH > 0f)
            {
                y += StripTitleGap;
                IMGUIStyles.DrawLabel(new Rect(right - textBoxW, y, textBoxW, nowH), line, nowStyle);
                y += nowH;
            }
            if (clockH > 0f)
                DrawClocks(new Rect(right - clockW, y + 4f, clockW, clockH), entry.Clocks);

            return frame;
        }

        // ── 子项清单 ─────────────────────────────────────────────────────
        //
        // 卷宗不知道未来：清单只列到当前那一项为止——做过的划掉，正在做的照常写，
        // 后面的一行不露。列出来的是「走到哪了」，不是这一节的攻略。
        // 一行一项，前面一个短横，缩进半格；不打勾也不画圈（那像清单，这是记事）。
        // 划线只划第一行——文案本来就该一行写完，换了行是文案的问题，不是版面的。

        private const string StepMark = "–";
        private const float StepIndent = 14f;
        private const float StepGap = 2f;

        /// <summary>做过的加当前那一项；后面的还没发生，不给玩家看。</summary>
        private static List<DossierStep> VisibleSteps(IReadOnlyList<DossierStep> steps)
        {
            var result = new List<DossierStep>();
            foreach (var step in steps)
            {
                result.Add(step);
                if (!step.Done) break;
            }
            return result;
        }

        private static GUIStyle PanelStepStyle() => new GUIStyle(IMGUIStyles.ModalBody)
        {
            fontSize = IMGUIStyles.FontSize(14),
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
        };

        private static float StepsHeight(IReadOnlyList<DossierStep> steps, float width, GUIStyle style)
        {
            float h = 0f;
            float textW = width - StepIndent;
            foreach (var step in VisibleSteps(steps))
                h += style.CalcHeight(new GUIContent(step.Text), textW) + StepGap;
            return h;
        }

        private static void DrawSteps(Rect rect, IReadOnlyList<DossierStep> steps, GUIStyle style,
            Color openColor, Color doneColor)
        {
            float textW = rect.width - StepIndent;
            float y = rect.y;
            var markStyle = new GUIStyle(style) { wordWrap = false, alignment = TextAnchor.UpperCenter };
            foreach (var step in VisibleSteps(steps))
            {
                float h = style.CalcHeight(new GUIContent(step.Text), textW);
                Color color = step.Done ? doneColor : openColor;
                style.normal.textColor = color;
                markStyle.normal.textColor = color;
                IMGUIStyles.DrawLabel(new Rect(rect.x, y, StepIndent, h), StepMark, markStyle);
                IMGUIStyles.DrawLabel(new Rect(rect.x + StepIndent, y, textW, h), step.Text, style);
                if (step.Done)
                {
                    float lineH = style.lineHeight > 0f ? style.lineHeight : h;
                    float w = Mathf.Min(style.CalcSize(new GUIContent(step.Text)).x, textW);
                    float midY = y + lineH * 0.55f;
                    IMGUIStyles.DrawLine(new Vector2(rect.x + StepIndent, midY),
                        new Vector2(rect.x + StepIndent + w, midY), color, 1f);
                }
                y += h + StepGap;
            }
        }

        // 钉住是默认，取消才需要表态：新接的线不必先被发现"钉住"这个功能就上了地图，
        // 玩家每天照着做决定的那几个数一开始就在眼前。取消过的记在存档里，不许自动弹回来；
        // 了结的线自己退下地图，不占取消名额。以前是「只钉一条、默认第一条」——
        // 于是地图上老挂着一条玩家没选过的线，和面板里的钉子对不上。
        private static HashSet<string> Unpinned(SSNoirGameManager gameManager)
        {
            string raw = gameManager.GameState.Get<string>(SceneManager.DossierUnpinnedKey, string.Empty);
            var set = new HashSet<string>();
            foreach (var id in raw.Split('\n'))
                if (!string.IsNullOrEmpty(id)) set.Add(id);
            return set;
        }

        public static bool IsPinned(SSNoirGameManager gameManager, DossierEntry e)
            => !e.IsClosed && !Unpinned(gameManager).Contains(e.Id);

        /// <summary>钉住的线，按卷宗顺序（主要在前，每组内主线 → 委托 → 人物/城市）。</summary>
        public static List<DossierEntry> PinnedEntries(SSNoirGameManager gameManager)
        {
            var result = new List<DossierEntry>();
            var unpinned = Unpinned(gameManager);
            foreach (var e in Ordered(gameManager.DisplayedSnapshot.Dossier))
                if (!e.IsClosed && !unpinned.Contains(e.Id))
                    result.Add(e);
            return result;
        }

        private static void TogglePin(SSNoirGameManager gameManager, string id)
        {
            var unpinned = Unpinned(gameManager);
            if (!unpinned.Remove(id))
                unpinned.Add(id);
            gameManager.SceneManager.SetDossierUnpinned(unpinned);
        }

        // ── 面板 ─────────────────────────────────────────────────────────

        public static void Draw(SSNoirGameManager gameManager, IMGUIInteractionContext ui, TopHudLayout topHud)
        {
            var (toggleRect, panelRect) = GetRects(topHud);
            var dossier = gameManager.DisplayedSnapshot.Dossier;

            if (IMGUIButton.DrawTopTextToggle(toggleRect, "卷宗", _isOpen, ui))
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
                contentH += MeasureEntry(e, viewport.width - 16f) + 6f;

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
                y += h + 6f;
            }
            GUI.EndScrollView();

            if (!string.IsNullOrEmpty(toggledPin))
                TogglePin(gameManager, toggledPin);
            if (!string.IsNullOrEmpty(toggledExpand))
                _expandedId = _expandedId == toggledExpand ? string.Empty : toggledExpand;
        }

        // 主要在前，次要在后，了结的沉到最后。每组内部仍按 kind（主线 → 委托 → 人物/城市）。
        // 玩家不必自己在一串里翻找还在办的事，更不必翻找这一章的主轴。
        private static List<DossierEntry> Ordered(IReadOnlyList<DossierEntry> dossier)
        {
            var primaryMain = new List<DossierEntry>();
            var primaryOpen = new List<DossierEntry>();
            var primaryOthers = new List<DossierEntry>();
            var secondaryMain = new List<DossierEntry>();
            var secondaryOpen = new List<DossierEntry>();
            var secondaryOthers = new List<DossierEntry>();
            var closed = new List<DossierEntry>();
            foreach (var e in dossier)
            {
                if (e.IsClosed) closed.Add(e);
                else
                {
                    var bucket = e.IsPrimary
                        ? (e.Kind == "主线" ? primaryMain : e.Kind == "委托" ? primaryOpen : primaryOthers)
                        : (e.Kind == "主线" ? secondaryMain : e.Kind == "委托" ? secondaryOpen : secondaryOthers);
                    bucket.Add(e);
                }
            }
            var result = new List<DossierEntry>();
            result.AddRange(primaryMain);
            result.AddRange(primaryOpen);
            result.AddRange(primaryOthers);
            result.AddRange(secondaryMain);
            result.AddRange(secondaryOpen);
            result.AddRange(secondaryOthers);
            result.AddRange(closed);
            return result;
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
        private const float RowPad = 6f;

        private static float MeasureEntry(DossierEntry e, float width)
        {
            float textW = width - PinW - RowPad * 2f;
            float h = RowPad + 20f;                                   // 名字那一行
            if (!string.IsNullOrEmpty(e.Now))
                h += NowStyle(e.IsClosed).CalcHeight(new GUIContent(e.Now), textW) + 2f;
            if (e.Steps.Count > 0)
                h += StepsHeight(e.Steps, textW, PanelStepStyle()) + 2f;
            if (e.Clocks.Count > 0)
                h += ClocksHeight(e.Clocks, textW) + 6f;
            if (e.Log.Count > 0)
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

            bool isPinned = IsPinned(gameManager, e);

            float textX = row.x + PinW;
            float textW = row.width - PinW - RowPad;
            float y = row.y + RowPad;

            // 钉子。已完成的条目只是回顾，钉在地图上没有意义——不画、也不接受点击。
            // 滚动视图里的坐标是本地的，命中测试要换算回屏幕坐标再问 ui。
            if (!e.IsClosed)
            {
                var pinRect = new Rect(row.x, y, PinW - 4f, 20f);
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
                    toggledPin = e.Id;   // 钉住 ↔ 取消
                    Event.current.Use();
                }
            }

            IMGUIStyles.DrawLabel(new Rect(textX, y, textW - 140f, 20f), e.Id, NameStyle(e.IsClosed));
            // 「进行中」是常态，不值得写；只有等人和了结才是信息。
            // 内部状态叫「了结」，给玩家看就写「已完成」。
            string tierStatus = e.Status == "进行中"
                ? (e.IsPrimary ? "主要" : "次要")
                : $"{(e.IsPrimary ? "主要" : "次要")} · {(e.IsClosed ? "已完成" : e.Status)}";
            IMGUIStyles.DrawLabel(new Rect(row.xMax - 138f, y, 138f, 20f), tierStatus,
                new GUIStyle(MetaStyle()) { alignment = TextAnchor.MiddleRight });
            y += 20f;

            if (!string.IsNullOrEmpty(e.Now))
            {
                var nowStyle = NowStyle(e.IsClosed);
                float h = nowStyle.CalcHeight(new GUIContent(e.Now), textW);
                IMGUIStyles.DrawLabel(new Rect(textX, y, textW, h), e.Now, nowStyle);
                y += h + 2f;
            }

            if (e.Steps.Count > 0)
            {
                float stepsH = StepsHeight(e.Steps, textW, PanelStepStyle());
                DrawSteps(new Rect(textX, y, textW, stepsH), e.Steps, PanelStepStyle(),
                    e.IsClosed ? IMGUIStyles.PaperTextSecondary : IMGUIStyles.PaperTextPrimary,
                    IMGUIStyles.PaperTextDisabled);
                y += stepsH + 2f;
            }

            if (e.Clocks.Count > 0)
            {
                float clocksH = ClocksHeight(e.Clocks, textW);
                DrawClocks(new Rect(textX, y, textW, clocksH), e.Clocks);
                y += clocksH + 6f;
            }

            // 地点不写：钉住条与地图标签已经说了该去哪，这里再写一遍是废字。
            if (e.Log.Count > 0)
            {
                {
                    var moreRect = new Rect(row.xMax - 118f, y, 118f, 18f);
                    var moreScreen = new Rect(moreRect.x + viewport.x - _scroll.x,
                        moreRect.y + viewport.y - _scroll.y, moreRect.width, moreRect.height);
                    bool expanded = _expandedId == e.Id;
                    IMGUIStyles.DrawLabel(moreRect,
                        expanded ? "收起 ▴" : $"记录 {e.Log.Count} 条 ▾",
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
