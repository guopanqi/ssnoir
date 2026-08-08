#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SSNoir.Editor
{
    [CustomEditor(typeof(NodeAnchor))]
    internal class NodeAnchorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var anchor = (NodeAnchor)target;
            if (string.IsNullOrWhiteSpace(anchor.NodeName))
            {
                EditorGUILayout.HelpBox(
                    $"Node Name 留空：会使用 GameObject 名“{anchor.gameObject.name}”作为节点名。改名会断，建议显式填写。",
                    MessageType.Info);
            }
        }
    }
}
#endif
