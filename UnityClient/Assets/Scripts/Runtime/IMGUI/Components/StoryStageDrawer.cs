#nullable enable
using System.Collections.Generic;
using System.Linq;
using SSNoir.Core;
using SSNoir.IMGUI.Stage;
using UnityEngine;

namespace SSNoir.IMGUI
{
    // 故事舞台画面层：play-dialogue! 的对白与 play-stage! 的无字动作共用立绘和灯。
    //
    // 分层（都在 IMGUI/Stage/）：
    //   StageState         模型——台上有谁、每个人的终态、画面状态；只算数，不画；时间由外面注入
    //   LampPainter        画一盏灯：黑夜里的霓虹 / 纸上的墨，两种介质
    //   StageScreenPainter 画面层：环境遮罩、负片（世界那幅画交给后处理翻）、色光、闪光
    //   DialogueBoxDrawer  字的那一层：对白框、名牌、缺图牌
    // 这里只做编排：接台词、更新模型、按站位排版、依次调各层画笔。
    //
    // 舞台指示（DialogueStageCue）改的都是人物或画面的终态——换哪组灯管（姿势）、亮到几档、
    // 逼近中间还是退开、画面翻不翻；渲染只负责从上一个终态过渡过去，跳过动画也是同一幅画面。
    public static class StoryStageDrawer
    {
        private const string NarratorSpeaker = "世界";
        private const float TypewriterCharactersPerSecond = 30f;

        // 每个人的灯就是他画里的颜色：立绘上点缀色那几根管子的颜色（离线加工时算出的默认值），
        // 白管人物一律是冷白——尼尔的「灼」就是白炽，侦探的情绪就该是这个颜色。
        // 剧情可以改标志色（engine.scm 的 set-portrait-accent!，存在全局 "立绘色/<人>"），
        // 由外面把查询函数接进来；舞台在几秒里把颜色过渡过去。
        private static readonly Color DefaultGlow = new Color(0.82f, 0.88f, 1f);
        public static System.Func<string, string?>? AccentOverride { get; set; }

        private static Color AccentTargetOf(StageActor actor)
        {
            var hex = AccentOverride?.Invoke(actor.Name);
            if (hex != null && ColorUtility.TryParseHtmlString(hex, out var overridden))
                return overridden;
            var skeleton = actor.Texture != null ? NeonPortraitLibrary.LayersOf(actor.Texture).Skeleton : null;
            return skeleton?.DefaultAccent ?? DefaultGlow;
        }

        private static readonly StageState State = new();
        private static Texture2D? _burstTexture;
        private static Texture2D? _hitTexture;
        private static readonly HashSet<string> MissingPortraitWarnings = new();
        private static string _visibleLineKey = string.Empty;
        private static float _lineStartedAt;
        private static int _currentLineLength;
        private static bool _currentLineCompletedInstantly;

        public static bool IsCurrentLineFullyRevealed =>
            _currentLineCompletedInstantly || VisibleCharacterCount() >= _currentLineLength;

        public static void CompleteCurrentLine() => _currentLineCompletedInstantly = true;

        public static void BeginConversation()
        {
            _visibleLineKey = string.Empty;
            _lineStartedAt = 0f;
            _currentLineLength = 0;
            _currentLineCompletedInstantly = false;
            State.Reset();
            StageScreenPainter.Release();
        }

        public static void StageSpawn(string id, string asset, float x, float y, string layer)
            => State.Spawn(id, asset, x, y, layer, Time.unscaledTime, NeonPortraitLibrary.Load);

        public static void StageProp(string id, string asset, float x, float y, string layer)
            => State.SpawnProp(id, asset, x, y, layer, Time.unscaledTime);

        public static void StagePropAt(string id, string asset, string anchor, float dx, float dy, string layer)
            => State.SpawnPropAt(id, asset, anchor, dx, dy, layer, Time.unscaledTime);

        public static void StageMove(string id, float x, float y, float seconds)
            => State.Move(id, x, y, seconds, Time.unscaledTime);

        public static void StagePath(string id, IReadOnlyList<StagePoint> points, float seconds, bool relative)
            => State.Path(id, seconds, points, relative, Time.unscaledTime);

        public static void StageEffect(string id, string effect, float dx, float dy)
            => State.Effect(id, effect, dx, dy, Time.unscaledTime);

        public static void StageRemove(string id) => State.Remove(id);

        public static void StagePose(string id, string pose)
            => State.Pose(id, pose, Time.unscaledTime,
                (asset, p) => p == StageState.BasePose ? NeonPortraitLibrary.Load(asset) : NeonPortraitLibrary.LoadPose(asset, p));

