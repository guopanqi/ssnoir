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
        private static readonly Color Paper72 = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.72f);
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
        //
        // 牌子这一族（地点牌、动作牌）比动作面板更紧：内边距、行高、字号都收一档。
        // 牌子是「场景里有个东西」的标记，不是要读一段话的物件；它越轻，场景越是主体。
        // 字号 21 而不是卡标题的 23：一屏要同时看见十来块牌子，标题字再大就又变回卡了。
        private const float LocationPadX = 12f;
        private const float LocationIconSize = 22f;
        private const float GapIconToName = 8f;
        private const float LocationRowH = 26f;
        private const float PlatePadY = 10f;
        private const string NodeIconResourceDirectory = "UI/NodeIcons";
        private static readonly IReadOnlyDictionary<string, Texture2D> NodeIcons = LoadNodeIcons();
        // 牌子再小也要好点：不低于一个手指目标。
        private static float MinLocationHeight => UIScale.MinTouchSize;

        /// <summary>牌子族共用的标题样式（地点牌 / 动作牌），比卡标题小一档。</summary>
        public static GUIStyle PlateTitleStyle(Color color) => new GUIStyle(IMGUIStyles.CardTitle)
        {
            fontSize = IMGUIStyles.FontSize(21),
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
            normal = { textColor = color }
        };

        /// <summary>牌子按「一行文字 + 左右内边距」需要多宽。动作牌也用这个量。</summary>
        public static float PlateTextWidth(string text)
            => LocationPadX * 2f + PlateTitleStyle(IMGUIStyles.Paper).CalcSize(new GUIContent(text)).x;

        /// <summary>牌子（一行）的高度：时钟块 + 一行 + 下内边距，按触控尺寸兜底。</summary>
        public static float MeasurePlateHeight(float clocksHeight)
            => Mathf.Max(MinLocationHeight, (clocksHeight > 0f ? clocksHeight + GapAfterClocks : PlatePadY) + LocationRowH + PlatePadY);

        /// <summary>牌子的底：Ink + 硬投影，无描边（存在靠阴影）。</summary>
        // 试过把牌子换成「场景底色 + 描线框、无投影」的线稿牌，想让它更像长在图纸上。
        // 结论：不要。卡是能拿起来的器物，实墨底 + 硬投影正是「可以碰」的信号，换掉就脏且
        // 认不出；融入场景的活只交给标注那一族（雾底 + 描边字，见 AnnotationDrawer）。
        public static void DrawPlateBase(Rect rect)
        {
            DrawPlateBase(rect, IMGUIStyles.Ink);
        }

        /// <summary>牌子底的换底色版：顶栏暗条用 HudBg（深蓝、暗、微透），和地点牌区分。</summary>
        public static void DrawPlateBase(Rect rect, Color bg)
        {
            IMGUIStyles.DrawShadow(rect, new Vector2(7f, 9f), 0.58f);
            IMGUIStyles.SetColor(bg);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
        }

        public static Color PlateLineColor(bool disabled, bool hover)
            => disabled ? Paper35 : (hover ? IMGUIStyles.Paper : Paper72);

        /// <summary>牌子上那一行：让开时钟徽章后，在剩余高度里居中画「可选图标 + 文字」。</summary>
        public static void DrawPlateRow(Rect rect, string text, Texture2D? icon, Color line, float clocksBottomY)
        {
            bool hasClocks = clocksBottomY > rect.y;
            float contentTop = hasClocks ? clocksBottomY + GapAfterClocks : rect.y + PlatePadY;
            float availH = rect.yMax - PlatePadY - contentTop;
            // 徽章占掉太多、按流量已经没地方了：挤到卡底也要把字画出来，不能什么都不画。
            if (availH < LocationRowH)
                availH = Mathf.Min(LocationRowH, rect.yMax - contentTop);
            if (availH <= 0f)
                return;

            float rowH = Mathf.Min(LocationRowH, availH);
            float rowY = contentTop + (availH - rowH) * 0.5f;
            float nameX = rect.x + LocationPadX;
            if (icon != null)
            {
                float iconH = Mathf.Min(LocationIconSize, rowH);
                var iconArea = new Rect(nameX, rowY + (rowH - iconH) * 0.5f, iconH, iconH);
                IMGUIStyles.SetColor(line);
                GUI.DrawTexture(iconArea, icon, ScaleMode.ScaleToFit, true);
                IMGUIStyles.ResetColor();
                nameX = iconArea.xMax + GapIconToName;
            }
            IMGUIStyles.DrawLabel(
                new Rect(nameX, rowY, Mathf.Max(0f, rect.xMax - LocationPadX - nameX), rowH),
                text, PlateTitleStyle(line));
        }

        private static float PreferredPhotoHeight(float cardWidth)
            => Mathf.Min((cardWidth - 32f) * 0.72f, MaxPhotoHeight);

        // 地点牌按「可选图标 + 一个地名」实际需要多宽收敛：叫「家」的地方不该和「老街酒馆」
        // 占一样宽。窄卡在屏幕上少占地方，同一栋楼的几张卡也就更容易各自让开。
        //
        // 牌子上挂着的时钟按自己需要的宽度一起参与：地名短不等于这张牌可以窄到把钟裁掉。
        // 短标题算出的下限可能小于时钟等动态标签所需宽度，标签就会被截断——
        // 和 CardDrawer.LayoutClockBadges 注释里说的是同一个错误：
        // 宽度必须由内容说了算，不能由一个猜出来的下限说了算。
        public static float PreferredLocationWidth(GameNode node)
        {
            float iconW = NodeIcon(node) != null ? LocationIconSize + GapIconToName : 0f;
            float need = PlateTextWidth(node.DisplayTitle) + iconW;

            // +24 是 LayoutClockBadges 给徽章行留的左右余量，两边必须用同一个数。
            foreach (var clock in node.Clocks)
                need = Mathf.Max(need, CardDrawer.MeasureClockBadge(clock, compact: false) + 24f);

            return Mathf.Clamp(need, 96f, 260f);
        }

        // 副标题按实际换行结果占高（上限 4 行左右），不再固定「剩下多少算多少」。
        private static float MeasureSubtitleHeight(GameNode node, float cardWidth)
        {
            if (string.IsNullOrEmpty(node.Subtitle))
                return 0f;
            var style = new GUIStyle(IMGUIStyles.CardSubtitle) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            return Mathf.Clamp(style.CalcHeight(new GUIContent(node.Subtitle), cardWidth - 24f), 18f, 88f);
        }

        // 地点牌不套 MinCardHeight：那个下限是给「里面有东西要操作」的卡定的，
        // 地名牌整张就是一个点击目标，按 MinTouchSize 兜底就够。
        public static float MeasureLocationHeight(GameNode node, float cardWidth, float clocksHeight)
        {
            return MeasurePlateHeight(clocksHeight);
        }

        // 普通 Container 与地点牌同一形态：一行牌子。它是「进去看」的入口，不是读物，
        // 副标题不上牌（网格里一排入口各顶着一段说明，就又变回一叠卡了）。
        public static float MeasureCommonHeight(GameNode node, float cardWidth, float clocksHeight)
        {
            return MeasurePlateHeight(clocksHeight);
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
            DrawPlateBase(rect);

            Color line = PlateLineColor(disabled, hover);

            DrawPlateRow(rect, node.DisplayTitle, NodeIcon(node), line, clocksBottomY);
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
                IMGUIStyles.DrawLabel(plateRect, node.DisplayTitle, plateStyle);
                return;
            }

            float photoW = rect.width - 32f;
            // 照片高度同时受首选尺寸（与 MeasureCharacterHeight 同一个）与卡片剩余竖向空间约束——
            // 瘦高卡不会再把照片撑得比卡还高；下方至少给标题+副标题预留位置。
            float titleBudget = GapPhotoToTitle + TitleH + MeasureSubtitleHeight(node, rect.width) + 6f;
            float photoH = Mathf.Min(PreferredPhotoHeight(rect.width), Mathf.Max(30f, rect.yMax - photoTop - titleBudget));
            var photoRect = new Rect(rect.x + 16f, photoTop, photoW, photoH);

            IMGUIStyles.SetColor(IMGUIStyles.PhotoBlack);
            GUI.DrawTexture(photoRect, Texture2D.whiteTexture);
            IMGUIStyles.ResetColor();
            IMGUIStyles.DrawOutline(photoRect, 1f, new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.30f));

            var photoStyle = new GUIStyle(IMGUIStyles.CardTitle)
            {
                fontSize = IMGUIStyles.FontSize(32),
                normal = { textColor = new Color(IMGUIStyles.Paper.r, IMGUIStyles.Paper.g, IMGUIStyles.Paper.b, 0.25f) }
            };
            IMGUIStyles.DrawLabel(photoRect, "?", photoStyle);

            float textY = photoRect.yMax + GapPhotoToTitle;
            var titleStyle = new GUIStyle(IMGUIStyles.CardTitle) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            if (disabled) titleStyle.normal.textColor = IMGUIStyles.TextSecondary;
            IMGUIStyles.DrawLabel(new Rect(rect.x + 10f, textY, rect.width - 20f, TitleH), node.DisplayTitle, titleStyle);

            if (!string.IsNullOrEmpty(node.Subtitle))
            {
                var subStyle = new GUIStyle(IMGUIStyles.CardSubtitle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    clipping = TextClipping.Clip
                };
                float subY = textY + TitleH + 4f;
                IMGUIStyles.DrawLabel(new Rect(rect.x + 10f, subY, rect.width - 20f, Mathf.Max(0f, rect.yMax - subY - 6f)), node.Subtitle, subStyle);
            }
        }
    }
}
