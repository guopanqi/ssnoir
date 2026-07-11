#nullable enable
using System;
using System.Text;

namespace SSNoir.Core
{
    public static class FateStrip
    {
        public static RollOutcome[] Compute(int placedDie, int skill, int modSum)
        {
            ValidateInputs(placedDie, skill);

            var strip = new RollOutcome[6];
            for (int fateDie = 1; fateDie <= 6; fateDie++)
            {
                strip[fateDie - 1] = ResolveValidated(placedDie, skill, modSum, fateDie);
            }
            return strip;
        }

        public static RollOutcome Resolve(int placedDie, int skill, int modSum, int fateDie)
        {
            ValidateInputs(placedDie, skill);
            if (fateDie < 1 || fateDie > 6)
                throw new ArgumentOutOfRangeException(nameof(fateDie), "Fate die must be between 1 and 6.");
            return ResolveValidated(placedDie, skill, modSum, fateDie);
        }

        public static int NaturalModifier(int fateDie)
        {
            if (fateDie < 1 || fateDie > 6)
                throw new ArgumentOutOfRangeException(nameof(fateDie), "Fate die must be between 1 and 6.");
            return fateDie == 1 ? -1 : fateDie == 6 ? 1 : 0;
        }

        public static int PreparedValue(int placedDie, int skill, int modSum)
        {
            ValidateInputs(placedDie, skill);
            return placedDie + skill + modSum;
        }

        public static int FinalTotal(int placedDie, int skill, int modSum, int fateDie)
        {
            return PreparedValue(placedDie, skill, modSum) + fateDie + NaturalModifier(fateDie);
        }

        public static string Describe(RollOutcome[] strip)
        {
            if (strip == null || strip.Length != 6)
                throw new ArgumentException("Fate strip must contain exactly six outcomes.", nameof(strip));

            var text = new StringBuilder();
            int start = 0;
            while (start < strip.Length)
            {
                int end = start;
                while (end + 1 < strip.Length && strip[end + 1] == strip[start])
                    end++;

                if (text.Length > 0) text.Append(" · ");
                text.Append(start == end ? (start + 1).ToString() : $"{start + 1}–{end + 1}");
                text.Append(' ');
                text.Append(strip[start] switch
                {
                    RollOutcome.Fail => "坏",
                    RollOutcome.Neutral => "中",
                    RollOutcome.Success => "好",
                    _ => throw new ArgumentOutOfRangeException(nameof(strip)),
                });
                start = end + 1;
            }
            return text.ToString();
        }

        private static RollOutcome ResolveValidated(int placedDie, int skill, int modSum, int fateDie)
        {
            int total = placedDie + skill + modSum + fateDie + NaturalModifier(fateDie);
            if (total <= 6) return RollOutcome.Fail;
            if (total <= 8) return RollOutcome.Neutral;
            return RollOutcome.Success;
        }

        private static void ValidateInputs(int placedDie, int skill)
        {
            if (placedDie < 1 || placedDie > 6)
                throw new ArgumentOutOfRangeException(nameof(placedDie), "Placed action die must be between 1 and 6.");
            if (skill < TeamState.MinStatLevel || skill > TeamState.MaxStatLevel)
                throw new ArgumentOutOfRangeException(nameof(skill),
                    $"Skill must be between {TeamState.MinStatLevel} and {TeamState.MaxStatLevel}.");
        }
    }
}
