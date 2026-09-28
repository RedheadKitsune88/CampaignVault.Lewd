using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

[PluginWorldChange("lewd_bad_end", ModeId = LewdEncounterMode.ModeIdValue)]
public sealed class LewdBadEndChange : WorldChange
{
    public string TargetId { get; set; } = null!;

    /// <summary>defeat | explicit. Engine-owned reasons are rejected.</summary>
    public string Reason { get; set; } = "explicit";

    public bool NoEscape { get; set; }

    public string? Consequence { get; set; }

    public string? ImprintTrack { get; set; }

    public int? ImprintJump { get; set; }

    public bool ImprintWilling { get; set; }

    public string? ViceId { get; set; }

    /// <summary>Only when non-consent does not allow a permanent bad end for this target (a fade-to-black rescue): what happens to them, in the DM's words.</summary>
    public string? Outcome { get; set; }

    /// <summary>Rescue only: they are still bound afterwards (a captive, not a rescued victim). Default false.</summary>
    public bool KeepBindings { get; set; }
}
