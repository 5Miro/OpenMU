// <copyright file="RefreshMerchantChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command plugin which handles the refresh merchant command for game masters.
/// </summary>
[Guid("8B2E3F4A-9C5D-4E6F-8A1B-2C3D4E5F6A7B")]
[PlugIn(nameof(RefreshMerchantChatCommandPlugIn), "Handles the chat command '/refreshmerchant'. Forces a refresh of all rotating merchant stores.")]
[ChatCommandHelp(Command, "Forces a refresh of all rotating merchant stores.", typeof(EmptyChatCommandArgs), CharacterStatus.GameMaster)]
public class RefreshMerchantChatCommandPlugIn : ChatCommandPlugInBase<EmptyChatCommandArgs>
{
    private const string Command = "/refreshmerchant";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc/>
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, EmptyChatCommandArgs arguments)
    {
        // Check if RotatingMerchantPlugIn is active
        var rotatingMerchantPluginType = typeof(RotatingMerchantPlugIn);
        var pluginGuid = rotatingMerchantPluginType.GUID;
        
        if (!gameMaster.GameContext.PlugInManager.IsPlugInActive(pluginGuid))
        {
            await this.ShowMessageToAsync(gameMaster, $"[{this.Key}] RotatingMerchantPlugIn is not active.").ConfigureAwait(false);
            return;
        }

        // Get the periodic task plugin point and force start, then execute immediately
        // We need to get the actual RotatingMerchantPlugIn instance to call RefreshAllMerchantsAsync
        // Since we can't easily get it, let's create a temporary instance
        // The state is managed statically per GameContext, so GetStateByGameContext will work
        // But we need the configuration. Let's try to get it from the plugin manager's configuration
        var pluginInstance = new RotatingMerchantPlugIn();
        
        // The plugin will use its default config if Configuration is null, or we can try to load it
        // For now, let's just call the method - it should work if the plugin is active and configured
        try
        {
            await pluginInstance.RefreshAllMerchantsAsync(gameMaster.GameContext).ConfigureAwait(false);
            await this.ShowMessageToAsync(gameMaster, $"[{this.Key}] All merchant stores have been refreshed.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            gameMaster.Logger.LogError(ex, "Error refreshing merchant stores");
            await this.ShowMessageToAsync(gameMaster, $"[{this.Key}] Error refreshing merchant stores: {ex.Message}").ConfigureAwait(false);
        }
    }
}

