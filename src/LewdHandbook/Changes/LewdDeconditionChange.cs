using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

[PluginWorldChange("lewd_decondition")]
public sealed class LewdDeconditionChange : WorldChange
{
    public string TargetId { get; set; } = null!;

    public string? Category { get; set; }

    /// <summary>therapy | aftercare | rest</summary>
    public string Method { get; set; } = "therapy";

    public int D20 { get; set; }

    /// <summary>Second die when aftercare or a devoted partner grants advantage.</summary>
    public int? D20Other { get; set; }

    /// <summary>Omit to read Wisdom from Dnd5eExtension. Explicit 0 stays 0.</summary>
    public int? WisMod { get; set; }
}
