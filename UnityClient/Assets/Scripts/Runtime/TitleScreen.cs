#nullable enable
using UnityEngine;
using SSNoir.Core;
using SSNoir.IMGUI;
using SSNoir.IMGUI.Stage;

namespace SSNoir
{
    /// <summary>
    /// 标题菜单：进游戏之前停在世界视角上的那一层。
    ///
    /// 它是一个模式，不是一个面板——和影幕同一个道理：菜单在的时候世界照常渲染（那就是背景，
    /// 活的，不是一张图），但一个游戏控件都不出现，镜头也不接受拖拽。所以它排在 OnGUI 最前面
    /// 画完就 return，并且算进 IsInputLocked。
    ///
    /// 「新游戏」之后发生什么，这里一个字都不知道：它只负责把世界重置回第一天，然后交给剧本里
    /// 的 '开场动作 全局。客户端不认识"来访"，也不认识"有人敲门"——换开场、换章节，改的都是
    /// .scm，不是这个文件。
    /// </summary>
    public class TitleScreen
    {

        private const float ColumnX = 0.10f;   // 文字栏左边距（屏宽占比）
        private const float TitleY = 0.15f;    // 片名基线（屏高占比）
        private const float TitleMenuGap = 36f;
        private const float ButtonWidth = 320f;
        private const float ItemHeight = 48f;
        private const float ItemSpacing = 10f;

        private readonly SSNoirGameManager _gameManager;

        private bool _isActive;
        private float _openedAt;
        private bool _leaving;
        private float _leaveAt;
        private string? _selectedItem;
        private float ForegroundAlpha => _leaving ? 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01((Time.unscaledTime - _leaveAt) / 0.55f)) : 1f;
        private PortraitSkeleton? _wordmark;
        private PortraitSkeleton? _heading;
        private static Texture2D? _backdrop;

