#nullable enable
using SSNoir.Theatre;
using SSNoir.IMGUI;
using UnityEngine;

namespace SSNoir.UnityTheatre
{
    // 播放器提供时间与点击状态；视觉由与旁白、Dialogue 共用的字幕组件绘制。
    internal sealed class TheatreCaptions
    {
        public void Draw(TheatreSession session, Rect stage, float fade)
        {
            var line = session.Caption;
            if (line == null) return;
            StoryCaptionDrawer.Draw(stage, line.Text,
                line.Target == "世界" ? "" : line.Target,
                new Color(line.CaptionColor.R, line.CaptionColor.G, line.CaptionColor.B), fade,
                session.VisibleCharacters, session.CaptionTime, session.CaptionIsRevealed,
                maxRows: 2, variant: StoryCaptionDrawer.Variant.Theatre);
        }
    }
}
