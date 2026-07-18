#nullable enable
using System.Collections.Generic;
using SSNoir.Core;

namespace SSNoir.IMGUI
{
    public sealed class CardPresentationResidue
    {
        public string AnchorNodeName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public RollOutcome? RollOutcome { get; set; }
        public int? FateDieValue { get; set; }
        public int PreparedValue { get; set; }
        public List<ActionEffectRecord> Effects { get; set; } = new List<ActionEffectRecord>();
        // 结算前的节点外观。节点从新快照消失后，仍用它作为不可交互的结果卡宿主。
        public GameNode? SourceNode { get; set; }

        // 结果首次可见（动画落定、切到 residue）的时刻；用于「结果从命运条下方揭开」的过渡。首帧惰性写入。
        public float RevealStartTime { get; set; } = 0f;
    }
}
