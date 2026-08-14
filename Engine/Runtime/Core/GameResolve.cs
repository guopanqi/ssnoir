#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public enum ResolveType
    {
        Instant,
        Roll,
        Observe,
        Note
    }

    public class GameResolve
    {
        public ResolveType Type { get; set; }

        // ── Note（标注）────────────────────────────────────────────────
        // 空间里一段不可操作的说明。玩家眼里它只有一种：站在这儿就看得见的字。
        // 带不带读数是它的一个属性，不是另一个物种——所以纯文字与「钟 + 描述」
        // 共用这一个类型，而不是各立一个。三项都可空，但不能全空。
        //
        // 数据来源仍然分家：Clock 是 make-clock 造出来的活对象（脚本会 tick 它），
        // Title/Text 只是字符串。合并的是呈现，不是来源。
        public string NoteTitle { get; set; } = string.Empty;
        public string NoteText { get; set; } = string.Empty;

        // 读数（可选）。钟自己的 Label / Note 不参与绘制——它们在解析时已经
        // 分别落到 NoteTitle / NoteText，绘制层只认后者，免得同一句话画两遍。
        public GameClock? Clock { get; set; }

        // Instant
        public ActionOutcome? Outcome { get; set; }

        // Roll
        public string SkillName { get; set; } = string.Empty;
        public List<DifficultyModifierInfo> DifficultyModifiers { get; set; } = new List<DifficultyModifierInfo>();
        public ActionOutcome? FailOutcome { get; set; }
        public ActionOutcome? NeutralOutcome { get; set; }
        public ActionOutcome? SuccessOutcome { get; set; }

        // Observe
        public string ObserveText { get; set; } = string.Empty;
    }
}
