#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    public static class PieDrawer
    {
        public static void DrawPie(Rect rect, float fillPercent, Color fillColor, Color outlineColor)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (fillPercent <= 0) return;

            float cx = rect.x + rect.width / 2f;
            float cy = rect.y + rect.height / 2f;
            float radius = Mathf.Min(rect.width, rect.height) / 2f - 1f;

            float endAngle = -90f + 360f * fillPercent;

            // Draw filled sector
            DrawPieSector(cx, cy, radius, -90f, endAngle, fillColor);

            // Draw outline ring
            DrawCircleOutline(cx, cy, radius, outlineColor);
        }

        public static void DrawPieBadge(Rect rect, float fillPercent, Color fillColor, Color outlineColor)
        {
            if (Event.current.type != EventType.Repaint) return;

            float cx = rect.x + rect.width / 2f;
            float cy = rect.y + rect.height / 2f;
            float radius = Mathf.Min(rect.width, rect.height) / 2f - 1f;

            // Draw outline ring
            DrawCircleOutline(cx, cy, radius, outlineColor);

            // Draw filled sector
            if (fillPercent > 0)
            {
                float endAngle = -90f + 360f * fillPercent;
                DrawPieSector(cx, cy, radius, -90f, endAngle, fillColor);
            }
        }

        private static void DrawPieSector(float cx, float cy, float radius, float startAngle, float endAngle, Color color)
        {
            if (IMGUIStyles.PieMaterial == null) return;

            // GL bypasses both GUI.matrix and the current GUI.BeginGroup clip stack.
            // GUIToScreenPoint restores both transforms; scaling virtual coordinates
            // directly would lose a grid viewport's local origin and draw the pie near
            // the top of the screen instead of inside its clock badge.
            var screenCenter = GUIUtility.GUIToScreenPoint(new Vector2(cx, cy));
            float screenRadius = UIScale.ScaleSize(radius);
            float scx = screenCenter.x;
            float scy = screenCenter.y;

            IMGUIStyles.PieMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);

            GL.Begin(GL.TRIANGLES);
            GL.Color(color);

            int segments = Mathf.Max(3, (int)((endAngle - startAngle) / 2f));
            float startRad = startAngle * Mathf.Deg2Rad;
            float endRad = endAngle * Mathf.Deg2Rad;

            for (int i = 0; i < segments; i++)
            {
                float t0 = (float)i / segments;
                float t1 = (float)(i + 1) / segments;
                float angle0 = Mathf.Lerp(startRad, endRad, t0);
                float angle1 = Mathf.Lerp(startRad, endRad, t1);

                GL.Vertex3(scx, scy, 0);
                GL.Vertex3(scx + Mathf.Cos(angle0) * screenRadius, scy + Mathf.Sin(angle0) * screenRadius, 0);
                GL.Vertex3(scx + Mathf.Cos(angle1) * screenRadius, scy + Mathf.Sin(angle1) * screenRadius, 0);
            }

            GL.End();
            GL.PopMatrix();
        }

        private static void DrawCircleOutline(float cx, float cy, float radius, Color color)
        {
            if (IMGUIStyles.PieMaterial == null) return;

            var screenCenter = GUIUtility.GUIToScreenPoint(new Vector2(cx, cy));
            float screenRadius = UIScale.ScaleSize(radius);
            float scx = screenCenter.x;
            float scy = screenCenter.y;

            IMGUIStyles.PieMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);

            GL.Begin(GL.LINE_STRIP);
            GL.Color(color);

            int segments = 36;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                GL.Vertex3(scx + Mathf.Cos(angle) * screenRadius, scy + Mathf.Sin(angle) * screenRadius, 0);
            }

            GL.End();
            GL.PopMatrix();
        }
    }
}
