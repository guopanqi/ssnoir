#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Theatre
{
    public sealed class TheatreObjectState
    {
        private readonly float[] _values = { 0f, 0f, 1f, 1f, 0f, 1f, 1f, 1f };
        public TheatreNode Node { get; }
        public string Asset { get; internal set; }
        public TheatreObjectState(TheatreNode node)
        {
            Node = node; Asset = node.Asset;
            foreach (var pair in node.Initial) _values[(int)pair.Key] = pair.Value;
        }
        public float this[TheatreProperty property] { get => _values[(int)property]; internal set => _values[(int)property] = value; }
    }

    // Owns the only presentation clock. Tick is injected by the host; rendering reads state only.
    public sealed class TheatreSession
    {
        private readonly TheatreScene _scene;
        private readonly Dictionary<string, TheatreObjectState> _objects = new(StringComparer.Ordinal);
        private readonly Dictionary<TheatreCommand, float> _starts = new();
        private readonly HashSet<TheatreCommand> _firedSounds = new();
        private int _beat;
        private float _elapsed, _captionStarted;
        private bool _revealed, _started;
        public IReadOnlyDictionary<string, TheatreObjectState> Objects => _objects;
        public bool IsComplete { get; private set; }
        public bool IsPaused { get; private set; }
        public float Time { get; private set; }
        public float BeatTime => _elapsed;
        public int BeatIndex => _beat;
        public TheatreCommand? Line { get; private set; }
        public TheatreCommand? Caption { get; private set; }
        public bool CaptionIsRevealed => _revealed;
        public float CaptionTime => Time - _captionStarted;
        public int VisibleCharacters => Caption == null ? 0 : _revealed ? Caption.Text.Length : Math.Min(Caption.Text.Length, 1 + (int)(CaptionTime / 0.048f));
        public event Action<TheatreCommand>? SoundRequested;

        public TheatreSession(TheatreScene scene)
        {
            _scene = scene;
            foreach (var node in scene.Nodes) _objects.Add(node.Id, new TheatreObjectState(node));
        }
        // Separate from construction so hosts can load resources and subscribe before events fire.
        public void Start()
        {
            if (_started) throw new InvalidOperationException("theatre session already started");
            _started = true;
            BeginBeat();
        }
        public void Pause(bool paused) => IsPaused = paused;
        public void Tick(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) throw new ArgumentException("theatre tick must be finite and nonnegative");
            if (!_started) throw new InvalidOperationException("theatre session has not started");
            if (IsComplete || IsPaused) return;
            // Carry over overshoot, including zero-duration beats; frame rate cannot stretch choreography.
            float remaining = seconds;
            while (!IsComplete)
            {
                var beat = _scene.Beats[_beat];
                bool waiting = Line != null && Line.Seconds == 0f;
                float step = waiting ? remaining : Math.Min(remaining, Math.Max(0f, beat.Duration - _elapsed));
                _elapsed += step; Time += step; remaining -= step;
                ApplyAnimations();
                if (waiting || _elapsed < beat.Duration) break;
                NextBeat();
                if (remaining <= 0f && !IsComplete && _scene.Beats[_beat].Duration > 0f) break;
            }
        }
        public void Advance()
        {
            if (IsComplete || IsPaused || Line == null || Line.Seconds > 0f) return;
            if (VisibleCharacters < Line.Text.Length) { _revealed = true; return; }
            if (_elapsed < _scene.Beats[_beat].Duration) return;
            NextBeat();
            Tick(0f);
        }
        private void NextBeat()
        {
            _beat++;
            if (_beat == _scene.Beats.Count) { IsComplete = true; Line = null; Caption = null; return; }
            BeginBeat();
        }
        private void BeginBeat()
        {
            _elapsed = 0f; Line = null; _starts.Clear(); _firedSounds.Clear();
            foreach (var command in _scene.Beats[_beat].Commands)
            {
                if (command.Kind == TheatreCommandKind.Animate)
                    _starts.Add(command, _objects[command.Target][command.Property]);
                else if (command.Kind == TheatreCommandKind.Image) _objects[command.Target].Asset = command.Asset;
                else if (command.Kind == TheatreCommandKind.Say)
                { Line = command; Caption = command; _captionStarted = Time; _revealed = false; }
                else if (command.Kind == TheatreCommandKind.ClearCaption) Caption = null;

            }
            ApplyAnimations();
        }
        private void ApplyAnimations()
        {
            foreach (var command in _scene.Beats[_beat].Commands)
                if ((command.Kind == TheatreCommandKind.Sound || command.Kind == TheatreCommandKind.StopSound) && _elapsed >= command.Delay && _firedSounds.Add(command))
                    SoundRequested?.Invoke(command);
            foreach (var pair in _starts)
            {
                var command = pair.Key;
                var keys = command.Keys;
                float value = keys[keys.Length - 1].Value;
                for (int i = 1; i < keys.Length; i++)
                {
                    if (_elapsed > keys[i].Time) continue;
                    float from = i == 1 && command.FromCurrent ? pair.Value : keys[i - 1].Value;
                    float t = (_elapsed - keys[i - 1].Time) / (keys[i].Time - keys[i - 1].Time);
                    value = from + (keys[i].Value - from) * t;
                    break;
                }
                _objects[command.Target][command.Property] = value;
            }
        }
    }
}
