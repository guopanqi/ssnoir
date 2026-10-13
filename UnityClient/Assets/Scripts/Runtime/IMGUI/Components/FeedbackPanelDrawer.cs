#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>反馈是一张居中的纸。草稿只留在当前会话，关闭窗口不丢字，不另建本地持久化。</summary>
    public sealed class FeedbackPanelDrawer
    {
        private readonly FeedbackClient _client = new();
        private string _draft = string.Empty;
        private string _location = string.Empty;
        private bool _inEncounter;
        private int _worldDay;
        private FeedbackClient.Submission? _pending;
        private string _error = string.Empty;
        private bool _saved;
        private bool _clearFocus;
        private Vector2 _scroll;
        public bool IsOpen { get; private set; }

        public Rect GetPanelRect()
        {
            Rect safe = UIScale.SafeArea;
            // 键盘占据的是输入可用区域；同一套约束用于所有宿主，不按设备分版面。
            Rect keyboard = TouchScreenKeyboard.area;
            if (keyboard.height > 0f)
            {
                float keyboardTop = (Screen.height - keyboard.yMax) / UIScale.Scale;
                safe.height = Mathf.Max(0f, Mathf.Min(safe.yMax, keyboardTop) - safe.y);
            }
            float w = Mathf.Min(680f, safe.width - 32f);
            float h = Mathf.Min(430f, safe.height - 24f);
            return UIScale.PixelSnap(new Rect(safe.x + (safe.width - w) / 2f,
                safe.y + (safe.height - h) / 2f, w, h));
        }

        public bool DrawToggle(SSNoirGameManager manager, IMGUIInteractionContext ui, TopHudLayout hud)
        {
            if (_clearFocus) { GUI.FocusControl(null); _clearFocus = false; }
            if (!IMGUIButton.DrawTopTextToggle(hud.FeedbackToggle, UiText.Get("反馈"), IsOpen, ui)) return false;
            if (IsOpen) { Close(); return false; }
            if (_draft.Length == 0 && !_client.IsSubmitting)
            {
                _location = manager.DisplayedSnapshot.RootNode?.DisplayTitle ?? UiText.Get("未加载");
                if (manager.NavigationStack.Count > 0)
                    _location += " > " + string.Join(" > ", manager.NavigationStack.ConvertAll(n => n.DisplayTitle));
                _inEncounter = manager.DisplayedSnapshot.IsInEncounter;
                _worldDay = manager.DisplayedSnapshot.WorldDay;
            }
            IsOpen = true;
            return true;
        }

        public void Draw(MonoBehaviour owner, IMGUIInteractionContext ui)
        {
            if (!IsOpen) return;
            Rect panel = GetPanelRect();
            if (IMGUIStyles.DrawModalChrome(panel, UiText.Get("意见反馈"), ui)) { Close(); return; }
            float x = panel.x + 24f;
            float width = panel.width - 48f;
            var body = new GUIStyle(IMGUIStyles.ModalBody) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            if (_saved)
            {
                IMGUIStyles.DrawLabel(new Rect(x, panel.y + IMGUIStyles.ModalContentTop, width, 60f),
                    UiText.Get("反馈已收到，谢谢。"), body);
                if (PaperButton(new Rect(panel.xMax - 156f, panel.yMax - 64f, 132f, 40f),
                    UiText.Get("返回游戏"), ui, true, true)) Close();
                return;
            }

            float y = panel.y + IMGUIStyles.ModalContentTop;
            float footerY = panel.yMax - 64f;
            float contextY = footerY - 48f;
            var field = new Rect(x, y, width, Mathf.Max(40f, contextY - y - 12f));
            IMGUIStyles.DrawOutline(field, 1f, new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g,
                IMGUIStyles.PaperInk.b, 0.35f));
            var input = new GUIStyle(body)
            {
                padding = new RectOffset(10, 10, 8, 8),
                normal = { background = null, textColor = IMGUIStyles.PaperInk },
                focused = { background = null, textColor = IMGUIStyles.PaperInk },
            };
            // 长反馈在输入框内滚动；不会把提交按钮挤出纸张。
            var viewport = new Rect(field.x + 1f, field.y + 1f, field.width - 2f, field.height - 2f);
            float inputW = viewport.width - 18f;
            float inputH = Mathf.Max(viewport.height, input.CalcHeight(new GUIContent(_draft + "\n"), inputW));
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && !ui.IsLocked && !_client.IsSubmitting;
            ui.IsPointerInside(field);
            _scroll = GUI.BeginScrollView(viewport, _scroll, new Rect(0, 0, inputW, inputH));
            GUI.SetNextControlName("FeedbackDraft");
            bool caretMovedByKey = Event.current.type == EventType.KeyDown;
            string text = GUI.TextArea(new Rect(0, 0, inputW, inputH), _draft, FeedbackClient.MaxTextLength, input);
            GUI.EndScrollView();
            GUI.enabled = oldEnabled;
            if ((text != _draft || caretMovedByKey) && GUI.GetNameOfFocusedControl() == "FeedbackDraft"
                && !ui.IsLocked && !_client.IsSubmitting)
            {
                var editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                float caretY = input.GetCursorPixelPosition(new Rect(0, 0, inputW, inputH),
                    new GUIContent(text), editor.cursorIndex).y;
                float lineH = input.lineHeight + 8f;
                if (caretY < _scroll.y) _scroll.y = Mathf.Max(0f, caretY - 8f);
                else if (caretY + lineH > _scroll.y + viewport.height)
                    _scroll.y = Mathf.Min(inputH - viewport.height, caretY + lineH - viewport.height);
            }
            if (text != _draft)
            {
                _draft = text;
                _pending = null;
                _error = string.Empty;
            }
            if (_draft.Length == 0 && GUI.GetNameOfFocusedControl() != "FeedbackDraft")
            {
                var hint = new GUIStyle(body) { normal = { textColor = IMGUIStyles.PaperTextDisabled } };
                IMGUIStyles.DrawLabel(new Rect(field.x + 10f, field.y + 8f, inputW - 20f, field.height - 16f),
                    UiText.Get("请输入反馈内容"), hint);
            }
            var secondary = new GUIStyle(body) { fontSize = IMGUIStyles.FontSize(12),
                normal = { textColor = IMGUIStyles.PaperTextSecondary } };
            var counter = new GUIStyle(secondary) { alignment = TextAnchor.UpperRight, wordWrap = false };
            IMGUIStyles.DrawLabel(new Rect(panel.xMax - 100f, contextY, 76f, 20f),
                $"{_draft.Length} / {FeedbackClient.MaxTextLength}", counter);
            if (_error.Length > 0)
            {
                var errorStyle = new GUIStyle(secondary) { normal = { textColor = IMGUIStyles.SealRed } };
                IMGUIStyles.DrawLabel(new Rect(x, contextY + 22f, width, 24f), _error, errorStyle);
            }
            const float gap = 12f;
            float buttonW = Mathf.Min(132f, (width - gap) / 2f);
            if (PaperButton(new Rect(panel.xMax - 24f - buttonW * 2f - gap, footerY, buttonW, 40f),
                UiText.Get("返回游戏"), ui, false, true)) Close();
            bool enabled = !_client.IsSubmitting && !string.IsNullOrWhiteSpace(_draft);
            if (PaperButton(new Rect(panel.xMax - 24f - buttonW, footerY, buttonW, 40f),
                UiText.Get(_client.IsSubmitting ? "提交中…" : "提交反馈"), ui, true, enabled))
            {
                GUI.FocusControl(null);
                _pending ??= new FeedbackClient.Submission { text = _draft, version = Application.version,
                    location = _location, inEncounter = _inEncounter, worldDay = _worldDay };
                _error = string.Empty;
                owner.StartCoroutine(_client.Submit(_pending, (saved, status) =>
                {
                    _saved = saved;
                    if (saved) { _draft = string.Empty; _pending = null; _scroll = Vector2.zero; }
                    else _error = UiText.Get(status == 429
                        ? "提交太频繁，请稍后重试。文字已保留。"
                        : "提交失败，请检查网络后重试。文字已保留。");
                }));
            }
        }

        private static bool PaperButton(Rect rect, string text, IMGUIInteractionContext ui, bool primary, bool enabled)
        {
            bool canInteract = enabled && !ui.IsLocked;
            bool hover = canInteract && ui.CanHover(rect);
            if (primary)
            {
                IMGUIStyles.SetColor(canInteract ? IMGUIStyles.Gold : IMGUIStyles.TabMutedPaper);
                GUI.DrawTexture(UIScale.PixelSnap(rect), Texture2D.whiteTexture);
                IMGUIStyles.ResetColor();
            }
            else IMGUIStyles.DrawOutline(rect, 1f, IMGUIStyles.PaperTextDisabled);
            if (hover) IMGUIStyles.DrawOutline(rect, 1f, IMGUIStyles.PaperInk);
            var style = new GUIStyle(IMGUIStyles.ModalBody) { alignment = TextAnchor.MiddleCenter,
                normal = { textColor = canInteract ? IMGUIStyles.PaperInk : IMGUIStyles.PaperTextDisabled } };
            IMGUIStyles.DrawLabel(rect, text, style);
            if (!canInteract || !ui.WasTapped(rect)) return false;
            Event.current.Use();
            return true;
        }

        public void Close()
        {
            IsOpen = false;
            _saved = false;
            _clearFocus = true;
        }

        public void Dispose() => _client.Dispose();
    }
}
