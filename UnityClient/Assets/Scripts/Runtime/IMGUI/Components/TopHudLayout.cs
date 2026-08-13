#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 整条顶栏的唯一布局所有者：左边一组（返回 / 面包屑）、右边一组（天数 / 关系 / 成长 /
    /// Debug / 设置），面包屑吃掉中间剩下的所有宽度。
    ///
    /// 以前左边各控件自己写死 x（返回 40、面包屑 170、天数 670），右边从屏幕右缘往左推。
    /// 在 1920 宽的桌面上中间空着一大块，看不出问题；手机的虚拟画布只有 800 上下，两组
    /// 直接叠在一起。顶栏必须由一处统一分配宽度，控件只能问它要矩形。
    ///
    /// 所有横向锚点都取自 <see cref="UIScale.SafeArea"/>——刘海屏横屏时左右两侧会被挖掉。
    /// </summary>
    public readonly struct TopHudLayout
    {
        // 边距与行高随设备走：桌面 40px 的留白放到手机的虚拟画布上就是一大条空地。
        private const float SideMargin = 16f;
        private const float TopInset = 12f;
        private const float Gap = 10f;

        // 顶栏控件都是主要操作入口，一律不低于最小触控尺寸。
        private static float RowHeight => UIScale.TouchHeight(40f);

        /// <summary>整条顶栏（含左右两组）占据的矩形。</summary>
        public Rect Bar { get; }

        public Rect Back { get; }
        public Rect Breadcrumb { get; }
        public Rect Day { get; }
        public Rect RelationToggle { get; }
        public Rect GrowthToggle { get; }
        public Rect DebugToggle { get; }
        public Rect SettingsToggle { get; }

        /// <summary>顶栏下方那条分隔线的 y。</summary>
        public float DividerY { get; }

        /// <summary>顶栏之下、可以开始摆世界内容（卡片 / 边缘信标）的 y。</summary>
        public float ContentTop { get; }

        private TopHudLayout(Rect bar, Rect back, Rect breadcrumb, Rect day,
            Rect relationToggle, Rect growthToggle, Rect debugToggle, Rect settingsToggle,
            float dividerY, float contentTop)
        {
            Bar = bar;
            Back = back;
            Breadcrumb = breadcrumb;
            Day = day;
            RelationToggle = relationToggle;
            GrowthToggle = growthToggle;
            DebugToggle = debugToggle;
            SettingsToggle = settingsToggle;
            DividerY = dividerY;
            ContentTop = contentTop;
        }

        public static TopHudLayout Create()
        {
            Rect safe = UIScale.SafeArea;

            float margin = SideMargin;
            float gap = Gap;
            float rowH = RowHeight;
            float top = safe.y + TopInset;

            float left = safe.x + margin;
            float right = safe.xMax - margin;

            // 小游戏宿主的胶囊按钮占着右上角。那块地不归游戏管，画上去就是被压住、点不到
            // ——按钮组必须整体往左让，而不是指望它不挡。
            Rect hostReserved = UIScale.TopRightReserved;
            if (hostReserved.width > 0f && top < hostReserved.yMax)
                right = Mathf.Min(right, hostReserved.xMin - gap);

            var bar = new Rect(left, top, Mathf.Max(0f, right - left), rowH);

            // 右组：从右往左依次安放，宽度在窄屏上收成短名。
            float cursorRight = right;
            Rect settings = TakeFromRight(ref cursorRight, 60f, rowH, top, gap);
            Rect debug    = TakeFromRight(ref cursorRight, 60f, rowH, top, gap);
            Rect growth   = TakeFromRight(ref cursorRight, 72f, rowH, top, gap);
            Rect relation = TakeFromRight(ref cursorRight, 72f, rowH, top, gap);

            // 左组：返回 → 天数 → 面包屑（吃掉剩下的全部宽度，自己打省略号）。
            //
            // 天数放在左边而不是跟着右组：右上角除了胶囊还压着开发版的绿色横幅，屏幕中上部
            // 那一条基本是别人的地方。左边这一串正好是"我在哪、第几天"，读起来也是一句话。
            float cursorLeft = left;
            Rect back = new Rect(cursorLeft, top, 92f, rowH);
            cursorLeft = back.xMax + gap;
            Rect day = new Rect(cursorLeft, top, 72f, rowH);
            cursorLeft = day.xMax + gap;
            float breadcrumbWidth = Mathf.Max(0f, cursorRight - cursorLeft);
            var breadcrumb = new Rect(cursorLeft, top, breadcrumbWidth, rowH);

            float dividerY = top + rowH + 12f;
            float contentTop = dividerY + 14f;

            return new TopHudLayout(
                bar,
                UIScale.PixelSnap(back),
                breadcrumb,
                UIScale.PixelSnap(day),
                UIScale.PixelSnap(relation),
                UIScale.PixelSnap(growth),
                UIScale.PixelSnap(debug),
                UIScale.PixelSnap(settings),
                dividerY,
                contentTop);
        }

        private static Rect TakeFromRight(ref float cursorX, float width, float height, float top, float gap)
        {
            cursorX -= width;
            var rect = new Rect(cursorX, top, width, height);
            cursorX -= gap;
            return rect;
        }
    }
}
