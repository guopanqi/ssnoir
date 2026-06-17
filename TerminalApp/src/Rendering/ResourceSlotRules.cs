using System;
using System.Collections.Generic;
using SSNoir.Core;

namespace SSNoir.Rendering
{
    public static class ResourceSlotRules
    {
        public static bool CanMatchRequirement(ActionCost requirement, SelectedResource resource)
        {
            if (requirement.Type == "die")
            {
                return resource.Type == "die";
            }

            if (requirement.Type == "item")
            {
                return resource.Type == "item"
                    && requirement.ItemId.Equals(resource.ItemName, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public static bool CanPlaceSelectedResource(
            RendererState state,
            ActionCost requirement,
            IReadOnlyList<SlottedResource?> targetSlots,
            int targetSlotIndex)
        {
            var resource = state.SelectedResource;
            if (resource == null || !CanMatchRequirement(requirement, resource))
            {
                return false;
            }

            if (requirement.Type == "die")
            {
                return true;
            }

            if (requirement.Type != "item")
            {
                return false;
            }

            int totalOwned = state.DisplayedSnapshot.Inventory.TryGetValue(requirement.ItemId, out var ownedQty)
                ? ownedQty
                : 0;
            int totalSlotted = 0;

            foreach (var slotsList in state.NodeSlots.Values)
            {
                for (int i = 0; i < slotsList.Count; i++)
                {
                    if (ReferenceEquals(slotsList, targetSlots) && i == targetSlotIndex)
                    {
                        continue;
                    }

                    var slot = slotsList[i];
                    if (slot != null
                        && slot.Type == "item"
                        && slot.ItemId.Equals(requirement.ItemId, StringComparison.OrdinalIgnoreCase))
                    {
                        totalSlotted += slot.Qty > 0 ? slot.Qty : slot.Value;
                    }
                }
            }

            return totalOwned - totalSlotted >= requirement.Qty;
        }
    }
}
