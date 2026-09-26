#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// Orbit 等非 Pan 视角下动作面板首次出现时的版面：退到左右两条栏里，把中间留给场景。
    /// 避让完成后的坐标由 IMGUIWorldRenderer 保存，不随每帧锚点投影重算。
    ///
    /// <b>栏宽是个常数，不去测建筑。</b>曾经想过把建筑的旋转包络投影出来、按它的实际体量
    /// 决定卡片让开多少——那是在给一个不存在的问题造机器。卡片宽度本来就是固定的，
    /// 需要的只是两边各留一条够宽的缝；建筑在画面里多大，由 Blender 那头的机位保证，
    /// 拍得太满就去改机位，那本来就是做资产时该做的事。
    ///
    /// Pan 的卡片在锚点周围避让，并在首次排布后固定在世界空间；本类只负责其他视角。
    /// </summary>
    public static class CardGutterLayout
    {
        // 卡片离安全区边缘留的缝。与 CardKeepOut.EdgeInset 同一个量级，各管各的方向。
        private const float EdgeInset = 12f;

        // 换栏的迟滞：锚点要越过屏幕中线这么远（安全区宽度的比例）才改换另一条栏。
        //
        // 没有这个的话，一栋楼的锚点大多挤在画面中央，谁在中线左边、谁在右边只差几个像素；
        // 镜头绕楼一转，同一个锚点就来回过线，而排布是停稳才重算的——于是每停一次卡片
        // 换一次边。位置得稳到能形成肌肉记忆，那比"离锚点最近"重要。
        private const float SideSwitchMargin = 0.10f;

        // 每个锚点认定的那条栏。键是锚点名，取值范围就是这座城里的锚点集合（几十个），
        // 不会无限增长，所以不做清理。
        private static readonly Dictionary<string, bool> _sideByAnchor = new Dictionary<string, bool>();

        /// <summary>
        /// 这张卡该去哪条栏、停在哪。
        ///
        /// 初始竖直方向对齐锚点（不再上浮）：卡片与它指的东西大致等高，引线就是一条几乎水平
        /// 的直线，穿过中间的空白进到卡的侧边中点。横向按锚点在屏幕的哪一半选栏。
        /// 同一条栏里的卡随后由 SolveProjectedStacks 竖着消重叠。
        ///
        /// 卡片一律贴着栏的**内缘**对齐——那是引线进来的那条边，对齐它，几张宽窄不同的卡
        /// 也共用同一个入口 x。
        ///
        /// 选栏按<b>锚点</b>而不是按卡：同一处长出来的几个动作必须待在一块儿，
        /// 分到屏幕两边就读不出"它们是一个地方的"了。
        /// </summary>
        public static Vector2 TargetCenter(string anchorKey, Vector2 anchorPos, float cardWidth, float laneWidth)
        {
            Rect safe = UIScale.SafeArea;
            bool onLeft = ResolveSide(anchorKey, anchorPos.x, safe);

            float innerEdge = onLeft
                ? safe.xMin + EdgeInset + laneWidth
                : safe.xMax - EdgeInset - laneWidth;
            float centerX = onLeft
                ? innerEdge - cardWidth / 2f
                : innerEdge + cardWidth / 2f;

            // 比一条栏还宽的卡（聚焦态）只能往画面中间长，但不许把自己顶出安全区。
            float half = cardWidth / 2f;
            centerX = Mathf.Clamp(
                centerX,
                safe.xMin + EdgeInset + half,
                Mathf.Max(safe.xMin + EdgeInset + half, safe.xMax - EdgeInset - half));

            return new Vector2(centerX, anchorPos.y);
        }

        private static bool ResolveSide(string anchorKey, float anchorX, Rect safe)
        {
            float middle = safe.center.x;
            if (!_sideByAnchor.TryGetValue(anchorKey, out bool onLeft))
            {
                onLeft = anchorX < middle;
                _sideByAnchor[anchorKey] = onLeft;
                return onLeft;
            }

            float margin = safe.width * SideSwitchMargin;
            if (onLeft && anchorX > middle + margin)
                onLeft = false;
            else if (!onLeft && anchorX < middle - margin)
                onLeft = true;

            _sideByAnchor[anchorKey] = onLeft;
            return onLeft;
        }
    }
}
