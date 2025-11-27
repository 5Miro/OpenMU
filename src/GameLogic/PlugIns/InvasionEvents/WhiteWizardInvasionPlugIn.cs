// <copyright file="WhiteWizardInvasionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;

using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// This plugin enables White Wizard Invasion feature.
/// </summary>
[PlugIn(nameof(WhiteWizardInvasionPlugIn), "Handle White Wizard Invasion event")]
[Guid("9BB69242-BADE-4D97-B6EB-6292980D5D8B")]
public class WhiteWizardInvasionPlugIn : BaseInvasionPlugIn<PeriodicInvasionConfiguration>, ISupportDefaultCustomConfiguration
{
    private const ushort WhiteWizardId = 135;
    private const ushort DestructiveOgreSolderId = 136;
    private const ushort DestructiveOgreArcherId = 137;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhiteWizardInvasionPlugIn"/> class.
    /// </summary>
    public WhiteWizardInvasionPlugIn()
        : base(
            null,
            [
                new(WhiteWizardId, 1, MapId: LorenciaId, 148, 47, 164, 65),
                new(DestructiveOgreSolderId, 10, MapId: LorenciaId, 148, 47, 164, 65),
                new(DestructiveOgreArcherId, 10, MapId: LorenciaId, 148, 47, 164, 65),
                new(WhiteWizardId, 1, MapId: NoriaId, 194, 70, 204, 86),
                new(DestructiveOgreSolderId, 10, MapId: NoriaId, 194, 70, 204, 86),
                new(DestructiveOgreArcherId, 10, MapId: NoriaId, 194, 70, 204, 86),
            ],
            null)
    {
    }

    /// <inheritdoc />
    public object CreateDefaultConfig() => PeriodicInvasionConfiguration.DefaultWhiteWizardInvasion;
}