#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir
{
    [CreateAssetMenu(menuName = "SSNoir/Narration Sequence", fileName = "NarrationSequence")]
    public sealed class NarrationSequence : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private AudioClip? audioClip;
        [SerializeField] private List<NarrationCue> subtitleCues = new List<NarrationCue>();

        public string Id => id;
        public AudioClip? AudioClip => audioClip;
        public IReadOnlyList<NarrationCue> SubtitleCues => subtitleCues;

        public float Duration
        {
            get
            {
                float duration = audioClip != null ? audioClip.length : 0f;
                if (subtitleCues.Count > 0)
                    duration = Mathf.Max(duration, subtitleCues[subtitleCues.Count - 1].Time + 3f);
                return Mathf.Max(duration, 0.1f);
            }
        }
    }

    [Serializable]
    public sealed class NarrationCue
    {
        [SerializeField] private float time;
        [SerializeField, TextArea(1, 3)] private string text = string.Empty;

        public float Time => time;
        public string Text => text;
    }
}
