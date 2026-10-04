#nullable enable
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSNoir
{
    /// <summary>地点选择只影响灯光和环境；材质引用由正式模型导入器建立。</summary>
    public sealed class CityWorldVisuals : IDisposable
    {
        [Serializable] public sealed class LightSpec
        {
            public string name = "", profile = "", type = "", palette = "", mode = "";
            public float energy, size, spotAngle, spotBlend;
        }
        [Serializable] public sealed class Profile
        {
            public string name = "";
            public Color worldLinear;
            public bool fog;
            public float fogStart, fogEnd;
        }
        [Serializable] public sealed class PlaceHash { public string name = "", hash = ""; }
        [Serializable] public sealed class Spec
        {
            public int version;
            public LightSpec[] lights = Array.Empty<LightSpec>();
            public Profile[] profiles = Array.Empty<Profile>();
            public PlaceHash[] placeHashes = Array.Empty<PlaceHash>();
        }
        private readonly Light[] _cityLights;
        private readonly bool[] _cityEnabled;
        private readonly AmbientMode _ambientMode;
        private readonly FogMode _fogMode;
        private readonly float _reflection;
        private readonly Color _cameraColor;
        private readonly Camera? _camera;
        private readonly Spec _spec;
        private readonly CityWorldPalette _palette;
        private readonly Light[] _sceneLights;
        private readonly bool[] _sceneEnabled;
        private readonly Color _ambient, _fogColor;
        private readonly bool _fog;
        private readonly float _fogStart, _fogEnd;

        public CityWorldVisuals(Transform city)
        {
            _cityLights = city.GetComponentsInChildren<Light>(true);
            _cityEnabled = _cityLights.Select(l => l.enabled).ToArray();
            _ambientMode = RenderSettings.ambientMode; _fogMode = RenderSettings.fogMode;
            _reflection = RenderSettings.reflectionIntensity;
            _camera = Camera.main; _cameraColor = _camera != null ? _camera.backgroundColor : Color.black;
            var data = Resources.Load<TextAsset>("City/World.visual")
                ?? throw new InvalidOperationException("缺少 CityBox 世界视觉声明 World.visual.json");
            _spec = JsonUtility.FromJson<Spec>(data.text);
            if (_spec.version != 1) throw new InvalidOperationException("不支持的世界视觉声明版本");
            _palette = CityWorldPalette.Load();
            _sceneLights = UnityEngine.Object.FindObjectsOfType<Light>(true)
                .Where(l => !l.transform.IsChildOf(city) && l.type == LightType.Directional).ToArray();
            _sceneEnabled = _sceneLights.Select(l => l.enabled).ToArray();
            _ambient = RenderSettings.ambientLight;
            _fog = RenderSettings.fog; _fogColor = RenderSettings.fogColor;
            _fogStart = RenderSettings.fogStartDistance; _fogEnd = RenderSettings.fogEndDistance;
            foreach (var row in _spec.lights)
            {
                var light = _cityLights.Single(l => l.name == row.name);
                Configure(light,row,_palette);
                light.enabled = false;
            }
        }
        public static void Configure(Light light, LightSpec row, CityWorldPalette palette)
        {
            light.color = palette.FindLightColor(row.palette).gamma;
            light.shadows = LightShadows.Soft;
            light.shadowBias = 0.02f; light.shadowNormalBias = 0.05f;
            light.renderMode = LightRenderMode.ForcePixel;
            light.range = 10;
            if (row.mode == "baked")
            {
                if (row.type != "AREA") throw new InvalidOperationException("静态柔光声明仅接受 AREA");
                light.type = LightType.Area;light.lightmapBakeType = LightmapBakeType.Baked;
                light.areaSize = Vector2.one * row.size * .1f;
                light.intensity = palette.BakedAreaScale * row.energy / (Mathf.PI * row.size * row.size);
            }
            else if (row.type == "SUN")
            {
                light.lightmapBakeType = LightmapBakeType.Realtime;
                light.type = LightType.Directional;
                light.intensity = row.energy / Mathf.PI;
            }
            else
            {
                light.lightmapBakeType = LightmapBakeType.Realtime;
                light.type = row.type == "POINT" ? LightType.Point : LightType.Spot;
                light.intensity = row.energy * 0.01f / (row.type == "AREA" ? Mathf.PI*Mathf.PI : 4*Mathf.PI*Mathf.PI);
                light.spotAngle = row.spotAngle;
                light.innerSpotAngle = row.spotAngle * (1-row.spotBlend);
            }
        }
        public static void ApplyLightmaps(GameObject instance, string name)
        {
            var data = Resources.Load<TextAsset>("City/World.visual") ?? throw new InvalidOperationException("缺少世界视觉声明");
            var spec = JsonUtility.FromJson<Spec>(data.text);
            if (!spec.lights.Any(l => l.profile == name && l.mode == "baked")) return;
            var baked = Resources.Load<CityLightmapData>("City/Lighting/" + name)
                ?? throw new InvalidOperationException("缺少地点静态光照："+name);
            baked.Apply(instance,spec.placeHashes.Single(p => p.name == name).hash);
        }
        public void SetFocused(string? name)
        {
            var profile = _spec.profiles.SingleOrDefault(p => p.name == name);
            foreach (var row in _spec.lights)
                _cityLights.Single(l => l.name == row.name).enabled = row.profile == profile?.name;
            for (int i = 0; i < _sceneLights.Length; i++) _sceneLights[i].enabled = profile == null && _sceneEnabled[i];
            RenderSettings.ambientMode = profile == null ? _ambientMode : AmbientMode.Flat;
            RenderSettings.reflectionIntensity = profile == null ? _reflection : 0;
            RenderSettings.ambientLight = profile == null ? _ambient : profile.worldLinear.gamma;
            RenderSettings.fog = profile?.fog ?? _fog;
            RenderSettings.fogMode = profile == null ? _fogMode : FogMode.Linear;
            RenderSettings.fogColor = profile == null ? _fogColor : profile.worldLinear.gamma;
            RenderSettings.fogStartDistance = profile == null ? _fogStart : profile.fogStart * 0.1f;
            RenderSettings.fogEndDistance = profile == null ? _fogEnd : profile.fogEnd * 0.1f;
            if (_camera != null) _camera.backgroundColor = profile == null ? _cameraColor : profile.worldLinear.gamma;
        }
        public void Dispose()
        {
            SetFocused(null);
            for (int i = 0; i < _cityLights.Length; i++)
                if (_cityLights[i] != null) _cityLights[i].enabled = _cityEnabled[i];
        }
    }
}
