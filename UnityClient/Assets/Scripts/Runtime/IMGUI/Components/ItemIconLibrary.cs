#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 物品图标：把每样东西烘成一张纯白 + alpha 的剪影贴图，由 GUI.color 上色。
    ///
    /// 为什么不用字：物品格里原来写的是名字的第一个字（"酒""烟""药"），那是占位不是图标——
    /// 一格里两种排版（大字 + 小字）本来就打架，而且"金"和"金钱"的 $ 又各是一套语气。
    /// 剪影统一了语气：同一种黑白、同一种粗细，缩到 20px 也还认得出轮廓。
    ///
    /// 为什么烘贴图而不画线：和 <see cref="ShapeDrawer"/> 同一个理由——GUI.DrawTexture 走
    /// 界面自己的矩阵/分组/裁剪栈，放进滚动视图、模态面板都自动是对的；而且缩下去顺带
    /// 有一层双线性抗锯齿。<b>要加新物品图标，在 Shapes 里加一条，不要在调用点画线。</b>
    ///
    /// 坐标约定：判据函数收到的 (x, y) 是以图标中心为原点、边到边为 ±1 的归一化坐标，
    /// y 向下为正（和 GUI 一致）。形状用下面的 Box/RoundBox/Arc/Taper/Capsule 拼（斜着的用 RotX/RotY 转过去），减法就是 &amp;&amp; !。
    /// </summary>
    public static class ItemIconLibrary
    {
        private const int SourceSize = 64;
        private const int Supersample = 3;

        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        private static readonly Dictionary<string, Func<float, float, bool>> Shapes =
            new Dictionary<string, Func<float, float, bool>>
            {
                // 金钱：一张钞票，中间把 $ 挖掉。剪影里"钱"没法靠形状说清楚——
                // 钞票的轮廓和名片、纸条、卡片是同一个矩形；能一眼定性的是那个符号，
                // 所以符号做负形（挖空），轮廓做正形，两层各说一半。
                ["金钱"] = (x, y) =>
                    RoundBox(x, y, 0f, 0.02f, 0.94f, 0.58f, 0.10f)
                        && !Dollar(x, y, 0f, 0.02f, 0.66f),

                // 香烟：横躺的一支，左边一小段是滤嘴（留一道白缝断开），右端飘着一缕烟。
                // 之前画的是烟盒——盒子在剪影里就是块砖。真正让人认出"烟"的是那缕烟。
                ["香烟"] = (x, y) =>
                    Box(x, y, 0.10f, 0.34f, 0.62f, 0.16f) ||     // 烟身
                    Box(x, y, -0.66f, 0.34f, 0.20f, 0.16f) ||    // 滤嘴
                    Arc(x, y, 0.34f, -0.20f, 0.26f, 0.08f, 250f, 430f) ||  // 烟：下半段
                    Arc(x, y, 0.74f, -0.52f, 0.26f, 0.08f, 70f, 250f),     // 烟：上半段（反向）

                // 酒：长颈 + 溜肩 + 方身的酒瓶，腰上挖一圈标签。
                // 上一版颈太短肩太直，看着像罐头；瓶子之所以是瓶子，全在颈肩那段收腰。
                ["酒"] = (x, y) =>
                    (Box(x, y, 0f, -0.66f, 0.12f, 0.24f) ||          // 瓶颈
                     Box(x, y, 0f, -0.90f, 0.18f, 0.09f) ||          // 瓶口
                     Taper(x, y, -0.42f, -0.08f, 0.12f, 0.40f) ||    // 溜肩
                     RoundBox(x, y, 0f, 0.44f, 0.40f, 0.44f, 0.10f)) // 瓶身
                        && !Box(x, y, 0f, 0.36f, 0.40f, 0.13f),      // 标签留白

                // 药品：药瓶 + 瓶身上挖一个十字。胶囊在这个尺寸下只是一段斜的圆角条，
                // 和别的什么都能混；十字是这一格里唯一没有歧义的记号。
                ["药品"] = (x, y) =>
                    Box(x, y, 0f, -0.66f, 0.34f, 0.18f) ||           // 瓶盖
                    (RoundBox(x, y, 0f, 0.24f, 0.44f, 0.66f, 0.10f)  // 瓶身
                        && !(Box(x, y, 0f, 0.26f, 0.30f, 0.09f)      // 十字：横
                          || Box(x, y, 0f, 0.26f, 0.09f, 0.30f))),   // 十字：竖

                // 半包「老金牌」：烟盒 + 探出来的三根。和「香烟」必须一眼分得开——
                // 那边是散装的一支，这边是一整包，所以三根、且带盒身上那道商标横条。
                ["半包「老金牌」"] = (x, y) =>
                    (RoundBox(x, y, 0f, 0.44f, 0.52f, 0.46f, 0.07f)  // 盒身
                        && !Box(x, y, 0f, 0.44f, 0.52f, 0.08f))      // 商标横条留白
                    || PackCigarettes(x, y),

                // 奥托的地址纸条：撕下来的一角纸，上面几道字。字不写具体内容——
                // 剪影里"有字"本身就是信息，写成什么反而在这个尺寸下糊成一团。
                ["奥托的地址纸条"] = (x, y) =>
                    Box(RotX(x, y, -8f), RotY(x, y, -8f), 0f, 0f, 0.62f, 0.72f)
                        && !SlipLines(RotX(x, y, -8f), RotY(x, y, -8f))
                        && !TornEdge(RotX(x, y, -8f), RotY(x, y, -8f), 0.72f, 0.16f, 14f),

                // 莱恩的底片与照片：一截胶片，两边打齿孔。齿孔是胶片唯一无可替代的特征，
                // 少了它就只是个斜矩形。
                ["莱恩的底片与照片"] = (x, y) =>
                    Box(RotX(x, y, -20f), RotY(x, y, -20f), 0f, 0f, 0.86f, 0.46f)
                        && !FilmPerforations(RotX(x, y, -20f), RotY(x, y, -20f)),

                // 莱恩的照片与信：信封 + 从后面探出来的照片一角。
                // 光画信封就只是"信"；那一角是"还夹着照片"这件事。
                ["莱恩的照片与信"] = (x, y) =>
                    Envelope(x, y, 0.78f, 0.46f, 0.24f)
                    || (Box(RotX(x - 0.40f, y + 0.52f, 16f), RotY(x - 0.40f, y + 0.52f, 16f), 0f, 0f, 0.34f, 0.30f)
                        && !RoundBox(x, y, 0f, 0.24f, 0.84f, 0.52f, 0.06f)),
            };

        /// <summary>
        /// 空骰位里的那颗骰子。它不在 <see cref="Shapes"/> 表里——那张表是按物品名查的，
        /// 骰子不是物品。
        ///
        /// 画成**空心**是有意的：这里表示的是"还没放"。实心剪影的语气是"这儿有一颗骰子"，
        /// 那正好是相反的意思。点数取五点（最认得出是骰子的一面），只是形状不是数值——
        /// 真放进来之后画的是真实点数的数字，不是这张图。
        /// </summary>
        public static void DrawDiePlaceholder(Rect rect, Color color, float scale = 1f)
        {
            if (Event.current.type != EventType.Repaint) return;

            float side = Mathf.Min(rect.width, rect.height) * scale;
            var square = new Rect(
                rect.x + (rect.width - side) * 0.5f,
                rect.y + (rect.height - side) * 0.5f,
                side, side);

            Color prev = GUI.color;
            IMGUIStyles.SetColor(color);
            GUI.DrawTexture(square, DieTexture());
            GUI.color = prev;
        }

        /// <summary>这个物品有图标吗。没有的调用点继续写字，别硬凑。</summary>
        public static bool Has(string? itemName) =>
            !string.IsNullOrEmpty(itemName) && Shapes.ContainsKey(itemName!);

        /// <summary>
        /// 在 rect 里画物品图标（居中、按短边取正方形）。没有这个物品的图标就返回 false，
        /// 由调用点退回文字。
        /// </summary>
        public static bool TryDraw(Rect rect, string? itemName, Color color, float scale = 1f)
        {
            if (!Has(itemName)) return false;
            if (Event.current.type != EventType.Repaint) return true;

            var tex = IconTexture(itemName!);
            float side = Mathf.Min(rect.width, rect.height) * scale;
            var square = new Rect(
                rect.x + (rect.width - side) * 0.5f,
                rect.y + (rect.height - side) * 0.5f,
                side, side);

            Color prev = GUI.color;
            IMGUIStyles.SetColor(color);
            GUI.DrawTexture(square, tex);
            GUI.color = prev;
            return true;
        }

        // ── 形状原语（归一化坐标，y 向下） ───────────────────────────────

        private static bool Box(float x, float y, float cx, float cy, float halfW, float halfH) =>
            Mathf.Abs(x - cx) <= halfW && Mathf.Abs(y - cy) <= halfH;

        private static bool RoundBox(float x, float y, float cx, float cy, float halfW, float halfH, float r)
        {
            float dx = Mathf.Max(Mathf.Abs(x - cx) - (halfW - r), 0f);
            float dy = Mathf.Max(Mathf.Abs(y - cy) - (halfH - r), 0f);
            return Mathf.Abs(x - cx) <= halfW && Mathf.Abs(y - cy) <= halfH && dx * dx + dy * dy <= r * r;
        }

        /// <summary>
        /// 圆环上的一段：以 (cx, cy) 为心、半径 R、线宽 2t，覆盖 [a0, a1] 这段角度。
        /// 角度按数学惯例（0° 朝右、逆时针为正、y 向上），a1 可以超过 360 表示跨过 0°。
        /// </summary>
        private static bool Arc(float x, float y, float cx, float cy, float R, float t, float a0, float a1)
        {
            float dx = x - cx, dy = -(y - cy);
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            if (Mathf.Abs(r - R) > t) return false;

            float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            if (a < 0f) a += 360f;
            return a1 > 360f ? (a >= a0 || a <= a1 - 360f) : (a >= a0 && a <= a1);
        }

        /// <summary>上窄下宽（或反过来）的一段梯形：瓶子的肩、杯子的身都是它。</summary>
        private static bool Taper(float x, float y, float yTop, float yBottom, float halfWTop, float halfWBottom)
        {
            if (y < yTop || y > yBottom) return false;
            float f = (y - yTop) / (yBottom - yTop);
            return Mathf.Abs(x) <= halfWTop + (halfWBottom - halfWTop) * f;
        }

        /// <summary>
        /// 美元符号：上下两段弧咬成 S，再穿一根竖杠。
        /// 这里不用字体画——字体在缩放和缺字回退上都不可控，而 $ 只是两段弧，画出来更稳。
        /// </summary>
        private static bool Dollar(float x, float y, float cx, float cy, float s)
        {
            x = (x - cx) / s;
            y = (y - cy) / s;
            return Arc(x, y, 0f, -0.34f, 0.38f, 0.12f, 340f, 610f)
                || Arc(x, y, 0f, 0.34f, 0.38f, 0.12f, 180f, 430f)
                || Box(x, y, 0f, 0f, 0.096f, 0.90f);
        }

        /// <summary>线段两端加圆头的胶囊：这里只用来在实心形状上切斜缝（信封的封口）。</summary>
        private static bool Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float vx = bx - ax, vy = by - ay;
            float len2 = vx * vx + vy * vy;
            float t = len2 <= 0f ? 0f : Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / len2);
            float px = ax + vx * t - x, py = ay + vy * t - y;
            return px * px + py * py <= r * r;
        }

        // 把坐标转过一个角度再喂给别的原语，就得到一个斜着的形状（纸条、胶片、照片角）。
        // 拆成 X / Y 两个函数是为了让图标那一栏还能写成一个表达式。
        private static float RotX(float x, float y, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return x * Mathf.Cos(r) + y * Mathf.Sin(r);
        }

        private static float RotY(float x, float y, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return -x * Mathf.Sin(r) + y * Mathf.Cos(r);
        }

        /// <summary>撕口：把下边缘啃成一排小豁口，纸就从"裁下来的"变成"撕下来的"。</summary>
        private static bool TornEdge(float x, float y, float edgeY, float depth, float frequency) =>
            y > edgeY - depth * Mathf.Abs(Mathf.Sin(x * frequency));

        // 纸条上的字：三道长的 + 末尾一道短的（落款/门牌号的位置）。
        private static bool SlipLines(float x, float y) =>
            Box(x, y, -0.06f, -0.34f, 0.40f, 0.055f) ||
            Box(x, y, -0.06f, -0.06f, 0.40f, 0.055f) ||
            Box(x, y, -0.06f, 0.22f, 0.40f, 0.055f) ||
            Box(x, y, -0.24f, 0.50f, 0.22f, 0.055f);

        // 胶片两边的齿孔。
        private static bool FilmPerforations(float x, float y)
        {
            for (int i = -2; i <= 2; i++)
            {
                float cx = i * 0.31f;
                if (Box(x, y, cx, -0.34f, 0.075f, 0.075f)) return true;
                if (Box(x, y, cx, 0.34f, 0.075f, 0.075f)) return true;
            }
            return false;
        }

        // 烟盒里探出来的三根，中间那根高一点。
        private static bool PackCigarettes(float x, float y)
        {
            for (int i = -1; i <= 1; i++)
                if (Box(x, y, i * 0.24f, -0.30f + Mathf.Abs(i) * 0.06f, 0.09f, 0.28f)) return true;
            return false;
        }

        /// <summary>信封：一个圆角矩形，两道从上角连到中心的斜缝就是封口。</summary>
        private static bool Envelope(float x, float y, float halfW, float halfH, float cy)
        {
            const float seam = 0.055f;
            return RoundBox(x, y, 0f, cy, halfW, halfH, 0.06f)
                && !(Capsule(x, y, -halfW, cy - halfH, 0f, cy + 0.06f, seam)
                  || Capsule(x, y, halfW, cy - halfH, 0f, cy + 0.06f, seam));
        }

        // 斜 12° 的圆角方框 + 五个点。转一点角度是为了和骰位本身的方边框分开——
        // 正着画就成了"框里又一个框"。
        private static bool Die(float x, float y)
        {
            float rx = RotX(x, y, 12f) / 0.76f;
            float ry = RotY(x, y, 12f) / 0.76f;
            bool frame = RoundBox(rx, ry, 0f, 0f, 0.92f, 0.92f, 0.26f)
                      && !RoundBox(rx, ry, 0f, 0f, 0.74f, 0.74f, 0.20f);
            return frame || DiePips(rx, ry);
        }

        private static bool DiePips(float x, float y)
        {
            const float r = 0.11f;
            for (int i = -1; i <= 1; i += 2)
            {
                for (int j = -1; j <= 1; j += 2)
                {
                    float dx = x - i * 0.34f, dy = y - j * 0.34f;
                    if (dx * dx + dy * dy <= r * r) return true;
                }
            }
            return x * x + y * y <= r * r;
        }

        // ── 烘焙 ─────────────────────────────────────────────────────────

        private static Texture2D? _die;

        private static Texture2D DieTexture()
        {
            if (_die == null) _die = Bake(Die);
            return _die;
        }

        private static Texture2D IconTexture(string itemName)
        {
            if (_cache.TryGetValue(itemName, out var cached) && cached != null)
                return cached;

            var tex = Bake(Shapes[itemName]);
            _cache[itemName] = tex;
            return tex;
        }

        private static Texture2D Bake(Func<float, float, bool> inside)
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
                            float fx = (px + sx * step - half) / half;
                            float fy = (py + sy * step - half) / half;
                            if (inside(fx, fy)) hits++;
                        }
                    }
                    byte a = (byte)(255 * hits / (Supersample * Supersample));
                    // Texture2D 第 0 行在下、GUI 在上：整张翻着存，调用点就不用记这件事。
                    pixels[(SourceSize - 1 - py) * SourceSize + px] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false);
            return tex;
        }
    }
}
