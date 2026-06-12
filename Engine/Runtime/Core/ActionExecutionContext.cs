#nullable enable
using System.Collections.Generic;

namespace SSNoir.Core
{
    public class ActionExecutionContext
    {
        public string ActorId { get; set; } = string.Empty;
        public string Mode { get; set; } = "world"; // "world" or "encounter"
        public List<SlottedResource?> SlottedResources { get; set; } = new List<SlottedResource?>();
    }
}