        // 启动页专属遮光幕：左侧文字区安静，右侧城市保留层次，边缘收暗。
        // 一张连续渐变避免分段蒙版的边界；不改变世界本身的材质或灯光。
        private static void DrawBackdrop(float width, float height, float alpha)
        {
            if (_backdrop == null)
            {
                const int w = 256, h = 128;
                var pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = x / (float)(w - 1), v = y / (float)(h - 1);
                        float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, u));
                        float shade = Mathf.Lerp(0.89f, 0.52f, fade);
                        float edge = Mathf.Pow(Mathf.Abs(v * 2f - 1f), 3f) * 0.18f;
                        shade = shade + (1f - shade) * edge;
                        pixels[y * w + x] = new Color(0.008f, 0.009f, 0.022f, shade);
                    }
                _backdrop = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                _backdrop.SetPixels(pixels);
                _backdrop.Apply(false, true);
            }
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), _backdrop);
            GUI.color = previous;
        }

        private static PortraitSkeleton LoadSign(string name)
        {
            var asset = Resources.Load<TextAsset>("Title/" + name + ".neon");
            if (asset == null) throw new System.InvalidOperationException("缺少标题灯管：" + name);
            var skeleton = PortraitSkeleton.Parse(asset.text);
            if (skeleton == null || skeleton.TotalLength <= 0f)
                throw new System.InvalidOperationException("标题灯管骨架为空：" + name);
            return skeleton;
        }

        // 最近一次存档：读档走的是它，不是 SaveManager.DefaultSavePath。那条路径只是拿来
        // 推导存档目录的（槽位文件名由它的 dirname 拼出来），本身从来没有人往里写过。
        private string? _latestSlotPath;
        private string _latestSaveTime = string.Empty;

        public TitleScreen(SSNoirGameManager gameManager)
        {
            _gameManager = gameManager;
        }

        public bool IsActive => _isActive;

        /// <summary>
        /// 升起菜单，并挑出最近的那个存档槽。
        ///
        /// 这里读一次文件，不每帧读——OnGUI 一秒要跑几十遍，而 GetSaveTime 是要把整个存档
        /// 解析成 JSON 的。存档时间是 "2026-08-06 10:39:15" 这种写法，直接按字典序比就是
        /// 按时间比。
        /// </summary>
        public void Open()
        {
            _wordmark ??= LoadSign("Belleville");
            _heading ??= LoadSign("Ballads");
            _leaving = false;
            _selectedItem = null;
            _openedAt = Time.unscaledTime;
            _isActive = true;
            _latestSlotPath = null;
            _latestSaveTime = string.Empty;

            for (int slot = 1; slot <= SaveManager.SlotCount; slot++)
            {
                string path = SaveManager.GetSlotFilePath(slot);
                string time = SaveManager.GetSaveTime(path);   // 没有存档时返回空串
                if (string.IsNullOrEmpty(time)
                    || string.CompareOrdinal(time, _latestSaveTime) <= 0)
                    continue;

                _latestSaveTime = time;
                _latestSlotPath = path;
            }
        }

        public void Close()
        {
            _isActive = false;
        }

        // ── 三个入口 ──────────────────────────────────────────────────────

        /// <summary>
        /// 新游戏：重置世界，然后把开场交给剧本。
        ///
        /// 菜单先退场再重置，顺序不能反——开场动作可能当场就拉起过场，影幕要占满屏幕，
        /// 菜单不能还压在上面。
        /// </summary>
        private void NewGame()
        {
            Close();
            _gameManager.RestartGame();
            _gameManager.RunOpeningAction();
        }

        /// <summary>读档：读进来就是玩到一半的世界，没有开场。</summary>
        private void Continue()
        {
            Close();
            _gameManager.LoadGame(_latestSlotPath);
        }

        private void BeginLeave(bool newGame, string label)
        {
            if (_leaving) return;
            _leaving = true;
            _leaveAt = Time.unscaledTime;
            _selectedItem = label;
            _gameManager.StartCoroutine(Leave(newGame));
        }

        private System.Collections.IEnumerator Leave(bool newGame)
        {
            // 用非缩放时间，过渡期间标题模式继续占住输入。
            while (_isActive && Time.unscaledTime - _leaveAt < 0.9f)
                yield return null;
            if (!_isActive) yield break;
            if (newGame) NewGame(); else Continue();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── 绘制 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 画菜单。由 IMGUIWorldRenderer 在所有游戏控件之前调用，调完那边就直接 return。
        /// </summary>
        public void Draw(Vector2 mouse)
        {
            var ui = new IMGUIInteractionContext(mouse, isLocked: _leaving);

            float vw = UIScale.VW;
            float vh = UIScale.VH;
            DrawBackdrop(vw, vh, _leaving ? 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((Time.unscaledTime - _leaveAt - 0.15f) / 0.75f)) : 1f);

            float x = vw * ColumnX;
            // 标题组与菜单按内容高度往下排；窄宽高比只收招牌宽度。
            float titleY = vh * TitleY;

            float signWidth = Mathf.Min(720f, vw - x * 2f);
            float signHeight = signWidth * 0.3f;
            float age = Time.unscaledTime - _openedAt;
            var sign = new Rect(x - signWidth * 0.045f, titleY + 26f, signWidth, signHeight);
            float hue = 0.5f + 0.5f * Mathf.Sin(age * 0.22f);
            var rose = Color.Lerp(new Color(1f, 0.12f, 0.29f), new Color(0.78f, 0.16f, 1f), hue * 0.65f);
            var violet = new Color(0.65f, 0.39f, 1f);
            var look = new LampLook(1f, 0.95f, 1f, rose, true, 1f, 0f, current: 1f);
            LampPainter.PaintSkeleton(sign, _wordmark!, look, age, ForegroundAlpha);
            LampPainter.PaintSkeleton(new Rect(x + signWidth * 0.11f, titleY, signWidth * 0.48f, 21f),
                _heading!, new LampLook(1f, 0.8f, 1f, violet, true, 1f, 0f), Mathf.Max(0f, age - 0.3f), ForegroundAlpha);

            float textX = x + signWidth * 0.11f;
            // 字形底部留有空白，小字紧跟实际下划弧线，而不是贴图外框。
            var caption = new Rect(textX, sign.y + sign.height * 0.93f + 8f, signWidth * 0.8f, 28f);
            var captionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = IMGUIStyles.FontSize(16),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.86f, 0.83f, 0.81f, ForegroundAlpha) }
            };
            IMGUIStyles.ApplyStrongFont(captionStyle);
            IMGUIStyles.DrawLabel(caption,
                UiText.Get("贝尔维尔的歌谣"), captionStyle);

            float languageX = vw - 180f;
            if (DrawItem(new Rect(languageX, 30f, 68f, 36f),
                    "中文", ui,
                    enabled: true, compact: true, active: GameLanguage.Current == GameLanguage.Chinese))
                GameLanguage.Current = GameLanguage.Chinese;
            if (DrawItem(new Rect(languageX + 74f, 30f, 94f, 36f),
                    "English", ui,
                    enabled: true, compact: true, active: GameLanguage.Current == GameLanguage.English))
                GameLanguage.Current = GameLanguage.English;

            float itemY = caption.yMax + TitleMenuGap - 8f;
            float menuWidth = Mathf.Min(ButtonWidth, vw - textX - x);
            float menuX = textX;

            if (DrawItem(new Rect(menuX, itemY, menuWidth, ItemHeight),
                    UiText.Get("新游戏"), ui, enabled: true))
            {
                BeginLeave(true, UiText.Get("新游戏"));
                return;
            }
            itemY += ItemHeight + ItemSpacing;

            // 没有存档时留着但灰掉：位置固定，玩家才知道自己缺的是什么，而不是以为没这功能。
            // 存档时间收在读档选项右侧，以较小字号形成次级信息。
            if (DrawItem(new Rect(menuX, itemY, menuWidth, ItemHeight),
                    UiText.Get("从存档加载"), ui,
                    enabled: _latestSlotPath != null, hint: _latestSaveTime))
            {
                BeginLeave(false, UiText.Get("从存档加载"));
                return;
            }
            itemY += ItemHeight + ItemSpacing;

            // 浏览器里没有"退出游戏"这回事，Application.Quit() 是个空操作，摆上去只会骗人。
