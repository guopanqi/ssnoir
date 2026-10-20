#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    // 休息阻塞的唯一事实来源仍是 engine.scm 的注册表。
    // 这份镜像只把注册表交给快照和客户端，绝不反过来决定能否休息。
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
