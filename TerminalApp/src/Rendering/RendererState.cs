using System;
using System.Collections.Generic;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public class SelectedResource
    {
        public string Type { get; set; } = string.Empty; // "die" or "item"
        public string ItemName { get; set; } = string.Empty;
        public int Value { get; set; }
        public int SourceIndex { get; set; } = -1;
        public string ActorId { get; set; } = string.Empty;
        public int DieIndex { get; set; } = -1;
    }

    public class DropdownItem
    {
        public string Name { get; set; } = string.Empty;
        public bool IsHeader { get; set; }
        public string SceneName { get; set; } = string.Empty;
    }

    public class RendererState
    {
        public List<GameNode> NavigationStack { get; } = new List<GameNode>();
        public List<GameNode> VisibleNodes { get; set; } = new List<GameNode>();

        public bool IsDropdownOpen { get; set; } = false;
        public List<DropdownItem> DropdownItems { get; } = new List<DropdownItem>();

        public HashSet<string> FlippedNodes { get; } = new HashSet<string>();
        public Dictionary<string, List<SlottedResource?>> NodeSlots { get; } = new Dictionary<string, List<SlottedResource?>>();
        public SelectedResource? SelectedResource { get; set; } = null;
        public ActionReport? ActiveRollResult { get; set; } = null;
        public string ActiveRollActionName { get; set; } = string.Empty;
        public float ActiveRollTime { get; set; } = 0f;
        public int ActiveRollPhase { get; set; } = 0; // 0: rolling, 1: reveal pulse, 2: outcome
        public int ActiveRollDisplayDieValue { get; set; } = 1;
        public float ActiveRollDisplayScale { get; set; } = 1f;
        public string UiNotification { get; set; } = string.Empty;
        public float UiNotificationTimer { get; set; } = 0f;

        public void TriggerNotification(string message)
        {
            UiNotification = message;
            UiNotificationTimer = 2.5f;
        }

        public void ClearOtherNodeSlots(string activeNodeName)
        {
            foreach (var pair in NodeSlots)
            {
                if (pair.Key != activeNodeName)
                {
                    var slots = pair.Value;
                    for (int i = 0; i < slots.Count; i++)
                    {
                        slots[i] = null;
                    }
                }
            }
        }

        public bool IsDieSlotted(int dieIndex)
        {
            if (SelectedResource != null && SelectedResource.Type == "die" && SelectedResource.SourceIndex == dieIndex)
                return true;
            foreach (var slots in NodeSlots.Values)
            {
                foreach (var slot in slots)
                {
                    if (slot != null && slot.Type == "die" && slot.SourceIndex == dieIndex)
                        return true;
                }
            }
            return false;
        }

        public int GetRemainingItemQty(GameState gameState, string itemName)
        {
            int total = gameState.Get<int>("item:" + itemName, 0);

            foreach (var slots in NodeSlots.Values)
            {
                foreach (var slot in slots)
                {
                    if (slot != null && slot.Type == "item")
                    {
                        if (slot.ItemId.Equals(itemName, StringComparison.OrdinalIgnoreCase))
                        {
                            total -= slot.Qty > 0 ? slot.Qty : slot.Value;
                        }
                    }
                }
            }

            if (SelectedResource != null && SelectedResource.Type == "item")
            {
                if (SelectedResource.ItemName.Equals(itemName, StringComparison.OrdinalIgnoreCase))
                {
                    total -= 1;
                }
            }

            return Math.Max(0, total);
        }
    }
}
