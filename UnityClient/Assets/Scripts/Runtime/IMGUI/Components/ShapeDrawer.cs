#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// GUI 空间里的几何图形：饼形时钟徽章、气泡的小尾巴。
    ///
    /// <b>它为什么曾经反复出问题（别再改回去）</b>
    ///
    /// 这个饼原来是用 GL 直接画的：GL.LoadPixelMatrix 走的是屏幕像素，绕开 GUI 的一切
    /// 变换。为了补回来，它自己调 GUIUtility.GUIToScreenPoint + UIScale.ScaleSize 把坐标
    /// 换算过去——也就是说，界面里每多一层 GUI 变换，这里就得多补一次，而补不补、补得对
    /// 不对，全靠写这一处的人记得。于是同一枚徽章在卡片上是对的，进了网格视口偏一次，
    /// 进了滚动视图又偏一次（滚动条一动它还不跟着走），叠在别的元素上面也不会被裁掉——
    /// 因为 GL 根本不认 GUI 的裁剪栈。
    ///
    /// 根治的办法不是再补一次换算，是<b>不再用 GL</b>：把扇形和圆环各烘成一张纯白 + alpha
    /// 的小贴图，用 GUI.DrawTexture 画。这样它和界面里其他所有东西走的是同一条路——
    /// 同一套矩阵、同一个分组偏移、同一个裁剪栈、同一个绘制顺序——不需要任何特例。
    /// 以后无论把徽章放进滚动视图、分组、模态面板还是别的什么，它都自动是对的。
    ///
    /// 贴图只按「扫过多少度」缓存（整度，最多 361 张 64×64），颜色由 GUI.color 上色，
    /// 所以尺寸和配色的变化都不产生新贴图。
    ///
    /// <b>要在这套界面里画任何非矩形的东西，加在这里，不要再开一处 GL。</b>
    /// </summary>
    public static class ShapeDrawer
    {
        // 源图分辨率。徽章实际显示只有十几到二十几个物理像素，64 足够，缩下去还顺带
        // 得到一层双线性抗锯齿——这正是以前 GL 画线画不出的那点圆润。
        private const int SourceSize = 64;
        private const int Supersample = 3;      // 每像素 3×3 采样：细环的边缘 2×2 还是会发毛
        private const float RingThickness = 3f; // 源图像素；缩到显示尺寸约合 1 px

        private static readonly Dictionary<int, Texture2D> _sectors = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<int, Texture2D> _arcs = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<int, Texture2D> _octagons = new Dictionary<int, Texture2D>();
        private static Texture2D? _ring;
        private static Texture2D? _triangle;

        public static void DrawPie(Rect rect, float fillPercent, Color fillColor, Color outlineColor)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (fillPercent <= 0f) return;
            DrawSector(rect, fillPercent, fillColor);
            DrawRing(rect, outlineColor);
        }

        public static void DrawPieBadge(Rect rect, float fillPercent, Color fillColor, Color outlineColor)
        {
            if (Event.current.type != EventType.Repaint) return;
            DrawRing(rect, outlineColor);
            if (fillPercent > 0f)
                DrawSector(rect, fillPercent, fillColor);
        }

        // ── 倒计时表盘 ───────────────────────────────────────────────────
        //
        // 空心圆环，中心留给剩余数字（数字由调用方画）。这是倒计时**唯一**的形状：
        // 圆＝时间在走，条＝我在推进，玩家不读标签就能分开两者。
        //
        // 三条刻意的选择：
        //   · 亮着的是**剩余量**，从十二点顺时针一格格熄下去。Blades 的钟是填满，
        //     我们反着来——填满和倒计时如果动作方向一样，形状上的区分就被抵消了。
        //   · 上限 ≤ DialMaxSegments 才分段。再多的格子在手机上糊成一圈毛边，
        //     读不出格数，那就交给中心的数字，环只表达「还剩多少比例」。
        //   · 环要**细**。第一版内径 0.54，环占了半径的一半；满格时整枚就是一块金色
        //     圆饼，中心那点空隙又被数字填死——读起来是一枚徽章，不是一只表。
        //     0.72 之后剩下的是一圈线，数字浮在中间的空里，这才有仪表的样子。

        public const int DialMaxSegments = 6;
        private const float DialInnerRatio = 0.72f;   // 中心留空的半径比例
        private const float DialSegmentGapDegrees = 13f;
        private const float GlowInflate = 1.6f;       // 亮段外扩这么多像素做一层弱光晕

        /// <summary>倒计时表盘：亮着的扇区＝还剩多少。</summary>
        public static void DrawDial(Rect rect, int current, int max, Color activeColor, Color trackColor)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (max <= 0 || rect.width < 4f) return;
            current = Mathf.Clamp(current, 0, max);

            var glow = new Color(activeColor.r, activeColor.g, activeColor.b, activeColor.a * 0.28f);
            var glowRect = new Rect(rect.x - GlowInflate, rect.y - GlowInflate,
                rect.width + GlowInflate * 2f, rect.height + GlowInflate * 2f);

            if (max <= DialMaxSegments)
            {
                float per = 360f / max;
                float gap = Mathf.Min(DialSegmentGapDegrees, per * 0.28f);
                for (int i = 0; i < max; i++)
                {
                    float start = i * per + gap * 0.5f;
                    float sweep = per - gap;
                    if (i < current)
                    {
                        DrawArc(glowRect, start, sweep, glow);
                        DrawArc(rect, start, sweep, activeColor);
                    }
                    else
                    {
                        DrawArc(rect, start, sweep, trackColor);
                    }
                }
                return;
            }

            DrawArc(rect, 0f, 360f, trackColor);
            if (current > 0)
            {
                float sweep = 360f * current / max;
                DrawArc(glowRect, 0f, sweep, glow);
                DrawArc(rect, 0f, sweep, activeColor);
            }
        }

        /// <summary>实心圆。表盘底下垫一层，让中心的数字压在城市上也读得出来。</summary>
        public static void DrawDisc(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            DrawSector(rect, 1f, color);
        }

        // 十二点起，顺时针扫 sweep 度的一段圆环。
        private static void DrawArc(Rect rect, float startDegrees, float sweepDegrees, Color color)
        {
            int start = ((Mathf.RoundToInt(startDegrees) % 360) + 360) % 360;
            int sweep = Mathf.Clamp(Mathf.RoundToInt(sweepDegrees), 0, 360);
            if (sweep <= 0) return;

            var tex = ArcTexture(start, sweep);
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, tex);
            GUI.color = prev;
        }

        // 八角（切掉四角的方）：消耗槽的形状，见 ActionNodeDrawer 的槽位注释。
        // 切角比例。0.44 试过，太狠了——看着是一枚八角路牌，抢在内容前面。
        // 0.30 是"方形被削了角"：和判定槽的方仍然一眼分得开，但它自己不再是个图形。
        private const float OctagonCut = 0.30f;

        private static bool InOctagon(float dx, float dy, float scale) =>
            Mathf.Abs(dx) <= scale && Mathf.Abs(dy) <= scale
            && Mathf.Abs(dx) + Mathf.Abs(dy) <= scale * (2f - OctagonCut);

        /// <summary>实心八角。</summary>
        public static void DrawOctagon(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, OctagonTexture(0));
            GUI.color = prev;
        }

        /// <summary>
        /// 八角描边，线宽按**显示像素**给（和 IMGUIStyles.DrawOutline 的参数意义一致）。
        /// 源图 64 见方，所以先把显示线宽换算回源图线宽再烘：同一个形状画多大，
        /// 线看起来都是要的那么粗。
        /// </summary>
        public static void DrawOctagonOutline(Rect rect, float thickness, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;

            float side = Mathf.Max(1f, Mathf.Min(rect.width, rect.height));
            int sourceThickness = Mathf.Clamp(Mathf.RoundToInt(thickness * SourceSize / side), 1, 12);
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, OctagonTexture(sourceThickness));
            GUI.color = prev;
        }

        // sourceThickness = 0 是实心；> 0 是那么粗的一圈边。
        private static Texture2D OctagonTexture(int sourceThickness)
        {
            if (_octagons.TryGetValue(sourceThickness, out var cached) && cached != null)
                return cached;

            float inner = 1f - sourceThickness * 2f / SourceSize;
            var tex = sourceThickness <= 0
                ? Bake((dx, dy, _) => InOctagon(dx, dy, 1f))
                : Bake((dx, dy, _) => InOctagon(dx, dy, 1f) && !InOctagon(dx, dy, inner));
            _octagons[sourceThickness] = tex;
            return tex;
        }

        private static void DrawSector(Rect rect, float fillPercent, Color color)
        {
            int degrees = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(fillPercent) * 360f), 0, 360);
            if (degrees <= 0) return;

            var tex = SectorTexture(degrees);
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, tex);
            GUI.color = prev;
        }

        private static void DrawRing(Rect rect, Color color)
        {
            var tex = RingTexture();
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, tex);
            GUI.color = prev;
        }

        /// <summary>
        /// 任意三角形（对白气泡的小尾巴）。
        ///
        /// 只烘一张「直角三角形」：任何三角形都是它的一次仿射变换，所以拿 GUI.matrix
        /// 把单位方块映射到 a-b-c 上再画这张贴图就行。变换是**乘在**当前矩阵上的，
        /// 于是分组偏移、缩放、裁剪一律照旧生效——这正是它和以前那套 GL 的区别。
        /// </summary>
        public static void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;

            var tex = TriangleTexture();
            var m = Matrix4x4.identity;
            m.SetColumn(0, new Vector4(b.x - a.x, b.y - a.y, 0f, 0f));
            m.SetColumn(1, new Vector4(c.x - a.x, c.y - a.y, 0f, 0f));
            m.SetColumn(3, new Vector4(a.x, a.y, 0f, 1f));

            Matrix4x4 prevMatrix = GUI.matrix;
            Color prevColor = GUI.color;
            GUI.matrix = prevMatrix * m;
            GUI.color = color;
            GUI.DrawTexture(new Rect(0f, 0f, 1f, 1f), tex);
            GUI.color = prevColor;
            GUI.matrix = prevMatrix;
        }

        // ── 贴图 ─────────────────────────────────────────────────────────

        // 单位直角三角形：左上、右上、左下三个角，斜边是 x + y = 1。
        private static Texture2D TriangleTexture()
        {
            if (_triangle != null) return _triangle;
            _triangle = Bake((dx, dy, _) =>
            {
                float x = (dx + 1f) * 0.5f;
                float y = (dy + 1f) * 0.5f;
                return x + y <= 1f;
            });
            return _triangle;
        }

        private static Texture2D SectorTexture(int degrees)
        {
            if (_sectors.TryGetValue(degrees, out var cached) && cached != null)
                return cached;

            var tex = Bake((dx, dy, r) =>
            {
                if (r > 1f) return false;
                if (degrees >= 360) return true;
                // 十二点方向起，顺时针扫。Atan2 的 y 取负是因为贴图坐标向下为正。
                float angle = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;
                return angle <= degrees;
            });
            _sectors[degrees] = tex;
            return tex;
        }

        // 圆环的一段。按 (起点, 扫过) 缓存；上限 ≤6 时只有几种组合，连续模式则固定
        // 起点为 0、扫过取整度，因此贴图数量始终是有界的。
        private static Texture2D ArcTexture(int start, int sweep)
        {
            int key = start * 512 + sweep;
            if (_arcs.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var tex = Bake((dx, dy, r) =>
            {
                if (r > 1f || r < DialInnerRatio) return false;
                if (sweep >= 360) return true;
                float angle = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;
                float rel = angle - start;
                if (rel < 0f) rel += 360f;
                return rel <= sweep;
            });
            _arcs[key] = tex;
            return tex;
        }

        private static Texture2D RingTexture()
        {
            if (_ring != null) return _ring;

            float inner = 1f - RingThickness * 2f / SourceSize;
            _ring = Bake((dx, dy, r) => r <= 1f && r >= inner);
            return _ring;
        }

        /// <summary>
        /// 按一个「这一点在不在图形里」的判据烘一张纯白 + alpha 的贴图。
        /// dx/dy/r 都是以圆心为原点、半径为 1 的归一化坐标。
        /// </summary>
        private static Texture2D Bake(System.Func<float, float, float, bool> inside)
        {
            var tex = new Texture2D(SourceSize, SourceSize, TextureFormat.ARGB32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[SourceSize * SourceSize];
            float half = SourceSize / 2f;
            float step = 1f / (Supersample + 1);

            for (int py = 0; py < SourceSize; py++)
            {
                for (int px = 0; px < SourceSize; px++)
                {
                    int hits = 0;
                    for (int sy = 1; sy <= Supersample; sy++)
                    {
                        for (int sx = 1; sx <= Supersample; sx++)
                        {
                            float x = px + sx * step;
                            float y = py + sy * step;
                            float dx = (x - half) / half;
                            float dy = (y - half) / half;
                            if (inside(dx, dy, Mathf.Sqrt(dx * dx + dy * dy)))
                                hits++;
                        }
                    }
                    byte a = (byte)(255 * hits / (Supersample * Supersample));
                    // Texture2D 的第 0 行在下，GUI 的在上；这里整张翻过来存，
                    // 免得每个调用点都得记着翻一次。
                    pixels[(SourceSize - 1 - py) * SourceSize + px] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false);
            return tex;
        }
    }
}
