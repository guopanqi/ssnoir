using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace SSNoir
{
    public class SceneDirectory : MonoBehaviour
    {
        private readonly Dictionary<string, NodeAnchor> _anchors = new Dictionary<string, NodeAnchor>();

        private void Awake()
        {
            CollectAnchors();
        }

        public void CollectAnchors()
        {
            _anchors.Clear();
            var found = FindObjectsOfType<NodeAnchor>(true);
            foreach (var anchor in found)
            {
                string anchorName = anchor.ResolvedNodeName;
                if (!string.IsNullOrEmpty(anchorName))
                {
                    if (_anchors.TryGetValue(anchorName, out var existing))
                    {
                        var message =
                            $"SceneDirectory found duplicate AnchorName '{anchorName}' " +
                            $"on '{GetHierarchyPath(existing.transform)}' and " +
                            $"'{GetHierarchyPath(anchor.transform)}'. " +
                            "Anchor names must be globally unique.";
                        Debug.LogError(message, anchor);
                        Assert.IsTrue(false, message);
                        throw new InvalidOperationException(message);
                    }

                    _anchors.Add(anchorName, anchor);
                }
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var parts = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                parts.Push(current.name);
            }

            return string.Join("/", parts);
        }

        public NodeAnchor GetAnchor(string anchorName)
        {
            if (_anchors.TryGetValue(anchorName, out var anchor))
            {
                return anchor;
            }
            return null;
        }

        public ICollection<NodeAnchor> AllAnchors => _anchors.Values;
    }
}
