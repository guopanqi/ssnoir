#if UNITY_EDITOR
#nullable enable
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SSNoir.Editor
{
    /// <summary>
    /// 一次性迁移：勒索信 场景已进 CityBox（prefabs/勒索信.blend，嵌在 码头 里，锚点
    /// 勒索信-报摊 / 勒索信-巷口 随 City.fbx 导入）。Main.unity 里手摆的旧模型实例和旧锚点要删掉，
    /// 否则 SceneDirectory 会因锚点重名拒绝启动。跑完自删。
    /// </summary>
    internal static class RemoveLegacyDeliveryScene
    {
        private static readonly string[] LegacyObjects = { "码头送信场景", "勒索信-报摊", "勒索信-巷口" };
        private static readonly string[] LegacyAssets =
        {
            "Assets/Resources/Models/码头送信场景.blend",
            "Assets/Resources/Models/码头送信场景.blend1",
        };

        [MenuItem("SSNoir/迁移/删除旧的码头送信场景（勒索信已进 CityBox）")]
        private static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            int removed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (t == null || !LegacyObjects.Contains(t.name))
                        continue;
                    // 只删场景里手摆的那份；City.fbx 导入的同名锚点在 City 之下，不动
                    if (IsUnderCity(t))
                        continue;
                    Undo.DestroyObjectImmediate(t.gameObject);
                    removed++;
                }
            }
            foreach (var path in LegacyAssets)
                if (AssetDatabase.LoadMainAssetAtPath(path) != null && AssetDatabase.DeleteAsset(path))
                    Debug.Log($"[SSNoir] 已删除 {path}");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SSNoir] 旧码头送信场景迁移完成：删除 {removed} 个场景对象。");
            AssetDatabase.DeleteAsset("Assets/Editor/Migrations/RemoveLegacyDeliveryScene.cs");
        }

        private static bool IsUnderCity(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == "City") return true;
            return false;
        }
    }
}
#endif
