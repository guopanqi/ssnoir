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
    // fullscreen, with a fixed enter/exit fade owned by the player. Scripts never
    // hand-write the transition. Subtitles float over the lower part of the stage,
    // at most two rows plus the name; longer lines are a content error.
    public sealed class TheatrePlayer : IDisposable
    {
        private const float EnterSeconds = 0.6f;
        private const float ExitSeconds = 0.5f;
        private const int MaxCaptionRows = 2;
        private const float MinCaptionScale = 0.7f;
        private static Texture2D? _vignette;
        private readonly Transform _owner;
        private readonly Font _captionFont = Resources.Load<Font>("Fonts/SourceHanSerifCN-Regular") ?? throw new InvalidOperationException("theatre caption font missing");
        private readonly Font _nameFont = Resources.Load<Font>("Fonts/SourceHanSerifCN-SemiBold") ?? throw new InvalidOperationException("theatre name font missing");
        private readonly Dictionary<string, AudioClip> _clips = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioSource> _loops = new(StringComparer.Ordinal);
        private readonly List<(AudioSource source, float volume)> _channels = new();
        private GameObject? _audio;
        private TheatreSurface? _surface;
        private TheatreSession? _session;
        private Action? _done;
        private float _age;
        private float _exitAge;
        private bool _exiting;
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
                if (scene.Width <= 0f || scene.Height <= 0f) throw new InvalidOperationException("theatre scene has no size");
                _surface = new TheatreSurface(scene);
                _audio = new GameObject("Line Theatre Audio"); _audio.transform.SetParent(_owner, false);
                _session = new TheatreSession(scene); _session.SoundRequested += Sound;
                _done = done;
                _age = 0f; _exitAge = 0f; _exiting = false;
                _session.Start(); _session.Tick(0f);
                Render();
            }
            catch { Reset(); throw; }
        }
        public void Update(float seconds)
        {
            if (_session == null) return;
            if (!_session.IsPaused)
            {
                _age += seconds;
                if (_exiting) _exitAge += seconds;
                else
                {
                    _session.Tick(seconds);
                    if (_session.IsComplete) { _exiting = true; _exitAge = 0f; }
                }
            }
            foreach (var channel in _channels) channel.source.volume = channel.volume * AudioVolumes.Sfx;
            if (_exiting && Fade() <= 0f) { Finish(); return; }
            if (!_exiting) Render();
        }
        public void Advance()
        {
            if (_session == null || _exiting) return;
            _session.Advance();
            if (_session.IsComplete) { _exiting = true; _exitAge = 0f; }
        }
        public void Pause(bool paused)
        {
            if (_session == null || _session.IsPaused == paused) return;
            _session.Pause(paused);
            foreach (var channel in _channels) { if (paused) channel.source.Pause(); else channel.source.UnPause(); }
        }
        private float Fade()
        {
            float enter = Mathf.Clamp01(_age / EnterSeconds);
            float exit = _exiting ? 1f - Mathf.Clamp01(_exitAge / ExitSeconds) : 1f;
            return Mathf.Min(enter, exit);
        }
        public void Draw()
        {
            if (_session == null || _surface == null) return;
            float fade = Fade();
            if (fade <= 0f) return;
            var stage = new Rect(0, 0, UIScale.VW, UIScale.VH);
            float dim = TheatreSettings.Active()?.DimStrength ?? 1f;
            // The city dims into the canvas; the fullscreen stage fades in above it.
            GUI.color = new Color(0f, 0f, 0f, fade * dim);
            GUI.DrawTexture(new Rect(0, 0, UIScale.VW, UIScale.VH), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, fade);
            GUI.DrawTexture(stage, _surface.Texture);
            // Static corner vignette, independent of the focus that follows the speaker.
            float vignette = TheatreSettings.Active()?.Vignette ?? 1f;
            GUI.color = new Color(0f, 0f, 0f, fade * vignette);
            GUI.DrawTexture(stage, Vignette());
            GUI.color = Color.white;
            DrawCaption(stage, fade);
            GUI.color = Color.white;
        }
        private static Texture2D Vignette()
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
                float alpha = Mathf.SmoothStep(0.4f, 1f, d) * 0.7f;
                texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
            texture.Apply(false, true);
            return _vignette = texture;
        }
        private void DrawCaption(Rect stage, float fade)
        {
            var line = _session!.Caption;
            if (line == null) return;
            float unit = stage.height / 900f;
            var text = new GUIStyle(IMGUIStyles.ModalBody) { font = _captionFont, alignment = TextAnchor.UpperLeft, wordWrap = false,
                fontStyle = FontStyle.Normal, padding = new RectOffset(), fontSize = IMGUIStyles.FontSize(Mathf.RoundToInt(36.8f * unit)) };
            text.normal.textColor = new Color(.945f, .925f, .878f);
            var name = new GUIStyle(text) { font = _nameFont,
                fontSize = IMGUIStyles.FontSize(Mathf.RoundToInt(22.4f * unit)) };
            name.normal.textColor = new Color(line.CaptionColor.R, line.CaptionColor.G, line.CaptionColor.B);
            float width = stage.width * .86f, left = stage.x + stage.width * .07f, lineHeight = 36.8f * unit * 1.55f;
            // Layout the complete sentence first, so revealing letters cannot reflow already visible text.
            // At most two rows plus the name; longer lines must be split by the author.
            var rows = LayoutRows(line.Text, text, width, 1f);
            float scale = 1f;
            while (rows.Count > MaxCaptionRows && scale > MinCaptionScale + 0.001f)
            {
                scale -= 0.05f;
                rows = LayoutRows(line.Text, text, width, scale);
            }
            if (rows.Count > MaxCaptionRows)
                throw new InvalidOperationException($"theatre caption exceeds {MaxCaptionRows} rows: " + line.Target);
            float nameHeight = 22.4f * unit * 1.55f;
            float bottom = stage.yMax - stage.height * .035f;
            float top = bottom - nameHeight - 8f * unit - rows.Count * lineHeight * scale;
            DrawSpaced(line.Target, left, top, width, nameHeight, name, .35f * 22.4f * unit, fade);
            int index = 0;
            foreach (string words in rows)
            {
                float rowWidth = WidthOf(words, text, scale);
                float x = left + (width - rowWidth) / 2f;
                foreach (char c in words)
                {
                    float glyph = WidthOf(c.ToString(), text, scale);
                    float alpha = _session.CaptionIsRevealed ? 1f : index < _session.VisibleCharacters ? Mathf.Clamp01((_session.CaptionTime - index * .048f) / .3f) : 0f;
                    DrawGlyph(new Rect(x, top + nameHeight + 8f * unit, glyph + 2f, lineHeight * scale), c.ToString(), text, alpha * fade);
                    x += glyph; index++;
                }
                top += lineHeight * scale;
            }
        }
        private static List<string> LayoutRows(string content, GUIStyle style, float width, float scale)
        {
            var rows = new List<string>(); string row = ""; float used = 0;
            foreach (char c in content)
            {
                float glyph = WidthOf(c.ToString(), style, scale);
                if (used + glyph > width && row.Length > 0) { rows.Add(row); row = ""; used = 0; }
                row += c; used += glyph;
            }
            rows.Add(row);
            return rows;
        }
        private static float WidthOf(string glyph, GUIStyle style, float scale)
            => style.CalcSize(new GUIContent(glyph)).x * scale;
        private static void DrawSpaced(string words, float left, float top, float width, float height, GUIStyle style, float spacing, float fade)
        {
            float total = style.CalcSize(new GUIContent(words)).x + spacing * (words.Length - 1);
            float x = left + (width - total) / 2f;
            foreach (char c in words)
            {
                float glyph = style.CalcSize(new GUIContent(c.ToString())).x;
                DrawGlyph(new Rect(x, top, glyph + 2f, height), c.ToString(), style, fade); x += glyph + spacing;
            }
        }
        private static void DrawGlyph(Rect rect, string glyph, GUIStyle style, float alpha)
        {
            var color = style.normal.textColor;
            style.normal.textColor = Color.black; GUI.color = new Color(1, 1, 1, alpha * .9f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 2, rect.width, rect.height), glyph, style);
            style.normal.textColor = color; GUI.color = new Color(1, 1, 1, alpha);
            GUI.Label(rect, glyph, style);
        }
        private void Render()
        {
            var settings = TheatreSettings.Active();
            _surface!.Render(_session!, Screen.width, Screen.height,
                settings?.ContentScale ?? 1f, settings?.BackgroundBlur ?? 1f, (settings?.Dither ?? 1.5f) / 255f);
        }
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
            _age = 0f; _exitAge = 0f; _exiting = false;
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
