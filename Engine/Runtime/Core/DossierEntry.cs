#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    /// <summary>
    /// 卷宗里的一条故事线。由拥有这条故事的内容模块自己回答（见 engine.scm 的 dossier），
    /// 世界只负责收集——和地点可见性一样，世界不解释故事。
    ///
    /// <see cref="Now"/> 说这一小节整体要干什么，说不清就留空；<see cref="Steps"/> 说走到了哪。
    /// 客户端不自己拼这些话，也不从 Log 里推——推不出来就是内容没写。
    /// </summary>
    public sealed class DossierEntry
    {
        /// <summary>存档键，也是「钉住哪条」记的那个值。</summary>
        public string Id { get; init; } = string.Empty;
        /// <summary>委托 / 人物 / 城市。决定它在面板里分到哪一组。</summary>
        public string Kind { get; init; } = "委托";
        /// <summary>是不是非走不可的主轴（阻塞性、必须到场）。面板里显示主要/次要，主要排前。</summary>
        public bool IsPrimary { get; init; }
        /// <summary>进行中 / 等着别人 / 了结。</summary>
        public string Status { get; init; } = "进行中";
        /// <summary>这一小节的整体目标，一句话；说不清就空着。当前该做什么由 Steps 说。</summary>
        public string Now { get; init; } = string.Empty;
        /// <summary>该去哪个地点，可空。</summary>
        public string Where { get; init; } = string.Empty;
        /// <summary>故事已经在用的钟，原样借用，不为卷宗新建一套。</summary>
        public IReadOnlyList<GameClock> Clocks { get; init; } = new List<GameClock>();
        /// <summary>这一小节的子项清单，按故事顺序；做完的划掉。界面只露到当前那一项，后面的不给看。</summary>
        public IReadOnlyList<DossierStep> Steps { get; init; } = new List<DossierStep>();
        /// <summary>履历，最新在前。</summary>
        public IReadOnlyList<DossierLogEntry> Log { get; init; } = new List<DossierLogEntry>();

        public bool IsClosed => Status == "了结";
    }

    /// <summary>子项清单里的一行。</summary>
    public sealed class DossierStep
    {
        public string Text { get; init; } = string.Empty;
        public bool Done { get; init; }
    }

    /// <summary>履历里的一行：哪一天，发生了什么。</summary>
    public sealed class DossierLogEntry
    {
        public int Day { get; init; }
        public string Text { get; init; } = string.Empty;
    }
}
