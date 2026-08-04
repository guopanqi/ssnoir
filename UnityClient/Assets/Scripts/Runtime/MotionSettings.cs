#nullable enable
using UnityEngine;

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
    /// </summary>
    public static class MotionSettings
    {
        private const string PrefsKey = "SSNoir.ReduceMotion";

        /// <summary>低动画下焦点切换的溶解时长。短到不像一段动画，长到不像一次闪屏。</summary>
        public const float CrossfadeDuration = 0.16f;

        /// <summary>低动画下灯标平移的时长，对应正常模式的 0.42 秒。</summary>
        public const float ReducedNavigationDuration = 0.18f;

        private static bool _loaded;
        private static bool _reduceMotion;

        public static bool ReduceMotion
        {
            get
            {
                if (!_loaded)
                {
                    _reduceMotion = PlayerPrefs.GetInt(PrefsKey, 0) != 0;
                    _loaded = true;
                }
                return _reduceMotion;
            }
            set
            {
                _reduceMotion = value;
                _loaded = true;
                PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
