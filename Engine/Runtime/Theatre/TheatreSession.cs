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
    public enum TheatrePhase { Entering, Playing, Exiting, Complete, Cancelled }
    public sealed class TheatreSoundState
    {
        public float Volume { get; internal set; }
        internal Runner? Owner;
        internal string Id = "";
        internal bool Loop;
    }

    public readonly struct TheatreAudioEvent
    {
        public TheatreCommand Command { get; }
        public int PlaybackId { get; }
        public TheatreAudioEvent(TheatreCommand command, int playbackId) { Command = command; PlaybackId = playbackId; }
    }
    // No Unity types or Scheme callbacks. Every action and transition consumes the same injected time.
    public sealed class TheatreSession
    {
        private readonly Dictionary<string, TheatreObjectState> _objects = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TheatreSoundState> _sounds = new(StringComparer.Ordinal);
        private readonly Dictionary<int, TheatreSoundState> _playbacks = new();
        private int _playbackId;
        private readonly Runner _root;
        private readonly float _enter, _exit;
        private float _phaseElapsed, _captionStarted;
        private bool _started, _revealed;
        internal Runner? Waiting;
        public IReadOnlyDictionary<string, TheatreObjectState> Objects => _objects;
        public IReadOnlyDictionary<string, TheatreSoundState> Sounds => _sounds;
        public IReadOnlyDictionary<int, TheatreSoundState> Playbacks => _playbacks;
        public TheatrePhase Phase { get; private set; } = TheatrePhase.Entering;
        public bool IsComplete => Phase == TheatrePhase.Complete || Phase == TheatrePhase.Cancelled;
        public bool IsPaused { get; private set; }
        public float Time { get; private set; }
        public float Fade => Phase == TheatrePhase.Entering ? (_enter == 0 ? 1 : _phaseElapsed / _enter)
            : Phase == TheatrePhase.Exiting ? (_exit == 0 ? 0 : 1 - _phaseElapsed / _exit) : IsComplete ? 0 : 1;
        public TheatreCommand? Line => Waiting?.Cue;
        public TheatreCommand? Caption { get; private set; }
        public bool CaptionIsRevealed => _revealed;
        public float CaptionTime => Time - _captionStarted;
        public int VisibleCharacters => Caption == null ? 0 : _revealed ? Caption.Text.Length : Math.Min(Caption.Text.Length, 1 + (int)(CaptionTime / .048f));
        public event Action<TheatreAudioEvent>? SoundRequested;
        public TheatreSession(TheatreScene scene, float enterSeconds = 0, float exitSeconds = 0)
        {
            if (!Finite(enterSeconds) || !Finite(exitSeconds) || enterSeconds < 0 || exitSeconds < 0) throw new ArgumentException("invalid theatre transition duration");
            _enter = enterSeconds; _exit = exitSeconds;
            foreach (var node in scene.Nodes) _objects.Add(node.Id, new TheatreObjectState(node));
            _root = new Runner(this, scene.Program);
        }
        public void Start()
        {
            if (_started) throw new InvalidOperationException("theatre session already started");
            _started = true; Tick(0);
        }
        public void Pause(bool paused) => IsPaused = paused;
        public void Advance()
        {
            if (IsPaused || Waiting == null || Phase != TheatrePhase.Playing) return;
            if (VisibleCharacters < Waiting.Cue.Text.Length) { _revealed = true; return; }
            Waiting.Acknowledged = true; Tick(0);
        }
        public void Tick(float seconds)
        {
            if (!Finite(seconds) || seconds < 0) throw new ArgumentException("theatre tick must be finite and nonnegative");
            if (!_started) throw new InvalidOperationException("theatre session has not started");
            if (IsComplete || IsPaused) return;
            float remaining = seconds;
            while (!IsComplete)
            {
                if (Phase == TheatrePhase.Playing)
                {
                    float left = _root.Step(remaining, Time);
                    Time += remaining - left;
                    if (!_root.Done) return;
                    _root.Cancel(); StopAllSounds(); Waiting = null;
                    Phase = TheatrePhase.Exiting; _phaseElapsed = 0; remaining = left;
                }
                else
                {
                    float duration = Phase == TheatrePhase.Entering ? _enter : _exit;
                    float step = Math.Min(remaining, Math.Max(0, duration - _phaseElapsed));
                    _phaseElapsed += step; Time += step; remaining -= step;
                    if (_phaseElapsed < duration) return;
                    Phase = Phase == TheatrePhase.Entering ? TheatrePhase.Playing : TheatrePhase.Complete;
                    _phaseElapsed = 0;
                    if (IsComplete) Caption = null;
                }
            }
        }
        public void Stop()
        {
            if (IsComplete) return;
            _root.Cancel(); StopAllSounds(); Waiting = null; Caption = null; Phase = TheatrePhase.Cancelled;
        }
        internal void CaptionStart(Runner runner, float time)
        {
            Caption = runner.Cue; _captionStarted = time; _revealed = false;
            if (runner.Cue.Seconds == 0)
            {
                if (Waiting != null) throw new InvalidOperationException("concurrent dialogue waits");
                Waiting = runner;
            }
        }
        internal void ClearCaption() => Caption = null;
        internal void Sound(Runner owner)
        {
            var c = owner.Cue;
            if (c.Kind == TheatreCommandKind.StopSound) { StopSound(c.Target); return; }
            if (_sounds.ContainsKey(c.Target)) throw new InvalidOperationException("sound id already looping: " + c.Target);
            var state = new TheatreSoundState { Volume = c.Volume, Owner = owner, Id = c.Target, Loop = c.Loop };
            if (c.Loop) _sounds.Add(c.Target, state);
            int playback = ++_playbackId; _playbacks.Add(playback, state);
            SoundRequested?.Invoke(new TheatreAudioEvent(c, playback));
        }
        internal void StopOwned(Runner owner)
        {
            var ids = new List<int>();
            foreach (var pair in _playbacks) if (pair.Value.Owner == owner) ids.Add(pair.Key);
            foreach (var id in ids) StopPlayback(id);
        }
        // Host reports natural one-shot completion; timing of the score never depends on audio duration.
        public void SoundFinished(int playback)
        {
            if (!_playbacks.TryGetValue(playback, out var state)) throw new InvalidOperationException("unknown audio playback: " + playback);
            if (state.Loop) throw new InvalidOperationException("loop cannot finish naturally");
            _playbacks.Remove(playback);
        }
        private void StopAllSounds()
        {
            foreach (var id in new List<int>(_playbacks.Keys)) StopPlayback(id);
        }
        private void StopSound(string id)
        {
            if (!_sounds.TryGetValue(id, out var state)) throw new InvalidOperationException("sound is not looping: " + id);
            foreach (var pair in _playbacks)
                if (pair.Value == state) { StopPlayback(pair.Key); return; }
            throw new InvalidOperationException("loop lost its playback: " + id);
        }
        private void StopPlayback(int playback)
        {
            var state = _playbacks[playback]; _playbacks.Remove(playback);
            if (state.Loop) _sounds.Remove(state.Id);
            SoundRequested?.Invoke(new TheatreAudioEvent(new TheatreCommand { Kind = TheatreCommandKind.StopSound, Target = state.Id }, playback));
        }
        internal float Volume(string id) => _sounds.TryGetValue(id, out var sound) ? sound.Volume : throw new InvalidOperationException("volume needs looping sound: " + id);
        internal void SetVolume(string id, float value) => _sounds[id].Volume = value;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // Each tree node owns its children and every audio playback it starts. Cancellation descends exactly once.
    internal sealed class Runner
    {
        private readonly TheatreSession _session;
        private readonly List<Runner> _children = new();
        private bool _started, _cancelled;
        private int _index, _iterations;
        private float _elapsed, _from;
        public TheatreCommand Cue { get; }
        public bool Done { get; private set; }
        public bool Acknowledged;
        public Runner(TheatreSession session, TheatreCommand cue) { _session = session; Cue = cue; }
        public float Step(float seconds, float now)
        {
            if (Done) return seconds;
            if (!_started)
            {
                _started = true;
                foreach (var child in Cue.Children) _children.Add(new Runner(_session, child));
                switch (Cue.Kind)
                {
                    case TheatreCommandKind.Animate: _from = _session.Objects[Cue.Target][Cue.Property]; break;
                    case TheatreCommandKind.SoundVolume: _from = _session.Volume(Cue.Target); break;
                    case TheatreCommandKind.Image: _session.Objects[Cue.Target].Asset = Cue.Asset; Done = true; break;
                    case TheatreCommandKind.Sound:
                    case TheatreCommandKind.StopSound: _session.Sound(this); Done = true; break;
                    case TheatreCommandKind.ClearCaption: _session.ClearCaption(); Done = true; break;
                    case TheatreCommandKind.Say: _session.CaptionStart(this, now); break;
                }
                if (Done) return seconds;
            }
            switch (Cue.Kind)
            {
                case TheatreCommandKind.Sequence:
                    float remaining = seconds;
                    while (_index < _children.Count)
                    {
                        var child = _children[_index]; float left = child.Step(remaining, now + seconds - remaining);
                        if (!child.Done) return 0;
                        remaining = left; _index++;
                    }
                    Done = true; return remaining;
                case TheatreCommandKind.Parallel:
                    float unused = seconds; bool complete = true;
                    foreach (var child in _children) { unused = Math.Min(unused, child.Step(seconds, now)); complete &= child.Done; }
                    Done = complete; return complete ? unused : 0;
                case TheatreCommandKind.During:
                    float rest = _children[0].Step(seconds, now);
                    float consumed = _children[0].Done ? seconds - rest : seconds;
                    for (int i = 1; i < _children.Count; i++) _children[i].Step(consumed, now);
                    if (!_children[0].Done) return 0;
                    foreach (var child in _children) child.Cancel(); Done = true; return rest;
                case TheatreCommandKind.Repeat:
                    float extra = seconds;
                    while (true)
                    {
                        float left = _children[0].Step(extra, now + seconds - extra);
                        if (!_children[0].Done) return 0;
                        _children[0].Cancel(); _iterations++;
                        if (Cue.RepeatCount != 0 && _iterations >= Cue.RepeatCount) { Done = true; return left; }
                        if (extra > 0 && left == extra) throw new InvalidOperationException("theatre repeat made no time progress; tick exceeds float resolution");
                        _children[0] = new Runner(_session, Cue.Children[0]); extra = left;
                        if (extra == 0) { _children[0].Step(0, now + seconds); return 0; }
                    }
                case TheatreCommandKind.Say:
                    if (Cue.Seconds == 0)
                    {
                        if (!Acknowledged) return 0;
                        _session.Waiting = null; Done = true; return seconds;
                    }
                    break;
            }
            float step = Math.Min(seconds, Math.Max(0, Cue.Seconds - _elapsed)); _elapsed += step;
            if (Cue.Kind == TheatreCommandKind.Animate || Cue.Kind == TheatreCommandKind.SoundVolume)
            {
                var keys = Cue.Keys; float value = keys[keys.Length - 1].Value;
                for (int i = 1; i < keys.Length; i++)
                {
                    if (_elapsed > keys[i].Time) continue;
                    float from = i == 1 && Cue.FromCurrent ? _from : keys[i - 1].Value;
                    float t = (_elapsed - keys[i - 1].Time) / (keys[i].Time - keys[i - 1].Time);
                    if (Cue.Smooth) t = t * t * (3 - 2 * t);
                    value = from + (keys[i].Value - from) * t; break;
                }
                if (Cue.Kind == TheatreCommandKind.SoundVolume) _session.SetVolume(Cue.Target, value);
                else _session.Objects[Cue.Target][Cue.Property] = value;
            }
            Done = _elapsed >= Cue.Seconds;
            return Done ? seconds - step : 0;
        }
        public void Cancel()
        {
            if (_cancelled) return;
            _cancelled = true;
            foreach (var child in _children) child.Cancel();
            _session.StopOwned(this);
            if (_session.Waiting == this) _session.Waiting = null;
            Done = true;
        }
    }
}
