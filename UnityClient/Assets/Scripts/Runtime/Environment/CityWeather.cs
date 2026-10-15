#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSNoir
{
    /// <summary>
    /// Rain: world-space strokes that can be occluded by architecture.
    /// Snow: independent camera-frustum flakes, built from a single dynamic mesh.
    /// No camera child, old overlay particles, or per-flake GameObject is used.
    /// Geometry is updated before the camera renders to avoid following one frame behind.
    /// </summary>
    public sealed class CityWeather : MonoBehaviour
    {
        private const int RainCount = 420;
        private const int SnowCount = 320;
        private SSNoirGameManager _manager = null!;
        private Mesh _rainMesh = null!, _snowMesh = null!;
        private Material _rainMaterial = null!, _snowMaterial = null!;
        private MeshRenderer _rainRenderer = null!, _snowRenderer = null!;
        private readonly Vector3[] _rainSeeds = new Vector3[RainCount];
        private readonly Vector3[] _rainVertices = new Vector3[RainCount * 4];
        private readonly Color[] _rainColors = new Color[RainCount * 4];
        private readonly Vector3[] _snowSeeds = new Vector3[SnowCount];
        private readonly Vector3[] _snowVertices = new Vector3[SnowCount * 4];
        private readonly Color[] _snowColors = new Color[SnowCount * 4];
        private float _rainWeight, _snowWeight, _clock;
        private bool _exposed;
        private string? _debugWeatherPreview;

        public void Initialize(SSNoirGameManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            if (Camera.main == null) throw new InvalidOperationException("城市天气需要 Main Camera");

            var shader = Resources.Load<Shader>("Environment/CityRain")
                ?? throw new InvalidOperationException("城市天气缺少 Environment/CityRain Shader");
            _rainMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            _snowMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            _snowMaterial.SetFloat("_Snow", 1f);
            var rng = new System.Random(137);
            _rainMesh = CreateQuads("城市雨线", RainCount, _rainSeeds, rng);
            _rainRenderer = BuildRenderer("雨幕", _rainMesh, _rainMaterial);
            rng = new System.Random(9137);
            _snowMesh = CreateQuads("镜头雪片", SnowCount, _snowSeeds, rng);
            _snowRenderer = BuildRenderer("镜头雪", _snowMesh, _snowMaterial);
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private MeshRenderer BuildRenderer(string label, Mesh mesh, Material material)
        {
            var obj = new GameObject(label) { hideFlags = HideFlags.DontSave };
            obj.transform.SetParent(transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        private static Mesh CreateQuads(string label, int count, Vector3[] seeds, System.Random rng)
        {
            var mesh = new Mesh { name = label, hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            var uv = new Vector2[count * 4];
            var indices = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                seeds[i] = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble());
                int v = i * 4, t = i * 6;
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.right;
                uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
                indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
            }
            mesh.vertices = new Vector3[count * 4];
            mesh.uv = uv;
            mesh.triangles = indices;
            return mesh;
        }

        private void LateUpdate()
        {
            if (_manager == null) return;
            var camera = Camera.main;
            bool outdoors = !_manager.StageController.HasActivePortal;
            _exposed = camera != null && outdoors && !_manager.Title.IsActive;
            string weather = _debugWeatherPreview ?? _manager.DisplayedSnapshot.Weather;
            float dt = Time.unscaledDeltaTime;
            _clock += dt;
            _rainWeight = Mathf.MoveTowards(_rainWeight, _exposed && weather == "雨" ? 1 : 0, dt * 1.5f);
            _snowWeight = Mathf.MoveTowards(_snowWeight, _exposed && weather == "雪" ? 1 : 0, dt * 1.5f);
            // Neither weather effect is drawn indoors. A weather switch outdoors crossfades.
            _rainRenderer.enabled = outdoors && _rainWeight > .001f;
            _snowRenderer.enabled = outdoors && _snowWeight > .001f;
            if (_rainRenderer.enabled && camera != null) UpdateRain(camera);
        }

        // URP renders after Cinemachine has driven the Main Camera. Drawing here, rather
        // than copying a camera-child position in LateUpdate, prevents drifting snow.
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (_snowRenderer == null || !_snowRenderer.enabled || camera != Camera.main) return;
            UpdateSnow(camera);
        }

        private void UpdateSnow(Camera camera)
        {
            float nearDepth = Mathf.Max(camera.nearClipPlane + 2f, 8f);
            float farDepth = Mathf.Min(camera.farClipPlane * .7f, Mathf.Max(nearDepth + 10f, 22f));
            if (farDepth <= nearDepth) farDepth = nearDepth;
            for (int i = 0; i < SnowCount; i++)
            {
                bool near = i < SnowCount / 2;
                float distance = near ? nearDepth : farDepth;
                float hh = camera.orthographic ? camera.orthographicSize
                    : distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
                float hw = hh * camera.aspect;
                var seed = _snowSeeds[i];
                float speed = (near ? .12f : .065f) * (.65f + seed.z * .7f);
                float phase = Mathf.Repeat(seed.y - _clock * speed, 1f);
                float drift = Mathf.Sin(_clock * (.55f + seed.z) + seed.x * 17f) * (near ? .022f : .012f);
                float u = Mathf.Repeat(seed.x + drift + _clock * (near ? .013f : .006f), 1f);
                // Shift outside the viewport for smooth entrance and exit.
                float x = (u * 2f - 1f) * hw * 1.15f;
                float y = (phase * 2f - 1f) * hh * 1.2f;
                Vector3 center = camera.transform.position + camera.transform.forward * distance
                    + camera.transform.right * x + camera.transform.up * y;
                float size = (near ? .0028f : .0015f) * (0.6f + seed.z * 1.25f);
                Vector3 right = camera.transform.right * (hw * size * 2f);
                Vector3 up = camera.transform.up * (hh * size * 2f);
                int k = i * 4;
                _snowVertices[k] = center - right - up;
                _snowVertices[k + 1] = center + right - up;
                _snowVertices[k + 2] = center + right + up;
                _snowVertices[k + 3] = center - right + up;
                float edgeFade = Mathf.SmoothStep(0, 1, Mathf.Min(phase, 1f - phase) * 8f);
                float alpha = _snowWeight * edgeFade * (near ? .65f : .38f) * (.6f + seed.z * .4f);
                Color color = new Color(.83f, .88f, .94f, alpha);
                for (int j = 0; j < 4; j++) _snowColors[k + j] = color;
            }
            _snowMesh.vertices = _snowVertices;
            _snowMesh.colors = _snowColors;
            // Explicit view-volume bounds because billboards are rebuilt before this camera renders.
            float maxDepth = Mathf.Max(nearDepth, farDepth);
            float maxHalfHeight = camera.orthographic ? camera.orthographicSize
                : maxDepth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
            float extent = maxDepth + 4f * maxHalfHeight * Mathf.Max(1f, camera.aspect);
            _snowMesh.bounds = new Bounds(camera.transform.position, Vector3.one * extent * 2f);
        }

        private void UpdateRain(Camera camera)
        {
            var anchor = _manager.ResolveAnchor(_manager.FocusedNodeName);
            var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            float ground = anchor != null ? anchor.transform.position.y : 0;
            var plane = new Plane(Vector3.up, new Vector3(0, ground, 0));
            if (!plane.Raycast(ray, out float distance)) { _rainRenderer.enabled = false; return; }
            Vector3 center = ray.GetPoint(distance);
            float radius = Mathf.Max(.1f, camera.orthographic ? camera.orthographicSize
                : distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f));
            radius *= Mathf.Max(1, camera.aspect) * 1.15f;
            float height = radius * 1.5f;
            Vector3 direction = new Vector3(-.14f, -1, .05f).normalized;
            Vector3 side = Vector3.Cross(direction, camera.transform.forward).normalized;
            for (int i = 0; i < RainCount; i++)
            {
                Vector3 seed = _rainSeeds[i];
                float phase = Mathf.Repeat(seed.y + _clock * (.8f + seed.z * .55f), 1);
                Vector3 top = center + new Vector3((seed.x * 2 - 1) * radius,
                    height * (1 - phase), (seed.z * 2 - 1) * radius);
                top += new Vector3(-.14f, 0, .05f) * height * phase;
                Vector3 bottom = top + direction * radius * (.035f + seed.x * .025f);
                Vector3 halfWidth = side * radius * (.0006f + seed.z * .00045f);
                int v = i * 4;
                _rainVertices[v] = top - halfWidth; _rainVertices[v + 1] = top + halfWidth;
                _rainVertices[v + 2] = bottom + halfWidth; _rainVertices[v + 3] = bottom - halfWidth;
                float depth = Vector3.Dot(top - camera.transform.position, camera.transform.forward);
                float fade = Mathf.Clamp01(depth / Mathf.Max(radius * .12f, .01f));
                fade *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(radius * 2, radius * 5, depth));
                fade *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .05f, phase))
                    * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.88f, 1, phase)));
                var color = new Color(.70f, .80f, .90f, _rainWeight * fade * (.22f + seed.z * .12f));
                for (int j = 0; j < 4; j++) _rainColors[v + j] = color;
            }
            _rainMesh.vertices = _rainVertices;
            _rainMesh.colors = _rainColors;
            _rainMesh.bounds = new Bounds(center + Vector3.up * height * .5f,
                new Vector3(radius * 2.5f, height * 1.3f, radius * 2.5f));
        }

        public string? DebugWeatherPreview => _debugWeatherPreview;
        public void SetDebugWeatherPreview(string? weather)
        {
            if (weather != null && weather != "晴" && weather != "雨" && weather != "雪")
                throw new ArgumentException($"未知天气预览：{weather}", nameof(weather));
            _debugWeatherPreview = weather;
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            if (_rainMesh != null) Destroy(_rainMesh);
            if (_snowMesh != null) Destroy(_snowMesh);
            if (_rainMaterial != null) Destroy(_rainMaterial);
            if (_snowMaterial != null) Destroy(_snowMaterial);
        }
    }
}
