#nullable enable
using System;
using System.Collections.Generic;
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

        // ── 纵向流量（测量与绘制共用）─────────────────────────────────
        // 这一组常量是「卡有多高」和「东西画在哪」的同一份定义。人物卡的照片按内容宽度推出
        // 首选尺寸，卡片再按这个尺寸长高——而不是反过来，把图形塞进一个与内容无关的固定卡高里
        // （那样一挤就退化成光秃秃的名牌）。
        private const float ContentPad = 14f;
        private const float TitleH = 26f;
        private const float GapAfterClocks = 10f;
        private const float PhotoTopPad = 16f;
        private const float GapPhotoToTitle = 12f;
        // 人物照在手机上不能按 168 画：一张人物卡光照片就占掉三成屏高，
        // 和它旁边的动作卡一起就再也排不开了。
        private const float MaxPhotoHeight = 120f;

        // ── 地点牌（一行读完）─────────────────────────────────────────
        // 地点卡是「这儿有个地方」，不是一张插画。以前它上面顶着一枚半张卡高的建筑线稿，
        // 一间酒馆的牌子就有一百五十像素高——同一栋楼挂三四个动作时，光地名牌就先把竖向
        // 空间吃掉，剩下的卡只能叠成一摞。现在收成一行：有同名节点图时左边显示图标，右边地名；
        // 没有图时只显示地名。
        private const float LocationPadX = 14f;
        private const float LocationIconSize = 24f;
        private const float GapIconToName = 10f;
        private const float LocationRowH = 28f;
        private const string NodeIconResourceDirectory = "UI/NodeIcons";
        private static readonly IReadOnlyDictionary<string, Texture2D> NodeIcons = LoadNodeIcons();
        // 牌子再小也要好点：不低于一个手指目标。
        private static float MinLocationHeight => UIScale.MinTouchSize;

        private static float PreferredPhotoHeight(float cardWidth)
            => Mathf.Min((cardWidth - 32f) * 0.72f, MaxPhotoHeight);

        // 地点牌按「可选图标 + 一个地名」实际需要多宽收敛：叫「家」的地方不该和「老街酒馆」
        // 占一样宽。窄卡在屏幕上少占地方，同一栋楼的几张卡也就更容易各自让开。
        // 下限保证带时钟徽章的牌子仍排得开，上限压住长地名。
        public static float PreferredLocationWidth(GameNode node)
        {
            float nameW = new GUIStyle(IMGUIStyles.CardTitle).CalcSize(new GUIContent(node.Name)).x;
            float iconW = NodeIcon(node) != null ? LocationIconSize + GapIconToName : 0f;
            return Mathf.Clamp(
                LocationPadX * 2f + iconW + nameW,
                136f, 260f);
        }

        // 副标题按实际换行结果占高（上限 4 行左右），不再固定「剩下多少算多少」。
        private static float MeasureSubtitleHeight(GameNode node, float cardWidth)
        {
            if (string.IsNullOrEmpty(node.Subtitle))
                return 0f;
            var style = new GUIStyle(IMGUIStyles.CardSubtitle) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            return Mathf.Clamp(style.CalcHeight(new GUIContent(node.Subtitle), cardWidth - 24f), 18f, 88f);
        }

        private static float ClocksBlock(float clocksHeight)
            => clocksHeight > 0f ? clocksHeight + GapAfterClocks : ContentPad;

        // 地点牌不套 MinCardHeight：那个下限是给「里面有东西要操作」的卡定的，
        // 地名牌整张就是一个点击目标，按 MinTouchSize 兜底就够。
        public static float MeasureLocationHeight(GameNode node, float cardWidth, float clocksHeight)
        {
            return Mathf.Max(MinLocationHeight, ClocksBlock(clocksHeight) + LocationRowH + ContentPad);
        }

        public static float MeasureCommonHeight(GameNode node, float cardWidth, float clocksHeight)
        {
            float subtitleH = MeasureSubtitleHeight(node, cardWidth);
            return Mathf.Max(CardDrawer.MinCardHeight, ClocksBlock(clocksHeight)
                + TitleH + (subtitleH > 0f ? 6f + subtitleH : 0f)
                + ContentPad);
        }

        public static float MeasureCharacterHeight(GameNode node, float cardWidth, float clocksHeight)
        {
            float subtitleH = MeasureSubtitleHeight(node, cardWidth);
            return Mathf.Max(clocksHeight + 6f, PhotoTopPad)
                + PreferredPhotoHeight(cardWidth) + GapPhotoToTitle
                + TitleH + (subtitleH > 0f ? 4f + subtitleH : 0f)
                + ContentPad;
        }

        // ── 无边悬浮（地点 / 普通）──────────────────────────────────────

        // Ink 填充 + 放大硬投影（无模糊），去描边。地点画一行「可选图标 + 地名」；
        // 普通画「可选图标 + 标题」（+副标题）。矮框退化为只有名字的悬浮名牌。
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

        // 地点：一行读完——存在同名节点图时左边显示图标，右边地名（左对齐）。
        //
        // 图标只是节点的识别标记，不是插画，所以尺寸不跟卡宽走；地名才是要读的东西，
        // 占掉这一行剩下的全部宽度。
        // 时钟徽章会先把内容起点往下推，所以这一行按「让开徽章后还剩多少」(availH) 落位，
        // 空间不够时先收窄符号、再由 Clip 兜底，绝不画到卡外面。
        private static void DrawLocationBody(Rect rect, GameNode node, Color line, float clocksBottomY)
        {
            bool hasClocks = clocksBottomY > rect.y;
            float contentTop = hasClocks ? clocksBottomY + GapAfterClocks : rect.y + ContentPad;
            // 与 MeasureLocationHeight 同一份流量：内容区下面还留一个 ContentPad。
            float availH = rect.yMax - ContentPad - contentTop;
            // 徽章占掉太多、按流量已经没地方了：挤到卡底也要把地名画出来，不能什么都不画。
            if (availH < LocationRowH)
                availH = Mathf.Min(LocationRowH, rect.yMax - contentTop);
            if (availH <= 0f)
                return;

            float rowH = Mathf.Min(LocationRowH, availH);
            float rowY = contentTop + (availH - rowH) * 0.5f;

            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = line }
            };

            float nameX = rect.x + LocationPadX;
            var icon = NodeIcon(node);
            if (icon != null)
            {
                float iconH = Mathf.Min(LocationIconSize, rowH);
                var iconArea = new Rect(
                    nameX,
                    rowY + (rowH - iconH) * 0.5f,
                    iconH,
                    iconH);
                GUI.color = line;
                GUI.DrawTexture(iconArea, icon, ScaleMode.ScaleToFit, true);
                GUI.color = Color.white;
                nameX = iconArea.xMax + GapIconToName;
            }

            GUI.Label(
                new Rect(nameX, rowY, Mathf.Max(0f, rect.xMax - LocationPadX - nameX), rowH),
                node.Name, titleStyle);
        }

        // 普通：可选的同名节点图 + 标题（+副标题）整体居中。标题跟随悬停变亮。
        private static void DrawCommonBody(Rect rect, GameNode node, Color line, float clocksBottomY)
        {
            float subtitleH = MeasureSubtitleHeight(node, rect.width);
            float contentH = TitleH + (subtitleH > 0f ? 6f + subtitleH : 0f);
            float startY = rect.y + (rect.height - contentH) / 2f;
            if (clocksBottomY > rect.y)
                startY = Mathf.Max(startY, clocksBottomY + GapAfterClocks);
            // 徽章占用空间过多时，标题起点不能无限下压探出卡底：最多退到刚好留出一行标题的
            // 位置，宁可这行标题贴近甚至压住徽章区，也不让文字画到卡外面。副标题在空间不够
            // 时会被下面的 Mathf.Max(0f, …) 自然挤成 0 高度，等同于隐藏。
            startY = Mathf.Min(startY, rect.yMax - TitleH - 6f);

            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                normal = { textColor = line }
            };

            var icon = NodeIcon(node);
            if (icon == null)
            {
                GUI.Label(new Rect(rect.x + 10f, startY, rect.width - 20f, TitleH), node.Name, titleStyle);
            }
            else
            {
                float iconH = Mathf.Min(LocationIconSize, TitleH);
                float nameW = Mathf.Min(
                    titleStyle.CalcSize(new GUIContent(node.Name)).x,
                    Mathf.Max(0f, rect.width - 20f - iconH - GapIconToName));
                float groupW = iconH + GapIconToName + nameW;
                float groupX = rect.center.x - groupW * 0.5f;
                var iconArea = new Rect(groupX, startY + (TitleH - iconH) * 0.5f, iconH, iconH);
                GUI.color = line;
                GUI.DrawTexture(iconArea, icon, ScaleMode.ScaleToFit, true);
                GUI.color = Color.white;

                titleStyle.alignment = TextAnchor.MiddleLeft;
                GUI.Label(
                    new Rect(iconArea.xMax + GapIconToName, startY, nameW, TitleH),
                    node.Name,
                    titleStyle);
            }

            if (subtitleH > 0f)
            {
                var subStyle = new GUIStyle(IMGUIStyles.CardSubtitle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    clipping = TextClipping.Clip
                };
                float subY = startY + TitleH + 6f;
                GUI.Label(new Rect(rect.x + 12f, subY, rect.width - 24f, Mathf.Min(subtitleH, Mathf.Max(0f, rect.yMax - subY - 6f))), node.Subtitle, subStyle);
            }
        }

        private static Texture2D? NodeIcon(GameNode node)
        {
            return NodeIcons.TryGetValue(node.Name, out var icon) ? icon : null;
        }

        private static IReadOnlyDictionary<string, Texture2D> LoadNodeIcons()
        {
            var iconsByNodeName = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            foreach (var icon in Resources.LoadAll<Texture2D>(NodeIconResourceDirectory))
            {
                if (iconsByNodeName.ContainsKey(icon.name))
                {
                    throw new InvalidOperationException(
                        $"[SSNoir] Duplicate node icon '{icon.name}' under " +
                        $"Resources/{NodeIconResourceDirectory}. Node icon names must be unique.");
                }

                iconsByNodeName.Add(icon.name, icon);
            }

            return iconsByNodeName;
        }

        // ── 人物相册页（走共享卡框架）──────────────────────────────────

        // Ink 底白框 + PhotoBlack 照片块 + 下方白字注记。框架 + ±2° 微旋转由 CardDrawer 外层包住，
        // 本函数只画内容。头像美术方案待定，占位：PhotoBlack 底 + 白色人形。
        // clocksBottomY：时钟徽章底部 Y（无徽章时等于 rect.y）——照片要让开，不然会被徽章压住。
        public static void DrawCharacter(Rect rect, GameNode node, bool disabled, float clocksBottomY)
        {
            bool hasClocks = clocksBottomY > rect.y;
            float photoTop = Mathf.Max(rect.y + PhotoTopPad, hasClocks ? clocksBottomY + 6f : rect.y);

            // 矮卡（或徽章占掉大半卡高）放不下照片时，退化为只有名字的悬浮名牌——
            // 与地点节点的矮框退化同一套语言，而不是无视竖向空间硬画一张比卡还高的照片。
            if (rect.yMax - photoTop < 90f)
            {
                var plateRect = new Rect(rect.x, photoTop, rect.width, Mathf.Max(0f, rect.yMax - photoTop));
                var plateStyle = new GUIStyle(IMGUIStyles.CardTitle)
                {
                    fontSize = IMGUIStyles.FontSize(15),
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    normal = { textColor = disabled ? IMGUIStyles.TextSecondary : IMGUIStyles.Paper }
                };
                GUI.Label(plateRect, node.Name, plateStyle);
                return;
            }

            float photoW = rect.width - 32f;
            // 照片高度同时受首选尺寸（与 MeasureCharacterHeight 同一个）与卡片剩余竖向空间约束——
            // 瘦高卡不会再把照片撑得比卡还高；下方至少给标题+副标题预留位置。
            float titleBudget = GapPhotoToTitle + TitleH + MeasureSubtitleHeight(node, rect.width) + 6f;
            float photoH = Mathf.Min(PreferredPhotoHeight(rect.width), Mathf.Max(30f, rect.yMax - photoTop - titleBudget));
            var photoRect = new Rect(rect.x + 16f, photoTop, photoW, photoH);

            GUI.color = IMGUIStyles.PhotoBlack;
            GUI.DrawTexture(photoRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            IMGUIStyles.DrawOutline(photoRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.30f));

            var photoStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                fontSize = IMGUIStyles.FontSize(32),
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f) }
            };
            GUI.Label(photoRect, "?", photoStyle);

            float textY = photoRect.yMax + GapPhotoToTitle;
            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            if (disabled) titleStyle.normal.textColor = IMGUIStyles.TextSecondary;
            GUI.Label(new Rect(rect.x + 10f, textY, rect.width - 20f, TitleH), node.Name, titleStyle);

            if (!string.IsNullOrEmpty(node.Subtitle))
            {
                var subStyle = new GUIStyle(IMGUIStyles.CardSubtitle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    clipping = TextClipping.Clip
                };
                float subY = textY + TitleH + 4f;
                GUI.Label(new Rect(rect.x + 10f, subY, rect.width - 20f, Mathf.Max(0f, rect.yMax - subY - 6f)), node.Subtitle, subStyle);
            }
        }
    }
}
