#nullable enable

namespace SSNoir.Core
{
    // 只有这句非阻塞气泡真正开始展示时才兑现；未播放、被清除或跨日均不补发。
    public sealed class BanterComposureRecovery
    {
        public string ActorId { get; init; } = string.Empty;
        public int Amount { get; init; }
        public string Notice { get; init; } = string.Empty;
        public int WorldDay { get; init; }
        internal bool Consumed { get; set; }
    }

    // 一条台词。Speaker 解析顺序固定:队伍角色 Id → 队伍角色 Name → 当前场景节点 Name → 报错。
    public sealed class DialogueLine
    {
        public BanterComposureRecovery? RecoveryOnShow { get; init; }
        public string Speaker { get; init; } = string.Empty;   // 队员 Id/Name,或场景节点名(如"海伦")
        public string DisplaySpeaker { get; init; } = string.Empty; // 玩家可见署名；Speaker 保持锚点身份
        // null：旧式字符串；空串：声明人物但未指定外貌，使用通用剪影。
        public string? Appearance { get; init; }
        public string Text { get; init; } = string.Empty;
        public string? VoiceId { get; init; }                  // 预留语音;为 null 表示无语音
        public float DwellSeconds { get; init; }               // 仅 Banter 使用;<=0 表示由文本长度估算
        public DialogueStageCue Stage { get; init; } = DialogueStageCue.None;  // 舞台指示；仅阻塞对话使用
        public string Anchor { get; init; } = string.Empty;  // 行级世界锚点 override（line 的 :at）；为空则按 Speaker 解析；仅 Banter 使用
    }
}
