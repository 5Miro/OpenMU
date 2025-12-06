// <copyright file="RotatingMerchantPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.PlugIns;

    /// <summary>
    /// Periodically rotates configured merchants' wares and enforces per-account purchase limits.
    /// </summary>
    [PlugIn(nameof(RotatingMerchantPlugIn), "Rotates merchant wares and enforces per-account one-purchase-per-item-per-rotation limits.")]
    [Guid("7A1E1C09-89BA-4E8C-9CB7-C8578F0D9873")]
    public class RotatingMerchantPlugIn
        : PeriodicTaskBasePlugIn<RotatingMerchantConfiguration, RotatingMerchantState>,
          IMerchantPurchaseValidatorPlugIn,
          IItemBoughtFromMerchantPlugIn,
          ISupportDefaultCustomConfiguration
    {
        /// <inheritdoc />
        public object CreateDefaultConfig()
        {
            return new RotatingMerchantConfiguration
            {
                RotationInterval = TimeSpan.FromHours(24),
                Merchants =
                {
                    new MerchantConfig
                    {
                        MerchantId = 253, // Example: Potion Girl Amy (adjust as needed)
                        ItemPools =
                        {
                            new ItemPool
                            {
                                Name = "Potions",
                                SelectionCount = 3,
                                Items =
                                {
                                    new ItemPoolEntry { ItemGroup = 14, ItemNumber = 0, Level = 0 }, // HP Potion
                                    new ItemPoolEntry { ItemGroup = 14, ItemNumber = 1, Level = 0 }, // MP Potion
                                    new ItemPoolEntry { ItemGroup = 14, ItemNumber = 2, Level = 0 }, // HP/MP Potion
                                },
                            },
                        },
                    },
                },
            };
        }

    /// <inheritdoc />
    protected override RotatingMerchantState CreateState(IGameContext gameContext)
    {
        var state = new RotatingMerchantState(gameContext)
        {
            NextRotationUtc = DateTime.UtcNow + (this.Configuration?.RotationInterval ?? TimeSpan.FromHours(24)),
        };

        // Load persisted rotation IDs from plugin configuration
        this.LoadPersistedRotationIds(state);

        return state;
    }

    private void LoadPersistedRotationIds(RotatingMerchantState state)
    {
        var config = this.Configuration;
        if (config?.CurrentRotationIds is null)
        {
            return;
        }

        foreach (var kvp in config.CurrentRotationIds)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                state.CurrentRotationIds.TryAdd(kvp.Key, kvp.Value);
            }
        }
    }

    /// <inheritdoc />
    protected override bool IsItTimeToStart(IGameContext gameContext)
    {
        var state = this.GetStateByGameContext(gameContext);
        return DateTime.UtcNow >= state.NextRotationUtc;
    }

    /// <inheritdoc />
    protected override ValueTask OnPrepareEventAsync(RotatingMerchantState state)
    {
        // No special pre-rotation work needed.
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask OnPreparedAsync(RotatingMerchantState state)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override async ValueTask OnStartedAsync(RotatingMerchantState state)
    {
        await this.RefreshAllMerchantsAsync(state.Context).ConfigureAwait(false);
    }

    /// <summary>
    /// Manually refreshes all configured merchants. Can be called by game masters via chat command.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>A task that completes when all merchants have been refreshed.</returns>
    public async ValueTask RefreshAllMerchantsAsync(IGameContext gameContext)
    {
        var state = this.GetStateByGameContext(gameContext);
        var config = this.Configuration;
        
        // If configuration is null, try to use default configuration
        if (config is null && this is ISupportDefaultCustomConfiguration defaultConfigSupporter)
        {
            var logger = gameContext.LoggerFactory.CreateLogger(this.GetType().Name);
            logger.LogWarning("Configuration is not set. Using default configuration for manual refresh.");
            this.Configuration = config = defaultConfigSupporter.CreateDefaultConfig() as RotatingMerchantConfiguration;
        }
        
        if (config is null)
        {
            return;
        }

        foreach (var merchantConfig in config.Merchants)
        {
            await this.RotateMerchantAsync(state, merchantConfig).ConfigureAwait(false);
        }

        // Schedule next rotation:
        state.NextRotationUtc = DateTime.UtcNow + config.RotationInterval;
    }

    /// <inheritdoc />
    protected override ValueTask OnFinishedAsync(RotatingMerchantState state)
    {
        // Nothing special to do after rotation.
        return ValueTask.CompletedTask;
    }

    private async ValueTask RotateMerchantAsync(RotatingMerchantState state, MerchantConfig merchantConfig)
    {
        var gameConfig = state.Context.Configuration;
        var merchantDefinition = gameConfig.Monsters.FirstOrDefault(m => m.Number == merchantConfig.MerchantId);

        if (merchantDefinition?.MerchantStore is null)
        {
            return;
        }

        var context = state.Context.PersistenceContextProvider.CreateNewContext(gameConfig);
        try
        {
            context.Attach(merchantDefinition.MerchantStore);

            // Clear existing store items by removing them one by one
            // (CollectionAdapter.Clear() doesn't work properly with Reset action)
            var itemsToRemove = merchantDefinition.MerchantStore.Items.ToList();
            foreach (var item in itemsToRemove)
            {
                merchantDefinition.MerchantStore.Items.Remove(item);
            }

            byte slot = 0;
            foreach (var pool in merchantConfig.ItemPools)
            {
                var created = this.SelectRandomItemsFromPool(context, gameConfig, pool, ref slot);
                foreach (var item in created)
                {
                    merchantDefinition.MerchantStore.Items.Add(item);
                }
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }
        finally
        {
            context.Dispose();
        }

        var rotationId = DateTime.UtcNow.Ticks.ToString();
        state.CurrentRotationIds.AddOrUpdate(merchantConfig.MerchantId, rotationId, (_, _) => rotationId);

        // Persist the rotation ID to plugin configuration
        await this.PersistRotationIdAsync(state, merchantConfig.MerchantId, rotationId).ConfigureAwait(false);

        // Notify players with this merchant open
        await this.NotifyPlayersOfRotationAsync(state, merchantConfig.MerchantId).ConfigureAwait(false);
    }

    private async ValueTask PersistRotationIdAsync(RotatingMerchantState state, short merchantId, string rotationId)
    {
        var config = this.Configuration;
        if (config is null)
        {
            return;
        }

        // Update the configuration
        if (config.CurrentRotationIds is null)
        {
            config.CurrentRotationIds = new Dictionary<short, string>();
        }

        config.CurrentRotationIds[merchantId] = rotationId;

        // Save the configuration to the database
        var gameConfig = state.Context.Configuration;
        var context = state.Context.PersistenceContextProvider.CreateNewContext(gameConfig);
        try
        {
            context.Attach(gameConfig);

            // Find the plugin configuration
            var pluginConfig = gameConfig.PlugInConfigurations.FirstOrDefault(p => p.TypeId == this.GetType().GUID);
            if (pluginConfig is null)
            {
                return;
            }

            // Get the reference handler and save the configuration
            var referenceHandler = state.Context.PersistenceContextProvider.GetType()
                .GetProperty("ReferenceHandler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(state.Context.PersistenceContextProvider) as ReferenceHandler;

            if (referenceHandler is null)
            {
                // Fallback: try to create a default reference handler
                var loggerFactory = state.Context.LoggerFactory;
                var dataSource = new GameConfigurationDataSource(
                    loggerFactory.CreateLogger<GameConfigurationDataSource>(),
                    state.Context.PersistenceContextProvider);
                referenceHandler = new ByDataSourceReferenceHandler(dataSource);
            }

            pluginConfig.SetConfiguration(config, referenceHandler);

            await context.SaveChangesAsync().ConfigureAwait(false);
        }
        finally
        {
            context.Dispose();
        }
    }

    private IList<Item> SelectRandomItemsFromPool(
        IContext context,
        GameConfiguration gameConfig,
        ItemPool pool,
        ref byte nextSlot)
    {
        var result = new List<Item>();

        var candidates = pool.Items.ToList();
        if (candidates.Count == 0 || pool.SelectionCount <= 0)
        {
            return result;
        }

        for (var i = 0; i < pool.SelectionCount && candidates.Count > 0; i++)
        {
            var index = Rand.NextInt(0, candidates.Count);
            var entry = candidates[index];
            candidates.RemoveAt(index);

            var def = gameConfig.Items.FirstOrDefault(d => d.Group == entry.ItemGroup && d.Number == entry.ItemNumber);
            if (def is null)
            {
                continue;
            }

            var item = context.CreateNew<Item>();
            item.Definition = def;
            item.Level = entry.Level;
            item.HasSkill = entry.HasSkill && def.Skill is not null;
            item.ItemSlot = nextSlot++;
            item.Durability = def.Durability;

            // Add option level if specified
            if (entry.OptionLevel > 0 && def.PossibleItemOptions.Any())
            {
                var optionLink = context.CreateNew<ItemOptionLink>();
                var option = def.PossibleItemOptions
                    .SelectMany(o => o.PossibleOptions)
                    .FirstOrDefault(o => o.OptionType == DataModel.Configuration.Items.ItemOptionTypes.Option);
                if (option is not null)
                {
                    optionLink.ItemOption = option;
                    optionLink.Level = entry.OptionLevel;
                    item.ItemOptions.Add(optionLink);
                }
            }

            // Add luck if specified
            if (entry.Luck && def.PossibleItemOptions.Any())
            {
                var luckOption = def.PossibleItemOptions
                    .SelectMany(o => o.PossibleOptions)
                    .FirstOrDefault(o => o.OptionType == DataModel.Configuration.Items.ItemOptionTypes.Luck);
                if (luckOption is not null)
                {
                    var luckLink = context.CreateNew<ItemOptionLink>();
                    luckLink.ItemOption = luckOption;
                    item.ItemOptions.Add(luckLink);
                }
            }

            result.Add(item);
        }

        return result;
    }

    private async ValueTask NotifyPlayersOfRotationAsync(RotatingMerchantState state, short merchantId)
    {
        await state.Context.ForEachPlayerAsync(async player =>
        {
            if (player.OpenedNpc?.Definition?.Number == merchantId && player.OpenedNpc.Definition.MerchantStore is not null)
            {
                await player.InvokeViewPlugInAsync<IShowMerchantStoreItemListPlugIn>(
                    p => p.ShowMerchantStoreItemListAsync(
                        player.OpenedNpc.Definition.MerchantStore.Items,
                        StoreKind.Normal)).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    // ------- Purchase limit implementation (per-account) --------

    /// <inheritdoc />
    public void CanBuyItem(Player player, NonPlayerCharacter merchant, Item storeItem, MerchantPurchaseCancelEventArgs eventArgs)
    {
        if (player.Account is null)
        {
            return; // Allowed
        }

        var config = this.Configuration;
        if (config is null)
        {
            return; // Allowed
        }

        // Only handle merchants which are configured for rotation
        var merchantConfig = config.Merchants.FirstOrDefault(m => m.MerchantId == merchant.Definition.Number);
        if (merchantConfig is null)
        {
            return; // Allowed
        }

        var state = this.GetStateByGameContext(player.GameContext);
        if (!state.CurrentRotationIds.TryGetValue(merchant.Definition.Number, out var rotationId))
        {
            // No rotation id in memory -> try to load from plugin configuration
            rotationId = this.GetPersistedRotationId(merchant.Definition.Number);
            if (string.IsNullOrEmpty(rotationId))
            {
                // No persisted rotation ID either -> treat as unrestricted for now
                // This should only happen if the merchant was just configured or items were manually added
                return; // Allowed
            }

            // Load into memory for faster access
            state.CurrentRotationIds.TryAdd(merchant.Definition.Number, rotationId);
        }

        var itemKey = this.GenerateItemKey(storeItem);
        var account = player.Account;
        var purchases = account.MerchantPurchases ?? Array.Empty<AccountMerchantPurchase>();

        var alreadyBought = purchases.Any(p =>
            p.MerchantId == merchant.Definition.Number &&
            p.RotationId == rotationId &&
            p.ItemKey == itemKey);

        if (alreadyBought)
        {
            eventArgs.Cancel = true;
            eventArgs.ErrorMessage = "You have already purchased this item in the current rotation.";
        }
    }

    /// <inheritdoc />
    public void ItemBought(Player player, Item item, Item sourceItem, NonPlayerCharacter merchant)
    {
        if (player.Account is null)
        {
            return;
        }

        var config = this.Configuration;
        if (config is null || config.Merchants.All(m => m.MerchantId != merchant.Definition.Number))
        {
            return;
        }

        var state = this.GetStateByGameContext(player.GameContext);
        if (!state.CurrentRotationIds.TryGetValue(merchant.Definition.Number, out var rotationId))
        {
            return;
        }

        // Calculate the price that was paid for this item
        var priceCalculator = new ItemPriceCalculator();
        var amountPaid = priceCalculator.CalculateFinalBuyingPrice(sourceItem);

        var account = player.Account;
        var context = player.PersistenceContext;

        var purchase = context.CreateNew<AccountMerchantPurchase>();
        purchase.Account = account; // Set navigation property
        purchase.MerchantId = merchant.Definition.Number;
        purchase.RotationId = rotationId;
        purchase.ItemKey = this.GenerateItemKey(sourceItem);
        purchase.PurchaseDateUtc = DateTime.UtcNow;
        purchase.AmountPaid = amountPaid;

        account.MerchantPurchases.Add(purchase);
        // Persisted with normal account/character save flow.
    }

    private string GenerateItemKey(Item item)
    {
        var def = item.Definition;
        if (def is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append(def.Group).Append('_').Append(def.Number).Append('_').Append(item.Level);

        if (item.ItemOptions is { Count: > 0 })
        {
            var optionParts = item.ItemOptions
                .Select(o => $"{o.ItemOption?.Number}_{o.ItemOption?.OptionType?.Name}_{o.Level}")
                .OrderBy(s => s, StringComparer.Ordinal);

            sb.Append('_').Append(string.Join('|', optionParts));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the rotation ID for a merchant from plugin configuration.
    /// </summary>
    /// <param name="merchantId">The merchant ID.</param>
    /// <returns>The rotation ID, or null if not found.</returns>
    private string? GetPersistedRotationId(short merchantId)
    {
        var config = this.Configuration;
        if (config?.CurrentRotationIds is null)
        {
            return null;
        }

        return config.CurrentRotationIds.TryGetValue(merchantId, out var rotationId) ? rotationId : null;
    }
}