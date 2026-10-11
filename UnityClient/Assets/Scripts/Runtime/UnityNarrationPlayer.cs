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
        private int _playingCue = -1;
        private AudioClip[] _cueClips = Array.Empty<AudioClip>();

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
            Stop();

            var jsonAsset = Resources.Load<TextAsset>($"Narrations/{id}");
            if (jsonAsset != null)
            {
                PlayJsonNarration(id, jsonAsset);
                return;
            }

            var clip = Resources.Load<AudioClip>($"Narrations/{id}");
            if (clip != null)
            {
                PlayAudioOnly(id, clip);
                return;
            }

            Debug.LogWarning($"[SSNoir] Narration asset not found: Resources/Narrations/{id}");
            PlayFallback(id);
        }

        // 黑幕使用旁白音轨，字幕由电影黑幕自身显示。
        public float PlayIntertitleAudio(string id)
        {
            EnsureAudioSource();
            Stop();
            var clip = Resources.Load<AudioClip>($"Narrations/{id}")
                ?? throw new InvalidOperationException($"Intertitle audio not found: Narrations/{id}");
            _audioSource.clip = clip;
            _audioSource.volume = AudioVolumes.Dialogue;
            _audioSource.Play();
            return clip.length;
        }

        private void Update()
        {
            EnsureAudioSource();
            _audioSource.volume = AudioVolumes.Dialogue;

            if (_activeNarration == null && string.IsNullOrEmpty(_fallbackSubtitle))
                return;

            _elapsed += Time.unscaledDeltaTime;
            if (_activeNarration != null && _cueClips.Length > 0)
            {
                int cue = -1;
                for (int i = 0; i < _activeNarration.cues.Count; i++)
                    if (_elapsed >= _activeNarration.cues[i].time) cue = i;
                if (cue != _playingCue && cue >= 0)
                {
                    _playingCue = cue;
                    _audioSource.Stop();
                    _audioSource.clip = _cueClips[cue];
                    _audioSource.Play();
                }
            }
            if (_elapsed >= _duration)
                Stop();
        }

        private void PlayJsonNarration(string id, TextAsset jsonAsset)
        {
            var narration = JsonUtility.FromJson<NarrationJson>(jsonAsset.text);
            if (narration == null)
                throw new InvalidOperationException($"Invalid narration json: Resources/Narrations/{id}.json");

            if (string.IsNullOrWhiteSpace(narration.id))
                narration.id = id;

            // 空 audio 明确表示纯字幕 narration，不查找或播放旧配音。
            var clip = string.IsNullOrWhiteSpace(narration.audio) ? null : Resources.Load<AudioClip>($"Narrations/{narration.audio}");
            _cueClips = Array.Empty<AudioClip>();
            _playingCue = -1;
            bool voicedCues = narration.cues.Exists(cue => !string.IsNullOrEmpty(cue.audio));
            if (voicedCues)
            {
                if (!string.IsNullOrEmpty(narration.audio))
                    throw new InvalidOperationException($"Narration {id} cannot mix sequence audio with cue audio");
                _cueClips = new AudioClip[narration.cues.Count];
                for (int i = 0; i < _cueClips.Length; i++)
                {
                    string cueId = narration.cues[i].audio;
                    if (string.IsNullOrWhiteSpace(cueId)) throw new InvalidOperationException($"Narration {id} cue {i} has no audio");
                    _cueClips[i] = Resources.Load<AudioClip>($"Narrations/{cueId}")
                        ?? throw new InvalidOperationException($"Narration cue audio not found: {cueId}");
                    if (i > 0 && narration.cues[i].time < narration.cues[i - 1].time + _cueClips[i - 1].length)
                        throw new InvalidOperationException($"Narration {id} has overlapping voiced cues");
                }
            }
            _activeNarration = narration;
            _fallbackSubtitle = string.Empty;
            _elapsed = 0f;
            _duration = _cueClips.Length == 0 ? ResolveDuration(narration, clip)
                : narration.cues[narration.cues.Count - 1].time + _cueClips[_cueClips.Length - 1].length;

            if (clip != null)
            {
                _audioSource.Stop();
                _audioSource.clip = clip;
                _audioSource.volume = AudioVolumes.Dialogue;
                _audioSource.Play();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(narration.audio))
                    throw new InvalidOperationException($"Narration audio not found: Resources/Narrations/{narration.audio}");
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

        public void Stop()
        {
            _activeNarration = null;
            _cueClips = Array.Empty<AudioClip>();
            _playingCue = -1;
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
                duration = Mathf.Max(duration, narration.cues[narration.cues.Count - 1].time + Mathf.Max(3f, narration.cues[narration.cues.Count - 1].text.Length * .16f));
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

            // 旁白独占音轨，不能拿到 AmbientMusic 或对白的 AudioSource。
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0;
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
            public string audio = string.Empty;
            public string text = string.Empty;
            public string textEn = string.Empty;
        }
    }
}
