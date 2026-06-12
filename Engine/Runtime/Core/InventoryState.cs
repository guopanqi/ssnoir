#nullable enable
using System;
using System.Collections.Generic;

namespace SSNoir.Core
{
    public class InventoryState
    {
        public Dictionary<string, int> Items { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public event Action? OnInventoryChanged;

        public int GetCount(string itemId)
        {
            if (Items.TryGetValue(itemId, out var count))
            {
                return count;
            }
            return 0;
        }

        public void SetCount(string itemId, int count)
        {
            if (count < 0)
            {
                throw new ArgumentException($"Item count for '{itemId}' cannot be negative: {count}");
            }
            Items[itemId] = count;
            OnInventoryChanged?.Invoke();
        }
    }
}
