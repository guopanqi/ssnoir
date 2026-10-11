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

        public sealed class Space
        {
            private const float Edge = 8f;
            private const float Gap = 10f;
            private const float SearchStep = 28f;
            private readonly Rect _safe;
            private readonly List<(Rect rect, ZoneKind kind, bool blocksInput)> _zones = new();
            private readonly HashSet<string> _overflow = new();

            public Space(Rect safe) => _safe = safe;

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

            // The previous center is relative to the CURRENT projected anchor,
            // never an absolute screen or world position from a different camera frame.
            public Rect Place(string key, Kind kind, Vector2 anchor, Vector2 preferred,
                Vector2? previous, Vector2 size)
            {
                if (size.x <= 0f || size.y <= 0f)
                    throw new ArgumentException("World label must have positive measured dimensions: " + key);

                float maxReach = Reach(kind);
                if (previous.HasValue)
                {
                    Rect retained = At(previous.Value, size);
                    if (Fits(retained) && !retained.Contains(anchor)
                        && DistanceToEdge(anchor, retained) <= maxReach)
                    {
                        Reserve(retained, ZoneKind.Hard);
                        return retained;
                    }
                }

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

            private static Rect At(Vector2 center, Vector2 size)
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
