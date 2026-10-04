#nullable enable
using System;
using UnityEngine;

namespace SSNoir
{
    /// <summary>从当前配光状态过渡；途中换目标时从当前值重新开始。</summary>
    public sealed class CityEnvironmentTransition : MonoBehaviour
    {
        private Action<float>? _apply;
        private float _elapsed, _duration;
        public void Begin(float duration, Action<float> apply)
        {
            _apply = apply; _elapsed = 0; _duration = duration;
            if (!Application.isPlaying || duration <= 0) Complete();
            else apply(0);
        }
        private void Update()
        {
            if (_apply == null) return;
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            _apply(t * t * (3 - 2 * t));
            if (t >= 1) _apply = null;
        }
        public void Complete() { var apply = _apply; _apply = null; apply?.Invoke(1); }
        public void Cancel() => _apply = null;
    }
}
