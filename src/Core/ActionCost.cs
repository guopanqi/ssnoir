using System;

namespace SSNoir.Core
{
    public class ActionCost
    {
        public string Type { get; set; } = string.Empty; // "die" or "item"
        public string ItemName { get; set; } = string.Empty;
        public int Qty { get; set; } = 0;
    }

    public class SlottedResource
    {
        public string Type { get; set; } = string.Empty; // "die" or "item"
        public string ItemName { get; set; } = string.Empty;
        public int Value { get; set; } = 0; // Value for die, 0 or unused for items
        public int SourceIndex { get; set; } = -1;
    }
}
