// <copyright file="RotatingMerchantState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Collections.Concurrent;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// State for the rotating merchant plugin.
/// </summary>
public class RotatingMerchantState : PeriodicTaskGameServerState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RotatingMerchantState"/> class.
    /// </summary>
    /// <param name="context">The game context.</param>
    public RotatingMerchantState(IGameContext context)
        : base(context)
    {
    }

    /// <summary>
    /// Gets or sets the next rotation time.
    /// </summary>
    public DateTime NextRotationUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the current rotation id per merchant.
    /// </summary>
    public ConcurrentDictionary<short, string> CurrentRotationIds { get; } = new();
}
