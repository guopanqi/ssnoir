#nullable enable
using System;
using System.Collections.Generic;
using SSNoir.Theatre;
using SSNoir.IMGUI;
using UnityEngine;

namespace SSNoir.UnityTheatre
{
    // Unity host: assets, render surface, audio and subtitles. All timing belongs to TheatreSession.
    public sealed class TheatrePlayer : IDisposable
    {
        private readonly Transform _owner;
        private readonly Dictionary<string, AudioClip> _clips = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioSource> _loops = new(StringComparer.Ordinal);
        private readonly List<(AudioSource source, float volume)> _channels = new();
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
                // Preflight every asset before starting, including sounds in later beats.
                foreach (var beat in scene.Beats)
                    foreach (var command in beat.Commands)
                        if (command.Kind == TheatreCommandKind.Sound && !_clips.ContainsKey(command.Asset))
                            _clips.Add(command.Asset, Resources.Load<AudioClip>(command.Asset) ?? throw new InvalidOperationException("theatre sound missing: " + command.Asset));
                _surface = new TheatreSurface(scene);
                _audio = new GameObject("Line Theatre Audio"); _audio.transform.SetParent(_owner, false);
                _session = new TheatreSession(scene); _session.SoundRequested += Sound;
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
            foreach (var channel in _channels) channel.source.volume = channel.volume * AudioVolumes.Sfx;
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
            foreach (var channel in _channels) { if (paused) channel.source.Pause(); else channel.source.UnPause(); }
        }
        public void Draw()
        {
            if (_session == null || _surface == null) return;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, 0, UIScale.VW, UIScale.VH), _surface.Texture);
            var line = _session.Line;
            if (line == null) return;
            var band = new Rect(UIScale.VW * 0.08f, UIScale.VH - 112f, UIScale.VW * 0.84f, 102f);
            GUI.color = new Color(0.015f, 0.02f, 0.035f, 0.88f); GUI.DrawTexture(band, Texture2D.whiteTexture); GUI.color = Color.white;
            var name = new GUIStyle(IMGUIStyles.StatusLabel) { alignment = TextAnchor.MiddleCenter, fontSize = IMGUIStyles.FontSize(16) };
            var text = new GUIStyle(IMGUIStyles.ModalBody) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = IMGUIStyles.FontSize(24) };
            IMGUIStyles.DrawLabel(new Rect(band.x + 12, band.y + 4, band.width - 24, 24), line.Target, name);
            IMGUIStyles.DrawLabel(new Rect(band.x + 12, band.y + 30, band.width - 24, 64), line.Text.Substring(0, _session.VisibleCharacters), text);
        }
        private void Render() => _surface!.Render(_session!, Screen.width, Screen.height);
        private void Sound(TheatreCommand command)
        {
            if (command.Kind == TheatreCommandKind.StopSound) { _loops[command.Target].Stop(); _loops[command.Target].loop = false; _loops.Remove(command.Target); return; }
            AudioSource? source = null;
            foreach (var channel in _channels)
                if (!channel.source.isPlaying && !channel.source.loop) { source = channel.source; break; }
            if (source == null) source = _audio!.AddComponent<AudioSource>();
            _channels.RemoveAll(channel => channel.source == source);
            source.playOnAwake = false; source.spatialBlend = 0f;
            source.clip = _clips[command.Asset]; source.loop = command.Loop;
            source.panStereo = command.Pan; source.volume = command.Volume * AudioVolumes.Sfx;
            _channels.Add((source, command.Volume));
            if (command.Loop) _loops.Add(command.Target, source);
            source.Play();
        }
        private void Finish() { var done = _done; Reset(); done?.Invoke(); }
        public void Reset()
        {
            if (_session != null) _session.SoundRequested -= Sound;
            _session = null; _done = null;
            foreach (var channel in _channels) channel.source.Stop();
            _channels.Clear(); _loops.Clear(); _clips.Clear();
            if (_audio != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_audio);
                else UnityEngine.Object.DestroyImmediate(_audio);
                _audio = null;
            }
            _surface?.Dispose(); _surface = null;
        }
        public void Dispose() => Reset();
    }
}
