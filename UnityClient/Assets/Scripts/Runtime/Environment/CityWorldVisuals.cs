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
            public string[] surfaceRoles = Array.Empty<string>();
            public string[] lineRoles = Array.Empty<string>();
            public string[] lightPalettes = Array.Empty<string>();
            public LightSpec[] lights = Array.Empty<LightSpec>();
            public Profile[] profiles = Array.Empty<Profile>();
            public PlaceHash[] placeHashes = Array.Empty<PlaceHash>();
        }
        private readonly RenderPipelineAsset? _previousPipeline;
        private readonly float _scale;
        private readonly Light[] _cityLights;
        private readonly bool[] _cityEnabled;
        private readonly AmbientMode _ambientMode;
        private readonly FogMode _fogMode;
        private readonly float _reflection;
        private readonly Color _cameraColor;
        private readonly Camera? _camera;
        private readonly Spec _spec;
        private readonly CityWorldPalette _palette;
        private CityWorldOverview _overview;
        private readonly Transform _city;
        private string? _focusedName;
        private readonly CityEnvironmentTransition _transition;
        private readonly float[] _cityIntensity, _sceneIntensity;
        private bool _hasFocused;
#if UNITY_EDITOR
        private static readonly System.Collections.Generic.HashSet<CityWorldVisuals> EditorInstances = new();
        // 调色工具重新应用正式配光，不创建另一套预览材质或场景。
        public static void CompleteEditorTransitions()
        {
            foreach (var instance in EditorInstances) instance._transition.Complete();
        }
        public static int RefreshEditorPalette()
        {
            foreach (var instance in EditorInstances)
            {
                instance._palette.Validate();
                instance._transition.Cancel();
                instance._overview.Dispose();
                foreach (var row in instance._spec.lights)
                    Configure(instance._cityLights.Single(l => l.name == row.name), row, instance._palette, instance._scale);
                for (int i = 0; i < instance._cityLights.Length; i++)
                    instance._cityIntensity[i] = instance._cityLights[i].intensity;
                instance._overview = new CityWorldOverview(instance._city, instance._palette);
                instance._hasFocused = false;
                instance.SetFocused(instance._focusedName);
            }
            return EditorInstances.Count;
        }
