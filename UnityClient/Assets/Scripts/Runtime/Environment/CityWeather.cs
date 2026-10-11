#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSNoir
{
    /// <summary>统一管理城市雨雪；雨线接受建筑遮挡，雪花沿用相机前的双层雪景，不覆盖 UI。</summary>
    public sealed class CityWeather : MonoBehaviour
    {
        private const int DropCount = 420;
        private SSNoirGameManager _manager = null!;
        private Mesh _mesh = null!;
        private Material _material = null!;
        private MeshRenderer _renderer = null!;
        private GameObject _snowOverlay = null!;
        private Transform _snowCamera = null!;
        private ParticleSystem[] _snowParticles = Array.Empty<ParticleSystem>();
        private Vector3 _snowCameraPosition;
        private Quaternion _snowCameraRotation;
        private bool _snowing;
        private string? _debugWeatherPreview;
        private readonly Vector3[] _seeds = new Vector3[DropCount];
        private readonly Vector3[] _vertices = new Vector3[DropCount * 4];
        private readonly Color[] _colors = new Color[DropCount * 4];
        private float _weight, _clock;

        public void Initialize(SSNoirGameManager manager, Transform snowOverlay)
        {
            _manager = manager;
            _snowOverlay = snowOverlay != null ? snowOverlay.gameObject
                : throw new InvalidOperationException("城市天气缺少 Main Camera/SnowOverlay");
            _snowCamera = Camera.main != null ? Camera.main.transform
                : throw new InvalidOperationException("城市天气需要 Main Camera");
            _snowCameraPosition = _snowCamera.InverseTransformPoint(_snowOverlay.transform.position);
            _snowCameraRotation = Quaternion.Inverse(_snowCamera.rotation) * _snowOverlay.transform.rotation;
            _snowOverlay.transform.SetParent(transform, true);
            var nearSnow = _snowOverlay.transform.Find("SnowOverlay_Near")?.GetComponent<ParticleSystem>();
            var farSnow = _snowOverlay.transform.Find("SnowOverlay_Far")?.GetComponent<ParticleSystem>();
            if (nearSnow == null || farSnow == null)
                throw new InvalidOperationException("SnowOverlay 缺少 SnowOverlay_Near 或 SnowOverlay_Far");
            _snowParticles = new[] { nearSnow, farSnow };
            ConfigureSnowflake(nearSnow, .018f, .075f);
            ConfigureSnowflake(farSnow, .012f, .045f);
            _snowOverlay.SetActive(false);
            var shader = Resources.Load<Shader>("Environment/CityRain")
                ?? throw new InvalidOperationException("城市天气缺少 Environment/CityRain Shader");
            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            _mesh = new Mesh { name = "城市雨线", hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();
            var random = new System.Random(137);
            var uv = new Vector2[_vertices.Length];
            var triangles = new int[DropCount * 6];
            for (int i = 0; i < DropCount; i++)
            {
                _seeds[i] = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                int v = i * 4, t = i * 6;
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.right;
                uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            _mesh.vertices = _vertices; _mesh.uv = uv; _mesh.triangles = triangles;
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off; _renderer.receiveShadows = false;
            _renderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (_manager == null) return;
            var camera = Camera.main;
            bool outdoors = !_manager.StageController.HasActivePortal;
            bool exposed = camera != null && outdoors && !_manager.Title.IsActive;
            string weather = _debugWeatherPreview ?? _manager.DisplayedSnapshot.Weather;
            bool rainy = exposed && weather == "雨";
            bool snowing = exposed && weather == "雪";
            SetSnowing(snowing);
            if (camera != null)
                _snowOverlay.transform.SetPositionAndRotation(
                    _snowCamera.TransformPoint(_snowCameraPosition),
                    _snowCamera.rotation * _snowCameraRotation);
            _weight = Mathf.MoveTowards(_weight, rainy ? 1 : 0, Time.unscaledDeltaTime * 1.5f);
            // 进入室内立即移除画面雨线，室外天气切换则淡出。
            _renderer.enabled = outdoors && _weight > .001f;
            if (!_renderer.enabled || camera == null) return;
            _clock += Time.unscaledDeltaTime;
            var anchor = _manager.ResolveAnchor(_manager.FocusedNodeName);
            var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            float ground = anchor != null ? anchor.transform.position.y : 0;
            var plane = new Plane(Vector3.up, new Vector3(0, ground, 0));
            if (!plane.Raycast(ray, out float distance)) { _renderer.enabled = false; return; }
            Vector3 center = ray.GetPoint(distance);
            float radius = Mathf.Max(.1f, camera.orthographic ? camera.orthographicSize
                : distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f));
            radius *= Mathf.Max(1, camera.aspect) * 1.15f;
            float height = radius * 1.5f;
            Vector3 direction = new Vector3(-.14f, -1, .05f).normalized;
            Vector3 side = Vector3.Cross(direction, camera.transform.forward).normalized;
            for (int i = 0; i < DropCount; i++)
            {
                Vector3 seed = _seeds[i];
                float phase = Mathf.Repeat(seed.y + _clock * (.8f + seed.z * .55f), 1);
                Vector3 top = center + new Vector3((seed.x * 2 - 1) * radius,
                    height * (1 - phase), (seed.z * 2 - 1) * radius);
                top += new Vector3(-.14f, 0, .05f) * height * phase;
                Vector3 bottom = top + direction * radius * (.035f + seed.x * .025f);
                Vector3 halfWidth = side * radius * (.0006f + seed.z * .00045f);
                int v = i * 4;
                _vertices[v] = top - halfWidth; _vertices[v + 1] = top + halfWidth;
                _vertices[v + 2] = bottom + halfWidth; _vertices[v + 3] = bottom - halfWidth;
                float depth = Vector3.Dot(top - camera.transform.position, camera.transform.forward);
                float fade = Mathf.Clamp01(depth / Mathf.Max(radius * .12f, .01f));
                fade *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(radius * 2, radius * 5, depth));
                fade *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .05f, phase))
                    * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.88f, 1, phase)));
                var color = new Color(.70f, .80f, .90f, _weight * fade * (.22f + seed.z * .12f));
                for (int j = 0; j < 4; j++) _colors[v + j] = color;
            }
            _mesh.vertices = _vertices; _mesh.colors = _colors;
            _mesh.bounds = new Bounds(center + Vector3.up * height * .5f,
                new Vector3(radius * 2.5f, height * 1.3f, radius * 2.5f));
        }

        public string? DebugWeatherPreview => _debugWeatherPreview;

        public void SetDebugWeatherPreview(string? weather)
        {
            if (weather != null && weather != "晴" && weather != "雨" && weather != "雪")
                throw new ArgumentException($"未知天气预览：{weather}", nameof(weather));
            _debugWeatherPreview = weather;
        }

        private static void ConfigureSnowflake(ParticleSystem particles, float minSize, float maxSize)
        {
            var main = particles.main;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        }

        private void SetSnowing(bool snowing)
        {
            if (_snowing == snowing) return;
            _snowing = snowing;
            if (snowing)
            {
                _snowOverlay.SetActive(true);
                foreach (var particles in _snowParticles) particles.Play(true);
            }
            else
            {
                foreach (var particles in _snowParticles)
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _snowOverlay.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_snowOverlay != null && _snowCamera != null)
                _snowOverlay.transform.SetParent(_snowCamera, true);
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
