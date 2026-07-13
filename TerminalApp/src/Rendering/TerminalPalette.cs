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
    }
}
