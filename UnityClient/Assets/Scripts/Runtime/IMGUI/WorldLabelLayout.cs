#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// One screen-space placement contract for world cards, anchored notes and banter.
    /// All distances are virtual GUI pixels. The view owns measurement and drawing;
    /// this solver alone owns safe bounds, occupied regions, locality and collision.
    /// </summary>
    public static class WorldLabelLayout
    {
        public enum Kind { Location, Action, Annotation, Bubble }
        public enum ZoneKind { Hard, Soft }

        // Preferred side is a property of label type, never the anchor's current
        // side of the screen. An action starts above its world subject.
        public static Vector2 PreferredActionCenter(Vector2 anchor, Vector2 size)
            => new Vector2(anchor.x, anchor.y - size.y / 2f - 18f);

        /// <summary>Measured label request. The caller supplies content, not a placement policy.</summary>
        public readonly struct Request
        {
            public readonly string Key;
            public readonly Kind Kind;
            public readonly Vector2 Anchor;
            public readonly Vector2 Preferred;
            public readonly Vector2 Size;
            public readonly int Order;
            public readonly string? GroupOwner;

            public Request(string key, Kind kind, Vector2 anchor, Vector2 preferred,
                Vector2 size, int order = 0, string? groupOwner = null)
            {
                Key = key;
                Kind = kind;
                Anchor = anchor;
                Preferred = preferred;
                Size = size;
                Order = order;
                GroupOwner = groupOwner;
            }
        }

        /// <summary>
        /// Layout memory belongs to an authored scene, not to a camera frame.
        /// When the camera moves, only the projected anchor changes; its label offset
        /// is immutable until the content dimensions or layout context changes.
        /// A label may leave the viewport instead of flipping to the other side.
        /// </summary>
        public sealed class PlacementState
        {
            private readonly struct Entry
            {
                public readonly Vector2 Offset;
                public readonly Vector2 Size;
                public Entry(Vector2 offset, Vector2 size) { Offset = offset; Size = size; }
            }

            private readonly Dictionary<string, Entry> _entries = new();
            private readonly Dictionary<string, string> _groupSignatures = new();

            public bool Contains(string key) => _entries.ContainsKey(key);
            public void Clear() { _entries.Clear(); _groupSignatures.Clear(); }

            /// <summary>Forget removed authored nodes, not nodes merely outside the camera.</summary>
            public void KeepOnly(IEnumerable<string> liveKeys)
            {
                var live = new HashSet<string>(liveKeys);
                var removed = new List<string>();
                foreach (var key in _entries.Keys)
                    if (!live.Contains(key)) removed.Add(key);
                foreach (var key in removed) { _entries.Remove(key); _groupSignatures.Remove(key); }
            }

            /// <summary>
            /// First reserve all remembered placements, then solve genuinely new/changed
            /// labels. Existing labels never flip side because a newcomer, viewport edge
            /// or transient HUD happens to pass through their screen rectangle.
            /// </summary>
            public Dictionary<string, Rect> Arrange(Space space, IReadOnlyList<Request> requests)
            {
                foreach (var request in requests)
                    if (request.GroupOwner != null) return ArrangeGroups(space, requests);
                var ordered = new List<Request>(requests);
                ordered.Sort((a, b) =>
                {
                    int kind = a.Kind.CompareTo(b.Kind);
                    if (kind != 0) return kind;
                    int order = a.Order.CompareTo(b.Order);
                    return order != 0 ? order : string.CompareOrdinal(a.Key, b.Key);
                });

                var result = new Dictionary<string, Rect>(ordered.Count);
                var identities = new HashSet<string>();
                foreach (var request in ordered)
                {
                    if (!identities.Add(request.Key))
                        throw new ArgumentException("Duplicate label key: " + request.Key);
                    if (!TryRemember(request, out var rect)) continue;
                    space.Reserve(rect, ZoneKind.Hard);
                    result.Add(request.Key, rect);
                }
                foreach (var request in ordered)
                {
                    if (result.ContainsKey(request.Key)) continue;
                    result.Add(request.Key, SolveNew(space, request));
                }
                return result;
            }

            // A Clock and its direct actions reserve one rectangle. No transitive
            // graph traversal: shared anchors and risk targets do not imply a group.
            private Dictionary<string, Rect> ArrangeGroups(Space space, IReadOnlyList<Request> requests)
            {
                const float gap = 12f;
                var groups = new Dictionary<string, List<Request>>();
                foreach (var request in requests)
                {
                    if (request.GroupOwner == null) continue;
                    if (!groups.TryGetValue(request.GroupOwner, out var members))
                        groups.Add(request.GroupOwner, members = new List<Request>());
                    members.Add(request);
                }
                var units = new List<Request>();
                var children = new Dictionary<string, List<(Request request, Rect local)>>();
                var grouped = new HashSet<string>();
                foreach (var pair in groups)
                {
                    var members = pair.Value;
                    int ownerIndex = members.FindIndex(r => r.Key == pair.Key);
                    if (ownerIndex < 0 || members.Count < 2) continue;
                    var owner = members[ownerIndex];
                    members.RemoveAt(ownerIndex);
                    members.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order)
                        : string.CompareOrdinal(a.Key, b.Key));
                    float widest = owner.Size.x;
                    foreach (var member in members) widest = Mathf.Max(widest, member.Size.x);
                    float leftWidth = 0f, rightWidth = 0f;
                    for (int i = 0; i < members.Count; i++)
                        if (i % 2 == 0) leftWidth = Mathf.Max(leftWidth, members[i].Size.x);
                        else rightWidth = Mathf.Max(rightWidth, members[i].Size.x);
                    bool columns = members.Count > 1
                        && Mathf.Max(owner.Size.x, leftWidth + gap + rightWidth) <= space.AvailableWidth;
                    float width = columns ? Mathf.Max(owner.Size.x, leftWidth + gap + rightWidth) : widest;
                    var local = new List<(Request request, Rect local)>();
                    local.Add((owner, new Rect((width - owner.Size.x) / 2f, 0, owner.Size.x, owner.Size.y)));
                    float y = owner.Size.y + gap;
                    for (int i = 0; i < members.Count;)
                    {
                        int count = columns ? Math.Min(2, members.Count - i) : 1;
                        float rowHeight = 0f;
                        for (int column = 0; column < count; column++)
                        {
                            var member = members[i + column];
                            float x = columns ? column * (leftWidth + gap) : (width - member.Size.x) / 2f;
                            local.Add((member, new Rect(x, y, member.Size.x, member.Size.y)));
                            rowHeight = Mathf.Max(rowHeight, member.Size.y);
                        }
                        y += rowHeight + gap;
                        i += count;
                    }
                    var signature = string.Empty;
                    foreach (var child in local)
                    {
                        grouped.Add(child.request.Key);
                        signature += child.request.Key + ":" + child.local.ToString() + ";";
                    }
                    if (!_groupSignatures.TryGetValue(owner.Key, out var prior) || prior != signature)
                        _entries.Remove(owner.Key);
                    _groupSignatures[owner.Key] = signature;
                    var size = new Vector2(width, y - gap);
                    units.Add(new Request(owner.Key, Kind.Action, owner.Anchor,
                        PreferredActionCenter(owner.Anchor, size), size, owner.Order));
                    children.Add(owner.Key, local);
                }
                foreach (var request in requests)
                    if (!grouped.Contains(request.Key))
                    {
                        if (_groupSignatures.Remove(request.Key)) _entries.Remove(request.Key);
                        units.Add(new Request(request.Key, request.Kind, request.Anchor,
                            request.Preferred, request.Size, request.Order));
                    }
                var placed = Arrange(space, units);
                var result = new Dictionary<string, Rect>();
                foreach (var unit in units)
                {
                    var rect = placed[unit.Key];
                    if (!children.TryGetValue(unit.Key, out var local))
                    { result.Add(unit.Key, rect); continue; }
                    foreach (var child in local)
                    {
                        var childRect = new Rect(rect.x + child.local.x, rect.y + child.local.y,
                            child.local.width, child.local.height);
                        result.Add(child.request.Key, childRect);
                        // Keep landing/culling memory for each child, while the owner
                        // retains the group's full bounds for the next frame.
                        if (child.request.Key != unit.Key)
                            _entries[child.request.Key] = new Entry(
                                childRect.center - child.request.Anchor, child.request.Size);
                    }
                }
                return result;
            }

            // Banter enters after world cards and uses the exact same placement rules.
            public Rect Place(Space space, in Request request)
            {
                if (TryRemember(request, out var rect))
                {
                    space.Reserve(rect, ZoneKind.Hard);
                    return rect;
                }
                return SolveNew(space, request);
            }

            // 连续气泡只调整高度，底边与横向偏移沿用第一句，尾巴不随文字换行跳位。
            public Rect PlaceBanter(Space space, in Request request)
            {
                Rect rect;
                if (_entries.TryGetValue(request.Key, out var entry))
                {
                    Vector2 center = request.Anchor + entry.Offset;
                    // 在人物上方时钉底边；被避让到下方时钉顶边，连接边不跳动。
                    float y = entry.Offset.y > 0f
                        ? center.y - entry.Size.y / 2f
                        : center.y + entry.Size.y / 2f - request.Size.y;
                    rect = new Rect(center.x - request.Size.x / 2f, y,
                        request.Size.x, request.Size.y);
                    space.Reserve(rect, ZoneKind.Hard);
                    return rect;
                }
                return SolveNew(space, request);
            }

            private bool TryRemember(in Request request, out Rect rect)
            {
                if (_entries.TryGetValue(request.Key, out var entry)
                    && Mathf.Approximately(entry.Size.x, request.Size.x)
                    && Mathf.Approximately(entry.Size.y, request.Size.y))
                {
                    rect = Space.At(request.Anchor + entry.Offset, request.Size);
                    return true; // No screen-edge clamp, no collision-driven side switch.
                }
                rect = default;
                return false;
            }

            private Rect SolveNew(Space space, in Request request)
            {
                Rect rect = space.Place(request.Key, request.Kind, request.Anchor,
                    request.Preferred, request.Size);
                _entries[request.Key] = new Entry(rect.center - request.Anchor, request.Size);
                return rect;
            }
        }

        public sealed class Space
        {
            private const float Edge = 8f;
            private const float Gap = 10f;
            private const float SearchStep = 28f;
            private readonly Rect _safe;
            private readonly List<(Rect rect, ZoneKind kind, bool blocksInput)> _zones = new();
            private readonly HashSet<string> _overflow = new();

            public Space(Rect safe) => _safe = safe;
            public float AvailableWidth => Mathf.Max(0f, _safe.width - Edge * 2f);

            public void Reserve(Rect rect, ZoneKind kind, bool blocksInput = false)
            {
                if (rect.width > 0f && rect.height > 0f)
                    _zones.Add((rect, kind, blocksInput));
            }

            public bool IsUiBlocked(Rect rect)
            {
                foreach (var zone in _zones)
                    if (zone.blocksInput && zone.rect.Overlaps(rect))
                        return true;
                return false;
            }

            public IReadOnlyCollection<string> Overflow => _overflow;

            // Called only for a new label, a measured-size change or a scene reset.
            // A moving camera must never trigger this search for an existing label.
            public Rect Place(string key, Kind kind, Vector2 anchor, Vector2 preferred,
                Vector2 size)
            {
                if (size.x <= 0f || size.y <= 0f)
                    throw new ArgumentException("World label must have positive measured dimensions: " + key);

                float maxReach = Reach(kind);

                // A deterministic finite set: preferred, anchor-relative positions, and
                // obstacle edges. Never scan the entire viewport to "find any empty spot".
                var candidates = new List<Vector2> { preferred };
                float halfW = size.x * 0.5f;
                float halfH = size.y * 0.5f;
                float sx = halfW + 18f;
                float sy = halfH + 18f;
                candidates.Add(new Vector2(anchor.x, anchor.y - sy));
                candidates.Add(new Vector2(anchor.x + sx, anchor.y));
                candidates.Add(new Vector2(anchor.x - sx, anchor.y));
                candidates.Add(new Vector2(anchor.x, anchor.y + sy));
                foreach (float dx in new[] { -sx, sx })
                foreach (float dy in new[] { -sy, sy })
                    candidates.Add(new Vector2(anchor.x + dx, anchor.y + dy));
                for (float dx = -168f; dx <= 168f; dx += SearchStep)
                for (float dy = -168f; dy <= 168f; dy += SearchStep)
                    candidates.Add(preferred + new Vector2(dx, dy));
                foreach (var zone in _zones)
                {
                    Rect r = zone.rect;
                    candidates.Add(new Vector2(r.xMin - halfW - Gap, preferred.y));
                    candidates.Add(new Vector2(r.xMax + halfW + Gap, preferred.y));
                    candidates.Add(new Vector2(preferred.x, r.yMin - halfH - Gap));
                    candidates.Add(new Vector2(preferred.x, r.yMax + halfH + Gap));
                }

                Rect best = default;
                float bestScore = float.PositiveInfinity;
                bool found = false;
                foreach (var center in candidates)
                {
                    Rect rect = At(center, size);
                    if (!Inside(rect)) continue;
                    float reach = DistanceToEdge(anchor, rect);
                    if (reach > maxReach || rect.Contains(anchor)) continue;
                    if (!Fits(rect)) continue;
                    float score = Score(rect, anchor, preferred, kind);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = rect;
                        found = true;
                    }
                }

                if (!found)
                {
                    // Keep an interactive card close to its subject even in an
                    // over-constrained scene. Report the unsatisfied contract; do not
                    // silently send the card across the world or stretch its tether.
                    _overflow.Add(key);
                    bestScore = float.PositiveInfinity;
                    foreach (var center in candidates)
                    {
                        Rect rect = At(ClampCenter(center, size), size);
                        float reach = DistanceToEdge(anchor, rect);
                        float score = Score(rect, anchor, preferred, kind)
                            + (rect.Contains(anchor) ? 200000f : 0f)
                            + HardOverlapArea(rect) * 500f
                            + Mathf.Pow(Mathf.Max(0f, reach - maxReach), 2f) * 300f;
                        if (score < bestScore)
                        {
                            best = rect;
                            bestScore = score;
                        }
                    }
                }

                Reserve(best, ZoneKind.Hard);
                return best;
            }

            private static float Reach(Kind kind) => kind switch
            {
                Kind.Location => 112f,
                Kind.Action => 168f,
                Kind.Annotation => 148f,
                Kind.Bubble => 112f,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };

            internal static Rect At(Vector2 center, Vector2 size)
                => new Rect(center.x - size.x / 2f, center.y - size.y / 2f, size.x, size.y);

            private Vector2 ClampCenter(Vector2 center, Vector2 size)
            {
                float left = _safe.xMin + Edge + size.x / 2f;
                float right = _safe.xMax - Edge - size.x / 2f;
                float top = _safe.yMin + Edge + size.y / 2f;
                float bottom = _safe.yMax - Edge - size.y / 2f;
                return new Vector2(Mathf.Clamp(center.x, left, Mathf.Max(left, right)),
                    Mathf.Clamp(center.y, top, Mathf.Max(top, bottom)));
            }

            private bool Inside(Rect rect) =>
                rect.xMin >= _safe.xMin + Edge && rect.xMax <= _safe.xMax - Edge &&
                rect.yMin >= _safe.yMin + Edge && rect.yMax <= _safe.yMax - Edge;

            private bool Fits(Rect rect)
            {
                if (!Inside(rect)) return false;
                Rect expanded = new Rect(rect.x - Gap / 2f, rect.y - Gap / 2f,
                    rect.width + Gap, rect.height + Gap);
                foreach (var zone in _zones)
                    if (zone.kind == ZoneKind.Hard && expanded.Overlaps(zone.rect))
                        return false;
                return true;
            }

            private float Score(Rect rect, Vector2 anchor, Vector2 preferred, Kind kind)
            {
                // Nearness to the actual subject outranks the aesthetic preference for
                // a particular side. Soft regions (portraits, visual focal points)
                // only matter after hard collisions have been eliminated.
                float line = DistanceToEdge(anchor, rect);
                float preference = (rect.center - preferred).sqrMagnitude;
                float soft = 0f;
                foreach (var zone in _zones)
                    if (zone.kind == ZoneKind.Soft)
                        soft += OverlapArea(rect, zone.rect);
                float positionWeight = kind == Kind.Bubble ? 0.55f : 0.20f;
                return line * line * 5f + preference * positionWeight + soft * 12f;
            }

            private float HardOverlapArea(Rect rect)
            {
                float area = 0f;
                foreach (var zone in _zones)
                    if (zone.kind == ZoneKind.Hard)
                        area += OverlapArea(rect, zone.rect);
                return area;
            }

            private static float OverlapArea(Rect a, Rect b)
                => Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin))
                 * Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));

            public static float DistanceToEdge(Vector2 anchor, Rect rect)
            {
                float x = Mathf.Clamp(anchor.x, rect.xMin, rect.xMax);
                float y = Mathf.Clamp(anchor.y, rect.yMin, rect.yMax);
                return Vector2.Distance(anchor, new Vector2(x, y));
            }
        }
    }
}
