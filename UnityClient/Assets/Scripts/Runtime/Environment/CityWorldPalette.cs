#nullable enable
using System;
using System.Linq;
using UnityEngine;

namespace SSNoir
{
    /// <summary>Unity 对世界视觉语言的实现。材质是共享资产，CityBox 重建不改写这些值。</summary>
    public sealed class CityWorldPalette : ScriptableObject
    {
        [Serializable] public sealed class Surface
        {
            public string[] SourceNames = Array.Empty<string>();
            public Material Material = null!;
        }
        [Serializable] public sealed class LightColor
        {
            public string Name = "";
            public Color ColorLinear = Color.white;
        }
        public Surface[] Surfaces = Array.Empty<Surface>();
        public LightColor[] LightColors = Array.Empty<LightColor>();
        public float BakedAreaScale = 0.35f;
        public float FloorHighlight = 0.12f;
        public Material? FindMaterial(string source) => Surfaces.SingleOrDefault(s => s.SourceNames.Contains(source))?.Material;
        public Color FindLightColor(string name) => LightColors.Single(c => c.Name == name).ColorLinear;
        public static CityWorldPalette Load() => Resources.Load<CityWorldPalette>("City/WorldPalette")
            ?? throw new InvalidOperationException("缺少 Unity 世界视觉基准 City/WorldPalette");
    }
}
