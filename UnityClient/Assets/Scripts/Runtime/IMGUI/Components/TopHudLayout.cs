#nullable enable
using UnityEngine;

namespace SSNoir.IMGUI
{
    /// <summary>
    /// 顶栏右侧控件的水平流式布局。
    /// 从屏幕右边缘向左依次安放，避免各控件各自反推坐标而发生重叠。
    /// </summary>
    public struct TopHudLayout
    {
        private const float RightMargin = 10f;
        private const float Top = 26f;
        private const float RowHeight = 36f;
        private const float Gap = 8f;

        public Rect DebugToggle { get; }
        public Rect GrowthToggle { get; }
        public Rect RelationToggle { get; }

        private TopHudLayout(Rect debugToggle, Rect growthToggle, Rect relationToggle)
        {
            DebugToggle = debugToggle;
            GrowthToggle = growthToggle;
            RelationToggle = relationToggle;
        }

        public static TopHudLayout Create()
        {
            float cursorX = UIScale.VW - RightMargin;
            Rect debugToggle = TakeFromRight(ref cursorX, 70f, 32f);
            Rect growthToggle = TakeFromRight(ref cursorX, 112f, 34f);
            Rect relationToggle = TakeFromRight(ref cursorX, 236f, 36f);
            return new TopHudLayout(debugToggle, growthToggle, relationToggle);
        }

        private static Rect TakeFromRight(ref float cursorX, float width, float height)
        {
            cursorX -= width;
            var rect = UIScale.PixelSnap(new Rect(cursorX, Top + (RowHeight - height) * 0.5f, width, height));
            cursorX -= Gap;
            return rect;
        }
    }
}
