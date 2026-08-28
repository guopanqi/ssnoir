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
        private const float TopInset = 10f;
        private const float Gap = 8f;

        // 顶栏比手牌和骰位低一档，是有意的：这几个都是"想起来才点一下"的入口
        // （面板、设置、Debug），不是每回合都要瞄准的操作位。它们按最小触控尺寸撑满，
        // 就等于用整条屏幕顶去换几个几乎不点的按钮。34 仍在手指够得着的范围内，
        // 省下来的高度全部还给世界。真正的难点控件另有放大规则，不受这里影响。
        // 刻意不走 UIScale.TouchHeight()——那条下限（40）是给"要瞄准的控件"定的，
        // 这里是明知故犯的一档例外，不是忘了调。
        private const float RowHeightValue = 34f;

        private static float RowHeight => RowHeightValue;

        /// <summary>整条顶栏（含左右两组）占据的矩形。</summary>
        public Rect Bar { get; }

        public Rect Back { get; }
        public Rect Breadcrumb { get; }
        public Rect Day { get; }
        public Rect DossierToggle { get; }
        public Rect RelationToggle { get; }
        public Rect GrowthToggle { get; }
        public Rect DebugToggle { get; }
        public Rect HelpToggle { get; }
        public Rect SettingsToggle { get; }

        /// <summary>顶栏下方那条分隔线的 y。</summary>
        public float DividerY { get; }

        /// <summary>顶栏之下、可以开始摆世界内容（卡片 / 边缘信标）的 y。</summary>
        public float ContentTop { get; }

        private TopHudLayout(Rect bar, Rect back, Rect breadcrumb, Rect day, Rect dossierToggle,
            Rect relationToggle, Rect growthToggle, Rect debugToggle, Rect helpToggle,
            Rect settingsToggle, float dividerY, float contentTop)
        {
            Bar = bar;
            Back = back;
            Breadcrumb = breadcrumb;
            Day = day;
            DossierToggle = dossierToggle;
            RelationToggle = relationToggle;
            GrowthToggle = growthToggle;
            DebugToggle = debugToggle;
            HelpToggle = helpToggle;
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

            var bar = new Rect(left, top, Mathf.Max(0f, right - left), rowH);

            // 右组：从右往左依次安放，宽度在窄屏上收成短名。
            float cursorRight = right;
            Rect settings = TakeFromRight(ref cursorRight, 54f, rowH, top, gap);
            // 帮助紧挨设置：两个都是「想起来才点一下」的东西，但帮助在 demo 里要找得到。
            Rect help     = TakeFromRight(ref cursorRight, 54f, rowH, top, gap);
            Rect debug    = TakeFromRight(ref cursorRight, 54f, rowH, top, gap);
            Rect growth   = TakeFromRight(ref cursorRight, 64f, rowH, top, gap);
            // 声誉这一版整个收起来（见 NavigationDrawer.ShowRelationPanel），
            // 位置也一起让出去：它不是"有时候不画"，是这一版没有这个东西。
            Rect relation = NavigationDrawer.ShowRelationPanel
                ? TakeFromRight(ref cursorRight, 64f, rowH, top, gap)
                : Rect.zero;
            // 卷宗排在这一组最左：它是这组里唯一每天都要开的，离面包屑最近。
            Rect dossier  = TakeFromRight(ref cursorRight, 64f, rowH, top, gap);

            // 左组：返回 → 面包屑 → 天数。读起来是一句话：从哪儿回去、我在哪、第几天。
            //
            // 天数不紧跟在面包屑后面，而是靠着右边那组按钮：面包屑是弹性的（地点名一长
            // 一短），跟着它走天数就会左右跳。它是每天都要瞟一眼的读数，位置必须钉死。
            // 代价是面包屑和天数之间会空一段——空的那段总比会动的读数好。
            float cursorLeft = left;
            Rect back = new Rect(cursorLeft, top, 82f, rowH);
            cursorLeft = back.xMax + gap;
            Rect day = new Rect(Mathf.Max(cursorLeft, cursorRight - 64f), top, 64f, rowH);
            float breadcrumbWidth = Mathf.Max(0f, day.x - gap - cursorLeft);
            var breadcrumb = new Rect(cursorLeft, top, breadcrumbWidth, rowH);

            float dividerY = top + rowH + 9f;
            float contentTop = dividerY + 12f;

            return new TopHudLayout(
                bar,
                UIScale.PixelSnap(back),
                breadcrumb,
                UIScale.PixelSnap(day),
                UIScale.PixelSnap(dossier),
                UIScale.PixelSnap(relation),
                UIScale.PixelSnap(growth),
                UIScale.PixelSnap(debug),
                UIScale.PixelSnap(help),
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
