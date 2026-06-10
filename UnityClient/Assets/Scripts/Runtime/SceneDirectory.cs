using System.Collections.Generic;
using UnityEngine;

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
            var found = GetComponentsInChildren<NodeAnchor>(true);
            foreach (var anchor in found)
            {
                if (!string.IsNullOrEmpty(anchor.NodeName))
                {
                    _anchors[anchor.NodeName] = anchor;
                }
            }
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
