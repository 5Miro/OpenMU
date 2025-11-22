// <copyright file="SortInventoryChatCommandPlugInV2.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// Alternative implementation of the inventory sorting chat command.
/// This version removes all items first, then adds them back in sorted order.
/// This approach is simpler and avoids swap logic complexity.
/// </summary>
[Guid("B2C3D4E5-F6A7-5B8C-9D0E-1F2A3B4C5D6E")]
[PlugIn("Sort Inventory V2 chat command", "Sorts inventory by item type and ID (alternative implementation). Usage: /sortinv2")]
[ChatCommandHelp(Command, "Sorts inventory by item type, then by item ID, optimizing placement considering item sizes. This version removes all items first, then adds them back in sorted order.")]
public class SortInventoryChatCommandPlugInV2 : ChatCommandPlugInBase<EmptyChatCommandArgs>
{
    private const string Command = "/sortinv2";

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

        // Step 1: Store original positions and remove all items from inventory
        var itemsWithOriginalSlots = new List<(Item Item, byte OriginalSlot)>();
        foreach (var item in inventoryItems)
        {
            itemsWithOriginalSlots.Add((item, item.ItemSlot));
            await player.Inventory.RemoveItemAsync(item).ConfigureAwait(false);
        }

        // Step 2: Sort items by group, then by size and ID
        var sortedItems = this.SortItems(itemsWithOriginalSlots.Select(t => t.Item).ToList());

        // Step 3: Add items back in sorted order
        var inventoryExtensions = player.SelectedCharacter?.InventoryExtensions ?? 0;
        var usedSlots = new HashSet<byte>();
        var addedCount = 0;
        var failedCount = 0;
        var itemsThatCantFit = new List<Item>();

        foreach (var item in sortedItems)
        {
            // Find optimal slot for this item
            byte? targetSlot = this.FindOptimalSlot(
                player.Inventory,
                item,
                usedSlots,
                inventoryExtensions);

            if (targetSlot.HasValue)
            {
                // Try to add item at target slot
                if (await player.Inventory.AddItemAsync(targetSlot.Value, item).ConfigureAwait(false))
                {
                    // Mark slots as used
                    var itemSlots = this.GetItemSlots(targetSlot.Value, item.Definition!.Width, item.Definition.Height);
                    foreach (var slot in itemSlots)
                    {
                        usedSlots.Add(slot);
                    }

                    addedCount++;
                }
                else
                {
                    // Failed to add - this shouldn't happen if our logic is correct
                    player.Logger.LogWarning(
                        "Failed to add item {Item} (Group: {Group}, Number: {Number}) to slot {Slot}",
                        item,
                        item.Definition!.Group,
                        item.Definition.Number,
                        targetSlot.Value);
                    failedCount++;
                    itemsThatCantFit.Add(item);
                }
            }
            else
            {
                // Item can't fit - this shouldn't happen if inventory wasn't full before
                player.Logger.LogWarning(
                    "Item {Item} (Group: {Group}, Number: {Number}) cannot fit in sorted position",
                    item,
                    item.Definition!.Group,
                    item.Definition.Number);
                failedCount++;
                itemsThatCantFit.Add(item);
            }
        }

        // Step 4: Handle items that couldn't be added back
        if (itemsThatCantFit.Count > 0)
        {
            // Try to restore them to their original positions
            foreach (var item in itemsThatCantFit)
            {
                var originalSlot = itemsWithOriginalSlots.FirstOrDefault(t => t.Item == item).OriginalSlot;
                
                // Try original slot first
                if (await player.Inventory.AddItemAsync(originalSlot, item).ConfigureAwait(false))
                {
                    addedCount++;
                    failedCount--;
                    player.Logger.LogInformation(
                        "Restored item {Item} to original slot {Slot}",
                        item,
                        originalSlot);
                }
                else
                {
                    // Try to find any available slot
                    var fallbackSlot = this.FindOptimalSlot(
                        player.Inventory,
                        item,
                        usedSlots,
                        inventoryExtensions);
                    
                    if (fallbackSlot.HasValue && await player.Inventory.AddItemAsync(fallbackSlot.Value, item).ConfigureAwait(false))
                    {
                        addedCount++;
                        failedCount--;
                        var itemSlots = this.GetItemSlots(fallbackSlot.Value, item.Definition!.Width, item.Definition.Height);
                        foreach (var slot in itemSlots)
                        {
                            usedSlots.Add(slot);
                        }
                        
                        player.Logger.LogInformation(
                            "Restored item {Item} to fallback slot {Slot}",
                            item,
                            fallbackSlot.Value);
                    }
                    else
                    {
                        player.Logger.LogError(
                            "CRITICAL: Failed to restore item {Item} to inventory! Item may be lost!",
                            item);
                    }
                }
            }

            if (failedCount > 0)
            {
                await this.ShowMessageToAsync(
                    player,
                    $"Warning: {failedCount} item(s) could not be restored to inventory. Please check your inventory.").ConfigureAwait(false);
            }
        }

        // Step 5: Report results
        if (addedCount == inventoryItems.Count)
        {
            await this.ShowMessageToAsync(
                player,
                $"Inventory sorted! All {addedCount} item(s) reorganized.").ConfigureAwait(false);
        }
        else if (addedCount > 0)
        {
            await this.ShowMessageToAsync(
                player,
                $"Inventory sorted! {addedCount} item(s) reorganized, {failedCount} item(s) had issues.").ConfigureAwait(false);
        }
        else
        {
            await this.ShowMessageToAsync(
                player,
                "Failed to sort inventory. Items may have been restored to original positions.").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sorts items by group, then by size and ID.
    /// </summary>
    private List<Item> SortItems(List<Item> items)
    {
        // Step 1: Group items by their Group (type), then sort groups
        var groupedItems = items
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

        return sortedItems;
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
    private byte? FindOptimalSlot(IStorage storage, Item item, HashSet<byte> usedSlots, int inventoryExtensions)
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

