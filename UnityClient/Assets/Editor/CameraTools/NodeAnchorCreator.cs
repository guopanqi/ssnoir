#nullable enable
using Cinemachine;
using UnityEditor;
using UnityEngine;

namespace SSNoir.Editor
{
    /// <summary>
    /// 从 Hierarchy 的 GameObject 菜单创建一个可立即用于节点聚焦的完整组合：
    /// NodeAnchor + 已绑定的子级 Cinemachine VCam。
    /// </summary>
    internal static class NodeAnchorCreator
    {
        [MenuItem("GameObject/SSNoir/Node Anchor（含 Focus Camera）", false, 10)]
        private static void Create(MenuCommand command)
        {
            var anchorObject = new GameObject("新节点");
            GameObjectUtility.SetParentAndAlign(anchorObject, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(anchorObject, "Create Node Anchor");

            var anchor = anchorObject.AddComponent<NodeAnchor>();
            anchor.NodeName = string.Empty;

            var orbitObject = new GameObject("Orbit");
            Undo.RegisterCreatedObjectUndo(orbitObject, "Create Orbit Pivot");
            orbitObject.transform.SetParent(anchorObject.transform, false);

            var cameraObject = new GameObject("Focus Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create Focus Camera");
            cameraObject.transform.SetParent(anchorObject.transform, false);

            var focusCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
            focusCamera.Priority = 5;
            focusCamera.m_Lens.FieldOfView = 27f;
            focusCamera.m_Lens.FarClipPlane = 100f;
            var cameraConfig = cameraObject.AddComponent<SSNoirVirtualCameraConfig>();
            cameraConfig.dragMode = CameraDragMode.Orbit;
            cameraConfig.orbitPivot = orbitObject.transform;
            anchor.FocusVirtualCamera = focusCamera;

            EditorUtility.SetDirty(anchor);
            Selection.activeGameObject = anchorObject;
        }
    }
}
