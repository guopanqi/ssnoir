#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    public readonly struct ImportantNodeBeacon
    {
        public string NodeName { get; }
        public string Reason { get; }
        public Vector2 Direction { get; }

        public ImportantNodeBeacon(string nodeName, string reason, Vector2 direction)
        {
            NodeName = nodeName;
            Reason = reason;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        }
    }

    // 屏外重要路径的折叠投影：只负责告诉玩家“哪边有必须处理的事”。
    // 点击返回地点名，由渲染器只移动当前相机；这里不改变节点 Focus、不进入地点、不执行动作。
    public static class ImportantNodeBeaconDrawer
    {
        private const float BeaconWidth = 228f;
        private const float BeaconHeight = 68f;
        private const float BeaconGap = 8f;
        private const float EdgeInset = 8f;
        private const float BottomSafeInset = 8f;

        public static string? Draw(IReadOnlyList<ImportantNodeBeacon> beacons, IMGUIInteractionContext ui, float topSafeY)
        {
            if (beacons.Count == 0)
                return null;

            // 贴的是安全区的边，不是画布的边——刘海屏横屏时左右两侧会被挖掉一块。
            Rect safe = UIScale.SafeArea;
            var safeRect = new Rect(
                safe.x + EdgeInset,
                topSafeY,
                Mathf.Max(0f, safe.width - EdgeInset * 2f),
                Mathf.Max(0f, safe.yMax - topSafeY - BottomSafeInset));
            var placed = new List<Rect>(beacons.Count);

            foreach (var beacon in beacons)
            {
                var rect = PlaceAtEdge(safeRect, beacon.Direction);
                rect = AvoidOverlap(rect, safeRect, beacon.Direction, placed);
                placed.Add(rect);

                bool hovered = ui.CanHover(rect);
                DrawBeacon(rect, beacon, hovered);
                if (ui.WasTapped(rect))
                {
                    Event.current.Use();
                    return beacon.NodeName;
                }
            }

            return null;
        }

        private static Rect PlaceAtEdge(Rect safeRect, Vector2 direction)
        {
            bool onVerticalEdge = IsOnVerticalEdge(safeRect, direction);
            if (onVerticalEdge)
            {
                float halfTravel = Mathf.Max(0f, safeRect.height * 0.5f - BeaconHeight * 0.5f);
                float y = safeRect.center.y + direction.y / Mathf.Max(Mathf.Abs(direction.x), 0.0001f) *
                    Mathf.Max(0f, safeRect.width * 0.5f - BeaconWidth * 0.5f);
                y = Mathf.Clamp(y - BeaconHeight * 0.5f, safeRect.center.y - halfTravel, safeRect.center.y + halfTravel);
                float x = direction.x < 0f ? safeRect.xMin : safeRect.xMax - BeaconWidth;
                return new Rect(x, y, BeaconWidth, BeaconHeight);
            }

            float halfHorizontalTravel = Mathf.Max(0f, safeRect.width * 0.5f - BeaconWidth * 0.5f);
            float xAtEdge = safeRect.center.x + direction.x / Mathf.Max(Mathf.Abs(direction.y), 0.0001f) *
                Mathf.Max(0f, safeRect.height * 0.5f - BeaconHeight * 0.5f);
            xAtEdge = Mathf.Clamp(xAtEdge - BeaconWidth * 0.5f,
                safeRect.center.x - halfHorizontalTravel,
                safeRect.center.x + halfHorizontalTravel);
            float yAtEdge = direction.y < 0f ? safeRect.yMin : safeRect.yMax - BeaconHeight;
            return new Rect(xAtEdge, yAtEdge, BeaconWidth, BeaconHeight);
        }

        private static Rect AvoidOverlap(Rect origin, Rect safeRect, Vector2 direction, List<Rect> placed)
        {
            bool onVerticalEdge = IsOnVerticalEdge(safeRect, direction);
            for (int step = 0; step <= placed.Count * 2 + 2; step++)
            {
                int magnitude = (step + 1) / 2;
                float sign = step % 2 == 1 ? 1f : -1f;
                float itemSpan = onVerticalEdge ? BeaconHeight : BeaconWidth;
                float offset = step == 0 ? 0f : sign * magnitude * (itemSpan + BeaconGap);
                var candidate = origin;
                if (onVerticalEdge)
                    candidate.y = Mathf.Clamp(origin.y + offset, safeRect.yMin, safeRect.yMax - candidate.height);
                else
                    candidate.x = Mathf.Clamp(origin.x + offset, safeRect.xMin, safeRect.xMax - candidate.width);

                bool overlaps = false;
                foreach (var occupied in placed)
                {
                    if (candidate.Overlaps(occupied))
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (!overlaps)
                    return candidate;
            }
            return origin;
        }

        private static bool IsOnVerticalEdge(Rect safeRect, Vector2 direction)
        {
            float horizontalTravel = Mathf.Max(0f, safeRect.width - BeaconWidth);
            float verticalTravel = Mathf.Max(0f, safeRect.height - BeaconHeight);
            return Mathf.Abs(direction.x) * verticalTravel >= Mathf.Abs(direction.y) * horizontalTravel;
        }

        private static void DrawBeacon(Rect rect, ImportantNodeBeacon beacon, bool hovered)
        {
            IMGUIStyles.DrawShadow(rect, new Vector2(6f, 7f), 0.58f);
            IMGUIStyles.SetColor(IMGUIStyles.Ink);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();

            IMGUIStyles.DrawOutline(rect, hovered ? 2f : 1.5f, IMGUIStyles.Gold);
            IMGUIStyles.DrawGoldPulse(rect, baseAlpha: hovered ? 0.65f : 0.42f, rings: 2, ringStep: 2f, speed: 1.8f);

            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = IMGUIStyles.FontSize(18),
                clipping = TextClipping.Clip,
                normal = { textColor = IMGUIStyles.Paper }
            };
            var reasonStyle = new GUIStyle(IMGUIStyles.CardSubtitle)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = IMGUIStyles.FontSize(13),
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.72f) }
            };
            var locateStyle = new GUIStyle(IMGUIStyles.StatusLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = IMGUIStyles.FontSize(12),
                normal = { textColor = IMGUIStyles.Gold }
            };

            IMGUIStyles.DrawLabel(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 78f, 24f), beacon.NodeName, titleStyle);
            IMGUIStyles.DrawLabel(new Rect(rect.xMax - 62f, rect.y + 9f, 50f, 20f), "定 位", locateStyle);
            IMGUIStyles.DrawLabel(new Rect(rect.x + 14f, rect.y + 35f, rect.width - 28f, 24f), beacon.Reason, reasonStyle);
        }
    }
}
