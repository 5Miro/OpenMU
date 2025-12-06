// <copyright file="RotatingMerchantConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// Configuration for rotating merchant plugin.
/// </summary>
public class RotatingMerchantConfiguration : PeriodicTaskConfiguration
{
    /// <summary>
    /// Gets or sets how often merchants rotate their wares.
    /// </summary>
    public TimeSpan RotationInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the configured merchants.
    /// </summary>
    public IList<MerchantConfig> Merchants { get; set; } = new List<MerchantConfig>();

    /// <summary>
    /// Gets or sets the current rotation IDs per merchant.
    /// This is persisted to survive server restarts.
    /// Key: MerchantId, Value: CurrentRotationId
    /// </summary>
    public IDictionary<short, string> CurrentRotationIds { get; set; } = new Dictionary<short, string>();
}

/// <summary>
/// Configuration for a single merchant.
/// </summary>
public class MerchantConfig
{
    /// <summary>
    /// Gets or sets the monster number of the merchant npc.
    /// </summary>
    public short MerchantId { get; set; }

    /// <summary>
    /// Gets or sets the item pools which define the rotation.
    /// </summary>
    public IList<ItemPool> ItemPools { get; set; } = new List<ItemPool>();
}

/// <summary>
/// A pool of items to select from.
/// </summary>
public class ItemPool
{
    /// <summary>
    /// Gets or sets the name of the pool.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how many items are selected from this pool each rotation.
    /// </summary>
    public int SelectionCount { get; set; }

    /// <summary>
    /// Gets or sets the items in this pool.
    /// </summary>
    public IList<ItemPoolEntry> Items { get; set; } = new List<ItemPoolEntry>();
}

/// <summary>
/// An entry in an item pool.
/// </summary>
public class ItemPoolEntry
{
    /// <summary>
    /// Gets or sets the item group.
    /// </summary>
    public byte ItemGroup { get; set; }

    /// <summary>
    /// Gets or sets the item number.
    /// </summary>
    public byte ItemNumber { get; set; }

    /// <summary>
    /// Gets or sets the item level.
    /// </summary>
    public byte Level { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item has skill.
    /// </summary>
    public bool HasSkill { get; set; }

    /// <summary>
    /// Gets or sets the option level.
    /// </summary>
    public byte OptionLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the item has luck.
    /// </summary>
    public bool Luck { get; set; }
}
