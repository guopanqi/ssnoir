#nullable enable
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SSNoir.Editor
{
    /// <summary>
    /// 编辑器取景时只看 City 的 High 描线。Low / High 都留在模型里是运行时焦点切换的契约，
    /// 不能为了出参考图而改模型或 Renderer.enabled。
    /// </summary>
    [InitializeOnLoad]
    internal static class CityOutlineEditorPreview
    {
        private const string OutlinePrefix = "描线_";
        private const string LowSuffix = "_Low";
        private const string HighSuffix = "_High";

        static CityOutlineEditorPreview()
        {
            EditorApplication.hierarchyChanged += ApplyHighPreview;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ClearPreviewOverride;
            EditorApplication.delayCall += ApplyHighPreview;
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
                    ApplyHighPreview();
                    break;
            }
        }

        private static void ApplyHighPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ForEachOutlineRenderer((renderer, isLow) =>
            {
                // forceRenderingOff 是非序列化的渲染覆盖；不会制造场景修改或 FBX Prefab override。
                renderer.forceRenderingOff = isLow;
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

                        if (name.EndsWith(LowSuffix, StringComparison.Ordinal))
                            action(renderer, true);
                        else if (name.EndsWith(HighSuffix, StringComparison.Ordinal))
                            action(renderer, false);
                    }
                }
            }
        }
    }
}
