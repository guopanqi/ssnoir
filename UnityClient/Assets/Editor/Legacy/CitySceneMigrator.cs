#if UNITY_EDITOR
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SSNoir.Editor
{
    /// <summary>
    /// One-time migration from individually placed city model prefabs to CityBox's City.fbx.
    /// Generated model content belongs to CityBox; scene-authored objects are preserved under
    /// CityRuntimeOverlay so future FBX reimports cannot delete them.
    /// </summary>
    internal static class CitySceneMigrator
    {
        private const string CityAssetPath =
            "Assets/Resources/Models/Environment/City.fbx";
        private const string OverlayName = "CityRuntimeOverlay";

        private static readonly HashSet<string> LegacyAssetPaths = new HashSet<string>(
            new[]
            {
                "Assets/Resources/Models/Environment/city_greybox.fbx",
                "Assets/Resources/Models/Environment/river.blend",
                "Assets/Resources/Models/Environment/公园.blend",
                "Assets/Resources/Models/Props/布告栏.blend",
                "Assets/Resources/Models/Buildings/码头居民区.blend",
                "Assets/Resources/Models/Buildings/警察局.blend",
                "Assets/Resources/Models/Buildings/大厦.blend",
                "Assets/Resources/Models/Buildings/老街市集.blend",
                "Assets/Resources/Models/Buildings/老街酒馆.blend",
                "Assets/Resources/Models/Buildings/码头.blend",
                "Assets/Resources/Models/Buildings/剧院.blend",
                "Assets/Resources/Models/Buildings/公寓.blend",
                "Assets/Resources/Models/Buildings/诊所.blend",
                "Assets/Resources/Models/Buildings/city_block.blend",
            },
            StringComparer.Ordinal);

        [MenuItem("SSNoir/City/迁移当前场景到 CityBox 整城")]
        private static void MigrateActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("不能在 Play Mode 中迁移城市场景");

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("没有已加载的活动场景");

            var cityAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CityAssetPath);
            if (cityAsset == null)
                throw new InvalidOperationException($"找不到整城资产: {CityAssetPath}");

            var roots = scene.GetRootGameObjects();
            var legacyRoots = roots
                .Where(root => LegacyAssetPaths.Contains(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)))
                .ToArray();
            var existingCity = roots.FirstOrDefault(root => string.Equals(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root),
                CityAssetPath,
                StringComparison.Ordinal));

            if (legacyRoots.Length == 0 && existingCity != null)
            {
                EditorUtility.DisplayDialog("CityBox", "当前场景已经使用整城资产。", "确定");
                return;
            }

            ValidateNoUnsupportedOverrides(legacyRoots);
            if (!EditorUtility.DisplayDialog(
                    "迁移到 CityBox 整城",
                    $"将移除 {legacyRoots.Length} 个已经由 CityBox 覆盖的旧模型实例。\n\n" +
                    "旧 prefab 下手工添加的 GameObject 会先移到 CityRuntimeOverlay；" +
                    "独立道具和车辆不受影响。该命令只应在 Timeline 制作开始前运行。",
                    "迁移并保存",
                    "取消"))
                return;

            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("迁移到 CityBox 整城");

            var overlay = roots.FirstOrDefault(root => root.name == OverlayName);
            if (overlay == null)
            {
                overlay = new GameObject(OverlayName);
                Undo.RegisterCreatedObjectUndo(overlay, "创建城市运行时覆盖层");
                SceneManager.MoveGameObjectToScene(overlay, scene);
            }

            var oldCityTransform = legacyRoots
                .FirstOrDefault(root => string.Equals(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root),
                    "Assets/Resources/Models/Environment/city_greybox.fbx",
                    StringComparison.Ordinal))
                ?.transform;
            var position = oldCityTransform != null ? oldCityTransform.position : Vector3.zero;
            var rotation = oldCityTransform != null ? oldCityTransform.rotation : Quaternion.identity;
            // 新旧整城 FBX 的原始边界同为约 2800m；当前游戏场景使用 0.1 倍。
            var scale = oldCityTransform != null ? oldCityTransform.localScale : Vector3.one * 0.1f;

            var movedObjects = new List<string>();
            foreach (var legacyRoot in legacyRoots)
            {
                MoveAddedGameObjectsToOverlay(legacyRoot, overlay.transform, movedObjects);
                Undo.DestroyObjectImmediate(legacyRoot);
            }

            if (existingCity == null)
            {
                var instance = PrefabUtility.InstantiatePrefab(cityAsset, scene) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException("实例化 City.fbx 失败");
                Undo.RegisterCreatedObjectUndo(instance, "实例化 CityBox 整城");
                instance.name = "City";
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.transform.localScale = scale;
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("城市迁移已执行，但场景保存失败");

            Debug.Log(
                $"[SSNoir] City migration complete. Removed {legacyRoots.Length} legacy model " +
                $"instances and moved {movedObjects.Count} scene-authored objects to {OverlayName}: " +
                string.Join(", ", movedObjects));
        }

        private static void ValidateNoUnsupportedOverrides(IEnumerable<GameObject> legacyRoots)
        {
            var problems = new List<string>();
            foreach (var root in legacyRoots)
            {
                var addedComponents = PrefabUtility.GetAddedComponents(root);
                var removedComponents = PrefabUtility.GetRemovedComponents(root);
                var removedObjects = PrefabUtility.GetRemovedGameObjects(root);
                if (addedComponents.Count > 0)
                    problems.Add($"{root.name}: 有 {addedComponents.Count} 个加在 prefab 节点上的组件");
                if (removedComponents.Count > 0 || removedObjects.Count > 0)
                    problems.Add($"{root.name}: 有删除型 prefab override");

                // 旧模型上的材质、相机和激活状态 override 都属于即将删除的旧视觉资产，
                // 不迁移。只有新增/删除组件可能承载剧情或运行时代码，必须人工处理。
            }

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "旧城市模型上存在不能自动迁移的 Unity 配置。先把它们移到 " +
                    OverlayName + " 再重试：\n" + string.Join("\n", problems));
            }
        }

        private static void MoveAddedGameObjectsToOverlay(
            GameObject prefabRoot,
            Transform overlay,
            ICollection<string> movedObjects)
        {
            var added = PrefabUtility.GetAddedGameObjects(prefabRoot)
                .Select(record => record.instanceGameObject)
                .Where(gameObject => gameObject != null)
                .ToArray();
            var addedSet = new HashSet<GameObject>(added);

            foreach (var gameObject in added)
            {
                var hasAddedAncestor = false;
                for (var parent = gameObject.transform.parent;
                     parent != null && parent != prefabRoot.transform;
                     parent = parent.parent)
                {
                    if (addedSet.Contains(parent.gameObject))
                    {
                        hasAddedAncestor = true;
                        break;
                    }
                }

                if (hasAddedAncestor)
                    continue;

                movedObjects.Add(gameObject.name);
                Undo.SetTransformParent(gameObject.transform, overlay, "保留场景手工节点");
            }
        }
    }
}
#endif
