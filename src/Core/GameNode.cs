using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public class GameNode
    {
        public string Name { get; set; } = string.Empty;
        public List<GameClock> Clocks { get; } = new List<GameClock>();
        public List<GameNode> Children { get; set; } = new List<GameNode>();
        
        public List<ActionCost> Requires { get; set; } = new List<ActionCost>();
        public GameResolve? Resolve { get; set; }

        public bool HasChildren => Children != null && Children.Count > 0;
        public bool HasResolve => Resolve != null;
    }
}
