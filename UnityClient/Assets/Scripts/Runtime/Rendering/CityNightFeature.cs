#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SSNoir.Rendering
{
    /// <summary>城市与地点共用调色、颗粒和辉光；世界雾与地点雾分别渐变。</summary>
    public sealed class CityNightFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader = null!;
        private Material? _material;
        private NightPass? _pass;
        public override void Create()
        {
            // Unity calls Create during import before referenced shaders are imported.
            if (shader == null) return;
            _pass?.Dispose(); CoreUtils.Destroy(_material);
            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new NightPass(_material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (CityWorldOverview.ActiveNight == null || data.cameraData.cameraType != CameraType.Game) return;
            if (_pass == null) throw new InvalidOperationException("夜城后处理缺少 Shader");
            _pass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(_pass);
        }
        protected override void Dispose(bool disposing) { _pass?.Dispose(); CoreUtils.Destroy(_material); }
        private sealed class NightPass : ScriptableRenderPass
        {
            private readonly Material _material;
            private RTHandle? _copy, _bright, _blur, _wide, _wideBlur;
            public NightPass(Material material) { _material = material; }
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
            {
                var d = data.cameraData.cameraTargetDescriptor; d.depthBufferBits = 0; d.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(ref _copy,d,FilterMode.Bilinear,TextureWrapMode.Clamp,name:"Night scene HDR");
                d.width = Mathf.Max(1,d.width/4); d.height = Mathf.Max(1,d.height/4);
                RenderingUtils.ReAllocateIfNeeded(ref _bright,d,FilterMode.Bilinear,TextureWrapMode.Clamp,name:"Night bloom bright");
                RenderingUtils.ReAllocateIfNeeded(ref _blur,d,FilterMode.Bilinear,TextureWrapMode.Clamp,name:"Night bloom blur");
                d.width=Mathf.Max(1,d.width/2);d.height=Mathf.Max(1,d.height/2);
                RenderingUtils.ReAllocateIfNeeded(ref _wide,d,FilterMode.Bilinear,TextureWrapMode.Clamp,name:"Night bloom wide");
                RenderingUtils.ReAllocateIfNeeded(ref _wideBlur,d,FilterMode.Bilinear,TextureWrapMode.Clamp,name:"Night bloom wide blur");
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                var settings = CityWorldOverview.ActiveNight;
                if (settings == null) return;
                var source = data.cameraData.renderer.cameraColorTargetHandle;
                if (source.rt == null) throw new InvalidOperationException("夜城后处理需要中间颜色缓冲");
                _material.SetVector("_Fog",new Vector4(settings.FogDensity,settings.FogFalloff,settings.Haze,settings.Mist));
                _material.SetFloat("_WorldFogWeight", CityWorldOverview.WorldWeight);
                _material.SetVector("_LocationFog", CityWorldOverview.LocationFog);
                _material.SetColor("_LocationFogColor", CityWorldOverview.LocationFogColor.linear);
                // Image-to-video source frames exclude screen grain; review screenshots retain it.
                float grain=ReferenceEquals(SSNoirStylizeMaterial.CaptureCamera,data.cameraData.camera)?0:settings.Grain;
                _material.SetVector("_Grade",new Vector4(settings.Contrast,settings.Saturation,settings.Vignette,grain));
                _material.SetVector("_Bloom",new Vector4(settings.Bloom * CityWorldOverview.BloomMultiplier,settings.BloomThreshold,settings.Exposure,settings.BloomScatter));
                _material.SetColor("_FogLow",settings.FogLow.linear); _material.SetColor("_FogHigh",settings.FogHigh.linear);
                _material.SetVector("_NightSourceTexel",new Vector4(1f/_copy!.rt.width,1f/_copy.rt.height,0,0));
                var cmd = CommandBufferPool.Get("City night HDR");
                Blitter.BlitCameraTexture(cmd,source,_copy!);
                Blitter.BlitCameraTexture(cmd,_copy!,_bright!,_material,0);
                cmd.SetGlobalVector("_NightBlurDirection",new Vector4(1,0,1f/_bright!.rt.width,1f/_bright.rt.height));
                Blitter.BlitCameraTexture(cmd,_bright!,_blur!,_material,1);
                cmd.SetGlobalVector("_NightBlurDirection",new Vector4(0,1,1f/_bright!.rt.width,1f/_bright.rt.height));
                Blitter.BlitCameraTexture(cmd,_blur!,_bright!,_material,1);
                Blitter.BlitCameraTexture(cmd,_bright!,_wide!);
                cmd.SetGlobalVector("_NightBlurDirection",new Vector4(1,0,1f/_wide!.rt.width,1f/_wide.rt.height));
                Blitter.BlitCameraTexture(cmd,_wide!,_wideBlur!,_material,1);
                cmd.SetGlobalVector("_NightBlurDirection",new Vector4(0,1,1f/_wide!.rt.width,1f/_wide.rt.height));
                Blitter.BlitCameraTexture(cmd,_wideBlur!,_wide!,_material,1);
                cmd.SetGlobalTexture("_NightBloomTexture",_bright!);
                cmd.SetGlobalTexture("_NightWideBloomTexture",_wide!);
                Blitter.BlitCameraTexture(cmd,_copy!,source,_material,2);
                context.ExecuteCommandBuffer(cmd); CommandBufferPool.Release(cmd);
            }
            public void Dispose() { _copy?.Release(); _bright?.Release(); _blur?.Release(); _wide?.Release(); _wideBlur?.Release(); }
        }
    }
}
