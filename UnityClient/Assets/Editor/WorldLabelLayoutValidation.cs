#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SSNoir.IMGUI;

// Deliberately invokes the production solver, not a duplicate geometry algorithm.
// Run after changes to label policy: SSNoir > Validate World Label Layout.
public static class WorldLabelLayoutValidation
{
    [MenuItem("SSNoir/Validate World Label Layout")]
    public static void Validate()
    {
        var area = new Rect(0, 0, 996, 600);
        var space = new WorldLabelLayout.Space(area);
        space.Reserve(new Rect(0, 0, 996, 56), WorldLabelLayout.ZoneKind.Hard, true);
        space.Reserve(new Rect(0, 502, 300, 98), WorldLabelLayout.ZoneKind.Hard, true);
        space.Reserve(new Rect(790, 66, 196, 98), WorldLabelLayout.ZoneKind.Hard, true);
        Check(space.IsUiBlocked(new Rect(50, 20, 1, 1)), "HUD hit blocker");
        var anchor = new Vector2(230, 135);
        var first = space.Place("乔", WorldLabelLayout.Kind.Action, anchor,
            new Vector2(230, 70), null, new Vector2(218, 84));
        Check(!first.Contains(anchor), "label does not cover its own subject");
        Check(WorldLabelLayout.Space.DistanceToEdge(anchor, first) <= 168f,
            "long line regression: anchor at 230 must not connect across viewport");
        Check(first.yMin >= 56f && first.xMin >= 0f, "safe layout");

        // Several cards belonging to one location have distinct, local slots.
        var crowded = new WorldLabelLayout.Space(area);
        crowded.Reserve(new Rect(0, 0, 996, 56), WorldLabelLayout.ZoneKind.Hard);
        var cards = new List<Rect>();
        for (int i = 0; i < 4; i++)
        {
            var card = crowded.Place("action:" + i, WorldLabelLayout.Kind.Action,
                new Vector2(500, 280), new Vector2(500, 180), null, new Vector2(215, 70));
            foreach (var previous in cards)
                Check(!previous.Overlaps(card), "dense actions must not overlap");
            cards.Add(card);
        }
        Check(crowded.Overflow.Count == 0, "dense action placement must find local slots");

        // Reusing the previous anchor-relative center is stable while it remains valid.
        var stable = new WorldLabelLayout.Space(area);
        var target = new Vector2(620, 250);
        var original = stable.Place("stable", WorldLabelLayout.Kind.Annotation, target,
            new Vector2(740, 210), null, new Vector2(170, 54));
        var following = new WorldLabelLayout.Space(area);
        var movedAnchor = target + new Vector2(38, -12);
        var moved = following.Place("stable", WorldLabelLayout.Kind.Annotation, movedAnchor,
            new Vector2(778, 198), original.center + new Vector2(38, -12), new Vector2(170, 54));
        Check((moved.center - (original.center + new Vector2(38, -12))).sqrMagnitude < 0.01f,
            "offset from anchor must remain stable");

        // A previously valid placement must be recomputed when a HUD region grows.
        var changed = new WorldLabelLayout.Space(area);
        changed.Reserve(new Rect(moved.xMin - 5, moved.yMin - 5, moved.width + 10, moved.height + 10),
            WorldLabelLayout.ZoneKind.Hard);
        var relocated = changed.Place("stable", WorldLabelLayout.Kind.Annotation, movedAnchor,
            moved.center, moved.center, new Vector2(170, 54));
        Check(!relocated.Overlaps(moved), "fixed occupancy invalidates cached position");
        Debug.Log("[SSNoir] World label layout validation passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("World label layout: " + message);
    }
}