        public static void StageLight(string id, string light)
            => State.Light(id, light, Time.unscaledTime);

        // Say 拍开始时就建立打字机状态；输入可能先于下一次 OnGUI 绘制到达。
        public static void BeginStageLine(DialogueLine line, int beatIndex)
        {
            _visibleLineKey = beatIndex + "\n" + line.Speaker + "\n" + line.Text;
            _lineStartedAt = Time.unscaledTime;
            _currentLineLength = line.Text.Length;
            _currentLineCompletedInstantly = false;
        }

        // 舞台动作可以没有台词。只有 Say 拍才绘制对白框和打字机。
        public static void DrawStoryStageFrame(DialogueLine? line, int beatIndex)
        {
            float now = Time.unscaledTime;
            string speaker = line?.Speaker ?? string.Empty;
            string content = line?.Text ?? string.Empty;
            string key = beatIndex + "\n" + speaker + "\n" + content;
            if (_visibleLineKey != key)
            {
                _visibleLineKey = key;
                _lineStartedAt = now;
                _currentLineLength = content.Length;
                _currentLineCompletedInstantly = false;
            }
            float negative = State.NegativeAmount(now);
            StageScreenPainter.PaintBackdrop(negative);
            DrawActors(speaker, negative, now);
            DrawPropsAndEffects(now);
            if (line != null)
            {
                int count = _currentLineCompletedInstantly ? content.Length : Mathf.Min(content.Length, VisibleCharacterCount());
                string visible = count >= content.Length ? content : content.Substring(0, count);
                bool neon = State.TryGetActor(speaker, out var actor) && !actor.Missing;
                var glow = neon ? actor!.CurrentAccent(now) : DefaultGlow;
                DialogueBoxDrawer.Draw(speaker, visible, content, count < content.Length, false, false,
                    speaker == StageState.Protagonist, neon, 1f, negative, glow);
            }
            StageScreenPainter.PaintFlash(State.FlashAlpha(now), negative);
        }

        // 每帧没有对话时调一次，幂等：把借给舞台的画面状态（世界负片）还回去。
        public static void EndConversation()
        {
            StageScreenPainter.Release();
        }

        // 返回 true：普通 play-dialogue! 的说话人此刻不在场，画面仍使用对白舞台，但调用方应报警。
        // 显式 play-remote-dialogue! 允许同一舞台承接场外人物，不视为降级。
        public static bool DrawConversationLine(
            string speaker,
            string text,
            int lineIndex,
            DialogueAnchors anchors,
            bool allowsRemoteParticipant,
            DialogueStageCue? stage = null)
        {
            float now = Time.unscaledTime;
            bool isNarration = speaker == NarratorSpeaker;
            bool usedRemoteFallback = !isNarration
                && !anchors.TryResolve(speaker, out _)
                && !allowsRemoteParticipant;

            // 行号保证连续两句内容完全相同时仍被视为两句，各自触发弹跳和打字机。
            string lineKey = lineIndex + "\n" + speaker + "\n" + text;
            var cue = stage ?? DialogueStageCue.None;
            if (_visibleLineKey != lineKey)
            {
                _visibleLineKey = lineKey;
                _lineStartedAt = now;
                _currentLineLength = text.Length;
                _currentLineCompletedInstantly = false;
                State.ApplyLine(speaker, isNarration, cue, now,
                    NeonPortraitLibrary.Load, NeonPortraitLibrary.LoadPose, WarnMissing);
            }

            float lineReveal = Mathf.Clamp01((now - _lineStartedAt) / StageState.EnterDuration);
            float lineEase = 1f - Mathf.Pow(1f - lineReveal, 3f);
            int visibleCharacterCount = _currentLineCompletedInstantly
                ? text.Length
                : Mathf.Min(text.Length, VisibleCharacterCount());
            string visibleText = visibleCharacterCount >= text.Length
                ? text
                : text.Substring(0, visibleCharacterCount);
            bool isTyping = visibleCharacterCount < text.Length;

            float negative = State.NegativeAmount(now);
            StageScreenPainter.PaintBackdrop(negative);
            DrawActors(isNarration ? string.Empty : speaker, negative, now);
            StageActor? speakerActor = null;
            bool speakerIsNeon = !isNarration && State.TryGetActor(speaker, out speakerActor) && !speakerActor.Missing;
            var speakerGlow = speakerIsNeon && speakerActor != null ? speakerActor.CurrentAccent(now) : DefaultGlow;
            DialogueBoxDrawer.Draw(
                speaker, visibleText, text, isTyping, isNarration, cue.Inner,
                speaker == StageState.Protagonist, speakerIsNeon, lineEase, negative, speakerGlow);
            StageScreenPainter.PaintFlash(State.FlashAlpha(now), negative);
            return usedRemoteFallback;
        }

