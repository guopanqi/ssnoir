#nullable enable
using System;

namespace SSNoir.Core
{
    public sealed class NarrationCenter
    {
        public event Action<string>? OnNarrationRequested;

        public void Play(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("narration id cannot be empty");
            OnNarrationRequested?.Invoke(id);
        }
    }
}
