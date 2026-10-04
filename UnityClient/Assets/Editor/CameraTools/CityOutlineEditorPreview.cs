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
                // 只在离开编辑模式时清。EnteredPlayMode 是在 Start 之后才到的：那时 City 下带标记的
                // 已经是运行时自己挂的细节，再清一遍就把 CityOutlineState 手里的对象销毁了
                //（新游戏一落快照就 MissingReferenceException）。
                case PlayModeStateChange.ExitingEditMode:
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
                        try
                        {
                            CityPlaces.Attach(city, shell.name, asset, HideFlags.DontSave);
                        }
                        catch (System.Exception ex)
                        {
                            // 刚发布完、clips.json 还在导入这类瞬时状态：这个地点这次没预览，别拖累别的地点
                            Debug.LogWarning($"[SSNoir] 编辑器预览跳过地点 '{shell.name}'：{ex.Message}");
                        }
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

        /// <summary>取景临时使用同一套地点灯光；结束后还原，不能写进场景覆盖。</summary>
        internal static IDisposable? BeginLightingPreview(Transform camera)
        {
            if (EditorApplication.isPlaying) return null;
            var city = CityRoots().SingleOrDefault(root => camera.IsChildOf(root));
            if (city == null) return null;
            var owner = camera;
            while (owner.parent != city) owner = owner.parent;
            var visuals = new CityWorldVisuals(city);
            visuals.SetFocused(owner.name);
            return visuals;
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

        // 预览实例 = 带标记 + DontSave；运行时挂的细节也带标记但不带 DontSave，这里不能碰它们
        private static IEnumerable<Transform> PreviewInstances(Transform city)
            => city == null
                ? Enumerable.Empty<Transform>()
                : CityPlaces.DetailInstances(city).Where(t => (t.gameObject.hideFlags & HideFlags.DontSave) == HideFlags.DontSave);

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
