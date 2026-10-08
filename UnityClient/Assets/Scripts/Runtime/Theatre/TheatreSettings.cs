#nullable enable
using UnityEngine;

namespace SSNoir.UnityTheatre
{
    // Stage tuning knobs, edited live in the Inspector during rehearsal.
    // Asset lives at Resources/Theatre/LineTheatreSettings (create once via SSNoir/剧场 menu).
    // Required presentation configuration; content timing stays in TheatreSession.
    public sealed class TheatreSettings : ScriptableObject
    {
        [Range(0.5f, 1.5f), Tooltip("内容缩放：1 当前大小，调小内容缩小（画布始终全屏，没有框）")]
        public float ContentScale = 1f;
        [Range(0f, .8f), Tooltip("背景焦点渐暗强度：0.5 对应 Claude 原型；人物与字幕不受影响")]
        public float FocusStrength = .5f;
        [Range(0f, 1f), Tooltip("画布压暗：1 纯黑画布，调小透出后面的城市（舞台空白处本身透明）")]
        public float DimStrength = 1f;
        [Range(0f, 1f), Tooltip("四角暗角强度")]
        public float Vignette = 1f;
        [Range(0f, 4f), Tooltip("抗环带抖动幅度（1/255 单位），保持小")]
        public float Dither = 1.5f;

        [Min(0)] public float EnterSeconds = .6f;
        [Min(0)] public float ExitSeconds = .5f;

        private static TheatreSettings? _cached;
        public static TheatreSettings Active()
        {
            // Only successful loads are cached: creating the asset mid-session takes effect immediately.
            if (_cached == null) _cached = Resources.Load<TheatreSettings>("Theatre/LineTheatreSettings");
            if (_cached == null) throw new System.InvalidOperationException("缺少 Theatre/LineTheatreSettings 舞台参数资产");
            RequireRange(_cached.ContentScale, .5f, 1.5f, nameof(ContentScale));
            RequireRange(_cached.FocusStrength, 0, .8f, nameof(FocusStrength));
            RequireRange(_cached.DimStrength, 0, 1, nameof(DimStrength));
            RequireRange(_cached.Vignette, 0, 1, nameof(Vignette));
            RequireRange(_cached.Dither, 0, 4, nameof(Dither));
            RequireRange(_cached.EnterSeconds, 0, 120, nameof(EnterSeconds));
            RequireRange(_cached.ExitSeconds, 0, 120, nameof(ExitSeconds));
            return _cached;
        }
        private static void RequireRange(float value, float min, float max, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new System.InvalidOperationException($"舞台参数 {name} 必须在 {min}..{max} 内");
        }
    }
}
