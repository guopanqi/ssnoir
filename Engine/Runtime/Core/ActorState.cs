#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public class ActorState
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // "protagonist", "companion"
        public string Status { get; set; } = "active";  // "active", "away"
        
        private int _stress = 0;
        public int Stress
        {
            get => _stress;
            set => _stress = Math.Clamp(value, 0, TeamState.MaxStress);
        }

        public List<int> ActionDice { get; set; } = new List<int>();

        public int SpentGrowthPoints { get; set; } = 0;
        
        public Dictionary<string, int> Stats { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "violence", 0 },
            { "knowledge", 0 },
            { "sharpness", 0 },
            { "social", 0 }
        };
    }
}
