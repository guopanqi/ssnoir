#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SSNoir
{
    /// <summary>地点静态光照资源。与正式地点 FBX 同源烘焙，不改变共享材质。</summary>
    public sealed class CityLightmapData : ScriptableObject
    {
        [Serializable] public sealed class Binding
        {
            public string Path = "";
            public int Index;
            public Vector4 ScaleOffset;
        }
        public string SourceHash = "";
        public Texture2D[] Lightmaps = Array.Empty<Texture2D>();
        public Binding[] Renderers = Array.Empty<Binding>();
        public Material? FloorHighlight;

        public void Apply(GameObject detail, string expectedHash)
        {
            if (SourceHash != expectedHash) throw new InvalidOperationException("地点几何已变，请重新烘焙：" + detail.name);
            var maps = new List<LightmapData>(LightmapSettings.lightmaps ?? Array.Empty<LightmapData>());
            var indices = Lightmaps.Select(texture => {
                int index = maps.FindIndex(m => m.lightmapColor == texture);
                if (index < 0) { index = maps.Count;maps.Add(new LightmapData {lightmapColor=texture}); }
                return index;
            }).ToArray();
            LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
            LightmapSettings.lightmaps = maps.ToArray();
            foreach (var binding in Renderers)
            {
                var target = detail.transform.Find(binding.Path) ?? throw new InvalidOperationException("光照绑定对象缺失："+binding.Path);
                var renderer = target.GetComponent<MeshRenderer>() ?? throw new InvalidOperationException("光照绑定没有 MeshRenderer："+binding.Path);
                renderer.receiveGI = ReceiveGI.Lightmaps;
                renderer.lightmapIndex = indices[binding.Index];renderer.lightmapScaleOffset = binding.ScaleOffset;
            }
            if (FloorHighlight != null)
                foreach (var renderer in detail.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(r => r.sharedMaterials.Any(m => m.name == "抛光地面")))
                {
                    var overlay = new GameObject("抛光地面面光",typeof(MeshFilter),typeof(MeshRenderer));
                    overlay.transform.SetParent(renderer.transform,false);
                    overlay.GetComponent<MeshFilter>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    var layer = overlay.GetComponent<MeshRenderer>();layer.sharedMaterial = FloorHighlight;
                    layer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;layer.receiveShadows=false;
                }
        }
    }
}
