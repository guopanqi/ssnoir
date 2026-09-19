#nullable enable
using UnityEngine;

namespace SSNoir
{
    /// <summary>
    /// 城市配乐：只认全局键 <c>音乐</c>（家里唱片机放的那张）。值是 Resources/Music/ 下的 clip 名，
    /// #f 就是安静。每次快照落地时由 <see cref="SSNoirGameManager"/> 喂一次当前值，这里只做
    /// "变了就交叉淡入淡出"——读档、新游戏、抬起唱针都走同一条路，没有第二种状态来源。
    ///
    /// 两个 AudioSource 轮换：旧的淡出、新的淡入，同一时间最多两条在响。
    /// </summary>
    public class AmbientMusic : MonoBehaviour
    {
        private const string ResourcesFolder = "Music/";
        private const float FadeSeconds = 1.6f;
        private const float Volume = 0.55f;

        private AudioSource _a = null!;
        private AudioSource _b = null!;
        private AudioSource _current = null!;
        private string? _currentId;
        private float _fade = 1f;

        private void Awake()
        {
            _a = gameObject.AddComponent<AudioSource>();
            _b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { _a, _b })
            {
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.volume = 0f;
            }
            _current = _a;
        }

        /// <summary>把当前应当响着的曲子告诉它；同一个值重复喂不会重头播。</summary>
        public void Apply(string? clipId)
        {
            if (string.IsNullOrEmpty(clipId))
                clipId = null;
            if (string.Equals(clipId, _currentId, System.StringComparison.Ordinal))
                return;

            var next = _current == _a ? _b : _a;
            next.Stop();
            next.clip = null;
            if (clipId != null)
            {
                var clip = Resources.Load<AudioClip>(ResourcesFolder + clipId);
                if (clip == null)
                {
                    string message = $"[SSNoir] 全局键 音乐 = '{clipId}'，但 Resources/{ResourcesFolder}{clipId} 不存在。";
                    Debug.LogError(message);
                    throw new System.InvalidOperationException(message);
                }
                next.clip = clip;
                next.volume = 0f;
                next.Play();
            }

            _current = next;
            _currentId = clipId;
            _fade = 0f;
        }

        private void Update()
        {
            if (_fade >= 1f)
                return;
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / FadeSeconds);
            var previous = _current == _a ? _b : _a;
            _current.volume = _current.clip != null ? Volume * _fade : 0f;
            previous.volume = Volume * (1f - _fade);
            if (_fade >= 1f && previous.isPlaying)
            {
                previous.Stop();
                previous.clip = null;
            }
        }
    }
}
