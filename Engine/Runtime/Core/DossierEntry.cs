#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    /// <summary>
    /// 卷宗里的一条故事线。由拥有这条故事的内容模块自己回答（见 engine.scm 的 dossier），
    /// 世界只负责收集——和地点可见性一样，世界不解释故事。
    ///
    /// <see cref="Now"/> 是这条线的全部意义：玩家隔三天回来读的那一句。
    /// 客户端不自己拼这句话，也不从 Log 里推——推不出来就是内容没写。
    /// </summary>
    public sealed class DossierEntry
    {
        /// <summary>存档键，也是「钉住哪条」记的那个值。</summary>
        public string Id { get; init; } = string.Empty;
        /// <summary>委托 / 人物 / 城市。决定它在面板里分到哪一组。</summary>
        public string Kind { get; init; } = "委托";
        /// <summary>进行中 / 等着别人 / 了结。</summary>
        public string Status { get; init; } = "进行中";
        /// <summary>「现在」那一句。第二人称，能照着做。</summary>
        public string Now { get; init; } = string.Empty;
        /// <summary>该去哪个地点，可空。</summary>
        public string Where { get; init; } = string.Empty;
        /// <summary>故事已经在用的钟，原样借用，不为卷宗新建一套。</summary>
        public IReadOnlyList<GameClock> Clocks { get; init; } = new List<GameClock>();
        /// <summary>履历，最新在前。</summary>
        public IReadOnlyList<DossierLogEntry> Log { get; init; } = new List<DossierLogEntry>();

        public bool IsClosed => Status == "了结";
    }

    /// <summary>履历里的一行：哪一天，发生了什么。</summary>
    public sealed class DossierLogEntry
    {
        public int Day { get; init; }
        public string Text { get; init; } = string.Empty;
    }
}
