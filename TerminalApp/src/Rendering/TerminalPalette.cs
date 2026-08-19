using Raylib_cs;

namespace SSNoir.Rendering
{
    // Terminal 客户端自己的高亮语言：亮紫负责交互与进展，红黄绿只表达结果语义。
    public static class TerminalPalette
    {
        public static readonly Color Surface = new Color(20, 20, 28, 255);
        public static readonly Color SurfaceRaised = new Color(30, 30, 42, 255);
        public static readonly Color Border = new Color(70, 70, 95, 255);
        public static readonly Color Accent = new Color(130, 130, 250, 255);
        public static readonly Color AccentBright = new Color(175, 165, 255, 255);
        public static readonly Color AccentDark = new Color(45, 45, 82, 255);
        public static readonly Color Text = new Color(220, 220, 240, 255);
        public static readonly Color TextMuted = new Color(150, 150, 175, 255);

        // 只读信息（时钟、标注）不是可执行卡：用更平的表面与高对比冷白文字区分，
        // 紫色仅表示进度，不能再承担说明文字的可读性。
        public static readonly Color InfoSurface = new Color(25, 26, 37, 255);
        public static readonly Color InfoBorder = new Color(77, 80, 108, 255);
        public static readonly Color InfoTrack = new Color(52, 54, 70, 255);
        public static readonly Color InfoText = new Color(225, 227, 238, 255);
        public static readonly Color InfoBody = new Color(184, 188, 207, 255);
    }
}
