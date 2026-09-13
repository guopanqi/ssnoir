#nullable enable
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SSNoir.Editor
{
    /// <summary>
    /// 编辑器取景时看 City 的标准描线、藏远景描线（运行时聚焦地点的样子）。两套都留在模型里
    /// 是运行时焦点切换的契约，不能为了出参考图而改模型或 Renderer.enabled。
    /// </summary>
    [InitializeOnLoad]
    internal static class CityOutlineEditorPreview
    {
        private const string OutlinePrefix = "描线_";
        private const string FarSuffix = "_远景";

        static CityOutlineEditorPreview()
        {
            EditorApplication.hierarchyChanged += ApplyFocusedPreview;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ClearPreviewOverride;
            EditorApplication.delayCall += ApplyFocusedPreview;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                case PlayModeStateChange.EnteredPlayMode:
                    ClearPreviewOverride();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    ApplyFocusedPreview();
                    break;
            }
        }

        private static void ApplyFocusedPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ForEachOutlineRenderer((renderer, isFar) =>
            {
                // forceRenderingOff 是非序列化的渲染覆盖；不会制造场景修改或 FBX Prefab override。
                renderer.forceRenderingOff = isFar;
            });
        }

        private static void ClearPreviewOverride()
        {
            ForEachOutlineRenderer((renderer, _) => renderer.forceRenderingOff = false);
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
