// <copyright file="IMerchantPurchaseValidatorPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.ComponentModel;
using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Plugin point which can veto a merchant purchase per player/account.
/// </summary>
[PlugInPoint("Merchant purchase validator", "Validates if a player can buy a merchant item.")]
[Guid("A1F0F4AA-7E77-4B43-9E8E-8E3B7D7F7399")]
public interface IMerchantPurchaseValidatorPlugIn
{
    /// <summary>
    /// Validates if the player is allowed to buy the given item from the given merchant.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="merchant">The merchant npc.</param>
    /// <param name="storeItem">The item in the merchant store (source item).</param>
    /// <param name="eventArgs">The event args which can be used to cancel the purchase and set an error message.</param>
    void CanBuyItem(Player player, NonPlayerCharacter merchant, Item storeItem, MerchantPurchaseCancelEventArgs eventArgs);
}

/// <summary>
/// Event args for the <see cref="IMerchantPurchaseValidatorPlugIn"/>.
/// </summary>
/// <seealso cref="System.ComponentModel.CancelEventArgs" />
public class MerchantPurchaseCancelEventArgs : CancelEventArgs
{
    /// <summary>
    /// Gets or sets the error message to display to the player if the purchase is cancelled.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
