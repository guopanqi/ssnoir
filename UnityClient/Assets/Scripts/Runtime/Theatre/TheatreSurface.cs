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
        private RenderTexture? _target;
        private bool _disposed;
        public RenderTexture Texture => _target ?? throw new InvalidOperationException("surface has not rendered");
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
                foreach (var beat in scene.Beats)
                    foreach (var command in beat.Commands)
                        if (command.Kind == TheatreCommandKind.Image) LoadImage(command.Asset);
            }
            catch { Dispose(); throw; }
        }
        private void LoadImage(string asset)
        {
            if (!_images.ContainsKey(asset))
                _images.Add(asset, Resources.Load<Texture2D>(asset) ?? throw new InvalidOperationException("theatre image missing: " + asset));
        }
        public void Render(TheatreSession session, int width, int height)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TheatreSurface));
            width = Math.Max(1, width); height = Math.Max(1, height);
            if (_target == null || _target.width != width || _target.height != height)
            {
                if (_target != null) { _target.Release(); Release(_target); }
                _target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = "Line Theatre", filterMode = FilterMode.Bilinear };
                _target.Create();
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
            float viewWidth = _scene.Height * width / height;
            float left = (_scene.Width - viewWidth) * 0.5f;
            var projection = GL.GetGPUProjectionMatrix(
                Matrix4x4.Ortho(left, left + viewWidth, _scene.Height, 0, -1, 1), true);
            _commands.Clear(); _commands.SetRenderTarget(_target);
            _commands.ClearRenderTarget(false, true, ColorOf(_scene.Background));
            // Shader uses explicit scene coordinates; no camera matrices or SRP globals are changed.
            foreach (var node in _scene.Nodes)
            {
                if (!_meshes.TryGetValue(node.Id, out var mesh)) continue;
                _properties.Clear();
                _properties.SetMatrix("_SceneTransform", _matrices[node.Id]);
                _properties.SetMatrix("_SceneToClip", projection);
                float brightness = _brightness[node.Id];
                var tint = ColorOf(node.Color); tint.a *= _opacity[node.Id];
                if (node.Shape == TheatreShape.Glow || (node.Shape == TheatreShape.Polygon && node.Color.A < 1f)) tint.a *= Mathf.Clamp01(brightness);
                tint.r *= brightness; tint.g *= brightness; tint.b *= brightness;
                _properties.SetColor("_Tint", tint);
                _properties.SetFloat("_Mode", node.Shape == TheatreShape.Line ? 0 : node.Shape == TheatreShape.Polygon ? 1 : node.Shape == TheatreShape.Glow ? 2 : 3);
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
                _commands.DrawMesh(mesh, Matrix4x4.identity, _material, 0, 0, _properties);
            }
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
        }
        private static void Release(UnityEngine.Object asset)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(asset);
            else UnityEngine.Object.DestroyImmediate(asset);
        }
        private static Color ColorOf(TheatreColor c)
        {
            var color = new Color(c.R, c.G, c.B, c.A);
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
        }
    }
}
