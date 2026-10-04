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
        public override uint GetVersion() => 8;
        public override int GetPostprocessOrder() => 1000;
        private const string PalettePath = "Assets/Resources/City/WorldPalette.asset";
        private static CityWorldPalette? ImportedPalette()
        {
            if (!File.Exists(PalettePath))
                throw new InvalidOperationException("缺少 Unity 世界视觉基准资产");
            // 冷导入先登记源资产，再产生 .asset 的导入结果。模型映射在第二阶段完成。
            return AssetDatabase.LoadAssetAtPath<CityWorldPalette>(PalettePath);
        }
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
        {
            if (!imported.Contains(PalettePath)) return;
            var palette = ImportedPalette()
                ?? throw new InvalidOperationException("世界视觉基准导入失败");
            palette.Validate();
            // 原生 .asset 尚未生成时 FBX 只完成几何导入；共享资源就绪后完成材质/灯光映射。
            // 只在 Palette 导入完成时重导模型，不由模型的回调触发自己，避免刷新循环。
            AssetDatabase.ImportAsset("Assets/Resources/Models/Environment/City.fbx", ImportAssetOptions.ForceUpdate);
            foreach (var model in Directory.GetFiles("Assets/Resources/City/Places", "*.fbx"))
                AssetDatabase.ImportAsset(model, ImportAssetOptions.ForceUpdate);
        }
        private void OnPreprocessModel()
        {
            if (!IsCityModel) return;
            // 材质映射消费的是已导入资产；源文件依赖不能保证首次导入的先后顺序。
            context.DependsOnArtifact("Assets/Resources/City/WorldPalette.asset");
            context.DependsOnSourceAsset("Assets/Resources/City/World.visual.json");
            var importer = (ModelImporter)assetImporter;
            var palette = ImportedPalette();
            if (palette != null)
            {
                palette.Validate();
                foreach (var key in importer.GetExternalObjectMap().Keys.Where(key => key.type == typeof(Material)).ToArray())
                    importer.RemoveRemap(key);
                foreach (var role in palette.Materials)
                    foreach (var name in role.SourceNames)
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), role.Material);
            }
            importer.generateSecondaryUV = assetPath.EndsWith("/晚宴.fbx",StringComparison.Ordinal);
            if (importer.generateSecondaryUV) importer.secondaryUVPackMargin = 12;
            if (assetPath.EndsWith("/Environment/City.fbx",StringComparison.Ordinal)) importer.importLights = true;
        }
        private void OnPostprocessModel(GameObject root)
        {
            if (!IsCityModel) return;
            var palette = ImportedPalette();
            if (palette == null) return; // 等待共享资源阶段；OnPostprocessAllAssets 必须完成第二次导入。
            palette.Validate();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => {
                    if (m == null) throw new InvalidOperationException("CityBox 材质槽缺失：" + renderer.name);
                    var shared = palette.FindMaterial(m.name);
                    if (shared == null && !palette.Materials.Any(s => s.Material == m))
                        throw new InvalidOperationException("Unity 尚未实现材质角色："+m.name);
                    var resolved = shared ?? m;
                    bool worldOutline = false;
                    for (var t = renderer.transform; t != null; t = t.parent)
                        if (t.name.StartsWith("描线_world_", StringComparison.Ordinal)) worldOutline = true;
                    return worldOutline && resolved == palette.FindMaterial("主线")
                        ? palette.WorldLandmarkLine : resolved;
                }).ToArray();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                bool line = renderer.sharedMaterials.All(palette.IsLine);
                renderer.shadowCastingMode = line ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = !line;
            }
            var file = "Assets/Resources/City/World.visual.json";
            var spec = JsonUtility.FromJson<CityWorldVisuals.Spec>(File.ReadAllText(file));
            palette.ValidateSpec(spec);
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
