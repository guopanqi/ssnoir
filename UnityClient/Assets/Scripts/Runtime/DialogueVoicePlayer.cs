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

        public void Play(string? voiceId)
        {
            if (string.IsNullOrWhiteSpace(voiceId))
                return;

            EnsureAudioSource();
            var clip = Resources.Load<AudioClip>($"Voices/{voiceId}");
            if (clip == null)
            {
                Debug.LogWarning($"[SSNoir] Dialogue voice not found: Resources/Voices/{voiceId}");
                return;
            }

            _audioSource.Stop();
            _audioSource.clip = clip;
            _audioSource.Play();
        }

        private void EnsureAudioSource()
        {
            if (_audioSource != null)
                return;
            _audioSource = gameObject.GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
    }
}
