#nullable enable
using System;
using UnityEngine;

namespace SSNoir
{
    /// <summary>驱动配光或描线过渡；途中换目标时从当前值重新开始。</summary>
    public sealed class CityEnvironmentTransition : MonoBehaviour
    {
        private Action<float>? _apply;
        private float _elapsed, _duration;
        private bool _unscaled, _smooth;
        public void Begin(float duration, Action<float> apply, bool unscaled = false, bool smooth = true)
        {
            _apply = apply; _elapsed = 0; _duration = duration; _unscaled = unscaled; _smooth = smooth;
            if (!Application.isPlaying || duration <= 0) Complete();
            else apply(0);
        }
        private void Update() => Advance(_unscaled ? Time.unscaledDeltaTime : Time.deltaTime);
#if UNITY_EDITOR
        // 离屏录制只更换时钟，仍执行正式插值；必须先停自动更新，避免重复推进。
        public void AdvanceEditorPreview(float seconds)
        {
            if (enabled || !float.IsFinite(seconds) || seconds < 0)
                throw new InvalidOperationException("手动预览过渡必须关闭自动更新并提供非负步长");
            Advance(seconds);
        }
#endif
        private void Advance(float seconds)
        {
            if (_apply == null) return;
            _elapsed += seconds;
            float t = Mathf.Clamp01(_elapsed / _duration);
            _apply(_smooth ? t * t * (3 - 2 * t) : t);
            if (t >= 1) _apply = null;
        }
        public void Complete() { var apply = _apply; _apply = null; apply?.Invoke(1); }
        public void Cancel() => _apply = null;
    }
}
