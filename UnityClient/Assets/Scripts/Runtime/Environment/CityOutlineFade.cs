#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SSNoir
{
    /// <summary>仅交接期间使用透明描线；端点恢复原共享材质，不修改正式颜色或材质资产。</summary>
    internal sealed class CityOutlineFade : IDisposable
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly Shader _shader;
        private readonly Dictionary<Material,Material> _materials = new();
        private readonly Dictionary<Renderer,Entry> _renderers = new();
        private sealed class Entry
        {
            public Material[] Original = Array.Empty<Material>();
            public Material[] Fading = Array.Empty<Material>();
            public MaterialPropertyBlock[] Saved = Array.Empty<MaterialPropertyBlock>();
            public MaterialPropertyBlock[] Working = Array.Empty<MaterialPropertyBlock>();
        }
        public CityOutlineFade(Shader shader) { _shader=shader; }
        public void Apply(Renderer renderer,float opacity)
        {
            if(renderer==null)return; // 场景卸载时模型可能先于状态宿主销毁。
            if(opacity<=0 || opacity>=1)
            {
                Restore(renderer);
                renderer.enabled=opacity>0;
                return;
            }
            if(!_renderers.TryGetValue(renderer,out var entry))
            {
                entry=new Entry { Original=renderer.sharedMaterials };
                entry.Fading=entry.Original.Select(FadingMaterial).ToArray();
                entry.Saved=new MaterialPropertyBlock[entry.Original.Length];
                entry.Working=new MaterialPropertyBlock[entry.Original.Length];
                for(int i=0;i<entry.Original.Length;i++)
                {
                    entry.Saved[i]=new MaterialPropertyBlock();
                    entry.Working[i]=new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(entry.Saved[i],i);
                    renderer.GetPropertyBlock(entry.Working[i],i);
                }
                _renderers.Add(renderer,entry);
                renderer.sharedMaterials=entry.Fading;
            }
            renderer.enabled=true;
            for(int i=0;i<entry.Original.Length;i++)
            {
                Color color=entry.Original[i].GetColor(BaseColor);
                color.a*=opacity;
                entry.Working[i].SetColor(BaseColor,color);
                renderer.SetPropertyBlock(entry.Working[i],i);
            }
        }
        private Material FadingMaterial(Material source)
        {
            if(_materials.TryGetValue(source,out var material))return material;
            if(source.shader.name!="Universal Render Pipeline/Unlit")
                throw new InvalidOperationException("描线渐变只接受正式 Unlit 描线材质："+source.name);
            material=new Material(_shader) { name=source.name+"（描线交接）",hideFlags=HideFlags.HideAndDontSave };
            material.SetColor(BaseColor,source.GetColor(BaseColor));
            material.SetFloat("_Cull",source.GetFloat("_Cull"));
            _materials.Add(source,material);
            return material;
        }
        private void Restore(Renderer renderer)
        {
            if (renderer is null) return;
            if(!_renderers.TryGetValue(renderer,out var entry))return;
            _renderers.Remove(renderer);
            if(renderer!=null)
            {
                renderer.sharedMaterials=entry.Original;
                for(int i=0;i<entry.Saved.Length;i++)renderer.SetPropertyBlock(entry.Saved[i],i);
            }
        }
        public void Dispose()
        {
            foreach(var renderer in _renderers.Keys.ToArray())Restore(renderer);
            foreach(var material in _materials.Values)UnityEngine.Object.DestroyImmediate(material);
            _materials.Clear();
        }
    }
}
