using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public class GameNode
    {
        public string Name { get; set; }
        public List<GameNode> Children { get; set; } = new List<GameNode>();
        public Action Effect { get; set; }

        public bool HasChildren => Children != null && Children.Count > 0;
        public bool HasEffect => Effect != null;
    }
}
