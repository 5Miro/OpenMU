// <copyright file="ResetExperienceRateEntry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

/// <summary>
/// An entry in the reset experience rate table, defining the experience rate for a specific reset count range.
/// </summary>
public class ResetExperienceRateEntry
{
    /// <summary>
    /// Gets or sets the minimum reset count (inclusive) for this experience rate entry.
    /// </summary>
    public int MinimumResetCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum reset count (inclusive) for this experience rate entry.
    /// </summary>
    public int MaximumResetCount { get; set; }

    /// <summary>
    /// Gets or sets the experience rate multiplier for this reset count range.
    /// </summary>
    public float ExperienceRate { get; set; }
}

