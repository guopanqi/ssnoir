#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public partial class IMGUIWorldRenderer
    {
        private static IEnumerable<GameNode> ExpandedInlineNodes(GameNode node)
        {
            yield return node;
            if (node.IsInlineGroup)
                foreach (var child in node.Children)
                    foreach (var descendant in ExpandedInlineNodes(child)) yield return descendant;
        }

        private const float InlinePad = 12f;
        private const float InlineGap = 18f;
        private const float InlineTitleHeight = 30f;
        private static float InlineOddsSpace(GameNode node) => node.Resolve?.Type == ResolveType.Roll ? 138f : 0f;
        private float InlinePreferredWidth(GameNode group)
        {
            float available = Mathf.Max(180f, UIScale.SafeArea.width - 40f);
            float cell = ActionCardDesignWidth + group.Children.Max(InlineOddsSpace);
            return Mathf.Min(available, InlinePad * 2f + cell);
        }

        private static GUIStyle InlineSubtitleStyle() => new(IMGUIStyles.CardSubtitle)
            { alignment = TextAnchor.UpperLeft, wordWrap = true };

        private float InlineHeaderHeight(GameNode group, float width)
            => InlinePad + InlineTitleHeight +
                (string.IsNullOrWhiteSpace(group.Subtitle) ? 0f : InlineSubtitleStyle().CalcHeight(new GUIContent(group.Subtitle), width - 2f * InlinePad) + 6f)
                + CardDrawer.MeasureClockBadgesHeight(width, ClocksForCard(group)) + InlinePad;

        // 同一份几何用于测量、显示、资源落槽和操作台附件。整组只向空间避让器提交一个矩形。
        private List<(GameNode node, Rect rect)> InlineChildren(GameNode group, Rect rect)
        {
            float available = Mathf.Max(1f, rect.width - InlinePad * 2f);
            float y = rect.y + InlineHeaderHeight(group, rect.width);
            var result = new List<(GameNode node, Rect rect)>();
            foreach (var child in group.Children)
            {
                float width = Mathf.Max(80f, available - InlineOddsSpace(child));
                bool note = AnnotationDrawer.IsAnnotation(child);
                float tags = note ? 0f : CardTagDrawer.MeasureRise(child, width,
                    RestBlockerPresentation.ContainsTarget(child, _gameManager.DisplayedSnapshot.RestBlockers),
                    ClocksForCard(child), _gameManager.HasUnreadContentTag(child.Id));
                float height = child.IsInlineGroup ? MeasureInlineGroup(child, width)
                    : note ? AnnotationDrawer.MeasureHeight(child, width)
                    : CardDrawer.MeasureCardHeight(child, CardDrawer.Classify(child, false), width, _gameManager.DisplayedSnapshot.Actors, ClocksForCard(child));
                result.Add((child, new Rect(rect.x + InlinePad, y + tags, width, height)));
                y += tags + height + InlineGap;
            }
            return result;
        }

        private float MeasureInlineGroup(GameNode group, float width)
        {
            // 子组在有限的内容树中递归测量，不涉及世界Anchor或导航。
            var children = InlineChildren(group, new Rect(0f, 0f, width, 0f));
            return children.Max(c => c.rect.yMax) + InlinePad;
        }

        private bool ContainsInlineDropSlot(GameNode node, Rect rect, Vector2 mouse)
        {
            if (node.IsInlineGroup)
                return !node.Disabled && InlineChildren(node, rect).Any(c => ContainsInlineDropSlot(c.node, c.rect, mouse));
            return !AnnotationDrawer.IsAnnotation(node) && ActionNodeDrawer.ContainsDropSlot(rect, node,
                CardDrawer.MeasureClockBadgesBottom(rect, ClocksForCard(node)), mouse, _gameManager);
        }

        private void DrawInlineGroup(GameNode group, Rect rect, IMGUIInteractionContext ui, Vector2 screenOffset)
        {
            float layer = IMGUIStyles.BeginLayer(group.Disabled ? 0.35f : 1f);
            var localUi = new IMGUIInteractionContext(ui.Mouse, ui.IsLocked || group.Disabled, ui.IsOccluded);
            var title = new Rect(rect.x + InlinePad, rect.y + InlinePad, rect.width - 2f * InlinePad, InlineTitleHeight);
            var style = new GUIStyle(IMGUIStyles.CardTitle) { alignment = TextAnchor.MiddleLeft, fontSize = IMGUIStyles.FontSize(20) };
            IMGUIStyles.DrawLabel(title, group.Name, style);
            float y = title.yMax;
            IMGUIStyles.DrawLine(new Vector2(title.x, y), new Vector2(title.xMax, y), IMGUIStyles.CityPlateLine, 1f);
            if (!string.IsNullOrWhiteSpace(group.Subtitle))
            {
                float h = InlineSubtitleStyle().CalcHeight(new GUIContent(group.Subtitle), title.width);
                IMGUIStyles.DrawLabel(new Rect(title.x, y + 4f, title.width, h), group.Subtitle, InlineSubtitleStyle());
                y += h + 6f;
            }
            ClockPulse.BeginHost(group.Id);
            CardDrawer.DrawClockBadges(new Rect(rect.x, y, rect.width, 0f), ClocksForCard(group));
            ClockPulse.EndHost();
            CardTagDrawer.Draw(rect, group, group.Disabled,
                RestBlockerPresentation.ContainsTarget(group, _gameManager.DisplayedSnapshot.RestBlockers),
                ClocksForCard(group), _gameManager.HasUnreadContentTag(group.Id));
            IMGUIStyles.DrawLine(new Vector2(rect.x + 2f, title.y), new Vector2(rect.x + 2f, rect.yMax), IMGUIStyles.CityPlateLine, 1f);
            foreach (var child in InlineChildren(group, rect))
            {
                var screenRect = child.rect;
                screenRect.position += screenOffset;
                _dialogueAnchors.RegisterNode(child.node.Id, screenRect);
                if (AnnotationDrawer.IsAnnotation(child.node))
                    AnnotationDrawer.DrawAnchored(child.rect, child.node, new Vector2(rect.x + 2f, child.rect.center.y));
                else DrawNodeCard(child.node, child.rect, localUi, screenOffset);
            }
            if (Event.current.type == EventType.Repaint && !ui.IsLocked && !ui.IsOccluded)
            {
                _gameManager.NoteInlineGroupPainted(group);
                if (ui.CanHover(title)) _gameManager.NoteWorldContentHovered(group);
            }
            IMGUIStyles.EndLayer(layer);
        }
    }
}