#endif
        private readonly Light[] _sceneLights;
        private readonly bool[] _sceneEnabled;
        private readonly Color _ambient, _fogColor;
        private readonly bool _fog;
        private readonly float _fogStart, _fogEnd;

        public CityWorldVisuals(Transform city)
        {
            _city = city;
            _scale = city.lossyScale.x;
            if (_scale <= 0 || !Mathf.Approximately(_scale,city.lossyScale.y) || !Mathf.Approximately(_scale,city.lossyScale.z))
                throw new InvalidOperationException("City 必须采用正值统一缩放");
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
            _palette.ValidateSpec(_spec);
            _previousPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = _palette.Pipeline
                ?? throw new InvalidOperationException("世界视觉基准缺少正式渲染配置");
            _sceneLights = UnityEngine.Object.FindObjectsOfType<Light>(true)
                .Where(l => !l.transform.IsChildOf(city) && l.type == LightType.Directional).ToArray();
            _sceneEnabled = _sceneLights.Select(l => l.enabled).ToArray();
            _overview = new CityWorldOverview(city, _palette);
            var transitionObject = new GameObject("~城市视觉过渡") { hideFlags = HideFlags.DontSave };
            transitionObject.transform.SetParent(city, false);
            _transition = transitionObject.AddComponent<CityEnvironmentTransition>();
            _ambient = RenderSettings.ambientLight;
            _fog = RenderSettings.fog; _fogColor = RenderSettings.fogColor;
            _fogStart = RenderSettings.fogStartDistance; _fogEnd = RenderSettings.fogEndDistance;
            foreach (var row in _spec.lights)
            {
                var light = _cityLights.Single(l => l.name == row.name);
                Configure(light,row,_palette,_scale);
                light.enabled = false;
            }
            _cityIntensity = _cityLights.Select(l => l.intensity).ToArray();
            _sceneIntensity = _sceneLights.Select(l => l.intensity).ToArray();
#if UNITY_EDITOR
            EditorInstances.Add(this);
#endif
        }
        public static void Configure(Light light, LightSpec row, CityWorldPalette palette, float scale = 1)
        {
            light.color = palette.FindLightColor(row.palette).gamma;
            light.shadows = LightShadows.Soft;
            light.shadowBias = 0.02f; light.shadowNormalBias = 0.05f;
            light.renderMode = LightRenderMode.ForcePixel;
            light.range = 100 * scale;
            if (row.mode == "baked")
            {
                if (row.type != "AREA") throw new InvalidOperationException("静态柔光声明仅接受 AREA");
                light.type = LightType.Area;
#if UNITY_EDITOR
                // 面光尺寸和烘焙模式仅用于 Editor，Player 使用 CityLightmapData。
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.areaSize = Vector2.one * row.size * scale;
#endif
                light.intensity = palette.BakedAreaScale * row.energy / (Mathf.PI * row.size * row.size);
            }
            else if (row.type == "SUN")
            {
#if UNITY_EDITOR
                light.lightmapBakeType = LightmapBakeType.Realtime;
#endif
                light.type = LightType.Directional;
                light.intensity = row.energy / Mathf.PI;
            }
            else
            {
#if UNITY_EDITOR
                light.lightmapBakeType = LightmapBakeType.Realtime;
#endif
                light.type = row.type == "POINT" ? LightType.Point : LightType.Spot;
                light.intensity = row.energy * scale * scale / (row.type == "AREA" ? Mathf.PI*Mathf.PI : 4*Mathf.PI*Mathf.PI);
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
            _focusedName = name;
            bool world = name == null || name == CityPlaces.WorldBaseName;
            var profile = _spec.profiles.SingleOrDefault(p => p.name == name);
            Color targetAmbient = world ? _palette.Overview.Ambient : profile?.worldLinear.gamma ?? _ambient;
            if (!world) { targetAmbient *= _palette.Focus.AmbientMultiplier; targetAmbient.a = 1; }
            Color targetBackground = world ? _palette.Overview.Atmosphere : profile?.worldLinear.gamma ?? _cameraColor;
            bool localFog = !world && (profile?.fog ?? _fog);
            float fogStart = profile == null ? _fogStart : profile.fogStart * _scale;
            float fogEnd = profile == null ? _fogEnd : profile.fogEnd * _scale;
            if (localFog && fogEnd <= fogStart) throw new InvalidOperationException("地点雾距离无效：" + name);
            Color targetFogColor = profile?.worldLinear.gamma ?? _fogColor;
            var targetFog = new Vector4(localFog ? _palette.Focus.LocalFogStrength : 0, fogStart, fogEnd, 0);
            var lights = _cityLights.Concat(_sceneLights).ToArray();
            var fromLights = lights.Select(l => l.enabled ? l.intensity : 0).ToArray();
            var targetLights = new float[lights.Length];
            for (int i = 0; i < _cityLights.Length; i++)
                targetLights[i] = _spec.lights.Any(row => row.name == _cityLights[i].name && row.profile == profile?.name)
                    ? _cityIntensity[i] * _palette.Focus.LightMultiplier : 0;
            for (int i = 0; i < _sceneLights.Length; i++)
                targetLights[_cityLights.Length + i] = !world && profile == null && _sceneEnabled[i] ? _sceneIntensity[i] * _palette.Focus.LightMultiplier : 0;
            Color fromAmbient = RenderSettings.ambientLight;
            Color fromBackground = _camera != null ? _camera.backgroundColor : _cameraColor;
            Color fromFogColor = CityWorldOverview.LocationFogColor;
            Vector4 fromFog = CityWorldOverview.LocationFog;
            // 雾出现/退出只渐变强度；距离从0插值会造成一帧近处白墙。
            if (fromFog.x == 0) { fromFog.y = targetFog.y; fromFog.z = targetFog.z; }
            if (targetFog.x == 0) { targetFog.y = fromFog.y; targetFog.z = fromFog.z; }
            float fromWeight = CityWorldOverview.WorldWeight;
            bool immediate = !_hasFocused;
            _hasFocused = true;
            _transition.Begin(immediate ? 0 : _palette.Focus.TransitionSeconds, t => {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.reflectionIntensity = 0;
                RenderSettings.ambientLight = Color.Lerp(fromAmbient, targetAmbient, t);
                RenderSettings.fog = false; // 两种雾均在共用 HDR 链中施加一次。
                if (_camera != null) _camera.backgroundColor = Color.Lerp(fromBackground, targetBackground, t);
                CityWorldOverview.LocationFog = Vector4.Lerp(fromFog, targetFog, t);
                CityWorldOverview.LocationFogColor = Color.Lerp(fromFogColor, targetFogColor, t);
                _overview.SetWeight(Mathf.Lerp(fromWeight, world ? 1 : 0, t), world);
                for (int i = 0; i < lights.Length; i++) {
                    lights[i].intensity = Mathf.Lerp(fromLights[i], targetLights[i], t);
                    lights[i].enabled = lights[i].intensity > 0;
                }
            });
        }
        private void ApplyEnvironment(Profile? profile)
        {
            RenderSettings.ambientMode = profile == null ? _ambientMode : AmbientMode.Flat;
            RenderSettings.reflectionIntensity = profile == null ? _reflection : 0;
            RenderSettings.ambientLight = profile == null ? _ambient : profile.worldLinear.gamma;
            RenderSettings.fog = profile?.fog ?? _fog;
            RenderSettings.fogMode = profile == null ? _fogMode : FogMode.Linear;
            RenderSettings.fogColor = profile == null ? _fogColor : profile.worldLinear.gamma;
            RenderSettings.fogStartDistance = profile == null ? _fogStart : profile.fogStart * _scale;
            RenderSettings.fogEndDistance = profile == null ? _fogEnd : profile.fogEnd * _scale;
            if (_camera != null) _camera.backgroundColor = profile == null ? _cameraColor : profile.worldLinear.gamma;
        }
        public void Dispose()
        {
#if UNITY_EDITOR
            EditorInstances.Remove(this);
#endif
            if (_transition != null) {
                _transition.Cancel();
                UnityEngine.Object.DestroyImmediate(_transition.gameObject);
            }
            ApplyEnvironment(null);
            _overview.Dispose();
            QualitySettings.renderPipeline = _previousPipeline;
            for (int i = 0; i < _sceneLights.Length; i++)
                if (_sceneLights[i] != null) { _sceneLights[i].enabled = _sceneEnabled[i]; _sceneLights[i].intensity = _sceneIntensity[i]; }
            for (int i = 0; i < _cityLights.Length; i++)
                if (_cityLights[i] != null) { _cityLights[i].enabled = _cityEnabled[i]; _cityLights[i].intensity = _cityIntensity[i]; }
        }
    }
}
