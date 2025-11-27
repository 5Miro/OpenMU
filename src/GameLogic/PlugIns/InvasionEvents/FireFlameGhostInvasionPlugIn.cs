// <copyright file="FireFlameGhostInvasionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// This plugin enables Fire Flame Ghost Invasion feature.
/// </summary>
[PlugIn(nameof(FireFlameGhostInvasionPlugIn), "Handle Fire Flame Ghost invasion event")]
[Guid("24BB68B9-3014-4CC2-BDB5-53A9AE74D10A")]
public class FireFlameGhostInvasionPlugIn : BaseInvasionPlugIn<PeriodicInvasionConfiguration>, ISupportDefaultCustomConfiguration
{
    private const ushort FireFlameGhostId = 463;

    /// <summary>
    /// Noria.
    /// </summary>
    protected const ushort ElbelandId = 51;

    /// <summary>
    /// Initializes a new instance of the <see cref="FireFlameGhostInvasionPlugIn"/> class.
    /// </summary>
    public FireFlameGhostInvasionPlugIn()
        : base(null,
            [
                new(FireFlameGhostId, 10, MapId: DeviasId),
                new(FireFlameGhostId, 10, MapId: ElbelandId),
            ],
            null)
    {
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => PeriodicInvasionConfiguration.DefaultFireFlameGhostInvasion;
}