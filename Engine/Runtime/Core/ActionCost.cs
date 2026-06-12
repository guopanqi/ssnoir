#nullable enable
using System;

namespace SSNoir.Core
{
    public class ActionCost
    {
        public string Type { get; set; } = string.Empty; // "die" or "item"
        public string ItemId { get; set; } = string.Empty;
        public int Qty { get; set; } = 0;
    }

    public class SlottedResource
    {
        public string ActorId { get; set; } = string.Empty;
        public int DieIndex { get; set; } = -1;
        public string Type { get; set; } = string.Empty; // "die" or "item"
        public string ItemId { get; set; } = string.Empty;
        public int Value { get; set; } = 0;
        public int Qty { get; set; } = 0;
        public int SourceIndex { get; set; } = -1;
    }
}
