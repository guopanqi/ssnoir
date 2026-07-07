#nullable enable
using UnityEngine;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    // 容器节点（无 Resolve）的绘制。分类与派发由 CardDrawer 统一处理（见 CardDrawer.CardKind）：
    //   - 地点 / 普通：无边悬浮（DrawFloating）——「漂在图纸上的一个地方 / 一件事」，本类自绘背景+内容。
    //   - 人物相册页：走共享卡框架（DrawCardFrame 已在外层画好），本类只补内容（DrawCharacter）。
    //
    // 统一语言：悬浮靠阴影（Ink + 硬投影，去描边），操作靠描边（框架由 CardDrawer 画）。
    public static class ContainerNodeDrawer
    {
        private static readonly Color Paper85 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.85f);
        private static readonly Color Paper35 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.35f);

        // ── 无边悬浮（地点 / 普通）──────────────────────────────────────

        // Ink 填充 + 放大硬投影（无模糊），去描边。地点画建筑线稿 + 地平线 + 地名；
        // 普通画标题（+副标题）。矮框退化为只有名字的悬浮名牌。
        public static void DrawFloating(Rect rect, GameNode node, bool isLocation, bool hover, bool disabled)
        {
            // 阴影加重一档：Ink 与场景蓝太接近，无边卡靠更明确的硬投影才不「融」。
            IMGUIStyles.DrawShadow(rect, new Vector2(7f, 9f), 0.58f);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            Color line = disabled ? Paper35 : (hover ? IMGUIStyles.Paper : Paper85);

            if (isLocation)
                DrawLocationBody(rect, node, line);
            else
                DrawCommonBody(rect, node, line);
        }

        // 地点：上=白色线稿建筑符号，中=一根白色地平线，下=白字地名。矮框退化为悬浮名牌。
        private static void DrawLocationBody(Rect rect, GameNode node, Color line)
        {
            if (rect.height < 90f)
            {
                var plateStyle = new GUIStyle(IMGUIStyles.CardTitle)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = line }
                };
                GUI.Label(rect, node.Name, plateStyle);
                return;
            }

            float iconH = Mathf.Min(72f, rect.height * 0.42f);
            var iconArea = new Rect(rect.center.x - iconH * 0.55f, rect.y + rect.height * 0.14f, iconH * 1.1f, iconH);
            DrawBuildingGlyph(iconArea, line);

            float lineY = iconArea.yMax + 14f;
            IMGUIStyles.DrawLine(
                new Vector2(rect.x + rect.width * 0.14f, lineY),
                new Vector2(rect.xMax - rect.width * 0.14f, lineY), line, 1.5f);

            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = line }
            };
            GUI.Label(new Rect(rect.x + 8f, lineY + 10f, rect.width - 16f, 30f), node.Name, titleStyle);
        }

        // 普通：标题（+副标题）居中。无图形、无地平线——与地点区分。标题跟随悬停变亮。
        private static void DrawCommonBody(Rect rect, GameNode node, Color line)
        {
            float contentH = string.IsNullOrEmpty(node.Subtitle) ? 26f : 56f;
            float startY = rect.y + (rect.height - contentH) / 2f;

            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = line }
            };
            GUI.Label(new Rect(rect.x + 10f, startY, rect.width - 20f, 26f), node.Name, titleStyle);

            if (!string.IsNullOrEmpty(node.Subtitle))
            {
                var subStyle = new GUIStyle(IMGUIStyles.CardSubtitle) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(rect.x + 12f, startY + 28f, rect.width - 24f, 32f), node.Subtitle, subStyle);
            }
        }

        // 建筑线稿占位（缺省通用符号）：三角屋顶 + 方形楼身 + 中缝门线。
        // 资产到位后可替换为木刻/版画白线符号；未提供时所有地点都画它。
        private static void DrawBuildingGlyph(Rect area, Color line)
        {
            float cx = area.center.x;
            float roofBaseY = area.y + area.height * 0.42f;
            float bodyW = area.width * 0.66f;
            float left = cx - bodyW / 2f;
            float right = cx + bodyW / 2f;
            var apex = new Vector2(cx, area.y);

            IMGUIStyles.DrawLine(apex, new Vector2(left, roofBaseY), line, 1.5f);
            IMGUIStyles.DrawLine(apex, new Vector2(right, roofBaseY), line, 1.5f);

            var body = new Rect(left, roofBaseY, bodyW, area.yMax - roofBaseY);
            IMGUIStyles.DrawOutline(body, 1.5f, line);
            IMGUIStyles.DrawLine(new Vector2(cx, roofBaseY + body.height * 0.35f), new Vector2(cx, area.yMax), line, 1f);
        }

        // ── 人物相册页（走共享卡框架）──────────────────────────────────

        // Ink 底白框 + PhotoBlack 照片块 + 下方白字注记。框架 + ±2° 微旋转由 CardDrawer 外层包住，
        // 本函数只画内容。头像美术方案待定，占位：PhotoBlack 底 + 白色人形。
        public static void DrawCharacter(Rect rect, GameNode node, bool disabled)
        {
            float photoW = rect.width - 32f;
            float photoH = photoW * 0.72f;
            var photoRect = new Rect(rect.x + 16f, rect.y + 16f, photoW, photoH);

            GUI.color = IMGUIStyles.PhotoBlack;
            GUI.DrawTexture(photoRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(photoRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.30f));

            var photoStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                fontSize = 32,
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f) }
            };
            GUI.Label(photoRect, "?", photoStyle);

            float textY = photoRect.yMax + 12f;
            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle) { alignment = TextAnchor.MiddleCenter };
            if (disabled) titleStyle.normal.textColor = IMGUIStyles.TextSecondary;
            GUI.Label(new Rect(rect.x + 10f, textY, rect.width - 20f, 26f), node.Name, titleStyle);

            if (!string.IsNullOrEmpty(node.Subtitle))
            {
                var subStyle = new GUIStyle(IMGUIStyles.CardSubtitle) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(rect.x + 10f, textY + 26f, rect.width - 20f, 32f), node.Subtitle, subStyle);
            }
        }
    }
}
