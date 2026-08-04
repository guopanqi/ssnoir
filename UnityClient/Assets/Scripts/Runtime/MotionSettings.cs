#nullable enable

namespace SSNoir
{
    /// <summary>
    /// 减少动画（reduce motion）总开关，语义对齐 macOS / iOS 的同名系统设置：
    /// 华丽的空间位移被换成不移动的透明度过渡。
    ///
    /// 靶子是**绕轴旋转和推近**，不是所有运动——让人晕的是"绕着一栋楼转过去"和
    /// "往里推"，纯位移并不会。所以打开后：
    ///   · 焦点切换：不再走弧线，硬切 + 交叉溶解（见 <see cref="ViewCrossfade"/>）。
    ///   · 场景过渡：推进 / 穿过 / 拉出三段路全部不走，只留黑场对切。
    ///   · 灯标导航：平移保留但缩短；绕轴那支改成溶解。
    ///
    /// **只活在内存里，每次启动回到默认值。** 一个随手可改的显示开关不值得为它引入
    /// 一套持久化：PlayerPrefs 在本项目的 WebGL 构建里被 TapSDK 接管，落盘又要自己
    /// 铺一层文件读写和失败处理——两条路都比这个开关本身重。真要跨启动记住，
    /// 以后跟着存档一起走即可。
    /// </summary>
    public static class MotionSettings
    {
        /// <summary>
        /// 低动画下焦点切换的溶解时长。这是本模式唯一还看得见的"动画"，宁可偏慢也不能偏快：
        /// 短了读起来就是硬切，而这个模式的用户要的正是"别抢我的眼睛"。
        /// </summary>
        public const float CrossfadeDuration = 0.5f;

        /// <summary>低动画下灯标平移的时长，对应正常模式的 0.42 秒。</summary>
        public const float ReducedNavigationDuration = 0.18f;

        public static bool ReduceMotion { get; set; }

        /// <summary>
        /// Debug 专用的零时长镜头模式。开启后所有自动运镜直接硬切到终点，优先级高于
        /// <see cref="ReduceMotion"/>，但不会修改玩家的减少动画设置。
        /// </summary>
        public static bool DebugInstantCameraCuts { get; set; }
    }
}
