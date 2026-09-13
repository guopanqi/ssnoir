#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SSNoir.Editor
{
    /// <summary>
    /// 编辑器取景时看到的是"聚焦地点"的样子：地点细节（标准描线 + 内部）在运行时才由
    /// CityPlaces 从 Resources/City/Places 挂进来，这里在编辑模式下用不保存的临时实例补上，
    /// 并藏掉远景描线。临时实例带 DontSave，不会进场景文件；进 Play 前全部删掉，由运行时重建。
    /// 不能为了出参考图而改模型或 Renderer.enabled。
    /// </summary>
    [InitializeOnLoad]
    internal static class CityOutlineEditorPreview
    {
        private const string CityRootName = "City";
        private const string OutlinePrefix = "描线_";
        private const string FarSuffix = "_远景";
        private static bool _applying;

        static CityOutlineEditorPreview()
        {
            EditorApplication.hierarchyChanged += ApplyFocusedPreview;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ClearPreview;
            EditorApplication.delayCall += ApplyFocusedPreview;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                case PlayModeStateChange.EnteredPlayMode:
                    ClearPreview();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    ApplyFocusedPreview();
                    break;
            }
        }

        private static void ApplyFocusedPreview()
        {
            if (_applying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            _applying = true;
            try
            {
                foreach (var city in CityRoots())
                {
                    foreach (var shell in CityPlaces.ShellRoots(city).ToArray())
                    {
                        if (PreviewInstances(city).Any(t => t.name == shell.name))
                            continue;
                        var asset = Resources.Load<GameObject>(CityPlaces.ResourcesFolder + shell.name);
                        if (asset == null)
                            continue;   // 还没发布过的地点：编辑器里就没有细节，不算错
                        var instance = CityPlaces.Attach(city, shell.name, asset);
                        instance.hideFlags = HideFlags.DontSave;
                    }
                }

                ForEachOutlineRenderer((renderer, isFar) =>
                {
                    // forceRenderingOff 是非序列化的渲染覆盖；不会制造场景修改或 FBX Prefab override。
                    renderer.forceRenderingOff = isFar;
                });
            }
            finally
            {
                _applying = false;
            }
        }

        private static void ClearPreview()
        {
            foreach (var city in CityRoots())
                foreach (var instance in PreviewInstances(city).ToArray())
                    UnityEngine.Object.DestroyImmediate(instance.gameObject);
            ForEachOutlineRenderer((renderer, _) => renderer.forceRenderingOff = false);
        }

        private static IEnumerable<Transform> PreviewInstances(Transform city)
        {
            foreach (Transform child in city)
                if ((child.gameObject.hideFlags & HideFlags.DontSave) == HideFlags.DontSave)
                    yield return child;
        }

        private static IEnumerable<Transform> CityRoots()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (string.Equals(t.name, CityRootName, StringComparison.Ordinal))
                            yield return t;
            }
        }

        private static void ForEachOutlineRenderer(Action<Renderer, bool> action)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        string name = renderer.gameObject.name;
                        if (!name.StartsWith(OutlinePrefix, StringComparison.Ordinal))
                            continue;

                        action(renderer, name.EndsWith(FarSuffix, StringComparison.Ordinal));
                    }
                }
            }
        }
    }
}
