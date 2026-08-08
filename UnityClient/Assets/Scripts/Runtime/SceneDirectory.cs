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
                string nodeName = anchor.ResolvedNodeName;
                if (!string.IsNullOrEmpty(nodeName))
                {
                    if (_anchors.TryGetValue(nodeName, out var existing))
                    {
                        var message =
                            $"SceneDirectory found duplicate NodeName '{nodeName}' " +
                            $"on '{GetHierarchyPath(existing.transform)}' and " +
                            $"'{GetHierarchyPath(anchor.transform)}'. " +
                            "Anchor NodeName must be globally unique and exactly match GameNode.Name.";
                        Debug.LogError(message, anchor);
                        Assert.IsTrue(false, message);
                        throw new InvalidOperationException(message);
                    }

                    _anchors.Add(nodeName, anchor);
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

        public NodeAnchor GetAnchor(string nodeName)
        {
            if (_anchors.TryGetValue(nodeName, out var anchor))
            {
                return anchor;
            }
            return null;
        }

        public ICollection<NodeAnchor> AllAnchors => _anchors.Values;
    }
}
