using System;
using System.Collections.Generic;
using System.Linq;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public class SelectedResource
    {
        public string Type { get; set; } = string.Empty; // "die" or "item"
        public string ItemName { get; set; } = string.Empty;
        public int Qty { get; set; } = 1;
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
        public PresentationSnapshot DisplayedSnapshot { get; set; } = new PresentationSnapshot();
        public List<GameNode> NavigationStack { get; } = new List<GameNode>();
        public List<GameNode> VisibleNodes { get; set; } = new List<GameNode>();

        public bool IsDropdownOpen { get; set; } = false; // legacy, kept for DropdownItems loading
        public bool IsDebugMenuOpen { get; set; } = false;
        public bool IsGrowthPanelOpen { get; set; } = false;
        public bool IsTurnPanelOpen { get; set; } = false;
        public List<DropdownItem> DropdownItems { get; } = new List<DropdownItem>();

        public bool IsPresentingAction { get; set; }
        public ActionReport? PendingReport { get; set; }
        public string PendingActionName { get; set; } = string.Empty;
        public float PresentationTimer { get; set; }
        public int PresentationStepIndex { get; set; }

        public HashSet<string> FlippedNodes { get; } = new HashSet<string>();
        public Dictionary<string, List<SlottedResource?>> NodeSlots { get; } = new Dictionary<string, List<SlottedResource?>>();
        public SelectedResource? SelectedResource { get; set; } = null;
        public float CardsScrollOffset { get; set; } = 0f;
        public List<float> CardsScrollStack { get; } = new List<float>();
        public float HandItemsScrollOffset { get; set; } = 0f;
        public ActionReport? ActiveRollResult { get; set; } = null;
        public string ActiveRollActionName { get; set; } = string.Empty;
        public float ActiveRollTime { get; set; } = 0f;
        public int ActiveRollPhase { get; set; } = 0; // 0: rolling, 1: reveal pulse, 2: outcome
        public int ActiveRollDisplayDieValue { get; set; } = 1;
        public float ActiveRollDisplayScale { get; set; } = 1f;
        public ActionReport? ActiveOutcomeResult { get; set; } = null;
        public string ActiveOutcomeActionName { get; set; } = string.Empty;
        public SpotlightCard? Spotlight { get; set; } = null;
        public Queue<SpotlightCard> PendingActionSpotlights { get; } = new Queue<SpotlightCard>();
        public SpotlightCard? ActiveActionSpotlight { get; set; } = null;
        public string ActiveNarrationId { get; set; } = string.Empty;
        public float ActiveNarrationTime { get; set; }
        public float ActiveNarrationDuration { get; set; } = 4f;
        public Dictionary<string, CardPresentationResidue> CardResidues { get; } = new Dictionary<string, CardPresentationResidue>();

        public void ClearAllNodeSlots()
        {
            NodeSlots.Clear();
            FlippedNodes.Clear();
            CardResidues.Clear();
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
            var toUnflip = FlippedNodes.Where(n => n != activeNodeName).ToList();
            foreach (var n in toUnflip)
                FlippedNodes.Remove(n);

            var toClear = CardResidues.Keys.Where(n => n != activeNodeName).ToList();
            foreach (var n in toClear)
                CardResidues.Remove(n);
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

        public int GetRemainingItemQty(string itemName)
        {
            int total = 0;
            if (DisplayedSnapshot.Inventory.TryGetValue(itemName, out var qty))
            {
                total = qty;
            }

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
                    total -= Math.Max(1, SelectedResource.Qty);
                }
            }

            return Math.Max(0, total);
        }

        public IEnumerable<(string Name, int Qty)> GetInventoryItems()
        {
            return DisplayedSnapshot.Inventory
                .Where(kvp => kvp.Value > 0)
                .Select(kvp => (kvp.Key, kvp.Value));
        }

        public int GetAvailableGrowthPoints(ActorSnapshot actor)
        {
            return Math.Max(0, DisplayedSnapshot.GrowthLevel - actor.SpentGrowthPoints);
        }
    }

    public class CardPresentationResidue
    {
        public string AnchorNodeName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public RollOutcome? RollOutcome { get; set; }
        public int? DieValue { get; set; }
        public int? ModifiedRollValue { get; set; }
        public List<ActionEffectRecord> Effects { get; set; } = new List<ActionEffectRecord>();
    }
}
