#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SSNoir
{
    /// <summary>全城夜景的配光与后处理作用域。局部地点继续使用自己的灯光。</summary>
    public sealed class CityWorldOverview : IDisposable
    {
        [Serializable]
        public sealed class Settings
        {
            public Color Ambient = new Color(0.17f, 0.18f, 0.21f, 1);
            public Color Atmosphere = new Color(0.09f, 0.105f, 0.14f, 1);
            public float FogStart = 1450;
            public float FogEnd = 2300;
            public float ShadowDistance = 2100;
            public float KeyIntensity = 0.85f;
            public Color KeyColor = new Color(.83f,.86f,.91f,1);
            public float FillIntensity = 0.14f;
            public float ShadowStrength = 0.8f;
            public Vector3 KeyDirection = new Vector3(48, -42, 0);
            public Vector3 FillDirection = new Vector3(65, 135, 0);
        }

        [Serializable] public sealed class NightSettings
        {
            public float LampGlow=.65f, LampPool=1, WarmLight=.4f;
            public float Bloom=.38f, BloomThreshold=.7f, BloomScatter=.4f, Exposure=1.15f;
            public float AccentPoolSize=72, AccentPoolGain=.13f;
            public float DevelopedLampGain=.45f, PoorLampGain=.12f, PoorRiverBand=180;
            public int DevelopedAccentLights=10, PoorAccentLights=4;
            public float Contrast=1.12f, Saturation=.78f, Vignette=.55f, Grain=.018f;
            public float FogDensity=.0005f, FogFalloff=.02f, Haze=.00012f, Mist=.3f;
            public Color FogLow=new Color(.063f,.09f,.149f,1), FogHigh=new Color(.043f,.063f,.11f,1);
            public int Cars=40, Boats=4;
            public float CarSpeed=1, BoatSpeed=1, HeadlightRange=6, HeadlightGain=.65f, WakeGain=.3f;
        }
        public static NightSettings? ActiveNight { get; private set; }
        public static float WorldWeight { get; private set; }
        public static float BloomMultiplier { get; private set; } = 1;
        public static Vector4 LocationFog { get; set; }
        public static Color LocationFogColor { get; set; }
        private readonly CityWorldPalette.FocusSettings _focus;
        private readonly CityNightEnvironment _night;
        private readonly GameObject _rig;
        private readonly Light _key, _fill;
        private readonly UniversalRenderPipelineAsset _pipeline;
        private readonly UniversalRenderPipelineAsset _worldPipeline;
        private bool _disposed;
        private readonly float _scale;
        private readonly Settings _settings;

        public CityWorldOverview(Transform city, CityWorldPalette palette)
        {
            _settings = palette.Overview;
            _focus = palette.Focus;
            _scale = city.lossyScale.x;
            _pipeline = palette.Pipeline as UniversalRenderPipelineAsset
                ?? throw new InvalidOperationException("世界视角需要正式 URP 配置");
            if (_settings.FogEnd <= _settings.FogStart || _settings.ShadowDistance <= 0)
                throw new InvalidOperationException("世界视角的雾距离或阴影距离无效");
            // 世界视角只派生阴影距离，Renderer、Shader 与其它设置仍来自同一正式配置。
            // 使用运行时实例，避免把全城阴影距离写回共享资产或留给局部机位。
            _worldPipeline = UnityEngine.Object.Instantiate(_pipeline);
            _worldPipeline.name = "世界视角 URP";
            _worldPipeline.hideFlags = HideFlags.HideAndDontSave;
            _worldPipeline.shadowDistance = _settings.ShadowDistance * _scale;
            _rig = new GameObject("~世界光照") { hideFlags = HideFlags.DontSave };
            _rig.transform.SetParent(city, false);
            _night = CityNightEnvironment.Create(_rig.transform, palette);
            _key = CreateLight("世界主光", _settings.KeyDirection,
                _settings.KeyColor, _settings.KeyIntensity, LightShadows.Soft);
            _fill = CreateLight("世界补光", _settings.FillDirection,
                palette.FindLightColor("冷边").gamma, _settings.FillIntensity, LightShadows.None);
        }

        private Light CreateLight(string name, Vector3 direction, Color color, float intensity, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_rig.transform, false);
            go.transform.rotation = Quaternion.Euler(direction);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
            light.shadowStrength = _settings.ShadowStrength;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.05f;
            light.enabled = false;
            return light;
        }

        public void SetWeight(float weight, bool worldTarget)
        {
            WorldWeight = Mathf.Clamp01(weight);
            ActiveNight = _night.Settings;
            BloomMultiplier = Mathf.Lerp(_focus.BloomMultiplier, 1, WorldWeight);
            _night.gameObject.SetActive(worldTarget);
            _key.enabled = WorldWeight > 0;
            _fill.enabled = WorldWeight > 0;
            _key.intensity = _settings.KeyIntensity * WorldWeight;
            _fill.intensity = _settings.FillIntensity * WorldWeight;
            QualitySettings.renderPipeline = WorldWeight > 0 ? _worldPipeline : _pipeline;
        }

        public void Dispose()
        {
            if (_disposed) return;
            ActiveNight = null;
            WorldWeight = 0; LocationFog = Vector4.zero;
            _disposed = true;
            if (QualitySettings.renderPipeline == _worldPipeline) QualitySettings.renderPipeline = _pipeline;
            if (_rig != null) UnityEngine.Object.DestroyImmediate(_rig);
            UnityEngine.Object.DestroyImmediate(_worldPipeline);
        }
    }
}
