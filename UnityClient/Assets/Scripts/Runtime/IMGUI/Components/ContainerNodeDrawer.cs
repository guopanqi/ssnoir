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
        // clocksBottomY：CardDrawer 量出的时钟徽章底部 Y（无徽章时等于 rect.y）——内容要让开这块，
        // 不然徽章一多、换了行，标题/图标就会被压在下面。
        public static void DrawFloating(Rect rect, GameNode node, bool isLocation, bool hover, bool disabled, float clocksBottomY)
        {
            // 阴影加重一档：Ink 与场景蓝太接近，无边卡靠更明确的硬投影才不「融」。
            IMGUIStyles.DrawShadow(rect, new Vector2(7f, 9f), 0.58f);
            GUI.color = IMGUIStyles.Ink;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            Color line = disabled ? Paper35 : (hover ? IMGUIStyles.Paper : Paper85);

            if (isLocation)
                DrawLocationBody(rect, node, line, clocksBottomY);
            else
                DrawCommonBody(rect, node, line, clocksBottomY);
        }

        // 地点：上=白色线稿建筑符号，中=一根白色地平线，下=白字地名。矮框退化为悬浮名牌。
        // 判断「矮不矮」不能只看卡片原始高度——时钟徽章会先把内容起点往下推，卡片本身够高
        // 但徽章一多照样挤不下建筑符号，所以按「让开徽章后还剩多少」(availH) 来判断退化，
        // 而不是 rect.height 本身。
        private static void DrawLocationBody(Rect rect, GameNode node, Color line, float clocksBottomY)
        {
            bool hasClocks = clocksBottomY > rect.y;
            float contentTop = hasClocks ? clocksBottomY + 4f : rect.y;
            float availH = rect.yMax - contentTop;

            if (availH < 90f)
            {
                var plateRect = new Rect(rect.x, contentTop, rect.width, Mathf.Max(0f, availH));
                var plateStyle = new GUIStyle(IMGUIStyles.CardTitle)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    normal = { textColor = line }
                };
                GUI.Label(plateRect, node.Name, plateStyle);
                return;
            }

            // 建筑线稿宽度同时受 availH（竖向）与 rect.width（横向）约束——瘦高卡（投影到窄立面）
            // 不会再把线稿横向撑出卡外。
            float iconH = Mathf.Min(72f, availH * 0.42f, rect.width * 0.7f / 1.1f);
            float iconTop = contentTop + availH * 0.12f;
            var iconArea = new Rect(rect.center.x - iconH * 0.55f, iconTop, iconH * 1.1f, iconH);
            DrawBuildingGlyph(iconArea, line);

            float lineY = iconArea.yMax + 14f;
            IMGUIStyles.DrawLine(
                new Vector2(rect.x + rect.width * 0.14f, lineY),
                new Vector2(rect.xMax - rect.width * 0.14f, lineY), line, 1.5f);

            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = line }
            };
            GUI.Label(new Rect(rect.x + 8f, lineY + 10f, rect.width - 16f, 30f), node.Name, titleStyle);
        }

        // 普通：标题（+副标题）居中。无图形、无地平线——与地点区分。标题跟随悬停变亮。
        private static void DrawCommonBody(Rect rect, GameNode node, Color line, float clocksBottomY)
        {
            float contentH = string.IsNullOrEmpty(node.Subtitle) ? 26f : 56f;
            float startY = rect.y + (rect.height - contentH) / 2f;
            if (clocksBottomY > rect.y)
                startY = Mathf.Max(startY, clocksBottomY + 6f);
            // 徽章占用空间过多时，标题起点不能无限下压探出卡底：最多退到刚好留出一行标题的
            // 位置，宁可这行标题贴近甚至压住徽章区，也不让文字画到卡外面。副标题在空间不够
            // 时会被下面的 Mathf.Max(0f, …) 自然挤成 0 高度，等同于隐藏。
            startY = Mathf.Min(startY, rect.yMax - 26f - 6f);

            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = line }
            };
            GUI.Label(new Rect(rect.x + 10f, startY, rect.width - 20f, 26f), node.Name, titleStyle);

            if (!string.IsNullOrEmpty(node.Subtitle))
            {
                var subStyle = new GUIStyle(IMGUIStyles.CardSubtitle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };
                GUI.Label(new Rect(rect.x + 12f, startY + 28f, rect.width - 24f, Mathf.Max(0f, rect.yMax - (startY + 28f) - 6f)), node.Subtitle, subStyle);
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
        // clocksBottomY：时钟徽章底部 Y（无徽章时等于 rect.y）——照片要让开，不然会被徽章压住。
        public static void DrawCharacter(Rect rect, GameNode node, bool disabled, float clocksBottomY)
        {
            bool hasClocks = clocksBottomY > rect.y;
            float photoTop = Mathf.Max(rect.y + 16f, hasClocks ? clocksBottomY + 6f : rect.y);

            // 矮卡（或徽章占掉大半卡高）放不下照片时，退化为只有名字的悬浮名牌——
            // 与地点节点的矮框退化同一套语言，而不是无视竖向空间硬画一张比卡还高的照片。
            if (rect.yMax - photoTop < 90f)
            {
                var plateRect = new Rect(rect.x, photoTop, rect.width, Mathf.Max(0f, rect.yMax - photoTop));
                var plateStyle = new GUIStyle(IMGUIStyles.CardTitle)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    normal = { textColor = disabled ? IMGUIStyles.TextSecondary : IMGUIStyles.Paper }
                };
                GUI.Label(plateRect, node.Name, plateStyle);
                return;
            }

            float photoW = rect.width - 32f;
            // 照片高度同时受宽高比(0.72)与卡片剩余竖向空间约束——瘦高卡不会再把照片撑得
            // 比卡还高；下方至少给标题预留一行 + 间距 + 底边距。
            const float titleBudget = 12f + 26f + 6f;
            float photoH = Mathf.Min(photoW * 0.72f, Mathf.Max(30f, rect.yMax - photoTop - titleBudget));
            var photoRect = new Rect(rect.x + 16f, photoTop, photoW, photoH);

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
            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            if (disabled) titleStyle.normal.textColor = IMGUIStyles.TextSecondary;
            GUI.Label(new Rect(rect.x + 10f, textY, rect.width - 20f, 26f), node.Name, titleStyle);

            if (!string.IsNullOrEmpty(node.Subtitle))
            {
                var subStyle = new GUIStyle(IMGUIStyles.CardSubtitle) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
                GUI.Label(new Rect(rect.x + 10f, textY + 26f, rect.width - 20f, Mathf.Max(0f, rect.yMax - (textY + 26f) - 6f)), node.Subtitle, subStyle);
            }
        }
    }
}
