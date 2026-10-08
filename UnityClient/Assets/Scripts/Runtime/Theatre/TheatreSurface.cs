#nullable enable
using System;
using System.Collections.Generic;
using SSNoir.Theatre;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSNoir.UnityTheatre
{
    // Self-contained surface. Never changes the world's cameras, layers, lights or post-processing.
    public sealed class TheatreSurface : IDisposable
    {
        private readonly TheatreScene _scene;
        private readonly Dictionary<string, Mesh> _meshes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> _images = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Matrix4x4> _matrices = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _opacity = new(StringComparer.Ordinal), _brightness = new(StringComparer.Ordinal);
        private readonly Material _material;
        private readonly CommandBuffer _commands = new() { name = "Line Theatre" };
        private readonly MaterialPropertyBlock _properties = new();
        private RenderTexture? _target, _display;
        private bool _disposed;
        private readonly Vector4[] _spots = new Vector4[8], _spotColors = new Vector4[8];
        public RenderTexture Texture => _display ?? throw new InvalidOperationException("surface has not rendered");
        public TheatreSurface(TheatreScene scene)
        {
            _scene = scene;
            var shader = Resources.Load<Shader>("Theatre/LineTheatre") ?? throw new InvalidOperationException("theatre shader missing");
            _material = new Material(shader);
            try
            {
                foreach (var node in scene.Nodes)
                {
                    if (node.Shape == TheatreShape.Image)
                        LoadImage(node.Asset);
                    if (node.Shape != TheatreShape.Group && node.Shape != TheatreShape.Light) _meshes.Add(node.Id, TheatreMesh.Build(node));
                }
                foreach (var command in scene.Commands)
                        if (command.Kind == TheatreCommandKind.Image) LoadImage(command.Asset);
            }
            catch { Dispose(); throw; }
        }
        private void LoadImage(string asset)
        {
            if (!_images.ContainsKey(asset))
                _images.Add(asset, Resources.Load<Texture2D>(asset) ?? throw new InvalidOperationException("theatre image missing: " + asset));
        }
        public void Render(TheatreSession session, int width, int height, float contentScale = 1f, float focusStrength = .45f, float dither = 1.5f / 255f)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TheatreSurface));
            width = Math.Max(1, width); height = Math.Max(1, height);
            if (_target == null || _target.width != width || _target.height != height)
            {
                if (_target != null) { _target.Release(); Release(_target); }
                if (_display != null) { _display.Release(); Release(_display); }
                _target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) { name = "Line Theatre", filterMode = FilterMode.Bilinear };
                _target.Create();
                _display = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = "Line Theatre Display", filterMode = FilterMode.Bilinear };
                _display.Create();
            }
            _matrices.Clear(); _opacity.Clear(); _brightness.Clear();
            foreach (var node in _scene.Nodes)
            {
                var state = session.Objects[node.Id];
                var local = Matrix4x4.TRS(new Vector3(state[TheatreProperty.X], state[TheatreProperty.Y], 0),
                    Quaternion.Euler(0, 0, state[TheatreProperty.Rotation]), new Vector3(state[TheatreProperty.ScaleX], state[TheatreProperty.ScaleY], 1));
                bool root = node.Parent.Length == 0;
                _matrices[node.Id] = root ? local : _matrices[node.Parent] * local;
                _opacity[node.Id] = state[TheatreProperty.Opacity] * (root ? 1f : _opacity[node.Parent]);
                _brightness[node.Id] = state[TheatreProperty.Brightness] * (root ? 1f : _brightness[node.Parent]);
            }
            int spotCount = 0;
            foreach (var node in _scene.Nodes)
            {
                if (node.Shape != TheatreShape.Spotlight) continue;
                if (spotCount == _spots.Length) throw new InvalidOperationException("theatre supports at most 8 spotlights");
                var center = _matrices[node.Id].MultiplyPoint3x4(Vector3.zero);
                _spots[spotCount] = new Vector4(center.x, center.y, node.Width, node.Height);
                _spotColors[spotCount] = new Vector4(node.Color.R, node.Color.G, node.Color.B, _brightness[node.Id] * _opacity[node.Id]);
                spotCount++;
            }
            float viewHeight = _scene.Height / Math.Max(0.01f, contentScale);
            float viewWidth = viewHeight * width / height;
            float centerX = _scene.Width * 0.5f, centerY = _scene.Height * 0.5f;
            float left = centerX - viewWidth * 0.5f, top = centerY - viewHeight * 0.5f;
            var projection = GL.GetGPUProjectionMatrix(
                Matrix4x4.Ortho(left, left + viewWidth, top + viewHeight, top, -1, 1), true);
            _commands.Clear(); _commands.SetRenderTarget(_target);
            // Artistic 2D composition uses display-space colors, matching SVG/CSS.
            // Resolve premultiplied alpha and convert to the Unity project color space once.
            _commands.ClearRenderTarget(false, true, Color.clear);
            // Shader uses explicit scene coordinates; no camera matrices or SRP globals are changed.
            foreach (var node in _scene.Nodes)
            {
                if (!_meshes.TryGetValue(node.Id, out var mesh)) continue;
                _properties.Clear();
                _properties.SetMatrix("_SceneTransform", _matrices[node.Id]);
                _properties.SetMatrix("_SceneToClip", projection);
                float brightness = _brightness[node.Id];
                var tint = ColorOf(node.Color); tint.a *= _opacity[node.Id];
                if (node.Shape == TheatreShape.Glow || node.Shape == TheatreShape.Spotlight || (node.Shape == TheatreShape.Polygon && node.Color.A < 1f)) tint.a *= Mathf.Clamp01(brightness);
                tint.r *= brightness; tint.g *= brightness; tint.b *= brightness;
                _properties.SetColor("_Tint", tint);
                _properties.SetFloat("_Mode", node.Shape == TheatreShape.Spotlight ? 5 : node.Shape == TheatreShape.Focus ? 4 : node.Shape == TheatreShape.Line ? 0 : node.Shape == TheatreShape.Polygon ? 1 : node.Shape == TheatreShape.Glow ? 2 : 3);
                _properties.SetFloat("_Dither", dither);
                _properties.SetInt("_SpotCount", spotCount);
                _properties.SetVectorArray("_Spots", _spots);
                _properties.SetVectorArray("_SpotColors", _spotColors);
                _properties.SetFloat("_Reveal", session.Objects[node.Id][TheatreProperty.Reveal]);
                _properties.SetFloat("_Lit", node.Light.Length == 0 ? 0 : 1);
                if (node.Shape == TheatreShape.Image) _properties.SetTexture("_MainTex", _images[session.Objects[node.Id].Asset]);
                if (node.Light.Length != 0)
                {
                    var lamp = session.Objects[node.Light].Node;
                    var transform = _matrices[lamp.Id];
                    if (Mathf.Abs(transform.determinant) < 0.00001f) throw new InvalidOperationException("theatre light transform is singular: " + lamp.Id);
                    _properties.SetMatrix("_SceneToLight", transform.inverse);
                    _properties.SetVector("_Lamp", new Vector4(0, 0, lamp.Width, _brightness[lamp.Id] * _opacity[lamp.Id]));
                    _properties.SetColor("_LightColor", ColorOf(lamp.Color));
                }
                if (node.Shape == TheatreShape.Focus)
                {
                    // The focus is a transparent lighting veil, not a copy of the preceding framebuffer.
                    _properties.SetMatrix("_SceneTransform", Matrix4x4.TRS(new Vector3(left + viewWidth / 2f, top + viewHeight / 2f, 0), Quaternion.identity, new Vector3(viewWidth / 2f, viewHeight / 2f, 1)));
                    var center = _matrices[node.Id].MultiplyPoint3x4(Vector3.zero);
                    _properties.SetVector("_Focus", new Vector4(center.x, center.y, focusStrength, _opacity[node.Id]));
                    _properties.SetVector("_FocusRadii", new Vector4(node.Width, node.Height, 0, 0));
                    _commands.DrawMesh(mesh, Matrix4x4.identity, _material, 0, 0, _properties);
                    continue;
                }
                _commands.DrawMesh(mesh, Matrix4x4.identity, _material, 0, 0, _properties);
            }
            _commands.Blit(_target, _display, _material, 1);
            Graphics.ExecuteCommandBuffer(_commands);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _commands.Dispose();
            foreach (var mesh in _meshes.Values) Release(mesh);
            _meshes.Clear();
            Release(_material);
            if (_target != null) { _target.Release(); Release(_target); _target = null; }
            if (_display != null) { _display.Release(); Release(_display); _display = null; }
        }
        private static void Release(UnityEngine.Object asset)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(asset);
            else UnityEngine.Object.DestroyImmediate(asset);
        }
        private static Color ColorOf(TheatreColor c)
        {
            var color = new Color(c.R, c.G, c.B, c.A);
            return color;
        }
    }
}
