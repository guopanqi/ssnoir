#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SSNoir;
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

        // Navigation transition policy is independent of label placement.
        // Regression: the previous "settleImmediately" shortcut disabled the
        // house → world pullback while room → house still animated.
        Check(SSNoirCameraManager.NavigationTravelPath(storyDriven: false)
            == FocusTravelPath.Exploration,
            "all player navigation, including parent-world return, must animate");
        Check(SSNoirCameraManager.NavigationTravelPath(storyDriven: true)
            == FocusTravelPath.AuthoredArc,
            "scripted navigation retains authored camera travel");

        // Enter a location and return to the world with its map labels still authored
        // in the same root tree. Navigating to a child must NOT prune the map memory.
        // This reproduces the old "initial world arrangement, then jump" regression.
        var worldState = new WorldLabelLayout.PlacementState();
        var worldLabels = new List<WorldLabelLayout.Request>
        {
            new("报社", WorldLabelLayout.Kind.Location, new Vector2(300, 240),
                new Vector2(220, 198), new Vector2(108, 40), 0),
            new("码头", WorldLabelLayout.Kind.Location, new Vector2(710, 320),
                new Vector2(630, 278), new Vector2(108, 40), 1),
        };
        var beforeEntry = worldState.Arrange(new WorldLabelLayout.Space(area), worldLabels);
        worldState.KeepOnly(new[] { "报社", "码头", "酒馆房间" });
        worldState.Arrange(new WorldLabelLayout.Space(area), new List<WorldLabelLayout.Request>
        {
            new("酒馆房间", WorldLabelLayout.Kind.Action, new Vector2(520, 250),
                new Vector2(520, 180), new Vector2(200, 70)),
        });
        var afterReturn = worldState.Arrange(new WorldLabelLayout.Space(area), worldLabels);
        Check(Approximately(beforeEntry["报社"].center, afterReturn["报社"].center)
            && Approximately(beforeEntry["码头"].center, afterReturn["码头"].center),
            "returning to world must reuse the original map layout");

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

        ValidateProgressGroups();

        Debug.Log("[SSNoir] Stable world label layout validation passed.");
    }

    public static void ValidateProgressGroups()
    {
        var state = new WorldLabelLayout.PlacementState();
        List<WorldLabelLayout.Request> Requests(Vector2 anchor) => new()
        {
            new("clock", WorldLabelLayout.Kind.Annotation, anchor, anchor, new Vector2(180, 90), 0, "clock"),
            new("roll", WorldLabelLayout.Kind.Action, anchor, anchor, new Vector2(310, 160), 1, "clock"),
            new("pay", WorldLabelLayout.Kind.Action, anchor, anchor, new Vector2(180, 140), 2, "clock"),
            new("work", WorldLabelLayout.Kind.Action, anchor, anchor, new Vector2(220, 130), 3)
        };
        var area = new Rect(0, 0, 1100, 700);
        var anchor = new Vector2(550, 500);
        var wide = state.Arrange(new WorldLabelLayout.Space(area), Requests(anchor));
        Check(wide["clock"].yMax < wide["roll"].yMin, "Clock above its actions");
        Check(Mathf.Approximately(wide["roll"].yMin, wide["pay"].yMin), "actions share a row");
        Check(!wide["roll"].Overlaps(wide["pay"]), "attachment reserves do not overlap");
        foreach (var key in new[] { "clock", "roll", "pay" })
            Check(!wide[key].Overlaps(wide["work"]), "unrelated work stays outside group");
        var moved = state.Arrange(new WorldLabelLayout.Space(area), Requests(anchor + new Vector2(90, 20)));
        foreach (var key in new[] { "clock", "roll", "pay" })
            Check(Approximately(moved[key].center - wide[key].center, new Vector2(90, 20)),
                "camera movement keeps group rigid");
        var narrow = new WorldLabelLayout.PlacementState().Arrange(
            new WorldLabelLayout.Space(new Rect(0, 0, 480, 900)), Requests(new Vector2(240, 800)));
        Check(narrow["roll"].yMax < narrow["pay"].yMin, "narrow area stacks actions");
        var reduced = Requests(anchor);
        reduced.RemoveAt(2);
        var fewer = state.Arrange(new WorldLabelLayout.Space(area), reduced);
        Check(fewer.Count == 3 && !fewer.ContainsKey("pay"), "removed action leaves group");
        Check(fewer["clock"].yMax < fewer["roll"].yMin, "remaining action stays below Clock");
    }

    private static bool Approximately(Vector2 a, Vector2 b)
        => (a - b).sqrMagnitude < 0.01f;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("World label layout: " + message);
    }
}
