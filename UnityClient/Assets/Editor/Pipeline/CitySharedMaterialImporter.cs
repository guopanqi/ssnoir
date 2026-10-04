#if UNITY_EDITOR
#nullable enable
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SSNoir.Editor
{
    /// <summary>正式 CityBox 模型按名字引用 Unity 共享材质；不把 Blender BSDF 覆盖到 Unity 材质。</summary>
    internal sealed class CitySharedMaterialImporter : AssetPostprocessor
    {
        private bool IsCityModel => assetPath == "Assets/Resources/Models/Environment/City.fbx"
            || (assetPath.StartsWith("Assets/Resources/City/Places/",StringComparison.Ordinal) && assetPath.EndsWith(".fbx",StringComparison.Ordinal));
        public override uint GetVersion() => 4;
        public override int GetPostprocessOrder() => 1000;
        private static CityWorldPalette Palette() => AssetDatabase.LoadAssetAtPath<CityWorldPalette>("Assets/Resources/City/WorldPalette.asset")
            ?? throw new InvalidOperationException("缺少 Unity 世界视觉基准资产");
        private void OnPreprocessModel()
        {
            if (!IsCityModel) return;
            context.DependsOnSourceAsset("Assets/Resources/City/WorldPalette.asset");
            context.DependsOnSourceAsset("Assets/Resources/City/World.visual.json");
            var importer = (ModelImporter)assetImporter;
            foreach (var surface in Palette().Surfaces)
                foreach (var name in surface.SourceNames)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name),surface.Material);
            importer.generateSecondaryUV = assetPath.EndsWith("/晚宴.fbx",StringComparison.Ordinal);
            if (importer.generateSecondaryUV) importer.secondaryUVPackMargin = 12;
            if (assetPath.EndsWith("/Environment/City.fbx",StringComparison.Ordinal)) importer.importLights = true;
        }
        private void OnPostprocessModel(GameObject root)
        {
            if (!IsCityModel) return;
            var palette = Palette();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => {
                    if (m == null) throw new InvalidOperationException("CityBox 材质槽缺失：" + renderer.name);
                    var shared = palette.FindMaterial(m.name);
                    if (shared == null && !palette.Surfaces.Any(s => s.Material == m) && m.name != "RiverFlowUV")
                        throw new InvalidOperationException("Unity 尚未实现材质角色："+m.name);
                    return shared ?? m;
                }).ToArray();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                bool line = renderer.sharedMaterials.All(m => m.shader.name == "Universal Render Pipeline/Unlit");
                renderer.shadowCastingMode = line ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = !line;
            }
            var file = "Assets/Resources/City/World.visual.json";
            var spec = JsonUtility.FromJson<CityWorldVisuals.Spec>(File.ReadAllText(file));
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                var row = spec.lights.Single(l => l.name == light.name);
                CityWorldVisuals.Configure(light,row,palette);
                light.enabled = false; // 初始为全城状态，地点聚焦由统一状态控制。
            }
        }
    }
}
#endif
