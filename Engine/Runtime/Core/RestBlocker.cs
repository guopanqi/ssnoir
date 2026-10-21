#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    // 从世界节点树派生的只读投影；节点 ID 同时就是事件身份和导航目标。
    public sealed class RestBlocker
    {
        public string Id { get; }
        public string Reason { get; }
        public string LocationName { get; }
        public string TargetNodeId { get; }

        public RestBlocker(string id, string reason, string locationName, string targetNodeId)
        {
            Id = id;
            Reason = reason;
            LocationName = locationName;
            TargetNodeId = targetNodeId;
        }
    }

    public static class RequiredBeforeRest
    {
        public static IReadOnlyList<RestBlocker> Collect(GameNode root, bool world)
        {
            var result = new List<RestBlocker>();
            void Visit(GameNode node, GameNode? place, bool ancestorsEnabled)
            {
                if (node.IsPlace) place = node;
                bool enabled = ancestorsEnabled && !node.Disabled;
                if (node.RequiredBeforeRest != null)
                {
                    if (!world || place == null)
                        throw new InvalidOperationException($"Required node '{node.Id}' must belong to a world place.");
                    if (!enabled || !HasAction(node))
                        throw new InvalidOperationException($"Required node '{node.Id}' has no enabled processing path.");
                    result.Add(new RestBlocker(node.Id, node.RequiredBeforeRest, place.Id, node.Id));
                }
                foreach (var child in node.Children) Visit(child, place, enabled);
            }
            Visit(root, null, true);
            return result.AsReadOnly();
        }
        private static bool HasAction(GameNode node)
        {
            if (node.Disabled) return false;
            if (node.IsPlace && node.Arrivals.Count > 0) return true;
            if (node.Resolve != null && node.Resolve.Type != ResolveType.Note && node.Resolve.Type != ResolveType.Observe)
                return true;
            foreach (var child in node.Children) if (HasAction(child)) return true;
            return false;
        }
        public static bool Same(IReadOnlyList<RestBlocker> a, IReadOnlyList<RestBlocker> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Id != b[i].Id || a[i].Reason != b[i].Reason || a[i].LocationName != b[i].LocationName)
                    return false;
            return true;
        }
    }

    public static class RestBlockerPresentation
    {
        public static bool IsTarget(GameNode node, IReadOnlyList<RestBlocker> blockers)
        {
            foreach (var blocker in blockers)
            {
                if (string.Equals(node.Id, blocker.TargetNodeId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static bool ContainsTarget(GameNode node, IReadOnlyList<RestBlocker> blockers)
        {
            return FindContained(node, blockers) != null;
        }

        public static RestBlocker? FindContained(GameNode node, IReadOnlyList<RestBlocker> blockers)
        {
            foreach (var blocker in blockers)
            {
                if (ContainsTarget(node, blocker.TargetNodeId))
                    return blocker;
            }
            return null;
        }

        private static bool ContainsTarget(GameNode node, string targetNodeId)
        {
            if (string.Equals(node.Id, targetNodeId, StringComparison.Ordinal))
                return true;
            foreach (var child in node.Children)
            {
                if (ContainsTarget(child, targetNodeId))
                    return true;
            }
            return false;
        }
    }
}
