#nullable enable

namespace SSNoir
{
    /// <summary>
    /// 减少动画（reduce motion）总开关。
    ///
    /// 它只管**玩家自己翻页**的镜头：城里点一张地点卡、返回、回家、拖灯标。这些一局要做几十次，
    /// 每次都绕着楼转一圈很快就腻，也拖慢操作，所以打开后：
    ///   · 焦点切换：不再走弧线，硬切 + 交叉溶解（见 <see cref="ViewCrossfade"/>）。
    ///   · 灯标导航：平移保留但缩短；绕轴那支改成溶解。
    ///
    /// **剧情带着镜头走的那些不听它**：Portal 推进 / 穿过 / 拉出、交锋里换场、倒下送医、
    /// 过场。它们一局没几次，而且那段路本身在交代空间关系——"剧院在城里哪儿、你进到了
    /// 里面"——砍掉就只剩一次莫名其妙的黑场。代码上对应 <c>BeginFocusTravel</c> 的
    /// <c>respectReduceMotion: false</c> 与 <c>UpdateCameraFocus(storyDriven: true)</c>。
    /// 过夜黑场本来就不动镜头，两种模式一个节奏。
    ///
    /// 随游戏存档保存；旧存档没有该字段时默认使用减少镜头动画。
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

        public static bool ReduceMotion { get; set; } = true;

    }
}
