// <copyright file="AccountMerchantPurchase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Records that an account bought a specific merchant item in a specific rotation.
/// </summary>
public class AccountMerchantPurchase
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the account which made this purchase.
    /// </summary>
    public virtual Account? Account { get; set; }

    /// <summary>
    /// Gets or sets the merchant npc number.
    /// </summary>
    public short MerchantId { get; set; }

    /// <summary>
    /// Gets or sets the item key (definition + level + options hash).
    /// </summary>
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rotation identifier.
    /// </summary>
    public string RotationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the purchase date (UTC).
    /// </summary>
    public DateTime PurchaseDateUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the amount of currency paid for this purchase.
    /// </summary>
    public long AmountPaid { get; set; }
}