        private static int VisibleCharacterCount()
        {
            float elapsed = Mathf.Max(0f, Time.unscaledTime - _lineStartedAt);
            return Mathf.CeilToInt(elapsed * TypewriterCharactersPerSecond);
        }

        private static void WarnMissing(string key)
        {
            if (MissingPortraitWarnings.Add(key))
                Debug.LogWarning($"[SSNoir] play-dialogue! '{key}' 没有立绘。请添加 Resources/Portraits/Neon/{key}。");
        }

        // 排版：立绘从靠上的地方立起来，占满对白框以上的全部空间——对白舞台上人是主角，框只是
        // 他说的话。上下限按屏高取比例：写死的 360 在手机的画布里既可能顶穿、也可能把人压成一小条。
        // 霓虹是一整块封闭灯管图形，切半身等于把灯管掐断，因此用整幅招牌的窄长比例。
        private static Rect PortraitFrame(StageActor actor, float now, out float restingX)
        {
            float dialogueTop = UIScale.VH - Mathf.Min(230f, UIScale.VH * DialogueBoxDrawer.BoxHeightRatio) - DialogueBoxDrawer.BoxBottomMargin;
            const float portraitTop = 28f;
            float height = Mathf.Clamp(dialogueTop + 42f - portraitTop, UIScale.VH * 0.55f, UIScale.VH * 0.92f);
            float width = height * (LampPainter.CropWidth / LampPainter.CropHeight);
            restingX = actor.OnLeft ? 64f : UIScale.VW - width - 64f;
            float towardCenter = actor.OnLeft ? 1f : -1f;
            float x = restingX + actor.CurrentOffset(now) * towardCenter + StageState.ShakeOffset(actor, now);
            if (actor.UsesStageX)
                x = UIScale.VW * (0.5f + actor.CurrentStageX(now) / 20f) - width / 2f;
            float layerY = actor.StageLayer == "back" ? -20f : actor.StageLayer == "front" ? 16f : 0f;
            return new Rect(x, portraitTop + layerY - actor.CurrentStageY(now) * UIScale.VH / 20f, width, height);
        }

        private static Vector2 StagePixel(float x, float y)
        {
            float dialogueTop = UIScale.VH - Mathf.Min(230f, UIScale.VH * DialogueBoxDrawer.BoxHeightRatio)
                - DialogueBoxDrawer.BoxBottomMargin;
            return new Vector2(UIScale.VW * (0.5f + x / 20f), dialogueTop - UIScale.VH * (0.06f + y / 20f));
        }

        private static void DrawPropsAndEffects(float now)
        {
            var oldColor = GUI.color;
            foreach (var prop in State.Props.OrderBy(p => p.Layer == "back" ? 0 : p.Layer == "front" ? 2 : 1))
            {
                var center = StagePixel(prop.X(now), prop.Y(now));
                float height = UIScale.VH * 0.22f;
                float width = height * prop.Texture.width / prop.Texture.height;
                GUI.DrawTexture(new Rect(center.x - width / 2f, center.y - height / 2f, width, height), prop.Texture, ScaleMode.ScaleToFit, true);
            }
            foreach (var effect in State.Effects)
            {
                float progress = (now - effect.StartedAt) / SSNoir.IMGUI.Stage.StageEffect.Duration;
                if (progress < 0f || progress >= 1f) continue;
                var center = StagePixel(effect.X, effect.Y);
                bool hit = effect.Name == "受击闪光";
                var texture = hit
                    ? _hitTexture ??= BuildBurstTexture(true)
                    : _burstTexture ??= BuildBurstTexture(false);
                float size = UIScale.VH * (0.13f + progress * 0.05f);
                GUI.color = hit
                    ? new Color(1f, 1f, 1f, 1f - progress)
                    : new Color(1f, 0.9f, 0.72f, 1f - progress);
                GUI.DrawTexture(new Rect(center.x - size / 2f, center.y - size / 2f, size, size), texture);
            }
            GUI.color = oldColor;
        }

