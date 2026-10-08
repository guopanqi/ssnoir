#nullable enable
using UnityEngine;

namespace SSNoir.UnityTheatre
{
    // Stage tuning knobs, edited live in the Inspector during rehearsal.
    // Asset lives at Resources/Theatre/LineTheatreSettings (create once via SSNoir/剧场 menu).
    // Missing asset means defaults; the player never throws for tuning.
    public sealed class TheatreSettings : ScriptableObject
    {
        [Range(0.5f, 1.5f), Tooltip("内容缩放：1 当前大小，调小内容缩小（画布始终全屏，没有框）")]
        public float ContentScale = 1f;
        [Range(0f, 4f), Tooltip("舞台内部背景柔焦倍数：0 只压暗不模糊（不管城市透不透）")]
        public float BackgroundBlur = 1f;
        [Range(0f, 1f), Tooltip("画布压暗：1 纯黑画布，调小透出后面的城市（舞台空白处本身透明）")]
        public float DimStrength = 1f;
        [Range(0f, 1f), Tooltip("四角暗角强度")]
        public float Vignette = 1f;
        [Range(0f, 4f), Tooltip("抗环带抖动幅度（1/255 单位），保持小")]
        public float Dither = 1.5f;

        private static TheatreSettings? _cached;
        public static TheatreSettings? Active()
        {
            // Only successful loads are cached: creating the asset mid-session takes effect immediately.
            if (_cached == null) _cached = Resources.Load<TheatreSettings>("Theatre/LineTheatreSettings");
            return _cached;
        }
    }
}
