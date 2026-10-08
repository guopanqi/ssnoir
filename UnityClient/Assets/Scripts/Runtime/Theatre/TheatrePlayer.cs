#nullable enable
using System;
using System.Collections.Generic;
using SSNoir.Theatre;
using SSNoir.IMGUI;
using UnityEngine;

namespace SSNoir.UnityTheatre
{
    // Unity host: assets, render surface, audio and subtitles. All timing belongs to TheatreSession.
    //
    // Stage presentation: the city dims to black as the canvas and the line stage plays
    // fullscreen, with enter/exit phases owned by TheatreSession. Scripts never
    // hand-write the transition. Subtitles float over the lower part of the stage,
    // at most two rows plus the name; longer lines are a content error.
    public sealed class TheatrePlayer : IDisposable
    {
        private Texture2D? _vignette;
        private Color _background;
        private readonly Transform _owner;
        private readonly TheatreCaptions _captions = new();
        private readonly Dictionary<string, AudioClip> _clips = new(StringComparer.Ordinal);
        private readonly Dictionary<int, AudioSource> _sources = new();
        private readonly List<AudioSource> _pool = new();
        private GameObject? _audio;
        private TheatreSurface? _surface;
        private TheatreSession? _session;
        private Action? _done;
        public bool IsActive => _session != null;
        public TheatrePlayer(Transform owner) { _owner = owner; }
        public void Start(TheatreScene scene, Action done)
        {
            if (IsActive) throw new InvalidOperationException("theatre already playing");
            try
            {
                // Preflight every asset before starting, including sounds in nested branches.
                foreach (var command in scene.Commands)
                    if (command.Kind == TheatreCommandKind.Sound && !_clips.ContainsKey(command.Asset))
                        _clips.Add(command.Asset, Resources.Load<AudioClip>(command.Asset) ?? throw new InvalidOperationException("theatre sound missing: " + command.Asset));
                if (scene.Width <= 0f || scene.Height <= 0f) throw new InvalidOperationException("theatre scene has no size");
                _background = new Color(scene.Background.R, scene.Background.G, scene.Background.B, scene.Background.A);
                if (QualitySettings.activeColorSpace == ColorSpace.Linear) _background = _background.linear;
                _surface = new TheatreSurface(scene);
                _audio = new GameObject("Line Theatre Audio"); _audio.transform.SetParent(_owner, false);
                var settings = TheatreSettings.Active();
                _session = new TheatreSession(scene, settings.EnterSeconds, settings.ExitSeconds); _session.SoundRequested += Sound;
                _done = done;
                _session.Start(); _session.Tick(0f);
                Render();
            }
            catch { Reset(); throw; }
        }
        public void Update(float seconds)
        {
            if (_session == null) return;
            _session.Tick(seconds);
            foreach (int id in new List<int>(_sources.Keys))
            {
                var source = _sources[id];
                source.volume = _session.Playbacks[id].Volume * AudioVolumes.Sfx;
                if (!_session.IsPaused && !source.loop && !source.isPlaying)
                { _sources.Remove(id); _session.SoundFinished(id); }
            }
            if (_session.IsComplete) { Finish(); return; }
            Render();
        }
        public void Advance()
        {
            if (_session == null) return;
            _session.Advance();
            if (_session.IsComplete) Finish();
        }
        public void Pause(bool paused)
        {
            if (_session == null || _session.IsPaused == paused) return;
            _session.Pause(paused);
            foreach (var source in _sources.Values) { if (paused) source.Pause(); else source.UnPause(); }
        }
        public void Draw()
        {
            if (_session == null || _surface == null) return;
            float fade = _session.Fade;
            if (fade <= 0f) return;
            var stage = new Rect(0, 0, UIScale.VW, UIScale.VH);
            float dim = TheatreSettings.Active().DimStrength;
            // The city dims into the canvas; the fullscreen stage fades in above it.
            GUI.color = new Color(_background.r, _background.g, _background.b, fade * dim * _background.a);
            GUI.DrawTexture(new Rect(0, 0, UIScale.VW, UIScale.VH), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, fade);
            GUI.DrawTexture(stage, _surface.Texture);
            // Static corner vignette, independent of the focus that follows the speaker.
            float vignette = TheatreSettings.Active().Vignette;
            GUI.color = new Color(0f, 0f, 0f, fade * vignette);
            GUI.DrawTexture(stage, Vignette());
            GUI.color = Color.white;
            _captions.Draw(_session, stage, fade);
            GUI.color = Color.white;
        }
        private Texture2D Vignette()
        {
            if (_vignette != null) return _vignette;
            const int side = 128;
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            float corner = Mathf.Sqrt(2f);
            for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                float dx = (x + 0.5f - side / 2f) / (side / 2f);
                float dy = (y + 0.5f - side / 2f) / (side / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) / corner;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.4f, 1f, d)) * 0.7f;
                texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
            texture.Apply(false, true);
            return _vignette = texture;
        }
        private void Render()
        {
            var settings = TheatreSettings.Active();
            _surface!.Render(_session!, Screen.width, Screen.height,
                settings.ContentScale, settings.FocusStrength, settings.Dither / 255f);
        }
        private void Sound(TheatreAudioEvent audio)
        {
            var command = audio.Command;
            if (command.Kind == TheatreCommandKind.StopSound)
            {
                var stopped = _sources[audio.PlaybackId]; stopped.Stop(); stopped.loop = false; _sources.Remove(audio.PlaybackId); return;
            }
            AudioSource? source = null;
            foreach (var candidate in _pool)
                if (!_sources.ContainsValue(candidate)) { source = candidate; break; }
            if (source == null) { source = _audio!.AddComponent<AudioSource>(); _pool.Add(source); }
            source.playOnAwake = false; source.spatialBlend = 0;
            source.clip = _clips[command.Asset]; source.loop = command.Loop;
            source.panStereo = command.Pan; source.volume = command.Volume * AudioVolumes.Sfx;
            _sources.Add(audio.PlaybackId, source); source.Play();
        }
        private void Finish() { var done = _done; Reset(); done?.Invoke(); }
        public void Reset()
        {
            if (_session != null) { _session.Stop(); _session.SoundRequested -= Sound; }
            _session = null; _done = null;
            foreach (var source in _pool) source.Stop();
            _pool.Clear(); _sources.Clear(); _clips.Clear();
            if (_audio != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_audio);
                else UnityEngine.Object.DestroyImmediate(_audio);
                _audio = null;
            }
            _surface?.Dispose(); _surface = null;
            if (_vignette != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_vignette); else UnityEngine.Object.DestroyImmediate(_vignette);
                _vignette = null;
            }
        }
        public void Dispose() => Reset();
    }
}
