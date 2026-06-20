#nullable enable
using System;

namespace SSNoir.Core
{
    // Single shared RNG for all gameplay randomness (dice rolls, random-choice).
    // Creating `new Random()` per call is unsafe on time-seeded runtimes (e.g. Mono):
    // instances created within the same tick share a seed and produce correlated
    // sequences. One shared instance avoids that. Game logic is single-threaded,
    // so no locking is needed.
    public static class GameRandom
    {
        public static readonly Random Instance = new Random();
    }
}
