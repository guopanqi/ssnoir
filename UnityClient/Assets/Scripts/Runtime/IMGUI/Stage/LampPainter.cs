#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI.Stage
{
    // 一盏灯此刻的样子。全是数字，由舞台从模型算出来交给画笔。
    public readonly struct LampLook
    {
        public readonly float Reveal;     // 通电点亮的进度，1 = 已稳定
        public readonly float Level;      // 总亮度：说话/听着 × 闪烁 × 黑场
        public readonly float Glow;       // :light 的档位（faint..surge 之间的插值），决定这是哪一种灯
        public readonly Color Color;      // 这个人的光色
        public readonly bool OnLeft;      // 站位，决定翻不翻转
        public readonly float Wipe;       // 燃的进度，1 = 不在燃
        public readonly float Negative;   // 0 黑夜里的灯，1 纸上的墨
        public readonly float Warmth;     // 0 荧光，1 欠压的钨丝（ember）
        public readonly float Current;    // 电流档：0 不走，1 缓慢游走，2 狂飙
        public readonly float Pop;        // surge 进入瞬间的过冲
        public readonly int DeadStep;     // 闪烁的死拍序号：≥0 时这一拍有一部分管子是灭的；-1 没有

        public LampLook(float reveal, float level, float glow, Color color, bool onLeft, float wipe, float negative,
            float warmth = 0f, float current = 0f, float pop = 0f, int deadStep = -1)
        {
            Reveal = reveal; Level = level; Glow = glow; Color = color; OnLeft = onLeft; Wipe = wipe; Negative = negative;
            Warmth = warmth; Current = current; Pop = pop; DeadStep = deadStep;
        }

        public LampLook WithLevel(float level) => new LampLook(Reveal, level, Glow, Color, OnLeft, Wipe, Negative, Warmth, Current, Pop, DeadStep);
        public LampLook WithReveal(float reveal) => new LampLook(reveal, Level, Glow, Color, OnLeft, Wipe, Negative, Warmth, Current, Pop, DeadStep);
        public LampLook Stable() => new LampLook(1f, Level, Glow, Color, OnLeft, 1f, Negative, Warmth, 0f, 0f);
    }

    // 画一盏霓虹灯——也就是一张立绘——的全部方法。两种介质：
    //   黑夜：灯管发光，有光晕、光池、湿地反光；
    //   纸上（负片）：同一张图染墨画在纸白上，不发光，surge 是墨渗开。
    // 贴图是纯黑底、alpha 从灰度取，所以染成什么颜色就是什么颜色的管子。
    public static class LampPainter
    {
        // 霓虹贴图是方形画布；舞台须保留人物的手势和随身物，不能只取躯干中线。
        // 换新的霓虹立绘若构图不同，只需重调这四个值。
        private const float CropX = 0.20f;
        private const float CropY = 0.02f;
        public const float CropWidth = 0.70f;
        public const float CropHeight = 0.97f;
        private const int WipeStrips = 28;

        // 欠压钨丝的颜色：暗橙，快灭的残烛。
        private static readonly Color EmberTube = new Color(1f, 0.46f, 0.18f);

        public static void Paint(Rect rect, Texture2D portrait, in LampLook look)
        {
            var uv = new Rect(CropX, CropY, CropWidth, CropHeight);
            // 素材统一面向右：左侧人物保持朝内，右侧人物翻转后同样朝内。
            if (!look.OnLeft)
                uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);

            var layers = NeonPortraitLibrary.LayersOf(portrait);
            if (look.Negative >= 1f)
                PaintInk(rect, portrait, layers, uv, look);
            else
                PaintNeon(rect, portrait, layers, uv, look);
            GUI.color = Color.white;
        }

        // 只画管子本体，用于残影。
        public static void PaintTubesOnly(Rect rect, Texture2D portrait, in LampLook look)
        {
            var uv = new Rect(CropX, CropY, CropWidth, CropHeight);
            if (!look.OnLeft)
                uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
            float brightness = Mathf.Clamp01(Brightness(look.Reveal) * look.Level);
            var tube = look.Negative >= 1f ? IMGUIStyles.Ink : Color.white;
            if (look.Negative < 1f) brightness *= 1f - look.Negative;
            DrawTubes(rect, portrait, uv, tube, brightness, look.Negative >= 1f);
            GUI.color = Color.white;
        }

        // ── 黑夜 ──

        private static void PaintNeon(Rect rect, Texture2D portrait, in PortraitLayers layers, Rect uv, in LampLook look)
        {
            float brightness = Mathf.Clamp01(Brightness(look.Reveal) * look.Level) * (1f - look.Negative);
            // 荧光是白管 + 人物自己颜色的点缀管；欠压时两种都退成暗橙的钨丝。
            var tube = Color.Lerp(Color.white, EmberTube, look.Warmth);
            var accent = Color.Lerp(look.Color, EmberTube, look.Warmth);

            // 燃：光晕、光池都跟着已经点亮的比例走，灯管本体按条带从脚往上亮。
            if (look.Wipe < 1f)
            {
                DrawVignette(rect);
                DrawLightPool(rect, brightness * look.Wipe * look.Wipe, look.Color);
                if (layers.Skeleton != null)
                    DrawTubesRelight(rect, portrait, layers, uv, tube, accent, brightness, look.Wipe, new Color(0.004f, 0.007f, 0.016f, 1f), false);
                else
                    DrawTubesWipe(rect, portrait, uv, brightness, look.Wipe, tube);
                return;
            }

            // faint 往下光晕整个收掉，只剩管子；ember 一样没有光晕；surge 往上光晕撑开、加厚，
            // 进入那一瞬还要过冲一下。
            float surge = Mathf.Clamp01((look.Glow - 1f) / (StageState.GlowSurge - 1f));
            float haloAmount = Mathf.Clamp01((look.Glow - StageState.GlowFaint) / (1f - StageState.GlowFaint)) * (1f - look.Warmth);
            float haloSpread = 1f + surge * 1.4f + look.Pop * 0.8f;
            float haloThick = 1f + surge * 0.9f + look.Pop * 1.2f;

            // 人物正后方的暗晕：霓虹的好看全靠亮度对比，背后必须是黑；而暗晕只罩住人物这一圈，
            // 远处的城市原样留着，不会把整块画面关掉。
            DrawVignette(rect);
            // 底部光池：灯管把地面照出一摊光，也把人物和对白框连起来。
            DrawLightPool(rect, brightness * haloAmount * haloThick, look.Color);

            // 外层光晕：同一张图逐层放大、压暗地叠出溢光，代替做不到的加法混合。
            float halo = Brightness(look.Reveal) * look.Level * haloAmount * haloThick;
            for (int i = 3; i >= 1; i--)
            {
                float spread = i * 9f * haloSpread;
                var haloRect = new Rect(rect.x - spread, rect.y - spread, rect.width + spread * 2f, rect.height + spread * 2f);
                GUI.color = new Color(look.Color.r, look.Color.g, look.Color.b, Mathf.Clamp01(0.13f / i * halo));
                GUI.DrawTextureWithTexCoords(haloRect, portrait, uv, true);
            }

            DrawLayeredTubes(rect, portrait, layers, uv, tube, accent, brightness, false);
            DrawReflection(rect, portrait, uv, brightness * haloAmount);
            if (look.Current > 0.01f && layers.Skeleton != null)
                DrawCurrent(rect, layers.Skeleton, uv, look.Current, brightness, Color.Lerp(Color.white, accent, 0.5f));
            if (look.DeadStep >= 0 && layers.Skeleton != null)
                DrawDeadTubes(rect, layers.Skeleton, uv, look.DeadStep, new Color(0.004f, 0.007f, 0.016f, 1f));
        }

        // 接触不良的死拍：按根灭管子。哪几根死由拍号决定，同一拍里稳定、拍与拍之间换，
        // 招牌看上去是一块块地抽搐，而不是整个眨眼。贴图本身不能按根遮，就沿骨架用背景色把管子盖掉；
        // 光晕是整张图放大叠出来的，盖不掉，正好像灭了的管子还残着一点余光。
        private static void DrawDeadTubes(Rect rect, PortraitSkeleton skeleton, Rect uv, int step, Color background)
        {
            float width = rect.width * 0.017f;
            for (int i = 0; i < skeleton.Paths.Count; i++)
            {
                if (Hash(step * 131 + i * 7) > StageState.FlickerDeadShare) continue;
                var pts = skeleton.Paths[i].Points;
                bool hasPrevious = ToScreen(rect, uv, pts[0], out var previous);
                for (int k = 1; k < pts.Length; k++)
                {
                    bool visible = ToScreen(rect, uv, pts[k], out var screen);
                    if (visible && hasPrevious)
                        IMGUIStyles.DrawLine(previous, screen, background, width);
                    previous = screen; hasPrevious = visible;
                }
            }
            GUI.color = Color.white;
        }

        private static float Hash(int n)
        {
            float v = Mathf.Sin(n * 12.9898f + 78.233f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        // 分层画管子：有 _lines/_accent 时线稿染 tube 色、点缀蒙版染 accent 色（标志色可随剧情变）；
        // 没跑过加工的图退回整张原图。
        private static void DrawLayeredTubes(Rect rect, Texture2D portrait, in PortraitLayers layers, Rect uv,
            Color tube, Color accent, float alpha, bool bold)
        {
            if (!layers.HasLayers)
            {
                DrawTubes(rect, portrait, uv, tube, alpha, bold);
                return;
            }
            DrawTubes(rect, layers.Lines!, uv, tube, alpha, bold);
            DrawTubes(rect, layers.Accent!, uv, accent, alpha, bold);
        }

        // 电流沿管子游走：几道"彗星"沿骨架巡回往前跑——头是一点亮，尾巴是身后一小段被点亮的管子。
        // 不是小球：亮的是管子本身那一段。pulse 是慢而少的呼吸，racing 是快而多的狂飙；档位之间连续插值。
        // 电流认管子：尾巴只在自己这根管子里，头跑到管口时在电极上炸一下白光，然后从下一根的管口进去。
        private static void DrawCurrent(Rect rect, PortraitSkeleton skeleton, Rect uv, float current, float brightness, Color color)
        {
            if (skeleton.TotalLength <= 0f) return;
            float t = Mathf.Clamp01(current - 1f);           // 0 = pulse, 1 = racing
            float speed = Mathf.Lerp(StageState.PulseSpeed, StageState.RacingSpeed, t) * skeleton.TotalLength;
            int comets = Mathf.RoundToInt(Mathf.Lerp(StageState.PulseSparks, StageState.RacingSparks, t));
            float tail = Mathf.Lerp(StageState.PulseTail, StageState.RacingTail, t);
            float fade = Mathf.Clamp01(current);              // 0→1 淡入
            float spacing = skeleton.TotalLength / comets;
            float head = Time.unscaledTime * speed;
            const int TailSamples = 14;
            const float ElectrodeReach = 0.02f;              // 离管口这么近（uv）算到了电极
            float width = Mathf.Max(2f, rect.width * 0.012f);
            var dot = NeonPortraitLibrary.RadialFalloff();

            for (int k = 0; k < comets; k++)
            {
                float d = head + k * spacing;
                if (!skeleton.Locate(d, out int tube, out float local)) continue;
                var path = skeleton.Paths[tube];
                // 尾巴不越过管口：只回溯到这根管子的头。
                float reach = Mathf.Min(tail, local);
                Vector2 previous = default;
                bool hasPrevious = false;
                for (int i = 0; i < TailSamples; i++)
                {
                    float back = i / (float)(TailSamples - 1);
                    var p = path.At(local - back * reach);
                    if (!ToScreen(rect, uv, p, out var screen)) { hasPrevious = false; continue; }
                    if (hasPrevious)
                    {
                        float a = (1f - back) * (1f - back) * brightness * fade;
                        IMGUIStyles.DrawLine(previous, screen, new Color(color.r, color.g, color.b, 0.35f * a), width * 3f);
                        IMGUIStyles.DrawLine(previous, screen, new Color(1f, 1f, 1f, 0.9f * a), width);
                    }
                    previous = screen;
                    hasPrevious = true;
                    if (i == 0)
                    {
                        float size = width * 2.2f;
                        float toEnd = Mathf.Min(local, path.Length - local);
                        if (toEnd < ElectrodeReach)
                        {
                            // 电极上那一下：更大、更白，靠得越近越炸。
                            float flash = 1f - toEnd / ElectrodeReach;
                            size *= 1f + 1.6f * flash;
                        }
                        GUI.color = new Color(1f, 1f, 1f, 0.95f * brightness * fade);
                        GUI.DrawTexture(new Rect(screen.x - size, screen.y - size, size * 2f, size * 2f), dot);
                    }
                }
            }
            GUI.color = Color.white;
        }

        // 骨架 uv → 裁切后的画面坐标（右侧人物是翻转的）。裁切框外的点不画。
        private static bool ToScreen(Rect rect, Rect uv, Vector2 p, out Vector2 screen)
        {
            float u = (p.x - CropX) / CropWidth;
            float v = (p.y - CropY) / CropHeight;
            screen = default;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return false;
            if (uv.width < 0f) u = 1f - u;
            screen = new Vector2(rect.x + u * rect.width, rect.yMax - v * rect.height);
            return true;
        }

        // 电流感：低频呼吸 + 偶发跳闸；点亮瞬间先抖两下再稳定。
        public static float Brightness(float reveal)
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

        private static void DrawVignette(Rect rect)
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

        private static void DrawLightPool(Rect rect, float brightness, Color color)
        {
            // 脚下一摊光：同一张径向渐变压扁成椭圆，一笔画完，不再有横条阶梯。
            var pool = new Rect(
                rect.center.x - rect.width * 1.15f,
                rect.yMax - rect.height * 0.20f,
                rect.width * 2.3f,
                rect.height * 0.44f);
            GUI.color = new Color(color.r, color.g, color.b, 0.16f * brightness);
            GUI.DrawTexture(pool, NeonPortraitLibrary.RadialFalloff());
        }

        // 湿地面上的倒影：整块竖直翻转画一次，再盖一层竖直渐变把它抹进地面。
        private static void DrawReflection(Rect rect, Texture2D portrait, Rect uv, float brightness)
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

        // ── 纸上 ──

        // 灯的语法在纸上换一套物理：surge 不是发光而是墨渗开——线更重、周围一圈灰晕；
        // faint 是墨淡了；flicker 是线断掉露出纸；blackout 是整页空白。没有光池和反光，纸不反光。
        private static void PaintInk(Rect rect, Texture2D portrait, in PortraitLayers layers, Rect uv, in LampLook look)
        {
            var ink = IMGUIStyles.Ink;
            float brightness = Mathf.Clamp01(Brightness(look.Reveal) * look.Level);
            float surge = Mathf.Clamp01((look.Glow - 1f) / (StageState.GlowSurge - 1f));
            float weight = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01((look.Glow - StageState.GlowFaint) / (1f - StageState.GlowFaint)));

            if (look.Wipe < 1f)
            {
                if (layers.Skeleton != null)
                    DrawTubesRelight(rect, portrait, layers, uv, ink, ink, brightness * weight, look.Wipe, IMGUIStyles.Paper, true);
                else
                    DrawTubesWipe(rect, portrait, uv, brightness * weight, look.Wipe, ink);
                return;
            }

            if (surge > 0f)
            {
                for (int i = 3; i >= 1; i--)
                {
                    float spread = i * 7f * (1f + surge);
                    var bleed = new Rect(rect.x - spread, rect.y - spread, rect.width + spread * 2f, rect.height + spread * 2f);
                    GUI.color = new Color(ink.r, ink.g, ink.b, 0.10f / i * surge * brightness);
                    GUI.DrawTextureWithTexCoords(bleed, portrait, uv, true);
                }
            }
            DrawTubes(rect, portrait, uv, ink, brightness * weight, true);
            if (look.Current > 0.01f && layers.Skeleton != null)
                DrawCurrent(rect, layers.Skeleton, uv, look.Current, brightness, ink);
            if (look.DeadStep >= 0 && layers.Skeleton != null)
                DrawDeadTubes(rect, layers.Skeleton, uv, look.DeadStep, IMGUIStyles.Paper);
        }

        // ── 共用 ──

        // 灯管本体。黑夜里叠两遍让细线压住背景；纸上再往四个方向各挪一像素多画几遍——
        // 白纸上的线要有分量，一根细线在纸上只是铅笔稿。
        private static void DrawTubes(Rect rect, Texture2D portrait, Rect uv, Color tube, float alpha, bool bold)
        {
            GUI.color = new Color(tube.r, tube.g, tube.b, alpha);
            GUI.DrawTextureWithTexCoords(rect, portrait, uv, true);
            GUI.color = new Color(tube.r, tube.g, tube.b, 0.55f * alpha);
            GUI.DrawTextureWithTexCoords(rect, portrait, uv, true);
            if (!bold) return;
            const float thick = 1.3f;
            GUI.color = new Color(tube.r, tube.g, tube.b, 0.85f * alpha);
            GUI.DrawTextureWithTexCoords(new Rect(rect.x + thick, rect.y, rect.width, rect.height), portrait, uv, true);
            GUI.DrawTextureWithTexCoords(new Rect(rect.x - thick, rect.y, rect.width, rect.height), portrait, uv, true);
            GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y + thick, rect.width, rect.height), portrait, uv, true);
            GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y - thick, rect.width, rect.height), portrait, uv, true);
        }

        // 一格一格通电：把立绘切成横向条带，从脚到头依次点亮，最前面那几条还在抖。
        // 不需要拆图——只是同一张贴图的 uv 子矩形。
        // 燃，按根：电从脚下的接线口进来，沿线路一根管子一根管子点过去。整张图先画好，还没通电的管子
        // 沿骨架用背景色盖住；刚通电的那根先抖两下，再过冲一道白，然后稳住。
        // 顺序是离线排好的巡回（空间相邻的管子挨着），只把起点转到管头最低的那根。
        private const float RelightRamp = 0.22f;   // 每根管子从通电到稳定占整个燃的比例

        private static void DrawTubesRelight(Rect rect, Texture2D portrait, in PortraitLayers layers, Rect uv,
            Color tube, Color accent, float brightness, float wipe, Color background, bool bold)
        {
            var skeleton = layers.Skeleton!;
            int n = skeleton.Paths.Count;
            if (n == 0) { DrawTubesWipe(rect, portrait, uv, brightness, wipe, tube); return; }
            DrawLayeredTubes(rect, portrait, layers, uv, tube, accent, brightness, bold);

            int first = 0;
            float lowest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float v = skeleton.Paths[i].Points[0].y;
                if (v < lowest) { lowest = v; first = i; }
            }
            float width = rect.width * 0.017f;
            for (int k = 0; k < n; k++)
            {
                int i = (first + k) % n;
                float ignite = k / (float)n * (1f - RelightRamp);
                float age = (wipe - ignite) / RelightRamp;
                if (age >= 1f) continue;
                float cover;
                Color flash = default;
                if (age <= 0f) cover = 1f;
                else
                {
                    // 抖两下：前半段有一半时间是灭的；过冲随年龄衰减。
                    cover = age < 0.5f && Mathf.Sin(age * 40f) < 0f ? 1f : 0f;
                    flash = new Color(1f, 1f, 1f, 0.8f * (1f - age) * brightness);
                    if (bold) flash = new Color(tube.r, tube.g, tube.b, 0.5f * (1f - age));
                }
                var pts = skeleton.Paths[i].Points;
                bool hasPrevious = ToScreen(rect, uv, pts[0], out var previous);
                for (int m = 1; m < pts.Length; m++)
                {
                    bool visible = ToScreen(rect, uv, pts[m], out var screen);
                    if (visible && hasPrevious)
                    {
                        if (cover > 0f) IMGUIStyles.DrawLine(previous, screen, background, width);
                        else if (flash.a > 0.01f) IMGUIStyles.DrawLine(previous, screen, flash, width * 0.5f);
                    }
                    previous = screen; hasPrevious = visible;
                }
            }
            GUI.color = Color.white;
        }

        // 没跑过加工、没有骨架的图：退回按条带从脚往上亮。
        private static void DrawTubesWipe(Rect rect, Texture2D portrait, Rect uv, float brightness, float wipe, Color tube)
        {
            float stripHeight = rect.height / WipeStrips;
            float uvStrip = uv.height / WipeStrips;
            // 条带从底部数起：第 0 条是脚。
            for (int i = 0; i < WipeStrips; i++)
            {
                float threshold = (i + 1f) / WipeStrips;
                if (threshold > wipe + 0.08f) break;
                // 刚点亮的那几条先跳两下再稳住。
                float age = Mathf.Clamp01((wipe - threshold + 0.08f) / 0.16f);
                float flick = age < 1f && Mathf.Sin(age * 40f) < 0f ? 0.35f : 1f;
                float alpha = brightness * Mathf.Clamp01(age * 2f) * flick;
                var strip = new Rect(rect.x, rect.yMax - (i + 1) * stripHeight, rect.width, stripHeight);
                var stripUv = new Rect(uv.x, uv.y + i * uvStrip, uv.width, uvStrip);
                GUI.color = new Color(tube.r, tube.g, tube.b, alpha);
                GUI.DrawTextureWithTexCoords(strip, portrait, stripUv, true);
                GUI.color = new Color(tube.r, tube.g, tube.b, 0.55f * alpha);
                GUI.DrawTextureWithTexCoords(strip, portrait, stripUv, true);
            }
        }
    }
}
