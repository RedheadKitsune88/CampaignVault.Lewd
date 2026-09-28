using CampaignVault.Plugins;

namespace GoblinPonyriders.Mechanics;

/// <summary>Ensures goblinClans / goblinHardLimits keys exist without overwriting player choices.</summary>
public sealed class GoblinCampaignOptionsUpgrader : IPluginCampaignOptionsUpgrader
{
    public string PluginId => GoblinKeys.PluginId;

    public bool TryUpgrade(IDictionary<string, string> systemOptions)
    {
        var changed = false;
        if (FindKey(systemOptions, GoblinKeys.ClansOption) is null)
        {
            systemOptions[GoblinKeys.ClansOption] = GoblinKeys.OptionOff;
            changed = true;
        }

        if (FindKey(systemOptions, GoblinKeys.HardLimitsOption) is null)
        {
            systemOptions[GoblinKeys.HardLimitsOption] = "";
            changed = true;
        }

        return changed;
    }

    private static string? FindKey(IDictionary<string, string> options, string key) =>
        options.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
}
