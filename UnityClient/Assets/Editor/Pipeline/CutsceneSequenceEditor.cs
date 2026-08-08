#nullable enable
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SSNoir.Editor
{
    /// <summary>
    /// CutsceneSequence 的 Inspector：默认面板之外补一个「从子物体填充」，以及一份
    /// 配置体检。
    ///
    /// 镜头列表是显式的（同一机位可以在一场戏里出现两次，层级顺序表达不了），代价是它会和
    /// 层级脱节——加了子物体忘了进列表，或者删了子物体留下空槽。两种都不会报错，只会表现成
    /// "过场少了一镜"，很难往配置上想。所以这里直接把差异摆出来。
    /// </summary>
    [CustomEditor(typeof(CutsceneSequence))]
    internal sealed class CutsceneSequenceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var sequence = (CutsceneSequence)target;

            EditorGUILayout.Space();

            if (GUILayout.Button("从子物体填充镜头列表"))
            {
                var found = sequence.GetComponentsInChildren<CutsceneShot>(true);
                Undo.RecordObject(sequence, "Fill Cutscene Shots");
                sequence.Shots = new List<CutsceneShot>(found);
                EditorUtility.SetDirty(sequence);
            }

            DrawDiagnostics(sequence);
        }

        private static void DrawDiagnostics(CutsceneSequence sequence)
        {
            if (string.IsNullOrWhiteSpace(sequence.SequenceId))
            {
                EditorGUILayout.HelpBox(
                    $"Sequence Id 留空，剧本里的 tag 要写 GameObject 名「{sequence.name}」。"
                    + "改名就会断，建议显式填一个。",
                    MessageType.Info);
            }

            int emptySlots = 0;
            foreach (var shot in sequence.Shots)
            {
                if (shot == null)
                    emptySlots++;
            }

            if (emptySlots > 0)
            {
                EditorGUILayout.HelpBox(
                    $"列表里有 {emptySlots} 个空槽，播放时会被跳过。多半是删了子物体没清列表。",
                    MessageType.Warning);
            }

            // 挂在子物体上却没进列表的镜头——这个不会报错，只会静静地不播。
            var inChildren = sequence.GetComponentsInChildren<CutsceneShot>(true);
            var listed = new HashSet<CutsceneShot>(sequence.Shots);
            var missing = new List<string>();
            foreach (var shot in inChildren)
            {
                if (!listed.Contains(shot))
                    missing.Add(shot.name);
            }

            if (missing.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "这些镜头挂在子物体上但不在列表里，不会播放：\n  " + string.Join("\n  ", missing),
                    MessageType.Warning);
            }

            // 同一 tag 出现两次，Find 只会拿到先扫到的那个，另一场永远播不出来。
            foreach (var other in Object.FindObjectsOfType<CutsceneSequence>(true))
            {
                if (!ReferenceEquals(other, sequence) && other.ResolvedId == sequence.ResolvedId)
                {
                    EditorGUILayout.HelpBox(
                        $"tag「{sequence.ResolvedId}」和 '{other.name}' 重复了。"
                        + "按 tag 查找只会命中其中一个，另一个永远播不到。",
                        MessageType.Error);
                    break;
                }
            }
        }
    }
}
