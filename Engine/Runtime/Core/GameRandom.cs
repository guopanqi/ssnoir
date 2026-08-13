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
        public static Random Instance { get; private set; } = new Random();

        /// <summary>固定随机种子，让一局可以原样重放。只给离线工具用（试跑、复现某一局），
        /// 正式游戏不调用它。</summary>
        public static void Reseed(int seed)
        {
            Instance = new Random(seed);
        }
    }
}
