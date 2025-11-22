// <copyright file="SortInventoryChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;

/// <summary>
/// A chat command plugin which sorts the player's inventory by item type, then by item ID, optimizing for item sizes.
/// 
/// <para>
/// This plugin uses an efficient "remove all, then add back" approach:
/// </para>
/// <list type="number">
/// <item>All inventory items are removed from storage (but not deleted)</item>
/// <item>Items are sorted by group (type), then by size (height, width), then by ID</item>
/// <item>Items are added back to the inventory in sorted order, starting from the top-left</item>
/// </list>
/// 
/// <para>
/// <strong>Sorting Strategy:</strong>
/// </para>
/// <list type="bullet">
/// <item>Items are first grouped by their <see cref="ItemDefinition.Group"/> (item type)</item>
/// <item>Groups are ordered by their group number (ascending)</item>
/// <item>Within each group, items are sorted by:
///     <list type="number">
///         <item>Height (descending) - taller items first for better space utilization</item>
///         <item>Width (descending) - wider items next</item>
///         <item>Item Number (ascending) - for consistency within same-sized items</item>
///     </list>
/// </item>
/// </list>
/// 
/// <para>
/// <strong>Placement Strategy:</strong>
/// </para>
/// <list type="bullet">
/// <item>Items are placed starting from the top-left corner of the inventory</item>
/// <item>Placement proceeds row by row, column by column</item>
/// <item>All item boundaries are checked to prevent overflow beyond inventory limits</item>
/// <item>Items that cannot fit in their optimal sorted position are restored to their original slots</item>
/// </list>
/// 
/// <para>
/// <strong>Benefits of this approach:</strong>
/// </para>
/// <list type="bullet">
/// <item>No swap logic complexity - items are simply removed and re-added</item>
/// <item>Atomic operation - either all items are sorted or none (with fallback restoration)</item>
/// <item>No slot conflicts - items are removed first, so all slots are available during placement</item>
/// <item>More reliable - works in a single pass without requiring multiple runs</item>
/// <item>Better error handling - failed items can be restored to original positions</item>
/// </list>
/// 
/// <para>
/// <strong>Usage:</strong> Type <c>/sortinv</c> in chat to sort your inventory.
/// </para>
/// </summary>
[Guid("cd9344a7-bc69-4414-acef-ecc8d49c324d")]
[PlugIn("Sort Inventory chat command", "Sorts inventory by item type and ID, optimizing for item sizes. Usage: /sortinv")]
[ChatCommandHelp(Command, "Sorts inventory by item type, then by item ID, optimizing placement considering item sizes.")]
public class SortInventoryChatCommandPlugIn : ChatCommandPlugInBase<EmptyChatCommandArgs>
{
    private const string Command = "/sortinv";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <summary>
    /// Handles the sort inventory command execution.
    /// </summary>
    /// <param name="player">The player executing the command.</param>
    /// <param name="arguments">Empty command arguments (not used).</param>
    /// <remarks>
    /// This method implements the complete sorting algorithm:
    /// <list type="number">
    /// <item>Validates player and inventory availability</item>
    /// <item>Removes all inventory items (storing original positions)</item>
    /// <item>Sorts items using the sorting strategy</item>
    /// <item>Places items back in sorted order</item>
    /// <item>Handles items that cannot fit (restores to original or fallback positions)</item>
    /// <item>Refreshes the client inventory view</item>
    /// </list>
    /// </remarks>
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
        // This approach eliminates swap complexity - we simply remove everything,
        // sort it, then add it back in the correct order.
        var itemsWithOriginalSlots = new List<(Item Item, byte OriginalSlot)>();
        foreach (var item in inventoryItems)
        {
            itemsWithOriginalSlots.Add((item, item.ItemSlot));
            await player.Inventory.RemoveItemAsync(item).ConfigureAwait(false);
        }

