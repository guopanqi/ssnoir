#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SSNoir.Editor
{
    public sealed class ImportDependencyCheck : AssetPostprocessor
    {
        private const string Visual = "Assets/Resources/City/World.visual.json";
        private const string City = "Assets/Resources/Models/Environment/City.fbx";
        private const string A = "Assets/Resources/City/Places/ProbeA.fbx";
        private const string B = "Assets/Resources/City/Places/ProbeB.fbx";
        private static readonly HashSet<string> Imported = new HashSet<string>();
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
        {
            foreach (var path in imported.Where(p => p.EndsWith(".fbx", StringComparison.Ordinal)))
                Imported.Add(path);
        }

        public static void Run()
        {
            if (!Application.isBatchMode || !File.Exists(".import-dependency-check"))
                throw new InvalidOperationException("只能在隔离的导入依赖验证工程运行");
            CitySharedMaterialImporter.RegisterVisualDependencies();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in new[] { City, A, B })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    throw new Exception("探针模型导入失败：" + path);
            var spec = JsonUtility.FromJson<CityWorldVisuals.Spec>(File.ReadAllText(Visual));
            var checks = new List<string>();
            Imported.Clear();
            if (File.ReadAllBytes(A).SequenceEqual(File.ReadAllBytes("ProbeA-updated.fbx")))
                throw new Exception("探针 A 没有几何变化，请先重新生成初始探针");
            File.Copy("ProbeA-updated.fbx", A, true);
            // 探针在同一秒生成且大小相同，显式导入以避免扫描器仅按时间戳跳过。
            AssetDatabase.ImportAsset(A, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!Imported.Where(p => p == City || p == A || p == B).ToHashSet().SetEquals(new[] { A }))
                throw new Exception("单独更新 A 的 FBX：实际导入 " + string.Join(",", Imported));
            checks.Add("单独更新 A 的 FBX：只重导 ProbeA.fbx");
            Check(spec, "仅修改地点 FBX 哈希", s => s.placeHashes[0].hash += "-changed", new string[0], checks);
            Check(spec, "仅修改地点环境", s => s.profiles[0].fogStart += 1, new string[0], checks);
            Check(spec, "仅重排声明顺序", s => { Array.Reverse(s.lights); Array.Reverse(s.surfaceRoles); }, new string[0], checks);
            Check(spec, "修改 A 组灯光", s => { foreach (var l in s.lights.Where(l => l.profile == "ProbeA")) l.energy += 1; }, new[] { City, A }, checks);
            // 同时移除元数据与 FBX 中的灯会由 FBX 更新触发导入。这里只检查空组哈希也会变化。
            var before = CitySharedMaterialImporter.VisualDependencyHashes(spec);
            var removed = JsonUtility.FromJson<CityWorldVisuals.Spec>(JsonUtility.ToJson(spec));
            removed.lights = removed.lights.Where(l => l.profile != "ProbeA").ToArray();
            var after = CitySharedMaterialImporter.VisualDependencyHashes(removed);
            if (before["SSNoir/CityVisual/Lights/ProbeA"] == after["SSNoir/CityVisual/Lights/ProbeA"])
                throw new Exception("删除最后一盏灯未使依赖失效");
            checks.Add("删除最后一盏灯：组哈希变化");
            Check(spec, "修改公共角色契约", s => s.surfaceRoles = s.surfaceRoles.Skip(1).ToArray(), new[] { City, A, B }, checks);
            File.WriteAllLines("dependency-check-results.txt", checks);
            Debug.Log("IMPORT_DEPENDENCY_CHECK_PASS\n" + string.Join("\n", checks));
        }

        private static void Check(CityWorldVisuals.Spec spec, string name, Action<CityWorldVisuals.Spec> edit,
            string[] expected, List<string> checks)
        {
            edit(spec);
            Imported.Clear();
            File.WriteAllText(Visual, JsonUtility.ToJson(spec));
            AssetDatabase.ImportAsset(Visual, ImportAssetOptions.ForceSynchronousImport);
            CitySharedMaterialImporter.RegisterVisualDependencies();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var models = Imported.Where(p => p == City || p == A || p == B).ToHashSet();
            if (!models.SetEquals(expected))
                throw new Exception(name + "：预期 " + string.Join(",", expected) + "；实际 " + string.Join(",", models));
            checks.Add(name + "：重导 " + models.Count + " 个模型" + (models.Count == 0 ? "" : "（" + string.Join(",", models.Select(Path.GetFileName)) + "）"));
        }
    }
}
#endif