        private static Texture2D BuildBurstTexture(bool hit)
        {
            const int side = 128;
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[side * side];
            for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                float px = (x + 0.5f - side / 2f) / (side / 2f);
                float py = (y + 0.5f - side / 2f) / (side / 2f);
                float radius = Mathf.Sqrt(px * px + py * py);
                float alpha = hit ? Mathf.Clamp01((0.22f - radius) / 0.14f) : 0f;
                for (int ray = 0; ray < 8; ray++)
                {
                    float angle = ray * Mathf.PI / 4f;
                    float along = px * Mathf.Cos(angle) + py * Mathf.Sin(angle);
                    float across = Mathf.Abs(px * Mathf.Sin(angle) - py * Mathf.Cos(angle));
                    if (along < 0.23f || along > 0.85f) continue;
                    alpha = Mathf.Max(alpha, Mathf.Clamp01((0.035f - across) / 0.018f)
                        * Mathf.Clamp01((0.85f - along) / 0.12f));
                }
                pixels[y * side + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DrawActors(string speaker, float negative, float now)
        {
            float blackout = State.BlackoutLevel(now);
            // 色光压制：说话人灼起来，听者被压得更暗。
            float dominance = 0f;
            if (State.TryGetActor(speaker, out var speakerActor))
                dominance = Mathf.Clamp01((speakerActor.CurrentGlow(now) - 1f) / (StageState.GlowSurge - 1f));
            // 先画听者再画说话人：逼近到中间时说话人压在上面。
            for (int pass = 0; pass < 2; pass++)
            {
                bool drawSpeaker = pass == 1;
                foreach (var actor in State.Actors.OrderBy(a => a.StageLayer == "back" ? 0 : a.StageLayer == "front" ? 2 : 1))
                {
                    bool isSpeaker = actor.Name == speaker;
                    if (isSpeaker != drawSpeaker) continue;

                    var rect = PortraitFrame(actor, now, out float restingX);
                    float enter = StageState.EaseOut(actor.EnteredAt, StageState.EnterDuration, now);
                    if (actor.Missing)
                    {
                        DialogueBoxDrawer.DrawMissingPortrait(
                            new Rect(restingX, rect.y, rect.height * 0.68f, rect.height), actor.Name, enter);
                        continue;
                    }

                    actor.TargetAccent(AccentTargetOf(actor), now);
                    float glow = actor.CurrentGlow(now);
                    float flicker = StageState.FlickerLevel(actor, now);
                    // 死拍：声音里是整盏灯死掉，画面上是大半管子灭、幸存的几根还亮着——所以整体只压到幸存档。
                    int deadStep = flicker <= StageState.FlickerDeadLevel ? StageState.FlickerStep(actor, now) : -1;
                    float lampFlicker = deadStep >= 0 ? StageState.FlickerSurvivorLevel : flicker;
                    float listener = StageState.ListenerLevel * (1f - StageState.DominanceDim * dominance);
                    float level = (speaker.Length == 0 || isSpeaker ? 1f : listener) * glow * lampFlicker * blackout;
                    var color = actor.CurrentAccent(now);

                    // 灯打到底时，这个人的颜色洗满半个舞台——是他这句话在占着这个空间。听者的灯再亮也洗不出去。
                    if (isSpeaker)
                    {
                        float surge = Mathf.Clamp01((glow - 1f) / (StageState.GlowSurge - 1f)) * flicker;
                        StageScreenPainter.PaintWash(rect, color, surge, negative);
                    }

                    var look = new LampLook(enter, level, glow, color, actor.OnLeft, StageState.RelightProgress(actor, now), negative,
                        actor.CurrentWarmth(now), actor.CurrentCurrent(now), isSpeaker ? actor.SurgePop(now) : 0f, deadStep);

                    // 换姿势＝旧灯管灭、新灯管通电点亮。两组管子短暂同时半亮，像招牌切换时那一下重影。
                    float swap = actor.PoseSwap(now);
                    if (actor.PreviousTexture != null)
                    {
                        LampPainter.Paint(rect, actor.PreviousTexture, look.Stable().WithLevel(level * (1f - swap)));
                        look = look.WithReveal(swap);
                    }

                    // 一震时留两道残影：灯管抖动在视网膜上的重影，比单纯位移更像一声吼。
                    float ghost = StageState.ShakeGhost(actor, now);
                    if (ghost > 0f)
                    {
                        for (int g = 1; g <= 2; g++)
                        {
                            float dx = g * 10f * ghost * (actor.OnLeft ? -1f : 1f);
                            LampPainter.PaintTubesOnly(
                                new Rect(rect.x + dx, rect.y, rect.width, rect.height),
                                actor.Texture!, look.Stable().WithLevel(level * 0.35f * ghost / g));
                        }
                    }
                    LampPainter.Paint(rect, actor.Texture!, look);
                }
            }
        }
    }
}
