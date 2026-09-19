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
    /// 编辑器取景时看到地点近景：地点细节（focus 描线 + 内部）在运行时才由
    /// CityPlaces 从 Resources/City/Places 挂进来，这里在编辑模式下用不保存的临时实例补上，
    /// 并藏掉 world 描线。临时实例带 DontSave，不会进场景文件；进 Play 前全部删掉，由运行时重建。
    /// 不能为了出参考图而改模型或 Renderer.enabled。
    /// </summary>
    [InitializeOnLoad]
    internal static class CityOutlineEditorPreview
    {
        private const string CityRootName = "City";
        private const string FocusOutlinePrefix = "描线_focus_";
        private const string WorldOutlinePrefix = "描线_world_";
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

                ForEachOutlineRenderer((renderer, visibility) =>
                {
                    // forceRenderingOff 是非序列化的渲染覆盖；不会制造场景修改或 FBX Prefab override。
                    renderer.forceRenderingOff = visibility == OutlineVisibility.World;
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
                {
                    if (instance != null && instance.gameObject != null)
                        UnityEngine.Object.DestroyImmediate(instance.gameObject);
                }
            ForEachOutlineRenderer((renderer, _) => renderer.forceRenderingOff = false);
        }

        private static IEnumerable<Transform> PreviewInstances(Transform city)
        {
            if (city == null)
                yield break;

            foreach (Transform child in city)
            {
                if (child == null)
                    continue;

                var gameObject = child.gameObject;
                if (gameObject != null && (gameObject.hideFlags & HideFlags.DontSave) == HideFlags.DontSave)
                    yield return child;
            }
        }

        private static IEnumerable<Transform> CityRoots()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root == null)
                        continue;

                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t == null)
                            continue;

                        if (string.Equals(t.name, CityRootName, StringComparison.Ordinal))
                            yield return t;
                    }
                }
            }
        }

        private enum OutlineVisibility { Focus, World }

        private static void ForEachOutlineRenderer(Action<Renderer, OutlineVisibility> action)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root == null)
                        continue;

                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer == null || renderer.gameObject == null)
                            continue;

                        string name = renderer.gameObject.name;
                        if (name.StartsWith(FocusOutlinePrefix, StringComparison.Ordinal))
                            action(renderer, OutlineVisibility.Focus);
                        else if (name.StartsWith(WorldOutlinePrefix, StringComparison.Ordinal))
                            action(renderer, OutlineVisibility.World);
                    }
                }
            }
        }
    }
}
