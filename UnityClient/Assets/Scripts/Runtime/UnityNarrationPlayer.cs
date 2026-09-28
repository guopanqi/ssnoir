#nullable enable
using UnityEngine;
using System;
using System.Collections.Generic;
using SSNoir.Core;

namespace SSNoir
{
    public sealed class UnityNarrationPlayer : MonoBehaviour
    {
        private const float FallbackDuration = 4f;

        private AudioSource _audioSource = null!;
        private NarrationJson? _activeNarration;
        private string _fallbackSubtitle = string.Empty;
        private float _elapsed;
        private float _duration;

        public string CurrentSubtitle
        {
            get
            {
                if (_activeNarration != null)
                    return ResolveNarrationSubtitle(_activeNarration, _elapsed);
                return _fallbackSubtitle;
            }
        }

        private void Awake()
        {
            EnsureAudioSource();
        }

        public void Play(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new System.ArgumentException("narration id cannot be empty");

            EnsureAudioSource();

            var jsonAsset = Resources.Load<TextAsset>($"Narrations/{id}");
            var clip = Resources.Load<AudioClip>($"Narrations/{id}");
            if (jsonAsset != null)
            {
                PlayJsonNarration(id, jsonAsset, clip);
                return;
            }

            if (clip != null)
            {
                PlayAudioOnly(id, clip);
                return;
            }

            Debug.LogWarning($"[SSNoir] Narration asset not found: Resources/Narrations/{id}");
            PlayFallback(id);
        }

        private void Update()
        {
            if (_audioSource != null)
                _audioSource.volume = AudioVolumes.Dialogue;

            if (_activeNarration == null && string.IsNullOrEmpty(_fallbackSubtitle))
                return;

            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration)
                Stop();
        }

        private void PlayJsonNarration(string id, TextAsset jsonAsset, AudioClip? clip)
        {
            var narration = JsonUtility.FromJson<NarrationJson>(jsonAsset.text);
            if (narration == null)
                throw new InvalidOperationException($"Invalid narration json: Resources/Narrations/{id}.json");

            if (string.IsNullOrWhiteSpace(narration.id))
                narration.id = id;

            _activeNarration = narration;
            _fallbackSubtitle = string.Empty;
            _elapsed = 0f;
            _duration = ResolveDuration(narration, clip);

            if (clip != null)
            {
                _audioSource.Stop();
                _audioSource.clip = clip;
                _audioSource.volume = AudioVolumes.Dialogue;
                _audioSource.Play();
            }
            else
            {
                Debug.LogWarning($"[SSNoir] Narration audio not found: Resources/Narrations/{id}");
                _audioSource.Stop();
                _audioSource.clip = null;
            }
        }

        private void PlayAudioOnly(string id, AudioClip clip)
        {
            _activeNarration = null;
            _fallbackSubtitle = id;
            _elapsed = 0f;
            _duration = Mathf.Max(clip.length, FallbackDuration);

            _audioSource.Stop();
            _audioSource.clip = clip;
            _audioSource.volume = AudioVolumes.Dialogue;
            _audioSource.Play();
        }

        private void PlayFallback(string id)
        {
            _activeNarration = null;
            _fallbackSubtitle = id;
            _elapsed = 0f;
            _duration = FallbackDuration;

            _audioSource.Stop();
            _audioSource.clip = null;
        }

        private void Stop()
        {
            _activeNarration = null;
            _fallbackSubtitle = string.Empty;
            _elapsed = 0f;
            _duration = 0f;
            _audioSource.Stop();
            _audioSource.clip = null;
        }

        private static float ResolveDuration(NarrationJson narration, AudioClip? clip)
        {
            float duration = clip != null ? clip.length : 0f;
            if (narration.cues != null && narration.cues.Count > 0)
                duration = Mathf.Max(duration, narration.cues[narration.cues.Count - 1].time + 3f);
            return Mathf.Max(duration, FallbackDuration);
        }

        private static string ResolveNarrationSubtitle(NarrationJson narration, float time)
        {
            var cues = narration.cues;
            if (cues == null || cues.Count == 0)
                throw new InvalidOperationException($"Narration '{narration.id}' has no subtitle cues");

            string text = string.Empty;
            for (int i = 0; i < cues.Count; i++)
            {
                if (time < cues[i].time)
                    break;
                text = GameLanguage.Current == GameLanguage.English
                    ? !string.IsNullOrWhiteSpace(cues[i].textEn)
                        ? cues[i].textEn
                        : throw new InvalidOperationException($"Narration '{narration.id}' cue {i} is missing English text")
                    : cues[i].text;
            }
            return text;
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

        [Serializable]
        private sealed class NarrationJson
        {
            public string id = string.Empty;
            public string audio = string.Empty;
            public List<NarrationJsonCue> cues = new List<NarrationJsonCue>();
        }

        [Serializable]
        private sealed class NarrationJsonCue
        {
            public float time = 0f;
            public string text = string.Empty;
            public string textEn = string.Empty;
        }
    }
}
