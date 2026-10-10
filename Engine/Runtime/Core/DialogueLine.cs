#nullable enable

namespace SSNoir.Core
{
    // 一条台词。Speaker 解析顺序固定:队伍角色 Id → 队伍角色 Name → 当前场景节点 Name → 报错。
    public sealed class DialogueLine
    {
        public string Speaker { get; init; } = string.Empty;   // 队员 Id/Name,或场景节点名(如"海伦")
        public string DisplaySpeaker { get; init; } = string.Empty; // 玩家可见署名；Speaker 保持锚点身份
        public string Text { get; init; } = string.Empty;
        public string? VoiceId { get; init; }                  // 预留语音;为 null 表示无语音
        public float DwellSeconds { get; init; }               // 仅 Banter 使用;<=0 表示由文本长度估算
        public DialogueStageCue Stage { get; init; } = DialogueStageCue.None;  // 舞台指示；仅阻塞对话使用
        public string Anchor { get; init; } = string.Empty;  // 行级世界锚点 override（line 的 :at）；为空则按 Speaker 解析；仅 Banter 使用
    }
}
