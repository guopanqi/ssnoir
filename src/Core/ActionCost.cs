using System;

namespace SSNoir.Core
{
    public class ActionCost
    {
        public string Type { get; set; } = string.Empty; // "die" or "item"
        public string ItemName { get; set; } = string.Empty;
        public int Qty { get; set; } = 0;
    }
}
