#nullable enable
using UnityEngine;

namespace SSNoir
{
    // 对话语音轨。刻意与 UnityNarrationPlayer 分开:角色语音和旁白可以同时响,不能互相打断。
    // v1:从 Resources/Voices/{voiceId} 加载音频,缺失则静默跳过。
    public sealed class DialogueVoicePlayer : MonoBehaviour
    {
        private AudioSource _audioSource = null!;

        private void Awake() => EnsureAudioSource();

        // 返回实际开始播放的时长。调用方用它延长非阻塞对白的可见时间，
        // 使生成语音不会被下一句抢断；无语音或资源缺失时返回 0。
        public float Play(string? voiceId)
        {
            if (string.IsNullOrWhiteSpace(voiceId))
                return 0f;

            EnsureAudioSource();
            var clip = Resources.Load<AudioClip>($"Voices/{voiceId}");
            if (clip == null)
            {
                Debug.LogWarning($"[SSNoir] Dialogue voice not found: Resources/Voices/{voiceId}");
                return 0f;
            }

            _audioSource.Stop();
            _audioSource.clip = clip;
            _audioSource.Play();
            return clip.length;
        }

        private void EnsureAudioSource()
        {
            if (_audioSource != null)
                return;
            // 自己加一个，不 GetComponent：同一个 GameObject 上还有 AmbientMusic 的两条音轨，
            // 拿到它们的话语音会被它每帧压到零音量。
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }
    }
}
