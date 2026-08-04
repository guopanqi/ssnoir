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
        public string TargetNodeName { get; }

        public RestBlocker(string id, string reason, string locationName, string targetNodeName)
        {
            Id = id;
            Reason = reason;
            LocationName = locationName;
            TargetNodeName = targetNodeName;
        }
    }

    public static class RestBlockerPresentation
    {
        public static bool IsTarget(GameNode node, IReadOnlyList<RestBlocker> blockers)
        {
            foreach (var blocker in blockers)
            {
                if (string.Equals(node.Name, blocker.TargetNodeName, StringComparison.Ordinal))
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
                if (ContainsTarget(node, blocker.TargetNodeName))
                    return blocker;
            }
            return null;
        }

        private static bool ContainsTarget(GameNode node, string targetNodeName)
        {
            if (string.Equals(node.Name, targetNodeName, StringComparison.Ordinal))
                return true;
            foreach (var child in node.Children)
            {
                if (ContainsTarget(child, targetNodeName))
                    return true;
            }
            return false;
        }
    }
}
