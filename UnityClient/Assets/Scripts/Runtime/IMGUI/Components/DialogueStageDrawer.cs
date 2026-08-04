#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    // 阻塞式 play-dialogue! 的专用舞台：大幅当前说话人立绘 + 底部对白框。
    // Banter 仍由 DialogueBubbleDrawer 依附世界锚点绘制，两种表现不再混用。
    public static class DialogueStageDrawer
    {
        private const string NarratorSpeaker = "世界";
        private const string ProtagonistSpeaker = "主角";
        private const string PortraitResourceRoot = "Portraits/";
        // 同名文件放进 Portraits/Neon/（见 NeonPortraitLibrary）就切换到霓虹灯管表现；
        // 留在 Portraits/ 下仍走传统半身像。
        private const float EnterDuration = 0.20f;
        private const float TypewriterCharactersPerSecond = 30f;

        // 霓虹辉光的染色（灯管本身的颜色来自贴图，这里只染外层光晕与光池）。
        private static readonly Color NeonGlow = new Color(0.30f, 0.58f, 1f);

        private static readonly Dictionary<string, Portrait> PortraitCache = new();
        private static readonly HashSet<string> MissingPortraitWarnings = new();
        private static string _visibleLineKey = string.Empty;
        private static string _visibleSpeaker = string.Empty;
        private static float _lineStartedAt;
        private static float _portraitStartedAt;
        private static int _currentLineLength;
        private static bool _currentLineCompletedInstantly;

        public static bool IsCurrentLineFullyRevealed =>
            _currentLineCompletedInstantly || VisibleCharacterCount() >= _currentLineLength;

        public static void CompleteCurrentLine() => _currentLineCompletedInstantly = true;

        public static void BeginConversation()
        {
            _visibleLineKey = string.Empty;
            _visibleSpeaker = string.Empty;
            _lineStartedAt = 0f;
            _portraitStartedAt = 0f;
            _currentLineLength = 0;
            _currentLineCompletedInstantly = false;
        }

        // 返回 true：普通 play-dialogue! 的说话人此刻不在场，画面仍使用对白舞台，但调用方应报警。
        // 显式 play-remote-dialogue! 允许同一舞台承接场外人物，不视为降级。
        public static bool DrawConversationLine(
            string speaker,
            string text,
            int lineIndex,
            DialogueAnchors anchors,
            bool allowsRemoteParticipant)
        {
            bool isNarration = speaker == NarratorSpeaker;
            bool usedRemoteFallback = !isNarration
                && !anchors.TryResolve(speaker, out _)
                && !allowsRemoteParticipant;

            // 行号保证连续两句内容完全相同时仍被视为两句，各自触发弹跳和打字机。
            string lineKey = lineIndex + "\n" + speaker + "\n" + text;
            if (_visibleLineKey != lineKey)
            {
                _visibleLineKey = lineKey;
                _lineStartedAt = Time.unscaledTime;
                _currentLineLength = text.Length;
                _currentLineCompletedInstantly = false;
            }

            if (_visibleSpeaker != speaker)
            {
                _visibleSpeaker = speaker;
                _portraitStartedAt = Time.unscaledTime;
            }

            float lineReveal = Mathf.Clamp01((Time.unscaledTime - _lineStartedAt) / EnterDuration);
            float lineEase = 1f - Mathf.Pow(1f - lineReveal, 3f);
            float portraitReveal = Mathf.Clamp01((Time.unscaledTime - _portraitStartedAt) / EnterDuration);
            float portraitEase = 1f - Mathf.Pow(1f - portraitReveal, 3f);
            int visibleCharacterCount = _currentLineCompletedInstantly
                ? text.Length
                : Mathf.Min(text.Length, VisibleCharacterCount());
            string visibleText = visibleCharacterCount >= text.Length
                ? text
                : text.Substring(0, visibleCharacterCount);
            bool isTyping = visibleCharacterCount < text.Length;

            var portrait = isNarration ? default : LoadPortrait(speaker);
            DrawBackdrop();
            bool portraitOnLeft = speaker == ProtagonistSpeaker;
            if (!isNarration)
                DrawPortraitStage(speaker, portrait, portraitOnLeft, portraitEase);
            DrawDialogueBox(speaker, visibleText, text, isTyping, isNarration, portraitOnLeft, portrait.IsNeon, lineEase);
            return usedRemoteFallback;
        }

        private static int VisibleCharacterCount()
        {
            float elapsed = Mathf.Max(0f, Time.unscaledTime - _lineStartedAt);
            return Mathf.CeilToInt(elapsed * TypewriterCharactersPerSecond);
        }

        private static void DrawBackdrop()
        {
            // 全局遮罩两种模式一致：把人物那一圈理干净的是 DrawNeonVignette 的暗晕，
            // 不该由全局遮罩去背这个锅——那样会连远处的场景一起关掉。
            GUI.color = new Color(0.004f, 0.009f, 0.020f, 0.78f);
            GUI.DrawTexture(new Rect(0f, 0f, UIScale.VW, UIScale.VH), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static void DrawPortraitStage(string speaker, Portrait portrait, bool onLeft, float reveal)
        {
            float dialogueTop = UIScale.VH - Mathf.Min(230f, UIScale.VH * 0.26f) - 44f;
            float portraitTop = 72f;
            float portraitHeight = Mathf.Clamp(dialogueTop + 42f - portraitTop, 360f, 720f);
            // 霓虹是一整块封闭灯管图形，切半身等于把灯管掐断，因此改用整幅招牌的窄长比例。
            float portraitWidth = portraitHeight * (portrait.IsNeon ? NeonCropWidth / NeonCropHeight : 0.68f);
            float restingX = onLeft ? 64f : UIScale.VW - portraitWidth - 64f;

            if (portrait.Texture == null)
            {
                DrawMissingPortrait(new Rect(restingX, portraitTop, portraitHeight * 0.68f, portraitHeight), speaker, reveal);
                return;
            }

            if (portrait.IsNeon)
            {
                // 霓虹不做横向滑入，改为「通电点亮」：位置固定，亮度带一次跳闸再稳住。
                DrawNeonPortrait(new Rect(restingX, portraitTop, portraitWidth, portraitHeight), portrait.Texture, reveal);
                return;
            }

            float enteringOffset = (1f - reveal) * 28f * (onLeft ? -1f : 1f);
            var portraitRect = new Rect(restingX + enteringOffset, portraitTop, portraitWidth, portraitHeight);
            // 用同一张 Alpha 贴图偏移绘制人物轮廓阴影；不能再画矩形卡底或矩形投影。
            var shadowRect = new Rect(portraitRect.x + (onLeft ? 9f : -9f), portraitRect.y + 12f,
                portraitRect.width, portraitRect.height);
            GUI.color = new Color(0f, 0f, 0f, 0.58f * reveal);
            DrawPortraitTexture(shadowRect, portrait.Texture);
            GUI.color = new Color(1f, 1f, 1f, reveal);
            DrawPortraitTexture(portraitRect, portrait.Texture);
            GUI.color = Color.white;
        }

        // 霓虹贴图是方形画布、人物只占中间一条；这几个常量把灯管那一条裁出来。
        // 换新的霓虹立绘若构图不同，只需重调这四个值。
        private const float NeonCropX = 0.30f;
        private const float NeonCropY = 0.02f;
        private const float NeonCropWidth = 0.44f;
        private const float NeonCropHeight = 0.97f;

        private static void DrawNeonPortrait(Rect rect, Texture2D portrait, float reveal)
        {
            var uv = new Rect(NeonCropX, NeonCropY, NeonCropWidth, NeonCropHeight);
            float brightness = NeonBrightness(reveal);

            // 人物正后方的暗晕：中心几乎全黑、向外径向散尽。
            // 霓虹的好看全靠亮度对比，背后必须是黑；而暗晕只罩住人物这一圈，
            // 远处的城市原样留着，不会把整块画面关掉。
            DrawNeonVignette(rect);

            // 底部光池：灯管把地面照出一摊光，也把人物和对白框连起来。
            DrawLightPool(rect, brightness);

            // 外层光晕：同一张图逐层放大、压暗地叠出溢光，代替做不到的加法混合。
            for (int i = 3; i >= 1; i--)
            {
                float spread = i * 9f;
                var halo = new Rect(rect.x - spread, rect.y - spread, rect.width + spread * 2f, rect.height + spread * 2f);
                GUI.color = new Color(NeonGlow.r, NeonGlow.g, NeonGlow.b, 0.13f / i * brightness);
                GUI.DrawTextureWithTexCoords(halo, portrait, uv, true);
            }

            // 灯管本体：叠两遍让细线的亮度压住背景，不至于被光晕吃掉。
            GUI.color = new Color(1f, 1f, 1f, brightness);
            GUI.DrawTextureWithTexCoords(rect, portrait, uv, true);
            GUI.color = new Color(1f, 1f, 1f, 0.55f * brightness);
            GUI.DrawTextureWithTexCoords(rect, portrait, uv, true);

            DrawNeonReflection(rect, portrait, uv, brightness);
            GUI.color = Color.white;
        }

        // 电流感：低频呼吸 + 偶发跳闸；点亮瞬间先抖两下再稳定。
        private static float NeonBrightness(float reveal)
        {
            float t = Time.unscaledTime;
            float hum = 0.93f + 0.07f * Mathf.Sin(t * 2.3f) * Mathf.Sin(t * 0.71f + 1.3f);

            float phase = t * 0.31f;
            float cell = Mathf.Floor(phase);
            float noise = Mathf.Abs(Mathf.Sin(cell * 127.1f) * 43758.5453f);
            noise -= Mathf.Floor(noise);
            float within = phase - cell;
            if (noise > 0.88f && within < 0.10f)
                hum *= 0.52f + 0.30f * Mathf.Sin(within * 110f);

            float ignite = reveal < 1f
                ? (0.18f + 0.82f * reveal) * (reveal < 0.55f && Mathf.Sin(reveal * 52f) < 0f ? 0.32f : 1f)
                : 1f;
            return hum * ignite;
        }

        private static void DrawNeonVignette(Rect rect)
        {
            var halo = NeonPortraitLibrary.RadialFalloff();
            var area = new Rect(
                rect.center.x - rect.width * 1.75f,
                rect.center.y - rect.height * 0.95f,
                rect.width * 3.5f,
                rect.height * 1.90f);
            // 叠三遍：最外圈大而淡负责过渡，中圈补浓，内圈保证人物正后方是死黑。
            GUI.color = new Color(0.004f, 0.007f, 0.016f, 0.88f);
            GUI.DrawTexture(area, halo);
            var mid = new Rect(
                rect.center.x - rect.width * 1.15f,
                rect.center.y - rect.height * 0.70f,
                rect.width * 2.3f,
                rect.height * 1.40f);
            GUI.color = new Color(0.004f, 0.007f, 0.016f, 0.86f);
            GUI.DrawTexture(mid, halo);
            var core = new Rect(
                rect.center.x - rect.width * 0.80f,
                rect.center.y - rect.height * 0.54f,
                rect.width * 1.6f,
                rect.height * 1.08f);
            GUI.color = new Color(0.004f, 0.007f, 0.016f, 0.84f);
            GUI.DrawTexture(core, halo);
        }

        private static void DrawLightPool(Rect rect, float brightness)
        {
            // 脚下一摊光：同一张径向渐变压扁成椭圆，一笔画完，不再有横条阶梯。
            var pool = new Rect(
                rect.center.x - rect.width * 1.15f,
                rect.yMax - rect.height * 0.20f,
                rect.width * 2.3f,
                rect.height * 0.44f);
            GUI.color = new Color(NeonGlow.r, NeonGlow.g, NeonGlow.b, 0.16f * brightness);
            GUI.DrawTexture(pool, NeonPortraitLibrary.RadialFalloff());
        }

        // 湿地面上的倒影：整块竖直翻转画一次，再盖一层竖直渐变把它抹进地面。
        private static void DrawNeonReflection(Rect rect, Texture2D portrait, Rect uv, float brightness)
        {
            float reflectHeight = rect.height * 0.16f;
            float uvHeight = uv.height * (reflectHeight / rect.height);
            var area = new Rect(rect.x, rect.yMax, rect.width, reflectHeight);
            var flipped = new Rect(uv.x, uv.y + uvHeight, uv.width, -uvHeight);

            GUI.color = new Color(1f, 1f, 1f, 0.20f * brightness);
            GUI.DrawTextureWithTexCoords(area, portrait, flipped, true);
            GUI.color = new Color(0.004f, 0.007f, 0.016f, 1f);
            GUI.DrawTexture(area, NeonPortraitLibrary.VerticalFade());
        }

        private static void DrawPortraitTexture(Rect rect, Texture2D portrait)
        {
            float sourceAspect = portrait.width / (float)portrait.height;
            if (sourceAspect <= 1.2f)
            {
                GUI.DrawTexture(rect, portrait, ScaleMode.ScaleAndCrop);
                return;
            }

            // 当前角色图是横版全身构图。截取画面上部并按立绘框宽高比收窄，形成经典半身像，
            // 同时不拉伸人物；未来直接提供竖版立绘时会走上面的常规裁切。
            const float cropBottom = 0.36f;
            const float cropHeight = 0.60f;
            float cropWidth = Mathf.Clamp((rect.width / rect.height) * cropHeight / sourceAspect, 0.16f, 1f);
            var uv = new Rect((1f - cropWidth) * 0.5f, cropBottom, cropWidth, cropHeight);
            GUI.DrawTextureWithTexCoords(rect, portrait, uv, true);
        }

        private readonly struct Portrait
        {
            public readonly Texture2D? Texture;
            public readonly bool IsNeon;
            public Portrait(Texture2D? texture, bool isNeon)
            {
                Texture = texture;
                IsNeon = isNeon;
            }
        }

        private static Portrait LoadPortrait(string speaker)
        {
            if (PortraitCache.TryGetValue(speaker, out var cached))
                return cached;

            var neon = NeonPortraitLibrary.Load(speaker);
            var portrait = neon != null
                ? new Portrait(neon, true)
                : new Portrait(Resources.Load<Texture2D>(PortraitResourceRoot + speaker), false);
            PortraitCache[speaker] = portrait;
            if (portrait.Texture == null && MissingPortraitWarnings.Add(speaker))
            {
                Debug.LogWarning(
                    $"[SSNoir] play-dialogue! 说话人 '{speaker}' 尚无立绘。"
                    + $"请添加 Resources/{PortraitResourceRoot}{speaker}，当前使用缺图人物牌。");
            }
            return portrait;
        }

        private static void DrawMissingPortrait(Rect rect, string speaker, float reveal)
        {
            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, 0.94f * reveal);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var nameStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(34),
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.72f * reveal) }
            };
            GUI.Label(new Rect(rect.x + 24f, rect.center.y - 38f, rect.width - 48f, 76f), speaker, nameStyle);
        }

        private static void DrawDialogueBox(
            string speaker,
            string visibleText,
            string fullText,
            bool isTyping,
            bool isNarration,
            bool portraitOnLeft,
            bool isNeon,
            float reveal)
        {
            float boxWidth = Mathf.Min(1180f, Mathf.Max(440f, UIScale.VW - 160f));
            var bodyStyle = new GUIStyle(IMGUIStyles.ModalBody)
            {
                wordWrap = true,
                alignment = isNarration ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft,
                fontSize = IMGUIStyles.FontSize(isNarration ? 20 : 22),
                normal = { textColor = new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, reveal) }
            };
            float textWidth = boxWidth - 72f;
            float textHeight = bodyStyle.CalcHeight(new GUIContent(fullText), textWidth);
            float boxHeight = Mathf.Clamp(textHeight + (isNarration ? 62f : 86f), 148f, Mathf.Min(230f, UIScale.VH * 0.32f));
            // 每句都短促上弹一次以提示文本已更新；人物立绘使用独立计时，不跟着重复淡入。
            float sentenceBounce = (1f - reveal) * 16f - Mathf.Sin(reveal * Mathf.PI) * 5f;
            float boxY = UIScale.VH - boxHeight - 44f + sentenceBounce;
            var box = new Rect((UIScale.VW - boxWidth) / 2f, boxY, boxWidth, boxHeight);

            IMGUIStyles.DrawShadow(box, new Vector2(8f, 10f), 0.64f * reveal);
            GUI.color = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, reveal);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(box, 1.5f, new Color(IMGUIStyles.PaperInk.r, IMGUIStyles.PaperInk.g, IMGUIStyles.PaperInk.b, 0.68f * reveal));

            float textY = box.y + (isNarration ? 28f : 48f);
            GUI.Label(
                new Rect(box.x + 36f, textY, textWidth, box.yMax - textY - 32f),
                visibleText,
                bodyStyle);

            if (!isNarration)
                DrawSpeakerTab(box, speaker, portraitOnLeft, isNeon, reveal);

            var continueStyle = new GUIStyle(IMGUIStyles.StatusLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = IMGUIStyles.FontSize(11),
                normal = { textColor = new Color(IMGUIStyles.PaperTextSecondary.r, IMGUIStyles.PaperTextSecondary.g, IMGUIStyles.PaperTextSecondary.b, 0.72f * reveal) }
            };
            GUI.Label(
                new Rect(box.xMax - 170f, box.yMax - 26f, 138f, 18f),
                isTyping ? "点击显示全文" : "点击继续",
                continueStyle);
        }

        private static void DrawSpeakerTab(Rect box, string speaker, bool onLeft, bool isNeon, float reveal)
        {
            const float tabWidth = 210f;
            const float tabHeight = 42f;
            float x = onLeft ? box.x + 28f : box.xMax - tabWidth - 28f;
            var tab = new Rect(x, box.y - 22f, tabWidth, tabHeight);

            IMGUIStyles.DrawShadow(tab, new Vector2(4f, 5f), 0.46f * reveal);
            GUI.color = new Color(IMGUIStyles.Ink.r, IMGUIStyles.Ink.g, IMGUIStyles.Ink.b, 0.98f * reveal);
            GUI.DrawTexture(tab, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 霓虹说话人的名牌也当灯管处理：同色描边 + 一圈溢光，和立绘是同一盏灯。
            var edge = isNeon ? NeonGlow : IMGUIStyles.Gold;
            if (isNeon)
            {
                float brightness = NeonBrightness(reveal);
                for (int i = 3; i >= 1; i--)
                {
                    var ring = new Rect(tab.x - i * 2f, tab.y - i * 2f, tab.width + i * 4f, tab.height + i * 4f);
                    IMGUIStyles.DrawOutline(ring, 1f, new Color(edge.r, edge.g, edge.b, 0.16f / i * brightness));
                }
                IMGUIStyles.DrawOutline(tab, 1.5f, new Color(edge.r, edge.g, edge.b, 0.92f * brightness));
            }
            else
            {
                IMGUIStyles.DrawOutline(tab, 1.5f, new Color(edge.r, edge.g, edge.b, 0.86f * reveal));
            }

            var nameStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(20),
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, reveal) }
            };
            GUI.Label(tab, speaker, nameStyle);
        }
    }
}
