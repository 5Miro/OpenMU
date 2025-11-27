// <copyright file="LunarRabbitInvasionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// This plugin enables Lunar Rabbit Invasion feature.
/// </summary>
[PlugIn(nameof(LunarRabbitInvasionPlugIn), "Handle Lunar Rabbit invasion event")]
[Guid("10DFE40B-ACE6-4763-A430-511901CB857E")]
public class LunarRabbitInvasionPlugIn : BaseInvasionPlugIn<PeriodicInvasionConfiguration>, ISupportDefaultCustomConfiguration
{
    private const ushort LunarRabbitId = 413;

    /// <summary>
    /// Noria.
    /// </summary>
    protected const ushort ElbelandId = 51;

    /// <summary>
    /// Initializes a new instance of the <see cref="LunarRabbitInvasionPlugIn"/> class.
    /// </summary>
    public LunarRabbitInvasionPlugIn()
        : base(null,
            [
                new(LunarRabbitId, 10, MapId: DeviasId),
                new(LunarRabbitId, 10, MapId: ElbelandId),
            ],
            null)
    {
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => PeriodicInvasionConfiguration.DefaultLunarRabbitInvasion;
}