#if !UNITY_WEBGL || UNITY_EDITOR
            if (DrawItem(new Rect(menuX, itemY, menuWidth, ItemHeight),
                    UiText.Get("退出游戏"), ui, enabled: true))
            {
                Quit();
                return;
            }
#endif
        }

        // 半透明底与轻描边建立点击范围，悬停时微亮。
        private bool DrawItem(
            Rect rect, string label, IMGUIInteractionContext ui, bool enabled,
            string hint = "", bool compact = false, bool active = false)
        {
            bool hovered = enabled && ui.CanHover(rect);
            bool selected = _leaving && _selectedItem == label;
            float alpha = ForegroundAlpha;
            var textColor = !enabled ? IMGUIStyles.TextDisabled
                : selected ? new Color(1f, 0.73f, 0.86f)
                : hovered || active ? new Color(1f, 0.47f, 0.71f)
                : compact ? new Color(0.65f, 0.63f, 0.68f) : IMGUIStyles.TextPrimary;
            textColor.a *= alpha;

            if (!compact)
            {
                var previous = GUI.color;
                GUI.color = new Color(0.015f, 0.02f, 0.04f,
                    (hovered || selected ? 0.62f : enabled ? 0.48f : 0.28f) * alpha);
                GUI.DrawTexture(UIScale.PixelSnap(rect), Texture2D.whiteTexture);
                GUI.color = previous;
                var border = hovered || selected ? new Color(1f, 0.47f, 0.71f, 0.3f * alpha)
                    : new Color(0.85f, 0.81f, 0.85f, (enabled ? 0.11f : 0.06f) * alpha);
                IMGUIStyles.DrawOutline(UIScale.PixelSnap(rect), 1f, border);
            }

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = IMGUIStyles.FontSize(compact ? 14 : 22),
                alignment = compact ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal =
                {
                    textColor = textColor,
                },
            };
            IMGUIStyles.ApplyStrongFont(style);
            float inset = compact ? 4f : (hovered || selected ? 23f : 20f);
            var labelRect = new Rect(rect.x + inset, rect.y, rect.width - inset * 2f, rect.height);
            var shadow = new GUIStyle(style);
            shadow.normal.textColor = new Color(0f, 0f, 0f, 0.3f * alpha);
            IMGUIStyles.DrawLabel(new Rect(labelRect.x + 0.5f, labelRect.y + 0.75f, labelRect.width, labelRect.height), label, shadow);
            IMGUIStyles.DrawLabel(labelRect, label, style);
            if (!string.IsNullOrEmpty(hint))
            {
                var hintStyle = new GUIStyle(GUI.skin.label)
                {
                    font = IMGUIStyles.ChineseFont,
                    fontSize = IMGUIStyles.FontSize(11),
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(IMGUIStyles.TextDisabled.r, IMGUIStyles.TextDisabled.g, IMGUIStyles.TextDisabled.b, alpha * 0.65f) },
                };
                IMGUIStyles.DrawLabel(
                    new Rect(rect.x + 20f, rect.y, rect.width - 36f, rect.height), hint, hintStyle);
            }

            if (!enabled || !ui.WasTapped(rect))
                return false;

            // 吃掉这次点击：菜单下面就是世界，漏过去会当成一次世界点击。
            Event.current.Use();
            return true;
        }
    }
}
