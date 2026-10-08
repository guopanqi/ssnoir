#nullable enable
using System;
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>面向演示者的入口；名单独立于开发调试场景扫描。</summary>
    public static class DemoPanelDrawer
    {
        [Serializable] private sealed class Entry
        {
            public string label = "";
            public string scene = "";
            public string stage = "";
            public string entry = "";
        }
        [Serializable] private sealed class Catalog { public Entry[] encounters = Array.Empty<Entry>(); }
        private static Entry[] _entries = Array.Empty<Entry>();
        private static bool _isOpen;
        private static float _scrollOffset;
        private static bool _scrollDragActive;
        private static float _scrollDragLastY;
        private static float _scrollDragTravel;
        private const float RowH = 34f;
        public static bool IsOpen => _isOpen;
        public static void Close() { _isOpen = false; _scrollDragActive = false; }
        public static void Reset() { Close(); _scrollOffset = 0f; }

        public static Rect GetPanelRect(TopHudLayout hud)
        {
            Rect safe = UIScale.SafeArea;
            float width = Mathf.Min(520f, safe.width - 32f);
            float y = hud.DemoToggle.yMax + 4f;
            return new Rect(Mathf.Max(safe.x, hud.DemoToggle.xMax - width), y,
                width, Mathf.Min(ContentHeight(), safe.yMax - y - 8f));
        }

        private static void Open(SSNoirGameManager gm)
        {
            var asset = Resources.Load<TextAsset>("DemoEncounters")
                ?? throw new InvalidOperationException("缺少演示交锋名单 DemoEncounters。");
            var catalog = JsonUtility.FromJson<Catalog>(asset.text);
            if (catalog == null || catalog.encounters.Length == 0)
                throw new InvalidOperationException("演示交锋名单不能为空。");
            var scenes = new System.Collections.Generic.HashSet<string>();
            foreach (var item in catalog.encounters)
            {
                if (string.IsNullOrWhiteSpace(item.label) || !scenes.Add(item.scene)
                    || Resources.Load<TextAsset>("Content/scenes/encounters/" + item.scene) == null
                    || gm.ResolveStageAnchor(item.stage) == null)
                    throw new InvalidOperationException($"演示交锋配置无效：{item.label} / {item.scene} / {item.stage}");
            }
            _entries = catalog.encounters;
            _scrollOffset = 0f;
            _isOpen = true;
            DebugPanelDrawer.Close();
        }

        public static void Draw(SSNoirGameManager gm, IMGUIInteractionContext ui, TopHudLayout hud)
        {
            if (IMGUIButton.DrawTopTextToggle(hud.DemoToggle, UiText.Get("演示"), _isOpen, ui))
            {
                if (_isOpen) Close(); else Open(gm);
            }
            if (!_isOpen) return;
            Rect panel = GetPanelRect(hud);
            float contentHeight = ContentHeight();
            float maxScroll = Mathf.Max(0f, contentHeight - panel.height);
            UpdateScroll(ui, panel, maxScroll);
            _scrollOffset = Mathf.Clamp(_scrollOffset, 0f, maxScroll);
            GUI.color = new Color(IMGUIStyles.HudBg.r, IMGUIStyles.HudBg.g, IMGUIStyles.HudBg.b, IMGUIStyles.ModalOpacity);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(panel, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, .40f));
            var style = new GUIStyle(GUI.skin.label)
            {
                font = IMGUIStyles.ChineseFont,
                fontSize = IMGUIStyles.FontSize(13),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = IMGUIStyles.TextPrimary }
            };
            var muted = new GUIStyle(style)
            {
                fontSize = IMGUIStyles.FontSize(11),
                normal = { textColor = IMGUIStyles.TextDisabled }
            };
            float split = panel.width / 2f;
            IMGUIStyles.DrawLine(new Vector2(panel.x + split, panel.y + 8f),
                new Vector2(panel.x + split, panel.yMax - 8f),
                new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, .25f), 1f);
            GUI.BeginGroup(panel);
            try
            {
                var contentUi = panel.Contains(ui.Mouse)
                    ? ui.Translated(panel.position) : ui.Translated(panel.position).Occluded();
                float top = 8f - _scrollOffset;
                IMGUIStyles.DrawLabel(new Rect(8f, top + 2f, split - 16f, 18f), "地点工具", muted);
                IMGUIStyles.DrawLabel(new Rect(split + 8f, top + 2f, split - 16f, 18f), "交锋（直接进入）", muted);
                float rowY = top + 20f;
                bool world = gm.SceneManager.CurrentSceneName == "world";
                if (Row(0f, rowY, "解锁全部地点", world))
                {
                    gm.UnlockDemoLocations();
                    Close(); return;
                }
                if (Row(0f, rowY + RowH, "返回城市全景", true))
                {
                    gm.OnSceneButtonClicked("world");
                    gm.SetFocusedNode(null, updateCamera: true);
                    Close(); return;
                }
                for (int i = 0; i < _entries.Length; i++)
                {
                    var entry = _entries[i];
                    bool current = gm.SceneManager.CurrentSceneName == entry.scene;
                    if (Row(split, rowY + i * RowH, entry.label, !current, current))
                    {
                        gm.EnterDemoEncounter(entry.scene, entry.entry);
                        Close(); return;
                    }
                }

                bool Row(float x, float y, string label, bool enabled, bool current = false)
                {
                    var rect = new Rect(x + 4f, y, split - 8f, RowH - 2f);
                    bool hovered = enabled && contentUi.CanHover(rect);
                    if (hovered)
                    {
                        GUI.color = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, .08f);
                        GUI.DrawTexture(rect, Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }
                    if (current)
                    {
                        GUI.color = IMGUIStyles.Gold;
                        GUI.DrawTexture(new Rect(rect.x, rect.y + 2f, 3f, rect.height - 4f), Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }
                    var rowStyle = new GUIStyle(style)
                    {
                        normal = { textColor = current ? IMGUIStyles.Gold : !enabled
                            ? IMGUIStyles.TextDisabled : hovered ? IMGUIStyles.TextPrimary : IMGUIStyles.TextSecondary }
                    };
                    IMGUIStyles.DrawLabel(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 12f, rect.height), label, rowStyle);
                    return enabled && contentUi.WasTapped(rect);
                }
            }
            finally { GUI.EndGroup(); }
            DrawScrollbar(panel, contentHeight, maxScroll);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && !ui.IsLocked
                && !panel.Contains(ui.Mouse) && !hud.DemoToggle.Contains(ui.Mouse))
                Close();
        }

        private static float ContentHeight() => 8f + 20f + Mathf.Max(2, _entries.Length) * RowH + 8f;
        private static void UpdateScroll(IMGUIInteractionContext ui, Rect panelRect, float maxScroll)
        {
            bool pointerInPanel = panelRect.Contains(ui.Mouse);
            var e = Event.current;

            if (pointerInPanel && e.type == EventType.ScrollWheel && maxScroll > 0f)
            {
                _scrollOffset += e.delta.y * 18f;
                e.Use();
                return;
            }

            // 桌面用滚轮；手机用手指拖动。面板内的按钮都在 MouseUp 且未滑动时响应，
            // 因此从按钮上开始滑动也不会误触。
            if (UIScale.HasHoverPointer)
                return;

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && pointerInPanel && maxScroll > 0f:
                    _scrollDragActive = true;
                    _scrollDragLastY = ui.Mouse.y;
                    _scrollDragTravel = 0f;
                    break;

                case EventType.MouseDrag when _scrollDragActive:
                {
                    float dy = ui.Mouse.y - _scrollDragLastY;
                    _scrollDragLastY = ui.Mouse.y;
                    _scrollDragTravel += Mathf.Abs(dy);
                    _scrollOffset -= dy;
                    if (_scrollDragTravel >= IMGUIInteractionContext.TapSlop)
                        e.Use();
                    break;
                }

                case EventType.MouseUp:
                    _scrollDragActive = false;
                    _scrollDragTravel = 0f;
                    break;
            }
        }

        private static void DrawScrollbar(Rect panelRect, float contentHeight, float maxScroll)
        {
            if (maxScroll <= 0f)
                return;

            const float trackW = 5f;
            var track = new Rect(panelRect.xMax - trackW - 3f, panelRect.y + 4f,
                trackW, panelRect.height - 8f);
            float thumbH = Mathf.Max(RowH, track.height * (panelRect.height / contentHeight));
            float travel = Mathf.Max(0f, track.height - thumbH);
            var thumb = new Rect(track.x, track.y + travel * (_scrollOffset / maxScroll), track.width, thumbH);

            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, 0.65f);
            GUI.DrawTexture(track, Texture2D.whiteTexture);
            GUI.color = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.55f);
            GUI.DrawTexture(thumb, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

    }
}
