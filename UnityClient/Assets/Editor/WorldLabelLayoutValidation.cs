#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SSNoir.IMGUI;

// Exercises the production placement state and solver without a second geometry implementation.
public static class WorldLabelLayoutValidation
{
    [MenuItem("SSNoir/Validate World Label Layout")]
    public static void Validate()
    {
        var area = new Rect(0, 0, 996, 600);
        var state = new WorldLabelLayout.PlacementState();
        var space = new WorldLabelLayout.Space(area);
        space.Reserve(new Rect(0, 0, 996, 56), WorldLabelLayout.ZoneKind.Hard, true);
        space.Reserve(new Rect(0, 502, 300, 98), WorldLabelLayout.ZoneKind.Hard, true);
        space.Reserve(new Rect(790, 66, 196, 98), WorldLabelLayout.ZoneKind.Hard, true);
        Check(space.IsUiBlocked(new Rect(50, 20, 1, 1)), "HUD input blocker");

        var anchor = new Vector2(230, 135);
        var first = state.Place(space, new WorldLabelLayout.Request(
            "乔", WorldLabelLayout.Kind.Action, anchor, new Vector2(230, 70),
            new Vector2(218, 84)));
        Check(!first.Contains(anchor), "label does not cover its own anchor");
        Check(WorldLabelLayout.Space.DistanceToEdge(anchor, first) <= 168f,
            "new label must remain local to its anchor");
        Check(first.yMin >= 56f && first.xMin >= 0f, "new label respects safe layout");

        // Both an initial preference and the remembered placement are independent
        // of which half of the viewport contains the same world anchor.
        var leftPreferred = WorldLabelLayout.PreferredActionCenter(
            new Vector2(330, 260), new Vector2(218, 80));
        var rightPreferred = WorldLabelLayout.PreferredActionCenter(
            new Vector2(665, 260), new Vector2(218, 80));
        Check(Approximately(leftPreferred - new Vector2(330, 260),
            rightPreferred - new Vector2(665, 260)),
            "preferred action direction must not depend on screen midpoint");

        // No flip when a projected anchor crosses the screen midpoint. The preferred
        // point may change, but an EXISTING label belongs to its original attachment.
        var fixedState = new WorldLabelLayout.PlacementState();
        var startAnchor = new Vector2(330, 260);
        var start = fixedState.Place(new WorldLabelLayout.Space(area),
            new WorldLabelLayout.Request("action", WorldLabelLayout.Kind.Action, startAnchor,
                new Vector2(330, 178), new Vector2(218, 80)));
        var shiftedAnchor = new Vector2(665, 260);
        var shifted = fixedState.Place(new WorldLabelLayout.Space(area),
            new WorldLabelLayout.Request("action", WorldLabelLayout.Kind.Action, shiftedAnchor,
                new Vector2(800, 260), new Vector2(218, 80)));
        Check(Approximately(start.center - startAnchor, shifted.center - shiftedAnchor),
            "crossing center must not reverse the label's side");

        // At the viewport edge, the card must leave the screen rather than teleport
        // to the other side or stretch an ever-longer tether.
        var edgeAnchor = new Vector2(987, 260);
        var edge = fixedState.Place(new WorldLabelLayout.Space(area),
            new WorldLabelLayout.Request("action", WorldLabelLayout.Kind.Action, edgeAnchor,
                new Vector2(800, 260), new Vector2(218, 80)));
        Check(Approximately(start.center - startAnchor, edge.center - edgeAnchor),
            "screen boundary must not clamp remembered world-label positions");

        // If the anchor leaves view and returns without leaving the authored scene,
        // the side is still remembered; only removed content is pruned.
        fixedState.KeepOnly(new[] { "action" });
        var returned = fixedState.Place(new WorldLabelLayout.Space(area),
            new WorldLabelLayout.Request("action", WorldLabelLayout.Kind.Action, startAnchor,
                new Vector2(120, 260), new Vector2(218, 80)));
        Check(Approximately(start.center, returned.center), "off-screen return keeps attachment");

        // New labels are placed around old ones without dislodging the old cards.
        var occupied = new WorldLabelLayout.Space(area);
        var requests = new List<WorldLabelLayout.Request>
        {
            new("old", WorldLabelLayout.Kind.Action, new Vector2(500, 280),
                new Vector2(500, 180), new Vector2(200, 70), 0)
        };
        var memory = new WorldLabelLayout.PlacementState();
        var oldPlacement = memory.Arrange(occupied, requests)["old"];
        requests.Add(new WorldLabelLayout.Request("new", WorldLabelLayout.Kind.Action,
            new Vector2(500, 280), new Vector2(500, 180), new Vector2(200, 70), 1));
        var together = memory.Arrange(new WorldLabelLayout.Space(area), requests);
        Check(Approximately(oldPlacement.center, together["old"].center),
            "new content must not move established card");
        Check(!together["old"].Overlaps(together["new"]), "cards must occupy distinct slots");

        // Structural invalidation is explicit. A changed HUD does not move an existing
        // world label under the camera, but a scene reset does trigger a fresh solve.
        var fixedHud = new WorldLabelLayout.Space(area);
        fixedHud.Reserve(new Rect(start.xMin - 5f, start.yMin - 5f,
            start.width + 10f, start.height + 10f), WorldLabelLayout.ZoneKind.Hard);
        var held = fixedState.Place(fixedHud, new WorldLabelLayout.Request(
            "action", WorldLabelLayout.Kind.Action, startAnchor,
            new Vector2(330, 178), new Vector2(218, 80)));
        Check(Approximately(start.center, held.center), "transient occlusion must not reflow");

        fixedState.Clear();
        var reshaped = new WorldLabelLayout.Space(area);
        reshaped.Reserve(new Rect(start.xMin - 5f, start.yMin - 5f,
            start.width + 10f, start.height + 10f), WorldLabelLayout.ZoneKind.Hard);
        var reflowed = fixedState.Place(reshaped, new WorldLabelLayout.Request(
            "action", WorldLabelLayout.Kind.Action, startAnchor,
            new Vector2(330, 178), new Vector2(218, 80)));
        Check(!reflowed.Overlaps(start), "new layout context resolves new blockers");

        Debug.Log("[SSNoir] Stable world label layout validation passed.");
    }

    private static bool Approximately(Vector2 a, Vector2 b)
        => (a - b).sqrMagnitude < 0.01f;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("World label layout: " + message);
    }
}
