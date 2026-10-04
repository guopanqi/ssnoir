#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>玩家可调的界面缩放档。只改整体大小，不改版面结构。</summary>
    public enum UISizePreset
    {
        Compact,   // 看得更多
        Standard,  // 标准
        Large,     // 看得更清
    }

    /// <summary>
    /// 用 GUI.matrix 把整个 IMGUI 放进一套虚拟坐标里，所有界面代码只写虚拟像素。
    ///
    /// <b>只有一套版面，到哪都一样。</b>这是这套界面最要紧的一条规则：
    /// 游戏发布在手机上，所以版面就按手机设计；Editor、桌面构建、真机跑的是同一套数字，
    /// 没有「手机版」和「桌面版」之分。这样在哪预览看到的就是玩家最终看到的。
    ///
    /// 由此推出两条硬约束，改这套界面时不要破坏：
    ///
    /// 1. <b>版面尺寸里不许出现平台判断。</b>没有 <c>if (手机)</c> 这种分支——
    ///    一旦有，Editor 里看到的就不是玩家看到的，预览失去意义，问题只能等打包到真机才暴露。
    ///    宿主占位（比如小游戏宿主的胶囊按钮）那种「不是我们的地盘」，哪天真要留也一律
    ///    **恒定预留**，让预览照样看得见；绝不做「只在那个宿主里才让位」。
    /// 2. <b>能变的只有画布的宽高比。</b>竖直方向恒定 <see cref="DesignHeight"/> 个虚拟像素，
    ///    横向按屏幕比例延展。所以版面必须对宽度有弹性（约束驱动、流式），
    ///    但不需要、也不应该对「什么设备」有弹性。
    ///
    /// 输入是例外：鼠标有悬停、手指没有，这是输入能力的差别，不是版面的差别，
    /// 也不影响任何一个元素画在哪、画多大。见 <see cref="HasHoverPointer"/>。
    ///
    /// OnGUI() 里的用法：
    ///   1. 开头调一次 UIScale.Apply()（在 IMGUIStyles.Init 之前）。
    ///   2. 屏幕边缘定位用 VW / VH；<b>HUD 贴边一律用 SafeArea</b>。
    ///   3. Event.current.mousePosition 直接用——GUI.matrix 生效时 Unity 已换算好。
    ///   4. Camera.WorldToScreenPoint 用 WorldPointToVirtual() 转换。
    ///   5. 有边框/实底的 Rect 套一层 PixelSnap()，消掉非整数缩放下的边缘发虚。
    /// </summary>
    public static class UIScale
    {
        // ── 唯一的设计基准 ───────────────────────────────────────────────
        //
        // 一屏的高度 = 600 个虚拟像素，**不多不少、到哪都是这个数**。
        //
        // 这个数是按手机横屏定的：一台 6 寸手机横过来屏高约 65mm，于是 16px 的正文约 1.7mm、
        // 23px 的标题约 2.5mm——手机上读得清；而在显示器上看就是「一台放大的手机」，
        // 版面一模一样。
        //
        // 改它等于改整套界面的信息密度，不是调参：一屏能放几张卡、卡里塞不塞得下副标题、
        // 世界卡在同一列里让不让得开，全跟着动。改之前先把三档都过一遍。
        private const float BaseDesignHeight = 600f;

        private static float DesignHeight => _sizePreset switch
        {
            UISizePreset.Compact => BaseDesignHeight * 1.50f,   // 900：城市优先，默认紧凑
            UISizePreset.Large   => BaseDesignHeight,           // 600：原标准尺寸
            _                    => BaseDesignHeight * 1.25f,   // 750：紧凑与放大之间
        };

        /// <summary>
        /// 一个手指目标的下限（虚拟像素）。在 600 的画布、约 65mm 的屏高上合 4.3mm。
        /// 比人机指南的 7–9mm 小一档是刻意的：这套界面控件多，全撑到指南值之后整屏都是
        /// 大按钮、反而抢内容。真正难点中的控件（骰位、手牌方块）另行放大。
        /// </summary>
        public const float MinTouchSize = 40f;

        private static float _scale = 1f;
        private static UISizePreset _sizePreset = UISizePreset.Compact;

        /// <summary>物理像素 / 虚拟像素。</summary>
        public static float Scale => _scale;

        /// <summary>虚拟画布宽度——随屏幕比例延展，版面要对它有弹性。</summary>
        public static float VW { get; private set; } = 996f;

        /// <summary>虚拟画布高度——恒定，就是当前档位的设计高度，任何屏幕上都一样。</summary>
        public static float VH { get; private set; } = BaseDesignHeight;

        /// <summary>
        /// 虚拟坐标下的安全区（刘海 / 圆角 / 手势条之外的可用区域）。
        /// HUD 贴边元素一律锚这个矩形，不要锚 (0,0,VW,VH)。
        /// </summary>
        public static Rect SafeArea { get; private set; }

        /// <summary>玩家选的尺寸档。改动下一帧生效。</summary>
        public static UISizePreset SizePreset
        {
            get => _sizePreset;
            set => _sizePreset = value;
        }

        /// <summary>
        /// 这台设备有没有「悬停」这个状态。<b>只影响交互反馈，不影响任何尺寸和位置。</b>
        /// 鼠标能悬停、手指不能，这是输入能力的差别；手机上单纯是看不到悬停高亮，
        /// 版面一模一样。
        /// </summary>
        public static bool HasHoverPointer => !Input.touchSupported;

        /// <summary>
        /// OnGUI() 开头调一次。设置 GUI.matrix，并按本帧的屏幕尺寸重算 VW / VH / SafeArea。
        /// </summary>
        public static void Apply()
        {
            // 缩放直接由屏幕高度除设计高度得到，**不做任何吸附**。
            //
            // 以前会把它吸附到 1/8 的整数倍，为的是让边框像素落在整数物理像素上。代价是
            // VH 会随屏幕高度在 ±6% 内浮动——1136×640 的手机得到 569，1920×1080 的 Editor
            // 得到 540，两边看到的东西就不一样多了。而清晰度其实不依赖这个吸附：字号由
            // FontSize() 单独反算到整数物理像素，面板边框由 PixelSnap() 对齐。
            // 既然如此，就该要那个「到哪都一样」的保证。
            _scale = Screen.height / DesignHeight;
            VH = DesignHeight;
            VW = Screen.width  / _scale;
            SafeArea = ComputeSafeArea();

            GUI.matrix = Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                new Vector3(_scale, _scale, 1f));
        }

        // ── 关于文字发虚（已知问题，试过一次没成，留个路标）────────────────────
        //
        // 上面这层缩放会让文字比线糊：IMGUI 的动态字体只按 style.fontSize 烘字形、不看矩阵，
        // 所以 1080 高的屏上 14px 的字形被拉到 25 个物理像素，信息量只有 14px。
        // 边框由 PixelSnap() 对齐，所以线是利的——只有字是糊的。
        // IMGUIStyles.FontSize() 那个反算救不了它：它返回的仍是虚拟字号。
        //
        // 试过的做法：画字前把 GUI.matrix 拍回 identity，rect 和字号一起乘 scale，
        // 让字形按最终尺寸烘。设置面板上确实清楚很多，但**推广到全局会把交锋卡的文字画错位**。
        // 原因：GUI.BeginGroup / BeginScrollView 的组偏移不在 GUI.matrix 里，
        // 所以"矩阵还等于这层缩放"并不能证明当前坐标系没被平移过——闸放行了，字就跑到组外面去。
        // 下次要再做，得用 GUIUtility.GUIToScreenPoint(Vector2.zero) 把组偏移也算进来，
        // 并且**先在卷宗面板（BeginScrollView）上验**，不要在居中模态框上验——那里恰好没有 group。

        /// <summary>当前画布读数，供设置面板显示。</summary>
        public static string DescribeCanvas() =>
            $"画布 {VW:0}×{VH:0}　屏幕 {Screen.width}×{Screen.height}";

        // Screen.safeArea 是物理像素、原点在左下；GUI 是虚拟像素、原点在左上。
        private static Rect ComputeSafeArea()
        {
            Rect sa = Screen.safeArea;
            if (sa.width <= 0f || sa.height <= 0f)
                return new Rect(0f, 0f, VW, VH);

            return new Rect(
                sa.x / _scale,
                (Screen.height - sa.yMax) / _scale,
                sa.width  / _scale,
                sa.height / _scale);
        }

        // ── 触控命中 ─────────────────────────────────────────────────────

        /// <summary>
        /// 把控件矩形撑到最小触控尺寸。<b>只影响命中判定，不影响绘制</b>——视觉上还是原来
        /// 那么大，手指够得着的范围比看到的大一圈。
        /// 只补不足的那个轴：宽度本来就够的控件不横向膨胀，免得和邻居抢点击。
        /// </summary>
        public static Rect ExpandToMinTouch(Rect rect)
        {
            float dw = Mathf.Max(0f, MinTouchSize - rect.width);
            float dh = Mathf.Max(0f, MinTouchSize - rect.height);
            if (dw <= 0f && dh <= 0f) return rect;

            return new Rect(
                rect.x - dw * 0.5f,
                rect.y - dh * 0.5f,
                rect.width  + dw,
                rect.height + dh);
        }

        /// <summary>按最小触控尺寸给出控件应有的高度。用在构造控件矩形的地方。</summary>
        public static float TouchHeight(float designHeight) =>
            Mathf.Max(designHeight, MinTouchSize);

        // ── 模态框 ───────────────────────────────────────────────────────

        /// <summary>
        /// 把一个按设计尺寸写的模态框放进安全区正中，并收进安全区以内。
        /// </summary>
        public static Rect CenteredModal(float designWidth, float designHeight)
        {
            Rect safe = SafeArea;
            float w = Mathf.Min(designWidth,  Mathf.Max(0f, safe.width  - 32f));
            float h = Mathf.Min(designHeight, Mathf.Max(0f, safe.height - 24f));
            return new Rect(
                safe.x + (safe.width  - w) * 0.5f,
                safe.y + (safe.height - h) * 0.5f,
                w, h);
        }

        // ── 像素对齐 ─────────────────────────────────────────────────────
        // 把虚拟坐标吸附到物理像素边界。用于有可见边框 / 实底的面板矩形。

        public static float Floor(float v) => Mathf.Floor(v * _scale) / _scale;
        public static float Ceil(float v)  => Mathf.Ceil(v  * _scale) / _scale;

        /// <summary>把 Rect 四条边吸附到最近的物理像素边界。</summary>
        public static Rect PixelSnap(Rect r) => new Rect(
            Floor(r.x),
            Floor(r.y),
            Ceil(r.x + r.width)  - Floor(r.x),
            Ceil(r.y + r.height) - Floor(r.y));

        // ── 坐标换算 ─────────────────────────────────────────────────────

        /// <summary>
        /// Camera.WorldToScreenPoint（物理像素、Y 向上）→ 虚拟 GUI 坐标（左上原点、Y 向下）。
        /// </summary>
        public static Vector2 WorldPointToVirtual(Vector3 screenPoint) =>
            new Vector2(
                screenPoint.x / _scale,
                (Screen.height - screenPoint.y) / _scale);

        /// <summary>
        /// 虚拟尺寸 → 物理像素。GL 的位置应该走 GUIUtility.GUIToScreenPoint，以保留当前
        /// GUI group 的原点。
        /// </summary>
        public static float ScaleSize(float v) => v * _scale;
    }
}
