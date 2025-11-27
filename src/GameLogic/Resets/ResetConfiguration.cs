// <copyright file="ResetConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using System.Linq;

/// <summary>
/// Configuration of the Reset System.
/// </summary>
public class ResetConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether dynamic experience rate based on reset count is enabled.
    /// When enabled, the experience rate will be determined by the player's reset count using the <see cref="ExperienceRateTable"/>.
    /// </summary>
    public bool EnableDynamicExperienceRate { get; set; }

    /// <summary>
    /// Gets or sets the experience rate table entries, which define experience rates for specific reset count ranges.
    /// </summary>
    public IList<ResetExperienceRateEntry> ExperienceRateTable { get; set; } = new List<ResetExperienceRateEntry>();
    /// <summary>
    /// Gets or sets the reset limit, which is the maximum amount of possible resets.
    /// </summary>
    public int? ResetLimit { get; set; }

    /// <summary>
    /// Gets or sets the required level for a reset.
    /// </summary>
    public int RequiredLevel { get; set; } = 400;

    /// <summary>
    /// Gets or sets the character level after a reset.
    /// </summary>
    public int LevelAfterReset { get; set; } = 10;

    /// <summary>
    /// Gets or sets the required money for a reset.
    /// </summary>
    public int RequiredMoney { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether the required money should
    /// be multiplied with the current reset count.
    /// </summary>
    public bool MultiplyRequiredMoneyByResetCount { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a reset sets the stat points back to the initial values.
    /// </summary>
    public bool ResetStats { get; set; } = true;

    /// <summary>
    /// Gets or sets the amount of points which will be set at the <see cref="Character.LevelUpPoints"/> when doing a reset.
    /// </summary>
    public int PointsPerReset { get; set; } = 1500;

    /// <summary>
    /// Gets or sets a value indicating whether the <see cref="PointsPerReset"/> should be multiplied with the current reset count.
    /// </summary>
    public bool MultiplyPointsByResetCount { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a reset will replace (true) or add (false) the <see cref="Character.LevelUpPoints"/>.
    /// </summary>
    public bool ReplacePointsPerReset { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a reset moves the player home.
    /// </summary>
    public bool MoveHome { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a reset logs the player out back to character selection.
    /// </summary>
    public bool LogOut { get; set; } = true;

    /// <summary>
    /// Gets the experience rate for the given reset count.
    /// If dynamic experience rate is enabled and a matching entry is found, returns that rate.
    /// Otherwise, returns null to indicate the global experience rate should be used.
    /// </summary>
    /// <param name="resetCount">The reset count of the player.</param>
    /// <returns>The experience rate for the reset count, or null if the global rate should be used.</returns>
    public float? GetExperienceRateForResetCount(int resetCount)
    {
        if (!this.EnableDynamicExperienceRate || this.ExperienceRateTable == null || this.ExperienceRateTable.Count == 0)
        {
            return null;
        }

        var entry = this.ExperienceRateTable.FirstOrDefault(
            e => resetCount >= e.MinimumResetCount && resetCount <= e.MaximumResetCount);

        return entry?.ExperienceRate;
    }
}