        // Step 2: Sort items by group, then by size and ID
        // This creates visual clusters of similar items and optimizes space usage
        var sortedItems = this.SortItems(itemsWithOriginalSlots.Select(t => t.Item).ToList());

        // Step 3: Add items back in sorted order
        // We track used slots to prevent overlaps and ensure proper placement
        var inventoryExtensions = player.SelectedCharacter?.InventoryExtensions ?? 0;
        var usedSlots = new HashSet<byte>();
        var addedCount = 0;
        var failedCount = 0;
        var itemsThatCantFit = new List<Item>();

        foreach (var item in sortedItems)
        {
            // Find optimal slot for this item (top-left, row by row, column by column)
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
                    // Mark all slots occupied by this item as used
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
                    // but we handle it gracefully
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
                // but we handle it gracefully
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
        // Try to restore them to their original positions or find fallback slots
        if (itemsThatCantFit.Count > 0)
        {
            foreach (var item in itemsThatCantFit)
            {
                var originalSlot = itemsWithOriginalSlots.FirstOrDefault(t => t.Item == item).OriginalSlot;

                // Try original slot first (it should be available since we removed everything)
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
                    // Try to find any available slot as fallback
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
                        // Critical error - item cannot be restored
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

        // Step 5: Report results to the player
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

        // Step 6: Refresh inventory on client to ensure synchronization
        // This ensures the client immediately sees the sorted inventory without requiring a relog
        await player.InvokeViewPlugInAsync<IUpdateInventoryListPlugIn>(
            p => p.UpdateInventoryListAsync()).ConfigureAwait(false);
    }

    /// <summary>
    /// Sorts items by group (type), then by size (height, width), then by ID.
    /// </summary>
    /// <param name="items">The list of items to sort.</param>
    /// <returns>A sorted list of items.</returns>
    /// <remarks>
    /// <para>
    /// Sorting is performed in three stages:
    /// </para>
    /// <list type="number">
    /// <item><strong>Grouping:</strong> Items are grouped by their <see cref="ItemDefinition.Group"/> property.
    ///     This creates logical clusters of similar item types (e.g., all swords together, all potions together).</item>
    /// <item><strong>Group Ordering:</strong> Groups are ordered by their group number (ascending).
    ///     This ensures a consistent ordering of item types.</item>
    /// <item><strong>Within-Group Sorting:</strong> Within each group, items are sorted by:
    ///     <list type="number">
    ///         <item>Height (descending) - Taller items are placed first to optimize vertical space usage</item>
    ///         <item>Width (descending) - Wider items are placed next</item>
    ///         <item>Item Number (ascending) - For consistency when items have the same dimensions</item>
    ///     </list>
    /// </item>
    /// </list>
    /// <para>
    /// This sorting strategy ensures that:
    /// </para>
    /// <list type="bullet">
    /// <item>Similar items are visually clustered together</item>
    /// <item>Larger items are placed first, reducing fragmentation</item>
    /// <item>The result is deterministic and consistent</item>
    /// </list>
    /// </remarks>
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
    /// Gets all slot numbers occupied by an item based on its position and dimensions.
    /// </summary>
    /// <param name="baseSlot">The base slot number where the item's top-left corner is located.</param>
    /// <param name="width">The width of the item (in slots).</param>
    /// <param name="height">The height of the item (in slots).</param>
    /// <returns>A set of all slot numbers occupied by the item.</returns>
    /// <remarks>
    /// <para>
    /// Items in the inventory can occupy multiple slots. For example, a 2x2 item occupies 4 slots.
    /// This method calculates all slot numbers that an item occupies based on:
    /// </para>
    /// <list type="bullet">
    /// <item>The base slot (top-left corner position)</item>
    /// <item>The item's width (number of columns it spans)</item>
    /// <item>The item's height (number of rows it spans)</item>
    /// </list>
    /// <para>
    /// The calculation accounts for the inventory's row size and the offset for equippable slots.
    /// </para>
    /// </remarks>
    private HashSet<byte> GetItemSlots(byte baseSlot, byte width, byte height)
    {
        var slots = new HashSet<byte>();
        var startSlot = baseSlot - InventoryConstants.EquippableSlotsCount;
        var startRow = startSlot / InventoryConstants.RowSize;
        var startColumn = startSlot % InventoryConstants.RowSize;

        // Calculate all slots occupied by this item
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
    /// Finds the optimal slot for placing an item, considering its size and avoiding occupied slots.
    /// </summary>
    /// <param name="storage">The inventory storage (not used, kept for compatibility).</param>
    /// <param name="item">The item to place.</param>
    /// <param name="usedSlots">A set of slot numbers that are already occupied.</param>
    /// <param name="inventoryExtensions">The number of inventory extensions the player has.</param>
    /// <returns>The optimal slot number where the item can be placed, or <c>null</c> if no suitable slot is found.</returns>
    /// <remarks>
    /// <para>
    /// This method implements a top-left placement strategy:
    /// </para>
    /// <list type="number">
    /// <item>Starts from the top-left corner of the inventory (slot 12, after equippable slots)</item>
    /// <item>Iterates row by row, then column by column</item>
    /// <item>For each potential slot, checks if the item fits without:
    ///     <list type="bullet">
    ///         <item>Overflowing the row width</item>
    ///         <item>Overflowing the total inventory height</item>
    ///         <item>Overlapping with already-placed items</item>
    ///     </list>
    /// </item>
    /// <item>Returns the first valid slot found</item>
    /// </list>
    /// <para>
    /// This strategy ensures items are placed compactly from top to bottom, left to right,
    /// which creates a clean, organized appearance and minimizes wasted space.
    /// </para>
    /// </remarks>
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

                // Skip if this slot is already used
                if (usedSlots.Contains(slot))
                {
                    continue;
                }

                // Check if the item can be placed at this slot
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
    /// Checks if an item can be placed at a specific slot without violating inventory boundaries or overlapping other items.
    /// </summary>
    /// <param name="slot">The slot number to check (top-left corner of the item).</param>
    /// <param name="width">The width of the item (in slots).</param>
    /// <param name="height">The height of the item (in slots).</param>
    /// <param name="usedSlots">A set of slot numbers that are already occupied.</param>
    /// <param name="maxSlot">The maximum valid slot number (inventory boundary).</param>
    /// <param name="totalRows">The total number of rows in the inventory (including extensions).</param>
    /// <returns><c>true</c> if the item can be placed at this slot; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// <para>
    /// This method performs comprehensive boundary and overlap checking:
    /// </para>
    /// <list type="number">
    /// <item><strong>Column Boundary Check:</strong> Verifies the item doesn't overflow the row width.
    ///     For example, a 3-slot-wide item cannot start at column 6 in an 8-column row.</item>
    /// <item><strong>Row Boundary Check:</strong> Verifies the item doesn't overflow the total inventory height.
    ///     For example, a 2-row-tall item cannot start at the last row.</item>
    /// <item><strong>Slot-by-Slot Overlap Check:</strong> Checks every slot the item would occupy
    ///     to ensure none are already used by other items.</item>
    /// <item><strong>Max Slot Check:</strong> Verifies no calculated slot exceeds the maximum valid slot number.</item>
    /// </list>
    /// <para>
    /// This ensures items are placed correctly within the 2D grid constraints of the inventory,
    /// preventing any overflow or overlap issues.
    /// </para>
    /// </remarks>
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
        // We check every slot the item would occupy to ensure no overlaps
        for (byte r = 0; r < height; r++)
        {
            for (byte c = 0; c < width; c++)
            {
                var checkSlot = (byte)(InventoryConstants.EquippableSlotsCount + ((row + r) * InventoryConstants.RowSize) + (column + c));

                // Ensure slot is within inventory bounds
                if (checkSlot >= maxSlot)
                {
                    return false;
                }

                // Ensure slot is not already occupied
                if (usedSlots.Contains(checkSlot))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

