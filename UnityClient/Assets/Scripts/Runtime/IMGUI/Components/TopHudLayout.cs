#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 整条顶栏的唯一布局所有者：左边一组（返回 / 面包屑）、右边一格微透暗条
    /// （天数读数 + 卷宗 / 成长 / 帮助 / 设置 / 调试五个文字开关），面包屑吃掉中间剩下的所有宽度。
    ///
    /// 右边这格只有一层微透暗底（见 IMGUIStyles.FunctionSlotBg），无框、无投影——
    /// 实墨 + 硬投影在右上角太像一件可拿的东西，细框也一样往「卡」上靠，都不要。
    /// 画法由调用方自己画一笔底，这里面只管把矩形算出来。暗条里没有图标，
    /// 五个入口都是次级灰字（见 IMGUIButton.DrawTopTextToggle），天数是左侧的读数，
    /// 中间一道细分隔线。天数不紧跟在面包屑后面，而是钉在暗条里：面包屑是弹性的
    /// （地点名一长一短），跟着它走天数就会左右跳。它是每天都要瞟一眼的读数，
    /// 位置必须钉死。代价是面包屑和天数之间会空一段——空的那段总比会动的读数好。
    ///
    /// 所有横向锚点都取自 <see cref="UIScale.SafeArea"/>——刘海屏横屏时左右两侧会被挖掉。
    /// </summary>
    public readonly struct TopHudLayout
    {
        // 边距与行高随设备走：桌面 40px 的留白放到手机的虚拟画布上就是一大条空地。
        private const float SideMargin = 16f;
        private const float TopInset = 10f;
        private const float Gap = 6f;

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
        public Rect GrowthToggle { get; }
        public Rect HelpToggle { get; }
        public Rect SettingsToggle { get; }
        public Rect DebugToggle { get; }

        /// <summary>右上角整块暗条（含天数读数与五个文字开关）；交锋里卷宗入口隐藏时缩掉空位。</summary>
        public Rect FunctionPlate(bool showDossier)
        {
            float left = (showDossier ? DossierToggle : GrowthToggle).xMin;
            // 天数是暗条的一部分：读数和开关共用同一块底，顶栏左右两截才是一家人。
            left = Mathf.Min(left, Day.xMin);
            return UIScale.PixelSnap(new Rect(left - PlatePadX, SettingsToggle.yMin - PlatePadY,
                DebugToggle.xMax - left + PlatePadX * 2f, SettingsToggle.height + PlatePadY * 2f));
        }

        /// <summary>顶栏之下、可以开始摆世界内容（卡片 / 边缘信标）的 y。</summary>
        public float ContentTop { get; }

        private TopHudLayout(Rect bar, Rect back, Rect breadcrumb, Rect day, Rect dossierToggle,
            Rect growthToggle, Rect helpToggle,
            Rect settingsToggle, Rect debugToggle, float contentTop)
        {
            Bar = bar;
            Back = back;
            Breadcrumb = breadcrumb;
            Day = day;
            DossierToggle = dossierToggle;
            GrowthToggle = growthToggle;
            HelpToggle = helpToggle;
            SettingsToggle = settingsToggle;
            DebugToggle = debugToggle;
            ContentTop = contentTop;
        }

        // 暗条内部尺寸：二字开关 52×5、读数 76、开关之间 2、读数与开关之间留 10 给分隔线。
        // 整块约 370，面包屑相应收窄。
        private const float EntryW = 52f;
        private const float DayW = 76f;
        private const float EntryGap = 2f;
        private const float DayGap = 10f;
        private const float PlatePadX = 8f;
        private const float PlatePadY = 4f;

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

            // 右组是低频文字开关；卷宗排在最左——它是这组里唯一每天都要开的，离面包屑最近。
            // 调试排在最右：开发用的东西贴边，不跟天天点的抢位置。
            float cursorRight = right;
            Rect debug    = TakeFromRight(ref cursorRight, EntryW, rowH, top, EntryGap);
            Rect settings = TakeFromRight(ref cursorRight, EntryW, rowH, top, EntryGap);
            Rect help     = TakeFromRight(ref cursorRight, EntryW, rowH, top, EntryGap);
            Rect growth   = TakeFromRight(ref cursorRight, EntryW, rowH, top, EntryGap);
            Rect dossier  = TakeFromRight(ref cursorRight, EntryW, rowH, top, EntryGap);

            // 天数是暗条左侧的读数，和开关共用同一块底。
            cursorRight -= DayGap;
            Rect day = TakeFromRight(ref cursorRight, DayW, rowH, top, 0f);

            // 左组：返回 → 面包屑。读起来是一句话：从哪儿回去、我在哪；第几天钉在右边暗条里。
            float cursorLeft = left;
            Rect back = new Rect(cursorLeft, top, 82f, rowH);
            cursorLeft = back.xMax + gap;
            float breadcrumbWidth = Mathf.Max(0f, day.x - gap - cursorLeft);
            var breadcrumb = new Rect(cursorLeft, top, breadcrumbWidth, rowH);

            float contentTop = top + rowH + 8f;

            return new TopHudLayout(
                bar,
                UIScale.PixelSnap(back),
                breadcrumb,
                UIScale.PixelSnap(day),
                UIScale.PixelSnap(dossier),
                UIScale.PixelSnap(growth),
                UIScale.PixelSnap(help),
                UIScale.PixelSnap(settings),
                UIScale.PixelSnap(debug),
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
