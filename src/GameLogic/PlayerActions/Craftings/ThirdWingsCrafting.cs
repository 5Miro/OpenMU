// <copyright file="ThirdWingsCrafting.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Craftings;

using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Crafting for Third Wings.
/// </summary>
public class ThirdWingsCrafting : SimpleItemCraftingHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ThirdWingsCrafting"/> class.
    /// </summary>
    /// <param name="settings">The settings.</param>
    public ThirdWingsCrafting(SimpleCraftingSettings settings)
        : base(settings)
    {
    }

    /// <inheritdoc/>
    protected override void AddRandomItemOption(Item resultItem, Player player, byte successRate)
    {
        if (resultItem.Definition!.PossibleItemOptions.Where(o => o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.Option))
                is { } options && options.Any())
        {
            (int chance1, int level) = Rand.NextInt(0, 4) switch
            {
                0 => (0, 0),
                1 => (12, 1),
                2 => (6, 2),
                _ => (3, 3),
            }; // From 400 created wings about 0+12+6+3=21 (~5%) will have item option

            if (Rand.NextRandomBool(chance1))
            {
                var link = player.PersistenceContext.CreateNew<ItemOptionLink>();
                link.Level = level;
                (int chance2, int type) = Rand.NextRandomBool()
                    ? (40, 1)
                    : (30, 2);

                IncreasableItemOption selectedOption;
                if (Rand.NextRandomBool(chance2))
                {
                    selectedOption = options.ElementAt(type).PossibleOptions.First();  // Additional dmg (phys, wiz, curse) or defense
                }
                else
                {
                    selectedOption = options.ElementAt(0).PossibleOptions.First(); // HP recovery %
                }

                // Set ItemOptionId first to establish the foreign key relationship
                var optionId = selectedOption.GetId();
                var linkType = link.GetType();
                var itemOptionIdProperty = linkType.GetProperty("ItemOptionId");
                if (itemOptionIdProperty != null)
                {
                    itemOptionIdProperty.SetValue(link, optionId);
                }
                
                // Set ItemOption navigation property for serialization (AccountContext ignores config types, so this won't be tracked)
                link.ItemOption = selectedOption;
                resultItem.ItemOptions.Add(link);
            }
        }
    }

    /// <inheritdoc/>
    protected override void AddRandomExcellentOptions(Item resultItem, Player player)
    {
        if (resultItem.Definition!.PossibleItemOptions.FirstOrDefault(o => o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.Wing))
                is { } wingOption)
        {
            (int chance, int type) = Rand.NextInt(0, 4) switch
            {
                0 => (4, 0),   // Ignore def
                1 => (2, 1),   // 5% full reflect
                2 => (7, 2),   // 5% HP restore
                _ => (7, 3),   // 5% mana restore
            };  // From 400 created wings about 4+2+7+7=20 (5%) will have wing "exc" option

            if (Rand.NextRandomBool(chance))
            {
                var link = player.PersistenceContext.CreateNew<ItemOptionLink>();
                var selectedWingOption = wingOption.PossibleOptions.ElementAt(type);
                // Set ItemOptionId first to establish the foreign key relationship
                var optionId = selectedWingOption.GetId();
                var linkType = link.GetType();
                var itemOptionIdProperty = linkType.GetProperty("ItemOptionId");
                if (itemOptionIdProperty != null)
                {
                    itemOptionIdProperty.SetValue(link, optionId);
                }
                
                // Set ItemOption navigation property for serialization (AccountContext ignores config types, so this won't be tracked)
                link.ItemOption = selectedWingOption;
                resultItem.ItemOptions.Add(link);
            }
        }
    }
}