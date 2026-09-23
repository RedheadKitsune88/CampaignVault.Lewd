using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

[PluginWorldChange("lewd_vice")]
public sealed class LewdViceChange : WorldChange
{
    public string CharacterId { get; set; } = null!;

    /// <summary>consume | resist | note_presence | rest</summary>
    public string Action { get; set; } = "consume";

    public string? ViceId { get; set; }

    /// <summary>con | wis | cha. Required for complex vices when rolling.</summary>
    public string? Ability { get; set; }

    public int D20 { get; set; }

    /// <summary>Omit to read Con/Wis/Cha from Dnd5eExtension. Explicit 0 stays 0.</summary>
    public int? AbilityMod { get; set; }

    public bool InPresence { get; set; }

    /// <summary>Recorded in the message only. LLM emits item/item_use if a charge is spent.</summary>
    public string? ItemId { get; set; }
}
