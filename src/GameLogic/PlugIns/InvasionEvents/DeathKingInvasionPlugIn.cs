// <copyright file="DeathKingInvasionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// This plugin enables Death King Invasion feature.
/// </summary>
[PlugIn(nameof(DeathKingInvasionPlugIn), "Handle Death King Invasion event")]
[Guid("A1F68F50-E010-499A-91B5-2C6E43B0E7DA")]
public class DeathKingInvasionPlugIn : BaseInvasionPlugIn<PeriodicInvasionConfiguration>, ISupportDefaultCustomConfiguration
{
    private const ushort DeathKingId = 55;
    private const ushort DeathBoneId = 56;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeathKingInvasionPlugIn"/> class.
    /// </summary>
    public DeathKingInvasionPlugIn()
        : base(null, null, [
                new(DeathKingId, 2),
                new(DeathBoneId, 20),
            ])
    {
    }

    /// <summary>
    /// Gets possible maps for the event. Overridden to only allow Lorencia.
    /// </summary>
    protected override ushort[] PossibleMaps { get; } = { LorenciaId };

    /// <inheritdoc />
    public object CreateDefaultConfig() => PeriodicInvasionConfiguration.DefaultDeathKingInvasion;
}

