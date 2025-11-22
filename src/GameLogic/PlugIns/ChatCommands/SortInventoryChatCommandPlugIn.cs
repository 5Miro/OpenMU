// <copyright file="SortInventoryChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// A chat command plugin which sorts the player's inventory by item type, then by item ID, optimizing for item sizes.
/// </summary>
[Guid("A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D")]
[PlugIn("Sort Inventory chat command", "Sorts inventory by item type and ID, optimizing for item sizes. Usage: /sortinv")]
[ChatCommandHelp(Command, "Sorts inventory by item type, then by item ID, optimizing placement considering item sizes.")]
public class SortInventoryChatCommandPlugIn : ChatCommandPlugInBase<EmptyChatCommandArgs>
{
    private const string Command = "/sortinv";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, EmptyChatCommandArgs arguments)
    {
        if (player.SelectedCharacter is null || player.Inventory is null)
        {
            await this.ShowMessageToAsync(player, "Inventory not available.").ConfigureAwait(false);
            return;
        }

        // Get all items from inventory (excluding equipped items, slots 0-11)
        var inventoryItems = player.Inventory.Items
            .Where(item => item.ItemSlot >= InventoryConstants.EquippableSlotsCount)
            .Where(item => item.Definition is not null)
            .ToList();

        if (inventoryItems.Count == 0)
        {
            await this.ShowMessageToAsync(player, "Inventory is empty.").ConfigureAwait(false);
            return;
        }

        // Step 1: Group items by their Group (type), then sort groups
        var groupedItems = inventoryItems
            .GroupBy(item => item.Definition!.Group)
            .OrderBy(group => group.Key) // Sort groups by type number
            .ToList();

        // Step 2: For each group, sort by height desc, then width desc, then ID
        // This creates visual clusters of similar items and optimizes space usage
        var sortedItems = new List<Item>();
        foreach (var group in groupedItems)
        {
            var sortedGroup = group
                .OrderByDescending(item => item.Definition!.Height) // Height first (better packing)
                .ThenByDescending(item => item.Definition!.Width)   // Then width
                .ThenBy(item => item.Definition!.Number)            // Then ID for consistency
                .ToList();
            
            sortedItems.AddRange(sortedGroup);
        }

        // Create a map to track which slots are occupied by which items
        var itemSlotMap = new Dictionary<Item, HashSet<byte>>();
        foreach (var item in inventoryItems)
        {
            var slots = this.GetItemSlots(item.ItemSlot, item.Definition!.Width, item.Definition.Height);
            itemSlotMap[item] = slots;
        }

        // Build a set of all currently used slots
        var allUsedSlots = new HashSet<byte>();
        foreach (var slots in itemSlotMap.Values)
        {
            foreach (var slot in slots)
            {
                allUsedSlots.Add(slot);
            }
        }

        // Plan the moves: find optimal slots for each sorted item
        var moveActions = new List<(Item Item, byte TargetSlot)>();
        var plannedUsedSlots = new HashSet<byte>();
        var itemsThatCantFit = new List<Item>();
        var inventoryExtensions = player.SelectedCharacter?.InventoryExtensions ?? 0;

        foreach (var item in sortedItems)
        {
            var currentSlots = itemSlotMap[item];
            
            // Find the first available slot that fits this item
            byte? targetSlot = this.FindOptimalSlot(
                player.Inventory,
                item,
                plannedUsedSlots,
                item.ItemSlot,
                inventoryExtensions);

            if (targetSlot.HasValue)
            {
                var targetSlots = this.GetItemSlots(targetSlot.Value, item.Definition!.Width, item.Definition.Height);
                
                // Mark target slots as planned
                foreach (var slot in targetSlots)
                {
                    plannedUsedSlots.Add(slot);
                }

                // Only add move if target is different from current
                if (targetSlot.Value != item.ItemSlot)
                {
                    moveActions.Add((item, targetSlot.Value));
                }
                else
                {
                    // Item stays in place, but mark its slots as used
                    foreach (var slot in currentSlots)
                    {
                        plannedUsedSlots.Add(slot);
                    }
                }
            }
            else
            {
                // Item can't be placed in sorted order - keep it in current position
                // Mark its current slots as used so other items don't try to move there
                foreach (var slot in currentSlots)
                {
                    plannedUsedSlots.Add(slot);
                }
                
                itemsThatCantFit.Add(item);
                
                // Log warning
                player.Logger.LogWarning(
                    "Item {Item} (Group: {Group}, Number: {Number}) cannot fit in sorted position, keeping at slot {Slot}",
                    item,
                    item.Definition!.Group,
                    item.Definition.Number,
                    item.ItemSlot);
            }
        }

        // Warn player if some items couldn't fit
        if (itemsThatCantFit.Count > 0)
        {
            await this.ShowMessageToAsync(
                player, 
                $"Note: {itemsThatCantFit.Count} item(s) could not fit in sorted order and remain in place.").ConfigureAwait(false);
        }

        if (moveActions.Count == 0)
        {
            await this.ShowMessageToAsync(player, "Inventory is already sorted.").ConfigureAwait(false);
            return;
        }

        // Execute moves - we need to be careful about order to avoid conflicts
        // Strategy: Move items that are going to currently empty slots first, then handle swaps
        var moveItemAction = new MoveItemAction();
        int movedCount = 0;
        int failedCount = 0;

        // Build a set of target slots to identify which moves go to empty slots
        var targetSlotsSet = new HashSet<byte>(moveActions.Select(m => m.TargetSlot));
        var currentItemSlots = new HashSet<byte>(inventoryItems.Select(i => i.ItemSlot));

        // Separate moves: those going to empty slots vs those that will swap
        var movesToEmptySlots = moveActions
            .Where(m => !currentItemSlots.Contains(m.TargetSlot) || m.Item.ItemSlot == m.TargetSlot)
            .OrderBy(m => m.TargetSlot)
            .ToList();

        var swapMoves = moveActions
            .Where(m => currentItemSlots.Contains(m.TargetSlot) && m.Item.ItemSlot != m.TargetSlot)
            .OrderBy(m => m.TargetSlot)
            .ToList();

        // First, move items to empty slots
        foreach (var (item, targetSlot) in movesToEmptySlots)
        {
            if (item.ItemSlot == targetSlot)
            {
                continue; // Already in place
            }

            try
            {
                await moveItemAction.MoveItemAsync(
                    player,
                    item.ItemSlot,
                    Storages.Inventory,
                    targetSlot,
                    Storages.Inventory).ConfigureAwait(false);
                movedCount++;
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Failed to move item {Item} from slot {FromSlot} to {ToSlot}", item, item.ItemSlot, targetSlot);
                failedCount++;
            }
        }

        // Then handle swaps (items moving to occupied slots - MoveItemAction should handle the swap)
        foreach (var (item, targetSlot) in swapMoves)
        {
            try
            {
                await moveItemAction.MoveItemAsync(
                    player,
                    item.ItemSlot,
                    Storages.Inventory,
                    targetSlot,
                    Storages.Inventory).ConfigureAwait(false);
                movedCount++;
            }
            catch (Exception ex)
            {
                player.Logger.LogWarning(ex, "Failed to move item {Item} from slot {FromSlot} to {ToSlot}", item, item.ItemSlot, targetSlot);
                failedCount++;
            }
        }

        if (movedCount > 0)
        {
            var message = failedCount > 0
                ? $"Inventory sorted! Moved {movedCount} item(s), {failedCount} failed."
                : $"Inventory sorted! Moved {movedCount} item(s).";
            await this.ShowMessageToAsync(player, message).ConfigureAwait(false);
        }
        else
        {
            await this.ShowMessageToAsync(player, "Failed to sort inventory. Some items may be blocking movement.").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets all slot numbers occupied by an item.
    /// </summary>
    private HashSet<byte> GetItemSlots(byte baseSlot, byte width, byte height)
    {
        var slots = new HashSet<byte>();
        var startSlot = baseSlot - InventoryConstants.EquippableSlotsCount;
        var startRow = startSlot / InventoryConstants.RowSize;
        var startColumn = startSlot % InventoryConstants.RowSize;

        for (byte r = 0; r < height; r++)
        {
            for (byte c = 0; c < width; c++)
            {
                var slot = (byte)(InventoryConstants.EquippableSlotsCount + ((startRow + r) * InventoryConstants.RowSize) + (startColumn + c));
                slots.Add(slot);
            }
        }

        return slots;
    }

    /// <summary>
    /// Finds the optimal slot for an item, considering its size and avoiding occupied slots.
    /// </summary>
    private byte? FindOptimalSlot(IStorage storage, Item item, HashSet<byte> usedSlots, byte currentSlot, int inventoryExtensions)
    {
        if (item.Definition is null)
        {
            return null;
        }

        var width = item.Definition.Width;
        var height = item.Definition.Height;

        // Get inventory size including extensions
        var totalRows = InventoryConstants.InventoryRows + (inventoryExtensions * InventoryConstants.RowsOfOneExtension);
        var startSlot = InventoryConstants.EquippableSlotsCount;
        var maxSlot = (byte)(startSlot + (totalRows * InventoryConstants.RowSize));

        // Try to find a slot that fits, starting from the top-left
        // Iterate row by row, column by column to ensure proper boundary checking
        for (int row = 0; row < totalRows; row++)
        {
            // Check if item would overflow total rows
            if (row + height > totalRows)
            {
                break; // No more rows can fit this item
            }

            for (int column = 0; column < InventoryConstants.RowSize; column++)
            {
                // Check if item would overflow row width
                if (column + width > InventoryConstants.RowSize)
                {
                    break; // Item won't fit in this row, move to next row
                }

                var slot = (byte)(startSlot + (row * InventoryConstants.RowSize) + column);
                
                if (usedSlots.Contains(slot))
                {
                    continue;
                }

                if (this.CanPlaceItemAtSlot(slot, width, height, usedSlots, maxSlot, totalRows))
                {
                    return slot;
                }
            }
        }

        // No valid slot found
        return null;
    }

    /// <summary>
    /// Checks if an item can be placed at a specific slot.
    /// </summary>
    private bool CanPlaceItemAtSlot(byte slot, byte width, byte height, HashSet<byte> usedSlots, byte maxSlot, int totalRows)
    {
        var baseSlot = slot - InventoryConstants.EquippableSlotsCount;
        var row = baseSlot / InventoryConstants.RowSize;
        var column = baseSlot % InventoryConstants.RowSize;

        // Check if item fits within column bounds (can't overflow row width)
        if (column + width > InventoryConstants.RowSize)
        {
            return false;
        }

        // Check if item fits within row bounds (can't overflow total rows)
        if (row + height > totalRows)
        {
            return false;
        }

        // Check if all required slots are available and within bounds
        for (byte r = 0; r < height; r++)
        {
            for (byte c = 0; c < width; c++)
            {
                var checkSlot = (byte)(InventoryConstants.EquippableSlotsCount + ((row + r) * InventoryConstants.RowSize) + (column + c));
                
                if (checkSlot >= maxSlot)
                {
                    return false;
                }

                if (usedSlots.Contains(checkSlot))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

