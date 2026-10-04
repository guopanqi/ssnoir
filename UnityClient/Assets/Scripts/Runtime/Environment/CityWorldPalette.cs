#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSNoir
{
    /// <summary>Unity 对世界视觉语言的实现。材质是共享资产，CityBox 重建不改写这些值。</summary>
    public sealed class CityWorldPalette : ScriptableObject
    {
        [Serializable] public sealed class MaterialRole
        {
            public string Name = "";
            // 正式表面按角色生成导出名；基础设施和描线保留构建器的明确命名。
            public string[] ImportNames = Array.Empty<string>();
            public IEnumerable<string> SourceNames => ImportNames.Length == 0 ? new[] {"M_世界_" + Name} : ImportNames;
            public Material Material = null!;
        }
        [Serializable] public sealed class LightColor
        {
            public string Name = "";
            public Color ColorLinear = Color.white;
        }
        public RenderPipelineAsset Pipeline = null!;
        public MaterialRole[] Surfaces = Array.Empty<MaterialRole>();
        public MaterialRole[] Lines = Array.Empty<MaterialRole>();
        // 世界代理轮廓与地点真实模型的主线分开校准；仍共用正式描线可见性契约。
        public Material WorldLandmarkLine = null!;
        public IEnumerable<MaterialRole> Materials => Surfaces.Concat(Lines);
        public LightColor[] LightColors = Array.Empty<LightColor>();
        public CityWorldOverview.Settings Overview = new();
        public CityWorldOverview.NightSettings Night = new();
        public Shader NightGlowShader = null!;
        public float BakedAreaScale = 0.35f;
        public float FloorHighlight = 0.12f;
        public Material? FindMaterial(string source) => Materials.SingleOrDefault(s => s.SourceNames.Contains(source) || s.Name == source)?.Material;
        public Color FindLightColor(string name) => LightColors.Single(c => c.Name == name).ColorLinear;
        public bool IsLine(Material material) => material == WorldLandmarkLine || Lines.Any(role => role.Material == material);
        public void Validate()
        {
            if (Pipeline == null) throw new InvalidOperationException("世界视觉基准缺少渲染管线");
            if (WorldLandmarkLine == null || Materials.Any(role => role.Material == WorldLandmarkLine))
                throw new InvalidOperationException("地标远景线必须使用独立共享材质");
            var names = new HashSet<string>(StringComparer.Ordinal);
            var sources = new HashSet<string>(StringComparer.Ordinal);
            var materials = new HashSet<Material>();
            foreach (var role in Materials)
            {
                if (string.IsNullOrWhiteSpace(role.Name) || !names.Add(role.Name))
                    throw new InvalidOperationException("世界视觉角色名称为空或重复：" + role.Name);
                if (role.Material == null || !materials.Add(role.Material))
                    throw new InvalidOperationException("世界视觉材质缺失或重复登记：" + role.Name);
                foreach (var source in role.SourceNames)
                    if (string.IsNullOrWhiteSpace(source) || !sources.Add(source))
                        throw new InvalidOperationException("世界视觉导入名称为空或重复：" + source);
            }
            if (LightColors.Any(c => string.IsNullOrWhiteSpace(c.Name)) || LightColors.Select(c => c.Name).Distinct().Count() != LightColors.Length)
                throw new InvalidOperationException("世界灯光色名称为空或重复");
        }
        public void ValidateSpec(CityWorldVisuals.Spec spec)
        {
            if (spec.version != 1 || spec.surfaceRoles.Length == 0 || spec.lineRoles.Length == 0 || spec.lightPalettes.Length == 0)
                throw new InvalidOperationException("世界视觉声明缺少正式角色契约，请重建 CityBox");
            foreach (var name in spec.surfaceRoles)
                if (!Surfaces.Any(role => role.Name == name))
                    throw new InvalidOperationException("Unity 缺少表面实现：" + name);
            foreach (var name in spec.lineRoles)
                if (!Lines.Any(role => role.Name == name))
                    throw new InvalidOperationException("Unity 缺少描线实现：" + name);
            foreach (var name in spec.lightPalettes)
                if (!LightColors.Any(role => role.Name == name))
                    throw new InvalidOperationException("Unity 缺少灯光色实现：" + name);
        }
        public static CityWorldPalette Load()
        {
            var palette = Resources.Load<CityWorldPalette>("City/WorldPalette")
                ?? throw new InvalidOperationException("缺少 Unity 世界视觉基准 City/WorldPalette");
            palette.Validate();
            return palette;
        }
    }
}
